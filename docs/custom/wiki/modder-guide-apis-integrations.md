> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Integrations)

# Modding:Modder Guide/APIs/Integrations (`Modder_Guide/APIs/Integrations`)

A *mod integration* refers to two mods communicating or cooperating in some way. SMAPI provides a few features to support this.

## Dependencies
You can define dependencies in your `manifest.json` file, which are other mods that must be loaded before yours. If the dependency is required and missing, SMAPI will show a friendly error to the player. See the manifest page for more info.

## Mod registry
Your mod can get information about loaded mods, or check if a particular mod is loaded. (All mods are loaded by the time your mod's `Entry(…)` method is called.)

```c#
// check if a mod is loaded
bool isLoaded = this.Helper.ModRegistry.IsLoaded("UniqueModID");

// get info for a mod
IModInfo mod = this.Helper.ModRegistry.Get("UniqueModID");
bool isContentPack = mod.IsContentPack;
IManifest manifest = mod.Manifest; // name, description, version, etc

// get info for all loaded mods
foreach(IModInfo mod in this.Helper.ModRegistry.GetAll()) { … }
```

## Mod-provided APIs
Mods can provide their own APIs to other mods, even without a dependency or assembly reference. This can be used to integrate mods, provide custom information, or provide new framework APIs beyond those offered by SMAPI itself.

### Providing an API
To provide an API for other mods to use:
<ol>
<li>Add a normal class to your mod project with the methods and properties you want to expose.

```c#
public class YourModApi
{
    public string ExampleProperty { get; set; } = "Example value";

    public bool GetThing(string example)
    {
       return true;
    }
}
```

(You can use a [constructor](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/constructors) to initialise the API if desired.)</li>
<li>Override `GetApi` in your mod's entry class and return an instance of your API. You can choose between two versions of this method:
<ul>
<li>Override `GetApi()` to provide one instance of the API to all mods. This will only be called once, and SMAPI will cache the mod instance.

```c#
   public override object GetApi()
   {
      return new YourModApi();
   }
```

</li>
<li>*Or* Override `GetApi(IModInfo mod)` to provide one instance per mod. This will be called once for each mod that requests an API.

Note that this info is provided for informational purposes only (e.g. to log errors). Denying API access to specific mods is strongly discouraged and may be considered abusive.

```c#
   public override object GetApi(IModInfo mod)
   {
      return new YourModApi(mod.Manifest);
   }
```

</li>
</ul>
</li>
</ol>

That's it! SMAPI will get one instance of your API and cache it.

Notes:
- `GetApi` is always called after `Entry`, so it's safe to pass in your mod's initialised fields.
- Be careful when changing your API! If you make a breaking change, other mods may need an update before they can access your API again.
- You can optionally add a public [interface](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface) to your API. If a mod directly references your mod DLL, they'll be able to use your interface instead of creating their own.

### Using an API
You can use a mod-provided API by mapping it to an [interface](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface):
<ol>
<li>Create an interface with only the properties and methods you want to access. This interface must be public and cannot be an inner interface. (If you directly reference the mod DLL and it provides a public API interface, you can use that instead.)

```c#
public interface ISomeModApi
{
   bool GetThing(string example);
}
```
</li>
<li>In your mod code (after `Entry`), you can get the API by specifying the interface you created in step 1, and the mod's unique ID:

```c#
ISomeModApi api = helper.ModRegistry.GetApi<ISomeModApi>("other-mod-ID");
if (api != null)
   bool result = api.GetThing("boop");
```
</li>
</ol>

For a quick integration, you can also use reflection instead of creating an interface:

```c#
object api = helper.ModRegistry.GetApi("other-mod-id");
if (api != null)
   bool result = helper.Reflection.GetMethod(api, "GetThing").Invoke<bool>("boop");
```

Notes:
- You can't call `GetApi` until all mods are initialised and their `Entry` methods called. You can use the `GameLoop.GameLaunched` event if you need to access mod APIs early; this is guaranteed to happen after all mods are initialised.
- You should always null-check APIs you consume. `GetApi` will return `null` if the API isn't available (*e.g.,* because the mod isn't already installed or doesn't have one). If `GetApi` cannot map an API, it will throw an exception.
- Keep in mind that mods may change their API interfaces over time; it may be useful to check the version of the other mod before trying to map the interface.

### Known limitations
- When providing an API, the interface and implementation must be public.

## Message sending
You can send messages using the multiplayer API, with a destination ranging from very narrow (*e.g.,* one mod on one connected computer) to very broad (all mods on all computers). Messages can also be sent to the current computer, *e.g.,* to communicate between two mods. This can be used for a variety of integrations.

For example:
- Request something from a host mod. (Tractor Mod uses this to summon a tractor to the current player in multiplayer, even if the tractor isn't in a location synced to that player.)
- Notify another mod about something. (Chests Anywhere uses this to notify Automate when a chest's automation options are edited.)

## Shared assembly reference
*NOTE*: This is not a SMAPI feature, but this is documented here since it is also a common pattern for providing mod integration.

You can add a reference to a mod's assembly in your `csproj` and directly import its namespaces and functions into your mod code. Note that this should only be attempted if what you want isn't available through the mod's public API, or if this is the target mod's intended method of exposing its own. This would also make said mod a hard dependency for your own mod.

For example, this snippet adds an assembly reference to Content Patcher's assembly in your mod code under the `ContentPatcher` namespace, without also including `ContentPatcher.dll` in your mod build:

```xml
<ItemGroup>
    <Reference Include="ContentPatcher" HintPath="$(GamePath)\Mods\ContentPatcher\ContentPatcher.dll" Private="false" />
</ItemGroup>
```

Make sure the path to `ContentPatcher.dll` is correct on your own installation.

ru:Модификации:Руководство мододела/API/Интеграции
zh:模组:制作指南/APIs/Integrations
