using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using SdvKb.Project;
using SdvKb.Validation;

namespace SdvKb.Build
{
    public sealed class ModBuildResult
    {
        public bool Success { get; set; }
        public bool IsContentPack { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public string OutputDllPath { get; set; } = string.Empty;
        public string DeployedPath { get; set; } = string.Empty;
        public string CompilerOutput { get; set; } = string.Empty;
    }

    /// <summary>
    /// Built-in in-memory Roslyn C# compiler (`Microsoft.CodeAnalysis.CSharp`) paired with `BepInEx.AssemblyPublicizer`.
    /// Compiles SMAPI .NET 6.0 C# mods and dynamic `eval` snippets directly in memory against Stardew Valley's
    /// bundled .NET 6.0 BCL &amp; game assemblies — 100% free of Visual Studio, MSBuild, csc.exe, or .NET SDK on the target machine!
    /// </summary>
    public static class DotNet6ModCompiler
    {
        private static readonly JsonDocumentOptions JsoncOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly HashSet<string> ExcludedFacadeOrNativeDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Data.dll",
            "System.Drawing.dll",
            "System.Net.dll",
            "System.Numerics.dll",
            "System.Security.dll",
            "System.ServiceModel.Web.dll",
            "System.ServiceProcess.dll",
            "System.Transactions.dll",
            "System.Web.dll",
            "System.Windows.dll",
            "System.Xml.dll",
            "System.Xml.Linq.dll",
            "System.Xml.Serialization.dll",
            "WindowsBase.dll",
            "Microsoft.VisualBasic.dll",
            "Microsoft.VisualBasic.Core.dll",
            "SDL2.dll",
            "soft_oal.dll",
            "steam_api64.dll",
            "libSkiaSharp.dll",
            "liblwjgl_lz4.dll",
            "Galaxy64.dll",
            "GalaxyCSharpGlue.dll",
            "coreclr.dll",
            "clrjit.dll",
            "clretwrc.dll",
            "dbgshim.dll",
            "hostfxr.dll",
            "hostpolicy.dll",
            "mscordaccore.dll",
            "mscordbi.dll",
            "mscorrc.dll",
            "msquic.dll",
            "ucrtbase.dll"
        };

        private const string IgnoresAccessChecksSource = @"
using System.Runtime.CompilerServices;
[assembly: IgnoresAccessChecksTo(""Stardew Valley"")]
[assembly: IgnoresAccessChecksTo(""StardewValley.GameData"")]
[assembly: IgnoresAccessChecksTo(""xTile"")]

namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : System.Attribute
    {
        public string AssemblyName { get; }
        public IgnoresAccessChecksToAttribute(string assemblyName) => AssemblyName = assemblyName;
    }
}
";

        private sealed class CachedMemoryRefs
        {
            public string GamePath { get; set; } = string.Empty;
            public long DirTicksUtc { get; set; }
            public int RootDllCount { get; set; }
            public bool Publicized { get; set; }
            public Dictionary<string, MetadataReference> BySimpleName { get; set; } =
                new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly object RefCacheLock = new object();
        private static CachedMemoryRefs? _memRefCacheNormal;
        private static CachedMemoryRefs? _memRefCachePublicized;
        private static readonly Dictionary<string, (DateTime WriteUtc, MetadataReference Ref)> ExtraModRefCache =
            new Dictionary<string, (DateTime, MetadataReference)>(StringComparer.OrdinalIgnoreCase);

