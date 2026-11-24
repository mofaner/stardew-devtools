> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Trinkets)

# Modding:Trinkets (`Trinkets`)

← Items

This page explains how the game stores and parses trinket-type item data. For items in general, see Modding:Items.

## Overview
Trinkets are items that can be equipped in the player's trinket slot to enable special effects.

They have item type `(TR)` (or `ItemRegistry.type_trinket` in C# code), their data in `Data/Trinkets`, their vanilla icon sprites in `TileSheets\Objects_2`, and their code in `StardewValley.Objects.Trinkets.Trinket`.

## Data format
The trinket data in `Data/Trinkets` consists of a string → model lookup, where...
- The key is the unqualified item ID and internal name.
- The value is a model with the fields listed below.

| field | purpose |
| --- | --- |
| `DisplayName`; `Description` | A tokenizable string for the item's in-game display name and description. |
| `Texture` | The asset name for the texture containing the item's sprite. |
| `SheetIndex` | The sprite's index within the `Texture`, where 0 is the top-left sprite. |
| `TrinketEffectClass` | The C# `TrinketEffect` subclass which implements the trinket behavior. This can safely be a mod class, since it's not written to the save file. This should be the full assembly-qualified name in the form `namespace.class, assembly`. For example: `StardewValley.Objects.Trinkets.CompanionTrinketEffect, StardewValley`. |
| `DropsNaturally` | *(Optional)* Whether this trinket can be spawned randomly (e.g. in mine treasure chests). Default true. |
| `CanBeReforged` | *(Optional)* Whether players can re-roll this trinket's stats using an anvil. This assumes that the `TrinketEffectClass` implements the `GenerateRandomStats` method. Default true. |
| `CustomFields` | *(Optional)* The custom fields for this entry. |
| `ModData` | *(Optional)* The mod data fields to add to created trinket instances. Default none. For example: 
```js
"ModData": {
    "Example.ModId_FieldName": "some custom data"
}
```
 |

## See also
- Modding:Items for item data in general

ru:Модификации:Брелоки
zh:模组:饰品
