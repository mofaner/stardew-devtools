> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Content_Patcher)

# Modding:Content Patcher (`Content_Patcher`)

Do you want to create Content Patcher packs for Stardew Valley? This page is for you! **For using mods, see Modding:Player Guide. For creating other mod types, see Modding:Index#Creating mods.**

## Quick start
This page is meant as a gentle introduction to creating Content Patcher packs. If you don't need an introduction, see the [full Content Patcher readme](https://github.com/Pathoschild/StardewMods/tree/develop/ContentPatcher#readme).

## Basic concepts
### What is Content Patcher?
1915 Content Patcher is a SMAPI mod which lets you change the game assets (images, dialogue, data, and maps) without replacing game files or writing code. You use it by creating a content pack (basically a folder) with a couple of JSON files (basically text). Just by editing a JSON file, you can...

- replace one image file;
- make seasonal changes;
- make dialogue that changes based on the weather, date, your relationships with other NPCs, etc;
- make very specific changes (like coffee is more expensive on winter weekends when it's snowing after you've completed the JojaMart);
- and much more.

### Assets
An *asset* is essentially a file in the game's `Content` folder with a unique *asset name*. The asset name never includes the `Content` path, language, or file extension (you can use tokens to target specific languages). For example:

| file | asset name |
| --- | --- |
| `Content/Portraits/Abigail.xnb` | `Portraits/Abigail` |
| `Content/Maps/spring_beach.xnb`; `Content/Maps/spring_beach.es-ES.xnb`; `Content/Maps/spring_beach.fr-FR.xnb` | `Maps/spring_beach` |

An asset may contain multiple sprites or data entries. For example, here's what `Portraits/Abigail` contains if you unpack it:

So if you wanted to change Abigail's portraits, you would use Content Patcher to load or edit `Portraits/Abigail`.

### Load vs edits
There are two conceptual ways you can change an asset:

- *Load* the initial version of an asset. Each asset can only be loaded by one mod at the same time. This is mainly useful for total replacement mods (like a mod that completely changes an NPC's portraits), or to provide files that don't exist in the `Content` folder.
- *Edit* an asset after it's loaded. Any number of edits can be applied to the same asset.

For example, let's say the game needs Abigail's portraits. This is how changes are applied:

```text
                                           ┌────────────┐
                                           │ edit asset │
                         ┌────────────┐    ├────────────┤
get Portraits/Abigail ──>│ load asset │───>│ edit asset │──> portrait asset
                         └────────────┘    ├────────────┤
                                           │ edit asset │
                                           └────────────┘
```

This is divided into four main action types (`Load`, `EditData`, `EditImage`, `EditMap`), which are explained in more detail in the Content Patcher readme (see below).

## Get started
### Intro to JSON
You'll notice a lot of files with `.json` at the end of the name when creating mods for Stardew Valley. That means they're formatted as JSON, which is just a way of writing text that's readable to code. If you haven't used JSON before, reading *[An Introduction to JSON](https://towardsdatascience.com/an-introduction-to-json-c9acb464f43e)* first will be very helpful to understand what the files are doing.

### Create example mod
First let's get our basic content pack up and running:
<ol>
<li>Install [SMAPI](https://smapi.io/) and 1915 Content Patcher.</li>
<li>Unpack the game's `Content` folder so you can see what each asset contains (see Modding:Editing XNB files#Unpack game files).</li>
<li>Create a SMAPI content pack per step 3 of the general Create a Content Pack page.</li>
<li>Create a `content.json` file in the same folder with this content:

    "Changes": [
    ]

</li>
<li>Launch the game.</li>
</ol>

If you did everything correctly so far, you should see the new mod under "Loaded X content packs" in the SMAPI console. (If not, review the above steps or ask for help.)

### Content format
The `content.json` file you created above is what tells Content Patcher what to change. This has two main fields:

- `Format`: the format version. You should always use the latest version (currently {{Content Patcher version}}) to enable the latest features and avoid obsolete behavior.
- `Changes`: the changes you want to make. Each entry is called a *patch*, and describes a specific action to perform: replace this file, copy this image into the file, etc. You can list any number of patches.

You can list any number of patches in the `Changes` field, each surrounded by `{` and `}`. See the next section for more info, but here's a quick example:

    "Changes": [
        {
            "Action": "Load",
            "Target": "Animals/Dinosaur",
            "FromFile": "assets/dinosaur.png"
        },
        {
            "Action": "EditImage",
            "Target": "Maps/springobjects",
            "FromFile": "assets/fish-object.png"
        }
    ]

(There are other fields like `ConfigSchema` and `DynamicTokens` for more advanced usage; these are covered in the full readme.)

## Next steps
You've created a Content Patcher pack! 

For help making it do something, see...
- [Content Patcher readme](https://github.com/Pathoschild/StardewMods/tree/develop/ContentPatcher#readme) for the full reference;
- [video intro to Content Patcher](https://www.youtube.com/watch?v=uqRTgjWvDYs) (unofficial);
- [intro to converting XNB mods](https://docs.google.com/presentation/d/1OBIJSNOwEA2sdBzNbUiVUQni-ajABGFmL-FUanhuLvk) (unofficial).

## Examples
(We'll have a guided tutorial here soon.)

### Change horse/pet icons
For edits to replace the look of horses and/or pets (cats and dogs), you can add these to your content.json in order to also replace the little head icon in the inventory menu:

For horses:

```javascript
//horse head in inventory
{
    "Action": "EditImage",
    "Target": "LooseSprites/Cursors",
    "FromFile": "yourfile.png",
    "FromArea": { insert values here }, 
    "ToArea": { "X": 192, "Y": 192, "Width": 16, "Height": 16 }
}
```

For dogs:

```javascript
//dog head in inventory

"ToArea": { "X": 208, "Y": 208, "Width": 16, "Height": 16 }, //Dog 1

"ToArea": { "X": 224, "Y": 208, "Width": 16, "Height": 16 }, //Dog 2

"ToArea": { "X": 240, "Y": 208, "Width": 16, "Height": 16 }, //Dog 3
```

For cats:

```javascript
//cat head in inventory

"ToArea": { "X": 160, "Y": 208, "Width": 16, "Height": 16 }, //Cat 1 

"ToArea": { "X": 176, "Y": 208, "Width": 16, "Height": 16 }, //Cat 2 

"ToArea": { "X": 192, "Y": 208, "Width": 16, "Height": 16 }, //Cat 3 
```

es:Modding:Content Patcher
fr:Modding:Content Patcher
pt:Modificações:Content Patcher
ru:Модификации:Content Patcher
zh:模组:Content Patcher
