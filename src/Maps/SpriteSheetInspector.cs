using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;

namespace SdvKb.Maps
{
    public sealed class SpriteFrameReport
    {
        public int Index { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public string? Label { get; set; }
        public string ContentBounds { get; set; } = string.Empty; // relative x,y,w,h inside cell
        public double CenterOffsetX { get; set; }
        public int BottomGapPx { get; set; }
    }

    public static class SpriteSheetInspector
    {
        private static readonly string[] NpcRowLabels =
        {
            "Walk Down",
            "Walk Right",
            "Walk Up",
            "Walk Left"
        };

        private static readonly string[] PortraitSlotLabels =
        {
            "Neutral",
            "Happy",
            "Sad",
            "Unique",
            "Blush / Love",
            "Angry"
        };

        public static object InspectAndRenderSpriteSheet(
            string targetQuery,
            string gamePath,
            string? outPngPath,
            string? presetRaw,
            (int W, int H)? explicitCellSize,
            int? zoomOverride,
            bool showBoundingBoxes = true)
        {
            string? resolved = ResolveSpritePngPath(targetQuery, gamePath);
            if (resolved == null || !File.Exists(resolved))
                throw new FileNotFoundException($"Could not locate texture `{targetQuery}` locally, in `Content (unpacked)`, or in `{Path.Combine(gamePath, "Content")}` (.xnb).");

            using var bmp = LoadBitmapUnlocked(resolved);
            string preset = DeterminePreset(resolved, targetQuery, bmp.Width, bmp.Height, presetRaw, explicitCellSize, out int cellW, out int cellH);

            var warnings = new List<string>();
            if (bmp.Width % cellW != 0 || bmp.Height % cellH != 0)
            {
                warnings.Add($"Image size {bmp.Width}x{bmp.Height} is not an exact multiple of cell size {cellW}x{cellH} (remainder {bmp.Width % cellW}x{bmp.Height % cellH}px will be truncated by Stardew).");
            }
            if (preset == "portrait" && bmp.Width != 128 && bmp.Width != 64)
            {
                warnings.Add($"Stardew NPC portrait sheets are normally 128px wide (2 columns of 64x64); got width={bmp.Width}px.");
            }
            if (preset == "npc" && bmp.Width != 64)
            {
                warnings.Add($"Standard Stardew NPC overworld sheets are 64px wide (4 columns of 16x32); got width={bmp.Width}px.");
            }

            int cols = Math.Max(1, bmp.Width / cellW);
            int rows = Math.Max(1, bmp.Height / cellH);
            int totalSlots = cols * rows;

            var nonEmptyFrames = new List<SpriteFrameReport>();
            var frameBoxes = new Dictionary<int, Rectangle>();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (TryComputeOpaqueBounds(bmp, c * cellW, r * cellH, cellW, cellH, out var relBox))
                    {
                        frameBoxes[idx] = relBox;
                        double centerDiff = Math.Round((relBox.X + relBox.Width / 2.0) - (cellW / 2.0), 1);
                        int bottomGap = cellH - (relBox.Y + relBox.Height);

                        string? label = null;
                        if (preset == "portrait" && idx < PortraitSlotLabels.Length)
                            label = PortraitSlotLabels[idx];
                        else if (preset == "npc")
                            label = r < NpcRowLabels.Length ? $"{NpcRowLabels[r]} #{c}" : $"Anim r{r} #{c}";

                        nonEmptyFrames.Add(new SpriteFrameReport
                        {
                            Index = idx,
                            Column = c,
                            Row = r,
                            Label = label,
                            ContentBounds = $"{relBox.X},{relBox.Y},{relBox.Width}x{relBox.Height}",
                            CenterOffsetX = centerDiff,
                            BottomGapPx = bottomGap
                        });
                    }
                }
            }

