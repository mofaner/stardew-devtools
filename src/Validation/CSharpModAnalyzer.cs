using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace SdvKb.Validation
{
    public enum ExpectedJsonKind
    {
        Any = 0,
        String,
        Number,
        Boolean,
        Array,
        Object
    }

    public sealed class EngineFieldRule
    {
        public string Name { get; set; } = string.Empty;
        public string CSharpType { get; set; } = string.Empty;
        public ExpectedJsonKind JsonKind { get; set; }
    }

    public sealed class EngineAssetRule
    {
        public string AssetPath { get; set; } = string.Empty;
        public string ModelTypeName { get; set; } = string.Empty;
        public bool IsListRoot { get; set; }
        public bool IsStringDictionary { get; set; }
        public Dictionary<string, EngineFieldRule> Fields { get; } = new Dictionary<string, EngineFieldRule>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CachedEngineRules
    {
        public string GamePath { get; set; } = string.Empty;
        public long SdvWriteTicks { get; set; }
        public Dictionary<string, EngineAssetRule> AssetRules { get; } = new Dictionary<string, EngineAssetRule>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidGsqNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidTriggerActions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidEventCommands { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidEventPreconditions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidAudioCues { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts 1.6 engine semantic rules directly from Stardew Valley DLLs and performs static analysis
    /// on compiled C# SMAPI mod DLLs + source files (Harmony patch target/injection verification &amp; multiplayer checks).
    /// </summary>
    public static class CSharpModAnalyzer
    {
        private static readonly object CacheLock = new object();
        private static CachedEngineRules? _cachedRules;

        private static readonly Dictionary<string, (string FullModelType, bool IsList)> KnownDataAssetMappings =
            new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Data/Objects"] = ("StardewValley.GameData.Objects.ObjectData", false),
                ["Data/BigCraftables"] = ("StardewValley.GameData.BigCraftables.BigCraftableData", false),
                ["Data/Machines"] = ("StardewValley.GameData.Machines.MachineData", false),
                ["Data/Shops"] = ("StardewValley.GameData.Shops.ShopData", false),
                ["Data/Buildings"] = ("StardewValley.GameData.Buildings.BuildingData", false),
                ["Data/Characters"] = ("StardewValley.GameData.Characters.CharacterData", false),
                ["Data/Locations"] = ("StardewValley.GameData.Locations.LocationData", false),
                ["Data/Crops"] = ("StardewValley.GameData.Crops.CropData", false),
                ["Data/FruitTrees"] = ("StardewValley.GameData.FruitTrees.FruitTreeData", false),
                ["Data/Weapons"] = ("StardewValley.GameData.Weapons.WeaponData", false),
                ["Data/Tools"] = ("StardewValley.GameData.Tools.ToolData", false),
                ["Data/Buffs"] = ("StardewValley.GameData.Buffs.BuffData", false),
                ["Data/Pets"] = ("StardewValley.GameData.Pets.PetData", false),
                ["Data/FarmAnimals"] = ("StardewValley.GameData.FarmAnimals.FarmAnimalData", false),
                ["Data/WildTrees"] = ("StardewValley.GameData.WildTrees.WildTreeData", false),
                ["Data/Minecarts"] = ("StardewValley.GameData.Minecarts.MinecartNetworkData", false),
                ["Data/Powers"] = ("StardewValley.GameData.Powers.PowersData", false),
                ["Data/Pants"] = ("StardewValley.GameData.Pants.PantsData", false),
                ["Data/Shirts"] = ("StardewValley.GameData.Shirts.ShirtData", false),
                ["Data/Trinkets"] = ("StardewValley.GameData.Trinkets.TrinketData", false),
                ["Data/Mannequins"] = ("StardewValley.GameData.Mannequins.MannequinData", false),
                ["Data/AudioChanges"] = ("StardewValley.GameData.Audio.AudioCueData", false),
                ["Data/SpecialOrders"] = ("StardewValley.GameData.SpecialOrders.SpecialOrderData", false),
                ["Data/TriggerActions"] = ("StardewValley.GameData.TriggerActionData", true),
                ["Data/TailoringRecipes"] = ("StardewValley.GameData.Crafting.TailoringCraftItem", true),
                ["Data/MakeoverOutfits"] = ("StardewValley.GameData.MakeoverOutfits.MakeoverOutfit", true)
            };

        private static readonly Dictionary<string, string> Legacy15FieldHints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ParentSheetIndex"] = "Use `SpriteIndex` (and optional `Texture`) in Stardew Valley 1.6.",
                ["Edibility"] = "Check spelling/casing; in `Data/Objects` it is `Edibility` (int), not available on `Data/BigCraftables`.",
                ["ObjectInternalName"] = "Use `Name` in Stardew Valley 1.6 `GameData`.",
                ["Portrait"] = "Character portraits use `Maps/` / `Portraits/<Name>` asset path or `CustomFields`."
            };

        private static readonly HashSet<string> SafeStaticFieldTypeTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "IMonitor", "IModHelper", "IManifest", "Harmony", "PerScreen",
                "Regex", "JsonSerializerOptions", "JsonDocumentOptions", "Random",
                "ConcurrentDictionary", "ConcurrentQueue", "ConcurrentBag", "Object",
                "Encoding", "StringComparer", "SemaphoreSlim", "Mutex"
            };

        public static CachedEngineRules? TryGetEngineRules(string? gamePath)
        {
            string? resolvedGame = ResolveGamePath(gamePath);
            if (string.IsNullOrWhiteSpace(resolvedGame))
                return null;

            string sdvDll = Path.Combine(resolvedGame!, "Stardew Valley.dll");
            string gdDll = Path.Combine(resolvedGame!, "StardewValley.GameData.dll");
            if (!File.Exists(sdvDll))
                return null;

            long ticks = File.GetLastWriteTimeUtc(sdvDll).Ticks;
            lock (CacheLock)
            {
                if (_cachedRules != null &&
                    _cachedRules.GamePath.Equals(resolvedGame, StringComparison.OrdinalIgnoreCase) &&
                    _cachedRules.SdvWriteTicks == ticks)
                {
                    return _cachedRules;
                }

                var built = new CachedEngineRules
                {
                    GamePath = resolvedGame!,
                    SdvWriteTicks = ticks
                };

                try
                {
                    PopulateEngineRules(resolvedGame!, sdvDll, gdDll, built);
                    _cachedRules = built;
                    return built;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string? ResolveGamePath(string? explicitGamePath)
        {
            if (!string.IsNullOrWhiteSpace(explicitGamePath) && Directory.Exists(explicitGamePath))
                return Path.GetFullPath(explicitGamePath!);

            var cfg = KbConfig.Load();
            if (!string.IsNullOrWhiteSpace(cfg.GamePath) && Directory.Exists(cfg.GamePath))
                return Path.GetFullPath(cfg.GamePath);

            return null;
        }

        private static void PopulateEngineRules(string gamePath, string sdvDll, string gdDll, CachedEngineRules rules)
        {
            var resolver = new UniversalAssemblyResolver(sdvDll, throwOnError: false, ".NETCoreApp,Version=v6.0");
            resolver.AddSearchDirectory(gamePath);
            string smapiInternal = Path.Combine(gamePath, "smapi-internal");
            if (Directory.Exists(smapiInternal))
                resolver.AddSearchDirectory(smapiInternal);

            var settings = new DecompilerSettings(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false };

            using (var sdvPe = new PEFile(sdvDll, PEStreamOptions.PrefetchEntireImage))
            {
                var sdvDec = new CSharpDecompiler(sdvPe, resolver, settings);
                var mainMod = sdvDec.TypeSystem.MainModule;

                // 1. GSQ names + OtherNames aliases
                rules.ValidGsqNames.Add("TRUE");
                rules.ValidGsqNames.Add("FALSE");
                rules.ValidGsqNames.Add("ANY");
                rules.ValidGsqNames.Add("ALL");
                CollectCommandMethodsAndAliases(
                    mainMod.GetTypeDefinition(new FullTypeName("StardewValley.GameStateQuery+DefaultResolvers")),
                    rules.ValidGsqNames);

                // 2. TriggerAction names + OtherNames aliases
                rules.ValidTriggerActions.Add("Null");
                CollectCommandMethodsAndAliases(
                    mainMod.GetTypeDefinition(new FullTypeName("StardewValley.Triggers.TriggerActionManager+DefaultActions")),
                    rules.ValidTriggerActions);

                // 3. Event commands + OtherNames aliases + special control tokens
                foreach (var builtin in new[] { "continue", "none", "null", "end", "pause" })
                    rules.ValidEventCommands.Add(builtin);
                CollectCommandMethodsAndAliases(
                    mainMod.GetTypeDefinition(new FullTypeName("StardewValley.Event+DefaultCommands")),
                    rules.ValidEventCommands);

                // 4. Event preconditions + OtherNames aliases
                CollectCommandMethodsAndAliases(
                    mainMod.GetTypeDefinition(new FullTypeName("StardewValley.Event+Preconditions")),
                    rules.ValidEventPreconditions);

                // 5. Vanilla XACT Sound Bank & Data/AudioChanges Cues
                foreach (var cue in SdvKb.Indexers.VanillaDataIndexer.ExtractVanillaAudioCues(gamePath))
                {
                    rules.ValidAudioCues.Add(cue.Name);
                }
                foreach (var specialTrack in new[] { "none", "continue", "-1", "ambient", "silence" })
                {
                    rules.ValidAudioCues.Add(specialTrack);
                }
            }

            if (File.Exists(gdDll))
            {
                using var gdPe = new PEFile(gdDll, PEStreamOptions.PrefetchEntireImage);
                var gdDec = new CSharpDecompiler(gdPe, resolver, settings);
                var gdMod = gdDec.TypeSystem.MainModule;

                foreach (var kv in KnownDataAssetMappings)
                {
                    var typeDef = gdMod.GetTypeDefinition(new FullTypeName(kv.Value.FullModelType));
                    if (typeDef == null)
                        continue;

                    var assetRule = new EngineAssetRule
                    {
                        AssetPath = kv.Key,
                        ModelTypeName = typeDef.Name,
                        IsListRoot = kv.Value.IsList
                    };

                    var hierarchy = new List<ITypeDefinition>();
                    IType? cur = typeDef;
                    while (cur != null)
                    {
                        var d = cur.GetDefinition();
                        if (d == null || d.FullName == "System.Object")
                            break;
                        hierarchy.Add(d);
                        cur = d.DirectBaseTypes.FirstOrDefault(b => b.Kind == TypeKind.Class);
                    }

                    foreach (var td in hierarchy)
                    {
                        foreach (var p in td.Properties.Where(p => p.Accessibility == Accessibility.Public && !p.IsStatic))
                        {
                            assetRule.Fields[p.Name] = new EngineFieldRule
                            {
                                Name = p.Name,
                                CSharpType = p.ReturnType.Name,
                                JsonKind = MapTypeToJsonKind(p.ReturnType)
                            };
                        }
                        foreach (var f in td.Fields.Where(f => f.Accessibility == Accessibility.Public && !f.IsStatic && !f.IsConst))
                        {
                            assetRule.Fields[f.Name] = new EngineFieldRule
                            {
                                Name = f.Name,
                                CSharpType = f.ReturnType.Name,
                                JsonKind = MapTypeToJsonKind(f.ReturnType)
                            };
                        }
                    }

                    rules.AssetRules[kv.Key] = assetRule;
                }
            }
        }

        private static void CollectCommandMethodsAndAliases(ITypeDefinition? container, HashSet<string> dest)
        {
            if (container == null)
                return;

            foreach (var m in container.Methods.Where(m => m.Accessibility == Accessibility.Public && m.IsStatic && !m.IsConstructor))
            {
                dest.Add(m.Name);
                foreach (var attr in m.GetAttributes())
                {
                    if (!attr.AttributeType.Name.Equals("OtherNamesAttribute", StringComparison.Ordinal))
                        continue;
                    foreach (var arg in attr.FixedArguments)
                    {
                        if (arg.Value is CustomAttributeTypedArgument<IType>[] arr)
                        {
                            foreach (var el in arr)
                            {
                                if (el.Value is string s && !string.IsNullOrWhiteSpace(s))
                                    dest.Add(s);
                            }
                        }
                        else if (arg.Value is string single && !string.IsNullOrWhiteSpace(single))
                        {
                            dest.Add(single);
                        }
                    }
                }
            }
        }

        private static ExpectedJsonKind MapTypeToJsonKind(IType t)
        {
            if (t.FullName == "System.Nullable" && t is ParameterizedType pt && pt.TypeArguments.Count == 1)
                t = pt.TypeArguments[0];

            string fn = t.FullName;
            if (fn == "System.String")
                return ExpectedJsonKind.String;
            if (fn == "System.Boolean")
                return ExpectedJsonKind.Boolean;
            if (fn == "System.Int32" || fn == "System.Int64" || fn == "System.Single" || fn == "System.Double" || fn == "System.Byte" || fn == "System.Int16")
                return ExpectedJsonKind.Number;
            if (t.Kind == TypeKind.Enum)
                return ExpectedJsonKind.Any; // Newtonsoft allows string or integer for enums
            if (t.Kind == TypeKind.Array)
                return ExpectedJsonKind.Array;

            if (fn.StartsWith("System.Collections.Generic.List", StringComparison.Ordinal) ||
                fn.StartsWith("System.Collections.Generic.HashSet", StringComparison.Ordinal) ||
                fn.StartsWith("System.Collections.Generic.IList", StringComparison.Ordinal) ||
                fn.StartsWith("System.Collections.Generic.IEnumerable", StringComparison.Ordinal))
            {
                return ExpectedJsonKind.Array;
            }

            if (fn.StartsWith("System.Collections.Generic.Dictionary", StringComparison.Ordinal) ||
                fn.StartsWith("System.Collections.Generic.IDictionary", StringComparison.Ordinal))
            {
                return ExpectedJsonKind.Object;
            }

            if (t.Kind == TypeKind.Class || t.Kind == TypeKind.Struct)
                return ExpectedJsonKind.Object;

            return ExpectedJsonKind.Any;
        }

        public static string? GetLegacyFieldHint(string fieldName)
        {
            return Legacy15FieldHints.TryGetValue(fieldName, out var hint) ? hint : null;
        }

        /// <summary>
        /// Analyzes a C# SMAPI mod project:
        /// 1. Scans compiled EntryDll (if present) for [HarmonyPatch] target existence, overload ambiguity,
        ///    and parameter/field injection (`__instance`, `__result`, `___field`, named args) against Stardew Valley 1.6 DLLs;
        /// 2. Scans C# mod classes &amp; source files for split-screen / multiplayer static state anti-patterns.
        /// </summary>
        public static void AnalyzeCSharpMod(
            string modDir,
            string? entryDllRelative,
            string? explicitGamePath,
            List<ValidationIssue> issues)
        {
            string? gamePath = ResolveGamePath(explicitGamePath);
            string[] csFiles = Directory.Exists(modDir)
                ? Directory.GetFiles(modDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string rel = f.Substring(modDir.Length);
                        return !rel.Contains("\\obj\\") && !rel.Contains("\\bin\\") && !rel.Contains("/obj/") && !rel.Contains("/bin/");
                    })
                    .ToArray()
                : Array.Empty<string>();

            // Source-level multiplayer check (lightweight & works even before first build)
            AnalyzeCSharpSourcesForMultiplayer(modDir, csFiles, issues);

            if (string.IsNullOrWhiteSpace(entryDllRelative) || string.IsNullOrWhiteSpace(gamePath))
                return;

            string dllPath = Path.Combine(modDir, entryDllRelative!);
            if (!File.Exists(dllPath))
                return;

            AnalyzeCompiledDllHarmonyAndStatics(dllPath, Path.GetFileName(dllPath), gamePath!, issues);
        }

        public static List<ValidationIssue> VerifyHarmonyPatchesInDll(string dllPath, string gamePath)
        {
            var list = new List<ValidationIssue>();
            if (File.Exists(dllPath) && Directory.Exists(gamePath))
            {
                AnalyzeCompiledDllHarmonyAndStatics(dllPath, Path.GetFileName(dllPath), gamePath, list);
            }
            return list;
        }

        private static void AnalyzeCSharpSourcesForMultiplayer(string modDir, string[] csFiles, List<ValidationIssue> issues)
        {
            if (csFiles.Length == 0)
                return;

            bool mentionsIsMainPlayer = false;
            var dayHookMutators = new List<string>();

            foreach (var file in csFiles)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                if (text.Contains("Context.IsMainPlayer") || text.Contains("IsMainPlayer"))
                    mentionsIsMainPlayer = true;

                if ((text.Contains("GameLoop.DayStarted") || text.Contains("GameLoop.Saving") || text.Contains("GameLoop.DayEnding")) &&
                    (text.Contains("Game1.locations") || text.Contains(".terrainFeatures") || text.Contains(".objects.Add") || text.Contains("Game1.MasterPlayer")))
                {
                    string rel = file.Substring(modDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    dayHookMutators.Add(rel);
                }
            }

            if (!mentionsIsMainPlayer && dayHookMutators.Count > 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = dayHookMutators[0],
                    Message = "Multiplayer check: Lifecycle handler mutates shared world/location state without checking `Context.IsMainPlayer` (farmhands may duplicate world changes or desync)."
                });
            }
        }

        public static List<ValidationIssue> VerifyHarmonyPatchesInDll(string modDllPath, string gamePath, string? displayFile = null)
        {
            var issues = new List<ValidationIssue>();
            if (string.IsNullOrWhiteSpace(modDllPath) || !File.Exists(modDllPath) || string.IsNullOrWhiteSpace(gamePath))
                return issues;

            AnalyzeCompiledDllHarmonyAndStatics(
                modDllPath,
                displayFile ?? Path.GetFileName(modDllPath),
                gamePath,
                issues);
            return issues;
        }

        private static void AnalyzeCompiledDllHarmonyAndStatics(
            string modDllPath,
            string displayFile,
            string gamePath,
            List<ValidationIssue> issues)
        {
            string sdvDll = Path.Combine(gamePath, "Stardew Valley.dll");
            if (!File.Exists(sdvDll))
                return;

            try
            {
                var resolver = new UniversalAssemblyResolver(modDllPath, throwOnError: false, ".NETCoreApp,Version=v6.0");
                resolver.AddSearchDirectory(Path.GetDirectoryName(modDllPath)!);
                resolver.AddSearchDirectory(gamePath);
                string smapiInternal = Path.Combine(gamePath, "smapi-internal");
                if (Directory.Exists(smapiInternal))
                    resolver.AddSearchDirectory(smapiInternal);

                var settings = new DecompilerSettings(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false };

                using var modPe = new PEFile(modDllPath, PEStreamOptions.PrefetchEntireImage);
                using var sdvPe = new PEFile(sdvDll, PEStreamOptions.PrefetchEntireImage);

                var modDec = new CSharpDecompiler(modPe, resolver, settings);
                var sdvDec = new CSharpDecompiler(sdvPe, resolver, settings);

                PEFile? smapiPe = null;
                CSharpDecompiler? smapiDec = null;
                string smapiDll = Path.Combine(gamePath, "StardewModdingAPI.dll");
                if (File.Exists(smapiDll))
                {
                    smapiPe = new PEFile(smapiDll, PEStreamOptions.PrefetchEntireImage);
                    smapiDec = new CSharpDecompiler(smapiPe, resolver, settings);
                }

                using (smapiPe)
                {
                    foreach (var type in modDec.TypeSystem.MainModule.TypeDefinitions)
                    {
                        if (type.Name == "<Module>" || type.Name.StartsWith("<", StringComparison.Ordinal))
                            continue;

                        // 1. Check mutable non-PerScreen static fields for split-screen multiplayer safety
                        CheckStaticFieldsOnType(type, displayFile, issues);

                        // 2. Check [HarmonyPatch] on class + methods
                        var classPatchAttrs = GetHarmonyPatchAttributes(type.GetAttributes());
                        foreach (var method in type.Methods)
                        {
                            var methodPatchAttrs = GetHarmonyPatchAttributes(method.GetAttributes());
                            if (classPatchAttrs.Count == 0 && methodPatchAttrs.Count == 0)
                                continue;

                            bool isPatchCallback = methodPatchAttrs.Count > 0 ||
                                                   HasAnyHarmonyRoleAttribute(method) ||
                                                   IsConventionalHarmonyMethodName(method.Name);

                            if (!isPatchCallback)
                                continue;

                            if (method.Name == "TargetMethod" || method.Name == "TargetMethods" || method.Name == "Prepare" || method.Name == "Cleanup")
                                continue;

                            var merged = MergeHarmonySpec(classPatchAttrs, methodPatchAttrs);
                            if (merged.HasTargetMethodDynamic)
                                continue; // Uses dynamic [HarmonyTargetMethod]

                            ValidateSingleHarmonyPatch(type, method, merged, sdvDec, smapiDec, displayFile, issues);
                        }
                    }
                }
            }
            catch
            {
                // Ignore corrupt/locked intermediate DLL edge cases during check
            }
        }

        private static void CheckStaticFieldsOnType(ITypeDefinition type, string displayFile, List<ValidationIssue> issues)
        {
            if (type.Kind != TypeKind.Class)
                return;

            // Skip generated GMCM / Eval / Compiler types
            if (type.Namespace == "SdvEvalDynamic" || type.Name.EndsWith("Attribute", StringComparison.Ordinal))
                return;

            foreach (var f in type.Fields)
            {
                if (!f.IsStatic || f.IsReadOnly || f.IsConst || f.Name.StartsWith("<", StringComparison.Ordinal))
                    continue;

                string retName = f.ReturnType.Name;
                string retFull = f.ReturnType.FullName;

                if (SafeStaticFieldTypeTokens.Any(tok => retName.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                // Singleton Instance or ModConfig is common, though UI/Farmer/Menu/Gameplay state in static breaks split-screen
                if (f.Name.Equals("Instance", StringComparison.OrdinalIgnoreCase) ||
                    retName.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (retFull.StartsWith("StardewValley.", StringComparison.Ordinal) ||
                    retFull == "System.Boolean" || retFull == "System.Int32" || retFull == "System.Single" ||
                    retFull.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal) ||
                    retFull.StartsWith("System.Collections.Generic.List", StringComparison.Ordinal) ||
                    retFull.StartsWith("System.Collections.Generic.Dictionary", StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "WARN",
                        File = $"{displayFile}#{type.Name}.{f.Name}",
                        Message = $"Split-screen multiplayer hazard: mutable static field `static {retName} {f.Name}` is shared across all local split-screen players. Wrap per-player UI/world state in `PerScreen<{retName}>`."
                    });
                }
            }
        }

        private sealed class HarmonyPatchSpec
        {
            public string? TargetTypeFullName { get; set; }
            public string? MethodName { get; set; }
            public int MethodType { get; set; } // 0=Normal, 1=Getter, 2=Setter, 3=Constructor, 4=StaticConstructor, 5=Enumerator, 6=Async
            public List<string>? ArgumentTypeNames { get; set; }
            public bool HasTargetMethodDynamic { get; set; }
        }

        private static List<IAttribute> GetHarmonyPatchAttributes(IEnumerable<IAttribute> attrs)
        {
            return attrs.Where(a =>
                a.AttributeType.Name.Equals("HarmonyPatch", StringComparison.Ordinal) ||
                a.AttributeType.Name.Equals("HarmonyPatchAttribute", StringComparison.Ordinal))
                .ToList();
        }

        private static bool HasAnyHarmonyRoleAttribute(IMethod m)
        {
            return m.GetAttributes().Any(a =>
            {
                string n = a.AttributeType.Name;
                return n.StartsWith("HarmonyPrefix", StringComparison.Ordinal) ||
                       n.StartsWith("HarmonyPostfix", StringComparison.Ordinal) ||
                       n.StartsWith("HarmonyTranspiler", StringComparison.Ordinal) ||
                       n.StartsWith("HarmonyFinalizer", StringComparison.Ordinal);
            });
        }

        private static bool IsConventionalHarmonyMethodName(string name)
        {
            return name == "Prefix" || name == "Postfix" || name == "Transpiler" || name == "Finalizer";
        }

        private static HarmonyPatchSpec MergeHarmonySpec(List<IAttribute> classAttrs, List<IAttribute> methodAttrs)
        {
            var spec = new HarmonyPatchSpec();
            foreach (var a in classAttrs.Concat(methodAttrs))
            {
                foreach (var arg in a.FixedArguments)
                {
                    if (arg.Type.FullName == "System.Type" && arg.Value is IType targetType)
                    {
                        spec.TargetTypeFullName = targetType.FullName;
                    }
                    else if (arg.Type.FullName == "System.String" && arg.Value is string s)
                    {
                        // First string if no TargetType yet AND looks like a dotted namespace type might be type name, otherwise method name
                        if (spec.TargetTypeFullName == null && s.Contains(".") && char.IsUpper(s[0]))
                            spec.TargetTypeFullName = s;
                        else
                            spec.MethodName = s;
                    }
                    else if (arg.Type.Name == "MethodType" && arg.Value != null)
                    {
                        spec.MethodType = Convert.ToInt32(arg.Value);
                    }
                    else if (arg.Type is ArrayType arrType && arrType.ElementType.FullName == "System.Type" &&
                             arg.Value is CustomAttributeTypedArgument<IType>[] typeArr)
                    {
                        spec.ArgumentTypeNames = typeArr
                            .Select(x => (x.Value as IType)?.FullName ?? string.Empty)
                            .ToList();
                    }
                }

                foreach (var named in a.NamedArguments)
                {
                    if (named.Name == "declaringType" && named.Value is IType dt)
                        spec.TargetTypeFullName = dt.FullName;
                    else if (named.Name == "methodName" && named.Value is string mn)
                        spec.MethodName = mn;
                    else if (named.Name == "methodType" && named.Value != null)
                        spec.MethodType = Convert.ToInt32(named.Value);
                }
            }

            // Check if class contains a [HarmonyTargetMethod] / TargetMethod()
            if (classAttrs.Count > 0 && spec.MethodName == null && spec.MethodType == 0)
            {
                // Could be resolved by TargetMethod() on the patch class; caller checks methods
            }

            return spec;
        }

        private static void ValidateSingleHarmonyPatch(
            ITypeDefinition patchClass,
            IMethod patchMethod,
            HarmonyPatchSpec spec,
            CSharpDecompiler sdvDec,
            CSharpDecompiler? smapiDec,
            string displayFile,
            List<ValidationIssue> issues)
        {
            // Skip if class defines dynamic TargetMethod / TargetMethods
            if (patchClass.Methods.Any(m => m.Name == "TargetMethod" || m.Name == "TargetMethods" ||
                                            m.GetAttributes().Any(a => a.AttributeType.Name.StartsWith("HarmonyTargetMethod", StringComparison.Ordinal))))
            {
                return;
            }

            string locLabel = $"{displayFile}#{patchClass.Name}.{patchMethod.Name}";

            if (!patchMethod.IsStatic)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = locLabel,
                    Message = $"Harmony patch method `{patchClass.Name}.{patchMethod.Name}` must be `static`."
                });
            }

            if (string.IsNullOrWhiteSpace(spec.TargetTypeFullName))
                return;

            // Only validate targets in StardewValley* or StardewModdingAPI* (third-party mod targets may live in other DLLs)
            bool isVanillaTarget = spec.TargetTypeFullName!.StartsWith("StardewValley", StringComparison.Ordinal) ||
                                   spec.TargetTypeFullName!.StartsWith("Netcode", StringComparison.Ordinal);
            bool isSmapiTarget = spec.TargetTypeFullName!.StartsWith("StardewModdingAPI", StringComparison.Ordinal);
            if (!isVanillaTarget && !isSmapiTarget)
                return;

            ITypeDefinition? targetType = ResolveTypeInDecompiler(isSmapiTarget ? smapiDec : sdvDec, spec.TargetTypeFullName!);
            if (targetType == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = locLabel,
                    Message = $"Harmony target type `{spec.TargetTypeFullName}` does not exist in Stardew Valley 1.6."
                });
                return;
            }

            List<IMethod> candidates;
            string memberDesc;

            if (spec.MethodType == 3) // Constructor
            {
                memberDesc = $"{targetType.Name}..ctor";
                candidates = targetType.Methods.Where(m => m.IsConstructor && !m.IsStatic).ToList();
            }
            else if (spec.MethodType == 4) // StaticConstructor
            {
                memberDesc = $"{targetType.Name}..cctor";
                candidates = targetType.Methods.Where(m => m.IsConstructor && m.IsStatic).ToList();
            }
            else if (spec.MethodType == 1) // Getter
            {
                if (string.IsNullOrWhiteSpace(spec.MethodName))
                    return;
                memberDesc = $"{targetType.Name}.{spec.MethodName} (getter)";
                var prop = FindPropertyInHierarchy(targetType, spec.MethodName!);
                candidates = prop?.CanGet == true && prop.Getter != null ? new List<IMethod> { prop.Getter } : new List<IMethod>();
            }
            else if (spec.MethodType == 2) // Setter
            {
                if (string.IsNullOrWhiteSpace(spec.MethodName))
                    return;
                memberDesc = $"{targetType.Name}.{spec.MethodName} (setter)";
                var prop = FindPropertyInHierarchy(targetType, spec.MethodName!);
                candidates = prop?.CanSet == true && prop.Setter != null ? new List<IMethod> { prop.Setter } : new List<IMethod>();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(spec.MethodName))
                    return;
                memberDesc = $"{targetType.Name}.{spec.MethodName}";
                candidates = FindMethodsInHierarchy(targetType, spec.MethodName!);
            }

            if (candidates.Count == 0)
            {
                var similar = targetType.Methods
                    .Where(m => !m.IsConstructor && !m.IsAccessor &&
                                m.Name.IndexOf(spec.MethodName ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => m.Name)
                    .Distinct()
                    .Take(4)
                    .ToList();
                string hint = similar.Count > 0 ? $" Did you mean: `{string.Join("`, `", similar)}`?" : "";
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = locLabel,
                    Message = $"Harmony target `{memberDesc}` not found on `{targetType.FullName}`.{hint}"
                });
                return;
            }

            IMethod? matchedTarget = null;
            if (spec.ArgumentTypeNames != null)
            {
                matchedTarget = candidates.FirstOrDefault(c => ParametersMatch(c, spec.ArgumentTypeNames));
                if (matchedTarget == null)
                {
                    string expectedSig = string.Join(", ", spec.ArgumentTypeNames);
                    string actualSigs = string.Join(" | ", candidates.Select(FormatShortParamList));
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = locLabel,
                        Message = $"Harmony overload `{memberDesc}({expectedSig})` not found. Available overload(s): {actualSigs}"
                    });
                    return;
                }
            }
            else if (candidates.Count > 1)
            {
                string actualSigs = string.Join(" | ", candidates.Select(FormatShortParamList));
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = locLabel,
                    Message = $"Harmony `AmbiguousMatchException`: `{memberDesc}` has {candidates.Count} overloads ({actualSigs}). Specify `new Type[] {{ ... }}` in `[HarmonyPatch]`!"
                });
                return;
            }
            else
            {
                matchedTarget = candidates[0];
            }

            // Transpilers receive IEnumerable<CodeInstruction> / ILGenerator / MethodBase, not target method parameters
            bool isTranspiler = patchMethod.Name == "Transpiler" ||
                                patchMethod.GetAttributes().Any(a => a.AttributeType.Name.StartsWith("HarmonyTranspiler", StringComparison.Ordinal));
            if (isTranspiler)
                return;

            // Verify Harmony injected parameters on Prefix / Postfix / Finalizer!
            foreach (var p in patchMethod.Parameters)
            {
                string pName = p.Name;

                // Check [HarmonyArgument("origName")] or [HarmonyArgument(index)]
                var argAttr = p.GetAttributes().FirstOrDefault(a => a.AttributeType.Name.StartsWith("HarmonyArgument", StringComparison.Ordinal));
                if (argAttr != null && argAttr.FixedArguments.Length > 0)
                {
                    if (argAttr.FixedArguments[0].Value is string mappedName)
                        pName = mappedName;
                    else if (argAttr.FixedArguments[0].Value is int mappedIdx)
                    {
                        if (mappedIdx < 0 || mappedIdx >= matchedTarget.Parameters.Count)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = locLabel,
                                Message = $"Harmony `[HarmonyArgument({mappedIdx})]` on `{p.Name}` is out of range for `{memberDesc}` ({matchedTarget.Parameters.Count} parameter(s))."
                            });
                        }
                        continue;
                    }
                }

                if (pName == "__instance")
                {
                    if (matchedTarget.IsStatic)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = locLabel,
                            Message = $"Harmony injection error: `{memberDesc}` is `static`, so `__instance` cannot be injected."
                        });
                    }
                    continue;
                }

                if (pName == "__result" || pName == "__resultRef")
                {
                    if (matchedTarget.ReturnType.Kind == TypeKind.Void && !matchedTarget.IsConstructor)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = locLabel,
                            Message = $"Harmony injection error: `{memberDesc}` returns `void`, so `{pName}` cannot be injected."
                        });
                    }
                    continue;
                }

                if (pName.StartsWith("___", StringComparison.Ordinal) && pName.Length > 3)
                {
                    string fieldName = pName.Substring(3);
                    var field = FindFieldInHierarchy(targetType, fieldName);
                    if (field == null)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = locLabel,
                            Message = $"Harmony field injection `{pName}` failed: field `{fieldName}` does not exist on `{targetType.FullName}` (or its base classes)."
                        });
                    }
                    continue;
                }

                if (pName.StartsWith("__", StringComparison.Ordinal))
                {
                    // Built-in Harmony injections (__state, __args, __originalMethod, __runOriginal, __exception, __0, __1...)
                    if (pName == "__state" || pName == "__args" || pName == "__originalMethod" ||
                        pName == "__runOriginal" || pName == "__exception" || Regex.IsMatch(pName, @"^__\d+$"))
                    {
                        if (Regex.IsMatch(pName, @"^__\d+$") && int.TryParse(pName.Substring(2), out int posIdx))
                        {
                            if (posIdx >= matchedTarget.Parameters.Count)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = locLabel,
                                    Message = $"Harmony positional parameter `{pName}` is out of range for `{memberDesc}` ({matchedTarget.Parameters.Count} parameter(s))."
                                });
                            }
                        }
                        continue;
                    }
                }

                // Regular parameter name must match an exact parameter name on matchedTarget!
                var targetParam = matchedTarget.Parameters.FirstOrDefault(tp => tp.Name.Equals(pName, StringComparison.Ordinal));
                if (targetParam == null)
                {
                    string validNames = matchedTarget.Parameters.Count == 0
                        ? "(none)"
                        : string.Join(", ", matchedTarget.Parameters.Select(tp => $"`{tp.Type.Name} {tp.Name}`"));
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = locLabel,
                        Message = $"Harmony parameter `{pName}` not found on `{memberDesc}`! Target parameter names are: {validNames}."
                    });
                }
            }
        }

        private static ITypeDefinition? ResolveTypeInDecompiler(CSharpDecompiler? dec, string fullName)
        {
            if (dec == null)
                return null;
            string normalized = fullName.Replace('/', '+');
            var direct = dec.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName(normalized));
            if (direct != null)
                return direct;
            return dec.TypeSystem.MainModule.TypeDefinitions.FirstOrDefault(t =>
                t.FullName.Equals(fullName, StringComparison.Ordinal) ||
                t.ReflectionName.Equals(normalized, StringComparison.Ordinal));
        }

        private static List<IMethod> FindMethodsInHierarchy(ITypeDefinition type, string methodName)
        {
            var cur = type;
            while (cur != null)
            {
                var found = cur.Methods.Where(m => !m.IsConstructor && m.Name.Equals(methodName, StringComparison.Ordinal)).ToList();
                if (found.Count > 0)
                    return found;
                cur = cur.DirectBaseTypes.FirstOrDefault(b => b.Kind == TypeKind.Class)?.GetDefinition();
            }
            return new List<IMethod>();
        }

        private static IProperty? FindPropertyInHierarchy(ITypeDefinition type, string propName)
        {
            var cur = type;
            while (cur != null)
            {
                var prop = cur.Properties.FirstOrDefault(p => p.Name.Equals(propName, StringComparison.Ordinal));
                if (prop != null)
                    return prop;
                cur = cur.DirectBaseTypes.FirstOrDefault(b => b.Kind == TypeKind.Class)?.GetDefinition();
            }
            return null;
        }

        private static IField? FindFieldInHierarchy(ITypeDefinition type, string fieldName)
        {
            var cur = type;
            while (cur != null)
            {
                var field = cur.Fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.Ordinal));
                if (field != null)
                    return field;
                cur = cur.DirectBaseTypes.FirstOrDefault(b => b.Kind == TypeKind.Class)?.GetDefinition();
            }
            return null;
        }

        private static bool ParametersMatch(IMethod method, List<string> expectedTypes)
        {
            if (method.Parameters.Count != expectedTypes.Count)
                return false;
            for (int i = 0; i < expectedTypes.Count; i++)
            {
                string actual = StripRefType(method.Parameters[i].Type.FullName);
                string exp = StripRefType(expectedTypes[i]);
                if (!actual.Equals(exp, StringComparison.Ordinal) &&
                    !method.Parameters[i].Type.Name.Equals(exp, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static string StripRefType(string name) => name.TrimEnd('&');

        private static string FormatShortParamList(IMethod m)
        {
            return "(" + string.Join(", ", m.Parameters.Select(p => $"{p.Type.Name} {p.Name}")) + ")";
        }
    }
}
