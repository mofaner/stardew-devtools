using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace SdvDevBridge
{
    internal sealed class PendingPipeCommand
    {
        public JObject Request { get; }
        public TaskCompletionSource<JObject> Tcs { get; } = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingPipeCommand(JObject request)
        {
            Request = request;
        }
    }

    internal sealed class PendingScreenCapture
    {
        public string OutputPath { get; }
        public int TargetScreenId { get; }
        public bool IncludeUiLayout { get; }
        public long QueuedTick { get; }
        public TaskCompletionSource<JObject> Tcs { get; }

        public PendingScreenCapture(string outputPath, int targetScreenId, bool includeUiLayout, long queuedTick, TaskCompletionSource<JObject> tcs)
        {
            OutputPath = outputPath;
            TargetScreenId = targetScreenId;
            IncludeUiLayout = includeUiLayout;
            QueuedTick = queuedTick;
            Tcs = tcs;
        }
    }

    internal sealed class PendingCheckpointRestore
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = "Farm";
        public int TileX { get; set; } = 64;
        public int TileY { get; set; } = 15;
        public int Facing { get; set; } = 2;
        public int TimeOfDay { get; set; } = 600;
        public float Stamina { get; set; } = 270f;
        public int Health { get; set; } = 100;
        public int WaitTicks { get; set; }
        public bool WarpIssued { get; set; }
        public TaskCompletionSource<JObject>? Tcs { get; set; }
    }

    internal sealed class PendingDayAdvanceJob
    {
        public int TotalDays { get; }
        public int RemainingDays { get; set; }
        public bool SaveOnNewDay { get; }
        public bool OriginalSaveOnNewDay { get; }
        public bool InTransition { get; set; }
        public int WaitTicks { get; set; }
        public JArray ChangesSummary { get; }
        public TaskCompletionSource<JObject> Tcs { get; }

        public PendingDayAdvanceJob(int days, bool saveOnNewDay, bool origSave, JArray summary, TaskCompletionSource<JObject> tcs)
        {
            TotalDays = days;
            RemainingDays = days;
            SaveOnNewDay = saveOnNewDay;
            OriginalSaveOnNewDay = origSave;
            ChangesSummary = summary;
            Tcs = tcs;
        }
    }

    internal sealed class CollectibleEvalContext : AssemblyLoadContext
    {
        public CollectibleEvalContext() : base("SdvDevEval", isCollectible: true)
        {
            Resolving += OnResolving;
        }

        private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => !a.IsDynamic && string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal sealed class ProfileSampleEntry
    {
        public string Kind { get; set; } = "Event"; // Event or Harmony
        public string ModId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public long Calls;
        public long TotalTicks;
        public long MaxTicks;
    }

    internal static class TemporaryProfilerHooks
    {
        public static volatile bool Active;
        public static Dictionary<MethodBase, ProfileSampleEntry> Entries = new Dictionary<MethodBase, ProfileSampleEntry>();
        private static readonly object Sync = new object();

        public static void SamplePrefix(MethodBase __originalMethod, out long __state)
        {
            __state = Active ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void SamplePostfix(MethodBase __originalMethod, long __state)
        {
            if (__state == 0L || !Active || __originalMethod == null)
                return;

            long elapsed = Stopwatch.GetTimestamp() - __state;
            if (Entries.TryGetValue(__originalMethod, out var entry))
            {
                lock (Sync)
                {
                    entry.Calls++;
                    entry.TotalTicks += elapsed;
                    if (elapsed > entry.MaxTicks)
                        entry.MaxTicks = elapsed;
                }
            }
        }
    }

    internal sealed class PendingProfileSession
    {
        public const string HarmonyOwnerId = "SdvDevBridge.TempProfiler";
        public double TargetSeconds { get; }
        public int TopCount { get; }
        public Stopwatch WallClock { get; } = Stopwatch.StartNew();
        public Harmony TempHarmony { get; } = new Harmony(HarmonyOwnerId);
        public List<long> FrameIntervalTicks { get; } = new List<long>(1024);
        public long LastRenderedTimestamp;
        public long StartAllocBytes { get; }
        public int StartGen0 { get; }
        public int StartGen1 { get; }
        public int StartGen2 { get; }
        public int HookedMethodCount { get; set; }
        public TaskCompletionSource<JObject> Tcs { get; }

        public PendingProfileSession(double targetSeconds, int topCount, TaskCompletionSource<JObject> tcs)
        {
            TargetSeconds = targetSeconds;
            TopCount = topCount;
            Tcs = tcs;
            StartAllocBytes = GC.GetTotalAllocatedBytes(precise: false);
            StartGen0 = GC.CollectionCount(0);
            StartGen1 = GC.CollectionCount(1);
            StartGen2 = GC.CollectionCount(2);
            LastRenderedTimestamp = Stopwatch.GetTimestamp();
        }
    }

    internal sealed class PendingWarpJob
    {
        public int TargetScreenId { get; }
        public string TargetLocation { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public int Facing { get; }
        public bool SkipEvents { get; }
        public int TicksElapsed { get; set; }
        public int PostWarpSettleTicks { get; set; }
        public int CleanedEventCount { get; set; }
        public List<string> SkippedEventIds { get; } = new List<string>();
        public TaskCompletionSource<JObject> Tcs { get; }

        public PendingWarpJob(int targetScreenId, string targetLocation, int x, int y, int facing, bool skipEvents, TaskCompletionSource<JObject> tcs)
        {
            TargetScreenId = targetScreenId;
            TargetLocation = targetLocation;
            TargetX = x;
            TargetY = y;
            Facing = facing;
            SkipEvents = skipEvents;
            Tcs = tcs;
        }
    }

    public sealed partial class ModEntry : Mod
    {
        public const string DedicatedTestSaveFolder = "SdvDevTest_999999999";
        public const ulong DedicatedTestUniqueId = 999999999UL;

        private readonly ConcurrentQueue<PendingPipeCommand> _mainThreadQueue = new ConcurrentQueue<PendingPipeCommand>();
        private readonly ConcurrentQueue<PendingScreenCapture> _screenCaptureQueue = new ConcurrentQueue<PendingScreenCapture>();
        private PendingDayAdvanceJob? _activeDayAdvanceJob;
        private PendingProfileSession? _activeProfileSession;
        private PendingWarpJob? _activeWarpJob;
        private PendingCheckpointRestore? _pendingCheckpointRestore;
        private PendingMultiFrameCapture? _activeMultiFrameCapture;
        private CancellationTokenSource? _cts;
        private string? _pendingAutoLoadSave;
        private int _autoLoadWaitTicks;
        private bool _useDedicatedTestWorld;
        private bool _resetDedicatedTestWorld;
        private bool _automationBackgroundMode;
        private bool _hideConsoleOnly;
        private bool _launchNoFocus;
        private long _tickCounter;

        public static string ComputeUserToken()
        {
            string raw = (Environment.UserName ?? "default").Trim().ToLowerInvariant();
            byte[] bytes = Encoding.UTF8.GetBytes(raw);
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }
            return $"u{hash:x8}";
        }

        public static string GetPipeName()
        {
            return $"sdv-dev-bridge-{ComputeUserToken()}";
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, int bMaximumWindow, ref CONSOLE_FONT_INFOEX lpConsoleCurrentFontEx);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, int bMaximumWindow, ref CONSOLE_FONT_INFOEX lpConsoleCurrentFontEx);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private unsafe struct CONSOLE_FONT_INFOEX
        {
            public uint cbSize;
            public uint nFont;
            public short dwFontSizeX;
            public short dwFontSizeY;
            public uint FontFamily;
            public uint FontWeight;
            public fixed char FaceName[32];
        }

        private static unsafe void EnsureModernUtf8Console()
        {
            try
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return;

                SetConsoleOutputCP(65001);
                SetConsoleCP(65001);
                Console.OutputEncoding = new UTF8Encoding(false);

                IntPtr hOut = GetStdHandle(-11);
                if (hOut == IntPtr.Zero || hOut == new IntPtr(-1))
                    return;

                var cur = new CONSOLE_FONT_INFOEX { cbSize = (uint)sizeof(CONSOLE_FONT_INFOEX) };
                if (!GetCurrentConsoleFontEx(hOut, 0, ref cur))
                    return;

                string face;
                {
                    int len = 0;
                    while (len < 32 && cur.FaceName[len] != '\0') len++;
                    face = new string(cur.FaceName, 0, len);
                }

                if (string.IsNullOrWhiteSpace(face)
                    || face.Equals("Terminal", StringComparison.OrdinalIgnoreCase)
                    || face.Equals("__DefaultTTFont__", StringComparison.OrdinalIgnoreCase)
                    || face.IndexOf("宋体", StringComparison.OrdinalIgnoreCase) >= 0
                    || face.IndexOf("SimSun", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetConsoleFontFace(hOut, "Cascadia Mono", 18);
                    var verify = new CONSOLE_FONT_INFOEX { cbSize = (uint)sizeof(CONSOLE_FONT_INFOEX) };
                    if (GetCurrentConsoleFontEx(hOut, 0, ref verify))
                    {
                        int vLen = 0;
                        while (vLen < 32 && verify.FaceName[vLen] != '\0') vLen++;
                        string vFace = new string(verify.FaceName, 0, vLen);
                        if (!vFace.StartsWith("Cascadia", StringComparison.OrdinalIgnoreCase))
                            SetConsoleFontFace(hOut, "Consolas", 18);
                    }
                }
            }
            catch { }
        }

        private static unsafe void SetConsoleFontFace(IntPtr hOut, string faceName, short height)
        {
            var next = new CONSOLE_FONT_INFOEX
            {
                cbSize = (uint)sizeof(CONSOLE_FONT_INFOEX),
                dwFontSizeY = height,
                FontFamily = 54,
                FontWeight = 400
            };
            int max = Math.Min(faceName.Length, 31);
            for (int i = 0; i < max; i++)
                next.FaceName[i] = faceName[i];
            next.FaceName[max] = '\0';
            SetCurrentConsoleFontEx(hOut, 0, ref next);
        }

        public override void Entry(IModHelper helper)
        {
            EnsureModernUtf8Console();

            _automationBackgroundMode = Environment.GetEnvironmentVariable("SDV_DEV_AUTOMATION") == "1";
            _hideConsoleOnly = Environment.GetEnvironmentVariable("SDV_DEV_HIDE_CONSOLE") == "1";
            _launchNoFocus = Environment.GetEnvironmentVariable("SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN") == "1";
            _pendingAutoLoadSave = Environment.GetEnvironmentVariable("SDV_DEV_AUTOLOAD_SAVE");
            if (!string.IsNullOrEmpty(_pendingAutoLoadSave))
                _automationBackgroundMode = true;

            if (string.Equals(_pendingAutoLoadSave, "__NEW_TEST_SAVE__", StringComparison.OrdinalIgnoreCase))
            {
                _pendingAutoLoadSave = null;
                _useDedicatedTestWorld = true;
            }
            else if (string.Equals(_pendingAutoLoadSave, "__RESET_TEST_SAVE__", StringComparison.OrdinalIgnoreCase))
            {
                _pendingAutoLoadSave = null;
                _useDedicatedTestWorld = true;
                _resetDedicatedTestWorld = true;
            }

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnDisplayRendered;

            _cts = new CancellationTokenSource();
            Task.Run(() => PipeServerLoopAsync(_cts.Token));
            Monitor.Log($"SdvDevBridge listening on named pipe `{GetPipeName()}` (automationMode={_automationBackgroundMode}).", LogLevel.Trace);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Only disable pauseWhenOutOfFocus when launched in AI automation mode (SDV_DEV_AUTOMATION=1)!
            if (_automationBackgroundMode && Game1.options != null)
                Game1.options.pauseWhenOutOfFocus = false;

            ApplyStartupWindowPolicy();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            _tickCounter++;

            if (_automationBackgroundMode && Game1.options != null && Game1.options.pauseWhenOutOfFocus)
                Game1.options.pauseWhenOutOfFocus = false;

            // TitleMenu 首帧可能会重新居中窗口（CenterOnDisplay），前 120 ticks（以及 SetWindowSize 缩放恢复后）持续对齐前后台/无控制台窗口策略
            if ((_tickCounter <= 120 && (_tickCounter % 15 == 1)) || _reapplyBackgroundAfterZoomRestore)
            {
                _reapplyBackgroundAfterZoomRestore = false;
                ApplyStartupWindowPolicy();
            }

            // Handle startup auto-load once TitleMenu is ready
            if ((!string.IsNullOrEmpty(_pendingAutoLoadSave) || _useDedicatedTestWorld) && Game1.activeClickableMenu is TitleMenu)
            {
                _autoLoadWaitTicks++;
                if (_autoLoadWaitTicks >= 20)
                {
                    if (_useDedicatedTestWorld)
                    {
                        bool forceReset = _resetDedicatedTestWorld;
                        _useDedicatedTestWorld = false;
                        _resetDedicatedTestWorld = false;
                        LoadOrCreateDedicatedTestWorld("SdvTester", "SdvDevTest", forceReset);
                    }
                    else if (!string.IsNullOrEmpty(_pendingAutoLoadSave))
                    {
                        string saveToLoad = _pendingAutoLoadSave!;
                        _pendingAutoLoadSave = null;
                        Monitor.Log($"Auto-loading save `{saveToLoad}`...", LogLevel.Info);
                        SaveGame.Load(saveToLoad);
                        Game1.exitActiveMenu();
                    }
                }
            }

            // Fallback if Display.Rendered did not fire within ~50 ticks (e.g. minimized window)
            if (_screenCaptureQueue.TryPeek(out var pendingShot) &&
                Context.ScreenId == pendingShot.TargetScreenId &&
                (_tickCounter - pendingShot.QueuedTick) > 50)
            {
                if (_screenCaptureQueue.TryDequeue(out var expiredShot))
                {
                    expiredShot.Tcs.TrySetResult(CaptureScreenBufferImmediate(expiredShot.OutputPath, expiredShot.IncludeUiLayout));
                }
            }

            if (_pendingCheckpointRestore != null && Context.ScreenId == 0)
            {
                TickCheckpointRestore();
            }

            if (_activeMultiFrameCapture != null && Context.ScreenId == 0)
            {
                TickMultiFrameCapture();
            }

            if (ActiveInputSimInstance != null && Context.ScreenId == 0)
            {
                TickInputSimulation();
            }

            // Tick-driven authentic multi-day overnight progression (`sdv-dev world --advance-days N`)
            if (_activeDayAdvanceJob != null && Context.ScreenId == 0)
            {
                TickDayAdvanceJob();
            }

            if (_activeWarpJob != null && Context.ScreenId == _activeWarpJob.TargetScreenId)
            {
                TickActiveWarpJob();
            }

            if (_activeProfileSession != null && Context.ScreenId == 0 && _activeProfileSession.WallClock.Elapsed.TotalSeconds >= _activeProfileSession.TargetSeconds)
            {
                CompleteProfileSession();
            }

            if (Context.ScreenId != 0)
                return;

            while (_mainThreadQueue.TryDequeue(out var item))
            {
                try
                {
                    string action = item.Request.Value<string>("action")?.ToLowerInvariant() ?? "status";
                    if (action == "screenshot" && string.Equals(item.Request.Value<string>("mode"), "screen", StringComparison.OrdinalIgnoreCase))
                    {
                        string? outPath = item.Request.Value<string>("outPath");
                        if (string.IsNullOrWhiteSpace(outPath))
                        {
                            item.Tcs.TrySetResult(new JObject { ["ok"] = false, ["error"] = "Missing `outPath` for screenshot." });
                        }
                        else
                        {
                            int reqScreen = ParseScreenSelector(item.Request["screen"]) ?? 0;
                            int activeScreens = GameRunner.instance?.gameInstances?.Count ?? 1;
                            if (reqScreen < 0 || reqScreen >= activeScreens)
                            {
                                item.Tcs.TrySetResult(new JObject
                                {
                                    ["ok"] = false,
                                    ["error"] = $"Requested `--screen {reqScreen}`, but only {activeScreens} screen instance(s) are active (0..{activeScreens - 1})."
                                });
                            }
                            else
                            {
                                bool incUi = item.Request.Value<bool?>("uiOverlay") == true;
                                _screenCaptureQueue.Enqueue(new PendingScreenCapture(outPath!, reqScreen, incUi, _tickCounter, item.Tcs));
                            }
                        }
                        continue;
                    }

                    JObject? resp = ExecuteWithScreenContext(item.Request, item.Tcs);
                    if (resp != null)
                        item.Tcs.TrySetResult(resp);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Bridge command failed:\n{ex}", LogLevel.Error);
                    item.Tcs.TrySetResult(new JObject
                    {
                        ["ok"] = false,
                        ["error"] = ex.Message,
                        ["stack"] = ex.ToString()
                    });
                }
            }
        }

        private void OnDisplayRendered(object? sender, RenderedEventArgs e)
        {
            if (_activeProfileSession != null && Context.ScreenId == 0)
            {
                long now = Stopwatch.GetTimestamp();
                long delta = now - _activeProfileSession.LastRenderedTimestamp;
                _activeProfileSession.LastRenderedTimestamp = now;
                if (delta > 0)
                    _activeProfileSession.FrameIntervalTicks.Add(delta);
            }

            while (_screenCaptureQueue.TryPeek(out var peekCap) && peekCap.TargetScreenId == Context.ScreenId)
            {
                if (_screenCaptureQueue.TryDequeue(out var cap))
                {
                    cap.Tcs.TrySetResult(CaptureScreenBufferImmediate(cap.OutputPath, cap.IncludeUiLayout));
                }
            }

            if (_activeMultiFrameCapture != null && Context.ScreenId == 0 && _activeMultiFrameCapture.ReadyForRenderCapture)
            {
                CaptureMultiFrameStepOnRendered();
            }

            if (ActiveInputSimInstance != null && Context.ScreenId == 0 && ActiveInputSimInstance.WaitingForFinalRender)
            {
                CaptureInputSimScreenshotOnRendered();
            }
        }

        private JObject CaptureScreenBufferImmediate(string outPath, bool includeUiLayout = false)
        {
            try
            {
                string fullOut = Path.GetFullPath(outPath);
                string? parent = Path.GetDirectoryName(fullOut);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                RenderTarget2D? worldTarget = Game1.game1?.screen;
                RenderTarget2D? uiTarget = Game1.game1?.uiScreen;

                // Composite world RenderTarget2D + uiScreen RenderTarget2D so active menus, HUD & dialogues are ALWAYS visible in PNG!
                if (worldTarget != null && !worldTarget.IsDisposed)
                {
                    if (uiTarget != null && !uiTarget.IsDisposed)
                    {
                        GraphicsDevice gdComp = Game1.graphics.GraphicsDevice;
                        int outW = uiTarget.Width;
                        int outH = uiTarget.Height;
                        using var compositeRt = new RenderTarget2D(gdComp, outW, outH, false, SurfaceFormat.Color, DepthFormat.None);
                        RenderTargetBinding[] prevTargets = gdComp.GetRenderTargets();
                        try
                        {
                            gdComp.SetRenderTarget(compositeRt);
                            gdComp.Clear(Color.Black);
                            using (var sb = new SpriteBatch(gdComp))
                            {
                                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
                                sb.Draw(worldTarget, new Rectangle(0, 0, outW, outH), Color.White);
                                sb.Draw(uiTarget, new Rectangle(0, 0, outW, outH), Color.White);
                                sb.End();
                            }
                        }
                        finally
                        {
                            gdComp.SetRenderTargets(prevTargets);
                        }

                        using (var fs = new FileStream(fullOut, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            compositeRt.SaveAsPng(fs, outW, outH);
                        }

                        var compRes = new JObject
                        {
                            ["ok"] = true,
                            ["mode"] = "screen",
                            ["outputPng"] = fullOut,
                            ["width"] = outW,
                            ["height"] = outH,
                            ["source"] = "Game1.screen+uiScreen"
                        };
                        if (includeUiLayout)
                            compRes["uiLayout"] = BuildUiJson(maxComponents: 200);
                        return compRes;
                    }

                    using (var fs = new FileStream(fullOut, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        worldTarget.SaveAsPng(fs, worldTarget.Width, worldTarget.Height);
                    }
                    var wRes = new JObject
                    {
                        ["ok"] = true,
                        ["mode"] = "screen",
                        ["outputPng"] = fullOut,
                        ["width"] = worldTarget.Width,
                        ["height"] = worldTarget.Height,
                        ["source"] = "Game1.screen"
                    };
                    if (includeUiLayout)
                        wRes["uiLayout"] = BuildUiJson(maxComponents: 200);
                    return wRes;
                }

                // Fallback: capture GraphicsDevice backbuffer directly
                GraphicsDevice gd = Game1.graphics.GraphicsDevice;
                int w = gd.PresentationParameters.BackBufferWidth;
                int h = gd.PresentationParameters.BackBufferHeight;
                Color[] pixels = new Color[w * h];
                gd.GetBackBufferData(pixels);
                // Ensure opaque alpha on backbuffer capture
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i].A = 255;

                using var tex = new Texture2D(gd, w, h, mipmap: false, SurfaceFormat.Color);
                tex.SetData(pixels);
                using (var fs = new FileStream(fullOut, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    tex.SaveAsPng(fs, w, h);
                }

                return new JObject
                {
                    ["ok"] = true,
                    ["mode"] = "screen",
                    ["outputPng"] = fullOut,
                    ["width"] = w,
                    ["height"] = h,
                    ["source"] = "BackBuffer"
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["ok"] = false,
                    ["error"] = $"Screen capture failed: {ex.Message}"
                };
            }
        }

        private async Task PipeServerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    var pipeSecurity = new PipeSecurity();
                    var currentUser = WindowsIdentity.GetCurrent().User;
                    if (currentUser != null)
                    {
                        pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
                    }

                    server = NamedPipeServerStreamAcl.Create(
                        GetPipeName(),
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 4,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 65536,
                        outBufferSize: 65536,
                        pipeSecurity);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
                    using var writer = new StreamWriter(server, new UTF8Encoding(false), bufferSize: 8192, leaveOpen: true) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var req = JObject.Parse(line);
                        var pending = new PendingPipeCommand(req);
                        _mainThreadQueue.Enqueue(pending);

                        var completed = await Task.WhenAny(pending.Tcs.Task, Task.Delay(90000, token)).ConfigureAwait(false);
                        JObject resp = completed == pending.Tcs.Task
                            ? await pending.Tcs.Task.ConfigureAwait(false)
                            : new JObject { ["ok"] = false, ["error"] = "Main thread execution timed out after 90s." };

                        await writer.WriteLineAsync(resp.ToString(Formatting.None)).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Pipe loop error: {ex.Message}", LogLevel.Trace);
                    await Task.Delay(150, token).ConfigureAwait(false);
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                }
            }
        }

        private JObject? ExecuteWithScreenContext(JObject req, TaskCompletionSource<JObject> tcs)
        {
            int? targetScreen = ParseScreenSelector(req["screen"]);
            var runner = GameRunner.instance;

            if (targetScreen.HasValue && runner?.gameInstances != null && runner.gameInstances.Count > 0)
            {
                if (targetScreen.Value < 0 || targetScreen.Value >= runner.gameInstances.Count)
                {
                    return new JObject
                    {
                        ["ok"] = false,
                        ["error"] = $"Requested `--screen {targetScreen.Value}`, but only {runner.gameInstances.Count} screen instance(s) are active (0..{runner.gameInstances.Count - 1})."
                    };
                }

                Game1? origGame = Game1.game1;
                Game1 chosen = runner.gameInstances[targetScreen.Value];
                if (origGame != null && origGame != chosen)
                {
                    GameRunner.SaveInstance(origGame);
                    GameRunner.LoadInstance(chosen);
                    try
                    {
                        JObject? inner = ExecuteOnMainThread(req, tcs);
                        if (inner != null)
                            inner["screenIndex"] = targetScreen.Value;
                        return inner;
                    }
                    finally
                    {
                        GameRunner.SaveInstance(chosen);
                        GameRunner.LoadInstance(origGame);
                    }
                }
            }

            return ExecuteOnMainThread(req, tcs);
        }

        private static int? ParseScreenSelector(JToken? tok)
        {
            if (tok == null || tok.Type == JTokenType.Null)
                return null;
            if (tok.Type == JTokenType.Integer)
                return tok.Value<int>();
            string s = tok.ToString().Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s))
                return null;
            if (s == "host" || s == "main")
                return 0;
            if (s == "farmhand" || s == "client" || s == "peer")
                return 1;
            return int.TryParse(s, out int idx) ? idx : (int?)null;
        }

        private JObject? ExecuteOnMainThread(JObject req, TaskCompletionSource<JObject>? tcs = null)
        {
            string action = req.Value<string>("action")?.ToLowerInvariant() ?? "status";

            switch (action)
            {
                case "status":
                    return BuildStatusJson();
                case "player":
                    return BuildPlayerJson();
                case "ui":
                    return BuildUiJson();
                case "ui_click":
                    return HandleUiClick(req);
                case "eval_asm":
                    return HandleEvalAssembly(req);
                case "console_cmd":
                    return HandleConsoleCommand(req);
                case "reload_mod":
                case "reload_assets":
                    return HandleReloadAssets(req);
                case "warp":
                    return HandleWarp(req, tcs);
                case "screenshot":
                    return HandleMapScreenshot(req);
                case "map_find_space":
                    return HandleMapFindSpace(req);
                case "harmony_inspect":
                    return HandleHarmonyInspect(req);
                case "asset_inspect":
                    return HandleAssetInspect(req);
                case "event_play":
                    return HandleEventPlay(req);
                case "world_control":
                    return HandleWorldControl(req, tcs);
                case "mp_control":
                    return HandleMultiplayerControl(req);
                case "interact":
                    return HandleInteract(req);
                case "profile":
                    return StartProfileSession(req, tcs);
                case "checkpoint":
                    return HandleCheckpoint(req, tcs);
                case "schedule_check":
                    return HandleScheduleCheck(req);
                case "mod_config":
                    return HandleModConfig(req);
                case "screenshot_advanced":
                    return StartMultiFrameCapture(req, tcs);
                case "input_simulate":
                    return StartInputSimulation(req, tcs);
                case "window_control":
                    return HandleWindowControl(req);
                case "self_restart":
                    return HandleSelfRestart(req);
                case "load_save":
                    return HandleLoadSave(req);
                case "new_test_save":
                    string fName = req.Value<string>("farmer") ?? "SdvTester";
                    string farm = req.Value<string>("farm") ?? "SdvDevTest";
                    bool reset = req.Value<bool?>("reset") == true;
                    if (Context.IsWorldReady)
                    {
                        _useDedicatedTestWorld = true;
                        _resetDedicatedTestWorld = reset;
                        _autoLoadWaitTicks = 0;
                        Game1.ExitToTitle();
                        return new JObject { ["ok"] = true, ["message"] = $"Returning to title and loading dedicated test save `{DedicatedTestSaveFolder}`..." };
                    }
                    LoadOrCreateDedicatedTestWorld(fName, farm, reset);
                    return new JObject { ["ok"] = true, ["saveFolder"] = DedicatedTestSaveFolder, ["message"] = $"Loading dedicated test save `{DedicatedTestSaveFolder}`..." };
                case "exit_to_title":
                    Game1.ExitToTitle();
                    return new JObject { ["ok"] = true, ["message"] = "Exiting to title screen without saving." };
                case "quit":
                    Game1.quit = true;
                    return new JObject { ["ok"] = true, ["message"] = "Shutting down game." };
                default:
                    return new JObject { ["ok"] = false, ["error"] = $"Unknown bridge action `{action}`." };
            }
        }

        private JObject BuildStatusJson()
        {
            var mods = Helper.ModRegistry.GetAll()
                .Select(m => new JObject
                {
                    ["uniqueId"] = m.Manifest.UniqueID,
                    ["name"] = m.Manifest.Name,
                    ["version"] = m.Manifest.Version.ToString(),
                    ["isContentPack"] = m.IsContentPack
                })
                .ToList();

            string activeModsPath = Path.GetDirectoryName(Helper.DirectoryPath) ?? Path.Combine(Constants.GamePath, "Mods");
            string defaultModsPath = Path.Combine(Constants.GamePath, "Mods");
            bool isIsolated = !string.Equals(
                Path.GetFullPath(activeModsPath).TrimEnd('\\', '/'),
                Path.GetFullPath(defaultModsPath).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);

            string? activeMenu = Game1.activeClickableMenu?.GetType().FullName;
            if (Game1.activeClickableMenu is TitleMenu && TitleMenu.subMenu != null)
                activeMenu += $" (subMenu: {TitleMenu.subMenu.GetType().FullName})";

            var winInfo = InspectCurrentWindowState();
            var obj = new JObject
            {
                ["ok"] = true,
                ["bridgeConnected"] = true,
                ["gameVersion"] = Game1.version,
                ["smapiVersion"] = Constants.ApiVersion.ToString(),
                ["modsPath"] = activeModsPath,
                ["isolatedMods"] = isIsolated,
                ["backgroundMode"] = _hiddenBackgroundWindow,
                ["hideConsole"] = _hideConsoleOnly,
                ["windowVisible"] = winInfo.Value<bool?>("windowVisible") ?? !_hiddenBackgroundWindow,
                ["consoleVisible"] = winInfo.Value<bool?>("consoleVisible") ?? (!_hiddenBackgroundWindow && !_hideConsoleOnly),
                ["windowBounds"] = winInfo["windowBounds"],
                ["desktopName"] = winInfo["desktopName"],
                ["onInteractiveDesktop"] = winInfo["onInteractiveDesktop"],
                ["inWorld"] = Context.IsWorldReady,
                ["isPlayerFree"] = Context.IsPlayerFree,
                ["eventActive"] = Game1.eventUp || Game1.CurrentEvent != null || Game1.currentLocation?.currentEvent != null,
                ["activeEventId"] = (Game1.CurrentEvent ?? Game1.currentLocation?.currentEvent)?.id,
                ["screenCount"] = GameRunner.instance?.gameInstances?.Count ?? 1,
                ["saveFolderName"] = Constants.SaveFolderName,
                ["currentSavePath"] = Constants.CurrentSavePath,
                ["activeMenu"] = activeMenu,
                ["loadedModCount"] = mods.Count,
                ["mods"] = new JArray(mods)
            };

            if (Context.IsWorldReady && Game1.player != null)
            {
                obj["playerName"] = Game1.player.Name;
                obj["farmName"] = Game1.player.farmName.Value;
                obj["location"] = Game1.currentLocation?.NameOrUniqueName;
                obj["tileX"] = Game1.player.TilePoint.X;
                obj["tileY"] = Game1.player.TilePoint.Y;
                obj["date"] = $"Year {Game1.year} {Game1.currentSeason} {Game1.dayOfMonth}";
                obj["timeOfDay"] = Game1.timeOfDay;
                obj["money"] = Game1.player.Money;
                obj["stamina"] = Game1.player.Stamina;
                obj["maxStamina"] = Game1.player.MaxStamina;
                obj["health"] = Game1.player.health;
                obj["maxHealth"] = Game1.player.maxHealth;
            }

            return obj;
        }

        private JObject BuildPlayerJson()
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return new JObject { ["ok"] = false, ["error"] = "World is not loaded yet (`inWorld=false`)." };

            var p = Game1.player;
            var inv = new JArray();
            for (int i = 0; i < p.Items.Count; i++)
            {
                var it = p.Items[i];
                if (it == null)
                    continue;
                inv.Add(new JObject
                {
                    ["slot"] = i,
                    ["qualifiedItemId"] = it.QualifiedItemId,
                    ["itemId"] = it.ItemId,
                    ["name"] = it.Name,
                    ["displayName"] = it.DisplayName,
                    ["stack"] = it.Stack,
                    ["quality"] = it.Quality,
                    ["category"] = it.Category
                });
            }

            return new JObject
            {
                ["ok"] = true,
                ["name"] = p.Name,
                ["farmName"] = p.farmName.Value,
                ["location"] = Game1.currentLocation?.NameOrUniqueName,
                ["tile"] = new JObject { ["x"] = p.TilePoint.X, ["y"] = p.TilePoint.Y },
                ["money"] = p.Money,
                ["stamina"] = p.Stamina,
                ["maxStamina"] = p.MaxStamina,
                ["health"] = p.health,
                ["maxHealth"] = p.maxHealth,
                ["skills"] = new JObject
                {
                    ["farming"] = p.FarmingLevel,
                    ["mining"] = p.MiningLevel,
                    ["foraging"] = p.ForagingLevel,
                    ["fishing"] = p.FishingLevel,
                    ["combat"] = p.CombatLevel
                },
                ["inventoryCount"] = inv.Count,
                ["inventory"] = inv
            };
        }

        private JObject BuildUiJson(int maxComponents = 80)
        {
            IClickableMenu? menu = Game1.activeClickableMenu;
            if (menu is TitleMenu && TitleMenu.subMenu != null)
                menu = TitleMenu.subMenu;

            if (menu == null)
                return new JObject { ["ok"] = true, ["hasActiveMenu"] = false };

            menu.populateClickableComponentList();
            var comps = new JArray();
            if (menu.allClickableComponents != null)
            {
                foreach (var c in menu.allClickableComponents.Where(c => c != null && c.visible).Take(maxComponents))
                {
                    comps.Add(new JObject
                    {
                        ["myID"] = c.myID,
                        ["name"] = c.name,
                        ["label"] = c.label,
                        ["region"] = c.region,
                        ["upNeighborID"] = c.upNeighborID,
                        ["downNeighborID"] = c.downNeighborID,
                        ["leftNeighborID"] = c.leftNeighborID,
                        ["rightNeighborID"] = c.rightNeighborID,
                        ["hoverText"] = (c as ClickableTextureComponent)?.hoverText,
                        ["bounds"] = new JObject
                        {
                            ["x"] = c.bounds.X,
                            ["y"] = c.bounds.Y,
                            ["w"] = c.bounds.Width,
                            ["h"] = c.bounds.Height,
                            ["centerX"] = c.bounds.Center.X,
                            ["centerY"] = c.bounds.Center.Y
                        }
                    });
                }
            }

            var res = new JObject
            {
                ["ok"] = true,
                ["hasActiveMenu"] = true,
                ["menuType"] = menu.GetType().FullName,
                ["uiViewport"] = new JObject
                {
                    ["width"] = Game1.uiViewport.Width,
                    ["height"] = Game1.uiViewport.Height
                },
                ["bounds"] = new JObject
                {
                    ["x"] = menu.xPositionOnScreen,
                    ["y"] = menu.yPositionOnScreen,
                    ["width"] = menu.width,
                    ["height"] = menu.height
                },
                ["components"] = comps
            };

            if (menu is DialogueBox db)
            {
                res["currentDialogue"] = db.getCurrentString();
                res["isQuestion"] = db.isQuestion;
                if (db.responses != null)
                {
                    var rArr = new JArray();
                    for (int i = 0; i < db.responses.Length; i++)
                    {
                        rArr.Add(new JObject
                        {
                            ["index"] = i,
                            ["responseKey"] = db.responses[i].responseKey,
                            ["responseText"] = db.responses[i].responseText
                        });
                    }
                    res["responses"] = rArr;
                }
            }
            else if (menu is ShopMenu shop)
            {
                var shopArr = new JArray();
                foreach (var salable in shop.forSale.Take(40))
                {
                    shop.itemPriceAndStock.TryGetValue(salable, out var stockInfo);
                    shopArr.Add(new JObject
                    {
                        ["qualifiedItemId"] = (salable as Item)?.QualifiedItemId,
                        ["name"] = salable.Name,
                        ["displayName"] = salable.DisplayName,
                        ["price"] = stockInfo?.Price ?? 0,
                        ["stock"] = stockInfo?.Stock ?? 0
                    });
                }
                res["shopId"] = shop.ShopId;
                res["forSale"] = shopArr;
            }

            return res;
        }

        private JObject HandleUiClick(JObject req)
        {
            IClickableMenu? menu = Game1.activeClickableMenu;
            if (menu is TitleMenu && TitleMenu.subMenu != null)
                menu = TitleMenu.subMenu;

            if (menu == null)
                return new JObject { ["ok"] = false, ["error"] = "No active menu to click." };

            if (req.Value<bool?>("close") == true)
            {
                string closedType = menu.GetType().Name;
                menu.exitThisMenu();
                // 确保 DialogueBox 等带渐隐收缩动画的菜单在同帧立即解绑，防止数帧后动画结束时误清空后续紧接着打开的新菜单
                if (ReferenceEquals(Game1.activeClickableMenu, menu))
                {
                    Game1.activeClickableMenu = null;
                    Game1.dialogueUp = false;
                }
                else if (Game1.activeClickableMenu is TitleMenu && ReferenceEquals(TitleMenu.subMenu, menu))
                {
                    TitleMenu.subMenu = null;
                }
                return new JObject { ["ok"] = true, ["message"] = $"Closed `{closedType}`." };
            }

            if (menu is DialogueBox db)
            {
                // Fast-forward DialogueBox intro transition, typewriter text, & 750ms safetyTimer lock so CLI clicks NEVER no-op!
                db.transitioning = false;
                db.safetyTimer = 0;
                db.questionFinishPauseTimer = 0;
                string curDialogueText = db.getCurrentString() ?? string.Empty;
                db.characterIndexInDialogue = Math.Max(0, curDialogueText.Length);

                int? respIdx = req.Value<int?>("responseIndex");
                if (respIdx.HasValue)
                {
                    if (!db.isQuestion || db.responses == null || db.responses.Length == 0)
                        return new JObject { ["ok"] = false, ["error"] = "Active `DialogueBox` is not a question dialogue with selectable responses." };
                    if (respIdx.Value < 0 || respIdx.Value >= db.responses.Length)
                        return new JObject { ["ok"] = false, ["error"] = $"Response index {respIdx.Value} is out of range (valid `0..{db.responses.Length - 1}`)." };

                    string chosenResponseText = db.responses[respIdx.Value].responseText;
                    string chosenResponseKey = db.responses[respIdx.Value].responseKey;
                    db.selectedResponse = respIdx.Value;
                    if (db.responseCC != null && respIdx.Value < db.responseCC.Count)
                    {
                        var pt = db.responseCC[respIdx.Value].bounds.Center;
                        db.receiveLeftClick(pt.X, pt.Y, playSound: true);
                    }
                    else
                    {
                        db.receiveLeftClick(db.xPositionOnScreen + 32, db.yPositionOnScreen + 32, playSound: true);
                    }
                    return new JObject
                    {
                        ["ok"] = true,
                        ["clickedResponseIndex"] = respIdx.Value,
                        ["clickedResponseKey"] = chosenResponseKey,
                        ["clickedResponse"] = chosenResponseText
                    };
                }
            }

            int? x = req.Value<int?>("x");
            int? y = req.Value<int?>("y");
            int? compId = req.Value<int?>("componentId");
            string? matchText = req.Value<string>("match");

            menu.populateClickableComponentList();
            if (compId.HasValue && menu.allClickableComponents != null)
            {
                var c = menu.allClickableComponents.FirstOrDefault(cc => cc != null && cc.myID == compId.Value);
                if (c == null)
                    return new JObject { ["ok"] = false, ["error"] = $"Component with myID={compId.Value} not found." };
                x = c.bounds.Center.X;
                y = c.bounds.Center.Y;
            }
            else if (!string.IsNullOrWhiteSpace(matchText) && menu.allClickableComponents != null)
            {
                var c = menu.allClickableComponents.FirstOrDefault(cc =>
                    cc != null && cc.visible &&
                    ((cc.name != null && cc.name.IndexOf(matchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (cc.label != null && cc.label.IndexOf(matchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     ((cc as ClickableTextureComponent)?.hoverText?.IndexOf(matchText, StringComparison.OrdinalIgnoreCase) >= 0)));
                if (c == null)
                    return new JObject { ["ok"] = false, ["error"] = $"No visible component matched `{matchText}`." };
                x = c.bounds.Center.X;
                y = c.bounds.Center.Y;
            }

            if (!x.HasValue || !y.HasValue)
                return new JObject { ["ok"] = false, ["error"] = "Specify `--id`, `--match`, `--response`, `--close`, or `--xy x,y`." };

            menu.performHoverAction(x.Value, y.Value);
            menu.receiveLeftClick(x.Value, y.Value, playSound: true);
            return new JObject
            {
                ["ok"] = true,
                ["menuType"] = menu.GetType().FullName,
                ["clickedX"] = x.Value,
                ["clickedY"] = y.Value
            };
        }

        private JObject? HandleWarp(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return new JObject { ["ok"] = false, ["error"] = "World is not loaded (`inWorld=false`)." };

            string? locQuery = req.Value<string>("location");
            if (string.IsNullOrWhiteSpace(locQuery))
                return new JObject { ["ok"] = false, ["error"] = "Missing target `location` name." };

            GameLocation? loc = FindLocationFlexible(locQuery!);
            if (loc == null)
                return new JObject { ["ok"] = false, ["error"] = $"Location `{locQuery}` not found in `Game1.locations`." };

            int facing = req.Value<int?>("facing") ?? 2;
            int? rx = req.Value<int?>("x");
            int? ry = req.Value<int?>("y");
            bool explicitCoords = rx.HasValue && ry.HasValue;
            bool skipEvents = req.Value<bool?>("skipEvents") ?? explicitCoords;

            int tx, ty;
            if (explicitCoords)
            {
                tx = rx!.Value;
                ty = ry!.Value;
            }
            else
            {
                var autoTile = FindSafeLandingTile(loc);
                tx = autoTile.X;
                ty = autoTile.Y;
            }

            var preSkippedIds = new List<string>();
            if (skipEvents && (Game1.eventUp || Game1.CurrentEvent != null || Game1.currentLocation?.currentEvent != null))
            {
                AbortCurrentEventForWarp(loc, tx, ty, facing, preSkippedIds);
            }

            Game1.exitActiveMenu();
            Game1.warpFarmer(loc.NameOrUniqueName, tx, ty, facing);
            // Fast-complete fade transition on next tick (same technique vanilla uses for IsDedicatedHost) so CLI warp returns synchronously!
            if (Game1.isWarping || Game1.locationRequest != null)
            {
                Game1.fadeToBlackAlpha = 1.1f;
                Game1.fadeToBlack = true;
                Game1.nonWarpFade = false;
                if (tcs != null && _activeWarpJob == null)
                {
                    var job = new PendingWarpJob(Context.ScreenId, loc.NameOrUniqueName, tx, ty, facing, skipEvents, tcs);
                    if (preSkippedIds.Count > 0)
                    {
                        job.CleanedEventCount += preSkippedIds.Count;
                        job.SkippedEventIds.AddRange(preSkippedIds);
                    }
                    _activeWarpJob = job;
                    return null;
                }
            }

            Event? curEvt = Game1.CurrentEvent ?? Game1.currentLocation?.currentEvent;
            var syncRes = new JObject
            {
                ["ok"] = true,
                ["location"] = loc.NameOrUniqueName,
                ["targetX"] = tx,
                ["targetY"] = ty,
                ["facing"] = facing,
                ["eventActive"] = Game1.eventUp || curEvt != null,
                ["eventId"] = curEvt?.id
            };
            if (preSkippedIds.Count > 0)
                syncRes["skippedEventId"] = preSkippedIds[0];
            return syncRes;
        }

        /// <summary>
        /// 驱动异步切图落地与进图自动触发剧情 (`CurrentEvent`) 的无残留清理：
        ///
        /// 【当初为什么要写这个机制及原版二段传送坑说明】：
        /// 1. 在新测试档调 `sdv-dev warp Town 45 71` 或 `Beach 38 14` 时，原版 `performWarpFarmer -> resetForPlayerEntry -> checkForEvents()`
        ///    会自动拉起进图剧情（如刘易斯社区中心 `611439`、威利送鱼竿 `739330`），把 `eventUp`/`viewportFreeze` 锁死并把玩家和镜头拽离目标格；
        /// 2. 原版 `Event.exitEvent()` 并不会立即清理 `currentLocation.currentEvent`，而是设 `eventOver = true` 等下一帧 `Game1.eventFinished()`；
        ///    且 `Game1.eventFinished()` 内部会**再次调用 `warpFarmer(exitLocation, positionBeforeEvent.X, positionBeforeEvent.Y, ...)`**！
        ///    因此必须先将 `exitLocation` 与 `positionBeforeEvent` 对齐至 `(TargetX, TargetY)`，再连续执行 `exitEvent() + eventFinished()`，
        ///    并等该收尾传送彻底落地后固定玩家坐标与 `Game1.UpdateViewPort(true, player.getStandingPosition())`。
        /// </summary>
        private void TickActiveWarpJob()
        {
            var wj = _activeWarpJob;
            if (wj == null)
                return;

            wj.TicksElapsed++;
            if ((Game1.isWarping || Game1.locationRequest != null) && wj.TicksElapsed <= 90)
            {
                Game1.fadeToBlackAlpha = 1.1f;
                Game1.fadeToBlack = true;
                Game1.nonWarpFade = false;
                return;
            }

            // 留 1 tick 让 SMAPI `Player.Warped` 上的 Mod 剧情触发器也执行完毕再检查是否被劫持进剧情
            if (wj.PostWarpSettleTicks < 1 && wj.TicksElapsed <= 90)
            {
                wj.PostWarpSettleTicks++;
                return;
            }

            Event? activeEvt = Game1.CurrentEvent ?? Game1.currentLocation?.currentEvent;
            if (wj.SkipEvents && (activeEvt != null || Game1.eventUp) && wj.CleanedEventCount < 4 && wj.TicksElapsed <= 90)
            {
                GameLocation? dstLoc = FindLocationFlexible(wj.TargetLocation) ?? Game1.currentLocation;
                AbortCurrentEventForWarp(dstLoc, wj.TargetX, wj.TargetY, wj.Facing, wj.SkippedEventIds);
                wj.CleanedEventCount++;
                wj.PostWarpSettleTicks = 0;

                if (Game1.isWarping || Game1.locationRequest != null)
                {
                    Game1.fadeToBlackAlpha = 1.1f;
                    Game1.fadeToBlack = true;
                    Game1.nonWarpFade = false;
                    return;
                }
            }

            _activeWarpJob = null;

            if (wj.SkipEvents && Game1.player != null)
            {
                GameLocation? expectedLoc = FindLocationFlexible(wj.TargetLocation);
                if (expectedLoc != null && Game1.currentLocation == expectedLoc)
                {
                    Game1.player.setTileLocation(new Vector2(wj.TargetX, wj.TargetY));
                    Game1.player.faceDirection(wj.Facing);
                }

                if (wj.CleanedEventCount > 0)
                {
                    Game1.eventUp = false;
                    Game1.eventOver = false;
                    Game1.displayHUD = true;
                    Game1.viewportFreeze = false;
                    Game1.player.CanMove = true;
                    Game1.player.freezePause = 0;
                }

                Game1.UpdateViewPort(true, Game1.player.StandingPixel);
            }

            Game1.fadeToBlack = false;
            Game1.fadeToBlackAlpha = 0f;
            Game1.fadeIn = false;

            Event? finalEvt = Game1.CurrentEvent ?? Game1.currentLocation?.currentEvent;
            bool eventActive = Game1.eventUp || finalEvt != null;
            var res = new JObject
            {
                ["ok"] = true,
                ["screenIndex"] = Context.ScreenId,
                ["location"] = Game1.currentLocation?.NameOrUniqueName ?? wj.TargetLocation,
                ["targetX"] = Game1.player?.TilePoint.X ?? wj.TargetX,
                ["targetY"] = Game1.player?.TilePoint.Y ?? wj.TargetY,
                ["facing"] = Game1.player?.FacingDirection ?? wj.Facing,
                ["eventActive"] = eventActive,
                ["eventId"] = finalEvt?.id
            };
            if (wj.SkippedEventIds.Count > 0)
            {
                res["skippedEventId"] = wj.SkippedEventIds[0];
                if (wj.SkippedEventIds.Count > 1)
                    res["skippedEventIds"] = new JArray(wj.SkippedEventIds);
            }
            wj.Tcs.TrySetResult(res);
        }

        private static void AbortCurrentEventForWarp(GameLocation? dstLoc, int targetX, int targetY, int facing, List<string> recordedIds)
        {
            Event? evt = Game1.CurrentEvent ?? Game1.currentLocation?.currentEvent;
            if (evt != null)
            {
                string evtId = !string.IsNullOrWhiteSpace(evt.id) ? evt.id : "custom";
                if (!recordedIds.Contains(evtId))
                    recordedIds.Add(evtId);

                if (dstLoc != null)
                    evt.exitLocation = new LocationRequest(dstLoc.NameOrUniqueName, dstLoc.isStructure.Value, dstLoc);
                if (Game1.player != null)
                {
                    Game1.player.positionBeforeEvent = new Vector2(targetX, targetY);
                    Game1.player.orientationBeforeEvent = facing;
                }

                try { evt.exitEvent(); } catch { }
            }

            try
            {
                if (Game1.player != null)
                {
                    Game1.player.positionBeforeEvent = new Vector2(targetX, targetY);
                    Game1.player.orientationBeforeEvent = facing;
                }
                Game1.eventFinished();
            }
            catch
            {
                if (Game1.currentLocation != null)
                {
                    try { Game1.currentLocation.currentEvent?.cleanup(); } catch { }
                    Game1.currentLocation.currentEvent = null;
                }
                Game1.eventUp = false;
                Game1.eventOver = false;
                Game1.displayHUD = true;
                Game1.viewportFreeze = false;
            }
        }

        private JObject HandleMapScreenshot(JObject req)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null)
                return new JObject { ["ok"] = false, ["error"] = "Map screenshot requires `inWorld=true`." };

            string? outPath = req.Value<string>("outPath");
            if (string.IsNullOrWhiteSpace(outPath))
                return new JObject { ["ok"] = false, ["error"] = "Missing `outPath` for screenshot." };

            float scale = req.Value<float?>("scale") ?? 1.0f;
            if (scale <= 0.05f) scale = 0.25f;
            if (scale > 2.0f) scale = 2.0f;

            string tempShotId = $"sdv_dev_shot_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
            string? writtenFile = Game1.game1.takeMapScreenshot(scale, tempShotId, null);
            string shotsDir = Game1.game1.GetScreenshotFolder(createIfMissing: true);

            string? srcPng = null;
            if (!string.IsNullOrWhiteSpace(writtenFile))
            {
                string cand1 = Path.IsPathRooted(writtenFile) ? writtenFile! : Path.Combine(shotsDir, writtenFile!);
                if (File.Exists(cand1))
                    srcPng = cand1;
                else if (File.Exists(cand1 + ".png"))
                    srcPng = cand1 + ".png";
            }
            if (srcPng == null)
            {
                string cand2 = Path.Combine(shotsDir, tempShotId + ".png");
                if (File.Exists(cand2))
                    srcPng = cand2;
            }

            if (srcPng == null || !File.Exists(srcPng))
                return new JObject { ["ok"] = false, ["error"] = $"takeMapScreenshot failed to write PNG in `{shotsDir}`." };

            string fullOut = Path.GetFullPath(outPath!);
            string? outDir = Path.GetDirectoryName(fullOut);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            File.Copy(srcPng, fullOut, overwrite: true);
            try
            {
                if (!string.Equals(Path.GetFullPath(srcPng), fullOut, StringComparison.OrdinalIgnoreCase))
                    File.Delete(srcPng);
            }
            catch { }

            return new JObject
            {
                ["ok"] = true,
                ["mode"] = "map",
                ["location"] = Game1.currentLocation.NameOrUniqueName,
                ["scale"] = scale,
                ["outputPng"] = fullOut
            };
        }

        private JObject HandleReloadAssets(JObject req)
        {
            string? uniqueId = req.Value<string>("uniqueId");
            bool reloadMap = req.Value<bool?>("reloadMap") ?? false;
            var invalidated = new List<string>();
            var reloadedShaders = new List<string>();
            string? shaderHookCalled = null;

            // 同步热刷新 C# 模组已加载的 `.mgfxo` Effect 实例或触发 Content Patcher 的 `patch reload`
            if (!string.IsNullOrWhiteSpace(uniqueId))
            {
                var modInfo = Helper.ModRegistry.Get(uniqueId!);
                if (modInfo == null || modInfo.IsContentPack)
                {
                    HandleConsoleCommand(new JObject { ["command"] = $"patch reload {uniqueId}" });
                }
                else
                {
                    HotReloadModShadersInMemory(modInfo, req["compiledShaders"] as JArray, reloadedShaders, out shaderHookCalled);
                }
            }

            if (req["assets"] is JArray assetsArr)
            {
                foreach (var rawAsset in assetsArr.Values<string>())
                {
                    if (string.IsNullOrWhiteSpace(rawAsset))
                        continue;

                    string asset = rawAsset!.Trim().Replace('\\', '/');
                    if (asset.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        asset = Path.ChangeExtension(asset, null) ?? asset;

                    bool any = Helper.GameContent.InvalidateCache(asset);
                    if (!any)
                    {
                        Helper.GameContent.InvalidateCache(info =>
                            info.NameWithoutLocale.IsEquivalentTo(asset) ||
                            info.NameWithoutLocale.BaseName.EndsWith("/" + asset, StringComparison.OrdinalIgnoreCase));
                    }

                    invalidated.Add(asset);
                    if (asset.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase) ||
                        asset.IndexOf("tilesheet", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        reloadMap = true;
                    }
                }
            }

            string? reloadedLocation = null;
            if (reloadMap && Context.IsWorldReady && Game1.currentLocation != null)
            {
                var loc = Game1.currentLocation;
                loc.reloadMap();
                loc.updateDoors();
                loc.updateWarps();
                loc.resetForPlayerEntry();
                reloadedLocation = loc.NameOrUniqueName;
            }

            return new JObject
            {
                ["ok"] = true,
                ["uniqueId"] = uniqueId,
                ["invalidatedAssets"] = new JArray(invalidated),
                ["reloadedShaders"] = reloadedShaders.Count > 0 ? new JArray(reloadedShaders) : null,
                ["shaderReloadHook"] = shaderHookCalled,
                ["reloadedLocation"] = reloadedLocation
            };
        }

        private JObject HandleMapFindSpace(JObject req)
        {
            if (!Context.IsWorldReady)
                return new JObject { ["ok"] = false, ["error"] = "map-find-space requires `inWorld=true` so Content Patcher & SVE map edits are active." };

            string? locName = req.Value<string>("location") ?? Game1.currentLocation?.NameOrUniqueName;
            if (string.IsNullOrWhiteSpace(locName))
                return new JObject { ["ok"] = false, ["error"] = "Specify a target `location`." };

            GameLocation? loc = FindLocationFlexible(locName!);
            if (loc == null || loc.Map == null)
                return new JObject { ["ok"] = false, ["error"] = $"Location `{locName}` not found or has no loaded Map." };

            int reqW = Math.Max(1, req.Value<int?>("width") ?? 4);
            int reqH = Math.Max(1, req.Value<int?>("height") ?? 4);
            int limit = Math.Max(1, req.Value<int?>("limit") ?? 10);
            int? nearX = req.Value<int?>("nearX");
            int? nearY = req.Value<int?>("nearY");

            var backLayer = loc.Map.GetLayer("Back");
            int mapW = backLayer?.LayerWidth ?? loc.Map.DisplayWidth / 64;
            int mapH = backLayer?.LayerHeight ?? loc.Map.DisplayHeight / 64;

            // Collect reserved Warp buffers (1-tile margin) & NPC schedule walking routes in this location!
            var warpBuffer = new HashSet<Point>();
            foreach (var w in loc.warps)
            {
                int cx = Math.Max(0, Math.Min(mapW - 1, w.X));
                int cy = Math.Max(0, Math.Min(mapH - 1, w.Y));
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        warpBuffer.Add(new Point(cx + dx, cy + dy));
            }

            var npcRouteTiles = CollectNpcScheduleRouteTiles(loc);

            bool[,] clear = new bool[mapW, mapH];
            var buildingsLayer = loc.Map.GetLayer("Buildings");
            CollisionMask blockMask = CollisionMask.All & ~CollisionMask.Farmers & ~CollisionMask.Characters;

            for (int y = 0; y < mapH; y++)
            {
                for (int x = 0; x < mapW; x++)
                {
                    var pt = new Point(x, y);
                    if (warpBuffer.Contains(pt) || npcRouteTiles.Contains(pt))
                        continue;
                    if (loc.isWaterTile(x, y))
                        continue;
                    if (!loc.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport))
                        continue;
                    if (loc.IsTileBlockedBy(new Vector2(x, y), blockMask))
                        continue;

                    // Also skip tiles with existing Action / TouchAction properties
                    string? act = loc.doesTileHaveProperty(x, y, "Action", "Buildings");
                    string? tact = loc.doesTileHaveProperty(x, y, "TouchAction", "Back");
                    if (!string.IsNullOrEmpty(act) || !string.IsNullOrEmpty(tact))
                        continue;
                    if (buildingsLayer != null && buildingsLayer.Tiles[x, y] != null)
                        continue;

                    clear[x, y] = true;
                }
            }

            var rawCandidates = new List<(int X, int Y, double Score)>();
            for (int y = 1; y + reqH < mapH; y++)
            {
                for (int x = 1; x + reqW < mapW; x++)
                {
                    bool allOk = true;
                    for (int dy = 0; dy < reqH && allOk; dy++)
                    {
                        for (int dx = 0; dx < reqW; dx++)
                        {
                            if (!clear[x + dx, y + dy])
                            {
                                allOk = false;
                                break;
                            }
                        }
                    }

                    if (!allOk)
                        continue;

                    double dist = 0;
                    if (nearX.HasValue && nearY.HasValue)
                    {
                        double cx = x + reqW / 2.0;
                        double cy = y + reqH / 2.0;
                        dist = Math.Sqrt((cx - nearX.Value) * (cx - nearX.Value) + (cy - nearY.Value) * (cy - nearY.Value));
                    }
                    else
                    {
                        double cx = x + reqW / 2.0;
                        double cy = y + reqH / 2.0;
                        dist = Math.Sqrt((cx - mapW / 2.0) * (cx - mapW / 2.0) + (cy - mapH / 2.0) * (cy - mapH / 2.0));
                    }

                    rawCandidates.Add((x, y, dist));
                }
            }

            // Non-overlapping greedy selection so returned spots are distinct regions across the map
            var chosen = new JArray();
            var occupiedRects = new List<Rectangle>();
            foreach (var cand in rawCandidates.OrderBy(c => c.Score))
            {
                var r = new Rectangle(cand.X, cand.Y, reqW, reqH);
                if (occupiedRects.Any(o => o.Intersects(r)))
                    continue;

                occupiedRects.Add(r);
                chosen.Add(new JObject
                {
                    ["x"] = cand.X,
                    ["y"] = cand.Y,
                    ["width"] = reqW,
                    ["height"] = reqH,
                    ["center"] = $"{cand.X + reqW / 2},{cand.Y + reqH / 2}",
                    ["distanceScore"] = Math.Round(cand.Score, 1)
                });

                if (chosen.Count >= limit)
                    break;
            }

            return new JObject
            {
                ["ok"] = true,
                ["location"] = loc.NameOrUniqueName,
                ["mapSize"] = $"{mapW}x{mapH}",
                ["requestedSize"] = $"{reqW}x{reqH}",
                ["avoidedWarpBufferTiles"] = warpBuffer.Count,
                ["avoidedNpcScheduleTiles"] = npcRouteTiles.Count,
                ["candidateCount"] = chosen.Count,
                ["candidates"] = chosen
            };
        }

        private static HashSet<Point> CollectNpcScheduleRouteTiles(GameLocation targetLoc)
        {
            var points = new HashSet<Point>();
            string locName = targetLoc.NameOrUniqueName;

            foreach (var loc in Game1.locations)
            {
                foreach (var ch in loc.characters)
                {
                    if (ch == null)
                        continue;
                    if (string.Equals(loc.NameOrUniqueName, locName, StringComparison.OrdinalIgnoreCase))
                        points.Add(ch.TilePoint);

                    var sched = ch.Schedule;
                    if (sched == null)
                        continue;

                    foreach (var entry in sched.Values)
                    {
                        if (entry == null)
                            continue;
                        if (string.Equals(entry.targetLocationName, locName, StringComparison.OrdinalIgnoreCase))
                        {
                            points.Add(entry.targetTile);
                            if (entry.route != null)
                            {
                                foreach (var pt in entry.route)
                                    points.Add(pt);
                            }
                        }
                    }
                }
            }

            return points;
        }

        private static GameLocation? FindLocationFlexible(string locQuery)
        {
            var exact = Game1.getLocationFromName(locQuery);
            if (exact != null)
                return exact;

            return Game1.locations.FirstOrDefault(l =>
                string.Equals(l.NameOrUniqueName, locQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Name, locQuery, StringComparison.OrdinalIgnoreCase));
        }

        private static Point FindSafeLandingTile(GameLocation loc)
        {
            var back = loc.Map?.GetLayer("Back");
            int w = back?.LayerWidth ?? 20;
            int h = back?.LayerHeight ?? 20;
            int cx = w / 2;
            int cy = h / 2;

            for (int radius = 0; radius < Math.Max(w, h); radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1)
                            continue;
                        if (!loc.isWaterTile(x, y) && loc.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport))
                            return new Point(x, y);
                    }
                }
            }
            return new Point(cx, cy);
        }

        private JObject HandleEvalAssembly(JObject req)
        {
            string? b64 = req.Value<string>("assemblyBase64");
            if (string.IsNullOrWhiteSpace(b64))
                return new JObject { ["ok"] = false, ["error"] = "Missing assemblyBase64." };

            byte[] rawBytes = Convert.FromBase64String(b64!);
            var alc = new CollectibleEvalContext();
            try
            {
                using var ms = new MemoryStream(rawBytes);
                Assembly asm = alc.LoadFromStream(ms);
                Type? entryType = asm.GetType("SdvEvalDynamic.EvalEntry");
                MethodInfo? execMethod = entryType?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (execMethod == null)
                    return new JObject { ["ok"] = false, ["error"] = "EvalEntry.Execute not found in compiled snippet." };

                object? val = execMethod.Invoke(null, new object?[] { Helper, Monitor });
                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    MaxDepth = 5,
                    Error = (s, args) => { args.ErrorContext.Handled = true; }
                };

                JToken serialized;
                if (val == null)
                    serialized = JValue.CreateNull();
                else if (val is string || val.GetType().IsPrimitive || val is decimal)
                    serialized = JToken.FromObject(val);
                else
                    serialized = JToken.Parse(JsonConvert.SerializeObject(val, jsonSettings));

                return new JObject
                {
                    ["ok"] = true,
                    ["resultType"] = val?.GetType().FullName ?? "null",
                    ["result"] = serialized
                };
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                return new JObject
                {
                    ["ok"] = false,
                    ["error"] = tie.InnerException.Message,
                    ["exceptionType"] = tie.InnerException.GetType().FullName,
                    ["stack"] = tie.InnerException.ToString()
                };
            }
            finally
            {
                alc.Unload();
            }
        }

        private JObject HandleConsoleCommand(JObject req)
        {
            string? rawCmd = req.Value<string>("command");
            if (string.IsNullOrWhiteSpace(rawCmd))
                return new JObject { ["ok"] = false, ["error"] = "Empty console command." };

            try
            {
                Assembly smapiAsm = typeof(Mod).Assembly;
                Type? sCoreType = smapiAsm.GetType("StardewModdingAPI.Framework.SCore");
                PropertyInfo? instProp = sCoreType?.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                object? sCore = instProp?.GetValue(null);
                FieldInfo? cmdMgrField = sCoreType?.GetField("CommandManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                object? cmdMgr = cmdMgrField?.GetValue(sCore);

                if (cmdMgr != null)
                {
                    FieldInfo? rawQueueField = sCoreType?.GetField("RawCommandQueue", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    object? rawQueue = rawQueueField?.GetValue(sCore);
                    MethodInfo? addMethod = rawQueue?.GetType().GetMethod("Add", new[] { typeof(string) })
                                         ?? rawQueue?.GetType().GetMethod("Enqueue", new[] { typeof(string) });
                    if (rawQueue != null && addMethod != null)
                    {
                        addMethod.Invoke(rawQueue, new object[] { rawCmd! });
                        return new JObject { ["ok"] = true, ["queuedCommand"] = rawCmd };
                    }

                    var parts = rawCmd!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    MethodInfo? trigger = cmdMgr.GetType().GetMethod("Trigger", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (trigger != null && parts.Length > 0)
                    {
                        bool handled = (bool)(trigger.Invoke(cmdMgr, new object[] { parts[0], parts.Skip(1).ToArray() }) ?? false);
                        return new JObject { ["ok"] = handled, ["command"] = parts[0], ["handled"] = handled };
                    }
                }

                if (rawCmd!.StartsWith("debug ", StringComparison.OrdinalIgnoreCase))
                {
                    string debugSub = rawCmd.Substring(6).Trim();
                    Game1.game1.parseDebugInput(debugSub);
                    return new JObject { ["ok"] = true, ["executedDebug"] = debugSub };
                }

                return new JObject { ["ok"] = false, ["error"] = "Could not locate SMAPI CommandManager queue." };
            }
            catch (Exception ex)
            {
                return new JObject { ["ok"] = false, ["error"] = ex.Message };
            }
        }

        private JObject HandleLoadSave(JObject req)
        {
            string? saveFolder = req.Value<string>("save");
            if (string.IsNullOrWhiteSpace(saveFolder))
                return new JObject { ["ok"] = false, ["error"] = "Missing `save` folder name." };

            string savesRoot = Constants.SavesPath;
            string savePath = Path.Combine(savesRoot, saveFolder!);
            if (!Directory.Exists(savePath))
                return new JObject { ["ok"] = false, ["error"] = $"Save folder `{savePath}` does not exist." };

            if (Context.IsWorldReady)
            {
                _pendingAutoLoadSave = saveFolder;
                _autoLoadWaitTicks = 0;
                Game1.ExitToTitle();
                return new JObject { ["ok"] = true, ["message"] = $"Returning to title and auto-loading `{saveFolder}`..." };
            }

            SaveGame.Load(saveFolder!);
            Game1.exitActiveMenu();
            return new JObject { ["ok"] = true, ["message"] = $"Loading save `{saveFolder}`..." };
        }

        /// <summary>
        /// Loads or initializes the dedicated reusable test save slot `SdvDevTest_999999999`.
        /// Unlike pure saveOnNewDay=false sessions where `Constants.CurrentSavePath` is null (which crashes SVE/SpaceCore/JsonAssets),
        /// this creates a single dedicated test folder on disk so `Constants.CurrentSavePath` is always valid while never touching personal saves!
        /// </summary>
        private void LoadOrCreateDedicatedTestWorld(string farmerName, string farmName, bool forceReset)
        {
            string dedicatedDir = Path.Combine(Constants.SavesPath, DedicatedTestSaveFolder);
            string saveInfoFile = Path.Combine(dedicatedDir, "SaveGameInfo");

            if (forceReset && Directory.Exists(dedicatedDir))
            {
                try { Directory.Delete(dedicatedDir, recursive: true); } catch { }
            }

            if (!forceReset && File.Exists(saveInfoFile))
            {
                Monitor.Log($"Loading existing dedicated test save `{DedicatedTestSaveFolder}`...", LogLevel.Info);
                TitleMenu.subMenu = null;
                SaveGame.Load(DedicatedTestSaveFolder);
                Game1.exitActiveMenu();
                return;
            }

            if (Directory.Exists(dedicatedDir) && !File.Exists(saveInfoFile))
            {
                // Clean any partial empty folder so SaveGame.Save() doesn't suffix `2` onto farmName
                try { Directory.Delete(dedicatedDir, recursive: true); } catch { }
            }

            Monitor.Log($"Creating dedicated reusable test save `{DedicatedTestSaveFolder}` (`{farmerName}` / `{farmName} Farm`)...", LogLevel.Info);

            Game1.uniqueIDForThisGame = DedicatedTestUniqueId;
            Game1.player.Name = farmerName;
            Game1.player.displayName = farmerName;
            Game1.player.farmName.Value = farmName;
            Game1.player.favoriteThing.Value = "Modding";
            Game1.player.isCustomized.Value = true;
            Game1.whichFarm = Farm.default_layout;

            TitleMenu.subMenu = null;
            Game1.game1.loadForNewGame();
            Game1.uniqueIDForThisGame = DedicatedTestUniqueId;

            // saveOnNewDay = true ensures Game1.NewDay(0f) writes SaveGameInfo & save XML BEFORE SMAPI's SaveLoaded event fires!
            Game1.saveOnNewDay = true;
            Game1.player.eventsSeen.Add("60367");
            Game1.player.currentLocation = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            Game1.exitActiveMenu();
            Game1.setGameMode(3);
        }

        private static readonly FieldInfo? NewDayTaskField =
            typeof(Game1).GetField("_newDayTask", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        private void TickDayAdvanceJob()
        {
            var job = _activeDayAdvanceJob;
            if (job == null)
                return;

            try
            {
                bool taskBusy = NewDayTaskField?.GetValue(null) != null ||
                                (GameRunner.instance?.activeNewDayProcesses != null && GameRunner.instance.activeNewDayProcesses.Count > 0);

                if (job.InTransition)
                {
                    job.WaitTicks++;
                    // Dismiss any end-of-day ShippingMenu if it popped up during save
                    if (Game1.activeClickableMenu is ShippingMenu shipMenu && shipMenu.okButton != null)
                    {
                        var pt = shipMenu.okButton.bounds.Center;
                        shipMenu.receiveLeftClick(pt.X, pt.Y, playSound: false);
                    }

                    if (!taskBusy && !Game1.newDay && Context.IsWorldReady && job.WaitTicks >= 6)
                    {
                        job.InTransition = false;
                        job.RemainingDays--;
                        Game1.exitActiveMenu();
                    }
                    return;
                }

                if (job.RemainingDays <= 0)
                {
                    Game1.saveOnNewDay = job.OriginalSaveOnNewDay;
                    _activeDayAdvanceJob = null;
                    job.Tcs.TrySetResult(new JObject
                    {
                        ["ok"] = true,
                        ["advancedDays"] = job.TotalDays,
                        ["date"] = $"Year {Game1.year} {Game1.currentSeason} {Game1.dayOfMonth}",
                        ["timeOfDay"] = Game1.timeOfDay,
                        ["weather"] = Game1.netWorldState?.Value?.GetWeatherForLocation("Default")?.Weather,
                        ["applied"] = job.ChangesSummary
                    });
                    return;
                }

                if (!taskBusy && !Game1.newDay && Context.IsWorldReady && Game1.player != null)
                {
                    Game1.exitActiveMenu();
                    Game1.saveOnNewDay = job.SaveOnNewDay;
                    Game1.player.isInBed.Value = true;
                    Game1.NewDay(0f);
                    job.InTransition = true;
                    job.WaitTicks = 0;
                }
            }
            catch (Exception ex)
            {
                Game1.saveOnNewDay = job.OriginalSaveOnNewDay;
                _activeDayAdvanceJob = null;
                job.Tcs.TrySetResult(new JObject
                {
                    ["ok"] = false,
                    ["error"] = $"Day advance failed: {ex.Message}",
                    ["stack"] = ex.ToString()
                });
            }
        }

        private JObject HandleHarmonyInspect(JObject req)
        {
            string? query = req.Value<string>("query")?.Trim();
            string? modFilter = req.Value<string>("modId")?.Trim();
            bool includeIl = req.Value<bool?>("includeIl") == true;
            int limit = Math.Max(1, req.Value<int?>("limit") ?? (includeIl ? 3 : 40));

            var list = new JArray();
            int totalMatched = 0;

            var candidateMethods = Harmony.GetAllPatchedMethods().Where(m => m != null).ToList();

            string normQuery = (query ?? string.Empty).Replace("::", ".").Replace(':', '.');

            foreach (var method in candidateMethods)
            {
                string declType = method.DeclaringType?.FullName ?? "<Global>";
                string fullSig = $"{declType}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";

                if (!string.IsNullOrEmpty(normQuery) &&
                    fullSig.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase) < 0 &&
                    $"{method.DeclaringType?.Name}.{method.Name}".IndexOf(normQuery, StringComparison.OrdinalIgnoreCase) < 0 &&
                    method.Name.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Patches? info = Harmony.GetPatchInfo(method);
                if (info == null)
                    continue;

                if (!string.IsNullOrEmpty(modFilter) &&
                    !info.Owners.Any(o => o.IndexOf(modFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                totalMatched++;
                if (list.Count >= limit)
                    continue;

                var entryObj = new JObject
                {
                    ["targetMethod"] = fullSig,
                    ["owners"] = new JArray(info.Owners.OrderBy(o => o)),
                    ["prefixes"] = FormatPatchList(info.Prefixes, includeBoolReturnCheck: true),
                    ["postfixes"] = FormatPatchList(info.Postfixes, includeBoolReturnCheck: false),
                    ["transpilers"] = FormatPatchList(info.Transpilers, includeBoolReturnCheck: false),
                    ["finalizers"] = FormatPatchList(info.Finalizers, includeBoolReturnCheck: false)
                };

                if (includeIl)
                {
                    entryObj["ilInspection"] = BuildTranspilerIlReport(method, info);
                }

                list.Add(entryObj);
            }

            // If `--il` was requested on a method that has NOT been patched yet, resolve it directly from loaded assemblies!
            if (includeIl && list.Count == 0 && !string.IsNullOrWhiteSpace(query))
            {
                var unpatched = ResolveMethodFromLoadedAssemblies(query!);
                if (unpatched != null)
                {
                    string declType = unpatched.DeclaringType?.FullName ?? "<Global>";
                    string fullSig = $"{declType}.{unpatched.Name}({string.Join(", ", unpatched.GetParameters().Select(p => p.ParameterType.Name))})";
                    totalMatched = 1;
                    list.Add(new JObject
                    {
                        ["targetMethod"] = fullSig,
                        ["owners"] = new JArray(),
                        ["note"] = "Method currently has 0 active Harmony patches; showing original baseline IL.",
                        ["ilInspection"] = BuildTranspilerIlReport(unpatched, null)
                    });
                }
            }

            return new JObject
            {
                ["ok"] = true,
                ["query"] = query,
                ["modFilter"] = modFilter,
                ["includeIl"] = includeIl,
                ["totalMatched"] = totalMatched,
                ["shown"] = list.Count,
                ["patchedMethods"] = list
            };
        }

        private static JObject BuildTranspilerIlReport(MethodBase method, Patches? info)
        {
            try
            {
                var origInsns = PatchProcessor.GetOriginalInstructions(method);
                var origFormatted = origInsns.Select(FormatSingleInstruction).ToList();

                var sortedTranspilers = info?.Transpilers?
                    .OrderByDescending(x => x.priority)
                    .ThenBy(x => x.index)
                    .ToList() ?? new List<Patch>();

                if (sortedTranspilers.Count == 0)
                {
                    var linesArr = new JArray();
                    for (int i = 0; i < origFormatted.Count; i++)
                        linesArr.Add($"[{i:D4}] {origFormatted[i]}");

                    return new JObject
                    {
                        ["originalInstructionCount"] = origFormatted.Count,
                        ["currentInstructionCount"] = origFormatted.Count,
                        ["transpilerSteps"] = new JArray(),
                        ["instructions"] = linesArr
                    };
                }

                // Walk step-by-step using Harmony's native `maxTranspilers` to attribute exact instruction additions/removals per mod!
                List<string> prevLines = origFormatted;
                string?[] prevBlame = new string?[prevLines.Count];
                var stepSummaries = new JArray();

                for (int step = 1; step <= sortedTranspilers.Count; step++)
                {
                    var tp = sortedTranspilers[step - 1];
                    string ownerLabel = $"{tp.owner} ({tp.PatchMethod?.DeclaringType?.Name}.{tp.PatchMethod?.Name})";
                    var curStepInsns = PatchProcessor.GetCurrentInstructions(method, maxTranspilers: step);
                    var nextLines = curStepInsns.Select(FormatSingleInstruction).ToList();

                    var (nextBlame, addedCount, removedCount) = AttributeInstructionDeltaLcs(prevLines, prevBlame, nextLines, tp.owner);
                    stepSummaries.Add(new JObject
                    {
                        ["step"] = step,
                        ["owner"] = tp.owner,
                        ["patchMethod"] = $"{tp.PatchMethod?.DeclaringType?.FullName}.{tp.PatchMethod?.Name}",
                        ["beforeCount"] = prevLines.Count,
                        ["afterCount"] = nextLines.Count,
                        ["addedInstructions"] = addedCount,
                        ["removedInstructions"] = removedCount
                    });

                    prevLines = nextLines;
                    prevBlame = nextBlame;
                }

                var finalArr = new JArray();
                for (int i = 0; i < prevLines.Count; i++)
                {
                    string tag = prevBlame[i] != null ? $"   // + [{prevBlame[i]}]" : "";
                    finalArr.Add($"[{i:D4}] {prevLines[i]}{tag}");
                }

                return new JObject
                {
                    ["originalInstructionCount"] = origFormatted.Count,
                    ["currentInstructionCount"] = prevLines.Count,
                    ["transpilerSteps"] = stepSummaries,
                    ["instructions"] = finalArr
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["error"] = $"Failed to extract runtime IL: {ex.Message}"
                };
            }
        }

        private static string FormatSingleInstruction(CodeInstruction ci)
        {
            var sb = new StringBuilder();
            if (ci.blocks != null && ci.blocks.Count > 0)
            {
                sb.Append('{').Append(string.Join(",", ci.blocks.Select(b => b.blockType.ToString()))).Append("} ");
            }
            if (ci.labels != null && ci.labels.Count > 0)
            {
                sb.Append($"[L{string.Join(",L", ci.labels.Select(l => l.GetHashCode() & 0xFFFF))}] ");
            }
            sb.Append(ci.opcode.Name);

            if (ci.operand != null)
            {
                sb.Append(' ');
                if (ci.operand is MethodBase mb)
                {
                    sb.Append($"{mb.DeclaringType?.FullName}::{mb.Name}({string.Join(",", mb.GetParameters().Select(p => p.ParameterType.Name))})");
                }
                else if (ci.operand is FieldInfo fi)
                {
                    sb.Append($"{fi.DeclaringType?.FullName}::{fi.Name}");
                }
                else if (ci.operand is Type t)
                {
                    sb.Append(t.FullName ?? t.Name);
                }
                else if (ci.operand is string str)
                {
                    sb.Append('"').Append(str.Replace("\r", "\\r").Replace("\n", "\\n")).Append('"');
                }
                else if (ci.operand is Label lbl)
                {
                    sb.Append($"-> L{lbl.GetHashCode() & 0xFFFF}");
                }
                else if (ci.operand is LocalBuilder loc)
                {
                    sb.Append($"V_{loc.LocalIndex} ({loc.LocalType?.Name})");
                }
                else
                {
                    sb.Append(ci.operand.ToString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Strips volatile label hash prefixes when comparing instruction equivalence across two `GetCurrentInstructions` passes.
        /// </summary>
        private static string NormalizeInsnForDiff(string formatted)
        {
            int idx = 0;
            while (idx < formatted.Length && (formatted[idx] == '{' || formatted[idx] == '['))
            {
                char close = formatted[idx] == '{' ? '}' : ']';
                int end = formatted.IndexOf(close, idx);
                if (end < 0) break;
                idx = end + 1;
                while (idx < formatted.Length && formatted[idx] == ' ') idx++;
            }
            string core = formatted.Substring(idx);
            int arrow = core.IndexOf("-> L", StringComparison.Ordinal);
            if (arrow >= 0)
                core = core.Substring(0, arrow + 4);
            return core;
        }

        private static (string?[] NextBlame, int Added, int Removed) AttributeInstructionDeltaLcs(
            List<string> prev,
            string?[] prevBlame,
            List<string> next,
            string owner)
        {
            int n = prev.Count;
            int m = next.Count;
            var nextBlame = new string?[m];

            if (n * m > 400_000)
            {
                // Fallback for gigantic >600-instruction methods
                var prevSet = new HashSet<string>(prev.Select(NormalizeInsnForDiff), StringComparer.Ordinal);
                int addedFast = 0;
                for (int j = 0; j < m; j++)
                {
                    if (!prevSet.Contains(NormalizeInsnForDiff(next[j])))
                    {
                        nextBlame[j] = owner;
                        addedFast++;
                    }
                }
                return (nextBlame, addedFast, Math.Max(0, n - (m - addedFast)));
            }

            string[] normPrev = prev.Select(NormalizeInsnForDiff).ToArray();
            string[] normNext = next.Select(NormalizeInsnForDiff).ToArray();

            int[,] dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    dp[i, j] = normPrev[i] == normNext[j]
                        ? 1 + dp[i + 1, j + 1]
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            int pi = 0, nj = 0, matched = 0;
            while (pi < n && nj < m)
            {
                if (normPrev[pi] == normNext[nj])
                {
                    nextBlame[nj] = prevBlame[pi];
                    matched++;
                    pi++;
                    nj++;
                }
                else if (dp[pi + 1, nj] >= dp[pi, nj + 1])
                {
                    pi++;
                }
                else
                {
                    nextBlame[nj] = owner;
                    nj++;
                }
            }
            while (nj < m)
            {
                nextBlame[nj] = owner;
                nj++;
            }

            return (nextBlame, m - matched, n - matched);
        }

        private static MethodBase? ResolveMethodFromLoadedAssemblies(string query)
        {
            string norm = query.Trim().Replace("::", ".").Replace(':', '.');
            int paren = norm.IndexOf('(');
            if (paren > 0)
                norm = norm.Substring(0, paren);

            int dot = norm.LastIndexOf('.');
            if (dot <= 0 || dot >= norm.Length - 1)
                return null;

            string typePart = norm.Substring(0, dot);
            string memberPart = norm.Substring(dot + 1);
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;
                string asmName = asm.GetName().Name ?? "";
                if (asmName.StartsWith("System", StringComparison.Ordinal) || asmName.StartsWith("Microsoft.", StringComparison.Ordinal))
                    continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(t => t != null).ToArray()!; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    string fullName = (t.FullName ?? t.Name).Replace('+', '.');
                    if (fullName.Equals(typePart, StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Equals(typePart, StringComparison.OrdinalIgnoreCase) ||
                        fullName.EndsWith("." + typePart, StringComparison.OrdinalIgnoreCase))
                    {
                        var method = t.GetMethods(flags).FirstOrDefault(m => m.Name.Equals(memberPart, StringComparison.OrdinalIgnoreCase));
                        if (method != null)
                            return method;
                    }
                }
            }

            return null;
        }

        private JObject HandleInteract(JObject req)
        {
            if (!Context.IsWorldReady || Game1.player == null || Game1.currentLocation == null)
                return new JObject { ["ok"] = false, ["error"] = "World interaction requires `inWorld=true`." };

            Farmer who = Game1.player;
            GameLocation loc = Game1.currentLocation;
            string mode = req.Value<string>("mode")?.ToLowerInvariant() ?? "right_click";
            int? rx = req.Value<int?>("x");
            int? ry = req.Value<int?>("y");

            // Default to the tile directly in front of the player if x,y omitted!
            Point targetPt;
            if (rx.HasValue && ry.HasValue)
            {
                targetPt = new Point(rx.Value, ry.Value);
            }
            else
            {
                Vector2 grab = who.GetGrabTile();
                targetPt = new Point((int)grab.X, (int)grab.Y);
            }

            int x = targetPt.X;
            int y = targetPt.Y;
            Vector2 tileVec = new Vector2(x, y);
            bool moveAdjacent = req.Value<bool?>("moveAdjacent") ?? true;

            if (moveAdjacent && (Math.Abs(who.TilePoint.X - x) > 1 || Math.Abs(who.TilePoint.Y - y) > 1 || (who.TilePoint.X == x && who.TilePoint.Y == y)))
            {
                PositionPlayerAdjacentToTile(loc, who, x, y);
            }
            else
            {
                FacePlayerTowardTile(who, x, y);
            }

            switch (mode)
            {
                case "place":
                {
                    string? itemSpec = req.Value<string>("item")?.Trim();
                    if (string.IsNullOrWhiteSpace(itemSpec))
                        return new JObject { ["ok"] = false, ["error"] = "Missing `--place <ItemId>`." };

                    var (itemId, count, quality) = ParseItemSpec(itemSpec!);
                    Item? created = ItemRegistry.Create(itemId, count, quality, allowNull: true);
                    if (created == null)
                        return new JObject { ["ok"] = false, ["error"] = $"Could not resolve item `{itemId}` via `ItemRegistry.Create`." };

                    if (!(created is SObject sObj))
                        return new JObject { ["ok"] = false, ["error"] = $"Item `{created.QualifiedItemId}` ({created.GetType().Name}) is not a placeable `StardewValley.Object`." };

                    bool placed = sObj.placementAction(loc, x * 64, y * 64, who);
                    loc.objects.TryGetValue(tileVec, out var placedObj);

                    return new JObject
                    {
                        ["ok"] = placed,
                        ["mode"] = "place",
                        ["placed"] = placed,
                        ["location"] = loc.NameOrUniqueName,
                        ["tile"] = new JObject { ["x"] = x, ["y"] = y },
                        ["objectAtTile"] = SummarizeWorldObject(placedObj)
                    };
                }

                case "drop_in":
                case "drop-in":
                {
                    string? itemSpec = req.Value<string>("item")?.Trim();
                    if (string.IsNullOrWhiteSpace(itemSpec))
                        return new JObject { ["ok"] = false, ["error"] = "Missing `--drop-in <ItemId[:count[:quality]]>`." };

                    var (itemId, count, quality) = ParseItemSpec(itemSpec!);
                    Item? dropItem = ItemRegistry.Create(itemId, count, quality, allowNull: true);
                    if (dropItem == null)
                        return new JObject { ["ok"] = false, ["error"] = $"Could not resolve input item `{itemId}`." };

                    // Ensure item is temporarily in Farmer's active slot if 1.6 MachineDataUtility deducts from Farmer inventory!
                    int injectedSlot = -1;
                    int prevToolIdx = who.CurrentToolIndex;
                    for (int i = 0; i < who.Items.Count; i++)
                    {
                        if (who.Items[i] == null)
                        {
                            who.Items[i] = dropItem;
                            who.CurrentToolIndex = i;
                            injectedSlot = i;
                            break;
                        }
                    }

                    bool accepted = false;
                    SObject? machineObj = null;
                    string? targetDesc = null;

                    try
                    {
                        if (loc.objects.TryGetValue(tileVec, out machineObj) && machineObj != null)
                        {
                            targetDesc = $"{machineObj.QualifiedItemId} ({machineObj.DisplayName})";
                            accepted = machineObj.performObjectDropInAction(dropItem, probe: false, who);
                        }
                        else
                        {
                            var bld = loc.getBuildingAt(tileVec);
                            if (bld != null)
                            {
                                targetDesc = $"Building:{bld.buildingType.Value}";
                                accepted = bld.performActiveObjectDropInAction(who, probe: false);
                            }
                            else
                            {
                                return new JObject { ["ok"] = false, ["error"] = $"No machine object or building found at ({x},{y}) in `{loc.NameOrUniqueName}`." };
                            }
                        }
                    }
                    finally
                    {
                        // Clean up temporary slot if unconsumed or restore tool index
                        if (injectedSlot >= 0)
                        {
                            if (!accepted || (who.Items[injectedSlot] != null && who.Items[injectedSlot].Stack <= 0))
                                who.Items[injectedSlot] = null;
                            else if (accepted && ReferenceEquals(who.Items[injectedSlot], dropItem) && dropItem.Stack == count)
                            {
                                // Legacy C# machine that didn't consume from who.Items directly: clean temporary slot
                                who.Items[injectedSlot] = null;
                            }
                        }
                        who.CurrentToolIndex = prevToolIdx;
                    }

                    return new JObject
                    {
                        ["ok"] = accepted,
                        ["mode"] = "drop_in",
                        ["accepted"] = accepted,
                        ["inputItem"] = $"{dropItem.QualifiedItemId}x{count}",
                        ["target"] = targetDesc,
                        ["tile"] = new JObject { ["x"] = x, ["y"] = y },
                        ["machineStateAfter"] = SummarizeWorldObject(machineObj)
                    };
                }

                case "use_tool":
                case "use-tool":
                {
                    string? toolSpec = req.Value<string>("tool")?.Trim();
                    if (string.IsNullOrWhiteSpace(toolSpec))
                        return new JObject { ["ok"] = false, ["error"] = "Missing `--use-tool <Hoe|Pickaxe|Axe|WateringCan|Scythe|ItemId>`." };

                    int power = Math.Max(1, req.Value<int?>("power") ?? 1);
                    Tool? tool = ResolveOrCreateTool(who, toolSpec!);
                    if (tool == null)
                        return new JObject { ["ok"] = false, ["error"] = $"Could not resolve tool `{toolSpec}`." };

                    if (tool is WateringCan wc && wc.WaterLeft <= 0)
                        wc.WaterLeft = wc.waterCanMax;

                    int px = x * 64 + 32;
                    int py = y * 64 + 32;
                    who.lastClick = new Vector2(px, py);

                    if (tool is MeleeWeapon weapon && !weapon.isScythe())
                    {
                        weapon.DoDamage(loc, x * 64, y * 64, who.FacingDirection, power, who);
                    }
                    else
                    {
                        tool.DoFunction(loc, px, py, power, who);
                    }

                    loc.objects.TryGetValue(tileVec, out var objAfter);
                    loc.terrainFeatures.TryGetValue(tileVec, out var tfAfter);
                    string? tfDesc = tfAfter != null
                        ? (tfAfter is HoeDirt hd ? $"HoeDirt(state={hd.state.Value}, crop={hd.crop?.indexOfHarvest.Value})" : tfAfter.GetType().Name)
                        : null;

                    return new JObject
                    {
                        ["ok"] = true,
                        ["mode"] = "use_tool",
                        ["tool"] = tool.Name,
                        ["location"] = loc.NameOrUniqueName,
                        ["tile"] = new JObject { ["x"] = x, ["y"] = y },
                        ["terrainFeatureAfter"] = tfDesc,
                        ["objectAfter"] = SummarizeWorldObject(objAfter)
                    };
                }

                case "right_click":
                case "right-click":
                case "action":
                default:
                {
                    var beforeCounts = SnapshotInventoryCounts(who);
                    loc.objects.TryGetValue(tileVec, out var objBefore);

                    var xLoc = new xTile.Dimensions.Location(x, y);
                    bool handled = loc.checkAction(xLoc, Game1.viewport, who);
                    if (!handled && objBefore != null)
                    {
                        handled = objBefore.checkForAction(who, justCheckingForActivity: false);
                    }

                    var afterCounts = SnapshotInventoryCounts(who);
                    var deltaArr = new JArray();
                    foreach (var kv in afterCounts)
                    {
                        beforeCounts.TryGetValue(kv.Key, out int prev);
                        if (kv.Value != prev)
                            deltaArr.Add($"{(kv.Value > prev ? "+" : "")}{kv.Value - prev} {kv.Key}");
                    }
                    foreach (var kv in beforeCounts)
                    {
                        if (!afterCounts.ContainsKey(kv.Key))
                            deltaArr.Add($"-{kv.Value} {kv.Key}");
                    }

                    loc.objects.TryGetValue(tileVec, out var objAfter);
                    IClickableMenu? menuAfter = Game1.activeClickableMenu;

                    return new JObject
                    {
                        ["ok"] = true,
                        ["mode"] = "right_click",
                        ["handled"] = handled,
                        ["location"] = loc.NameOrUniqueName,
                        ["playerTile"] = new JObject { ["x"] = who.TilePoint.X, ["y"] = who.TilePoint.Y, ["facing"] = who.FacingDirection },
                        ["targetTile"] = new JObject { ["x"] = x, ["y"] = y },
                        ["activeMenuAfter"] = menuAfter?.GetType().FullName,
                        ["dialogueAfter"] = (menuAfter as DialogueBox)?.getCurrentString(),
                        ["inventoryDelta"] = deltaArr,
                        ["objectStateAfter"] = SummarizeWorldObject(objAfter)
                    };
                }
            }
        }

        private static JObject? SummarizeWorldObject(SObject? obj)
        {
            if (obj == null)
                return null;
            return new JObject
            {
                ["qualifiedItemId"] = obj.QualifiedItemId,
                ["name"] = obj.Name,
                ["displayName"] = obj.DisplayName,
                ["readyForHarvest"] = obj.readyForHarvest.Value,
                ["minutesUntilReady"] = obj.MinutesUntilReady,
                ["heldObject"] = obj.heldObject.Value?.QualifiedItemId,
                ["heldObjectName"] = obj.heldObject.Value?.DisplayName,
                ["heldStack"] = obj.heldObject.Value?.Stack
            };
        }

        private static Dictionary<string, int> SnapshotInventoryCounts(Farmer who)
        {
            var dict = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var it in who.Items)
            {
                if (it == null) continue;
                string key = $"{it.QualifiedItemId} ({it.DisplayName})";
                dict.TryGetValue(key, out int cur);
                dict[key] = cur + it.Stack;
            }
            return dict;
        }

        private static (string ItemId, int Count, int Quality) ParseItemSpec(string spec)
        {
            var parts = spec.Split(':');
            string id = parts[0].Trim();
            int count = parts.Length >= 2 && int.TryParse(parts[1], out int c) ? Math.Max(1, c) : 1;
            int qual = parts.Length >= 3 && int.TryParse(parts[2], out int q) ? q : 0;
            return (id, count, qual);
        }

        private static Tool? ResolveOrCreateTool(Farmer who, string spec)
        {
            var fromInv = who.Items.OfType<Tool>().FirstOrDefault(t =>
                t.GetType().Name.Equals(spec, StringComparison.OrdinalIgnoreCase) ||
                t.Name.IndexOf(spec, StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.QualifiedItemId.Equals(spec, StringComparison.OrdinalIgnoreCase));
            if (fromInv != null)
                return fromInv;

            return spec.ToLowerInvariant() switch
            {
                "hoe" => new Hoe(),
                "pickaxe" or "pick" => new Pickaxe(),
                "axe" => new Axe(),
                "wateringcan" or "water" or "can" => new WateringCan(),
                "scythe" => ItemRegistry.Create("(W)47", allowNull: true) as Tool,
                "fishingrod" or "rod" => new FishingRod(),
                _ => ItemRegistry.Create(spec, allowNull: true) as Tool
            };
        }

        private static void PositionPlayerAdjacentToTile(GameLocation loc, Farmer who, int tx, int ty)
        {
            // Candidate standing tiles around (tx, ty): South facing Up(0), North facing Down(2), West facing Right(1), East facing Left(3)
            var candidates = new (int X, int Y, int Facing)[]
            {
                (tx, ty + 1, 0),
                (tx, ty - 1, 2),
                (tx - 1, ty, 1),
                (tx + 1, ty, 3)
            };

            foreach (var c in candidates)
            {
                if (!loc.isWaterTile(c.X, c.Y) && loc.isTilePassable(new xTile.Dimensions.Location(c.X, c.Y), Game1.viewport))
                {
                    who.setTileLocation(new Vector2(c.X, c.Y));
                    who.FacingDirection = c.Facing;
                    return;
                }
            }

            who.setTileLocation(new Vector2(tx, ty + 1));
            who.FacingDirection = 0;
        }

        private static void FacePlayerTowardTile(Farmer who, int tx, int ty)
        {
            int dx = tx - who.TilePoint.X;
            int dy = ty - who.TilePoint.Y;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                if (dx > 0) who.FacingDirection = 1;
                else if (dx < 0) who.FacingDirection = 3;
            }
            else
            {
                if (dy > 0) who.FacingDirection = 2;
                else if (dy < 0) who.FacingDirection = 0;
            }
        }

        private JObject? StartProfileSession(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            if (tcs == null)
                return new JObject { ["ok"] = false, ["error"] = "Async TaskCompletionSource unavailable for profiler." };
            if (_activeProfileSession != null)
                return new JObject { ["ok"] = false, ["error"] = "A profiling session is already running." };

            double seconds = Math.Max(0.5, Math.Min(30.0, req.Value<double?>("seconds") ?? 5.0));
            int top = Math.Max(1, Math.Min(50, req.Value<int?>("top") ?? 12));

            var entries = new Dictionary<MethodBase, ProfileSampleEntry>();
            Assembly bridgeAsm = typeof(ModEntry).Assembly;
            Assembly smapiAsm = typeof(Mod).Assembly;

            // 1. Discover all active SMAPI mod event handlers via ModEventsBase.EventManager (or SCore.EventManager fallback)
            try
            {
                object? evtMgr = null;
                Type? glType = Helper.Events.GameLoop.GetType();
                while (glType != null && evtMgr == null)
                {
                    evtMgr = glType.GetField("EventManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(Helper.Events.GameLoop);
                    glType = glType.BaseType;
                }

                if (evtMgr == null)
                {
                    Type? sCoreType = smapiAsm.GetType("StardewModdingAPI.Framework.SCore");
                    object? sCore = sCoreType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                    evtMgr = sCoreType?.GetField("EventManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sCore)
                          ?? sCoreType?.GetProperty("EventManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sCore);
                }

                if (evtMgr != null)
                {
                    foreach (var field in evtMgr.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        object? managedEvt = field.GetValue(evtMgr);
                        if (managedEvt == null) continue;

                        string evtName = managedEvt.GetType().GetProperty("EventName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(managedEvt) as string
                                         ?? field.Name;

                        FieldInfo? handlersField = null;
                        Type? curEvtType = managedEvt.GetType();
                        while (curEvtType != null && handlersField == null)
                        {
                            handlersField = curEvtType.GetField("Handlers", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                            curEvtType = curEvtType.BaseType;
                        }

                        if (handlersField?.GetValue(managedEvt) is IEnumerable handlersList)
                        {
                            List<object?> snapshot;
                            lock (handlersList)
                            {
                                snapshot = handlersList.Cast<object?>().ToList();
                            }

                            foreach (object? hItem in snapshot)
                            {
                                if (hItem == null) continue;
                                Type ht = hItem.GetType();
                                var del = (ht.GetProperty("Handler", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hItem)
                                        ?? ht.GetField("Handler", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hItem)) as Delegate;
                                object? srcMod = ht.GetProperty("SourceMod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hItem)
                                              ?? ht.GetField("SourceMod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hItem);
                                object? manifest = srcMod?.GetType().GetProperty("Manifest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(srcMod);
                                string modId = (manifest as IManifest)?.UniqueID ?? del?.Method.DeclaringType?.Assembly.GetName().Name ?? "Unknown";

                                if (del == null)
                                    continue;

                                foreach (var singleDel in del.GetInvocationList())
                                {
                                    MethodInfo? mInfo = singleDel.Method;
                                    if (mInfo == null || mInfo.DeclaringType == null || mInfo.IsGenericMethod || mInfo.DeclaringType.IsGenericType)
                                        continue;
                                    if (mInfo.DeclaringType.Assembly == bridgeAsm || mInfo.DeclaringType.Assembly == smapiAsm)
                                        continue;

                                    if (!entries.ContainsKey(mInfo))
                                    {
                                        entries[mInfo] = new ProfileSampleEntry
                                        {
                                            Kind = "SMAPI.Event",
                                            ModId = modId,
                                            Label = $"[{evtName}] {mInfo.DeclaringType.Name}.{mInfo.Name}"
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Profiler event discovery warning: {ex.Message}", LogLevel.Trace);
            }

            // 2. Discover all active Harmony Prefix / Postfix / Finalizer patch methods
            foreach (var patchedTarget in Harmony.GetAllPatchedMethods())
            {
                if (patchedTarget == null) continue;
                Patches? pInfo = Harmony.GetPatchInfo(patchedTarget);
                if (pInfo == null) continue;

                string targetShort = $"{patchedTarget.DeclaringType?.Name}.{patchedTarget.Name}";
                var allFixes = pInfo.Prefixes.Select(p => (Patch: p, Kind: "Prefix"))
                    .Concat(pInfo.Postfixes.Select(p => (Patch: p, Kind: "Postfix")))
                    .Concat(pInfo.Finalizers.Select(p => (Patch: p, Kind: "Finalizer")));

                foreach (var item in allFixes)
                {
                    MethodInfo? pm = item.Patch.PatchMethod;
                    if (pm == null || pm.DeclaringType == null || pm.IsGenericMethod || pm.DeclaringType.IsGenericType)
                        continue;
                    if (pm.DeclaringType.Assembly == bridgeAsm || item.Patch.owner == PendingProfileSession.HarmonyOwnerId)
                        continue;

                    if (!entries.ContainsKey(pm))
                    {
                        entries[pm] = new ProfileSampleEntry
                        {
                            Kind = $"Harmony.{item.Kind}",
                            ModId = item.Patch.owner,
                            Label = $"{targetShort} -> {pm.DeclaringType.Name}.{pm.Name}"
                        };
                    }
                }
            }

            TemporaryProfilerHooks.Entries = entries;
            var session = new PendingProfileSession(seconds, top, tcs);

            var prefixHm = new HarmonyMethod(typeof(TemporaryProfilerHooks).GetMethod(nameof(TemporaryProfilerHooks.SamplePrefix), BindingFlags.Public | BindingFlags.Static));
            var postfixHm = new HarmonyMethod(typeof(TemporaryProfilerHooks).GetMethod(nameof(TemporaryProfilerHooks.SamplePostfix), BindingFlags.Public | BindingFlags.Static));

            int hooked = 0;
            foreach (var m in entries.Keys.ToList())
            {
                try
                {
                    session.TempHarmony.Patch(m, prefix: prefixHm, postfix: postfixHm);
                    hooked++;
                }
                catch
                {
                    // Skip methods that cannot be dynamically wrapped (e.g. tiny inlined/marshal stubs)
                }
            }

            session.HookedMethodCount = hooked;
            session.WallClock.Restart();
            session.LastRenderedTimestamp = Stopwatch.GetTimestamp();
            TemporaryProfilerHooks.Active = true;
            _activeProfileSession = session;

            return null; // Completed asynchronously once TargetSeconds elapses!
        }

        private void CompleteProfileSession()
        {
            var session = _activeProfileSession;
            if (session == null)
                return;

            _activeProfileSession = null;
            TemporaryProfilerHooks.Active = false;
            double elapsedSec = Math.Max(0.001, session.WallClock.Elapsed.TotalSeconds);

            try
            {
                session.TempHarmony.UnpatchAll(PendingProfileSession.HarmonyOwnerId);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Profiler unpatch warning: {ex.Message}", LogLevel.Trace);
            }

            var capturedEntries = TemporaryProfilerHooks.Entries.Values.ToList();
            TemporaryProfilerHooks.Entries = new Dictionary<MethodBase, ProfileSampleEntry>();

            double tickToMs = 1000.0 / Stopwatch.Frequency;
            long allocDelta = Math.Max(0L, GC.GetTotalAllocatedBytes(precise: false) - session.StartAllocBytes);
            int gen0Delta = GC.CollectionCount(0) - session.StartGen0;
            int gen1Delta = GC.CollectionCount(1) - session.StartGen1;
            int gen2Delta = GC.CollectionCount(2) - session.StartGen2;

            double avgFrameMs = 0, p95FrameMs = 0, p99FrameMs = 0, maxFrameMs = 0, avgFps = 0, onePctLowFps = 0;
            int frameCount = session.FrameIntervalTicks.Count;
            if (frameCount > 0)
            {
                var sortedMs = session.FrameIntervalTicks.Select(t => t * tickToMs).OrderBy(x => x).ToList();
                avgFrameMs = sortedMs.Average();
                p95FrameMs = sortedMs[Math.Min(sortedMs.Count - 1, (int)(sortedMs.Count * 0.95))];
                p99FrameMs = sortedMs[Math.Min(sortedMs.Count - 1, (int)(sortedMs.Count * 0.99))];
                maxFrameMs = sortedMs[sortedMs.Count - 1];
                avgFps = avgFrameMs > 0 ? 1000.0 / avgFrameMs : 0;
                onePctLowFps = p99FrameMs > 0 ? 1000.0 / p99FrameMs : 0;
            }

            var hotspots = new JArray(
                capturedEntries
                    .Where(e => e.Calls > 0)
                    .OrderByDescending(e => e.TotalTicks)
                    .Take(session.TopCount)
                    .Select(e =>
                    {
                        double totalMs = e.TotalTicks * tickToMs;
                        return new JObject
                        {
                            ["kind"] = e.Kind,
                            ["modId"] = e.ModId,
                            ["callback"] = e.Label,
                            ["calls"] = e.Calls,
                            ["totalMs"] = Math.Round(totalMs, 3),
                            ["avgMsPerCall"] = Math.Round(totalMs / e.Calls, 4),
                            ["maxSpikeMs"] = Math.Round(e.MaxTicks * tickToMs, 3)
                        };
                    }));

            var byMod = new JArray(
                capturedEntries
                    .Where(e => e.Calls > 0)
                    .GroupBy(e => e.ModId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new
                    {
                        ModId = g.Key,
                        TotalMs = g.Sum(x => x.TotalTicks) * tickToMs,
                        TotalCalls = g.Sum(x => x.Calls),
                        MaxSpikeMs = g.Max(x => x.MaxTicks) * tickToMs
                    })
                    .OrderByDescending(x => x.TotalMs)
                    .Take(session.TopCount)
                    .Select(x => new JObject
                    {
                        ["modId"] = x.ModId,
                        ["totalMs"] = Math.Round(x.TotalMs, 3),
                        ["msPerSecond"] = Math.Round(x.TotalMs / elapsedSec, 3),
                        ["calls"] = x.TotalCalls,
                        ["maxSpikeMs"] = Math.Round(x.MaxSpikeMs, 3)
                    }));

            session.Tcs.TrySetResult(new JObject
            {
                ["ok"] = true,
                ["durationSeconds"] = Math.Round(elapsedSec, 2),
                ["instrumentedCallbacks"] = session.HookedMethodCount,
                ["unpatchedCleanly"] = true,
                ["frames"] = new JObject
                {
                    ["renderedFrames"] = frameCount,
                    ["avgFps"] = Math.Round(avgFps, 1),
                    ["onePercentLowFps"] = Math.Round(onePctLowFps, 1),
                    ["avgFrameMs"] = Math.Round(avgFrameMs, 2),
                    ["p95FrameMs"] = Math.Round(p95FrameMs, 2),
                    ["p99FrameMs"] = Math.Round(p99FrameMs, 2),
                    ["maxFrameMs"] = Math.Round(maxFrameMs, 2)
                },
                ["gc"] = new JObject
                {
                    ["allocatedMb"] = Math.Round(allocDelta / (1024.0 * 1024.0), 2),
                    ["allocMbPerSec"] = Math.Round((allocDelta / (1024.0 * 1024.0)) / elapsedSec, 2),
                    ["gen0"] = gen0Delta,
                    ["gen1"] = gen1Delta,
                    ["gen2"] = gen2Delta
                },
                ["topModTotals"] = byMod,
                ["topHotspots"] = hotspots
            });
        }

        private static JArray FormatPatchList(IEnumerable<Patch>? patches, bool includeBoolReturnCheck)
        {
            var arr = new JArray();
            if (patches == null)
                return arr;

            foreach (var p in patches.OrderByDescending(x => x.priority).ThenBy(x => x.index))
            {
                var obj = new JObject
                {
                    ["owner"] = p.owner,
                    ["priority"] = p.priority,
                    ["patchMethod"] = $"{p.PatchMethod?.DeclaringType?.FullName}.{p.PatchMethod?.Name}"
                };
                if (includeBoolReturnCheck && p.PatchMethod != null)
                {
                    obj["canSkipOriginal"] = p.PatchMethod.ReturnType == typeof(bool);
                }
                if (p.before != null && p.before.Length > 0)
                    obj["before"] = new JArray(p.before);
                if (p.after != null && p.after.Length > 0)
                    obj["after"] = new JArray(p.after);
                arr.Add(obj);
            }
            return arr;
        }

        private JObject HandleAssetInspect(JObject req)
        {
            string? target = req.Value<string>("target")?.Trim();
            if (string.IsNullOrWhiteSpace(target))
                return new JObject { ["ok"] = false, ["error"] = "Missing `target` asset path (e.g. `Data/Objects`, `Maps/Town`)." };

            string cleanTarget = target!.Replace('\\', '/');
            string? keyFilter = req.Value<string>("key")?.Trim();
            string? exportPath = req.Value<string>("exportPath")?.Trim();
            bool triggerCpSummary = req.Value<bool?>("cpTrace") == true;
            int limit = Math.Max(1, req.Value<int?>("limit") ?? 10);

            if (triggerCpSummary && Helper.ModRegistry.IsLoaded("Pathoschild.ContentPatcher"))
            {
                HandleConsoleCommand(new JObject { ["command"] = $"patch summary asset \"{cleanTarget}\"" });
            }

            var jsonSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                MaxDepth = 8,
                Formatting = Formatting.Indented,
                Error = (s, a) => { a.ErrorContext.Handled = true; }
            };

            // 1. Try Map (`Maps/*`)
            if (cleanTarget.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var map = Helper.GameContent.Load<xTile.Map>(cleanTarget);
                    var layers = new JArray(map.Layers.Select(l => new JObject
                    {
                        ["id"] = l.Id,
                        ["width"] = l.LayerWidth,
                        ["height"] = l.LayerHeight
                    }));
                    var sheets = new JArray(map.TileSheets.Select(ts => new JObject
                    {
                        ["id"] = ts.Id,
                        ["imageSource"] = ts.ImageSource,
                        ["sheetWidth"] = ts.SheetWidth,
                        ["sheetHeight"] = ts.SheetHeight,
                        ["tileCount"] = ts.TileCount
                    }));
                    return new JObject
                    {
                        ["ok"] = true,
                        ["target"] = cleanTarget,
                        ["assetType"] = "xTile.Map",
                        ["layers"] = layers,
                        ["tileSheets"] = sheets
                    };
                }
                catch
                {
                    // Might be a Texture2D inside Maps/ (e.g. Maps/springobjects)! Fall through to Texture2D check.
                }
            }

            // 2. Try Texture2D (`Portraits/*`, `Characters/*`, `TileSheets/*`, `LooseSprites/*`, `Maps/*tilesheet*`, etc.)
            bool likelyImage = cleanTarget.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("TileSheets/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("LooseSprites/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("Animals/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("Buildings/", StringComparison.OrdinalIgnoreCase) ||
                               cleanTarget.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase) ||
                               (exportPath != null && exportPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

            if (likelyImage && !cleanTarget.StartsWith("Characters/Dialogue", StringComparison.OrdinalIgnoreCase) &&
                !cleanTarget.StartsWith("Characters/schedules", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tex = Helper.GameContent.Load<Texture2D>(cleanTarget);
                    string? writtenPng = null;
                    if (!string.IsNullOrWhiteSpace(exportPath))
                    {
                        writtenPng = Path.GetFullPath(exportPath!);
                        Directory.CreateDirectory(Path.GetDirectoryName(writtenPng)!);
                        using var fs = new FileStream(writtenPng, FileMode.Create, FileAccess.Write, FileShare.Read);
                        tex.SaveAsPng(fs, tex.Width, tex.Height);
                    }
                    return new JObject
                    {
                        ["ok"] = true,
                        ["target"] = cleanTarget,
                        ["assetType"] = "Texture2D",
                        ["width"] = tex.Width,
                        ["height"] = tex.Height,
                        ["exportedPng"] = writtenPng
                    };
                }
                catch
                {
                    // Fall through to generic data load
                }
            }

            // 3. Load Data / Dictionary / List asset
            object loaded = Helper.GameContent.Load<object>(cleanTarget);
            string runtimeType = loaded.GetType().FullName ?? loaded.GetType().Name;

            if (!string.IsNullOrWhiteSpace(exportPath))
            {
                string fullExp = Path.GetFullPath(exportPath!);
                Directory.CreateDirectory(Path.GetDirectoryName(fullExp)!);
                File.WriteAllText(fullExp, JsonConvert.SerializeObject(loaded, jsonSettings), new UTF8Encoding(false));
            }

            if (loaded is IDictionary dict)
            {
                var matched = new JObject();
                int totalCount = dict.Count;

                if (!string.IsNullOrEmpty(keyFilter))
                {
                    // Exact match first, then substring match
                    foreach (DictionaryEntry de in dict)
                    {
                        string k = de.Key?.ToString() ?? "";
                        if (k.Equals(keyFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            matched[k] = JToken.Parse(JsonConvert.SerializeObject(de.Value, jsonSettings));
                        }
                    }

                    if (matched.Count == 0)
                    {
                        foreach (DictionaryEntry de in dict)
                        {
                            string k = de.Key?.ToString() ?? "";
                            string rawVal = JsonConvert.SerializeObject(de.Value, jsonSettings);
                            if (k.IndexOf(keyFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                rawVal.IndexOf(keyFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matched[k] = JToken.Parse(rawVal);
                                if (matched.Count >= limit)
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (DictionaryEntry de in dict)
                    {
                        string k = de.Key?.ToString() ?? "";
                        matched[k] = JToken.Parse(JsonConvert.SerializeObject(de.Value, jsonSettings));
                        if (matched.Count >= limit)
                            break;
                    }
                }

                return new JObject
                {
                    ["ok"] = true,
                    ["target"] = cleanTarget,
                    ["assetType"] = runtimeType,
                    ["totalEntries"] = totalCount,
                    ["keyFilter"] = keyFilter,
                    ["matchedCount"] = matched.Count,
                    ["exportedTo"] = string.IsNullOrWhiteSpace(exportPath) ? null : Path.GetFullPath(exportPath!),
                    ["entries"] = matched
                };
            }

            string rawJson = JsonConvert.SerializeObject(loaded, jsonSettings);
            return new JObject
            {
                ["ok"] = true,
                ["target"] = cleanTarget,
                ["assetType"] = runtimeType,
                ["exportedTo"] = string.IsNullOrWhiteSpace(exportPath) ? null : Path.GetFullPath(exportPath!),
                ["data"] = JToken.Parse(rawJson)
            };
        }

        private JObject HandleEventPlay(JObject req)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return new JObject { ["ok"] = false, ["error"] = "Event sandbox requires `inWorld=true`." };

            string mode = req.Value<string>("mode")?.ToLowerInvariant() ?? "play";

            if (mode == "status")
            {
                var ev = Game1.CurrentEvent;
                if (ev == null)
                    return new JObject { ["ok"] = true, ["eventActive"] = false, ["eventUp"] = Game1.eventUp };

                var actors = new JArray();
                if (ev.actors != null)
                {
                    foreach (var a in ev.actors)
                    {
                        actors.Add(new JObject
                        {
                            ["name"] = a.Name,
                            ["tileX"] = a.TilePoint.X,
                            ["tileY"] = a.TilePoint.Y,
                            ["facing"] = a.FacingDirection
                        });
                    }
                }

                return new JObject
                {
                    ["ok"] = true,
                    ["eventActive"] = true,
                    ["id"] = ev.id,
                    ["isFestival"] = ev.isFestival,
                    ["currentCommandIndex"] = ev.CurrentCommand,
                    ["totalCommands"] = ev.eventCommands?.Length ?? 0,
                    ["currentCommand"] = ev.GetCurrentCommand(),
                    ["skippable"] = ev.skippable,
                    ["skipped"] = ev.skipped,
                    ["activeDialogue"] = (Game1.activeClickableMenu as DialogueBox)?.getCurrentString(),
                    ["actors"] = actors
                };
            }

            if (mode == "skip")
            {
                var ev = Game1.CurrentEvent;
                if (ev == null)
                    return new JObject { ["ok"] = true, ["message"] = "No active event to skip." };

                Game1.exitActiveMenu();
                if (ev.skippable)
                    ev.skipEvent();
                else
                    ev.exitEvent();

                return new JObject { ["ok"] = true, ["skippedEventId"] = ev.id };
            }

            if (mode == "step")
            {
                if (Game1.activeClickableMenu is DialogueBox db)
                {
                    db.receiveLeftClick(db.xPositionOnScreen + 40, db.yPositionOnScreen + 40, playSound: true);
                    return new JObject { ["ok"] = true, ["stepped"] = "dialogue_click", ["currentCommand"] = Game1.CurrentEvent?.GetCurrentCommand() };
                }

                var ev = Game1.CurrentEvent;
                if (ev == null)
                    return new JObject { ["ok"] = false, ["error"] = "No active event to step." };

                ev.CurrentCommand++;
                return new JObject
                {
                    ["ok"] = true,
                    ["stepped"] = "increment_command",
                    ["currentCommandIndex"] = ev.CurrentCommand,
                    ["currentCommand"] = ev.GetCurrentCommand()
                };
            }

            string locName = req.Value<string>("location") ?? Game1.currentLocation?.NameOrUniqueName ?? "Town";
            GameLocation? loc = FindLocationFlexible(locName);
            if (loc == null)
                return new JObject { ["ok"] = false, ["error"] = $"Location `{locName}` not found." };

            string? rawScript = req.Value<string>("script");
            string? eventId = req.Value<string>("eventId");
            bool keepSeen = req.Value<bool?>("keepSeen") == true;

            if (string.IsNullOrWhiteSpace(rawScript))
            {
                if (string.IsNullOrWhiteSpace(eventId))
                    return new JObject { ["ok"] = false, ["error"] = "Provide either `eventId` or raw `script`." };

                string assetPath = $"Data/Events/{loc.Name}";
                Dictionary<string, string> eventsDict;
                try
                {
                    eventsDict = Helper.GameContent.Load<Dictionary<string, string>>(assetPath);
                }
                catch (Exception ex)
                {
                    return new JObject { ["ok"] = false, ["error"] = $"Could not load `{assetPath}`: {ex.Message}" };
                }

                string? matchedKey = eventsDict.Keys.FirstOrDefault(k =>
                    k.Equals(eventId, StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith(eventId + "/", StringComparison.OrdinalIgnoreCase));

                if (matchedKey == null)
                {
                    return new JObject
                    {
                        ["ok"] = false,
                        ["error"] = $"Event ID `{eventId}` not found in `{assetPath}` (available count: {eventsDict.Count})."
                    };
                }

                rawScript = eventsDict[matchedKey];
                if (!keepSeen)
                {
                    Game1.player.eventsSeen.Remove(eventId!);
                }
            }

            // Exit any prior event/menu cleanly first
            Game1.exitActiveMenu();
            if (Game1.CurrentEvent != null)
            {
                Game1.CurrentEvent.exitEvent();
                Game1.eventUp = false;
            }

            if (Game1.currentLocation != loc)
            {
                Point safe = FindSafeLandingTile(loc);
                Game1.player.currentLocation = loc;
                Game1.currentLocation = loc;
                Game1.player.setTileLocation(new Vector2(safe.X, safe.Y));
                loc.resetForPlayerEntry();
            }

            string effectiveId = string.IsNullOrWhiteSpace(eventId) ? "sdv_dev_preview" : eventId!;
            var newEvt = new Event(rawScript!, $"Data/Events/{loc.Name}", effectiveId, Game1.player);
            loc.startEvent(newEvt);

            return new JObject
            {
                ["ok"] = true,
                ["location"] = loc.NameOrUniqueName,
                ["eventId"] = effectiveId,
                ["commandCount"] = newEvt.eventCommands?.Length ?? 0,
                ["firstCommand"] = newEvt.GetCurrentCommand()
            };
        }

        private JObject? HandleWorldControl(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return new JObject { ["ok"] = false, ["error"] = "World control requires `inWorld=true`." };

            var applied = new JArray();

            // 1. Season / Day / Year
            bool dateMutated = false;
            string? seasonStr = req.Value<string>("season")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(seasonStr))
            {
                if (Enum.TryParse<Season>(seasonStr, ignoreCase: true, out var parsedSeason))
                {
                    Game1.season = parsedSeason;
                    Game1.setGraphicsForSeason();
                    dateMutated = true;
                    applied.Add($"season={Game1.currentSeason}");
                }
                else
                {
                    return new JObject { ["ok"] = false, ["error"] = $"Invalid season `{seasonStr}` (expected spring, summer, fall, winter)." };
                }
            }

            int? dayVal = req.Value<int?>("day");
            if (dayVal.HasValue)
            {
                Game1.dayOfMonth = Math.Max(1, Math.Min(28, dayVal.Value));
                dateMutated = true;
                applied.Add($"day={Game1.dayOfMonth}");
            }

            int? yearVal = req.Value<int?>("year");
            if (yearVal.HasValue && yearVal.Value >= 1)
            {
                Game1.year = yearVal.Value;
                dateMutated = true;
                applied.Add($"year={Game1.year}");
            }

            // 【当初为什么要在这里同步刷新节日与天气图标状态】：
            // 当存档原本停在节日当天（如春 13 复活节、秋 16 展览会）时，`Game1.weatherIcon` 会被设为 `1`（代表右上角时钟显示节日小旗）。
            // 如果此时用 `world --day 9 --time ...` 直接跳到非节日日期却没调 `Game1.updateWeatherIcon()`，
            // 下方 `performTenMinuteClockUpdate()` 看到残留的 `weatherIcon == 1` 就会去硬读不存在的 `Data\Festivals\spring9.xnb` 崩溃报错！
            if (dateMutated)
            {
                Game1.whereIsTodaysFest = null;
                try { Game1.UpdatePassiveFestivalStates(); } catch { }
                Game1.updateWeatherIcon();
            }

            // 2. Weather (1.6 LocationWeather + ApplyWeatherForNewDay) — 先于 `--time` 执行，确保时钟刷新时已处于目标天气与图标状态
            string? weatherRaw = req.Value<string>("weather")?.Trim();
            if (!string.IsNullOrEmpty(weatherRaw))
            {
                string wId = weatherRaw!.ToLowerInvariant() switch
                {
                    "sun" or "sunny" or "clear" => "Sun",
                    "rain" or "rainy" => "Rain",
                    "storm" or "lightning" or "thunder" => "Storm",
                    "wind" or "debris" or "windy" => "Wind",
                    "snow" or "snowy" => "Snow",
                    "green_rain" or "greenrain" => "GreenRain",
                    _ => weatherRaw
                };

                string ctxId = Game1.currentLocation?.GetLocationContextId() ?? "Default";
                foreach (var targetCtx in new[] { "Default", ctxId }.Distinct())
                {
                    var lw = Game1.netWorldState.Value.GetWeatherForLocation(targetCtx);
                    lw.WeatherForTomorrow = wId;
                    lw.InitializeDayWeather();
                }
                Game1.ApplyWeatherForNewDay();
                Game1.updateWeatherIcon();
                applied.Add($"weather={wId}");
            }

            // 3. Time of day
            int? timeVal = req.Value<int?>("time");
            if (timeVal.HasValue)
            {
                // 二次兜底：若此前外部代码把 weatherIcon 留在了 1（节日）但当天并非节日，立即纠正图标避免加载假节日 XNB
                if (Game1.weatherIcon == 1 && !Utility.isFestivalDay())
                {
                    Game1.whereIsTodaysFest = null;
                    Game1.updateWeatherIcon();
                }

                int clamped = Math.Max(600, Math.Min(2600, timeVal.Value));
                Game1.timeOfDay = Utility.ModifyTime(clamped, -10);
                Game1.performTenMinuteClockUpdate();
                Game1.timeOfDay = clamped;
                Game1.gameTimeInterval = 0;
                applied.Add($"time={Game1.timeOfDay}");
            }

            // 4. Money / Stamina / Health
            int? moneyVal = req.Value<int?>("money");
            if (moneyVal.HasValue)
            {
                Game1.player.Money = Math.Max(0, moneyVal.Value);
                applied.Add($"money={Game1.player.Money}");
            }

            if (req.Value<bool?>("refill") == true)
            {
                Game1.player.Stamina = Game1.player.MaxStamina;
                Game1.player.health = Game1.player.maxHealth;
                applied.Add("refilledStaminaAndHealth");
            }

            // 5. Friendship (`NPC:Points`, e.g. `Abigail:1000`)
            string? friendSpec = req.Value<string>("friendship");
            if (!string.IsNullOrWhiteSpace(friendSpec))
            {
                var parts = friendSpec!.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int pts))
                {
                    string npcName = parts[0].Trim();
                    if (!Game1.player.friendshipData.TryGetValue(npcName, out var fData))
                    {
                        fData = new Friendship(pts);
                        Game1.player.friendshipData[npcName] = fData;
                    }
                    else
                    {
                        fData.Points = pts;
                    }
                    applied.Add($"friendship[{npcName}]={pts}");
                }
            }

            // 6. Add Item via 1.6 ItemRegistry.Create (`(O)74:5:4` or `74`)
            string? itemSpec = req.Value<string>("addItem");
            if (!string.IsNullOrWhiteSpace(itemSpec))
            {
                var parts = itemSpec!.Split(':');
                string itemId = parts[0].Trim();
                int count = parts.Length >= 2 && int.TryParse(parts[1], out int ic) ? Math.Max(1, ic) : 1;
                int quality = parts.Length >= 3 && int.TryParse(parts[2], out int iq) ? iq : 0;

                Item? created = ItemRegistry.Create(itemId, count, quality, allowNull: true);
                if (created == null)
                    return new JObject { ["ok"] = false, ["error"] = $"Item `{itemId}` could not be resolved by `ItemRegistry.Create`." };

                Game1.player.addItemToInventoryBool(created);
                applied.Add($"addedItem={created.QualifiedItemId}x{count}(q={quality})");
            }

            // 7. Mailbox injection
            string? mailId = req.Value<string>("mail")?.Trim();
            if (!string.IsNullOrEmpty(mailId))
            {
                if (!Game1.player.mailbox.Contains(mailId!))
                    Game1.player.mailbox.Add(mailId!);
                applied.Add($"mailbox+={mailId}");
            }

            // 7.5 Camera Zoom (`--zoom <0.35..3.0>`)
            // 【当初为什么要原生封装 `--zoom`】：
            // 以往手写 `eval` 只改 `Game1.options.desiredBaseZoomLevel = 0.8f` 却没调用 `SetWindowSize` 重建 `Game1.screen` 离屏缓冲，
            // 会导致 `Game1.viewport` 与 `Game1.screen` 尺寸失配，在画面右侧 `uv.x > 0.8` 处撕裂出一条假竖线，误导着色器排查！
            float? zoomReq = req.Value<float?>("zoom");
            if (zoomReq.HasValue && zoomReq.Value > 0.05f)
            {
                ApplyCameraZoomAndRebuildScreen(zoomReq.Value);
                applied.Add($"zoom={Game1.options.zoomLevel:0.##} (screen={Game1.game1.screen?.Width}x{Game1.game1.screen?.Height})");
            }

            // 8. Multi-day overnight pipeline (`--advance-days N`)
            int advDays = req.Value<int?>("advanceDays") ?? 0;
            if (advDays > 0 && tcs != null)
            {
                if (_activeDayAdvanceJob != null)
                    return new JObject { ["ok"] = false, ["error"] = "Another day-advance operation is already in progress." };

                bool saveFlag = req.Value<bool?>("save") == true;
                _activeDayAdvanceJob = new PendingDayAdvanceJob(
                    Math.Min(56, advDays),
                    saveFlag,
                    Game1.saveOnNewDay,
                    applied,
                    tcs);
                return null; // Completed asynchronously across UpdateTicked frames!
            }

            return new JObject
            {
                ["ok"] = true,
                ["date"] = $"Year {Game1.year} {Game1.currentSeason} {Game1.dayOfMonth}",
                ["timeOfDay"] = Game1.timeOfDay,
                ["weather"] = Game1.netWorldState?.Value?.GetWeatherForLocation("Default")?.Weather,
                ["zoom"] = Math.Round(Game1.options.zoomLevel, 3),
                ["viewport"] = $"{Game1.viewport.Width}x{Game1.viewport.Height}",
                ["screenBuffer"] = Game1.game1?.screen != null ? $"{Game1.game1.screen.Width}x{Game1.game1.screen.Height}" : null,
                ["money"] = Game1.player.Money,
                ["applied"] = applied
            };
        }

        private JObject HandleMultiplayerControl(JObject req)
        {
            var runner = GameRunner.instance;
            if (runner == null || runner.gameInstances == null)
                return new JObject { ["ok"] = false, ["error"] = "GameRunner.instance is not available." };

            string sub = req.Value<string>("sub")?.ToLowerInvariant() ?? "status";

            if (sub == "add_farmhand" || sub == "add-farmhand")
            {
                if (!Context.IsWorldReady)
                    return new JObject { ["ok"] = false, ["error"] = "Load into a world on screen 0 first before adding a split-screen farmhand." };
                if (runner.gameInstances.Count >= 4)
                    return new JObject { ["ok"] = false, ["error"] = "Maximum of 4 local split-screen instances already active." };

                // Ensure a cabin exists on Farm if needed so farmhand can join seamlessly!
                bool cabinEnsured = EnsureCabinOnFarmIfNeeded();
                PlayerIndex nextIdx = (PlayerIndex)runner.gameInstances.Count;
                runner.AddGameInstance(nextIdx);

                return new JObject
                {
                    ["ok"] = true,
                    ["action"] = "add_farmhand",
                    ["newScreenIndex"] = runner.gameInstances.Count - 1,
                    ["screenCount"] = runner.gameInstances.Count,
                    ["cabinEnsured"] = cabinEnsured,
                    ["tip"] = "Use `--screen 1` (or `--screen farmhand`) with `ui`, `ui-click`, `player`, `warp`, or `eval` to inspect/control the split-screen farmhand."
                };
            }

            if (sub == "remove_farmhand" || sub == "remove-farmhand")
            {
                int targetIdx = req.Value<int?>("targetScreen") ?? (runner.gameInstances.Count - 1);
                if (targetIdx <= 0 || targetIdx >= runner.gameInstances.Count)
                    return new JObject { ["ok"] = false, ["error"] = $"Invalid farmhand screen index `{targetIdx}` (host screen 0 cannot be removed)." };

                var inst = runner.gameInstances[targetIdx];
                runner.RemoveGameInstance(inst);
                return new JObject
                {
                    ["ok"] = true,
                    ["removedScreenIndex"] = targetIdx,
                    ["remainingScreens"] = runner.gameInstances.Count
                };
            }

            // Default: list all screens
            var screens = new JArray();
            Game1? savedCurrent = Game1.game1;
            if (savedCurrent != null)
                GameRunner.SaveInstance(savedCurrent);

            try
            {
                for (int i = 0; i < runner.gameInstances.Count; i++)
                {
                    var inst = runner.gameInstances[i];
                    GameRunner.LoadInstance(inst);
                    screens.Add(new JObject
                    {
                        ["screenIndex"] = i,
                        ["role"] = i == 0 ? "host" : "farmhand",
                        ["instanceId"] = inst.instanceId,
                        ["inWorld"] = Context.IsWorldReady,
                        ["isMainPlayer"] = Context.IsMainPlayer,
                        ["playerName"] = Game1.player?.Name,
                        ["uniqueMultiplayerId"] = Game1.player?.UniqueMultiplayerID.ToString(),
                        ["location"] = Game1.currentLocation?.NameOrUniqueName,
                        ["tile"] = Game1.player != null ? $"{Game1.player.TilePoint.X},{Game1.player.TilePoint.Y}" : null,
                        ["activeMenu"] = Game1.activeClickableMenu?.GetType().FullName
                    });
                    GameRunner.SaveInstance(inst);
                }
            }
            finally
            {
                if (savedCurrent != null)
                    GameRunner.LoadInstance(savedCurrent);
            }

            return new JObject
            {
                ["ok"] = true,
                ["isMultiplayer"] = Context.IsMultiplayer,
                ["isSplitScreen"] = runner.gameInstances.Count > 1,
                ["screenCount"] = runner.gameInstances.Count,
                ["screens"] = screens
            };
        }

        private static bool EnsureCabinOnFarmIfNeeded()
        {
            var farm = Game1.getFarm();
            if (farm == null)
                return false;

            bool hasCabin = farm.buildings.Any(b => b.isCabin);
            if (hasCabin)
                return false;

            try
            {
                var cabin = StardewValley.Buildings.Building.CreateInstanceFromId("Cabin", new Vector2(58, 14));
                if (cabin != null)
                {
                    cabin.FinishConstruction();
                    farm.buildings.Add(cabin);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string GetCheckpointsRoot()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dir = Path.Combine(home, ".local", "share", "sdv-dev", "checkpoints");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string SanitizeCheckpointName(string raw)
        {
            string trimmed = (raw ?? "quick").Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(c, '_');
            return string.IsNullOrWhiteSpace(trimmed) ? "quick" : trimmed;
        }

        private JObject? HandleCheckpoint(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            string op = req.Value<string>("op")?.ToLowerInvariant() ?? "list";
            string rawName = req.Value<string>("name") ?? "quick";
            string name = SanitizeCheckpointName(rawName);
            bool force = req.Value<bool?>("force") == true;
            string root = GetCheckpointsRoot();
            string cpDir = Path.Combine(root, name);

            if (op == "save")
            {
                if (!Context.IsWorldReady || Game1.player == null || string.IsNullOrEmpty(Constants.SaveFolderName) || string.IsNullOrEmpty(Constants.CurrentSavePath))
                {
                    return new JObject { ["ok"] = false, ["error"] = "Cannot save checkpoint when `inWorld=false`." };
                }

                string saveFolder = Constants.SaveFolderName!;
                string savePath = Constants.CurrentSavePath!;

                // Drain vanilla SaveGame.getSaveEnumerator() synchronously on main thread (~80-150ms) to flush all live world/machine/inventory state!
                var prevCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                    IEnumerator<int> saveEnum = SaveGame.getSaveEnumerator();
                    while (saveEnum.MoveNext())
                    {
                    }
                }
                finally
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = prevCulture;
                    SaveGame.IsProcessing = false;
                }

                if (Directory.Exists(cpDir))
                    Directory.Delete(cpDir, recursive: true);
                Directory.CreateDirectory(cpDir);

                string filesDir = Path.Combine(cpDir, "savefiles");
                Directory.CreateDirectory(filesDir);
                foreach (var f in Directory.GetFiles(savePath))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.EndsWith("_old", StringComparison.OrdinalIgnoreCase) || fn.StartsWith("SaveGameInfo_old", StringComparison.OrdinalIgnoreCase))
                        continue;
                    File.Copy(f, Path.Combine(filesDir, fn), overwrite: true);
                }

                var meta = new JObject
                {
                    ["name"] = name,
                    ["createdAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["saveFolderName"] = saveFolder,
                    ["year"] = Game1.year,
                    ["season"] = Game1.currentSeason,
                    ["dayOfMonth"] = Game1.dayOfMonth,
                    ["date"] = $"Year {Game1.year} {Game1.currentSeason} {Game1.dayOfMonth}",
                    ["timeOfDay"] = Game1.timeOfDay,
                    ["location"] = Game1.currentLocation?.NameOrUniqueName ?? "Farm",
                    ["tileX"] = Game1.player.TilePoint.X,
                    ["tileY"] = Game1.player.TilePoint.Y,
                    ["facing"] = Game1.player.FacingDirection,
                    ["money"] = Game1.player.Money,
                    ["stamina"] = Game1.player.Stamina,
                    ["health"] = Game1.player.health
                };
                File.WriteAllText(Path.Combine(cpDir, "checkpoint-meta.json"), meta.ToString(Formatting.Indented), Encoding.UTF8);

                return new JObject
                {
                    ["ok"] = true,
                    ["op"] = "save",
                    ["checkpoint"] = meta
                };
            }
            else if (op == "load")
            {
                string metaFile = Path.Combine(cpDir, "checkpoint-meta.json");
                string filesDir = Path.Combine(cpDir, "savefiles");
                if (!File.Exists(metaFile) || !Directory.Exists(filesDir))
                {
                    return new JObject { ["ok"] = false, ["error"] = $"Checkpoint `{name}` does not exist. Run `sdv-dev checkpoint list`." };
                }

                var meta = JObject.Parse(File.ReadAllText(metaFile, Encoding.UTF8));
                string targetSaveFolder = meta.Value<string>("saveFolderName") ?? DedicatedTestSaveFolder;
                if (!force && !targetSaveFolder.StartsWith("SdvDevTest_", StringComparison.OrdinalIgnoreCase))
                {
                    return new JObject
                    {
                        ["ok"] = false,
                        ["error"] = $"Checkpoint `{name}` targets non-test save `{targetSaveFolder}`. Pass `--force` if authorized to overwrite that save slot."
                    };
                }

                string savesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");
                string dstSaveDir = Path.Combine(savesRoot, targetSaveFolder);
                Directory.CreateDirectory(dstSaveDir);

                foreach (var f in Directory.GetFiles(filesDir))
                {
                    File.Copy(f, Path.Combine(dstSaveDir, Path.GetFileName(f)), overwrite: true);
                }

                _pendingCheckpointRestore = new PendingCheckpointRestore
                {
                    Name = name,
                    Location = meta.Value<string>("location") ?? "Farm",
                    TileX = meta.Value<int?>("tileX") ?? 64,
                    TileY = meta.Value<int?>("tileY") ?? 15,
                    Facing = meta.Value<int?>("facing") ?? 2,
                    TimeOfDay = meta.Value<int?>("timeOfDay") ?? 600,
                    Stamina = meta.Value<float?>("stamina") ?? 270f,
                    Health = meta.Value<int?>("health") ?? 100,
                    Tcs = tcs
                };

                Game1.exitActiveMenu();
                SaveGame.Load(targetSaveFolder);
                return tcs != null ? null : new JObject { ["ok"] = true, ["op"] = "load", ["restoring"] = name };
            }

            return new JObject { ["ok"] = false, ["error"] = $"Unsupported checkpoint op `{op}`." };
        }

        private void TickCheckpointRestore()
        {
            var job = _pendingCheckpointRestore;
            if (job == null)
                return;

            job.WaitTicks++;
            if (!Context.IsWorldReady || Game1.player == null || Game1.currentLocation == null)
            {
                if (job.WaitTicks > 600)
                {
                    _pendingCheckpointRestore = null;
                    job.Tcs?.TrySetResult(new JObject { ["ok"] = false, ["error"] = $"Timed out restoring checkpoint `{job.Name}`." });
                }
                return;
            }

            if (!job.WarpIssued)
            {
                job.WarpIssued = true;
                Game1.timeOfDay = job.TimeOfDay;
                Game1.player.Stamina = job.Stamina;
                Game1.player.health = job.Health;

                if (!string.Equals(Game1.currentLocation.NameOrUniqueName, job.Location, StringComparison.OrdinalIgnoreCase) ||
                    Game1.player.TilePoint.X != job.TileX || Game1.player.TilePoint.Y != job.TileY)
                {
                    Game1.warpFarmer(job.Location, job.TileX, job.TileY, job.Facing);
                    if (Game1.isWarping || Game1.locationRequest != null)
                    {
                        Game1.fadeToBlackAlpha = 1.1f;
                        Game1.fadeToBlack = true;
                        Game1.nonWarpFade = false;
                        return;
                    }
                }
                else
                {
                    Game1.player.faceDirection(job.Facing);
                }
            }

            if ((Game1.isWarping || Game1.locationRequest != null) && job.WaitTicks < 660)
                return;

            _pendingCheckpointRestore = null;
            Game1.fadeToBlack = false;
            Game1.fadeToBlackAlpha = 0f;
            Game1.timeOfDay = job.TimeOfDay;
            Game1.gameTimeInterval = 0;
            Game1.player.faceDirection(job.Facing);

            job.Tcs?.TrySetResult(new JObject
            {
                ["ok"] = true,
                ["op"] = "load",
                ["checkpoint"] = job.Name,
                ["date"] = $"Year {Game1.year} {Game1.currentSeason} {Game1.dayOfMonth}",
                ["timeOfDay"] = Game1.timeOfDay,
                ["location"] = Game1.currentLocation?.NameOrUniqueName,
                ["tileX"] = Game1.player.TilePoint.X,
                ["tileY"] = Game1.player.TilePoint.Y,
                ["facing"] = Game1.player.FacingDirection,
                ["money"] = Game1.player.Money
            });
        }

        private JObject HandleScheduleCheck(JObject req)
        {
            if (!Context.IsWorldReady)
                return new JObject { ["ok"] = false, ["error"] = "Live `schedule-check` requires `inWorld=true`." };

            string? npcQuery = req.Value<string>("npc");
            string? keyFilter = req.Value<string>("key");
            if (string.IsNullOrWhiteSpace(npcQuery))
                return new JObject { ["ok"] = false, ["error"] = "Specify NPC name (e.g. `sdv-dev schedule-check Abigail`)." };

            NPC? npc = Utility.fuzzyCharacterSearch(npcQuery!, must_be_villager: false);
            if (npc == null)
            {
                foreach (var loc in Game1.locations)
                {
                    npc = loc.characters.FirstOrDefault(c => c != null && c.Name.Equals(npcQuery, StringComparison.OrdinalIgnoreCase));
                    if (npc != null) break;
                }
            }

            if (npc == null)
                return new JObject { ["ok"] = false, ["error"] = $"NPC `{npcQuery}` not found in loaded world." };

            var rawSchedules = npc.getMasterScheduleRawData();
            if (rawSchedules == null || rawSchedules.Count == 0)
            {
                return new JObject
                {
                    ["ok"] = true,
                    ["npc"] = npc.Name,
                    ["scheduleKeyCount"] = 0,
                    ["note"] = $"NPC `{npc.Name}` has no master schedule entries in `Characters/schedules/{npc.Name}`."
                };
            }

            var results = new JArray();
            int brokenCount = 0;

            foreach (var kv in rawSchedules)
            {
                if (!string.IsNullOrWhiteSpace(keyFilter) && !kv.Key.Equals(keyFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<int, SchedulePathDescription>? parsed = null;
                string? parseErr = null;
                try
                {
                    parsed = npc.parseMasterSchedule(kv.Key, kv.Value);
                }
                catch (Exception ex)
                {
                    parseErr = ex.Message;
                }

                var legsArr = new JArray();
                if (parsed != null)
                {
                    foreach (var leg in parsed.OrderBy(p => p.Key))
                    {
                        var desc = leg.Value;
                        int stepCount = desc?.route?.Count ?? 0;
                        Point? finalStep = stepCount > 0 ? desc!.route.Last() : (Point?)null;
                        bool reachedExact = finalStep.HasValue && desc != null && finalStep.Value == desc.targetTile;
                        if (stepCount == 0)
                            brokenCount++;

                        legsArr.Add(new JObject
                        {
                            ["time"] = leg.Key,
                            ["targetLocation"] = desc?.targetLocationName,
                            ["targetTile"] = desc != null ? $"{desc.targetTile.X},{desc.targetTile.Y}" : null,
                            ["facing"] = desc?.facingDirection,
                            ["animation"] = string.IsNullOrEmpty(desc?.endOfRouteBehavior) ? null : desc!.endOfRouteBehavior,
                            ["routeSteps"] = stepCount,
                            ["reachedTargetTile"] = reachedExact,
                            ["status"] = stepCount == 0 ? "UNREACHABLE_OR_EMPTY_ROUTE" : (reachedExact ? "OK" : "PARTIAL_PATH")
                        });
                    }
                }
                else if (parseErr != null)
                {
                    brokenCount++;
                }

                results.Add(new JObject
                {
                    ["scheduleKey"] = kv.Key,
                    ["rawScript"] = kv.Value,
                    ["parseError"] = parseErr,
                    ["legCount"] = legsArr.Count,
                    ["legs"] = legsArr
                });
            }

            return new JObject
            {
                ["ok"] = brokenCount == 0,
                ["npc"] = npc.Name,
                ["defaultMap"] = npc.DefaultMap,
                ["defaultTile"] = $"{(int)(npc.DefaultPosition.X / 64f)},{(int)(npc.DefaultPosition.Y / 64f)}",
                ["brokenLegCount"] = brokenCount,
                ["schedules"] = results
            };
        }
    }
}
