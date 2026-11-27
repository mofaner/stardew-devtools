using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SdvKb.Indexers;

namespace SdvKb.Project
{
    public sealed class InstalledModInfo
    {
        public string UniqueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public string? EntryDll { get; set; }
        public string? EntryDllFullPath => !string.IsNullOrWhiteSpace(EntryDll) ? Path.Combine(DirectoryPath, EntryDll!) : null;
        public string? ContentPackForId { get; set; }
        public string Source { get; set; } = "local-mods"; // local-mods | deps-cache
        public List<(string UniqueId, bool IsRequired)> Dependencies { get; set; } = new List<(string UniqueId, bool IsRequired)>();
    }

    public sealed class ProjectDepSpec
    {
        public string UniqueId { get; set; } = string.Empty;
        public string? MinVersion { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool ReferenceDll { get; set; } = true;
        public string? GitHubRepo { get; set; }
        public string? AssetKeyword { get; set; }
    }

    public sealed class SdvProjectConfig
    {
        public bool Isolated { get; set; }
        public List<string> ExtraMods { get; set; } = new List<string>();
        public List<ProjectDepSpec> Dependencies { get; set; } = new List<ProjectDepSpec>();
    }

    public sealed class DependencyResolutionResult
    {
        public string ProjectDir { get; set; } = string.Empty;
        public string ModUniqueId { get; set; } = string.Empty;
        public bool IsolatedDefault { get; set; }
        public List<InstalledModInfo> ResolvedMods { get; set; } = new List<InstalledModInfo>();
        public List<string> CompileReferenceDlls { get; set; } = new List<string>();
        public List<string> MissingRequired { get; set; } = new List<string>();
        public List<string> MissingOptional { get; set; } = new List<string>();
        public List<string> Notes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Lightweight project &amp; prerequisite mod resolver:
    /// 1. Reads prerequisites from `manifest.json` (`ContentPackFor` + `Dependencies`) and optional `sdv-project.json`.
    /// 2. Scans local `&lt;GamePath&gt;\Mods` (recursively handling nested folders) and `~/.local/share/sdv-dev/deps-cache`.
    /// 3. Resolves full transitive prerequisite graph for `--isolated` (`--mods-path`) loading and C# compile-time DLL references.
    /// 4. Falls back to GitHub Releases `.zip` download (never NexusMods!) when an open-source prerequisite is missing locally.
    /// </summary>
    public static class ModDependencyResolver
    {
        public static string DepsCacheRoot => Path.Combine(KbConfig.DataDir, "deps-cache");
        public static string DefaultIsolatedModsRoot => Path.Combine(KbConfig.DataDir, "isolated-mods");

        private static readonly JsonDocumentOptions JsoncReadOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // Common Stardew 1.6 framework aliases, open-source GitHub Release repos, and CurseForge CDN slugs!
        // 【当初为什么要给 Pathoschild / spacechase0 特殊仓库单独配 CurseForgeSlug】：
        // 实测 `Pathoschild/StardewMods` 与 `spacechase0/StardewValleyMods` 在 GitHub 上只打源码 git tag（Releases 列表为空，不挂预编译 .zip），
        // 新手电脑 `Mods\` 为空时走 GitHub Releases 会拉空；而它们同步发布的 CurseForge 官方 CDN (`edge.forgecdn.net`) 支持免登录 HTTP 200 直拉最新预编译 `.zip`！
        private static readonly (string Alias, string UniqueId, string? GitHubRepo, string? AssetHint, string? CurseForgeSlug)[] KnownFrameworks =
        {
            ("cp", "Pathoschild.ContentPatcher", "Pathoschild/StardewMods", "ContentPatcher", "content-patcher"),
            ("contentpatcher", "Pathoschild.ContentPatcher", "Pathoschild/StardewMods", "ContentPatcher", "content-patcher"),
            ("gmcm", "spacechase0.GenericModConfigMenu", "spacechase0/StardewValleyMods", "GenericModConfigMenu", "generic-mod-config-menu"),
            ("genericmodconfigmenu", "spacechase0.GenericModConfigMenu", "spacechase0/StardewValleyMods", "GenericModConfigMenu", "generic-mod-config-menu"),
            ("spacecore", "spacechase0.SpaceCore", "spacechase0/StardewValleyMods", "SpaceCore", "spacecore"),
            ("ftm", "Esca.FarmTypeManager", "Esca-MMC/FarmTypeManager", "FarmTypeManager", null),
            ("farmtypemanager", "Esca.FarmTypeManager", "Esca-MMC/FarmTypeManager", "FarmTypeManager", null),
            ("automate", "Pathoschild.Automate", "Pathoschild/StardewMods", "Automate", "automate"),
            ("lookupanything", "Pathoschild.LookupAnything", "Pathoschild/StardewMods", "LookupAnything", "lookup-anything"),
            ("chestsanywhere", "Pathoschild.ChestsAnywhere", "Pathoschild/StardewMods", "ChestsAnywhere", "chests-anywhere"),
            ("tractormod", "Pathoschild.TractorMod", "Pathoschild/StardewMods", "TractorMod", "tractor-mod"),
            ("centralstation", "Pathoschild.CentralStation", "Pathoschild/StardewMods", "CentralStation", "central-station"),
            ("uiinfosuite2", "Annosz.UiInfoSuite2", "Annosz/UIInfoSuite2", "UIInfoSuite2", null),
            ("alternativetextures", "PeacefulEnd.AlternativeTextures", "Floogen/AlternativeTextures", "AlternativeTextures", null),
            ("fashionSense", "PeacefulEnd.FashionSense", "Floogen/FashionSense", "FashionSense", null),
            ("customcompanions", "PeacefulEnd.CustomCompanions", "Floogen/CustomCompanions", "CustomCompanions", null),
            ("solidfoundations", "PeacefulEnd.SolidFoundations", "Floogen/SolidFoundations", "SolidFoundations", null),
            ("stardewhack", "bcmpinc.StardewHack", "bcmpinc/StardewHack", "StardewHack", null),
            ("kokoro", "Shockah.Kokoro", "Shockah/Stardew-Valley-Mods", "Kokoro", null),
            ("trinkettinker", "mushymato.TrinketTinker", "Mushymato/TrinketTinker", "TrinketTinker", null),
            ("sve", "FlashShifter.StardewValleyExpandedCP", null, null, null)
        };

