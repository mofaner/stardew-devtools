> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Mod_structure)

# Modding:Modder Guide/APIs/Mod structure (`Modder_Guide/APIs/Mod_structure`)

**See Modding:Modder Guide for an introduction to creating a SMAPI mod. This is a technical reference page which assumes you've already followed that guide.**

## Basic structure
A SMAPI mod must have...
# a compiled DLL file which contains the code to run as part of the mod (see *mod entry* below);
# a `manifest.json` file which describes the mod.

It may optionally have...
# any number of compiled `.dll` and `.pdb` files referenced by the main mod DLL;
# an `i18n` folder containing translations;
# any number of custom files used by the mod code (usually in an `assets` folder by convention).

## Mod entry
The main mod project must have a subclass of `StardewModdingAPI.Mod`. The class is often named `ModEntry` by convention, but you can use any valid C# name. The class must implement an `Entry` method, which SMAPI will call once the mod is loaded. The `helper` argument provides access to nearly all SMAPI APIs (available as `this.Helper` in other methods).

Here's the minimum possible implementation (which does nothing at all):

```c#
using StardewModdingAPI;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        
    }
}
```

The `Entry` method is called very early in the launch process, so some things aren't initialised yet. You can use events like `GameLaunched`, `SaveLoaded`, or `DayStarted` to access all features. Here's a quick summary of using APIs in `Entry`:

| feature | status |
| --- | --- |
| most SMAPI APIs | ✓ available. That includes registering event handlers, reading configuration, loading assets, and fetching mod info. |
| `helper.ModRegistry.GetApi` | ✖ Not available, since some mods aren't initialised yet. |
| Game fields | ✖ Not reliably available, since mods are loaded early in the process. That includes core fields like `Game1.objectInformation`. |

## Dependencies
Most mods are self-contained and only have one DLL file, but that's not a hard limit. Here are some common patterns:

- Declare a dependency on another mod, and SMAPI will load the other mod before yours and show a friendly error if it's not installed. You can use a mod-provided API if the mod has one, or reference the DLL directly. (If you reference a mod DLL, make sure it's *not* [marked 'copy local'](https://msdn.microsoft.com/en-us/library/t1zz5y8c(v=vs.100).aspx) to avoid issues; SMAPI will load the installed version instead.)
- [Create a shared project](https://docs.microsoft.com/en-us/xamarin/cross-platform/app-fundamentals/shared-projects) and reference it from your mod project. The shared project's code will be compiled as part of your mod project (so it'll be one DLL containing both projects in the mod folder).
- Reference projects/DLLs and copy their DLLs into your mod folder. SMAPI will detect the reference and load the DLLs from your mod folder automatically. **Don't do this for referencing a mod** — that can cause confusing errors when multiple versions are available (see the first option instead).

es:Modding:Guía del Modder/APIs/Mod structure
ru:Модификации:Руководство мододела/API/Структура мода
zh:模组:制作指南/APIs/Mod structure
