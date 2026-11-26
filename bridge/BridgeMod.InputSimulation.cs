using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SdvDevBridge
{
    public sealed partial class ModEntry
    {
        #region 5. 无焦点真实键鼠 / 蓄力 / 悬停 Tooltip / 手柄菜单焦点跳转模拟引擎 (`sdv-dev input`)

        /// <summary>
        /// 单步输入动作：支持同时按住一组 `SButton`（键盘、鼠标键或手柄方向/肩键/ABXY）持续 `DurationTicks` 帧，
        /// 可选移动虚拟光标或滚动滚轮。
        /// </summary>
        internal sealed class SimulatedInputStep
        {
            public List<SButton> Buttons { get; } = new List<SButton>();
            public int DurationTicks { get; set; } = 1;
            public int ReleaseGapTicks { get; set; }
            public Point? MoveUiOrViewportPos { get; set; }
            public Point? AimWorldTile { get; set; }
            public int ScrollWheelDelta { get; set; }
        }

        internal sealed class PendingInputSimulation
        {
            public List<SimulatedInputStep> Steps { get; } = new List<SimulatedInputStep>();
            public int CurrentStepIndex { get; set; }
            public int TickInCurrentPhase { get; set; }
            public bool InReleaseGapPhase { get; set; }
            public bool InSettlePhase { get; set; }
            public int RequestedSettleTicks { get; set; } = -1; // -1 = auto (18 if tool charged, else 2)
            public int SettleTicksRemaining { get; set; }
            public int TotalTicksExecuted { get; set; }
            public string? OutScreenshotPath { get; set; }
            public bool WaitingForFinalRender { get; set; }
            public TaskCompletionSource<JObject>? Tcs { get; set; }

            // 虚拟鼠标与滚轮状态
            public int VirtualRawMouseX { get; set; }
            public int VirtualRawMouseY { get; set; }
            public int CumulativeScrollWheel { get; set; }
            public Point? ActiveUiOrViewportPos { get; set; }
            public Point? ActiveAimWorldTile { get; set; }

            // 上一帧按下的按钮集合（用于在松开帧显式向 SInputState 发送 setDown: false）
            public HashSet<SButton> PreviouslyDownButtons { get; } = new HashSet<SButton>();
            public List<string> InjectedTransitions { get; } = new List<string>();

            // 手柄模式备份（测试手柄焦点跳转时临时强制开启 gamepadMode，结束后无损恢复）
            public bool TouchedGamepadMode { get; set; }
            public Options.GamepadModes OrigGamepadMode { get; set; }
            public bool OrigGamepadControls { get; set; }

            // 实机手感与 UI 遥测数据
            public string LocationBefore { get; set; } = string.Empty;
            public Point TileBefore { get; set; }
            public Vector2 PixelBefore { get; set; }
            public int FacingBefore { get; set; }
            public int MaxToolPowerSeen { get; set; }
            public bool SawUsingTool { get; set; }
            public string? MenuBefore { get; set; }
            public string? SnappedBefore { get; set; }
            public List<string> SnapTrail { get; } = new List<string>();
            public JObject? PeakHoverSnapshot { get; set; }
            public string? PeakSnappedAfter { get; set; }
            public string? PeakMenuAfter { get; set; }
            public JObject? ScreenshotResult { get; set; }
        }

        internal static PendingInputSimulation? ActiveInputSimInstance;
        private static bool _inputHarmonyPatched;
        private static Harmony? _inputHarmony;
        private static MethodInfo? _sInputOverrideButtonMethod;
        private static FieldInfo? _fCurrentKeyboardState;
        private static FieldInfo? _fCurrentMouseState;
        private static FieldInfo? _fCurrentGamepadState;
        private static FieldInfo? _fLastKeyStateTick;

        /// <summary>
        /// 惰性挂载无焦点输入模拟所需的两个超轻量 Harmony Postfix（平时 `ActiveInputSimInstance == null` 时仅一次空指针判断，0 开销）。
        ///
        /// 【当初为什么要这样设计而不是调 Win32 `SendInput`】：
        /// 1. Win32 `SendInput` 必须强行把游戏窗口置顶 (`SetForegroundWindow`) 并抢走玩家真实的物理鼠标和键盘，没法后台跑；
        /// 2. 反编译 SMAPI 的 `SCore` 与原版 `Game1._update` 发现，当窗口失焦或处于 `--background` 离屏状态时：
        ///    - `SCore` 第 729 行 `if (Game.IsActive) input.TrueUpdate();` 会直接跳过整个 SMAPI 输入更新；
        ///    - `Game1._update` 第 288 / 572 行也会因 `base.IsActive == false` 跳过菜单悬停/手柄导航与玩家移动蓄力。
        /// 3. 因此仅在 `ActiveInputSimInstance != null` 的几十个模拟 Tick 期间：
        ///    - 让 `Game.IsActive` 局部返回 `true`；
        ///    - 在每帧开头的 `InputState.UpdateStates` 末尾隔离物理键鼠，写入虚拟鼠标坐标并调用 `SInputState.OverrideButton`；
        ///    即可在不碰桌面焦点的前提下，100% 跑通原版 WASD 物理移动、农具蓄力、自定义菜单悬停 Tooltip 与手柄 `neighborID` 焦点跳转！
        /// </summary>
        private void EnsureInputSimulationHooks()
        {
            if (_inputHarmonyPatched)
                return;

            const BindingFlags AllInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _fCurrentKeyboardState = typeof(InputState).GetField("_currentKeyboardState", AllInst);
            _fCurrentMouseState = typeof(InputState).GetField("_currentMouseState", AllInst);
            _fCurrentGamepadState = typeof(InputState).GetField("_currentGamepadState", AllInst);
            _fLastKeyStateTick = typeof(InputState).GetField("_lastKeyStateTick", AllInst);

            if (Game1.input != null)
            {
                _sInputOverrideButtonMethod = Game1.input.GetType().GetMethod(
                    "OverrideButton",
                    AllInst,
                    null,
                    new[] { typeof(SButton), typeof(bool) },
                    null);
            }

            _inputHarmony = new Harmony("SdvDev.TestBridge.InputSim");

            MethodInfo? isActiveGetter = typeof(Game).GetProperty("IsActive", AllInst)?.GetMethod;
            if (isActiveGetter != null)
            {
                _inputHarmony.Patch(
                    original: isActiveGetter,
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameIsActivePostfix)));
            }

            MethodInfo? updateStatesMethod = typeof(InputState).GetMethod("UpdateStates", AllInst);
            if (updateStatesMethod != null)
            {
                _inputHarmony.Patch(
                    original: updateStatesMethod,
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(InputStateUpdateStatesPostfix)));
            }

            _inputHarmonyPatched = true;
        }

        private static void GameIsActivePostfix(ref bool __result)
        {
            if (ActiveInputSimInstance != null)
                __result = true;
        }

        private static void InputStateUpdateStatesPostfix(InputState __instance)
        {
            var sim = ActiveInputSimInstance;
            if (sim == null || !ReferenceEquals(__instance, Game1.input))
                return;

            try
            {
                InjectFrameInputBeforeTrueUpdate(sim, __instance);
            }
            catch
            {
            }
        }

        private JObject? StartInputSimulation(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            if (ActiveInputSimInstance != null)
                return new JObject { ["ok"] = false, ["error"] = "Another input simulation is currently running." };

            EnsureInputSimulationHooks();
            if (_sInputOverrideButtonMethod == null && Game1.input != null)
            {
                _sInputOverrideButtonMethod = Game1.input.GetType().GetMethod(
                    "OverrideButton",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(SButton), typeof(bool) },
                    null);
            }

            var sim = new PendingInputSimulation
            {
                Tcs = tcs,
                RequestedSettleTicks = req.Value<int?>("settleTicks") ?? -1,
                OutScreenshotPath = req.Value<string>("outPath")
            };

            // 初始化当前虚拟鼠标坐标
            MouseState curMouse = Game1.input != null ? Game1.input.GetMouseState() : Mouse.GetState();
            sim.VirtualRawMouseX = curMouse.X;
            sim.VirtualRawMouseY = curMouse.Y;
            sim.CumulativeScrollWheel = curMouse.ScrollWheelValue;

            Point? initialMove = ParseJsonPoint(req["move"]);
            Point? initialAimTile = ParseJsonPoint(req["aimTile"]);
            int scrollDelta = req.Value<int?>("scroll") ?? 0;
            string? clickKind = req.Value<string>("click")?.Trim().ToLowerInvariant();
            int defaultTicks = Math.Clamp(req.Value<int?>("ticks") ?? 1, 1, 600);
            int intervalTicks = Math.Clamp(req.Value<int?>("intervalTicks") ?? 3, 1, 120);

            if (initialMove.HasValue)
                sim.ActiveUiOrViewportPos = initialMove.Value;
            if (initialAimTile.HasValue)
                sim.ActiveAimWorldTile = initialAimTile.Value;

            bool hasGamepadButtons = false;

            // 1. 解析 `--hold "W+LeftShift"`（长按持续 N ticks，测走动碰撞或农具蓄力）
            string? holdRaw = req.Value<string>("hold");
            if (!string.IsNullOrWhiteSpace(holdRaw))
            {
                if (!TryParseButtonCombo(holdRaw!, out var holdBtns, out string? parseErr))
                    return new JObject { ["ok"] = false, ["error"] = parseErr };

                if (holdBtns.Any(IsGamepadSButton))
                    hasGamepadButtons = true;

                var step = new SimulatedInputStep
                {
                    DurationTicks = Math.Max(1, req.Value<int?>("ticks") ?? 20),
                    ReleaseGapTicks = 1,
                    MoveUiOrViewportPos = initialMove,
                    AimWorldTile = initialAimTile,
                    ScrollWheelDelta = scrollDelta
                };
                step.Buttons.AddRange(holdBtns);
                sim.Steps.Add(step);
                scrollDelta = 0;
            }

            // 2. 解析按键/手柄序列 `--sequence`（支持逗号分隔如 `DPadDown,DPadRight,ControllerA` 或带帧长 `W:20,D:15`）
            if (req["sequence"] is JArray seqArr && seqArr.Count > 0)
            {
                bool firstSeq = sim.Steps.Count == 0;
                foreach (var tok in seqArr)
                {
                    string itemStr = tok?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(itemStr))
                        continue;

                    int stepHoldTicks = 2;
                    int colon = itemStr.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(itemStr.Substring(colon + 1), out int explicitHold) && explicitHold > 0)
                    {
                        stepHoldTicks = Math.Clamp(explicitHold, 1, 600);
                        itemStr = itemStr.Substring(0, colon);
                    }

                    if (!TryParseButtonCombo(itemStr, out var seqBtns, out string? seqErr))
                        return new JObject { ["ok"] = false, ["error"] = seqErr };

                    if (seqBtns.Any(IsGamepadSButton))
                        hasGamepadButtons = true;

                    var step = new SimulatedInputStep
                    {
                        DurationTicks = stepHoldTicks,
                        ReleaseGapTicks = intervalTicks,
                        MoveUiOrViewportPos = firstSeq ? initialMove : null,
                        AimWorldTile = firstSeq ? initialAimTile : null,
                        ScrollWheelDelta = firstSeq ? scrollDelta : 0
                    };
                    step.Buttons.AddRange(seqBtns);
                    sim.Steps.Add(step);
                    firstSeq = false;
                }
                scrollDelta = 0;
            }

            // 3. 解析鼠标悬停 + 可选点击/滚轮（例如 `--move 360,220 --ticks 4` 或 `--move 360,220 --click left`）
            if (sim.Steps.Count == 0 && (initialMove.HasValue || initialAimTile.HasValue || scrollDelta != 0 || !string.IsNullOrEmpty(clickKind)))
            {
                if (!string.IsNullOrEmpty(clickKind))
                {
                    // 先悬停 2 ticks 让 performHoverAction 激活按钮高亮，再按住鼠标键 2 ticks 后释放
                    sim.Steps.Add(new SimulatedInputStep
                    {
                        DurationTicks = Math.Max(2, req.Value<int?>("ticks") ?? 2),
                        ReleaseGapTicks = 0,
                        MoveUiOrViewportPos = initialMove,
                        AimWorldTile = initialAimTile,
                        ScrollWheelDelta = scrollDelta
                    });

                    var clickStep = new SimulatedInputStep
                    {
                        DurationTicks = 2,
                        ReleaseGapTicks = 2,
                        MoveUiOrViewportPos = initialMove,
                        AimWorldTile = initialAimTile
                    };
                    clickStep.Buttons.Add(clickKind == "right" ? SButton.MouseRight : SButton.MouseLeft);
                    sim.Steps.Add(clickStep);
                }
                else
                {
                    sim.Steps.Add(new SimulatedInputStep
                    {
                        DurationTicks = Math.Max(2, defaultTicks),
                        ReleaseGapTicks = 0,
                        MoveUiOrViewportPos = initialMove,
                        AimWorldTile = initialAimTile,
                        ScrollWheelDelta = scrollDelta
                    });
                }
            }
            else if (!string.IsNullOrEmpty(clickKind))
            {
                var clickStep = new SimulatedInputStep
                {
                    DurationTicks = 2,
                    ReleaseGapTicks = 2,
                    MoveUiOrViewportPos = initialMove,
                    AimWorldTile = initialAimTile
                };
                clickStep.Buttons.Add(clickKind == "right" ? SButton.MouseRight : SButton.MouseLeft);
                sim.Steps.Add(clickStep);
            }

            if (sim.Steps.Count == 0)
            {
                return new JObject
                {
                    ["ok"] = false,
                    ["error"] = "Specify at least one input action (`--hold`, `--press`/`--keys`/`--gamepad`, `--move`, `--aim-tile`, `--click`, or `--scroll`)."
                };
            }

            // 记录初始角色与菜单状态
            if (Context.IsWorldReady && Game1.player != null)
            {
                sim.LocationBefore = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
                sim.TileBefore = Game1.player.TilePoint;
                sim.PixelBefore = Game1.player.Position;
                sim.FacingBefore = Game1.player.FacingDirection;
            }

            sim.MenuBefore = DescribeActiveMenuType();
            sim.SnappedBefore = FormatSnappedComponent(GetActiveMenuOrSubPage()?.currentlySnappedComponent);
            if (sim.SnappedBefore != null)
                sim.SnapTrail.Add(sim.SnappedBefore);

            // 若包含手柄按键，临时启用手柄控制并在菜单未聚焦组件时触发初始 snapToDefaultClickableComponent
            if (hasGamepadButtons && Game1.options != null)
            {
                sim.TouchedGamepadMode = true;
                sim.OrigGamepadMode = Game1.options.gamepadMode;
                sim.OrigGamepadControls = Game1.options.gamepadControls;
                Game1.options.gamepadMode = Options.GamepadModes.ForceOn;
                Game1.options.gamepadControls = true;
                if (Game1.playerOneIndex < PlayerIndex.One)
                    Game1.playerOneIndex = PlayerIndex.One;

                var page = GetActiveMenuOrSubPage();
                if (page != null && page.currentlySnappedComponent == null)
                {
                    try { page.snapToDefaultClickableComponent(); } catch { }
                    string? snappedInit = FormatSnappedComponent(page.currentlySnappedComponent);
                    if (snappedInit != null && !sim.SnapTrail.Contains(snappedInit))
                        sim.SnapTrail.Add(snappedInit);
                }
            }

            ActiveInputSimInstance = sim;
            return tcs != null ? null : new JObject { ["ok"] = true, ["started"] = true };
        }

        private static void InjectFrameInputBeforeTrueUpdate(PendingInputSimulation sim, InputState inputInst)
        {
            if (sim.WaitingForFinalRender)
                return;

            var desiredDown = new HashSet<SButton>();
            int stepWheel = 0;

            if (!sim.InSettlePhase && sim.CurrentStepIndex < sim.Steps.Count)
            {
                var step = sim.Steps[sim.CurrentStepIndex];
                if (step.MoveUiOrViewportPos.HasValue)
                    sim.ActiveUiOrViewportPos = step.MoveUiOrViewportPos.Value;
                if (step.AimWorldTile.HasValue)
                    sim.ActiveAimWorldTile = step.AimWorldTile.Value;

                if (!sim.InReleaseGapPhase)
                {
                    foreach (var b in step.Buttons)
                        desiredDown.Add(b);

                    if (sim.TickInCurrentPhase == 0 && step.ScrollWheelDelta != 0)
                        stepWheel = step.ScrollWheelDelta;
                }
            }

            // 计算对应的底层像素坐标（自动适配 UI 缩放系数 uiScale 与世界缩放系数 zoomLevel）
            if (sim.ActiveAimWorldTile.HasValue && Context.IsWorldReady && Game1.options != null)
            {
                Point tile = sim.ActiveAimWorldTile.Value;
                float worldViewPxX = tile.X * 64f + 32f - Game1.viewport.X;
                float worldViewPxY = tile.Y * 64f + 32f - Game1.viewport.Y;
                sim.VirtualRawMouseX = (int)Math.Round(worldViewPxX * Game1.options.zoomLevel);
                sim.VirtualRawMouseY = (int)Math.Round(worldViewPxY * Game1.options.zoomLevel);
                Game1.mouseCursorTransparency = 1f;
            }
            else if (sim.ActiveUiOrViewportPos.HasValue && Game1.options != null)
            {
                Point pt = sim.ActiveUiOrViewportPos.Value;
                bool useUiScale = Game1.activeClickableMenu != null || Game1.uiMode;
                float scale = useUiScale ? Game1.options.uiScale : Game1.options.zoomLevel;
                sim.VirtualRawMouseX = (int)Math.Round(pt.X * scale);
                sim.VirtualRawMouseY = (int)Math.Round(pt.Y * scale);
                Game1.mouseCursorTransparency = 1f;
            }

            sim.CumulativeScrollWheel += stepWheel;

            // 隔离真实物理键鼠输入，写入干净的虚拟 MouseState
            _fCurrentKeyboardState?.SetValue(inputInst, default(KeyboardState));
            _fCurrentGamepadState?.SetValue(inputInst, default(GamePadState));
            _fLastKeyStateTick?.SetValue(inputInst, -1);

            ButtonState leftState = desiredDown.Contains(SButton.MouseLeft) ? ButtonState.Pressed : ButtonState.Released;
            ButtonState rightState = desiredDown.Contains(SButton.MouseRight) ? ButtonState.Pressed : ButtonState.Released;
            ButtonState midState = desiredDown.Contains(SButton.MouseMiddle) ? ButtonState.Pressed : ButtonState.Released;

            var virtualMouse = new MouseState(
                sim.VirtualRawMouseX,
                sim.VirtualRawMouseY,
                sim.CumulativeScrollWheel,
                leftState,
                midState,
                rightState,
                ButtonState.Released,
                ButtonState.Released);
            _fCurrentMouseState?.SetValue(inputInst, virtualMouse);

            // 通过 SMAPI 官方 SInputState.OverrideButton 注入按键按下与松开（同时驱动 SMAPI ButtonPressed/Released 事件与原版状态）
            if (_sInputOverrideButtonMethod != null)
            {
                foreach (var prev in sim.PreviouslyDownButtons)
                {
                    if (!desiredDown.Contains(prev))
                    {
                        _sInputOverrideButtonMethod.Invoke(inputInst, new object[] { prev, false });
                        sim.InjectedTransitions.Add($"{prev}:Up@t{sim.TotalTicksExecuted}");
                    }
                }

                foreach (var cur in desiredDown)
                {
                    _sInputOverrideButtonMethod.Invoke(inputInst, new object[] { cur, true });
                    if (!sim.PreviouslyDownButtons.Contains(cur))
                        sim.InjectedTransitions.Add($"{cur}:Down@t{sim.TotalTicksExecuted}");
                }
            }

            sim.PreviouslyDownButtons.Clear();
            foreach (var cur in desiredDown)
                sim.PreviouslyDownButtons.Add(cur);
        }

        private void TickInputSimulation()
        {
            var sim = ActiveInputSimInstance;
            if (sim == null || sim.WaitingForFinalRender)
                return;

            sim.TotalTicksExecuted++;

            // 采集当前帧玩家蓄力/动作与菜单焦点/悬停状态
            if (Context.IsWorldReady && Game1.player != null)
            {
                int tp = Game1.player.toolPower.Value;
                if (tp > sim.MaxToolPowerSeen)
                    sim.MaxToolPowerSeen = tp;
                if (Game1.player.UsingTool)
                    sim.SawUsingTool = true;
            }

            var activePage = GetActiveMenuOrSubPage();
            string? curSnap = FormatSnappedComponent(activePage?.currentlySnappedComponent);
            if (curSnap != null && (sim.SnapTrail.Count == 0 || sim.SnapTrail[sim.SnapTrail.Count - 1] != curSnap))
            {
                sim.SnapTrail.Add(curSnap);
            }

            if (sim.InSettlePhase)
            {
                sim.SettleTicksRemaining--;
                bool toolBusy = Context.IsWorldReady && Game1.player != null && Game1.player.UsingTool;
                if (sim.SettleTicksRemaining <= 0 || (!toolBusy && sim.RequestedSettleTicks < 0 && sim.SettleTicksRemaining < 14))
                {
                    BeginFinalizeOrCaptureInputSim(sim);
                }
                return;
            }

            if (sim.CurrentStepIndex >= sim.Steps.Count)
            {
                EnterSettlePhase(sim);
                return;
            }

            var step = sim.Steps[sim.CurrentStepIndex];
            sim.TickInCurrentPhase++;

            if (!sim.InReleaseGapPhase)
            {
                if (sim.TickInCurrentPhase >= step.DurationTicks)
                {
                    // 在最后一个主动作步骤结束瞬间，趁虚拟光标/手柄仍悬停在目标控件上，立即抓取悬停 Tooltip 快照！
                    if (sim.CurrentStepIndex == sim.Steps.Count - 1)
                    {
                        sim.PeakHoverSnapshot = ExtractMenuHoverSnapshot(activePage);
                        sim.PeakSnappedAfter = curSnap;
                        sim.PeakMenuAfter = DescribeActiveMenuType();
                    }

                    sim.TickInCurrentPhase = 0;
                    if (step.ReleaseGapTicks > 0)
                    {
                        sim.InReleaseGapPhase = true;
                    }
                    else
                    {
                        sim.CurrentStepIndex++;
                        if (sim.CurrentStepIndex >= sim.Steps.Count)
                            EnterSettlePhase(sim);
                    }
                }
            }
            else
            {
                if (sim.TickInCurrentPhase >= step.ReleaseGapTicks)
                {
                    sim.InReleaseGapPhase = false;
                    sim.TickInCurrentPhase = 0;
                    sim.CurrentStepIndex++;
                    if (sim.CurrentStepIndex >= sim.Steps.Count)
                        EnterSettlePhase(sim);
                }
            }
        }

        private void EnterSettlePhase(PendingInputSimulation sim)
        {
            sim.PeakHoverSnapshot ??= ExtractMenuHoverSnapshot(GetActiveMenuOrSubPage());
            sim.PeakSnappedAfter ??= FormatSnappedComponent(GetActiveMenuOrSubPage()?.currentlySnappedComponent);
            sim.PeakMenuAfter ??= DescribeActiveMenuType();

            int autoSettle = (sim.MaxToolPowerSeen > 0 || sim.SawUsingTool) ? 20 : (sim.PreviouslyDownButtons.Count > 0 ? 2 : 0);
            int settle = sim.RequestedSettleTicks >= 0 ? sim.RequestedSettleTicks : autoSettle;
            if (settle <= 0)
            {
                BeginFinalizeOrCaptureInputSim(sim);
                return;
            }

            sim.InSettlePhase = true;
            sim.SettleTicksRemaining = settle;
        }

        private void BeginFinalizeOrCaptureInputSim(PendingInputSimulation sim)
        {
            if (!string.IsNullOrWhiteSpace(sim.OutScreenshotPath))
            {
                sim.WaitingForFinalRender = true;
                return;
            }

            CompleteInputSimulation(sim);
        }

        private void CaptureInputSimScreenshotOnRendered()
        {
            var sim = ActiveInputSimInstance;
            if (sim == null || !sim.WaitingForFinalRender)
                return;

            sim.WaitingForFinalRender = false;
            if (!string.IsNullOrWhiteSpace(sim.OutScreenshotPath))
            {
                sim.ScreenshotResult = CaptureScreenBufferImmediate(sim.OutScreenshotPath!, includeUiLayout: false);
            }
            CompleteInputSimulation(sim);
        }

        private void CompleteInputSimulation(PendingInputSimulation sim)
        {
            // 确保所有残留按下状态被显式释放
            if (_sInputOverrideButtonMethod != null && Game1.input != null)
            {
                foreach (var rem in sim.PreviouslyDownButtons)
                {
                    try { _sInputOverrideButtonMethod.Invoke(Game1.input, new object[] { rem, false }); } catch { }
                }
            }
            sim.PreviouslyDownButtons.Clear();

            if (sim.TouchedGamepadMode && Game1.options != null)
            {
                Game1.options.gamepadMode = sim.OrigGamepadMode;
                Game1.options.gamepadControls = sim.OrigGamepadControls;
            }

            ActiveInputSimInstance = null;

            var resp = new JObject
            {
                ["ok"] = true,
                ["focusFree"] = true,
                ["ticksExecuted"] = sim.TotalTicksExecuted,
                ["transitions"] = new JArray(sim.InjectedTransitions)
            };

            if (Context.IsWorldReady && Game1.player != null)
            {
                Vector2 posAfter = Game1.player.Position;
                double distPx = Math.Round(Vector2.Distance(sim.PixelBefore, posAfter), 2);
                resp["player"] = new JObject
                {
                    ["locationBefore"] = sim.LocationBefore,
                    ["locationAfter"] = Game1.currentLocation?.NameOrUniqueName,
                    ["tileBefore"] = $"{sim.TileBefore.X},{sim.TileBefore.Y}",
                    ["tileAfter"] = $"{Game1.player.TilePoint.X},{Game1.player.TilePoint.Y}",
                    ["pixelBefore"] = $"{Math.Round(sim.PixelBefore.X, 1)},{Math.Round(sim.PixelBefore.Y, 1)}",
                    ["pixelAfter"] = $"{Math.Round(posAfter.X, 1)},{Math.Round(posAfter.Y, 1)}",
                    ["movedPixels"] = distPx,
                    ["facingBefore"] = sim.FacingBefore,
                    ["facingAfter"] = Game1.player.FacingDirection,
                    ["currentTool"] = Game1.player.CurrentTool?.GetType().Name,
                    ["maxToolPowerReached"] = sim.MaxToolPowerSeen,
                    ["usedToolDuringSim"] = sim.SawUsingTool
                };
            }

            var latestHover = ExtractMenuHoverSnapshot(GetActiveMenuOrSubPage()) ?? sim.PeakHoverSnapshot;
            string? finalSnap = FormatSnappedComponent(GetActiveMenuOrSubPage()?.currentlySnappedComponent) ?? sim.PeakSnappedAfter;
            resp["menu"] = new JObject
            {
                ["activeMenuBefore"] = sim.MenuBefore,
                ["activeMenuAfter"] = DescribeActiveMenuType() ?? sim.PeakMenuAfter,
                ["snappedBefore"] = sim.SnappedBefore,
                ["snappedAfter"] = finalSnap,
                ["snapTrail"] = new JArray(sim.SnapTrail),
                ["hover"] = sim.PeakHoverSnapshot ?? latestHover
            };

            if (sim.ScreenshotResult != null)
                resp["screenshot"] = sim.ScreenshotResult;

            sim.Tcs?.TrySetResult(resp);
        }

        private static IClickableMenu? GetActiveMenuOrSubPage()
        {
            IClickableMenu? menu = Game1.activeClickableMenu;
            if (menu is TitleMenu && TitleMenu.subMenu != null)
                menu = TitleMenu.subMenu;
            else if (menu is GameMenu gm)
                menu = gm.GetCurrentPage() ?? gm;
            return menu;
        }

        private static string? DescribeActiveMenuType()
        {
            var m = GetActiveMenuOrSubPage();
            return m?.GetType().FullName;
        }

        private static string? FormatSnappedComponent(ClickableComponent? cc)
        {
            if (cc == null)
                return null;
            string label = !string.IsNullOrWhiteSpace(cc.name) ? cc.name : cc.label;
            return string.IsNullOrWhiteSpace(label)
                ? $"#{cc.myID}@({cc.bounds.Center.X},{cc.bounds.Center.Y})"
                : $"#{cc.myID} ({label})@({cc.bounds.Center.X},{cc.bounds.Center.Y})";
        }

        private static JObject? ExtractMenuHoverSnapshot(IClickableMenu? menu)
        {
            if (menu == null)
                return null;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = menu.GetType();

            string? hoverText = ReadStringMember(menu, t, Flags, "hoverText", "_hoverText", "HoverText", "descriptionText");
            string? hoverTitle = ReadStringMember(menu, t, Flags, "boldTitleText", "hoverTitleText", "hoverTitle", "HoverTitle");
            object? hoveredItemObj = ReadObjectMember(menu, t, Flags, "hoveredItem", "hoverItem", "HoveredItem");

            string? hoveredItemDesc = null;
            if (hoveredItemObj is Item item)
                hoveredItemDesc = $"{item.QualifiedItemId} ({item.DisplayName} x{item.Stack})";

            int uiMouseX = Game1.getMouseX(ui_scale: true);
            int uiMouseY = Game1.getMouseY(ui_scale: true);
            string? hitComponent = null;
            if (menu.allClickableComponents != null)
            {
                var hit = menu.allClickableComponents.FirstOrDefault(c => c != null && c.visible && c.bounds.Contains(uiMouseX, uiMouseY));
                if (hit != null)
                    hitComponent = FormatSnappedComponent(hit);
            }

            return new JObject
            {
                ["cursorUiPos"] = $"{uiMouseX},{uiMouseY}",
                ["hoveredComponent"] = hitComponent,
                ["hoverTitle"] = string.IsNullOrWhiteSpace(hoverTitle) ? null : hoverTitle,
                ["hoverText"] = string.IsNullOrWhiteSpace(hoverText) ? null : hoverText,
                ["hoveredItem"] = hoveredItemDesc
            };
        }

        private static string? ReadStringMember(object target, Type type, BindingFlags flags, params string[] names)
        {
            foreach (var name in names)
            {
                var f = type.GetField(name, flags);
                if (f != null && f.FieldType == typeof(string))
                {
                    string? v = f.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
                var p = type.GetProperty(name, flags);
                if (p != null && p.CanRead && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                {
                    string? v = p.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
            }
            return null;
        }

        private static object? ReadObjectMember(object target, Type type, BindingFlags flags, params string[] names)
        {
            foreach (var name in names)
            {
                var f = type.GetField(name, flags);
                if (f != null)
                {
                    object? v = f.GetValue(target);
                    if (v != null) return v;
                }
                var p = type.GetProperty(name, flags);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                {
                    object? v = p.GetValue(target);
                    if (v != null) return v;
                }
            }
            return null;
        }

        private static Point? ParseJsonPoint(JToken? tok)
        {
            if (tok == null || tok.Type == JTokenType.Null)
                return null;
            if (tok is JObject o && o["x"] != null && o["y"] != null)
                return new Point(o.Value<int>("x"), o.Value<int>("y"));
            string s = tok.ToString().Trim();
            var parts = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                return new Point(x, y);
            return null;
        }

        private static bool TryParseButtonCombo(string rawCombo, out List<SButton> buttons, out string? error)
        {
            buttons = new List<SButton>();
            error = null;
            var parts = rawCombo.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawPart in parts)
            {
                string p = rawPart.Trim();
                if (string.IsNullOrEmpty(p))
                    continue;

                if (TryParseSingleSButton(p, out SButton btn))
                {
                    if (!buttons.Contains(btn))
                        buttons.Add(btn);
                }
                else
                {
                    error = $"Unrecognized button `{p}` (examples: `W`, `A`, `S`, `D`, `C`, `X`, `E`, `Escape`, `LeftShift`, `MouseLeft`, `MouseRight`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`, `ControllerA`, `ControllerB`, `LeftShoulder`, `RightShoulder`).";
                    return false;
                }
            }

            if (buttons.Count == 0)
            {
                error = $"Empty button combo `{rawCombo}`.";
                return false;
            }
            return true;
        }

        private static bool TryParseSingleSButton(string token, out SButton button)
        {
            if (Enum.TryParse(token, ignoreCase: true, out button))
                return true;

            switch (token.ToLowerInvariant())
            {
                case "shift": button = SButton.LeftShift; return true;
                case "ctrl":
                case "control": button = SButton.LeftControl; return true;
                case "alt": button = SButton.LeftAlt; return true;
                case "esc": button = SButton.Escape; return true;
                case "enter":
                case "return": button = SButton.Enter; return true;
                case "lmb":
                case "leftclick":
                case "mouse1": button = SButton.MouseLeft; return true;
                case "rmb":
                case "rightclick":
                case "mouse2": button = SButton.MouseRight; return true;
                case "mmb":
                case "middleclick": button = SButton.MouseMiddle; return true;
                case "up": button = SButton.DPadUp; return true;
                case "down": button = SButton.DPadDown; return true;
                case "left": button = SButton.DPadLeft; return true;
                case "right": button = SButton.DPadRight; return true;
                case "pad_a":
                case "btn_a": button = SButton.ControllerA; return true;
                case "pad_b":
                case "btn_b": button = SButton.ControllerB; return true;
                case "pad_x":
                case "btn_x": button = SButton.ControllerX; return true;
                case "pad_y":
                case "btn_y": button = SButton.ControllerY; return true;
                case "lb": button = SButton.LeftShoulder; return true;
                case "rb": button = SButton.RightShoulder; return true;
                case "lt": button = SButton.LeftTrigger; return true;
                case "rt": button = SButton.RightTrigger; return true;
                default:
                    button = SButton.None;
                    return false;
            }
        }

        private static bool IsGamepadSButton(SButton b) => b.TryGetController(out _);

        #endregion
    }
}
