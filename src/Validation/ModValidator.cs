using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SdvKb.Validation
{
    public sealed class ValidationIssue
    {
        public string Severity { get; set; } = "ERROR"; // ERROR or WARN
        public string File { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public static class ModValidator
    {
        private static readonly JsonDocumentOptions JsoncOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly HashSet<string> ValidCpActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Load", "EditData", "EditImage", "EditMap", "Include"
        };

        private static readonly HashSet<string> ValidItemTypeQualifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "(O)", "(BC)", "(F)", "(W)", "(B)", "(H)", "(P)", "(S)", "(T)", "(WP)", "(FL)", "(M)", "(TR)"
        };

        public static List<ValidationIssue> ValidateModDirectory(string modDir, string? explicitGamePath = null)
        {
            var issues = new List<ValidationIssue>();
            if (!Directory.Exists(modDir))
            {
                issues.Add(new ValidationIssue { Severity = "ERROR", File = modDir, Message = "Directory does not exist." });
                return issues;
            }

            string fullModDir = Path.GetFullPath(modDir);
            string manifestPath = Path.Combine(fullModDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                issues.Add(new ValidationIssue { Severity = "ERROR", File = "manifest.json", Message = "Missing required `manifest.json` in mod root." });
                return issues;
            }

            CachedEngineRules? engineRules = CSharpModAnalyzer.TryGetEngineRules(explicitGamePath);
            var customAudioCues = CollectModCustomAudioCues(fullModDir);
            var referencedI18nKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var loadedLocalTextures = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);

            JsonDocument manifestDoc;
            try
            {
                manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath), JsoncOptions);
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue { Severity = "ERROR", File = "manifest.json", Message = $"Invalid JSON/JSONC syntax: {ex.Message}" });
                return issues;
            }

            string? entryDll = null;
            using (manifestDoc)
            {
                var root = manifestDoc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue { Severity = "ERROR", File = "manifest.json", Message = "Root element must be a JSON object." });
                    return issues;
                }

                RequireStringProp(root, "Name", "manifest.json", issues);
                RequireStringProp(root, "Author", "manifest.json", issues);
                string? version = RequireStringProp(root, "Version", "manifest.json", issues);
                string? uniqueId = RequireStringProp(root, "UniqueID", "manifest.json", issues);

                if (!string.IsNullOrEmpty(version) && !Regex.IsMatch(version!, @"^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$"))
                {
                    issues.Add(new ValidationIssue { Severity = "WARN", File = "manifest.json", Message = $"Version `{version}` is not standard SemVer (e.g. `1.0.0`)." });
                }

                if (!string.IsNullOrEmpty(uniqueId) && !Regex.IsMatch(uniqueId!, @"^[a-zA-Z0-9_.\-]+$"))
                {
                    issues.Add(new ValidationIssue { Severity = "ERROR", File = "manifest.json", Message = $"UniqueID `{uniqueId}` contains invalid characters." });
                }
                else if (!string.IsNullOrEmpty(uniqueId) && !uniqueId!.Contains('.'))
                {
                    issues.Add(new ValidationIssue { Severity = "WARN", File = "manifest.json", Message = $"UniqueID `{uniqueId}` should follow `<Author>.<ModName>` convention." });
                }

                entryDll = GetPropIgnoreCase(root, "EntryDll");
                string? cpTargetId = null;
                if (TryGetElementIgnoreCase(root, "ContentPackFor", out var cpf) && cpf.ValueKind == JsonValueKind.Object)
                {
                    cpTargetId = GetPropIgnoreCase(cpf, "UniqueID");
                }

                if (string.IsNullOrWhiteSpace(entryDll) && string.IsNullOrWhiteSpace(cpTargetId))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = "manifest.json",
                        Message = "Must specify either `EntryDll` (C# SMAPI mod) or `ContentPackFor.UniqueID` (content pack)."
                    });
                }

                if (!string.IsNullOrWhiteSpace(entryDll))
                {
                    string dllFile = Path.Combine(fullModDir, entryDll!);
                    var csprojs = Directory.GetFiles(fullModDir, "*.csproj", SearchOption.AllDirectories);
                    var csFiles = Directory.GetFiles(fullModDir, "*.cs", SearchOption.AllDirectories);
                    if (!File.Exists(dllFile) && csprojs.Length == 0 && csFiles.Length == 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "WARN",
                            File = "manifest.json",
                            Message = $"EntryDll `{entryDll}` not found in folder (build output missing?)."
                        });
                    }

                    CollectI18nReferencesFromCSharp(fullModDir, csFiles, referencedI18nKeys);
                }

                if (string.Equals(cpTargetId, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase))
                {
                    string contentJson = Path.Combine(fullModDir, "content.json");
                    if (!File.Exists(contentJson))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = "content.json",
                            Message = "Content Patcher pack is missing `content.json`."
                        });
                    }
                    else
                    {
                        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ValidateContentPatcherFile(
                            fullModDir,
                            contentJson,
                            isRoot: true,
                            visited,
                            engineRules,
                            referencedI18nKeys,
                            loadedLocalTextures,
                            issues);
                    }
                }

                // Validate i18n JSON files & bidirectional key coverage
                ValidateTranslationsDirectory(fullModDir, referencedI18nKeys, issues);
            }

            // C# Harmony static verification & split-screen multiplayer analysis
            if (!string.IsNullOrWhiteSpace(entryDll))
            {
                CSharpModAnalyzer.AnalyzeCSharpMod(fullModDir, entryDll, explicitGamePath, issues);
            }

            // Validate any *.tmx map files in the mod directory (PNG dimensions, GID bounds, Warps, BFS walkability)
            TmxMapChecker.ValidateModTmxFiles(fullModDir, gamePath: engineRules?.GamePath ?? explicitGamePath, issues);

            return issues;
        }

        private static void ValidateContentPatcherFile(
            string modRoot,
            string jsonPath,
            bool isRoot,
            HashSet<string> visitedFiles,
            CachedEngineRules? engineRules,
            Dictionary<string, string> referencedI18nKeys,
            Dictionary<string, (int Width, int Height)> loadedLocalTextures,
            List<ValidationIssue> issues)
        {
            string fullPath = Path.GetFullPath(jsonPath);
            if (!visitedFiles.Add(fullPath))
                return;

            string relFile = fullPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(modRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/')
                : Path.GetFileName(jsonPath);

            string rawJsonText = File.ReadAllText(fullPath);
            CollectI18nReferencesFromCpText(rawJsonText, relFile, referencedI18nKeys);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(rawJsonText, JsoncOptions);
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue { Severity = "ERROR", File = relFile, Message = $"Invalid JSON/JSONC syntax: {ex.Message}" });
                return;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue { Severity = "ERROR", File = relFile, Message = "Root must be a JSON object." });
                    return;
                }

                if (isRoot)
                {
                    string? fmt = RequireStringProp(root, "Format", relFile, issues);
                    if (!string.IsNullOrEmpty(fmt) && fmt!.StartsWith("1.", StringComparison.Ordinal))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "WARN",
                            File = relFile,
                            Message = $"Content Patcher Format `{fmt}` targets legacy 1.5 schema; use `2.0.0`+ (e.g. `2.4.0`) for Stardew 1.6."
                        });
                    }
                }

                if (!TryGetElementIgnoreCase(root, "Changes", out var changes))
                {
                    issues.Add(new ValidationIssue { Severity = "ERROR", File = relFile, Message = "Missing required `Changes` array." });
                    return;
                }

                if (changes.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue { Severity = "ERROR", File = relFile, Message = "`Changes` must be an array." });
                    return;
                }

                // Pre-pass: record static `Load` PNG dimensions so `EditData` SpriteIndex can be bounds-checked!
                foreach (var patch in changes.EnumerateArray())
                {
                    if (patch.ValueKind != JsonValueKind.Object)
                        continue;
                    string? act = GetPropIgnoreCase(patch, "Action");
                    string? tgt = GetPropIgnoreCase(patch, "Target");
                    string? ff = GetPropIgnoreCase(patch, "FromFile");
                    if (string.Equals(act, "Load", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(tgt) && !tgt!.Contains("{{") &&
                        !string.IsNullOrWhiteSpace(ff) && !ff!.Contains("{{") &&
                        ff.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        string firstFile = ff.Split(',')[0].Trim();
                        string cand = Path.Combine(modRoot, firstFile.Replace('/', Path.DirectorySeparatorChar));
                        if (TryReadPngSize(cand, out int w, out int h))
                        {
                            foreach (var singleTarget in tgt.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                            {
                                loadedLocalTextures[NormalizeAssetKey(singleTarget)] = (w, h);
                            }
                        }
                    }
                }

                int index = 0;
                foreach (var patch in changes.EnumerateArray())
                {
                    string patchLabel = $"{relFile}#Changes[{index}]";
                    if (patch.ValueKind != JsonValueKind.Object)
                    {
                        issues.Add(new ValidationIssue { Severity = "ERROR", File = patchLabel, Message = "Patch entry must be an object." });
                        index++;
                        continue;
                    }

                    string? action = RequireStringProp(patch, "Action", patchLabel, issues);
                    string? target = null;
                    if (!string.IsNullOrEmpty(action))
                    {
                        if (!ValidCpActions.Contains(action!))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = patchLabel,
                                Message = $"Unknown Content Patcher Action `{action}` (expected Load, EditData, EditImage, EditMap, or Include)."
                            });
                        }
                        else if (!action!.Equals("Include", StringComparison.OrdinalIgnoreCase))
                        {
                            target = RequireStringProp(patch, "Target", patchLabel, issues);
                        }
                    }

                    string? fromFile = GetPropIgnoreCase(patch, "FromFile");
                    string? resolvedFirstStaticFile = null;

                    if (!string.IsNullOrWhiteSpace(fromFile))
                    {
                        CheckTokenBalance(fromFile!, patchLabel, issues);

                        if (!fromFile!.Contains("{{"))
                        {
                            foreach (var singleFile in fromFile.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                            {
                                string candidate = Path.Combine(modRoot, singleFile.Replace('/', Path.DirectorySeparatorChar));
                                if (!File.Exists(candidate))
                                {
                                    issues.Add(new ValidationIssue
                                    {
                                        Severity = "ERROR",
                                        File = patchLabel,
                                        Message = $"`FromFile` target `{singleFile}` does not exist at `{candidate}`."
                                    });
                                }
                                else
                                {
                                    resolvedFirstStaticFile ??= candidate;
                                    if (string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ValidateContentPatcherFile(
                                            modRoot,
                                            candidate,
                                            isRoot: false,
                                            visitedFiles,
                                            engineRules,
                                            referencedI18nKeys,
                                            loadedLocalTextures,
                                            issues);
                                    }
                                }
                            }
                        }
                    }
                    else if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(action, "EditImage", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = patchLabel,
                            Message = $"Action `{action}` requires `FromFile`."
                        });
                    }

                    // Validate EditImage FromArea / ToArea pixel bounds against actual PNG size!
                    if (string.Equals(action, "EditImage", StringComparison.OrdinalIgnoreCase))
                    {
                        ValidateEditImagePatch(patch, resolvedFirstStaticFile, patchLabel, issues);
                    }

                    // Validate EditData against 1.6 GameData schemas, GSQs, TriggerActions, Event commands & SpriteIndex bounds!
                    if (string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(target))
                    {
                        ValidateEditDataPatch(patch, target!, resolvedFirstStaticFile, engineRules, loadedLocalTextures, patchLabel, issues);
                    }

                    // Check When conditions for `Query:` GSQ syntax
                    if (TryGetElementIgnoreCase(patch, "When", out var whenObj) && whenObj.ValueKind == JsonValueKind.Object && engineRules != null)
                    {
                        foreach (var wp in whenObj.EnumerateObject())
                        {
                            if (wp.Name.StartsWith("Query", StringComparison.OrdinalIgnoreCase) && wp.Value.ValueKind == JsonValueKind.String)
                            {
                                string? qVal = wp.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(qVal))
                                    ValidateGsqExpression(qVal!, engineRules, $"{patchLabel}#When.{wp.Name}", issues);
                            }
                        }
                    }

                    CheckTokenBalance(patch.GetRawText(), patchLabel, issues);
                    index++;
                }
            }
        }

        private static void ValidateEditImagePatch(
            JsonElement patch,
            string? sourcePngPath,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            int srcW = 0, srcH = 0;
            bool hasSrcSize = sourcePngPath != null && TryReadPngSize(sourcePngPath, out srcW, out srcH);

            if (TryGetElementIgnoreCase(patch, "FromArea", out var fromArea) && fromArea.ValueKind == JsonValueKind.Object)
            {
                if (TryParseAreaRect(fromArea, out int fx, out int fy, out int fw, out int fh))
                {
                    if (fx < 0 || fy < 0 || fw <= 0 || fh <= 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = patchLabel,
                            Message = $"`FromArea` ({fx},{fy},{fw}x{fh}) has negative origin or non-positive size."
                        });
                    }
                    else if (hasSrcSize && (fx + fw > srcW || fy + fh > srcH))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = patchLabel,
                            Message = $"`FromArea` ({fx},{fy},{fw}x{fh}) exceeds source PNG `{Path.GetFileName(sourcePngPath)}` bounds ({srcW}x{srcH})!"
                        });
                    }
                }
            }

            if (TryGetElementIgnoreCase(patch, "ToArea", out var toArea) && toArea.ValueKind == JsonValueKind.Object)
            {
                if (TryParseAreaRect(toArea, out int tx, out int ty, out int tw, out int th))
                {
                    if (tx < 0 || ty < 0 || tw <= 0 || th <= 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = patchLabel,
                            Message = $"`ToArea` ({tx},{ty},{tw}x{th}) has negative origin or non-positive size."
                        });
                    }
                }
            }
        }

        private static void ValidateEditDataPatch(
            JsonElement patch,
            string rawTarget,
            string? externalJsonPath,
            CachedEngineRules? engineRules,
            Dictionary<string, (int Width, int Height)> loadedLocalTextures,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            if (rawTarget.Contains("{{"))
                return;

            string normTarget = NormalizeAssetKey(rawTarget.Split(',')[0].Trim());

            // Event script validation for Data/Events/<Location>
            bool isEventData = normTarget.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase);

            EngineAssetRule? schemaRule = null;
            if (engineRules != null)
            {
                engineRules.AssetRules.TryGetValue(normTarget, out schemaRule);
            }

            if (TryGetElementIgnoreCase(patch, "Entries", out var entries) && entries.ValueKind == JsonValueKind.Object)
            {
                ValidateDataEntriesObject(normTarget, entries, isEventData, schemaRule, engineRules, loadedLocalTextures, patchLabel, issues);
            }
            else if (!string.IsNullOrEmpty(externalJsonPath) && externalJsonPath!.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var extDoc = JsonDocument.Parse(File.ReadAllText(externalJsonPath), JsoncOptions);
                    if (extDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        ValidateDataEntriesObject(normTarget, extDoc.RootElement, isEventData, schemaRule, engineRules, loadedLocalTextures, patchLabel, issues);
                    }
                }
                catch (Exception ex)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = patchLabel,
                        Message = $"External JSON `{Path.GetFileName(externalJsonPath)}` has invalid syntax: {ex.Message}"
                    });
                }
            }

            if (schemaRule != null && !schemaRule.IsListRoot &&
                TryGetElementIgnoreCase(patch, "Fields", out var fieldsObj) && fieldsObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var entryProp in fieldsObj.EnumerateObject())
                {
                    if (entryProp.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    foreach (var fp in entryProp.Value.EnumerateObject())
                    {
                        if (fp.Name.Contains("{{") || fp.Name.Contains("/"))
                            continue;
                        ValidateSingleModelField(schemaRule, fp.Name, fp.Value, engineRules, $"{patchLabel}#Fields[{entryProp.Name}]", issues);
                    }
                }
            }
        }

        private static void ValidateDataEntriesObject(
            string normTarget,
            JsonElement entries,
            bool isEventData,
            EngineAssetRule? schemaRule,
            CachedEngineRules? engineRules,
            Dictionary<string, (int Width, int Height)> loadedLocalTextures,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            foreach (var entry in entries.EnumerateObject())
            {
                string entryLabel = $"{patchLabel}[{entry.Name}]";
                if (entry.Value.ValueKind == JsonValueKind.Null)
                    continue; // Deleting an entry in CP

                if (isEventData && entry.Value.ValueKind == JsonValueKind.String)
                {
                    string? script = entry.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(script))
                        ValidateEventScriptEntry(entry.Name, script!, engineRules, entryLabel, issues);
                    continue;
                }

                if (schemaRule == null || entry.Value.ValueKind != JsonValueKind.Object)
                    continue;

                string? entryTexture = null;
                int? spriteIndex = null;

                foreach (var fieldProp in entry.Value.EnumerateObject())
                {
                    if (fieldProp.Name.Contains("{{"))
                        continue;

                    ValidateSingleModelField(schemaRule, fieldProp.Name, fieldProp.Value, engineRules, entryLabel, issues);

                    if (fieldProp.Name.Equals("Texture", StringComparison.OrdinalIgnoreCase) && fieldProp.Value.ValueKind == JsonValueKind.String)
                        entryTexture = fieldProp.Value.GetString();
                    else if (fieldProp.Name.Equals("SpriteIndex", StringComparison.OrdinalIgnoreCase) &&
                             fieldProp.Value.ValueKind == JsonValueKind.Number && fieldProp.Value.TryGetInt32(out int si))
                        spriteIndex = si;
                }

                // Check SpriteIndex against local custom Texture dimensions if both are present!
                if (spriteIndex.HasValue)
                {
                    if (spriteIndex.Value < 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = entryLabel,
                            Message = $"`SpriteIndex` ({spriteIndex.Value}) cannot be negative."
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(entryTexture) && !entryTexture!.Contains("{{") &&
                             loadedLocalTextures.TryGetValue(NormalizeAssetKey(entryTexture), out var texDim))
                    {
                        int cellW = 16;
                        int cellH = normTarget.Equals("Data/BigCraftables", StringComparison.OrdinalIgnoreCase) ? 32 : 16;
                        int cols = Math.Max(1, texDim.Width / cellW);
                        int rows = Math.Max(1, texDim.Height / cellH);
                        int maxCount = cols * rows;
                        if (spriteIndex.Value >= maxCount)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = entryLabel,
                                Message = $"`SpriteIndex` {spriteIndex.Value} is out of bounds for custom texture `{entryTexture}` ({texDim.Width}x{texDim.Height} => {maxCount} slot(s) of {cellW}x{cellH}, valid 0..{maxCount - 1})."
                            });
                        }
                    }
                }
            }
        }

        private static void ValidateSingleModelField(
            EngineAssetRule schemaRule,
            string propName,
            JsonElement propValue,
            CachedEngineRules? engineRules,
            string contextLabel,
            List<ValidationIssue> issues)
        {
            if (!schemaRule.Fields.TryGetValue(propName, out var fieldRule))
            {
                string? legacyHint = CSharpModAnalyzer.GetLegacyFieldHint(propName);
                string suffix = legacyHint != null ? $" {legacyHint}" : $" (run `sdv-dev schema {schemaRule.AssetPath}` to view valid 1.6 fields).";
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = contextLabel,
                    Message = $"Unknown field `{propName}` on 1.6 `{schemaRule.ModelTypeName}` (`{schemaRule.AssetPath}`).{suffix}"
                });
                return;
            }

            // Warn if casing differs from official PascalCase C# property
            if (!fieldRule.Name.Equals(propName, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = contextLabel,
                    Message = $"Field `{propName}` differs in casing from official `{schemaRule.ModelTypeName}.{fieldRule.Name}`."
                });
            }

            // Validate JSON value type compatibility (skip if null or string containing CP {{token}})
            if (propValue.ValueKind != JsonValueKind.Null)
            {
                bool isDynamicTokenString = propValue.ValueKind == JsonValueKind.String &&
                                            (propValue.GetString()?.Contains("{{") == true);

                if (!isDynamicTokenString && fieldRule.JsonKind != ExpectedJsonKind.Any)
                {
                    bool mismatch = fieldRule.JsonKind switch
                    {
                        ExpectedJsonKind.Array => propValue.ValueKind != JsonValueKind.Array,
                        ExpectedJsonKind.Object => propValue.ValueKind != JsonValueKind.Object,
                        ExpectedJsonKind.Boolean => propValue.ValueKind != JsonValueKind.True && propValue.ValueKind != JsonValueKind.False &&
                                                    !(propValue.ValueKind == JsonValueKind.String && bool.TryParse(propValue.GetString(), out _)),
                        ExpectedJsonKind.Number => propValue.ValueKind != JsonValueKind.Number &&
                                                   !(propValue.ValueKind == JsonValueKind.String && double.TryParse(propValue.GetString(), out _)),
                        ExpectedJsonKind.String => propValue.ValueKind != JsonValueKind.String && propValue.ValueKind != JsonValueKind.Number,
                        _ => false
                    };

                    if (mismatch)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = contextLabel,
                            Message = $"Type mismatch on `{fieldRule.Name}` (expected `{fieldRule.CSharpType}` / JSON {fieldRule.JsonKind}, got JSON {propValue.ValueKind})."
                        });
                    }
                }
            }

            // Semantic checks on Condition (GSQ), TriggerAction Action/Actions, Machine Sounds, and QualifiedItemId syntax!
            if (engineRules != null)
            {
                if (fieldRule.Name.Equals("Condition", StringComparison.Ordinal) && propValue.ValueKind == JsonValueKind.String)
                {
                    string? cond = propValue.GetString();
                    if (!string.IsNullOrWhiteSpace(cond))
                        ValidateGsqExpression(cond!, engineRules, $"{contextLabel}.Condition", issues);
                }
                else if (schemaRule.AssetPath.Equals("Data/TriggerActions", StringComparison.OrdinalIgnoreCase))
                {
                    if (fieldRule.Name.Equals("Action", StringComparison.Ordinal) && propValue.ValueKind == JsonValueKind.String)
                    {
                        ValidateTriggerActionCommand(propValue.GetString(), engineRules, $"{contextLabel}.Action", issues);
                    }
                    else if (fieldRule.Name.Equals("Actions", StringComparison.Ordinal) && propValue.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var actEl in propValue.EnumerateArray())
                        {
                            if (actEl.ValueKind == JsonValueKind.String)
                                ValidateTriggerActionCommand(actEl.GetString(), engineRules, $"{contextLabel}.Actions", issues);
                        }
                    }
                }
                else if (schemaRule.AssetPath.Equals("Data/Machines", StringComparison.OrdinalIgnoreCase) &&
                         (fieldRule.Name.Equals("LoadEffects", StringComparison.OrdinalIgnoreCase) ||
                          fieldRule.Name.Equals("WorkingEffects", StringComparison.OrdinalIgnoreCase)) &&
                         propValue.ValueKind == JsonValueKind.Array)
                {
                    ValidateMachineEffectSounds(propValue, engineRules, $"{contextLabel}.{fieldRule.Name}", issues);
                }
            }

            // Malformed Qualified Item ID prefix check (e.g. `(BC )Item` or unknown `(XYZ)Item`)
            if ((fieldRule.Name.EndsWith("ItemId", StringComparison.Ordinal) || fieldRule.Name.Equals("QualifiedItemId", StringComparison.Ordinal)) &&
                propValue.ValueKind == JsonValueKind.String)
            {
                string? itemVal = propValue.GetString();
                if (!string.IsNullOrWhiteSpace(itemVal) && !itemVal!.Contains("{{") && itemVal.StartsWith("(", StringComparison.Ordinal))
                {
                    int closeParen = itemVal.IndexOf(')');
                    if (closeParen > 0)
                    {
                        string prefix = itemVal.Substring(0, closeParen + 1);
                        if (!ValidItemTypeQualifiers.Contains(prefix))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = $"{contextLabel}.{fieldRule.Name}",
                                Message = $"Invalid 1.6 item type qualifier `{prefix}` in `{itemVal}` (valid qualifiers: `(O)`, `(BC)`, `(F)`, `(W)`, `(B)`, `(H)`, `(P)`, `(S)`, `(T)`, `(TR)`...)."
                            });
                        }
                    }
                }
            }
        }

        private static void ValidateGsqExpression(
            string expression,
            CachedEngineRules rules,
            string label,
            List<ValidationIssue> issues)
        {
            if (expression.Contains("{{"))
                return;

            // Split top-level comma-separated queries (ignoring quoted sub-queries inside ANY/ALL)
            foreach (var clause in SplitTopLevelCsv(expression))
            {
                string trimmed = clause.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.StartsWith("!", StringComparison.Ordinal))
                    trimmed = trimmed.Substring(1).TrimStart();

                string cmdName = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                if (string.IsNullOrEmpty(cmdName))
                    continue;

                // Custom mod GSQs use `<ModId>_<Name>` or `<Author>.<Mod>_<Name>`
                if (cmdName.Contains('.') || cmdName.Contains('_') && char.IsLower(cmdName[0]))
                    continue;

                if (!rules.ValidGsqNames.Contains(cmdName) && !cmdName.Contains('.'))
                {
                    // Only flag if it doesn't look like a ModUniqueID prefix (`AuthorMod_Query`)
                    bool looksLikeVanillaAttempt = cmdName.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c));
                    if (looksLikeVanillaAttempt)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = label,
                            Message = $"Unknown 1.6 GameStateQuery `{cmdName}` in condition `{expression}`. Run `sdv-dev doc \"{cmdName}\" --cat GameStateQuery` to check exact name."
                        });
                    }
                }
            }
        }

        private static void ValidateTriggerActionCommand(
            string? actionLine,
            CachedEngineRules rules,
            string label,
            List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(actionLine) || actionLine!.Contains("{{"))
                return;

            string cmdName = actionLine.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (string.IsNullOrEmpty(cmdName) || cmdName.Contains('.') || cmdName.Contains('_'))
                return; // Custom mod actions usually contain `.` or `_`

            if (!rules.ValidTriggerActions.Contains(cmdName))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = label,
                    Message = $"Unknown 1.6 TriggerAction `{cmdName}` in `{actionLine}`. Run `sdv-dev doc \"{cmdName}\" --cat TriggerAction`."
                });
            }
        }

        private static void ValidateEventScriptEntry(
            string eventKey,
            string script,
            CachedEngineRules? rules,
            string label,
            List<ValidationIssue> issues)
        {
            if (script.Contains("{{") && !script.Contains("/"))
                return;

            // Forked continuation scripts (key without `/` or starting after fork) can be shorter, standard scripts have >= 3 slash segments
            string[] segments = SplitEventSlashes(script);
            bool hasPreconditionKey = eventKey.Contains("/");
            if (hasPreconditionKey && segments.Length < 3)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = label,
                    Message = $"Event script has only {segments.Length} `/` segment(s); standard Stardew events require at least 3 (`<music> / <viewport x y> / <actors> / ...`)."
                });
                return;
            }

            if (hasPreconditionKey && rules != null && rules.ValidAudioCues.Count > 0)
            {
                string bgm = segments[0].Trim();
                if (!string.IsNullOrEmpty(bgm) && !bgm.Contains("{{"))
                {
                    ValidateAudioCueReference(bgm, rules, $"{label}#bgm", issues);
                }
            }

            int startIdx = hasPreconditionKey ? 3 : 0;
            for (int i = startIdx; i < segments.Length; i++)
            {
                string seg = segments[i].Trim();
                if (string.IsNullOrEmpty(seg) || seg.Contains("{{"))
                    continue;

                // Check unescaped quote balance inside segment
                int unescapedQuotes = 0;
                for (int c = 0; c < seg.Length; c++)
                {
                    if (seg[c] == '"' && (c == 0 || seg[c - 1] != '\\'))
                        unescapedQuotes++;
                }
                if (unescapedQuotes % 2 != 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = $"{label}#cmd[{i}]",
                        Message = $"Unclosed `\"` quote in event command segment: `{Truncate(seg, 70)}`."
                    });
                }

                if (rules != null && rules.ValidEventCommands.Count > 0)
                {
                    string[] cmdParts = seg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    string cmd = cmdParts.FirstOrDefault() ?? "";
                    if (!string.IsNullOrEmpty(cmd) && !cmd.Contains('.') && !cmd.Contains('_') && !rules.ValidEventCommands.Contains(cmd))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{label}#cmd[{i}]",
                            Message = $"Unknown 1.6 Event command `{cmd}` in segment `{Truncate(seg, 60)}`."
                        });
                    }
                    else if (cmdParts.Length >= 2 && rules.ValidAudioCues.Count > 0 &&
                             (cmd.Equals("playSound", StringComparison.OrdinalIgnoreCase) ||
                              cmd.Equals("playMusic", StringComparison.OrdinalIgnoreCase) ||
                              cmd.Equals("music", StringComparison.OrdinalIgnoreCase) ||
                              cmd.Equals("stopSound", StringComparison.OrdinalIgnoreCase)))
                    {
                        string cueArg = cmdParts[1].Trim('"');
                        ValidateAudioCueReference(cueArg, rules, $"{label}#cmd[{i}]({cmd})", issues);
                    }
                }
            }
        }

        [ThreadStatic]
        private static HashSet<string>? _activeModCustomCues;

        private static HashSet<string> CollectModCustomAudioCues(string modRoot)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var jsonFile in Directory.GetFiles(modRoot, "*.json", SearchOption.AllDirectories))
                {
                    if (jsonFile.Contains("\\bin\\") || jsonFile.Contains("\\obj\\") || jsonFile.Contains("\\i18n\\"))
                        continue;
                    string raw = File.ReadAllText(jsonFile);
                    if (raw.IndexOf("AudioChanges", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    using var doc = JsonDocument.Parse(raw, JsoncOptions);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        TryGetElementIgnoreCase(doc.RootElement, "Changes", out var changes) &&
                        changes.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var patch in changes.EnumerateArray())
                        {
                            string? target = GetPropIgnoreCase(patch, "Target");
                            if (target != null && NormalizeAssetKey(target).Equals("Data/AudioChanges", StringComparison.OrdinalIgnoreCase) &&
                                TryGetElementIgnoreCase(patch, "Entries", out var entries) &&
                                entries.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var ep in entries.EnumerateObject())
                                {
                                    set.Add(ep.Name);
                                    if (ep.Value.ValueKind == JsonValueKind.Object)
                                    {
                                        string? idProp = GetPropIgnoreCase(ep.Value, "Id");
                                        if (!string.IsNullOrWhiteSpace(idProp))
                                            set.Add(idProp!);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            _activeModCustomCues = set;
            return set;
        }

        private static void ValidateMachineEffectSounds(
            JsonElement effectsArray,
            CachedEngineRules rules,
            string label,
            List<ValidationIssue> issues)
        {
            if (rules.ValidAudioCues.Count == 0)
                return;

            int idx = 0;
            foreach (var eff in effectsArray.EnumerateArray())
            {
                if (eff.ValueKind == JsonValueKind.Object &&
                    TryGetElementIgnoreCase(eff, "Sounds", out var soundsEl) &&
                    soundsEl.ValueKind == JsonValueKind.Array)
                {
                    int sIdx = 0;
                    foreach (var snd in soundsEl.EnumerateArray())
                    {
                        if (snd.ValueKind == JsonValueKind.Object)
                        {
                            string? sndId = GetPropIgnoreCase(snd, "Id");
                            if (!string.IsNullOrWhiteSpace(sndId) && !sndId!.Contains("{{"))
                            {
                                ValidateAudioCueReference(sndId!, rules, $"{label}[{idx}].Sounds[{sIdx}]", issues);
                            }
                        }
                        sIdx++;
                    }
                }
                idx++;
            }
        }

        private static void ValidateAudioCueReference(
            string cueName,
            CachedEngineRules rules,
            string label,
            List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(cueName) || cueName.Contains("{{"))
                return;
            if (rules.ValidAudioCues.Contains(cueName))
                return;
            if (_activeModCustomCues != null && _activeModCustomCues.Contains(cueName))
                return;

            // Allow mod-prefixed custom cues (e.g. `Author.Mod_Cue`) that may come from prerequisite audio packs
            if (cueName.Contains('.'))
                return;

            string? suggestion = FindClosestAudioCue(cueName, rules.ValidAudioCues);
            string hint = suggestion != null
                ? $" Did you mean `{suggestion}`?"
                : " Run `sdv-dev data <keyword> --asset Audio/Cues` to search valid vanilla cues.";

            issues.Add(new ValidationIssue
            {
                Severity = "WARN",
                File = label,
                Message = $"Unknown vanilla audio/music cue `{cueName}`.{hint}"
            });
        }

        private static string? FindClosestAudioCue(string input, HashSet<string> validCues)
        {
            string? best = null;
            int bestDist = 4; // suggest if edit distance <= 3
            string lowerIn = input.ToLowerInvariant();

            foreach (var cand in validCues)
            {
                if (Math.Abs(cand.Length - lowerIn.Length) > 3)
                    continue;
                int d = ComputeLevenshtein(lowerIn, cand.ToLowerInvariant());
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cand;
                }
            }
            return best;
        }

        private static int ComputeLevenshtein(string a, string b)
        {
            int n = a.Length, m = b.Length;
            int[,] dp = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            }
            return dp[n, m];
        }

        private static void ValidateTranslationsDirectory(
            string modRoot,
            Dictionary<string, string> referencedKeys,
            List<ValidationIssue> issues)
        {
            string i18nDir = Path.Combine(modRoot, "i18n");
            var defaultKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasDefaultFile = false;

            if (Directory.Exists(i18nDir))
            {
                string defaultPath = Path.Combine(i18nDir, "default.json");
                if (File.Exists(defaultPath))
                {
                    hasDefaultFile = true;
                    try
                    {
                        using var d = JsonDocument.Parse(File.ReadAllText(defaultPath), JsoncOptions);
                        if (d.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var p in d.RootElement.EnumerateObject())
                                defaultKeys.Add(p.Name);
                        }
                    }
                    catch
                    {
                        // Syntax error reported in loop below
                    }
                }

                foreach (var i18nFile in Directory.GetFiles(i18nDir, "*.json"))
                {
                    string fileName = Path.GetFileName(i18nFile);
                    string relName = "i18n/" + fileName;
                    try
                    {
                        using var d = JsonDocument.Parse(File.ReadAllText(i18nFile), JsoncOptions);
                        if (d.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            issues.Add(new ValidationIssue { Severity = "ERROR", File = relName, Message = "Translation file root must be a JSON object." });
                            continue;
                        }

                        if (hasDefaultFile && !fileName.Equals("default.json", StringComparison.OrdinalIgnoreCase))
                        {
                            var localeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var p in d.RootElement.EnumerateObject())
                                localeKeys.Add(p.Name);

                            var missingInLocale = defaultKeys.Where(k => !localeKeys.Contains(k)).Take(8).ToList();
                            if (missingInLocale.Count > 0)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "WARN",
                                    File = relName,
                                    Message = $"Locale file is missing {defaultKeys.Count(k => !localeKeys.Contains(k))} key(s) present in `i18n/default.json`: `{string.Join("`, `", missingInLocale)}`."
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new ValidationIssue { Severity = "ERROR", File = relName, Message = $"Invalid JSON syntax: {ex.Message}" });
                    }
                }
            }

            if (referencedKeys.Count > 0)
            {
                if (!hasDefaultFile)
                {
                    var sample = referencedKeys.Keys.Take(5).ToList();
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = "i18n/default.json",
                        Message = $"Mod references {referencedKeys.Count} translation key(s) (`{string.Join("`, `", sample)}`), but `i18n/default.json` is missing!"
                    });
                }
                else
                {
                    foreach (var kv in referencedKeys)
                    {
                        if (!defaultKeys.Contains(kv.Key))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "WARN",
                                File = kv.Value,
                                Message = $"Translation key `{kv.Key}` is referenced in `{kv.Value}` but missing from `i18n/default.json`."
                            });
                        }
                    }
                }
            }
        }

        private static void CollectI18nReferencesFromCpText(string rawJson, string relFile, Dictionary<string, string> dest)
        {
            foreach (Match m in Regex.Matches(rawJson, @"\{\{\s*i18n\s*:\s*([A-Za-z0-9_.\-]+)\s*(?:\||\}\})", RegexOptions.IgnoreCase))
            {
                string key = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(key) && !dest.ContainsKey(key))
                    dest[key] = relFile;
            }
        }

        private static void CollectI18nReferencesFromCSharp(string modRoot, string[] csFiles, Dictionary<string, string> dest)
        {
            foreach (var f in csFiles)
            {
                string rel = f.Substring(modRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) || rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text;
                try { text = File.ReadAllText(f); }
                catch { continue; }

                foreach (Match m in Regex.Matches(text, @"Translation\.Get\s*\(\s*""([A-Za-z0-9_.\-]+)"""))
                {
                    string key = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(key) && !dest.ContainsKey(key))
                        dest[key] = rel;
                }
            }
        }

        private static bool TryReadPngSize(string filePath, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false);
                width = img.Width;
                height = img.Height;
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseAreaRect(JsonElement area, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            return TryGetIntIgnoreCase(area, "X", out x) &&
                   TryGetIntIgnoreCase(area, "Y", out y) &&
                   TryGetIntIgnoreCase(area, "Width", out w) &&
                   TryGetIntIgnoreCase(area, "Height", out h);
        }

        private static bool TryGetIntIgnoreCase(JsonElement obj, string name, out int val)
        {
            val = 0;
            if (!TryGetElementIgnoreCase(obj, name, out var el))
                return false;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out val))
                return true;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out val))
                return true;
            return false;
        }

        private static string NormalizeAssetKey(string raw)
        {
            return raw.Trim().Replace('\\', '/').Trim('/');
        }

        private static List<string> SplitTopLevelCsv(string text)
        {
            var res = new List<string>();
            int start = 0;
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    res.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start <= text.Length)
                res.Add(text.Substring(start));
            return res;
        }

        private static string[] SplitEventSlashes(string script)
        {
            var list = new List<string>();
            int start = 0;
            bool inQuotes = false;
            for (int i = 0; i < script.Length; i++)
            {
                char c = script[i];
                if (c == '"' && (i == 0 || script[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (c == '/' && !inQuotes && (i == 0 || script[i - 1] != '\\'))
                {
                    list.Add(script.Substring(start, i - start));
                    start = i + 1;
                }
            }
            list.Add(script.Substring(start));
            return list.ToArray();
        }

        private static string Truncate(string s, int maxLen) =>
            s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";

        private static void CheckTokenBalance(string text, string fileLabel, List<ValidationIssue> issues)
        {
            int opens = Regex.Matches(text, @"\{\{").Count;
            int closes = Regex.Matches(text, @"\}\}").Count;
            if (opens != closes)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = fileLabel,
                    Message = $"Unbalanced Content Patcher token braces (`{{{{` count={opens}, `}}}}` count={closes})."
                });
            }
        }

        private static string? RequireStringProp(JsonElement obj, string propName, string fileLabel, List<ValidationIssue> issues)
        {
            string? val = GetPropIgnoreCase(obj, propName);
            if (string.IsNullOrWhiteSpace(val))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = fileLabel,
                    Message = $"Missing or empty required property `{propName}`."
                });
                return null;
            }
            return val;
        }

        private static string? GetPropIgnoreCase(JsonElement obj, string propName)
        {
            if (TryGetElementIgnoreCase(obj, propName, out var el))
            {
                return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            }
            return null;
        }

        private static bool TryGetElementIgnoreCase(JsonElement obj, string propName, out JsonElement value)
        {
            if (obj.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in obj.EnumerateObject())
                {
                    if (p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = p.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }
    }
}
