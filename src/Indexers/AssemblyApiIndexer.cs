using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using SdvKb.Database;

namespace SdvKb.Indexers
{
    public sealed class SchemaFieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class AssemblyIndexResult
    {
        public List<ApiRecord> ApiRecords { get; } = new List<ApiRecord>();
        public List<SchemaRecord> SchemaRecords { get; } = new List<SchemaRecord>();
        public List<DocRecord> EngineDocs { get; } = new List<DocRecord>();
        public string GameVersion { get; set; } = string.Empty;
        public string SmapiVersion { get; set; } = string.Empty;
    }

    public static class AssemblyApiIndexer
    {
        // 【当初为什么要扩展这些程序集】：
        // 早期仅索引了 `Stardew Valley.dll`、`StardewModdingAPI.dll`（且排除了 internal 框架类）与 `GameData`，
        // 导致开发渲染/地图/底层补丁模组时，无法通过 `sdv-dev api` 或 `decompile` 查询 `MonoGame.Framework.dll`
        //（如 `SpriteBatch`、`RenderTarget2D`、`Effect`、`BlendState`）、`xTile.dll`（如 `XnaDisplayDevice`、`Layer`）、
        // `0Harmony.dll`、`SMAPI.Toolkit.dll` 以及 SMAPI 内部钩子（如 `SModHooks`、`SCore`）。纳入后可一站式反编译全部底层实现。
        private static readonly string[] TargetAssemblies =
        {
            "Stardew Valley.dll",
            "StardewModdingAPI.dll",
            "StardewValley.GameData.dll",
            "MonoGame.Framework.dll",
            "xTile.dll",
            Path.Combine("smapi-internal", "0Harmony.dll"),
            Path.Combine("smapi-internal", "SMAPI.Toolkit.dll")
        };

