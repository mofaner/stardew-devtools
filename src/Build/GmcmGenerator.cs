using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using SdvKb.Indexers;

namespace SdvKb.Build
{
    public sealed class GmcmOptionProp
    {
        public string Name { get; set; } = string.Empty;
        public string CSharpType { get; set; } = string.Empty;
        public string Kind { get; set; } = "text"; // bool, int, float, double, string, keybind, button, enum
        public string Summary { get; set; } = string.Empty;
        public string? ExplicitTitle { get; set; }
        public string? ExplicitDescription { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Interval { get; set; }
    }

    public sealed class GmcmGenerationResult
    {
        public bool Ok { get; set; }
        public string ConfigFile { get; set; } = string.Empty;
        public string ConfigClassName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public int OptionCount { get; set; }
        public string ApiInterfaceFile { get; set; } = string.Empty;
        public string RegistrationHelperFile { get; set; } = string.Empty;
        public List<string> UpdatedTranslationFiles { get; set; } = new List<string>();
        public string UsageSnippet { get; set; } = string.Empty;
    }

    /// <summary>
    /// Compiles `ModConfig.cs` via official Roslyn `csc.exe -doc:` + inspects metadata via ILSpy TypeSystem &amp; `XmlDocReader`
    /// to generate `IGenericModConfigMenuApi.cs` (spacechase0 official contract), `GmcmIntegration.g.cs`, and bilingual `i18n` files.
    /// </summary>
    public static class GmcmGenerator
    {
        private static readonly JsonDocumentOptions JsoncOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static GmcmGenerationResult GenerateForProject(string modDir, string? explicitConfigFile = null, string? explicitGamePath = null)
        {
            string fullModDir = Path.GetFullPath(modDir);
            if (!Directory.Exists(fullModDir))
                throw new DirectoryNotFoundException($"Mod directory `{fullModDir}` does not exist.");

            var cfg = KbConfig.Load(explicitGamePath);
            string? configPath = explicitConfigFile != null
                ? (Path.IsPathRooted(explicitConfigFile) ? explicitConfigFile : Path.Combine(fullModDir, explicitConfigFile))
                : FindConfigSourceFile(fullModDir);

            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    $"Could not locate `ModConfig.cs` (or `*Config.cs`) in `{fullModDir}`. Pass `--config <PathToConfig.cs>` or create `ModConfig.cs` first.");
            }

            string fullConfigPath = Path.GetFullPath(configPath!);
            var (ns, className, props) = ExtractConfigMetadataViaRoslyn(fullModDir, fullConfigPath, cfg.GamePath);

            if (props.Count == 0)
                throw new InvalidOperationException($"No supported public read/write properties found on `{className}` in `{fullConfigPath}`.");

            var res = new GmcmGenerationResult
            {
                Ok = true,
                ConfigFile = fullConfigPath,
                ConfigClassName = className,
                Namespace = ns,
                OptionCount = props.Count
            };

            string configDir = Path.GetDirectoryName(fullConfigPath)!;

            // 1. Write spacechase0's official IGenericModConfigMenuApi.cs if not already defined
            string existingApiFile = FindExistingGmcmApiFile(fullModDir);
            if (!string.IsNullOrEmpty(existingApiFile))
            {
                res.ApiInterfaceFile = existingApiFile;
            }
            else
            {
                string apiPath = Path.Combine(configDir, "IGenericModConfigMenuApi.cs");
                File.WriteAllText(apiPath, BuildGmcmInterfaceCode(ns), new UTF8Encoding(false));
                res.ApiInterfaceFile = apiPath;
            }

            // 2. Write GmcmIntegration.g.cs
            string helperPath = Path.Combine(configDir, "GmcmIntegration.g.cs");
            File.WriteAllText(helperPath, BuildGmcmHelperCode(ns, className, props), new UTF8Encoding(false));
            res.RegistrationHelperFile = helperPath;

            // 3. Merge i18n/default.json & i18n/zh.json keys
            string i18nDir = Path.Combine(fullModDir, "i18n");
            Directory.CreateDirectory(i18nDir);

            string defaultJson = Path.Combine(i18nDir, "default.json");
            MergeI18nFile(defaultJson, props, preferChinese: false);
            res.UpdatedTranslationFiles.Add(defaultJson);

            string zhJson = Path.Combine(i18nDir, "zh.json");
            MergeI18nFile(zhJson, props, preferChinese: true);
            res.UpdatedTranslationFiles.Add(zhJson);

            res.UsageSnippet =
                $"// Call inside GameLoop.GameLaunched event:\n" +
                $"GmcmIntegration.Register(this.ModManifest, this.Helper, () => this.Config, v => this.Config = v);";

            return res;
        }

