> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Objects)

# Modding:Objects (`Objects`)

← Items

This page explains how the game stores and parses object-type item data. For items in general, see Modding:Items.

## Overview
Objects are the default type for items in inventories or in the world. Depending on their data, they can be placed, picked up, eaten, sold to shops, etc.

They have item type `(O)` (or `ItemRegistry.type_object` in C# code), their data in `Data/Objects`, their icon sprites in `Maps/springobjects` or `TileSheets/Objects_2` by default, and their code in `StardewValley.Object`. See a table of sprites and their corresponding indexes.

## Data format
The object data in `Data/Objects` consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

### Basic info
| field | purpose |
| --- | --- |
| `Name` | The internal item name. |
| `DisplayName`; `Description` | A tokenizable string for the item's in-game display name and description. |
| `Type` | The item's general type, like Arch (artifact) or Minerals. The vanilla types are: `Basic`, `Arch` (artifact), `Litter` (e.g. debris), `Minerals`, `Quest`, `Crafting`, `Fish`, `Cooking`, `Seeds`, `Ring`, `interactive`, and some placeholder values like `asdf`. **Caution:** adding custom items with the `Arch` type is inadvisable as it often leads to artifact spots becoming broken and not giving any items. **Note:** The "Type" affects whether an item shows up in the collections menu. For example, items with `Type: Cooking` will show up as silhouettes in the cooked items collection menu, items with `Type: Minerals` will show up as silhouettes in the mineral collection menu, etc. However, the Category of the item must also be the correct number or the item will not count toward Perfection/Achievements. For example, if an item has Type: Cooking, it must also be "Category": -7 in order to count toward the "Gourmet Chef" achievement and Perfection. |
| `Category` | The item category. |
| `Price` | *(Optional)* The price when sold by the player. This is not the price when bought from a shop. Default 0. |

### Appearance
| field | purpose |
| --- | --- |
| `Texture` | The asset name for the texture containing the item's sprite. Defaults to `Maps/springobjects`. |
| `SpriteIndex` | The sprite's index within the `Texture`, where 0 is the top-left sprite. |
| `ColorOverlayFromNextIndex` | *(Optional)* When drawn as a colored object, whether to apply the color to the next sprite in the spritesheet and draw that over the main sprite. If false, the color is applied to the main sprite instead. Default false. |

### Edibility
| field | purpose |
| --- | --- |
| `Edibility` | *(Optional)* A numeric value that determines how much energy (edibility × 2.5) and health (edibility × 1.125) is restored when this item is eaten. An item with an edibility of -300 can't be eaten, values from -299 to -1 reduce health and energy, and zero can be eaten but doesn't change health/energy. Default -300. |
| `IsDrink` | *(Optional)* Whether to drink the item instead of eating it. Default false. |
| `Buffs` | *(Optional)* The buffs to apply to the player when this item is eaten, if any. Default none. This consists of a list of models with these fields: |
| field | purpose |
| `Id` | The unique string ID for this entry within the list. |
| `Duration` | *(Optional if `BuffId` is set)* The buff duration measured in in-game minutes. This can be set to `-2` for a buff that should last for the rest of the day. |
| `BuffId` | *(Optional)* The unique ID of a buff from `Data/Buffs` to apply, or `null` to ignore `Data/Buffs` and set the ID to `food` or `drink` depending on the item's `IsDrink` field. If a buff from `Data/Buffs` is applied and you also specify other fields, here's how the buff data is combined: |
| field | result |
| `Duration`; `IconTexture`; `IconSpriteIndex`; `GlowColor` | If specified, the value in `Data/Objects` is used instead of the one in `Data/Buffs`. If omitted, defaults to the value from `Data/Buffs`. |
| `CustomAttributes` | The values from both entries are combined (e.g. +1 speed in `Data/Objects` and +1 speed in `Data/Buffs` results in +2 speed). |
| `IsDebuff` | The value in `Data/Objects` is used. |

|-
| `IsDebuff`
| *(Optional)* Whether this buff counts as a debuff, so its duration should be halved when wearing a Sturdy Ring. Default false.
|-
| `IconTexture`
| *(Optional)* The asset name for the icon texture to load. This must contain one or more 16x16 icons in a grid of any size. If omitted, the game will draw a default icon based on the `BuffId` and `CustomAttributes` fields.
|-
| `IconSpriteIndex`
| *(Optional)* The buff's icon index within the `IconTexture`, where 0 is the top-left icon. Default 0.
|-
| `GlowColor`
| *(Optional)* The glow color to apply to the player. See color format. Default none.
|-
| `CustomAttributes`
| The custom buff attributes to apply, if any.

This consists of a model with any combination of these fields:

| field | purpose |
| --- | --- |
| `CombatLevel`; `FarmingLevel`; `FishingLevel`; `ForagingLevel`; `LuckLevel`; `MiningLevel` | *(Optional)* An amount applied to the matching skill level while the buff is active. This can be negative for a debuff. Default 0. |
| `Attack`; `AttackMultiplier`; `CriticalChanceMultiplier`; `CriticalPowerMultiplier`; `Defense`; `Immunity`; `KnockbackMultiplier`; `MagneticRadius`; `MaxStamina`; `Speed`; `WeaponPrecisionMultiplier`; `WeaponSpeedMultiplier` | *(Optional)* An amount applied to the player's attack, critical chance and  critical power, defense, immunity, knockback (i.e. weight), magnetic radius, maximum stamina, speed, or weapon precision/speed while the buff is active. This can be negative for a debuff. Default 0. |

|-
| `CustomFields`
| *(Optional)* The custom fields for this entry. 
|}
|}