        public static ModBuildResult BuildAndOptionallyDeploy(
            string modDir,
            string gamePath,
            bool deploy,
            string? targetModsRootOverride = null,
            bool publicize = true)
        {
            var res = new ModBuildResult();
            string fullModDir = Path.GetFullPath(modDir);
            string manifestPath = Path.Combine(fullModDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"manifest.json not found in `{fullModDir}`.");

            string? entryDll = null;
            using (var doc = JsonDocument.Parse(File.ReadAllText(manifestPath), JsoncOpts))
            {
                var root = doc.RootElement;
                res.ModName = GetProp(root, "Name") ?? Path.GetFileName(fullModDir);
                res.UniqueId = GetProp(root, "UniqueID") ?? res.ModName;
                entryDll = GetProp(root, "EntryDll");
                if (string.IsNullOrWhiteSpace(entryDll))
                    res.IsContentPack = true;
            }

            string effectiveModsRoot = string.IsNullOrWhiteSpace(targetModsRootOverride)
                ? Path.Combine(gamePath, "Mods")
                : Path.GetFullPath(targetModsRootOverride!);

            if (res.IsContentPack)
            {
                res.Success = true;
                if (deploy)
                {
                    res.DeployedPath = DeployModFolderToModsRoot(fullModDir, effectiveModsRoot, res.UniqueId);
                }
                return res;
            }

            var csprojOpts = ParseOptionalCsprojSettings(fullModDir);

            var csFiles = Directory.GetFiles(fullModDir, "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string rel = f.Substring(fullModDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                        rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    return !IsExcludedByCsprojRemove(rel, csprojOpts.RemovedCompilePatterns);
                })
                .ToList();

            if (csFiles.Count == 0)
                throw new InvalidOperationException($"No .cs source files found in `{fullModDir}` for EntryDll `{entryDll}`.");

            string outDll = Path.Combine(fullModDir, entryDll!);
            string outPdb = Path.ChangeExtension(outDll, ".pdb");
            string stagingDll = outDll + ".building.dll";
            string stagingPdb = outPdb + ".building.pdb";

            var extraRefPaths = new List<string>(FindLocalModDllReferences(fullModDir, outDll));
            try
            {
                var depRes = ModDependencyResolver.ResolveProjectDependencies(fullModDir, gamePath, allowGitHubDownload: true);
                foreach (var depDll in depRes.CompileReferenceDlls)
                {
                    if (!depDll.Equals(outDll, StringComparison.OrdinalIgnoreCase) &&
                        !extraRefPaths.Contains(depDll, StringComparer.OrdinalIgnoreCase))
                    {
                        extraRefPaths.Add(depDll);
                    }
                }
            }
            catch
            {
            }

            try
            {
                var (ok, output) = CompileSourcesToFiles(
                    gamePath,
                    csFiles,
                    stagingDll,
                    stagingPdb,
                    includeLoadedMods: false,
                    extraReferences: extraRefPaths,
                    publicize: publicize,
                    outXmlDocPath: null,
                    csprojSettings: csprojOpts);

                if (!ok)
                {
                    res.Success = false;
                    res.CompilerOutput = output;
                    return res;
                }

                // Immediately verify [HarmonyPatch] targets, overload ambiguity, & parameter injections on the newly compiled DLL
                // BEFORE replacing the existing DLL on disk or deploying to Mods/!
                var harmonyIssues = CSharpModAnalyzer.VerifyHarmonyPatchesInDll(stagingDll, gamePath, entryDll);
                var harmonyErrors = harmonyIssues.Where(i => i.Severity == "ERROR").ToList();
                var diagSb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(output))
                    diagSb.AppendLine(output.TrimEnd());
                foreach (var hi in harmonyIssues)
                    diagSb.AppendLine($"[{hi.Severity}] {hi.File}: {hi.Message}");

                if (harmonyErrors.Count > 0)
                {
                    res.Success = false;
                    res.CompilerOutput = diagSb.ToString().TrimEnd();
                    return res;
                }

                File.Move(stagingDll, outDll, overwrite: true);
                if (File.Exists(stagingPdb))
                    File.Move(stagingPdb, outPdb, overwrite: true);

