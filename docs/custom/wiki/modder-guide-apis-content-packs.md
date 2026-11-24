> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs)

# Modding:Modder Guide/APIs/Content Packs (`Modder_Guide/APIs/Content_Packs`)

A **content pack** is a special type of mod that isn't run by SMAPI directly, but contains files your own mod can read.

Note that a custom content pack format is not required to provide integration with content mods; the most common method is to provide a custom game asset that other mods (including Content Patcher packs) can edit. The advantage of custom assets is that both other C# mods and Content Patcher packs can edit them via the content pipeline, with the latter benefiting from convenient features such as tokens, conditional loading and targeted edits, removing the need to implement similar features in your mod.

See the wiki page for loading and editing content, as well as the external tutorial for [making framework mods](https://stardewmodding.wiki.gg/wiki/Tutorial:_Making_Framework_Mods) for more info. The rest of this page details instructions for defining custom content packs formats.

## Content pack format
A content pack is a folder containing a `manifest.json` file which specifies your mod in its `ContentPackFor` field (see manifest). The format beyond that is up to you to define. For example, 1915 Content Patcher requires a `content.json` file, but that's not validated by SMAPI itself. Instead, SMAPI provides an API you can use to read any other file you need from the content pack.

See Modding:Content packs for more info about content packs.

## Content pack API
### Get owned content packs
To get the content packs installed for your mod (including a `Manifest` field with the content pack's full manifest info):

```c#
foreach(IContentPack contentPack in this.Helper.ContentPacks.GetOwned())
{
   this.Monitor.Log($"Reading content pack: {contentPack.Manifest.Name} {contentPack.Manifest.Version} from {contentPack.DirectoryPath}");
}
```

Content packs are listed in load order, so they're already sorted for dependencies. For example, if content pack B requires A, they'll be listed in the order A → B.

As a note, this can be done as early as ModEntry, although mods often choose to perform this step in the GameLoad or SaveLoaded event. 

### Check if a file exists
You can check if a file exists in the content pack folder using `HasFile`. You can specify a relative path like `data/content.json`.

```c#
if (!contentPack.HasFile("content.json"))
{
   // show 'required file missing' error
}
```

It is often a good idea to alert the player (and modders who may be using your mod) if the file is missing on deployment.

### Read a JSON file
You can read a JSON file from a content pack folder into a data model (`YourDataModel` in the example below) using `ReadJsonFile`. You can specify a relative path like `data/content.json`. This will return `null` if the file doesn't exist.

```c#
YourDataModel data = contentPack.ReadJsonFile<YourDataFile>("content.json");
```

### Write a JSON file
You can write a JSON file in a content pack folder using `WriteJsonFile`, using a data model (`YourDataModel` in the example below). You can specify a relative path like `data/content.json` (subdirectories will be created automatically if needed). This will create the file if it doesn't exist, or overwrite it if it does.

**Note:** this is best used for generated files. Overwriting the original files isn't recommended, since a bug in your mod which changes an original file might permanently break the content pack and require the player to reinstall it.

```c#
YourDataModel data = new YourDataModel();
contentPack.WriteJsonFile("content.json", data);
```

### Read content asset
You can read a content asset from the content pack folder using `LoadAsset`. You can optionally specify a relative path to read from a subfolder.

```c#
Texture2D image = contentPack.ModContent.Load<Texture2D>("image.png");
```

If you need to pass an asset name to game code, you can use the `GetActualAssetKey` method:

```c#
tilesheet.ImageSource = contentPack.GetActualAssetKey("image.png");
```

### Get translations
You can read translations from the content pack's `i18n` folder. This is identical to the translation API, but accessed through `contentPack.Translations`:

```c#
string text = contentPack.Translation.Get("item-type.label");
```

### Create a fake content pack
In specialised cases, you can create a temporary content pack for a given directory path:

```c#
// create with random manifest data
IContentPack contentPack = this.Helper.ContentPacks.CreateFake(
   directoryPath: Path.Combine(this.Helper.DirectoryPath, "content-pack"),
);

// create with given manifest fields
IContentPack contentPack = this.Helper.ContentPacks.CreateTemporary(
   directoryPath: Path.Combine(this.Helper.DirectoryPath, "content-pack"),
   id: Guid.NewGuid().ToString("N"),
   name: "temporary content pack",
   description: "...",
   author: "...",
   version: new SemanticVersion(1, 0, 0)
);
```

ru:Модификации:Руководство мододела/API/Контент-паки
zh:模组:制作指南/APIs/Content Packs