### Geodes & artifact spots
| field | purpose |
| --- | --- |
| `GeodeDrops`; `<samp>GeodeDropsDefaultItems` | *(Optional)* The items that can be dropped when breaking open this item as a geode. Specifying either or both fields automatically enables geode behavior for this item. You can specify one or both fields: <ul> <li>`GeodeDrops` can be set to the specific items to drop. Default none. This consists of a list of models with these fields: |
| field | purpose |
| *common fields* | See item spawn fields for the generic item fields supported by geode drops. If set to an item query which returns multiple items, one of them will be selected at random. |
| `Chance` | *(Optional)* The probability that the item will be dropped if the other fields match, as a decimal value between 0 (never) and 1 (always). Default 1. |
| `SetFlagOnPickup` | *(Optional)* The mail flag to set for the current player when this drop is picked up. |
| `Precedence` | *(Optional)* The order in which this entry should be checked, where lower values are checked first. This can be a negative value. Geode drops with the same precedence are checked in the order listed. Default 0. For consistency, vanilla drops mostly use these values: * `-1000`: special overrides like the golden helmet; * `0`: normal items. |
</li>
<li>`GeodeDropsDefaultItems` chooses a predefined list of possible geode drops like clay, coal, copper, iridium, etc. Default false.</li>
</ul>

If both fields are specified, each geode will choose between them with an equal 50% chance. If `GeodeDrops` is specified but no entries match, the geode will use the `GeodeDropsDefaultItems` regardless of whether it's true.
|-
| `ArtifactSpotChances`
| *(Optional)* If this is an artifact (i.e. the `Type` field is `Arch`), the chance that it can be found by digging artifact spots in each location.

This consists of a string → model lookup, where:
- the key is the internal location name;
- the value is the probability the item will spawn if checked, as a decimal value between 0 (never) and 1 (always).
|}

### Context tags & exclusions
| field | purpose |
| --- | --- |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other object data). This is formatted as a list; for example: 
```json
"ContextTags": [ "color_yellow", "fish_ocean", "fish_upright", "season_summer" ]
```
 |
| `CanBeGivenAsGift` | *(Optional)* Whether this item can be gifted to NPCs. Default true. |
| `CanBeTrashed` | *(Optional)* Whether this item can be trashed from the player's inventory. Also affects whether the item can be shipped but not whether it can be sold to shops. Default true. |
| `ExcludeFromRandomSale` | *(Optional)* Whether to exclude this item from shops when selecting random items to sell. Default false. |
| `ExcludeFromFishingCollection`; `ExcludeFromShippingCollection` | *(Optional)* Whether to exclude this item from the fishing/shipping collection and their respective effect on the perfection score. Default false, in which case the normal requirements apply (e.g., artifacts are always excluded from the shipping collection). |

### Advanced
| field | purpose |
| --- | --- |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

## Unobtainable items
The spritesheet and data have items that can't normally be found in the player inventory (like twigs and lumber), and some sprites have no corresponding item data. There are also multiple entries for *weeds* and *stone* corresponding to different sprites, but the player can only normally obtain one *stone* item (index 390) and no *weeds* items.

## For C# mods
Object-type items are represented by the `StardewValley.Object` type.

This provides many methods to simplify common logic. Some notable examples:

| method | effect |
| --- | --- |
| `Object.GetRadiusForScarecrow()` | The tile radius that Object protects as a scarecrow. Default behavior: Checks for the value in the `crow_scare_radius_<radius>` context tag if applicable, then defaults to 17 if the internal name contains "Deluxe" and 9 otherwise. Can be patched to support custom scarecrows. |
| `Object.IsBar()` | Whether the item is a copper bar, iron bar, gold bar, iridium bar, or radioactive bar. |
| `Object.IsBreakableStone()` | Whether the item is a stone debris item which can be broken by a pickaxe. |
| `Object.IsFence()` | Whether the item is a fence. |
| `Object.IsFruitTreeSapling()` | Whether the item is a fruit tree sapling. This checks the `Data\fruitTrees` keys, so it works with custom fruit trees too. |
| `Object.IsHeldOverHead()` | Whether the player is shown holding up the item when it's selected in their toolbar. Default true (except for furniture). |
| `Object.IsIncubator()` | Whether the item can incubate farm animal eggs when placed in a building. |
| `Object.IsScarecrow()` | Whether the item is a scarecrow. Default: `true` if the object has the `crow_scare` context tag. Otherwise, returns whether the object's internal name contains "arecrow". Can be patched to support custom scarecrows. |
| `Object.IsTapper()` | Whether the item is a tapper or heavy tapper. |
| `Object.IsTeaSapling()` | Whether the item is a tea sapling. |
| `Object.IsTwig()` | Whether the item is a twig debris item. |
| `Object.IsWeeds()` | Whether the item is a weed debris item. |

## See also
- Modding:Items for item data in general

ru:Модификации:Объекты
zh:模组:物体
