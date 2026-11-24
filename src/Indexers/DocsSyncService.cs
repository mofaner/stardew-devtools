using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SdvKb.Database;

namespace SdvKb.Indexers
{
    public static class DocsSyncService
    {
        private static readonly (string Category, string Slug, string TitlePrefix, string Url)[] GithubMarkdownSources =
        {
            ("ContentPatcher", "cp-author-guide", "CP Author Guide", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide.md"),
            ("ContentPatcher", "cp-tokens", "CP Tokens", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/tokens.md"),
            ("ContentPatcher", "cp-action-load", "CP Action: Load", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/action-load.md"),
            ("ContentPatcher", "cp-action-editdata", "CP Action: EditData", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/action-editdata.md"),
            ("ContentPatcher", "cp-action-editimage", "CP Action: EditImage", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/action-editimage.md"),
            ("ContentPatcher", "cp-action-editmap", "CP Action: EditMap", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/action-editmap.md"),
            ("ContentPatcher", "cp-action-include", "CP Action: Include", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/action-include.md"),
            ("ContentPatcher", "cp-config-schema", "CP ConfigSchema", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/config.md"),
            ("ContentPatcher", "cp-translations", "CP Translations (i18n)", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/translations.md"),
            ("ContentPatcher", "cp-text-operations", "CP Text Operations", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/text-operations.md"),
            ("ContentPatcher", "cp-custom-locations", "CP Custom Locations", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/custom-locations.md"),
            ("ContentPatcher", "cp-trigger-actions", "CP Trigger Actions", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/trigger-actions.md"),
            ("ContentPatcher", "cp-troubleshooting", "CP Troubleshooting", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-guide/troubleshooting.md"),
            ("ContentPatcher", "cp-migration", "CP Migration Guide", "https://raw.githubusercontent.com/Pathoschild/StardewMods/develop/ContentPatcher/docs/author-migration-guide.md"),
            ("SMAPI", "smapi-technical", "SMAPI Technical Docs", "https://raw.githubusercontent.com/Pathoschild/SMAPI/develop/docs/technical/smapi.md"),
            ("SMAPI", "smapi-mod-package", "SMAPI Mod Package Guide", "https://raw.githubusercontent.com/Pathoschild/SMAPI/develop/docs/technical/mod-package.md")
        };

        private static readonly (string ApiBase, string SourceTag, string Slug, string PageTitle)[] MediaWikiPages =
            Array.Empty<(string, string, string, string)>();

        public static List<DocRecord> BuildOfflineCuratedDocs(string? customDocsDir = null, string? mirrorOutDir = null)
        {
            var docs = new List<DocRecord>();

            docs.Add(CreateBuiltinDoc(
                "Quickstart",
                "architecture-1.6",
                "Stardew Valley 1.6 Modding Architecture & Qualified Item IDs",
                "整理自官方 Wiki (https://stardewvalleywiki.com/Modding:Items) 与 Stardew Valley 1.6 ItemRegistry 规范",
                @"# Stardew Valley 1.6 Modding Core Concepts

## 1. Two Main Modding Approaches
- **Content Patcher (`Pathoschild.ContentPatcher`)**: Data-driven JSON packs (`manifest.json` + `content.json`). Used for adding/editing items, machines, crops, shops, NPCs, dialogues, events, maps, and sprites without C#.
- **SMAPI C# Mods (`StardewModdingAPI`)**: Compiled `.NET 6.0` class libraries inheriting `StardewModdingAPI.Mod` (`Entry(IModHelper helper)`). Used for custom game mechanics, custom UI (`IClickableMenu`), multiplayer sync, and Harmony runtime patches (`Lib.Harmony`).

## 2. Qualified Item IDs (`ItemRegistry` in 1.6)
Stardew Valley 1.6 unified item creation and lookup via `ItemRegistry` using type prefixes:
- `(O)` — Objects (`Data/Objects`), e.g. `(O)74` (Prismatic Shard), `(O)MyMod.CustomId_Item`
- `(BC)` — Big Craftables / Machines (`Data/BigCraftables`), e.g. `(BC)12` (Keg)
- `(W)` — Weapons (`Data/Weapons`), e.g. `(W)4` (Galaxy Sword)
- `(B)` — Boots (`Data/Boots`)
- `(H)` — Hats (`Data/Hats`)
- `(P)` — Pants (`Data/Pants`)
- `(S)` — Shirts (`Data/Shirts`)
- `(F)` — Furniture (`Data/Furniture`)
- `(T)` — Tools (`Data/Tools`)
- `(TR)` — Trinkets (`Data/Trinkets`)

C# usage: `Item item = ItemRegistry.Create(""(O)74"", amount: 1, quality: 0);`
Unique ID naming rule (1.6 mandatory best practice): Always prefix custom IDs in `Data/*` with `{{ModId}}_`, e.g. `""{{ModId}}_RubyApple""`!",
                mirrorOutDir));

            docs.Add(CreateBuiltinDoc(
                "SMAPI",
                "smapi-csharp-template",
                "SMAPI C# Mod `.csproj` & `ModEntry` Standard Template (1.6 / .NET 6)",
                "整理自 SMAPI 官方开发指南 (https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started)",
                @"# SMAPI C# Mod Standard Setup (Stardew Valley 1.6)

## `.csproj` Configuration (`ModBuildConfig`)
```xml
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <EnableHarmony>true</EnableHarmony> <!-- Only set true if using Harmony patches -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Pathoschild.Stardew.ModBuildConfig"" Version=""4.*"" />
  </ItemGroup>
</Project>
```

## `manifest.json` for C# Mod
```json
{
  ""Name"": ""My Mod"",
  ""Author"": ""YourName"",
  ""Version"": ""1.0.0"",
  ""Description"": ""Mod description."",
  ""UniqueID"": ""YourName.MyMod"",
  ""EntryDll"": ""MyMod.dll"",
  ""MinimumApiVersion"": ""4.0.0"",
  ""UpdateKeys"": []
}
```

## `ModEntry.cs` Skeleton
```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MyMod;

internal sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) { }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(""Data/Objects""))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, StardewValley.GameData.Objects.ObjectData>().Data;
                // Modify or add entries
            });
        }
    }
}
```",
                mirrorOutDir));

            docs.Add(CreateBuiltinDoc(
                "Harmony",
                "harmony-best-practices",
                "Harmony Patching Rules & Safe Patterns in Stardew Valley",
                "整理自官方 Wiki Harmony 指南 (https://stardewvalleywiki.com/Modding:Harmony)",
                @"# Harmony Patching Guide & Pitfalls in Stardew Valley

## Golden Rules
1. **Prefer SMAPI Events / Content API first**: Use `helper.Events.Content.AssetRequested` for data/textures and `helper.Events.GameLoop.*` / `Input.*` before resorting to Harmony.
2. **Always check exact method signature first**: Run `sdv-dev decompile <Type>.<Method>` to verify parameter types, `ref` arguments, static vs instance, and return type.
3. **Wrap patch logic in `try/catch`**: Unhandled exceptions inside a Harmony patch crash the game loop; log errors via `Monitor.Log($""Failed in {nameof(MyPatch)}:\n{ex}"", LogLevel.Error)`.
4. **Avoid `Prefix` returning `false` unless strictly necessary**: Skipping original game code breaks other mods' patches. Prefer `Postfix` (modifying `ref __result` or side effects) or `Transpiler`.

## Standard Harmony Pattern
```csharp
using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

public static class ObjectPatches
{
    private static IMonitor? Monitor;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        Monitor = monitor;
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.sellToStorePrice), new[] { typeof(long) }),
            postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(SellToStorePrice_Postfix))
        );
    }

    private static void SellToStorePrice_Postfix(StardewValley.Object __instance, long specificPlayerID, ref int __result)
    {
        try
        {
            // Modify __result safely
        }
        catch (Exception ex)
        {
            Monitor?.Log($""Error in {nameof(SellToStorePrice_Postfix)}:\n{ex}"", LogLevel.Error);
        }
    }
}
```",
                mirrorOutDir));

            docs.Add(CreateBuiltinDoc(
                "ContentPatcher",
                "content-patcher-cheatsheet",
                "Content Patcher 2.x Core Structure & Common Actions Cheat Sheet",
                "整理自 Content Patcher 官方作者文档 (https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md)",
                @"# Content Patcher (`content.json`) Quick Reference

## `manifest.json` for Content Patcher Pack
```json
{
  ""Name"": ""My Content Pack"",
  ""Author"": ""YourName"",
  ""Version"": ""1.0.0"",
  ""Description"": ""Adds custom items and machines."",
  ""UniqueID"": ""YourName.MyContentPack"",
  ""MinimumApiVersion"": ""4.0.0"",
  ""ContentPackFor"": {
    ""UniqueID"": ""Pathoschild.ContentPatcher""
  }
}
```

## Core Actions in `content.json` (`Format`: `2.3.0`+)
1. **`Load`**: Provide initial texture or JSON/TMX asset (often for `Mods/{{ModId}}/Assets/...`).
   ```json
   {
     ""Action"": ""Load"",
     ""Target"": ""Mods/{{ModId}}/Objects"",
     ""FromFile"": ""assets/objects.png""
   }
   ```
2. **`EditData`**: Add/modify entries, fields, or text operations in `Data/*` or `Strings/*`.
   ```json
   {
     ""Action"": ""EditData"",
     ""Target"": ""Data/Objects"",
     ""Entries"": {
       ""{{ModId}}_StarTea"": {
         ""Name"": ""{{ModId}}_StarTea"",
         ""DisplayName"": ""{{i18n:star-tea.name}}"",
         ""Description"": ""{{i18n:star-tea.desc}}"",
         ""Type"": ""Basic"",
         ""Category"": -26,
         ""Price"": 350,
         ""Texture"": ""Mods/{{ModId}}/Objects"",
         ""SpriteIndex"": 0,
         ""Edibility"": 60,
         ""IsDrink"": true
       }
     }
   }
   ```
3. **`EditImage`**: Overlay or replace a region of an existing sprite sheet (`PatchMode`: `Replace` or `Overlay`).
4. **`EditMap`**: Patch tiles, map properties, warps, or splice a `.tmx` sub-map onto an existing location.
5. **`Include`**: Split large `content.json` into modular sub-files (`""FromFile"": ""data/items.json""`).",
                mirrorOutDir));

            // Load any user custom markdown files (marking custom/wiki/ separately as `wiki-dump` so `sync` without local wiki files never wipes Wiki docs!)
            bool loadedLocalWikiFiles = false;
            if (!string.IsNullOrWhiteSpace(customDocsDir) && Directory.Exists(customDocsDir))
            {
                string fullCustomRoot = Path.GetFullPath(customDocsDir);
                foreach (var mdFile in Directory.GetFiles(fullCustomRoot, "*.md", SearchOption.AllDirectories))
                {
                    string content = File.ReadAllText(mdFile);
                    string rel = mdFile.Substring(fullCustomRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    bool isWiki = rel.StartsWith("wiki" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                  rel.StartsWith("wiki/", StringComparison.OrdinalIgnoreCase);

                    if (isWiki)
                        loadedLocalWikiFiles = true;

                    string docSource = isWiki ? "wiki-dump" : "local";
                    string category = isWiki ? "Wiki" : "CustomDoc";
                    string docTitle = Path.GetFileNameWithoutExtension(mdFile);
                    string sourceLabel = $"本地自定义文档 ({mdFile})";
                    string urlOrPath = mdFile;

                    var srcMatch = Regex.Match(content, @"^>\s*\*\*文档来源\*\*:\s*(.+)$", RegexOptions.Multiline);
                    if (srcMatch.Success)
                    {
                        sourceLabel = srcMatch.Groups[1].Value.Trim();
                        var urlMatch = Regex.Match(sourceLabel, @"https?://[^\s)]+");
                        if (urlMatch.Success)
                            urlOrPath = urlMatch.Value;
                    }

                    var h1Match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
                    if (h1Match.Success)
                    {
                        docTitle = h1Match.Groups[1].Value.Trim();
                    }

                    docs.AddRange(SplitMarkdownIntoSections(docSource, category, docTitle, urlOrPath, sourceLabel, content));
                }
            }

            // Fallback: if local `docs/custom/wiki/*.md` does not exist on an external user's machine,
            // restore all 106 Wiki docs directly from the embedded `WikiDocsBundle.zip` resource inside `sdv-dev-worker.exe`!
            if (!loadedLocalWikiFiles)
            {
                try
                {
                    using Stream? resStream = typeof(DocsSyncService).Assembly.GetManifestResourceStream("WikiDocsBundle.zip");
                    if (resStream != null)
                    {
                        using var zip = new ZipArchive(resStream, ZipArchiveMode.Read);
                        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
                        {
                            using var sr = new StreamReader(entry.Open(), Encoding.UTF8);
                            string content = sr.ReadToEnd();
                            string docTitle = Path.GetFileNameWithoutExtension(entry.Name);
                            string sourceLabel = $"Stardew Valley Official Wiki ({docTitle})";
                            string urlOrPath = $"https://stardewvalleywiki.com/{docTitle}";

                            var srcMatch = Regex.Match(content, @"^>\s*\*\*文档来源\*\*:\s*(.+)$", RegexOptions.Multiline);
                            if (srcMatch.Success)
                            {
                                sourceLabel = srcMatch.Groups[1].Value.Trim();
                                var urlMatch = Regex.Match(sourceLabel, @"https?://[^\s)]+");
                                if (urlMatch.Success)
                                    urlOrPath = urlMatch.Value;
                            }

                            var h1Match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
                            if (h1Match.Success)
                                docTitle = h1Match.Groups[1].Value.Trim();

                            docs.AddRange(SplitMarkdownIntoSections("wiki-dump", "Wiki", docTitle, urlOrPath, sourceLabel, content));
                        }
                    }
                }
                catch
                {
                }
            }

            return docs;
        }

        private static DocRecord CreateBuiltinDoc(
            string category,
            string slug,
            string title,
            string sourceDescription,
            string body,
            string? mirrorOutDir)
        {
            string fullContent = $"> **文档来源**: {sourceDescription}\n\n{body.Trim()}";
            SaveMirrorMarkdown(mirrorOutDir, "guides", slug, fullContent);
            return new DocRecord
            {
                Source = "builtin-guide",
                Category = category,
                Title = title,
                UrlOrPath = sourceDescription,
                Content = fullContent
            };
        }

        public static void MirrorEngineDocs(IEnumerable<DocRecord> engineDocs, string? mirrorOutDir)
        {
            if (string.IsNullOrWhiteSpace(mirrorOutDir))
                return;

            var byCat = engineDocs.GroupBy(d => d.Category);
            foreach (var g in byCat)
            {
                var sb = new StringBuilder();
                string firstSource = g.First().UrlOrPath;
                string dllHeader = firstSource.Equals("StardewModdingAPI.dll", StringComparison.OrdinalIgnoreCase)
                    ? "本地 SMAPI 官方程序集与注释直提 (`StardewModdingAPI.dll` + `StardewModdingAPI.xml`)"
                    : $"本地游戏本体程序集与官方注释直提 (`Stardew Valley.dll` + `Stardew Valley.xml` -> `{firstSource}`)";
                sb.AppendLine($"> **文档来源**: {dllHeader}");
                sb.AppendLine($"> **同步时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                foreach (var d in g)
                {
                    // Strip per-chunk header when combining into a single mirror file
                    string body = Regex.Replace(d.Content, @"^>\s*\*\*文档来源\*\*:[^\n]*\n+", "");
                    sb.AppendLine(body.Trim());
                    sb.AppendLine();
                }

                SaveMirrorMarkdown(mirrorOutDir, "dll-extracted", g.Key.ToLowerInvariant(), sb.ToString());
            }
        }

        public static async Task<(List<DocRecord> Docs, int OkCount, int SkipCount)> FetchRemoteDocsAsync(string? mirrorOutDir = null)
        {
            var docs = new List<DocRecord>();
            int ok = 0;
            int skipped = 0;

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("stardew-devtools/1.0 (Local Modding Knowledge Indexer)");

            // 1. Fetch GitHub Raw Markdown docs (Content Patcher & GMCM)
            foreach (var item in GithubMarkdownSources)
            {
                try
                {
                    using var resp = await client.GetAsync(item.Url).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        skipped++;
                        continue;
                    }
                    string md = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(md))
                    {
                        string sourceLabel = $"GitHub 官方仓库文档 ({item.Url})";
                        string fullMirror = $"> **文档来源**: {sourceLabel}\n> **分类**: {item.Category}\n\n{md.Trim()}";
                        SaveMirrorMarkdown(mirrorOutDir, "github", item.Slug, fullMirror);

                        docs.AddRange(SplitMarkdownIntoSections("github", item.Category, item.TitlePrefix, item.Url, sourceLabel, md));
                        ok++;
                    }
                }
                catch
                {
                    skipped++;
                }
            }

            // 2. Fetch Stardew Valley Wiki Modding:* pages via official MediaWiki api.php
            foreach (var wp in MediaWikiPages)
            {
                try
                {
                    string reqUrl = $"{wp.ApiBase}?action=parse&page={Uri.EscapeDataString(wp.PageTitle)}&prop=wikitext&format=json";
                    using var resp = await client.GetAsync(reqUrl).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        skipped++;
                        continue;
                    }

                    string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        // Gracefully skip if non-JSON / anti-bot challenge page
                        skipped++;
                        continue;
                    }

                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("parse", out var parseEl) &&
                        parseEl.TryGetProperty("wikitext", out var wtEl) &&
                        wtEl.TryGetProperty("*", out var starEl))
                    {
                        string wikitext = starEl.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(wikitext))
                        {
                            string cleanedMd = ConvertWikitextToCleanMarkdown(wikitext);
                            string webUrl = wp.ApiBase.Replace("/mediawiki/api.php", "/") + wp.PageTitle;
                            string wikiLang = wp.SourceTag == "wiki-zh" ? "星露谷物语官方中文 Wiki" : "Stardew Valley Official Wiki";
                            string sourceLabel = $"{wikiLang} ({webUrl})";

                            string fullMirror = $"> **文档来源**: {sourceLabel}\n> **页面标题**: {wp.PageTitle}\n\n# {wp.PageTitle}\n\n{cleanedMd}";
                            SaveMirrorMarkdown(mirrorOutDir, wp.SourceTag, wp.Slug, fullMirror);

                            docs.AddRange(SplitMarkdownIntoSections(wp.SourceTag, "Wiki", wp.PageTitle, webUrl, sourceLabel, cleanedMd));
                            ok++;
                        }
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch
                {
                    skipped++;
                }
            }

            return (docs, ok, skipped);
        }

        private static void SaveMirrorMarkdown(string? mirrorOutDir, string subFolder, string slug, string content)
        {
            if (string.IsNullOrWhiteSpace(mirrorOutDir))
                return;
            try
            {
                string dir = Path.Combine(mirrorOutDir!, subFolder);
                Directory.CreateDirectory(dir);
                string safeSlug = Regex.Replace(slug, @"[^a-zA-Z0-9._\-]+", "-");
                File.WriteAllText(Path.Combine(dir, safeSlug + ".md"), content, new UTF8Encoding(false));
            }
            catch
            {
                // Ignore mirror write failure
            }
        }

        public static List<DocRecord> SplitMarkdownIntoSections(
            string source,
            string category,
            string docTitle,
            string urlOrPath,
            string sourceHeaderText,
            string markdown)
        {
            var result = new List<DocRecord>();
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string currentHeading = "Overview";
            var sb = new StringBuilder();

            void Flush()
            {
                string body = sb.ToString().Trim();
                if (body.Length >= 40)
                {
                    string prefix = body.StartsWith("> **文档来源**:", StringComparison.Ordinal)
                        ? string.Empty
                        : $"> **文档来源**: {sourceHeaderText}\n\n";

                    if (body.Length <= 6500)
                    {
                        result.Add(new DocRecord
                        {
                            Source = source,
                            Category = category,
                            Title = $"{docTitle} — {currentHeading}",
                            UrlOrPath = urlOrPath,
                            Content = prefix + body
                        });
                    }
                    else
                    {
                        // Split cleanly on newline / subheading boundaries (`### ` / `#### ` / `\n`) instead of mid-word!
                        var bodyLines = body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        var chunkSb = new StringBuilder();
                        int part = 1;
                        string subLabel = currentHeading;

                        void FlushChunk()
                        {
                            string chunkText = chunkSb.ToString().Trim();
                            if (chunkText.Length > 0)
                            {
                                string chunkPrefix = chunkText.StartsWith("> **文档来源**:", StringComparison.Ordinal)
                                    ? string.Empty
                                    : $"> **文档来源**: {sourceHeaderText}\n\n";
                                result.Add(new DocRecord
                                {
                                    Source = source,
                                    Category = category,
                                    Title = $"{docTitle} — {subLabel} (Part {part})",
                                    UrlOrPath = urlOrPath,
                                    Content = chunkPrefix + chunkText
                                });
                                part++;
                            }
                            chunkSb.Clear();
                        }

                        foreach (var bl in bodyLines)
                        {
                            bool isSubHeading = bl.StartsWith("### ", StringComparison.Ordinal) || bl.StartsWith("#### ", StringComparison.Ordinal);
                            if (chunkSb.Length >= 3500 && (isSubHeading || chunkSb.Length + bl.Length > 5800))
                            {
                                FlushChunk();
                            }
                            if (bl.StartsWith("### ", StringComparison.Ordinal) && chunkSb.Length < 200)
                            {
                                subLabel = $"{currentHeading} > {bl.Substring(4).Trim()}";
                            }
                            chunkSb.AppendLine(bl);
                        }
                        FlushChunk();
                    }
                }
                sb.Clear();
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    Flush();
                    currentHeading = line.Substring(3).Trim().Trim('#').Trim();
                    sb.AppendLine(line);
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            Flush();

            return result;
        }

        private static string ConvertWikitextToCleanMarkdown(string wikitext)
        {
            string s = wikitext;
            s = Regex.Replace(s, @"<(?:syntaxhighlight|source)(?:\s+lang=""?([a-zA-Z0-9+#]+)""?)?[^>]*>", m =>
                $"```{(m.Groups[1].Success ? m.Groups[1].Value : "")}\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</(?:syntaxhighlight|source)>", "\n```", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<code>(.*?)</code>", "`$1`", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            s = Regex.Replace(s, @"^====\s*(.+?)\s*====\s*$", "#### $1", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^===\s*(.+?)\s*===\s*$", "### $1", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^==\s*(.+?)\s*==\s*$", "## $1", RegexOptions.Multiline);

            s = Regex.Replace(s, @"\[\[([^|\]]+)\|([^\]]+)\]\]", "$2");
            s = Regex.Replace(s, @"\[\[([^\]]+)\]\]", "$1");

            s = s.Replace("'''", "**").Replace("''", "*");

            return s.Trim();
        }
    }
}
