using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace SdvKb.Maps
{
    public sealed class PrefabTilesetDef
    {
        public string Name { get; set; } = string.Empty;
        public string ImageSource { get; set; } = string.Empty;
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int TileWidth { get; set; } = 16;
        public int TileHeight { get; set; } = 16;
        public int TileCount { get; set; }
        public int Columns { get; set; }
        public Dictionary<int, Dictionary<string, string>> TileProperties { get; set; } = new Dictionary<int, Dictionary<string, string>>();
        public Dictionary<int, List<TmxAnimFrame>> Animations { get; set; } = new Dictionary<int, List<TmxAnimFrame>>();
    }

    public sealed class PrefabCellEntry
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public int SheetIndex { get; set; }
        public int LocalTileId { get; set; }
        public uint FlipFlags { get; set; }
    }

    public sealed class PrefabObjectPropEntry
    {
        public string Layer { get; set; } = string.Empty;
        public int Dx { get; set; }
        public int Dy { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    public sealed class MapPrefabData
    {
        public string Name { get; set; } = string.Empty;
        public string SourceMap { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IncludesBackLayer { get; set; }
        public List<PrefabTilesetDef> Tilesets { get; set; } = new List<PrefabTilesetDef>();
        public Dictionary<string, List<PrefabCellEntry>> Layers { get; set; } = new Dictionary<string, List<PrefabCellEntry>>();
        public List<PrefabObjectPropEntry> ObjectTileData { get; set; } = new List<PrefabObjectPropEntry>();
    }

    public sealed class AutotileSpec
    {
        public string Theme { get; set; } = "island"; // island or beach
        public string? Season { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public List<string> Rows { get; set; } = new List<string>();
    }

    public static class MapPrefabAndAutotile
    {
        private static readonly (string Id, string MapName, int X, int Y, int W, int H, bool IncludeBack, string Desc)[] BuiltinPrefabs =
        {
            ("@island-palm", "Island_S", 0, 0, 3, 4, false, "Ginger Island clean standalone coconut palm (3x4, transparent bg + Buildings trunk root, safe on sand or grass)"),
            ("@island-tall-palm", "Island_S", 0, 0, 4, 5, false, "Ginger Island tall coconut palm (4x5, transparent bg + Buildings trunk root, safe on sand or grass)"),
            ("@island-pier-segment", "Island_S", 19, 35, 5, 4, true, "Ginger Island 3-wide wooden pier with side rails (5x4, includes Back planks)"),
            ("@island-cliff-stairs", "Island_S", 17, 1, 3, 4, true, "Ginger Island walkable stone/sand stairs (3x4, includes Back)"),
            ("@beach-wave-strip", "Island_S", 14, 33, 6, 3, true, "Ginger Island 6-wide shoreline wet sand + 13-frame animated wave strip (6x3)")
        };

        public static object ListBuiltinPrefabCatalog()
        {
            return BuiltinPrefabs.Select(p => new
            {
                id = p.Id,
                sourceMap = p.MapName,
                rect = $"{p.X},{p.Y},{p.W},{p.H}",
                includesBack = p.IncludeBack,
                description = p.Desc
            }).ToList();
        }

        public static MapPrefabData ExtractPrefabFromMap(
            string mapOrAsset,
            string gamePath,
            int rx,
            int ry,
            int rw,
            int rh,
            bool includeBack,
            string? prefabName = null)
        {
            string tmxPath = TmxMap.ResolveMapFilePath(mapOrAsset, gamePath);
            var map = TmxMap.Load(tmxPath);

            if (rx < 0 || ry < 0 || rw <= 0 || rh <= 0 || rx + rw > map.Width || ry + rh > map.Height)
                throw new ArgumentOutOfRangeException($"Rect ({rx},{ry},{rw},{rh}) exceeds map bounds ({map.Width}x{map.Height}).");

            var prefab = new MapPrefabData
            {
                Name = prefabName ?? $"{Path.GetFileNameWithoutExtension(tmxPath)}_{rx}_{ry}_{rw}x{rh}",
                SourceMap = Path.GetFileName(tmxPath),
                Width = rw,
                Height = rh,
                IncludesBackLayer = includeBack
            };

            var usedSheets = new Dictionary<int, int>(); // mapTilesetIdx -> prefabSheetIdx

            foreach (var layer in map.Layers)
            {
                if (layer.Name.Equals("Paths", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!includeBack && layer.Name.Equals("Back", StringComparison.OrdinalIgnoreCase))
                    continue;

                var entries = new List<PrefabCellEntry>();
                for (int dy = 0; dy < rh; dy++)
                {
                    for (int dx = 0; dx < rw; dx++)
                    {
                        uint raw = layer.GetRawGid(rx + dx, ry + dy);
                        int clean = (int)(raw & 0x1FFFFFFFu);
                        if (clean <= 0)
                            continue;

                        var ts = map.FindTilesetForGid(clean);
                        if (ts == null)
                            continue;
                        int mapTsIdx = map.Tilesets.IndexOf(ts);
                        int localId = clean - ts.FirstGid;

                        if (!usedSheets.TryGetValue(mapTsIdx, out int pSheetIdx))
                        {
                            pSheetIdx = prefab.Tilesets.Count;
                            usedSheets[mapTsIdx] = pSheetIdx;
                            prefab.Tilesets.Add(new PrefabTilesetDef
                            {
                                Name = ts.Name,
                                ImageSource = ts.ImageSource,
                                ImageWidth = ts.ImageWidth,
                                ImageHeight = ts.ImageHeight,
                                TileWidth = ts.TileWidth,
                                TileHeight = ts.TileHeight,
                                TileCount = ts.TileCount,
                                Columns = ts.Columns
                            });
                        }

                        var pTs = prefab.Tilesets[pSheetIdx];
                        if (ts.TileIndexProperties.TryGetValue(localId, out var tProps) && !pTs.TileIndexProperties_Contains(localId))
                        {
                            pTs.TileProperties[localId] = new Dictionary<string, string>(tProps);
                        }
                        if (ts.Animations.TryGetValue(localId, out var anim) && !pTs.Animations.ContainsKey(localId))
                        {
                            pTs.Animations[localId] = anim.ToList();
                            foreach (var fr in anim)
                            {
                                if (ts.TileIndexProperties.TryGetValue(fr.LocalTileId, out var frProps))
                                    pTs.TileProperties[fr.LocalTileId] = new Dictionary<string, string>(frProps);
                            }
                        }

                        entries.Add(new PrefabCellEntry
                        {
                            Dx = dx,
                            Dy = dy,
                            SheetIndex = pSheetIdx,
                            LocalTileId = localId,
                            FlipFlags = raw & 0xE0000000u
                        });
                    }
                }

                if (entries.Count > 0)
                    prefab.Layers[layer.Name] = entries;
            }

            // Copy coordinate-level ObjectTileData inside rect
            foreach (var ogKv in map.ObjectTileData)
            {
                if (!includeBack && ogKv.Key.Equals("Back", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var cellKv in ogKv.Value)
                {
                    int dx = cellKv.Key.X - rx;
                    int dy = cellKv.Key.Y - ry;
                    if (dx >= 0 && dx < rw && dy >= 0 && dy < rh && cellKv.Value.Count > 0)
                    {
                        prefab.ObjectTileData.Add(new PrefabObjectPropEntry
                        {
                            Layer = ogKv.Key,
                            Dx = dx,
                            Dy = dy,
                            Properties = new Dictionary<string, string>(cellKv.Value)
                        });
                    }
                }
            }

            return prefab;
        }

        private static bool TileIndexProperties_Contains(this PrefabTilesetDef def, int id) =>
            def.TileProperties.ContainsKey(id);

        public static MapPrefabData LoadOrResolvePrefab(string prefabIdOrFile, string gamePath)
        {
            if (prefabIdOrFile.StartsWith("@", StringComparison.Ordinal))
            {
                if (prefabIdOrFile.Equals("@island-palm", StringComparison.OrdinalIgnoreCase))
                    return BuildCleanIslandPalmPrefab(gamePath, tall: false);
                if (prefabIdOrFile.Equals("@island-tall-palm", StringComparison.OrdinalIgnoreCase))
                    return BuildCleanIslandPalmPrefab(gamePath, tall: true);

                var preset = BuiltinPrefabs.FirstOrDefault(p => p.Id.Equals(prefabIdOrFile, StringComparison.OrdinalIgnoreCase));
                if (preset.Id == null)
                    throw new ArgumentException($"Unknown built-in prefab `{prefabIdOrFile}`. Run `sdv-dev map-prefab --list` to see built-ins.");

                return ExtractPrefabFromMap(preset.MapName, gamePath, preset.X, preset.Y, preset.W, preset.H, preset.IncludeBack, preset.Id);
            }

            if (!File.Exists(prefabIdOrFile))
                throw new FileNotFoundException($"Prefab JSON file not found: `{prefabIdOrFile}`.");

            string json = File.ReadAllText(prefabIdOrFile);
            return JsonSerializer.Deserialize<MapPrefabData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException($"Invalid prefab JSON: `{prefabIdOrFile}`.");
        }

        private static MapPrefabData BuildCleanIslandPalmPrefab(string gamePath, bool tall)
        {
            var tsDef = new PrefabTilesetDef
            {
                Name = "island_tilesheet_1",
                ImageSource = "island_tilesheet_1",
                ImageWidth = 512,
                ImageHeight = 1040,
                TileWidth = 16,
                TileHeight = 16,
                TileCount = 2080,
                Columns = 32
            };

            if (!tall)
            {
                // Clean 3x3 standalone coconut palm from island_tilesheet_1:
                // Crown (364..366 / 396..398 / 430) on AlwaysFront + rounded trunk root (429) directly on Buildings at (1,2)!
                var prefab = new MapPrefabData
                {
                    Name = "@island-palm",
                    SourceMap = "island_tilesheet_1",
                    Width = 3,
                    Height = 3,
                    IncludesBackLayer = false
                };
                prefab.Tilesets.Add(tsDef);
                prefab.Layers["AlwaysFront"] = new List<PrefabCellEntry>
                {
                    new PrefabCellEntry { Dx = 0, Dy = 0, SheetIndex = 0, LocalTileId = 364 },
                    new PrefabCellEntry { Dx = 1, Dy = 0, SheetIndex = 0, LocalTileId = 365 },
                    new PrefabCellEntry { Dx = 2, Dy = 0, SheetIndex = 0, LocalTileId = 366 },
                    new PrefabCellEntry { Dx = 0, Dy = 1, SheetIndex = 0, LocalTileId = 396 },
                    new PrefabCellEntry { Dx = 1, Dy = 1, SheetIndex = 0, LocalTileId = 397 },
                    new PrefabCellEntry { Dx = 2, Dy = 1, SheetIndex = 0, LocalTileId = 398 },
                    new PrefabCellEntry { Dx = 2, Dy = 2, SheetIndex = 0, LocalTileId = 430 }
                };
                prefab.Layers["Buildings"] = new List<PrefabCellEntry>
                {
                    new PrefabCellEntry { Dx = 1, Dy = 2, SheetIndex = 0, LocalTileId = 429 }
                };
                return prefab;
            }
            else
            {
                // Clean 3x4 lush double-frond palm from island_tilesheet_1 (194..196 / 226..228 / 258..260 + clean trunk base 291)
                var prefab = new MapPrefabData
                {
                    Name = "@island-tall-palm",
                    SourceMap = "island_tilesheet_1",
                    Width = 3,
                    Height = 4,
                    IncludesBackLayer = false
                };
                prefab.Tilesets.Add(tsDef);
                prefab.Layers["AlwaysFront"] = new List<PrefabCellEntry>
                {
                    new PrefabCellEntry { Dx = 0, Dy = 0, SheetIndex = 0, LocalTileId = 194 },
                    new PrefabCellEntry { Dx = 1, Dy = 0, SheetIndex = 0, LocalTileId = 195 },
                    new PrefabCellEntry { Dx = 2, Dy = 0, SheetIndex = 0, LocalTileId = 196 },
                    new PrefabCellEntry { Dx = 0, Dy = 1, SheetIndex = 0, LocalTileId = 226 },
                    new PrefabCellEntry { Dx = 1, Dy = 1, SheetIndex = 0, LocalTileId = 227 },
                    new PrefabCellEntry { Dx = 2, Dy = 1, SheetIndex = 0, LocalTileId = 228 },
                    new PrefabCellEntry { Dx = 0, Dy = 2, SheetIndex = 0, LocalTileId = 258 },
                    new PrefabCellEntry { Dx = 2, Dy = 2, SheetIndex = 0, LocalTileId = 260 }
                };
                prefab.Layers["Front"] = new List<PrefabCellEntry>
                {
                    new PrefabCellEntry { Dx = 1, Dy = 2, SheetIndex = 0, LocalTileId = 259 }
                };
                prefab.Layers["Buildings"] = new List<PrefabCellEntry>
                {
                    new PrefabCellEntry { Dx = 1, Dy = 3, SheetIndex = 0, LocalTileId = 291 }
                };
                return prefab;
            }
        }

        public static object StampPrefabOntoMap(
            string targetTmxPath,
            string prefabIdOrFile,
            string gamePath,
            int atX,
            int atY,
            bool? overrideIncludeBack = null,
            bool clearTargetRectFirst = false)
        {
            string fullTmx = Path.GetFullPath(targetTmxPath);
            var map = TmxMap.Load(fullTmx);
            var prefab = LoadOrResolvePrefab(prefabIdOrFile, gamePath);

            bool stampBack = overrideIncludeBack ?? prefab.IncludesBackLayer;

            // Map each prefab tileset to an existing or newly appended TmxTileset in target map!
            var sheetMap = new Dictionary<int, TmxTileset>();
            for (int i = 0; i < prefab.Tilesets.Count; i++)
            {
                var pts = prefab.Tilesets[i];
                string ptsImgName = Path.GetFileNameWithoutExtension(pts.ImageSource);

                var existing = map.Tilesets.FirstOrDefault(t =>
                    Path.GetFileNameWithoutExtension(t.ImageSource).Equals(ptsImgName, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    int nextFirstGid = 1;
                    if (map.Tilesets.Count > 0)
                    {
                        var last = map.Tilesets.OrderByDescending(t => t.FirstGid).First();
                        nextFirstGid = last.FirstGid + Math.Max(1, last.TileCount);
                    }

                    existing = new TmxTileset
                    {
                        FirstGid = nextFirstGid,
                        Name = string.IsNullOrEmpty(pts.Name) ? ptsImgName : pts.Name,
                        ImageSource = pts.ImageSource,
                        ImageWidth = pts.ImageWidth,
                        ImageHeight = pts.ImageHeight,
                        TileWidth = pts.TileWidth,
                        TileHeight = pts.TileHeight,
                        TileCount = pts.TileCount,
                        Columns = pts.Columns
                    };
                    map.Tilesets.Add(existing);
                }

                // Merge tile properties & animations used by prefab
                foreach (var kv in pts.TileProperties)
                {
                    if (!existing.TileIndexProperties.ContainsKey(kv.Key))
                        existing.TileIndexProperties[kv.Key] = new Dictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase);
                }
                foreach (var kv in pts.Animations)
                {
                    if (!existing.Animations.ContainsKey(kv.Key))
                        existing.Animations[kv.Key] = kv.Value.ToList();
                }

                sheetMap[i] = existing;
            }

            if (clearTargetRectFirst)
            {
                foreach (var layer in map.Layers)
                {
                    if (!stampBack && layer.Name.Equals("Back", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (layer.Name.Equals("Paths", StringComparison.OrdinalIgnoreCase))
                        continue;
                    for (int dy = 0; dy < prefab.Height; dy++)
                        for (int dx = 0; dx < prefab.Width; dx++)
                            layer.SetCleanGid(atX + dx, atY + dy, 0);
                }
            }

            int stampedCells = 0;
            foreach (var lkv in prefab.Layers)
            {
                if (!stampBack && lkv.Key.Equals("Back", StringComparison.OrdinalIgnoreCase))
                    continue;

                var targetLayer = map.GetOrCreateLayer(lkv.Key);
                foreach (var cell in lkv.Value)
                {
                    int tx = atX + cell.Dx;
                    int ty = atY + cell.Dy;
                    if (tx < 0 || ty < 0 || tx >= map.Width || ty >= map.Height)
                        continue;

                    var targetTs = sheetMap[cell.SheetIndex];
                    uint newRawGid = (uint)(targetTs.FirstGid + cell.LocalTileId) | cell.FlipFlags;
                    targetLayer.Gids[ty * targetLayer.Width + tx] = newRawGid;
                    stampedCells++;
                }
            }

            foreach (var obj in prefab.ObjectTileData)
            {
                if (!stampBack && obj.Layer.Equals("Back", StringComparison.OrdinalIgnoreCase))
                    continue;
                int tx = atX + obj.Dx;
                int ty = atY + obj.Dy;
                if (tx < 0 || ty < 0 || tx >= map.Width || ty >= map.Height)
                    continue;

                if (!map.ObjectTileData.TryGetValue(obj.Layer, out var coordDict))
                {
                    coordDict = new Dictionary<(int X, int Y), Dictionary<string, string>>();
                    map.ObjectTileData[obj.Layer] = coordDict;
                }
                coordDict[(tx, ty)] = new Dictionary<string, string>(obj.Properties, StringComparer.OrdinalIgnoreCase);
            }

            map.Save(fullTmx);
            return new
            {
                ok = true,
                targetTmx = fullTmx,
                prefab = prefab.Name,
                stampedAt = new { x = atX, y = atY, width = prefab.Width, height = prefab.Height },
                stampedBackLayer = stampBack,
                stampedTileCells = stampedCells
            };
        }

        /// <summary>
        /// Generates a complete multi-layer Stardew Valley .tmx map from a compact ASCII or JSON terrain grid!
        /// Legend:
        ///   W = Ocean / Water (with automatic wet-sand shoreline &amp; 13-frame animated waves!)
        ///   S = Beach Sand
        ///   G = Grass (with automatic 8-direction grass-to-sand edges &amp; corners!)
        ///   P = Wooden Pier / Plank Bridge (passable over water/cliffs, no Water=T bleed!)
        ///   C = Impassable Cliff / Rock Wall
        ///   D = Dirt Path
        /// </summary>
        public static object GenerateAutotiledTmx(
            string specFilePath,
            string outTmxPath,
            string gamePath,
            string? themeOverride = null)
        {
            if (!File.Exists(specFilePath))
                throw new FileNotFoundException($"Autotile spec file not found: `{specFilePath}`.");

            string raw = File.ReadAllText(specFilePath);
            AutotileSpec spec;
            if (raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                spec = JsonSerializer.Deserialize<AutotileSpec>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidDataException("Invalid JSON autotile spec.");
            }
            else
            {
                var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal) && !l.StartsWith("//", StringComparison.Ordinal))
                    .ToList();
                spec = new AutotileSpec { Rows = lines };
            }

            if (!string.IsNullOrWhiteSpace(themeOverride))
                spec.Theme = themeOverride!;

            if (spec.Rows == null || spec.Rows.Count == 0)
                throw new InvalidDataException("Autotile spec contains 0 rows.");

            int height = spec.Rows.Count;
            int width = spec.Rows.Max(r => r.Length);
            char[,] grid = new char[width, height];
            for (int y = 0; y < height; y++)
            {
                string r = spec.Rows[y];
                for (int x = 0; x < width; x++)
                {
                    char ch = x < r.Length ? char.ToUpperInvariant(r[x]) : 'W';
                    grid[x, y] = ch;
                }
            }

            bool isBeachTheme = spec.Theme.Equals("beach", StringComparison.OrdinalIgnoreCase);
            string templateVanillaMap = isBeachTheme ? "Beach" : "Island_S";
            var vanillaRef = TmxMap.Load(TmxMap.ResolveMapFilePath(templateVanillaMap, gamePath));

            // Clone primary terrain tileset directly from unpacked 1.6 vanilla map so all Water=T, Diggable=T, and 13-frame wave animations are 100% authentic!
            var srcTs = isBeachTheme
                ? vanillaRef.Tilesets.First(t => t.ImageSource.IndexOf("beach", StringComparison.OrdinalIgnoreCase) >= 0)
                : vanillaRef.Tilesets.First(t => t.ImageSource.IndexOf("island_tilesheet_1", StringComparison.OrdinalIgnoreCase) >= 0);

            var mainTs = new TmxTileset
            {
                FirstGid = 1,
                Name = srcTs.Name,
                ImageSource = srcTs.ImageSource,
                ImageWidth = srcTs.ImageWidth,
                ImageHeight = srcTs.ImageHeight,
                TileWidth = 16,
                TileHeight = 16,
                TileCount = srcTs.TileCount,
                Columns = srcTs.Columns
            };
            foreach (var kv in srcTs.TileIndexProperties)
                mainTs.TileIndexProperties[kv.Key] = new Dictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in srcTs.Animations)
                mainTs.Animations[kv.Key] = kv.Value.ToList();

            var map = new TmxMap
            {
                Width = width,
                Height = height,
                TileWidth = 16,
                TileHeight = 16
            };
            map.Properties["Outdoors"] = "T";
            if (!isBeachTheme)
                map.Properties["LocationContext"] = "Island";
            if (!string.IsNullOrWhiteSpace(spec.Season))
                map.Properties["SeasonOverride"] = spec.Season!;
            if (spec.Properties != null)
            {
                foreach (var kv in spec.Properties)
                    map.Properties[kv.Key] = kv.Value;
            }

            map.Tilesets.Add(mainTs);

            var back = map.GetOrCreateLayer("Back");
            var back2 = map.GetOrCreateLayer("Back2");
            var buildings = map.GetOrCreateLayer("Buildings");
            var front = map.GetOrCreateLayer("Front");
            var alwaysFront = map.GetOrCreateLayer("AlwaysFront");

            char At(int x, int y) =>
                x < 0 || y < 0 || x >= width || y >= height ? grid[Math.Max(0, Math.Min(width - 1, x)), Math.Max(0, Math.Min(height - 1, y))] : grid[x, y];

            bool IsWaterLike(char c) => c == 'W';
            bool IsLand(char c) => c == 'S' || c == 'G' || c == 'D' || c == 'C';

            var rng = new Random(1337); // Deterministic subtle sand variation

            if (!isBeachTheme)
            {
                // Verified 1.6 Island_S (`island_tilesheet_1`, firstgid=1) Autotile Rules!
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        char c = grid[x, y];
                        int fg = mainTs.FirstGid;

                        if (c == 'P')
                        {
                            // Wooden Pier / Plank Bridge: NO Water=T on Back so water shimmer never bleeds onto bridge!
                            bool leftPier = At(x - 1, y) == 'P';
                            bool rightPier = At(x + 1, y) == 'P';
                            int plankLocal = !leftPier && rightPier ? 549 : (leftPier && !rightPier ? 551 : 550);
                            if (y % 3 == 0)
                                plankLocal = !leftPier && rightPier ? 581 : (leftPier && !rightPier ? 583 : 582);
                            back.SetCleanGid(x, y, fg + plankLocal);
                            continue;
                        }

                        if (c == 'W')
                        {
                            bool landNorth1 = IsLand(At(x, y - 1));
                            bool landNorth2 = !landNorth1 && IsLand(At(x, y - 2));
                            bool landAdj = landNorth1 || IsLand(At(x, y + 1)) || IsLand(At(x - 1, y)) || IsLand(At(x + 1, y));

                            if (landNorth1)
                            {
                                // Shallow shore water + 13-frame animated wave foam on Buildings!
                                back.SetCleanGid(x, y, fg + 197);
                                int waveAnim = (x % 2 == 0) ? 261 : 293;
                                buildings.SetCleanGid(x, y, fg + waveAnim);
                                // Mark animated foam tile Passable=F via Water on Back; ensure no accidental walk-on-water
                            }
                            else if (landNorth2)
                            {
                                back.SetCleanGid(x, y, fg + ((x % 3 == 0) ? 229 : 165));
                            }
                            else if (landAdj)
                            {
                                back.SetCleanGid(x, y, fg + 199);
                            }
                            else
                            {
                                back.SetCleanGid(x, y, fg + 135);
                            }
                            continue;
                        }

                        if (c == 'G')
                        {
                            // Solid lush green Ginger Island grass (305 / 306 on island_tilesheet_1; avoids 337/338 fern overlays)
                            int grassTile = ((x + y) % 2 == 0) ? 305 : 306;

                            if (!mainTs.TileIndexProperties.ContainsKey(grassTile))
                                mainTs.TileIndexProperties[grassTile] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Type"] = "Grass" };

                            back.SetCleanGid(x, y, fg + grassTile);
                            continue;
                        }

                        if (c == 'C')
                        {
                            // Highland / Cliff Rock Wall (impassable via Buildings layer!)
                            back.SetCleanGid(x, y, fg + 72);
                            bool cliffSouth = At(x, y + 1) != 'C';
                            buildings.SetCleanGid(x, y, fg + (cliffSouth ? 540 : 508));
                            continue;
                        }

                        // Sand ('S') or Dirt ('D')
                        bool waterSouth = IsWaterLike(At(x, y + 1));
                        bool grassNorth = At(x, y - 1) == 'G';
                        bool grassWest = At(x - 1, y) == 'G';
                        bool grassEast = At(x + 1, y) == 'G';
                        int sandTile = 72;
                        if (waterSouth)
                        {
                            sandTile = (x % 5 == 0) ? 103 : 39; // Wet sand along shoreline
                        }
                        else if (c == 'D')
                        {
                            sandTile = 207;
                        }
                        else if (grassNorth && grassWest)
                        {
                            sandTile = 609;
                        }
                        else if (grassNorth && grassEast)
                        {
                            sandTile = 611;
                        }
                        else if (grassNorth)
                        {
                            sandTile = 610; // Grass-over-sand north fringe
                        }
                        else if (grassWest && !grassEast)
                        {
                            sandTile = 641;
                        }
                        else if (grassEast && !grassWest)
                        {
                            sandTile = 643;
                        }
                        else
                        {
                            int roll = rng.Next(100);
                            if (roll < 4) sandTile = 73;
                            else if (roll == 4) sandTile = 40;
                            else if (roll == 5) sandTile = 41;
                        }

                        back.SetCleanGid(x, y, fg + sandTile);
                    }
                }
            }
            else
            {
                // Verified 1.6 Beach (`spring_beach`, firstgid=1) Autotile Rules!
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        char c = grid[x, y];
                        int fg = mainTs.FirstGid;

                        if (c == 'P')
                        {
                            back.SetCleanGid(x, y, fg + 488);
                        }
                        else if (c == 'W')
                        {
                            bool landNorth = IsLand(At(x, y - 1));
                            back.SetCleanGid(x, y, fg + (landNorth ? 107 : 75));
                        }
                        else if (c == 'C')
                        {
                            back.SetCleanGid(x, y, fg + 42);
                            buildings.SetCleanGid(x, y, fg + 56);
                        }
                        else
                        {
                            bool waterSouth = IsWaterLike(At(x, y + 1));
                            back.SetCleanGid(x, y, fg + (waterSouth ? 24 : 42));
                        }
                    }
                }
            }

            string fullOut = Path.GetFullPath(outTmxPath);
            map.Save(fullOut);

            return new
            {
                ok = true,
                outputTmx = fullOut,
                theme = isBeachTheme ? "beach" : "island",
                width,
                height,
                tilesheet = mainTs.ImageSource
            };
        }
    }
}
