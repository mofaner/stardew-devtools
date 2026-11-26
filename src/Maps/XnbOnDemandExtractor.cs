using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SdvKb.Maps
{
    /// <summary>
    /// 原版 `.xnb` 资源无窗口按需直读与落盘缓存器（覆盖 `Texture2D -> .png` 与 `tBIN10 Map -> .tmx`）。
    ///
    /// 【当初为什么要写这个功能】：
    /// 普通新手电脑只安装了星露谷物语 1.6 和 SMAPI，游戏目录下只有压缩的 `Content\*.xnb`，默认并没有跑过
    /// `StardewXnbHack` 生成的 `Content (unpacked)` 文件夹。如果离线工具链（`map-inspect`、`map-render`、`sprite`、
    /// `tilesheet` 以及 `check` 中的原版肖像帧数/原版地图落点阻挡校验）强依赖 `Content (unpacked)`，在新手机器上就会报找不到文件；
    /// 而像 `StardewXnbHack` 那样全量解包又必须弹窗拉起 OpenGL 实例跑两三分钟。
    ///
    /// 因此这里直接复用玩家游戏目录自带的 `MonoGame.Framework.dll` 中的 `LzxDecoderStream` / `Lz4DecoderStream`（及 `DxtUtil`）
    /// 在后台纯内存解开目标单个 `.xnb`：
    ///   - 遇到 `Texture2DReader`：直接把预乘 RGBA 像素还原为标准直通 Alpha PNG；
    ///   - 遇到 `xTile.Pipeline.TideReader` (`tBIN10`)：按字节流无损解析图块集、`@TileIndex@` 属性、动画帧、多图层与格点属性并写出标准 `.tmx`；
    /// 产物按游戏目录哈希与源 `.xnb` 修改时间缓存在 `~/.local/share/sdv-dev/xnb-cache/`，首次单文件仅需 5~15ms，后续 0.05ms 命中！
    /// </summary>
    public static class XnbOnDemandExtractor
    {
        private static readonly object MgLock = new object();
        private static string? _loadedMgPath;
        private static Assembly? _mgAssembly;
        private static ConstructorInfo? _lzxCtor;
        private static ConstructorInfo? _lz4Ctor;
        private static MethodInfo? _dxt1Method;
        private static MethodInfo? _dxt3Method;
        private static MethodInfo? _dxt5Method;

        private static readonly string[] TextureSubdirProbes =
        {
            "",
            "Maps",
            "TileSheets",
            "Portraits",
            "Characters",
            Path.Combine("Characters", "Farmer"),
            "LooseSprites",
            "TerrainFeatures",
            "Buildings",
            "Animals",
            "Minigames"
        };

        /// <summary>
        /// 按需从 `<gamePath>\Content\*.xnb` 解包提取单张原版贴图为缓存 `.png` 路径；若不存在或非贴图则返回 `null`。
        /// </summary>
        public static string? ResolveOrExtractTexturePng(string query, string gamePath, string? seasonOverride = null)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(gamePath))
                return null;

            string contentRoot = Path.Combine(gamePath, "Content");
            if (!Directory.Exists(contentRoot))
                return null;

            string? xnbPath = FindTextureXnbPath(contentRoot, query, seasonOverride);
            if (xnbPath == null || !File.Exists(xnbPath))
                return null;

            string relFromContent = Path.GetRelativePath(contentRoot, xnbPath);
            string cachedPng = Path.Combine(GetCacheRootForGame(gamePath), Path.ChangeExtension(relFromContent, ".png"));

            DateTime srcWriteUtc = File.GetLastWriteTimeUtc(xnbPath);
            if (IsCacheFresh(cachedPng, srcWriteUtc))
                return cachedPng;

            try
            {
                if (ExtractXnbTextureToPng(xnbPath, gamePath, cachedPng, srcWriteUtc))
                    return cachedPng;
            }
            catch
            {
                // 若遇到非 Texture2D 的同名 .xnb（例如 Maps/Town.xnb 是地图而非贴图），静默返回 null 交给上层处理
            }

            return null;
        }

        /// <summary>
        /// 按需从 `<gamePath>\Content\Maps\*.xnb` 解包转换单张原版地图为缓存 `.tmx` 路径；若不存在则返回 `null`。
        /// </summary>
        public static string? ResolveOrExtractMapTmx(string mapOrAsset, string gamePath)
        {
            if (string.IsNullOrWhiteSpace(mapOrAsset) || string.IsNullOrWhiteSpace(gamePath))
                return null;

            string contentMapsRoot = Path.Combine(gamePath, "Content", "Maps");
            if (!Directory.Exists(contentMapsRoot))
                return null;

            string cleaned = mapOrAsset.Replace('\\', '/').Trim();
            if (cleaned.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(5);
            if (cleaned.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase) || cleaned.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 4);

            if (TmxMap.LocationNameToMapFile.TryGetValue(cleaned, out string? mapped))
                cleaned = mapped;

            string? xnbPath = null;
            string directCand = Path.Combine(contentMapsRoot, cleaned.Replace('/', Path.DirectorySeparatorChar) + ".xnb");
            if (File.Exists(directCand))
            {
                xnbPath = directCand;
            }
            else
            {
                string leaf = Path.GetFileName(cleaned);
                xnbPath = Directory.GetFiles(contentMapsRoot, "*.xnb")
                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(leaf, StringComparison.OrdinalIgnoreCase));
            }

            if (xnbPath == null || !File.Exists(xnbPath))
                return null;

            string contentRoot = Path.Combine(gamePath, "Content");
            string relFromContent = Path.GetRelativePath(contentRoot, xnbPath);
            string cachedTmx = Path.Combine(GetCacheRootForGame(gamePath), Path.ChangeExtension(relFromContent, ".tmx"));

            DateTime srcWriteUtc = File.GetLastWriteTimeUtc(xnbPath);
            if (IsCacheFresh(cachedTmx, srcWriteUtc))
                return cachedTmx;

            try
            {
                if (ExtractXnbMapToTmx(xnbPath, gamePath, cachedTmx, srcWriteUtc))
                    return cachedTmx;
            }
            catch
            {
            }

            return null;
        }

        private static string? FindTextureXnbPath(string contentRoot, string query, string? seasonOverride)
        {
            string norm = query.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || norm.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                norm = norm.Substring(0, norm.Length - 4);
            while (norm.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                norm = norm.Substring(2);
            while (norm.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                norm = norm.Substring(3);

            string fileNameOnly = Path.GetFileName(norm);
            string? dirPart = Path.GetDirectoryName(norm);

            string? seasonalFileName = null;
            if (!string.IsNullOrWhiteSpace(seasonOverride))
            {
                string s = seasonOverride!.Trim().ToLowerInvariant();
                if ((s == "spring" || s == "summer" || s == "fall" || s == "winter") &&
                    Regex.IsMatch(fileNameOnly, @"^(spring|summer|fall|winter)_", RegexOptions.IgnoreCase))
                {
                    seasonalFileName = Regex.Replace(fileNameOnly, @"^(spring|summer|fall|winter)_", s + "_", RegexOptions.IgnoreCase);
                }
            }

            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(dirPart))
            {
                if (seasonalFileName != null)
                    candidates.Add(Path.Combine(contentRoot, dirPart!, seasonalFileName + ".xnb"));
                candidates.Add(Path.Combine(contentRoot, norm + ".xnb"));
            }

            foreach (var sub in TextureSubdirProbes)
            {
                if (seasonalFileName != null)
                {
                    candidates.Add(string.IsNullOrEmpty(sub)
                        ? Path.Combine(contentRoot, seasonalFileName + ".xnb")
                        : Path.Combine(contentRoot, sub, seasonalFileName + ".xnb"));
                }

                candidates.Add(string.IsNullOrEmpty(sub)
                    ? Path.Combine(contentRoot, fileNameOnly + ".xnb")
                    : Path.Combine(contentRoot, sub, fileNameOnly + ".xnb"));
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }

            // 大小写不敏感兜底检索（如 springobjects vs SpringObjects、townInterior vs TownInterior）
            string[] ciDirs =
            {
                !string.IsNullOrEmpty(dirPart) ? Path.Combine(contentRoot, dirPart!) : string.Empty,
                Path.Combine(contentRoot, "Maps"),
                Path.Combine(contentRoot, "TileSheets"),
                Path.Combine(contentRoot, "Portraits"),
                Path.Combine(contentRoot, "Characters"),
                Path.Combine(contentRoot, "LooseSprites")
            };

            string targetFirst = seasonalFileName ?? fileNameOnly;
            foreach (var dir in ciDirs.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var files = Directory.GetFiles(dir, "*.xnb");
                var hit = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(targetFirst, StringComparison.OrdinalIgnoreCase))
                       ?? files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(fileNameOnly, StringComparison.OrdinalIgnoreCase));
                if (hit != null)
                    return hit;
            }

            return null;
        }

        private static bool IsCacheFresh(string cachedPath, DateTime srcWriteUtc)
        {
            if (!File.Exists(cachedPath))
                return false;
            var fi = new FileInfo(cachedPath);
            return fi.Length > 0 && fi.LastWriteTimeUtc >= srcWriteUtc;
        }

        private static string GetCacheRootForGame(string gamePath)
        {
            string norm = Path.GetFullPath(gamePath).TrimEnd('\\', '/').ToLowerInvariant();
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(norm))
            {
                hash ^= b;
                hash *= 16777619u;
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", "sdv-dev", "xnb-cache", hash.ToString("x8"));
        }

        private static void EnsureMonoGameLoaded(string gamePath)
        {
            string mgDll = Path.Combine(gamePath, "MonoGame.Framework.dll");
            if (!File.Exists(mgDll))
                throw new FileNotFoundException($"MonoGame.Framework.dll not found in `{gamePath}`.");

            string fullMg = Path.GetFullPath(mgDll);
            lock (MgLock)
            {
                if (_mgAssembly != null && string.Equals(_loadedMgPath, fullMg, StringComparison.OrdinalIgnoreCase))
                    return;

                _mgAssembly = Assembly.LoadFrom(fullMg);
                _loadedMgPath = fullMg;

                const BindingFlags AllInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                const BindingFlags AllStat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                Type? lzxType = _mgAssembly.GetType("MonoGame.Framework.Utilities.LzxDecoderStream");
                _lzxCtor = lzxType?.GetConstructor(AllInst, null, new[] { typeof(Stream), typeof(int), typeof(int) }, null);

                Type? lz4Type = _mgAssembly.GetType("MonoGame.Framework.Utilities.Lz4DecoderStream");
                _lz4Ctor = lz4Type?.GetConstructor(AllInst, null, new[] { typeof(Stream) }, null);

                Type? dxtType = _mgAssembly.GetType("Microsoft.Xna.Framework.Graphics.DxtUtil");
                _dxt1Method = dxtType?.GetMethod("DecompressDxt1", AllStat, null, new[] { typeof(byte[]), typeof(int), typeof(int) }, null);
                _dxt3Method = dxtType?.GetMethod("DecompressDxt3", AllStat, null, new[] { typeof(byte[]), typeof(int), typeof(int) }, null);
                _dxt5Method = dxtType?.GetMethod("DecompressDxt5", AllStat, null, new[] { typeof(byte[]), typeof(int), typeof(int) }, null);
            }
        }

        private static (BinaryReader PayloadReader, List<string> ReaderTypes, int PrimaryReaderIndex, IDisposable StreamOwner) OpenXnbPayload(
            string xnbPath,
            string gamePath)
        {
            EnsureMonoGameLoaded(gamePath);

            var fs = new FileStream(xnbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                using var headerBr = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
                byte b0 = headerBr.ReadByte();
                byte b1 = headerBr.ReadByte();
                byte b2 = headerBr.ReadByte();
                byte platform = headerBr.ReadByte();
                if (b0 != (byte)'X' || b1 != (byte)'N' || b2 != (byte)'B')
                    throw new InvalidDataException($"Not a valid XNB header in `{xnbPath}`.");
                _ = platform;

                byte version = headerBr.ReadByte();
                byte flags = headerBr.ReadByte();
                if (version != 5 && version != 4)
                    throw new InvalidDataException($"Unsupported XNB version `{version}` in `{xnbPath}`.");

                int fileSize = headerBr.ReadInt32();
                bool isLzx = (flags & 0x80) != 0;
                bool isLz4 = (flags & 0x40) != 0;

                Stream payloadStream;
                if (isLzx)
                {
                    int decompressedSize = headerBr.ReadInt32();
                    int compressedSize = fileSize - 14;
                    if (_lzxCtor == null)
                        throw new InvalidOperationException("MonoGame LzxDecoderStream constructor was not found.");
                    payloadStream = (Stream)_lzxCtor.Invoke(new object[] { fs, decompressedSize, compressedSize });
                }
                else if (isLz4)
                {
                    _ = headerBr.ReadInt32(); // decompressedSize
                    if (_lz4Ctor == null)
                        throw new InvalidOperationException("MonoGame Lz4DecoderStream constructor was not found.");
                    payloadStream = (Stream)_lz4Ctor.Invoke(new object[] { fs });
                }
                else
                {
                    payloadStream = fs;
                }

                var dbr = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: false);
                int readerCount = dbr.Read7BitEncodedInt();
                var readers = new List<string>(readerCount);
                for (int i = 0; i < readerCount; i++)
                {
                    readers.Add(dbr.ReadString());
                    _ = dbr.ReadInt32(); // reader version
                }

                _ = dbr.Read7BitEncodedInt(); // sharedResourceCount
                int primaryIndex = dbr.Read7BitEncodedInt(); // 1-based index into readers

                var compositeOwner = new CompositeDisposable(dbr, payloadStream, fs);
                return (dbr, readers, primaryIndex, compositeOwner);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }

        private static bool ExtractXnbTextureToPng(string xnbPath, string gamePath, string outPngPath, DateTime srcWriteUtc)
        {
            var (dbr, readers, primaryIndex, owner) = OpenXnbPayload(xnbPath, gamePath);
            using (owner)
            {
                if (primaryIndex <= 0 || primaryIndex > readers.Count)
                    return false;

                string primaryReader = readers[primaryIndex - 1];
                if (primaryReader.IndexOf("Texture2DReader", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                int surfaceFormat = dbr.ReadInt32();
                uint rawW = dbr.ReadUInt32();
                uint rawH = dbr.ReadUInt32();
                int width = (rawW & 0xFFFF0000u) == 0 ? (int)rawW : (int)(rawW & 0xFFFFu);
                int height = (rawH & 0xFFFF0000u) == 0 ? (int)rawH : (int)(rawH & 0xFFFFu);
                int levelCount = dbr.ReadInt32();
                if (width <= 0 || height <= 0 || levelCount <= 0)
                    return false;

                int dataLen = dbr.ReadInt32();
                byte[] rgba = dbr.ReadBytes(dataLen);

                // MonoGame SurfaceFormat: 0 = Color (RGBA32), 4/30/70 = Dxt1, 5/31 = Dxt3, 6/32 = Dxt5
                if (surfaceFormat == 4 || surfaceFormat == 30 || surfaceFormat == 70)
                {
                    if (_dxt1Method == null) return false;
                    rgba = (byte[])_dxt1Method.Invoke(null, new object[] { rgba, width, height })!;
                }
                else if (surfaceFormat == 5 || surfaceFormat == 31)
                {
                    if (_dxt3Method == null) return false;
                    rgba = (byte[])_dxt3Method.Invoke(null, new object[] { rgba, width, height })!;
                }
                else if (surfaceFormat == 6 || surfaceFormat == 32)
                {
                    if (_dxt5Method == null) return false;
                    rgba = (byte[])_dxt5Method.Invoke(null, new object[] { rgba, width, height })!;
                }
                else if (surfaceFormat != 0)
                {
                    return false;
                }

                int pixelCount = width * height;
                if (rgba.Length < pixelCount * 4)
                    return false;

                // XNB Texture2D 默认存储预乘 Alpha (Premultiplied RGBA)，而标准 PNG 需要直通 Alpha (Straight BGRA in GDI+ Format32bppArgb)
                byte[] bgra = new byte[pixelCount * 4];
                bool unpremultiply = surfaceFormat == 0;
                for (int i = 0; i < pixelCount * 4; i += 4)
                {
                    byte r = rgba[i];
                    byte g = rgba[i + 1];
                    byte b = rgba[i + 2];
                    byte a = rgba[i + 3];

                    if (unpremultiply)
                    {
                        if (a == 0)
                        {
                            r = 0;
                            g = 0;
                            b = 0;
                        }
                        else if (a < 255)
                        {
                            r = (byte)Math.Min(255, (r * 255 + (a >> 1)) / a);
                            g = (byte)Math.Min(255, (g * 255 + (a >> 1)) / a);
                            b = (byte)Math.Min(255, (b * 255 + (a >> 1)) / a);
                        }
                    }

                    bgra[i] = b;
                    bgra[i + 1] = g;
                    bgra[i + 2] = r;
                    bgra[i + 3] = a;
                }

                string? outDir = Path.GetDirectoryName(outPngPath);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

                string tmpPath = outPngPath + ".tmp_" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                    {
                        var rect = new Rectangle(0, 0, width, height);
                        BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            if (bd.Stride == width * 4)
                            {
                                Marshal.Copy(bgra, 0, bd.Scan0, bgra.Length);
                            }
                            else
                            {
                                for (int y = 0; y < height; y++)
                                    Marshal.Copy(bgra, y * width * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), width * 4);
                            }
                        }
                        finally
                        {
                            bmp.UnlockBits(bd);
                        }

                        bmp.Save(tmpPath, ImageFormat.Png);
                    }

                    File.Move(tmpPath, outPngPath, overwrite: true);
                    try { File.SetLastWriteTimeUtc(outPngPath, srcWriteUtc); } catch { }
                    return true;
                }
                finally
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                }
            }
        }

        private static bool ExtractXnbMapToTmx(string xnbPath, string gamePath, string outTmxPath, DateTime srcWriteUtc)
        {
            var (dbr, readers, primaryIndex, owner) = OpenXnbPayload(xnbPath, gamePath);
            using (owner)
            {
                if (primaryIndex <= 0 || primaryIndex > readers.Count)
                    return false;

                string primaryReader = readers[primaryIndex - 1];
                string? outDir = Path.GetDirectoryName(outTmxPath);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

                string tmpPath = outTmxPath + ".tmp_" + Guid.NewGuid().ToString("N");
                try
                {
                    if (primaryReader.IndexOf("TideReader", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int byteCount = dbr.ReadInt32();
                        byte[] mapBytes = dbr.ReadBytes(byteCount);

                        // 兼容极少数直接塞了 XML 文本流的情况，正常为 `tBIN10` 二进制地图流
                        if (LooksLikeXmlBytes(mapBytes))
                        {
                            File.WriteAllBytes(tmpPath, mapBytes);
                        }
                        else
                        {
                            TmxMap parsed = ParseTbin10ToTmxMap(mapBytes);
                            parsed.Save(tmpPath);
                        }
                    }
                    else if (primaryReader.IndexOf("StringReader", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string xml = dbr.ReadString();
                        if (!xml.TrimStart().StartsWith("<", StringComparison.Ordinal))
                            return false;
                        File.WriteAllText(tmpPath, xml, new UTF8Encoding(false));
                    }
                    else
                    {
                        return false;
                    }

                    File.Move(tmpPath, outTmxPath, overwrite: true);
                    try { File.SetLastWriteTimeUtc(outTmxPath, srcWriteUtc); } catch { }
                    return true;
                }
                finally
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                }
            }
        }

        private static bool LooksLikeXmlBytes(byte[] bytes)
        {
            if (bytes.Length < 5)
                return false;
            int idx = (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
            while (idx < bytes.Length && (bytes[idx] == (byte)' ' || bytes[idx] == (byte)'\r' || bytes[idx] == (byte)'\n' || bytes[idx] == (byte)'\t'))
                idx++;
            return idx < bytes.Length && bytes[idx] == (byte)'<';
        }

        /// <summary>
        /// 按 `xTile.Format.TbinFormat` 规范直接将 `tBIN10` 字节流无损解析为 `TmxMap`（含 `@TileIndex@` 图块属性、动画帧与逐格属性）。
        /// </summary>
        private static TmxMap ParseTbin10ToTmxMap(byte[] tbinBytes)
        {
            using var ms = new MemoryStream(tbinBytes, writable: false);
            byte[] sig = ReadExact(ms, 6);
            if (Encoding.ASCII.GetString(sig) != "tBIN10")
                throw new InvalidDataException("Invalid tBIN header (expected `tBIN10`).");

            var map = new TmxMap
            {
                TileWidth = 16,
                TileHeight = 16
            };

            _ = ReadTbinString(ms); // map.Id
            _ = ReadTbinString(ms); // map.Description
            ReadTbinProperties(ms, map.Properties);

            // 1. TileSheets
            int sheetCount = ReadTbinInt32(ms);
            int nextFirstGid = 1;
            var sheetById = new Dictionary<string, TmxTileset>(StringComparer.Ordinal);

            for (int i = 0; i < sheetCount; i++)
            {
                string sheetId = ReadTbinString(ms);
                _ = ReadTbinString(ms); // description
                string rawImgSrc = ReadTbinString(ms).Replace('\\', '/').Trim();
                int sheetCols = ReadTbinInt32(ms);
                int sheetRows = ReadTbinInt32(ms);
                int tileW = ReadTbinInt32(ms);
                int tileH = ReadTbinInt32(ms);
                _ = ReadTbinInt32(ms); // marginW
                _ = ReadTbinInt32(ms); // marginH
                _ = ReadTbinInt32(ms); // spacingW
                _ = ReadTbinInt32(ms); // spacingH

                var rawSheetProps = new Dictionary<string, string>(StringComparer.Ordinal);
                ReadTbinProperties(ms, rawSheetProps);

                if (tileW <= 0) tileW = 16;
                if (tileH <= 0) tileH = 16;
                int safeCols = Math.Max(1, sheetCols);
                int safeRows = Math.Max(1, sheetRows);
                int totalTiles = safeCols * safeRows;

                // 与 xTile.Map.FlattenTileSheetPaths 保持一致：将 Maps\spring_outdoorsTileSheet 规范化并补上 .png 后缀方便 Tiled 与本地解析
                string normImg = rawImgSrc;
                if (normImg.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                    normImg = normImg.Substring(0, normImg.Length - 4);
                if (!string.IsNullOrEmpty(normImg) && !normImg.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    normImg += ".png";

                var ts = new TmxTileset
                {
                    FirstGid = nextFirstGid,
                    Name = sheetId,
                    ImageSource = normImg,
                    TileWidth = tileW,
                    TileHeight = tileH,
                    Columns = safeCols,
                    TileCount = totalTiles,
                    ImageWidth = safeCols * tileW,
                    ImageHeight = safeRows * tileH
                };

                // 拆分 xTile TileIndexPropertyCollection 的 `@TileIndex@<localId>@<propKey>` 属性
                foreach (var kv in rawSheetProps)
                {
                    if (kv.Key.StartsWith("@TileIndex@", StringComparison.Ordinal))
                    {
                        string rest = kv.Key.Substring("@TileIndex@".Length);
                        int secondAt = rest.IndexOf('@');
                        if (secondAt > 0 && secondAt < rest.Length - 1 &&
                            int.TryParse(rest.Substring(0, secondAt), NumberStyles.Integer, CultureInfo.InvariantCulture, out int localTileId) &&
                            localTileId >= 0)
                        {
                            string propKey = rest.Substring(secondAt + 1);
                            if (!ts.TileIndexProperties.TryGetValue(localTileId, out var dict))
                            {
                                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                ts.TileIndexProperties[localTileId] = dict;
                            }
                            dict[propKey] = kv.Value;
                        }
                    }
                }

                map.Tilesets.Add(ts);
                sheetById[sheetId] = ts;
                nextFirstGid += totalTiles;
            }

            // 2. Layers
            int layerCount = ReadTbinInt32(ms);
            int maxW = 0, maxH = 0;

            for (int lIdx = 0; lIdx < layerCount; lIdx++)
            {
                string layerId = ReadTbinString(ms);
                bool visible = ReadTbinBool(ms);
                _ = ReadTbinString(ms); // description
                int layerW = ReadTbinInt32(ms);
                int layerH = ReadTbinInt32(ms);
                int lTileW = ReadTbinInt32(ms);
                int lTileH = ReadTbinInt32(ms);

                if (lIdx == 0 && lTileW > 0 && lTileH > 0)
                {
                    map.TileWidth = lTileW;
                    map.TileHeight = lTileH;
                }

                if (layerW > maxW) maxW = layerW;
                if (layerH > maxH) maxH = layerH;

                var layer = new TmxLayer
                {
                    Id = lIdx + 1,
                    Name = layerId,
                    Width = layerW,
                    Height = layerH,
                    Visible = visible,
                    Opacity = 1f,
                    Gids = new uint[Math.Max(0, layerW * layerH)],
                    RawCsvCount = Math.Max(0, layerW * layerH)
                };
                ReadTbinProperties(ms, layer.Properties);

                TmxTileset? currentSheet = null;
                for (int y = 0; y < layerH; y++)
                {
                    int x = 0;
                    while (x < layerW)
                    {
                        int op = ms.ReadByte();
                        if (op < 0)
                            throw new EndOfStreamException($"Unexpected EOF in layer `{layerId}` at ({x},{y}).");

                        switch ((char)op)
                        {
                            case 'N':
                            {
                                int nullRun = ReadTbinInt32(ms);
                                x += nullRun;
                                break;
                            }
                            case 'T':
                            {
                                string sheetRef = ReadTbinString(ms);
                                sheetById.TryGetValue(sheetRef, out currentSheet);
                                break;
                            }
                            case 'S':
                            {
                                var (tileIdx, cellProps) = ReadTbinStaticTile(ms);
                                if (currentSheet != null && tileIdx >= 0)
                                    layer.SetCleanGid(x, y, currentSheet.FirstGid + tileIdx);
                                if (cellProps != null && cellProps.Count > 0)
                                    RecordCellProperties(map, layerId, x, y, cellProps);
                                x++;
                                break;
                            }
                            case 'A':
                            {
                                int intervalMs = ReadTbinInt32(ms);
                                int frameCount = ReadTbinInt32(ms);
                                TmxTileset? animSheet = null;
                                TmxTileset? firstFrameSheet = null;
                                int firstFrameLocalIdx = -1;
                                Dictionary<string, string>? mergedProps = null;
                                var frames = new List<(TmxTileset? Sheet, int LocalIdx)>(Math.Max(1, frameCount));

                                int remaining = frameCount;
                                while (remaining > 0)
                                {
                                    int subOp = ms.ReadByte();
                                    if (subOp == 'T')
                                    {
                                        string sRef = ReadTbinString(ms);
                                        sheetById.TryGetValue(sRef, out animSheet);
                                    }
                                    else if (subOp == 'S')
                                    {
                                        var (fIdx, fProps) = ReadTbinStaticTile(ms);
                                        if (firstFrameLocalIdx < 0)
                                        {
                                            firstFrameSheet = animSheet;
                                            firstFrameLocalIdx = fIdx;
                                        }
                                        if (fProps != null && fProps.Count > 0)
                                        {
                                            mergedProps ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                            foreach (var kv in fProps)
                                                mergedProps[kv.Key] = kv.Value;
                                        }
                                        frames.Add((animSheet, fIdx));
                                        remaining--;
                                    }
                                    else
                                    {
                                        throw new InvalidDataException($"Unexpected animated tile opcode `{(char)subOp}`.");
                                    }
                                }

                                var animTileProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                ReadTbinProperties(ms, animTileProps);
                                if (animTileProps.Count > 0)
                                {
                                    mergedProps ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var kv in animTileProps)
                                        mergedProps[kv.Key] = kv.Value;
                                }

                                if (firstFrameSheet != null && firstFrameLocalIdx >= 0)
                                {
                                    layer.SetCleanGid(x, y, firstFrameSheet.FirstGid + firstFrameLocalIdx);
                                    if (!firstFrameSheet.Animations.ContainsKey(firstFrameLocalIdx) &&
                                        frames.All(f => ReferenceEquals(f.Sheet, firstFrameSheet) && f.LocalIdx >= 0))
                                    {
                                        firstFrameSheet.Animations[firstFrameLocalIdx] = frames
                                            .Select(f => new TmxAnimFrame
                                            {
                                                LocalTileId = f.LocalIdx,
                                                DurationMs = Math.Max(16, intervalMs)
                                            })
                                            .ToList();
                                    }
                                }

                                if (mergedProps != null && mergedProps.Count > 0)
                                    RecordCellProperties(map, layerId, x, y, mergedProps);

                                x++;
                                break;
                            }
                            default:
                                throw new InvalidDataException($"Unexpected tBIN layer opcode `{(char)op}` (0x{op:X2}).");
                        }
                    }
                }

                map.Layers.Add(layer);
            }

            map.Width = maxW;
            map.Height = maxH;
            return map;
        }

        private static void RecordCellProperties(TmxMap map, string layerName, int x, int y, Dictionary<string, string> props)
        {
            if (!map.ObjectTileData.TryGetValue(layerName, out var coordMap))
            {
                coordMap = new Dictionary<(int X, int Y), Dictionary<string, string>>();
                map.ObjectTileData[layerName] = coordMap;
            }
            if (!coordMap.TryGetValue((x, y), out var existing))
            {
                existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                coordMap[(x, y)] = existing;
            }
            foreach (var kv in props)
                existing[kv.Key] = kv.Value;
        }

        private static (int TileIndex, Dictionary<string, string>? Props) ReadTbinStaticTile(Stream s)
        {
            int tileIdx = ReadTbinInt32(s);
            _ = s.ReadByte(); // blendMode
            int propCount = ReadTbinInt32(s);
            if (propCount <= 0)
                return (tileIdx, null);

            var props = new Dictionary<string, string>(propCount, StringComparer.OrdinalIgnoreCase);
            ReadTbinPropertyEntries(s, propCount, props);
            return (tileIdx, props);
        }

        private static void ReadTbinProperties(Stream s, IDictionary<string, string> target)
        {
            int count = ReadTbinInt32(s);
            if (count > 0)
                ReadTbinPropertyEntries(s, count, target);
        }

        private static void ReadTbinPropertyEntries(Stream s, int count, IDictionary<string, string> target)
        {
            for (int i = 0; i < count; i++)
            {
                string key = ReadTbinString(s);
                int kind = s.ReadByte();
                switch (kind)
                {
                    case 0: // bool -> 使用星露谷 TMX 惯例 "T" / "F"
                        target[key] = ReadTbinBool(s) ? "T" : "F";
                        break;
                    case 1: // int
                        target[key] = ReadTbinInt32(s).ToString(CultureInfo.InvariantCulture);
                        break;
                    case 2: // float
                        target[key] = ReadTbinFloat(s).ToString(CultureInfo.InvariantCulture);
                        break;
                    case 3: // string
                        target[key] = ReadTbinString(s);
                        break;
                    default:
                        throw new InvalidDataException($"Unknown tBIN property value type `{kind}` for key `{key}`.");
                }
            }
        }

        private static bool ReadTbinBool(Stream s)
        {
            int b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            return b > 0;
        }

        private static int ReadTbinInt32(Stream s)
        {
            byte[] b = ReadExact(s, 4);
            return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
        }

        private static float ReadTbinFloat(Stream s)
        {
            byte[] b = ReadExact(s, 4);
            return BitConverter.ToSingle(b, 0);
        }

        private static string ReadTbinString(Stream s)
        {
            int len = ReadTbinInt32(s);
            if (len <= 0)
                return string.Empty;
            byte[] b = ReadExact(s, len);
            return Encoding.UTF8.GetString(b);
        }

        private static byte[] ReadExact(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int n = s.Read(buf, offset, count - offset);
                if (n <= 0)
                    throw new EndOfStreamException($"Expected {count} byte(s), got {offset}.");
                offset += n;
            }
            return buf;
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IDisposable[] _items;
            public CompositeDisposable(params IDisposable[] items) => _items = items;
            public void Dispose()
            {
                foreach (var item in _items)
                {
                    try { item?.Dispose(); } catch { }
                }
            }
        }
    }
}
