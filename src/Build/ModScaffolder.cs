using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SdvKb.Build
{
    public static class ModScaffolder
    {
        public static string CreateModProject(string templateType, string modName, string author, string? outputParentDir)
        {
            string safeIdPart = Regex.Replace(modName, @"[^a-zA-Z0-9_]", "");
            if (string.IsNullOrEmpty(safeIdPart))
                safeIdPart = "MyStardewMod";
            string safeAuthor = Regex.Replace(author, @"[^a-zA-Z0-9_]", "");
            if (string.IsNullOrEmpty(safeAuthor))
                safeAuthor = "ModAuthor";

            string uniqueId = $"{safeAuthor}.{safeIdPart}";
            string baseDir = string.IsNullOrWhiteSpace(outputParentDir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(outputParentDir!);
            string projectDir = Path.Combine(baseDir, safeIdPart);

            if (Directory.Exists(projectDir) && Directory.GetFileSystemEntries(projectDir).Length > 0)
                throw new InvalidOperationException($"Target directory `{projectDir}` already exists and is not empty.");

            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(Path.Combine(projectDir, "i18n"));

            bool isCp = templateType.Equals("cp", StringComparison.OrdinalIgnoreCase) ||
                        templateType.Equals("content-patcher", StringComparison.OrdinalIgnoreCase);

            var utf8 = new UTF8Encoding(false);

            if (isCp)
            {
                Directory.CreateDirectory(Path.Combine(projectDir, "assets"));
                File.WriteAllText(Path.Combine(projectDir, "manifest.json"), $@"{{
  ""Name"": ""{modName}"",
  ""Author"": ""{author}"",
  ""Version"": ""1.0.0"",
  ""Description"": ""{modName} (Content Patcher Pack)"",
  ""UniqueID"": ""{uniqueId}"",
  ""MinimumApiVersion"": ""4.0.0"",
  ""UpdateKeys"": [],
  ""ContentPackFor"": {{
    ""UniqueID"": ""Pathoschild.ContentPatcher""
  }}
}}
", utf8);

                File.WriteAllText(Path.Combine(projectDir, "content.json"), @"{
  ""Format"": ""2.3.0"",
  ""Changes"": [
    // Add Load, EditData, EditImage, EditMap, or Include patches here
  ]
}
", utf8);

                File.WriteAllText(Path.Combine(projectDir, "i18n", "default.json"), "{\n}\n", utf8);
                File.WriteAllText(Path.Combine(projectDir, "i18n", "zh.json"), "{\n}\n", utf8);
            }
            else
            {
                File.WriteAllText(Path.Combine(projectDir, "manifest.json"), $@"{{
  ""Name"": ""{modName}"",
  ""Author"": ""{author}"",
  ""Version"": ""1.0.0"",
  ""Description"": ""{modName} (SMAPI C# Mod)"",
  ""UniqueID"": ""{uniqueId}"",
  ""EntryDll"": ""{safeIdPart}.dll"",
  ""MinimumApiVersion"": ""4.0.0"",
  ""UpdateKeys"": []
}}
", utf8);

                File.WriteAllText(Path.Combine(projectDir, $"{safeIdPart}.csproj"), $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <EnableHarmony>true</EnableHarmony>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Pathoschild.Stardew.ModBuildConfig"" Version=""4.*"" />
    <PackageReference Include=""Krafs.Publicizer"" Version=""2.*"" PrivateAssets=""all"" />
  </ItemGroup>
  <ItemGroup>
    <Publicize Include=""Stardew Valley;StardewValley.GameData;xTile"" />
  </ItemGroup>
</Project>
", utf8);

                File.WriteAllText(Path.Combine(projectDir, "ModConfig.cs"), $@"using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace {safeIdPart}
{{
    public sealed class ModConfig
    {{
        /// <summary>启用模组核心功能</summary>
        public bool Enabled {{ get; set; }} = true;

        /// <summary>打开功能的快捷键</summary>
        public KeybindList ToggleKey {{ get; set; }} = KeybindList.Parse(""F6"");
    }}
}}
", utf8);

                File.WriteAllText(Path.Combine(projectDir, "ModEntry.cs"), $@"using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace {safeIdPart}
{{
    public sealed class ModEntry : Mod
    {{
        public ModConfig Config {{ get; private set; }} = new();

        public override void Entry(IModHelper helper)
        {{
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        }}

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {{
            this.Monitor.Log(""{modName} initialized."", LogLevel.Info);
        }}

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {{
            if (!Context.IsWorldReady)
                return;
        }}
    }}
}}
", utf8);

                File.WriteAllText(Path.Combine(projectDir, "i18n", "default.json"), "{\n}\n", utf8);
                File.WriteAllText(Path.Combine(projectDir, "i18n", "zh.json"), "{\n}\n", utf8);
            }

            return projectDir;
        }
    }
}
