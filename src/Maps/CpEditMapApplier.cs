using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SdvKb.Maps
{
    public sealed class CpEditMapResult
    {
        public TmxMap Map { get; set; } = null!;
        public string BaseTmxPath { get; set; } = string.Empty;
        public string ModRoot { get; set; } = string.Empty;
        public List<string> AppliedPatches { get; } = new List<string>();
        public List<Rectangle> PatchedTileRects { get; } = new List<Rectangle>();
        public Dictionary<string, string> CustomTextureAssets { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates Content Patcher `Action: "Load"` and `Action: "EditMap"` (`FromFile` PatchMode Replace/Overlay/ReplaceByLayer,
    /// `MapTiles`, `AddWarps`, `MapProperties`) completely offline in memory on top of unpacked vanilla or mod `.tmx` maps!
    /// </summary>
    public static class CpEditMapApplier
    {
        private static readonly JsonDocumentOptions JsoncOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static CpEditMapResult BuildPatchedMapInMemory(
            string cpPathOrModDir,
            string targetMapAsset,
            string gamePath,
            string? seasonToken = null)
        {
            string fullInput = Path.GetFullPath(cpPathOrModDir);
            string modRoot;
            string contentJson;

            if (Directory.Exists(fullInput))
            {
                modRoot = fullInput;
                contentJson = Path.Combine(modRoot, "content.json");
            }
            else if (File.Exists(fullInput))
            {
                contentJson = fullInput;
                modRoot = Path.GetDirectoryName(fullInput)!;
            }
            else
            {
                throw new FileNotFoundException($"Content Patcher directory or file not found: `{cpPathOrModDir}`.");
            }

            if (!File.Exists(contentJson))
                throw new FileNotFoundException($"Missing `content.json` in `{modRoot}`.");

            string modId = "Unknown.Mod";
            string manifestFile = Path.Combine(modRoot, "manifest.json");
            if (File.Exists(manifestFile))
            {
                try
                {
                    using var mDoc = JsonDocument.Parse(File.ReadAllText(manifestFile), JsoncOpts);
                    modId = GetStr(mDoc.RootElement, "UniqueID") ?? modId;
                }
                catch { }
            }

            string normTarget = NormalizeAsset(targetMapAsset);
            if (!normTarget.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase) && !normTarget.Contains('/'))
                normTarget = "Maps/" + normTarget;

            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ModId"] = modId,
                ["Season"] = string.IsNullOrWhiteSpace(seasonToken) ? "spring" : seasonToken!
            };

            // Collect default config values + static Load mappings + matching EditMap patches across Include tree
            string? loadedBaseTmx = null;
            var customTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var editPatches = new List<(string Label, JsonElement PatchClone)>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ScanCpTree(
                modRoot,
                contentJson,
                normTarget,
                tokens,
                ref loadedBaseTmx,
                customTextures,
                editPatches,
                visited);

            string baseTmxPath = loadedBaseTmx ?? TmxMap.ResolveMapFilePath(normTarget, gamePath);
            var map = TmxMap.Load(baseTmxPath);

            var res = new CpEditMapResult
            {
                Map = map,
                BaseTmxPath = baseTmxPath,
                ModRoot = modRoot
            };
            foreach (var kv in customTextures)
                res.CustomTextureAssets[kv.Key] = kv.Value;

            if (loadedBaseTmx != null)
                res.AppliedPatches.Add($"Load {normTarget} <- {Path.GetRelativePath(modRoot, loadedBaseTmx)}");

            foreach (var (label, patch) in editPatches)
            {
                ApplySingleEditMapPatch(res, patch, label, modRoot, tokens);
            }

            return res;
        }

        private static void ScanCpTree(
            string modRoot,
            string jsonPath,
            string normTarget,
            Dictionary<string, string> tokens,
            ref string? loadedBaseTmx,
            Dictionary<string, string> customTextures,
            List<(string Label, JsonElement PatchClone)> editPatches,
            HashSet<string> visited)
        {
            string full = Path.GetFullPath(jsonPath);
            if (!visited.Add(full) || !File.Exists(full))
                return;

            string rel = Path.GetRelativePath(modRoot, full).Replace('\\', '/');
            using var doc = JsonDocument.Parse(File.ReadAllText(full), JsoncOpts);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            // Populate ConfigSchema DefaultValues into tokens!
            if (TryGetProp(root, "ConfigSchema", out var cfgSchema) && cfgSchema.ValueKind == JsonValueKind.Object)
            {
                foreach (var cp in cfgSchema.EnumerateObject())
                {
                    if (cp.Value.ValueKind == JsonValueKind.Object && GetStr(cp.Value, "Default") is string defVal)
                        tokens[cp.Name] = defVal;
                }
            }

            if (!TryGetProp(root, "Changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                return;

            int idx = 0;
            foreach (var patch in changes.EnumerateArray())
            {
                string label = $"{rel}#Changes[{idx}]";
                idx++;
                if (patch.ValueKind != JsonValueKind.Object)
                    continue;

                // Honor explicit `"Enabled": false`
                if (TryGetProp(patch, "Enabled", out var enEl) &&
                    (enEl.ValueKind == JsonValueKind.False ||
                     (enEl.ValueKind == JsonValueKind.String && string.Equals(ExpandTokens(enEl.GetString() ?? "", tokens), "false", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                string? action = GetStr(patch, "Action");
                string? fromFile = ExpandTokens(GetStr(patch, "FromFile") ?? "", tokens);
                string? rawTarget = ExpandTokens(GetStr(patch, "Target") ?? "", tokens);

                if (string.Equals(action, "Include", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(fromFile))
                {
                    foreach (var f in fromFile.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.Contains("{{")))
                    {
                        ScanCpTree(modRoot, Path.Combine(modRoot, f.Replace('/', Path.DirectorySeparatorChar)), normTarget, tokens, ref loadedBaseTmx, customTextures, editPatches, visited);
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawTarget))
                    continue;

                bool matchesMap = rawTarget.Split(',')
                    .Select(s => NormalizeAsset(s))
                    .Any(t => t.Equals(normTarget, StringComparison.OrdinalIgnoreCase));

                if (string.Equals(action, "Load", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(fromFile))
                {
                    string firstFrom = fromFile.Split(',')[0].Trim();
                    if (!firstFrom.Contains("{{"))
                    {
                        string cand = Path.Combine(modRoot, firstFrom.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(cand))
                        {
                            if (matchesMap && cand.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
                            {
                                loadedBaseTmx = cand;
                            }
                            else if (cand.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var t in rawTarget.Split(',').Select(NormalizeAsset))
                                    customTextures[t] = cand;
                            }
                        }
                    }
                }
                else if (string.Equals(action, "EditMap", StringComparison.OrdinalIgnoreCase) && matchesMap)
                {
                    editPatches.Add((label, patch.Clone()));
                }
            }
        }

        private static void ApplySingleEditMapPatch(
            CpEditMapResult res,
            JsonElement patch,
            string label,
            string modRoot,
            Dictionary<string, string> tokens)
        {
            var map = res.Map;
            string? logName = GetStr(patch, "LogName");
            string desc = string.IsNullOrWhiteSpace(logName) ? label : $"{label} ({logName})";

            // 1. Sub-map splice (`FromFile` + `FromArea` + `ToArea` + `PatchMode`)
            string? fromFile = ExpandTokens(GetStr(patch, "FromFile") ?? "", tokens);
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                string firstFrom = fromFile.Split(',')[0].Trim();
                string srcPath = Path.Combine(modRoot, firstFrom.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(srcPath))
                    throw new FileNotFoundException($"EditMap `{desc}` FromFile not found: `{srcPath}`.");

                var srcMap = TmxMap.Load(srcPath);
                int fx = 0, fy = 0, fw = srcMap.Width, fh = srcMap.Height;
                if (TryGetProp(patch, "FromArea", out var fa) && TryParseRect(fa, tokens, out int rfx, out int rfy, out int rfw, out int rfh))
                {
                    fx = rfx; fy = rfy; fw = rfw; fh = rfh;
                }

                int tx = 0, ty = 0, tw = fw, th = fh;
                if (TryGetProp(patch, "ToArea", out var ta) && TryParseRect(ta, tokens, out int rtx, out int rty, out int rtw, out int rth))
                {
                    tx = rtx; ty = rty; tw = rtw; th = rth;
                }

                int copyW = Math.Min(fw, tw);
                int copyH = Math.Min(fh, th);
                string patchMode = ExpandTokens(GetStr(patch, "PatchMode") ?? "Replace", tokens);
                bool isOverlay = patchMode.Equals("Overlay", StringComparison.OrdinalIgnoreCase);
                bool isReplaceByLayer = patchMode.Equals("ReplaceByLayer", StringComparison.OrdinalIgnoreCase);

                EnsureMapCapacity(map, tx + copyW, ty + copyH);

                // Remap source tilesets into target map
                var sheetMap = new Dictionary<int, TmxTileset>();
                for (int i = 0; i < srcMap.Tilesets.Count; i++)
                {
                    var sts = srcMap.Tilesets[i];
                    string stsKey = Path.GetFileNameWithoutExtension(sts.ImageSource);
                    var existing = map.Tilesets.FirstOrDefault(t =>
                        Path.GetFileNameWithoutExtension(t.ImageSource).Equals(stsKey, StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Equals(sts.Name, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        int nextFirstGid = map.Tilesets.Count == 0
                            ? 1
                            : map.Tilesets.Max(t => t.FirstGid + Math.Max(1, t.TileCount));

                        // Resolve relative PNG next to srcPath if present so renderer finds custom sheet PNG immediately!
                        string imgSrc = sts.ImageSource;
                        string candRel = Path.Combine(Path.GetDirectoryName(srcPath)!, sts.ImageSource.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(candRel) && File.Exists(candRel + ".png"))
                            candRel += ".png";
                        if (File.Exists(candRel))
                            imgSrc = candRel;

                        existing = new TmxTileset
                        {
                            FirstGid = nextFirstGid,
                            Name = sts.Name,
                            ImageSource = imgSrc,
                            ImageWidth = sts.ImageWidth,
                            ImageHeight = sts.ImageHeight,
                            TileWidth = sts.TileWidth,
                            TileHeight = sts.TileHeight,
                            TileCount = sts.TileCount,
                            Columns = sts.Columns
                        };
                        map.Tilesets.Add(existing);
                    }

                    foreach (var kv in sts.TileIndexProperties)
                    {
                        if (!existing.TileIndexProperties.ContainsKey(kv.Key))
                            existing.TileIndexProperties[kv.Key] = new Dictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase);
                    }
                    sheetMap[i] = existing;
                }

                // PatchMode == "Replace" clears ALL existing layers in ToArea first!
                if (!isOverlay && !isReplaceByLayer)
                {
                    foreach (var tLayer in map.Layers)
                    {
                        for (int dy = 0; dy < copyH; dy++)
                            for (int dx = 0; dx < copyW; dx++)
                                tLayer.SetCleanGid(tx + dx, ty + dy, 0);
                    }
                    foreach (var og in map.ObjectTileData.Values)
                    {
                        for (int dy = 0; dy < copyH; dy++)
                            for (int dx = 0; dx < copyW; dx++)
                                og.Remove((tx + dx, ty + dy));
                    }
                }

                foreach (var sLayer in srcMap.Layers)
                {
                    var tLayer = map.GetOrCreateLayer(sLayer.Name);
                    if (isReplaceByLayer)
                    {
                        for (int dy = 0; dy < copyH; dy++)
                            for (int dx = 0; dx < copyW; dx++)
                                tLayer.SetCleanGid(tx + dx, ty + dy, 0);
                    }

                    for (int dy = 0; dy < copyH; dy++)
                    {
                        for (int dx = 0; dx < copyW; dx++)
                        {
                            int sx = fx + dx;
                            int sy = fy + dy;
                            if (sx < 0 || sy < 0 || sx >= sLayer.Width || sy >= sLayer.Height)
                                continue;

                            uint raw = sLayer.GetRawGid(sx, sy);
                            int clean = (int)(raw & 0x1FFFFFFFu);
                            if (clean <= 0)
                                continue;

                            var sts = srcMap.FindTilesetForGid(clean);
                            if (sts == null)
                                continue;
                            int sIdx = srcMap.Tilesets.IndexOf(sts);
                            int localId = clean - sts.FirstGid;
                            var targetTs = sheetMap[sIdx];
                            uint mappedRaw = (uint)(targetTs.FirstGid + localId) | (raw & 0xE0000000u);
                            tLayer.Gids[(ty + dy) * tLayer.Width + (tx + dx)] = mappedRaw;
                        }
                    }
                }

                // Merge coordinate-level ObjectTileData inside FromArea -> ToArea
                foreach (var ogKv in srcMap.ObjectTileData)
                {
                    if (!map.ObjectTileData.TryGetValue(ogKv.Key, out var dstCoordDict))
                    {
                        dstCoordDict = new Dictionary<(int X, int Y), Dictionary<string, string>>();
                        map.ObjectTileData[ogKv.Key] = dstCoordDict;
                    }

                    foreach (var cellKv in ogKv.Value)
                    {
                        int dx = cellKv.Key.X - fx;
                        int dy = cellKv.Key.Y - fy;
                        if (dx >= 0 && dx < copyW && dy >= 0 && dy < copyH)
                        {
                            dstCoordDict[(tx + dx, ty + dy)] = new Dictionary<string, string>(cellKv.Value, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }

                res.PatchedTileRects.Add(new Rectangle(tx, ty, copyW, copyH));
            }

            // 2. Per-tile `MapTiles` edits!
            if (TryGetProp(patch, "MapTiles", out var mapTiles) && mapTiles.ValueKind == JsonValueKind.Array)
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                foreach (var mt in mapTiles.EnumerateArray())
                {
                    if (mt.ValueKind != JsonValueKind.Object)
                        continue;
                    string? layerName = ExpandTokens(GetStr(mt, "Layer") ?? "", tokens);
                    if (string.IsNullOrWhiteSpace(layerName) || !TryGetProp(mt, "Position", out var posObj))
                        continue;
                    if (!TryParseIntField(posObj, "X", tokens, out int px) || !TryParseIntField(posObj, "Y", tokens, out int py))
                        continue;

                    EnsureMapCapacity(map, px + 1, py + 1);
                    var layer = map.GetOrCreateLayer(layerName);

                    bool remove = TryGetProp(mt, "Remove", out var remEl) &&
                                  (remEl.ValueKind == JsonValueKind.True ||
                                   (remEl.ValueKind == JsonValueKind.String && bool.TryParse(ExpandTokens(remEl.GetString() ?? "", tokens), out bool rb) && rb));
                    if (remove)
                    {
                        layer.SetCleanGid(px, py, 0);
                        if (map.ObjectTileData.TryGetValue(layerName, out var cd))
                            cd.Remove((px, py));
                    }
                    else
                    {
                        if (TryParseIntField(mt, "SetIndex", tokens, out int setIdx))
                        {
                            string? sheetId = ExpandTokens(GetStr(mt, "SetTilesheet") ?? "", tokens);
                            TmxTileset? ts = null;
                            if (!string.IsNullOrWhiteSpace(sheetId))
                            {
                                ts = map.Tilesets.FirstOrDefault(t =>
                                    t.Name.Equals(sheetId, StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileNameWithoutExtension(t.ImageSource).Equals(sheetId, StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                int curGid = layer.GetCleanGid(px, py);
                                ts = curGid > 0 ? map.FindTilesetForGid(curGid) : map.Tilesets.FirstOrDefault();
                            }

                            if (ts != null)
                                layer.SetCleanGid(px, py, ts.FirstGid + setIdx);
                        }

                        if (TryGetProp(mt, "SetProperties", out var setProps) && setProps.ValueKind == JsonValueKind.Object)
                        {
                            if (!map.ObjectTileData.TryGetValue(layerName, out var coordDict))
                            {
                                coordDict = new Dictionary<(int X, int Y), Dictionary<string, string>>();
                                map.ObjectTileData[layerName] = coordDict;
                            }
                            if (!coordDict.TryGetValue((px, py), out var propDict))
                            {
                                propDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                coordDict[(px, py)] = propDict;
                            }
                            foreach (var sp in setProps.EnumerateObject())
                            {
                                if (sp.Value.ValueKind == JsonValueKind.Null)
                                    propDict.Remove(sp.Name);
                                else
                                    propDict[sp.Name] = ExpandTokens(sp.Value.ToString(), tokens);
                            }
                        }
                    }

                    if (px < minX) minX = px;
                    if (py < minY) minY = py;
                    if (px > maxX) maxX = px;
                    if (py > maxY) maxY = py;
                }

                if (maxX >= minX && maxY >= minY)
                    res.PatchedTileRects.Add(new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }

            // 3. `MapProperties`
            if (TryGetProp(patch, "MapProperties", out var mapProps) && mapProps.ValueKind == JsonValueKind.Object)
            {
                foreach (var mp in mapProps.EnumerateObject())
                {
                    if (mp.Value.ValueKind == JsonValueKind.Null)
                        map.Properties.Remove(mp.Name);
                    else
                        map.Properties[mp.Name] = ExpandTokens(mp.Value.ToString(), tokens);
                }
            }

            // 4. `AddWarps`
            if (TryGetProp(patch, "AddWarps", out var addWarps) && addWarps.ValueKind == JsonValueKind.Array)
            {
                var warpStrings = new List<string>();
                if (map.Properties.TryGetValue("Warp", out var existingWarp) && !string.IsNullOrWhiteSpace(existingWarp))
                    warpStrings.Add(existingWarp.Trim());

                foreach (var w in addWarps.EnumerateArray())
                {
                    if (w.ValueKind == JsonValueKind.String)
                    {
                        string ws = ExpandTokens(w.GetString() ?? "", tokens).Trim();
                        if (!string.IsNullOrEmpty(ws))
                            warpStrings.Add(ws);
                    }
                }
                if (warpStrings.Count > 0)
                    map.Properties["Warp"] = string.Join(" ", warpStrings);
            }

            res.AppliedPatches.Add(desc);
        }

        private static void EnsureMapCapacity(TmxMap map, int reqWidth, int reqHeight)
        {
            if (reqWidth <= map.Width && reqHeight <= map.Height)
                return;

            int newW = Math.Max(map.Width, reqWidth);
            int newH = Math.Max(map.Height, reqHeight);

            foreach (var layer in map.Layers)
            {
                var resized = new uint[newW * newH];
                for (int y = 0; y < layer.Height; y++)
                {
                    for (int x = 0; x < layer.Width; x++)
                    {
                        resized[y * newW + x] = layer.Gids[y * layer.Width + x];
                    }
                }
                layer.Width = newW;
                layer.Height = newH;
                layer.Gids = resized;
            }

            map.Width = newW;
            map.Height = newH;
        }

        private static string ExpandTokens(string raw, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(raw) || !raw.Contains("{{"))
                return raw;

            return Regex.Replace(raw, @"\{\{\s*([a-zA-Z0-9_.\-]+)\s*\}\}", m =>
            {
                string key = m.Groups[1].Value;
                return tokens.TryGetValue(key, out string? val) ? val : m.Value;
            });
        }

        private static bool TryParseRect(JsonElement obj, Dictionary<string, string> tokens, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            if (obj.ValueKind != JsonValueKind.Object)
                return false;
            return TryParseIntField(obj, "X", tokens, out x) &&
                   TryParseIntField(obj, "Y", tokens, out y) &&
                   TryParseIntField(obj, "Width", tokens, out w) &&
                   TryParseIntField(obj, "Height", tokens, out h);
        }

        private static bool TryParseIntField(JsonElement obj, string name, Dictionary<string, string> tokens, out int val)
        {
            val = 0;
            if (!TryGetProp(obj, name, out var el))
                return false;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out val))
                return true;
            if (el.ValueKind == JsonValueKind.String)
                return int.TryParse(ExpandTokens(el.GetString() ?? "", tokens), out val);
            return false;
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

        private static string? GetStr(JsonElement obj, string name) =>
            TryGetProp(obj, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
