using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SdvKb.Validation;

namespace SdvKb.Build
{
    public sealed class ModPackageResult
    {
        public bool Ok { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ArchiveRootFolder { get; set; } = string.Empty;
        public string OutputZip { get; set; } = string.Empty;
        public long ZipSizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public List<string> PackagedFiles { get; set; } = new List<string>();
        public List<ValidationIssue> Warnings { get; set; } = new List<ValidationIssue>();
    }

    /// <summary>
    /// Validates, compiles, and packages a Stardew Valley mod into a clean Nexus / CurseForge / ModDrop
    /// standard `.zip` archive with a single top-level mod folder and zero source/temp/config artifacts.
    /// </summary>
    public static class ModPackager
    {
        private static readonly JsonDocumentOptions JsoncOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // `.fx` / `.fxh` / `.hlsl` 为着色器源码，打包发布时仅保留编译产物 `.mgfxo`
            ".cs", ".csproj", ".sln", ".user", ".pdb", ".suo", ".rsp", ".tmp", ".bak", ".orig", ".log", ".fx", ".fxh", ".hlsl", ".hlsli"
        };

        private static readonly HashSet<string> ExcludedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "config.json",
            "sdv-project.json",
            ".gitignore",
            ".gitattributes",
            ".editorconfig",
            "Thumbs.db",
            ".DS_Store"
        };

        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", ".idea", ".vscode", "releases", "dist", "node_modules", "__pycache__"
        };

        public static ModPackageResult PackageMod(
            string modDir,
            string gamePath,
            string? explicitOutZip = null,
            bool skipStaticCheck = false)
        {
            string fullModDir = Path.GetFullPath(modDir);
            if (!Directory.Exists(fullModDir))
                throw new DirectoryNotFoundException($"Mod directory `{fullModDir}` does not exist.");

            string manifestPath = Path.Combine(fullModDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Missing `manifest.json` in `{fullModDir}`.");

            // 1. Build C# mod first so EntryDll exists before static check & packaging!
            var buildRes = DotNet6ModCompiler.BuildAndOptionallyDeploy(fullModDir, gamePath, deploy: false);
            if (!buildRes.Success)
            {
                throw new InvalidOperationException($"Mod build failed before packaging:\n{buildRes.CompilerOutput}");
            }

            // 2. Run full 1.6 semantic + Harmony + TMX static validation
            var issues = skipStaticCheck
                ? new List<ValidationIssue>()
                : ModValidator.ValidateModDirectory(fullModDir, gamePath);

            var errors = issues.Where(i => i.Severity == "ERROR").ToList();
            if (errors.Count > 0)
            {
                string errText = string.Join("\n", errors.Select(e => $"[{e.Severity}] {e.File}: {e.Message}"));
                throw new InvalidOperationException($"Refusing to package mod with {errors.Count} static validation error(s):\n{errText}");
            }

            var warnings = issues.Where(i => i.Severity != "ERROR").ToList();

            string modName;
            string version;
            string uniqueId;
            using (var doc = JsonDocument.Parse(File.ReadAllText(manifestPath), JsoncOpts))
            {
                var root = doc.RootElement;
                modName = GetStringProp(root, "Name") ?? Path.GetFileName(fullModDir);
                version = GetStringProp(root, "Version") ?? "1.0.0";
                uniqueId = GetStringProp(root, "UniqueID") ?? modName;

                string? minApi = GetStringProp(root, "MinimumApiVersion");
                if (string.IsNullOrWhiteSpace(minApi))
                {
                    warnings.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = "manifest.json",
                        Message = "Release check: Consider setting `\"MinimumApiVersion\": \"4.0.0\"` for Stardew Valley 1.6."
                    });
                }

                if (!TryGetUpdateKeys(root, out var updateKeys) || updateKeys.Count == 0)
                {
                    warnings.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = "manifest.json",
                        Message = "Release check: `UpdateKeys` is empty/missing (add e.g. `[\"Nexus:12345\"]` before uploading to Nexus so SMAPI notifies players of updates)."
                    });
                }
                else if (updateKeys.Any(k => k.Contains("?") || k.EndsWith(":0", StringComparison.Ordinal)))
                {
                    warnings.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = "manifest.json",
                        Message = $"Release check: `UpdateKeys` contains placeholder value `{string.Join(", ", updateKeys)}`."
                    });
                }
            }

            string cleanFolder = SanitizeFileName(modName);
            if (string.IsNullOrWhiteSpace(cleanFolder))
                cleanFolder = SanitizeFileName(uniqueId);

            string outZipPath;
            if (!string.IsNullOrWhiteSpace(explicitOutZip))
            {
                outZipPath = Path.GetFullPath(explicitOutZip!);
                if (Directory.Exists(outZipPath))
                    outZipPath = Path.Combine(outZipPath, $"{cleanFolder} {version}.zip");
            }
            else
            {
                string releasesDir = Path.Combine(fullModDir, "releases");
                Directory.CreateDirectory(releasesDir);
                outZipPath = Path.Combine(releasesDir, $"{cleanFolder} {version}.zip");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outZipPath)!);
            if (File.Exists(outZipPath))
                File.Delete(outZipPath);

            var filesToPack = new List<(string DiskPath, string ZipEntryPath)>();
            CollectPackableFiles(fullModDir, fullModDir, cleanFolder, outZipPath, filesToPack);

            using (var fs = new FileStream(outZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var item in filesToPack.OrderBy(f => f.ZipEntryPath, StringComparer.OrdinalIgnoreCase))
                {
                    archive.CreateEntryFromFile(item.DiskPath, item.ZipEntryPath, CompressionLevel.Optimal);
                }
            }

            var zipInfo = new FileInfo(outZipPath);
            string sha256 = ComputeSha256Hex(outZipPath);

            return new ModPackageResult
            {
                Ok = true,
                UniqueId = uniqueId,
                ModName = modName,
                Version = version,
                ArchiveRootFolder = cleanFolder,
                OutputZip = outZipPath,
                ZipSizeBytes = zipInfo.Length,
                Sha256 = sha256,
                FileCount = filesToPack.Count,
                PackagedFiles = filesToPack.Select(f => f.ZipEntryPath).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = warnings
            };
        }

        private static void CollectPackableFiles(
            string modRoot,
            string currentDir,
            string archiveRootName,
            string outputZipFullPath,
            List<(string DiskPath, string ZipEntryPath)> dest)
        {
            foreach (var file in Directory.GetFiles(currentDir))
            {
                if (Path.GetFullPath(file).Equals(outputZipFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(file);
                string ext = Path.GetExtension(file);
                if (fileName.IndexOf(".old_locked_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ExcludedFileNames.Contains(fileName) ||
                    ExcludedExtensions.Contains(ext))
                {
                    continue;
                }

                // Exclude README / markdown docs at root unless inside assets
                if (currentDir.Equals(modRoot, StringComparison.OrdinalIgnoreCase) && ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                    continue;

                string rel = file.Substring(modRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                dest.Add((file, $"{archiveRootName}/{rel}"));
            }

            foreach (var sub in Directory.GetDirectories(currentDir))
            {
                string dirName = Path.GetFileName(sub);
                if (dirName.StartsWith(".", StringComparison.Ordinal) || ExcludedDirectories.Contains(dirName))
                    continue;

                CollectPackableFiles(modRoot, sub, archiveRootName, outputZipFullPath, dest);
            }
        }

        private static string SanitizeFileName(string raw)
        {
            string cleaned = Regex.Replace(raw.Trim(), @"[<>:""/\\|?*]+", "").Trim();
            return Regex.Replace(cleaned, @"\s+", " ");
        }

        private static string ComputeSha256Hex(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string? GetStringProp(JsonElement root, string name)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                    return p.Value.GetString();
            }
            return null;
        }

        private static bool TryGetUpdateKeys(JsonElement root, out List<string> keys)
        {
            keys = new List<string>();
            foreach (var p in root.EnumerateObject())
            {
                if (p.Name.Equals("UpdateKeys", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in p.Value.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                            keys.Add(el.GetString()!);
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
