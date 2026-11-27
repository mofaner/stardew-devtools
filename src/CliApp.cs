using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SdvKb.Build;
using SdvKb.Database;
using SdvKb.Indexers;
using SdvKb.Live;
using SdvKb.Maps;
using SdvKb.Project;
using SdvKb.Validation;

namespace SdvKb
{
    public static class CliApp
    {
        public static int Run(string[] args, KbDatabase? sharedDb = null)
        {
            if (args.Length == 0 || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp(null);
                return 0;
            }

            if (string.Equals(args[0], "-v", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "version", StringComparison.OrdinalIgnoreCase))
            {
                string ver = typeof(CliApp).Assembly.GetName().Version?.ToString(3) ?? "1.3.0";
                Console.WriteLine($"sdv-dev {ver}");
                return 0;
            }

            string cmd = args[0].ToLowerInvariant();
            string[] rest = args.Skip(1).ToArray();

            if (cmd == "help")
            {
                PrintHelp(rest.Length > 0 ? rest[0] : null);
                return 0;
            }

            if (HasFlag(rest, "-h", "--help"))
            {
                PrintHelp(cmd);
                return 0;
            }

            ValidateNoUnknownGlobalFlags(cmd, rest);

            return cmd switch
            {
                // Knowledge & Inspection
                "sync" => RunSync(rest),
                "status" => RunStatus(rest, sharedDb),
                "api" => RunApiSearch(rest, sharedDb),
                "decompile" or "dec" => RunDecompile(rest, sharedDb),
                "schema" => RunSchema(rest, sharedDb),
                "data" => RunData(rest, sharedDb),
                "doc" or "docs" => RunDocs(rest, sharedDb),

                // Project Dependency Management, Scaffolding, Check, Build & Release Packaging
                "deps" or "dep" => RunDeps(rest),
                "new" or "init" => RunNewMod(rest),
                "gen-gmcm" or "gmcm" => RunGenGmcm(rest),
                "check" or "validate" => RunCheck(rest),
                "build" => RunBuild(rest),
                "shader" or "shader-compile" or "fx" => RunShaderCompile(rest),
                "pack" or "package" => RunPack(rest),

                // Offline & Live Map / Tilesheet / Sprite Toolchain
                "tilesheet" => RunTilesheet(rest),
                "sprite" or "sprite-check" => RunSprite(rest),
                "map-inspect" => RunMapInspect(rest),
                "map-render" => RunMapRender(rest),
                "map-prefab" => RunMapPrefab(rest),
                "map-extract" => RunMapExtract(rest),
                "map-stamp" => RunMapStamp(rest),
                "map-autotile" => RunMapAutotile(rest),
                "map-check" => RunMapCheck(rest),
                "map-find-space" => RunMapFindSpace(rest),

                // Live In-Game Automated Testing, Asset/Harmony Inspection & Multiplayer Closed Loop
                "live" or "game-status" => RunLiveStatus(),
                "window" or "win" => RunWindowControl(rest),
                "saves" => RunSaves(),
                "start" => RunStart(rest),
                "restart" => RunRestart(rest),
                "reload" => RunReload(rest),
                "reload-assets" => RunReloadAssets(rest),
                "harmony" or "harmony-inspect" => RunHarmony(rest),
                "asset" or "asset-inspect" => RunAsset(rest),
                "event-play" or "event" => RunEventPlay(rest),
                "world" => RunWorld(rest),
                "interact" => RunInteract(rest),
                "profile" => RunProfile(rest),
                "checkpoint" or "cp-save" => RunCheckpoint(rest),
                "schedule-check" or "schedule" => RunScheduleCheck(rest),
                "mp" or "multiplayer" => RunMultiplayer(rest),
                "warp" => RunWarp(rest),
                "screenshot" => RunScreenshot(rest),
                "config" or "mod-config" => RunModConfig(rest),
                "stop" => RunStop(rest),
                "menu" => RunMenu(),
                "eval" => RunEval(rest),
                "cmd" => RunConsoleCmd(rest),
                "player" => RunPlayer(rest),
                "ui" => RunUi(rest),
                "ui-click" => RunUiClick(rest),
                "input" or "key" or "keys" or "gamepad" => RunInput(rest),
                "logs" => RunLogs(rest),

                _ => UnknownCommand(cmd)
            };
        }

        private static T WithDatabase<T>(KbConfig cfg, KbDatabase? sharedDb, Func<KbDatabase, T> fn)
        {
            if (sharedDb != null &&
                Path.GetFullPath(cfg.DbPath).Equals(Path.GetFullPath(sharedDb.DbPath), StringComparison.OrdinalIgnoreCase))
            {
                return fn(sharedDb);
            }

            EnsureDbInitialized(cfg);
            using var db = new KbDatabase(cfg.DbPath);
            return fn(db);
        }

        #region Knowledge & Inspection Commands

