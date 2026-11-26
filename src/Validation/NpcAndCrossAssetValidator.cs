using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using SdvKb.Database;
using SdvKb.Maps;

namespace SdvKb.Validation
{
    internal sealed class VanillaCrossAssetCache
    {
        public string DbPath { get; set; } = string.Empty;
        public long WriteTicks { get; set; }
        public Dictionary<string, HashSet<string>> ItemsByPrefix { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MailIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BuffIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> QuestIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SpecialOrderIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AnimationKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ModCrossAssetContext
    {
        public string ModRoot { get; set; } = string.Empty;
        public string ModId { get; set; } = string.Empty;
        public string? GamePath { get; set; }
        public CachedEngineRules? EngineRules { get; set; }

        public Dictionary<string, HashSet<string>> ItemsByPrefix { get; } =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MailIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BuffIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> QuestIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SpecialOrderIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AnimationKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, (int Width, int Height)> PortraitSizes { get; } =
            new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> LoadedMapsByAsset { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DefaultI18n { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TmxMap?> CachedTmx { get; } =
            new Dictionary<string, TmxMap?>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class NpcAndCrossAssetValidator
    {
        private static readonly object CacheLock = new object();
        private static VanillaCrossAssetCache? _vanillaCache;

        private static readonly JsonDocumentOptions JsoncOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly Dictionary<string, string> AssetToItemPrefix =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Data/Objects"] = "(O)",
                ["Data/BigCraftables"] = "(BC)",
                ["Data/Furniture"] = "(F)",
                ["Data/Weapons"] = "(W)",
                ["Data/Tools"] = "(T)",
                ["Data/Boots"] = "(B)",
                ["Data/Hats"] = "(H)",
                ["Data/Pants"] = "(P)",
                ["Data/Shirts"] = "(S)",
                ["Data/Mannequins"] = "(M)",
                ["Data/Trinkets"] = "(TR)",
                ["Data/Wallpaper"] = "(WP)",
                ["Data/Floors"] = "(FL)"
            };

        private static readonly Dictionary<string, string> PrefixToAssetName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["(O)"] = "Data/Objects",
                ["(BC)"] = "Data/BigCraftables",
                ["(F)"] = "Data/Furniture",
                ["(W)"] = "Data/Weapons",
                ["(T)"] = "Data/Tools",
                ["(B)"] = "Data/Boots",
                ["(H)"] = "Data/Hats",
                ["(P)"] = "Data/Pants",
                ["(S)"] = "Data/Shirts",
                ["(M)"] = "Data/Mannequins",
                ["(TR)"] = "Data/Trinkets"
            };

        public static string ExpandModId(string raw, string modId)
        {
            if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(modId) || raw.IndexOf("ModId", StringComparison.OrdinalIgnoreCase) < 0)
                return raw;
            return Regex.Replace(raw, @"\{\{\s*ModId\s*\}\}", modId, RegexOptions.IgnoreCase);
        }

        public static void RunCrossAssetAndNpcChecks(
            string modRoot,
            string modId,
            string? gamePath,
            CachedEngineRules? engineRules,
            List<ValidationIssue> issues)
        {
            var ctx = BuildContext(modRoot, modId, gamePath, engineRules);
            if (ctx == null)
                return;

            string contentJson = Path.Combine(modRoot, "content.json");
            if (!File.Exists(contentJson))
                return;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ValidateCpTree(ctx, contentJson, visited, issues);
        }

        private static ModCrossAssetContext? BuildContext(
            string modRoot,
            string modId,
            string? explicitGamePath,
            CachedEngineRules? engineRules)
        {
            var cfg = KbConfig.Load(explicitGamePath);
            string? gamePath = engineRules?.GamePath ?? (Directory.Exists(cfg.GamePath) ? cfg.GamePath : null);

            var ctx = new ModCrossAssetContext
            {
                ModRoot = modRoot,
                ModId = modId,
                GamePath = gamePath,
                EngineRules = engineRules
            };

            foreach (var pfx in PrefixToAssetName.Keys)
                ctx.ItemsByPrefix[pfx] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Seed from prebuilt sdv-dev.db vanilla_data + unpacked Data/animationDescriptions.json
            var vanilla = TryGetVanillaCache(cfg.DbPath, gamePath);
            if (vanilla != null)
            {
                foreach (var kv in vanilla.ItemsByPrefix)
                {
                    if (!ctx.ItemsByPrefix.TryGetValue(kv.Key, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ctx.ItemsByPrefix[kv.Key] = set;
                    }
                    set.UnionWith(kv.Value);
                }
                ctx.MailIds.UnionWith(vanilla.MailIds);
                ctx.BuffIds.UnionWith(vanilla.BuffIds);
                ctx.QuestIds.UnionWith(vanilla.QuestIds);
                ctx.SpecialOrderIds.UnionWith(vanilla.SpecialOrderIds);
                ctx.AnimationKeys.UnionWith(vanilla.AnimationKeys);
            }

            // 2. Load i18n/default.json so {{i18n: key}} in Dialogue can also be checked for $q/$r/$portrait errors!
            string defaultI18n = Path.Combine(modRoot, "i18n", "default.json");
            if (File.Exists(defaultI18n))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(defaultI18n), JsoncOpts);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in doc.RootElement.EnumerateObject())
                        {
                            if (p.Value.ValueKind == JsonValueKind.String)
                                ctx.DefaultI18n[p.Name] = p.Value.GetString() ?? string.Empty;
                        }
                    }
                }
                catch { }
            }

