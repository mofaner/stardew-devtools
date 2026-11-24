using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SdvKb.Maps
{
    public static class MapInspectorAndRenderer
    {
        public static object ExportTilesheetPng(
            string sheetQuery,
            string gamePath,
            string outPngPath,
            bool drawGrid,
            int tileSize = 16,
            int? zoomOverride = null)
        {
            string? resolved = TmxMap.ResolveTilesheetPngPath(sheetQuery, Directory.GetCurrentDirectory(), gamePath);
            if (resolved == null || !File.Exists(resolved))
                throw new FileNotFoundException($"Could not locate tilesheet PNG for `{sheetQuery}` locally or in `{Path.Combine(gamePath, "Content (unpacked)")}`.");

            using var srcBmp = LoadBitmapZeroLock(resolved);
            int cols = srcBmp.Width / tileSize;
            int rows = srcBmp.Height / tileSize;
            int tileCount = cols * rows;

            int zoom = zoomOverride ?? (drawGrid ? 3 : 1);
            if (zoom < 1) zoom = 1;
            if (zoom > 6) zoom = 6;

            int cellPx = tileSize * zoom;
            int rulerMargin = drawGrid ? 24 : 0;
            int outW = rulerMargin + srcBmp.Width * zoom;
            int outH = rulerMargin + srcBmp.Height * zoom;

            string fullOut = Path.GetFullPath(outPngPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);

            using var outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outBmp))
            {
                g.Clear(drawGrid ? Color.FromArgb(255, 28, 30, 36) : Color.Transparent);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                // Checkerboard backdrop for tilesheet area when grid is enabled so transparent vs white pixels are obvious!
                if (drawGrid)
                {
                    using var c1 = new SolidBrush(Color.FromArgb(255, 42, 45, 52));
                    using var c2 = new SolidBrush(Color.FromArgb(255, 54, 58, 66));
                    int checkSize = Math.Max(8, cellPx / 2);
                    for (int py = rulerMargin; py < outH; py += checkSize)
                    {
                        for (int px = rulerMargin; px < outW; px += checkSize)
                        {
                            bool alt = (((px - rulerMargin) / checkSize) + ((py - rulerMargin) / checkSize)) % 2 == 1;
                            g.FillRectangle(alt ? c2 : c1, px, py, Math.Min(checkSize, outW - px), Math.Min(checkSize, outH - py));
                        }
                    }
                }

                g.DrawImage(srcBmp, new Rectangle(rulerMargin, rulerMargin, srcBmp.Width * zoom, srcBmp.Height * zoom),
                    new Rectangle(0, 0, srcBmp.Width, srcBmp.Height), GraphicsUnit.Pixel);

                if (drawGrid)
                {
                    using var minorPen = new Pen(Color.FromArgb(110, 255, 255, 255), 1f);
                    using var majorPen = new Pen(Color.FromArgb(200, 255, 215, 0), 1f);
                    using var labelBg = new SolidBrush(Color.FromArgb(185, 0, 0, 0));
                    using var labelFg = new SolidBrush(Color.FromArgb(255, 255, 248, 180));
                    using var rulerFg = new SolidBrush(Color.FromArgb(255, 190, 210, 255));
                    using var font = new Font("Consolas", zoom >= 3 ? 7.5f : 6.5f, FontStyle.Bold, GraphicsUnit.Point);

                    // Column & row headers in margin
                    for (int c = 0; c < cols; c++)
                    {
                        int x = rulerMargin + c * cellPx;
                        g.DrawLine(c % 4 == 0 ? majorPen : minorPen, x, rulerMargin, x, outH);
                        g.DrawString($"c{c}", font, rulerFg, x + 2, 4);
                    }
                    for (int r = 0; r < rows; r++)
                    {
                        int y = rulerMargin + r * cellPx;
                        g.DrawLine(r % 4 == 0 ? majorPen : minorPen, rulerMargin, y, outW, y);
                        if (r % 2 == 0 || zoom >= 3)
                            g.DrawString($"r{r}", font, rulerFg, 2, y + 2);
                    }

                    // TileIndex 0..N-1 badge on each non-empty tile
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            int idx = r * cols + c;
                            bool empty = IsTileCompletelyTransparent(srcBmp, c * tileSize, r * tileSize, tileSize, tileSize);
                            if (empty)
                                continue;

                            int x = rulerMargin + c * cellPx;
                            int y = rulerMargin + r * cellPx;
                            string text = idx.ToString();
                            SizeF sz = g.MeasureString(text, font);
                            g.FillRectangle(labelBg, x + 1, y + 1, Math.Min(cellPx - 2, sz.Width), Math.Min(cellPx - 2, sz.Height - 2));
                            g.DrawString(text, font, labelFg, x + 1, y + 1);
                        }
                    }
                }
            }

            outBmp.Save(fullOut, ImageFormat.Png);
            return new
            {
                ok = true,
                sourcePng = resolved,
                outputPng = fullOut,
                imageWidth = srcBmp.Width,
                imageHeight = srcBmp.Height,
                tileSize,
                columns = cols,
                rows,
                tileCount,
                grid = drawGrid,
                zoom
            };
        }

        public static object InspectMap(
            string mapOrAsset,
            string gamePath,
            (int X, int Y, int W, int H)? rectFilter,
            string? layerFilter)
        {
            string tmxPath = TmxMap.ResolveMapFilePath(mapOrAsset, gamePath);
            var map = TmxMap.Load(tmxPath);
            string? tmxDir = Path.GetDirectoryName(tmxPath);

            var tilesetsInfo = map.Tilesets.Select((ts, idx) =>
            {
                string? pngPath = TmxMap.ResolveTilesheetPngPath(ts.ImageSource, tmxDir, gamePath);
                return new
                {
                    sheetIndex = idx,
                    name = ts.Name,
                    firstGid = ts.FirstGid,
                    tileCount = ts.TileCount,
                    columns = ts.Columns,
                    imageSource = ts.ImageSource,
                    declaredSize = $"{ts.ImageWidth}x{ts.ImageHeight}",
                    resolvedPng = pngPath,
                    animatedTileIds = ts.Animations.Keys.OrderBy(k => k).ToList(),
                    waterTileIds = ts.TileIndexProperties
                        .Where(kv => kv.Value.ContainsKey("Water"))
                        .Select(kv => kv.Key)
                        .OrderBy(k => k)
                        .ToList()
                };
            }).ToList();

            var warps = map.ParseWarps();
            var lights = map.ParseLights();

            object? rectDump = null;
            if (rectFilter.HasValue)
            {
                var r = rectFilter.Value;
                int rx = Math.Max(0, r.X);
                int ry = Math.Max(0, r.Y);
                int rw = Math.Min(r.W, map.Width - rx);
                int rh = Math.Min(r.H, map.Height - ry);

                var layerMatrices = new Dictionary<string, List<string>>();
                foreach (var layer in map.Layers)
                {
                    if (!string.IsNullOrWhiteSpace(layerFilter) && !layer.Name.Equals(layerFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rows = new List<string>();
                    for (int y = ry; y < ry + rh; y++)
                    {
                        var cells = new List<string>();
                        for (int x = rx; x < rx + rw; x++)
                        {
                            int gid = layer.GetCleanGid(x, y);
                            if (gid == 0)
                            {
                                cells.Add("  .  ");
                            }
                            else
                            {
                                var ts = map.FindTilesetForGid(gid);
                                int sIdx = ts != null ? map.Tilesets.IndexOf(ts) : -1;
                                int localId = ts != null ? gid - ts.FirstGid : gid;
                                cells.Add($"{sIdx}:{localId,-3}");
                            }
                        }
                        rows.Add($"y={y,2}: " + string.Join(" ", cells));
                    }
                    layerMatrices[layer.Name] = rows;
                }

                // Passability & special properties summary in rect
                var passRows = new List<string>();
                var specialCells = new List<object>();
                for (int y = ry; y < ry + rh; y++)
                {
                    var pChars = new char[rw];
                    for (int x = rx; x < rx + rw; x++)
                    {
                        bool water = map.IsWaterTile(x, y);
                        bool pass = map.IsTilePassable(x, y, out string reason);
                        pChars[x - rx] = water && !pass ? '~' : (pass ? '.' : '#');

                        foreach (var l in map.Layers)
                        {
                            var props = map.GetEffectiveLayerCellProperties(l, x, y);
                            if (props.Count > 0 && !props.All(kv => kv.Key == "Type" || kv.Key == "Diggable"))
                            {
                                specialCells.Add(new { x, y, layer = l.Name, properties = props });
                            }
                        }
                    }
                    passRows.Add($"y={y,2}: " + new string(pChars));
                }

                rectDump = new
                {
                    rect = new { x = rx, y = ry, width = rw, height = rh },
                    legend = "SheetIndex:LocalTileId ('.' = empty); PassabilityMask: '.'=walkable, '#'=blocked, '~'=water",
                    passabilityMask = passRows,
                    layers = layerMatrices,
                    specialTileProperties = specialCells
                };
            }

            return new
            {
                ok = true,
                mapFile = tmxPath,
                width = map.Width,
                height = map.Height,
                tileSize = $"{map.TileWidth}x{map.TileHeight}",
                mapProperties = map.Properties,
                layers = map.Layers.Select(l => l.Name).ToList(),
                tilesets = tilesetsInfo,
                warps,
                lights,
                region = rectDump
            };
        }

        public static object RenderMapToPng(
            string mapOrAsset,
            string gamePath,
            string outPngPath,
            string? seasonOverride,
            bool debugOverlay,
            int zoom = 1,
            bool showPathsLayer = false)
        {
            string tmxPath = TmxMap.ResolveMapFilePath(mapOrAsset, gamePath);
            var map = TmxMap.Load(tmxPath);
            string? tmxDir = Path.GetDirectoryName(tmxPath);

            string? effectiveSeason = seasonOverride;
            if (string.IsNullOrWhiteSpace(effectiveSeason) && map.Properties.TryGetValue("SeasonOverride", out var mapSeason))
                effectiveSeason = mapSeason;

            if (zoom < 1) zoom = 1;
            if (zoom > 4) zoom = 4;

            int tw = map.TileWidth;
            int th = map.TileHeight;
            int cellW = tw * zoom;
            int cellH = th * zoom;

            int rulerLeft = debugOverlay ? 28 : 0;
            int rulerTop = debugOverlay ? 22 : 0;
            int legendBottom = debugOverlay ? 26 : 0;

            int mapPxW = map.Width * cellW;
            int mapPxH = map.Height * cellH;
            int totalW = Math.Max(320, rulerLeft + mapPxW);
            int totalH = rulerTop + mapPxH + legendBottom;

            var loadedSheets = new Dictionary<int, Bitmap>();
            var missingSheets = new List<string>();

            try
            {
                for (int i = 0; i < map.Tilesets.Count; i++)
                {
                    var ts = map.Tilesets[i];
                    string? png = TmxMap.ResolveTilesheetPngPath(ts.ImageSource, tmxDir, gamePath, effectiveSeason);
                    if (png != null && File.Exists(png))
                    {
                        var bmp = LoadBitmapZeroLock(png);
                        loadedSheets[i] = bmp;
                        if (ts.Columns <= 0)
                            ts.Columns = bmp.Width / Math.Max(1, ts.TileWidth);
                        if (ts.TileCount <= 0)
                            ts.TileCount = ts.Columns * (bmp.Height / Math.Max(1, ts.TileHeight));
                    }
                    else
                    {
                        missingSheets.Add(ts.ImageSource);
                    }
                }

                string fullOut = Path.GetFullPath(outPngPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);

                using var canvas = new Bitmap(totalW, totalH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.FromArgb(255, 20, 22, 28));
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.SmoothingMode = SmoothingMode.None;

                    // Draw map layers in order (skipping editor-only Paths layer unless requested)
                    foreach (var layer in map.Layers)
                    {
                        if (!layer.Visible)
                            continue;
                        if (!showPathsLayer && layer.Name.Equals("Paths", StringComparison.OrdinalIgnoreCase))
                            continue;

                        for (int y = 0; y < Math.Min(map.Height, layer.Height); y++)
                        {
                            for (int x = 0; x < Math.Min(map.Width, layer.Width); x++)
                            {
                                uint rawGid = layer.GetRawGid(x, y);
                                int cleanGid = (int)(rawGid & 0x1FFFFFFFu);
                                if (cleanGid <= 0)
                                    continue;

                                var ts = map.FindTilesetForGid(cleanGid);
                                if (ts == null)
                                    continue;
                                int tsIdx = map.Tilesets.IndexOf(ts);
                                if (!loadedSheets.TryGetValue(tsIdx, out var sheetBmp) || ts.Columns <= 0)
                                    continue;

                                int localId = cleanGid - ts.FirstGid;
                                int srcX = (localId % ts.Columns) * ts.TileWidth;
                                int srcY = (localId / ts.Columns) * ts.TileHeight;
                                if (srcX + ts.TileWidth > sheetBmp.Width || srcY + ts.TileHeight > sheetBmp.Height)
                                    continue;

                                var destRect = new Rectangle(rulerLeft + x * cellW, rulerTop + y * cellH, cellW, cellH);
                                var srcRect = new Rectangle(srcX, srcY, ts.TileWidth, ts.TileHeight);

                                bool flipH = (rawGid & TmxMap.FlipHorizontalFlag) != 0;
                                bool flipV = (rawGid & TmxMap.FlipVerticalFlag) != 0;
                                bool flipD = (rawGid & TmxMap.FlipDiagonalFlag) != 0;

                                if (!flipH && !flipV && !flipD)
                                {
                                    g.DrawImage(sheetBmp, destRect, srcRect, GraphicsUnit.Pixel);
                                }
                                else
                                {
                                    using var tileClone = sheetBmp.Clone(srcRect, PixelFormat.Format32bppArgb);
                                    ApplyTiledFlips(tileClone, flipH, flipV, flipD);
                                    g.DrawImage(tileClone, destRect);
                                }
                            }
                        }
                    }

                    if (debugOverlay)
                    {
                        DrawDebugOverlay(g, map, rulerLeft, rulerTop, cellW, cellH, totalW, totalH);
                    }
                }

                canvas.Save(fullOut, ImageFormat.Png);
                return new
                {
                    ok = true,
                    mapFile = tmxPath,
                    outputPng = fullOut,
                    mapDimensions = $"{map.Width}x{map.Height}",
                    imagePixels = $"{totalW}x{totalH}",
                    season = effectiveSeason ?? "default",
                    debugOverlay,
                    missingTilesheets = missingSheets
                };
            }
            finally
            {
                foreach (var bmp in loadedSheets.Values)
                    bmp.Dispose();
            }
        }

        private static void DrawDebugOverlay(
            Graphics g,
            TmxMap map,
            int ox,
            int oy,
            int cw,
            int ch,
            int totalW,
            int totalH)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var waterBrush = new SolidBrush(Color.FromArgb(105, 30, 144, 255));
            using var waterPen = new Pen(Color.FromArgb(190, 80, 195, 255), 1f);
            using var blockBrush = new SolidBrush(Color.FromArgb(105, 235, 50, 50));
            using var blockPen = new Pen(Color.FromArgb(180, 255, 80, 80), 1f);
            using var gridMinor = new Pen(Color.FromArgb(45, 255, 255, 255), 1f);
            using var gridMajor = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
            using var warpPen = new Pen(Color.FromArgb(255, 50, 255, 90), 2f);
            using var warpFill = new SolidBrush(Color.FromArgb(110, 40, 220, 80));
            using var actionPen = new Pen(Color.FromArgb(255, 255, 215, 0), 2f);
            using var actionFill = new SolidBrush(Color.FromArgb(110, 255, 200, 0));
            using var lightPen = new Pen(Color.FromArgb(255, 0, 245, 255), 2f);
            using var rulerTextBrush = new SolidBrush(Color.FromArgb(255, 210, 225, 255));
            using var tagBg = new SolidBrush(Color.FromArgb(205, 10, 12, 16));
            using var whiteBrush = new SolidBrush(Color.White);
            using var smallFont = new Font("Consolas", 7f, FontStyle.Bold, GraphicsUnit.Point);

            int waterCount = 0;
            int blockedCount = 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int px = ox + x * cw;
                    int py = oy + y * ch;
                    var rect = new Rectangle(px, py, cw, ch);

                    bool isWater = map.IsWaterTile(x, y);
                    bool passable = map.IsTilePassable(x, y, out _);

                    if (isWater)
                    {
                        waterCount++;
                        g.FillRectangle(waterBrush, rect);
                        g.DrawLine(waterPen, px + 2, py + ch / 2, px + cw - 3, py + ch / 2);
                    }

                    // If blocked AND not plain water (or blocked even on bridge/buildings over water), show red collision mask!
                    if (!passable && !isWater)
                    {
                        blockedCount++;
                        g.FillRectangle(blockBrush, rect);
                        g.DrawLine(blockPen, px + 2, py + 2, px + cw - 3, py + ch - 3);
                    }

                    // Highlight Action / TouchAction tiles
                    foreach (var l in map.Layers)
                    {
                        var props = map.GetEffectiveLayerCellProperties(l, x, y);
                        if (props.ContainsKey("Action") || props.ContainsKey("TouchAction"))
                        {
                            g.FillRectangle(actionFill, rect);
                            g.DrawRectangle(actionPen, px + 1, py + 1, cw - 2, ch - 2);
                            break;
                        }
                    }
                }
            }

            // Grid lines & coordinate rulers (every 5 tiles labeled)
            for (int x = 0; x <= map.Width; x++)
            {
                int px = ox + x * cw;
                g.DrawLine(x % 5 == 0 ? gridMajor : gridMinor, px, oy, px, oy + map.Height * ch);
                if (x < map.Width && x % 5 == 0)
                    g.DrawString(x.ToString(), smallFont, rulerTextBrush, px + 1, 4);
            }
            for (int y = 0; y <= map.Height; y++)
            {
                int py = oy + y * ch;
                g.DrawLine(y % 5 == 0 ? gridMajor : gridMinor, ox, py, ox + map.Width * cw, py);
                if (y < map.Height && y % 5 == 0)
                    g.DrawString(y.ToString(), smallFont, rulerTextBrush, 2, py + 1);
            }

            // Draw Warps (clamping off-map edge warps -1 / Width / Height onto edge tile with arrow, per Pathoschild's AccessibleLayer!)
            foreach (var w in map.ParseWarps())
            {
                string arrow = "";
                int cx = w.FromX;
                int cy = w.FromY;
                if (cx < 0) { cx = 0; arrow = "<"; }
                else if (cx >= map.Width) { cx = map.Width - 1; arrow = ">"; }
                if (cy < 0) { cy = 0; arrow = "^"; }
                else if (cy >= map.Height) { cy = map.Height - 1; arrow = "v"; }

                if (cx >= 0 && cx < map.Width && cy >= 0 && cy < map.Height)
                {
                    int px = ox + cx * cw;
                    int py = oy + cy * ch;
                    g.FillRectangle(warpFill, px, py, cw, ch);
                    g.DrawRectangle(warpPen, px + 1, py + 1, cw - 2, ch - 2);
                    if (!string.IsNullOrEmpty(arrow))
                        g.DrawString(arrow, smallFont, whiteBrush, px + 2, py + 1);
                }
            }

            // Draw Lights
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var lt in map.ParseLights())
            {
                if (lt.TileX >= 0 && lt.TileX < map.Width && lt.TileY >= 0 && lt.TileY < map.Height)
                {
                    int px = ox + lt.TileX * cw;
                    int py = oy + lt.TileY * ch;
                    g.DrawEllipse(lightPen, px + 2, py + 2, cw - 4, ch - 4);
                }
            }

            // Bottom legend bar
            int ly = totalH - 22;
            g.FillRectangle(tagBg, 0, ly - 2, totalW, 24);
            string legend = $"Map {map.Width}x{map.Height} | RED=Blocked({blockedCount}) | BLUE=Water({waterCount}) | GREEN=Warp | YELLOW=Action/Touch | CYAN=Light";
            g.DrawString(legend, smallFont, whiteBrush, 6, ly + 3);
        }

        private static void ApplyTiledFlips(Bitmap bmp, bool flipH, bool flipV, bool flipD)
        {
            if (flipD)
            {
                // In Tiled, anti-diagonal flip = Rotate90FlipX!
                bmp.RotateFlip(RotateFlipType.Rotate90FlipX);
            }
            if (flipH && flipV)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipXY);
            else if (flipH)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
            else if (flipV)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        private static bool IsTileCompletelyTransparent(Bitmap bmp, int startX, int startY, int w, int h)
        {
            int maxX = Math.Min(bmp.Width, startX + w);
            int maxY = Math.Min(bmp.Height, startY + h);
            for (int y = startY; y < maxY; y += 2)
            {
                for (int x = startX; x < maxX; x += 2)
                {
                    if (bmp.GetPixel(x, y).A > 0)
                        return false;
                }
            }
            return true;
        }

        internal static Bitmap LoadBitmapZeroLock(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            using var ms = new MemoryStream(bytes);
            using var temp = new Bitmap(ms);
            // Clone into standalone 32bppArgb bitmap so MemoryStream can be disposed immediately with zero handle retention
            return new Bitmap(temp);
        }
    }
}