        private static int RunSync(string[] args)
        {
            string? overrideGame = GetOption(args, "--game-path", "-g");
            string? overrideDb = GetOption(args, "--db");
            string? docsOutDir = GetOption(args, "--docs-out");
            bool unpack = HasFlag(args, "--unpack");
            bool offline = HasFlag(args, "--offline");
            bool docsOnly = HasFlag(args, "--docs-only");

            var cfg = KbConfig.Load(overrideGame, overrideDb);
            if (string.IsNullOrWhiteSpace(cfg.GamePath) || !Directory.Exists(cfg.GamePath))
            {
                Console.Error.WriteLine("[ERROR] Could not locate Stardew Valley folder. Pass `--game-path \"D:\\path\\to\\Stardew Valley\"`.");
                return 1;
            }

            // Persist detected/explicit GamePath, but NEVER overwrite global config.json with a transient `--db` or co-located DB path!
            cfg.Save(persistDbPath: false);
            DecompileService.ClearCache();

            if (string.IsNullOrWhiteSpace(docsOutDir))
            {
                string repoDocsCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "docs"));
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "StardewDevtools.sln")))
                    docsOutDir = repoDocsCandidate;
                else
                    docsOutDir = Path.Combine(KbConfig.DataDir, "docs");
            }

            string customDocsDir = Path.Combine(docsOutDir!, "custom");
            Directory.CreateDirectory(customDocsDir);

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"Game Path : {cfg.GamePath}");
            Console.WriteLine($"Database  : {cfg.DbPath}");
            Console.WriteLine($"Docs Dir  : {docsOutDir}");
            Console.WriteLine();

            using var db = new KbDatabase(cfg.DbPath);
            var engineDocs = new List<DocRecord>();

            if (!docsOnly)
            {
                Console.Write("[1/4] Indexing game & SMAPI DLLs + XML docs + 1.6 GameData schemas... ");
                var asmResult = AssemblyApiIndexer.IndexGameAssemblies(cfg.GamePath);
                db.ReplaceApiAndSchemas(asmResult.ApiRecords, asmResult.SchemaRecords);
                engineDocs = asmResult.EngineDocs;
                DocsSyncService.MirrorEngineDocs(engineDocs, docsOutDir);

                if (!string.IsNullOrEmpty(asmResult.GameVersion))
                    db.SetMeta("game_version", asmResult.GameVersion);
                if (!string.IsNullOrEmpty(asmResult.SmapiVersion))
                    db.SetMeta("smapi_version", asmResult.SmapiVersion);
                Console.WriteLine($"OK ({asmResult.ApiRecords.Count} APIs, {asmResult.SchemaRecords.Count} Schemas, {engineDocs.Count} Engine doc chunks)");

                Console.Write("[2/4] Indexing unpacked vanilla Content (Data + zh-CN/en Strings + XACT Audio Cues)... ");
                VanillaDataIndexer.EnsureUnpacked(cfg.GamePath, runUnpackerIfNeeded: unpack);
                var dataRecords = VanillaDataIndexer.IndexUnpackedContent(cfg.GamePath);
                if (dataRecords.Count > 0)
                {
                    db.ReplaceVanillaData(dataRecords);
                    Console.WriteLine($"OK ({dataRecords.Count} vanilla data & audio cue entries)");
                }
                else
                {
                    Console.WriteLine("SKIPPED (Content (unpacked) not found; pass `--unpack` to run StardewXnbHack)");
                }
            }

            Console.Write("[3/4] Building offline curated guides & local custom docs... ");
            var curatedDocs = DocsSyncService.BuildOfflineCuratedDocs(customDocsDir, docsOutDir);
            var allDocsToUpsert = new List<DocRecord>();
            var sourcesToReplace = new List<string> { "builtin-guide", "local" };
            if (curatedDocs.Any(d => d.Source == "wiki-dump"))
            {
                sourcesToReplace.Add("wiki-dump");
            }
            if (!docsOnly)
            {
                sourcesToReplace.Add("builtin-dll");
                allDocsToUpsert.AddRange(engineDocs);
            }
            allDocsToUpsert.AddRange(curatedDocs);
            Console.WriteLine($"OK ({curatedDocs.Count} sections)");

            if (!offline)
            {
                Console.Write("[4/4] Fetching remote GitHub Markdown (Content Patcher/SMAPI)... ");
                var remote = DocsSyncService.FetchRemoteDocsAsync(docsOutDir).GetAwaiter().GetResult();
                if (remote.Docs.Count > 0)
                {
                    sourcesToReplace.AddRange(new[] { "github", "wiki-en", "wiki-zh" });
                    allDocsToUpsert.AddRange(remote.Docs);
                }
                Console.WriteLine($"OK (fetched {remote.OkCount} pages -> {remote.Docs.Count} sections, skipped {remote.SkipCount})");
            }
            else
            {
                Console.WriteLine("[4/4] Remote doc fetch SKIPPED (--offline)");
            }

            db.UpsertDocsBySources(sourcesToReplace.Distinct(), allDocsToUpsert);
            db.SetMeta("last_sync", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            db.SetMeta("game_path", cfg.GamePath);
            db.SetMeta("docs_dir", docsOutDir!);
            db.CheckpointForPortableBundle();

            sw.Stop();
            var counts = db.GetCounts();
            Console.WriteLine();
            Console.WriteLine($"Sync completed in {sw.Elapsed.TotalSeconds:F1}s!");
            Console.WriteLine($"Total Indexed => APIs: {counts.ApiCount} | Schemas: {counts.SchemaCount} | Vanilla Data: {counts.DataCount} | Docs: {counts.DocCount}");
            return 0;
        }

        private static int RunStatus(string[] args, KbDatabase? sharedDb)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"), GetOption(args, "--db"));
            Console.WriteLine($"Runtime     : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"C# Compiler : Built-in Roslyn Memory Compiler (Zero VS / SDK dependency)");
            Console.WriteLine($"Config File : {KbConfig.ConfigFilePath}");
            Console.WriteLine($"Database    : {cfg.DbPath} (Exists: {File.Exists(cfg.DbPath)})");
            Console.WriteLine($"Game Path   : {cfg.GamePath} (Exists: {Directory.Exists(cfg.GamePath)})");

            if (!File.Exists(cfg.DbPath))
            {
                Console.WriteLine("Database not initialized yet. Run `sdv-dev sync` first.");
                return 0;
            }

            return WithDatabase(cfg, sharedDb, db =>
            {
                var meta = db.GetAllMeta();
                var counts = db.GetCounts();

                foreach (var kv in meta)
                    Console.WriteLine($"Meta.{kv.Key,-14}: {kv.Value}");

                Console.WriteLine();
                Console.WriteLine($"API Symbols  : {counts.ApiCount}");
                Console.WriteLine($"Data Schemas : {counts.SchemaCount}");
                Console.WriteLine($"Vanilla Data : {counts.DataCount}");
                Console.WriteLine($"Doc Sections : {counts.DocCount}");
                return 0;
            });
        }

        private static int RunApiSearch(string[] args, KbDatabase? sharedDb)
        {
            string? kind = GetOption(args, "--kind", "-k");
            string? asm = GetOption(args, "--asm", "-a");
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: 15);
            bool asJson = HasFlag(args, "--json");
            string query = GetPositionalQuery(args, "--kind", "-k", "--asm", "-a", "--limit", "-n", "--db");

            if (string.IsNullOrWhiteSpace(query))
            {
                PrintHelp("api");
                return 1;
            }

            if (asm != null)
            {
                asm = asm.ToLowerInvariant() switch
                {
                    "sdv" or "game" => "Stardew Valley",
                    "smapi" => "StardewModdingAPI",
                    "gamedata" => "StardewValley.GameData",
                    "monogame" or "mg" or "xna" => "MonoGame.Framework",
                    "xtile" => "xTile",
                    "harmony" => "0Harmony",
                    "toolkit" => "SMAPI.Toolkit",
                    _ => asm
                };
            }

            var cfg = KbConfig.Load(overrideDbPath: GetOption(args, "--db"));
            return WithDatabase(cfg, sharedDb, db =>
            {
                var hits = db.SearchApi(query, kind, asm, limit);

                if (asJson)
                {
                    PrintJson(hits);
                    return 0;
                }

                if (hits.Count == 0)
                {
                    Console.WriteLine($"No API symbols matched `{query}`.");
                    return 0;
                }

                Console.WriteLine($"Found {hits.Count} API match(es) for `{query}`:\n");
                foreach (var h in hits)
                {
                    Console.WriteLine($"### [{h.Assembly}] ({h.Kind}) `{h.FullTypeName}{(string.IsNullOrEmpty(h.MemberName) ? "" : "." + h.MemberName)}`");
                    Console.WriteLine($"- **Signature**: `{h.Signature}`");
                    if (!string.IsNullOrWhiteSpace(h.Summary))
                        Console.WriteLine($"- **Doc**: {h.Summary}");
                    Console.WriteLine();
                }
                return 0;
            });
        }

        private static int RunDecompile(string[] args, KbDatabase? sharedDb)
        {
            int lines = GetIntOption(args, "--lines", "-n", defaultValue: 200);
            bool full = HasFlag(args, "--full");
            string? overrideGame = GetOption(args, "--game-path", "-g");
            string? grep = GetOption(args, "--grep", "--search");
            int grepCtx = GetIntOption(args, "-C", "--context", defaultValue: 15);
            string target = GetPositionalQuery(args, "--lines", "-n", "--game-path", "-g", "--grep", "--search", "-C", "--context");

            if (string.IsNullOrWhiteSpace(target))
            {
                PrintHelp("decompile");
                return 1;
            }

            var cfg = KbConfig.Load(overrideGame);
            if (string.IsNullOrWhiteSpace(cfg.GamePath) || !Directory.Exists(cfg.GamePath))
            {
                Console.Error.WriteLine("[ERROR] Stardew Valley game folder not found.");
                return 1;
            }

            if (sharedDb != null)
            {
                string code = DecompileService.DecompileTarget(cfg.GamePath, target, lines, full, sharedDb, grep, grepCtx);
                Console.WriteLine(code);
                return 0;
            }

            KbDatabase? localDb = File.Exists(cfg.DbPath) ? new KbDatabase(cfg.DbPath) : null;
            using (localDb)
            {
                string code = DecompileService.DecompileTarget(cfg.GamePath, target, lines, full, localDb, grep, grepCtx);
                Console.WriteLine(code);
                return 0;
            }
        }

        private static int RunSchema(string[] args, KbDatabase? sharedDb)
        {
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: 5);
            bool listOnly = HasFlag(args, "--list");
            bool asJson = HasFlag(args, "--json");
            string query = GetPositionalQuery(args, "--limit", "-n", "--db");

            var cfg = KbConfig.Load(overrideDbPath: GetOption(args, "--db"));
            return WithDatabase(cfg, sharedDb, db =>
            {
                if (listOnly || string.IsNullOrWhiteSpace(query))
                {
                    var all = db.SearchSchemas(string.Empty, listOnly ? 300 : 40);
                    Console.WriteLine("Stardew Valley 1.6 `GameData` Schemas (use `sdv-dev schema <TypeOrAsset>` for full field table):\n");
                    foreach (var s in all)
                    {
                        string assetTag = string.IsNullOrEmpty(s.AssetPath) ? "" : $" [{s.AssetPath}]";
                        string brief = s.Summary.Length > 90 ? s.Summary.Substring(0, 90) + "..." : s.Summary;
                        Console.WriteLine($"- `{s.TypeName}`{assetTag} ({s.Kind}) — {brief}");
                    }
                    return 0;
                }

                var hits = db.SearchSchemas(query, limit);
                if (asJson)
                {
                    PrintJson(hits);
                    return 0;
                }

                if (hits.Count == 0)
                {
                    Console.WriteLine($"No GameData schema matched `{query}`.");
                    return 0;
                }

                foreach (var s in hits)
                {
                    Console.WriteLine($"## `{s.FullTypeName}` ({s.Kind})");
                    if (!string.IsNullOrEmpty(s.AssetPath))
                        Console.WriteLine($"- **Target Asset Path**: `{s.AssetPath}`");
                    if (!string.IsNullOrEmpty(s.Summary))
                        Console.WriteLine($"- **Description**: {s.Summary}");
                    Console.WriteLine();

                    var fields = JsonSerializer.Deserialize<List<SchemaFieldInfo>>(s.FieldsJson) ?? new List<SchemaFieldInfo>();
                    if (fields.Count > 0)
                    {
                        Console.WriteLine("| Field / Property | C# Type | Official XML Description |");
                        Console.WriteLine("| :--- | :--- | :--- |");
                        foreach (var f in fields)
                        {
                            string cleanDesc = f.Summary.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
                            Console.WriteLine($"| `{f.Name}` | `{f.Type}` | {cleanDesc} |");
                        }
                    }
                    Console.WriteLine();
                }
                return 0;
            });
        }

        private static int RunData(string[] args, KbDatabase? sharedDb)
        {
            string? assetFilter = GetOption(args, "--asset", "-a");
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: 5);
            bool listAssets = HasFlag(args, "--list-assets", "--list");
            bool asJson = HasFlag(args, "--json");
            string query = GetPositionalQuery(args, "--asset", "-a", "--limit", "-n", "--db");

            var cfg = KbConfig.Load(overrideDbPath: GetOption(args, "--db"));
            return WithDatabase(cfg, sharedDb, db =>
            {
                if (listAssets)
                {
                    var assets = db.ListDataAssets();
                    Console.WriteLine($"Indexed Vanilla Data Assets ({assets.Count}):\n");
                    foreach (var a in assets)
                        Console.WriteLine($"- `{a}`");
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(assetFilter))
                {
                    PrintHelp("data");
                    return 1;
                }

                var hits = db.SearchVanillaData(query, assetFilter, limit);
                if (asJson)
                {
                    PrintJson(hits);
                    return 0;
                }

                if (hits.Count == 0)
                {
                    Console.WriteLine($"No vanilla data entries matched `{query}`{(assetFilter != null ? $" in `{assetFilter}`" : "")}.");
                    return 0;
                }

                foreach (var h in hits)
                {
                    string names = !string.IsNullOrEmpty(h.ZhName) && !string.IsNullOrEmpty(h.EnName)
                        ? $"{h.ZhName} / {h.EnName}"
                        : (!string.IsNullOrEmpty(h.ZhName) ? h.ZhName : h.EnName);

                    Console.WriteLine($"### [{h.AssetName}] Key: `{h.EntryKey}`{(string.IsNullOrEmpty(names) ? "" : $" — **{names}**")}");
                    if (!string.IsNullOrWhiteSpace(h.Summary))
                        Console.WriteLine($"> {h.Summary}");
                    Console.WriteLine("```json");
                    Console.WriteLine(h.RawJson);
                    Console.WriteLine("```");
                    Console.WriteLine();
                }
                return 0;
            });
        }

        private static int RunDocs(string[] args, KbDatabase? sharedDb)
        {
            string? cat = GetOption(args, "--cat", "-c");
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: 4);
            bool listOnly = HasFlag(args, "--list");
            bool asJson = HasFlag(args, "--json");
            string query = GetPositionalQuery(args, "--cat", "-c", "--limit", "-n", "--db");

            var cfg = KbConfig.Load(overrideDbPath: GetOption(args, "--db"));
            return WithDatabase(cfg, sharedDb, db =>
            {
                if (listOnly || (string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(cat)))
                {
                    var docs = db.SearchDocs(string.Empty, cat, listOnly ? 200 : limit);
                    Console.WriteLine($"Documentation Sections ({docs.Count}):\n");
                    foreach (var d in docs)
                        Console.WriteLine($"- **[{d.Category}]** {d.Title}  *(Source: {d.UrlOrPath})*");
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    PrintHelp("doc");
                    return 1;
                }

                var hits = db.SearchDocs(query, cat, limit);
                if (asJson)
                {
                    PrintJson(hits);
                    return 0;
                }

                if (hits.Count == 0)
                {
                    Console.WriteLine($"No documentation sections matched `{query}`.");
                    return 0;
                }

                for (int i = 0; i < hits.Count; i++)
                {
                    var d = hits[i];
                    Console.WriteLine($"## [{i + 1}/{hits.Count}] [{d.Category}] {d.Title}");
                    Console.WriteLine(d.Content);
                    Console.WriteLine("\n---\n");
                }
                return 0;
            });
        }

        #endregion

        #region Project Dependency Management, Scaffolding, Check & Build

        private static int RunDeps(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string project = GetOption(args, "--project", "-p") ?? Directory.GetCurrentDirectory();
            string sub = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal)
                ? args[0].ToLowerInvariant()
                : "resolve";
            string[] subRest = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal)
                ? args.Skip(1).ToArray()
                : args;

            switch (sub)
            {
                case "list":
                case "ls":
                {
                    string filter = GetPositionalQuery(subRest, "--game-path", "-g", "--project", "-p");
                    var availDict = ModDependencyResolver.ScanAvailableMods(cfg.GamePath);
                    string expanded = string.IsNullOrWhiteSpace(filter) ? "" : ModDependencyResolver.ExpandQueryAlias(filter, availDict);
                    var all = availDict.Values
                        .Where(m => string.IsNullOrWhiteSpace(filter) ||
                                    m.UniqueId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (!string.IsNullOrEmpty(expanded) && m.UniqueId.IndexOf(expanded, StringComparison.OrdinalIgnoreCase) >= 0))
                        .OrderBy(m => m.UniqueId, StringComparer.OrdinalIgnoreCase)
                        .Select(m => new
                        {
                            uniqueId = m.UniqueId,
                            name = m.Name,
                            version = m.Version,
                            hasDll = !string.IsNullOrEmpty(m.EntryDll),
                            source = m.Source,
                            directory = m.DirectoryPath
                        })
                        .ToList();
                    PrintJson(new { ok = true, count = all.Count, mods = all });
                    return 0;
                }

                case "add":
                {
                    string target = GetPositionalQuery(subRest, "--project", "-p", "--game-path", "-g", "--github", "--min-version");
                    if (string.IsNullOrWhiteSpace(target))
                    {
                        PrintHelp("deps");
                        return 1;
                    }
                    bool optional = HasFlag(subRest, "--optional");
                    bool noRef = HasFlag(subRest, "--no-ref");
                    string? gh = GetOption(subRest, "--github");
                    string? minVer = GetOption(subRest, "--min-version");

                    var res = ModDependencyResolver.AddDependencyToProject(
                        project,
                        target,
                        cfg.GamePath,
                        isRequired: !optional,
                        referenceDll: !noRef,
                        explicitGitHubRepo: gh,
                        minVersionOverride: minVer);
                    PrintJson(res);
                    return 0;
                }

                case "remove":
                case "rm":
                {
                    string target = GetPositionalQuery(subRest, "--project", "-p", "--game-path", "-g");
                    if (string.IsNullOrWhiteSpace(target))
                    {
                        PrintHelp("deps");
                        return 1;
                    }
                    PrintJson(ModDependencyResolver.RemoveDependencyFromProject(project, target, cfg.GamePath));
                    return 0;
                }

                case "api":
                {
                    int maxLines = GetIntOption(subRest, "--lines", "-n", 260);
                    var pos = GetPositionalList(subRest, "--project", "-p", "--game-path", "-g", "--lines", "-n");
                    if (pos.Count == 0)
                    {
                        PrintHelp("deps");
                        return 1;
                    }
                    string modQuery = pos[0];
                    string? typeFilter = pos.Count > 1 ? string.Join(".", pos.Skip(1)) : null;
                    Console.WriteLine(ModDependencyResolver.InspectModProvidedApi(modQuery, cfg.GamePath, typeFilter, maxLines));
                    return 0;
                }

                case "isolated":
                {
                    var projCfg = ModDependencyResolver.LoadProjectConfig(project);
                    if (HasFlag(subRest, "--off", "off", "false"))
                        projCfg.Isolated = false;
                    else
                        projCfg.Isolated = true;
                    ModDependencyResolver.SaveProjectConfig(project, projCfg);
                    PrintJson(new { ok = true, projectDir = Path.GetFullPath(project), isolatedDefault = projCfg.Isolated });
                    return 0;
                }

                case "resolve":
                case "restore":
                case "status":
                default:
                {
                    bool offline = HasFlag(subRest, "--offline");
                    var resolved = ModDependencyResolver.ResolveProjectDependencies(project, cfg.GamePath, allowGitHubDownload: !offline);
                    PrintJson(new
                    {
                        ok = resolved.MissingRequired.Count == 0,
                        projectDir = resolved.ProjectDir,
                        modUniqueId = resolved.ModUniqueId,
                        isolatedDefault = resolved.IsolatedDefault,
                        resolvedPrerequisites = resolved.ResolvedMods.Select(m => new
                        {
                            uniqueId = m.UniqueId,
                            name = m.Name,
                            version = m.Version,
                            source = m.Source,
                            entryDll = m.EntryDllFullPath
                        }).ToList(),
                        compileReferenceDlls = resolved.CompileReferenceDlls,
                        missingRequired = resolved.MissingRequired,
                        missingOptional = resolved.MissingOptional,
                        notes = resolved.Notes
                    });
                    return resolved.MissingRequired.Count == 0 ? 0 : 1;
                }
            }
        }

        private static int RunNewMod(string[] args)
        {
            string? name = GetOption(args, "--name", "-n");
            string author = GetOption(args, "--author", "-a") ?? "DevAuthor";
            string? outDir = GetOption(args, "--out", "-o");
            string type = GetPositionalQuery(args, "--name", "-n", "--author", "-a", "--out", "-o");

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                PrintHelp("new");
                return 1;
            }

            string created = ModScaffolder.CreateModProject(type, name!, author, outDir);
            PrintJson(new { ok = true, template = type, projectDir = created });
            return 0;
        }

        private static int RunCheck(string[] args)
        {
            bool asJson = HasFlag(args, "--json");
            string? overrideGame = GetOption(args, "--game-path", "-g");
            string modPath = GetOption(args, "--project", "-p") ?? GetPositionalQuery(args, "--project", "-p", "--game-path", "-g");
            if (string.IsNullOrWhiteSpace(modPath))
                modPath = Directory.GetCurrentDirectory();

            var issues = ModValidator.ValidateModDirectory(modPath, overrideGame);
            if (asJson)
            {
                PrintJson(issues);
                return issues.Any(i => i.Severity == "ERROR") ? 1 : 0;
            }

            int errCount = issues.Count(i => i.Severity == "ERROR");
            int warnCount = issues.Count(i => i.Severity == "WARN");

            if (issues.Count == 0)
            {
                Console.WriteLine($"[PASS] `{modPath}` passed all static & 1.6 semantic checks (0 errors, 0 warnings).");
                return 0;
            }

            foreach (var issue in issues)
                Console.WriteLine($"[{issue.Severity}] {issue.File}: {issue.Message}");

            Console.WriteLine($"\nSummary: {errCount} error(s), {warnCount} warning(s).");
            return errCount > 0 ? 1 : 0;
        }

        private static int RunBuild(string[] args)
        {
            bool deploy = HasFlag(args, "--deploy", "-d");
            bool deployBoth = HasFlag(args, "--both", "--all-mods");
            bool singleTargetOnly = HasFlag(args, "--single-target");
            bool noPublicize = HasFlag(args, "--no-publicize");
            string? explicitModsRoot = GetOption(args, "--mods-path");
            string modPath = GetOption(args, "--project", "-p") ?? GetPositionalQuery(args, "--project", "-p", "--game-path", "-g", "--mods-path");
            if (string.IsNullOrWhiteSpace(modPath))
                modPath = Directory.GetCurrentDirectory();

            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));

            string? targetModsRoot = explicitModsRoot;
            var extraDeployRoots = new List<string>();

            if (deploy)
            {
                string fullModDir = Path.GetFullPath(modPath);
                string mf = Path.Combine(fullModDir, "manifest.json");
                string? uid = File.Exists(mf) ? ModDependencyResolver.ParseManifestFile(mf)?.UniqueId : null;
                string fallbackFolder = Path.GetFileName(fullModDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                string gameModsDir = Path.Combine(cfg.GamePath, "Mods");
                string isoModsDir = ModDependencyResolver.DefaultIsolatedModsRoot;
                string activeModsDir = LiveGameController.GetActiveModsRoot(cfg.GamePath);

                if (!string.IsNullOrWhiteSpace(explicitModsRoot))
                {
                    targetModsRoot = explicitModsRoot;
                    if (deployBoth)
                    {
                        extraDeployRoots.Add(gameModsDir);
                        extraDeployRoots.Add(isoModsDir);
                    }
                }
                else if (singleTargetOnly)
                {
                    targetModsRoot = activeModsDir;
                }
                else
                {
                    // 智能双端同步：
                    // 1. 始终刷入当前活跃目录 `activeModsDir`（无隔离游戏运行时即真实 `GamePath\Mods`）；
                    // 2. 若传了 `--both`，或另一端目录（真实 `Mods` / `isolated-mods`）已存在该模组目录，自动把另一端也无锁增量同步，
                    //    既免去手写 `Copy-Item` 双边拷 `dll`/`assets`/`i18n`，又不会把纯临时隔离测试模组污染进玩家真实 `Mods\`！
                    targetModsRoot = activeModsDir;

                    bool activeIsIso = !Path.GetFullPath(activeModsDir).TrimEnd('\\', '/')
                        .Equals(Path.GetFullPath(gameModsDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

                    if (activeIsIso)
                    {
                        if (deployBoth || (!string.IsNullOrWhiteSpace(uid) && DotNet6ModCompiler.ModsRootContainsMod(gameModsDir, uid!, fallbackFolder)))
                            extraDeployRoots.Add(gameModsDir);
                    }
                    else
                    {
                        if (deployBoth || (!string.IsNullOrWhiteSpace(uid) && DotNet6ModCompiler.ModsRootContainsMod(isoModsDir, uid!, fallbackFolder)))
                            extraDeployRoots.Add(isoModsDir);
                    }
                }
            }

            var res = DotNet6ModCompiler.BuildAndOptionallyDeploy(
                modPath,
                cfg.GamePath,
                deploy,
                targetModsRoot,
                publicize: !noPublicize,
                additionalDeployRoots: extraDeployRoots);
            PrintJson(res);
            return res.Success ? 0 : 1;
        }

        private static int RunShaderCompile(string[] args)
        {
            string? outPath = GetOption(args, "--out", "-o");
            string profile = GetOption(args, "--profile") ?? "OpenGL";
            string? defines = GetOption(args, "--defines");
            bool debug = HasFlag(args, "--debug");
            string target = GetOption(args, "--project", "-p")
                ?? GetPositionalQuery(args, "--out", "-o", "--profile", "--defines", "--project", "-p");

            if (string.IsNullOrWhiteSpace(target))
            {
                PrintHelp("shader");
                return 1;
            }

            if (Directory.Exists(target))
            {
                var (allOk, compiled, errs) = ShaderCompiler.CompileAllInProject(target);
                PrintJson(new
                {
                    ok = allOk,
                    projectDir = Path.GetFullPath(target),
                    compiledCount = compiled.Count,
                    compiledShaders = compiled,
                    errors = errs
                });
                return allOk ? 0 : 1;
            }

            var single = ShaderCompiler.CompileShaderFile(target, outPath, profile, defines, debug);
            PrintJson(single);
            return single.Success ? 0 : 1;
        }

        private static int RunGenGmcm(string[] args)
        {
            string? configFile = GetOption(args, "--config", "-c");
            string modPath = GetOption(args, "--project", "-p") ?? GetPositionalQuery(args, "--project", "-p", "--config", "-c");
            if (string.IsNullOrWhiteSpace(modPath))
                modPath = Directory.GetCurrentDirectory();

            var res = GmcmGenerator.GenerateForProject(modPath, configFile);
            PrintJson(res);
            return 0;
        }

        private static int RunPack(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outZip = GetOption(args, "--out", "-o");
            bool skipCheck = HasFlag(args, "--skip-check");
            string modPath = GetOption(args, "--project", "-p") ?? GetPositionalQuery(args, "--project", "-p", "--game-path", "-g", "--out", "-o");
            if (string.IsNullOrWhiteSpace(modPath))
                modPath = Directory.GetCurrentDirectory();

            var res = ModPackager.PackageMod(modPath, cfg.GamePath, outZip, skipCheck);
            PrintJson(res);
            return res.Ok ? 0 : 1;
        }

        private static int RunSprite(string[] args)
        {
            string[] valOpts = { "--game-path", "-g", "--out", "-o", "--preset", "--mode", "-m", "--cell", "-c", "--grid", "--zoom", "-z" };
            EnsureCommandOptions("sprite", args, valOpts, "--no-bbox");

            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outPath = GetOption(args, "--out", "-o");
            string? preset = GetOption(args, "--preset", "--mode", "-m");
            string? cellStr = GetOption(args, "--cell", "-c", "--grid");
            string? zoomStr = GetOption(args, "--zoom", "-z");
            bool noBbox = HasFlag(args, "--no-bbox");
            string target = GetPositionalQuery(args, valOpts);

            if (string.IsNullOrWhiteSpace(target))
            {
                PrintHelp("sprite");
                return 1;
            }

            (int W, int H)? cell = null;
            if (!string.IsNullOrWhiteSpace(cellStr))
            {
                var p = cellStr!.ToLowerInvariant().Split(new[] { 'x', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 2 && int.TryParse(p[0], out int cw) && int.TryParse(p[1], out int ch))
                    cell = (cw, ch);
            }

            int? zoom = int.TryParse(zoomStr, out int zv) && zv > 0 ? zv : (int?)null;
            PrintJson(SpriteSheetInspector.InspectAndRenderSpriteSheet(target, cfg.GamePath, outPath, preset, cell, zoom, !noBbox));
            return 0;
        }

        #endregion

        #region Offline & Live Map / Tilesheet Toolchain Commands

        private static int RunTilesheet(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outPath = GetOption(args, "--out", "-o");
            bool grid = HasFlag(args, "--grid");
            int tileSize = GetIntOption(args, "--tile-size", "-s", 16);
            string? zoomStr = GetOption(args, "--zoom", "-z");
            int? zoom = int.TryParse(zoomStr, out int zv) && zv > 0 ? zv : (int?)null;
            string sheet = GetPositionalQuery(args, "--game-path", "-g", "--out", "-o", "--tile-size", "-s", "--zoom", "-z");

            if (string.IsNullOrWhiteSpace(sheet) || string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("tilesheet");
                return 1;
            }

            PrintJson(MapInspectorAndRenderer.ExportTilesheetPng(sheet, cfg.GamePath, outPath!, grid, tileSize, zoom));
            return 0;
        }

        private static int RunMapInspect(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? rectStr = GetOption(args, "--rect", "-r");
            string? layer = GetOption(args, "--layer", "-l");
            bool full = HasFlag(args, "--full");
            string mapName = GetPositionalQuery(args, "--game-path", "-g", "--rect", "-r", "--layer", "-l");

            if (string.IsNullOrWhiteSpace(mapName))
            {
                PrintHelp("map-inspect");
                return 1;
            }

            (int X, int Y, int W, int H)? rect = ParseRect4(rectStr);
            PrintJson(MapInspectorAndRenderer.InspectMap(mapName, cfg.GamePath, rect, layer, includeFullMapMeta: full));
            return 0;
        }

        private static int RunMapRender(string[] args)
        {
            string[] valOpts = { "--game-path", "-g", "--out", "-o", "--season", "-s", "--zoom", "-z", "--cp", "--target", "-t", "--crop", "--rect", "-r" };
            EnsureCommandOptions("map-render", args, valOpts, "--debug-overlay", "--debug", "--show-paths", "--auto-crop");

            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outPath = GetOption(args, "--out", "-o");
            string? season = GetOption(args, "--season", "-s");
            string? cpMod = GetOption(args, "--cp");
            string? targetOpt = GetOption(args, "--target", "-t");
            string? cropStr = GetOption(args, "--crop", "--rect", "-r");
            bool autoCrop = HasFlag(args, "--auto-crop") || string.Equals(cropStr, "auto", StringComparison.OrdinalIgnoreCase);
            bool debugOverlay = HasFlag(args, "--debug-overlay", "--debug");
            bool showPaths = HasFlag(args, "--show-paths");
            int zoom = GetIntOption(args, "--zoom", "-z", defaultValue: (debugOverlay || autoCrop || cropStr != null) ? 2 : 1);
            string mapTarget = !string.IsNullOrWhiteSpace(targetOpt)
                ? targetOpt!
                : GetPositionalQuery(args, valOpts);

            if (string.IsNullOrWhiteSpace(mapTarget) || string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("map-render");
                return 1;
            }

            var cropRect = (!string.IsNullOrWhiteSpace(cropStr) && !autoCrop) ? ParseRect4(cropStr) : null;
            PrintJson(MapInspectorAndRenderer.RenderMapToPng(
                mapTarget,
                cfg.GamePath,
                outPath!,
                season,
                debugOverlay,
                zoom,
                showPaths,
                cpModOrJson: cpMod,
                cropRect: cropRect,
                autoCropPatched: autoCrop));
            return 0;
        }

        private static int RunMapPrefab(string[] args)
        {
            PrintJson(new
            {
                ok = true,
                builtinPrefabs = MapPrefabAndAutotile.ListBuiltinPrefabCatalog()
            });
            return 0;
        }

        private static int RunMapExtract(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? rectStr = GetOption(args, "--rect", "-r");
            string? outPath = GetOption(args, "--out", "-o");
            string? name = GetOption(args, "--name", "-n");
            bool includeBack = HasFlag(args, "--include-back");
            string mapTarget = GetPositionalQuery(args, "--game-path", "-g", "--rect", "-r", "--out", "-o", "--name", "-n");

            var rect = ParseRect4(rectStr);
            if (string.IsNullOrWhiteSpace(mapTarget) || !rect.HasValue || string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("map-prefab");
                return 1;
            }

            var r = rect.Value;
            var prefab = MapPrefabAndAutotile.ExtractPrefabFromMap(mapTarget, cfg.GamePath, r.X, r.Y, r.W, r.H, includeBack, name);
            string fullOut = Path.GetFullPath(outPath!);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
            File.WriteAllText(fullOut, JsonSerializer.Serialize(prefab, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }), new UTF8Encoding(false));

            PrintJson(new
            {
                ok = true,
                outputPrefab = fullOut,
                name = prefab.Name,
                size = $"{prefab.Width}x{prefab.Height}",
                includesBack = prefab.IncludesBackLayer,
                layers = prefab.Layers.ToDictionary(k => k.Key, v => v.Value.Count)
            });
            return 0;
        }

        private static int RunMapStamp(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? prefab = GetOption(args, "--prefab", "-p");
            string? atStr = GetOption(args, "--at");
            bool clearFirst = HasFlag(args, "--clear");
            bool? overrideBack = HasFlag(args, "--include-back") ? true : (HasFlag(args, "--no-back") ? false : (bool?)null);
            string targetTmx = GetPositionalQuery(args, "--game-path", "-g", "--prefab", "-p", "--at");

            var pt = ParsePoint2(atStr);
            if (string.IsNullOrWhiteSpace(targetTmx) || string.IsNullOrWhiteSpace(prefab) || !pt.HasValue)
            {
                PrintHelp("map-prefab");
                return 1;
            }

            PrintJson(MapPrefabAndAutotile.StampPrefabOntoMap(targetTmx, prefab!, cfg.GamePath, pt.Value.X, pt.Value.Y, overrideBack, clearFirst));
            return 0;
        }

        private static int RunMapAutotile(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? spec = GetOption(args, "--spec", "-s");
            string? outTmx = GetOption(args, "--out", "-o");
            string? theme = GetOption(args, "--theme", "-t");

            if (string.IsNullOrWhiteSpace(spec))
                spec = GetPositionalQuery(args, "--game-path", "-g", "--out", "-o", "--theme", "-t");

            if (string.IsNullOrWhiteSpace(spec) || string.IsNullOrWhiteSpace(outTmx))
            {
                PrintHelp("map-autotile");
                return 1;
            }

            PrintJson(MapPrefabAndAutotile.GenerateAutotiledTmx(spec!, outTmx!, cfg.GamePath, theme));
            return 0;
        }

        private static int RunMapCheck(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? entryStr = GetOption(args, "--entry", "-e");
            bool asJson = HasFlag(args, "--json");
            string target = GetPositionalQuery(args, "--game-path", "-g", "--entry", "-e");
            if (string.IsNullOrWhiteSpace(target))
                target = Directory.GetCurrentDirectory();

            var entryPt = ParsePoint2(entryStr);
            var reports = TmxMapChecker.CheckTarget(target, cfg.GamePath, entryPt);
            bool anyError = reports.Any(r => !r.Ok);

            if (asJson)
            {
                PrintJson(reports);
                return anyError ? 1 : 0;
            }

            foreach (var r in reports)
            {
                Console.WriteLine($"=== Map Check: {r.MapFile} ({r.Width}x{r.Height}) ===");
                Console.WriteLine($"Walkability : {r.ReachableWalkableTiles}/{r.TotalWalkableTiles} walkable tiles reachable from seeds [{string.Join("; ", r.EntrySeeds)}]");
                if (r.DisconnectedIslands.Count > 0)
                {
                    Console.WriteLine($"Islands     : {r.DisconnectedIslands.Count} disconnected walkable pocket(s):");
                    foreach (var isl in r.DisconnectedIslands.OrderByDescending(i => i.TileCount).Take(5))
                    {
                        string blk = isl.AdjacentBlockers.Count > 0 ? $" | blockers: {string.Join(", ", isl.AdjacentBlockers.Take(3))}" : "";
                        Console.WriteLine($"  - {isl.TileCount} tile(s) around ({isl.SampleTile}) [{isl.BoundingBox}]{blk}");
                    }
                }
                if (r.Issues.Count == 0)
                {
                    Console.WriteLine("[PASS] 0 errors, 0 warnings.");
                }
                else
                {
                    foreach (var iss in r.Issues)
                        Console.WriteLine($"[{iss.Severity}] {iss.File}: {iss.Message}");
                }
                Console.WriteLine();
            }

            return anyError ? 1 : 0;
        }

        private static int RunMapFindSpace(string[] args)
        {
            string? sizeStr = GetOption(args, "--size", "-s") ?? "4x4";
            string? nearStr = GetOption(args, "--near");
            int limit = GetIntOption(args, "--limit", "-n", 10);
            string loc = GetPositionalQuery(args, "--size", "-s", "--near", "--limit", "-n");

            int w = 4, h = 4;
            var szParts = sizeStr!.ToLowerInvariant().Split(new[] { 'x', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (szParts.Length == 2 && int.TryParse(szParts[0], out int pw) && int.TryParse(szParts[1], out int ph))
            {
                w = pw;
                h = ph;
            }

            var nearPt = ParsePoint2(nearStr);
            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "map_find_space",
                location = string.IsNullOrWhiteSpace(loc) ? null : loc,
                width = w,
                height = h,
                nearX = nearPt?.X,
                nearY = nearPt?.Y,
                limit
            }));
            return 0;
        }

        #endregion

        #region Live In-Game Automated Testing Commands

        private static int RunLiveStatus()
        {
            Console.WriteLine(LiveGameController.GetLiveStatusJson());
            return 0;
        }

        private static int RunSaves()
        {
            PrintJson(LiveGameController.ListLocalSaves());
            return 0;
        }

        private static int RunStart(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? save = GetOption(args, "--save", "-s");
            string? project = GetOption(args, "--project", "-p");
            string? modsPath = GetOption(args, "--mods-path");
            string? withModsRaw = GetOption(args, "--with-mods");
            bool isolated = HasFlag(args, "--isolated");
            bool testWorld = HasFlag(args, "--test-world", "--new-test-save");
            bool resetTestWorld = HasFlag(args, "--reset-test-world");
            bool background = HasFlag(args, "--background", "--no-window", "--headless") && !HasFlag(args, "--visible");
            bool hideConsole = HasFlag(args, "--no-console", "--hide-console") && !HasFlag(args, "--show-console");
            bool noFocus = HasFlag(args, "--no-focus");
            int timeout = GetIntOption(args, "--timeout", "-t", defaultValue: 65);

            var extraMods = ParseCsvList(withModsRaw);
            string json = LiveGameController.StartGameAndWait(
                cfg.GamePath,
                save,
                testWorld || resetTestWorld,
                timeout,
                isolated: isolated,
                customModsPath: modsPath,
                projectDir: project,
                extraWithMods: extraMods,
                resetTestWorld: resetTestWorld,
                background: background,
                noFocus: noFocus,
                hideConsole: hideConsole);
            Console.WriteLine(json);
            return 0;
        }

        private static int RunWindowControl(string[] args)
        {
            string[] valOpts = Array.Empty<string>();
            EnsureCommandOptions(
                "window",
                args,
                valOpts,
                "--show", "--visible", "--background", "--hide", "--no-window",
                "--no-console", "--hide-console", "--show-console", "--focus", "--no-focus");

            string? mode = null;
            if (HasFlag(args, "--background", "--hide", "--no-window"))
                mode = "background";
            else if (HasFlag(args, "--show", "--visible"))
                mode = "show";

            bool? hideConsole = null;
            if (HasFlag(args, "--no-console", "--hide-console"))
                hideConsole = true;
            else if (HasFlag(args, "--show-console"))
                hideConsole = false;

            bool focus = HasFlag(args, "--focus") && !HasFlag(args, "--no-focus");

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "window_control",
                mode,
                hideConsole,
                focus
            }));
            return 0;
        }

        private static int RunRestart(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? project = GetOption(args, "--project", "-p");
            string? save = GetOption(args, "--save", "-s");
            string? modsPath = GetOption(args, "--mods-path");
            string? withModsRaw = GetOption(args, "--with-mods");
            bool isolated = HasFlag(args, "--isolated");
            bool fullMods = HasFlag(args, "--full-mods");
            bool testWorld = HasFlag(args, "--test-world", "--new-test-save");
            bool resetTestWorld = HasFlag(args, "--reset-test-world");
            bool cold = HasFlag(args, "--cold");
            bool force = HasFlag(args, "--force");
            bool sameDesktop = HasFlag(args, "--same-desktop", "--keep-window", "--visible");
            bool noFocus = !HasFlag(args, "--focus");
            bool? bgOverride = HasFlag(args, "--background", "--no-window", "--headless")
                ? true
                : (sameDesktop ? false : (bool?)null);
            bool? hideConsoleOverride = HasFlag(args, "--no-console", "--hide-console")
                ? true
                : (HasFlag(args, "--show-console") ? false : (bool?)null);
            int timeout = GetIntOption(args, "--timeout", "-t", defaultValue: 65);

            var extraMods = ParseCsvList(withModsRaw);
            string json = LiveGameController.RestartOrReloadProject(
                project,
                cfg.GamePath,
                save,
                testWorld || resetTestWorld,
                cold,
                force,
                timeout,
                isolated: isolated,
                forceFullMods: fullMods,
                customModsPath: modsPath,
                extraWithMods: extraMods,
                resetTestWorld: resetTestWorld,
                backgroundOverride: bgOverride,
                sameDesktopExplicit: sameDesktop,
                noFocus: noFocus,
                hideConsoleOverride: hideConsoleOverride);
            Console.WriteLine(json);
            return 0;
        }

        private static int RunReload(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? project = GetOption(args, "--project", "-p");
            string? id = GetOption(args, "--id");
            string? assetsRaw = GetOption(args, "--assets", "-a");
            bool reloadMap = HasFlag(args, "--reload-map");

            if (!string.IsNullOrWhiteSpace(project))
            {
                var issues = ModValidator.ValidateModDirectory(project!);
                if (issues.Any(i => i.Severity == "ERROR"))
                {
                    PrintJson(new { ok = false, errors = issues });
                    return 1;
                }

                string activeModsRoot = LiveGameController.GetActiveModsRoot(cfg.GamePath);
                var res = DotNet6ModCompiler.BuildAndOptionallyDeploy(project!, cfg.GamePath, deploy: true, targetModsRootOverride: activeModsRoot);
                if (string.IsNullOrWhiteSpace(id))
                    id = res.UniqueId;
            }

            string[] assets = ParseCsvList(assetsRaw).ToArray();

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "reload_assets",
                uniqueId = id,
                assets,
                reloadMap
            }));
            return 0;
        }

        private static int RunReloadAssets(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? project = GetOption(args, "--project", "-p", "--deploy");
            string? id = GetOption(args, "--id");
            string? assetsRaw = GetOption(args, "--assets", "-a");
            bool reloadMap = !HasFlag(args, "--no-reload-map");

            string? deployedDir = null;
            List<string> compiledShaders = new List<string>();
            if (!string.IsNullOrWhiteSpace(project))
            {
                string fullProj = Path.GetFullPath(project!);
                // 先自动编译全部 `.fx -> .mgfxo`，再执行静态检查与精准目录部署（自动识别 isolated-mods 或 Mods 中的实际文件夹名）
                var (shadersOk, shaderOuts, shaderErrs) = ShaderCompiler.CompileAllInProject(fullProj);
                if (!shadersOk)
                {
                    PrintJson(new { ok = false, stage = "shader_compile", errors = shaderErrs });
                    return 1;
                }
                compiledShaders = shaderOuts;

                var issues = ModValidator.ValidateModDirectory(fullProj);
                if (issues.Any(i => i.Severity == "ERROR"))
                {
                    PrintJson(new { ok = false, errors = issues });
                    return 1;
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    string mf = Path.Combine(fullProj, "manifest.json");
                    if (File.Exists(mf))
                        id = ModDependencyResolver.ParseManifestFile(mf)?.UniqueId;
                }

                string activeModsRoot = LiveGameController.GetActiveModsRoot(cfg.GamePath);
                string fallbackFolder = id ?? Path.GetFileName(fullProj);
                deployedDir = DotNet6ModCompiler.DeployAssetsOnlyToModsRoot(fullProj, activeModsRoot, fallbackFolder);

                // 同时把重编后的 `.mgfxo` / `assets/` / `i18n/` 增量同步到真实 `<GamePath>\Mods`（若当前跑在 isolated-mods 且真实 Mods 已安装该模组），免手写二次拷贝！
                string realModsDir = Path.Combine(cfg.GamePath, "Mods");
                if (!Path.GetFullPath(activeModsRoot).TrimEnd('\\', '/').Equals(Path.GetFullPath(realModsDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(id) &&
                    DotNet6ModCompiler.ModsRootContainsMod(realModsDir, id!, fallbackFolder))
                {
                    DotNet6ModCompiler.DeployAssetsOnlyToModsRoot(fullProj, realModsDir, fallbackFolder);
                }
            }

            string[] assets = ParseCsvList(assetsRaw).ToArray();
            string bridgeResp = BridgeClient.SendRequired(new
            {
                action = "reload_assets",
                uniqueId = id,
                assets,
                compiledShaders = compiledShaders.Count > 0 ? compiledShaders : null,
                reloadMap
            });

            if (deployedDir != null)
            {
                PrintJson(new
                {
                    ok = true,
                    deployedAssetsTo = deployedDir,
                    compiledShaders,
                    bridge = JsonSerializer.Deserialize<object>(bridgeResp)
                });
            }
            else
            {
                Console.WriteLine(bridgeResp);
            }
            return 0;
        }

        private static int RunWarp(string[] args)
        {
            int facing = GetIntOption(args, "--facing", "-f", defaultValue: 2);
            string? screen = GetOption(args, "--screen");
            if (HasFlag(args, "--facing-up")) facing = 0;
            var pos = GetPositionalList(args, "--facing", "-f", "--screen");
            if (pos.Count == 0)
            {
                PrintHelp("warp");
                return 1;
            }

            string loc = pos[0];
            int? x = null, y = null;
            if (pos.Count >= 3 && int.TryParse(pos[1], out int px) && int.TryParse(pos[2], out int py))
            {
                x = px;
                y = py;
            }
            else if (pos.Count == 2)
            {
                var pt = ParsePoint2(pos[1]);
                if (pt.HasValue)
                {
                    x = pt.Value.X;
                    y = pt.Value.Y;
                }
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "warp",
                location = loc,
                x,
                y,
                facing,
                screen
            }));
            return 0;
        }

        private static int RunScreenshot(string[] args)
        {
            string[] valOpts = { "--mode", "-m", "--out", "-o", "--scale", "-s", "--zoom", "-z", "--screen", "--ab-compare", "--ab", "--ab-toggle", "--interval-ticks", "--interval-ms", "--pan" };
            EnsureCommandOptions("screenshot", args, valOpts, "--ui-overlay", "--ui", "--burst", "--grid", "--pan-test");

            string mode = (GetOption(args, "--mode", "-m") ?? "screen").Trim().ToLowerInvariant();
            string? outPath = GetOption(args, "--out", "-o");
            string? scaleStr = GetOption(args, "--scale", "-s");
            string? zoomStr = GetOption(args, "--zoom", "-z");
            string? screen = GetOption(args, "--screen");
            bool uiOverlay = HasFlag(args, "--ui-overlay", "--ui");
            string? abMod = GetOption(args, "--ab-compare", "--ab");
            string? abToggle = GetOption(args, "--ab-toggle");
            bool hasBurstFlag = HasFlag(args, "--burst");
            bool panTestFlag = HasFlag(args, "--pan-test");
            string? panRaw = GetOption(args, "--pan");
            float scale = float.TryParse(scaleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float sv) ? sv : 1.0f;
            float? tempZoom = float.TryParse(zoomStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float zv) && zv > 0.05f ? zv : (float?)null;

            // 支持 `--burst` 无参默认 4 帧，或 `--burst 6` 显式帧数
            int burstFrames = 4;
            bool burstTakesNumericNext = false;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--burst", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out int parsedFrames) && parsedFrames >= 2 && parsedFrames <= 9)
                {
                    burstFrames = parsedFrames;
                    burstTakesNumericNext = true;
                    break;
                }
            }

            var posOpts = burstTakesNumericNext
                ? valOpts.Concat(new[] { "--burst" }).ToArray()
                : valOpts;
            if (string.IsNullOrWhiteSpace(outPath))
            {
                outPath = GetPositionalQuery(args, posOpts);
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("screenshot");
                return 1;
            }

            string fullOut = Path.GetFullPath(outPath!);

            // 分支 1：带临时镜头缩放重建缓冲 (`--zoom`)、A/B 对比 (`--ab-compare`) 或 动态时序四宫格连拍 (`--burst` / `--pan-test`)
            bool isMultiOrZoomScreen = mode == "screen" && (!string.IsNullOrWhiteSpace(abMod) || hasBurstFlag || panTestFlag || !string.IsNullOrWhiteSpace(panRaw) || (tempZoom.HasValue && !uiOverlay));
            if (isMultiOrZoomScreen)
            {
                int intervalTicks = GetIntOption(args, "--interval-ticks", "--interval-ticks", defaultValue: -1);
                if (intervalTicks <= 0)
                {
                    int intervalMs = GetIntOption(args, "--interval-ms", "--interval-ms", defaultValue: 250);
                    intervalTicks = Math.Max(1, (int)Math.Round(intervalMs * 60.0 / 1000.0));
                }

                int panDx = 0, panDy = 0;
                if (!string.IsNullOrWhiteSpace(panRaw))
                {
                    var pt = ParsePoint2(panRaw);
                    if (pt.HasValue) { panDx = pt.Value.X; panDy = pt.Value.Y; }
                }
                else if (panTestFlag)
                {
                    panDx = 8; // 默认每 tick 向右平滑移动 8px（45 ticks 共平移 360px，足以暴露残影与 UV 错位）
                }

                string capMode = !string.IsNullOrWhiteSpace(abMod)
                    ? "ab_compare"
                    : ((hasBurstFlag || panTestFlag || !string.IsNullOrWhiteSpace(panRaw)) ? "burst" : "single_zoom");

                string advResp = BridgeClient.SendRequired(new
                {
                    action = "screenshot_advanced",
                    captureMode = capMode,
                    outPath = fullOut,
                    abMod,
                    abToggle,
                    burstFrames,
                    intervalTicks,
                    panDx,
                    panDy,
                    zoom = tempZoom,
                    screen
                }, connectTimeoutMs: 5000);

                Console.WriteLine(advResp);
                return 0;
            }

            string resp = BridgeClient.SendRequired(new
            {
                action = "screenshot",
                mode,
                outPath = fullOut,
                scale,
                screen,
                uiOverlay
            }, connectTimeoutMs: 5000);

            if (uiOverlay && mode == "screen" && File.Exists(fullOut))
            {
                try
                {
                    using var doc = JsonDocument.Parse(resp);
                    if (doc.RootElement.TryGetProperty("uiLayout", out var uiEl))
                    {
                        var overlayStats = SpriteSheetInspector.DrawUiOverlayOnScreenshotPng(fullOut, uiEl);
                        PrintJson(new
                        {
                            ok = true,
                            mode = "screen",
                            outputPng = fullOut,
                            uiOverlay = overlayStats,
                            uiLayout = JsonSerializer.Deserialize<object>(uiEl.GetRawText())
                        });
                        return 0;
                    }
                }
                catch { }
            }

            Console.WriteLine(resp);
            return 0;
        }

        private static int RunModConfig(string[] args)
        {
            bool noSave = HasFlag(args, "--no-save");
            bool reset = HasFlag(args, "--reset");
            string? getKey = GetOption(args, "--get", "-k", "--key");
            // 若用户只写了 `--get` 末尾标志而无参数，getKey 为 null（表示返回全部字段）
            if (getKey != null && getKey.StartsWith("-", StringComparison.Ordinal))
                getKey = null;

            var setAssignments = new List<string>();
            var positional = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.Equals("--set", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase))
                {
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        i++;
                        setAssignments.Add(args[i]);
                    }
                    continue;
                }
                if (a.Equals("--get", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-k", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--key", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) && !args[i + 1].Contains('='))
                        i++;
                    continue;
                }
                if (a.StartsWith("-", StringComparison.Ordinal))
                    continue;

                if (a.Contains('=') && positional.Count >= 1)
                    setAssignments.Add(a);
                else
                    positional.Add(a);
            }

            string? modId = GetOption(args, "--mod", "-m") ?? (positional.Count > 0 ? positional[0] : null);
            if (string.IsNullOrWhiteSpace(modId))
            {
                PrintHelp("config");
                return 1;
            }

            Dictionary<string, object?>? setObj = null;
            if (setAssignments.Count > 0)
            {
                setObj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var expr in setAssignments)
                {
                    int eq = expr.IndexOf('=');
                    if (eq <= 0)
                        throw new ArgumentException($"Invalid `--set` assignment `{expr}` (expected `Key=Value`).");
                    string k = expr.Substring(0, eq).Trim();
                    string rawVal = expr.Substring(eq + 1).Trim();
                    setObj[k] = ParseLooseScalar(rawVal);
                }
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "mod_config",
                modId,
                key = getKey,
                set = setObj,
                reset,
                save = !noSave
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static object? ParseLooseScalar(string raw)
        {
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (raw.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv)) return iv;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv)) return dv;
            return raw;
        }

        private static int RunStop(string[] args)
        {
            bool force = HasFlag(args, "--force");
            Console.WriteLine(LiveGameController.StopGame(force));
            return 0;
        }

        private static int RunMenu()
        {
            Console.WriteLine(BridgeClient.SendRequired(new { action = "exit_to_title" }));
            return 0;
        }

        private static int RunEval(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? file = GetOption(args, "--file", "-f");
            string? b64 = GetOption(args, "--code-b64");
            string? screen = GetOption(args, "--screen");
            bool stdinFlag = HasFlag(args, "--stdin");

            string code;
            if (!string.IsNullOrWhiteSpace(b64))
            {
                code = Encoding.UTF8.GetString(Convert.FromBase64String(b64!));
            }
            else if (!string.IsNullOrWhiteSpace(file))
            {
                code = File.ReadAllText(file!);
            }
            else if (stdinFlag)
            {
                code = Console.In.ReadToEnd();
            }
            else
            {
                code = GetPositionalQuery(args, "--game-path", "-g", "--file", "-f", "--code-b64", "--screen");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                PrintHelp("eval");
                return 1;
            }

            string activeModsRoot = LiveGameController.GetActiveModsRoot(cfg.GamePath);
            var (ok, asmBytes, errors, prepared) = DotNet6ModCompiler.CompileEvalSnippetToBytes(code, cfg.GamePath, activeModsRoot);
            if (!ok || asmBytes == null)
            {
                PrintJson(new { ok = false, stage = "compile", error = errors, preparedSource = prepared });
                return 1;
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "eval_asm",
                assemblyBase64 = Convert.ToBase64String(asmBytes),
                screen
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static int RunConsoleCmd(string[] args)
        {
            string raw = GetPositionalQuery(args);
            if (string.IsNullOrWhiteSpace(raw))
            {
                PrintHelp("cmd");
                return 1;
            }
            Console.WriteLine(BridgeClient.SendRequired(new { action = "console_cmd", command = raw }));
            return 0;
        }

        private static int RunPlayer(string[] args)
        {
            string? screen = GetOption(args, "--screen");
            Console.WriteLine(BridgeClient.SendRequired(new { action = "player", screen }));
            return 0;
        }

        private static int RunUi(string[] args)
        {
            string? screen = GetOption(args, "--screen");
            Console.WriteLine(BridgeClient.SendRequired(new { action = "ui", screen }));
            return 0;
        }

        private static int RunUiClick(string[] args)
        {
            bool close = HasFlag(args, "--close");
            string? idStr = GetOption(args, "--id");
            string? match = GetOption(args, "--match", "--button");
            string? respStr = GetOption(args, "--response", "-r");
            string? xyStr = GetOption(args, "--xy");
            string? screen = GetOption(args, "--screen");

            int? compId = int.TryParse(idStr, out int cid) ? cid : (int?)null;
            int? respIdx = int.TryParse(respStr, out int rid) ? rid : (int?)null;
            int? x = null, y = null;
            var pt = ParsePoint2(xyStr);
            if (pt.HasValue)
            {
                x = pt.Value.X;
                y = pt.Value.Y;
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "ui_click",
                close,
                componentId = compId,
                match,
                responseIndex = respIdx,
                x,
                y,
                screen
            }));
            return 0;
        }

        private static int RunInput(string[] args)
        {
            string[] valOpts =
            {
                "--hold", "--press", "--tap", "--keys", "--gamepad",
                "--move", "--hover", "--xy", "--aim-tile",
                "--click", "--scroll",
                "--ticks", "-t", "--interval-ticks", "--interval-ms", "--settle-ticks",
                "--out", "-o", "--screen"
            };
            EnsureCommandOptions("input", args, valOpts);

            string? hold = GetOption(args, "--hold");
            string? seqRaw = GetOption(args, "--press", "--tap", "--keys", "--gamepad");
            string? moveStr = GetOption(args, "--move", "--hover", "--xy");
            string? aimTileStr = GetOption(args, "--aim-tile");
            string? click = GetOption(args, "--click");
            string? scrollStr = GetOption(args, "--scroll");
            string? ticksStr = GetOption(args, "--ticks", "-t");
            string? intervalTicksStr = GetOption(args, "--interval-ticks");
            string? intervalMsStr = GetOption(args, "--interval-ms");
            string? settleStr = GetOption(args, "--settle-ticks");
            string? outPath = GetOption(args, "--out", "-o");
            string? screen = GetOption(args, "--screen");

            // 若没有传显式键值选项但给了位置参数（如 `sdv-dev input DPadDown,DPadRight`），视为按键序列
            string posQuery = GetPositionalQuery(args, valOpts);
            if (string.IsNullOrWhiteSpace(seqRaw) && string.IsNullOrWhiteSpace(hold) && !string.IsNullOrWhiteSpace(posQuery))
                seqRaw = posQuery;

            var sequence = ParseCsvList(seqRaw);
            var movePt = ParsePoint2(moveStr);
            var aimPt = ParsePoint2(aimTileStr);
            int? scroll = int.TryParse(scrollStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scv) ? scv : (int?)null;
            int? ticks = int.TryParse(ticksStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tkv) && tkv > 0 ? tkv : (int?)null;
            int? settleTicks = int.TryParse(settleStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int stv) && stv >= 0 ? stv : (int?)null;

            int? intervalTicks = null;
            if (int.TryParse(intervalTicksStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itv) && itv > 0)
                intervalTicks = itv;
            else if (int.TryParse(intervalMsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ims) && ims > 0)
                intervalTicks = Math.Max(1, (int)Math.Round(ims / (1000.0 / 60.0)));

            if (string.IsNullOrWhiteSpace(hold) && sequence.Count == 0 && !movePt.HasValue && !aimPt.HasValue && !scroll.HasValue && string.IsNullOrWhiteSpace(click))
            {
                PrintHelp("input");
                return 1;
            }

            string? fullOut = !string.IsNullOrWhiteSpace(outPath) ? Path.GetFullPath(outPath!) : null;
            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "input_simulate",
                hold,
                sequence = sequence.Count > 0 ? sequence : null,
                move = movePt.HasValue ? $"{movePt.Value.X},{movePt.Value.Y}" : null,
                aimTile = aimPt.HasValue ? $"{aimPt.Value.X},{aimPt.Value.Y}" : null,
                click,
                scroll,
                ticks,
                intervalTicks,
                settleTicks,
                outPath = fullOut,
                screen
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static int RunHarmony(string[] args)
        {
            string? modId = GetOption(args, "--mod", "-m");
            bool includeIl = HasFlag(args, "--il", "--instructions");
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: includeIl ? 3 : 40);
            string query = GetPositionalQuery(args, "--mod", "-m", "--limit", "-n");

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "harmony_inspect",
                query = string.IsNullOrWhiteSpace(query) ? null : query,
                modId,
                includeIl,
                limit
            }));
            return 0;
        }

        private static int RunInteract(string[] args)
        {
            string? screen = GetOption(args, "--screen");
            bool noMove = HasFlag(args, "--no-move");
            int power = GetIntOption(args, "--power", "-p", defaultValue: 1);

            string? placeItem = GetOption(args, "--place");
            string? dropInItem = GetOption(args, "--drop-in", "--dropin");
            string? useTool = GetOption(args, "--use-tool", "--tool");
            bool rightClickFlag = HasFlag(args, "--right-click", "--action", "-r");
            string? rcOptVal = GetOption(args, "--right-click", "--action", "-r");

            string mode;
            string? item = null;
            string? tool = null;

            if (!string.IsNullOrWhiteSpace(placeItem))
            {
                mode = "place";
                item = placeItem;
            }
            else if (!string.IsNullOrWhiteSpace(dropInItem))
            {
                mode = "drop_in";
                item = dropInItem;
            }
            else if (!string.IsNullOrWhiteSpace(useTool))
            {
                mode = "use_tool";
                tool = useTool;
            }
            else if (rightClickFlag)
            {
                mode = "right_click";
            }
            else
            {
                PrintHelp("interact");
                return 1;
            }

            // Parse target tile coordinates `x,y` or `x y` (either right after `--right-click x,y` or as positional arguments)
            (int X, int Y)? pt = ParsePoint2(rcOptVal);
            if (!pt.HasValue)
            {
                var pos = GetPositionalList(args, "--screen", "--power", "-p", "--place", "--drop-in", "--dropin", "--use-tool", "--tool");
                if (pos.Count >= 2 && int.TryParse(pos[0], out int px) && int.TryParse(pos[1], out int py))
                {
                    pt = (px, py);
                }
                else if (pos.Count >= 1)
                {
                    pt = ParsePoint2(pos[0]);
                }
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "interact",
                mode,
                item,
                tool,
                power,
                x = pt?.X,
                y = pt?.Y,
                moveAdjacent = !noMove,
                screen
            }));
            return 0;
        }

        private static int RunProfile(string[] args)
        {
            EnsureCommandOptions("profile", args, new[] { "--seconds", "-s", "--top", "-n" });
            string? secStr = GetOption(args, "--seconds", "-s");
            double seconds = double.TryParse(secStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double sv) && sv > 0 ? sv : 5.0;
            int top = GetIntOption(args, "--top", "-n", defaultValue: 12);

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "profile",
                seconds,
                top
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static int RunCheckpoint(string[] args)
        {
            EnsureCommandOptions("checkpoint", args, new[] { "--name", "-n" }, "--force");
            bool force = HasFlag(args, "--force");
            var pos = GetPositionalList(args, "--name", "-n");
            string op = pos.Count > 0 ? pos[0].ToLowerInvariant() : "list";
            string name = GetOption(args, "--name", "-n") ?? (pos.Count > 1 ? pos[1] : "quick");

            string cpRoot = Path.Combine(KbConfig.DataDir, "checkpoints");
            Directory.CreateDirectory(cpRoot);

            if (op == "list" || op == "ls")
            {
                var items = new List<object>();
                foreach (var dir in Directory.GetDirectories(cpRoot).OrderByDescending(Directory.GetLastWriteTimeUtc))
                {
                    string metaFile = Path.Combine(dir, "checkpoint-meta.json");
                    if (File.Exists(metaFile))
                    {
                        try
                        {
                            items.Add(JsonSerializer.Deserialize<object>(File.ReadAllText(metaFile))!);
                        }
                        catch { }
                    }
                }
                PrintJson(new { ok = true, count = items.Count, checkpoints = items });
                return 0;
            }
            else if (op == "delete" || op == "rm" || op == "remove")
            {
                string targetDir = Path.Combine(cpRoot, SanitizeFileNameLocal(name));
                bool existed = Directory.Exists(targetDir);
                if (existed)
                    Directory.Delete(targetDir, recursive: true);
                PrintJson(new { ok = true, deleted = existed, name });
                return 0;
            }
            else if (op == "save" || op == "load" || op == "restore")
            {
                string normOp = op == "restore" ? "load" : op;
                Console.WriteLine(BridgeClient.SendRequired(new
                {
                    action = "checkpoint",
                    op = normOp,
                    name,
                    force
                }, connectTimeoutMs: 5000));
                return 0;
            }

            PrintHelp("checkpoint");
            return 1;
        }

        private static string SanitizeFileNameLocal(string raw)
        {
            string s = (raw ?? "quick").Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "quick" : s;
        }

        private static int RunScheduleCheck(string[] args)
        {
            EnsureCommandOptions("schedule-check", args, new[] { "--key", "-k" });
            string? key = GetOption(args, "--key", "-k");
            string npc = GetPositionalQuery(args, "--key", "-k");
            if (string.IsNullOrWhiteSpace(npc))
            {
                PrintHelp("schedule-check");
                return 1;
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "schedule_check",
                npc,
                key
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static int RunAsset(string[] args)
        {
            string? key = GetOption(args, "--key", "-k");
            string? exportPath = GetOption(args, "--export", "-o");
            int limit = GetIntOption(args, "--limit", "-n", defaultValue: 10);
            bool cpTrace = HasFlag(args, "--cp-trace", "--trace");
            string target = GetPositionalQuery(args, "--key", "-k", "--export", "-o", "--limit", "-n");

            if (string.IsNullOrWhiteSpace(target))
            {
                PrintHelp("asset");
                return 1;
            }

            long logLengthBefore = 0;
            if (cpTrace && File.Exists(LiveGameController.SmapiLatestLogPath))
            {
                try { logLengthBefore = new FileInfo(LiveGameController.SmapiLatestLogPath).Length; }
                catch { }
            }

            string resp = BridgeClient.SendRequired(new
            {
                action = "asset_inspect",
                target,
                key,
                exportPath = string.IsNullOrWhiteSpace(exportPath) ? null : Path.GetFullPath(exportPath!),
                limit,
                cpTrace
            });

            if (!cpTrace)
            {
                Console.WriteLine(resp);
                return 0;
            }

            // Wait briefly for Content Patcher's `patch summary asset "<target>"` command queue to flush to SMAPI-latest.txt
            System.Threading.Thread.Sleep(350);
            string cpOutput = ReadAppendedSmapiLogText(logLengthBefore);
            PrintJson(new
            {
                asset = JsonSerializer.Deserialize<object>(resp),
                contentPatcherSummary = cpOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            });
            return 0;
        }

        private static string ReadAppendedSmapiLogText(long offset)
        {
            try
            {
                if (!File.Exists(LiveGameController.SmapiLatestLogPath))
                    return string.Empty;
                using var fs = new FileStream(LiveGameController.SmapiLatestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (offset > 0 && offset < fs.Length)
                    fs.Seek(offset, SeekOrigin.Begin);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                return sr.ReadToEnd().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int RunEventPlay(string[] args)
        {
            string? screen = GetOption(args, "--screen");
            if (HasFlag(args, "--status"))
            {
                Console.WriteLine(BridgeClient.SendRequired(new { action = "event_play", mode = "status", screen }));
                return 0;
            }
            if (HasFlag(args, "--skip"))
            {
                Console.WriteLine(BridgeClient.SendRequired(new { action = "event_play", mode = "skip", screen }));
                return 0;
            }
            if (HasFlag(args, "--step"))
            {
                Console.WriteLine(BridgeClient.SendRequired(new { action = "event_play", mode = "step", screen }));
                return 0;
            }

            string? script = GetOption(args, "--script");
            string? b64 = GetOption(args, "--script-b64");
            string? file = GetOption(args, "--file", "-f");
            bool keepSeen = HasFlag(args, "--keep-seen");

            if (!string.IsNullOrWhiteSpace(b64))
                script = Encoding.UTF8.GetString(Convert.FromBase64String(b64!));
            else if (!string.IsNullOrWhiteSpace(file))
                script = File.ReadAllText(file!);
            else if (HasFlag(args, "--stdin"))
                script = Console.In.ReadToEnd();

            var pos = GetPositionalList(args, "--script", "--script-b64", "--file", "-f", "--screen");
            string? location = pos.Count >= 1 ? pos[0] : null;
            string? eventId = pos.Count >= 2 ? pos[1] : null;

            // Allow `sdv-dev event-play <EventId>` in current location when only 1 numeric/id arg is passed and no raw script
            if (string.IsNullOrWhiteSpace(script) && pos.Count == 1)
            {
                eventId = pos[0];
                location = null;
            }

            if (string.IsNullOrWhiteSpace(script) && string.IsNullOrWhiteSpace(eventId))
            {
                PrintHelp("event-play");
                return 1;
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "event_play",
                mode = "play",
                location,
                eventId,
                script,
                keepSeen,
                screen
            }));
            return 0;
        }

        private static int RunWorld(string[] args)
        {
            string? season = GetOption(args, "--season", "-s");
            string? dayStr = GetOption(args, "--day", "-d");
            string? yearStr = GetOption(args, "--year", "-y");
            string? timeStr = GetOption(args, "--time", "-t");
            string? weather = GetOption(args, "--weather", "-w");
            string? moneyStr = GetOption(args, "--money");
            string? friendship = GetOption(args, "--friendship");
            string? addItem = GetOption(args, "--add-item", "--give");
            string? mail = GetOption(args, "--mail");
            string? advStr = GetOption(args, "--advance-days", "--days");
            string? zoomStr = GetOption(args, "--zoom", "-z");
            string? screen = GetOption(args, "--screen");
            bool refill = HasFlag(args, "--refill");
            bool save = HasFlag(args, "--save");

            int? day = int.TryParse(dayStr, out int dv) ? dv : (int?)null;
            int? year = int.TryParse(yearStr, out int yv) ? yv : (int?)null;
            int? time = int.TryParse(timeStr, out int tv) ? tv : (int?)null;
            int? money = int.TryParse(moneyStr, out int mv) ? mv : (int?)null;
            int? advanceDays = int.TryParse(advStr, out int av) ? av : (int?)null;
            float? zoom = float.TryParse(zoomStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float zv) && zv > 0.05f ? zv : (float?)null;

            if (season == null && !day.HasValue && !year.HasValue && !time.HasValue && weather == null &&
                !money.HasValue && friendship == null && addItem == null && mail == null && !advanceDays.HasValue && !zoom.HasValue && !refill)
            {
                PrintHelp("world");
                return 1;
            }

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "world_control",
                season,
                day,
                year,
                time,
                weather,
                money,
                refill,
                friendship,
                addItem,
                mail,
                advanceDays,
                zoom,
                save,
                screen
            }, connectTimeoutMs: 5000));
            return 0;
        }

        private static int RunMultiplayer(string[] args)
        {
            string sub = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal)
                ? args[0].ToLowerInvariant()
                : "status";
            string? screenStr = GetOption(args, "--screen");
            int? targetScreen = int.TryParse(screenStr, out int si) ? si : (int?)null;

            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "mp_control",
                sub,
                targetScreen
            }));
            return 0;
        }

        private static int RunLogs(string[] args)
        {
            string? level = GetOption(args, "--level", "-l");
            string? mod = GetOption(args, "--mod", "-m");
            int tail = GetIntOption(args, "--tail", "-n", defaultValue: 40);
            bool asJson = HasFlag(args, "--json");

            var entries = LiveGameController.ReadSmapiLogs(level, mod, tail);
            if (asJson)
            {
                PrintJson(entries);
                return 0;
            }

            if (entries.Count == 0)
            {
                Console.WriteLine("No matching log entries found in SMAPI-latest.txt.");
                return 0;
            }

            foreach (var e in entries)
                Console.WriteLine(e);
            return 0;
        }

        #endregion

        private static void EnsureDbInitialized(KbConfig cfg)
        {
            if (!File.Exists(cfg.DbPath))
                throw new InvalidOperationException($"Knowledge base not found at `{cfg.DbPath}`. Run `sdv-dev sync` first.");
        }

        private static void PrintJson<T>(T obj)
        {
            Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }

        private static int UnknownCommand(string cmd)
        {
            Console.Error.WriteLine($"Unknown subcommand: `{cmd}`. Run `sdv-dev help` to view all commands.");
            return 1;
        }

        #region Layered Help System (`sdv-dev help [topic|command]`)

        private static void PrintHelp(string? topic)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                PrintTopicHelp(topic!.Trim().ToLowerInvariant());
                return;
            }

            Console.WriteLine(@"sdv-dev 星露谷物语 1.6 模组开发资料库、着色器/C# 免 SDK 构建、静态体检、地图离线渲染与实机自动化测试工具

sdv-dev [OPTIONS] SUBCOMMAND

OPTIONS:
  -h, --help                  打印帮助信息；子命令详情用 `sdv-dev <命令> --help` 或 `sdv-dev help <命令>`

知识检索与源码检视:
  help                        查看子命令详细参数与示例（如 `sdv-dev help shader`、`sdv-dev help screenshot`、`sdv-dev help deps`）
  api                         模糊搜索 C# 类、方法（含私有方法）与事件（支持 `--asm sdv|smapi|gamedata|monogame|xtile|harmony|toolkit`）
  decompile                   实时反编译原版/SMAPI(含内部类)/MonoGame/xTile/Harmony 源码，支持 `--grep <关键词> -C 15` 局部切片
  schema                      查询 1.6 GameData 数据表强类型字段与官方 XML 注释
  data                        按中英文名或 ID 检索原版解包 JSON 条目与 `Audio/Cues` XACT 音效库
  doc                         全文检索 Content Patcher、SMAPI、官方 Wiki、GameStateQuery、TriggerAction、EventCommand 文档
  status / sync               查看知识库状态，或同步重建本地 SQLite FTS5 索引

工程构建、着色器编译、依赖管理、1.6 深度体检与发布打包:
  deps                        项目依赖管理（扫描本地 Mods、兜底拉取 GitHub Releases、反编译前置 `I*Api` 接口、挂载隔离环境）
  new                         创建 1.6 Content Patcher 内容包 (`cp`) 或 SMAPI C# 模组 (`csharp`，预集成 Publicizer) 工程骨架
  gen-gmcm                    解析 `ModConfig.cs` AST 一键生成 GMCM 配置菜单 (`IGenericModConfigMenuApi` + 注册代码 + 双语 `i18n`)
  shader                      内置免装 HLSL `.fx -> .mgfxo` 编译器（对齐星露谷 1.6 DesktopGL MGFX v9 字节码，支持子目录 `#include`）
  check                       深度静态校验（1.6 GameData/跨表外键断链/NPC日程对话肖像/.fx着色器/Harmony验签/MonoGame渲染坑/联机隐患/.tmx）
  build                       免 SDK 秒编 C# 模组与全部 `*.fx` 着色器（默认启用 `AssemblyPublicizer` 免反射直访私有成员 + Harmony 验签）
  pack                        规范化发布打包：自动编译+全量静态校验并生成剔除源码/`.fx`/PDB 的标准 `<ModName> <Version>.zip`

地图、精灵图与贴图离线/实机工具链:
  tilesheet                   从原版解包目录导出贴图 PNG，支持 `--grid` 自动叠加 `0..N` TileIndex 编号网格
  sprite                      物品/建筑/NPC 4方向行走帧 (`16x32`) / 肖像表情格 (`64x64`) 切分体检与包围盒对齐预览图导出
  map-inspect                 检视地图图层、TileSheets、Warp/Light/动画帧与指定 `--rect` 局部图块/通行掩码矩阵
  map-render                  离线合成 `.tmx` 或叠加 `--cp <ModDir> --auto-crop` 预览 CP 地图补丁（`--debug-overlay` 标碰撞/水面/Warp）
  map-prefab / map-extract    列出内置原版跨层预制件（棕榈树/栈桥/浪花等）或从原版地图抠取 5 层结构+动画保存为预制件
  map-stamp                   将跨层预制件盖章合入目标 `.tmx`（自动合并 tileset、动画与属性，默认不盖 Back 防沙块穿帮）
  map-autotile                根据紧凑字符网格（W水/S沙/G草/P桥/C崖）自动过渡边缘并挂接 13 帧海浪生成 `.tmx`
  map-check                   离线深度体检 `.tmx`：PNG 真实像素尺寸、GID 越界、跨图 Warp 落点安全与 BFS 连通性孤岛检测
  map-find-space              在实机（含 CP/SVE 合并后）扫描地图安全空地，自动避开水面/灌木/建筑/Warp 与 NPC 日程路线

实机自动化控制、免重启调参、视觉对比/连拍、快照与性能探针:
  live / window / saves       查看进程与真实窗口可见性 (`windowVisible`/`desktopName`)、运行期秒切前后台/隐藏控制台 (`window`)、列出存档
  start                       启动 SMAPI（`--isolated` 隔离秒开、`--no-console` 只显示游戏画面不弹黑框、`--background` 离屏隐藏运行）
  restart                     先 check+build（失败保留原进程），通过后无锁暂存热重启（支持 `--same-desktop` 无焦点原位刷新 / `--background`）
  reload / reload-assets      免重启热重载 CP 包，或对 C#/CP 模组自动重编 `.fx` 并 0.2s 热刷内存 `Effect` 着色器与地图贴图
  config                      免重启 0.05 秒实时读取/修改/重置模组配置（`--get [Key]` / `--set Key=Val` / `--reset`）
  screenshot                  实机截图：`--mode screen|map`、`--zoom 0.75` 大视野、`--ui-overlay`、`--ab-compare`、`--burst` 四宫格
  input                       无焦点真实输入模拟：`--hold W --ticks 30` 走动/蓄力、`--move` 悬停抓 Tooltip、`--gamepad` 测手柄焦点跳转
  interact                    世界交互模拟：`--place` 摆机器、`--drop-in` 投料、`--right-click` 右键收获/对话/开店、`--use-tool` 挥农具
  checkpoint                  测试档秒级时光机快照：`save`/`load`/`list`/`delete`（冷重启后也能 1 秒回档到精确分钟与坐标反复测分支）
  schedule-check              实机调用原版 `NPC.parseMasterSchedule` 推演 NPC 全天日程跨图寻路，排查不可达目标格或断链 Warp
  asset                       透视实机合并后的最终资产数据/贴图，支持 `--cp-trace` 抓取官方 Content Patcher `patch summary` 链路
  harmony                     查询实机 Harmony 补丁链；加 `--il` 导出经全部 Transpiler 改写后的实时 IL 指令序列与每段模组归属
  profile                     按需动态采样（完事自动卸载 0 残留）：统计帧率、1% Low、GC 速率及耗时 Top N 的 SMAPI 事件与 Harmony 补丁
  event-play                  剧情事件实时沙盒：按 EventID 强制重播（自动清已读）或通过 `--stdin` 直接预览原始 Event 脚本字符串
  world                       确定性世界控制：改时间/季节/天气/好感/发物品/塞信件，支持 `--advance-days N` 驱动真实跨夜结算管线
  mp                          本地分屏联机测试：列出分屏实例、`add-farmhand` 一键拉起客机，全系实机命令均支持 `--screen host|farmhand`
  warp                        主线程同步瞬移玩家至指定地点与坐标（如 `sdv-dev warp IslandSouth 20 35`）
  eval                        在 SMAPI 主线程动态执行 C# 代码（默认内置 Publicizer 免反射直访私有成员、支持 `--stdin` 与 `--screen`）
  cmd / player / ui / ui-click / logs / menu / stop
  daemon-status / daemon-stop 查看或关闭 10 分钟空闲自退的热驻加速守护进程（重编后自动指纹校验淘汰旧进程）");
        }

        private static void PrintTopicHelp(string topic)
        {
            switch (topic)
            {
                case "deps":
                case "dep":
                    Console.WriteLine(@"# sdv-dev deps — 项目依赖管理（前置模组解析、特殊仓库自动拉取、编译引用与隔离环境挂载）

用法：
  sdv-dev deps [--project <ModDir>] [--offline]              解析工程 manifest.json、GMCM 软依赖与 sdv-project.json 的全部前置
  sdv-dev deps list [<关键词>]                               扫描本机 `<GamePath>\Mods` 与本地缓存中所有可用前置模组
  sdv-dev deps add <UniqueID|别名> [--project <ModDir>] [--optional] [--no-ref] [--github <owner/repo>]
  sdv-dev deps remove <UniqueID|别名> [--project <ModDir>]   移除前置依赖声明
  sdv-dev deps api <UniqueID|别名> [<TypeOrMethod>]          直接反编译前置模组公开的 `I*Api` / `GetApi()` 接口签名（本地未装时自动拉取！）
  sdv-dev deps isolated [--on|--off] [--project <ModDir>]    设置该工程默认启用 `--isolated` 隔离测试模式

核心机制：
  1. 优先从玩家本地 `<GamePath>\Mods` 递归扫描 `manifest.json`（兼容带版本号嵌套目录），零网络耗时；
  2. 若本地缺失开源前置：自动区分常规 GitHub Releases 仓库与单仓只打 tag 的特殊仓库（如 `Pathoschild` / `spacechase0` 全家桶：`cp`、`gmcm`、`spacecore`、`automate` 等），自动经 GitHub Releases 或官方 CurseForge 公共 CDN 免登录直拉最新预编译 `.zip` 到缓存；
  3. 自动感知 C# 工程中的 `GenericModConfigMenu` 软依赖（`GmcmIntegration.g.cs` / `GetApi`），在 `--isolated` 测试时自动挂载 GMCM！

常用别名：
  `cp` (Pathoschild.ContentPatcher), `gmcm` (spacechase0.GenericModConfigMenu), `spacecore` (spacechase0.SpaceCore),
  `ftm` (Esca.FarmTypeManager), `sve` (FlashShifter.StardewValleyExpandedCP), `automate`, `lookupanything`, `stardewhack`, `kokoro`

示例：
  sdv-dev deps list gmcm
  sdv-dev deps add gmcm --optional --no-ref --project d:\myProject\MyMod
  sdv-dev deps api gmcm
  sdv-dev deps add spacecore --project d:\myProject\MyMod");
                    break;

                case "input":
                case "key":
                case "keys":
                case "gamepad":
                    Console.WriteLine(@"# sdv-dev input — 无焦点真实键鼠 / 农具蓄力 / 鼠标悬停 Tooltip / 手柄焦点跳转按帧模拟

用法：
  sdv-dev input --hold <Button[+Button2]> [--ticks 30] [--aim-tile <x,y>] [--out <shot.png>]
      在进程内按帧注入持续按键（完全不抢玩家桌面键鼠焦点，兼容 `--background` 离屏窗口！）：
      - 真实物理走动/冲刺/碰撞/踩传送点：`sdv-dev input --hold W --ticks 30` 或 `--hold ""LeftShift+D"" --ticks 25`
      - 农具长按蓄力与释放：`sdv-dev input --aim-tile 65,16 --hold C --ticks 48`（记录 `maxToolPowerReached` 并自动跑完收招帧）
  sdv-dev input --keys <Btn1[,Btn2:Ticks,...]> [--interval-ticks 3] [--out <shot.png>]
  sdv-dev input --gamepad <DPadDown,DPadRight,ControllerA> [--interval-ticks 4]
      自动临时启用手柄控制模式，逐拍注入手柄方向键/肩键/ABXY，完整返回菜单 `currentlySnappedComponent` 的焦点跳转轨迹 (`snapTrail`)！
  sdv-dev input --move <uiX,uiY> [--ticks 4] [--click left|right] [--scroll -120] [--out <hover.png>]
      注入虚拟鼠标坐标触发 SMAPI `CursorMoved` 与菜单 `performHoverAction(x, y)`，直接抓回当前菜单的 `hoverText`、`hoverTitle`、`hoveredItem` 及可选悬停态截图！

常用按键名（兼容全部 SMAPI `SButton`）：
  `W`,`A`,`S`,`D`, `C`(挥工具/确认), `X`(交互), `E`/`Escape`(菜单), `LeftShift`, `MouseLeft`, `MouseRight`,
  `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`, `ControllerA`, `ControllerB`, `ControllerX`, `ControllerY`, `LeftShoulder`, `RightShoulder`");
                    break;

                case "tilesheet":
                case "map-inspect":
                case "map-render":
                case "map-prefab":
                case "map-extract":
                case "map-stamp":
                case "map-autotile":
                case "map-check":
                case "map-find-space":
                case "maps":
                    Console.WriteLine(@"# sdv-dev 地图与贴图全套制作/检视/离线预览工具链

1. 贴图编号网格导出与原版地图检视（无 `Content (unpacked)` 时自动按需直读 `Content\*.xnb`）：
   sdv-dev tilesheet Maps/island_tilesheet_1 --grid --out sheet_grid.png
   sdv-dev map-inspect IslandSouth --rect 14,32,8,6 [--full]
   - 传 `--rect` 时默认精简输出局部图块、局部 Warp/光源与实际引用的 TileSheet（需全图元数据再加 `--full`）。

2. 免进游戏 1 秒离线渲染 `.tmx` 与 Content Patcher `EditMap` 补丁叠加预览：
   sdv-dev map-render assets/CoralBay.tmx --out preview.png --season summer --debug-overlay
   sdv-dev map-render Maps/Town --cp d:\myProject\MyCpMod --auto-crop --debug-overlay --out town_patch.png
   - `--cp <ModDir>` 自动解析 `content.json` 中的 `EditMap`（PatchMap / MapTiles / MapProperties / AddWarps）叠到原版底图；加 `--auto-crop` 自动裁切变更包围盒区域；
   - `--debug-overlay` 图例：红色=不可通行阻挡，蓝色=`Water=T` 水波纹格，绿色=Warp 传送点，黄色=Action/TouchAction，青色=光源。

3. 原版跨层预制件抠取与盖章（自动处理 Back2/Buildings/Front/AlwaysFront、动画帧与属性）：
   sdv-dev map-prefab --list
   sdv-dev map-extract IslandSouth --rect 38,23,4,6 --out palm.prefab.json
   sdv-dev map-stamp assets/CoralBay.tmx --prefab @island-palm --at 12,8
   - 默认不覆盖 `Back` 地板层（防止草地种棕榈树带出黄沙底），需要地板时加 `--include-back`。

4. 字符地形网格自动拼块（自动海岸湿沙过渡 + 13 帧动态海浪 + 草沙边缘）：
   sdv-dev map-autotile --spec terrain.txt --out assets/CoralBay.tmx --theme island
   - 字符图例：`W`=海水, `S`=沙滩, `G`=草地, `P`=木栈桥(无水面渗透), `C`=悬崖岩壁, `D`=土路。

5. `.tmx` 静态深度体检与实机空地扫描（防 SVE/CP 冲突）：
   sdv-dev map-check assets/CoralBay.tmx --entry 15,2
   sdv-dev map-find-space Beach --size 5x4 --near 40,20");
                    break;

                case "warp":
                case "reload-assets":
                    Console.WriteLine(@"# sdv-dev warp / reload-assets — 实机瞬移与着色器/地图/贴图 0.2s 免重启热刷

用法：
  sdv-dev warp <LocationName> [x] [y] [--facing 0..3]
  sdv-dev reload-assets [--project <ModDir>] [--assets <Asset1,Asset2>] [--no-reload-map]

说明：
  - 只改了 `.fx` 着色器、`.tmx` 地图或 `.png`/`.json` 时优先用 `reload-assets --project <ModDir>`，无需重启游戏！
  - 会自动将工程内全部 `.fx` 重编为 `.mgfxo`、精准拷入当前活跃 `modsPath` 对应的模组目录，并在主线程自动调用模组 `ReloadShaders()` 或直接原地替换内存中的 `Effect` 实例！

示例：
  sdv-dev warp IslandSouth 20 35
  sdv-dev reload-assets --project d:\myProject\MyShaderMod
  sdv-dev reload-assets --project d:\myProject\CoralMod --assets Maps/CoralBay");
                    break;

                case "eval":
                    Console.WriteLine(@"# sdv-dev eval — SMAPI 主线程实时动态 C# 求值（支持 --stdin 免转义与顶层 using）

用法：
  @'
  using xTile.Layers;
  var tile = Location.Map.GetLayer(""Buildings"")?.Tiles[10, 12];
  return new { loc = Location.NameOrUniqueName, hasBuildingTile = tile != null };
  '@ | sdv-dev eval --stdin
  sdv-dev eval --file <script.cs>
  sdv-dev eval ""<简单无引号表达式>""

特性与防踩坑提示：
  - 推荐在 PowerShell 中用 `@' ... '@ | sdv-dev eval --stdin` 管道传入代码，彻底避免命令行双引号被剥离！
  - 严禁手写 `eval` 只改 `Game1.options.desiredBaseZoomLevel` 调视野（未同步重建 `@Game1.screen` 会导致画面右侧出现假 UV 撕裂线）——切缩放请直接用 `sdv-dev world --zoom 0.8` 或 `screenshot --zoom 0.8`！
  - 实时修改模组配置请优先用 `sdv-dev config <ModId> --set Key=Val`（或 `--reset`），模拟键鼠/手柄请用 `sdv-dev input`！
  - 脚本顶部的 `using ...;` 会自动提升到编译单元顶层；已默认预置 `StardewValley.*`、`StardewModdingAPI.*`、`Microsoft.Xna.Framework(.Graphics)`、`xTile.*`、`System.Text` 与当前活跃 Mods 目录中所有模组 DLL；
  - 内置变量：`Helper` (`IModHelper`), `Monitor` (`IMonitor`), `Player` (`Farmer`), `Location` (`GameLocation`)。");
                    break;

                case "start":
                case "restart":
                case "window":
                case "win":
                case "reload":
                case "live":
                case "saves":
                case "stop":
                case "menu":
                case "ui":
                case "ui-click":
                case "player":
                case "cmd":
                case "logs":
                    Console.WriteLine(@"# sdv-dev 实机生命周期、窗口前后台/无控制台控制、模组隔离 (--isolated) 与热重启

启动、窗口模式与热迭代：
  sdv-dev live                                                         查看进程、桥接状态、真实窗口可见性 (`windowVisible`/`consoleVisible`/`desktopName`) 与坐标
  sdv-dev start --isolated [--project <dir>] [--no-console] [--background] [--no-focus] --test-world
      - 默认显示前台游戏窗口；加 `--no-console` 则**只显示星露谷游戏画面窗口、不弹 SMAPI 黑色控制台窗口**！
      - 加 `--background` 则连游戏画面也移至屏幕外无打扰隐藏运行（满分辨率 60FPS 照常截图）；
      - 热驻 `--daemon` 在游戏运行期间会自动续命（不因 10 分钟空闲自退而连坐关闭游戏）。
  sdv-dev window [--show | --background] [--no-console | --show-console] [--focus]
      无需重启游戏，运行中 10ms 秒切前后台窗口显示，或单独隐藏/恢复 SMAPI 黑色控制台窗口！
  sdv-dev restart --project <dir> [--isolated | --full-mods] [--no-console] [--same-desktop | --background] [--no-focus] --test-world
      先执行 check+build（失败不关原游戏），通过后无锁暂存并在旧进程退出后自动替换 DLL（默认继承上一轮窗口与无控制台状态）！
  sdv-dev reload --project <cpDir> [--assets A,B]                      免重启热重载 Content Patcher 包
  sdv-dev reload-assets --project <dir> --assets Maps/MyMap            免重启热刷 C#/CP 模组的地图与贴图资产
  sdv-dev config <ModId> [--get [Key] | --set Key=Val ...] [--no-save] 免重启 0.05 秒实时读写模组配置

游戏内检视、传送、截图与日志：
  sdv-dev warp <Location> [x] [y]                                      瞬移至目标地图坐标
  sdv-dev screenshot --mode screen|map [--ui-overlay] --out <file.png> 实机画面或全图截图
  sdv-dev screenshot --ab-compare <ModId> --out <ab.png>               同机位一键生成「原版关闭 vs 模组开启」左右对比图
  sdv-dev screenshot --burst [4] [--interval-ticks 15] [--pan-test] --out <grid.png>
  sdv-dev player / sdv-dev ui / sdv-dev ui-click (--id|--match|--response|--close)
  sdv-dev cmd ""patch summary""                                          执行任意 SMAPI / CP / debug 控制台命令
  sdv-dev logs [--level Error|Warn] [--mod <Id>] [-n 40]               读取 SMAPI-latest.txt 日志
  sdv-dev menu / sdv-dev stop [--force]                                不保存退回标题或安全关闭游戏");
                    break;

                case "api":
                    Console.WriteLine(@"# sdv-dev api — 模糊搜索 C# 类、方法（含私有方法）、属性、字段与 SMAPI 事件

用法：
  sdv-dev api <keywords> [--kind class|method|property|event|field|constructor] [--asm sdv|smapi|gamedata|monogame|xtile|harmony|toolkit] [-n 15] [--json]

示例：
  sdv-dev api ""shop purchase""
  sdv-dev api ""RenderTarget2D"" --asm monogame
  sdv-dev api ""XnaDisplayDevice"" --asm xtile
  sdv-dev api ""SModHooks"" --asm smapi");
                    break;

                case "decompile":
                case "dec":
                    Console.WriteLine(@"# sdv-dev decompile — 覆盖 Stardew / SMAPI / MonoGame / xTile / Harmony 的实时 C# 反编译

用法：
  sdv-dev decompile <Type | Type.Member> [--lines 200] [--full] [--grep <pattern> [-C 15]] [-g <gamePath>]

示例：
  sdv-dev decompile StardewValley.Locations.Beach.resetLocalState
  sdv-dev decompile Game1.DrawWorld --grep lighting -C 12
  sdv-dev decompile SModHooks.OnRendered
  sdv-dev decompile XnaDisplayDevice");
                    break;

                case "shader":
                case "shader-compile":
                case "fx":
                    Console.WriteLine(@"# sdv-dev shader — 内置免安装 HLSL (.fx) -> MonoGame DesktopGL (.mgfxo) 编译器

用法：
  sdv-dev shader <file.fx | modProjectDir> [--out <file.mgfxo>] [--defines A=1;B=2] [--profile OpenGL|DirectX_11] [--debug]

说明：
  - 内嵌与星露谷 1.6 (MonoGame 3.8.0.1641) 字节码版本严格对齐的 `mgfxc` + `MojoShader` (MGFX v9 OpenGL)；
  - `sdv-dev build` 会自动把模组工程内全部 `*.fx` 编译为同名 `.mgfxo`，`sdv-dev check` 也会在编译前静态预检 HLSL 语法。");
                    break;

                case "config":
                case "mod-config":
                    Console.WriteLine(@"# sdv-dev config — 免重启 0.05 秒实时读取/修改/重置模组配置（兼容 C# SMAPI 模组与 CP 包）

用法：
  sdv-dev config <ModUniqueID> [--get [Key]]
  sdv-dev config <ModUniqueID> --set Key1=Val1 [Key2=Val2 ...] [--no-save]
  sdv-dev config <ModUniqueID> --reset [--no-save]

示例：
  sdv-dev config Author.LightingMod --get
  sdv-dev config Author.LightingMod --set SunsetWarmth=1.4 BloomIntensity=0.85
  sdv-dev config Author.LightingMod --reset");
                    break;

                case "screenshot":
                    Console.WriteLine(@"# sdv-dev screenshot — 实机画面/全图截图、安全缩放、UI 拓扑叠图、A/B 光影对比与动态四宫格连拍

用法：
  sdv-dev screenshot --mode screen|map [--zoom 0.75] [--ui-overlay] --out <shot.png>
  sdv-dev screenshot --ab-compare <ModId[:BoolProp]> [--ab-toggle <BoolProp>] [--zoom 0.8] --out <ab_compare.png>
      同机位同时间自动切换目标模组开关（关闭截左图 -> 开启跑 2 ticks 截右图 -> 恢复原值），合成左右标注对比图！
  sdv-dev screenshot --burst [4] [--interval-ticks 15 | --interval-ms 250] [--pan <dx,dy> | --pan-test] [--zoom 0.8] --out <burst_grid.png>
      按固定游戏 Tick 间隔连续抓拍多帧（可选同步平滑平移镜头测残影/视差），自动合成带帧号与 Tick 角标的 2x2 时序四宫格 PNG！
  - 注：`--zoom <0.35..3.0>` 内部自动调用 `SetWindowSize` 重建 `@Game1.screen` 缓冲，拍完自动还原，绝不产生右侧 UV 撕裂假竖线。");
                    break;

                case "schema":
                    Console.WriteLine(@"# sdv-dev schema — 查询 1.6 GameData 数据表强类型定义

用法：
  sdv-dev schema [TypeOrAssetPath] [--list] [-n 5] [--json]

示例：
  sdv-dev schema --list
  sdv-dev schema Data/Locations
  sdv-dev schema Data/Machines");
                    break;

                case "data":
                    Console.WriteLine(@"# sdv-dev data — 中英文双语检索原版解包 JSON 数据

用法：
  sdv-dev data <中文名|英文名|条目ID> [--asset Data/AssetName] [-n 5] [--json]
  sdv-dev data --list-assets

示例：
  sdv-dev data IslandSouth --asset Data/Locations
  sdv-dev data 小桶 --asset Data/Machines
  sdv-dev data dwop --asset Audio/Cues
  sdv-dev data Music --asset Audio/Cues -n 20");
                    break;

                case "doc":
                case "docs":
                    Console.WriteLine(@"# sdv-dev doc — 全文检索开发文档、Wiki 与 1.6 游戏内建语法表

用法：
  sdv-dev doc <query> [--cat ContentPatcher|Wiki|GameStateQuery|TriggerAction|EventCommand|Harmony|SMAPI|GameData] [-n 4]
  sdv-dev doc --list [--cat <Category>]");
                    break;

                case "new":
                case "init":
                case "gen-gmcm":
                case "gmcm":
                case "check":
                case "validate":
                case "build":
                case "pack":
                case "package":
                    Console.WriteLine(@"# sdv-dev new / gen-gmcm / shader / check / build / pack — 工程与着色器构建、1.6 深度体检与发布打包

用法：
  sdv-dev new <cp|csharp> --name <ModName> [--author <Author>] [--out <ParentDir>]
  sdv-dev gen-gmcm [--project <ModDir>] [--config <ModConfig.cs>]
      基于 AST 解析 `ModConfig.cs` 属性（含 `SButton`/`KeybindList`）、注释与 `[Range]`，自动生成 `IGenericModConfigMenuApi.cs`、`GmcmIntegration.g.cs` 并增量补齐 `i18n/default.json` 与 `zh.json`！
  sdv-dev shader <file.fx | ModDir> [--out <file.mgfxo>] [--defines A=1;B=2] [--profile OpenGL|DirectX_11]
      内置免装 `mgfxc 3.8.0.1641` + `MojoShader` 将 `.fx` 编译为星露谷 1.6 DesktopGL 对齐的 `.mgfxo` 字节码（兼容中文路径与子目录 `#include`）。
  sdv-dev check [--project <ModDir>] [--json]
      执行 1.6 深度静态校验：
      - Content Patcher: `GameData` 强类型字段与 JSON 类型对账（含 `List<T>` 表与食谱斜杠表）、跨表物品/信件/Buff/任务 ID 外键断链与 `(O)`/`(BC)` 前缀核验、`GSQ`/`TriggerAction`/`Event` 语法及 XACT 音效拼写、NPC 日程/对话控制符/肖像 `64x64` 帧越界、`EditImage`/`SpriteIndex` 越界、`i18n` 双向漏译对账；
      - 着色器: 静态预编译校验全部 `*.fx` 的 HLSL 语法、OpenGL `ps_3_0`/`vs_3_0` 兼容性及漏编 `.mgfxo` 提醒；
      - C# SMAPI & MonoGame: `[HarmonyPatch]` 目标存在性/重载歧义/返回值与注入参数(`__instance`/`__result`/`___field`)静态验签、`Display.Rendered*` 内重复 `SpriteBatch.Begin` 崩溃、每帧无缓存 `new Texture2D/RenderTarget2D` 显存泄漏、离屏 `SetRenderTarget` 未还原、非预乘 `new Color(r,g,b,a)` 泛白及分屏静态变量隐患；
      - 地图: 全部 `.tmx` 尺寸、GID、Warp 与 BFS 孤岛检测。
  sdv-dev build [--project <ModDir>] [--deploy [--both | --single-target]] [--mods-path <Dir>] [--no-publicize]
      免装 .NET SDK 秒编 .NET 6.0 C# 模组并自动将工程内全部 `*.fx` 编译为 `.mgfxo`；加 `--deploy` 时默认无锁同步到真实 `<GamePath>\Mods` 及已存在的 `isolated-mods` 双端目录（免手写 `Copy-Item`）！
  sdv-dev pack [--project <ModDir>] [--out <Release.zip>] [--skip-check]
      编译 + 全量静态校验通过后，生成顶层带 `<ModName>/manifest.json`、剔除全部源码/`.fx`/PDB/临时配置的标准发布压缩包并输出 SHA256。");
                    break;

                case "sprite":
                case "sprite-check":
                    Console.WriteLine(@"# sdv-dev sprite — 物品/建筑/NPC 动作帧与肖像表情格切分体检与预览

用法：
  sdv-dev sprite <PngOrAsset> [--preset auto|object|craftable|npc|portrait] [--cell <WxH>] [--out <preview.png>] [--zoom 4] [--no-bbox]

说明：
  - 自动识别肖像 (`64x64`，标注 Neutral/Happy/Sad/Unique/Blush/Angry)、NPC 行走表 (`16x32`，标注 Down/Right/Up/Left 与中轴线偏移警告)、大件 (`16x32`) 与普通物品 (`16x16`)；
  - 检测贴图尺寸整除性、统计每个非空帧的紧致像素包围盒 (`contentBounds`) 与水平居中偏移，导出带棋盘格透明底的编号网格预览图。

示例：
  sdv-dev sprite Portraits/Abigail --out abigail_portrait_grid.png
  sdv-dev sprite Characters/Abigail --out abigail_walk_grid.png
  sdv-dev sprite assets/custom_machines.png --preset craftable --out machines_grid.png");
                    break;

                case "asset":
                case "asset-inspect":
                case "harmony":
                case "harmony-inspect":
                case "profile":
                    Console.WriteLine(@"# sdv-dev asset / harmony / profile — 实机资产追踪、Transpiler 实时 IL 透视与帧耗时采样

用法：
  sdv-dev asset <TargetAsset> [--key <EntryKeyOrKeyword>] [--export <out.json|out.png>] [--cp-trace] [-n 10]
      读取实机经 Content Patcher / C# `AssetRequested` 合并后的最终运行时数据表、地图信息或合成贴图；
      加 `--cp-trace` 自动联动官方 Content Patcher `patch summary asset ""<Target>""` 输出完整补丁生效/未激活原因！
  sdv-dev harmony [<TypeOrMethodKeyword>] [--mod <OwnerUniqueID>] [--il] [-n 40]
      实时列出 `0Harmony` 已挂载的全部补丁方法、所属模组、Prefix/Postfix/Transpiler/Finalizer 优先级；
      加 `--il` 直接调用 `PatchProcessor.GetCurrentInstructions` 导出打完所有 Transpiler 后的最终 IL 指令流，并按执行步进自动标注每条新增指令的归属模组！
  sdv-dev profile [--seconds 5] [--top 12]
      按需动态注入临时计时探针（采样完立即 `UnpatchAll`，平时 0 开销）：输出平均帧耗时、1% Low 帧率、GC 分配速率 (MB/s)，以及耗时最高的 Top N SMAPI 事件回调与 Harmony 补丁方法！

示例：
  sdv-dev asset Data/Objects --key Wine --cp-trace
  sdv-dev harmony ShopMenu.tryToPurchaseItem --il
  sdv-dev profile --seconds 5 --top 10");
                    break;

                case "interact":
                    Console.WriteLine(@"# sdv-dev interact — 世界地块/机器/NPC/农具交互原语（免手写 eval 跑完投放->投料->收获闭环）

用法：
  sdv-dev interact --place <ItemId> <x,y> [--no-move] [--screen host|farmhand]
      在目标格摆放机器/家具/物品（如 `--place ""(BC)12"" 12,14` 摆小桶）
  sdv-dev interact --drop-in <ItemId[:数量[:品质]]> <x,y>
      向目标格的机器或建筑投料（如 `--drop-in ""(O)258"" 12,14` 塞蓝莓进小桶，返回产物 ID 与剩余分钟数）
  sdv-dev interact --right-click [<x,y>]
      自动站到目标格旁面向目标触发原版 `checkAction`（收获已就绪机器产物并输出背包增量、与 NPC 对话、打开商店柜台或开门）
  sdv-dev interact --use-tool <Hoe|Pickaxe|Axe|WateringCan|Scythe|ItemId> <x,y> [--power 1]
      面向目标格挥动农具或武器（水壶缺水时自动补满）

标准机器全链路自测示例（零手写 eval）：
  sdv-dev interact --place ""(BC)12"" 12,14
  sdv-dev interact --drop-in ""(O)258"" 12,14
  sdv-dev world --advance-days 7
  sdv-dev interact --right-click 12,14");
                    break;

                case "event-play":
                case "event":
                case "world":
                case "mp":
                case "multiplayer":
                    Console.WriteLine(@"# sdv-dev event-play / world / mp — 剧情实时排演、确定性世界推进与分屏多端联调

1. 剧情事件实时沙盒 (`event-play`)：
   sdv-dev event-play <Location> <EventId> [--keep-seen]        自动移除已读标记、切图并无视前置条件拉起原版/Mod 剧情
   @'
   continue / 64 15 / farmer 64 16 2 Abigail 64 14 2 / pause 300 / speak Abigail ""嗨，直接预览剧本不用重启！"" / end
   '@ | sdv-dev event-play Town --stdin                         免改 JSON 实时排演原始 Event 脚本字符串！
   sdv-dev event-play --status | --step | --skip                查看当前执行到第几条指令/演员坐标、单步推进对话或跳过卡死剧情

2. 确定性世界控制、安全视野缩放与真实跨日管线 (`world`)：
   sdv-dev world --time 1800 --weather rain --zoom 0.80         切换时间、天气与视野缩放（正规重建 Game1.screen 缓冲防 UV 接缝！）
   sdv-dev world --season fall --day 15 --money 50000 --refill
   sdv-dev world --friendship Abigail:1000 --add-item (O)74:5:4 --mail MyMod_Letter
   sdv-dev world --advance-days 3 [--save]                      驱动真实的 `Game1.NewDay` 跨夜结算管线（触发作物生长/机器产出/信件投递/SMAPI DayStarted）

3. 本地分屏联机双端测试 (`mp` + `--screen`)：
   sdv-dev mp status                                            查看所有分屏实例 (screen 0=host, 1..3=farmhand) 状态
   sdv-dev mp add-farmhand                                      自动确保农场有联机小屋并在同进程内拉起本地分屏客机实例
   sdv-dev mp remove-farmhand [--screen 1]
   - `player`、`ui`、`ui-click`、`warp`、`eval`、`event-play`、`world` 均支持加 `--screen host|farmhand|0..3` 指定在主机或客机上下文执行！");
                    break;

                case "checkpoint":
                case "cp-save":
                case "schedule-check":
                case "schedule":
                    Console.WriteLine(@"# sdv-dev checkpoint / schedule-check — 测试档秒级时光机快照与 NPC 日程寻路实机推演

1. 测试档状态快照时光机 (`checkpoint`)：
   sdv-dev checkpoint save <快照名>        将当前世界所有机器/作物进度、玩家背包、时间与精确站位秒存为命名快照
   sdv-dev checkpoint load <快照名>        随时（即使改完 C# 代码冷重启后）1 秒回档到该快照的精确分钟与坐标反复测分支！
   sdv-dev checkpoint list                 离线列出所有已保存的测试快照
   sdv-dev checkpoint delete <快照名>      删除指定快照

2. NPC 日程寻路实机推演 (`schedule-check`)：
   sdv-dev schedule-check <NpcName> [--key spring]
   直接调用原版 `NPC.parseMasterSchedule` 实机跑完该 NPC 所有日程段的跨图寻路，瞬间揪出不可达目标格或断链 Warp！");
                    break;

                default:
                    PrintHelp(null);
                    break;
            }
        }

        #endregion

        private static readonly HashSet<string> GlobalKnownOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--ab", "--ab-compare", "--ab-toggle", "--action", "--add-item", "--advance-days", "--aim-tile", "--all-mods", "--asm", "--asset", "--assets", "--at", "--author", "--auto-crop",
            "--background", "--both", "--burst", "--button", "--cat", "--cell", "--clear", "--click", "--close", "--code-b64", "--cold", "--config", "--context", "--cp", "--cp-trace", "--crop",
            "--day", "--days", "--db", "--debug-overlay", "--debug", "--defines", "--deploy", "--docs-only", "--docs-out", "--drop-in", "--dropin",
            "--entry", "--export", "--facing-up", "--facing", "--file", "--focus", "--force", "--friendship", "--full-mods", "--full",
            "--game-path", "--gamepad", "--get", "--github", "--give", "--grep", "--grid", "--headless", "--help", "--hide", "--hide-console", "--hold", "--hover", "--id", "--il", "--include-back", "--instructions", "--interval-ms", "--interval-ticks", "--isolated",
            "--json", "--keep-seen", "--keep-window", "--key", "--keys", "--kind", "--layer", "--level", "--limit", "--lines", "--list-assets", "--list",
            "--mail", "--match", "--min-version", "--mod", "--mode", "--mods-path", "--money", "--move", "--name", "--near", "--new-test-save",
            "--no-back", "--no-bbox", "--no-console", "--no-focus", "--no-move", "--no-publicize", "--no-ref", "--no-reload-map", "--no-save", "--no-window", "--off", "--offline", "--optional",
            "--out", "--pan", "--pan-test", "--place", "--power", "--prefab", "--preset", "--press", "--profile", "--project", "--rect", "--refill", "--reload-map",
            "--reset", "--reset-test-world", "--response", "--right-click", "--same-desktop", "--save", "--scale", "--screen", "--script-b64", "--script", "--scroll", "--search",
            "--season", "--seconds", "--set", "--settle-ticks", "--show", "--show-console", "--show-paths", "--single-target", "--size", "--skip-check", "--skip", "--spec", "--status", "--stdin", "--step",
            "--tail", "--tap", "--target", "--test-world", "--theme", "--ticks", "--tile-size", "--time", "--timeout", "--tool", "--top", "--trace",
            "--ui", "--ui-overlay", "--unpack", "--use-tool", "--visible", "--weather", "--with-mods", "--xy", "--year", "--zoom",
            "-a", "-c", "-C", "-d", "-e", "-f", "-g", "-h", "-k", "-l", "-m", "-n", "-o", "-p", "-r", "-s", "-t", "-w", "-y", "-z", "-"
        };

        private static void ValidateNoUnknownGlobalFlags(string cmd, string[] args)
        {
            foreach (var a in args)
            {
                if (!a.StartsWith("-", StringComparison.Ordinal) || a.Length == 1)
                    continue;
                // Allow negative numbers like `-100` or `-4`
                if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    continue;
                if (!GlobalKnownOptions.Contains(a))
                    throw new ArgumentException($"Unknown option `{a}` for `sdv-dev {cmd}`. Run `sdv-dev {cmd} --help` to view valid options.");
            }
        }

        private static void EnsureCommandOptions(string cmd, string[] args, string[] valueOptions, params string[] booleanFlags)
        {
            var allowed = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
            foreach (var bf in booleanFlags)
                allowed.Add(bf);

            foreach (var a in args)
            {
                if (!a.StartsWith("-", StringComparison.Ordinal) || a.Length == 1)
                    continue;
                if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    continue;
                if (!allowed.Contains(a))
                    throw new ArgumentException($"Option `{a}` is not supported by `sdv-dev {cmd}`. Run `sdv-dev {cmd} --help` to view valid options.");
            }
        }

        private static bool HasFlag(string[] args, params string[] names) =>
            args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));

        private static string? GetOption(string[] args, params string[] names)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (names.Any(n => args[i].Equals(n, StringComparison.OrdinalIgnoreCase)))
                    return args[i + 1];
            }
            return null;
        }

        private static int GetIntOption(string[] args, string name1, string name2, int defaultValue)
        {
            string? val = GetOption(args, name1, name2);
            return int.TryParse(val, out int parsed) && parsed >= 0 ? parsed : defaultValue;
        }

        private static List<string> GetPositionalList(string[] args, params string[] optionsWithValues)
        {
            var optSet = new HashSet<string>(optionsWithValues, StringComparer.OrdinalIgnoreCase);
            var parts = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (optSet.Contains(a))
                {
                    i++;
                    continue;
                }
                if (a.StartsWith("-", StringComparison.Ordinal))
                    continue;
                parts.Add(a);
            }
            return parts;
        }

        private static string GetPositionalQuery(string[] args, params string[] optionsWithValues) =>
            string.Join(" ", GetPositionalList(args, optionsWithValues)).Trim();

        private static List<string> ParseCsvList(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? new List<string>()
                : raw!.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        private static (int X, int Y)? ParsePoint2(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var p = raw!.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y))
                return (x, y);
            return null;
        }

        private static (int X, int Y, int W, int H)? ParseRect4(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var p = raw!.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 4 &&
                int.TryParse(p[0], out int x) &&
                int.TryParse(p[1], out int y) &&
                int.TryParse(p[2], out int w) &&
                int.TryParse(p[3], out int h))
            {
                return (x, y, w, h);
            }
            return null;
        }
    }
}
