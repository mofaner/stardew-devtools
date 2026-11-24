> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Data)

# Modding:Modder Guide/APIs/Data (`Modder_Guide/APIs/Data`)

The data API lets you store arbitrary data and retrieve it later. For example, you can use this to store player progression, custom items that can't be saved by the game, etc.

## Storage options
### JSON files
You can store data in arbitrary `.json` files in your mod folder. Note that these files will be lost if the player deletes them (*e.g.,* when updating your mod), so this is mainly useful for bundled files, cache files, etc.

<ol>
<li>Create your data model.</li>
<li>In your mod code, use the mod helper to read/write a named file. This example assumes you created a class named `ModData`, but you can use different names too.

```c#
// read file
var model = this.Helper.Data.ReadJsonFile<ModData>("data.json") ?? new ModData();

// save file (if needed)
this.Helper.Data.WriteJsonFile("data.json", model);
```

Note that `ReadJsonFile` will return `null` if the file doesn't exist. The above example will create a default instance if that happens; if you don't want to do that, just remove the `?? new ModData()` part.</li>
</ol>

By changing the file path you specify, you can...
- store JSON files in a subfolder, by specifying a path relative to your mod folder (like `"data/some-file.json"`). SMAPI will create the folders automatically if needed.
- create per-save JSON files by using the save ID in the name, like `$"data/{Constants.SaveFolderName}.json"`.

### Save data
You can store arbitrary data in the current save file. This is mainly useful for save-specific data, like player progression and custom items. This is subject to some restrictions: the save file must be loaded (*e.g.,* it won't work on the title screen), it can't be used by farmhands in multiplayer (you can check `Context.IsMainPlayer`), and it'll be discarded if the player exits without saving. If the data needs to be prepared prior to saving, the `GameLoop.Saving` event is a good time to do it.

<ol>
<li>Create your data model.</li>
<li>Choose your data key.</li>
<li>In your mod code, use the data API to read/write a named entry. This example assumes your data model is `ModData` and your key is `example-key`, but you can use different values.

```c#
// read data
var model = this.Helper.Data.ReadSaveData<ModData>("example-key");

// save data (if needed)
this.Helper.Data.WriteSaveData("example-key", model);
```

Note that `ReadSaveData` will return `null` if the data doesn't exist. Additionally, `ReadSaveData` is scoped to your mod, so you do not need to prepend with your mod's uniqueID</li>
</ol>

### Global app data
You can store arbitrary data on the local computer, synchronised by GOG/Steam if applicable. This data is global (not per-save) and changes are saved immediately.

<ol>
<li>Create your data model.</li>
<li>Choose your data key.</li>
<li>In your mod code, use the data API to read/write a named entry. This example assumes your data model is `ModData` and your key is `example-key`, but you can use different values.

```c#
// read data
var model = this.Helper.Data.ReadGlobalData<ModData>("example-key");

// save data (if needed)
this.Helper.Data.WriteGlobalData("example-key", model);
```

Note that `ReadGlobalData` will return `null` if the data doesn't exist.</li>
</ol>

## Data model
### Creating a data model
The *data model* is a C# class you create, with properties representing the data you want to store. It can contain almost anything from a few boolean fields to a complex object graph. Here's a simple data model:

```c#
public sealed class ModData
{
   public bool ExampleBoolean { get; set; }
   public int ExampleNumber { get; set; }
}
```

If you save that model to a JSON file, the file would look like this:

```json
{
   "ExampleBoolean": false,
   "ExampleNumber": 0
}
```

Only public properties and fields will be serialized, and your class needs a zero-parameter constructor. SMAPI uses NewtonSoft to serialize these models, so if you need finer-grained control over what gets serialized, use NewtonSoft annotations.

### Default values
You can optionally set default values in your data model:

```c#
class ModData
{
   public bool ExampleBoolean { get; set; } = true;
   public int ExampleNumber { get; set; } = 5;
}
```

...or set defaults with a constructor:

```c#
class ModData
{
   public bool ExampleBoolean { get; set; }
   public int ExampleNumber { get; set; }

   public ModData()
   {
      this.ExampleBoolean = true;
      this.ExampleNumber = 5;
   }
}
```

## Data key
A *data key* identifies your data, which lets you access the data again later. It should be unique within your mod, but there's no need to worry about conflicts with other mods (SMAPI will namespace the key internally). A data key can only contain letters, numbers, underscores, hyphens, or dots.

## Deletion
To remove an entry or file, just pass `null` as the data model. This works with any of the `Write*` methods:

```c#
// delete entry (if present)
this.Helper.Data.WriteSaveData<DataModel>("example-key", null);
```

## See also
### Mod data
You can also store custom data for individual game entities which have a `modData` dictionary field. That includes NPCs and players (`Character`), `GameLocation`, `Item`, and `TerrainFeature`. This is persisted to the save file and synchronized in multiplayer.

Usage notes:
- To avoid mod conflicts, prefixing data fields with your mod ID is strongly recommended (see the example below).
- When you split an item stack, the new stack copies the previous one's `modData` field; when merged into another stack, the merged items adopt the target stack's mod data. Otherwise mod data has no effect on item split/merge logic (*e.g.,* you can still merge items with different mod data).</li>

For example, this writes and then reads a custom 'age' value for an item:

```c#
// write a custom value
item.modData[$"{this.ModManifest.UniqueID}/item-age"] = "30";

// read it
if (item.modData.TryGetValue($"{this.ModManifest.UniqueID}/item-age", out string rawAge) && int.TryParse(rawAge, int age))
   ...
```

ru:Модификации:Руководство мододела/API/Данные
zh:模组:制作指南/APIs/Data
