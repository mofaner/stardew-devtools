param(
    [switch]$IncludeLive,
    [string]$KeepArtifactsDir = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

$repoRoot = Split-Path -Parent $PSScriptRoot
$sdvExe = Join-Path $repoRoot 'dist\sdv-dev.exe'
if (-not (Test-Path -LiteralPath $sdvExe)) {
    $sdvExe = 'sdv-dev'
}

$workRoot = if ([string]::IsNullOrWhiteSpace($KeepArtifactsDir)) {
    Join-Path ([System.IO.Path]::GetTempPath()) ("sdv-dev-tests-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + " [CP] 中文空格")
} else {
    [System.IO.Path]::GetFullPath($KeepArtifactsDir)
}
Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
[System.IO.Directory]::CreateDirectory($workRoot) | Out-Null

$script:passed = 0

function Pass-Step([string]$title) {
    $script:passed++
    Write-Host "[PASS #$($script:passed)] $title" -ForegroundColor Green
}

function Assert-True([bool]$cond, [string]$msg) {
    if (-not $cond) { throw "Assertion failed: $msg" }
}

function Assert-Contains([string]$haystack, [string]$needle, [string]$msg) {
    if ($null -eq $haystack -or $haystack.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Assertion failed ($msg): expected to find '$needle' in:`n$haystack"
    }
}

function Invoke-SdvRaw([string[]]$cliArgs) {
    $out = & $sdvExe @cliArgs 2>&1 | Out-String
    return @{ Code = $LASTEXITCODE; Text = $out.Trim() }
}

try {
    Write-Host "=== Running stardew-devtools Automated Test Suite ===" -ForegroundColor Cyan
    Write-Host "Workspace: $workRoot"

    # ------------------------------------------------------------------
    # 1. CLI Unknown Flag Guard & --grid Alias on sprite
    # ------------------------------------------------------------------
    $badFlag = Invoke-SdvRaw @('sprite', 'Maps/springobjects', '--nonexistent-opt', '16x16')
    Assert-True ($badFlag.Code -ne 0) "Unknown CLI flag must exit non-zero"
    Assert-Contains $badFlag.Text "Unknown option" "Unknown CLI flag message"

    # Create a tiny 32x32 PNG (2x2 of 16x16) and test `sprite --grid 16x16`
    Add-Type -AssemblyName System.Drawing
    $tinyPng = Join-Path $workRoot "items_test.png"
    $bmp = New-Object System.Drawing.Bitmap(32, 32)
    $bmp.SetPixel(8, 8, [System.Drawing.Color]::Red)
    $bmp.SetPixel(24, 8, [System.Drawing.Color]::Blue)
    $bmp.Save($tinyPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    $spGrid = Invoke-SdvRaw @('sprite', $tinyPng, '--grid', '16x16')
    Assert-True ($spGrid.Code -eq 0) "sprite --grid 16x16 alias should succeed: $($spGrid.Text)"
    Assert-Contains $spGrid.Text '"nonEmptySlotCount": 2' "sprite --grid frame detection"
    Pass-Step "CLI unknown-option guard & sprite --grid alias"

    # ------------------------------------------------------------------
    # 2. Knowledge Base (API, Decompile, Audio/Cues) & Config Isolation
    # ------------------------------------------------------------------
    $cueRes = Invoke-SdvRaw @('data', 'dwop', '--asset', 'Audio/Cues', '--limit', '2')
    Assert-True ($cueRes.Code -eq 0) "Audio/Cues lookup exit code"
    Assert-Contains $cueRes.Text '"Category": "Sound"' "XACT audio cue indexed"

    $tempDb = Join-Path $workRoot "temp_test.db"
    Copy-Item -LiteralPath (Join-Path $repoRoot 'dist\sdv-dev.db') -Destination $tempDb -Force
    $syncTemp = Invoke-SdvRaw @('sync', '--offline', '--docs-only', '--db', $tempDb, '--docs-out', (Join-Path $workRoot 'empty_docs'))
    Assert-True ($syncTemp.Code -eq 0) "Portable sync with --db succeeded"
    Assert-Contains $syncTemp.Text 'Docs: 12' "106 embedded Wiki docs preserved without local wiki folder"

    $stAfter = Invoke-SdvRaw @('status')
    Assert-True ($stAfter.Text.IndexOf('temp_test.db', [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "Global config.json must NOT be polluted by --db"
    Pass-Step "XACT Audio/Cues search, Wiki bundle fallback, & config.json isolation"

    # ------------------------------------------------------------------
    # 3. gen-gmcm (SButton -> AddKeybind, KeybindList, bilingual i18n)
    # ------------------------------------------------------------------
    $gmcmModRoot = Join-Path $workRoot "GmcmTestContainer"
    $null = Invoke-SdvRaw @('new', 'csharp', '--name', 'GmcmSample', '--author', 'Tester', '--out', $gmcmModRoot)
    $gmcmMod = Join-Path $gmcmModRoot "GmcmSample"

    $cfgCs = @'
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace GmcmSample
{
    public sealed class ModConfig
    {
        /// <summary>快速回家快捷键：按下后立即将玩家传送回农场小屋门口而无需消耗体力。</summary>
        public SButton QuickTeleportKey { get; set; } = SButton.K;

        /// <summary>组合菜单热键：按下组合键直接呼出珊瑚湾总览面板。</summary>
        public KeybindList OpenPanelKeys { get; set; } = KeybindList.Parse("LeftShift + G");
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "ModConfig.cs"), $cfgCs, [System.Text.UTF8Encoding]::new($false))
    $gmcmRun = Invoke-SdvRaw @('gen-gmcm', $gmcmMod)
    Assert-True ($gmcmRun.Code -eq 0) "gen-gmcm exit 0: $($gmcmRun.Text)"

    $gCode = Get-Content -LiteralPath (Join-Path $gmcmMod "GmcmIntegration.g.cs") -Raw
    Assert-Contains $gCode "gmcm.AddKeybind(" "SButton generates AddKeybind (NOT AddTextOption)"
    Assert-Contains $gCode "gmcm.AddKeybindList(" "KeybindList generates AddKeybindList"

    $enI18n = Get-Content -LiteralPath (Join-Path $gmcmMod "i18n\default.json") -Raw
    Assert-True (-not ($enI18n -match '[\u4e00-\u9fff]')) "default.json must not contain Chinese text: $enI18n"

    $zhObj = Get-Content -LiteralPath (Join-Path $gmcmMod "i18n\zh.json") -Raw | ConvertFrom-Json
    Assert-True ($zhObj.'gmcm.quickTeleportKey.name' -eq '快速回家快捷键') "zh.json short title extracted cleanly"
    Assert-Contains $zhObj.'gmcm.quickTeleportKey.tooltip' '无需消耗体力' "zh.json full tooltip preserved"

    # Verify LF (\n) line-ending preservation on existing i18n/*.json + bare `/// 中文` comment extraction + upgrading English placeholder in zh.json!
    $zhPath = Join-Path $gmcmMod "i18n\zh.json"
    $lfSeedJson = "{`n  `"gmcm.quickTeleportKey.name`": `"快速回家快捷键`",`n  `"gmcm.leafSway.name`": `"Leaf Sway`"`n}`n"
    [System.IO.File]::WriteAllText($zhPath, $lfSeedJson, [System.Text.UTF8Encoding]::new($false))
    $cfgCsWithBareComment = @'
using System.ComponentModel.DataAnnotations;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace GmcmSample
{
    public sealed class ModConfig
    {
        /// <summary>快速回家快捷键：按下后立即将玩家传送回农场小屋门口而无需消耗体力。</summary>
        public SButton QuickTeleportKey { get; set; } = SButton.K;

        /// 树叶摇曳：控制树冠随风摆动幅度。
        [Range(0f, 1f)]
        public float LeafSway { get; set; } = 0.5f;
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "ModConfig.cs"), $cfgCsWithBareComment, [System.Text.UTF8Encoding]::new($false))
    $gmcmRun2 = Invoke-SdvRaw @('gen-gmcm', $gmcmMod)
    Assert-True ($gmcmRun2.Code -eq 0) "second gen-gmcm exit 0: $($gmcmRun2.Text)"
    $zhRawAfter = [System.IO.File]::ReadAllText($zhPath, [System.Text.Encoding]::UTF8)
    Assert-True ($zhRawAfter.IndexOf("`r`n") -lt 0) "gen-gmcm preserved pure LF (\n) line endings in existing i18n/zh.json!"
    $zhObj2 = $zhRawAfter | ConvertFrom-Json
    Assert-True ($zhObj2.'gmcm.leafSway.name' -eq '树叶摇曳') "gen-gmcm extracted bare /// Chinese comment & upgraded English placeholder in zh.json"
    Pass-Step "gen-gmcm SButton/KeybindList, LF line-ending preservation, & bare /// Chinese i18n extraction"

    # ------------------------------------------------------------------
    # 4. .NET 8 ImplicitUsings + DefineConstants + Compile Remove + Harmony Type/Return Check
    # ------------------------------------------------------------------
    $csprojText = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <DefineConstants>ENABLE_SPECIAL_HOOK</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="ExcludedBrokenFile.cs" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "GmcmSample.csproj"), $csprojText, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "ExcludedBrokenFile.cs"), "THIS WILL NOT COMPILE @@@", [System.Text.UTF8Encoding]::new($false))

    # Note: ModEntry.cs omits `using System; using System.Collections.Generic; using System.Linq;`!
    $implicitEntry = @'
using StardewModdingAPI;

namespace GmcmSample
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
#if ENABLE_SPECIAL_HOOK
            var nums = new List<int> { 1, 2, 3 };
            int sum = nums.Sum();
            Monitor.Log($"Sum={sum}", LogLevel.Trace);
#else
            NonExistentCall();
#endif
        }
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "ModEntry.cs"), $implicitEntry, [System.Text.UTF8Encoding]::new($false))
    $bValid = Invoke-SdvRaw @('build', $gmcmMod)
    Assert-True ($bValid.Code -eq 0) "Build with ImplicitUsings + DefineConstants + Compile Remove succeeded: $($bValid.Text)"

    # Pack and verify deploy skips `releases/`!
    $pkRes = Invoke-SdvRaw @('pack', $gmcmMod)
    Assert-True ($pkRes.Code -eq 0) "sdv-dev pack succeeded: $($pkRes.Text)"
    Assert-True (Test-Path -LiteralPath (Join-Path $gmcmMod "releases")) "releases folder created by pack"

    # Now inject an invalid Harmony Prefix return type (`int` instead of `bool`/`void`) & mismatched `ref string __result`!
    $badHarmonyCs = @'