                res.Success = true;
                res.CompilerOutput = diagSb.ToString().TrimEnd();
                res.OutputDllPath = outDll;
                if (deploy)
                {
                    res.DeployedPath = DeployModFolderToModsRoot(fullModDir, effectiveModsRoot, res.UniqueId);
                }
                return res;
            }
            finally
            {
                try { if (File.Exists(stagingDll)) File.Delete(stagingDll); } catch { }
                try { if (File.Exists(stagingPdb)) File.Delete(stagingPdb); } catch { }
            }
        }

        private sealed class CsprojCompilerSettings
        {
            public HashSet<string> DefineConstants { get; } = new HashSet<string>(StringComparer.Ordinal);
            public List<string> RemovedCompilePatterns { get; } = new List<string>();
            public bool AllowUnsafe { get; set; } = true;
            public bool ImplicitUsings { get; set; }
            public List<string> ExtraGlobalUsingLines { get; } = new List<string>();
        }

        private static CsprojCompilerSettings ParseOptionalCsprojSettings(string modDir)
        {
            var settings = new CsprojCompilerSettings { AllowUnsafe = true };
            settings.DefineConstants.Add("TRACE");

            try
            {
                var csprojFile = Directory.GetFiles(modDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (csprojFile == null)
                    return settings;

                var xdoc = XDocument.Load(csprojFile);
                foreach (var dc in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("DefineConstants", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var sym in (dc.Value ?? "").Split(new[] { ';', ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!sym.StartsWith("$(", StringComparison.Ordinal))
                            settings.DefineConstants.Add(sym.Trim());
                    }
                }

                foreach (var au in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("AllowUnsafeBlocks", StringComparison.OrdinalIgnoreCase)))
                {
                    if (bool.TryParse(au.Value?.Trim(), out bool allow))
                        settings.AllowUnsafe = allow;
                }

                foreach (var iu in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("ImplicitUsings", StringComparison.OrdinalIgnoreCase)))
                {
                    string v = (iu.Value ?? "").Trim();
                    if (v.Equals("enable", StringComparison.OrdinalIgnoreCase) || v.Equals("true", StringComparison.OrdinalIgnoreCase))
                        settings.ImplicitUsings = true;
                    else if (v.Equals("disable", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase))
                        settings.ImplicitUsings = false;
                }

                foreach (var u in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("Using", StringComparison.OrdinalIgnoreCase)))
                {
                    string? inc = u.Attribute("Include")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(inc))
                        continue;
                    bool isStatic = string.Equals(u.Attribute("Static")?.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                    string? alias = u.Attribute("Alias")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(alias))
                        settings.ExtraGlobalUsingLines.Add($"global using {alias} = global::{inc};");
                    else if (isStatic)
                        settings.ExtraGlobalUsingLines.Add($"global using static global::{inc};");
                    else
                        settings.ExtraGlobalUsingLines.Add($"global using global::{inc};");
                }

                foreach (var comp in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("Compile", StringComparison.OrdinalIgnoreCase)))
                {
                    string? rem = comp.Attribute("Remove")?.Value;
                    if (!string.IsNullOrWhiteSpace(rem))
                    {
                        foreach (var pat in rem!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string clean = pat.Trim().Replace('\\', '/').TrimStart('/');
                            if (!string.IsNullOrEmpty(clean))
                                settings.RemovedCompilePatterns.Add(clean);
                        }
                    }
                }
            }
            catch { }

            return settings;
        }

        private static bool IsExcludedByCsprojRemove(string relSlashPath, List<string> patterns)
        {
            foreach (var pat in patterns)
            {
                if (pat.EndsWith("/**", StringComparison.Ordinal) || pat.EndsWith("/*", StringComparison.Ordinal))
                {
                    string prefix = pat.Substring(0, pat.IndexOf("/*", StringComparison.Ordinal) + 1);
                    if (relSlashPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (pat.Contains('*'))
                {
                    string regexPat = "^" + Regex.Escape(pat).Replace("\\*\\*", ".*").Replace("\\*", "[^/]*") + "$";
                    if (Regex.IsMatch(relSlashPath, regexPat, RegexOptions.IgnoreCase))
                        return true;
                }
                else if (relSlashPath.Equals(pat, StringComparison.OrdinalIgnoreCase) ||
                         relSlashPath.StartsWith(pat.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static (bool Success, string Output) CompileSourcesToFiles(
            string gamePath,
            IEnumerable<string> sourceFiles,
            string outDllPath,
            string? outPdbPath,
            bool includeLoadedMods,
            IEnumerable<string>? extraReferences,
            bool publicize = true,
            string? outXmlDocPath = null,
            object? csprojSettings = null)
        {
            var cfg = csprojSettings as CsprojCompilerSettings;
            var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
            if (cfg != null && cfg.DefineConstants.Count > 0)
            {
                parseOpts = parseOpts.WithPreprocessorSymbols(cfg.DefineConstants);
            }
            var syntaxTrees = new List<SyntaxTree>();
            bool hasTargetFrameworkAttr = false;
            bool hasIgnoresAccessChecks = false;

            foreach (var csFile in sourceFiles)
            {
                string srcText = File.ReadAllText(csFile, Encoding.UTF8);
                if (srcText.Contains("TargetFrameworkAttribute") || srcText.Contains("TargetFramework("))
                    hasTargetFrameworkAttr = true;
                if (srcText.Contains("IgnoresAccessChecksToAttribute"))
                    hasIgnoresAccessChecks = true;

                syntaxTrees.Add(CSharpSyntaxTree.ParseText(srcText, parseOpts, path: csFile, encoding: Encoding.UTF8));
            }

            if (cfg != null && (cfg.ImplicitUsings || cfg.ExtraGlobalUsingLines.Count > 0))
            {
                var guSb = new StringBuilder();
                if (cfg.ImplicitUsings)
                {
                    guSb.AppendLine("global using global::System;");
                    guSb.AppendLine("global using global::System.Collections.Generic;");
                    guSb.AppendLine("global using global::System.IO;");
                    guSb.AppendLine("global using global::System.Linq;");
                    guSb.AppendLine("global using global::System.Net.Http;");
                    guSb.AppendLine("global using global::System.Threading;");
                    guSb.AppendLine("global using global::System.Threading.Tasks;");
                }
                foreach (var extraU in cfg.ExtraGlobalUsingLines)
                    guSb.AppendLine(extraU);

                syntaxTrees.Add(CSharpSyntaxTree.ParseText(guSb.ToString(), parseOpts, path: "_ImplicitGlobalUsings.g.cs", encoding: Encoding.UTF8));
            }

            if (!hasTargetFrameworkAttr)
            {
                const string tfmSource = "using System.Runtime.Versioning;\n[assembly: TargetFramework(\".NETCoreApp,Version=v6.0\", FrameworkDisplayName = \".NET 6.0\")]\n";
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(tfmSource, parseOpts, path: "_TargetFramework.g.cs", encoding: Encoding.UTF8));
            }

            if (publicize && !hasIgnoresAccessChecks)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(IgnoresAccessChecksSource, parseOpts, path: "_IgnoresAccessChecks.g.cs", encoding: Encoding.UTF8));
            }

            var references = CollectReferencesInMemory(gamePath, includeLoadedMods, extraReferences, publicize);
            string asmName = Path.GetFileNameWithoutExtension(outDllPath);

            var (ok, peBytes, pdbBytes, xmlBytes, diagText) = EmitCompilationInMemory(
                asmName,
                syntaxTrees,
                references,
                emitPdb: !string.IsNullOrEmpty(outPdbPath),
                emitXmlDocs: !string.IsNullOrEmpty(outXmlDocPath),
                allowUnsafe: cfg?.AllowUnsafe ?? true);

            if (!ok || peBytes == null)
                return (false, diagText);

            string? outDir = Path.GetDirectoryName(outDllPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllBytes(outDllPath, peBytes);
            if (!string.IsNullOrEmpty(outPdbPath) && pdbBytes != null && pdbBytes.Length > 0)
                File.WriteAllBytes(outPdbPath!, pdbBytes);
            if (!string.IsNullOrEmpty(outXmlDocPath) && xmlBytes != null && xmlBytes.Length > 0)
                File.WriteAllBytes(outXmlDocPath!, xmlBytes);

            return (true, diagText);
        }

        public static (bool Success, byte[]? AssemblyBytes, string CompilerErrors, string PreparedSource) CompileEvalSnippetToBytes(
            string code,
            string gamePath,
            string? activeModsRootOverride = null)
        {
            string prepared = WrapEvalCode(code);
            var parseOpts = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
            var trees = new List<SyntaxTree>
            {
                CSharpSyntaxTree.ParseText(prepared, parseOpts, path: "EvalSnippet.cs", encoding: Encoding.UTF8),
                CSharpSyntaxTree.ParseText(IgnoresAccessChecksSource, parseOpts, path: "_IgnoresAccessChecks.g.cs", encoding: Encoding.UTF8)
            };

            var extraModRefs = new List<string>();
            bool hasExplicitModsRoot = !string.IsNullOrWhiteSpace(activeModsRootOverride) && Directory.Exists(activeModsRootOverride);
            if (hasExplicitModsRoot)
            {
                CollectModEntryDllsFromRoot(activeModsRootOverride!, extraModRefs);
            }

            // Only scan default `<GamePath>\Mods` when no explicit/isolated activeModsRootOverride was supplied!
            var refs = CollectReferencesInMemory(gamePath, includeLoadedMods: !hasExplicitModsRoot, extraModRefs, publicize: true);
            string uniqueAsmName = "SdvEvalDynamic_" + Guid.NewGuid().ToString("N");
            var (ok, peBytes, _, _, diagText) = EmitCompilationInMemory(uniqueAsmName, trees, refs, emitPdb: false, emitXmlDocs: false);

            if (!ok || peBytes == null)
                return (false, null, diagText, prepared);

            return (true, peBytes, string.Empty, prepared);
        }

        private static (bool Success, byte[]? PeBytes, byte[]? PdbBytes, byte[]? XmlBytes, string DiagnosticsText) EmitCompilationInMemory(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            bool emitPdb,
            bool emitXmlDocs,
            bool allowUnsafe = true)
        {
            var compOpts = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: allowUnsafe,
                deterministic: true,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                specificDiagnosticOptions: new[]
                {
                    new KeyValuePair<string, ReportDiagnostic>("CS1701", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS8032", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS1591", ReportDiagnostic.Suppress)
                });

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                compOpts);

            using var peStream = new MemoryStream();
            using var pdbStream = emitPdb ? new MemoryStream() : null;
            using var xmlStream = emitXmlDocs ? new MemoryStream() : null;
            var emitOpts = emitPdb
                ? new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb)
                : null;

            EmitResult emitResult = compilation.Emit(
                peStream,
                pdbStream: pdbStream,
                xmlDocumentationStream: xmlStream,
                options: emitOpts);

            var sb = new StringBuilder();
            foreach (var d in emitResult.Diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Error || (!emitResult.Success && d.Severity == DiagnosticSeverity.Warning))
                    sb.AppendLine(d.ToString());
            }

            if (!emitResult.Success)
                return (false, null, null, null, sb.ToString().Trim());

            return (true, peStream.ToArray(), pdbStream?.ToArray(), xmlStream?.ToArray(), sb.ToString().Trim());
        }

        private static string WrapEvalCode(string rawCode)
        {
            var defaultUsings = new List<string>
            {
                "using System;",
                "using System.Collections;",
                "using System.Collections.Generic;",
                "using System.IO;",
                "using System.Linq;",
                "using System.Reflection;",
                "using System.Text;",
                "using Microsoft.Xna.Framework;",
                "using Microsoft.Xna.Framework.Graphics;",
                "using StardewModdingAPI;",
                "using StardewModdingAPI.Utilities;",
                "using StardewValley;",
                "using StardewValley.Characters;",
                "using StardewValley.Locations;",
                "using StardewValley.Menus;",
                "using StardewValley.Objects;",
                "using StardewValley.TerrainFeatures;",
                "using StardewValley.Tools;",
                "using xTile;",
                "using xTile.Dimensions;",
                "using xTile.Layers;",
                "using xTile.ObjectModel;",
                "using xTile.Tiles;",
                "using SObject = StardewValley.Object;"
            };

            var extraUsings = new List<string>();
            var remainingLines = new List<string>();
            bool stillInHeader = true;

            foreach (var rawLine in rawCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string t = rawLine.Trim();
                if (stillInHeader)
                {
                    if (string.IsNullOrEmpty(t) || t.StartsWith("//", StringComparison.Ordinal))
                    {
                        remainingLines.Add(rawLine);
                        continue;
                    }
                    if (Regex.IsMatch(t, @"^using\s+(?:static\s+)?[A-Za-z_][A-Za-z0-9_.]*(?:\s*=\s*[A-Za-z_][A-Za-z0-9_.<>]*)?\s*;$"))
                    {
                        if (!defaultUsings.Any(u => u.Equals(t, StringComparison.Ordinal)) &&
                            !extraUsings.Any(u => u.Equals(t, StringComparison.Ordinal)))
                        {
                            extraUsings.Add(t);
                        }
                        continue;
                    }
                    stillInHeader = false;
                }
                remainingLines.Add(rawLine);
            }

            string trimmed = string.Join("\n", remainingLines).Trim();
            bool hasReturn = Regex.IsMatch(trimmed, @"\breturn\b");
            bool hasSemicolon = trimmed.EndsWith(";", StringComparison.Ordinal) || trimmed.EndsWith("}", StringComparison.Ordinal);

            string body;
            if (hasReturn)
            {
                body = hasSemicolon ? trimmed : trimmed + ";";
            }
            else if (!trimmed.Contains(";") && !trimmed.StartsWith("if ", StringComparison.Ordinal) &&
                     !trimmed.StartsWith("for ", StringComparison.Ordinal) && !trimmed.StartsWith("foreach ", StringComparison.Ordinal) &&
                     !trimmed.StartsWith("while ", StringComparison.Ordinal) && !trimmed.StartsWith("using ", StringComparison.Ordinal))
            {
                body = $"return ({trimmed});";
            }
            else
            {
                body = (hasSemicolon ? trimmed : trimmed + ";") + "\nreturn \"OK\";";
            }

            string allUsings = string.Join("\n", defaultUsings.Concat(extraUsings));
            return $@"{allUsings}

[assembly: System.Runtime.Versioning.TargetFramework("".NETCoreApp,Version=v6.0"", FrameworkDisplayName = "".NET 6.0"")]

namespace SdvEvalDynamic
{{
    public static class EvalEntry
    {{
        public static object? Execute(IModHelper Helper, IMonitor Monitor)
        {{
            Farmer Player = Game1.player;
            GameLocation Location = Game1.currentLocation;
            {body}
        }}
    }}
}}
";
        }

        private static List<MetadataReference> CollectReferencesInMemory(
            string gamePath,
            bool includeLoadedMods,
            IEnumerable<string>? extraReferences,
            bool publicize)
        {
            var bySimpleName = GetCachedBaseMemoryReferences(gamePath, publicize);

            var modDllPaths = new List<string>();
            if (includeLoadedMods)
            {
                CollectModEntryDllsFromRoot(Path.Combine(gamePath, "Mods"), modDllPaths);
            }
            if (extraReferences != null)
            {
                modDllPaths.AddRange(extraReferences);
            }

            foreach (var modDll in modDllPaths)
            {
                if (!File.Exists(modDll))
                    continue;
                string simple = Path.GetFileNameWithoutExtension(modDll);
                if (bySimpleName.ContainsKey(simple) && includeLoadedMods && (extraReferences == null || !extraReferences.Contains(modDll, StringComparer.OrdinalIgnoreCase)))
                    continue;

                var mref = GetOrLoadModReferenceZeroLock(modDll);
                if (mref != null)
                    bySimpleName[simple] = mref;
            }

            return bySimpleName.Values.ToList();
        }

        private static Dictionary<string, MetadataReference> GetCachedBaseMemoryReferences(string gamePath, bool publicize)
        {
            string fullGame = Path.GetFullPath(gamePath);
            string[] rootDlls = Directory.GetFiles(fullGame, "*.dll");
            long dirTicks = Directory.GetLastWriteTimeUtc(fullGame).Ticks;
            int dllCount = rootDlls.Length;

            lock (RefCacheLock)
            {
                var slot = publicize ? _memRefCachePublicized : _memRefCacheNormal;
                if (slot != null &&
                    slot.GamePath.Equals(fullGame, StringComparison.OrdinalIgnoreCase) &&
                    slot.DirTicksUtc == dirTicks &&
                    slot.RootDllCount == dllCount)
                {
                    return new Dictionary<string, MetadataReference>(slot.BySimpleName, StringComparer.OrdinalIgnoreCase);
                }

                var pathBySimple = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var dll in rootDlls)
                {
                    string fileName = Path.GetFileName(dll);
                    if (fileName.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".Native.dll", StringComparison.OrdinalIgnoreCase) ||
                        ExcludedFacadeOrNativeDlls.Contains(fileName))
                    {
                        continue;
                    }
                    pathBySimple[Path.GetFileNameWithoutExtension(fileName)] = dll;
                }

                string smapiInternal = Path.Combine(fullGame, "smapi-internal");
                foreach (var name in new[] { "0Harmony.dll", "SMAPI.Toolkit.CoreInterfaces.dll", "Newtonsoft.Json.dll" })
                {
                    string p = Path.Combine(smapiInternal, name);
                    if (File.Exists(p))
                        pathBySimple[Path.GetFileNameWithoutExtension(name)] = p;
                }

                if (publicize)
                {
                    PublicizedAssemblyCache.ApplyPublicizedReferences(fullGame, pathBySimple);
                }

                var computed = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in pathBySimple)
                {
                    var mref = TryCreateMetadataReferenceZeroLock(kv.Value);
                    if (mref != null)
                        computed[kv.Key] = mref;
                }

                var createdSlot = new CachedMemoryRefs
                {
                    GamePath = fullGame,
                    DirTicksUtc = dirTicks,
                    RootDllCount = dllCount,
                    Publicized = publicize,
                    BySimpleName = computed
                };

                if (publicize)
                    _memRefCachePublicized = createdSlot;
                else
                    _memRefCacheNormal = createdSlot;

                return new Dictionary<string, MetadataReference>(computed, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static MetadataReference? GetOrLoadModReferenceZeroLock(string dllPath)
        {
            string full = Path.GetFullPath(dllPath);
            DateTime writeUtc = File.GetLastWriteTimeUtc(full);
            lock (RefCacheLock)
            {
                if (ExtraModRefCache.TryGetValue(full, out var hit) && hit.WriteUtc == writeUtc)
                    return hit.Ref;

                var created = TryCreateMetadataReferenceZeroLock(full);
                if (created != null)
                    ExtraModRefCache[full] = (writeUtc, created);
                return created;
            }
        }

        private static MetadataReference? TryCreateMetadataReferenceZeroLock(string dllPath)
        {
            try
            {
                byte[] bytes = ReadBytesZeroLock(dllPath);
                using var ms = new MemoryStream(bytes, writable: false);
                using var pe = new PEReader(ms);
                if (!pe.HasMetadata)
                    return null;

                return MetadataReference.CreateFromImage(bytes, filePath: dllPath);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ReadBytesZeroLock(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            byte[] buf = new byte[fs.Length];
            int offset = 0;
            while (offset < buf.Length)
            {
                int r = fs.Read(buf, offset, buf.Length - offset);
                if (r <= 0) break;
                offset += r;
            }
            return buf;
        }

        private static IEnumerable<string> FindLocalModDllReferences(string modDir, string outputDllPath)
        {
            string libDir = Path.Combine(modDir, "lib");
            if (!Directory.Exists(libDir))
                return Array.Empty<string>();
            return Directory.GetFiles(libDir, "*.dll")
                .Where(f => !f.Equals(outputDllPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void CollectModEntryDllsFromRoot(string modsDir, List<string> targetList)
        {
            if (!Directory.Exists(modsDir))
                return;
            foreach (var manifest in Directory.GetFiles(modsDir, "manifest.json", SearchOption.AllDirectories))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifest), JsoncOpts);
                    string? entryDll = GetProp(doc.RootElement, "EntryDll");
                    if (!string.IsNullOrWhiteSpace(entryDll))
                    {
                        string modDll = Path.Combine(Path.GetDirectoryName(manifest)!, entryDll!);
                        string simple = Path.GetFileNameWithoutExtension(modDll);
                        if (File.Exists(modDll) && !simple.Equals("SdvDevBridge", StringComparison.OrdinalIgnoreCase))
                        {
                            targetList.Add(modDll);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public static string DeployModFolder(string sourceModDir, string gamePath, string fallbackFolderName)
        {
            return DeployModFolderToModsRoot(sourceModDir, Path.Combine(gamePath, "Mods"), fallbackFolderName);
        }

        public static string DeployModFolderToModsRoot(string sourceModDir, string modsRoot, string fallbackFolderName)
        {
            Directory.CreateDirectory(modsRoot);

            string folderName = Path.GetFileName(sourceModDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName) || folderName.Equals("src", StringComparison.OrdinalIgnoreCase))
                folderName = Regex.Replace(fallbackFolderName, @"[^a-zA-Z0-9._\- ]+", "_");

            string targetDir = Path.Combine(modsRoot, folderName);

            if (Path.GetFullPath(sourceModDir).TrimEnd('\\').Equals(Path.GetFullPath(targetDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return targetDir;

            if (Directory.Exists(targetDir))
            {
                string existingConfig = Path.Combine(targetDir, "config.json");
                string? savedConfig = File.Exists(existingConfig) ? File.ReadAllText(existingConfig) : null;

                Directory.Delete(targetDir, recursive: true);
                Directory.CreateDirectory(targetDir);

                if (savedConfig != null && !File.Exists(Path.Combine(sourceModDir, "config.json")))
                    File.WriteAllText(existingConfig, savedConfig, new UTF8Encoding(false));
            }
            else
            {
                Directory.CreateDirectory(targetDir);
            }

            CopyModTree(sourceModDir, targetDir, skipBinaries: false);
            return targetDir;
        }

        public static string DeployAssetsOnlyToModsRoot(string sourceModDir, string modsRoot, string fallbackFolderName)
        {
            Directory.CreateDirectory(modsRoot);

            string folderName = Path.GetFileName(sourceModDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName) || folderName.Equals("src", StringComparison.OrdinalIgnoreCase))
                folderName = Regex.Replace(fallbackFolderName, @"[^a-zA-Z0-9._\- ]+", "_");

            string manifestSrc = Path.Combine(sourceModDir, "manifest.json");
            if (File.Exists(manifestSrc))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestSrc), JsoncOpts);
                    string? uid = GetProp(doc.RootElement, "UniqueID");
                    if (!string.IsNullOrWhiteSpace(uid))
                    {
                        foreach (var mf in Directory.GetFiles(modsRoot, "manifest.json", SearchOption.AllDirectories))
                        {
                            using var d2 = JsonDocument.Parse(File.ReadAllText(mf), JsoncOpts);
                            if (string.Equals(GetProp(d2.RootElement, "UniqueID"), uid, StringComparison.OrdinalIgnoreCase))
                            {
                                string existingDir = Path.GetDirectoryName(mf)!;
                                if (!Path.GetFullPath(sourceModDir).TrimEnd('\\').Equals(Path.GetFullPath(existingDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                                    CopyModTree(sourceModDir, existingDir, skipBinaries: true);
                                return existingDir;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            string targetDir = Path.Combine(modsRoot, folderName);
            if (!Path.GetFullPath(sourceModDir).TrimEnd('\\').Equals(Path.GetFullPath(targetDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(targetDir);
                CopyModTree(sourceModDir, targetDir, skipBinaries: true);
            }
            return targetDir;
        }

        private static void CopyModTree(string srcDir, string dstDir, bool skipBinaries)
        {
            Directory.CreateDirectory(dstDir);

            foreach (var file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".cs" || ext == ".csproj" || ext == ".sln" || ext == ".user" ||
                    ext == ".suo" || ext == ".zip" || ext == ".nupkg" || ext == ".bak" || ext == ".tmp" || ext == ".log")
                    continue;
                if (fileName.Equals("sdv-project.json", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (skipBinaries && (ext == ".dll" || ext == ".pdb" || ext == ".exe"))
                    continue;

                File.Copy(file, Path.Combine(dstDir, fileName), overwrite: true);
            }

            foreach (var sub in Directory.GetDirectories(srcDir))
            {
                string name = Path.GetFileName(sub);
                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(".vscode", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("releases", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                CopyModTree(sub, Path.Combine(dstDir, name), skipBinaries);
            }
        }

        private static string? GetProp(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object)
                return null;
            foreach (var p in obj.EnumerateObject())
            {
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                    return p.Value.GetString();
            }
            return null;
        }
    }
}
