using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SdvKb.Maps;

namespace SdvKb.Validation
{
    public sealed class DisconnectedIslandInfo
    {
        public int TileCount { get; set; }
        public string SampleTile { get; set; } = string.Empty;
        public string BoundingBox { get; set; } = string.Empty;
        public List<string> AdjacentBlockers { get; set; } = new List<string>();
    }

    public sealed class MapCheckReport
    {
        public bool Ok { get; set; }
        public string MapFile { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int TotalWalkableTiles { get; set; }
        public int ReachableWalkableTiles { get; set; }
        public List<string> EntrySeeds { get; set; } = new List<string>();
        public List<DisconnectedIslandInfo> DisconnectedIslands { get; set; } = new List<DisconnectedIslandInfo>();
        public List<string> UnreachableInteractiveTiles { get; set; } = new List<string>();
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }

    public static class TmxMapChecker
    {
        /// <summary>
        /// Automatically invoked by `sdv-dev check` on all *.tmx files inside a mod folder.
        /// </summary>
        public static void ValidateModTmxFiles(string modDir, string? gamePath, List<ValidationIssue> issues)
        {
            if (!Directory.Exists(modDir))
                return;

            string[] tmxFiles;
            try
            {
                tmxFiles = Directory.GetFiles(modDir, "*.tmx", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string rel = f.Substring(modDir.Length);
                        return !rel.Contains("\\bin\\") && !rel.Contains("\\obj\\") && !rel.Contains("/bin/") && !rel.Contains("/obj/");
                    })
                    .ToArray();
            }
            catch
            {
                return;
            }

            if (tmxFiles.Length == 0)
                return;

            string effectiveGamePath = gamePath ?? KbConfig.Load().GamePath;
            foreach (var tmx in tmxFiles)
            {
                string relLabel = tmx.StartsWith(Path.GetFullPath(modDir), StringComparison.OrdinalIgnoreCase)
                    ? tmx.Substring(Path.GetFullPath(modDir).Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/')
                    : Path.GetFileName(tmx);

                var report = InspectSingleTmx(tmx, relLabel, modDir, effectiveGamePath, entryOverride: null);
                issues.AddRange(report.Issues);
            }
        }

        /// <summary>
        /// Standalone `sdv-dev map-check &lt;tmxOrModDirOrAsset&gt; [--entry x,y]` entry point.
        /// </summary>
        public static List<MapCheckReport> CheckTarget(string target, string gamePath, (int X, int Y)? entryOverride)
        {
            var reports = new List<MapCheckReport>();

            if (Directory.Exists(target))
            {
                string fullDir = Path.GetFullPath(target);
                var tmxFiles = Directory.GetFiles(fullDir, "*.tmx", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                    .OrderBy(f => f)
                    .ToList();

                foreach (var f in tmxFiles)
                {
                    string rel = f.Substring(fullDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                    reports.Add(InspectSingleTmx(f, rel, fullDir, gamePath, entryOverride));
                }
                return reports;
            }

            string resolved = TmxMap.ResolveMapFilePath(target, gamePath);
            string? modRootGuess = GuessModRoot(resolved);
            reports.Add(InspectSingleTmx(resolved, Path.GetFileName(resolved), modRootGuess, gamePath, entryOverride));
            return reports;
        }

        private static MapCheckReport InspectSingleTmx(
            string tmxFilePath,
            string fileLabel,
            string? modRootDir,
            string gamePath,
            (int X, int Y)? entryOverride)
        {
            var issues = new List<ValidationIssue>();
            var report = new MapCheckReport
            {
                MapFile = Path.GetFullPath(tmxFilePath),
                Issues = issues
            };

            TmxMap map;
            try
            {
                map = TmxMap.Load(tmxFilePath);
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = fileLabel,
                    Message = $"Failed to parse .tmx XML: {ex.Message}"
                });
                report.Ok = false;
                return report;
            }

            report.Width = map.Width;
            report.Height = map.Height;

            if (map.Width <= 0 || map.Height <= 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = fileLabel,
                    Message = $"Invalid map dimensions `{map.Width}x{map.Height}`."
                });
                report.Ok = false;
                return report;
            }