        public static AssemblyIndexResult IndexGameAssemblies(string gamePath)
        {
            var result = new AssemblyIndexResult();
            var xmlDocs = new XmlDocReader();

            foreach (var dllName in TargetAssemblies)
            {
                string xmlPath = Path.Combine(gamePath, Path.ChangeExtension(dllName, ".xml"));
                xmlDocs.LoadXmlFile(xmlPath);
            }

            // First, extract DataLoader asset->type map from Stardew Valley.dll if available
            var typeToAssetPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SeedDefaultAssetMappings(typeToAssetPath);

            string sdvDll = Path.Combine(gamePath, "Stardew Valley.dll");
            if (File.Exists(sdvDll))
            {
                ExtractEngineMetadataFromSdvDll(gamePath, sdvDll, xmlDocs, typeToAssetPath, result);
            }

            foreach (var dllName in TargetAssemblies)
            {
                string dllPath = Path.Combine(gamePath, dllName);
                if (!File.Exists(dllPath))
                    continue;

                string asmLabel = Path.GetFileNameWithoutExtension(dllName);
                bool isGameData = asmLabel.Equals("StardewValley.GameData", StringComparison.OrdinalIgnoreCase);
                bool isSdvMain = asmLabel.Equals("Stardew Valley", StringComparison.OrdinalIgnoreCase);
                bool isSmapiCore = asmLabel.Equals("StardewModdingAPI", StringComparison.OrdinalIgnoreCase);
                bool isMonoGame = asmLabel.Equals("MonoGame.Framework", StringComparison.OrdinalIgnoreCase);
                bool isXTile = asmLabel.Equals("xTile", StringComparison.OrdinalIgnoreCase);
                bool isHarmony = asmLabel.Equals("0Harmony", StringComparison.OrdinalIgnoreCase);

                using var decompilerContext = OpenDecompiler(gamePath, dllPath);
                var decompiler = decompilerContext.Decompiler;

                string asmVersion = decompiler.TypeSystem.MainModule.AssemblyVersion.ToString();
                if (isSdvMain)
                    result.GameVersion = asmVersion;
                else if (isSmapiCore)
                    result.SmapiVersion = asmVersion;

                foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    if (IsCompilerGenerated(type.Name) || type.Name == "<Module>")
                        continue;

                    string ns = type.Namespace ?? string.Empty;

                    // Stardew Valley.dll: 保留全部 StardewValley* 与 Netcode*（含 internal/private 供 Harmony 检索）
                    if (isSdvMain && !ns.StartsWith("StardewValley", StringComparison.Ordinal) && !ns.StartsWith("Netcode", StringComparison.Ordinal))
                        continue;

                    // StardewModdingAPI.dll: 除公开接口外，保留 StardewModdingAPI.Framework* 关键内部类（如 SModHooks / SCore / EventManager）以便排查事件管线
                    if (isSmapiCore && type.Accessibility != Accessibility.Public && type.Accessibility != Accessibility.Protected &&
                        !ns.StartsWith("StardewModdingAPI.Framework", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // MonoGame.Framework.dll: 仅索引公开/受保护的 Microsoft.Xna.Framework* API（跳过上千个底层 SDL/OpenGL 私有 P/Invoke 委托噪声）
                    if (isMonoGame && ((type.Accessibility != Accessibility.Public && type.Accessibility != Accessibility.Protected) ||
                                       !ns.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    // 0Harmony.dll: 仅索引 HarmonyLib* 公开/受保护类型（排除内嵌的 MonoMod/Cecil 副本防污染）
                    if (isHarmony && ((type.Accessibility != Accessibility.Public && type.Accessibility != Accessibility.Protected) ||
                                      !ns.StartsWith("HarmonyLib", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    // 其余辅助程序集默认索引公开/受保护类型（xTile 额外保留 Display 内部实现）
                    if (!isSdvMain && !isSmapiCore && !isMonoGame && !isHarmony && !isXTile &&
                        type.Accessibility != Accessibility.Public && type.Accessibility != Accessibility.Protected)
                    {
                        continue;
                    }

                    string typeDocId = IdStringProvider.GetIdString(type);
                    string typeSummary = xmlDocs.GetSummary(typeDocId);
                    string kind = GetTypeKindString(type.Kind);
                    string typeAcc = FormatAccessibility(type.Accessibility);

                    result.ApiRecords.Add(new ApiRecord
                    {
                        Assembly = asmLabel,
                        Kind = kind,
                        Namespace = ns,
                        TypeName = type.Name,
                        FullTypeName = type.FullName,
                        MemberName = string.Empty,
                        Signature = FormatTypeSignature(type),
                        Summary = typeSummary,
                        Accessibility = typeAcc,
                        MetadataToken = MetadataTokens.GetToken(type.MetadataToken)
                    });

                    // If in StardewValley.GameData (or mapped by DataLoader), extract full schema
                    if (isGameData || typeToAssetPath.ContainsKey(type.FullName) || typeToAssetPath.ContainsKey(type.Name))
                    {
                        if (type.Accessibility == Accessibility.Public && (type.Kind == TypeKind.Class || type.Kind == TypeKind.Enum || type.Kind == TypeKind.Struct))
                        {
                            result.SchemaRecords.Add(BuildSchemaRecord(type, xmlDocs, typeToAssetPath));
                        }
                    }

                    if (type.Kind == TypeKind.Enum)
                    {
                        // Index enum values as fields for easy search
                        foreach (var f in type.Fields.Where(f => f.IsConst))
                        {
                            string fSummary = xmlDocs.GetSummary(IdStringProvider.GetIdString(f));
                            result.ApiRecords.Add(new ApiRecord
                            {
                                Assembly = asmLabel,
                                Kind = "enum_value",
                                Namespace = ns,
                                TypeName = type.Name,
                                FullTypeName = type.FullName,
                                MemberName = f.Name,
                                Signature = $"{type.Name}.{f.Name} = {f.GetConstantValue()}",
                                Summary = fSummary,
                                Accessibility = "public",
                                MetadataToken = MetadataTokens.GetToken(f.MetadataToken)
                            });
                        }
                        continue;
                    }

                    // Methods (include private/protected on StardewValley & SMAPI.Framework & xTile so Harmony / hook targets are searchable!)
                    foreach (var m in type.Methods)
                    {
                        if (m.IsAccessor || IsCompilerGenerated(m.Name))
                            continue;
                        if (!isSdvMain && !isSmapiCore && !isXTile && m.Accessibility != Accessibility.Public && m.Accessibility != Accessibility.Protected)
                            continue;

                        string mKind = m.IsConstructor ? "constructor" : "method";
                        string mName = m.IsConstructor ? type.Name : m.Name;
                        string mDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(m));

                        result.ApiRecords.Add(new ApiRecord
                        {
                            Assembly = asmLabel,
                            Kind = mKind,
                            Namespace = ns,
                            TypeName = type.Name,
                            FullTypeName = type.FullName,
                            MemberName = mName,
                            Signature = FormatMethodSignature(type, m),
                            Summary = mDoc,
                            Accessibility = FormatAccessibility(m.Accessibility),
                            MetadataToken = MetadataTokens.GetToken(m.MetadataToken)
                        });
                    }

                    // Properties
                    foreach (var p in type.Properties)
                    {
                        if (IsCompilerGenerated(p.Name))
                            continue;
                        if (p.Accessibility != Accessibility.Public && p.Accessibility != Accessibility.Protected && !isSdvMain)
                            continue;
                        // For private properties in Stardew Valley, only index public/protected/internal to reduce backing noise, keep fields/methods
                        if (p.Accessibility == Accessibility.Private)
                            continue;

                        string pDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(p));
                        result.ApiRecords.Add(new ApiRecord
                        {
                            Assembly = asmLabel,
                            Kind = "property",
                            Namespace = ns,
                            TypeName = type.Name,
                            FullTypeName = type.FullName,
                            MemberName = p.Name,
                            Signature = FormatPropertySignature(type, p),
                            Summary = pDoc,
                            Accessibility = FormatAccessibility(p.Accessibility),
                            MetadataToken = MetadataTokens.GetToken(p.MetadataToken)
                        });
                    }

                    // Events (critical for SMAPI IModEvents!)
                    foreach (var ev in type.Events)
                    {
                        if (IsCompilerGenerated(ev.Name))
                            continue;
                        string eDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(ev));
                        result.ApiRecords.Add(new ApiRecord
                        {
                            Assembly = asmLabel,
                            Kind = "event",
                            Namespace = ns,
                            TypeName = type.Name,
                            FullTypeName = type.FullName,
                            MemberName = ev.Name,
                            Signature = $"{FormatAccessibility(ev.Accessibility)} event {ShortTypeName(ev.ReturnType)} {type.Name}.{ev.Name}",
                            Summary = eDoc,
                            Accessibility = FormatAccessibility(ev.Accessibility),
                            MetadataToken = MetadataTokens.GetToken(ev.MetadataToken)
                        });
                    }

                    // Fields (Stardew Valley uses public/Netcode fields heavily, e.g. Game1.player, Object.Stack)
                    foreach (var f in type.Fields)
                    {
                        if (IsCompilerGenerated(f.Name))
                            continue;
                        if (f.Accessibility != Accessibility.Public && f.Accessibility != Accessibility.Protected && !(isSdvMain && f.Name.Length > 2 && !f.Name.StartsWith("_")))
                            continue;
                        // Skip private backing fields if not interesting
                        if (f.Accessibility == Accessibility.Private && !isSdvMain)
                            continue;

                        string fDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(f));
                        string constSuffix = f.IsConst && f.GetConstantValue() != null ? $" = {f.GetConstantValue()}" : string.Empty;
                        string mods = f.IsConst ? "const " : (f.IsStatic ? "static " : string.Empty);
                        result.ApiRecords.Add(new ApiRecord
                        {
                            Assembly = asmLabel,
                            Kind = "field",
                            Namespace = ns,
                            TypeName = type.Name,
                            FullTypeName = type.FullName,
                            MemberName = f.Name,
                            Signature = $"{FormatAccessibility(f.Accessibility)} {mods}{ShortTypeName(f.ReturnType)} {type.Name}.{f.Name}{constSuffix}",
                            Summary = fDoc,
                            Accessibility = FormatAccessibility(f.Accessibility),
                            MetadataToken = MetadataTokens.GetToken(f.MetadataToken)
                        });
                    }
                }
            }

            return result;
        }

        private static SchemaRecord BuildSchemaRecord(ITypeDefinition type, XmlDocReader xmlDocs, Dictionary<string, string> typeToAssetPath)
        {
            string assetPath = string.Empty;
            if (!typeToAssetPath.TryGetValue(type.FullName, out assetPath!))
            {
                typeToAssetPath.TryGetValue(type.Name, out assetPath!);
            }
            assetPath ??= string.Empty;

            string summary = xmlDocs.GetSummary(IdStringProvider.GetIdString(type));
            var fields = new List<SchemaFieldInfo>();
            var textSb = new StringBuilder();

            if (type.Kind == TypeKind.Enum)
            {
                foreach (var f in type.Fields.Where(f => f.IsConst))
                {
                    string fDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(f));
                    string valStr = f.GetConstantValue()?.ToString() ?? "";
                    fields.Add(new SchemaFieldInfo
                    {
                        Name = f.Name,
                        Type = $"enum ({valStr})",
                        Summary = fDoc
                    });
                    textSb.AppendLine($"{f.Name} = {valStr}: {fDoc}");
                }
            }
            else
            {
                // Walk base types in GameData too so inherited fields are included!
                var hierarchy = new List<ITypeDefinition>();
                IType? cur = type;
                while (cur != null)
                {
                    var def = cur.GetDefinition();
                    if (def == null || def.FullName == "System.Object" || def.FullName == "System.ValueType")
                        break;
                    hierarchy.Add(def);
                    cur = def.DirectBaseTypes.FirstOrDefault(b => b.Kind == TypeKind.Class);
                }
                hierarchy.Reverse();

                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tDef in hierarchy)
                {
                    foreach (var p in tDef.Properties.Where(p => p.Accessibility == Accessibility.Public && !p.IsStatic))
                    {
                        if (!seenNames.Add(p.Name))
                            continue;
                        string pDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(p));
                        string pType = ShortTypeName(p.ReturnType);
                        fields.Add(new SchemaFieldInfo { Name = p.Name, Type = pType, Summary = pDoc });
                        textSb.AppendLine($"{p.Name} ({pType}): {pDoc}");
                    }

                    foreach (var f in tDef.Fields.Where(f => f.Accessibility == Accessibility.Public && !f.IsStatic && !f.IsConst))
                    {
                        if (!seenNames.Add(f.Name))
                            continue;
                        string fDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(f));
                        string fType = ShortTypeName(f.ReturnType);
                        fields.Add(new SchemaFieldInfo { Name = f.Name, Type = fType, Summary = fDoc });
                        textSb.AppendLine($"{f.Name} ({fType}): {fDoc}");
                    }
                }
            }

