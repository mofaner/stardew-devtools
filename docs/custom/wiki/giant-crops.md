> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Giant_crops)

# Modding:Giant crops (`Giant_crops`)

← Index

This page describes how the game stores and parses Giant Crop data. This is an advanced guide for mod developers.

## Data format
You can now add/edit giant crops by editing the `Data/GiantCrops` data asset.

This consists of a string → model lookup, where...
- The key is a unique string ID for the giant crop.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| `FromItemId` | The item ID (qualified or unqualified) for the harvest ID of the regular crop which can turn into this giant crop. For example, `(O)254` is melon. Any number of giant crops can use the same `FromItemId` value. The first giant crop whose other fields match (if any) will spawn. |
| `HarvestItems` | The items which can be dropped when you break the giant crop. If multiple items match, they'll all be dropped. This consists of a list of models with these fields: |
| field | effect |
| *common fields* | See item spawn fields for the generic item fields supported for harvest items. If set to an item query which returns multiple items, one of them will be selected at random. |
| `Chance` | *(Optional)* The probability that this entry is selected, as a value between 0 (never drops) and 1 (always drops). Default 1 (100% chance). |
| `ForShavingEnchantment` | *(Optional)* Whether this item is only dropped for Shaving enchantment drops (`true`), only when the giant crop is broken (`false`), or both (`null`). Default both. |
| `ScaledMinStackWhenShaving`; `ScaledMaxStackWhenShaving` | *(Optional)* If set, the min/max stack size when this item is dropped due to the shaving enchantment, scaled to the tool's power level. This value is multiplied by the health deducted by the tool hit which triggered the enchantment. For example, an iridium axe which reduced the giant crop's health by 3 points will produce three times this value per hit. If both fields are set, the stack size is randomized between them. If only one is set, it's applied as a limit after the generic fields. If neither is set, the generic `MinStack`/`MaxStack` fields are applied as usual without scaling. |

|-
| `Texture`
| The asset name for the texture containing the giant crop's sprite.
|-
| `TexturePosition`
| *(Optional)* The top-left pixel position of the sprite within the `Texture`, specified as a model with `X` and `Y` fields. Defaults to (0, 0).
|-
| `TileSize`
| *(Optional)* The area in tiles occupied by the giant crop, specified as a model with `X` and `Y` fields. This affects both its sprite size (which should be 16 pixels per tile) and the grid of crops needed for it to grow. Note that giant crops are drawn with an extra tile's height. Defaults to (3, 3).
|-
| `Health`
| *(Optional)* The health points that must be depleted to break the giant crop. The number of points depleted per axe chop depends on the axe power level. Default 3.
|-
| `Chance`
| *(Optional)* The percentage chance that a given grid of crops will grow into the giant crop each night, as a value between 0 (never) and 1 (always). Default 0.01 (1%).

Note that the chance is checked for each giant crop that applies. If three giant crops each have a 1% chance of spawning for the same crop, then there's a 3% chance that one of them will spawn.
|-
| `Condition`
| *(Optional)* A game state query which indicates whether this giant crop is available to spawn. Defaults to always true.
|-
| `CustomFields`
| The custom fields for this entry.
|}

## See also
- Modding:Crop data for information about regular crops.

ru:Модификации:Гигантские культуры