        private static readonly HashSet<string> TagOnlyGitHubMonorepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pathoschild/StardewMods",
            "spacechase0/StardewValleyMods"
        };

        public static Dictionary<string, InstalledModInfo> ScanAvailableMods(string gamePath)
        {
            var index = new Dictionary<string, InstalledModInfo>(StringComparer.OrdinalIgnoreCase);

            // 1. Scan deps-cache first, then local GamePath\Mods (so player's local Mods version takes priority!)
            ScanRootIntoIndex(DepsCacheRoot, "deps-cache", index);
            if (!string.IsNullOrWhiteSpace(gamePath))
            {
                ScanRootIntoIndex(Path.Combine(gamePath, "Mods"), "local-mods", index);
            }

            return index;
        }

        private static void ScanRootIntoIndex(string rootDir, string sourceTag, Dictionary<string, InstalledModInfo> index)
        {
            if (!Directory.Exists(rootDir))
                return;

            string[] manifests;
            try
            {
                manifests = Directory.GetFiles(rootDir, "manifest.json", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            foreach (var mf in manifests)
            {
                try
                {
                    string modFolder = Path.GetDirectoryName(mf)!;
                    string folderName = Path.GetFileName(modFolder);
                    // Skip SMAPI disabled folders starting with '.'
                    if (folderName.StartsWith(".", StringComparison.Ordinal))
                        continue;

                    var parsed = ParseManifestFile(mf, sourceTag);
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.UniqueId))
                        continue;
                    if (parsed.UniqueId.Equals("SdvDev.Bridge", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("_SdvDevBridge", StringComparison.OrdinalIgnoreCase))
                        continue;

                    index[parsed.UniqueId] = parsed;
                }
                catch
                {
                }
            }
        }

        public static InstalledModInfo? ParseManifestFile(string manifestPath, string sourceTag = "project")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath), JsoncReadOpts);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            string? uid = GetString(root, "UniqueID");
            if (string.IsNullOrWhiteSpace(uid))
                return null;

            var info = new InstalledModInfo
            {
                UniqueId = uid!.Trim(),
                Name = GetString(root, "Name") ?? uid!.Trim(),
                Version = GetString(root, "Version") ?? "1.0.0",
                DirectoryPath = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!,
                EntryDll = GetString(root, "EntryDll"),
                Source = sourceTag
            };

            if (TryGetProp(root, "ContentPackFor", out var cpf) && cpf.ValueKind == JsonValueKind.Object)
            {
                string? cpfId = GetString(cpf, "UniqueID");
                if (!string.IsNullOrWhiteSpace(cpfId))
                {
                    info.ContentPackForId = cpfId!.Trim();
                    info.Dependencies.Add((cpfId!.Trim(), true));
                }
            }

            if (TryGetProp(root, "Dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in depsEl.EnumerateArray())
                {
                    if (d.ValueKind != JsonValueKind.Object)
                        continue;
                    string? depId = GetString(d, "UniqueID");
                    if (string.IsNullOrWhiteSpace(depId))
                        continue;

                    bool isReq = true;
                    if (TryGetProp(d, "IsRequired", out var reqEl))
                    {
                        if (reqEl.ValueKind == JsonValueKind.False)
                            isReq = false;
                        else if (reqEl.ValueKind == JsonValueKind.String && reqEl.GetString()!.Equals("false", StringComparison.OrdinalIgnoreCase))
                            isReq = false;
                    }

                    if (!info.Dependencies.Any(x => x.UniqueId.Equals(depId, StringComparison.OrdinalIgnoreCase)))
                        info.Dependencies.Add((depId!.Trim(), isReq));
                }
            }

            return info;
        }

