> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Harmony)

# Modding:Modder Guide/APIs/Harmony (`Modder_Guide/APIs/Harmony`)

> Here be dragons. Thou art forewarned.

pardeike/Harmony Harmony lets you patch or replace methods, effectively rewriting the game code. SMAPI includes a copy of Harmony for mods to use.

## When to use Harmony
**Harmony should be a last resort.** It's a powerful tool that lets you do things that may be harder otherwise, but it comes with significant disadvantages:

- It's very easy to cause crashes, errors, or subtle bugs, including difficult-to-diagnose memory corruption errors.
- SMAPI often can't detect incompatible Harmony code.
- SMAPI often can't rewrite Harmony patches for compatibility, so the mod may break on other platforms (e.g. Android) or in future game updates.
- Patches may conflict with other Harmony mods, sometimes in ways that are difficult to troubleshoot.
- Patches may have unpredictable effects on other mods that aren't using Harmony. That may also irritate other modders, if players often report bugs to other mods due to your patches.
- Patches may prevent you from attaching a debugger when testing (even when you're testing unrelated code).
- If someone else's mod calls code you patched and that code crashes, the error will be logged under their mod's name (unless you handle errors). This can cause players to report bugs to other authors, which can be irritating if they know it's due to your mod.
- SMAPI will show a warning when your mod is loaded saying it may affect game stability.

You should only use Harmony if you're sure what you want isn't feasible without it. For example: instead of patching logic that handles player interaction, you can detect and suppress the interaction using SMAPI's input API, and run your own code to handle it.

## How to use it
**Harmony should be a last resort** (see the previous section).
<ol>
<li>Edit your mod's `.csproj` project file, and add this to the first `<PropertyGroup>` section:

```xml
<EnableHarmony>true</EnableHarmony>
```
</li>
<li>In your mod's `Entry` method, use Harmony's code API to register patches:

```c#
var harmony = new Harmony(this.ModManifest.UniqueID);

// example patch, you'll need to edit this for your patch
harmony.Patch(
   original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.canBePlacedHere)),
   prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.CanBePlacedHere_Prefix))
);
```
</li>
<li>See the [Harmony tutorials and documentation](https://harmony.pardeike.net/). Additionally, you can check out some Stardew-specific examples on the [Modding wiki](https://stardewmodding.wiki.gg/wiki/Tutorial:_Harmony_Patching).</li>
</ol>

## Best practices
<ol>
<li>See when to use Harmony.</li>
<li>Unhandled errors in a patch can be hard to troubleshoot, which often leads to players asking for help on the page for SMAPI or someone else's mod. **Please** handle errors, log them, and default to the original code. For example:

```c#
internal class ObjectPatches
{
    private static IMonitor Monitor;

    // call this method from your Entry class
    internal static void Initialize(IMonitor monitor)
    {
        Monitor = monitor;
    }

    // patches need to be static!
    internal static bool CanBePlacedHere_Prefix(StardewValley.Object __instance, GameLocation location, Vector2 tile, ref bool __result)
    {
        try
        {
            ...; // your patch logic here
            return false; // don't run original logic
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed in {nameof(CanBePlacedHere_Prefix)}:\n{ex}", LogLevel.Error);
            return true; // run original logic
        }
    }
}
```

</li>
<li>Use postfixes when possible for best compatibility and stability.</li>
<li>Don't use transpile patches unless you have no choice and you know what you're doing. This has a much higher chance of causing issues, is more fragile and likely to break in game updates, and makes it much less likely that other modders can help if you need it.</li>
<li>Use the code API instead of annotations like `[HarmonyPatch]` (see *Why should I avoid Harmony annotations and `PatchAll`?*).</li>
</ol>

## FAQs
### Which version of Harmony should I use?
The Harmony version is managed by SMAPI (currently 2.2.*x*). If you use the `EnableHarmony` option, you'll use the version bundled with SMAPI automatically.

### Why should I avoid Harmony annotations and `PatchAll`?
There are two ways to add patches:
- Use the code API (recommended).
- Use annotations like `[HarmonyPatch]` on static classes, then call Harmony's `PatchAll` to dynamically scan the assembly for those annotations. This is fragile and discouraged.

For context, SMAPI automatically rewrites mods when needed for compatibility. That's used to keep mods working when there are breaking changes in the game/SMAPI/Harmony, or to handle crossplatform differences (e.g. using PC mods on Android). For example, the Stardew Valley 1.5.5 update broke most C# mods, but nearly all of them were fixed automatically by SMAPI's rewriting.

However annotations work differently in the compiled code, so SMAPI can't rewrite them. That means mods which use annotations are more fragile; they may break without warning in future game/SMAPI/Harmony updates, and may not work on some platforms.

### What is inlining and why does it matter?
[Inlining](https://en.wikipedia.org/wiki/Inline_expansion) is when the JIT takes a method call and sticks the body of the method called instead of emitting an actual call. If this happens, a harmony patch applied to the callee will not take effect.

The heuristics the JIT uses to decide whether or not to inline a method are not documented and are subject to change, but [small, simple methods that do not throw exceptions](https://stackoverflow.com/a/31467000/19366602) are the most likely to be inlined. Additionally, the heuristic seems to change between platform and platform, with mac being more aggressive than Windows.

ru:Модификации:Руководство мододела/API/Harmony
zh:模组:制作指南/APIs/Harmony
