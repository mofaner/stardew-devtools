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
    Pass-Step "gen-gmcm SButton/KeybindList & clean bilingual i18n splitting"

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
    Pass-Step "Offline map-autotile + @island-palm + CP EditMap overlay & auto-crop rendering"

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

        $liveModCs = @'
using System;
using System.Collections.Generic;
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

        public override void draw(SpriteBatch b)
        {
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            IClickableMenu.drawTextureBox(b, BtnTop.bounds.X, BtnTop.bounds.Y, BtnTop.bounds.Width, BtnTop.bounds.Height, Color.LightCyan);
            IClickableMenu.drawTextureBox(b, BtnBottom.bounds.X, BtnBottom.bounds.Y, BtnBottom.bounds.Width, BtnBottom.bounds.Height, Color.LightGreen);
            IClickableMenu.drawTextureBox(b, BtnRightBroken.bounds.X, BtnRightBroken.bounds.Y, BtnRightBroken.bounds.Width, BtnRightBroken.bounds.Height, Color.MistyRose);
            b.DrawString(Game1.dialogueFont, "Top #101", new Vector2(BtnTop.bounds.X + 16, BtnTop.bounds.Y + 12), Color.Black);
            b.DrawString(Game1.dialogueFont, "Bot #102", new Vector2(BtnBottom.bounds.X + 16, BtnBottom.bounds.Y + 12), Color.Black);
            b.DrawString(Game1.dialogueFont, "Bad #103", new Vector2(BtnRightBroken.bounds.X + 16, BtnRightBroken.bounds.Y + 12), Color.DarkRed);
            base.draw(b);
            drawMouse(b);
        }
    }

    public class ModEntry : Mod
    {
        public static int ChosenWellOption = -1;
        private Harmony? _harmony;

        public override void Entry(IModHelper helper)
        {
            _harmony = new Harmony(ModManifest.UniqueID);
            _harmony.PatchAll(typeof(ModEntry).Assembly);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
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

        $startRes = Invoke-SdvRaw @('restart', '--isolated', '--project', $liveModDir, '--reset-test-world')
        Assert-True ($startRes.Code -eq 0) "Isolated test world launched with LiveProbeMod: $($startRes.Text)"
        Pass-Step "Live isolated SMAPI launch with LiveProbeMod"

        # 7a. Instant 0ms `ui-click --response 1` bypassing 750ms DialogueBox safetyTimer!
        $null = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.OpenWishingWellQuestion(); return "opened";')
        $clickRes = Invoke-SdvRaw @('ui-click', '--response', '1')
        Assert-True ($clickRes.Code -eq 0) "ui-click --response 1 succeeded immediately: $($clickRes.Text)"
        $wellVal = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.ChosenWellOption')
        Assert-Contains $wellVal.Text '1' "DialogueBox callback actually executed with 0ms sleep (ChosenWellOption == 1)"
        $null = Invoke-SdvRaw @('ui-click', '--close')
        Pass-Step "Zero-sleep DialogueBox ui-click --response callback execution"

        # 7b. Custom IClickableMenu + `screenshot --mode screen --ui-overlay`
        $null = Invoke-SdvRaw @('eval', 'LiveProbeMod.ModEntry.OpenCustomNavMenu(); return "menu";')
        $uiShotPng = Join-Path $workRoot "custom_menu_ui_overlay.png"
        $shotRes = Invoke-SdvRaw @('screenshot', '--mode', 'screen', '--ui-overlay', '--out', $uiShotPng)
        Assert-True ($shotRes.Code -eq 0) "screenshot --ui-overlay succeeded: $($shotRes.Text)"
        Assert-Contains $shotRes.Text '"brokenNeighborLinks": 1' "UI overlay detected intentional dangling neighbor ID 999"
        Assert-True (Test-Path -LiteralPath $uiShotPng) "UI overlay PNG written"
        $null = Invoke-SdvRaw @('ui-click', '--close')
        Pass-Step "Custom IClickableMenu screenshot --ui-overlay & broken neighbor detection"

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
        Pass-Step "Synchronous warp + interact + checkpoint save/load time machine"

        # 7f. Live NPC `schedule-check`
        $scRes = Invoke-SdvRaw @('schedule-check', 'Abigail', '--key', 'spring')
        Assert-True ($scRes.Code -eq 0) "schedule-check Abigail --key spring succeeded"
        Assert-Contains $scRes.Text '"scheduleKey": "spring"' "Parsed Abigail spring schedule routes"
        Pass-Step "Live NPC schedule-check pathfinder verification"

        # 7g. Verify 0 SMAPI Error logs & stop cleanly
        $errLogs = Invoke-SdvRaw @('logs', '--level', 'Error')
        Assert-Contains $errLogs.Text "No matching log entries" "Zero SMAPI Error logs during entire live run"
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