            // Detect inconsistent horizontal centering across NPC walk frames
            if (preset == "npc" && nonEmptyFrames.Count >= 4)
            {
                var offCenter = nonEmptyFrames.Where(f => f.Row < 4 && Math.Abs(f.CenterOffsetX) >= 2.0).ToList();
                if (offCenter.Count > 0)
                {
                    warnings.Add($"NPC walk frame(s) [{string.Join(", ", offCenter.Select(f => $"#{f.Index}({f.CenterOffsetX:+0.0;-0.0}px)"))}] are horizontally off-center by >= 2px (may jitter while walking).");
                }
            }

            string? writtenPng = null;
            int usedZoom = zoomOverride ?? (cellW >= 64 ? 2 : 4);
            if (usedZoom < 1) usedZoom = 1;
            if (usedZoom > 8) usedZoom = 8;

            if (!string.IsNullOrWhiteSpace(outPngPath))
            {
                writtenPng = Path.GetFullPath(outPngPath!);
                Directory.CreateDirectory(Path.GetDirectoryName(writtenPng)!);
                RenderContactSheetPng(bmp, writtenPng, preset, cellW, cellH, cols, rows, usedZoom, showBoundingBoxes, frameBoxes);
            }

            return new
            {
                ok = true,
                sourcePng = resolved,
                outputPng = writtenPng,
                preset,
                imageSize = $"{bmp.Width}x{bmp.Height}",
                cellSize = $"{cellW}x{cellH}",
                columns = cols,
                rows,
                totalSlots,
                nonEmptySlotCount = nonEmptyFrames.Count,
                warnings,
                frames = nonEmptyFrames
            };
        }

