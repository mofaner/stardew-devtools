using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SdvKb.Maps
{
    public sealed class TmxAnimFrame
    {
        public int LocalTileId { get; set; }
        public int DurationMs { get; set; }
    }

    public sealed class TmxTileset
    {
        public int FirstGid { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TileWidth { get; set; } = 16;
        public int TileHeight { get; set; } = 16;
        public int TileCount { get; set; }
        public int Columns { get; set; }
        public string ImageSource { get; set; } = string.Empty;
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public Dictionary<int, Dictionary<string, string>> TileIndexProperties { get; } = new Dictionary<int, Dictionary<string, string>>();
        public Dictionary<int, List<TmxAnimFrame>> Animations { get; } = new Dictionary<int, List<TmxAnimFrame>>();

        public bool ContainsCleanGid(int cleanGid) =>
            cleanGid >= FirstGid && (TileCount <= 0 || cleanGid < FirstGid + TileCount);
    }

    public sealed class TmxLayer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; } = true;
        public float Opacity { get; set; } = 1f;
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public uint[] Gids { get; set; } = Array.Empty<uint>();
        public int RawCsvCount { get; set; }
        public string Encoding { get; set; } = "csv";

        public uint GetRawGid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return 0;
            int idx = y * Width + x;
            return idx >= 0 && idx < Gids.Length ? Gids[idx] : 0u;
        }

        public int GetCleanGid(int x, int y) => (int)(GetRawGid(x, y) & 0x1FFFFFFFu);

        public void SetCleanGid(int x, int y, int cleanGid)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;
            Gids[y * Width + x] = (uint)Math.Max(0, cleanGid);
        }
    }

    public sealed class TmxWarpEntry
    {
        public int FromX { get; set; }
        public int FromY { get; set; }
        public string TargetLocation { get; set; } = string.Empty;
        public int ToX { get; set; }
        public int ToY { get; set; }
        public string SourceKind { get; set; } = "MapWarp"; // MapWarp, Action, TouchAction
    }

    public sealed class TmxLightEntry
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int LightType { get; set; }
    }

    public sealed class TmxMap
    {
        public const uint FlipHorizontalFlag = 0x80000000u;
        public const uint FlipVerticalFlag = 0x40000000u;
        public const uint FlipDiagonalFlag = 0x20000000u;

        private static readonly HashSet<string> WarpActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EnterSewer", "LockedDoorWarp", "Mine", "Theater_Entrance", "Warp",
            "WarpCommunityCenter", "WarpGreenhouse", "WarpMensLocker", "WarpWomensLocker", "WizardHatch"
        };

        private static readonly HashSet<string> TouchWarpActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Door", "MagicWarp", "Warp"
        };

        private static readonly Dictionary<string, string> LocationNameToMapFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IslandSouth"] = "Island_S",
            ["IslandWest"] = "Island_W",
            ["IslandNorth"] = "Island_N",
            ["IslandEast"] = "Island_E",
            ["IslandSouthEast"] = "Island_SE",
            ["IslandFarmCave"] = "Island_FarmCave",
            ["CaptainRoom"] = "Island_CaptainRoom",
            ["IslandFieldOffice"] = "Island_FieldOffice",
            ["IslandHut"] = "Island_Hut",
            ["IslandShrine"] = "Island_Shrine",
            ["WizardHouseBasement"] = "WizardHouseBasement",
            ["AdventureGuild"] = "AdventureGuild"
        };

        public string FilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; } = 16;
        public int TileHeight { get; set; } = 16;
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<TmxTileset> Tilesets { get; } = new List<TmxTileset>();
        public List<TmxLayer> Layers { get; } = new List<TmxLayer>();

        /// <summary>
        /// Per-coordinate TileData properties from &lt;objectgroup name="LayerName"&gt;&lt;object name="TileData"&gt;
        /// Key: LayerName -> (tileX, tileY) -> PropertyDict
        /// </summary>
        public Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, string>>> ObjectTileData { get; } =
            new Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        public static string ResolveMapFilePath(string mapOrAsset, string gamePath)
        {
            if (File.Exists(mapOrAsset))
                return Path.GetFullPath(mapOrAsset);
            if (File.Exists(mapOrAsset + ".tmx"))
                return Path.GetFullPath(mapOrAsset + ".tmx");

            string unpackedMaps = Path.Combine(gamePath, "Content (unpacked)", "Maps");
            string cleaned = mapOrAsset.Replace('\\', '/').Trim();
            if (cleaned.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(5);
            if (cleaned.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 4);

            if (LocationNameToMapFile.TryGetValue(cleaned, out var mappedName))
                cleaned = mappedName;

            string candidate = Path.Combine(unpackedMaps, cleaned + ".tmx");
            if (File.Exists(candidate))
                return candidate;

            // Case-insensitive search in unpacked Maps
            if (Directory.Exists(unpackedMaps))
            {
                var found = Directory.GetFiles(unpackedMaps, "*.tmx")
                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(cleaned, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    return found;
            }

            throw new FileNotFoundException($"Could not locate .tmx map for `{mapOrAsset}` locally or in `{unpackedMaps}`.");
        }

        public static string? ResolveTilesheetPngPath(string imageSource, string? tmxDirectory, string gamePath, string? seasonOverride = null)
        {
            if (string.IsNullOrWhiteSpace(imageSource))
                return null;

            string norm = imageSource.Trim().Replace('/', Path.DirectorySeparatorChar);
            string withPng = norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? norm : norm + ".png";
            string fileNameOnly = Path.GetFileName(withPng);

            // Apply optional season substitution (spring_ -> summer_/fall_/winter_) if requested
            string? seasonalFileName = null;
            if (!string.IsNullOrWhiteSpace(seasonOverride))
            {
                string s = seasonOverride!.Trim().ToLowerInvariant();
                if (s == "spring" || s == "summer" || s == "fall" || s == "winter")
                {
                    if (Regex.IsMatch(fileNameOnly, @"^(spring|summer|fall|winter)_", RegexOptions.IgnoreCase))
                        seasonalFileName = Regex.Replace(fileNameOnly, @"^(spring|summer|fall|winter)_", s + "_", RegexOptions.IgnoreCase);
                }
            }

            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(tmxDirectory))
            {
                if (seasonalFileName != null)
                    candidates.Add(Path.Combine(tmxDirectory!, seasonalFileName));
                candidates.Add(Path.Combine(tmxDirectory!, withPng));
                candidates.Add(Path.Combine(tmxDirectory!, fileNameOnly));
                string? parent = Path.GetDirectoryName(tmxDirectory!);
                if (!string.IsNullOrEmpty(parent))
                {
                    candidates.Add(Path.Combine(parent!, withPng));
                    candidates.Add(Path.Combine(parent!, "assets", fileNameOnly));
                }
            }

            string unpacked = Path.Combine(gamePath, "Content (unpacked)");
            if (Directory.Exists(unpacked))
            {
                if (seasonalFileName != null)
                {
                    candidates.Add(Path.Combine(unpacked, "Maps", seasonalFileName));
                    candidates.Add(Path.Combine(unpacked, "TileSheets", seasonalFileName));
                }
                candidates.Add(Path.Combine(unpacked, withPng));
                candidates.Add(Path.Combine(unpacked, "Maps", fileNameOnly));
                candidates.Add(Path.Combine(unpacked, "TileSheets", fileNameOnly));
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }

            // Case-insensitive fallback in unpacked/Maps
            string unpackedMaps = Path.Combine(unpacked, "Maps");
            if (Directory.Exists(unpackedMaps))
            {
                string targetName = seasonalFileName ?? fileNameOnly;
                var ci = Directory.GetFiles(unpackedMaps, "*.png")
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    ?? Directory.GetFiles(unpackedMaps, "*.png")
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(fileNameOnly, StringComparison.OrdinalIgnoreCase));
                if (ci != null)
                    return ci;
            }

            return null;
        }

        public static TmxMap Load(string tmxFilePath)
        {
            string fullPath = Path.GetFullPath(tmxFilePath);
            var doc = XDocument.Load(fullPath);
            var root = doc.Root ?? throw new InvalidDataException("Empty .tmx XML document.");
            if (!root.Name.LocalName.Equals("map", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Root element is `{root.Name.LocalName}`, expected `map`.");

            var map = new TmxMap
            {
                FilePath = fullPath,
                Width = ParseIntAttr(root, "width"),
                Height = ParseIntAttr(root, "height"),
                TileWidth = ParseIntAttr(root, "tilewidth", 16),
                TileHeight = ParseIntAttr(root, "tileheight", 16)
            };

            ReadPropertiesElement(root.Element("properties"), map.Properties);

            foreach (var tsEl in root.Elements("tileset"))
            {
                var ts = new TmxTileset
                {
                    FirstGid = ParseIntAttr(tsEl, "firstgid", 1),
                    Name = tsEl.Attribute("name")?.Value ?? string.Empty,
                    TileWidth = ParseIntAttr(tsEl, "tilewidth", map.TileWidth),
                    TileHeight = ParseIntAttr(tsEl, "tileheight", map.TileHeight),
                    TileCount = ParseIntAttr(tsEl, "tilecount", 0),
                    Columns = ParseIntAttr(tsEl, "columns", 0)
                };

                var imgEl = tsEl.Element("image");
                if (imgEl != null)
                {
                    ts.ImageSource = imgEl.Attribute("source")?.Value ?? string.Empty;
                    ts.ImageWidth = ParseIntAttr(imgEl, "width", 0);
                    ts.ImageHeight = ParseIntAttr(imgEl, "height", 0);
                    if (ts.Columns <= 0 && ts.ImageWidth > 0 && ts.TileWidth > 0)
                        ts.Columns = ts.ImageWidth / ts.TileWidth;
                    if (ts.TileCount <= 0 && ts.Columns > 0 && ts.ImageHeight > 0 && ts.TileHeight > 0)
                        ts.TileCount = ts.Columns * (ts.ImageHeight / ts.TileHeight);
                }

                foreach (var tileEl in tsEl.Elements("tile"))
                {
                    int localId = ParseIntAttr(tileEl, "id", -1);
                    if (localId < 0)
                        continue;

                    var propsEl = tileEl.Element("properties");
                    if (propsEl != null)
                    {
                        var pDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        ReadPropertiesElement(propsEl, pDict);
                        if (pDict.Count > 0)
                            ts.TileIndexProperties[localId] = pDict;
                    }

                    var animEl = tileEl.Element("animation");
                    if (animEl != null)
                    {
                        var frames = new List<TmxAnimFrame>();
                        foreach (var frEl in animEl.Elements("frame"))
                        {
                            frames.Add(new TmxAnimFrame
                            {
                                LocalTileId = ParseIntAttr(frEl, "tileid", localId),
                                DurationMs = ParseIntAttr(frEl, "duration", 250)
                            });
                        }
                        if (frames.Count > 0)
                            ts.Animations[localId] = frames;
                    }
                }

                map.Tilesets.Add(ts);
            }

            foreach (var layerEl in root.Elements("layer"))
            {
                var layer = new TmxLayer
                {
                    Id = ParseIntAttr(layerEl, "id", map.Layers.Count + 1),
                    Name = layerEl.Attribute("name")?.Value ?? string.Empty,
                    Width = ParseIntAttr(layerEl, "width", map.Width),
                    Height = ParseIntAttr(layerEl, "height", map.Height),
                    Visible = ParseIntAttr(layerEl, "visible", 1) != 0,
                    Opacity = float.TryParse(layerEl.Attribute("opacity")?.Value ?? "1", NumberStyles.Float, CultureInfo.InvariantCulture, out float op) ? op : 1f
                };

                ReadPropertiesElement(layerEl.Element("properties"), layer.Properties);

                var dataEl = layerEl.Element("data");
                if (dataEl != null)
                {
                    string enc = dataEl.Attribute("encoding")?.Value?.Trim().ToLowerInvariant() ?? "xml";
                    string? comp = dataEl.Attribute("compression")?.Value?.Trim().ToLowerInvariant();
                    layer.Encoding = enc;
                    layer.Gids = ParseLayerData(dataEl, enc, comp, layer.Width * layer.Height, out int rawCount);
                    layer.RawCsvCount = rawCount;
                }

                map.Layers.Add(layer);
            }

            foreach (var ogEl in root.Elements("objectgroup"))
            {
                string ogName = ogEl.Attribute("name")?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(ogName))
                    continue;

                if (!map.ObjectTileData.TryGetValue(ogName, out var coordMap))
                {
                    coordMap = new Dictionary<(int X, int Y), Dictionary<string, string>>();
                    map.ObjectTileData[ogName] = coordMap;
                }

                foreach (var objEl in ogEl.Elements("object"))
                {
                    var propsEl = objEl.Element("properties");
                    if (propsEl == null)
                        continue;

                    double ox = ParseDoubleAttr(objEl, "x", 0);
                    double oy = ParseDoubleAttr(objEl, "y", 0);
                    double ow = ParseDoubleAttr(objEl, "width", map.TileWidth);
                    double oh = ParseDoubleAttr(objEl, "height", map.TileHeight);

                    int startX = (int)Math.Round(ox / map.TileWidth);
                    int startY = (int)Math.Round(oy / map.TileHeight);
                    int spanW = Math.Max(1, (int)Math.Round(ow / map.TileWidth));
                    int spanH = Math.Max(1, (int)Math.Round(oh / map.TileHeight));

                    var objProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    ReadPropertiesElement(propsEl, objProps);
                    if (objProps.Count == 0)
                        continue;

                    for (int dy = 0; dy < spanH; dy++)
                    {
                        for (int dx = 0; dx < spanW; dx++)
                        {
                            var pt = (startX + dx, startY + dy);
                            if (!coordMap.TryGetValue(pt, out var existing))
                            {
                                existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                coordMap[pt] = existing;
                            }
                            foreach (var kv in objProps)
                                existing[kv.Key] = kv.Value;
                        }
                    }
                }
            }

            return map;
        }

        public void Save(string outputPath)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            int nextLayerId = Layers.Count + ObjectTileData.Count + 2;
            var mapEl = new XElement("map",
                new XAttribute("version", "1.4"),
                new XAttribute("tiledversion", "1.4.2"),
                new XAttribute("orientation", "orthogonal"),
                new XAttribute("renderorder", "right-down"),
                new XAttribute("compressionlevel", "0"),
                new XAttribute("width", Width),
                new XAttribute("height", Height),
                new XAttribute("tilewidth", TileWidth),
                new XAttribute("tileheight", TileHeight),
                new XAttribute("infinite", "0"),
                new XAttribute("nextlayerid", nextLayerId),
                new XAttribute("nextobjectid", "100"));

            if (Properties.Count > 0)
                mapEl.Add(BuildPropertiesElement(Properties));

            foreach (var ts in Tilesets.OrderBy(t => t.FirstGid))
            {
                var tsEl = new XElement("tileset",
                    new XAttribute("firstgid", ts.FirstGid),
                    new XAttribute("name", string.IsNullOrEmpty(ts.Name) ? Path.GetFileNameWithoutExtension(ts.ImageSource) : ts.Name),
                    new XAttribute("tilewidth", ts.TileWidth),
                    new XAttribute("tileheight", ts.TileHeight),
                    new XAttribute("tilecount", ts.TileCount),
                    new XAttribute("columns", ts.Columns));

                var imgEl = new XElement("image", new XAttribute("source", ts.ImageSource));
                if (ts.ImageWidth > 0) imgEl.Add(new XAttribute("width", ts.ImageWidth));
                if (ts.ImageHeight > 0) imgEl.Add(new XAttribute("height", ts.ImageHeight));
                tsEl.Add(imgEl);

                var allTileIds = ts.TileIndexProperties.Keys.Union(ts.Animations.Keys).OrderBy(k => k);
                foreach (var tid in allTileIds)
                {
                    var tileEl = new XElement("tile", new XAttribute("id", tid));
                    if (ts.TileIndexProperties.TryGetValue(tid, out var tProps) && tProps.Count > 0)
                        tileEl.Add(BuildPropertiesElement(tProps));
                    if (ts.Animations.TryGetValue(tid, out var frames) && frames.Count > 0)
                    {
                        var animEl = new XElement("animation");
                        foreach (var f in frames)
                        {
                            animEl.Add(new XElement("frame",
                                new XAttribute("tileid", f.LocalTileId),
                                new XAttribute("duration", f.DurationMs)));
                        }
                        tileEl.Add(animEl);
                    }
                    tsEl.Add(tileEl);
                }

                mapEl.Add(tsEl);
            }

            int lid = 1;
            foreach (var layer in Layers)
            {
                var layerEl = new XElement("layer",
                    new XAttribute("id", lid++),
                    new XAttribute("name", layer.Name),
                    new XAttribute("width", layer.Width),
                    new XAttribute("height", layer.Height));

                if (layer.Properties.Count > 0)
                    layerEl.Add(BuildPropertiesElement(layer.Properties));

                var csvSb = new StringBuilder();
                csvSb.AppendLine();
                for (int y = 0; y < layer.Height; y++)
                {
                    for (int x = 0; x < layer.Width; x++)
                    {
                        uint gid = layer.GetRawGid(x, y);
                        csvSb.Append(gid);
                        if (x < layer.Width - 1 || y < layer.Height - 1)
                            csvSb.Append(',');
                    }
                    csvSb.AppendLine();
                }

                layerEl.Add(new XElement("data",
                    new XAttribute("encoding", "csv"),
                    csvSb.ToString()));
                mapEl.Add(layerEl);
            }

            int objId = 1;
            foreach (var kv in ObjectTileData)
            {
                if (kv.Value.Count == 0)
                    continue;
                var ogEl = new XElement("objectgroup",
                    new XAttribute("id", lid++),
                    new XAttribute("name", kv.Key));

                foreach (var cellKv in kv.Value.OrderBy(c => c.Key.Y).ThenBy(c => c.Key.X))
                {
                    if (cellKv.Value.Count == 0)
                        continue;
                    var objEl = new XElement("object",
                        new XAttribute("id", objId++),
                        new XAttribute("name", "TileData"),
                        new XAttribute("x", cellKv.Key.X * TileWidth),
                        new XAttribute("y", cellKv.Key.Y * TileHeight),
                        new XAttribute("width", TileWidth),
                        new XAttribute("height", TileHeight),
                        BuildPropertiesElement(cellKv.Value));
                    ogEl.Add(objEl);
                }
                mapEl.Add(ogEl);
            }

            var xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), mapEl);
            using var sw = new StreamWriter(outputPath, append: false, new UTF8Encoding(false));
            xdoc.Save(sw);
        }

        public TmxTileset? FindTilesetForGid(int cleanGid)
        {
            if (cleanGid <= 0)
                return null;
            TmxTileset? best = null;
            foreach (var ts in Tilesets)
            {
                if (cleanGid >= ts.FirstGid)
                {
                    if (best == null || ts.FirstGid > best.FirstGid)
                        best = ts;
                }
            }
            if (best != null && best.TileCount > 0 && cleanGid >= best.FirstGid + best.TileCount)
                return null;
            return best;
        }

        public TmxLayer GetOrCreateLayer(string layerName)
        {
            var existing = Layers.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            var created = new TmxLayer
            {
                Id = Layers.Count + 1,
                Name = layerName,
                Width = Width,
                Height = Height,
                Gids = new uint[Width * Height],
                RawCsvCount = Width * Height
            };

            // Insert in canonical Stardew Valley layer draw order!
            string[] order = { "Back", "Back2", "Buildings", "Buildings2", "Front", "Paths", "AlwaysFront" };
            int targetRank = Array.FindIndex(order, o => o.Equals(layerName, StringComparison.OrdinalIgnoreCase));
            if (targetRank < 0)
            {
                Layers.Add(created);
                return created;
            }

            int insertIdx = Layers.Count;
            for (int i = 0; i < Layers.Count; i++)
            {
                int r = Array.FindIndex(order, o => o.Equals(Layers[i].Name, StringComparison.OrdinalIgnoreCase));
                if (r > targetRank)
                {
                    insertIdx = i;
                    break;
                }
            }
            Layers.Insert(insertIdx, created);
            return created;
        }

        public Dictionary<string, string> GetEffectiveLayerCellProperties(TmxLayer layer, int x, int y)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int gid = layer.GetCleanGid(x, y);
            if (gid > 0)
            {
                var ts = FindTilesetForGid(gid);
                if (ts != null)
                {
                    int localId = gid - ts.FirstGid;
                    if (ts.TileIndexProperties.TryGetValue(localId, out var tip))
                    {
                        foreach (var kv in tip)
                            merged[kv.Key] = kv.Value;
                    }
                }
            }

            if (ObjectTileData.TryGetValue(layer.Name, out var coordMap) && coordMap.TryGetValue((x, y), out var td))
            {
                foreach (var kv in td)
                    merged[kv.Key] = kv.Value;
            }

            return merged;
        }

        public bool IsWaterTile(int x, int y)
        {
            foreach (var layer in Layers.Where(l => l.Name.StartsWith("Back", StringComparison.OrdinalIgnoreCase) || l.Name.StartsWith("Buildings", StringComparison.OrdinalIgnoreCase)))
            {
                var props = GetEffectiveLayerCellProperties(layer, x, y);
                if (props.ContainsKey("Water") || props.ContainsKey("WaterSource"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Evaluates Stardew Valley tile passability rules (matching GameLocation.isTilePassable &amp; Pathoschild's AccessibleLayer).
        /// </summary>
        public bool IsTilePassable(int x, int y, out string reason)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                reason = "out_of_bounds";
                return false;
            }

            // 1. Check Buildings* layers (Passable property overrides blocking, while water still blocks unless bridged!)
            var buildingLayers = Layers.Where(l => l.Name.StartsWith("Buildings", StringComparison.OrdinalIgnoreCase)).ToList();
            bool explicitlyPassableBridgeOnBuildings = false;
            foreach (var bLayer in buildingLayers)
            {
                int gid = bLayer.GetCleanGid(x, y);
                var props = GetEffectiveLayerCellProperties(bLayer, x, y);

                if (props.TryGetValue("Passable", out var passVal))
                {
                    if (!passVal.Equals("F", StringComparison.OrdinalIgnoreCase) && !passVal.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        // Note: Shoreline wave foam on Buildings has Passable=T so bobbers/visuals aren't walls,
                        // whereas actual bridges over water specify Type (Wood/Stone) or non-animated plank tiles.
                        bool isAnimatedTile = false;
                        if (gid > 0)
                        {
                            var ts = FindTilesetForGid(gid);
                            if (ts != null && ts.Animations.ContainsKey(gid - ts.FirstGid))
                                isAnimatedTile = true;
                        }
                        if (!isAnimatedTile && props.ContainsKey("Type"))
                            explicitlyPassableBridgeOnBuildings = true;
                        continue;
                    }
                    else
                    {
                        reason = $"blocked_by_{bLayer.Name}(Passable=F)";
                        return false;
                    }
                }

                if (gid > 0 && !props.ContainsKey("Shadow"))
                {
                    reason = $"blocked_by_{bLayer.Name}(gid={gid})";
                    return false;
                }
            }

            if (explicitlyPassableBridgeOnBuildings)
            {
                reason = "ok_bridge_override";
                return true;
            }

            // 2. Check Back* layers
            var backLayers = Layers.Where(l => l.Name.StartsWith("Back", StringComparison.OrdinalIgnoreCase)).ToList();
            bool hasAnyBackTile = false;
            bool backBridgePassable = false;

            foreach (var bkLayer in backLayers)
            {
                if (bkLayer.GetCleanGid(x, y) > 0)
                    hasAnyBackTile = true;

                var props = GetEffectiveLayerCellProperties(bkLayer, x, y);
                if (props.TryGetValue("Passable", out var bp))
                {
                    if (bp.Equals("F", StringComparison.OrdinalIgnoreCase) || bp.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = $"back_impassable({bkLayer.Name})";
                        return false;
                    }
                    // Note: In xTile / AccessibleLayer, Back 'Passable' without 'T' can mean impassable unless Passable='T' on Back2 bridge
                    if (bkLayer.Name.Equals("Back", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(bp))
                    {
                        reason = "back_impassable_flag";
                        return false;
                    }
                    if (bp.Equals("T", StringComparison.OrdinalIgnoreCase) && !bkLayer.Name.Equals("Back", StringComparison.OrdinalIgnoreCase))
                        backBridgePassable = true;
                }
                if (props.ContainsKey("Impassable"))
                {
                    reason = $"back_impassable({bkLayer.Name})";
                    return false;
                }
            }

            if (!hasAnyBackTile)
            {
                reason = "void_no_back_tile";
                return false;
            }

            if (!backBridgePassable && IsWaterTile(x, y))
            {
                reason = "water";
                return false;
            }

            reason = "ok";
            return true;
        }

        public List<TmxWarpEntry> ParseWarps()
        {
            var warps = new List<TmxWarpEntry>();
            if (Properties.TryGetValue("Warp", out var rawWarp) && !string.IsNullOrWhiteSpace(rawWarp))
            {
                var parts = rawWarp.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 4 < parts.Length; i += 5)
                {
                    if (int.TryParse(parts[i], out int fx) &&
                        int.TryParse(parts[i + 1], out int fy) &&
                        int.TryParse(parts[i + 3], out int tx) &&
                        int.TryParse(parts[i + 4], out int ty))
                    {
                        warps.Add(new TmxWarpEntry
                        {
                            FromX = fx,
                            FromY = fy,
                            TargetLocation = parts[i + 2],
                            ToX = tx,
                            ToY = ty,
                            SourceKind = "MapWarp"
                        });
                    }
                }
            }

            // Scan Action & TouchAction warp properties on Buildings / Back layers
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    foreach (var layer in Layers)
                    {
                        var props = GetEffectiveLayerCellProperties(layer, x, y);
                        if (props.TryGetValue("Action", out var act) && !string.IsNullOrWhiteSpace(act))
                        {
                            var tok = act.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tok.Length > 0 && WarpActions.Contains(tok[0]))
                            {
                                string target = tok.Length >= 4 && tok[0].IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0 ? tok[3] : tok[0];
                                int tx = tok.Length >= 3 && int.TryParse(tok[1], out int ax) ? ax : -1;
                                int ty = tok.Length >= 3 && int.TryParse(tok[2], out int ay) ? ay : -1;
                                warps.Add(new TmxWarpEntry { FromX = x, FromY = y, TargetLocation = target, ToX = tx, ToY = ty, SourceKind = $"Action:{tok[0]}" });
                            }
                        }
                        if (props.TryGetValue("TouchAction", out var tact) && !string.IsNullOrWhiteSpace(tact))
                        {
                            var tok = tact.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tok.Length > 0 && TouchWarpActions.Contains(tok[0]))
                            {
                                string target = tok.Length >= 2 ? tok[1] : tok[0];
                                int tx = tok.Length >= 4 && int.TryParse(tok[2], out int ax) ? ax : -1;
                                int ty = tok.Length >= 4 && int.TryParse(tok[3], out int ay) ? ay : -1;
                                warps.Add(new TmxWarpEntry { FromX = x, FromY = y, TargetLocation = target, ToX = tx, ToY = ty, SourceKind = $"TouchAction:{tok[0]}" });
                            }
                        }
                    }
                }
            }

            return warps;
        }

        public List<TmxLightEntry> ParseLights()
        {
            var lights = new List<TmxLightEntry>();
            if (Properties.TryGetValue("Light", out var rawLight) && !string.IsNullOrWhiteSpace(rawLight))
            {
                var parts = rawLight.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 2 < parts.Length; i += 3)
                {
                    if (int.TryParse(parts[i], out int lx) &&
                        int.TryParse(parts[i + 1], out int ly) &&
                        int.TryParse(parts[i + 2], out int lt))
                    {
                        lights.Add(new TmxLightEntry { TileX = lx, TileY = ly, LightType = lt });
                    }
                }
            }
            return lights;
        }

        private static uint[] ParseLayerData(XElement dataEl, string encoding, string? compression, int expectedCount, out int actualParsedCount)
        {
            if (encoding == "csv")
            {
                string raw = dataEl.Value ?? string.Empty;
                var tokens = raw.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                actualParsedCount = tokens.Length;
                var arr = new uint[Math.Max(expectedCount, tokens.Length)];
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (uint.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v))
                        arr[i] = v;
                }
                return arr;
            }
            else if (encoding == "base64")
            {
                byte[] rawBytes = Convert.FromBase64String((dataEl.Value ?? string.Empty).Trim());
                Stream stream = new MemoryStream(rawBytes);
                if (compression == "gzip")
                {
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                }
                else if (compression == "zlib")
                {
                    // Skip 2-byte zlib header before Deflate stream
                    var inner = new MemoryStream(rawBytes, 2, rawBytes.Length - 2);
                    stream = new DeflateStream(inner, CompressionMode.Decompress);
                }

                using var outMs = new MemoryStream();
                using (stream)
                {
                    stream.CopyTo(outMs);
                }
                byte[] decoded = outMs.ToArray();
                actualParsedCount = decoded.Length / 4;
                var arr = new uint[Math.Max(expectedCount, actualParsedCount)];
                for (int i = 0; i < actualParsedCount; i++)
                {
                    arr[i] = BitConverter.ToUInt32(decoded, i * 4);
                }
                return arr;
            }
            else
            {
                // XML <tile gid="..."/>
                var tileEls = dataEl.Elements("tile").ToList();
                actualParsedCount = tileEls.Count;
                var arr = new uint[Math.Max(expectedCount, actualParsedCount)];
                for (int i = 0; i < tileEls.Count; i++)
                {
                    if (uint.TryParse(tileEls[i].Attribute("gid")?.Value ?? "0", out uint v))
                        arr[i] = v;
                }
                return arr;
            }
        }

        private static void ReadPropertiesElement(XElement? propsEl, Dictionary<string, string> target)
        {
            if (propsEl == null)
                return;
            foreach (var p in propsEl.Elements("property"))
            {
                string? name = p.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name))
                    continue;
                string val = p.Attribute("value")?.Value ?? p.Value ?? string.Empty;
                target[name!] = val;
            }
        }

        private static XElement BuildPropertiesElement(Dictionary<string, string> props)
        {
            var el = new XElement("properties");
            foreach (var kv in props.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                el.Add(new XElement("property",
                    new XAttribute("name", kv.Key),
                    new XAttribute("type", "string"),
                    new XAttribute("value", kv.Value)));
            }
            return el;
        }

        private static int ParseIntAttr(XElement el, string attrName, int defaultValue = 0) =>
            int.TryParse(el.Attribute(attrName)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : defaultValue;

        private static double ParseDoubleAttr(XElement el, string attrName, double defaultValue = 0) =>
            double.TryParse(el.Attribute(attrName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : defaultValue;
    }
}
