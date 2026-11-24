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
                "mp" or "multiplayer" => RunMultiplayer(rest),
                "warp" => RunWarp(rest),
                "screenshot" => RunScreenshot(rest),
                "stop" => RunStop(rest),
                "menu" => RunMenu(),
                "eval" => RunEval(rest),
                "cmd" => RunConsoleCmd(rest),
                "player" => RunPlayer(rest),
                "ui" => RunUi(rest),
                "ui-click" => RunUiClick(rest),
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
            string target = GetPositionalQuery(args, "--lines", "-n", "--game-path", "-g");

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
                string code = DecompileService.DecompileTarget(cfg.GamePath, target, lines, full, sharedDb);
                Console.WriteLine(code);
                return 0;
            }

            KbDatabase? localDb = File.Exists(cfg.DbPath) ? new KbDatabase(cfg.DbPath) : null;
            using (localDb)
            {
                string code = DecompileService.DecompileTarget(cfg.GamePath, target, lines, full, localDb);
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
            bool noPublicize = HasFlag(args, "--no-publicize");
            string? explicitModsRoot = GetOption(args, "--mods-path");
            string modPath = GetOption(args, "--project", "-p") ?? GetPositionalQuery(args, "--project", "-p", "--game-path", "-g", "--mods-path");
            if (string.IsNullOrWhiteSpace(modPath))
                modPath = Directory.GetCurrentDirectory();

            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? targetModsRoot = explicitModsRoot ?? (deploy ? LiveGameController.GetActiveModsRoot(cfg.GamePath) : null);

            var res = DotNet6ModCompiler.BuildAndOptionallyDeploy(
                modPath,
                cfg.GamePath,
                deploy,
                targetModsRoot,
                publicize: !noPublicize);
            PrintJson(res);
            return res.Success ? 0 : 1;
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
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outPath = GetOption(args, "--out", "-o");
            string? preset = GetOption(args, "--preset");
            string? cellStr = GetOption(args, "--cell", "-c");
            string? zoomStr = GetOption(args, "--zoom", "-z");
            bool noBbox = HasFlag(args, "--no-bbox");
            string target = GetPositionalQuery(args, "--game-path", "-g", "--out", "-o", "--preset", "--cell", "-c", "--zoom", "-z");

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
            string mapName = GetPositionalQuery(args, "--game-path", "-g", "--rect", "-r", "--layer", "-l");

            if (string.IsNullOrWhiteSpace(mapName))
            {
                PrintHelp("map-inspect");
                return 1;
            }

            (int X, int Y, int W, int H)? rect = ParseRect4(rectStr);
            PrintJson(MapInspectorAndRenderer.InspectMap(mapName, cfg.GamePath, rect, layer));
            return 0;
        }

        private static int RunMapRender(string[] args)
        {
            var cfg = KbConfig.Load(GetOption(args, "--game-path", "-g"));
            string? outPath = GetOption(args, "--out", "-o");
            string? season = GetOption(args, "--season", "-s");
            bool debugOverlay = HasFlag(args, "--debug-overlay", "--debug");
            bool showPaths = HasFlag(args, "--show-paths");
            int zoom = GetIntOption(args, "--zoom", "-z", defaultValue: debugOverlay ? 2 : 1);
            string mapTarget = GetPositionalQuery(args, "--game-path", "-g", "--out", "-o", "--season", "-s", "--zoom", "-z");

            if (string.IsNullOrWhiteSpace(mapTarget) || string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("map-render");
                return 1;
            }

            PrintJson(MapInspectorAndRenderer.RenderMapToPng(mapTarget, cfg.GamePath, outPath!, season, debugOverlay, zoom, showPaths));
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
                resetTestWorld: resetTestWorld);
            Console.WriteLine(json);
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
                resetTestWorld: resetTestWorld);
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
            if (!string.IsNullOrWhiteSpace(project))
            {
                string fullProj = Path.GetFullPath(project!);
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
                deployedDir = DotNet6ModCompiler.DeployAssetsOnlyToModsRoot(fullProj, activeModsRoot, id ?? Path.GetFileName(fullProj));
            }

            string[] assets = ParseCsvList(assetsRaw).ToArray();
            string bridgeResp = BridgeClient.SendRequired(new
            {
                action = "reload_assets",
                uniqueId = id,
                assets,
                reloadMap
            });

            if (deployedDir != null)
            {
                PrintJson(new
                {
                    ok = true,
                    deployedAssetsTo = deployedDir,
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
            string mode = (GetOption(args, "--mode", "-m") ?? "screen").Trim().ToLowerInvariant();
            string? outPath = GetOption(args, "--out", "-o");
            string? scaleStr = GetOption(args, "--scale", "-s");
            float scale = float.TryParse(scaleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float sv) ? sv : 1.0f;

            if (string.IsNullOrWhiteSpace(outPath))
            {
                outPath = GetPositionalQuery(args, "--mode", "-m", "--scale", "-s");
            }

            if (string.IsNullOrWhiteSpace(outPath))
            {
                PrintHelp("screenshot");
                return 1;
            }

            string fullOut = Path.GetFullPath(outPath!);
            Console.WriteLine(BridgeClient.SendRequired(new
            {
                action = "screenshot",
                mode,
                outPath = fullOut,
                scale
            }, connectTimeoutMs: 5000));
            return 0;
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
            string? screen = GetOption(args, "--screen");
            bool refill = HasFlag(args, "--refill");
            bool save = HasFlag(args, "--save");

            int? day = int.TryParse(dayStr, out int dv) ? dv : (int?)null;
            int? year = int.TryParse(yearStr, out int yv) ? yv : (int?)null;
            int? time = int.TryParse(timeStr, out int tv) ? tv : (int?)null;
            int? money = int.TryParse(moneyStr, out int mv) ? mv : (int?)null;
            int? advanceDays = int.TryParse(advStr, out int av) ? av : (int?)null;

            if (season == null && !day.HasValue && !year.HasValue && !time.HasValue && weather == null &&
                !money.HasValue && friendship == null && addItem == null && mail == null && !advanceDays.HasValue && !refill)
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

            Console.WriteLine(@"sdv-dev 星露谷物语 1.6 模组开发、项目依赖、地图渲染与实机自动化测试命令行

sdv-dev [OPTIONS] SUBCOMMAND

OPTIONS:
  -h, --help                  打印帮助信息；子命令详情用 `sdv-dev <命令> --help` 或 `sdv-dev help <命令>`

知识检索与源码检视:
  help                        查看子命令或流程详细说明（如 `sdv-dev help deps`、`sdv-dev help map-render`）
  api                         用模糊英文意图词或驼峰词搜未知类、方法（含私有方法）与 SMAPI 事件
  decompile                   按 MetadataToken 实时反编译原版/SMAPI 源码并输出 Harmony 补丁提示
  schema                      查询 1.6 GameData 数据表强类型字段与官方 XML 注释
  data                        按中文名、英文名或 ID 检索原版解包 JSON 配置模板
  doc                         全文检索 Content Patcher、SMAPI、Wiki、GameStateQuery、TriggerAction、EventCommand 文档
  status / sync               查看知识库状态，或同步重建本地 SQLite FTS5 索引

工程构建、项目依赖管理、1.6 语义体检与发布打包:
  deps                        项目依赖管理（扫描本地 Mods、兜底拉取 GitHub Releases、导出前置 API 接口、自动挂载隔离环境）
  new                         创建 1.6 Content Patcher 内容包 (`cp`) 或 SMAPI C# 模组 (`csharp`，预集成 Publicizer) 工程骨架
  gen-gmcm                    解析 `ModConfig.cs` AST 一键生成 GMCM 配置菜单 (`IGenericModConfigMenuApi` + 注册代码 + 双语 `i18n`)
  check                       1.6 深度语义校验（GameData 强类型字段/GSQ/Event脚本/EditImage越界/i18n对账/Harmony静态验签/联机隐患/.tmx）
  build                       免装 .NET SDK 编译 .NET 6.0 C# 模组（内置 `BepInEx.AssemblyPublicizer` 免反射直访私有成员 + Harmony 验签）
  pack                        规范化发布打包：自动编译+全量静态校验并生成 Nexus/CurseForge 标准结构 `<ModName> <Version>.zip`

地图、精灵图与贴图离线/实机工具链:
  tilesheet                   从原版解包目录导出贴图 PNG，支持 `--grid` 自动叠加 `0..N` TileIndex 编号网格
  sprite                      物品/建筑/NPC 4方向行走帧 (`16x32`) / 肖像表情格 (`64x64`) 切分体检与包围盒对齐预览图导出
  map-inspect                 检视地图图层、TileSheets、Warp/Light/动画帧与指定 `--rect` 局部图块/通行掩码矩阵
  map-render                  1 秒离线合成多图层 `.tmx` 预览图（`--debug-overlay` 标注红阻挡/蓝水面/绿Warp/黄Action与标尺）
  map-prefab / map-extract    列出内置原版跨层预制件（棕榈树/栈桥/浪花等）或从原版地图抠取 5 层结构+动画保存为预制件
  map-stamp                   将跨层预制件盖章合入目标 `.tmx`（自动合并 tileset、动画与属性，默认不盖 Back 防沙块穿帮）
  map-autotile                根据紧凑字符网格（W水/S沙/G草/P桥/C崖）自动过渡边缘并挂接 13 帧海浪生成 `.tmx`
  map-check                   离线深度体检 `.tmx`：PNG 真实像素尺寸、GID 越界、跨图 Warp 落点安全与 BFS 连通性孤岛检测
  map-find-space              在实机（含 CP/SVE 合并后）扫描地图安全空地，自动避开水面/灌木/建筑/Warp 与 NPC 日程路线

实机自动化控制、资产/Harmony 探针、剧情沙盒与分屏联调:
  live / saves                查看当前游戏进程、桥接状态、活跃 Mods 路径、分屏数、所处世界坐标，或列出本机存档
  start                       启动 SMAPI（支持 `--isolated` 模组隔离自动带入前置、`--test-world` SVE 兼容专属测试档）
  restart                     先 check+build（失败保留原进程），通过后重启部署（支持 `--isolated` 极速隔离重启）
  reload / reload-assets      免重启热重载 CP 包，或对 C#/CP 模组仅同步非 DLL 资产（.tmx/.png/.json）并立即刷新当前地图
  asset                       透视实机合并后的最终资产数据/贴图，支持 `--cp-trace` 抓取官方 Content Patcher `patch summary` 链路
  harmony                     查询实机 Harmony 补丁链；加 `--il` 导出经全部 Transpiler 改写后的实时 IL 指令序列与每段模组归属
  profile                     按需开启（平时零开销）实时采样帧率、1% Low FPS、GC 分配速率及耗时 Top N 的 SMAPI 事件与 Harmony 补丁
  interact                    世界实体交互模拟：`--place` 摆机器、`--drop-in` 投料、`--right-click` 右键收获/对话/开店、`--use-tool` 挥农具
  event-play                  剧情事件实时沙盒：按 EventID 强制重播（自动清已读）或通过 `--stdin` 直接预览原始 Event 脚本字符串
  world                       确定性世界控制：改时间/季节/天气/好感/发物品/塞信件，支持 `--advance-days N` 驱动真实跨夜结算管线
  mp                          本地分屏联机测试：列出分屏实例、`add-farmhand` 一键拉起客机，全系实机命令均支持 `--screen host|farmhand`
  warp                        主线程瞬移玩家至指定地点与坐标（如 `sdv-dev warp IslandSouth 20 35`）
  screenshot                  实机截图：`--mode screen` 截取实时画面（光影/UI/粒子），`--mode map` 全图高清截图
  eval                        在 SMAPI 主线程动态执行 C# 代码（默认内置 Publicizer 免反射直访私有成员、支持 `--stdin` 与 `--screen`）
  cmd / player / ui / ui-click / logs / menu / stop
  daemon-status / daemon-stop 查看或关闭 10 分钟空闲自退的热驻加速守护进程");
        }

        private static void PrintTopicHelp(string topic)
        {
            switch (topic)
            {
                case "deps":
                case "dep":
                    Console.WriteLine(@"# sdv-dev deps — 项目依赖管理（前置模组解析、编译引用与隔离环境自动挂载）

用法：
  sdv-dev deps [--project <ModDir>] [--offline]              解析工程 manifest.json 与 sdv-project.json 的全部直接+传递前置依赖
  sdv-dev deps list [<关键词>]                               扫描本机 `<GamePath>\Mods` 与本地缓存中所有可用前置模组
  sdv-dev deps add <UniqueID|别名> [--project <ModDir>] [--optional] [--no-ref] [--github <owner/repo>]
  sdv-dev deps remove <UniqueID|别名> [--project <ModDir>]   移除前置依赖声明
  sdv-dev deps api <UniqueID|别名> [<TypeOrMethod>]          直接反编译前置模组公开的 `I*Api` / `GetApi()` 接口签名（免猜参数！）
  sdv-dev deps isolated [--on|--off] [--project <ModDir>]    设置该工程默认启用 `--isolated` 隔离测试模式

核心机制：
  1. 优先从玩家本地 `<GamePath>\Mods` 递归扫描 `manifest.json`（兼容带版本号嵌套目录），零网络耗时；
  2. 若本地缺失某个开源前置（如 `cp`、`ftm`、`automate`、`uiinfosuite2` 等），自动从 GitHub Releases 拉取 `.zip` 到缓存（绝不访问 N 网）；
  3. `sdv-dev build` 自动将前置模组 DLL 加入编译引用；`start/restart --isolated` 自动把工程及全部传递前置复制到独立 `--mods-path` 秒开测试！

常用别名：
  `cp` (Pathoschild.ContentPatcher), `gmcm` (spacechase0.GenericModConfigMenu), `spacecore` (spacechase0.SpaceCore),
  `ftm` (Esca.FarmTypeManager), `sve` (FlashShifter.StardewValleyExpandedCP), `automate`, `stardewhack`, `kokoro`

示例：
  sdv-dev deps list gmcm
  sdv-dev deps add gmcm --optional --no-ref --project d:\myProject\MyMod
  sdv-dev deps api gmcm
  sdv-dev deps add spacecore --project d:\myProject\MyMod");
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

1. 贴图编号网格导出与原版地图检视：
   sdv-dev tilesheet Maps/island_tilesheet_1 --grid --out sheet_grid.png
   sdv-dev map-inspect IslandSouth --rect 14,32,8,6

2. 免进游戏 1 秒离线渲染 `.tmx`（支持四季切换与碰撞/水面/传送调试叠加层）：
   sdv-dev map-render assets/CoralBay.tmx --out preview.png --season summer --debug-overlay
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

                case "screenshot":
                case "warp":
                case "reload-assets":
                    Console.WriteLine(@"# sdv-dev screenshot / warp / reload-assets — 实机视觉验收与秒级热刷

用法：
  sdv-dev warp <LocationName> [x] [y] [--facing 0..3]
  sdv-dev screenshot --mode screen --out <screen.png>
  sdv-dev screenshot --mode map [--scale 1.0] --out <fullmap.png>
  sdv-dev reload-assets [--project <ModDir>] --assets <Asset1,Asset2> [--no-reload-map]

说明：
  - `screenshot --mode screen` 在渲染帧末尾直接保存完整画面（含光影、水面波纹、粒子与当前 UI）；
  - `screenshot --mode map` 调用原版 `takeMapScreenshot` 导出整张当前地图并自动清理默认截图目录临时文件；
  - `reload-assets` 仅拷贝非 DLL 资产（`.tmx`/`.png`/`.json`），即使 C# 模组 DLL 正被游戏锁定也能 1 秒热刷新当前地图！

示例：
  sdv-dev warp IslandSouth 20 35
  sdv-dev screenshot --mode map --out C:\temp\island_map.png
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

特性：
  - 推荐在 PowerShell 中用 `@' ... '@ | sdv-dev eval --stdin` 管道传入代码，彻底避免命令行双引号被剥离！
  - 脚本顶部的 `using ...;` 会自动提升到编译单元顶层；
  - 已默认预置 `StardewValley.*`、`StardewModdingAPI.*`、`Microsoft.Xna.Framework(.Graphics)`、`xTile.*`、`System.Text` 与当前活跃 Mods 目录中所有模组 DLL；
  - 内置变量：`Helper` (`IModHelper`), `Monitor` (`IMonitor`), `Player` (`Farmer`), `Location` (`GameLocation`)。");
                    break;

                case "start":
                case "restart":
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
                    Console.WriteLine(@"# sdv-dev 实机生命周期、模组隔离 (--isolated) 与无焦点交互详情

启动、模组隔离与热迭代：
  sdv-dev live                                                         查看进程、桥接状态、活跃 modsPath、是否隔离模式与当前坐标
  sdv-dev start --isolated [--project <dir>] [--with-mods A,B] --test-world
      使用 SMAPI `--mods-path` 隔离启动：仅加载 `_SdvDevBridge` + 目标模组 + 自动解析的全部传递前置模组（3~4 秒极速进档！）
  sdv-dev start --test-world [--reset-test-world]
      使用全量 `Mods` 启动并进入专属测试槽位 `SdvDevTest_999999999`（具备真实 CurrentSavePath，完美兼容 SVE / SpaceCore，绝不触碰个人存档）
  sdv-dev restart --project <dir> [--isolated | --full-mods] --test-world
      先执行 check+build（失败不关原游戏），通过后自动部署并重进世界
  sdv-dev reload --project <cpDir> [--assets A,B]                      免重启热重载 Content Patcher 包
  sdv-dev reload-assets --project <dir> --assets Maps/MyMap            免重启热刷 C#/CP 模组的地图与贴图资产

游戏内检视、传送、截图与日志：
  sdv-dev warp <Location> [x] [y]                                      瞬移至目标地图坐标
  sdv-dev screenshot --mode screen|map --out <file.png>                实机画面或全图截图
  sdv-dev player / sdv-dev ui / sdv-dev ui-click (--id|--match|--response|--close)
  sdv-dev cmd ""patch summary""                                          执行任意 SMAPI / CP / debug 控制台命令
  sdv-dev logs [--level Error|Warn] [--mod <Id>] [-n 40]               读取 SMAPI-latest.txt 日志
  sdv-dev menu / sdv-dev stop [--force]                                不保存退回标题或安全关闭游戏");
                    break;

                case "api":
                    Console.WriteLine(@"# sdv-dev api — 模糊搜索 C# 类、方法（含私有方法）、属性、字段与 SMAPI 事件

用法：
  sdv-dev api <keywords> [--kind class|method|property|event|field|constructor] [--asm sdv|smapi|gamedata] [-n 15] [--json]

示例：
  sdv-dev api ""shop purchase""
  sdv-dev api ""takeMapScreenshot""
  sdv-dev api ""asset requested"" --asm smapi --kind event");
                    break;

                case "decompile":
                case "dec":
                    Console.WriteLine(@"# sdv-dev decompile — 基于 MetadataToken 直跳的实时 C# 源码反编译

用法：
  sdv-dev decompile <Type | Type.Member> [--lines 200] [--full] [-g <gamePath>]

示例：
  sdv-dev decompile StardewValley.Locations.Beach.resetLocalState
  sdv-dev decompile ShopMenu.tryToPurchaseItem
  sdv-dev decompile GameLocation.isTilePassable");
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
                    Console.WriteLine(@"# sdv-dev new / gen-gmcm / check / build / pack — 工程构建、1.6 语义体检、Publicizer 与发布打包

用法：
  sdv-dev new <cp|csharp> --name <ModName> [--author <Author>] [--out <ParentDir>]
  sdv-dev gen-gmcm [--project <ModDir>] [--config <ModConfig.cs>]
      基于 NRefactory/ILSpy AST 解析 `ModConfig.cs` 属性、注释与 `[Range]`，自动生成 `IGenericModConfigMenuApi.cs`、`GmcmIntegration.g.cs` 并增量补齐 `i18n/default.json` 与 `zh.json`！
  sdv-dev check [--project <ModDir>] [--json]
      执行 1.6 深度静态校验：
      - Content Patcher: `GameData` 强类型字段与 JSON 类型对账、`GameStateQuery`/`TriggerAction`/`Event` 脚本语法、`EditImage`/`SpriteIndex` 像素越界、`i18n` 双向漏译对账；
      - C# SMAPI: `[HarmonyPatch]` 目标方法存在性/重载歧义(`AmbiguousMatchException`)/注入参数(`__instance`/`__result`/`___field`/同名参数)静态核验 + 分屏/联机可变 `static` 隐患扫描；
      - 地图: 全部 `.tmx` 尺寸、GID、Warp 与 BFS 孤岛检测。
  sdv-dev build [--project <ModDir>] [--deploy] [--mods-path <Dir>] [--no-publicize]
      免装 .NET SDK 秒编 .NET 6.0 C# 模组；默认启用 `BepInEx.AssemblyPublicizer` + `IgnoresAccessChecksTo`（无需反射直接访问原版私有字段与方法，且完美兼容 `protected override`）并在编译后自动校验 Harmony 补丁签名。
  sdv-dev pack [--project <ModDir>] [--out <Release.zip>] [--skip-check]
      编译 + 全量静态校验通过后，生成顶层带 `<ModName>/manifest.json`、剔除全部源码/PDB/临时配置的标准发布压缩包并输出 SHA256。");
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

2. 确定性世界控制与真实跨日管线 (`world`)：
   sdv-dev world --time 1800 --weather rain                     切换时间与 1.6 LocationWeather 天气（Sun/Rain/Storm/Wind/Snow/GreenRain）
   sdv-dev world --season fall --day 15 --money 50000 --refill
   sdv-dev world --friendship Abigail:1000 --add-item (O)74:5:4 --mail MyMod_Letter
   sdv-dev world --advance-days 3 [--save]                      驱动真实的 `Game1.NewDay` 跨夜结算管线（触发作物生长/机器产出/信件投递/SMAPI DayStarted）

3. 本地分屏联机双端测试 (`mp` + `--screen`)：
   sdv-dev mp status                                            查看所有分屏实例 (screen 0=host, 1..3=farmhand) 状态
   sdv-dev mp add-farmhand                                      自动确保农场有联机小屋并在同进程内拉起本地分屏客机实例
   sdv-dev mp remove-farmhand [--screen 1]
   - `player`、`ui`、`ui-click`、`warp`、`eval`、`event-play`、`world` 均支持加 `--screen host|farmhand|0..3` 指定在主机或客机上下文执行！");
                    break;

                default:
                    PrintHelp(null);
                    break;
            }
        }

        #endregion

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
