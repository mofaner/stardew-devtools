> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Big_craftables)

# Modding:Big craftables (`Big_craftables`)

← Items

This page explains how the game stores and parses 'big craftable'-type item data. For items in general, see Modding:Items.

## Overview
Big craftables are items which can be placed in the world and are two tiles tall (instead of one like objects). Unlike objects, they can't be eaten or sold to most shops.

They have item type `(BC)` (or `ItemRegistry.type_bigCraftable` in C# code), their data in `Data/BigCraftables`, their in-game sprites in `TileSheets/Craftables` by default, and their code in `StardewValley.Object` (based on the `bigCraftable` field).

## Data format
The big craftable data in `Data/BigCraftables` consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

### Basic info
| field | purpose |
| --- | --- |
| `Name` | The internal item name. |
| `DisplayName`; `Description` | A tokenizable string for the item's in-game display name and description. |
| `Price` | *(Optional)* The price when sold by the player. This is not the price when bought from a shop. Default 0. |

### Behavior
| field | purpose |
| --- | --- |
| `Fragility` | *(Optional)* How the item can be picked up. The possible values are 0 (pick up with any tool), 1 (destroyed if hit with an axe/hoe/pickaxe, or picked up with any other tool), or 2 (can't be removed once placed). Default 0. |
| `CanBePlacedIndoors`; `CanBePlacedOutdoors` | *(Optional)* Whether the item can be placed indoors or outdoors. Default true. |
| `IsLamp` | *(Optional)* Whether this is a lamp and should produce light when dark. Default false. |

### Appearance
| field | purpose |
| --- | --- |
| `Texture` | *(Optional)* The asset name for the texture containing the item's sprite. Defaults to `TileSheets/Craftables`. |
| `SpriteIndex` | *(Optional)* The sprite's index within the `Texture`, where 0 is the top-left sprite. |

### Context tags
| field | purpose |
| --- | --- |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other object data). This is formatted as a list; for example: 
```json
"ContextTags": [ "light_source", "torch_item" ]
```
 |

### Advanced
| field | purpose |
| --- | --- |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

## Unobtainable items
The data asset has items that can't normally be picked up or may be unimplemented in-game. They may be completely absent from the game, or they may be unused as craftables and instead appear in object data or furniture data.

## See also
- Modding:Items for item data in general

ru:Модификации:Изготовляемые предметы
zh:模组:大型打造品
