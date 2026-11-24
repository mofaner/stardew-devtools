> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Content_packs)

# Modding:Content packs (`Content_packs`)

←Index

A **content pack** is a collection of files loaded by a specific content pack framework (essentially a sub-mod). Content packs can contain any files, but usually consist of JSON files and images.

## For players
If you want to use a content pack, install it just like a SMAPI mod (see player guide). Make sure you install the mod it needs too.

## For modders
### Create a content pack
# Decide which content pack framework you'll use (each framework defines the particular files you need).
# Install SMAPI.
# Create the generic content pack format (identical for all frameworks):
## Open your game's `Mods` folder (located in your game folder).
## Add a subfolder with the name you want (see folder naming convention).
## In the subfolder, add a `manifest.json` file with the `ContentPackFor` field (see manifest examples and format).
# Add the files needed by the content pack framework (see its mod page for details).
# Launch the game, and make sure your new content pack appears under "Loaded X content packs" in the SMAPI console.

### Release your content pack
# Add an update key to your manifest (if you haven't already).
# Update the `Version` field in the manifest. (Increase it for each release! SMAPI will use it for update and compatibility checks.)
# Create a `.zip` file containing the content pack's folder.
# Upload that file to [Nexus Mods](http://www.nexusmods.com/stardewvalley).

In your mod description, providing clear install steps will help reduce support questions. Example BBCode:

```text
[size=5]Install[/size]
[list=1]
[*][url=https://smapi.io]Install the latest version of SMAPI[/url].
[*][url=<url of required mod>]Install <name of required mod>[/url].
[*]Download this mod and unzip it into [font=Courier New]Stardew Valley/Mods[/font].
[*]Run the game using SMAPI.
[/list]
```

### Consume a content pack
If you're writing a SMAPI mod which will read content packs, see Modding:Modder Guide/APIs/Content Packs.

## Conventions
These are recommended practices, but they're not required.

### Folder name
The folder name should use upper camel case, with an acroynm prefix in square brackets showing which mod it's for. For example, a folder named `[CP] SampleName` is a content pack for Content Patcher.

See Modding:Content pack frameworks for the common acronyms.

### Folder structure
The folder should contain your `manifest.json`, the `content.json` (or the correct json for your framework), and an `assets` folder that contains any other files your mod uses.  This may include `.png` files, other `.json` files, and even or `.tmx/.tbin` files, among others. The assets folder may use further subfolders if you wish, though it is not required.

pt:Modificações:Pacotes de conteúdo
ru:Модификации:Контент-паки
zh:模组:内容包