        private static void RenderContactSheetPng(
            Bitmap srcBmp,
            string fullOut,
            string preset,
            int cellW,
            int cellH,
            int cols,
            int rows,
            int zoom,
            bool showBoundingBoxes,
            Dictionary<int, Rectangle> frameBoxes)
        {
            int leftMargin = preset == "npc" ? 96 : 32;
            int topMargin = 26;
            int scaledW = srcBmp.Width * zoom;
            int scaledH = srcBmp.Height * zoom;
            int outW = leftMargin + scaledW + 4;
            int outH = topMargin + scaledH + 4;

            using var outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outBmp))
            {
                g.Clear(Color.FromArgb(255, 24, 26, 32));
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                // Checkerboard transparency backdrop
                using (var b1 = new SolidBrush(Color.FromArgb(255, 42, 45, 54)))
                using (var b2 = new SolidBrush(Color.FromArgb(255, 56, 60, 70)))
                {
                    int chk = Math.Max(6, 4 * zoom);
                    for (int py = topMargin; py < topMargin + scaledH; py += chk)
                    {
                        for (int px = leftMargin; px < leftMargin + scaledW; px += chk)
                        {
                            bool alt = (((px - leftMargin) / chk) + ((py - topMargin) / chk)) % 2 == 1;
                            g.FillRectangle(alt ? b2 : b1, px, py, Math.Min(chk, leftMargin + scaledW - px), Math.Min(chk, topMargin + scaledH - py));
                        }
                    }
                }

                g.DrawImage(srcBmp,
                    new Rectangle(leftMargin, topMargin, scaledW, scaledH),
                    new Rectangle(0, 0, srcBmp.Width, srcBmp.Height),
                    GraphicsUnit.Pixel);

                int cellPxW = cellW * zoom;
                int cellPxH = cellH * zoom;

                using var gridPen = new Pen(Color.FromArgb(140, 255, 255, 255), 1f);
                using var majorPen = new Pen(Color.FromArgb(210, 255, 210, 60), 1f);
                using var bboxPen = new Pen(Color.FromArgb(190, 0, 230, 255), 1f);
                using var centerLinePen = new Pen(Color.FromArgb(80, 255, 90, 90), 1f) { DashStyle = DashStyle.Dot };
                using var badgeBg = new SolidBrush(Color.FromArgb(195, 0, 0, 0));
                using var badgeFg = new SolidBrush(Color.FromArgb(255, 255, 245, 160));
                using var headerFg = new SolidBrush(Color.FromArgb(255, 195, 215, 255));
                using var font = new Font("Consolas", 7.5f, FontStyle.Bold, GraphicsUnit.Point);

                for (int c = 0; c <= cols; c++)
                {
                    int x = leftMargin + c * cellPxW;
                    g.DrawLine(c % 4 == 0 ? majorPen : gridPen, x, topMargin, x, topMargin + rows * cellPxH);
                    if (c < cols)
                        g.DrawString($"c{c}", font, headerFg, x + 3, 6);
                }

                for (int r = 0; r <= rows; r++)
                {
                    int y = topMargin + r * cellPxH;
                    g.DrawLine(r % 4 == 0 ? majorPen : gridPen, leftMargin, y, leftMargin + cols * cellPxW, y);
                    if (r < rows)
                    {
                        string rowText = preset == "npc"
                            ? (r < NpcRowLabels.Length ? $"r{r} {NpcRowLabels[r]}" : $"r{r} Anim")
                            : $"r{r}";
                        g.DrawString(rowText, font, headerFg, 3, y + 3);
                    }
                }

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int idx = r * cols + c;
                        if (!frameBoxes.TryGetValue(idx, out var relBox))
                            continue;

                        int cellX = leftMargin + c * cellPxW;
                        int cellY = topMargin + r * cellPxH;

                        if (preset == "npc" || preset == "craftable")
                        {
                            int midX = cellX + cellPxW / 2;
                            g.DrawLine(centerLinePen, midX, cellY + 2, midX, cellY + cellPxH - 2);
                        }

                        if (showBoundingBoxes)
                        {
                            g.DrawRectangle(
                                bboxPen,
                                cellX + relBox.X * zoom,
                                cellY + relBox.Y * zoom,
                                Math.Max(1, relBox.Width * zoom - 1),
                                Math.Max(1, relBox.Height * zoom - 1));
                        }

                        string badge = preset == "portrait" && idx < PortraitSlotLabels.Length
                            ? $"{idx}:{PortraitSlotLabels[idx]}"
                            : idx.ToString();
                        SizeF sz = g.MeasureString(badge, font);
                        g.FillRectangle(badgeBg, cellX + 1, cellY + 1, Math.Min(cellPxW - 2, sz.Width), Math.Min(cellPxH - 2, sz.Height - 1));
                        g.DrawString(badge, font, badgeFg, cellX + 1, cellY + 1);
                    }
                }
            }

            outBmp.Save(fullOut, ImageFormat.Png);
        }

        private static string DeterminePreset(
            string fullPath,
            string rawQuery,
            int imgW,
            int imgH,
            string? presetRaw,
            (int W, int H)? explicitCell,
            out int cellW,
            out int cellH)
        {
            if (explicitCell.HasValue && explicitCell.Value.W > 0 && explicitCell.Value.H > 0)
            {
                cellW = explicitCell.Value.W;
                cellH = explicitCell.Value.H;
                return string.IsNullOrWhiteSpace(presetRaw) ? "custom" : presetRaw!.ToLowerInvariant();
            }

            string p = (presetRaw ?? "auto").Trim().ToLowerInvariant();
            if (p == "auto")
            {
                string norm = (rawQuery + "|" + fullPath).Replace('\\', '/');
                if (norm.IndexOf("/Portraits/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    norm.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (imgW == 128 && imgH >= 64 && imgH % 64 == 0))
                {
                    p = "portrait";
                }
                else if (norm.IndexOf("/Characters/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (imgW == 64 && imgH >= 128 && imgH % 32 == 0))
                {
                    p = "npc";
                }
                else if (norm.IndexOf("Craftables", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    p = "craftable";
                }
                else
                {
                    p = "object";
                }
            }

            switch (p)
            {
                case "portrait":
                case "portraits":
                    cellW = 64;
                    cellH = 64;
                    return "portrait";
                case "npc":
                case "character":
                    cellW = 16;
                    cellH = 32;
                    return "npc";
                case "craftable":
                case "bigcraftable":
                    cellW = 16;
                    cellH = 32;
                    return "craftable";
                default:
                    cellW = 16;
                    cellH = 16;
                    return "object";
            }
        }

        private static string? ResolveSpritePngPath(string query, string gamePath)
        {
            if (File.Exists(query))
                return Path.GetFullPath(query);
            if (File.Exists(query + ".png"))
                return Path.GetFullPath(query + ".png");

            string unpackedRoot = Path.Combine(gamePath, "Content (unpacked)");
            if (Directory.Exists(unpackedRoot))
            {
                string clean = query.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string cand = Path.Combine(unpackedRoot, clean);
                if (File.Exists(cand))
                    return cand;
                if (File.Exists(cand + ".png"))
                    return cand + ".png";
            }

            return TmxMap.ResolveTilesheetPngPath(query, Directory.GetCurrentDirectory(), gamePath);
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            using var ms = new MemoryStream(raw);
            using var temp = new Bitmap(ms);
            return new Bitmap(temp);
        }

        private static bool TryComputeOpaqueBounds(Bitmap bmp, int startX, int startY, int w, int h, out Rectangle relBounds)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            int endX = Math.Min(bmp.Width, startX + w);
            int endY = Math.Min(bmp.Height, startY + h);

            for (int py = startY; py < endY; py++)
            {
                for (int px = startX; px < endX; px++)
                {
                    if (bmp.GetPixel(px, py).A > 4)
                    {
                        int rx = px - startX;
                        int ry = py - startY;
                        if (rx < minX) minX = rx;
                        if (ry < minY) minY = ry;
                        if (rx > maxX) maxX = rx;
                        if (ry > maxY) maxY = ry;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                relBounds = Rectangle.Empty;
                return false;
            }

            relBounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        /// <summary>
        /// Draws a visual UI component &amp; controller neighbor navigation overlay onto a captured screen PNG
        /// (`sdv-dev screenshot --mode screen --ui-overlay`).
        /// </summary>
        public static object DrawUiOverlayOnScreenshotPng(string pngPath, System.Text.Json.JsonElement uiLayout)
        {
            if (!File.Exists(pngPath) || uiLayout.ValueKind != System.Text.Json.JsonValueKind.Object)
                return new { drawn = false };

            if (!uiLayout.TryGetProperty("components", out var compsEl) || compsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                return new { drawn = false, reason = "No active menu components." };

            float scaleX = 1f, scaleY = 1f;
            using var baseBmp = LoadBitmapUnlocked(pngPath);
            if (uiLayout.TryGetProperty("uiViewport", out var vpEl) && vpEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (vpEl.TryGetProperty("width", out var vwEl) && vwEl.TryGetInt32(out int vw) && vw > 0 &&
                    vpEl.TryGetProperty("height", out var vhEl) && vhEl.TryGetInt32(out int vh) && vh > 0)
                {
                    scaleX = (float)baseBmp.Width / vw;
                    scaleY = (float)baseBmp.Height / vh;
                }
            }

            var items = new List<(int Id, string? Name, Rectangle Rect, int Up, int Down, int Left, int Right)>();
            var byId = new Dictionary<int, Rectangle>();

            foreach (var c in compsEl.EnumerateArray())
            {
                int id = c.TryGetProperty("myID", out var idEl) && idEl.TryGetInt32(out int parsedId) ? parsedId : -1;
                string? name = c.TryGetProperty("name", out var nEl) && nEl.ValueKind == System.Text.Json.JsonValueKind.String ? nEl.GetString() : null;
                int up = c.TryGetProperty("upNeighborID", out var uEl) && uEl.TryGetInt32(out int pu) ? pu : -1;
                int down = c.TryGetProperty("downNeighborID", out var dEl) && dEl.TryGetInt32(out int pd) ? pd : -1;
                int left = c.TryGetProperty("leftNeighborID", out var lEl) && lEl.TryGetInt32(out int pl) ? pl : -1;
                int right = c.TryGetProperty("rightNeighborID", out var rEl) && rEl.TryGetInt32(out int pr) ? pr : -1;

                if (!c.TryGetProperty("bounds", out var bEl) || bEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                    continue;
                int bx = bEl.TryGetProperty("x", out var xe) && xe.TryGetInt32(out int px) ? (int)Math.Round(px * scaleX) : 0;
                int by = bEl.TryGetProperty("y", out var ye) && ye.TryGetInt32(out int py) ? (int)Math.Round(py * scaleY) : 0;
                int bw = bEl.TryGetProperty("w", out var we) && we.TryGetInt32(out int pw) ? Math.Max(2, (int)Math.Round(pw * scaleX)) : 4;
                int bh = bEl.TryGetProperty("h", out var he) && he.TryGetInt32(out int ph) ? Math.Max(2, (int)Math.Round(ph * scaleY)) : 4;

                var rect = new Rectangle(bx, by, bw, bh);
                items.Add((id, name, rect, up, down, left, right));
                if (id >= 0 && !byId.ContainsKey(id))
                    byId[id] = rect;
            }

            int brokenLinks = 0;
            int validLinks = 0;

            using var outBmp = new Bitmap(baseBmp.Width, baseBmp.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outBmp))
            {
                g.DrawImage(baseBmp, 0, 0, baseBmp.Width, baseBmp.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using var boxFill = new SolidBrush(Color.FromArgb(36, 0, 225, 255));
                using var boxPen = new Pen(Color.FromArgb(230, 0, 230, 255), 2f);
                using var brokenBoxPen = new Pen(Color.FromArgb(245, 255, 65, 65), 2.5f);
                using var navPen = new Pen(Color.FromArgb(185, 90, 255, 130), 1.6f)
                {
                    CustomEndCap = new AdjustableArrowCap(4f, 4f, true)
                };
                using var badgeBg = new SolidBrush(Color.FromArgb(215, 12, 16, 24));
                using var brokenBadgeBg = new SolidBrush(Color.FromArgb(225, 175, 25, 25));
                using var textWhite = new SolidBrush(Color.White);
                using var textYellow = new SolidBrush(Color.FromArgb(255, 255, 225, 100));
                using var font = new Font("Consolas", 8.5f, FontStyle.Bold, GraphicsUnit.Point);

                // 1. Draw directional gamepad neighbor arrows first
                foreach (var it in items)
                {
                    Point srcCenter = new Point(it.Rect.Left + it.Rect.Width / 2, it.Rect.Top + it.Rect.Height / 2);
                    foreach (int nb in new[] { it.Up, it.Down, it.Left, it.Right })
                    {
                        if (nb < 0 || nb == -7777 || nb == -99998 || nb == -99999)
                            continue; // -1=none, -7777=custom snap, -99998/-99999=automatic snap
                        if (byId.TryGetValue(nb, out var dstRect))
                        {
                            Point dstCenter = new Point(dstRect.Left + dstRect.Width / 2, dstRect.Top + dstRect.Height / 2);
                            if (srcCenter != dstCenter)
                            {
                                g.DrawLine(navPen, srcCenter, dstCenter);
                                validLinks++;
                            }
                        }
                        else
                        {
                            brokenLinks++;
                        }
                    }
                }

                // 2. Draw component bounding boxes & ID badges
                foreach (var it in items)
                {
                    bool hasDangling = false;
                    foreach (int nb in new[] { it.Up, it.Down, it.Left, it.Right })
                    {
                        if (nb >= 0 && !byId.ContainsKey(nb))
                            hasDangling = true;
                    }

                    g.FillRectangle(boxFill, it.Rect);
                    g.DrawRectangle(hasDangling ? brokenBoxPen : boxPen, it.Rect);

                    string label = it.Id >= 0 ? $"#{it.Id}" : "(-)";
                    if (!string.IsNullOrWhiteSpace(it.Name))
                    {
                        string shortName = it.Name!.Length > 14 ? it.Name.Substring(0, 14) : it.Name;
                        label += $":{shortName}";
                    }
                    if (hasDangling)
                        label += " [!NAV]";

                    SizeF sz = g.MeasureString(label, font);
                    float lx = Math.Max(0, Math.Min(outBmp.Width - sz.Width - 2, it.Rect.Left));
                    float ly = it.Rect.Top >= sz.Height + 2 ? it.Rect.Top - sz.Height : it.Rect.Top + 1;
                    g.FillRectangle(hasDangling ? brokenBadgeBg : badgeBg, lx, ly, sz.Width + 2, sz.Height);
                    g.DrawString(label, font, it.Id >= 0 ? textYellow : textWhite, lx + 1, ly);
                }

                // 3. Top summary banner
                string menuType = uiLayout.TryGetProperty("menuType", out var mt) ? (mt.GetString() ?? "Menu") : "Menu";
                string summary = $"UI Overlay: {menuType} | Components={items.Count} | NavArrows={validLinks} | BrokenNeighborIDs={brokenLinks}";
                g.FillRectangle(badgeBg, 0, 0, outBmp.Width, 22);
                g.DrawString(summary, font, brokenLinks > 0 ? textYellow : textWhite, 6, 3);
            }

            outBmp.Save(pngPath, ImageFormat.Png);
            return new
            {
                drawn = true,
                componentCount = items.Count,
                navArrowCount = validLinks,
                brokenNeighborLinks = brokenLinks
            };
        }
    }
}