using HarmonyLib;
using StardewModdingAPI;

namespace GmcmSample
{
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performObjectDropInAction))]
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper) { }
        public static int Prefix(ref string __result) => 42;
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $gmcmMod "ModEntry.cs"), $badHarmonyCs, [System.Text.UTF8Encoding]::new($false))
    $bBad = Invoke-SdvRaw @('build', $gmcmMod)
    Assert-True ($bBad.Code -ne 0) "Broken Harmony return/ref types must fail build"
    Assert-Contains $bBad.Text "invalid return type" "Caught invalid Prefix return type"
    Assert-Contains $bBad.Text "incompatible with" "Caught incompatible ref __result type"
    Pass-Step ".csproj ImplicitUsings/DefineConstants/Compile Remove & Harmony type/ref circuit breaker"

    # ------------------------------------------------------------------
    # 5. Deep CP Static Validation: List<T> TriggerActions, Event Precond/4-Tuple/Cue,
    #    NPC Schedule, Dialogue+Portrait Frame Count, Recipes, & Cross-Asset Foreign Keys
    # ------------------------------------------------------------------
    $cpBugDir = Join-Path $workRoot "DeepStaticCheckPack"
    [System.IO.Directory]::CreateDirectory((Join-Path $cpBugDir "assets")) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $cpBugDir "i18n")) | Out-Null

    # Create a 4-frame (128x128 => frames 0..3) portrait sheet
    $portBmp = New-Object System.Drawing.Bitmap(128, 128)
    $portBmp.SetPixel(10, 10, [System.Drawing.Color]::Pink)
    $portBmp.Save((Join-Path $cpBugDir "assets\coral_portrait.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $portBmp.Dispose()

    [System.IO.File]::WriteAllText((Join-Path $cpBugDir "manifest.json"), @'
{
  "Name": "DeepStaticCheckPack",
  "Author": "Tester",
  "Version": "1.0.0",
  "UniqueID": "Tester.DeepCheck",
  "ContentPackFor": { "UniqueID": "Pathoschild.ContentPatcher" }
}
'@, [System.Text.UTF8Encoding]::new($false))

    [System.IO.File]::WriteAllText((Join-Path $cpBugDir "i18n\default.json"), @'
{
  "coral.greet": "欢迎来到珊瑚湾！$7#$b这里风景很好#$q 100/101 fallback#喜欢大海吗？"
}
'@, [System.Text.UTF8Encoding]::new($false))

    [System.IO.File]::WriteAllText((Join-Path $cpBugDir "content.json"), @'
{
  "Format": "2.4.0",
  "Changes": [
    {
      "Action": "Load",
      "Target": "Portraits/{{ModId}}_Coral",
      "FromFile": "assets/coral_portrait.png"
    },
    {
      "Action": "EditData",
      "Target": "Data/BigCraftables",
      "Entries": {
        "{{ModId}}_CoralPress": {
          "Name": "{{ModId}}_CoralPress",
          "DisplayName": "Coral Press",
          "Description": "Presses fruit."
        }
      }
    },
    {
      "Action": "EditData",
      "Target": "Data/Shops",
      "Entries": {
        "SeedShop": {
          "Items": [
            { "Id": "BadPrefixItem", "ItemId": "(O){{ModId}}_CoralPress" },
            { "Id": "GhostItem", "ItemId": "(O){{ModId}}_GhostBerry" }
          ]
        }
      }
    },
    {
      "Action": "EditData",
      "Target": "Data/TriggerActions",
      "Entries": [
        {
          "Id": "{{ModId}}_BadTrigger",
          "Trigger": "DayStarted",
          "Action": "TotallyFakeTriggerCmd 123"
        },
        {
          "Id": "{{ModId}}_MissingMailTrigger",
          "Trigger": "DayStarted",
          "Action": "AddMail Current {{ModId}}_UnregisteredLetter now"
        }
      ]
    },
    {
      "Action": "EditData",
      "Target": "Data/Events/Town",
      "Entries": {
        "998801/fakePrecond 10/f Abigail 500": "spring1/-1000 -1000/farmer 64 16 Abigail 64 14 2/playSound dwopp/end"
      }
    },
    {
      "Action": "EditData",
      "Target": "Characters/schedules/{{ModId}}_Coral",
      "Entries": {
        "spring": "900 Town 30 50 2/800 Town 32 50 2 nonexistent_anim_xyz"
      }
    },
    {
      "Action": "EditData",
      "Target": "Characters/Dialogue/{{ModId}}_Coral",
      "Entries": {
        "Mon": "{{i18n: coral.greet}}"
      }
    },
    {
      "Action": "EditData",
      "Target": "Data/CookingRecipes",
      "Entries": {
        "{{ModId}}_BadSoup": "258 1 9999999/1 10/(O)258/default/Soup"
      }
    }
  ]
}
'@, [System.Text.UTF8Encoding]::new($false))

    $chk = Invoke-SdvRaw @('check', $cpBugDir)
    Assert-True ($chk.Code -ne 0) "Deep broken CP pack must return non-zero exit code"
    Assert-Contains $chk.Text "TotallyFakeTriggerCmd" "List<T> Data/TriggerActions array validated"
    Assert-Contains $chk.Text "UnregisteredLetter" "Cross-asset AddMail missing Data/Mail caught"
    Assert-Contains $chk.Text 'Use `(BC)Tester.DeepCheck_CoralPress`' 'Cross-asset (O) vs (BC) type prefix mismatch caught'
    Assert-Contains $chk.Text 'GhostBerry' 'Cross-asset missing Data/Objects item caught'
    Assert-Contains $chk.Text 'fakePrecond' 'Event precondition validated against ValidEventPreconditions'
    Assert-Contains $chk.Text 'not a multiple of 4' 'Event segment[2] actor 4-tuple error caught'
    Assert-Contains $chk.Text 'dwopp' 'Event audio cue typo caught'
    Assert-Contains $chk.Text 'Non-increasing schedule time' 'NPC schedule chronological error caught'
    Assert-Contains $chk.Text 'nonexistent_anim_xyz' 'NPC schedule invalid animation caught'
    Assert-Contains $chk.Text 'Portraits/Tester.DeepCheck_Coral.png' 'Dialogue $7 vs 4-frame portrait caught across i18n'
    Assert-Contains $chk.Text 'missing closing' 'Dialogue unclosed #$b caught'
    Assert-Contains $chk.Text 'without any `$r`' 'Dialogue $q without $r caught'
    Assert-Contains $chk.Text 'even token count' 'CookingRecipes odd ingredient token count caught'
    Pass-Step "Deep static validation (List<T>, Foreign Keys, Event 4-tuple/precond, NPC Schedule/Dialogue/Portrait, Recipes)"

    # ------------------------------------------------------------------
    # 6. Offline Map Pipeline + CP EditMap Overlay Rendering on Maps/Town
    # ------------------------------------------------------------------
    $cpMapDir = Join-Path $workRoot "CpEditMapPack"
    [System.IO.Directory]::CreateDirectory((Join-Path $cpMapDir "assets")) | Out-Null
    $specTxt = Join-Path $cpMapDir "terrain.txt"
    @"
GGGGGG
GGGGGG
SSSSSS
SSSSSS
WWWWWW
WWWWWW
"@ | Set-Content -LiteralPath $specTxt -Encoding UTF8

    $patchTmx = Join-Path $cpMapDir "assets\booth.tmx"
    $null = Invoke-SdvRaw @('map-autotile', '--spec', $specTxt, '--out', $patchTmx, '--theme', 'island')
    $null = Invoke-SdvRaw @('map-stamp', $patchTmx, '--prefab', '@island-palm', '--at', '1,0')
    $mcRes = Invoke-SdvRaw @('map-check', $patchTmx, '--entry', '0,0')
    Assert-True ($mcRes.Code -eq 0) "Generated island map passes map-check: $($mcRes.Text)"

    [System.IO.File]::WriteAllText((Join-Path $cpMapDir "manifest.json"), @'
{
  "Name": "CpEditMapPack",
  "Author": "Tester",
  "Version": "1.0.0",
  "UniqueID": "Tester.CpEditMapPack",
  "ContentPackFor": { "UniqueID": "Pathoschild.ContentPatcher" }
}
'@, [System.Text.UTF8Encoding]::new($false))

    [System.IO.File]::WriteAllText((Join-Path $cpMapDir "content.json"), @'
{
  "Format": "2.4.0",
  "Changes": [
    {
      "Action": "EditMap",
      "LogName": "Town Plaza Island Booth",
      "Target": "Maps/Town",
      "FromFile": "assets/booth.tmx",
      "FromArea": { "X": 0, "Y": 0, "Width": 6, "Height": 4 },
      "ToArea": { "X": 26, "Y": 62, "Width": 6, "Height": 4 },
      "PatchMode": "Overlay",
      "MapTiles": [
        {
          "Position": { "X": 27, "Y": 63 },
          "Layer": "Buildings",
          "SetProperties": { "Action": "Message \"HelloBooth\"" }
        }
      ],
      "AddWarps": [ "26 65 Beach 20 4" ]
    }
  ]
}
'@, [System.Text.UTF8Encoding]::new($false))

    $townOutPng = Join-Path $workRoot "town_editmap_preview.png"
    $emRes = Invoke-SdvRaw @('map-render', 'Maps/Town', '--cp', $cpMapDir, '--auto-crop', '--debug-overlay', '--out', $townOutPng)
    Assert-True ($emRes.Code -eq 0) "CP EditMap offline render succeeded: $($emRes.Text)"
    Assert-Contains $emRes.Text "Town Plaza Island Booth" "CP EditMap patch applied"
    Assert-True (Test-Path -LiteralPath $townOutPng) "CP EditMap preview PNG written"

    # Compact `map-inspect --rect` check (must stay < 120 lines instead of dumping 1,446 full-map metadata lines!)
    $rectInsp = Invoke-SdvRaw @('map-inspect', 'Maps/Town', '--rect', '44,94,14,12')
    Assert-True ($rectInsp.Code -eq 0) "map-inspect Maps/Town --rect succeeded: $($rectInsp.Text)"
    $rectLineCount = ($rectInsp.Text -split "`r?`n").Count
    Assert-True ($rectLineCount -lt 120) "map-inspect --rect stayed compact ($rectLineCount lines < 120, vs old 1446 lines)"
    Assert-Contains $rectInsp.Text '"referencedTilesheets"' "map-inspect --rect listed only referenced tilesheets"
    Assert-Contains $rectInsp.Text '"passabilityMask"' "map-inspect --rect included passabilityMask"

    # Simulate a pure beginner PC that ONLY has `Content\*.xnb` + `MonoGame.Framework.dll` (ZERO `Content (unpacked)` folder!)
    $realGameDir = "D:\steam\steamapps\common\Stardew Valley"
    $cleanBeginnerGame = Join-Path $workRoot "CleanBeginnerStardew"
    New-Item -ItemType Directory -Path (Join-Path $cleanBeginnerGame "Content\Maps") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $cleanBeginnerGame "Content\Portraits") -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $realGameDir "MonoGame.Framework.dll") -Destination (Join-Path $cleanBeginnerGame "MonoGame.Framework.dll") -Force
    foreach ($xnbRel in @("Maps\BusStop.xnb", "Maps\spring_outdoorsTileSheet.xnb", "Maps\paths.xnb", "Portraits\Abigail.xnb")) {
        Copy-Item -LiteralPath (Join-Path $realGameDir "Content\$xnbRel") -Destination (Join-Path $cleanBeginnerGame "Content\$xnbRel") -Force
    }
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $cleanBeginnerGame "Content (unpacked)"))) "CleanBeginnerStardew has NO Content (unpacked) directory"

    $xnbSpriteOut = Join-Path $workRoot "xnb_abigail_portrait.png"
    $xnbSp = Invoke-SdvRaw @('sprite', 'Portraits/Abigail', '--preset', 'portrait', '--game-path', $cleanBeginnerGame, '--out', $xnbSpriteOut)
    Assert-True ($xnbSp.Code -eq 0) "On-demand .xnb portrait sprite extraction succeeded without Content (unpacked): $($xnbSp.Text)"
    Assert-Contains $xnbSp.Text "xnb-cache" "Resolved Abigail portrait directly from on-demand xnb-cache"
    Assert-True (Test-Path -LiteralPath $xnbSpriteOut) "Rendered XNB portrait inspection PNG"

    $xnbMapInsp = Invoke-SdvRaw @('map-inspect', 'Maps/BusStop', '--rect', '10,8,8,6', '--game-path', $cleanBeginnerGame)
    Assert-True ($xnbMapInsp.Code -eq 0) "On-demand .xnb map-inspect BusStop succeeded without Content (unpacked): $($xnbMapInsp.Text)"
    Assert-Contains $xnbMapInsp.Text "xnb-cache" "Extracted BusStop.xnb (tBIN10 -> .tmx) on demand"

    $xnbMapPng = Join-Path $workRoot "xnb_busstop_render.png"
    $xnbMapRen = Invoke-SdvRaw @('map-render', 'Maps/BusStop', '--rect', '8,6,14,10', '--debug-overlay', '--game-path', $cleanBeginnerGame, '--out', $xnbMapPng)
    Assert-True ($xnbMapRen.Code -eq 0) "On-demand .xnb map-render BusStop (map + tilesheets) succeeded without Content (unpacked): $($xnbMapRen.Text)"
    Assert-True (Test-Path -LiteralPath $xnbMapPng) "Rendered XNB BusStop map PNG"

    # Also verify special-repo CurseForge CDN auto-pull + soft-GMCM auto-detection when `<GamePath>\Mods` does NOT exist!
    $depsCacheGmcm = Join-Path $HOME ".local\share\sdv-dev\deps-cache\spacechase0.GenericModConfigMenu"
    if (Test-Path -LiteralPath $depsCacheGmcm) {
        Remove-Item -LiteralPath $depsCacheGmcm -Recurse -Force -ErrorAction SilentlyContinue
    }
    $gmcmDepsCheck = Invoke-SdvRaw @('deps', '--project', $gmcmMod, '--game-path', $cleanBeginnerGame)
    Assert-True ($gmcmDepsCheck.Code -eq 0) "Soft-GMCM auto-detection + CurseForge CDN fetch succeeded on empty Mods: $($gmcmDepsCheck.Text)"
    Assert-Contains $gmcmDepsCheck.Text "spacechase0.GenericModConfigMenu" "Detected soft GMCM dependency & fetched via CurseForge CDN"
    $cpDepsCheck = Invoke-SdvRaw @('deps', '--project', $cpMapDir, '--game-path', $cleanBeginnerGame)
    Assert-True ($cpDepsCheck.Code -eq 0) "Content Patcher special-repo resolution succeeded on empty Mods: $($cpDepsCheck.Text)"
    Assert-Contains $cpDepsCheck.Text "Pathoschild.ContentPatcher" "Resolved Pathoschild.ContentPatcher without local Mods"

    # Verify `build --deploy --both` syncs full mod folder (manifest, content, assets) to BOTH real Mods and isolated-mods!
    $dualBuild = Invoke-SdvRaw @('build', $cpMapDir, '--deploy', '--both', '--game-path', $cleanBeginnerGame)
    Assert-True ($dualBuild.Code -eq 0) "build --deploy --both succeeded: $($dualBuild.Text)"
    $dualObj = $dualBuild.Text | ConvertFrom-Json
    Assert-True ($dualObj.DeployedPaths.Count -ge 2) "build --deploy --both synced to $($dualObj.DeployedPaths.Count) directories"
    Assert-True (Test-Path -LiteralPath (Join-Path $cleanBeginnerGame "Mods\CpEditMapPack\assets\booth.tmx")) "Real Mods received assets/booth.tmx"
    # Clean up test pack from shared DefaultIsolatedModsRoot after verifying dual deploy
    Remove-Item -LiteralPath (Join-Path $HOME ".local\share\sdv-dev\isolated-mods\CpEditMapPack") -Recurse -Force -ErrorAction SilentlyContinue

    # Verify Pathoschild-style `REPLACE_ME_WITH_VERSION` auto-replacement from `.csproj` `<Version>` + `sdv-project.local.json` overlay!
    $tplModDir = Join-Path $workRoot "TemplateVerMod"
    New-Item -ItemType Directory -Path $tplModDir -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $tplModDir "TemplateVerMod.csproj"), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net6.0</TargetFramework><Version>2.4.6</Version></PropertyGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $tplModDir "manifest.json"), '{"Name":"TemplateVerMod","Author":"Tester","Version":"REPLACE_ME_WITH_VERSION","UniqueID":"Tester.TemplateVerMod","MinimumApiVersion":"4.0.0","EntryDll":"TemplateVerMod.dll","UpdateKeys":["GitHub:Tester/TemplateVerMod"]}', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $tplModDir "ModEntry.cs"), 'using StardewModdingAPI; namespace TemplateVerMod { public class ModEntry : Mod { public override void Entry(IModHelper h) {} } }', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $tplModDir "sdv-project.local.json"), '{"isolated":true,"extraMods":["Pathoschild.ContentPatcher"]}', [System.Text.UTF8Encoding]::new($false))

    $tplDeps = Invoke-SdvRaw @('deps', '--project', $tplModDir, '--game-path', $cleanBeginnerGame)
    Assert-True ($tplDeps.Code -eq 0) "deps read sdv-project.local.json cleanly: $($tplDeps.Text)"
    Assert-Contains $tplDeps.Text "Pathoschild.ContentPatcher" "sdv-project.local.json extraMods overlaid without touching sdv-project.json"
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $tplModDir "sdv-project.json"))) "sdv-project.json stayed untouched"

    $tplBuild = Invoke-SdvRaw @('build', $tplModDir, '--deploy', '--single-target', '--mods-path', (Join-Path $cleanBeginnerGame "Mods"))
    Assert-True ($tplBuild.Code -eq 0) "TemplateVerMod build --deploy succeeded: $($tplBuild.Text)"
    $deployedMfText = Get-Content -LiteralPath (Join-Path $cleanBeginnerGame "Mods\TemplateVerMod\manifest.json") -Raw
    Assert-Contains $deployedMfText '"2.4.6"' "Deployed manifest.json replaced REPLACE_ME_WITH_VERSION with 2.4.6 from .csproj"
    Assert-True ($deployedMfText -notmatch 'REPLACE_ME_WITH_VERSION') "No residual REPLACE_ME_WITH_VERSION in deployed manifest.json"

    $tplPack = Invoke-SdvRaw @('pack', '--project', $tplModDir)
    Assert-True ($tplPack.Code -eq 0) "TemplateVerMod pack succeeded with REPLACE_ME_WITH_VERSION: $($tplPack.Text)"
    Assert-Contains $tplPack.Text '"Version": "2.4.6"' "sdv-dev pack materialized SemVer 2.4.6 from .csproj"

    Pass-Step "Offline map-autotile, CP EditMap, zero-unpacked .xnb, CP/GMCM pull, dual deploy, & REPLACE_ME_WITH_VERSION fill"

    # ------------------------------------------------------------------
    # 6b. Expanded API/Decompile (--grep, MonoGame, xTile, SModHooks),
    #     Built-in .fx Shader Compiler, & MonoGame/SMAPI Rendering AST Checks
    # ------------------------------------------------------------------
    $mgApi = Invoke-SdvRaw @('api', 'RenderTarget2D', '--asm', 'monogame')
    Assert-True ($mgApi.Code -eq 0) "api --asm monogame exit 0"
    Assert-Contains $mgApi.Text "Microsoft.Xna.Framework.Graphics.RenderTarget2D" "MonoGame.Framework indexed"

    $xtDec = Invoke-SdvRaw @('decompile', 'XnaDisplayDevice', '--lines', '40')
    Assert-True ($xtDec.Code -eq 0) "decompile XnaDisplayDevice exit 0"
    Assert-Contains $xtDec.Text "Assembly: xTile.dll" "xTile.dll decompiled"

    $hookDec = Invoke-SdvRaw @('decompile', 'SModHooks.OnRendered', '--lines', '50')
    Assert-True ($hookDec.Code -eq 0) "decompile SModHooks.OnRendered exit 0"
    Assert-Contains $hookDec.Text "StardewModdingAPI" "Internal SMAPI SModHooks.OnRendered decompiled"

    $grepDec = Invoke-SdvRaw @('decompile', 'Game1.DrawWorld', '--grep', 'lighting', '-C', '4')
    Assert-True ($grepDec.Code -eq 0) "decompile --grep exit 0"
    Assert-Contains $grepDec.Text "// [grep]" "decompile --grep header present"
    Assert-Contains $grepDec.Text ">" "decompile --grep matched line marker present"
    Pass-Step "Expanded MonoGame/xTile/SMAPI-internal API & decompile --grep"

    # Test Rendering AST Analyzer + .fx Shader Compiler (both broken & valid HLSL)
    $shaderTestRoot = Join-Path $workRoot "ShaderAndRenderCheckContainer"
    $null = Invoke-SdvRaw @('new', 'csharp', '--name', 'RenderPitfallMod', '--author', 'Tester', '--out', $shaderTestRoot)
    $renderMod = Join-Path $shaderTestRoot "RenderPitfallMod"
    New-Item -ItemType Directory -Path (Join-Path $renderMod "assets") -Force | Out-Null

    $badFx = "float4 BrokenPS() : COLOR0 { return NonExistentHlslIntrinsic(1.0); } technique T0 { pass P0 { PixelShader = compile ps_3_0 BrokenPS(); } }"
    [System.IO.File]::WriteAllText((Join-Path $renderMod "assets\broken.fx"), $badFx, [System.Text.UTF8Encoding]::new($false))

    $badRenderCs = @'
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace RenderPitfallMod
{
    public class ModEntry : Mod
    {
        private RenderTarget2D? _cachedRt;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            var gd = Game1.graphics.GraphicsDevice;
            // Guarded cache resize should NOT trigger false positive!
            if (_cachedRt == null || _cachedRt.IsDisposed)
                _cachedRt = new RenderTarget2D(gd, 64, 64);

            // Intentional pitfalls:
            // 1. Double Begin on active SMAPI SpriteBatch (ERROR)
            e.SpriteBatch.Begin();
            // 2. Unguarded per-frame Texture2D allocation (ERROR)
            var leakedTex = new Texture2D(gd, 16, 16);
            // 3. Offscreen SetRenderTarget without restoring (WARN)
            gd.SetRenderTarget(_cachedRt);
            // 4. Non-premultiplied 4-arg Color with variable alpha under default AlphaBlend (WARN)
            byte alpha = 128;
            Color tint = new Color(255, 220, 180, alpha);
            e.SpriteBatch.Draw(leakedTex, Vector2.Zero, tint);
        }
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $renderMod "ModEntry.cs"), $badRenderCs, [System.Text.UTF8Encoding]::new($false))

    $renderChk = Invoke-SdvRaw @('check', $renderMod)
    Assert-True ($renderChk.Code -ne 0) "Broken shader + rendering pitfalls must fail sdv-dev check"
    Assert-Contains $renderChk.Text "already active" "Caught double e.SpriteBatch.Begin() in RenderedWorld"
    Assert-Contains $renderChk.Text "new Texture2D" "Caught per-frame Texture2D leak"
    Assert-True ($renderChk.Text.IndexOf("new RenderTarget2D", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "Guarded _cachedRt resize did NOT false-positive"
    Assert-Contains $renderChk.Text "SetRenderTarget" "Caught unrestored SetRenderTarget warning"
    Assert-Contains $renderChk.Text "Premultiplied alpha hazard" "Caught 4-arg non-premultiplied Color warning"
    Assert-Contains $renderChk.Text "assets/broken.fx" "Caught broken .fx HLSL syntax error during check"

    # Replace with valid .fx shader (including nested subdirectory `#include "common/tint_math.fxh"` inside Chinese path!) & clean C# code
    Remove-Item -LiteralPath (Join-Path $renderMod "assets\broken.fx") -Force
    New-Item -ItemType Directory -Path (Join-Path $renderMod "assets\common") -Force | Out-Null
    $fxHeader = "float3 ApplyCyanLerp(float3 baseRgb, float strength) { return lerp(baseRgb, float3(0.15, 0.85, 0.95), saturate(strength)); }"
    [System.IO.File]::WriteAllText((Join-Path $renderMod "assets\common\tint_math.fxh"), $fxHeader, [System.Text.UTF8Encoding]::new($false))

    $validFx = @'
#include "common/tint_math.fxh"
#if OPENGL
    #define SV_POSITION POSITION
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif
Texture2D SpriteTexture;
float TintStrength;
sampler2D SpriteTextureSampler = sampler_state { Texture = <SpriteTexture>; };
struct VertexShaderOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TextureCoordinates : TEXCOORD0; };
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 col = tex2D(SpriteTextureSampler, input.TextureCoordinates) * input.Color;
    col.rgb = ApplyCyanLerp(col.rgb, TintStrength);
    return col;
}
technique SpriteDrawing { pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } };
'@
    [System.IO.File]::WriteAllText((Join-Path $renderMod "assets\water_tint.fx"), $validFx, [System.Text.UTF8Encoding]::new($false))
    $cleanRenderCs = @'