            // 3. Pre-scan all CP JSON files in this mod to collect newly registered IDs, Portraits, and custom Maps!
            string rootContent = Path.Combine(modRoot, "content.json");
            if (File.Exists(rootContent))
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectModDeclarations(ctx, rootContent, visited);
            }

            return ctx;
        }

        private static VanillaCrossAssetCache? TryGetVanillaCache(string dbPath, string? gamePath)
        {
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
                return null;

            long ticks = File.GetLastWriteTimeUtc(dbPath).Ticks;
            lock (CacheLock)
            {
                if (_vanillaCache != null &&
                    _vanillaCache.DbPath.Equals(dbPath, StringComparison.OrdinalIgnoreCase) &&
                    _vanillaCache.WriteTicks == ticks)
                {
                    return _vanillaCache;
                }

                try
                {
                    var built = new VanillaCrossAssetCache { DbPath = dbPath, WriteTicks = ticks };
                    foreach (var pfx in PrefixToAssetName.Keys)
                        built.ItemsByPrefix[pfx] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    using (var db = new KbDatabase(dbPath))
                    using (var cmd = db.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT asset_name, entry_key FROM vanilla_data;";
                        using var r = cmd.ExecuteReader();
                        while (r.Read())
                        {
                            string asset = r.GetString(0);
                            string key = r.GetString(1);
                            if (AssetToItemPrefix.TryGetValue(asset, out string? pfx))
                            {
                                if (!built.ItemsByPrefix.TryGetValue(pfx, out var set))
                                {
                                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    built.ItemsByPrefix[pfx] = set;
                                }
                                set.Add(key);
                            }
                            else if (asset.Equals("Data/Mail", StringComparison.OrdinalIgnoreCase))
                                built.MailIds.Add(key);
                            else if (asset.Equals("Data/Buffs", StringComparison.OrdinalIgnoreCase))
                                built.BuffIds.Add(key);
                            else if (asset.Equals("Data/Quests", StringComparison.OrdinalIgnoreCase))
                                built.QuestIds.Add(key);
                            else if (asset.Equals("Data/SpecialOrders", StringComparison.OrdinalIgnoreCase))
                                built.SpecialOrderIds.Add(key);
                            else if (asset.Equals("Data/animationDescriptions", StringComparison.OrdinalIgnoreCase))
                                built.AnimationKeys.Add(key);
                        }
                    }

                    // Also ensure unpacked Data/animationDescriptions.json & Data/Mail.json are completely loaded if unpacked exists
                    if (!string.IsNullOrWhiteSpace(gamePath))
                    {
                        LoadUnpackedDictKeys(Path.Combine(gamePath!, "Content (unpacked)", "Data", "animationDescriptions.json"), built.AnimationKeys);
                        LoadUnpackedDictKeys(Path.Combine(gamePath!, "Content (unpacked)", "Data", "Mail.json"), built.MailIds);
                        LoadUnpackedDictKeys(Path.Combine(gamePath!, "Content (unpacked)", "Data", "Quests.json"), built.QuestIds);
                        LoadUnpackedDictKeys(Path.Combine(gamePath!, "Content (unpacked)", "Data", "Buffs.json"), built.BuffIds);
                        LoadUnpackedDictKeys(Path.Combine(gamePath!, "Content (unpacked)", "Data", "SpecialOrders.json"), built.SpecialOrderIds);
                    }

                    _vanillaCache = built;
                    return built;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static void LoadUnpackedDictKeys(string jsonFile, HashSet<string> dest)
        {
            if (!File.Exists(jsonFile))
                return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonFile), JsoncOpts);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                        dest.Add(p.Name);
                }
            }
            catch { }
        }

        private static void CollectModDeclarations(ModCrossAssetContext ctx, string jsonFile, HashSet<string> visited)
        {
            string full = Path.GetFullPath(jsonFile);
            if (!visited.Add(full) || !File.Exists(full))
                return;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(full), JsoncOpts);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !TryGetProp(doc.RootElement, "Changes", out var changes) ||
                    changes.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (var patch in changes.EnumerateArray())
                {
                    if (patch.ValueKind != JsonValueKind.Object)
                        continue;

                    string? action = GetStr(patch, "Action");
                    string? rawTarget = GetStr(patch, "Target");
                    string? rawFrom = GetStr(patch, "FromFile");

                    if (string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rawFrom))
                    {
                        string expFrom = ExpandModId(rawFrom!, ctx.ModId);
                        if (!expFrom.Contains("{{"))
                        {
                            foreach (var f in expFrom.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                            {
                                CollectModDeclarations(ctx, Path.Combine(ctx.ModRoot, f.Replace('/', Path.DirectorySeparatorChar)), visited);
                            }
                        }
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(rawTarget))
                        continue;

                    foreach (var singleRawTgt in rawTarget!.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                    {
                        string normTarget = NormalizeAsset(ExpandModId(singleRawTgt, ctx.ModId));
                        if (normTarget.Contains("{{"))
                            continue;

                        string? resolvedFrom = null;
                        if (!string.IsNullOrWhiteSpace(rawFrom))
                        {
                            string expFrom = ExpandModId(rawFrom!.Split(',')[0].Trim(), ctx.ModId);
                            if (!expFrom.Contains("{{"))
                            {
                                string cand = Path.Combine(ctx.ModRoot, expFrom.Replace('/', Path.DirectorySeparatorChar));
                                if (File.Exists(cand))
                                    resolvedFrom = cand;
                            }
                        }

                        if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase) && resolvedFrom != null)
                        {
                            if (normTarget.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase) &&
                                resolvedFrom.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                                TryGetPngSize(resolvedFrom, out int pw, out int ph))
                            {
                                string npcName = normTarget.Substring("Portraits/".Length);
                                ctx.PortraitSizes[npcName] = (pw, ph);
                            }
                            else if (normTarget.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase) &&
                                     resolvedFrom.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
                            {
                                ctx.LoadedMapsByAsset[normTarget] = resolvedFrom;
                            }
                            else if (resolvedFrom.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    using var extDoc = JsonDocument.Parse(File.ReadAllText(resolvedFrom), JsoncOpts);
                                    RegisterDeclaredEntries(ctx, normTarget, extDoc.RootElement);
                                }
                                catch { }
                            }
                        }
                        else if (string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetProp(patch, "TargetField", out _))
                                continue;
                            if (TryGetProp(patch, "Entries", out var entries))
                                RegisterDeclaredEntries(ctx, normTarget, entries);
                        }
                    }
                }
            }
            catch { }
        }

        private static void RegisterDeclaredEntries(ModCrossAssetContext ctx, string normTarget, JsonElement entries)
        {
            if (entries.ValueKind != JsonValueKind.Object)
                return;

            foreach (var p in entries.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Null)
                    continue;

                string key = ExpandModId(p.Name, ctx.ModId);
                if (key.Contains("{{"))
                    continue;

                if (AssetToItemPrefix.TryGetValue(normTarget, out string? pfx))
                {
                    if (!ctx.ItemsByPrefix.TryGetValue(pfx, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ctx.ItemsByPrefix[pfx] = set;
                    }
                    set.Add(key);
                }
                else if (normTarget.Equals("Data/Mail", StringComparison.OrdinalIgnoreCase))
                    ctx.MailIds.Add(key);
                else if (normTarget.Equals("Data/Buffs", StringComparison.OrdinalIgnoreCase))
                    ctx.BuffIds.Add(key);
                else if (normTarget.Equals("Data/Quests", StringComparison.OrdinalIgnoreCase))
                    ctx.QuestIds.Add(key);
                else if (normTarget.Equals("Data/SpecialOrders", StringComparison.OrdinalIgnoreCase))
                    ctx.SpecialOrderIds.Add(key);
                else if (normTarget.Equals("Data/animationDescriptions", StringComparison.OrdinalIgnoreCase))
                    ctx.AnimationKeys.Add(key);
            }
        }

        private static void ValidateCpTree(
            ModCrossAssetContext ctx,
            string jsonFile,
            HashSet<string> visited,
            List<ValidationIssue> issues)
        {
            string full = Path.GetFullPath(jsonFile);
            if (!visited.Add(full) || !File.Exists(full))
                return;

            string relFile = full.StartsWith(ctx.ModRoot, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(ctx.ModRoot.Length).TrimStart('\\', '/').Replace('\\', '/')
                : Path.GetFileName(full);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(full), JsoncOpts);
            }
            catch
            {
                return; // Syntax errors already reported by ModValidator
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !TryGetProp(doc.RootElement, "Changes", out var changes) ||
                    changes.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                int idx = 0;
                foreach (var patch in changes.EnumerateArray())
                {
                    string patchLabel = $"{relFile}#Changes[{idx}]";
                    idx++;
                    if (patch.ValueKind != JsonValueKind.Object)
                        continue;

                    string? action = GetStr(patch, "Action");
                    string? rawTarget = GetStr(patch, "Target");
                    string? rawFrom = GetStr(patch, "FromFile");

                    if (string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rawFrom))
                    {
                        string expFrom = ExpandModId(rawFrom!, ctx.ModId);
                        if (!expFrom.Contains("{{"))
                        {
                            foreach (var f in expFrom.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                                ValidateCpTree(ctx, Path.Combine(ctx.ModRoot, f.Replace('/', Path.DirectorySeparatorChar)), visited, issues);
                        }
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(rawTarget))
                        continue;
                    if (TryGetProp(patch, "TargetField", out _))
                        continue;

                    string normTarget = NormalizeAsset(ExpandModId(rawTarget!.Split(',')[0].Trim(), ctx.ModId));
                    if (normTarget.Contains("{{"))
                        continue;

                    JsonDocument? extDoc = null;
                    JsonElement? entriesEl = null;

                    if (string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase) && TryGetProp(patch, "Entries", out var ent))
                    {
                        entriesEl = ent;
                    }
                    else if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rawFrom))
                    {
                        string expFrom = ExpandModId(rawFrom!.Split(',')[0].Trim(), ctx.ModId);
                        if (!expFrom.Contains("{{") && expFrom.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            string cand = Path.Combine(ctx.ModRoot, expFrom.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(cand))
                            {
                                try
                                {
                                    extDoc = JsonDocument.Parse(File.ReadAllText(cand), JsoncOpts);
                                    entriesEl = extDoc.RootElement;
                                }
                                catch { }
                            }
                        }
                    }

                    using (extDoc)
                    {
                        if (entriesEl == null)
                            continue;

                        ValidateTargetEntries(ctx, normTarget, entriesEl.Value, patchLabel, issues);
                    }
                }
            }
        }

        private static void ValidateTargetEntries(
            ModCrossAssetContext ctx,
            string normTarget,
            JsonElement entries,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            // 1. NPC Schedules: Characters/schedules/<NPC>
            if (normTarget.StartsWith("Characters/schedules/", StringComparison.OrdinalIgnoreCase) &&
                entries.ValueKind == JsonValueKind.Object)
            {
                string npcName = normTarget.Substring("Characters/schedules/".Length);
                ValidateNpcScheduleAsset(ctx, npcName, entries, patchLabel, issues);
                return;
            }

            // 2. NPC Dialogue & Portrait Frame Check: Characters/Dialogue/<NPC>
            if (normTarget.StartsWith("Characters/Dialogue/", StringComparison.OrdinalIgnoreCase) &&
                entries.ValueKind == JsonValueKind.Object)
            {
                string sub = normTarget.Substring("Characters/Dialogue/".Length);
                string npcName = sub.StartsWith("MarriageDialogue", StringComparison.OrdinalIgnoreCase) && sub.Length > "MarriageDialogue".Length
                    ? sub.Substring("MarriageDialogue".Length)
                    : sub;
                if (!npcName.Equals("rainy", StringComparison.OrdinalIgnoreCase) &&
                    !npcName.Equals("MarriageDialogue", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNpcDialogueAsset(ctx, npcName, entries, patchLabel, issues);
                }
                return;
            }

            // 3. Slash-delimited Recipes: Data/CookingRecipes & Data/CraftingRecipes
            if ((normTarget.Equals("Data/CookingRecipes", StringComparison.OrdinalIgnoreCase) ||
                 normTarget.Equals("Data/CraftingRecipes", StringComparison.OrdinalIgnoreCase)) &&
                entries.ValueKind == JsonValueKind.Object)
            {
                bool isCrafting = normTarget.Equals("Data/CraftingRecipes", StringComparison.OrdinalIgnoreCase);
                ValidateRecipesAsset(ctx, entries, isCrafting, patchLabel, issues);
                return;
            }

            // 4. Cross-Asset Foreign Keys on 1.6 Data Models!
            if (ctx.ItemsByPrefix["(O)"].Count == 0)
                return; // Skip foreign key checks if vanilla DB was not populated yet

            if (normTarget.Equals("Data/Crops", StringComparison.OrdinalIgnoreCase) && entries.ValueKind == JsonValueKind.Object)
            {
                foreach (var ep in entries.EnumerateObject())
                {
                    if (ep.Value.ValueKind != JsonValueKind.Object) continue;
                    string lbl = $"{patchLabel}[{ep.Name}]";
                    // Key of Data/Crops is the unqualified/qualified Seed ItemId!
                    CheckItemIdExists(ctx, ep.Name, defaultPrefix: "(O)", $"{lbl}(SeedKey)", issues, allowCategoryInt: false);
                    if (GetStr(ep.Value, "HarvestItemId") is string hid)
                        CheckItemIdExists(ctx, hid, defaultPrefix: "(O)", $"{lbl}.HarvestItemId", issues, allowCategoryInt: false);
                }
            }
            else if (normTarget.Equals("Data/FruitTrees", StringComparison.OrdinalIgnoreCase) && entries.ValueKind == JsonValueKind.Object)
            {
                foreach (var ep in entries.EnumerateObject())
                {
                    if (ep.Value.ValueKind != JsonValueKind.Object) continue;
                    string lbl = $"{patchLabel}[{ep.Name}]";
                    if (TryGetProp(ep.Value, "Fruit", out var fruitArr) && fruitArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var fr in fruitArr.EnumerateArray())
                        {
                            if (fr.ValueKind == JsonValueKind.Object && GetStr(fr, "ItemId") is string fid)
                                CheckItemIdExists(ctx, fid, defaultPrefix: "(O)", $"{lbl}.Fruit.ItemId", issues, allowCategoryInt: false);
                        }
                    }
                }
            }
            else if (normTarget.Equals("Data/Machines", StringComparison.OrdinalIgnoreCase) && entries.ValueKind == JsonValueKind.Object)
            {
                foreach (var ep in entries.EnumerateObject())
                {
                    if (ep.Value.ValueKind != JsonValueKind.Object) continue;
                    string lbl = $"{patchLabel}[{ep.Name}]";
                    CheckItemIdExists(ctx, ep.Name, defaultPrefix: "(BC)", $"{lbl}(MachineKey)", issues, allowCategoryInt: false);

                    if (TryGetProp(ep.Value, "OutputRules", out var rulesArr) && rulesArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var rule in rulesArr.EnumerateArray())
                        {
                            if (rule.ValueKind != JsonValueKind.Object) continue;
                            if (TryGetProp(rule, "Triggers", out var trigs) && trigs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in trigs.EnumerateArray())
                                {
                                    if (t.ValueKind == JsonValueKind.Object && GetStr(t, "RequiredItemId") is string reqId)
                                        CheckItemIdExists(ctx, reqId, defaultPrefix: "(O)", $"{lbl}.Triggers.RequiredItemId", issues, allowCategoryInt: false);
                                }
                            }
                            if (TryGetProp(rule, "OutputItem", out var outs) && outs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var o in outs.EnumerateArray())
                                {
                                    if (o.ValueKind == JsonValueKind.Object && GetStr(o, "ItemId") is string outId)
                                        CheckItemIdExists(ctx, outId, defaultPrefix: "(O)", $"{lbl}.OutputItem.ItemId", issues, allowCategoryInt: false, allowSpecialMachineTokens: true);
                                }
                            }
                        }
                    }
                }
            }
            else if (normTarget.Equals("Data/Shops", StringComparison.OrdinalIgnoreCase) && entries.ValueKind == JsonValueKind.Object)
            {
                foreach (var ep in entries.EnumerateObject())
                {
                    if (ep.Value.ValueKind != JsonValueKind.Object) continue;
                    string lbl = $"{patchLabel}[{ep.Name}]";
                    if (TryGetProp(ep.Value, "Items", out var itemsArr) && itemsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var it in itemsArr.EnumerateArray())
                        {
                            if (it.ValueKind == JsonValueKind.Object && GetStr(it, "ItemId") is string sid)
                                CheckItemIdExists(ctx, sid, defaultPrefix: "(O)", $"{lbl}.Items.ItemId", issues, allowCategoryInt: false, allowItemQueries: true);
                        }
                    }
                }
            }
            else if (normTarget.Equals("Data/Buildings", StringComparison.OrdinalIgnoreCase) && entries.ValueKind == JsonValueKind.Object)
            {
                foreach (var ep in entries.EnumerateObject())
                {
                    if (ep.Value.ValueKind != JsonValueKind.Object) continue;
                    string lbl = $"{patchLabel}[{ep.Name}]";
                    if (TryGetProp(ep.Value, "BuildMaterials", out var mats) && mats.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in mats.EnumerateArray())
                        {
                            if (m.ValueKind == JsonValueKind.Object && GetStr(m, "ItemId") is string mid)
                                CheckItemIdExists(ctx, mid, defaultPrefix: "(O)", $"{lbl}.BuildMaterials.ItemId", issues, allowCategoryInt: false);
                        }
                    }
                }
            }
            else if (normTarget.Equals("Data/TriggerActions", StringComparison.OrdinalIgnoreCase))
            {
                if (entries.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ep in entries.EnumerateObject())
                    {
                        if (ep.Value.ValueKind == JsonValueKind.Object)
                            CheckTriggerActionForeignKeys(ctx, ep.Value, $"{patchLabel}[{ep.Name}]", issues);
                    }
                }
                else if (entries.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var item in entries.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            string id = GetStr(item, "Id") ?? i.ToString();
                            CheckTriggerActionForeignKeys(ctx, item, $"{patchLabel}[{id}]", issues);
                        }
                        i++;
                    }
                }
            }
        }

        private static void CheckTriggerActionForeignKeys(
            ModCrossAssetContext ctx,
            JsonElement taObj,
            string label,
            List<ValidationIssue> issues)
        {
            if (GetStr(taObj, "Action") is string act)
                CheckSingleTriggerActionRefs(ctx, act, $"{label}.Action", issues);

            if (TryGetProp(taObj, "Actions", out var acts) && acts.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in acts.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                        CheckSingleTriggerActionRefs(ctx, el.GetString()!, $"{label}.Actions", issues);
                }
            }
        }

        private static void CheckSingleTriggerActionRefs(
            ModCrossAssetContext ctx,
            string rawAction,
            string label,
            List<ValidationIssue> issues)
        {
            string expanded = ExpandModId(rawAction.Trim(), ctx.ModId);
            if (string.IsNullOrEmpty(expanded) || expanded.Contains("{{"))
                return;

            string[] parts = SplitQuoteAware(expanded);
            if (parts.Length < 2)
                return;

            string cmd = parts[0];
            if (cmd.Equals("AddItem", StringComparison.OrdinalIgnoreCase) || cmd.Equals("RemoveItem", StringComparison.OrdinalIgnoreCase))
            {
                CheckItemIdExists(ctx, parts[1], defaultPrefix: "(O)", $"{label}({cmd})", issues, allowCategoryInt: false);
            }
            else if (cmd.Equals("AddMail", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                string mailId = parts[2];
                // Note: AddMail can also set invisible mail flags; emit WARN if mail type is `now` or `tomorrow` (actual letter!) and missing from Data/Mail
                string mailType = parts.Length >= 4 ? parts[3] : "tomorrow";
                if ((mailType.Equals("now", StringComparison.OrdinalIgnoreCase) || mailType.Equals("tomorrow", StringComparison.OrdinalIgnoreCase)) &&
                    ctx.MailIds.Count > 0 && !ctx.MailIds.Contains(mailId))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = $"{label}(AddMail)",
                        Message = $"Cross-asset broken reference: `AddMail` sends letter `{mailId}` ({mailType}) to mailbox, but `{mailId}` does not exist in `Data/Mail`!"
                    });
                }
            }
            else if (cmd.Equals("AddBuff", StringComparison.OrdinalIgnoreCase) && ctx.BuffIds.Count > 0 && !ctx.BuffIds.Contains(parts[1]))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = $"{label}(AddBuff)",
                    Message = $"Cross-asset broken reference: Buff ID `{parts[1]}` does not exist in `Data/Buffs`."
                });
            }
            else if (cmd.Equals("AddQuest", StringComparison.OrdinalIgnoreCase) && ctx.QuestIds.Count > 0 && !ctx.QuestIds.Contains(parts[1]))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = $"{label}(AddQuest)",
                    Message = $"Cross-asset broken reference: Quest ID `{parts[1]}` does not exist in `Data/Quests`."
                });
            }
            else if (cmd.Equals("AddSpecialOrder", StringComparison.OrdinalIgnoreCase) && ctx.SpecialOrderIds.Count > 0 && !ctx.SpecialOrderIds.Contains(parts[1]))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = $"{label}(AddSpecialOrder)",
                    Message = $"Cross-asset broken reference: SpecialOrder ID `{parts[1]}` does not exist in `Data/SpecialOrders`."
                });
            }
        }

        private static void ValidateNpcScheduleAsset(
            ModCrossAssetContext ctx,
            string npcName,
            JsonElement entries,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            var localScheduleKeys = new HashSet<string>(
                entries.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            var standardFallbackKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "spring", "summer", "fall", "winter", "default", "season", "no_schedule", "rain", "rain2", "marriage_Mon", "marriage_Fri"
            };

            foreach (var prop in entries.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                    continue;

                string schedKey = prop.Name;
                string raw = ExpandModId(prop.Value.GetString() ?? "", ctx.ModId).Trim();
                string entryLabel = $"{patchLabel}[{schedKey}]";

                if (string.IsNullOrEmpty(raw) || raw.Contains("{{"))
                    continue;

                string[] parts = raw.Split('/');
                int idx = 0;

                // Handle prefix directives: NOT friendship ... / MAIL <id> / GOTO <target>
                while (idx < parts.Length)
                {
                    string seg = parts[idx].Trim();
                    if (string.IsNullOrEmpty(seg))
                    {
                        idx++;
                        continue;
                    }

                    string[] tok = SplitQuoteAware(seg);
                    if (tok.Length == 0)
                    {
                        idx++;
                        continue;
                    }

                    if (tok[0].Equals("GOTO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tok.Length < 2)
                        {
                            issues.Add(new ValidationIssue { Severity = "ERROR", File = entryLabel, Message = "Schedule `GOTO` is missing target schedule key." });
                        }
                        else
                        {
                            string gotoTarget = tok[1];
                            if (gotoTarget.Equals(schedKey, StringComparison.OrdinalIgnoreCase))
                            {
                                issues.Add(new ValidationIssue { Severity = "ERROR", File = entryLabel, Message = $"Schedule `{schedKey}` has infinite self-loop `GOTO {gotoTarget}`." });
                            }
                            else if (!localScheduleKeys.Contains(gotoTarget) && !standardFallbackKeys.Contains(gotoTarget))
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "WARN",
                                    File = entryLabel,
                                    Message = $"Schedule `GOTO {gotoTarget}` references schedule key `{gotoTarget}` not defined in `{npcName}`'s schedule patch."
                                });
                            }
                        }
                        idx = parts.Length; // GOTO terminates schedule chain
                        break;
                    }
                    else if (tok[0].Equals("NOT", StringComparison.OrdinalIgnoreCase))
                    {
                        idx++;
                        continue;
                    }
                    else if (tok[0].Equals("MAIL", StringComparison.OrdinalIgnoreCase))
                    {
                        if (tok.Length < 2)
                            issues.Add(new ValidationIssue { Severity = "ERROR", File = entryLabel, Message = "Schedule `MAIL` condition is missing mail ID." });
                        idx++;
                        continue;
                    }
                    break;
                }

                int prevTime = -1;
                string currentMap = string.Empty;

                for (; idx < parts.Length; idx++)
                {
                    string seg = parts[idx].Trim();
                    if (string.IsNullOrEmpty(seg))
                        continue;

                    string[] tok = SplitQuoteAware(seg);
                    if (tok.Length == 0)
                        continue;

                    if (tok[0].Equals("GOTO", StringComparison.OrdinalIgnoreCase))
                        break;

                    // Parse time token (`610`, `0900`, or arrival time `a1300`)
                    string timeStr = tok[0].StartsWith("a", StringComparison.OrdinalIgnoreCase) ? tok[0].Substring(1) : tok[0];
                    if (!int.TryParse(timeStr, out int timeVal))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{entryLabel}#leg[{idx}]",
                            Message = $"Invalid schedule time `{tok[0]}` in `{seg}` (expected integer clock time like `610`, `1200`, or `a1430`)."
                        });
                        continue;
                    }

                    int mins = timeVal % 100;
                    if (timeVal < 600 || timeVal > 2600 || mins >= 60)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{entryLabel}#leg[{idx}]",
                            Message = $"Invalid Stardew clock time `{tok[0]}` in `{seg}` (valid range `600`..`2600`, minutes `00`..`59`)."
                        });
                    }
                    else if (prevTime >= 0 && timeVal <= prevTime)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{entryLabel}#leg[{idx}]",
                            Message = $"Non-increasing schedule time `{tok[0]}` after `{prevTime}` in `{seg}` (NPC schedule legs must be strictly chronological)."
                        });
                    }
                    prevTime = timeVal;

                    if (tok.Length == 2 && (tok[1].Equals("bed", StringComparison.OrdinalIgnoreCase) || tok[1].Equals("bus", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Format A: `<time> <location> <x> <y> [facing] [anim] ["dialogue"]`
                    // Format B: `<time> <x> <y> [facing] [anim] ["dialogue"]` (stays on currentMap)
                    int cursor = 1;
                    if (cursor < tok.Length && !int.TryParse(tok[cursor], out _))
                    {
                        currentMap = tok[cursor];
                        cursor++;
                    }

                    if (cursor + 1 >= tok.Length || !int.TryParse(tok[cursor], out int tx) || !int.TryParse(tok[cursor + 1], out int ty))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{entryLabel}#leg[{idx}]",
                            Message = $"Schedule leg `{seg}` is missing integer `<X> <Y>` target tile coordinates."
                        });
                        continue;
                    }
                    cursor += 2;

                    if (cursor < tok.Length && int.TryParse(tok[cursor], out int facing))
                    {
                        if (facing < 0 || facing > 3)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "WARN",
                                File = $"{entryLabel}#leg[{idx}]",
                                Message = $"Schedule leg `{seg}` has facing direction `{facing}` outside `0..3`."
                            });
                        }
                        cursor++;
                    }

                    // Optional animation or Strings\ dialogue
                    while (cursor < tok.Length)
                    {
                        string extra = tok[cursor++];
                        if (extra.StartsWith("\"", StringComparison.Ordinal) ||
                            extra.StartsWith("Strings\\", StringComparison.OrdinalIgnoreCase) ||
                            extra.StartsWith("Strings/", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!extra.StartsWith("square_", StringComparison.OrdinalIgnoreCase) &&
                            !extra.Equals("bed", StringComparison.OrdinalIgnoreCase) &&
                            ctx.AnimationKeys.Count > 0 &&
                            !ctx.AnimationKeys.Contains(extra))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = $"{entryLabel}#leg[{idx}]",
                                Message = $"Schedule animation `{extra}` does not exist in `Data/animationDescriptions`!"
                            });
                        }
                    }

                    // Offline target tile bounds & passability check if `.tmx` map is available locally!
                    if (!string.IsNullOrEmpty(currentMap))
                    {
                        var tmx = ResolveTmxForLocation(ctx, currentMap);
                        if (tmx != null)
                        {
                            if (tx < 0 || ty < 0 || tx >= tmx.Width || ty >= tmx.Height)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = $"{entryLabel}#leg[{idx}]",
                                    Message = $"Schedule destination `{currentMap} ({tx}, {ty})` is outside map bounds (`{tmx.Width}x{tmx.Height}`)."
                                });
                            }
                            else if (!IsTilePassableInTmx(tmx, tx, ty))
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "WARN",
                                    File = $"{entryLabel}#leg[{idx}]",
                                    Message = $"Schedule destination `{currentMap} ({tx}, {ty})` is blocked by a `Buildings` / `Water` / `Impassable` tile (`PathFindController` may stall)."
                                });
                            }
                        }
                    }
                }
            }
        }

        private static TmxMap? ResolveTmxForLocation(ModCrossAssetContext ctx, string locationName)
        {
            string assetKey = $"Maps/{locationName}";
            if (ctx.CachedTmx.TryGetValue(assetKey, out var cached))
                return cached;

            string? tmxPath = null;
            if (ctx.LoadedMapsByAsset.TryGetValue(assetKey, out string? modMapPath) && File.Exists(modMapPath))
            {
                tmxPath = modMapPath;
            }
            else if (!string.IsNullOrWhiteSpace(ctx.GamePath))
            {
                try
                {
                    string resolvedMap = TmxMap.ResolveMapFilePath(locationName, ctx.GamePath!);
                    if (File.Exists(resolvedMap))
                        tmxPath = resolvedMap;
                }
                catch { }
            }

            if (tmxPath == null)
            {
                ctx.CachedTmx[assetKey] = null;
                return null;
            }

            try
            {
                var loaded = TmxMap.Load(tmxPath);
                ctx.CachedTmx[assetKey] = loaded;
                return loaded;
            }
            catch
            {
                ctx.CachedTmx[assetKey] = null;
                return null;
            }
        }

        private static bool IsTilePassableInTmx(TmxMap map, int x, int y)
        {
            return !map.IsWaterTile(x, y) && map.IsTilePassable(x, y, out _);
        }

        private static void ValidateNpcDialogueAsset(
            ModCrossAssetContext ctx,
            string npcName,
            JsonElement entries,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            int? portraitFrameCount = ResolvePortraitFrameCount(ctx, npcName, patchLabel, issues);

            foreach (var prop in entries.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                    continue;

                string rawVal = prop.Value.GetString() ?? string.Empty;
                string entryLabel = $"{patchLabel}[{prop.Name}]";

                // Resolve pure {{i18n: key}} from i18n/default.json so dialogue syntax & portrait bounds inside i18n are checked too!
                var pureI18nMatch = Regex.Match(rawVal.Trim(), @"^\{\{\s*i18n\s*:\s*([a-zA-Z0-9_.\-]+)\s*\}\}$", RegexOptions.IgnoreCase);
                if (pureI18nMatch.Success)
                {
                    string i18nKey = pureI18nMatch.Groups[1].Value;
                    if (!ctx.DefaultI18n.TryGetValue(i18nKey, out string? resolvedText))
                        continue;
                    rawVal = resolvedText;
                    entryLabel = $"{entryLabel}(i18n:{i18nKey})";
                }

                string text = ExpandModId(rawVal, ctx.ModId);
                if (string.IsNullOrEmpty(text) || text.Contains("{{"))
                    continue;

                ValidateSingleDialogueString(ctx, npcName, text, portraitFrameCount, entryLabel, issues);
            }
        }

        private static int? ResolvePortraitFrameCount(
            ModCrossAssetContext ctx,
            string npcName,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            (int W, int H)? size = null;
            if (ctx.PortraitSizes.TryGetValue(npcName, out var modSize))
            {
                size = modSize;
            }
            else if (!string.IsNullOrWhiteSpace(ctx.GamePath))
            {
                string unpackedPng = Path.Combine(ctx.GamePath!, "Content (unpacked)", "Portraits", npcName + ".png");
                if (TryGetPngSize(unpackedPng, out int vw, out int vh))
                {
                    size = (vw, vh);
                }
                else
                {
                    string? xnbPng = XnbOnDemandExtractor.ResolveOrExtractTexturePng($"Portraits/{npcName}", ctx.GamePath!);
                    if (xnbPng != null && TryGetPngSize(xnbPng, out int xw, out int xh))
                        size = (xw, xh);
                }
            }

            if (size == null)
                return null;

            int w = size.Value.W;
            int h = size.Value.H;
            if (w < 64 || h < 64 || (w % 64) != 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = patchLabel,
                    Message = $"Portrait sheet for `{npcName}` has unusual dimensions `{w}x{h}` (expected width multiple of `64`, e.g. `128x192` or `128x320` for `64x64` frames)."
                });
            }

            int cols = Math.Max(1, w / 64);
            int rows = Math.Max(1, h / 64);
            return cols * rows;
        }

        private static void ValidateSingleDialogueString(
            ModCrossAssetContext ctx,
            string npcName,
            string masterString,
            int? portraitFrames,
            string entryLabel,
            List<ValidationIssue> issues)
        {
            // Catch common unclosed `#$b` / `#$e` typos (e.g. `你好#$b今天天气不错` missing trailing `#`!)
            var missingHashMatch = Regex.Match(masterString, @"#\$([ebk])(?![#\s$]|$)", RegexOptions.IgnoreCase);
            if (missingHashMatch.Success)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = entryLabel,
                    Message = $"Malformed dialogue break `#{missingHashMatch.Value}...`: missing closing `#` (should be `#${missingHashMatch.Groups[1].Value}#`)."
                });
            }

            foreach (var variant in masterString.Split(new[] { "||" }, StringSplitOptions.None))
            {
                string[] chunks = variant.Split('#');
                bool sawQuestionInVariant = false;
                int responseCountAfterQuestion = 0;

                for (int i = 0; i < chunks.Length; i++)
                {
                    string chunk = chunks[i];
                    if (string.IsNullOrEmpty(chunk))
                        continue;

                    if (chunk.StartsWith("$", StringComparison.Ordinal))
                    {
                        string[] cmdTokens = chunk.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        string cmd = cmdTokens[0];
                        string args = cmdTokens.Length > 1 ? cmdTokens[1].Trim() : string.Empty;

                        if (cmd == "$q")
                        {
                            sawQuestionInVariant = true;
                            string[] qArgs = SplitQuoteAware(args);
                            if (qArgs.Length < 2)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Malformed `$q` dialogue command `${chunk.Substring(1)}`: expected `$q <id1/id2> <fallbackKey>#<questionText>`."
                                });
                            }
                            if (i + 1 >= chunks.Length)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Dialogue `$q` command is missing `#<questionText>` after `$q`."
                                });
                            }
                            continue;
                        }
                        else if (cmd == "$r")
                        {
                            responseCountAfterQuestion++;
                            string[] rArgs = SplitQuoteAware(args);
                            if (rArgs.Length < 3 || !int.TryParse(rArgs[1], out _))
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Malformed `$r` dialogue response `${chunk.Substring(1)}`: expected `$r <responseId> <int friendshipDelta> <followupKey>#<optionText>`."
                                });
                            }
                            if (i + 1 >= chunks.Length)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Dialogue `$r` response is missing `#<optionText>`."
                                });
                            }
                            continue;
                        }
                        else if (cmd == "$p")
                        {
                            if (string.IsNullOrWhiteSpace(args) || i + 2 >= chunks.Length)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Malformed `$p` prerequisite dialogue: expected `$p <dialogueId>#<textIfSeen>#<textIfNotSeen>`."
                                });
                            }
                            continue;
                        }
                        else if (cmd == "$1")
                        {
                            if (string.IsNullOrWhiteSpace(args) || i + 2 >= chunks.Length)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Malformed `$1` one-time dialogue: expected `$1 <uniqueMessageId>#<firstTimeText>#<laterText>`."
                                });
                            }
                            continue;
                        }
                        else if (cmd == "$query" && ctx.EngineRules != null && !string.IsNullOrWhiteSpace(args))
                        {
                            ModValidator.ValidateGsqExpressionPublic(args, ctx.EngineRules, $"{entryLabel}($query)", issues);
                            continue;
                        }
                        else if (cmd == "$action" && ctx.EngineRules != null && !string.IsNullOrWhiteSpace(args))
                        {
                            ModValidator.ValidateTriggerActionCommandPublic(args, ctx.EngineRules, $"{entryLabel}($action)", issues);
                            CheckSingleTriggerActionRefs(ctx, args, $"{entryLabel}($action)", issues);
                            continue;
                        }
                        else if (cmd == "$e" || cmd == "$b" || cmd == "$k" || cmd == "$c" || cmd == "$t" || cmd == "$d" || cmd == "$y")
                        {
                            continue;
                        }
                    }

                    // Check trailing portrait emotion specifier on regular dialogue text (e.g. `Hello!$h`, `Whoa!$7`)
                    if (portraitFrames.HasValue)
                    {
                        var portMatch = Regex.Match(chunk, @"\$([hsula]|\d+)\s*$");
                        if (portMatch.Success)
                        {
                            string tok = portMatch.Groups[1].Value;
                            int frameIdx = tok switch
                            {
                                "h" => 1,
                                "s" => 2,
                                "u" => 3,
                                "l" => 4,
                                "a" => 5,
                                _ => int.TryParse(tok, out int n) ? n : 0
                            };

                            if (frameIdx >= portraitFrames.Value)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "ERROR",
                                    File = entryLabel,
                                    Message = $"Dialogue portrait emotion `${tok}` (frame index {frameIdx}) exceeds `Portraits/{npcName}.png` capacity ({portraitFrames.Value} frame(s), valid `0..{portraitFrames.Value - 1}`)!"
                                });
                            }
                        }
                    }
                }

                if (sawQuestionInVariant && responseCountAfterQuestion == 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = entryLabel,
                        Message = "Dialogue uses `$q` question without any `$r` player response options!"
                    });
                }
            }
        }

        private static void ValidateRecipesAsset(
            ModCrossAssetContext ctx,
            JsonElement entries,
            bool isCrafting,
            string patchLabel,
            List<ValidationIssue> issues)
        {
            foreach (var prop in entries.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                    continue;

                string raw = ExpandModId(prop.Value.GetString() ?? "", ctx.ModId).Trim();
                string entryLabel = $"{patchLabel}[{prop.Name}]";
                if (string.IsNullOrEmpty(raw) || raw.Contains("{{"))
                    continue;

                string[] fields = raw.Split('/');
                int minFields = isCrafting ? 5 : 4;
                if (fields.Length < minFields)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = entryLabel,
                        Message = $"{(isCrafting ? "Crafting" : "Cooking")} recipe has only {fields.Length} `/` field(s); expected at least {minFields}."
                    });
                    continue;
                }

                // Field 0: `<itemId> <count> [<itemId> <count> ...]`
                string[] ingTokens = fields[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (ingTokens.Length == 0 || ingTokens.Length % 2 != 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = $"{entryLabel}#ingredients",
                        Message = $"Recipe ingredients `{fields[0]}` has {ingTokens.Length} token(s); must be `<ItemId> <Count>` pairs (even token count)."
                    });
                }
                else
                {
                    for (int i = 0; i < ingTokens.Length; i += 2)
                    {
                        string ingId = ingTokens[i];
                        if (!int.TryParse(ingTokens[i + 1], out int cnt) || cnt <= 0)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = $"{entryLabel}#ingredients",
                                Message = $"Ingredient `{ingId}` has invalid quantity `{ingTokens[i + 1]}` (expected positive integer)."
                            });
                        }
                        if (ctx.ItemsByPrefix["(O)"].Count > 0)
                        {
                            CheckItemIdExists(ctx, ingId, defaultPrefix: "(O)", $"{entryLabel}#ingredient[{ingId}]", issues, allowCategoryInt: true);
                        }
                    }
                }

                // Field 3 on CraftingRecipes: `isBigCraftable` (`true` or `false`)
                bool yieldsBigCraftable = false;
                if (isCrafting)
                {
                    string bcFlag = fields[3].Trim();
                    if (!bool.TryParse(bcFlag, out yieldsBigCraftable))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = $"{entryLabel}#isBigCraftable",
                            Message = $"CraftingRecipe field[3] (`isBigCraftable`) must be `true` or `false`, got `{bcFlag}`."
                        });
                    }
                }

                // Field 2: `<yieldItemId> [count]`
                string[] yieldTokens = fields[2].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (yieldTokens.Length == 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = $"{entryLabel}#yield",
                        Message = "Recipe yield field[2] is empty."
                    });
                }
                else if (ctx.ItemsByPrefix["(O)"].Count > 0)
                {
                    string expectedPrefix = yieldsBigCraftable ? "(BC)" : "(O)";
                    CheckItemIdExists(ctx, yieldTokens[0], defaultPrefix: expectedPrefix, $"{entryLabel}#yield", issues, allowCategoryInt: false);
                }
            }
        }

        private static void CheckItemIdExists(
            ModCrossAssetContext ctx,
            string rawItemId,
            string defaultPrefix,
            string label,
            List<ValidationIssue> issues,
            bool allowCategoryInt,
            bool allowSpecialMachineTokens = false,
            bool allowItemQueries = false)
        {
            string id = ExpandModId(rawItemId.Trim(), ctx.ModId);
            if (string.IsNullOrEmpty(id) || id.Contains("{{"))
                return;

            if (allowSpecialMachineTokens && (id.StartsWith("DROP_IN", StringComparison.OrdinalIgnoreCase) || id.Contains("DROP_IN_ID")))
                return;

            if (allowItemQueries && (id.StartsWith("ALL_ITEMS", StringComparison.OrdinalIgnoreCase) ||
                                     id.StartsWith("RANDOM_ITEMS", StringComparison.OrdinalIgnoreCase) ||
                                     id.StartsWith("LOST_BOOK", StringComparison.OrdinalIgnoreCase) ||
                                     id.StartsWith("SECRET_NOTE", StringComparison.OrdinalIgnoreCase) ||
                                     id.Contains(' ')))
            {
                return;
            }

            if (allowCategoryInt && int.TryParse(id, out int numId) && numId < 0)
                return; // Negative category IDs like -4 (Fish), -5 (Egg), -6 (Milk), -777 (Wild Seeds)

            // Allow context_tag in recipe ingredients if it starts with item_ / category_
            if (allowCategoryInt && (id.StartsWith("item_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("category_", StringComparison.OrdinalIgnoreCase)))
                return;

            string prefix = defaultPrefix;
            string bareKey = id;

            if (id.StartsWith("(", StringComparison.Ordinal))
            {
                int close = id.IndexOf(')');
                if (close > 1)
                {
                    prefix = id.Substring(0, close + 1);
                    bareKey = id.Substring(close + 1);
                }
            }

            if (!ctx.ItemsByPrefix.TryGetValue(prefix, out var targetSet) || targetSet.Count == 0)
                return;

            if (targetSet.Contains(bareKey))
                return;

            // Check if the item actually exists under a DIFFERENT type qualifier (e.g. wrote `(O)MyKeg` or unqualified `MyKeg` for a `(BC)` BigCraftable!)
            foreach (var kv in ctx.ItemsByPrefix)
            {
                if (!kv.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase) && kv.Value.Contains(bareKey))
                {
                    string actualAsset = PrefixToAssetName.TryGetValue(kv.Key, out string? aName) ? aName : kv.Key;
                    string expectedAsset = PrefixToAssetName.TryGetValue(prefix, out string? eName) ? eName : prefix;
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = label,
                        Message = $"Cross-asset type mismatch (spawns Error Item!): `{rawItemId}` looked up `{bareKey}` in `{expectedAsset}`, but `{bareKey}` is registered in `{actualAsset}`! Use `{kv.Key}{bareKey}`."
                    });
                    return;
                }
            }

            string targetAssetName = PrefixToAssetName.TryGetValue(prefix, out string? tName) ? tName : prefix;
            issues.Add(new ValidationIssue
            {
                Severity = "ERROR",
                File = label,
                Message = $"Cross-asset broken ID (spawns Error Item!): `{rawItemId}` (`{bareKey}`) was not found in vanilla or mod `{targetAssetName}`."
            });
        }

        private static string NormalizeAsset(string raw) => raw.Trim().Replace('\\', '/').Trim('/');

        private static bool TryGetProp(JsonElement obj, string name, out JsonElement val)
        {
            if (obj.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in obj.EnumerateObject())
                {
                    if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        val = p.Value;
                        return true;
                    }
                }
            }
            val = default;
            return false;
        }

        private static string? GetStr(JsonElement obj, string name)
        {
            if (TryGetProp(obj, name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }

        private static bool TryGetPngSize(string path, out int w, out int h)
        {
            w = 0;
            h = 0;
            if (!File.Exists(path))
                return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false);
                w = img.Width;
                h = img.Height;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] SplitQuoteAware(string input)
        {
            var list = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '"' && (i == 0 || input[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && char.IsWhiteSpace(ch))
                {
                    if (cur.Length > 0)
                    {
                        list.Add(cur.ToString());
                        cur.Clear();
                    }
                }
                else
                {
                    cur.Append(ch);
                }
            }
            if (cur.Length > 0)
                list.Add(cur.ToString());
            return list.ToArray();
        }
    }
}
