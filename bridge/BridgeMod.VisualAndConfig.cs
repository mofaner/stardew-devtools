using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;

namespace SdvDevBridge
{
    /// <summary>
    /// 多帧截图异步状态机任务（驱动 `--ab-compare` 光影开关同帧位左右对比图 与 `--burst` / `--pan-test` 动态四宫格拼图）。
    /// </summary>
    internal sealed class PendingMultiFrameCapture
    {
        public string Mode { get; set; } = "burst"; // "ab_compare" | "burst"
        public string OutputPath { get; set; } = string.Empty;
        public TaskCompletionSource<JObject>? Tcs { get; set; }

        // 通用帧采集控制
        public int TargetFrames { get; set; } = 4;
        public int IntervalTicks { get; set; } = 15;
        public int TicksUntilNextCapture { get; set; }
        public bool ReadyForRenderCapture { get; set; }
        public int TotalElapsedTicks { get; set; }
        public List<(int Width, int Height, Color[] Pixels, string Badge)> CapturedFrames { get; } =
            new List<(int, int, Color[], string)>();

        // 镜头平移测试（`--pan dx,dy` / `--pan-test`）状态保存与还原
        public int PanDxPerTick { get; set; }
        public int PanDyPerTick { get; set; }
        public bool SavedViewportState { get; set; }
        public bool OrigViewportFreeze { get; set; }
        public int OrigViewportX { get; set; }
        public int OrigViewportY { get; set; }

        // 临时镜头缩放保存与还原（供 `screenshot --zoom <val>` 拍完自动恢复原视野）
        public float? TempZoom { get; set; }
        public float? OrigZoomToRestore { get; set; }

        // A/B 对比（`--ab-compare <ModId>`）状态保存与还原
        public string TargetModId { get; set; } = string.Empty;
        public string ToggleMechanism { get; set; } = string.Empty; // "config_bool" | "display_events"
        public string ToggleLabel { get; set; } = string.Empty;
        public object? ConfigInstance { get; set; }
        public MemberInfo? BoolMember { get; set; }
        public bool OrigBoolValue { get; set; }
        public List<(IList HandlerList, object Entry, int OriginalIndex)> TemporarilyRemovedHandlers { get; } =
            new List<(IList, object, int)>();
    }

    /// <summary>
    /// 桥接模组扩展模块：实时改配置 (`mod_config`)、A/B 对比截图与动态连拍四宫格 (`screenshot_advanced`)、
    /// 以及可见桌面会话原位自重启 (`self_restart`) 与无打扰后台隐藏窗口 (`--background`)。
    /// </summary>
    public sealed partial class ModEntry
    {
        private bool _hiddenBackgroundWindow;

        #region 1. 免重启实时读写模组配置 (`sdv-dev config`)

        /// <summary>
        /// 实时读取或修改运行中模组的内存配置对象（同时兼容 C# SMAPI 模组与 Content Patcher 内容包）。
        ///
        /// 【当初为什么要写这个功能】：
        /// 在调试光影强度、Bloom 阈值、水面反射透明度或数值平衡时，以往每改一个 `float` 参数都要重新编译并重启游戏（耗时约 10 秒）。
        /// 通过在游戏主线程反射定位目标 C# 模组实例持有的 `Config` 属性/字段（或 CP 包的 `config.json`），可以直接在 0.05 秒内
        /// 修改内存中的配置值立即看到画面变化，并可选自动调用 `Helper.WriteConfig` 同步写回磁盘。
        /// </summary>
        private JObject HandleModConfig(JObject req)
        {
            string? modId = req.Value<string>("modId");
            if (string.IsNullOrWhiteSpace(modId))
                return new JObject { ["ok"] = false, ["error"] = "Missing target `modId` (e.g. `sdv-dev config Author.ModName --get`)." };

            IModInfo? modInfo = Helper.ModRegistry.Get(modId!);
            if (modInfo == null)
            {
                // 尝试按模组名或后缀大小写不敏感模糊匹配唯一项
                var fuzzy = Helper.ModRegistry.GetAll()
                    .Where(m => m.Manifest.UniqueID.EndsWith("." + modId, StringComparison.OrdinalIgnoreCase) ||
                                m.Manifest.Name.Equals(modId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (fuzzy.Count == 1)
                    modInfo = fuzzy[0];
            }

            if (modInfo == null)
                return new JObject { ["ok"] = false, ["error"] = $"Mod `{modId}` is not loaded in current SMAPI session." };

            string resolvedId = modInfo.Manifest.UniqueID;
            string? filterKey = req.Value<string>("key");
            bool persist = req.Value<bool?>("save") ?? true;
            bool resetToDefault = req.Value<bool?>("reset") == true;
            JObject? updates = req["set"] as JObject;

            // 分支 A：Content Patcher 内容包 -> 读写或重置其目录下的 config.json 并触发 `patch reload <UniqueId>`
            if (modInfo.IsContentPack)
            {
                string? dirPath = TryGetModDirectoryPath(modInfo);
                if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
                    return new JObject { ["ok"] = false, ["error"] = $"Could not locate directory path for content pack `{resolvedId}`." };

                string cfgFile = Path.Combine(dirPath!, "config.json");
                if (resetToDefault)
                {
                    if (File.Exists(cfgFile))
                        File.Delete(cfgFile);
                    HandleConsoleCommand(new JObject { ["command"] = $"patch reload {resolvedId}" });
                    return new JObject
                    {
                        ["ok"] = true,
                        ["uniqueId"] = resolvedId,
                        ["modKind"] = "ContentPack",
                        ["resetToDefault"] = true,
                        ["configPath"] = cfgFile
                    };
                }

                JObject cpCfg = File.Exists(cfgFile)
                    ? JObject.Parse(File.ReadAllText(cfgFile, Encoding.UTF8))
                    : new JObject();

                var changed = new JObject();
                if (updates != null && updates.Count > 0)
                {
                    foreach (var kv in updates)
                    {
                        var existingProp = cpCfg.Properties().FirstOrDefault(p => p.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                        string targetPropName = existingProp?.Name ?? kv.Key;
                        cpCfg[targetPropName] = kv.Value;
                        changed[targetPropName] = kv.Value;
                    }

                    File.WriteAllText(cfgFile, cpCfg.ToString(Formatting.Indented), new UTF8Encoding(false));
                    HandleConsoleCommand(new JObject { ["command"] = $"patch reload {resolvedId}" });
                }

                return new JObject
                {
                    ["ok"] = true,
                    ["uniqueId"] = resolvedId,
                    ["modKind"] = "ContentPack",
                    ["configPath"] = cfgFile,
                    ["updated"] = changed.Count > 0 ? changed : null,
                    ["values"] = FilterJObjectByKey(cpCfg, filterKey)
                };
            }

            // 分支 B：C# SMAPI 模组 -> 反射获取 `IMod` 实例及其 `Config` / `_config` 对象
            IMod? modInstance = TryGetModInstance(modInfo);
            if (modInstance == null)
                return new JObject { ["ok"] = false, ["error"] = $"Could not access C# `IMod` instance for `{resolvedId}`." };

            var (cfgObj, holderMemberDesc) = TryLocateModConfigObject(modInstance);
            if (cfgObj == null)
            {
                return new JObject
                {
                    ["ok"] = false,
                    ["error"] = $"Mod `{resolvedId}` (`{modInstance.GetType().FullName}`) does not expose a recognizable Config field or property."
                };
            }

            var updatedFields = new JObject();
            // 一键恢复默认配置 (`sdv-dev config <ModId> --reset`)：
            // 直接反射 `new ModConfig()`，把所有公有可写属性与字段拷回当前内存 Config 实例（兼容子渲染器缓存了旧 Config 引用的情况），并落盘！
            if (resetToDefault)
            {
                object? freshCfg = Activator.CreateInstance(cfgObj.GetType());
                if (freshCfg == null)
                    return new JObject { ["ok"] = false, ["error"] = $"Could not instantiate default `{cfgObj.GetType().FullName}`." };

                CopyAllWritableConfigMembers(freshCfg, cfgObj);
            }

            if (updates != null && updates.Count > 0)
            {
                foreach (var kv in updates)
                {
                    if (!TrySetConfigMemberPath(cfgObj, kv.Key, kv.Value, out string resolvedMemberPath, out object? newTypedVal, out string? setErr))
                    {
                        return new JObject
                        {
                            ["ok"] = false,
                            ["uniqueId"] = resolvedId,
                            ["error"] = setErr ?? $"Failed to set `{kv.Key}` on `{cfgObj.GetType().Name}`."
                        };
                    }
                    updatedFields[resolvedMemberPath] = ToNonNullJsonToken(newTypedVal);
                }
            }

            if (resetToDefault || (updates != null && updates.Count > 0))
            {
                // 若模组定义了无参配置刷新钩子（如 ApplyConfig / OnConfigChanged / ReloadConfig），顺手触发使其派生缓存同步更新
                InvokeOptionalConfigHook(modInstance);

                if (persist)
                {
                    TryWriteSmapiModConfig(modInstance, cfgObj);
                }
            }

            JObject currentValues = DumpConfigMembersToJson(cfgObj, filterKey);
            return new JObject
            {
                ["ok"] = true,
                ["uniqueId"] = resolvedId,
                ["modKind"] = "CSharpMod",
                ["modEntryType"] = modInstance.GetType().FullName,
                ["configHolder"] = holderMemberDesc,
                ["configType"] = cfgObj.GetType().FullName,
                ["resetToDefault"] = resetToDefault ? true : (bool?)null,
                ["savedToDisk"] = (resetToDefault || (updates != null && updates.Count > 0)) ? persist : (bool?)null,
                ["updated"] = updatedFields.Count > 0 ? updatedFields : null,
                ["values"] = currentValues
            };
        }

        private static IMod? TryGetModInstance(IModInfo modInfo)
        {
            PropertyInfo? p = modInfo.GetType().GetProperty("Mod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p?.GetValue(modInfo) as IMod;
        }

        private static string? TryGetModDirectoryPath(IModInfo modInfo)
        {
            PropertyInfo? p = modInfo.GetType().GetProperty("DirectoryPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p?.GetValue(modInfo) as string;
        }

        private static (object? ConfigObj, string HolderDesc) TryLocateModConfigObject(IMod modInstance)
        {
            Type modType = modInstance.GetType();
            const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            // 1. 优先匹配命名为 Config / _config / ModConfig / Settings 的属性或字段
            string[] preferredNames = { "Config", "_config", "config", "ModConfig", "_modConfig", "Settings", "_settings" };
            foreach (var name in preferredNames)
            {
                PropertyInfo? prop = modType.GetProperty(name, AllFlags);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0 && IsCandidateConfigType(prop.PropertyType))
                {
                    object? v = prop.GetValue(prop.GetMethod!.IsStatic ? null : modInstance);
                    if (v != null)
                        return (v, $"{modType.Name}.{prop.Name}");
                }

                FieldInfo? field = modType.GetField(name, AllFlags);
                if (field != null && IsCandidateConfigType(field.FieldType))
                {
                    object? v = field.GetValue(field.IsStatic ? null : modInstance);
                    if (v != null)
                        return (v, $"{modType.Name}.{field.Name}");
                }
            }

            // 2. 次优匹配：类型名以 Config 或 Settings 结尾的任意实例/静态字段或属性
            foreach (var prop in modType.GetProperties(AllFlags))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                    continue;
                if (prop.PropertyType.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase) ||
                    prop.PropertyType.Name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase))
                {
                    object? v = prop.GetValue(prop.GetMethod!.IsStatic ? null : modInstance);
                    if (v != null)
                        return (v, $"{modType.Name}.{prop.Name}");
                }
            }

            foreach (var field in modType.GetFields(AllFlags))
            {
                if (field.FieldType.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase) ||
                    field.FieldType.Name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase))
                {
                    object? v = field.GetValue(field.IsStatic ? null : modInstance);
                    if (v != null)
                        return (v, $"{modType.Name}.{field.Name}");
                }
            }

