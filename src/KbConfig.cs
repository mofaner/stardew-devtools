using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SdvKb
{
    public sealed class KbConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public string DbPath { get; set; } = string.Empty;

        public static string DataDir
        {
            get
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(home, ".local", "share", "sdv-dev");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string ConfigFilePath => Path.Combine(DataDir, "config.json");

        /// <summary>
        /// Portable co-located database right next to `sdv-dev-worker.exe` (for 3-file green portable distribution).
        /// </summary>
        public static string CoLocatedDbPath => Path.Combine(AppContext.BaseDirectory, "sdv-dev.db");

        public static string UserShareDbPath => Path.Combine(DataDir, "sdv-dev.db");

        public static string DefaultDbPath => File.Exists(CoLocatedDbPath) ? CoLocatedDbPath : UserShareDbPath;

        public static string InstalledLibDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "lib", "sdv-dev");

        public static KbConfig Load(string? overrideGamePath = null, string? overrideDbPath = null)
        {
            KbConfig cfg = new KbConfig();
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<KbConfig>(json);
                    if (loaded != null)
                        cfg = loaded;
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(overrideGamePath))
                cfg.GamePath = overrideGamePath!;

            if (string.IsNullOrWhiteSpace(cfg.GamePath) || !Directory.Exists(cfg.GamePath))
            {
                string? detected = AutoDetectGamePath();
                if (detected != null)
                    cfg.GamePath = detected;
            }

            if (!string.IsNullOrWhiteSpace(overrideDbPath))
            {
                cfg.DbPath = overrideDbPath!;
            }
            else if (File.Exists(CoLocatedDbPath))
            {
                // Prefer co-located portable sdv-dev.db out-of-the-box unless user configured a custom non-default DB path that exists
                bool hasCustomDb = !string.IsNullOrWhiteSpace(cfg.DbPath) &&
                                   !Path.GetFullPath(cfg.DbPath).Equals(Path.GetFullPath(UserShareDbPath), StringComparison.OrdinalIgnoreCase) &&
                                   File.Exists(cfg.DbPath);
                if (!hasCustomDb)
                    cfg.DbPath = CoLocatedDbPath;
            }
            else if (string.IsNullOrWhiteSpace(cfg.DbPath))
            {
                cfg.DbPath = UserShareDbPath;
            }

            return cfg;
        }

        public void Save(bool persistDbPath = false)
        {
            string dbToPersist = string.Empty;
            if (persistDbPath && !string.IsNullOrWhiteSpace(DbPath))
            {
                string full = Path.GetFullPath(DbPath);
                if (!full.Equals(Path.GetFullPath(CoLocatedDbPath), StringComparison.OrdinalIgnoreCase) &&
                    !full.Equals(Path.GetFullPath(UserShareDbPath), StringComparison.OrdinalIgnoreCase))
                {
                    dbToPersist = DbPath;
                }
            }
            else if (File.Exists(ConfigFilePath))
            {
                // Preserve any existing valid custom DB path, but never write transient `--db` or co-located paths!
                try
                {
                    var prev = JsonSerializer.Deserialize<KbConfig>(File.ReadAllText(ConfigFilePath));
                    if (prev != null && !string.IsNullOrWhiteSpace(prev.DbPath) && File.Exists(prev.DbPath))
                    {
                        string prevFull = Path.GetFullPath(prev.DbPath);
                        if (!prevFull.Equals(Path.GetFullPath(CoLocatedDbPath), StringComparison.OrdinalIgnoreCase) &&
                            !prevFull.Equals(Path.GetFullPath(UserShareDbPath), StringComparison.OrdinalIgnoreCase))
                        {
                            dbToPersist = prev.DbPath;
                        }
                    }
                }
                catch { }
            }

            var toWrite = new KbConfig
            {
                GamePath = GamePath,
                DbPath = dbToPersist
            };
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(toWrite, opts));
        }

        public static string? AutoDetectGamePath()
        {
            var candidates = new List<string>();

            // 1. Auto-detect via Windows Steam & GOG Registry + Steam `libraryfolders.vdf` (supports Chinese / custom library paths!)
            try
            {
                var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string? sp = hkcu?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrWhiteSpace(sp))
                        steamRoots.Add(sp!.Replace('/', '\\'));
                }
                using (var hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    string? ip = hklm?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(ip))
                        steamRoots.Add(ip!.Replace('/', '\\'));
                }

                foreach (var sr in steamRoots)
                {
                    candidates.Add(Path.Combine(sr, "steamapps", "common", "Stardew Valley"));
                    string vdfPath = Path.Combine(sr, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        string vdf = File.ReadAllText(vdfPath, Encoding.UTF8);
                        foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
                        {
                            string libDir = Regex.Unescape(m.Groups[1].Value).Replace('/', '\\');
                            if (!string.IsNullOrWhiteSpace(libDir))
                                candidates.Add(Path.Combine(libDir, "steamapps", "common", "Stardew Valley"));
                        }
                    }
                }

                using (var gog = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1453375253"))
                {
                    string? gp = gog?.GetValue("path") as string;
                    if (!string.IsNullOrWhiteSpace(gp))
                        candidates.Add(gp!);
                }
            }
            catch { }

            // 2. Fallback common drive locations
            candidates.Add(@"D:\steam\steamapps\common\Stardew Valley");
            candidates.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley");
            candidates.Add(@"C:\Program Files\Steam\steamapps\common\Stardew Valley");

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                    continue;
                string root = drive.RootDirectory.FullName;
                candidates.Add(Path.Combine(root, "SteamLibrary", "steamapps", "common", "Stardew Valley"));
                candidates.Add(Path.Combine(root, "steam", "steamapps", "common", "Stardew Valley"));
                candidates.Add(Path.Combine(root, "Games", "Stardew Valley"));
            }

            foreach (var path in candidates)
            {
                try
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "Stardew Valley.dll")))
                        return path;
                }
                catch { }
            }

            return null;
        }
    }
}