        private static (string Namespace, string ClassName, List<GmcmOptionProp> Props) ExtractConfigMetadataViaRoslyn(
            string modDir,
            string configFilePath,
            string gamePath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sdv-gmcm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string tempDll = Path.Combine(tempDir, "GmcmProbe.dll");
                string tempXml = Path.Combine(tempDir, "GmcmProbe.xml");

                // First try compiling just configFilePath (fast & works even if ModEntry.cs is incomplete);
                // if config references enums in sibling .cs files, compile all non-generated .cs files in modDir!
                var minimalSources = new List<string> { configFilePath };
                var (ok, output) = DotNet6ModCompiler.CompileSourcesToFiles(
                    gamePath, minimalSources, tempDll, null, includeLoadedMods: false, extraReferences: null, publicize: true, outXmlDocPath: tempXml);

                if (!ok)
                {
                    var allCs = Directory.GetFiles(modDir, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") &&
                                    !Path.GetFileName(f).Equals("GmcmIntegration.g.cs", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    (ok, output) = DotNet6ModCompiler.CompileSourcesToFiles(
                        gamePath, allCs, tempDll, null, includeLoadedMods: false, extraReferences: null, publicize: true, outXmlDocPath: tempXml);
                }

                if (!ok || !File.Exists(tempDll))
                    throw new InvalidOperationException($"Failed to compile `{Path.GetFileName(configFilePath)}` via Roslyn:\n{output}");

                var xmlDocs = new XmlDocReader();
                xmlDocs.LoadXmlFile(tempXml);

                var resolver = new UniversalAssemblyResolver(tempDll, throwOnError: false, ".NETCoreApp,Version=v6.0");
                resolver.AddSearchDirectory(gamePath);
                using var pe = new PEFile(tempDll, PEStreamOptions.PrefetchEntireImage);
                var dec = new CSharpDecompiler(pe, resolver, new DecompilerSettings(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false });

                string targetHint = Path.GetFileNameWithoutExtension(configFilePath);
                var configType = dec.TypeSystem.MainModule.TypeDefinitions
                    .FirstOrDefault(t => t.Kind == TypeKind.Class && t.Name.Equals(targetHint, StringComparison.OrdinalIgnoreCase))
                    ?? dec.TypeSystem.MainModule.TypeDefinitions
                        .FirstOrDefault(t => t.Kind == TypeKind.Class && t.Accessibility == Accessibility.Public && t.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
                    ?? dec.TypeSystem.MainModule.TypeDefinitions
                        .FirstOrDefault(t => t.Kind == TypeKind.Class && t.Accessibility == Accessibility.Public && t.Name != "<Module>");

                if (configType == null)
                    throw new InvalidOperationException($"No public config class found in `{configFilePath}`.");

                var props = new List<GmcmOptionProp>();
                foreach (var p in configType.Properties)
                {
                    if (p.Accessibility != Accessibility.Public || p.IsStatic || !p.CanGet || !p.CanSet)
                        continue;

                    IType retType = p.ReturnType;
                    if (retType.FullName == "System.Nullable" && retType is ParameterizedType pt && pt.TypeArguments.Count == 1)
                        retType = pt.TypeArguments[0];

                    string? kind = ClassifyTypeSystemKind(retType);
                    if (kind == null)
                        continue;

                    double? min = null, max = null, step = null;
                    string? expTitle = null, expDesc = null;
                    foreach (var attr in p.GetAttributes())
                    {
                        string attrName = attr.AttributeType.Name;
                        if (attrName.Equals("RangeAttribute", StringComparison.OrdinalIgnoreCase) ||
                            attrName.Equals("Range", StringComparison.OrdinalIgnoreCase))
                        {
                            if (attr.FixedArguments.Length >= 2)
                            {
                                min = Convert.ToDouble(attr.FixedArguments[0].Value, CultureInfo.InvariantCulture);
                                max = Convert.ToDouble(attr.FixedArguments[1].Value, CultureInfo.InvariantCulture);
                                if (attr.FixedArguments.Length >= 3)
                                    step = Convert.ToDouble(attr.FixedArguments[2].Value, CultureInfo.InvariantCulture);
                            }
                        }
                        else if (attrName.Equals("DisplayAttribute", StringComparison.OrdinalIgnoreCase) ||
                                 attrName.Equals("Display", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var na in attr.NamedArguments)
                            {
                                if (na.Name == "Name" && na.Value is string dn && !string.IsNullOrWhiteSpace(dn))
                                    expTitle = dn.Trim();
                                else if (na.Name == "Description" && na.Value is string dd && !string.IsNullOrWhiteSpace(dd))
                                    expDesc = dd.Trim();
                            }
                        }
                        else if (attrName.Equals("DescriptionAttribute", StringComparison.OrdinalIgnoreCase) ||
                                 attrName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                        {
                            if (attr.FixedArguments.Length >= 1 && attr.FixedArguments[0].Value is string d1 && !string.IsNullOrWhiteSpace(d1))
                                expDesc = d1.Trim();
                        }
                    }

                    string summary = xmlDocs.GetSummary(IdStringProvider.GetIdString(p)).Trim();
                    props.Add(new GmcmOptionProp
                    {
                        Name = p.Name,
                        CSharpType = retType.FullName,
                        Kind = kind,
                        Summary = summary,
                        ExplicitTitle = expTitle,
                        ExplicitDescription = expDesc,
                        Min = min,
                        Max = max,
                        Interval = step
                    });
                }

                string ns = string.IsNullOrEmpty(configType.Namespace) ? "MyStardewMod" : configType.Namespace;
                return (ns, configType.Name, props);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private static string? ClassifyTypeSystemKind(IType t)
        {
            // Check exact SMAPI types BEFORE `TypeKind.Enum` because `SButton` is itself an enum!
            string? known = t.FullName switch
            {
                "System.Boolean" => "bool",
                "System.Int32" => "int",
                "System.Single" => "float",
                "System.Double" => "double",
                "System.String" => "string",
                "StardewModdingAPI.SButton" => "button",
                "StardewModdingAPI.Utilities.KeybindList" => "keybind",
                _ => null
            };
            if (known != null)
                return known;

            if (t.Kind == TypeKind.Enum)
                return "enum";
            return null;
        }

        private static string? FindConfigSourceFile(string modDir)
        {
            var csFiles = Directory.GetFiles(modDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return csFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("ModConfig.cs", StringComparison.OrdinalIgnoreCase))
                ?? csFiles.FirstOrDefault(f => Path.GetFileName(f).EndsWith("Config.cs", StringComparison.OrdinalIgnoreCase));
        }

        private static string FindExistingGmcmApiFile(string modDir)
        {
            foreach (var f in Directory.GetFiles(modDir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains("\\obj\\") || f.Contains("\\bin\\"))
                    continue;
                try
                {
                    if (File.ReadAllText(f).Contains("interface IGenericModConfigMenuApi"))
                        return f;
                }
                catch { }
            }
            return string.Empty;
        }

        private static string BuildGmcmInterfaceCode(string ns)
        {
            return $@"using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace {ns}
{{
    /// <summary>The API which lets other mods add a config UI through Generic Mod Config Menu (`spacechase0.GenericModConfigMenu`).</summary>
    public interface IGenericModConfigMenuApi
    {{
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
        void AddParagraph(IManifest mod, Func<string> text);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    }}
}}
";
        }

        private static string BuildGmcmHelperCode(string ns, string configClass, List<GmcmOptionProp> props)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated by `sdv-dev gen-gmcm`. Safe to regenerate after updating ModConfig.cs.");
            sb.AppendLine("using System;");
            sb.AppendLine("using StardewModdingAPI;");
            sb.AppendLine("using StardewModdingAPI.Utilities;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GmcmIntegration");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static void Register(IManifest manifest, IModHelper helper, Func<{configClass}> getConfig, Action<{configClass}> setConfig)");
            sb.AppendLine("        {");
            sb.AppendLine("            var gmcm = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(\"spacechase0.GenericModConfigMenu\");");
            sb.AppendLine("            if (gmcm == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            gmcm.Register(");
            sb.AppendLine("                mod: manifest,");
            sb.AppendLine($"                reset: () => setConfig(new {configClass}()),");
            sb.AppendLine("                save: () => helper.WriteConfig(getConfig()));");
            sb.AppendLine();

            foreach (var p in props)
            {
                string keyBase = $"gmcm.{CamelCase(p.Name)}";
                string nameExpr = $"() => helper.Translation.Get(\"{keyBase}.name\")";
                string tipExpr = $"() => helper.Translation.Get(\"{keyBase}.tooltip\")";

                switch (p.Kind)
                {
                    case "bool":
                        sb.AppendLine($"            gmcm.AddBoolOption(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, \"{p.Name}\");");
                        break;
                    case "int":
                    {
                        string minArg = p.Min.HasValue ? ((int)p.Min.Value).ToString(CultureInfo.InvariantCulture) : "null";
                        string maxArg = p.Max.HasValue ? ((int)p.Max.Value).ToString(CultureInfo.InvariantCulture) : "null";
                        string stepArg = p.Interval.HasValue ? ((int)p.Interval.Value).ToString(CultureInfo.InvariantCulture) : "null";
                        sb.AppendLine($"            gmcm.AddNumberOption(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, min: {minArg}, max: {maxArg}, interval: {stepArg}, fieldId: \"{p.Name}\");");
                        break;
                    }
                    case "float":
                    {
                        string minArg = p.Min.HasValue ? p.Min.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        string maxArg = p.Max.HasValue ? p.Max.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        string stepArg = p.Interval.HasValue ? p.Interval.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        sb.AppendLine($"            gmcm.AddNumberOption(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, min: {minArg}, max: {maxArg}, interval: {stepArg}, fieldId: \"{p.Name}\");");
                        break;
                    }
                    case "double":
                    {
                        string minArg = p.Min.HasValue ? p.Min.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        string maxArg = p.Max.HasValue ? p.Max.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        string stepArg = p.Interval.HasValue ? p.Interval.Value.ToString("0.###", CultureInfo.InvariantCulture) + "f" : "null";
                        sb.AppendLine($"            gmcm.AddNumberOption(manifest, () => (float)getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, min: {minArg}, max: {maxArg}, interval: {stepArg}, fieldId: \"{p.Name}\");");
                        break;
                    }
                    case "string":
                        sb.AppendLine($"            gmcm.AddTextOption(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, fieldId: \"{p.Name}\");");
                        break;
                    case "button":
                        sb.AppendLine($"            gmcm.AddKeybind(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, \"{p.Name}\");");
                        break;
                    case "keybind":
                        sb.AppendLine($"            gmcm.AddKeybindList(manifest, () => getConfig().{p.Name}, v => getConfig().{p.Name} = v, {nameExpr}, {tipExpr}, \"{p.Name}\");");
                        break;
                    case "enum":
                        sb.AppendLine($"            gmcm.AddTextOption(manifest, () => getConfig().{p.Name}.ToString(), v => {{ if (Enum.TryParse<{p.CSharpType}>(v, out var ev)) getConfig().{p.Name} = ev; }}, {nameExpr}, {tipExpr}, allowedValues: Enum.GetNames(typeof({p.CSharpType})), fieldId: \"{p.Name}\");");
                        break;
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void MergeI18nFile(string jsonPath, List<GmcmOptionProp> props, bool preferChinese)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(jsonPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath), JsoncOpts);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in doc.RootElement.EnumerateObject())
                        {
                            dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "") : p.Value.ToString();
                        }
                    }
                }
                catch { }
            }

            foreach (var p in props)
            {
                string keyBase = $"gmcm.{CamelCase(p.Name)}";
                string nameKey = $"{keyBase}.name";
                string tipKey = $"{keyBase}.tooltip";
                string englishFallback = HumanizeIdentifier(p.Name);

                string fullDoc = !string.IsNullOrWhiteSpace(p.ExplicitDescription) ? p.ExplicitDescription! : p.Summary;

                if (!dict.ContainsKey(nameKey))
                {
                    if (preferChinese)
                    {
                        if (!string.IsNullOrWhiteSpace(p.ExplicitTitle))
                            dict[nameKey] = p.ExplicitTitle!;
                        else if (ContainsChinese(p.Summary))
                            dict[nameKey] = ExtractShortChineseOptionTitle(p.Summary, englishFallback);
                        else
                            dict[nameKey] = englishFallback;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(p.ExplicitTitle) && !ContainsChinese(p.ExplicitTitle!))
                            dict[nameKey] = p.ExplicitTitle!;
                        else
                            dict[nameKey] = englishFallback;
                    }
                }

                if (!dict.ContainsKey(tipKey))
                {
                    if (preferChinese)
                    {
                        dict[tipKey] = !string.IsNullOrWhiteSpace(fullDoc) ? fullDoc : dict[nameKey];
                    }
                    else
                    {
                        // Never leak Chinese XML summaries into English default.json!
                        dict[tipKey] = (!string.IsNullOrWhiteSpace(fullDoc) && !ContainsChinese(fullDoc))
                            ? fullDoc
                            : englishFallback;
                    }
                }
            }

            string jsonOut = JsonSerializer.Serialize(dict, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(jsonPath, jsonOut + "\n", new UTF8Encoding(false));
        }

        private static string ExtractShortChineseOptionTitle(string summary, string fallback)
        {
            string head = summary.Split(new[] { '：', ':', '—', '（', '(', '。', '；', ',', '，', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                 .FirstOrDefault()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(head))
                return fallback;
            return head.Length <= 14 ? head : head.Substring(0, 14) + "…";
        }

        private static bool ContainsChinese(string s) =>
            !string.IsNullOrEmpty(s) && s.Any(c => c >= 0x4E00 && c <= 0x9FFF);

        private static string CamelCase(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private static string HumanizeIdentifier(string pascal) =>
            Regex.Replace(pascal, @"([a-z0-9])([A-Z])", "$1 $2");
    }
}
