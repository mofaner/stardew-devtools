> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Tools)

# Modding:Tools (`Tools`)

← Items

This page explains how the game stores and parses tool-type item data. For items in general, see Modding:Items.

## Overview
Tools are items that can be swung or used by the player to perform some effect (e.g. dig dirt, chop trees, etc).

They have item type `(T)` (or `ItemRegistry.type_tool` in C# code), their data in `Data/Tools`, their in-game sprites in `TileSheets/Tools` and translations in `Strings/Tools` by default, and their code in `StardewValley.Tool` and various subclasses like `StardewValley.Tools.Axe`.

## Data format
The tool data in `Data/Tools` consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

### Basic data
| field | purpose |
| --- | --- |
| `ClassName` | The name of the C# class to construct within the `StardewValley.Tools` namespace. The class must be a subclass of `StardewValley.Tool`, and have a constructor with no arguments. For example, given a value of `Axe`, the game will create `StardewValley.Tools.Axe` instances. The main values are: * main tools (`Axe`, `FishingRod`, `Hoe`, `MeleeWeapon`, `MilkPail`, `Pan`, `Pickaxe`, `Shears`, `Wand`, and `WateringCan`); * a special `GenericTool` type which applies the `Data/Tools` data and only has generic logic, so C# mods can patch in their own logic; * and two tools cut from the game which may not work correctly (`Lantern` and `Raft`). |
| `Name` | The internal name to set for the tool item. |
| `DisplayName`; `Description` | A tokenizable string for the tool's in-game display name and description. |
| `AttachmentSlots` | *(Optional)* The number of attachment slots to enable on the tool. Note that only `FishingRod` tools have the code to render and use attachment slots. Default `-1`, which keeps the default value set by the tool class. |
| `SalePrice` | *(Optional)* The default price when the item is sold to the player in a shop. Defaults to `-1`, in which case you should set the price manually in shops. |
| `CustomFields` | The custom fields for this entry. |

### Appearance
Note that drawing the tool correctly in the world (ie, while the player is trying to use it) will likely require custom code.

| field | purpose |
| --- | --- |
| `Texture` | The asset name for the texture containing the tool's sprite. |
| `SpriteIndex` | The tool's sprite index within the `Texture`, where 0 is the top row. |
| `MenuSpriteIndex` | *(Optional)* The sprite index within the `Texture` for the item icon. Defaults to `SpriteIndex`. |

### Upgrades
| field | purpose |  |
| --- | --- | --- |
| `UpgradeLevel` | *(Optional)* The tool upgrade level. Default `-1`, which keeps the default value set by the tool class. |  |
| `ConventionalUpgradeFrom` | *(Optional)* If set, prepends an upgrade for the given tool ID to the `UpgradeFrom` field. This applies these rules (based on the `UpgradeLevel` field, not the upgrade level of the specified tool ID): |  |
| upgrade level | price | items needed |
| 1 | 2000 | Copper Bar 5 |
| 2 | 5000 | Iron Bar 5 |
| 3 | 10000 | Gold Bar 5 |
| 4 | 25000 | Iridium Bar 5 |

For example, Iridium Axe specifies this value:

```js
"ConventionalUpgradeFrom": "(T)GoldAxe"
```

|-
| `UpgradeFrom`
| *(Optional)* The requirements to buy this tool from Clint's blacksmith tool upgrade shop. If you specify multiple entries, the first one which matches will be applied.

This consists of a list of models with these fields:

| field | purpose |
| --- | --- |
| `Price` | *(Optional)* The gold price to buy the upgrade. Defaults to 0. |
| `RequireToolId` | *(Optional)* If set, the qualified or unqualified item ID for the tool that must be in the player's inventory for the upgrade to appear. The tool will be destroyed when the upgrade is purchased. |
| `TradeItemId` | *(Optional)* If set, the qualified or unqualified item ID for an extra item that must be traded to upgrade the tool. (For example, many vanilla tools need metal bars.) |
| `TradeItemAmount` | *(Optional)* The number of `TradeItemId` required. Defaults to 1. |
| `Condition` | *(Optional)* A game state query which indicates whether this upgrade is available. Defaults to always true. |

For example, these are equivalent to the Steel Axe's upgrade settings:

```js
"UpgradeFrom": [
    {
        "RequireToolId": "(T)CopperAxe",
        "Price": 5000,
        "TradeItemId": "(O)335", // Iron Bar
        "TradeItemAmount": 5
    }
]
```

If you want the tool to always be available, you can just omit the conditions. For example:

```js
"UpgradeFrom": [
    {
        "Price": 5000
    }
]
```

Note that Clint needs a few days to smith the new tool. If you want to sell the tool directly, add it to a regular shop instead.
|}

### Game logic
| field | purpose |
| --- | --- |
| `CanBeLostOnDeath` | Whether the player can lose this tool when they die. Default false. |

### Extensibility
| field | purpose |
| --- | --- |
| `ModData` | *(Optional)* The mod data values to set when the tool is created, accessible in C# code via the `tool.modData` dictionary. For example: 
```js
"ModData": {
    "PowerLevel": 9000
}
```
 |
| `SetProperties` | *(Optional)* Set the value of arbitrary properties on the tool class. For example, this would disable the tool animation and require no stamina: 
```js
"SetProperties": {
    "InstantUse": true,
    "IsEfficient": true
}
```
 |

## See also
- Modding:Items for item data in general

ru:Модификации:Инструменты
zh:模组:工具