        public static SdvProjectConfig LoadProjectConfig(string modDir, bool includeLocalOverrides = true)
        {
            var jsonOpts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            SdvProjectConfig baseCfg = new SdvProjectConfig();
            string cfgPath = Path.Combine(modDir, "sdv-project.json");
            if (File.Exists(cfgPath))
            {
                try
                {
                    baseCfg = JsonSerializer.Deserialize<SdvProjectConfig>(File.ReadAllText(cfgPath), jsonOpts) ?? new SdvProjectConfig();
                }
                catch { }
            }

            // 支持免污染 Git 工作区的本地覆盖配置 `sdv-project.local.json`（用于临时挂载 SVE / RSV 等大型联调包）
            if (includeLocalOverrides)
            {
                string localPath = Path.Combine(modDir, "sdv-project.local.json");
                if (File.Exists(localPath))
                {
                    try
                    {
                        var localCfg = JsonSerializer.Deserialize<SdvProjectConfig>(File.ReadAllText(localPath), jsonOpts);
                        if (localCfg != null)
                        {
                            if (localCfg.Isolated)
                                baseCfg.Isolated = true;
                            foreach (var em in localCfg.ExtraMods)
                            {
                                if (!string.IsNullOrWhiteSpace(em) &&
                                    !baseCfg.ExtraMods.Contains(em.Trim(), StringComparer.OrdinalIgnoreCase))
                                {
                                    baseCfg.ExtraMods.Add(em.Trim());
                                }
                            }
                            foreach (var ld in localCfg.Dependencies)
                            {
                                if (!string.IsNullOrWhiteSpace(ld.UniqueId) &&
                                    !baseCfg.Dependencies.Any(d => d.UniqueId.Equals(ld.UniqueId, StringComparison.OrdinalIgnoreCase)))
                                {
                                    baseCfg.Dependencies.Add(ld);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            return baseCfg;
        }

        public static void SaveProjectConfig(string modDir, SdvProjectConfig cfg)
        {
            string cfgPath = Path.Combine(modDir, "sdv-project.json");
            string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(cfgPath, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// Resolves all direct and transitive prerequisites of `modDir` (plus any CLI `extraModQueries`),
        /// looking in local `GamePath\Mods` -> `deps-cache` -> GitHub Releases fallback (if `allowGitHubDownload` is true).
        /// </summary>
        public static DependencyResolutionResult ResolveProjectDependencies(
            string? modDir,
            string gamePath,
            IEnumerable<string>? extraModQueries = null,
            bool allowGitHubDownload = true)
        {
            var res = new DependencyResolutionResult();
            var available = ScanAvailableMods(gamePath);
            var queue = new Queue<(string QueryOrId, bool IsRequired, bool RefDll, string? ExplicitGitHub, string? AssetHint)>();

            if (!string.IsNullOrWhiteSpace(modDir) && Directory.Exists(modDir))
            {
                string fullModDir = Path.GetFullPath(modDir!);
                res.ProjectDir = fullModDir;

                var projCfg = LoadProjectConfig(fullModDir);
                res.IsolatedDefault = projCfg.Isolated;

                string manifestPath = Path.Combine(fullModDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var selfInfo = ParseManifestFile(manifestPath, "project");
                    if (selfInfo != null)
                    {
                        res.ModUniqueId = selfInfo.UniqueId;
                        foreach (var dep in selfInfo.Dependencies)
                        {
                            var cfgMatch = projCfg.Dependencies.FirstOrDefault(d => d.UniqueId.Equals(dep.UniqueId, StringComparison.OrdinalIgnoreCase));
                            queue.Enqueue((
                                dep.UniqueId,
                                dep.IsRequired,
                                cfgMatch?.ReferenceDll ?? true,
                                cfgMatch?.GitHubRepo,
                                cfgMatch?.AssetKeyword));
                        }
                    }
                }

                foreach (var pd in projCfg.Dependencies)
                {
                    if (!string.IsNullOrWhiteSpace(pd.UniqueId))
                        queue.Enqueue((pd.UniqueId, pd.IsRequired, pd.ReferenceDll, pd.GitHubRepo, pd.AssetKeyword));
                }

                foreach (var extra in projCfg.ExtraMods)
                {
                    if (!string.IsNullOrWhiteSpace(extra))
                        queue.Enqueue((extra.Trim(), true, false, null, null));
                }

                // 自动识别代码中的 GMCM 软依赖（`GetApi("spacechase0.GenericModConfigMenu")` 或 `GmcmIntegration.g.cs`）：
                // C# 模组通常不把 GMCM 写进 manifest.json 的 Dependencies，因此在隔离测试时自动将其作为可选前置带入，确保进游戏能直接看到小齿轮配置菜单！
                if (!string.Equals(res.ModUniqueId, "spacechase0.GenericModConfigMenu", StringComparison.OrdinalIgnoreCase) &&
                    ProjectReferencesSoftGmcm(fullModDir))
                {
                    queue.Enqueue(("spacechase0.GenericModConfigMenu", false, false, null, null));
                }
            }

            if (extraModQueries != null)
            {
                foreach (var q in extraModQueries)
                {
                    if (!string.IsNullOrWhiteSpace(q))
                        queue.Enqueue((q.Trim(), true, false, null, null));
                }
            }

            var visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(res.ModUniqueId))
                visitedIds.Add(res.ModUniqueId);

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                string canonicalId = ResolveAliasOrMatchLocal(item.QueryOrId, available);
                if (!visitedIds.Add(canonicalId))
                    continue;

                if (!available.TryGetValue(canonicalId, out var modInfo) && allowGitHubDownload)
                {
                    var (fetched, note) = TryFetchRemotePrerequisite(canonicalId, item.ExplicitGitHub, item.AssetHint);
                    if (!string.IsNullOrEmpty(note))
                        res.Notes.Add(note!);

                    if (fetched != null)
                    {
                        // Re-scan deps-cache in case the zip contained multiple folders (e.g., sub-packs)
                        ScanRootIntoIndex(DepsCacheRoot, "deps-cache", available);
                        available.TryGetValue(canonicalId, out modInfo);
                        modInfo ??= fetched;
                    }
                }

                if (modInfo == null)
                {
                    if (item.IsRequired)
                        res.MissingRequired.Add(canonicalId);
                    else
                        res.MissingOptional.Add(canonicalId);
                    continue;
                }

                res.ResolvedMods.Add(modInfo);

                if (item.RefDll && modInfo.EntryDllFullPath != null && File.Exists(modInfo.EntryDllFullPath))
                {
                    if (!res.CompileReferenceDlls.Contains(modInfo.EntryDllFullPath, StringComparer.OrdinalIgnoreCase))
                        res.CompileReferenceDlls.Add(modInfo.EntryDllFullPath);
                }

                // Enqueue transitive required dependencies (e.g., ContentPack -> Framework -> SubFramework)
                foreach (var trans in modInfo.Dependencies)
                {
                    if (trans.IsRequired && !visitedIds.Contains(trans.UniqueId))
                    {
                        queue.Enqueue((trans.UniqueId, true, false, null, null));
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Adds or updates a prerequisite dependency in the mod's `manifest.json` (`Dependencies`) and `sdv-project.json`,
        /// resolving it from local `GamePath\Mods` first, or GitHub Releases if missing locally.
        /// </summary>
        public static object AddDependencyToProject(
            string modDir,
            string queryOrId,
            string gamePath,
            bool isRequired,
            bool referenceDll,
            string? explicitGitHubRepo,
            string? minVersionOverride)
        {
            string fullModDir = Path.GetFullPath(modDir);
            string manifestPath = Path.Combine(fullModDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"manifest.json not found in `{fullModDir}`.");

            var available = ScanAvailableMods(gamePath);
            string targetId = ResolveAliasOrMatchLocal(queryOrId, available);
            string? note = null;

            if (!available.TryGetValue(targetId, out var resolvedMod))
            {
                var (fetched, dlNote) = TryFetchRemotePrerequisite(targetId, explicitGitHubRepo, assetHint: null);
                note = dlNote;
                if (fetched != null)
                {
                    resolvedMod = fetched;
                    targetId = fetched.UniqueId;
                }
            }

            string? minVer = minVersionOverride ?? resolvedMod?.Version;

            // 1. Update manifest.json Dependencies array cleanly using System.Text.Json.Nodes
            var rootNode = JsonNode.Parse(File.ReadAllText(manifestPath), documentOptions: JsoncReadOpts) as JsonObject
                ?? throw new InvalidDataException("manifest.json root must be a JSON object.");

            // Do not duplicate if already ContentPackFor
            string? cpfId = (rootNode["ContentPackFor"] as JsonObject)?["UniqueID"]?.GetValue<string>();
            if (!string.Equals(cpfId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                JsonArray depsArr;
                if (rootNode["Dependencies"] is JsonArray existingArr)
                {
                    depsArr = existingArr;
                }
                else
                {
                    depsArr = new JsonArray();
                    rootNode["Dependencies"] = depsArr;
                }

                JsonObject? existingEntry = depsArr
                    .OfType<JsonObject>()
                    .FirstOrDefault(o => string.Equals(o["UniqueID"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase));

                if (existingEntry == null)
                {
                    existingEntry = new JsonObject { ["UniqueID"] = targetId };
                    depsArr.Add(existingEntry);
                }

                if (!string.IsNullOrWhiteSpace(minVer))
                    existingEntry["MinimumVersion"] = minVer;
                existingEntry["IsRequired"] = isRequired;

                File.WriteAllText(manifestPath, rootNode.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }), new UTF8Encoding(false));
            }

            // 2. Update sdv-project.json (without leaking sdv-project.local.json entries into git-tracked sdv-project.json!)
            var projCfg = LoadProjectConfig(fullModDir, includeLocalOverrides: false);
            var existingSpec = projCfg.Dependencies.FirstOrDefault(d => d.UniqueId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
            if (existingSpec == null)
            {
                existingSpec = new ProjectDepSpec { UniqueId = targetId };
                projCfg.Dependencies.Add(existingSpec);
            }
            existingSpec.IsRequired = isRequired;
            existingSpec.ReferenceDll = referenceDll;
            existingSpec.MinVersion = minVer;
            if (!string.IsNullOrWhiteSpace(explicitGitHubRepo))
                existingSpec.GitHubRepo = explicitGitHubRepo;
            SaveProjectConfig(fullModDir, projCfg);

            return new
            {
                ok = resolvedMod != null || !isRequired,
                uniqueId = targetId,
                name = resolvedMod?.Name ?? targetId,
                version = resolvedMod?.Version ?? minVer,
                resolvedFrom = resolvedMod?.Source ?? "unresolved",
                resolvedDirectory = resolvedMod?.DirectoryPath,
                entryDll = resolvedMod?.EntryDllFullPath,
                isRequired,
                referenceDll,
                note
            };
        }

        public static object RemoveDependencyFromProject(string modDir, string queryOrId, string gamePath)
        {
            string fullModDir = Path.GetFullPath(modDir);
            var available = ScanAvailableMods(gamePath);
            string targetId = ResolveAliasOrMatchLocal(queryOrId, available);

            string manifestPath = Path.Combine(fullModDir, "manifest.json");
            bool removedFromManifest = false;
            if (File.Exists(manifestPath))
            {
                var rootNode = JsonNode.Parse(File.ReadAllText(manifestPath), documentOptions: JsoncReadOpts) as JsonObject;
                if (rootNode?["Dependencies"] is JsonArray depsArr)
                {
                    for (int i = depsArr.Count - 1; i >= 0; i--)
                    {
                        if (depsArr[i] is JsonObject o &&
                            string.Equals(o["UniqueID"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase))
                        {
                            depsArr.RemoveAt(i);
                            removedFromManifest = true;
                        }
                    }
                    if (removedFromManifest)
                    {
                        File.WriteAllText(manifestPath, rootNode!.ToJsonString(new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        }), new UTF8Encoding(false));
                    }
                }
            }

            var projCfg = LoadProjectConfig(fullModDir, includeLocalOverrides: false);
            int removedCfg = projCfg.Dependencies.RemoveAll(d => d.UniqueId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
            if (removedCfg > 0 || File.Exists(Path.Combine(fullModDir, "sdv-project.json")))
                SaveProjectConfig(fullModDir, projCfg);

            return new
            {
                ok = true,
                uniqueId = targetId,
                removedFromManifest,
                removedFromProjectJson = removedCfg > 0
            };
        }

        /// <summary>
        /// Inspects &amp; decompiles the public Mod-Provided API (`GetApi()` + `I*Api` interface) of a prerequisite mod DLL!
        /// Essential for SMAPI `Helper.ModRegistry.GetApi&lt;IMyModApi&gt;("UniqueID")` integration without guessing signatures.
        /// </summary>
        public static string InspectModProvidedApi(string queryOrId, string gamePath, string? typeFilter = null, int maxLines = 320)
        {
            var available = ScanAvailableMods(gamePath);
            string targetId = ResolveAliasOrMatchLocal(queryOrId, available);
            if (!available.TryGetValue(targetId, out var mod))
            {
                // 新手电脑 `Mods\` 为空时，自动拉取远程前置到 `deps-cache` 后再反编译 `I*Api` 接口
                var (fetched, _) = TryFetchRemotePrerequisite(targetId, null, null);
                if (fetched != null)
                {
                    ScanRootIntoIndex(DepsCacheRoot, "deps-cache", available);
                    available.TryGetValue(targetId, out mod);
                    mod ??= fetched;
                }
            }

            if (mod == null)
                throw new InvalidOperationException($"Mod `{queryOrId}` (`{targetId}`) is not installed in local Mods or deps-cache, and could not be downloaded automatically.");

            if (string.IsNullOrWhiteSpace(mod.EntryDllFullPath) || !File.Exists(mod.EntryDllFullPath))
                return $"// Mod `{mod.Name}` ({mod.UniqueId} v{mod.Version}) is a Content Pack without an EntryDll.";

            return DecompileService.DecompileExternalModApi(mod.EntryDllFullPath!, gamePath, mod.UniqueId, mod.Version, typeFilter, maxLines);
        }

        public static string ExpandQueryAlias(string query, Dictionary<string, InstalledModInfo> available) =>
            ResolveAliasOrMatchLocal(query, available);

        /// <summary>
        /// Stages an isolated SMAPI `--mods-path` folder containing ONLY:
        /// - `_SdvDevBridge`
        /// - Target mod (`projectDir`, if any)
        /// - All resolved direct &amp; transitive prerequisite mods!
        /// </summary>
        public static (string IsolatedModsDir, List<string> StagedModIds, List<string> MissingModIds) StageIsolatedModsDirectory(
            string gamePath,
            string? customIsolatedDir,
            string? projectDir,
            IEnumerable<string>? extraWithMods,
            string? preserveConfigsFromExistingDir = null)
        {
            string isoRoot = string.IsNullOrWhiteSpace(customIsolatedDir)
                ? DefaultIsolatedModsRoot
                : Path.GetFullPath(customIsolatedDir!);

            // Preserve any generated `config.json` files per mod folder before refreshing isolated directory.
            // 当处于 `self_restart` 预暂存阶段时，旧游戏正锁着 `preserveConfigsFromExistingDir` 中的 DLL，
            // 此时仅以共享只读方式提取其中的 `config.json` 写入全新的 `.staging` 目录，绝不删除正被占用的运行目录！
            var savedConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string configSourceRoot = !string.IsNullOrWhiteSpace(preserveConfigsFromExistingDir) && Directory.Exists(preserveConfigsFromExistingDir)
                ? Path.GetFullPath(preserveConfigsFromExistingDir!)
                : isoRoot;

            if (Directory.Exists(configSourceRoot))
            {
                foreach (var sub in Directory.GetDirectories(configSourceRoot))
                {
                    string cfgFile = Path.Combine(sub, "config.json");
                    if (File.Exists(cfgFile))
                    {
                        try
                        {
                            using var fs = new FileStream(cfgFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var sr = new StreamReader(fs, Encoding.UTF8);
                            savedConfigs[Path.GetFileName(sub)] = sr.ReadToEnd();
                        }
                        catch { }
                    }
                }
            }

            if (Directory.Exists(isoRoot))
            {
                Directory.Delete(isoRoot, recursive: true);
            }
            Directory.CreateDirectory(isoRoot);

            var depRes = ResolveProjectDependencies(projectDir, gamePath, extraWithMods, allowGitHubDownload: true);
            var stagedIds = new List<string>();

            foreach (var depMod in depRes.ResolvedMods)
            {
                string safeFolder = SanitizeFolderName(depMod.UniqueId);
                string dst = Path.Combine(isoRoot, safeFolder);
                CopyDirectoryAll(depMod.DirectoryPath, dst);
                if (savedConfigs.TryGetValue(safeFolder, out var oldCfg) && !File.Exists(Path.Combine(dst, "config.json")))
                {
                    File.WriteAllText(Path.Combine(dst, "config.json"), oldCfg, new UTF8Encoding(false));
                }
                stagedIds.Add($"{depMod.UniqueId} ({depMod.Version}, {depMod.Source})");
            }

            if (!string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir))
            {
                string fullProj = Path.GetFullPath(projectDir!);
                string folderName = Path.GetFileName(fullProj.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName) || folderName.Equals("src", StringComparison.OrdinalIgnoreCase))
                    folderName = SanitizeFolderName(depRes.ModUniqueId);

                string dst = Build.DotNet6ModCompiler.DeployModFolderToModsRoot(fullProj, isoRoot, folderName);
                if (savedConfigs.TryGetValue(Path.GetFileName(dst), out var oldProjCfg) && !File.Exists(Path.Combine(dst, "config.json")))
                {
                    File.WriteAllText(Path.Combine(dst, "config.json"), oldProjCfg, new UTF8Encoding(false));
                }
                stagedIds.Add($"{depRes.ModUniqueId} (target-project)");
            }

            return (isoRoot, stagedIds, depRes.MissingRequired);
        }

        private static string ResolveAliasOrMatchLocal(string query, Dictionary<string, InstalledModInfo> available)
        {
            string q = query.Trim();
            if (available.ContainsKey(q))
                return available[q].UniqueId;

            var known = KnownFrameworks.FirstOrDefault(k =>
                k.Alias.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                k.UniqueId.Equals(q, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(known.UniqueId))
                return known.UniqueId;

            // Substring match against local installed Mods (e.g. "Portraiture", "WearMoreRings", "SkullCavern")
            var byName = available.Values.FirstOrDefault(m =>
                m.Name.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                m.UniqueId.EndsWith("." + q, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Replace(" ", "").Equals(q.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName.UniqueId;

            return q;
        }

        private static bool ProjectReferencesSoftGmcm(string fullModDir)
        {
            try
            {
                if (File.Exists(Path.Combine(fullModDir, "GmcmIntegration.g.cs")) ||
                    File.Exists(Path.Combine(fullModDir, "IGenericModConfigMenuApi.cs")))
                {
                    return true;
                }

                string[] skipped = { "bin", "obj", "releases", ".git", ".vs" };
                foreach (var f in Directory.GetFiles(fullModDir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f);
                    if (!ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string rel = Path.GetRelativePath(fullModDir, f);
                    var parts = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Take(Math.Max(0, parts.Length - 1)).Any(p => skipped.Contains(p, StringComparer.OrdinalIgnoreCase)))
                        continue;

                    if (new FileInfo(f).Length > 512 * 1024)
                        continue;

                    string txt = File.ReadAllText(f);
                    if (txt.IndexOf("spacechase0.GenericModConfigMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 在非隔离模式下启动目标工程前，确保工程声明的必需前置（如新手没装的 `Pathoschild.ContentPatcher`）在目标 `Mods` 目录就位。
        /// </summary>
        public static void EnsureRequiredPrerequisitesInModsRoot(string? modDir, string gamePath, string targetModsRoot)
        {
            if (string.IsNullOrWhiteSpace(modDir) || !Directory.Exists(modDir))
                return;

            var existingInTarget = new Dictionary<string, InstalledModInfo>(StringComparer.OrdinalIgnoreCase);
            ScanRootIntoIndex(targetModsRoot, "local-mods", existingInTarget);

            var depRes = ResolveProjectDependencies(modDir, gamePath, extraModQueries: null, allowGitHubDownload: true);
            foreach (var depMod in depRes.ResolvedMods)
            {
                if (existingInTarget.ContainsKey(depMod.UniqueId))
                    continue;

                string dst = Path.Combine(targetModsRoot, SanitizeFolderName(depMod.UniqueId));
                CopyDirectoryAll(depMod.DirectoryPath, dst);
            }
        }

        /// <summary>
        /// 统一远程前置拉取路由：
        /// 1. 对已知不在 GitHub Releases 挂预编译 `.zip` 的特殊单仓多模组库（如 `Pathoschild/StardewMods`、`spacechase0/StardewValleyMods`），
        ///    直接走官方 CurseForge 公共 CDN (`api.cfwidget.com` -> `edge.forgecdn.net` / `mediafilez.forgecdn.net`) 免登录直拉最新发行包；
        /// 2. 其余仓库优先走 GitHub Releases，若该仓库未挂预编译 `.zip` 且配置了 `CurseForgeSlug` 则自动兜底走 CurseForge CDN。
        /// </summary>
        private static (InstalledModInfo? Mod, string? Note) TryFetchRemotePrerequisite(
            string uniqueIdOrRepo,
            string? explicitGitHubRepo,
            string? assetHint)
        {
            var known = KnownFrameworks.FirstOrDefault(k =>
                k.UniqueId.Equals(uniqueIdOrRepo, StringComparison.OrdinalIgnoreCase) ||
                k.Alias.Equals(uniqueIdOrRepo, StringComparison.OrdinalIgnoreCase));

            bool isTagOnlyMonorepo = string.IsNullOrWhiteSpace(explicitGitHubRepo) &&
                !string.IsNullOrEmpty(known.GitHubRepo) &&
                TagOnlyGitHubMonorepos.Contains(known.GitHubRepo!);

            if (isTagOnlyMonorepo && !string.IsNullOrEmpty(known.CurseForgeSlug))
            {
                var cfRes = TryFetchFromCurseForgeCdn(known.CurseForgeSlug!, known.UniqueId);
                if (cfRes.Mod != null)
                    return cfRes;
            }

            var ghRes = TryFetchFromGitHubReleases(uniqueIdOrRepo, explicitGitHubRepo, assetHint);
            if (ghRes.Mod != null)
                return ghRes;

            if (!isTagOnlyMonorepo && !string.IsNullOrEmpty(known.CurseForgeSlug))
            {
                var cfFallback = TryFetchFromCurseForgeCdn(known.CurseForgeSlug!, known.UniqueId);
                if (cfFallback.Mod != null)
                    return cfFallback;
            }

            return ghRes;
        }

        private static (InstalledModInfo? Mod, string? Note) TryFetchFromCurseForgeCdn(string cfSlug, string expectedUniqueId)
        {
            try
            {
                using var http = CreateGitHubHttpClient();
                string metaUrl = $"https://api.cfwidget.com/stardewvalley/mods/{cfSlug}";
                var metaResp = http.GetAsync(metaUrl).GetAwaiter().GetResult();
                if (!metaResp.IsSuccessStatusCode)
                    return (null, $"CurseForge metadata lookup for `{cfSlug}` returned HTTP {(int)metaResp.StatusCode}.");

                string metaJson = metaResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(metaJson);
                if (!doc.RootElement.TryGetProperty("download", out var dlEl) || dlEl.ValueKind != JsonValueKind.Object)
                    return (null, $"CurseForge metadata for `{cfSlug}` did not include a latest `download` entry.");

                int fileId = dlEl.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out int parsedId) ? parsedId : 0;
                string? fileName = GetString(dlEl, "name");
                if (fileId <= 0 || string.IsNullOrWhiteSpace(fileName))
                    return (null, $"Invalid file descriptor in CurseForge metadata for `{cfSlug}`.");

                int hi = fileId / 1000;
                int lo = fileId % 1000;
                string escapedName = Uri.EscapeDataString(fileName!);
                string[] cdnUrls =
                {
                    $"https://edge.forgecdn.net/files/{hi}/{lo}/{escapedName}",
                    $"https://mediafilez.forgecdn.net/files/{hi}/{lo}/{escapedName}"
                };

                byte[]? zipBytes = null;
                string? usedUrl = null;
                foreach (var url in cdnUrls)
                {
                    try
                    {
                        var dlResp = http.GetAsync(url).GetAwaiter().GetResult();
                        if (dlResp.IsSuccessStatusCode)
                        {
                            zipBytes = dlResp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                            usedUrl = url;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                if (zipBytes == null || zipBytes.Length == 0)
                    return (null, $"Failed to download `{fileName}` (id={fileId}) from CurseForge CDN.");

                Directory.CreateDirectory(DepsCacheRoot);
                string tempZip = Path.Combine(Path.GetTempPath(), $"sdv-cf-{Guid.NewGuid():N}.zip");
                string extractTemp = Path.Combine(Path.GetTempPath(), $"sdv-cf-ext-{Guid.NewGuid():N}");
                try
                {
                    File.WriteAllBytes(tempZip, zipBytes);
                    ZipFile.ExtractToDirectory(tempZip, extractTemp);

                    var manifests = Directory.GetFiles(extractTemp, "manifest.json", SearchOption.AllDirectories);
                    InstalledModInfo? primary = null;
                    foreach (var mf in manifests)
                    {
                        var parsed = ParseManifestFile(mf, "deps-cache");
                        if (parsed == null)
                            continue;

                        string dstDir = Path.Combine(DepsCacheRoot, SanitizeFolderName(parsed.UniqueId));
                        if (Directory.Exists(dstDir))
                            Directory.Delete(dstDir, recursive: true);
                        CopyDirectoryAll(Path.GetDirectoryName(mf)!, dstDir);

                        var cachedInfo = ParseManifestFile(Path.Combine(dstDir, "manifest.json"), "deps-cache");
                        if (cachedInfo != null && (primary == null || cachedInfo.UniqueId.Equals(expectedUniqueId, StringComparison.OrdinalIgnoreCase)))
                            primary = cachedInfo;
                    }

                    if (primary != null)
                        return (primary, $"Downloaded `{fileName}` via CurseForge CDN (`{cfSlug}`) -> `{primary.UniqueId}` v{primary.Version} ({usedUrl}).");

                    return (null, $"Downloaded `{fileName}` (`{cfSlug}`), but no valid `manifest.json` was found inside.");
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    try { if (Directory.Exists(extractTemp)) Directory.Delete(extractTemp, recursive: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return (null, $"CurseForge CDN download failed for `{cfSlug}`: {ex.Message}");
            }
        }

        private static (InstalledModInfo? Mod, string? Note) TryFetchFromGitHubReleases(
            string uniqueIdOrRepo,
            string? explicitGitHubRepo,
            string? assetHint)
        {
            string? repo = explicitGitHubRepo;
            string? hint = assetHint;

            if (string.IsNullOrWhiteSpace(repo))
            {
                if (Regex.IsMatch(uniqueIdOrRepo, @"^[A-Za-z0-9_.\-]+/[A-Za-z0-9_.\-]+$"))
                {
                    repo = uniqueIdOrRepo;
                }
                else
                {
                    var known = KnownFrameworks.FirstOrDefault(k => k.UniqueId.Equals(uniqueIdOrRepo, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(known.GitHubRepo))
                    {
                        repo = known.GitHubRepo;
                        hint ??= known.AssetHint;
                    }
                    else
                    {
                        repo = TryLookupGitHubRepoFromSmapiList(uniqueIdOrRepo);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(repo))
                return (null, $"Prerequisite `{uniqueIdOrRepo}` not found in local Mods, and no known GitHub repository was mapped.");

            hint ??= uniqueIdOrRepo.Contains('.') ? uniqueIdOrRepo.Split('.').Last() : uniqueIdOrRepo;

            try
            {
                using var http = CreateGitHubHttpClient();
                string apiUrl = $"https://api.github.com/repos/{repo}/releases?per_page=25";
                var resp = http.GetAsync(apiUrl).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    return (null, $"GitHub Releases query for `{repo}` returned HTTP {(int)resp.StatusCode}.");

                string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return (null, $"Unexpected GitHub API response for `{repo}`.");

                string? bestZipUrl = null;
                string? bestAssetName = null;

                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var dr) && dr.ValueKind == JsonValueKind.True)
                        continue;
                    if (!rel.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                        continue;

                    var zipAssets = new List<(string Name, string Url)>();
                    foreach (var a in assets.EnumerateArray())
                    {
                        string? aName = GetString(a, "name");
                        string? aUrl = GetString(a, "browser_download_url");
                        if (!string.IsNullOrEmpty(aName) && !string.IsNullOrEmpty(aUrl) &&
                            aName!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            zipAssets.Add((aName!, aUrl!));
                        }
                    }

                    if (zipAssets.Count == 0)
                        continue;

                    // Prefer asset matching hint (essential for monorepos like Pathoschild/StardewMods!)
                    var matched = zipAssets.FirstOrDefault(z =>
                        z.Name.Replace(" ", "").Replace("-", "").Replace("_", "")
                            .IndexOf(hint!.Replace(" ", "").Replace("-", "").Replace("_", ""), StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrEmpty(matched.Url))
                    {
                        bestAssetName = matched.Name;
                        bestZipUrl = matched.Url;
                        break;
                    }

                    // Single-mod repo fallback if repo name itself matches hint
                    string repoShort = repo!.Split('/').Last();
                    if (zipAssets.Count == 1 &&
                        repoShort.Replace("-", "").IndexOf(hint!.Replace("-", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bestAssetName = zipAssets[0].Name;
                        bestZipUrl = zipAssets[0].Url;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(bestZipUrl))
                {
                    return (null, $"GitHub repo `{repo}` has no prebuilt Release `.zip` matching `{hint}` (source-only repo; install once into local `Mods` to use).");
                }

                Directory.CreateDirectory(DepsCacheRoot);
                string tempZip = Path.Combine(Path.GetTempPath(), $"sdv-dep-{Guid.NewGuid():N}.zip");
                string extractTemp = Path.Combine(Path.GetTempPath(), $"sdv-dep-ext-{Guid.NewGuid():N}");
                try
                {
                    byte[] zipBytes = http.GetByteArrayAsync(bestZipUrl).GetAwaiter().GetResult();
                    File.WriteAllBytes(tempZip, zipBytes);
                    ZipFile.ExtractToDirectory(tempZip, extractTemp);

                    var manifests = Directory.GetFiles(extractTemp, "manifest.json", SearchOption.AllDirectories);
                    InstalledModInfo? primary = null;
                    foreach (var mf in manifests)
                    {
                        var parsed = ParseManifestFile(mf, "deps-cache");
                        if (parsed == null)
                            continue;

                        string dstDir = Path.Combine(DepsCacheRoot, SanitizeFolderName(parsed.UniqueId));
                        if (Directory.Exists(dstDir))
                            Directory.Delete(dstDir, recursive: true);
                        CopyDirectoryAll(Path.GetDirectoryName(mf)!, dstDir);

                        var cachedInfo = ParseManifestFile(Path.Combine(dstDir, "manifest.json"), "deps-cache");
                        if (cachedInfo != null && (primary == null || cachedInfo.UniqueId.Equals(uniqueIdOrRepo, StringComparison.OrdinalIgnoreCase)))
                            primary = cachedInfo;
                    }

                    if (primary != null)
                        return (primary, $"Downloaded `{bestAssetName}` from GitHub `{repo}` -> `{primary.UniqueId}` v{primary.Version}.");

                    return (null, $"Downloaded `{bestAssetName}` from `{repo}`, but no valid `manifest.json` was found inside.");
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    try { if (Directory.Exists(extractTemp)) Directory.Delete(extractTemp, recursive: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return (null, $"GitHub download failed for `{repo}`: {ex.Message}");
            }
        }

        private static Dictionary<string, string>? _embeddedSmapiRepoMap;
        private static readonly object SmapiRepoMapLock = new object();

        private static Dictionary<string, string> GetEmbeddedSmapiRepoMap()
        {
            if (_embeddedSmapiRepoMap != null)
                return _embeddedSmapiRepoMap;

            lock (SmapiRepoMapLock)
            {
                if (_embeddedSmapiRepoMap != null)
                    return _embeddedSmapiRepoMap;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using Stream? s = typeof(ModDependencyResolver).Assembly.GetManifestResourceStream("SmapiGithubMap.tsv");
                    if (s != null)
                    {
                        using var sr = new StreamReader(s, Encoding.UTF8);
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            int tab = line.IndexOf('\t');
                            if (tab > 0 && tab < line.Length - 1)
                            {
                                dict[line.Substring(0, tab).Trim()] = line.Substring(tab + 1).Trim();
                            }
                        }
                    }
                }
                catch { }

                _embeddedSmapiRepoMap = dict;
                return dict;
            }
        }

        /// <summary>
        /// 从内嵌的 2,700+ 条官方 SMAPI 兼容表 (`smapi.io` / `SmapiCompatibilityList`) 精确反查模组的真实 GitHub 仓库，
        /// 解决 `PeacefulEnd.CustomCompanions -> Floogen/CustomCompanions` 等作者 ID 与 GitHub 用户名不一致导致猜仓库 404 的问题。
        /// </summary>
        public static string? TryLookupGitHubRepoFromSmapiList(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                return null;

            var embedded = GetEmbeddedSmapiRepoMap();
            if (embedded.TryGetValue(uniqueId.Trim(), out var bundledRepo))
                return bundledRepo;

            try
            {
                string cacheFile = Path.Combine(KbConfig.DataDir, "smapi-github-map.json");
                Dictionary<string, string>? map = null;
                if (File.Exists(cacheFile) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalDays < 14)
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cacheFile));
                    if (loaded != null && loaded.Count > 0)
                        map = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                }

                if (map == null)
                {
                    using var http = CreateGitHubHttpClient();
                    string raw = http.GetStringAsync("https://raw.githubusercontent.com/Pathoschild/SmapiCompatibilityList/develop/data/mods.jsonc").GetAwaiter().GetResult();
                    map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using var doc = JsonDocument.Parse(raw, JsoncReadOpts);
                    if (doc.RootElement.TryGetProperty("mods", out var modsArr) && modsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in modsArr.EnumerateArray())
                        {
                            string? gh = GetString(m, "github") ?? GetString(m, "githubRepo");
                            string? ids = GetString(m, "id");
                            if (string.IsNullOrWhiteSpace(gh) || string.IsNullOrWhiteSpace(ids))
                                continue;
                            foreach (var singleId in ids!.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                                map[singleId] = gh!.Trim();
                        }
                    }
                    if (map.Count > 0)
                    {
                        try { File.WriteAllText(cacheFile, JsonSerializer.Serialize(map), new UTF8Encoding(false)); } catch { }
                    }
                }

                if (map != null && map.TryGetValue(uniqueId.Trim(), out var foundRepo))
                    return foundRepo;
            }
            catch
            {
            }
            return null;
        }

        private static HttpClient CreateGitHubHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("stardew-devtools/1.6");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private static void CopyDirectoryAll(string srcDir, string dstDir)
        {
            Directory.CreateDirectory(dstDir);
            foreach (var f in Directory.GetFiles(srcDir))
            {
                File.Copy(f, Path.Combine(dstDir, Path.GetFileName(f)), overwrite: true);
            }
            foreach (var sub in Directory.GetDirectories(srcDir))
            {
                string name = Path.GetFileName(sub);
                if (name.StartsWith(".", StringComparison.Ordinal))
                    continue;
                CopyDirectoryAll(sub, Path.Combine(dstDir, name));
            }
        }

        private static string SanitizeFolderName(string raw) =>
            string.IsNullOrWhiteSpace(raw) ? "Mod" : Regex.Replace(raw.Trim(), @"[^a-zA-Z0-9._\- ]+", "_");

        private static string? GetString(JsonElement obj, string propName)
        {
            if (TryGetProp(obj, propName, out var el))
                return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            return null;
        }

        private static bool TryGetProp(JsonElement obj, string propName, out JsonElement val)
        {
            if (obj.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in obj.EnumerateObject())
                {
                    if (p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))
                    {
                        val = p.Value;
                        return true;
                    }
                }
            }
            val = default;
            return false;
        }
    }
}