            return new SchemaRecord
            {
                AssetPath = assetPath,
                TypeName = type.Name,
                FullTypeName = type.FullName,
                Kind = GetTypeKindString(type.Kind),
                Summary = summary,
                FieldsJson = JsonSerializer.Serialize(fields),
                FieldsText = textSb.ToString().Trim()
            };
        }

        /// <summary>
        /// Directly inspects Stardew Valley.dll to extract 100% accurate 1.6 engine tables:
        /// - DataLoader asset path -> GameData C# model mappings
        /// - GameStateQuery.DefaultResolvers (every built-in GSQ condition + official XML docs!)
        /// - TriggerActionManager.DefaultActions (every built-in Trigger Action + official XML docs!)
        /// - Event.DefaultCommands & Event.DefaultPreconditions (every Event script command & precondition + official XML docs!)
        /// </summary>
        private static void ExtractEngineMetadataFromSdvDll(
            string gamePath,
            string sdvDllPath,
            XmlDocReader xmlDocs,
            Dictionary<string, string> typeToAssetPath,
            AssemblyIndexResult result)
        {
            using var ctx = OpenDecompiler(gamePath, sdvDllPath);
            var decompiler = ctx.Decompiler;

            // 1. DataLoader -> Asset path mappings + overview doc
            var dataLoaderType = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName("StardewValley.DataLoader"));
            if (dataLoaderType != null)
            {
                var dlDocSb = new StringBuilder();
                dlDocSb.AppendLine("> **文档来源**: 本地游戏本体程序集直提 (`Stardew Valley.dll` -> `StardewValley.DataLoader`)");
                dlDocSb.AppendLine();
                dlDocSb.AppendLine("# Stardew Valley 1.6 Vanilla DataLoader Assets (`StardewValley.DataLoader`)");
                dlDocSb.AppendLine();
                dlDocSb.AppendLine("| Data Asset Path (`Target`) | C# Return / Model Type | DataLoader Method |");
                dlDocSb.AppendLine("| :--- | :--- | :--- |");

                foreach (var m in dataLoaderType.Methods.Where(m => m.Accessibility == Accessibility.Public && m.IsStatic && !m.IsConstructor))
                {
                    string retType = ShortTypeName(m.ReturnType);
                    string guessedAsset = $"Data/{m.Name}";

                    try
                    {
                        string mBody = decompiler.DecompileAsString(m.MetadataToken);
                        var strMatch = Regex.Match(mBody, @"""((?:Data|Strings|Maps)[\\/][^""]+)""");
                        if (strMatch.Success)
                        {
                            guessedAsset = strMatch.Groups[1].Value.Replace("\\\\", "/").Replace('\\', '/');
                        }
                    }
                    catch
                    {
                    }

                    var innerType = ExtractElementModelType(m.ReturnType);
                    if (innerType != null && innerType.FullName.StartsWith("StardewValley.GameData", StringComparison.Ordinal))
                    {
                        if (!typeToAssetPath.ContainsKey(innerType.FullName))
                            typeToAssetPath[innerType.FullName] = guessedAsset;
                        if (!typeToAssetPath.ContainsKey(innerType.Name))
                            typeToAssetPath[innerType.Name] = guessedAsset;
                    }

                    dlDocSb.AppendLine($"| `{guessedAsset}` | `{retType}` | `DataLoader.{m.Name}` |");
                }

                result.EngineDocs.Add(new DocRecord
                {
                    Source = "builtin-dll",
                    Category = "GameData",
                    Title = "Stardew Valley 1.6 DataLoader Assets & C# Model Mapping Table",
                    UrlOrPath = "StardewValley.DataLoader",
                    Content = dlDocSb.ToString()
                });
            }

            // 2. GameStateQuery.DefaultResolvers (with automatic ArgUtility parameter extraction!)
            var gsqResolvers = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName("StardewValley.GameStateQuery+DefaultResolvers"));
            if (gsqResolvers != null)
            {
                var chunk = new List<string>();
                foreach (var m in gsqResolvers.Methods.Where(m => m.Accessibility == Accessibility.Public && m.IsStatic && !m.IsConstructor).OrderBy(m => m.Name))
                {
                    string doc = CleanEngineSummary(xmlDocs.GetSummary(IdStringProvider.GetIdString(m)));
                    string syntax = ExtractArgUtilitySyntax(decompiler, m, m.Name);
                    var entrySb = new StringBuilder();
                    entrySb.AppendLine($"### `{m.Name}`");
                    entrySb.AppendLine($"- **Syntax**: `{syntax}`");
                    if (!string.IsNullOrWhiteSpace(doc))
                        entrySb.AppendLine($"- **Description**: {doc}");
                    chunk.Add(entrySb.ToString());
                }

                AddChunkedDocs(result.EngineDocs, "builtin-dll", "GameStateQuery", "Game State Queries (1.6 Official DLL Reference)", "StardewValley.GameStateQuery.DefaultResolvers", chunk, 20);
            }

            // 3. TriggerActionManager.DefaultActions (with automatic ArgUtility parameter extraction!)
            var triggerActionsType = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName("StardewValley.Triggers.TriggerActionManager+DefaultActions"));
            if (triggerActionsType != null)
            {
                var items = new List<string>();
                foreach (var m in triggerActionsType.Methods.Where(m => m.Accessibility == Accessibility.Public && m.IsStatic && !m.IsConstructor).OrderBy(m => m.Name))
                {
                    string doc = CleanEngineSummary(xmlDocs.GetSummary(IdStringProvider.GetIdString(m)));
                    string syntax = ExtractArgUtilitySyntax(decompiler, m, m.Name);
                    var entrySb = new StringBuilder();
                    entrySb.AppendLine($"### Trigger Action: `{m.Name}`");
                    entrySb.AppendLine($"- **Syntax**: `{syntax}`");
                    if (!string.IsNullOrWhiteSpace(doc))
                        entrySb.AppendLine($"- **Description**: {doc}");
                    items.Add(entrySb.ToString());
                }
                AddChunkedDocs(result.EngineDocs, "builtin-dll", "TriggerAction", "Trigger Actions (`Data/TriggerActions` 1.6 Official DLL Reference)", "StardewValley.Triggers.TriggerActionManager.DefaultActions", items, 20);
            }

            // 4. Event.DefaultCommands (with automatic ArgUtility parameter extraction!)
            var eventCommandsType = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName("StardewValley.Event+DefaultCommands"));
            if (eventCommandsType != null)
            {
                var items = new List<string>();
                foreach (var m in eventCommandsType.Methods.Where(m => m.Accessibility == Accessibility.Public && m.IsStatic && !m.IsConstructor).OrderBy(m => m.Name))
                {
                    string doc = CleanEngineSummary(xmlDocs.GetSummary(IdStringProvider.GetIdString(m)));
                    string syntax = ExtractArgUtilitySyntax(decompiler, m, m.Name);
                    var entrySb = new StringBuilder();
                    entrySb.AppendLine($"### Event Command: `{m.Name}`");
                    entrySb.AppendLine($"- **Syntax**: `{syntax}`");
                    if (!string.IsNullOrWhiteSpace(doc))
                        entrySb.AppendLine($"- **Description**: {doc}");
                    items.Add(entrySb.ToString());
                }
                AddChunkedDocs(result.EngineDocs, "builtin-dll", "EventCommand", "Event Script Commands (`Event.DefaultCommands` 1.6 Official DLL Reference)", "StardewValley.Event.DefaultCommands", items, 25);
            }

            // 5. Extract SMAPI IModHelper & IModEvents complete guide directly from StardewModdingAPI.dll + XML
            string smapiDllPath = Path.Combine(gamePath, "StardewModdingAPI.dll");
            if (File.Exists(smapiDllPath))
            {
                ExtractSmapiOfficialApiDocs(gamePath, smapiDllPath, xmlDocs, result);
            }
        }

        private static string ExtractArgUtilitySyntax(CSharpDecompiler decompiler, IMethod method, string commandName)
        {
            try
            {
                string body = decompiler.DecompileAsString(method.MetadataToken);
                var argsByIndex = new SortedDictionary<int, string>();

                // Matches ArgUtility.TryGet*(array, INDEX, ..., name: "type paramName") or trailing string literal
                foreach (Match m in Regex.Matches(body, @"ArgUtility\.TryGet([A-Za-z0-9_]*)\s*\([^,]+,\s*(\d+)\s*,([^;]+?)\)", RegexOptions.Singleline))
                {
                    string variant = m.Groups[1].Value;
                    if (!int.TryParse(m.Groups[2].Value, out int idx))
                        continue;

                    string rest = m.Groups[3].Value;
                    bool isOptional = variant.IndexOf("Optional", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Last quoted string in ArgUtility call is the parameter label (e.g. "string mailId")
                    var strMatches = Regex.Matches(rest, @"""([^""]+)""");
                    string label = strMatches.Count > 0 ? strMatches[strMatches.Count - 1].Groups[1].Value : $"arg{idx}";
                    string formatted = isOptional ? $"[{label}]" : $"<{label}>";

                    if (!argsByIndex.ContainsKey(idx))
                        argsByIndex[idx] = formatted;
                }

                if (argsByIndex.Count > 0)
                {
                    return commandName + " " + string.Join(" ", argsByIndex.Values);
                }
            }
            catch
            {
            }
            return commandName;
        }

        private static void ExtractSmapiOfficialApiDocs(
            string gamePath,
            string smapiDllPath,
            XmlDocReader xmlDocs,
            AssemblyIndexResult result)
        {
            using var ctx = OpenDecompiler(gamePath, smapiDllPath);
            var mainMod = ctx.Decompiler.TypeSystem.MainModule;

            // Extract IModHelper & all sub-APIs + IModEvents interfaces
            var smapiInterfaces = mainMod.TypeDefinitions
                .Where(t => t.Accessibility == Accessibility.Public &&
                            t.Kind == TypeKind.Interface &&
                            (t.Namespace == "StardewModdingAPI" || t.Namespace == "StardewModdingAPI.Events"))
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToList();

            var sections = new List<string>();
            foreach (var iface in smapiInterfaces)
            {
                if (iface.Properties.Count() == 0 && iface.Methods.Count(m => !m.IsAccessor) == 0 && iface.Events.Count() == 0)
                    continue;

                var sb = new StringBuilder();
                string iDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(iface));
                sb.AppendLine($"### `{iface.FullName}`");
                if (!string.IsNullOrWhiteSpace(iDoc))
                    sb.AppendLine(iDoc);
                sb.AppendLine();

                foreach (var ev in iface.Events)
                {
                    string eDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(ev));
                    sb.AppendLine($"- **Event `{ev.Name}`** (`{ShortTypeName(ev.ReturnType)}`): {eDoc}");
                }
                foreach (var prop in iface.Properties)
                {
                    string pDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(prop));
                    sb.AppendLine($"- **Property `{prop.Name}`** (`{ShortTypeName(prop.ReturnType)}`): {pDoc}");
                }
                foreach (var m in iface.Methods.Where(m => !m.IsAccessor))
                {
                    string mDoc = xmlDocs.GetSummary(IdStringProvider.GetIdString(m));
                    sb.AppendLine($"- **`{FormatMethodSignature(iface, m)}`**: {mDoc}");
                }

                sections.Add(sb.ToString());
            }

            if (sections.Count > 0)
            {
                int chunkSize = 6;
                int total = (sections.Count + chunkSize - 1) / chunkSize;
                for (int i = 0; i < total; i++)
                {
                    var slice = sections.Skip(i * chunkSize).Take(chunkSize).ToList();
                    string first = ExtractHeadingName(slice.First());
                    string last = ExtractHeadingName(slice.Last());
                    string header = "> **文档来源**: 本地 SMAPI 官方程序集与注释直提 (`StardewModdingAPI.dll` + `StardewModdingAPI.xml`)\n\n";
                    result.EngineDocs.Add(new DocRecord
                    {
                        Source = "builtin-dll",
                        Category = "SMAPI",
                        Title = $"SMAPI Official APIs & Events Reference [{first} .. {last}]",
                        UrlOrPath = "StardewModdingAPI.dll",
                        Content = header + string.Join("\n", slice)
                    });
                }
            }
        }

        private static void AddChunkedDocs(
            List<DocRecord> target,
            string source,
            string category,
            string baseTitle,
            string path,
            List<string> entries,
            int chunkSize)
        {
            if (entries.Count == 0)
                return;

            int totalParts = (entries.Count + chunkSize - 1) / chunkSize;
            for (int i = 0; i < totalParts; i++)
            {
                var slice = entries.Skip(i * chunkSize).Take(chunkSize).ToList();
                // Extract entry headings for descriptive title
                string firstKey = ExtractHeadingName(slice.First());
                string lastKey = ExtractHeadingName(slice.Last());
                string title = totalParts == 1
                    ? baseTitle
                    : $"{baseTitle} [{firstKey} .. {lastKey}]";

                string header = $"> **文档来源**: 本地游戏本体程序集与官方注释直提 (`Stardew Valley.dll` + `Stardew Valley.xml` -> `{path}`)\n\n";

                target.Add(new DocRecord
                {
                    Source = source,
                    Category = category,
                    Title = title,
                    UrlOrPath = path,
                    Content = header + string.Join("\n", slice)
                });
            }
        }

        private static string ExtractHeadingName(string md)
        {
            var m = Regex.Match(md, @"`([^`]+)`");
            return m.Success ? m.Groups[1].Value : "Part";
        }

        private static string CleanEngineSummary(string doc)
        {
            if (string.IsNullOrWhiteSpace(doc))
                return string.Empty;
            // Strip boilerplate @param query/context lines that repeat on every GSQ/Event handler
            var lines = doc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l =>
                    !l.StartsWith("@param query:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@param context:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@param @event:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@param args:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@param action:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@param error:", StringComparison.OrdinalIgnoreCase) &&
                    !l.StartsWith("@returns:", StringComparison.OrdinalIgnoreCase));
            return string.Join("\n", lines).Trim();
        }

        private static IType? ExtractElementModelType(IType type)
        {
            if (type is ParameterizedType pt)
            {
                if (pt.TypeArguments.Count == 2)
                    return pt.TypeArguments[1];
                if (pt.TypeArguments.Count == 1)
                    return pt.TypeArguments[0];
            }
            return type;
        }

        private static void SeedDefaultAssetMappings(Dictionary<string, string> map)
        {
            map["ObjectData"] = "Data/Objects";
            map["BigCraftableData"] = "Data/BigCraftables";
            map["MachineData"] = "Data/Machines";
            map["CropData"] = "Data/Crops";
            map["FruitTreeData"] = "Data/FruitTrees";
            map["BuildingData"] = "Data/Buildings";
            map["ShopData"] = "Data/Shops";
            map["CharacterData"] = "Data/Characters";
            map["LocationData"] = "Data/Locations";
            map["WeaponData"] = "Data/Weapons";
            map["BuffData"] = "Data/Buffs";
            map["FarmAnimalData"] = "Data/FarmAnimals";
            map["FishPondData"] = "Data/FishPondData";
            map["TriggerActionData"] = "Data/TriggerActions";
            map["WildTreeData"] = "Data/WildTrees";
            map["GarbageCanData"] = "Data/GarbageCans";
            map["MannequinData"] = "Data/Mannequins";
            map["PantsData"] = "Data/Pants";
            map["ShirtData"] = "Data/Shirts";
            map["ToolData"] = "Data/Tools";
            map["TrinketData"] = "Data/Trinkets";
            map["AudioCueData"] = "Data/AudioChanges";
            map["ChairData"] = "Data/ChairTiles";
            map["HomeRenovation"] = "Data/HomeRenovations";
            map["IncomingPhoneCallData"] = "Data/IncomingPhoneCalls";
            map["MakeoverOutfit"] = "Data/MakeoverOutfits";
            map["MinecartNetworkData"] = "Data/Minecarts";
            map["MuseumData"] = "Data/Museum";
            map["PassiveFestivalData"] = "Data/PassiveFestivals";
            map["PetData"] = "Data/Pets";
            map["PowerData"] = "Data/Powers";
            map["WorldMapAreaData"] = "Data/WorldMap";
        }

        /// <summary>
        /// Resolves all referenced assemblies strictly into MemoryStream buffers so ZERO file handles
        /// in Stardew Valley's directory or .NET runtime folders remain open on Windows!
        /// </summary>
        internal sealed class ZeroLockAssemblyResolver : IAssemblyResolver, IDisposable
        {
            private readonly UniversalAssemblyResolver _pathFinder;
            private readonly ConcurrentDictionary<string, PEFile?> _byFullPath = new ConcurrentDictionary<string, PEFile?>(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, PEFile?> _byAsmRef = new ConcurrentDictionary<string, PEFile?>(StringComparer.OrdinalIgnoreCase);

            public ZeroLockAssemblyResolver(string mainDllPath, string? targetFramework, string gamePath)
            {
                _pathFinder = new UniversalAssemblyResolver(mainDllPath, throwOnError: false, targetFramework);
                _pathFinder.AddSearchDirectory(gamePath);
                string smapiInternal = Path.Combine(gamePath, "smapi-internal");
                if (Directory.Exists(smapiInternal))
                    _pathFinder.AddSearchDirectory(smapiInternal);

                string dotnetShared = @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App";
                if (Directory.Exists(dotnetShared))
                {
                    var net6Dir = Directory.GetDirectories(dotnetShared, "6.*").OrderByDescending(d => d).FirstOrDefault()
                               ?? Directory.GetDirectories(dotnetShared).OrderByDescending(d => d).FirstOrDefault();
                    if (net6Dir != null)
                        _pathFinder.AddSearchDirectory(net6Dir);
                }
            }

            public PEFile? Resolve(IAssemblyReference name)
            {
                return _byAsmRef.GetOrAdd(name.FullName, _ =>
                {
                    string? file = _pathFinder.FindAssemblyFile(name);
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                        return null;

                    return _byFullPath.GetOrAdd(file!, static path =>
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(path);
                            return new PEFile(path, new MemoryStream(bytes, writable: false), PEStreamOptions.Default);
                        }
                        catch
                        {
                            return null;
                        }
                    });
                });
            }

            public Task<PEFile?> ResolveAsync(IAssemblyReference name) => Task.FromResult(Resolve(name));

            public PEFile? ResolveModule(PEFile mainModule, string moduleName)
            {
                string? dir = Path.GetDirectoryName(mainModule.FileName);
                if (string.IsNullOrEmpty(dir))
                    return null;
                string candidate = Path.Combine(dir, moduleName);
                if (!File.Exists(candidate))
                    return null;

                return _byFullPath.GetOrAdd(candidate, static path =>
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        return new PEFile(path, new MemoryStream(bytes, writable: false), PEStreamOptions.Default);
                    }
                    catch
                    {
                        return null;
                    }
                });
            }

            public Task<PEFile?> ResolveModuleAsync(PEFile mainModule, string moduleName) =>
                Task.FromResult(ResolveModule(mainModule, moduleName));

            public void Dispose()
            {
                foreach (var kv in _byFullPath)
                {
                    try { kv.Value?.Dispose(); } catch { }
                }
                _byFullPath.Clear();
            }
        }

        internal sealed class DecompilerHandle : IDisposable
        {
            public PEFile Module { get; }
            public DecompilerTypeSystem TypeSystem { get; }
            public DecompilerSettings Settings { get; }
            public ZeroLockAssemblyResolver Resolver { get; }

            /// <summary>
            /// Creates a fresh, thread-safe CSharpDecompiler instance backed by the shared thread-safe DecompilerTypeSystem.
            /// Avoids concurrent AST state corruption ("Error decompiling @Token") when background prewarm runs concurrently!
            /// </summary>
            public CSharpDecompiler CreateDecompiler() => new CSharpDecompiler(TypeSystem, Settings);

            // Backward-compatible property for single-threaded indexing callers
            public CSharpDecompiler Decompiler => CreateDecompiler();

            public DecompilerHandle(PEFile module, DecompilerTypeSystem typeSystem, DecompilerSettings settings, ZeroLockAssemblyResolver resolver)
            {
                Module = module;
                TypeSystem = typeSystem;
                Settings = settings;
                Resolver = resolver;
            }

            public void Dispose()
            {
                Module.Dispose();
                Resolver.Dispose();
            }
        }

        internal static DecompilerHandle OpenDecompiler(string gamePath, string dllPath)
        {
            // Read entire PE into managed byte[] and close disk handle immediately!
            byte[] dllBytes = File.ReadAllBytes(dllPath);
            var module = new PEFile(
                dllPath,
                new MemoryStream(dllBytes, writable: false),
                PEStreamOptions.Default);

            var resolver = new ZeroLockAssemblyResolver(dllPath, module.DetectTargetFrameworkId(), gamePath);
            var settings = new DecompilerSettings(LanguageVersion.CSharp11_0)
            {
                ThrowOnAssemblyResolveErrors = false,
                ShowXmlDocumentation = false
            };

            var typeSystem = new DecompilerTypeSystem(module, resolver, settings);
            return new DecompilerHandle(module, typeSystem, settings, resolver);
        }

        internal static string FormatTypeSignature(ITypeDefinition type)
        {
            string acc = FormatAccessibility(type.Accessibility);
            string mod = type.IsStatic ? "static " : (type.IsAbstract && type.Kind == TypeKind.Class ? "abstract " : (type.IsSealed && type.Kind == TypeKind.Class ? "sealed " : string.Empty));
            string kind = GetTypeKindString(type.Kind);
            string tp = type.TypeParameters.Count > 0
                ? "<" + string.Join(", ", type.TypeParameters.Select(p => p.Name)) + ">"
                : string.Empty;

            var bases = type.DirectBaseTypes
                .Where(b => b.FullName != "System.Object" && b.FullName != "System.ValueType" && b.FullName != "System.Enum")
                .Select(ShortTypeName)
                .ToList();
            string baseClause = bases.Count > 0 ? " : " + string.Join(", ", bases) : string.Empty;

            return $"{acc} {mod}{kind} {type.FullName}{tp}{baseClause}";
        }

        internal static string FormatMethodSignature(ITypeDefinition declaringType, IMethod m)
        {
            string acc = FormatAccessibility(m.Accessibility);
            string mods = m.IsStatic ? "static " : (m.IsOverride ? "override " : (m.IsVirtual ? "virtual " : (m.IsAbstract ? "abstract " : string.Empty)));
            string tp = m.TypeParameters.Count > 0
                ? "<" + string.Join(", ", m.TypeParameters.Select(p => p.Name)) + ">"
                : string.Empty;

            var prms = m.Parameters.Select(FormatParameter);
            if (m.IsConstructor)
            {
                return $"{acc} {mods}{declaringType.Name}({string.Join(", ", prms)})";
            }
            return $"{acc} {mods}{ShortTypeName(m.ReturnType)} {declaringType.Name}.{m.Name}{tp}({string.Join(", ", prms)})";
        }

        internal static string FormatPropertySignature(ITypeDefinition declaringType, IProperty p)
        {
            string acc = FormatAccessibility(p.Accessibility);
            string mods = p.IsStatic ? "static " : (p.IsVirtual ? "virtual " : string.Empty);
            string gs = "{ " + (p.CanGet ? "get; " : "") + (p.CanSet ? "set; " : "") + "}";
            if (p.IsIndexer)
            {
                var prms = p.Parameters.Select(FormatParameter);
                return $"{acc} {mods}{ShortTypeName(p.ReturnType)} {declaringType.Name}[{string.Join(", ", prms)}] {gs}";
            }
            return $"{acc} {mods}{ShortTypeName(p.ReturnType)} {declaringType.Name}.{p.Name} {gs}";
        }

        private static string FormatParameter(IParameter p)
        {
            string prefix = p.ReferenceKind switch
            {
                ReferenceKind.Ref => "ref ",
                ReferenceKind.Out => "out ",
                ReferenceKind.In => "in ",
                _ => p.IsParams ? "params " : string.Empty
            };
            string defVal = string.Empty;
            if (p.HasConstantValueInSignature)
            {
                try
                {
                    object? cv = p.GetConstantValue();
                    defVal = cv == null ? " = null" : (cv is string s ? $" = \"{s}\"" : (cv is bool b ? $" = {(b ? "true" : "false")}" : $" = {cv}"));
                }
                catch
                {
                    // Ignore unresolved constant
                }
            }
            return $"{prefix}{ShortTypeName(p.Type)} {p.Name}{defVal}";
        }

        internal static string ShortTypeName(IType type)
        {
            if (type is ByReferenceType br)
                return ShortTypeName(br.ElementType);
            if (type is ArrayType arr)
                return $"{ShortTypeName(arr.ElementType)}[{(arr.Dimensions > 1 ? new string(',', arr.Dimensions - 1) : "")}]";
            if (type is ParameterizedType pt)
            {
                if (pt.FullName == "System.Nullable" && pt.TypeArguments.Count == 1)
                    return $"{ShortTypeName(pt.TypeArguments[0])}?";
                string args = string.Join(", ", pt.TypeArguments.Select(ShortTypeName));
                return $"{pt.Name}<{args}>";
            }

            return type.FullName switch
            {
                "System.Void" => "void",
                "System.Boolean" => "bool",
                "System.Int32" => "int",
                "System.Int64" => "long",
                "System.Single" => "float",
                "System.Double" => "double",
                "System.String" => "string",
                "System.Object" => "object",
                "System.Byte" => "byte",
                _ => type.Name
            };
        }

        private static string GetTypeKindString(TypeKind kind) => kind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => kind.ToString().ToLowerInvariant()
        };

        private static string FormatAccessibility(Accessibility acc) => acc switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Private => "private",
            _ => "private"
        };

        private static bool IsCompilerGenerated(string name)
        {
            return string.IsNullOrEmpty(name) || name.Contains('<') || name.Contains('>') || name.StartsWith("$", StringComparison.Ordinal);
        }
    }
}