            string? tmxDir = Path.GetDirectoryName(Path.GetFullPath(tmxFilePath));

            // 1. Tileset PNG existence, real IHDR pixel dimensions vs declared width/height/columns/tilecount, & firstgid overlap!
            var sortedSheets = map.Tilesets.OrderBy(t => t.FirstGid).ToList();
            for (int i = 0; i < sortedSheets.Count; i++)
            {
                var ts = sortedSheets[i];
                string sheetLabel = $"{fileLabel}#tileset[{ts.Name}]";

                if (ts.FirstGid <= 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = sheetLabel,
                        Message = $"Tileset `{ts.Name}` has invalid firstgid={ts.FirstGid} (must be >= 1)."
                    });
                }

                if (i + 1 < sortedSheets.Count && ts.TileCount > 0)
                {
                    var next = sortedSheets[i + 1];
                    int endExclusive = ts.FirstGid + ts.TileCount;
                    if (endExclusive > next.FirstGid)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = sheetLabel,
                            Message = $"Tileset GID range [{ts.FirstGid}..{endExclusive - 1}] overlaps next tileset `{next.Name}` (firstgid={next.FirstGid})!"
                        });
                    }
                }

                string? pngPath = TmxMap.ResolveTilesheetPngPath(ts.ImageSource, tmxDir, gamePath);
                if (pngPath == null || !File.Exists(pngPath))
                {
                    bool hasGameContent = !string.IsNullOrWhiteSpace(gamePath) &&
                        (Directory.Exists(Path.Combine(gamePath, "Content")) || Directory.Exists(Path.Combine(gamePath, "Content (unpacked)")));
                    issues.Add(new ValidationIssue
                    {
                        Severity = hasGameContent ? "ERROR" : "WARN",
                        File = sheetLabel,
                        Message = $"Tilesheet image `{ts.ImageSource}` could not be resolved locally or in game `Content` (.xnb / unpacked)."
                    });
                }
                else if (TryReadPngSizeZeroLock(pngPath, out int actualW, out int actualH))
                {
                    int tw = Math.Max(1, ts.TileWidth);
                    int th = Math.Max(1, ts.TileHeight);
                    int actualCols = actualW / tw;
                    int actualRows = actualH / th;
                    int actualTileCount = actualCols * actualRows;

                    if (actualW % tw != 0 || actualH % th != 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "WARN",
                            File = sheetLabel,
                            Message = $"PNG `{Path.GetFileName(pngPath)}` pixel size {actualW}x{actualH} is not an exact multiple of tile size {tw}x{th}."
                        });
                    }

                    if (ts.ImageWidth > 0 && ts.ImageHeight > 0 && (ts.ImageWidth != actualW || ts.ImageHeight != actualH))
                    {
                        // If actual image is SMALLER than declared, xTile throws at load/draw time -> ERROR!
                        string sev = (actualW < ts.ImageWidth || actualH < ts.ImageHeight) ? "ERROR" : "WARN";
                        issues.Add(new ValidationIssue
                        {
                            Severity = sev,
                            File = sheetLabel,
                            Message = $"Declared <image width=\"{ts.ImageWidth}\" height=\"{ts.ImageHeight}\"/> mismatches actual PNG size {actualW}x{actualH} (`{Path.GetFileName(pngPath)}`)."
                        });
                    }

                    if (ts.Columns > 0 && ts.Columns != actualCols)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = sheetLabel,
                            Message = $"Declared columns={ts.Columns} mismatches actual PNG columns={actualCols} ({actualW}px / {tw}px) — causes scrambled tile indices!"
                        });
                    }

                    if (ts.TileCount > actualTileCount)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = sheetLabel,
                            Message = $"Declared tilecount={ts.TileCount} exceeds actual PNG capacity ({actualTileCount} tiles in {actualW}x{actualH})."
                        });
                    }
                }
            }

            // 2. Layer dimension, CSV cell count, & out-of-range GID validation!
            bool hasBack = map.Layers.Any(l => l.Name.Equals("Back", StringComparison.OrdinalIgnoreCase));
            if (!hasBack)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "ERROR",
                    File = fileLabel,
                    Message = "Missing required `Back` layer in Stardew Valley .tmx map."
                });
            }

            int expectedCells = map.Width * map.Height;
            foreach (var layer in map.Layers)
            {
                string layerLabel = $"{fileLabel}#layer[{layer.Name}]";
                if (layer.Width != map.Width || layer.Height != map.Height)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = layerLabel,
                        Message = $"Layer size {layer.Width}x{layer.Height} mismatches map size {map.Width}x{map.Height}."
                    });
                }

                if (layer.RawCsvCount != expectedCells)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = layerLabel,
                        Message = $"Layer data contains {layer.RawCsvCount} cell(s), expected {expectedCells} ({map.Width}x{map.Height})."
                    });
                }

                int badGidCount = 0;
                string? firstBadSample = null;
                for (int y = 0; y < Math.Min(map.Height, layer.Height); y++)
                {
                    for (int x = 0; x < Math.Min(map.Width, layer.Width); x++)
                    {
                        int gid = layer.GetCleanGid(x, y);
                        if (gid <= 0)
                            continue;

                        var owner = map.FindTilesetForGid(gid);
                        if (owner == null)
                        {
                            badGidCount++;
                            firstBadSample ??= $"({x},{y}) gid={gid}";
                        }
                    }
                }

                if (badGidCount > 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "ERROR",
                        File = layerLabel,
                        Message = $"{badGidCount} cell(s) reference an invalid/out-of-bounds GID (first at {firstBadSample})."
                    });
                }
            }

            // 3. Warp source & cross-map Warp destination safety audit!
            var warps = map.ParseWarps();
            var loadedTargetMaps = new Dictionary<string, TmxMap?>(StringComparer.OrdinalIgnoreCase);

            foreach (var w in warps)
            {
                string warpLabel = $"{fileLabel}#Warp({w.FromX},{w.FromY}->{w.TargetLocation}:{w.ToX},{w.ToY})";

                // Check source trigger accessibility:
                // MapWarp often sits at x=-1, x=width, y=-1, y=height (1 tile outside map border) or on border tile;
                // verify at least one clamped inward tile adjacent to (FromX, FromY) is walkable!
                int clampedX = Math.Max(0, Math.Min(map.Width - 1, w.FromX));
                int clampedY = Math.Max(0, Math.Min(map.Height - 1, w.FromY));
                if (!map.IsTilePassable(clampedX, clampedY, out string srcReason))
                {
                    // For Action warps (doors on Buildings layer), player stands at (FromX, FromY + 1) or adjacent orthogonal tile
                    bool isActionDoor = w.SourceKind.StartsWith("Action:", StringComparison.OrdinalIgnoreCase);
                    bool adjPassable = isActionDoor && HasPassableNeighbor(map, clampedX, clampedY);
                    if (!adjPassable)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "WARN",
                            File = warpLabel,
                            Message = $"Warp trigger approach tile ({clampedX},{clampedY}) is not walkable ({srcReason})."
                        });
                    }
                }

                // Check target landing coordinates if destination map can be resolved offline!
                if (w.ToX >= 0 && w.ToY >= 0 && !string.IsNullOrWhiteSpace(w.TargetLocation))
                {
                    if (!loadedTargetMaps.TryGetValue(w.TargetLocation, out var dstMap))
                    {
                        dstMap = TryLoadDestinationMap(w.TargetLocation, modRootDir, tmxDir, gamePath);
                        loadedTargetMaps[w.TargetLocation] = dstMap;
                    }

                    if (dstMap != null)
                    {
                        if (w.ToX >= dstMap.Width || w.ToY >= dstMap.Height)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = warpLabel,
                                Message = $"Warp target ({w.ToX},{w.ToY}) is out of bounds on `{w.TargetLocation}` ({dstMap.Width}x{dstMap.Height})!"
                            });
                        }
                        else if (!dstMap.IsTilePassable(w.ToX, w.ToY, out string dstReason))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "ERROR",
                                File = warpLabel,
                                Message = $"Warp lands player on an impassable/water tile ({w.ToX},{w.ToY}) in `{w.TargetLocation}` ({dstReason})!"
                            });
                        }
                        else
                        {
                            // Check immediate infinite warp bounce (landing directly on an active MapWarp or TouchAction warp tile!)
                            var dstWarps = dstMap.ParseWarps();
                            var bounce = dstWarps.FirstOrDefault(dw =>
                                !dw.SourceKind.StartsWith("Action:", StringComparison.OrdinalIgnoreCase) &&
                                dw.FromX == w.ToX && dw.FromY == w.ToY);
                            if (bounce != null)
                            {
                                issues.Add(new ValidationIssue
                                {
                                    Severity = "WARN",
                                    File = warpLabel,
                                    Message = $"Warp lands at ({w.ToX},{w.ToY}) directly on `{w.TargetLocation}`'s warp trigger to `{bounce.TargetLocation}` (causes instant warp bounce!)."
                                });
                            }
                        }
                    }
                }
            }

            // 4. BFS Walkability Connectivity & Unreachable Island / Action Detection!
            bool[,] passable = new bool[map.Width, map.Height];
            int totalWalkable = 0;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (map.IsTilePassable(x, y, out _))
                    {
                        passable[x, y] = true;
                        totalWalkable++;
                    }
                }
            }
            report.TotalWalkableTiles = totalWalkable;

            var seeds = new List<(int X, int Y)>();
            if (entryOverride.HasValue)
            {
                var eo = entryOverride.Value;
                if (eo.X >= 0 && eo.X < map.Width && eo.Y >= 0 && eo.Y < map.Height)
                {
                    if (!passable[eo.X, eo.Y])
                    {
                        map.IsTilePassable(eo.X, eo.Y, out string entryWhy);
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = fileLabel,
                            Message = $"Specified `--entry {eo.X},{eo.Y}` is NOT walkable ({entryWhy})!"
                        });
                    }
                    else
                    {
                        seeds.Add((eo.X, eo.Y));
                    }
                }
            }
            else
            {
                // Seed from inward tiles of all Warps/Doors
                foreach (var w in warps)
                {
                    int sx = Math.Max(0, Math.Min(map.Width - 1, w.FromX));
                    int sy = Math.Max(0, Math.Min(map.Height - 1, w.FromY));
                    if (passable[sx, sy])
                    {
                        seeds.Add((sx, sy));
                    }
                    else
                    {
                        foreach (var nb in OrthogonalNeighbors(sx, sy, map.Width, map.Height))
                        {
                            if (passable[nb.X, nb.Y])
                                seeds.Add(nb);
                        }
                    }
                }

                // If map has no warps yet (e.g. WIP draft map), pick the largest connected walkable component as primary area
                if (seeds.Count == 0 && totalWalkable > 0)
                {
                    var largestSeed = FindLargestComponentSeed(passable, map.Width, map.Height);
                    if (largestSeed.HasValue)
                        seeds.Add(largestSeed.Value);
                }
            }

            seeds = seeds.Distinct().ToList();
            report.EntrySeeds = seeds.Select(s => $"{s.X},{s.Y}").ToList();

            bool[,] visited = new bool[map.Width, map.Height];
            var q = new Queue<(int X, int Y)>();
            foreach (var s in seeds)
            {
                visited[s.X, s.Y] = true;
                q.Enqueue(s);
            }

            int reachableCount = 0;
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                reachableCount++;
                foreach (var nb in OrthogonalNeighbors(cur.X, cur.Y, map.Width, map.Height))
                {
                    if (passable[nb.X, nb.Y] && !visited[nb.X, nb.Y])
                    {
                        visited[nb.X, nb.Y] = true;
                        q.Enqueue(nb);
                    }
                }
            }
            report.ReachableWalkableTiles = reachableCount;

            // Enumerate disconnected walkable islands
            bool[,] islandSeen = new bool[map.Width, map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (!passable[x, y] || visited[x, y] || islandSeen[x, y])
                        continue;

                    var comp = new List<(int X, int Y)>();
                    var blockers = new HashSet<string>();
                    var iq = new Queue<(int X, int Y)>();
                    islandSeen[x, y] = true;
                    iq.Enqueue((x, y));

                    int minX = x, maxX = x, minY = y, maxY = y;
                    while (iq.Count > 0)
                    {
                        var c = iq.Dequeue();
                        comp.Add(c);
                        if (c.X < minX) minX = c.X;
                        if (c.X > maxX) maxX = c.X;
                        if (c.Y < minY) minY = c.Y;
                        if (c.Y > maxY) maxY = c.Y;

                        foreach (var nb in OrthogonalNeighbors(c.X, c.Y, map.Width, map.Height))
                        {
                            if (passable[nb.X, nb.Y])
                            {
                                if (!islandSeen[nb.X, nb.Y])
                                {
                                    islandSeen[nb.X, nb.Y] = true;
                                    iq.Enqueue(nb);
                                }
                            }
                            else if (blockers.Count < 6)
                            {
                                map.IsTilePassable(nb.X, nb.Y, out string bWhy);
                                if (bWhy.StartsWith("blocked_by_Buildings", StringComparison.OrdinalIgnoreCase))
                                    blockers.Add($"({nb.X},{nb.Y}):{bWhy}");
                            }
                        }
                    }

                    report.DisconnectedIslands.Add(new DisconnectedIslandInfo
                    {
                        TileCount = comp.Count,
                        SampleTile = $"{comp[0].X},{comp[0].Y}",
                        BoundingBox = $"{minX},{minY}..{maxX},{maxY}",
                        AdjacentBlockers = blockers.ToList()
                    });
                }
            }

            // Flag significant disconnected islands (>= 4 tiles, e.g. beach/plateau cut off by uncleared stairs/bridge Buildings tiles)
            var significantIslands = report.DisconnectedIslands.Where(i => i.TileCount >= 4).OrderByDescending(i => i.TileCount).ToList();
            if (significantIslands.Count > 0)
            {
                var top = significantIslands[0];
                string blockerHint = top.AdjacentBlockers.Count > 0 ? $" (adjacent blockers: {string.Join(", ", top.AdjacentBlockers.Take(3))})" : "";
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = fileLabel,
                    Message = $"Found {significantIslands.Count} unreachable walkable area(s) cut off from entry seeds! Largest: {top.TileCount} tiles around ({top.SampleTile}) [{top.BoundingBox}]{blockerHint}."
                });
            }

            // Check unreachable interactive Action / TouchAction entities!
            // Connected tiles sharing the same Action/TouchAction string (e.g. a 3x2 moored boat, shop counter, or wishing well)
            // form a single multi-tile interactive entity: as long as ANY tile in the entity is reachable (stepped on or adjacent to walkable),
            // the player can interact with the entity without false-positive warnings!
            var interactiveCells = new Dictionary<(string Kind, string Value, int X, int Y), string>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    foreach (var l in map.Layers)
                    {
                        var props = map.GetEffectiveLayerCellProperties(l, x, y);
                        if (props.TryGetValue("Action", out var av) && !string.IsNullOrWhiteSpace(av))
                        {
                            interactiveCells[("Action", av!.Trim(), x, y)] = l.Name;
                        }
                        if (props.TryGetValue("TouchAction", out var tv) && !string.IsNullOrWhiteSpace(tv))
                        {
                            interactiveCells[("TouchAction", tv!.Trim(), x, y)] = l.Name;
                        }
                    }
                }
            }

            var entityVisited = new HashSet<(string Kind, string Value, int X, int Y)>();
            foreach (var kv in interactiveCells)
            {
                if (!entityVisited.Add(kv.Key))
                    continue;

                string kind = kv.Key.Kind;
                string val = kv.Key.Value;
                var cluster = new List<(int X, int Y, string Layer)>();
                var cq = new Queue<(int X, int Y, string Layer)>();
                cq.Enqueue((kv.Key.X, kv.Key.Y, kv.Value));

                while (cq.Count > 0)
                {
                    var cur = cq.Dequeue();
                    cluster.Add(cur);
                    foreach (var nb in OrthogonalNeighbors(cur.X, cur.Y, map.Width, map.Height))
                    {
                        var nbKey = (kind, val, nb.X, nb.Y);
                        if (interactiveCells.TryGetValue(nbKey, out var nbLayer) && entityVisited.Add(nbKey))
                        {
                            cq.Enqueue((nb.X, nb.Y, nbLayer));
                        }
                    }
                }

                bool anyTileReachable = cluster.Any(cell =>
                    visited[cell.X, cell.Y] ||
                    (kind == "Action" && OrthogonalNeighbors(cell.X, cell.Y, map.Width, map.Height).Any(n => visited[n.X, n.Y])));

                if (!anyTileReachable)
                {
                    var rep = cluster[0];
                    string span = cluster.Count > 1 ? $" ({cluster.Count}-tile entity)" : "";
                    report.UnreachableInteractiveTiles.Add($"({rep.X},{rep.Y}){span} [{rep.Layer}] {kind}=\"{val}\"");
                }
            }

            if (report.UnreachableInteractiveTiles.Count > 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "WARN",
                    File = fileLabel,
                    Message = $"{report.UnreachableInteractiveTiles.Count} Action/TouchAction tile(s) cannot be reached from map entry: {string.Join("; ", report.UnreachableInteractiveTiles.Take(3))}"
                });
            }

            report.Ok = !issues.Any(i => i.Severity == "ERROR");
            return report;
        }

        private static TmxMap? TryLoadDestinationMap(string targetLoc, string? modRootDir, string? tmxDir, string gamePath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(tmxDir))
                candidates.Add(Path.Combine(tmxDir!, targetLoc + ".tmx"));
            if (!string.IsNullOrEmpty(modRootDir) && Directory.Exists(modRootDir))
            {
                try
                {
                    var localMatch = Directory.GetFiles(modRootDir!, "*.tmx", SearchOption.AllDirectories)
                        .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(targetLoc, StringComparison.OrdinalIgnoreCase));
                    if (localMatch != null)
                        candidates.Add(localMatch);
                }
                catch { }
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    try { return TmxMap.Load(c); } catch { }
                }
            }

            try
            {
                string vanilla = TmxMap.ResolveMapFilePath(targetLoc, gamePath);
                return TmxMap.Load(vanilla);
            }
            catch
            {
                return null;
            }
        }

        private static (int X, int Y)? FindLargestComponentSeed(bool[,] passable, int w, int h)
        {
            bool[,] seen = new bool[w, h];
            (int X, int Y)? bestSeed = null;
            int bestSize = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!passable[x, y] || seen[x, y])
                        continue;

                    int count = 0;
                    var q = new Queue<(int X, int Y)>();
                    seen[x, y] = true;
                    q.Enqueue((x, y));
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        count++;
                        foreach (var nb in OrthogonalNeighbors(c.X, c.Y, w, h))
                        {
                            if (passable[nb.X, nb.Y] && !seen[nb.X, nb.Y])
                            {
                                seen[nb.X, nb.Y] = true;
                                q.Enqueue(nb);
                            }
                        }
                    }

                    if (count > bestSize)
                    {
                        bestSize = count;
                        bestSeed = (x, y);
                    }
                }
            }

            return bestSeed;
        }

        private static bool HasPassableNeighbor(TmxMap map, int x, int y) =>
            OrthogonalNeighbors(x, y, map.Width, map.Height).Any(n => map.IsTilePassable(n.X, n.Y, out _));

        private static IEnumerable<(int X, int Y)> OrthogonalNeighbors(int x, int y, int w, int h)
        {
            if (x > 0) yield return (x - 1, y);
            if (x + 1 < w) yield return (x + 1, y);
            if (y > 0) yield return (x, y - 1);
            if (y + 1 < h) yield return (x, y + 1);
        }

        private static string? GuessModRoot(string filePath)
        {
            string? cur = Path.GetDirectoryName(Path.GetFullPath(filePath));
            for (int depth = 0; depth < 5 && !string.IsNullOrEmpty(cur); depth++)
            {
                if (File.Exists(Path.Combine(cur!, "manifest.json")))
                    return cur;
                cur = Path.GetDirectoryName(cur!);
            }
            return null;
        }

        private static bool TryReadPngSizeZeroLock(string pngPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                byte[] header = new byte[24];
                using (var fs = new FileStream(pngPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Read(header, 0, 24) < 24)
                        return false;
                }

                // PNG signature: 89 50 4E 47 0D 0A 1A 0A + IHDR at offset 12..15
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                    header[12] == (byte)'I' && header[13] == (byte)'H' && header[14] == (byte)'D' && header[15] == (byte)'R')
                {
                    width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return width > 0 && height > 0;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
