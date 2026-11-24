> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest)

# Modding:Modder Guide/APIs/Manifest (`Modder_Guide/APIs/Manifest`)

Every SMAPI mod or content pack must have a `manifest.json` file in its folder. SMAPI uses this to identify and load the mod, perform update checks, etc. The manifest info is also available to any mod in code (using `this.ModManifest` for the current mod's manifest, or mod registry for other mods' manifests).

## Basic examples
Here's the basic format (see below for details on each field):

| For a SMAPI mod | For a content pack |
| --- | --- |
| 
```javascript
{
    "Name": "Your Project Name",
    "Author": "your name",
    "Version": "1.0.0",
    "Description": "One or two sentences about the mod.",
    "UniqueID": "YourName.YourProjectName",
    "EntryDll": "YourDllFileName.dll",
    "UpdateKeys": []
}
```
 | 
```javascript
{
    "Name": "Your Project Name",
    "Author": "your name",
    "Version": "1.0.0",
    "Description": "One or two sentences about the mod.",
    "UniqueID": "YourName.YourProjectName",
    "UpdateKeys": [],
    "ContentPackFor": {
        "UniqueID": "Pathoschild.ContentPatcher"
    }
}
```
 |

## Fields
### Basic fields
All mods should specify the following fields.

| field | description |
| --- | --- |
| `Name` | The mod name. SMAPI uses this in player messages, logs, and errors. Example: 
```javascript
"Name": "Lookup Anything"
```
 |
| `Author` | The name of the person who created the mod. Ideally this should include the username used to publish mods. Example: 
```javascript
"Author": "Pathoschild"
```
 |
| `Version` | The mod's [semantic version](http://semver.org/). Make sure you update this for each release! SMAPI uses this for update checks, mod dependencies, and compatibility blacklists (if the mod breaks in a future version of the game). For example: 
```javascript
"Version": "1.0.0"
```
 If you set the `<Version>` in your `.csproj` project file, you can use the exact text `%ProjectVersion%` in the manifest file to set the same version automatically: 
```javascript
"Version": "%ProjectVersion%"
```
 |
| `Description` | A short explanation of what your mod does (one or two sentences), shown in the SMAPI log. Example: 
```javascript
"Description": "View metadata about anything by pressing a button."
```
 |
| `UniqueID` | A unique identifier for your mod. The recommended format is `<your name>.<mod name>`, with no spaces or special characters. SMAPI uses this for update checks, mod dependencies, and compatibility blacklists (if the mod breaks in a future version of the game). When another mod needs to reference this mod, it uses the unique ID. For this reason, once you've published your mod, do not change this unique ID in future versions of the same mod. Example: 
```javascript
"UniqueID": "Pathoschild.LookupAnything"
```
 |
| `EntryDll` **or** `ContentPackFor` | <p>All mods must specify either `EntryDll` (for a SMAPI mod) or `ContentPackFor` (for a content pack). These are mutually exclusive — you can't specify both.</p> For a SMAPI mod, `EntryDll` is the mod's compiled DLL filename in its mod folder. Example: 
```javascript
"EntryDll": "LookupAnything.dll"
```
 For a content pack, `ContentPackFor` specifies which mod can read it. The `MinimumVersion` is optional. Example: 
```javascript
"ContentPackFor": {
   "UniqueID": "Pathoschild.ContentPatcher",
   "MinimumVersion": "1.0.0"
}
```
 |

### Minimum SMAPI or game version
The `MinimumApiVersion` and `MinimumGameVersion` field sets the minimum version of SMAPI or Stardew Valley needed to use this mod. If a player tries to use the mod with an older version, they'll see a friendly message saying they need to update it. 

For example:

```javascript
"MinimumApiVersion": "4.0.0"
```

### Dependencies
The `Dependencies` field specifies other mods required to use this mod. If a player tries to use the mod and the dependencies aren't installed, the mod won't be loaded and they'll see a friendly message saying they need to install those. For example:

```javascript
"Dependencies": [
   {
      "UniqueID": "SMAPI.ConsoleCommands",
      "MinimumVersion": "3.8.0" // optional. If specified, older versions won't meet the requirement.
   }
]
```

You can mark a dependency as optional. It will be loaded first if it's installed, otherwise it'll be ignored.

```javascript
"Dependencies": [
   {
      "UniqueID": "SMAPI.ConsoleCommands",
      "IsRequired": false
   }
]
```

### Update checks
SMAPI can detect new versions of your mod and alert the user with a link to your mod page. You can enable this by setting the `UpdateKeys` field in your `manifest.json`, which tells SMAPI where to check.

See *update checks* for more information.

## Specialized fields
**⚠️ Most mods shouldn't need to use these fields.**

### Anything else
Any other fields will be stored in the `IManifest.ExtraFields` dictionary, which is available through the mod registry. Extra fields are ignored by SMAPI, but may be useful for extended metadata intended for other mods.

es:Modding:Guía del Modder/APIs/Manifest
ru:Модификации:Руководство мододела/API/Манифест
tr:Modlama:Mod Rehberi/API'ler/Manifest
zh:模组:制作指南/APIs/Manifest