            return (null, string.Empty);
        }

        private static bool IsCandidateConfigType(Type t)
        {
            if (t.IsPrimitive || t == typeof(string) || typeof(IModHelper).IsAssignableFrom(t) || typeof(IMonitor).IsAssignableFrom(t))
                return false;
            if (t.Namespace != null && (t.Namespace.StartsWith("System.", StringComparison.Ordinal) || t.Namespace.StartsWith("StardewModdingAPI", StringComparison.Ordinal)))
                return false;
            return t.IsClass;
        }

        private static bool TrySetConfigMemberPath(
            object rootConfig,
            string dottedPath,
            JToken? rawToken,
            out string resolvedPath,
            out object? assignedValue,
            out string? error)
        {
            resolvedPath = dottedPath;
            assignedValue = null;
            error = null;

            string[] segments = dottedPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                error = "Empty config property name.";
                return false;
            }

            object current = rootConfig;
            var canonicalParts = new List<string>();

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var (member, memberType, canonicalName) = FindReadableWritableMember(current.GetType(), segments[i], requireWrite: false);
                if (member == null || memberType == null)
                {
                    error = $"Nested config section `{segments[i]}` not found on `{current.GetType().Name}`.";
                    return false;
                }
                object? next = GetMemberValue(member, current);
                if (next == null)
                {
                    error = $"Nested config section `{canonicalName}` is null.";
                    return false;
                }
                canonicalParts.Add(canonicalName);
                current = next;
            }

            string leafSeg = segments[segments.Length - 1];
            var (leafMember, leafType, leafCanonical) = FindReadableWritableMember(current.GetType(), leafSeg, requireWrite: true);
            if (leafMember == null || leafType == null)
            {
                string validProps = string.Join(", ", GetPublicConfigMemberNames(current.GetType()));
                error = $"Config property `{leafSeg}` not found (or read-only) on `{current.GetType().Name}`. Available: {validProps}";
                return false;
            }

            if (!TryConvertConfigValue(rawToken, leafType, out object? converted, out string? convErr))
            {
                error = $"Cannot convert `{rawToken}` to `{leafType.Name}` for `{leafCanonical}`: {convErr}";
                return false;
            }

            SetMemberValue(leafMember, current, converted);
            canonicalParts.Add(leafCanonical);
            resolvedPath = string.Join(".", canonicalParts);
            assignedValue = converted;
            return true;
        }

        private static (MemberInfo? Member, Type? ValueType, string CanonicalName) FindReadableWritableMember(Type type, string name, bool requireWrite)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            PropertyInfo? prop = type.GetProperty(name, Flags);
            if (prop != null && prop.CanRead && (!requireWrite || prop.CanWrite))
                return (prop, prop.PropertyType, prop.Name);

            FieldInfo? field = type.GetField(name, Flags);
            if (field != null && (!requireWrite || !field.IsInitOnly))
                return (field, field.FieldType, field.Name);

            return (null, null, name);
        }

        private static List<string> GetPublicConfigMemberNames(Type type)
        {
            var names = new List<string>();
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.CanRead && p.GetIndexParameters().Length == 0)
                    names.Add($"{p.Name} ({p.PropertyType.Name})");
            }
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.IsStatic && !f.IsInitOnly)
                    names.Add($"{f.Name} ({f.FieldType.Name})");
            }
            return names;
        }

        private static object? GetMemberValue(MemberInfo m, object instance) =>
            m is PropertyInfo p ? p.GetValue(instance) : ((FieldInfo)m).GetValue(instance);

        private static void SetMemberValue(MemberInfo m, object instance, object? value)
        {
            if (m is PropertyInfo p)
                p.SetValue(instance, value);
            else if (m is FieldInfo f)
                f.SetValue(instance, value);
        }

        private static bool TryConvertConfigValue(JToken? tok, Type targetType, out object? converted, out string? err)
        {
            converted = null;
            err = null;

            Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (tok == null || tok.Type == JTokenType.Null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return true;
                err = "Value type cannot be null.";
                return false;
            }

            string rawStr = tok.Type == JTokenType.String ? (tok.Value<string>() ?? string.Empty) : tok.ToString(Formatting.None);

            try
            {
                if (underlying == typeof(string))
                {
                    converted = rawStr;
                    return true;
                }
                if (underlying == typeof(bool))
                {
                    if (tok.Type == JTokenType.Boolean) { converted = tok.Value<bool>(); return true; }
                    if (rawStr == "1" || rawStr.Equals("true", StringComparison.OrdinalIgnoreCase) || rawStr.Equals("on", StringComparison.OrdinalIgnoreCase))
                    { converted = true; return true; }
                    if (rawStr == "0" || rawStr.Equals("false", StringComparison.OrdinalIgnoreCase) || rawStr.Equals("off", StringComparison.OrdinalIgnoreCase))
                    { converted = false; return true; }
                }
                if (underlying == typeof(int))
                {
                    converted = int.Parse(rawStr, CultureInfo.InvariantCulture);
                    return true;
                }
                if (underlying == typeof(float))
                {
                    converted = float.Parse(rawStr.TrimEnd('f', 'F'), CultureInfo.InvariantCulture);
                    return true;
                }
                if (underlying == typeof(double))
                {
                    converted = double.Parse(rawStr.TrimEnd('d', 'D'), CultureInfo.InvariantCulture);
                    return true;
                }
                if (underlying.IsEnum)
                {
                    converted = Enum.Parse(underlying, rawStr, ignoreCase: true);
                    return true;
                }
                if (underlying == typeof(Color))
                {
                    if (TryParseHexOrRgbColor(rawStr, out Color c))
                    {
                        converted = c;
                        return true;
                    }
                }
                if (underlying.FullName == "StardewModdingAPI.Utilities.KeybindList")
                {
                    MethodInfo? parseMethod = underlying.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (parseMethod != null)
                    {
                        converted = parseMethod.Invoke(null, new object[] { rawStr });
                        return true;
                    }
                }

                converted = tok.ToObject(targetType);
                return true;
            }
            catch (Exception ex)
            {
                err = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        private static bool TryParseHexOrRgbColor(string raw, out Color color)
        {
            color = Color.White;
            string s = raw.Trim().TrimStart('#');
            if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            {
                color = new Color((int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF), 255);
                return true;
            }
            if (s.Length == 8 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgba))
            {
                color = new Color((int)((rgba >> 24) & 0xFF), (int)((rgba >> 16) & 0xFF), (int)((rgba >> 8) & 0xFF), (int)(rgba & 0xFF));
                return true;
            }
            var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                int.TryParse(parts[0], out int r) &&
                int.TryParse(parts[1], out int g) &&
                int.TryParse(parts[2], out int b))
            {
                int a = parts.Length >= 4 && int.TryParse(parts[3], out int av) ? av : 255;
                color = new Color(r, g, b, a);
                return true;
            }
            return false;
        }

        private static void TryWriteSmapiModConfig(IMod modInstance, object cfgObj)
        {
            try
            {
                MethodInfo? genericWrite = typeof(IModHelper).GetMethods()
                    .FirstOrDefault(m => m.Name == "WriteConfig" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
                genericWrite?.MakeGenericMethod(cfgObj.GetType()).Invoke(modInstance.Helper, new[] { cfgObj });
            }
            catch { }
        }

        private static void InvokeOptionalConfigHook(IMod modInstance)
        {
            string[] hookNames = { "OnConfigChanged", "ApplyConfig", "ReloadConfig", "RefreshConfig" };
            foreach (var name in hookNames)
            {
                MethodInfo? m = modInstance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m != null)
                {
                    try { m.Invoke(modInstance, null); } catch { }
                    break;
                }
            }
        }

        private static JObject DumpConfigMembersToJson(object cfgObj, string? filterKey)
        {
            var result = new JObject();
            Type t = cfgObj.GetType();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0)
                    continue;
                if (!string.IsNullOrWhiteSpace(filterKey) && p.Name.IndexOf(filterKey!, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                try
                {
                    object? v = p.GetValue(cfgObj);
                    result[p.Name] = ToNonNullJsonToken(v);
                }
                catch { }
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsStatic)
                    continue;
                if (!string.IsNullOrWhiteSpace(filterKey) && f.Name.IndexOf(filterKey!, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                try
                {
                    object? v = f.GetValue(cfgObj);
                    result[f.Name] = ToNonNullJsonToken(v);
                }
                catch { }
            }
            return result;
        }

        private static JToken ToNonNullJsonToken(object? val)
        {
            if (val == null)
                return JValue.CreateNull();
            object mapped = SerializeConfigScalarOrObj(val);
            return JToken.FromObject(mapped);
        }

        private static object SerializeConfigScalarOrObj(object val)
        {
            if (val is Enum || val.GetType().FullName == "StardewModdingAPI.Utilities.KeybindList")
                return val.ToString() ?? string.Empty;
            if (val is Color c)
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}{(c.A == 255 ? "" : c.A.ToString("X2"))}";
            return val;
        }

        private static JObject FilterJObjectByKey(JObject src, string? filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey))
                return src;
            var dst = new JObject();
            foreach (var p in src.Properties())
            {
                if (p.Name.IndexOf(filterKey!, StringComparison.OrdinalIgnoreCase) >= 0)
                    dst[p.Name] = p.Value;
            }
            return dst;
        }

        #endregion

        #region 2. 一键 A/B 光影对比截图 (`--ab-compare`) 与动态时序连拍四宫格 (`--burst` / `--pan-test`)

        /// <summary>
        /// 启动多帧视觉验收采集任务：
        ///
        /// 【当初为什么要写这两个功能】：
        /// 1. `--ab-compare <ModId>`：做光影/滤镜/水面着色器验收时，靠分别手动改配置截两张图不仅慢，而且时间流逝、云影飘动、
        ///    NPC 走动会导致两张图画面对不上。本功能在同一游戏时刻自动将模组设为关闭截左图、设为开启跑 2 ticks 截右图、
        ///    随后无痕恢复原状态并拼成左右对比 PNG（优先切换模组布尔开关以兼容 Harmony 补丁，无开关模组自动回退临时摘除 Display 委托）。
        /// 2. `--burst` / `--pan-test`：静态单帧截图看不出“水面波纹不随时间流动”、“粒子卡死”或“移动镜头时离屏 RenderTarget 未清空留残影”。
        ///    按确定性游戏 Tick 间隔（默认每 15 ticks）连续抓取多帧（可选同步平滑平移镜头）并合成带帧标/Tick 角标的 2×2 四宫格，
        ///    让多模态 AI 读一张 PNG 就能直接审出动态节奏与拖影问题。
        /// </summary>
        private JObject? StartMultiFrameCapture(JObject req, TaskCompletionSource<JObject>? tcs)
        {
            if (_activeMultiFrameCapture != null)
                return new JObject { ["ok"] = false, ["error"] = "Another multi-frame screenshot capture is already in progress." };

            string? outPath = req.Value<string>("outPath");
            if (string.IsNullOrWhiteSpace(outPath))
                return new JObject { ["ok"] = false, ["error"] = "Missing `outPath` for screenshot." };

            string capMode = req.Value<string>("captureMode")?.ToLowerInvariant() ?? "burst";
            var job = new PendingMultiFrameCapture
            {
                Mode = capMode,
                OutputPath = Path.GetFullPath(outPath!),
                Tcs = tcs
            };

            float? reqZoom = req.Value<float?>("zoom");
            if (reqZoom.HasValue && reqZoom.Value > 0.05f && Game1.options != null)
            {
                job.OrigZoomToRestore = Game1.options.desiredBaseZoomLevel;
                job.TempZoom = reqZoom.Value;
                ApplyCameraZoomAndRebuildScreen(reqZoom.Value);
            }

            if (capMode == "single_zoom")
            {
                // 切换完 zoom 重建 Game1.screen 后必须至少跑过 1 帧完整 Draw，才能抓到新视野下的无接缝画面！
                job.TargetFrames = 1;
                job.IntervalTicks = 1;
                job.TicksUntilNextCapture = 1;
                _activeMultiFrameCapture = job;
                return tcs != null ? null : new JObject { ["ok"] = true, ["started"] = "single_zoom" };
            }
            else if (capMode == "ab_compare")
            {
                string? rawTarget = req.Value<string>("abMod");
                string? explicitToggleProp = req.Value<string>("abToggle");
                if (string.IsNullOrWhiteSpace(rawTarget))
                    return new JObject { ["ok"] = false, ["error"] = "Missing target mod ID for `--ab-compare <ModId>`." };

                string modId = rawTarget!;
                int colon = modId.IndexOf(':');
                if (colon > 0 && colon < modId.Length - 1)
                {
                    if (string.IsNullOrWhiteSpace(explicitToggleProp))
                        explicitToggleProp = modId.Substring(colon + 1);
                    modId = modId.Substring(0, colon);
                }

                if (!SetupAbCompareToggle(job, modId, explicitToggleProp, out string? setupErr))
                    return new JObject { ["ok"] = false, ["error"] = setupErr };

                // 第一阶段：先切到 OFF（原版对照态），等待 2 ticks 让渲染缓存刷新后截取左图
                SetAbModActiveState(job, active: false);
                job.TargetFrames = 2;
                job.IntervalTicks = 2;
                job.TicksUntilNextCapture = 2;
                _activeMultiFrameCapture = job;
                return tcs != null ? null : new JObject { ["ok"] = true, ["started"] = "ab_compare" };
            }
            else
            {
                int frames = Math.Clamp(req.Value<int?>("burstFrames") ?? 4, 2, 9);
                int intervalTicks = Math.Clamp(req.Value<int?>("intervalTicks") ?? 15, 1, 300);
                int panDx = req.Value<int?>("panDx") ?? 0;
                int panDy = req.Value<int?>("panDy") ?? 0;

                job.TargetFrames = frames;
                job.IntervalTicks = intervalTicks;
                job.PanDxPerTick = panDx;
                job.PanDyPerTick = panDy;
                job.TicksUntilNextCapture = 0; // 第 1 帧立即在当前帧截取 (t=+0t)
                job.ReadyForRenderCapture = true;

                if ((panDx != 0 || panDy != 0) && Context.IsWorldReady)
                {
                    job.SavedViewportState = true;
                    job.OrigViewportFreeze = Game1.viewportFreeze;
                    job.OrigViewportX = Game1.viewport.X;
                    job.OrigViewportY = Game1.viewport.Y;
                    Game1.viewportFreeze = true;
                }

                _activeMultiFrameCapture = job;
                return tcs != null ? null : new JObject { ["ok"] = true, ["started"] = "burst" };
            }
        }

        private void TickMultiFrameCapture()
        {
            var job = _activeMultiFrameCapture;
            if (job == null)
                return;

            job.TotalElapsedTicks++;

            // 超时兜底（例如窗口意外最小化未触发 Display.Rendered 时直接走即时缓冲抓取）
            if (job.TotalElapsedTicks > job.TargetFrames * (job.IntervalTicks + 40) + 120)
            {
                CaptureMultiFrameStepOnRendered();
                return;
            }

            if (job.ReadyForRenderCapture)
                return;

            // 镜头平移测试模式下，每 Tick 按设定像素步进移动 Game1.viewport 以检测动态残影与视差偏移
            if (job.Mode == "burst" && (job.PanDxPerTick != 0 || job.PanDyPerTick != 0) && Context.IsWorldReady && Game1.currentLocation?.Map != null)
            {
                int maxW = Math.Max(0, Game1.currentLocation.Map.DisplayWidth - Game1.viewport.Width);
                int maxH = Math.Max(0, Game1.currentLocation.Map.DisplayHeight - Game1.viewport.Height);
                Game1.viewport.X = Math.Clamp(Game1.viewport.X + job.PanDxPerTick, 0, maxW);
                Game1.viewport.Y = Math.Clamp(Game1.viewport.Y + job.PanDyPerTick, 0, maxH);
            }

            if (job.TicksUntilNextCapture > 0)
                job.TicksUntilNextCapture--;

            if (job.TicksUntilNextCapture <= 0)
                job.ReadyForRenderCapture = true;
        }

        private void CaptureMultiFrameStepOnRendered()
        {
            var job = _activeMultiFrameCapture;
            if (job == null)
                return;

            job.ReadyForRenderCapture = false;

            try
            {
                if (!TryGrabCurrentScreenPixels(out int w, out int h, out Color[] pixels, out string? grabErr))
                {
                    FinishAndCleanupMultiFrameJob(job, new JObject { ["ok"] = false, ["error"] = grabErr ?? "Failed to read screen pixels." });
                    return;
                }

                if (job.Mode == "single_zoom")
                {
                    bool includeUi = false;
                    JObject singleRes = CaptureScreenBufferImmediate(job.OutputPath, includeUi);
                    singleRes["zoom"] = Math.Round(Game1.options.zoomLevel, 3);
                    FinishAndCleanupMultiFrameJob(job, singleRes);
                    return;
                }
                else if (job.Mode == "ab_compare")
                {
                    if (job.CapturedFrames.Count == 0)
                    {
                        job.CapturedFrames.Add((w, h, pixels, $"OFF | VANILLA ({job.ToggleLabel}=false)"));
                        // 切换到 ON（开启目标模组效果），跑 2 ticks 后抓右图
                        SetAbModActiveState(job, active: true);
                        job.TicksUntilNextCapture = 2;
                        return;
                    }
                    else
                    {
                        job.CapturedFrames.Add((w, h, pixels, $"ON | {job.TargetModId} ({job.ToggleLabel}=true)"));
                        RestoreAbModOriginalState(job);
                        JObject resp = SaveStitchedMultiFramePng(job);
                        FinishAndCleanupMultiFrameJob(job, resp);
                        return;
                    }
                }
                else
                {
                    int idx = job.CapturedFrames.Count + 1;
                    int relTicks = (idx - 1) * job.IntervalTicks;
                    string vpTag = (job.PanDxPerTick != 0 || job.PanDyPerTick != 0)
                        ? $" vp=({Game1.viewport.X},{Game1.viewport.Y})"
                        : string.Empty;
                    job.CapturedFrames.Add((w, h, pixels, $"F{idx}/{job.TargetFrames} +{relTicks}t{vpTag}"));

                    if (job.CapturedFrames.Count >= job.TargetFrames)
                    {
                        JObject resp = SaveStitchedMultiFramePng(job);
                        FinishAndCleanupMultiFrameJob(job, resp);
                        return;
                    }

                    job.TicksUntilNextCapture = job.IntervalTicks;
                }
            }
            catch (Exception ex)
            {
                FinishAndCleanupMultiFrameJob(job, new JObject { ["ok"] = false, ["error"] = $"Multi-frame capture failed: {ex.Message}" });
            }
        }

        private bool _reapplyBackgroundAfterZoomRestore;

        private void FinishAndCleanupMultiFrameJob(PendingMultiFrameCapture job, JObject result)
        {
            _activeMultiFrameCapture = null;

            if (job.Mode == "ab_compare")
            {
                try { RestoreAbModOriginalState(job); } catch { }
            }

            if (job.SavedViewportState)
            {
                Game1.viewportFreeze = job.OrigViewportFreeze;
                Game1.viewport.X = job.OrigViewportX;
                Game1.viewport.Y = job.OrigViewportY;
            }

            if (job.OrigZoomToRestore.HasValue && Game1.options != null)
            {
                // 【关键时序防崩点】：
                // FinishAndCleanupMultiFrameJob 在 Display.Rendered 回调中触发，此时 Game1.Draw 尚未执行末尾的
                // `renderScreenBuffer(target_screen)`！若在此处直接调 SetWindowSize 重建并释放旧 `@Game1.screen`，
                // 当帧末尾就会抛 `ObjectDisposedException: Can't draw texture '@Game1.screen' because it's disposed`。
                // 因此这里只写回 `desiredBaseZoomLevel`，交由下一帧 `Game1._update` 开头原生安全触发 `refreshWindowSettings()`！
                float origZoom = Math.Clamp(job.OrigZoomToRestore.Value, 0.35f, 3.0f);
                Game1.options.desiredBaseZoomLevel = origZoom;
                Game1.options.localCoopBaseZoomLevel = origZoom;
                Game1.forceSnapOnNextViewportUpdate = true;
                if (_hiddenBackgroundWindow)
                    _reapplyBackgroundAfterZoomRestore = true;
            }

            job.Tcs?.TrySetResult(result);
        }

        /// <summary>
        /// 在 `UpdateTicked` 安全阶段规范调整游戏镜头缩放系数 (`0.35..3.0`)，并调用 `Game1.SetWindowSize` 同步重建 `@Game1.screen` 离屏缓冲与 `viewport`。
        ///
        /// 【当初为什么要封装这个方法】：
        /// 星露谷 1.6 的世界渲染先画到 `@Game1.screen` (`RenderTarget2D`，尺寸 = 窗口宽高 / `zoomLevel`)，再放大贴回主屏。
        /// 如果只改 `desiredBaseZoomLevel = 0.80f` 而不调 `SetWindowSize`（或没同步 `baseZoomLevel`），`viewport` 变大了但 `Game1.screen` 还停在 `1280x720`，
        /// 就会在画面右侧 `uv.x > 0.80` 处撕裂出一条假黑边/重复纹理接缝，误导着色器开发者以为是 Shader 算错了 UV！
        /// </summary>
        private void ApplyCameraZoomAndRebuildScreen(float requestedZoom)
        {
            if (Game1.options == null || Game1.game1 == null)
                return;

            float clamped = Math.Clamp(requestedZoom, 0.35f, 3.0f);
            // 必须同时更新 desiredBaseZoomLevel 与 baseZoomLevel：
            // 1) SetWindowSize 内部读取 options.zoomLevel (= baseZoomLevel * zoomModifier) 来分配新 @Game1.screen；
            // 2) 两者对齐后，下一帧 Game1._update 不会再重复触发第二次 refreshWindowSettings()。
            Game1.options.desiredBaseZoomLevel = clamped;
            Game1.options.localCoopBaseZoomLevel = clamped;
            Game1.options.baseZoomLevel = clamped;

            int winW = Game1.game1.localMultiplayerWindow.Width > 0
                ? Game1.game1.localMultiplayerWindow.Width
                : Game1.graphics.PreferredBackBufferWidth;
            int winH = Game1.game1.localMultiplayerWindow.Height > 0
                ? Game1.game1.localMultiplayerWindow.Height
                : Game1.graphics.PreferredBackBufferHeight;
            winW = Math.Max(1280, winW);
            winH = Math.Max(720, winH);

            // SetWindowSize 内部会自动按新 options.zoomLevel 重建 @Game1.screen、@Game1.uiScreen，
            // 并调用 updateViewportForScreenSizeChange 同步刷新 Game1.viewport 尺寸。
            Game1.game1.SetWindowSize(winW, winH);
            if (_hiddenBackgroundWindow)
            {
                // SetWindowSize 内部的 graphics.ApplyChanges() 会让 SDL 把窗口移回桌面可见坐标，后台沙盒模式下立即重新钉回屏幕外
                ApplyBackgroundWindowModeIfRequested();
            }

            Game1.forceSnapOnNextViewportUpdate = true;
            if (Context.IsWorldReady && Game1.currentLocation != null)
            {
                Game1.panScreen(0, 0);
            }
        }

        /// <summary>
        /// 免重启热刷 C# 模组中的 `.mgfxo` 着色器 (`Effect`) 实例。
        ///
        /// 【当初为什么要写这个功能】：
        /// 调试 HLSL 水面波纹/高光提取时，往往只改了 `.fx` 文件而没改 C# 逻辑。如果重启游戏要等 6~10 秒还会打断当前站位，
        /// 而单纯把新 `.mgfxo` 拷进 `Mods/` 不会自动更新模组内存里已 `new Effect(...)` 的实例。
        /// 因此这里同时支持两层热刷：
        ///   1. 若模组（或其子渲染器）定义了 `ReloadShaders()` / `ReloadEffects()` / `OnAssetsReloaded()` 无参方法，优先调用；
        ///   2. 若模组没写专用钩子，自动扫描 `ModEntry` 及其本程序集子渲染器字段上的 `Effect` 成员，按文件名匹配（或唯一 `.mgfxo`）
        ///      直接在主线程 `new Effect(GraphicsDevice, bytes)` 替换并释放旧实例，做到改完 `.fx` 0.2 秒热刷画面！
        /// </summary>
        private void HotReloadModShadersInMemory(IModInfo modInfo, JArray? compiledShaderRels, List<string> reloadedOut, out string? hookCalled)
        {
            hookCalled = null;
            IMod? modInst = TryGetModInstance(modInfo);
            string? modDir = TryGetModDirectoryPath(modInfo);
            if (modInst == null || string.IsNullOrWhiteSpace(modDir) || !Directory.Exists(modDir))
                return;

            string[] hookNames = { "ReloadShaders", "ReloadEffects", "OnShadersReloaded", "OnAssetsReloaded", "ReloadAssets" };
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            // 收集 ModEntry 本身 + 其持有的同程序集子模块实例（例如 WaterRenderer / LightingPipeline）
            var targetObjects = CollectModOwnedInstances(modInst);
            foreach (var obj in targetObjects)
            {
                foreach (var hName in hookNames)
                {
                    MethodInfo? m = obj.GetType().GetMethod(hName, Flags, null, Type.EmptyTypes, null);
                    if (m != null)
                    {
                        m.Invoke(m.IsStatic ? null : obj, null);
                        hookCalled = $"{obj.GetType().Name}.{m.Name}()";
                        break;
                    }
                }
                if (hookCalled != null)
                    break;
            }

            // 查找模组目录下所有 `.mgfxo` 文件
            var mgfxoFiles = Directory.GetFiles(modDir!, "*.mgfxo", SearchOption.AllDirectories);
            if (mgfxoFiles.Length == 0 || hookCalled != null)
                return;

            GraphicsDevice gd = Game1.graphics.GraphicsDevice;
            foreach (var obj in targetObjects)
            {
                Type t = obj.GetType();
                foreach (var f in t.GetFields(Flags))
                {
                    if (!typeof(Effect).IsAssignableFrom(f.FieldType) || f.IsInitOnly && f.IsStatic)
                        continue;
                    string? matchedFile = MatchMgfxoForMemberName(f.Name, mgfxoFiles);
                    if (matchedFile == null)
                        continue;

                    Effect? oldFx = f.GetValue(f.IsStatic ? null : obj) as Effect;
                    Effect newFx = new Effect(gd, File.ReadAllBytes(matchedFile));
                    f.SetValue(f.IsStatic ? null : obj, newFx);
                    try { oldFx?.Dispose(); } catch { }
                    reloadedOut.Add($"{t.Name}.{f.Name} <- {Path.GetFileName(matchedFile)}");
                }

                foreach (var p in t.GetProperties(Flags))
                {
                    if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0 || !typeof(Effect).IsAssignableFrom(p.PropertyType))
                        continue;
                    string? matchedFile = MatchMgfxoForMemberName(p.Name, mgfxoFiles);
                    if (matchedFile == null)
                        continue;

                    bool isStatic = p.SetMethod?.IsStatic == true;
                    Effect? oldFx = p.GetValue(isStatic ? null : obj) as Effect;
                    Effect newFx = new Effect(gd, File.ReadAllBytes(matchedFile));
                    p.SetValue(isStatic ? null : obj, newFx);
                    try { oldFx?.Dispose(); } catch { }
                    reloadedOut.Add($"{t.Name}.{p.Name} <- {Path.GetFileName(matchedFile)}");
                }
            }
        }

        private static List<object> CollectModOwnedInstances(IMod modInst)
        {
            var list = new List<object> { modInst };
            Assembly modAsm = modInst.GetType().Assembly;
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            foreach (var f in modInst.GetType().GetFields(Flags))
            {
                if (f.FieldType.Assembly != modAsm || f.FieldType.IsPrimitive || f.FieldType.IsEnum)
                    continue;
                try
                {
                    object? child = f.GetValue(f.IsStatic ? null : modInst);
                    if (child != null && !list.Contains(child))
                        list.Add(child);
                }
                catch { }
            }
            return list;
        }

        private static string? MatchMgfxoForMemberName(string memberName, string[] mgfxoFiles)
        {
            if (mgfxoFiles.Length == 1)
                return mgfxoFiles[0];

            string cleanMember = NormalizeShaderStem(memberName);
            foreach (var f in mgfxoFiles)
            {
                string stem = NormalizeShaderStem(Path.GetFileNameWithoutExtension(f));
                if (!string.IsNullOrEmpty(stem) && !string.IsNullOrEmpty(cleanMember) &&
                    (cleanMember.Contains(stem, StringComparison.OrdinalIgnoreCase) ||
                     stem.Contains(cleanMember, StringComparison.OrdinalIgnoreCase)))
                {
                    return f;
                }
            }
            return null;
        }

        private static string NormalizeShaderStem(string raw)
        {
            string s = raw.Trim('_');
            foreach (var suffix in new[] { "Shader", "Effect", "Fx", "Pass" })
            {
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && s.Length > suffix.Length)
                    s = s.Substring(0, s.Length - suffix.Length);
            }
            return s.Replace("_", "");
        }

        private static void CopyAllWritableConfigMembers(object src, object dst)
        {
            Type t = src.GetType();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                {
                    try { p.SetValue(dst, p.GetValue(src)); } catch { }
                }
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.IsStatic && !f.IsInitOnly)
                {
                    try { f.SetValue(dst, f.GetValue(src)); } catch { }
                }
            }
        }

        private static bool TryGrabCurrentScreenPixels(out int width, out int height, out Color[] pixels, out string? error)
        {
            width = 0;
            height = 0;
            pixels = Array.Empty<Color>();
            error = null;

            GraphicsDevice gd = Game1.graphics.GraphicsDevice;
            RenderTarget2D? worldTarget = Game1.game1?.screen;
            RenderTarget2D? uiTarget = Game1.game1?.uiScreen;

            if (worldTarget != null && !worldTarget.IsDisposed)
            {
                if (uiTarget != null && !uiTarget.IsDisposed)
                {
                    width = uiTarget.Width;
                    height = uiTarget.Height;
                    using var compRt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None);
                    RenderTargetBinding[] prev = gd.GetRenderTargets();
                    try
                    {
                        gd.SetRenderTarget(compRt);
                        gd.Clear(Color.Black);
                        using var sb = new SpriteBatch(gd);
                        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
                        sb.Draw(worldTarget, new Rectangle(0, 0, width, height), Color.White);
                        sb.Draw(uiTarget, new Rectangle(0, 0, width, height), Color.White);
                        sb.End();
                    }
                    finally
                    {
                        gd.SetRenderTargets(prev);
                    }

                    pixels = new Color[width * height];
                    compRt.GetData(pixels);
                    for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
                    return true;
                }

                width = worldTarget.Width;
                height = worldTarget.Height;
                pixels = new Color[width * height];
                worldTarget.GetData(pixels);
                for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
                return true;
            }

            width = gd.PresentationParameters.BackBufferWidth;
            height = gd.PresentationParameters.BackBufferHeight;
            pixels = new Color[width * height];
            gd.GetBackBufferData(pixels);
            for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
            return true;
        }

        private JObject SaveStitchedMultiFramePng(PendingMultiFrameCapture job)
        {
            int count = job.CapturedFrames.Count;
            int cellW = job.CapturedFrames[0].Width;
            int cellH = job.CapturedFrames[0].Height;

            int cols, rows;
            if (job.Mode == "ab_compare" || count == 2)
            {
                cols = 2;
                rows = 1;
            }
            else if (count <= 4)
            {
                cols = 2;
                rows = (count + 1) / 2;
            }
            else
            {
                cols = 3;
                rows = (count + 2) / 3;
            }

            const int Gap = 6;
            const int HeaderH = 30;
            int totalW = cols * cellW + (cols - 1) * Gap;
            int totalH = rows * (cellH + HeaderH) + (rows - 1) * Gap;

            Color[] canvas = new Color[totalW * totalH];
            var bgBorder = new Color(22, 24, 30, 255);
            var headerBg = new Color(16, 18, 24, 255);
            Array.Fill(canvas, bgBorder);

            for (int idx = 0; idx < count; idx++)
            {
                int col = idx % cols;
                int row = idx / cols;
                int dstX = col * (cellW + Gap);
                int dstY = row * (cellH + HeaderH + Gap);

                // 顶部信息栏
                FillRectPixels(canvas, totalW, totalH, dstX, dstY, cellW, HeaderH, headerBg);
                Color badgeAccent = (job.Mode == "ab_compare" && idx == 0)
                    ? new Color(180, 190, 205, 255)
                    : new Color(110, 235, 160, 255);
                DrawPixelText5x7(canvas, totalW, totalH, dstX + 10, dstY + 8, job.CapturedFrames[idx].Badge, scale: 2, color: badgeAccent);

                // 拷贝帧图像（若中途分辨率变化则做最近邻采样安全拷贝）
                var frame = job.CapturedFrames[idx];
                int imgTop = dstY + HeaderH;
                for (int y = 0; y < cellH; y++)
                {
                    int srcY = frame.Height == cellH ? y : (y * frame.Height / cellH);
                    int srcRowOff = srcY * frame.Width;
                    int dstRowOff = (imgTop + y) * totalW + dstX;
                    if (frame.Width == cellW)
                    {
                        Array.Copy(frame.Pixels, srcRowOff, canvas, dstRowOff, cellW);
                    }
                    else
                    {
                        for (int x = 0; x < cellW; x++)
                        {
                            int srcX = x * frame.Width / cellW;
                            canvas[dstRowOff + x] = frame.Pixels[srcRowOff + srcX];
                        }
                    }
                }
            }

            string? outDir = Path.GetDirectoryName(job.OutputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            GraphicsDevice gd = Game1.graphics.GraphicsDevice;
            using (var outTex = new Texture2D(gd, totalW, totalH, mipmap: false, SurfaceFormat.Color))
            {
                outTex.SetData(canvas);
                using var fs = new FileStream(job.OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                outTex.SaveAsPng(fs, totalW, totalH);
            }

            var res = new JObject
            {
                ["ok"] = true,
                ["mode"] = job.Mode,
                ["outputPng"] = job.OutputPath,
                ["grid"] = $"{cols}x{rows}",
                ["frameCount"] = count,
                ["width"] = totalW,
                ["height"] = totalH,
                ["labels"] = new JArray(job.CapturedFrames.Select(f => f.Badge))
            };

            if (job.Mode == "ab_compare")
            {
                res["targetMod"] = job.TargetModId;
                res["toggleMechanism"] = job.ToggleMechanism;
                res["toggleMember"] = job.ToggleLabel;
            }
            else
            {
                res["intervalTicks"] = job.IntervalTicks;
                if (job.PanDxPerTick != 0 || job.PanDyPerTick != 0)
                    res["panPerTick"] = $"{job.PanDxPerTick},{job.PanDyPerTick}";
            }

            return res;
        }

        private bool SetupAbCompareToggle(PendingMultiFrameCapture job, string modQuery, string? explicitProp, out string? error)
        {
            error = null;
            IModInfo? modInfo = Helper.ModRegistry.Get(modQuery);
            if (modInfo == null)
            {
                var fuzzy = Helper.ModRegistry.GetAll()
                    .Where(m => m.Manifest.UniqueID.EndsWith("." + modQuery, StringComparison.OrdinalIgnoreCase) ||
                                m.Manifest.Name.Equals(modQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (fuzzy.Count == 1)
                    modInfo = fuzzy[0];
            }

            if (modInfo == null)
            {
                error = $"Mod `{modQuery}` is not loaded in current session.";
                return false;
            }

            job.TargetModId = modInfo.Manifest.UniqueID;
            IMod? modInst = TryGetModInstance(modInfo);

            // 优先寻找模组 Config 上的布尔总开关（同时兼容 SMAPI 事件渲染与 Harmony Patch 渲染！）
            if (modInst != null)
            {
                var (cfgObj, _) = TryLocateModConfigObject(modInst);
                if (cfgObj != null)
                {
                    MemberInfo? boolMember = FindAbBooleanToggleMember(cfgObj.GetType(), explicitProp);
                    if (boolMember != null)
                    {
                        job.ConfigInstance = cfgObj;
                        job.BoolMember = boolMember;
                        job.OrigBoolValue = (bool)(GetMemberValue(boolMember, cfgObj) ?? true);
                        job.ToggleMechanism = "config_bool";
                        job.ToggleLabel = boolMember.Name;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(explicitProp))
            {
                error = $"Boolean config property `{explicitProp}` was not found on mod `{job.TargetModId}`.";
                return false;
            }

            // 兜底方案：若模组没有 Config 布尔总开关，则支持临时挂起其在 SMAPI EventManager 注册的 Display 渲染事件处理器
            job.ToggleMechanism = "display_events";
            job.ToggleLabel = "DisplayEvents";
            return true;
        }

        private static MemberInfo? FindAbBooleanToggleMember(Type cfgType, string? explicitProp)
        {
            if (!string.IsNullOrWhiteSpace(explicitProp))
            {
                var (m, vt, _) = FindReadableWritableMember(cfgType, explicitProp!, requireWrite: true);
                return vt == typeof(bool) ? m : null;
            }

            string[] commonSwitchNames = { "Enabled", "EnableMod", "Enable", "IsEnabled", "ModEnabled", "Active", "EnableEffects", "EnableShader", "EnableLighting" };
            foreach (var cand in commonSwitchNames)
            {
                var (m, vt, _) = FindReadableWritableMember(cfgType, cand, requireWrite: true);
                if (m != null && vt == typeof(bool))
                    return m;
            }

            foreach (var p in cfgType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite &&
                    p.Name.StartsWith("Enable", StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }

        private void SetAbModActiveState(PendingMultiFrameCapture job, bool active)
        {
            if (job.ToggleMechanism == "config_bool" && job.ConfigInstance != null && job.BoolMember != null)
            {
                SetMemberValue(job.BoolMember, job.ConfigInstance, active);
                IModInfo? info = Helper.ModRegistry.Get(job.TargetModId);
                IMod? modInst = info != null ? TryGetModInstance(info) : null;
                if (modInst != null)
                    InvokeOptionalConfigHook(modInst);
                return;
            }

            if (job.ToggleMechanism == "display_events")
            {
                if (!active)
                    SuspendModDisplayEventHandlers(job);
                else
                    RestoreSuspendedDisplayEventHandlers(job);
            }
        }

        private void RestoreAbModOriginalState(PendingMultiFrameCapture job)
        {
            if (job.ToggleMechanism == "config_bool" && job.ConfigInstance != null && job.BoolMember != null)
            {
                SetMemberValue(job.BoolMember, job.ConfigInstance, job.OrigBoolValue);
                IModInfo? info = Helper.ModRegistry.Get(job.TargetModId);
                IMod? modInst = info != null ? TryGetModInstance(info) : null;
                if (modInst != null)
                    InvokeOptionalConfigHook(modInst);
            }
            else if (job.ToggleMechanism == "display_events")
            {
                RestoreSuspendedDisplayEventHandlers(job);
            }
        }

        private void SuspendModDisplayEventHandlers(PendingMultiFrameCapture job)
        {
            if (job.TemporarilyRemovedHandlers.Count > 0)
                return;

            try
            {
                object displayEvents = Helper.Events.Display;
                FieldInfo? evMgrField = displayEvents.GetType().BaseType?.GetField("EventManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object? evMgr = evMgrField?.GetValue(displayEvents);
                if (evMgr == null)
                    return;

                foreach (var f in evMgr.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!f.Name.StartsWith("Rendering", StringComparison.Ordinal) && !f.Name.StartsWith("Rendered", StringComparison.Ordinal))
                        continue;

                    object? managedEv = f.GetValue(evMgr);
                    FieldInfo? handlersField = managedEv?.GetType().GetField("Handlers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (handlersField?.GetValue(managedEv) is not IList handlerList)
                        continue;

                    for (int i = handlerList.Count - 1; i >= 0; i--)
                    {
                        object? entry = handlerList[i];
                        if (entry == null)
                            continue;
                        PropertyInfo? srcModProp = entry.GetType().GetProperty("SourceMod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        IModMetadataIdMatch(srcModProp?.GetValue(entry), job.TargetModId, out bool isMatch);
                        if (isMatch)
                        {
                            job.TemporarilyRemovedHandlers.Add((handlerList, entry, i));
                            handlerList.RemoveAt(i);
                        }
                    }
                }
            }
            catch { }
        }

        private static void IModMetadataIdMatch(object? modMeta, string expectedId, out bool matched)
        {
            matched = false;
            if (modMeta == null)
                return;
            PropertyInfo? mfProp = modMeta.GetType().GetProperty("Manifest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mfProp?.GetValue(modMeta) is IManifest mf && mf.UniqueID.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
                matched = true;
        }

        private static void RestoreSuspendedDisplayEventHandlers(PendingMultiFrameCapture job)
        {
            for (int i = job.TemporarilyRemovedHandlers.Count - 1; i >= 0; i--)
            {
                var (list, entry, origIdx) = job.TemporarilyRemovedHandlers[i];
                int insertPos = Math.Clamp(origIdx, 0, list.Count);
                list.Insert(insertPos, entry);
            }
            job.TemporarilyRemovedHandlers.Clear();
        }

        #endregion

        #region 3. 同桌面可见会话自重启 (`self_restart`) 与无打扰后台隐藏模式 (`--background`)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO_RESTART
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
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
        private struct PROCESS_INFORMATION_RESTART
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
            ref STARTUPINFO_RESTART lpStartupInfo,
            out PROCESS_INFORMATION_RESTART lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT_WIN lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetThreadDesktop(uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserObjectInformationW(IntPtr hObj, int nIndex, StringBuilder pvInfo, int nLength, out int lpnLengthNeeded);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT_WIN
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// 由当前可见桌面中的游戏进程自己在退出前拉起过渡宿主脚本，等待旧进程彻底退出释放 DLL 文件锁后原子替换目录并启动新实例。
        ///
        /// 【当初为什么要写这个功能及为什么用暂存交换】：
        /// 1. 解决跨桌面可见性：AI 终端跑在沙盒桌面，由玩家可见桌面 (`Winsta0\Default`) 中的旧游戏进程自己派生子进程，新窗口才能留在可见桌面；
        /// 2. 解决 Windows DLL 文件锁：旧进程存活时正锁着 `Mods/` 或 `isolated-mods/` 下的 `.dll`，不能直接 `Directory.Delete`！
        ///    因此 CLI 先把新版文件写入无锁的 `.staging` 旁路目录，再由本方法拉起 PowerShell 脚本执行 `Wait-Process -Id <oldPid>`，
        ///    等旧进程完全退出释放全部文件句柄后再覆盖目标目录启动新 SMAPI；
        /// 3. 防抢焦点：设置 `SW_SHOWNOACTIVATE` + 原版 `SDL2.dll` 支持的 `SDL_WINDOW_NO_ACTIVATION_WHEN_SHOWN=1`，刷新画面不打断打字。
        /// </summary>
        private JObject HandleSelfRestart(JObject req)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new JObject { ["ok"] = false, ["error"] = "self_restart is only supported on Windows." };

            string smapiExe = req.Value<string>("smapiExe") ?? Path.Combine(Constants.GamePath, "StardewModdingAPI.exe");
            string gamePath = req.Value<string>("gamePath") ?? Constants.GamePath;
            string? modsPath = req.Value<string>("modsPath");
            string? stagingDir = req.Value<string>("stagingDir");
            string? swapTargetDir = req.Value<string>("swapTargetDir");
            string? swapMode = req.Value<string>("swapMode"); // "replace_dir" | "merge_subdirs"
            string? autoLoadSave = req.Value<string>("autoLoadSave");
            string? clientExe = req.Value<string>("clientExe");
            string? pwshExe = req.Value<string>("pwshExe");
            bool background = req.Value<bool?>("background") == true;
            bool hideConsole = req.Value<bool?>("hideConsole") ?? _hideConsoleOnly;
            bool noFocus = req.Value<bool?>("noFocus") ?? true;
            int myPid = Process.GetCurrentProcess().Id;

            try
            {
                const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
                const uint CREATE_NEW_CONSOLE = 0x00000010;
                const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
                const int STARTF_USESHOWWINDOW = 0x00000001;
                const short SW_SHOWNORMAL = 1;
                const short SW_SHOWNOACTIVATE = 4;
                const short SW_HIDE = 0;

                // 优先使用现代 pwsh.exe，若目标机器未安装 PowerShell 7 则回退至系统自带的 Windows PowerShell 5.1 (powershell.exe)
                string? shellExe = (!string.IsNullOrWhiteSpace(pwshExe) && File.Exists(pwshExe))
                    ? pwshExe!
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");

                if (!File.Exists(shellExe))
                    return new JObject { ["ok"] = false, ["error"] = "Neither pwsh.exe nor powershell.exe found for lock-safe self_restart." };

                // 生成带 UTF-8 BOM 的自清理重启跳板脚本（先等旧 PID 退出释放 DLL 锁，再执行目录替换并拉起新 SMAPI）
                string helperPs1 = Path.Combine(Path.GetTempPath(), $"sdv-self-restart-{Guid.NewGuid():N}.ps1");
                var ps = new StringBuilder();
                ps.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
                if (!string.IsNullOrWhiteSpace(clientExe) && File.Exists(clientExe))
                    ps.AppendLine($"& '{EscapePsSingle(clientExe!)}' --init-console");
                ps.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
                ps.AppendLine($"Wait-Process -Id {myPid} -Timeout 15 -ErrorAction SilentlyContinue");

                if (!string.IsNullOrWhiteSpace(stagingDir) && !string.IsNullOrWhiteSpace(swapTargetDir))
                {
                    string escStage = EscapePsSingle(stagingDir!);
                    string escTarget = EscapePsSingle(swapTargetDir!);
                    if (string.Equals(swapMode, "replace_dir", StringComparison.OrdinalIgnoreCase))
                    {
                        ps.AppendLine($"$stage = '{escStage}'; $target = '{escTarget}'");
                        ps.AppendLine("if (Test-Path -LiteralPath $stage) {");
                        ps.AppendLine("  for ($i = 0; $i -lt 25; $i++) {");
                        ps.AppendLine("    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue }");
                        ps.AppendLine("    if (-not (Test-Path -LiteralPath $target)) { break }");
                        ps.AppendLine("    Start-Sleep -Milliseconds 120");
                        ps.AppendLine("  }");
                        ps.AppendLine("  Move-Item -LiteralPath $stage -Destination $target -Force -ErrorAction SilentlyContinue");
                        ps.AppendLine("}");
                    }
                    else if (string.Equals(swapMode, "merge_subdirs", StringComparison.OrdinalIgnoreCase))
                    {
                        ps.AppendLine($"$stage = '{escStage}'; $target = '{escTarget}'");
                        ps.AppendLine("if (Test-Path -LiteralPath $stage) {");
                        ps.AppendLine("  for ($i = 0; $i -lt 25; $i++) {");
                        ps.AppendLine("    $okCopy = $true");
                        ps.AppendLine("    Get-ChildItem -LiteralPath $stage -Directory | ForEach-Object {");
                        ps.AppendLine("      $dstSub = Join-Path $target $_.Name");
                        ps.AppendLine("      if (Test-Path -LiteralPath $dstSub) { Remove-Item -LiteralPath $dstSub -Recurse -Force -ErrorAction SilentlyContinue }");
                        ps.AppendLine("      Copy-Item -LiteralPath $_.FullName -Destination $dstSub -Recurse -Force -ErrorAction SilentlyContinue");
                        ps.AppendLine("      if (-not $?) { $okCopy = $false }");
                        ps.AppendLine("    }");
                        ps.AppendLine("    if ($okCopy) { break }");
                        ps.AppendLine("    Start-Sleep -Milliseconds 120");
                        ps.AppendLine("  }");
                        ps.AppendLine("  Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue");
                        ps.AppendLine("}");
                    }
                }

                ps.AppendLine("Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue");
                ps.AppendLine("$ErrorActionPreference = 'Continue'");
                ps.Append($"& '{EscapePsSingle(smapiExe)}'");
                if (!string.IsNullOrWhiteSpace(modsPath))
                {
                    string cleanMods = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsPath!));
                    ps.Append($" --mods-path '{EscapePsSingle(cleanMods)}'");
                }
                ps.AppendLine();

                File.WriteAllText(helperPs1, ps.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                var cmdLine = new StringBuilder();
                cmdLine.Append(QuoteWinArg(shellExe))
                       .Append(" -NoLogo -NoProfile -ExecutionPolicy Bypass -File ")
                       .Append(QuoteWinArg(helperPs1));

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

                if (!string.IsNullOrWhiteSpace(autoLoadSave))
                    envDict["SDV_DEV_AUTOLOAD_SAVE"] = autoLoadSave!;
                else
                    envDict.Remove("SDV_DEV_AUTOLOAD_SAVE");

                var envSb = new StringBuilder();
                foreach (var kv in envDict)
                    envSb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
                envSb.Append('\0');
                byte[] envBytes = Encoding.Unicode.GetBytes(envSb.ToString());

                IntPtr envPtr = Marshal.AllocHGlobal(envBytes.Length);
                int childPid;
                try
                {
                    Marshal.Copy(envBytes, 0, envPtr, envBytes.Length);
                    var si = new STARTUPINFO_RESTART
                    {
                        cb = Marshal.SizeOf<STARTUPINFO_RESTART>(),
                        dwFlags = STARTF_USESHOWWINDOW,
                        wShowWindow = (background || hideConsole) ? SW_HIDE : (noFocus ? SW_SHOWNOACTIVATE : SW_SHOWNORMAL)
                    };

                    const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
                    const int ERROR_ACCESS_DENIED = 5;
                    uint baseFlags = CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE | CREATE_NEW_PROCESS_GROUP;
                    string rawCmdLine = cmdLine.ToString();

                    bool created = CreateProcessW(
                        shellExe,
                        cmdLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        baseFlags | CREATE_BREAKAWAY_FROM_JOB,
                        envPtr,
                        gamePath,
                        ref si,
                        out var pi);

                    if (!created && Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED)
                    {
                        cmdLine = new StringBuilder(rawCmdLine);
                        created = CreateProcessW(
                            shellExe,
                            cmdLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            baseFlags,
                            envPtr,
                            gamePath,
                            ref si,
                            out pi);
                    }

                    if (!created)
                    {
                        try { File.Delete(helperPs1); } catch { }
                        return new JObject { ["ok"] = false, ["error"] = $"CreateProcessW failed in self_restart (err={Marshal.GetLastWin32Error()})." };
                    }

                    childPid = pi.dwProcessId;
                    if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                    if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                }
                finally
                {
                    Marshal.FreeHGlobal(envPtr);
                }

                Game1.quit = true;
                return new JObject
                {
                    ["ok"] = true,
                    ["restartedFromPid"] = myPid,
                    ["spawnedHostPid"] = childPid,
                    ["background"] = background,
                    ["noFocus"] = noFocus
                };
            }
            catch (Exception ex)
            {
                return new JObject { ["ok"] = false, ["error"] = ex.Message };
            }
        }

        /// <summary>
        /// 统一执行启动期与运行期窗口可见性策略（覆盖前台正常窗口、`--no-console` 只显示游戏画面不弹控制台、`--background` 全离屏隐藏）：
        ///
        /// 【当初为什么要统一收口窗口控制并增加 `window_control`】：
        /// 1. 之前 AI 在隐藏宿主终端里启动前台窗口时，若父进程 `STARTUPINFO` 为隐藏且未主动对 `SDL_app` 调 `ShowWindow`，会导致 `MainWindowHandle == 0` 隐身；
        /// 2. 玩家希望实机看画面时经常“只想看星露谷游戏画面、不想桌面上多弹一个黑乎乎的 SMAPI 控制台窗口”（`--no-console`）；
        /// 3. 运行中途想在“后台静默跑测试”和“切回桌面肉眼看效果”之间秒切（`sdv-dev window --show | --background | --hide-console`），无需重启游戏！
        /// </summary>
        private void ApplyStartupWindowPolicy()
        {
            if (Environment.GetEnvironmentVariable("SDV_DEV_BACKGROUND") == "1")
                _hiddenBackgroundWindow = true;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            if (_hiddenBackgroundWindow)
            {
                ApplyBackgroundWindowModeIfRequested();
                return;
            }

            try
            {
                const int SW_HIDE = 0;
                const int SW_SHOWNORMAL = 1;
                const int SW_SHOWNOACTIVATE = 4;
                const uint SWP_NOSIZE = 0x0001;
                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_NOACTIVATE = 0x0010;

                IntPtr conHwnd = GetConsoleWindow();
                if (_hideConsoleOnly && conHwnd != IntPtr.Zero)
                {
                    ShowWindow(conHwnd, SW_HIDE);
                }

                IntPtr sdlHwnd = FindCurrentProcessSdlWindow();
                if (sdlHwnd != IntPtr.Zero)
                {
                    if (!IsWindowVisible(sdlHwnd))
                    {
                        ShowWindow(sdlHwnd, _launchNoFocus ? SW_SHOWNOACTIVATE : SW_SHOWNORMAL);
                    }

                    if (GetWindowRect(sdlHwnd, out var rect) && (rect.Left < -10000 || rect.Top < -10000))
                    {
                        SetWindowPos(sdlHwnd, IntPtr.Zero, 80, 60, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    }
                }
            }
            catch { }
        }

        private void ApplyBackgroundWindowModeIfRequested()
        {
            if (! _hiddenBackgroundWindow && Environment.GetEnvironmentVariable("SDV_DEV_BACKGROUND") != "1")
                return;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            _hiddenBackgroundWindow = true;
            try
            {
                const int SW_HIDE = 0;
                const uint SWP_NOSIZE = 0x0001;
                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_NOACTIVATE = 0x0010;

                IntPtr conHwnd = GetConsoleWindow();
                if (conHwnd != IntPtr.Zero)
                    ShowWindow(conHwnd, SW_HIDE);

                IntPtr sdlHwnd = FindCurrentProcessSdlWindow();
                if (sdlHwnd != IntPtr.Zero)
                {
                    SetWindowPos(sdlHwnd, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        private JObject HandleWindowControl(JObject req)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new JObject { ["ok"] = false, ["error"] = "window_control is only supported on Windows." };

            string? mode = req.Value<string>("mode")?.Trim().ToLowerInvariant();
            bool? hideConsoleOpt = req.Value<bool?>("hideConsole");
            bool focus = req.Value<bool?>("focus") == true;

            const int SW_HIDE = 0;
            const int SW_SHOWNORMAL = 1;
            const int SW_SHOWNOACTIVATE = 4;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_SHOWWINDOW = 0x0040;

            if (hideConsoleOpt.HasValue)
            {
                _hideConsoleOnly = hideConsoleOpt.Value;
                Environment.SetEnvironmentVariable("SDV_DEV_HIDE_CONSOLE", _hideConsoleOnly ? "1" : null);
            }

            IntPtr conHwnd = GetConsoleWindow();
            IntPtr sdlHwnd = FindCurrentProcessSdlWindow();

            if (mode == "background" || mode == "hide")
            {
                _hiddenBackgroundWindow = true;
                Environment.SetEnvironmentVariable("SDV_DEV_BACKGROUND", "1");
                if (conHwnd != IntPtr.Zero)
                    ShowWindow(conHwnd, SW_HIDE);
                if (sdlHwnd != IntPtr.Zero)
                    SetWindowPos(sdlHwnd, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            else if (mode == "show" || mode == "visible")
            {
                _hiddenBackgroundWindow = false;
                Environment.SetEnvironmentVariable("SDV_DEV_BACKGROUND", null);

                if (conHwnd != IntPtr.Zero)
                    ShowWindow(conHwnd, _hideConsoleOnly ? SW_HIDE : SW_SHOWNOACTIVATE);

                if (sdlHwnd != IntPtr.Zero)
                {
                    ShowWindow(sdlHwnd, focus ? SW_SHOWNORMAL : SW_SHOWNOACTIVATE);
                    if (GetWindowRect(sdlHwnd, out var rect) && (rect.Left < -10000 || rect.Top < -10000))
                    {
                        SetWindowPos(sdlHwnd, IntPtr.Zero, 80, 60, 0, 0, SWP_NOSIZE | (focus ? 0u : SWP_NOZORDER | SWP_NOACTIVATE) | SWP_SHOWWINDOW);
                    }
                    if (focus)
                        SetForegroundWindow(sdlHwnd);
                }
            }
            else if (hideConsoleOpt.HasValue && conHwnd != IntPtr.Zero)
            {
                Environment.SetEnvironmentVariable("SDV_DEV_HIDE_CONSOLE", _hideConsoleOnly ? "1" : null);
                if (!_hiddenBackgroundWindow)
                    ShowWindow(conHwnd, _hideConsoleOnly ? SW_HIDE : SW_SHOWNOACTIVATE);
            }

            var state = InspectCurrentWindowState();
            state["ok"] = true;
            state["backgroundMode"] = _hiddenBackgroundWindow;
            state["hideConsole"] = _hideConsoleOnly;
            return state;
        }

        private static IntPtr FindCurrentProcessSdlWindow()
        {
            IntPtr found = IntPtr.Zero;
            uint myPid = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint winPid);
                if (winPid != myPid)
                    return true;

                var cls = new StringBuilder(64);
                GetClassNameW(hWnd, cls, 64);
                if (cls.ToString().Equals("SDL_app", StringComparison.Ordinal))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static JObject InspectCurrentWindowState()
        {
            var res = new JObject();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return res;

            try
            {
                IntPtr sdlHwnd = FindCurrentProcessSdlWindow();
                IntPtr conHwnd = GetConsoleWindow();
                bool sdlVisibleFlag = sdlHwnd != IntPtr.Zero && IsWindowVisible(sdlHwnd);
                bool onScreenCoords = false;
                string? boundsStr = null;

                if (sdlHwnd != IntPtr.Zero && GetWindowRect(sdlHwnd, out var r))
                {
                    int w = r.Right - r.Left;
                    int h = r.Bottom - r.Top;
                    boundsStr = $"{r.Left},{r.Top} ({w}x{h})";
                    onScreenCoords = r.Left > -10000 && r.Top > -10000 && w > 0 && h > 0;
                }

                string? desktopName = null;
                IntPtr hDesk = GetThreadDesktop(GetCurrentThreadId());
                if (hDesk != IntPtr.Zero)
                {
                    var sb = new StringBuilder(128);
                    if (GetUserObjectInformationW(hDesk, 2 /* UOI_NAME */, sb, sb.Capacity * 2, out _))
                        desktopName = sb.ToString();
                }

                bool onInteractiveDesktop = string.IsNullOrEmpty(desktopName) ||
                    string.Equals(desktopName, "Default", StringComparison.OrdinalIgnoreCase);

                res["sdlHwnd"] = sdlHwnd != IntPtr.Zero ? $"0x{sdlHwnd.ToInt64():X}" : null;
                res["windowVisible"] = sdlVisibleFlag && onScreenCoords && onInteractiveDesktop;
                res["consoleVisible"] = conHwnd != IntPtr.Zero && IsWindowVisible(conHwnd) && onInteractiveDesktop;
                res["windowBounds"] = boundsStr;
                res["desktopName"] = desktopName;
                res["onInteractiveDesktop"] = onInteractiveDesktop;
            }
            catch { }
            return res;
        }

        private static string EscapePsSingle(string s) => s.Replace("'", "''");

        private static string QuoteWinArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";
            if (arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '\"' }) < 0)
                return arg;
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        #endregion

        #region 4. 轻量无依赖 5x7 像素等宽角标绘制器（用于 A/B 对比与四宫格顶部帧标签）

        private static void FillRectPixels(Color[] canvas, int canvasW, int canvasH, int rx, int ry, int rw, int rh, Color color)
        {
            int x0 = Math.Max(0, rx);
            int y0 = Math.Max(0, ry);
            int x1 = Math.Min(canvasW, rx + rw);
            int y1 = Math.Min(canvasH, ry + rh);
            for (int y = y0; y < y1; y++)
            {
                int row = y * canvasW;
                for (int x = x0; x < x1; x++)
                    canvas[row + x] = color;
            }
        }

        private static void DrawPixelText5x7(Color[] canvas, int canvasW, int canvasH, int startX, int startY, string text, int scale, Color color)
        {
            int curX = startX;
            foreach (char rawCh in text)
            {
                byte[] glyph = GetGlyph5x7(char.ToUpperInvariant(rawCh));
                for (int row = 0; row < 7; row++)
                {
                    byte bits = glyph[row];
                    for (int col = 0; col < 5; col++)
                    {
                        if (((bits >> (4 - col)) & 1) == 0)
                            continue;
                        FillRectPixels(canvas, canvasW, canvasH, curX + col * scale, startY + row * scale, scale, scale, color);
                    }
                }
                curX += 6 * scale;
                if (curX + 6 * scale >= canvasW)
                    break;
            }
        }

        private static byte[] GetGlyph5x7(char c) => c switch
        {
            'A' => new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            'B' => new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            'C' => new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            'D' => new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
            'E' => new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
            'F' => new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            'G' => new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            'H' => new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            'I' => new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            'J' => new byte[] { 0x07, 0x02, 0x02, 0x02, 0x12, 0x12, 0x0C },
            'K' => new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            'L' => new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            'M' => new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
            'N' => new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
            'O' => new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            'P' => new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            'Q' => new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            'R' => new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            'S' => new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            'T' => new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            'U' => new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            'V' => new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            'W' => new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
            'X' => new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            'Y' => new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
            'Z' => new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            '0' => new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            '1' => new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            '2' => new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            '3' => new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            '4' => new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            '5' => new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            '6' => new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            '7' => new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            '8' => new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            '9' => new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            '+' => new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
            '-' => new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
            '=' => new byte[] { 0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00 },
            '/' => new byte[] { 0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10 },
            '|' => new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ':' => new byte[] { 0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00 },
            '.' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            ',' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04, 0x04, 0x08 },
            '_' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F },
            '(' => new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 },
            ')' => new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 },
            ' ' => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            _   => new byte[] { 0x1F, 0x11, 0x02, 0x04, 0x00, 0x04, 0x00 }
        };

        #endregion
    }
}
