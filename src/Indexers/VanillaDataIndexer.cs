using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SdvKb.Database;

namespace SdvKb.Indexers
{
    public static class VanillaDataIndexer
    {
        private static readonly JsonDocumentOptions LenientJsonOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static string GetUnpackedPath(string gamePath) => Path.Combine(gamePath, "Content (unpacked)");

        public static bool EnsureUnpacked(string gamePath, bool runUnpackerIfNeeded)
        {
            string unpackedDir = GetUnpackedPath(gamePath);
            string objectsJson = Path.Combine(unpackedDir, "Data", "Objects.json");
            if (File.Exists(objectsJson))
                return true;

            if (!runUnpackerIfNeeded)
                return false;

            string xnbHackExe = Path.Combine(gamePath, "StardewXnbHack.exe");
            if (!File.Exists(xnbHackExe))
            {
                Console.Error.WriteLine($"[WARN] StardewXnbHack.exe not found in {gamePath}; skipping Content unpack.");
                return false;
            }

            Console.WriteLine("Running StardewXnbHack.exe in minimized console to unpack vanilla Content...");
            var psi = new ProcessStartInfo
            {
                FileName = xnbHackExe,
                WorkingDirectory = gamePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                bool exited = proc.WaitForExit(120_000);
                if (!exited)
                {
                    // Check if unpacking finished and process is merely sitting at Press any key
                    string dataDir = Path.Combine(unpackedDir, "Data");
                    if (Directory.Exists(dataDir) && Directory.GetFiles(dataDir, "*.json").Length > 40)
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
            }

            return File.Exists(objectsJson);
        }

        public sealed class XactCueInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = "Sound";
            public bool IsComplex { get; set; }
            public string Source { get; set; } = "XACT";
        }

