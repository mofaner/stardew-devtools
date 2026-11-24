> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Furniture)

# Modding:Furniture (`Furniture`)

← Items

This page explains how the game stores and parses furniture-type item data. For items in general, see Modding:Items.

## Overview
Furniture are decorative items which can be placed in the world. In some cases, players can sit on them or place items on them.

They have item type `(F)` (or `ItemRegistry.type_furniture` in C# code), their data in `Data/Furniture`, their sprites in `TileSheets/furniture` by default, their vanilla translations in `Strings/Furniture`, and their code in `StardewValley.Objects.Furniture`.

## Data format
The furniture data in `Data/Furniture` consists of an string → string lookup, where...
- The key is the unqualified item ID.
- The value is a slash-delimited strings with the fields listed below.

| index | field | effect |
| --- | --- | --- |
| 0 | name | The internal item name. |
| 1 | type | The furniture type. See table of possible values. |
| 2 | tilesheet size | The furniture sprite size on the tilesheet, measured in tiles. This can be `<width> <height>` (e.g. `1 2`), or `-1` to use the default size for the type. |
| 3 | bounding box size | The size of the hitbox when the furniture is placed in-game, measured in tiles. The bounding box will be anchored to the bottom-left corner and extend upwards and rightwards. This can be `<width> <height>` (e.g. `1 2`), or `-1` to use the default size for the type. |
| 4 | rotations | The number of rotations possible (1, 2, or 4). |
| 5 | price | The price when purchased from a shop. |
| 6 | placement restriction | Where the furniture can be placed. Possible values: |
| value | effect |  |
| -1 | default (uses furniture type) |  |
| 0 | indoors-only |  |
| 1 | outdoors-only |  |
| 2 | indoors or outdoors |  |

|-
| 7
| display name
| The translated furniture name, which allows tokenizable strings.
|-
| 8
| sprite index
| The sprite index within the spritesheet texture to draw.
|-
| 9
| texture
| *(Optional)* The asset name of the texture to draw. Defaults to `TileSheets/furniture`.
|-
| 10
| off limits for random sale
| *(Optional)* Whether to prevent this furniture from appearing in randomly generated shop stocks and the furniture catalogue. Default false.
|-
| 11
| context tags
| *(Optional)* A space-delimited list of context tags which apply to this furniture. Default none.
|}

## Furniture types
The 'type' field can be set to one of these values. (The '1.7' column shows the equivalent values in the upcoming Stardew Valley 1.7.)

| type (current) | type (1.7) | effect |
| --- | --- | --- |
| chairs |  |  |
| `armchair` | `Armchair` | An armchair which lets one player sit on it. |
| `bench` | `Bench` | A bench which lets two players sit on it if it's wide enough. |
| `chair` | `Chair` | A chair which lets one player sit on it. |
| `couch` | `Couch` | A couch which lets an arbitrary number of players sit on it depending on its tile width. |
| tables |  |  |
| `long table` | `LongTable` | A long table which allows placing an item on it. |
| `table` | `Table` | A table which allows placing an item on it. |
| lighting |  |  |
| `fireplace` | `Fireplace` | A fireplace which can be lit. |
| `lamp` | `Lamp` | A lamp which can be lit. |
| `sconce` | `Sconce` | A sconce light which can be lit. |
| `torch` | `Torch` | A torch which can be lit. |
| `window` | `Window` | A window which can be placed on a wall and lets through light from outside. |
| other interactions |  |  |
| `bed`; `bed child`; `bed double` | `Bed` | A bed which players can sleep in. |
| `dresser` | `Dresser` | A dresser which allows storing clothes within it. |
| `fishtank` | `FishTank` | A fish tank which can display fish placed inside it. |
|  | `Television` | 1.7 A television which can be turned on to access TV channels. |
| decorative |  |  |
| `bookcase` | `Bookcase` | A decorative bookcase which has no special behavior. |
| `decor` | `Decor` | A general decor item which has no special behavior. |
| `painting` | `Painting` | A painting which can be hung on a wall. |
| `randomized_plant` | `RandomizedPlant` | A plant whose appearance is dynamic by randomizing each of its three vertical tiles (usually a pot, stem, and head). |
| `rug` | `Rug` | A rug which can be placed on the ground and under furniture, and which lets players and NPCs walk over it. |
| `other` | `Other` | A miscellaneous furniture which has no special behavior. |

## See also
- Modding:Items for item data in general

ru:Модификации:Мебель
zh:模组:家具