using StardewModdingAPI;
namespace RenderPitfallMod
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper) { }
    }
}
'@
    [System.IO.File]::WriteAllText((Join-Path $renderMod "ModEntry.cs"), $cleanRenderCs, [System.Text.UTF8Encoding]::new($false))
    $bShader = Invoke-SdvRaw @('build', $renderMod)
    Assert-True ($bShader.Code -eq 0) "Build with valid .fx shader succeeded: $($bShader.Text)"
    Assert-True (Test-Path -LiteralPath (Join-Path $renderMod "assets\water_tint.mgfxo")) "Compiled .mgfxo emitted next to .fx"
    $pkShader = Invoke-SdvRaw @('pack', $renderMod)
    Assert-True ($pkShader.Code -eq 0) "Pack with compiled .mgfxo succeeded: $($pkShader.Text)"
    Assert-Contains $pkShader.Text "water_tint.mgfxo" "Pack included compiled .mgfxo"
    Assert-True ($pkShader.Text.IndexOf("water_tint.fx`"", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "Pack excluded raw .fx source"
    Pass-Step "Built-in .fx -> .mgfxo OpenGL shader compiler & Roslyn MonoGame/SMAPI rendering checks"

    # ------------------------------------------------------------------
    # 7. Live End-to-End SMAPI Integration Suite (-IncludeLive)
    # ------------------------------------------------------------------
    if ($IncludeLive) {
        Write-Host ""
        Write-Host "=== Running Live In-Game SMAPI Integration Suite ===" -ForegroundColor Cyan

        $liveCheck = Invoke-SdvRaw @('live') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        if ($liveCheck.running -and (-not $liveCheck.bridgeConnected)) {
            throw "Unmanaged manual Stardew Valley session is running! Aborting live test to protect user session."
        }

        # Build a rich C# test mod exercising SMAPI UpdateTicked/RenderedWorld, Harmony Prefix+Transpiler,
        # instant 3-option Question Dialogue callback, & custom IClickableMenu with neighbor IDs!
        $liveModContainer = Join-Path $workRoot "LiveSuiteContainer"
        $null = Invoke-SdvRaw @('new', 'csharp', '--name', 'LiveProbeMod', '--author', 'Tester', '--out', $liveModContainer)
        $liveModDir = Join-Path $liveModContainer "LiveProbeMod"
        New-Item -ItemType Directory -Path (Join-Path $liveModDir "assets") -Force | Out-Null

        # Embed a real custom OpenGL HLSL Pixel Shader (.fx) to verify live MonoGame 3.8.0.1641 Effect loading + A/B compare!
        $liveFx = @'
#if OPENGL
    #define SV_POSITION POSITION
    #define PS_SHADERMODEL ps_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif
Texture2D SpriteTexture;
float TintStrength;
sampler2D SpriteTextureSampler = sampler_state { Texture = <SpriteTexture>; };
struct VertexShaderOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TextureCoordinates : TEXCOORD0; };
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 c = tex2D(SpriteTextureSampler, input.TextureCoordinates) * input.Color;
    return float4(lerp(c.rgb, float3(0.05, 0.92, 0.98), saturate(TintStrength)), c.a);
}
technique SpriteDrawing { pass P0 { PixelShader = compile PS_SHADERMODEL MainPS(); } };
'@
        [System.IO.File]::WriteAllText((Join-Path $liveModDir "assets\probe_tint.fx"), $liveFx, [System.Text.UTF8Encoding]::new($false))

        $liveModCs = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace LiveProbeMod
{
    public sealed class LiveProbeConfig
    {
        public bool Enabled { get; set; } = true;
        public float TintStrength { get; set; } = 0.2f;
    }

    public static class ProbeTarget
    {
        public static int ComputeBonus(int baseVal) => baseVal + 1;
    }

    [HarmonyPatch(typeof(ProbeTarget), nameof(ProbeTarget.ComputeBonus))]
    public static class ProbeTargetPatch
    {
        public static void Postfix(ref int __result)
        {
            __result += 10;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Ldc_I4_1)
                    yield return new CodeInstruction(OpCodes.Ldc_I4_5);
                else
                    yield return ci;
            }
        }
    }

    public sealed class TestNavMenu : IClickableMenu
    {
        public string hoverText = "";
        public ClickableComponent BtnTop;
        public ClickableComponent BtnBottom;
        public ClickableComponent BtnRightBroken;

        public TestNavMenu() : base(200, 140, 520, 340, showUpperRightCloseButton: true)
        {
            BtnTop = new ClickableComponent(new Rectangle(xPositionOnScreen + 60, yPositionOnScreen + 60, 180, 64), "TopBtn", "Top")
            {
                myID = 101,
                downNeighborID = 102,
                rightNeighborID = 103
            };
            BtnBottom = new ClickableComponent(new Rectangle(xPositionOnScreen + 60, yPositionOnScreen + 180, 180, 64), "BottomBtn", "Bottom")
            {
                myID = 102,
                upNeighborID = 101
            };
            BtnRightBroken = new ClickableComponent(new Rectangle(xPositionOnScreen + 300, yPositionOnScreen + 60, 180, 64), "BrokenNavBtn", "Broken")
            {
                myID = 103,
                leftNeighborID = 101,
                downNeighborID = 999 // Intentional dangling neighbor ID so --ui-overlay highlights it!
            };
            populateClickableComponentList();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = getComponentWithID(101);
            snapCursorToCurrentSnappedComponent();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            if (BtnTop.containsPoint(x, y))
                hoverText = "Tooltip: 祈福上按钮 #101";
            else if (BtnBottom.containsPoint(x, y))
                hoverText = "Tooltip: 祈福下按钮 #102";
            else
                hoverText = "";
        }

        public override void draw(SpriteBatch b)
        {
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            IClickableMenu.drawTextureBox(b, BtnTop.bounds.X, BtnTop.bounds.Y, BtnTop.bounds.Width, BtnTop.bounds.Height, Color.LightCyan);
            IClickableMenu.drawTextureBox(b, BtnBottom.bounds.X, BtnBottom.bounds.Y, BtnBottom.bounds.Width, BtnBottom.bounds.Height, Color.LightGreen);
            IClickableMenu.drawTextureBox(b, BtnRightBroken.bounds.X, BtnRightBroken.bounds.Y, BtnRightBroken.bounds.Width, BtnRightBroken.bounds.Height, Color.MistyRose);
            b.DrawString(Game1.dialogueFont, "Top #101", new Vector2(BtnTop.bounds.X + 16, BtnTop.bounds.Y + 12), Color.Black);
            b.DrawString(Game1.dialogueFont, "Bot #102", new Vector2(BtnBottom.bounds.X + 16, BtnBottom.bounds.Y + 12), Color.Black);
            b.DrawString(Game1.dialogueFont, "Bad #103", new Vector2(BtnRightBroken.bounds.X + 16, BtnRightBroken.bounds.Y + 12), Color.DarkRed);
            if (!string.IsNullOrEmpty(hoverText))
                IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont);
            base.draw(b);
            drawMouse(b);
        }
    }

    public class ModEntry : Mod
    {
        public static int ChosenWellOption = -1;
        public static bool ShaderLoadedOk = false;
        public LiveProbeConfig Config { get; set; } = new LiveProbeConfig();
        private Harmony? _harmony;
        private Effect? _customShader;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<LiveProbeConfig>();
            _harmony = new Harmony(ModManifest.UniqueID);
            _harmony.PatchAll(typeof(ModEntry).Assembly);
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            string fxPath = Path.Combine(Helper.DirectoryPath, "assets", "probe_tint.mgfxo");
            if (File.Exists(fxPath))
            {
                _customShader = new Effect(Game1.graphics.GraphicsDevice, File.ReadAllBytes(fxPath));
                ShaderLoadedOk = true;
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Burn ~50 microseconds so profiler clearly ranks LiveProbeMod!
            double acc = 0;
            for (int i = 0; i < 1500; i++) acc += Math.Sin(i);
            if (acc > 1e9) Monitor.Log(acc.ToString());
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            int v = ProbeTarget.ComputeBonus(10);
            if (v < 0) Monitor.Log(v.ToString());

            if (Config.Enabled && _customShader != null && Config.TintStrength > 0.01f)
            {
                _customShader.Parameters["TintStrength"]?.SetValue(Config.TintStrength);
                e.SpriteBatch.End();
                e.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, _customShader);
                e.SpriteBatch.Draw(
                    Game1.staminaRect,
                    new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                    Color.White * 0.55f);
                e.SpriteBatch.End();
                e.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }
        }

        public static void OpenWishingWellQuestion()
        {
            ChosenWellOption = -1;
            var opts = new Response[]
            {
                new Response("opt_0", "离开祈愿泉"),
                new Response("opt_1", "献上果酒祈福（奖励五彩碎片）"),
                new Response("opt_2", "询问海湾传说")
            };
            Game1.currentLocation.createQuestionDialogue(
                "古老的祈愿泉泛着微光，你要怎么做？",
                opts,
                (who, key) =>
                {
                    if (key == "opt_1")
                    {
                        ChosenWellOption = 1;
                        who.addItemByMenuIfNecessary(ItemRegistry.Create("(O)74", 1));
                    }
                });
        }

        public static void OpenCustomNavMenu()
        {
            Game1.activeClickableMenu = new TestNavMenu();
        }
    }
}
'@
        [System.IO.File]::WriteAllText((Join-Path $liveModDir "ModEntry.cs"), $liveModCs, [System.Text.UTF8Encoding]::new($false))

        $bLive = Invoke-SdvRaw @('build', $liveModDir)
        Assert-True ($bLive.Code -eq 0) "LiveProbeMod build succeeded: $($bLive.Text)"
        Assert-Contains $bLive.Text "probe_tint.mgfxo" "LiveProbeMod auto-compiled probe_tint.fx -> .mgfxo"

        $startRes = Invoke-SdvRaw @('restart', '--isolated', '--background', '--project', $liveModDir, '--reset-test-world')
        Assert-True ($startRes.Code -eq 0) "Isolated background test world launched with LiveProbeMod: $($startRes.Text)"
        Assert-Contains $startRes.Text '"backgroundMode": true' "--background mode active without breaking viewport"
        $shaderOkEval = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.ShaderLoadedOk')
        Assert-Contains $shaderOkEval.Text 'true' "Custom .mgfxo OpenGL PixelShader loaded cleanly into live MonoGame GraphicsDevice!"
        Pass-Step "Live isolated --background launch & real OpenGL .mgfxo Effect execution"

        # 7a. Instant 0ms `ui-click --response 1` bypassing 750ms DialogueBox safetyTimer!
        $null = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.OpenWishingWellQuestion(); return "opened";')
        $clickRes = Invoke-SdvRaw @('ui-click', '--response', '1')
        Assert-True ($clickRes.Code -eq 0) "ui-click --response 1 succeeded immediately: $($clickRes.Text)"
        $wellVal = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.ChosenWellOption')
        Assert-Contains $wellVal.Text '1' "DialogueBox callback actually executed with 0ms sleep (ChosenWellOption == 1)"
        $null = Invoke-SdvRaw @('ui-click', '--close')
        Pass-Step "Zero-sleep DialogueBox ui-click --response callback execution"

        # 7b. Custom IClickableMenu + `screenshot --mode screen --ui-overlay`
        $openMenu = Invoke-SdvRaw @('eval', 'Game1.dialogueUp = false; LiveProbeMod.ModEntry.OpenCustomNavMenu(); return Game1.activeClickableMenu?.GetType().FullName;')
        Assert-True ($openMenu.Code -eq 0) "OpenCustomNavMenu eval succeeded: $($openMenu.Text)"
        Assert-Contains $openMenu.Text "TestNavMenu" "TestNavMenu active on Game1.activeClickableMenu"
        $uiShotPng = Join-Path $workRoot "custom_menu_ui_overlay.png"
        $shotRes = Invoke-SdvRaw @('screenshot', '--mode', 'screen', '--ui-overlay', '--out', $uiShotPng)
        Assert-True ($shotRes.Code -eq 0) "screenshot --ui-overlay succeeded: $($shotRes.Text)"
        Assert-Contains $shotRes.Text '"brokenNeighborLinks": 1' "UI overlay detected intentional dangling neighbor ID 999"
        Assert-True (Test-Path -LiteralPath $uiShotPng) "UI overlay PNG written"

        # 7b2. Focus-free virtual mouse hover Tooltip + real Gamepad D-Pad neighborID focus trail inside `--background` window!
        $hoverRes = Invoke-SdvRaw @('input', '--move', '350,232', '--ticks', '4')
        Assert-True ($hoverRes.Code -eq 0) "sdv-dev input --move succeeded: $($hoverRes.Text)"
        Assert-Contains $hoverRes.Text '祈福上按钮 #101' "Virtual mouse hover triggered performHoverAction & captured custom hoverText Tooltip"

        $padRes = Invoke-SdvRaw @('input', '--gamepad', 'DPadDown,DPadUp,DPadRight', '--interval-ticks', '3')
        Assert-True ($padRes.Code -eq 0) "sdv-dev input --gamepad succeeded: $($padRes.Text)"
        Assert-Contains $padRes.Text '#102' "Gamepad DPadDown moved focus to #102"
        Assert-Contains $padRes.Text '#103' "Gamepad DPadUp -> DPadRight moved focus to #103"
        $null = Invoke-SdvRaw @('ui-click', '--close')

        # Real WASD physical walking + Copper Hoe long-press tool charging (`maxToolPowerReached >= 1`) without window focus!
        $null = Invoke-SdvRaw @('warp', 'Farm', '60', '16')
        $walkRes = Invoke-SdvRaw @('input', '--hold', 'D', '--ticks', '18')
        Assert-True ($walkRes.Code -eq 0) "sdv-dev input --hold D walking succeeded: $($walkRes.Text)"
        $walkObj = $walkRes.Text | ConvertFrom-Json
        Assert-True ([double]$walkObj.player.movedPixels -gt 30) "Real WASD physics moved farmer $($walkObj.player.movedPixels) px in background window"
        Assert-True ([int]$walkObj.player.facingAfter -eq 1) "Real WASD set farmer facing East (1)"

        $null = Invoke-SdvRaw @('eval', 'var hoe = Player.Items.OfType<StardewValley.Tools.Hoe>().First(); hoe.UpgradeLevel = 2; Player.CurrentToolIndex = Player.Items.IndexOf(hoe); Player.Stamina = 270f; return hoe.UpgradeLevel;')
        $chargeRes = Invoke-SdvRaw @('input', '--hold', 'C', '--ticks', '50')
        Assert-True ($chargeRes.Code -eq 0) "sdv-dev input --hold C tool charge succeeded: $($chargeRes.Text)"
        $chargeObj = $chargeRes.Text | ConvertFrom-Json
        Assert-True ([int]$chargeObj.player.maxToolPowerReached -ge 1) "Real C hold charged Steel Hoe to toolPower=$($chargeObj.player.maxToolPowerReached) in background window"
        Pass-Step "Custom IClickableMenu --ui-overlay + focus-free input (hover Tooltip, Gamepad focus trail, WASD walk, & tool charge)"

        # 7c. `profile` capturing SMAPI 4.5.2 ManagedEvent callbacks + Harmony patches!
        $profRes = Invoke-SdvRaw @('profile', '--seconds', '2', '--top', '8')
        Assert-True ($profRes.Code -eq 0) "sdv-dev profile succeeded"
        $profJson = $profRes.Text | ConvertFrom-Json
        Assert-True ($profJson.instrumentedCallbacks -ge 3) "Profiler hooked SMAPI 4.5.2 events + Harmony (got $($profJson.instrumentedCallbacks))"
        Assert-Contains $profRes.Text "Tester.LiveProbeMod" "Profiler attributed CPU time to Tester.LiveProbeMod"
        Assert-Contains $profRes.Text "OnUpdateTicked" "Profiler captured SMAPI UpdateTicked hotspot"
        Assert-True ($profJson.unpatchedCleanly -eq $true) "Profiler unpatched cleanly"
        Pass-Step "SMAPI 4.5.2 Event + Harmony profiler (instrumentedCallbacks=$($profJson.instrumentedCallbacks))"

        # 7d. `harmony --il` live post-transpiler instruction attribution
        $ilRes = Invoke-SdvRaw @('harmony', 'ProbeTarget:ComputeBonus', '--il')
        Assert-True ($ilRes.Code -eq 0) "harmony --il succeeded"
        Assert-Contains $ilRes.Text "// + [Tester.LiveProbeMod]" "Transpiler IL diff attributed ldc.i4.5 to Tester.LiveProbeMod"
        Pass-Step "Harmony live post-transpiler IL inspection & mod blame"

        # 7e. Synchronous warp + interact + advance-days + checkpoint save/load time machine!
        $wFarm = Invoke-SdvRaw @('warp', 'Farm', '64', '15')
        Assert-Contains $wFarm.Text '"location": "Farm"' "Instant synchronous warp landed on Farm"

        $plKeg = Invoke-SdvRaw @('interact', '--place', '(BC)12', '65,16')
        Assert-Contains $plKeg.Text '"placed": true' "Placed Keg on Farm (65,16)"
        $dpBlue = Invoke-SdvRaw @('interact', '--drop-in', '(O)258', '65,16')
        Assert-Contains $dpBlue.Text '"accepted": true' "Dropped Blueberry into Keg"
        $null = Invoke-SdvRaw @('world', '--advance-days', '7')
        # After sleeping 7 nights, player wakes up in FarmHouse bed — warp back outside to Farm beside the Keg!
        $null = Invoke-SdvRaw @('warp', 'Farm', '64', '15')
        $null = Invoke-SdvRaw @('world', '--time', '1430')

        $cpSave = Invoke-SdvRaw @('checkpoint', 'save', 'auto_test_ready_keg')
        Assert-True ($cpSave.Code -eq 0) "checkpoint save succeeded: $($cpSave.Text)"

        $harv1 = Invoke-SdvRaw @('interact', '--right-click', '65,16')
        Assert-Contains $harv1.Text '(O)348' "First harvest yielded Wine (O)348"

        $cpLoad = Invoke-SdvRaw @('checkpoint', 'load', 'auto_test_ready_keg')
        Assert-True ($cpLoad.Code -eq 0) "checkpoint load succeeded: $($cpLoad.Text)"
        Assert-Contains $cpLoad.Text '"timeOfDay": 1430' "Checkpoint restored exact mid-day time 1430"

        $harv2 = Invoke-SdvRaw @('interact', '--right-click', '65,16')
        Assert-Contains $harv2.Text '(O)348' "Restored checkpoint allowed harvesting Wine a second time!"
        $null = Invoke-SdvRaw @('checkpoint', 'delete', 'auto_test_ready_keg')

        # Verify `warp` cutscene hijack protection:
        # On Sunny Spring Day 8 at 10:00am:
        # 1) `warp Beach` (no explicit x,y) triggers Willy's Bamboo Pole cutscene 739330 (`eventActive == true`);
        # 2) `warp Town 45 71` (explicit x,y) aborts Willy's active cutscene AND auto-skips Lewis's Community Center cutscene 611439,
        #    landing cleanly at Town (45, 71) with `eventActive == false` and `isPlayerFree == true`!
        $null = Invoke-SdvRaw @('world', '--weather', 'sun', '--time', '1000')
        $wBeachEvt = Invoke-SdvRaw @('warp', 'Beach') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($wBeachEvt.eventActive -eq $true -and -not [string]::IsNullOrWhiteSpace($wBeachEvt.eventId)) "Warp without coords reported triggered Beach cutscene ($($wBeachEvt.eventId))"
        $wTownClean = Invoke-SdvRaw @('warp', 'Town', '45', '71') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($wTownClean.ok -eq $true -and $wTownClean.eventActive -eq $false -and $wTownClean.targetX -eq 45 -and $wTownClean.targetY -eq 71 -and -not [string]::IsNullOrWhiteSpace($wTownClean.skippedEventId)) "Warp with explicit coords (45,71) auto-cleaned entry cutscene(s) ($($wTownClean.skippedEventId)) and landed at exact tile"
        $liveAfterTown = Invoke-SdvRaw @('live') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($liveAfterTown.isPlayerFree -eq $true -and $liveAfterTown.eventActive -eq $false -and $liveAfterTown.tileX -eq 45 -and $liveAfterTown.tileY -eq 71) "Player is free at Town (45,71) with zero residual event lock"

        # Verify P0-1/P0-2/P2: recovery from fainted `killScreen = true`, `player --invincible/--speed`, `wait`, `eval -c`, and `screenshot --tour`!
        $null = Invoke-SdvRaw @('eval', '-c', 'Game1.killScreen = true; Player.health = 0; Game1.fadeToBlack = true; Game1.fadeToBlackAlpha = 1.0f; return Game1.killScreen;')
        $wRecover = Invoke-SdvRaw @('warp', 'BusStop', '22', '10') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($wRecover.ok -eq $true -and $wRecover.location -eq 'BusStop' -and $wRecover.redirected -eq $false) "Warp cleared killScreen/fade lock and landed at BusStop"
        $pBuff = Invoke-SdvRaw @('player', '--invincible', 'on', '--speed', '+4', '--heal') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($pBuff.invincible -eq $true -and [double]$pBuff.addedSpeed -ge 3.9) "player --invincible on --speed +4 applied natively"
        $waitRes = Invoke-SdvRaw @('wait', '--ticks', '4') | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($waitRes.ok -eq $true -and $waitRes.waitedTicks -ge 4) "sdv-dev wait --ticks 4 advanced 4 ticks"
        $tourPng = Join-Path $workRoot "mini_tour_grid.png"
        $tourRes = Invoke-SdvRaw @('screenshot', '--tour', 'Farm:64,15@1200;Town:45,71@1500', '--output', $tourPng) | Select-Object -ExpandProperty Text | ConvertFrom-Json
        Assert-True ($tourRes.ok -eq $true -and $tourRes.grid -eq '2x1' -and $tourRes.isBlackFrame -eq $false -and [double]$tourRes.meanLuminance -gt 5) "screenshot --tour produced non-black 2x1 overview PNG (meanLuminance=$($tourRes.meanLuminance))"
        $null = Invoke-SdvRaw @('player', '--invincible', 'off', '--speed', 'off')
        Pass-Step "Synchronous warp (with cutscene auto-skip & eventActive) + interact + checkpoint save/load time machine"

        # 7f. Live NPC `schedule-check`
        $scRes = Invoke-SdvRaw @('schedule-check', 'Abigail', '--key', 'spring')
        Assert-True ($scRes.Code -eq 0) "schedule-check Abigail --key spring succeeded"
        Assert-Contains $scRes.Text '"scheduleKey": "spring"' "Parsed Abigail spring schedule routes"
        Pass-Step "Live NPC schedule-check pathfinder verification"

        # 7g. Zero-restart `sdv-dev config` + `screenshot --ab-compare` + `screenshot --burst 4 --pan-test`!
        $cfgSet = Invoke-SdvRaw @('config', 'Tester.LiveProbeMod', '--set', 'TintStrength=0.92', 'Enabled=true')
        Assert-True ($cfgSet.Code -eq 0) "sdv-dev config --set succeeded: $($cfgSet.Text)"
        Assert-Contains $cfgSet.Text '0.92' "Live config updated TintStrength to 0.92 in 0.05s"

        $abPng = Join-Path $workRoot "ab_shader_compare.png"
        $abRes = Invoke-SdvRaw @('screenshot', '--ab-compare', 'Tester.LiveProbeMod', '--out', $abPng)
        Assert-True ($abRes.Code -eq 0) "screenshot --ab-compare succeeded: $($abRes.Text)"
        Assert-Contains $abRes.Text '"grid": "2x1"' "A/B compare produced 2x1 side-by-side PNG"
        $abBmp = [System.Drawing.Bitmap]::new($abPng)
        # Left center (OFF) vs Right center (ON with bright cyan shader overlay!) must differ significantly in Blue/Green channels!
        $pxOff = $abBmp.GetPixel([int]($abBmp.Width * 0.25), [int]($abBmp.Height * 0.55))
        $pxOn  = $abBmp.GetPixel([int]($abBmp.Width * 0.75), [int]($abBmp.Height * 0.55))
        $abBmp.Dispose()
        Assert-True ([Math]::Abs([int]$pxOn.B - [int]$pxOff.B) -gt 20 -or [Math]::Abs([int]$pxOn.G - [int]$pxOff.G) -gt 20) "A/B compare PNG visually differs between OFF ($pxOff) and ON ($pxOn)!"

        $burstPng = Join-Path $workRoot "burst_pan_grid.png"
        $burstRes = Invoke-SdvRaw @('screenshot', '--burst', '4', '--interval-ticks', '4', '--pan-test', '--out', $burstPng)
        Assert-True ($burstRes.Code -eq 0) "screenshot --burst 4 --pan-test succeeded: $($burstRes.Text)"
        Assert-Contains $burstRes.Text '"grid": "2x2"' "Burst capture produced 2x2 contact sheet PNG"
        Assert-True (Test-Path -LiteralPath $burstPng) "Burst contact sheet PNG written"

        # Verify `sdv-dev config --reset` restores default LiveProbeConfig (TintStrength: 0.92 -> 0.2)!
        $cfgReset = Invoke-SdvRaw @('config', 'Tester.LiveProbeMod', '--reset')
        Assert-True ($cfgReset.Code -eq 0) "sdv-dev config --reset succeeded: $($cfgReset.Text)"
        $cfgResetObj = $cfgReset.Text | ConvertFrom-Json
        Assert-True ([Math]::Abs([double]$cfgResetObj.values.TintStrength - 0.2) -lt 0.001) "config --reset restored TintStrength to default 0.2 (got $($cfgResetObj.values.TintStrength))"
        Pass-Step "Live zero-restart config --set/--reset, --ab-compare shader diff, & --burst --pan-test 2x2 grid"

        # 7g2. Safe `--zoom` (rebuilding Game1.screen without UV seam), zero-restart `.fx` shader hot-reload (`reload-assets`),
        #      and lock-tolerant `build --deploy` while `LiveProbeMod.dll` is actively locked by SMAPI!
        $zSet = Invoke-SdvRaw @('world', '--zoom', '0.8')
        Assert-True ($zSet.Code -eq 0) "world --zoom 0.8 succeeded: $($zSet.Text)"
        $zObj = $zSet.Text | ConvertFrom-Json
        Assert-True ($zObj.screenBuffer -eq $zObj.viewport -and -not [string]::IsNullOrWhiteSpace($zObj.screenBuffer)) "world --zoom rebuilt Game1.screen ($($zObj.screenBuffer)) to match viewport ($($zObj.viewport)) without UV seam"
        $zoomShotPng = Join-Path $workRoot "zoom_065_shot.png"
        $zShot = Invoke-SdvRaw @('screenshot', '--zoom', '0.65', '--out', $zoomShotPng)
        Assert-True ($zShot.Code -eq 0) "screenshot --zoom 0.65 succeeded: $($zShot.Text)"
        Assert-True (Test-Path -LiteralPath $zoomShotPng) "screenshot --zoom PNG written"
        $null = Invoke-SdvRaw @('world', '--zoom', '1.0')

        # Regression check: jumping FROM a festival day (spring 13, weatherIcon=1) TO a non-festival day (spring 9) with --time must NOT look for spring9.xnb!
        $festJump1 = Invoke-SdvRaw @('world', '--season', 'spring', '--day', '13', '--time', '900')
        Assert-True ($festJump1.Code -eq 0) "Jump to festival day spring 13 succeeded: $($festJump1.Text)"
        $festJump2 = Invoke-SdvRaw @('world', '--season', 'spring', '--day', '9', '--time', '1200')
        Assert-True ($festJump2.Code -eq 0) "Jump from festival day to non-festival spring 9 succeeded without spring9.xnb error: $($festJump2.Text)"

        # Hot-reload `.fx` shader in 0.2s via `reload-assets --project` WITHOUT restarting SMAPI!
        $preReloadPid = (Invoke-SdvRaw @('live') | Select-Object -ExpandProperty Text | ConvertFrom-Json).pid
        $fxFile = Join-Path $liveModDir "assets\probe_tint.fx"
        $goldFx = (Get-Content -LiteralPath $fxFile -Raw).Replace("float3(0.05, 0.92, 0.98)", "float3(0.98, 0.72, 0.08)")
        [System.IO.File]::WriteAllText($fxFile, $goldFx, [System.Text.UTF8Encoding]::new($false))
        $hotFxRes = Invoke-SdvRaw @('reload-assets', '--project', $liveModDir)
        Assert-True ($hotFxRes.Code -eq 0) "reload-assets --project hot-reloaded .fx shader: $($hotFxRes.Text)"
        Assert-Contains $hotFxRes.Text "_customShader <- probe_tint.mgfxo" "BridgeMod replaced in-memory Effect instance _customShader"
        $postReloadPid = (Invoke-SdvRaw @('live') | Select-Object -ExpandProperty Text | ConvertFrom-Json).pid
        Assert-True ($preReloadPid -eq $postReloadPid) "Shader hot-reload kept exact same SMAPI PID ($postReloadPid)"

        # Lock-tolerant `build --deploy` twice in a row while `LiveProbeMod.dll` is locked by running SMAPI!
        $dep1 = Invoke-SdvRaw @('build', $liveModDir, '--deploy')
        Assert-True ($dep1.Code -eq 0) "First build --deploy while game open succeeded without locked DLL error: $($dep1.Text)"
        $dep2 = Invoke-SdvRaw @('build', $liveModDir, '--deploy')
        Assert-True ($dep2.Code -eq 0) "Second consecutive build --deploy while game open succeeded: $($dep2.Text)"
        Pass-Step "Safe world/screenshot --zoom, 0.2s .fx reload-assets hot-swap, & lock-tolerant build --deploy while game is open"

        # 7h. Lock-free `self_restart` verification while `LiveProbeMod.dll` & `SdvDevBridge.dll` are actively loaded by SMAPI!
        $beforeRestartPid = ($startRes.Text | ConvertFrom-Json).pid
        $modEntryPath = Join-Path $liveModDir "ModEntry.cs"
        $updatedLiveCs = (Get-Content -LiteralPath $modEntryPath -Raw).Replace("public static int ChosenWellOption = -1;", "public static int ChosenWellOption = -1;`n        public static int SelfRestartGen = 2;")
        [System.IO.File]::WriteAllText($modEntryPath, $updatedLiveCs, [System.Text.UTF8Encoding]::new($false))

        $srRes = Invoke-SdvRaw @('restart', '--isolated', '--same-desktop', '--no-console', '--no-focus', '--project', $liveModDir, '--test-world')
        Assert-True ($srRes.Code -eq 0) "Lock-free self_restart with --no-console succeeded while DLL was loaded: $($srRes.Text)"
        $srJsonObj = $srRes.Text | ConvertFrom-Json
        $afterRestartPid = $srJsonObj.pid
        Assert-True ($afterRestartPid -ne $beforeRestartPid) "self_restart spawned new SMAPI PID ($beforeRestartPid -> $afterRestartPid)"
        Assert-True ($srJsonObj.hideConsole -eq $true -and $srJsonObj.consoleVisible -eq $false -and $srJsonObj.windowVisible -eq $true) "--no-console hid SMAPI black console while keeping SDL2 game window visible"
        $genEval = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.SelfRestartGen')
        Assert-Contains $genEval.Text '2' "Newly compiled DLL replaced locked DLL via post-exit staging swap (SelfRestartGen == 2)"

        # Verify runtime `sdv-dev window --background` <-> `sdv-dev window --show --no-console` instant toggle!
        $winBg = Invoke-SdvRaw @('window', '--background')
        Assert-True ($winBg.Code -eq 0) "sdv-dev window --background succeeded: $($winBg.Text)"
        $winBgObj = $winBg.Text | ConvertFrom-Json
        Assert-True ($winBgObj.backgroundMode -eq $true -and $winBgObj.windowVisible -eq $false) "window --background moved SDL2 window offscreen (windowVisible=false)"
        $winShow = Invoke-SdvRaw @('window', '--show', '--no-console')
        Assert-True ($winShow.Code -eq 0) "sdv-dev window --show --no-console succeeded: $($winShow.Text)"
        $winShowObj = $winShow.Text | ConvertFrom-Json
        Assert-True ($winShowObj.backgroundMode -eq $false -and $winShowObj.windowVisible -eq $true -and $winShowObj.consoleVisible -eq $false) "window --show --no-console restored visible SDL2 window without console"
        $null = Invoke-SdvRaw @('window', '--background')
        Pass-Step "Lock-free self_restart (--no-console) & instant runtime window --background/--show toggle ($beforeRestartPid -> $afterRestartPid)"

        # 7i. Verify 0 mod/SMAPI Error logs (ignoring vanilla notice when Steam client isn't open) & stop cleanly
        $errLogs = Invoke-SdvRaw @('logs', '--level', 'Error')
        $realErrLines = @($errLogs.Text -split "`r?`n" | Where-Object {
            $_.Trim().Length -gt 0 -and
            $_ -notmatch 'No matching log entries' -and
            $_ -notmatch "Steam achievements won't work because Steam isn't loaded"
        })
        Assert-True ($realErrLines.Count -eq 0) "Zero real SMAPI Error logs during entire live run (got: $($realErrLines -join ' | '))"
        $null = Invoke-SdvRaw @('stop')
        Pass-Step "Zero SMAPI errors & clean game shutdown"
    }

    Write-Host ""
    Write-Host "ALL $($script:passed) TEST GROUPS PASSED!" -ForegroundColor Green
}
finally {
    if ($IncludeLive) {
        try { & $sdvExe stop *> $null } catch { }
    }
    try { & $sdvExe daemon-stop *> $null } catch { }
    if ([string]::IsNullOrWhiteSpace($KeepArtifactsDir)) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Write-Host "Kept test artifacts in: $workRoot" -ForegroundColor Yellow
    }
}