        /// <summary>
        /// Parses MonoGame XACT `FarmerSounds.xgs` + `Sound Bank.xsb` (plus 1.6 `Data/AudioChanges.json` if unpacked)
        /// directly from the game's `Content\XACT` folder using zero-lock memory streams.
        /// </summary>
        public static List<XactCueInfo> ExtractVanillaAudioCues(string gamePath)
        {
            var byName = new Dictionary<string, XactCueInfo>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
                return new List<XactCueInfo>();

            string xactDir = Path.Combine(gamePath, "Content", "XACT");
            string xgsPath = Path.Combine(xactDir, "FarmerSounds.xgs");
            string xsbPath = Path.Combine(xactDir, "Sound Bank.xsb");

            var categories = new List<string>();
            if (File.Exists(xgsPath))
            {
                try
                {
                    byte[] xgsBytes = File.ReadAllBytes(xgsPath);
                    using var ms = new MemoryStream(xgsBytes, writable: false);
                    using var br = new BinaryReader(ms, Encoding.UTF8);
                    if (br.ReadUInt32() == 1179862872u) // "XGSF"
                    {
                        ms.Seek(19, SeekOrigin.Begin);
                        uint numCategories = br.ReadUInt16();
                        ms.Seek(57, SeekOrigin.Begin);
                        uint categoryNamesOffset = br.ReadUInt32();
                        if (categoryNamesOffset > 0 && categoryNamesOffset < ms.Length)
                        {
                            ms.Seek(categoryNamesOffset, SeekOrigin.Begin);
                            for (int i = 0; i < numCategories && ms.Position < ms.Length; i++)
                            {
                                var sb = new StringBuilder();
                                byte b;
                                while (ms.Position < ms.Length && (b = br.ReadByte()) != 0)
                                    sb.Append((char)b);
                                categories.Add(sb.ToString());
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            string ResolveCategory(uint catId) =>
                catId < categories.Count && !string.IsNullOrWhiteSpace(categories[(int)catId])
                    ? categories[(int)catId]
                    : (catId == 2 ? "Music" : "Sound");

            if (File.Exists(xsbPath))
            {
                try
                {
                    byte[] xsbBytes = File.ReadAllBytes(xsbPath);
                    using var ms = new MemoryStream(xsbBytes, writable: false);
                    using var br = new BinaryReader(ms, Encoding.UTF8);
                    if (br.ReadUInt32() == 1262634067u) // "SDBK"
                    {
                        br.ReadUInt16(); // toolVersion
                        br.ReadUInt16(); // formatVersion
                        br.ReadUInt16(); // crc
                        br.ReadUInt32(); // lastModifiedLow
                        br.ReadUInt32(); // lastModifiedHigh
                        br.ReadByte();   // platform
                        uint numSimpleCues = br.ReadUInt16();
                        uint numComplexCues = br.ReadUInt16();
                        br.ReadUInt16();
                        br.ReadUInt16();
                        br.ReadByte();   // numWaveBanks
                        br.ReadUInt16(); // numSounds
                        uint cueNameTableLen = br.ReadUInt16();
                        br.ReadUInt16();
                        uint simpleCuesOffset = br.ReadUInt32();
                        uint complexCuesOffset = br.ReadUInt32();
                        uint cueNamesOffset = br.ReadUInt32();

                        ms.Seek(cueNamesOffset, SeekOrigin.Begin);
                        string[] cueNames = Encoding.UTF8.GetString(br.ReadBytes((int)cueNameTableLen)).Split('\0');

                        if (numSimpleCues > 0)
                        {
                            ms.Seek(simpleCuesOffset, SeekOrigin.Begin);
                            for (int j = 0; j < numSimpleCues && j < cueNames.Length; j++)
                            {
                                br.ReadByte();
                                uint soundOffset = br.ReadUInt32();
                                long savedPos = ms.Position;
                                string cat = "Sound";
                                if (soundOffset + 3 <= ms.Length)
                                {
                                    ms.Seek(soundOffset + 1, SeekOrigin.Begin);
                                    cat = ResolveCategory(br.ReadUInt16());
                                }
                                ms.Seek(savedPos, SeekOrigin.Begin);

                                string name = cueNames[j];
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    byName[name] = new XactCueInfo { Name = name, Category = cat, IsComplex = false, Source = "XACT" };
                                }
                            }
                        }

                        if (numComplexCues > 0)
                        {
                            ms.Seek(complexCuesOffset, SeekOrigin.Begin);
                            for (int k = 0; k < numComplexCues && (numSimpleCues + k) < cueNames.Length; k++)
                            {
                                byte flags = br.ReadByte();
                                uint targetOffset = br.ReadUInt32();
                                br.ReadUInt32();
                                long savedPos = ms.Position;

                                string cat = "Sound";
                                try
                                {
                                    if (((flags >> 2) & 1) != 0)
                                    {
                                        if (targetOffset + 3 <= ms.Length)
                                        {
                                            ms.Seek(targetOffset + 1, SeekOrigin.Begin);
                                            cat = ResolveCategory(br.ReadUInt16());
                                        }
                                    }
                                    else if (targetOffset + 8 <= ms.Length)
                                    {
                                        ms.Seek(targetOffset, SeekOrigin.Begin);
                                        uint entryCount = br.ReadUInt16();
                                        ushort varFlags = br.ReadUInt16();
                                        br.ReadByte();
                                        br.ReadUInt16();
                                        br.ReadByte();
                                        uint tableType = ((uint)varFlags >> 3) & 7u;
                                        if (entryCount > 0 && (tableType == 1u || tableType == 3u) && ms.Position + 4 <= ms.Length)
                                        {
                                            uint firstSoundOffset = br.ReadUInt32();
                                            if (firstSoundOffset + 3 <= ms.Length)
                                            {
                                                ms.Seek(firstSoundOffset + 1, SeekOrigin.Begin);
                                                cat = ResolveCategory(br.ReadUInt16());
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }

                                ms.Seek(savedPos, SeekOrigin.Begin);
                                br.ReadByte();   // instanceLimit
                                br.ReadUInt16();
                                br.ReadUInt16();
                                br.ReadByte();   // limitBehavior

                                string name = cueNames[numSimpleCues + k];
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    byName[name] = new XactCueInfo { Name = name, Category = cat, IsComplex = true, Source = "XACT" };
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            // Merge 1.6 Data/AudioChanges.json if unpacked Content exists
            string audioChangesPath = Path.Combine(GetUnpackedPath(gamePath), "Data", "AudioChanges.json");
            if (File.Exists(audioChangesPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(audioChangesPath), LenientJsonOptions);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in doc.RootElement.EnumerateObject())
                        {
                            string id = TryGetJsonString(p.Value, "Id") ?? p.Name;
                            string cat = TryGetJsonString(p.Value, "Category") ?? "Sound";
                            if (!string.IsNullOrWhiteSpace(id) && !byName.ContainsKey(id))
                            {
                                byName[id] = new XactCueInfo { Name = id, Category = cat, IsComplex = false, Source = "Data/AudioChanges" };
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return byName.Values.OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<VanillaDataRecord> IndexUnpackedContent(string gamePath)
        {
            var records = new List<VanillaDataRecord>();

            // Always index XACT Sound Bank Cues (works directly off Content\XACT\Sound Bank.xsb even before unpacking!)
            foreach (var cue in ExtractVanillaAudioCues(gamePath))
            {
                string zhCat = cue.Category.ToLowerInvariant() switch
                {
                    "music" => "背景音乐 BGM",
                    "ambient" => "环境音 Ambient",
                    "footsteps" => "脚步声 Footsteps",
                    _ => "音效 SFX"
                };
                records.Add(new VanillaDataRecord
                {
                    AssetName = "Audio/Cues",
                    EntryKey = cue.Name,
                    EnName = $"{cue.Name} ({cue.Category})",
                    ZhName = zhCat,
                    Summary = $"XACT Audio Cue | Category: `{cue.Category}` | MultiTrack/Variant: {cue.IsComplex} | Source: `{cue.Source}`",
                    RawJson = JsonSerializer.Serialize(new
                    {
                        CueId = cue.Name,
                        Category = cue.Category,
                        IsComplexVariant = cue.IsComplex,
                        Source = cue.Source
                    }, new JsonSerializerOptions { WriteIndented = true })
                });
            }

            string unpackedDir = GetUnpackedPath(gamePath);
            string dataDir = Path.Combine(unpackedDir, "Data");
            string stringsDir = Path.Combine(unpackedDir, "Strings");

            if (!Directory.Exists(dataDir))
                return records;

            // 1. Load English & Chinese string tables from Strings/*.json and Strings/*.zh-CN.json
            var enStrings = LoadLocalizationStrings(stringsDir, zhCn: false);
            var zhStrings = LoadLocalizationStrings(stringsDir, zhCn: true);

            // Also load NPCNames / Festivals / etc. from Data if present
            var qualifiedNames = new Dictionary<string, (string En, string Zh)>(StringComparer.OrdinalIgnoreCase);

            // 2. First pass: Index Objects.json and BigCraftables.json so qualified IDs ((O)xxx, (BC)xxx) can enrich Machines, Crops, Recipes!
            string objectsFile = Path.Combine(dataDir, "Objects.json");
            if (File.Exists(objectsFile))
            {
                IndexJsonAssetFile(objectsFile, "Data/Objects", enStrings, zhStrings, qualifiedNames, "(O)", records);
            }

            string bcFile = Path.Combine(dataDir, "BigCraftables.json");
            if (File.Exists(bcFile))
            {
                IndexJsonAssetFile(bcFile, "Data/BigCraftables", enStrings, zhStrings, qualifiedNames, "(BC)", records);
            }

            // 3. Second pass: Index all remaining Data/**/*.json files (except very noisy dialogue/animation frames if too granular, keep Events!)
            foreach (var file in Directory.GetFiles(dataDir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
            {
                if (file.Equals(objectsFile, StringComparison.OrdinalIgnoreCase) ||
                    file.Equals(bcFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                string rel = file.Substring(unpackedDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                // Skip language variants under Data/ (e.g., *.zh-CN.json, *.fr-FR.json, *.es-ES.json, etc.)
                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                if (Regex.IsMatch(fileNameNoExt, @"\.[a-z]{2}-[A-Z]{2}$") ||
                    Regex.IsMatch(fileNameNoExt, @"\.(de-DE|es-ES|fr-FR|hu-HU|it-IT|ja-JP|ko-KR|pt-BR|ru-RU|tr-TR|zh-CN)$", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                string assetName = rel.Substring(0, rel.Length - ".json".Length).Replace('\\', '/');
                string? qualPrefix = assetName switch
                {
                    "Data/Weapons" => "(W)",
                    "Data/Pants" => "(P)",
                    "Data/Shirts" => "(S)",
                    "Data/Tools" => "(T)",
                    "Data/Trinkets" => "(TR)",
                    _ => null
                };

                IndexJsonAssetFile(file, assetName, enStrings, zhStrings, qualifiedNames, qualPrefix, records);
            }

            return records;
        }

        private static Dictionary<string, string> LoadLocalizationStrings(string stringsDir, bool zhCn)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(stringsDir))
                return dict;

            foreach (var file in Directory.GetFiles(stringsDir, "*.json", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                bool isZhFile = fileName.EndsWith(".zh-CN", StringComparison.OrdinalIgnoreCase);
                if (zhCn != isZhFile)
                    continue;
                if (!zhCn && Regex.IsMatch(fileName, @"\.[a-z]{2}-[A-Z]{2}$", RegexOptions.IgnoreCase))
                    continue;

                string rel = file.Substring(stringsDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string baseAsset = rel.Substring(0, rel.Length - ".json".Length);
                if (zhCn && baseAsset.EndsWith(".zh-CN", StringComparison.OrdinalIgnoreCase))
                    baseAsset = baseAsset.Substring(0, baseAsset.Length - ".zh-CN".Length);

                string normPrefix = "Strings/" + baseAsset.Replace('\\', '/');

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file), LenientJsonOptions);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string val = prop.Value.GetString() ?? string.Empty;
                            dict[$"{normPrefix}:{prop.Name}"] = val;
                        }
                    }
                }
                catch
                {
                    // Skip unreadable string file
                }
            }

            return dict;
        }

        private static void IndexJsonAssetFile(
            string filePath,
            string assetName,
            Dictionary<string, string> enStrings,
            Dictionary<string, string> zhStrings,
            Dictionary<string, (string En, string Zh)> qualifiedNames,
            string? registerQualPrefix,
            List<VanillaDataRecord> output)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(filePath), LenientJsonOptions);
            }
            catch
            {
                return;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string key = prop.Name;
                        JsonElement val = prop.Value;

                        (string enName, string zhName, string summary) = ExtractEntryMetadata(
                            assetName, key, val, enStrings, zhStrings, qualifiedNames);

                        if (registerQualPrefix != null && (!string.IsNullOrEmpty(enName) || !string.IsNullOrEmpty(zhName)))
                        {
                            qualifiedNames[$"{registerQualPrefix}{key}"] = (enName, zhName);
                            // Also store bare key fallback for Objects if unambiguous
                            if (registerQualPrefix == "(O)" && !qualifiedNames.ContainsKey(key))
                                qualifiedNames[key] = (enName, zhName);
                        }

                        string rawJson = FormatElementJson(val, maxChars: 6000);
                        output.Add(new VanillaDataRecord
                        {
                            AssetName = assetName,
                            EntryKey = key,
                            EnName = enName,
                            ZhName = zhName,
                            Summary = summary,
                            RawJson = rawJson
                        });
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int idx = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        string key = TryGetJsonString(item, "Id") ?? TryGetJsonString(item, "ID") ?? TryGetJsonString(item, "Name") ?? $"[{idx}]";
                        (string enName, string zhName, string summary) = ExtractEntryMetadata(
                            assetName, key, item, enStrings, zhStrings, qualifiedNames);

                        output.Add(new VanillaDataRecord
                        {
                            AssetName = assetName,
                            EntryKey = key,
                            EnName = enName,
                            ZhName = zhName,
                            Summary = summary,
                            RawJson = FormatElementJson(item, maxChars: 6000)
                        });
                        idx++;
                    }
                }
            }
        }

        private static (string EnName, string ZhName, string Summary) ExtractEntryMetadata(
            string assetName,
            string key,
            JsonElement val,
            Dictionary<string, string> enStrings,
            Dictionary<string, string> zhStrings,
            Dictionary<string, (string En, string Zh)> qualifiedNames)
        {
            string enName = string.Empty;
            string zhName = string.Empty;
            string summary = string.Empty;

            if (val.ValueKind == JsonValueKind.Object)
            {
                string? rawName = TryGetJsonString(val, "Name") ?? TryGetJsonString(val, "Id");
                string? dispToken = TryGetJsonString(val, "DisplayName") ?? TryGetJsonString(val, "Title");
                string? descToken = TryGetJsonString(val, "Description");

                if (!string.IsNullOrEmpty(dispToken))
                {
                    enName = ResolveLocalizedText(dispToken!, enStrings);
                    zhName = ResolveLocalizedText(dispToken!, zhStrings);
                }

                if (string.IsNullOrEmpty(enName) && !string.IsNullOrEmpty(rawName))
                    enName = rawName!;

                if (!string.IsNullOrEmpty(descToken))
                {
                    string zhDesc = ResolveLocalizedText(descToken!, zhStrings);
                    string enDesc = ResolveLocalizedText(descToken!, enStrings);
                    summary = !string.IsNullOrEmpty(zhDesc) ? zhDesc : enDesc;
                }

                // Enrich Data/Machines (key is e.g. "(BC)12") from qualifiedNames!
                if (string.IsNullOrEmpty(enName) && qualifiedNames.TryGetValue(key, out var qMatch))
                {
                    enName = qMatch.En;
                    zhName = qMatch.Zh;
                }

                // Enrich Data/Crops (HarvestItemId -> e.g. "24" or "(O)24")
                if (assetName.Equals("Data/Crops", StringComparison.OrdinalIgnoreCase))
                {
                    string? harvestId = TryGetJsonString(val, "HarvestItemId");
                    if (!string.IsNullOrEmpty(harvestId))
                    {
                        if (qualifiedNames.TryGetValue("(O)" + harvestId, out var hMatch) || qualifiedNames.TryGetValue(harvestId!, out hMatch))
                        {
                            enName = hMatch.En;
                            zhName = hMatch.Zh;
                        }
                    }
                    if (qualifiedNames.TryGetValue("(O)" + key, out var seedMatch))
                    {
                        summary = $"Seed: {seedMatch.Zh} ({seedMatch.En}) -> Harvest ID: {harvestId}";
                    }
                }

                // Enrich Characters (NPCs)
                if (assetName.Equals("Data/Characters", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(enName))
                        enName = key;
                    if (string.IsNullOrEmpty(zhName) && zhStrings.TryGetValue($"Strings/NPCNames:{key}", out var npcZh))
                        zhName = npcZh;
                }
            }
            else if (val.ValueKind == JsonValueKind.String)
            {
                // Slash-delimited legacy formats: CraftingRecipes, CookingRecipes, Boots, Hats, Furniture, Fish, Monsters, Quests
                string rawStr = val.GetString() ?? string.Empty;
                string[] parts = rawStr.Split('/');

                if (assetName.Equals("Data/CraftingRecipes", StringComparison.OrdinalIgnoreCase) ||
                    assetName.Equals("Data/CookingRecipes", StringComparison.OrdinalIgnoreCase))
                {
                    enName = key;
                    string lookupKey = key.Replace(" ", "");
                    if (zhStrings.TryGetValue($"Strings/Objects:CraftingRecipe_{lookupKey}", out var crZh))
                        zhName = crZh;

                    // Also check output item in parts[2] (e.g. "12" or "324 10" or "BigChest")
                    if (parts.Length > 2)
                    {
                        string outItem = parts[2].Split(' ')[0];
                        bool isBigCraftable = assetName.Equals("Data/CraftingRecipes", StringComparison.OrdinalIgnoreCase)
                            && parts.Length > 3 && parts[3].Equals("true", StringComparison.OrdinalIgnoreCase);
                        string qKey = isBigCraftable ? $"(BC){outItem}" : $"(O){outItem}";
                        if (string.IsNullOrEmpty(zhName) && qualifiedNames.TryGetValue(qKey, out var outMatch))
                            zhName = outMatch.Zh;
                        summary = $"Ingredients: {parts[0]} | Output: {qKey}";
                    }
                }
                else if (assetName.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase))
                {
                    enName = key.Split('/')[0];
                    summary = $"Event Precondition: `{key}` | Script length: {rawStr.Length} chars";
                }
                else
                {
                    // Generic slash-delimited entry (Fish, Hats, Boots, Furniture, Monsters, Quests)
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                        enName = parts[0];
                    // Last part or localized token often holds localized display name
                    foreach (var p in parts)
                    {
                        if (p.Contains("[LocalizedText"))
                        {
                            string zh = ResolveLocalizedText(p, zhStrings);
                            if (!string.IsNullOrEmpty(zh) && string.IsNullOrEmpty(zhName))
                                zhName = zh;
                        }
                    }
                    summary = rawStr.Length > 140 ? rawStr.Substring(0, 140) + "..." : rawStr;
                }
            }

            if (enName == zhName)
                zhName = string.Empty;

            return (enName, zhName, summary);
        }

        private static string ResolveLocalizedText(string text, Dictionary<string, string> table)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Pattern: [LocalizedText Strings\Objects:Weeds_Name]
            return Regex.Replace(text, @"\[LocalizedText\s+([^\]]+)\]", m =>
            {
                string token = m.Groups[1].Value.Trim().Replace("\\\\", "/").Replace('\\', '/');
                if (table.TryGetValue(token, out var val))
                    return val;
                return string.Empty;
            }).Trim();
        }

        private static string? TryGetJsonString(JsonElement obj, string propName)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propName, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            return null;
        }

        private static string FormatElementJson(JsonElement el, int maxChars)
        {
            string formatted = JsonSerializer.Serialize(el, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            if (formatted.Length <= maxChars)
                return formatted;
            return formatted.Substring(0, maxChars) + "\n  // ... (truncated)";
        }
    }
}
