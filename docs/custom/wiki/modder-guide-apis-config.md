> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Config)

# Modding:Modder Guide/APIs/Config (`Modder_Guide/APIs/Config`)

You can let users configure your mod through a standard `config.json` file. SMAPI will automatically create the file and take care of reading, normalising, and updating it.

## Config model
### Creating a config model
The *config model* is a C# class you create, with properties representing the settings you want to store. It can contain almost anything from a few boolean fields to a complex object graph (though you should try to keep things simple for players). Here's a simple config model:

```c#
public sealed class ModConfig
{
   public bool ExampleBoolean { get; set; }
   public int ExampleNumber { get; set; }
}
```

That model would be saved to `config.json` with this content:

```json
{
   "ExampleBoolean": false,
   "ExampleNumber": 0
}
```

These properties must be public.

### Default values
You can set default values in your data model:

```c#
public sealed class ModConfig
{
   public bool ExampleBoolean { get; set; } = true;
   public int ExampleNumber { get; set; } = 5;
}
```

...or set defaults with a constructor:

```c#
public sealed class ModConfig
{
   public bool ExampleBoolean { get; set; }
   public int ExampleNumber { get; set; }

   public ModConfig()
   {
      this.ExampleBoolean = true;
      this.ExampleNumber = 5;
   }
}
```

## Using the config file
To read the `config.json` (SMAPI will create it automatically):

<ol>
<li>Create your config model.</li>
<li>Access the config values in your `ModEntry` class:

```c#
/// <summary>The main entry point for the mod.</summary>
internal sealed class ModEntry : Mod
{
    /*********
    ** Properties
    *********/
    /// <summary>The mod configuration from the player.</summary>
    private ModConfig Config;

    /*********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        this.Config = this.Helper.ReadConfig<ModConfig>();
        bool exampleBool = this.Config.ExampleBoolean;
    }
}
```

</li>
</ol>

That's it! When the player launches the game, SMAPI will create the `config.json` file automatically if it doesn't exist yet, using the default config options you provided in your model. If you need to save some changes, you can use `this.Helper.WriteConfig(this.Config)`.

Note that `ReadConfig` will raise an exception if the user does not provide a valid JSON.

## Keybind settings
  Modding:Modder Guide/APIs/Input

You can use SMAPI's `KeybindList` in your model to let users configure keybinds. This automatically supports multi-key or alternative bindings (*e.g.,* to support split-screen mode):

```c#
class ModConfig
{
   public KeybindList ToggleKey { get; set; } = KeybindList.Parse("LeftShift + F2, LeftTrigger");
}
```

The value is automatically written/parsed in the `config.json` file as a string:

```json
{
   "ToggleKey": "LeftShift + F2, LeftTrigger"
}
```

ru:Модификации:Руководство мододела/API/Конфигурация
zh:模组:制作指南/APIs/Config
