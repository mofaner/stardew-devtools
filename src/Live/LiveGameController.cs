using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using SdvKb.Build;
using SdvKb.Project;
using SdvKb.Validation;

namespace SdvKb.Live
{
    public sealed class SaveSummary
    {
        public string FolderName { get; set; } = string.Empty;
        public string FarmerName { get; set; } = string.Empty;
        public string FarmName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public int Money { get; set; }
    }

    public static class LiveGameController
    {
        public static string SavesRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");

        public static string SmapiLatestLogPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "ErrorLogs", "SMAPI-latest.txt");

        public static Process? FindRunningSmapiProcess()
        {
            foreach (var name in new[] { "StardewModdingAPI", "Stardew Valley" })
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                    return procs[0];
            }
            return null;
        }

        public static void EnsureBridgeModInstalled(string gamePath, string? targetModsRootOverride = null)
        {
            string modsRoot = string.IsNullOrWhiteSpace(targetModsRootOverride)
                ? Path.Combine(gamePath, "Mods")
                : Path.GetFullPath(targetModsRootOverride!);

            string targetDir = Path.Combine(modsRoot, "_SdvDevBridge");
            Directory.CreateDirectory(targetDir);

            // 1. Primary path: Extract directly from embedded resources inside single-file `sdv-dev-worker.exe`!
            var asm = typeof(LiveGameController).Assembly;
            using Stream? dllStream = asm.GetManifestResourceStream("SdvDevBridge.dll");
            using Stream? manifestStream = asm.GetManifestResourceStream("SdvDevBridge.manifest.json");
            if (dllStream != null && manifestStream != null)
            {
                WriteStreamToFile(dllStream, Path.Combine(targetDir, "SdvDevBridge.dll"));
                WriteStreamToFile(manifestStream, Path.Combine(targetDir, "manifest.json"));
                return;
            }

            // 2. Fallback during local unbundled development
            string baseDir = AppContext.BaseDirectory;
            string bridgeSrc = Path.Combine(baseDir, "bridge");
            if (!File.Exists(Path.Combine(bridgeSrc, "SdvDevBridge.dll")))
                bridgeSrc = Path.Combine(KbConfig.InstalledLibDir, "bridge");

            if (!File.Exists(Path.Combine(bridgeSrc, "SdvDevBridge.dll")))
                throw new FileNotFoundException($"Embedded SdvDevBridge.dll resource not found and fallback `{bridgeSrc}` missing. Run `build.ps1`.");

            foreach (var f in Directory.GetFiles(bridgeSrc))
            {
                File.Copy(f, Path.Combine(targetDir, Path.GetFileName(f)), overwrite: true);
            }
        }

        private static void WriteStreamToFile(Stream src, string dstPath)
        {
            using var fs = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            src.CopyTo(fs);
        }

        /// <summary>
        /// Queries the running bridge for its active `modsPath` (returning isolated `--mods-path` if active, or `&lt;GamePath&gt;\Mods`).
        /// </summary>
        public static string GetActiveModsRoot(string gamePath)
        {
            if (FindRunningSmapiProcess() != null)
            {
                var (conn, respJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", connectTimeoutMs: 700);
                if (conn)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(respJson);
                        if (doc.RootElement.TryGetProperty("modsPath", out var mp) && mp.ValueKind == JsonValueKind.String)
                        {
                            string? p = mp.GetString();
                            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                                return p!;
                        }
                    }
                    catch { }
                }
            }
            return Path.Combine(gamePath, "Mods");
        }

        public static string GetLiveStatusJson()
        {
            var proc = FindRunningSmapiProcess();
            if (proc == null)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    running = false,
                    bridgeConnected = false,
                    inWorld = false
                }, IndentedJson());
            }

            var (connected, respJson, err) = BridgeClient.TrySendJson("{\"action\":\"status\"}", connectTimeoutMs: 1200);
            if (!connected)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    running = true,
                    pid = proc.Id,
                    bridgeConnected = false,
                    inWorld = false,
                    note = $"Process PID={proc.Id} is running without SdvDevBridge connected ({err})."
                }, IndentedJson());
            }

            using var doc = JsonDocument.Parse(respJson);
            var dict = new Dictionary<string, object?>
            {
                ["running"] = true,
                ["pid"] = proc.Id
            };
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                dict[p.Name] = JsonSerializer.Deserialize<object>(p.Value.GetRawText());
            }
            return JsonSerializer.Serialize(dict, IndentedJson());
        }

        public static List<SaveSummary> ListLocalSaves()
        {
            var list = new List<SaveSummary>();
            if (!Directory.Exists(SavesRoot))
                return list;

            foreach (var dir in Directory.GetDirectories(SavesRoot).OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                string folder = Path.GetFileName(dir);
                string infoFile = Path.Combine(dir, "SaveGameInfo");
                if (!File.Exists(infoFile))
                    continue;

                try
                {
                    var xdoc = XDocument.Load(infoFile);
                    var root = xdoc.Root;
                    if (root == null)
                        continue;

                    string name = root.Element("name")?.Value ?? "";
                    string farm = root.Element("farmName")?.Value ?? "";
                    string season = root.Element("seasonForSaveGame")?.Value ?? root.Element("currentSeason")?.Value ?? "";
                    string day = root.Element("dayOfMonthForSaveGame")?.Value ?? "";
                    string year = root.Element("yearForSaveGame")?.Value ?? "";
                    int.TryParse(root.Element("money")?.Value ?? "0", out int money);

                    list.Add(new SaveSummary
                    {
                        FolderName = folder,
                        FarmerName = name,
                        FarmName = farm,
                        Date = $"Year {year} Season {season} Day {day}".Trim(),
                        Money = money
                    });
                }
                catch
                {
                    list.Add(new SaveSummary { FolderName = folder });
                }
            }

            return list;
        }

        public static string StartGameAndWait(
            string gamePath,
            string? saveFolder,
            bool useDisposableTestWorld,
            int timeoutSeconds = 60,
            bool isolated = false,
            string? customModsPath = null,
            string? projectDir = null,
            IEnumerable<string>? extraWithMods = null,
            bool resetTestWorld = false,
            bool background = false,
            bool noFocus = false,
            bool hideConsole = false)
        {
            // Check if project has sdv-project.json requesting isolated mode by default
            if (!isolated && string.IsNullOrWhiteSpace(customModsPath) && !string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir))
            {
                if (ModDependencyResolver.LoadProjectConfig(projectDir!).Isolated)
                    isolated = true;
            }

            bool wantsCustomModsRoot = isolated || !string.IsNullOrWhiteSpace(customModsPath);

            var existing = FindRunningSmapiProcess();
            if (existing != null)
            {
                var (conn, respJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", 1200);
                if (!conn)
                {
                    throw new InvalidOperationException(
                        $"Stardew Valley is already running (PID={existing.Id}) without SdvDevBridge connected. " +
                        "Please ask the user before closing a manually started game, or pass `--force` to `restart` if authorized.");
                }

                // Check if already running in a different isolation mode than requested
                if (wantsCustomModsRoot)
                {
                    using var sDoc = JsonDocument.Parse(respJson);
                    bool curIso = sDoc.RootElement.TryGetProperty("isolatedMods", out var isoEl) && isoEl.ValueKind == JsonValueKind.True;
                    if (!curIso)
                    {
                        throw new InvalidOperationException(
                            "Game is already running with full `Mods/`, while `--isolated` / `--mods-path` was requested. " +
                            "Use `sdv-dev restart --isolated ...` to relaunch in isolated mode.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(saveFolder))
                {
                    BridgeClient.SendRequired(new { action = "load_save", save = saveFolder });
                    return WaitUntilWorldReady(timeoutSeconds);
                }
                if (useDisposableTestWorld || resetTestWorld)
                {
                    BridgeClient.SendRequired(new { action = "new_test_save", farmer = "SdvTester", farm = "SdvDevTest", reset = resetTestWorld });
                    return WaitUntilWorldReady(timeoutSeconds);
                }
                return GetLiveStatusJson();
            }

            string? targetModsRoot = null;
            if (wantsCustomModsRoot)
            {
                if (!isolated && !string.IsNullOrWhiteSpace(customModsPath) && string.IsNullOrWhiteSpace(projectDir) && (extraWithMods == null || !extraWithMods.Any()))
                {
                    // Explicit pre-existing custom mods directory passed by user
                    targetModsRoot = Path.GetFullPath(customModsPath!);
                    Directory.CreateDirectory(targetModsRoot);
                }
                else
                {
                    var (isoDir, _, missingReq) = ModDependencyResolver.StageIsolatedModsDirectory(
                        gamePath,
                        customModsPath,
                        projectDir,
                        extraWithMods);

                    if (missingReq.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"Cannot start isolated SMAPI session: missing required prerequisite mod(s): {string.Join(", ", missingReq)}. " +
                            "Install them into local `Mods` or run `sdv-dev deps add <UniqueID>` first.");
                    }
                    targetModsRoot = isoDir;
                }
            }

            else if (!string.IsNullOrWhiteSpace(projectDir))
            {
                ModDependencyResolver.EnsureRequiredPrerequisitesInModsRoot(projectDir, gamePath, Path.Combine(gamePath, "Mods"));
            }

            EnsureBridgeModInstalled(gamePath, targetModsRoot);

            string smapiExe = Path.Combine(gamePath, "StardewModdingAPI.exe");
            if (!File.Exists(smapiExe))
                throw new FileNotFoundException($"StardewModdingAPI.exe not found at `{smapiExe}`.");

            string? autoLoadEnv = null;
            if (!string.IsNullOrWhiteSpace(saveFolder))
                autoLoadEnv = saveFolder!;
            else if (resetTestWorld)
                autoLoadEnv = "__RESET_TEST_SAVE__";
            else if (useDisposableTestWorld)
                autoLoadEnv = "__NEW_TEST_SAVE__";

            bool brokeAway = SpawnDetachedSmapiProcess(smapiExe, gamePath, targetModsRoot, autoLoadEnv, background, noFocus, hideConsole);

            string statusJson = WaitUntilNewBridgeSessionReady(
                saveFolder,
                useDisposableTestWorld,
                resetTestWorld,
                timeoutSeconds,
                previousPidToIgnore: null);

            return AttachEphemeralJobWarningIfNeeded(statusJson, brokeAway);
        }

        private static string AttachEphemeralJobWarningIfNeeded(string statusJson, bool brokeAway)
        {
            if (brokeAway || Daemon.WorkerDaemonServer.IsRunningInDaemon || !IsCurrentProcessInsideJobObject())
                return statusJson;

            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(statusJson) as System.Text.Json.Nodes.JsonObject;
                if (node != null)
                {
                    node["jobWarning"] =
                        "Launched inside a non-breakaway host Job Object without `--daemon`. " +
                        "If the host terminates child processes when this synchronous command exits, start `sdv-dev --daemon` with `IsDaemon: true` first!";
                    return node.ToJsonString(IndentedJson());
                }
            }
            catch { }
            return statusJson;
        }

        private static string WaitUntilNewBridgeSessionReady(
            string? saveFolder,
            bool useDisposableTestWorld,
            bool resetTestWorld,
            int timeoutSeconds,
            int? previousPidToIgnore)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                Thread.Sleep(650);
                var running = FindRunningSmapiProcess();
                if (running != null && previousPidToIgnore.HasValue && running.Id == previousPidToIgnore.Value)
                    continue;

                var (conn, respJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", 1000);
                if (!conn)
                    continue;

                if (string.IsNullOrWhiteSpace(saveFolder) && !useDisposableTestWorld && !resetTestWorld)
                    return GetLiveStatusJson();

                using var doc = JsonDocument.Parse(respJson);
                if (doc.RootElement.TryGetProperty("inWorld", out var iw) && iw.ValueKind == JsonValueKind.True)
                    return GetLiveStatusJson();
            }

            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for SMAPI / world load. Check `sdv-dev logs --level Error`.");
        }

        /// <summary>
        /// 编译、静态校验并热重载/重开目标模组工程。
        ///
        /// 【当初为什么要加同桌面 `self_restart` 与 `--background` 参数】：
        /// 1. 当玩家在可见屏幕打开了游戏，而 AI 在沙盒终端里调 `restart` 时，由旧游戏进程在退出前自举重开 (`self_restart`)
        ///    能让新游戏继承玩家的可见桌面原位刷新，而不是消失进 AI 不可见的后台沙盒桌面；
        /// 2. 反过来，万一玩家正在专心写代码、明确不想在桌面上看到弹窗，传入 `--background` (`background: true`) 就会关闭该可见会话
        ///    并切换为无打扰离屏隐藏模式（传 `--visible` 则可强制切回可见模式）。
        /// </summary>
        public static string RestartOrReloadProject(
            string? modDir,
            string gamePath,
            string? saveFolder,
            bool useDisposableTestWorld,
            bool forceColdRestart,
            bool forceKillUnmanaged,
            int timeoutSeconds = 65,
            bool isolated = false,
            bool forceFullMods = false,
            string? customModsPath = null,
            IEnumerable<string>? extraWithMods = null,
            bool resetTestWorld = false,
            bool? backgroundOverride = null,
            bool sameDesktopExplicit = false,
            bool noFocus = true,
            bool? hideConsoleOverride = null)
        {
            ModBuildResult? buildRes = null;

            if (!isolated && !forceFullMods && string.IsNullOrWhiteSpace(customModsPath) &&
                !string.IsNullOrWhiteSpace(modDir) && Directory.Exists(modDir))
            {
                if (ModDependencyResolver.LoadProjectConfig(modDir!).Isolated)
                    isolated = true;
            }

            bool wantsIsolated = !forceFullMods && (isolated || !string.IsNullOrWhiteSpace(customModsPath));

            // Step 1: Compile fresh DLL FIRST (including post-compile Harmony patch verification), THEN run full directory validation
            // on the newly built artifacts — BEFORE touching the running game!
            if (!string.IsNullOrWhiteSpace(modDir))
            {
                buildRes = DotNet6ModCompiler.BuildAndOptionallyDeploy(modDir!, gamePath, deploy: false);
                if (!buildRes.Success)
                {
                    throw new InvalidOperationException($"C# Mod compilation / Harmony verification failed (running game preserved):\n{buildRes.CompilerOutput}");
                }

                var issues = ModValidator.ValidateModDirectory(modDir!);
                var errors = issues.Where(i => i.Severity == "ERROR").ToList();
                if (errors.Count > 0)
                {
                    string errSummary = string.Join("\n", errors.Select(e => $"[{e.Severity}] {e.File}: {e.Message}"));
                    throw new InvalidOperationException($"Mod validation failed (running game preserved):\n{errSummary}");
                }
            }

            // Step 2: Fast hot-reload path if it's a Content Patcher pack and game is already running in compatible mode!
            var running = FindRunningSmapiProcess();
            if (running != null && buildRes != null && buildRes.IsContentPack && !forceColdRestart)
            {
                var (conn, statusJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", 1000);
                if (conn)
                {
                    using var sDoc = JsonDocument.Parse(statusJson);
                    bool curIso = sDoc.RootElement.TryGetProperty("isolatedMods", out var isoEl) && isoEl.ValueKind == JsonValueKind.True;
                    string activeModsRoot = sDoc.RootElement.TryGetProperty("modsPath", out var mpEl) && mpEl.ValueKind == JsonValueKind.String
                        ? mpEl.GetString()!
                        : Path.Combine(gamePath, "Mods");

                    if (curIso == wantsIsolated)
                    {
                        string deployed = DotNet6ModCompiler.DeployModFolderToModsRoot(Path.GetFullPath(modDir!), activeModsRoot, buildRes.UniqueId);
                        string reloadResp = BridgeClient.SendRequired(new
                        {
                            action = "reload_assets",
                            uniqueId = buildRes.UniqueId,
                            reloadMap = true
                        });
                        return JsonSerializer.Serialize(new
                        {
                            ok = true,
                            mode = "hot_reload_content_pack",
                            isolatedMods = curIso,
                            uniqueId = buildRes.UniqueId,
                            deployedPath = deployed,
                            bridge = JsonSerializer.Deserialize<object>(reloadResp)
                        }, IndentedJson());
                    }
                }
            }

            // Step 3: Stage updated mod files into a lock-free `.staging` directory & decide whether to self-restart!
            string? previousSave = saveFolder;
            string? liveCurrentSave = null;
            bool effectiveBackground = backgroundOverride ?? false;

            if (running != null)
            {
                int oldPid = running.Id;
                var (conn, statusJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", 1200);
                if (!conn && !forceKillUnmanaged)
                {
                    throw new InvalidOperationException(
                        $"Game process PID={oldPid} is running without SdvDevBridge. Refusing to kill potential manual player session without `--force`.");
                }

                bool prevWasBackground = false;
                bool prevHideConsole = false;
                if (conn)
                {
                    try
                    {
                        using var sDoc = JsonDocument.Parse(statusJson);
                        if (sDoc.RootElement.TryGetProperty("backgroundMode", out var bgEl) && bgEl.ValueKind == JsonValueKind.True)
                            prevWasBackground = true;
                        if (sDoc.RootElement.TryGetProperty("hideConsole", out var hcEl) && hcEl.ValueKind == JsonValueKind.True)
                            prevHideConsole = true;

                        if (sDoc.RootElement.TryGetProperty("saveFolderName", out var sf) && sf.ValueKind == JsonValueKind.String)
                        {
                            liveCurrentSave = sf.GetString();
                            if (string.IsNullOrWhiteSpace(previousSave) && !useDisposableTestWorld && !resetTestWorld &&
                                !string.IsNullOrWhiteSpace(liveCurrentSave) && Directory.Exists(Path.Combine(SavesRoot, liveCurrentSave!)))
                            {
                                previousSave = liveCurrentSave;
                            }
                        }
                    }
                    catch { }

                    // 个人存档保护边界：若当前正在运行非 SdvDevTest_ 的玩家个人存档，未加 `--force` 严禁自动接管重启！
                    if (!forceKillUnmanaged &&
                        !string.IsNullOrWhiteSpace(liveCurrentSave) &&
                        !liveCurrentSave!.StartsWith("SdvDevTest_", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Game is currently loaded into personal save `{liveCurrentSave}`. Refusing to auto-restart without `--force` (or exit to title via `sdv-dev menu` first).");
                    }
                }

                // 若调用方未显式传 --background / --visible / --no-console，则默认继承上一轮会话的可见/后台/控制台属性
                effectiveBackground = backgroundOverride ?? prevWasBackground;
                bool effectiveHideConsoleForRestart = hideConsoleOverride ?? prevHideConsole;

                // 允许走同桌面无锁 self_restart 的条件：
                // 1) 未指定 `--background`；
                // 2) 用户显式传了 `--same-desktop`/`--visible`，或上一轮本身就是可见的标题界面 / `SdvDevTest_` 专属测试档会话！
                bool allowSameDesktopSelfRestart = conn && !effectiveBackground &&
                    (sameDesktopExplicit || (!prevWasBackground &&
                        (string.IsNullOrWhiteSpace(liveCurrentSave) || liveCurrentSave!.StartsWith("SdvDevTest_", StringComparison.OrdinalIgnoreCase))));

                if (allowSameDesktopSelfRestart)
                {
                    // 注意：此时旧进程 `oldPid` 仍活着并锁着运行目录中的 `.dll`！
                    // 严禁直接修改/删除运行目录，必须先写入旁路无锁 `.staging` 目录，交由 `self_restart` 跳板脚本等 `oldPid` 死透后再覆盖！
                    var (finalModsRoot, stagingDir, swapTargetDir, swapMode) = PrepareLockFreeStagingForSelfRestart(
                        gamePath,
                        wantsIsolated,
                        customModsPath,
                        modDir,
                        extraWithMods,
                        buildRes);

                    string smapiExe = Path.Combine(gamePath, "StardewModdingAPI.exe");
                    string? autoLoadEnv = ResolveAutoLoadEnvValue(previousSave, useDisposableTestWorld, resetTestWorld);

                    var (srConn, srJson, _) = BridgeClient.TrySendJson(JsonSerializer.Serialize(new
                    {
                        action = "self_restart",
                        smapiExe,
                        gamePath,
                        modsPath = finalModsRoot,
                        stagingDir,
                        swapTargetDir,
                        swapMode,
                        autoLoadSave = autoLoadEnv,
                        clientExe = ResolveCompanionClientExe(),
                        pwshExe = FindExecutableInPath("pwsh.exe"),
                        background = false,
                        hideConsole = effectiveHideConsoleForRestart,
                        noFocus
                    }), 2500);

                    if (srConn && srJson.Contains("\"ok\":true", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!running.WaitForExit(10000))
                        {
                            try { running.Kill(); running.WaitForExit(4000); } catch { }
                        }
                        return WaitUntilNewBridgeSessionReady(previousSave, useDisposableTestWorld, resetTestWorld, timeoutSeconds, previousPidToIgnore: oldPid);
                    }
                }

                if (conn)
                    BridgeClient.TrySendJson("{\"action\":\"quit\"}", 1500);
                else
                    running.CloseMainWindow();

                if (!running.WaitForExit(10000))
                {
                    try { running.Kill(); running.WaitForExit(5000); } catch { }
                }
                Thread.Sleep(400);
            }

            // Step 4: Deploy to normal Mods/ when not isolated (and ensure required prerequisites like CP exist); when isolated, StartGameAndWait stages isolated-mods + prerequisites!
            if (!wantsIsolated && !string.IsNullOrWhiteSpace(modDir) && buildRes != null)
            {
                ModDependencyResolver.EnsureRequiredPrerequisitesInModsRoot(modDir, gamePath, Path.Combine(gamePath, "Mods"));
                DotNet6ModCompiler.DeployModFolder(Path.GetFullPath(modDir!), gamePath, buildRes.UniqueId);
            }

            return StartGameAndWait(
                gamePath,
                previousSave,
                useDisposableTestWorld,
                timeoutSeconds,
                isolated: wantsIsolated,
                customModsPath: customModsPath,
                projectDir: modDir,
                extraWithMods: extraWithMods,
                resetTestWorld: resetTestWorld,
                background: effectiveBackground,
                noFocus: noFocus,
                hideConsole: hideConsoleOverride ?? false);
        }

        /// <summary>
        /// 在旧游戏进程仍存活锁住运行目录 DLL 时，将下一轮所需的全部模组与桥接层写入独立的无锁旁路暂存目录（`.staging`），
        /// 供 `self_restart` 派生脚本在 `Wait-Process -Id <oldPid>` 结束后安全替换。
        /// </summary>
        private static (string? FinalModsRoot, string? StagingDir, string? SwapTargetDir, string? SwapMode) PrepareLockFreeStagingForSelfRestart(
            string gamePath,
            bool wantsIsolated,
            string? customModsPath,
            string? modDir,
            IEnumerable<string>? extraWithMods,
            ModBuildResult? buildRes)
        {
            if (wantsIsolated)
            {
                string finalIso = string.IsNullOrWhiteSpace(customModsPath)
                    ? ModDependencyResolver.DefaultIsolatedModsRoot
                    : Path.GetFullPath(customModsPath!);
                finalIso = Path.TrimEndingDirectorySeparator(finalIso);
                string stagingIso = finalIso + ".staging";

                if (string.IsNullOrWhiteSpace(modDir) && !string.IsNullOrWhiteSpace(customModsPath) && (extraWithMods == null || !extraWithMods.Any()))
                {
                    EnsureBridgeModInstalled(gamePath, finalIso);
                    return (finalIso, null, null, null);
                }

                var (_, _, missingReq) = ModDependencyResolver.StageIsolatedModsDirectory(
                    gamePath,
                    stagingIso,
                    modDir,
                    extraWithMods,
                    preserveConfigsFromExistingDir: finalIso);

                if (missingReq.Count > 0)
                    throw new InvalidOperationException($"Missing required prerequisite mod(s): {string.Join(", ", missingReq)}.");

                EnsureBridgeModInstalled(gamePath, stagingIso);
                return (finalIso, stagingIso, finalIso, "replace_dir");
            }
            else
            {
                string normalModsRoot = Path.Combine(gamePath, "Mods");
                string stagingModsRoot = Path.Combine(KbConfig.DataDir, "fullmods-restart-staging");
                if (Directory.Exists(stagingModsRoot))
                    Directory.Delete(stagingModsRoot, recursive: true);
                Directory.CreateDirectory(stagingModsRoot);

                if (!string.IsNullOrWhiteSpace(modDir) && buildRes != null)
                {
                    DotNet6ModCompiler.DeployModFolderToModsRoot(Path.GetFullPath(modDir!), stagingModsRoot, buildRes.UniqueId);
                }
                EnsureBridgeModInstalled(gamePath, stagingModsRoot);
                return (null, stagingModsRoot, normalModsRoot, "merge_subdirs");
            }
        }

        private static string? ResolveAutoLoadEnvValue(string? saveFolder, bool useDisposableTestWorld, bool resetTestWorld)
        {
            if (!string.IsNullOrWhiteSpace(saveFolder))
                return saveFolder!;
            if (resetTestWorld)
                return "__RESET_TEST_SAVE__";
            if (useDisposableTestWorld)
                return "__NEW_TEST_SAVE__";
            return null;
        }

        public static string StopGame(bool force)
        {
            var running = FindRunningSmapiProcess();
            if (running == null)
                return JsonSerializer.Serialize(new { ok = true, message = "Game is not running." }, IndentedJson());

            var (conn, _, _) = BridgeClient.TrySendJson("{\"action\":\"quit\"}", 1500);
            if (!conn)
            {
                if (!force)
                    throw new InvalidOperationException($"Game PID={running.Id} has no bridge connected. Pass `--force` if authorized to stop it.");
                running.CloseMainWindow();
            }

            if (!running.WaitForExit(8000) && force)
            {
                try { running.Kill(); } catch { }
            }

            return JsonSerializer.Serialize(new { ok = true, stoppedPid = running.Id }, IndentedJson());
        }

        public static string WaitUntilWorldReady(int timeoutSeconds)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                Thread.Sleep(600);
                var (conn, respJson, _) = BridgeClient.TrySendJson("{\"action\":\"status\"}", 1000);
                if (!conn)
                    continue;
                using var doc = JsonDocument.Parse(respJson);
                if (doc.RootElement.TryGetProperty("inWorld", out var iw) && iw.ValueKind == JsonValueKind.True)
                    return GetLiveStatusJson();
            }
            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for `inWorld=true`.");
        }

        public static List<string> ReadSmapiLogs(string? levelFilter, string? modFilter, int tailCount)
        {
            var result = new List<string>();
            if (!File.Exists(SmapiLatestLogPath))
                return result;

            using var fs = new FileStream(SmapiLatestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string all = sr.ReadToEnd();

            var entries = new List<(string Level, string Source, string FullText)>();
            var curSb = new StringBuilder();
            string curLevel = "INFO";
            string curSource = "";
            var headerRegex = new Regex(@"^\[(\d{2}:\d{2}:\d{2})\s+(TRACE|DEBUG|INFO|WARN|ERROR|ALERT)\s+([^\]]+)\]", RegexOptions.IgnoreCase);

            foreach (var line in all.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var m = headerRegex.Match(line);
                if (m.Success)
                {
                    if (curSb.Length > 0)
                        entries.Add((curLevel, curSource, curSb.ToString().TrimEnd()));
                    curSb.Clear();
                    curLevel = m.Groups[2].Value.ToUpperInvariant();
                    curSource = m.Groups[3].Value.Trim();
                    curSb.AppendLine(line);
                }
                else if (curSb.Length > 0)
                {
                    curSb.AppendLine(line);
                }
            }
            if (curSb.Length > 0)
                entries.Add((curLevel, curSource, curSb.ToString().TrimEnd()));

            IEnumerable<(string Level, string Source, string FullText)> filtered = entries;
            if (!string.IsNullOrWhiteSpace(levelFilter))
            {
                string lf = levelFilter!.Trim().ToUpperInvariant();
                if (lf == "ERROR")
                    filtered = filtered.Where(e => e.Level == "ERROR" || e.Level == "ALERT");
                else if (lf == "WARN")
                    filtered = filtered.Where(e => e.Level == "WARN" || e.Level == "ERROR" || e.Level == "ALERT");
                else
                    filtered = filtered.Where(e => e.Level.Equals(lf, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(modFilter))
            {
                filtered = filtered.Where(e => e.Source.IndexOf(modFilter!, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               e.FullText.IndexOf(modFilter!, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = filtered.ToList();
            return list.Skip(Math.Max(0, list.Count - tailCount)).Select(e => e.FullText).ToList();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessW(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsProcessInJob(IntPtr processHandle, IntPtr jobHandle, out bool result);

        private static bool IsCurrentProcessInsideJobObject()
        {
            try
            {
                if (IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out bool inJob))
                    return inJob;
            }
            catch { }
            return false;
        }

        private static bool SpawnDetachedSmapiProcess(
            string smapiExe,
            string gamePath,
            string? customModsRoot,
            string? autoLoadSaveValue,
            bool background = false,
            bool noFocus = false,
            bool hideConsole = false)
        {
            const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
            const uint CREATE_NEW_CONSOLE = 0x00000010;
            const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
            const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
            const int ERROR_ACCESS_DENIED = 5;
            const int STARTF_USESHOWWINDOW = 0x00000001;
            const short SW_HIDE = 0;
            const short SW_SHOWNORMAL = 1;
            const short SW_SHOWNOACTIVATE = 4;

            // Prefer hosting SMAPI inside modern `pwsh.exe` (or direct UTF-8 console fallback) WITHOUT `SW_SHOWMINNOACTIVE`,
            // so: (1) SDL2/MonoGame initializes the game window at full resolution instead of a clipped half-black rect,
            // and (2) console runs in UTF-8 + modern PowerShell styling instead of legacy CP936 SimSun conhost!
            string? pwshExe = FindExecutableInPath("pwsh.exe");
            string appExe;
            StringBuilder cmdLine;

            if (!string.IsNullOrWhiteSpace(pwshExe) && File.Exists(pwshExe))
            {
                appExe = pwshExe!;
                string psSmapi = EscapeSingleQuotedPowerShell(smapiExe);
                var psScript = new StringBuilder();
                string? clientExe = ResolveCompanionClientExe();
                if (!string.IsNullOrWhiteSpace(clientExe) && File.Exists(clientExe))
                {
                    psScript.Append($"& '{EscapeSingleQuotedPowerShell(clientExe!)}' --init-console; ");
                }
                psScript.Append($"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{psSmapi}'");
                if (!string.IsNullOrWhiteSpace(customModsRoot))
                {
                    string cleanMods = Path.TrimEndingDirectorySeparator(Path.GetFullPath(customModsRoot!));
                    psScript.Append($" --mods-path '{EscapeSingleQuotedPowerShell(cleanMods)}'");
                }

                cmdLine = new StringBuilder();
                cmdLine.Append(QuoteCommandLineArg(pwshExe!))
                       .Append(" -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ")
                       .Append(QuoteCommandLineArg(psScript.ToString()));
            }
            else
            {
                appExe = smapiExe;
                cmdLine = new StringBuilder(QuoteCommandLineArg(smapiExe));
                if (!string.IsNullOrWhiteSpace(customModsRoot))
                {
                    string cleanModsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(customModsRoot!));
                    cmdLine.Append(" --mods-path ").Append(QuoteCommandLineArg(cleanModsRoot));
                }
            }

            // Build Unicode environment block inheriting current env + SDV_DEV_* overrides
            var envDict = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                string? k = de.Key?.ToString();
                string? v = de.Value?.ToString();
                if (!string.IsNullOrEmpty(k) && v != null)
                    envDict[k!] = v;
            }
            envDict["SDV_DEV_AUTOMATION"] = "1";
            envDict["SDL_VIDEO_MINIMIZE_ON_FOCUS_LOSS"] = "0";
            if (noFocus || background)
                envDict["SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN"] = "1";
            else
                envDict.Remove("SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN");

            if (background)
                envDict["SDV_DEV_BACKGROUND"] = "1";
            else
                envDict.Remove("SDV_DEV_BACKGROUND");

            if (hideConsole)
                envDict["SDV_DEV_HIDE_CONSOLE"] = "1";
            else
                envDict.Remove("SDV_DEV_HIDE_CONSOLE");

            if (!string.IsNullOrWhiteSpace(autoLoadSaveValue))
                envDict["SDV_DEV_AUTOLOAD_SAVE"] = autoLoadSaveValue!;
            else
                envDict.Remove("SDV_DEV_AUTOLOAD_SAVE");

            var envBlockSb = new StringBuilder();
            foreach (var kv in envDict)
            {
                envBlockSb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
            }
            envBlockSb.Append('\0');
            byte[] envBytes = Encoding.Unicode.GetBytes(envBlockSb.ToString());

            string cmdLineSnapshot = cmdLine.ToString();
            IntPtr envPtr = Marshal.AllocHGlobal(envBytes.Length);
            try
            {
                Marshal.Copy(envBytes, 0, envPtr, envBytes.Length);
                var si = new STARTUPINFO
                {
                    cb = Marshal.SizeOf<STARTUPINFO>(),
                    dwFlags = STARTF_USESHOWWINDOW
                    // 【当初为什么要显式补齐所有分支的 wShowWindow】：
                    // 1. 当 `background` 或 `hideConsole` (`--no-console`) 为真时，外层控制台以 `SW_HIDE` 启动，不弹黑框（`--no-console` 模式下由 BridgeMod 单独点亮 SDL_app 游戏窗口）；
                    // 2. 当正常前台启动 (`background=false && noFocus=false`) 时必须显式传 `SW_SHOWNORMAL (1)`，否则 `dwFlags=0` 会继承隐藏宿主终端的隐藏属性导致窗口“隐身”！
                };
                if (background || hideConsole)
                    si.wShowWindow = SW_HIDE;
                else if (noFocus)
                    si.wShowWindow = SW_SHOWNOACTIVATE;
                else
                    si.wShowWindow = SW_SHOWNORMAL;

                uint baseFlags = CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE | CREATE_NEW_PROCESS_GROUP;
                bool brokeAway = false;
                var cmdTryBreakaway = new StringBuilder(cmdLineSnapshot);

                // 先安全尝试 `CREATE_BREAKAWAY_FROM_JOB`：若宿主作业组允许脱离，则即便同步命令退出也不会连坐杀死游戏；
                // 若宿主作业组未开放 `JOB_OBJECT_LIMIT_BREAKAWAY_OK` 返回 `ERROR_ACCESS_DENIED (5)`，立即自动回退普通创建。
                bool ok = CreateProcessW(
                    appExe,
                    cmdTryBreakaway,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    bInheritHandles: false,
                    dwCreationFlags: baseFlags | CREATE_BREAKAWAY_FROM_JOB,
                    envPtr,
                    gamePath,
                    ref si,
                    out var pi);

                if (ok)
                {
                    brokeAway = true;
                }
                else if (Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED)
                {
                    var cmdFallback = new StringBuilder(cmdLineSnapshot);
                    ok = CreateProcessW(
                        appExe,
                        cmdFallback,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        bInheritHandles: false,
                        dwCreationFlags: baseFlags,
                        envPtr,
                        gamePath,
                        ref si,
                        out pi);
                }

                if (!ok)
                {
                    int winErr = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to launch SMAPI process via CreateProcessW (Win32 error={winErr}).");
                }

                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                return brokeAway;
            }
            finally
            {
                Marshal.FreeHGlobal(envPtr);
            }
        }

        private static string EscapeSingleQuotedPowerShell(string s) => s.Replace("'", "''");

        private static string? ResolveCompanionClientExe()
        {
            try
            {
                string? procPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(procPath))
                {
                    string? dir = Path.GetDirectoryName(procPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        string cand = Path.Combine(dir, "sdv-dev.exe");
                        if (File.Exists(cand))
                            return cand;
                    }
                }
            }
            catch { }

            string fallback = Path.Combine(AppContext.BaseDirectory, "sdv-dev.exe");
            if (File.Exists(fallback))
                return fallback;

            return FindExecutableInPath("sdv-dev.exe");
        }

        private static string? FindExecutableInPath(string exeName)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
                return null;
            foreach (var dir in pathEnv.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                try
                {
                    string cand = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(cand))
                        return cand;
                }
                catch { }
            }
            return null;
        }

        private static string QuoteCommandLineArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";
            if (arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '\"' }) < 0)
                return arg;

            var sb = new StringBuilder();
            sb.Append('\"');
            for (int i = 0; ; i++)
            {
                int numBackslashes = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    i++;
                    numBackslashes++;
                }

                if (i == arg.Length)
                {
                    sb.Append('\\', numBackslashes * 2);
                    break;
                }
                else if (arg[i] == '\"')
                {
                    sb.Append('\\', numBackslashes * 2 + 1);
                    sb.Append('\"');
                }
                else
                {
                    sb.Append('\\', numBackslashes);
                    sb.Append(arg[i]);
                }
            }
            sb.Append('\"');
            return sb.ToString();
        }

        private static JsonSerializerOptions IndentedJson() => new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
