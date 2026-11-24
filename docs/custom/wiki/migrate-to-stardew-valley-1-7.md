> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Migrate_to_Stardew_Valley_1.7)

# Modding:Migrate to Stardew Valley 1.7 (`Migrate_to_Stardew_Valley_1.7`)

←Index

{{Modder compatibility header}}
1.7

This page helps mod authors prepare their mods for the upcoming Stardew Valley 1.7, and documents some of the changes and new functionality.

limit=3

## FAQs

### What's changing?
Stardew Valley 1.7 makes significant changes to many data assets to enable new game features and mod extensibility. See the sections below for more details.

### Is this the modapocalypse?
Probably not. SMAPI and Content Patcher will automatically rewrite older mods for many of the changes, so they should still work as-is. Some mods may need an update.

### How to update your mod
Here's a suggested 'quick start' guide to update a mod for Stardew Valley 1.7.

- **For C# mods:**: # Update for Stardew Valley 1.6.16 if you haven't already.
  # Rebuild the solution.
  # Fix any build errors and review any build warnings. You can search this page and previous migration guides for relevant info as needed.
  # Increment your mod version in `manifest.json`.
(Some mods may be added to SMAPI's compatibility blacklist due to runtime errors. Incrementing the mod version will fix that if so.)
  # Test to make sure the mod works.
  # Skim through this page, and check any section that might be relevant to the mod.

- **For Content Patcher packs:
  # Update for Stardew Valley 1.6.16 if you haven't already.
  # Skim through this page, and check any section that might be relevant to the mod.

- **For content packs which use another framework:**: See the documentation for the framework mod. Often you won't need to update the content pack if the framework mod itself was updated.

## Data asset changes
### `Data/Boots`
This has been overhauled into a data model asset, with translations moved into a new `Strings/Boots` asset. This also includes new fields like `ContextTags` and `CustomFields` which weren't available in the old format.

The data asset now consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `Name` | The internal item name (not shown in-game). |
| `DisplayName`; `Description` | A tokenizable string for the item's in-game display name and description. |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |
| equipment effects |  |
| `AddDefense`; `AddImmunity` | *(Optional)* A bonus applied to the player's defense or immunity stats when the boots are equipped. Default 0. |
| appearance |  |
| `Texture` | The asset name for the texture containing the boots sprite. Defaults to `Maps/springobjects` if omitted, but should always be specified for custom boots. |
| `SpriteIndex` | *(Optional)* The sprite's index within the `Texture`, where 0 is the top-left sprite. Default 0. |
| `ColorTexture` | *(Optional)* The asset name for the texture containing the boots color sprite. Defaults to `Characters/Farmer/shoeColors` if omitted. |
| `ColorSpriteIndex` | *(Optional)* The color sprite's index within the `ColorTexture`, where 0 is the top-left sprite. Default 0. |
| advanced |  |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

For example, here's how to add new boots in 1.7:

    "Changes": [
        // add data
        {
            "Action": "EditData",
            "Target": "Data/Boots",
            "Entries": {
                "{{ModId}}_FancyBoots": {
                    "Name": "{{ModId}}_FancyBoots",
                    "DisplayName": "{{i18n: fancy-boots.name}}",
                    "Description": "{{i18n: fancy-boots.description}}",
                    "Texture": "Mods/{{ModId}}/Boots",
                    "SpriteIndex": 0,
                    "ColorSpriteIndex": 1
                }
            }
        },

        // add texture
        {
            "Action": "Load",
            "Target": "Mods/{{ModId}}/Boots",
            "FromFile": "assets/boots.png"
        }
    ]

### `Data/Buffs`
Added one new field to buff attributes, contained in the `Effects` field:

| field | effect |
| --- | --- |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

### `Data/Characters`
The following fields have been added:

| field | effect |
| --- | --- |
| `FlowerDanceBehavior` | *(Optional)* A list of behaviors to consider for use during the Flower Festival. If no matching ones are found, the default behavior for that NPC's gender is used. Each entry in this list has the following fields: |
| field | effect |
| `Id` | An ID for this entry within the dance behavior list. This only needs to be unique within the current list. |
| `Condition` | A game state query which indicates whether this entry is a valid choice. Default true. |
| `Sprite` | The sprite to use for the dance, as would be passed in to the `changeSprite` event command. Default null. |
| `FirstDanceFrame` | The first frame of the relevant spritesheet to use for the dance animation. The animation frames after this index must still follow the standard structure. |
| `Precedence` | The order in which this behavior should be checked, where 0 is the default value used by most behaviors. |

|}

### `Data/CraftingRecipes` and `Data/CookingRecipes`
Crafting recipes and cooking recipes are now a data model. The data model for both are identical. Crafting recipes still live in `Data/CraftingRecipes`, and cooking recipes still live in `Data/CookingRecipes`.

Both assets are dictionary assets. The key is the recipe ID, the same as in 1.6, and the value is a data model with the following fields:

| field | effect |
| --- | --- |
| `DisplayName` | A tokenizable string for the display name of this recipe. |
| `Description` | A tokenizable string for the description of this recipe. |
| `Ingredients` | A list of ingredients for this recipe. |
| field | effect |
| `Id` | TThe unique string ID for this entry (not the item itself) within the current list. This is semi-optional — if omitted, it'll be auto-generated from `ItemId`. However multiple entries with the same ID may cause unintended behavior, so it's often a good idea to set a globally unique ID instead. |
| `ItemId` | The item for this ingredient. This can be either a qualified item ID, or a context tag. If a context tag is specified, it needs to have a corresponding entry in `Data/ContextTags`. |
| `Quantity` | The amount of this ingredient to require. Default 1. |
| `Condition` | The game state query for this ingredient. If not specified, defaults to TRUE. |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

|-
| `Result`
| The possible results for this recipe. This is a list of conditional item spawn fields.

If there are multiple resulting possibilities at once, the one chosen for display in menus uses the following precedence:
# First, it checks the recipe's `DisplayItemId`.
# If there was no `DisplayItemId`, then it will search all results for the first one that is not conditional (eg. `Condition` is either `null` or `TRUE`).
# If no unconditional results were found, then it chooses the first result, regardless of if the condition passes.
|-
| `DisplayItemId`
| A qualified item ID to override the displayed item in menus.
|-
| `Unlock`
| The unlock conditions for this recipe.

| field | effect |
| --- | --- |
| `Trigger` |  |
| name | effect |
| `DayStart` | Checks the `Condition` at the beginning of each day. |
| `Mail` | Checks the `Condition` at the beginning of each day, and queues `MailLetterName` for the mailbox if the condition passed. |
| `LevelUp` | Appears when reaching a level `RequiredSkillLevel` in the `RequiredSkill` skill. `Condition` is ignored. |
| `Default` | Added as a default recipe. `Condition` is ignored. |

|-
| `Condition`
| The game state query for this unlock. If not specified, defaults to TRUE.
|-
| `RequiredSkill`
| The skill for the required level for this unlock, if `Trigger` is equal to `LevelUp`.
|-
| `RequiredSkillLevel`
| The required level for this unlock, if `Trigger` is equal to `LevelUp`.
|-
| `MailLetterName`
| The name of letter sent for unlock, if `Trigger` is equal to `Mail`.
|-
| `CustomFields`
| *(Optional)* The custom fields for this entry.
|}
|-
| `ExcludeFromPerfection`
| Whether or not to exclude this recipe from perfection checks. Default false.
|-
| `CustomFields`
| *(Optional)* The custom fields for this entry.
|}

### `Data/GiftTastes`
Gift tastes, previously in `Data/NPCGiftTastes` with a localized version for every language, are now in `Data/GiftTastes` (taste data only) and `Strings/GiftTasteReactions` (reaction strings, one per language).

Responses to gifts are pulled from `Strings/GiftTasteReactions`, with the entry chosen from the following format: `{NPC}_{Taste}`, with `{NPC}` being the NPC ID, and `{Taste}` being the corresponding taste (ex. `Penny_Love` for Penny's loved gift response).

For the gift taste data itself, it now works based on a precedence and  game state query system. Additionally, a gift taste can apply to multiple NPCs at once.

`Data/GiftTastes` is a list of gift taste entries, with the following format:

| field | effect |
| --- | --- |
| `Id` | The ID of this entry. |
| `Condition` | *(Optional)* The  game state query for if this gift taste should apply, if any. |
| `Precedence` | *(Optional, but recommended)* The precedence for this entry, with higher values being checked first. For the precedence decided for various base game's gift tastes, see the notes after this table. |
| `ItemId` | The item that matches this gift taste. This can be a qualified item ID, a category, or a  context tag query. |
| `Taste` | The gift taste that applies for this entry. Possible values are `Love`, `Like`, `Neutral`, `Dislike`, and `Hate`. |
| `AppliesTo` | *(Optional)* Who this gift taste applies to. This is a comma-separated list of NPC names. If not specified, applies to all NPCs. |
| advanced |  |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

#### Precedences for old gift taste data
When converting the base game's legacy gift taste data, the precedence values were determined in a way to match previous behavior.

First, a base precedence was chosen based on who it applied to and what type of match it was:

| base precedence | who | what |
| --- | --- | --- |
| 0 | Specific character | Specific item |
| -1000 | Specific character | Items matching a context tag |
| -2000 | Universal | Specific item |
| -3000 | Specific character | Items of a specific category |
| -4000 | Universal | Items matching a context tag |
| -5000 | Universal | Items of a specific category |

After the base precedence is selected, an additional value is added based on the resulting gift taste. 

| precedence modifier | legacy gift taste |
| --- | --- |
| 400 | Love |
| 300 | Hate |
| 200 | Like |
| 100 | Dislike |
| 0 | Neutral |

Additionally, the previously hard-coded behavior of everyone disliking artifacts (except Penny and the Dwarf, who like them) are now in the data, with a precedence of -1250 (universal dislike) and -1200 (Penny and Dwarf like).

Certain fallback behavior for cheap or negative ediblity otherwise-neutral-reaction items are still hardcoded, but still only apply if:
- the chosen gift taste was neutral
- the chosen gift taste applies to everyone (ie. was universal)
- the chosen gift taste was for that specific item, and not an entire category or context tag

### `Data/Furniture`
This has been overhauled into a data model asset. The overhaul also adds support for previously-hardcoded features like custom item descriptions (via `Description`), custom TVs (via the new `Television` furniture type and `TelevisionScreen*` fields), and custom fields.

The data asset now consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `Name` | The internal item name (not shown in-game). |
| `DisplayName` | A tokenizable string for the item's in-game display name. |
| `Description` | *(Optional)* A tokenizable string for the item's in-game description. Defaults to a generic description based on the other fields like `CanPlaceIndoors` and `CanPlaceOutdoors`. |
| `Type` | *(Optional)* The furniture type, which affects various furniture functionality and default values (e.g. default bounding box size). Defaults to `Other`. See table of valid types. |
| `Price` | *(Optional)* The price for this furniture when purchased from a shop. Default 0. |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |
| behavior |  |
| `CanPlaceIndoors`; `CanPlaceOutdoors` | *(Optional)* Whether the player can place this furniture indoors and/or outdoors. Defaults to generic behavior based on the `Type` field (e.g. TVs can't be placed outdoors by default). |
| `ExcludeFromRandomSale` | *(Optional)* Whether to prevent this furniture from being shown in randomly generated shop stock or furniture catalogues. Default false. |
| appearance |  |
| `Texture` | The asset name for the texture containing the furniture sprite. Defaults to `TileSheets/furniture` if omitted, but should always be specified for custom furniture. |
| `SpriteIndex` | *(Optional)* The sprite's index within the `Texture`, where 0 is the top-left sprite. Default 0. |
| `Rotations` | *(Optional)* The number of rotations possible (1, 2, or 4). Default 1. |
| `SpriteSize` | *(Optional)* The sprite's size in the `Texture`, measured in tiles. Defaults to a generic size depending on the `Type` field. |
| `BoundingBoxSize` | *(Optional)* The collision box size when the furniture is placed in-game, measured in tiles. Defaults to a generic size depending on the `Type` field. The bounding box is anchored to the bottom-left corner of the `SpriteSize`. Width will push the right side of the box to the right, height will push the top of the box upwards. |
| type-specific options |  |
| `BedType` | *(Optional)* When the `Type` field is set to `Bed`, the bed type which affects who can sleep in it. Default `Single`. Valid values: |
| bed type | effect |
| `Single` | A single-sized bed with room for one player or NPC. |
| `Double` | A double-sized bed with room for two players or NPCs. |
| `Child` | A single bed for a child NPC. |

|-
| `BedUpgradeItemId`
| *(Optional)* When the `Type` field is set to `Bed`, the item ID of the double bed to replace with for house upgrades. Default `null`.

If omitted, when choosing a double bed for an upgraded Farmhouse or Cabin, a legacy integer `ItemId` will result in a value of `ItemId + 4`, otherwise the default value of `2052` will be used.
|-
| `TelevisionScreenOffset`
| *(Optional)* When the `Type` field is set to `Television`, the X and Y pixel offset relative to the TV's bounding box to apply when drawing the screen image onto the TV. Default none.
|-
| `TelevisionScreenScale`
| *(Optional)* When the `Type` field is set to `Television`, the zoom multiplier to apply to the screen image so it fits onto the TV's screen.
|-
!colspan="2"| advanced
|-
| `CustomFields`
| *(Optional)* The custom fields for this entry.
|}

### `Data/Hats`
This has been overhauled into a data model asset, with translations moved into a new `Strings/Hats` asset. This also includes new fields like `ContextTags` and `CustomFields` which weren't available in the old format.

The data asset now consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `Name` | The internal item name (not shown in-game). |
| `DisplayName`; `Description` | A tokenizable string for the item's in-game display name and description. |
| `HairStyle` | *(Optional)* What to do about the player's hair when they wear this hat. Default `DrawObscuredHair`. The possible values are: * `DrawFullHair`: the hair is drawn like usual (e.g. for hats that don't hide the hair like Abigail's bow). * `DrawObscuredHair`: the hair is partly obscured under the hat (e.g. for most hats like the cowboy hat). * `HideHair`: the hair is fully hidden (e.g. for hats which fully cover the head like the mummy mask). |
| `SkipHairOffset` | *(Optional)* Whether to skip offsetting the hat to sit 'on top' of the player's current hairstyle (e.g. for masks). Default false. |
| `Texture` | The asset name for the texture containing the hat sprite. Defaults to `Characters/Farmer/hats` if omitted, but should always be specified for custom hats. |
| `SpriteIndex` | *(Optional)* The sprite's index within the `Texture`, where 0 is the top-left sprite. Default 0. |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

For example, here's how to add a new hat in 1.7:

    "Changes": [
        // add data
        {
            "Action": "EditData",
            "Target": "Data/Hats",
            "Entries": {
                "{{ModId}}_FancyHat": {
                    "Name": "{{ModId}}_FancyHat",
                    "DisplayName": "{{i18n: fancy-hat.name}}",
                    "Description": "{{i18n: fancy-hat.description}}",
                    "HairStyle": "DrawObscuredHair",
                    "Texture": "Mods/{{ModId}}/Hats",
                    "SpriteIndex": 0
                }
            }
        },

        // add texture
        {
            "Action": "Load",
            "Target": "Mods/{{ModId}}/Hats",
            "FromFile": "assets/hats.png"
        }
    ]

### `Data/Monsters`
Monster data has been completely rethought in Stardew Valley 1.7. In particular...
- For monster data in general:
  - `Data/Monsters` has been been overhauled into a data model asset, with translations moved into a new `Strings/Monsters` asset. This includes significant new features which weren't supported with the old format.
  - A new 'monster variant' concept lets multiple specific monsters share common data (e.g. `Green Slime` and `Tiger Slime` are variants of the new base `Slime` monster). This is particularly used for common drops.
  - A new 'monster tag' system lets monster define custom tags like `slime_is_prismatic`, which can be used to filter monster drops.
- For monster drops:
  - A rethought monster drop system supports the full range of vanilla features like conditions, item queries, qualified item IDs for non-object drops, and monster-specific concepts like repeating drop chance and mutually-exclusive drops.
  - All drops are now qualified item IDs. The former 'resource IDs' (formatted as negative numbers like `-4`) are no longer supported; items are converted into their equivalent resource debris automatically where needed.
  - Nearly all monster drops are now defined in `Data/Monsters` (with just a few hardcoded drops common to all monsters left).

The data asset now consists of a string → model lookup, where...
- The key is the unique string ID.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `DisplayName` | A tokenizable string for the monster's display name. |
| `IsGlider` | *(Optional)* Whether the monster is flying (e.g. so they glide over obstacles). Default false. |
| `IsMineMonster` | *(Optional)* Whether this monster spawns in the mines, and should be buffed after the player reaches the bottom. This sometimes applies to monsters that can appear elsewhere too (e.g. wilderness golems on the farm receive mine buffs too). Default false. |
| `ExperiencePoints` | *(Optional)* The combat experience points received for defeating the monster. Default 1. |
| stats |  |
| `Health` | *(Optional)* The initial hit points. Default 20. |
| `Attack` | *(Optional)* The attack power (i.e. how much damage its hits inflict on players). Default 5. |
| `Defense` | *(Optional)* The defense which reduces damage received by this monster. Default 0. |
| `DodgeChance` | *(Optional)* The percentage chance that the monster dodges an attack, as a value between 0 (never) and 1 (always). Default 0. |
| `Speed` | *(Optional)* The default movement speed. Default 1. |
| `Jitteriness` | *(Optional)* How often the monster will move in random non-player directions to try to get around objects, as a value between 0 (never) and 1 (always). Default 0.1. |
| `DetectionDistance` | *(Optional)* The distance at which the monster detects a player and begins moving towards them, or -1 for unlimited. Default 4. |
| drops |  |
| `Drops` | *(Optional)* The items which can drop when the monster is defeated. Default none. All matching entries are dropped. If the player has a burglar's ring equipped, each entry will make two attempts to drop (but fields like `RepeatingChance` and `SkipIfDropped` are independent between the two attempts). This consists of a list of models with these fields: |
| field | effect |
| *common fields* | See item spawn fields for the generic item fields supported for monster drops. Notes: * If a drop has an item query which returns multiple item stacks, one of them will be selected at random. * The `Condition` field can optionally contain the `%RANDOM_KEY` token, which is set to a unique identifier for this monster during the current tick. |
| `ForTags` | *(Optional)* The monster tags for which this drop applies. This can be a context tag-like query string like `!variant_frost_bat, !variant_lava_bat` (not frost bat or lava bat). Default none, meaning it applies to all monsters with this data. |
| `Chance` | *(Optional)* A probability that this item will be found, as a value between 0 (never) and 1 (always). Default 1. |
| `RepeatingChance` | *(Optional)* A compounding probability of an extra drop, as a value between 0 (never) and .95 (nearly always). Default 0. This applies after an item has been dropped based on the other fields. This chance is repeatedly checked to produce an extra drop until it fails. Each repeat drop will ignore the `Chance` and `Condition` fields, but reapply all other item spawn fields (e.g. re-selecting `RandomItemId` if applicable). |
| `RepeatingChanceKey` | *(Optional)* When checking the `RepeatingChance` field, use this to seed a predictable RNG. This can be used to synchronize separate drops. Default none. This can optionally contain the `%RANDOM_KEY` token, which is set to a unique identifier for this monster during the current tick. |
| `ExplodeStack` | *(Optional)* Whether to split multi-item stacks into single-item stacks. Default false. For example, let's say a monster would drop a stack of 15 wood. With this option enabled, it would instead drop 15 stacks of one wood each. |
| `SkipIfDropped` | *(Optional)* Skip the current entry if one of these drop IDs have already been dropped. Default none. This is specified as an array. For example: 
```js
"SkipIfDropped": [ "ExampleDropId", "AnotherDropId" ]
```
 |

|-
!colspan="2"| tags
|-
| `ContextTags`
| *(Optional)* The context tags to add for this monster, in addition to those added automatically based on the other fields. Default none.
|-
!colspan="2"| variants
|-
| `DefaultVariant`
| *(Optional)* When the game creates a monster of this type without specifying a variant, use the given variant instead of creating it from only the base data. Default false.
|-
| `Variants`
| *(Optional)* The possible variations of this monster. Default none.

Each variant is simply a set of fields which override the base data when set. For example, this creates a variant which only differs from the base monster by name and health points:

```js
"Variants": [
    {
        "DisplayName": "{{i18n: variant-name}}",
        "Health": 50
    }
```

This consists of a list of models with these fields:

| field | effect |
| --- | --- |
| *monster fields* | All monster fields (except `DefaultVariant` and `Variants`) can be set on the variant. All fields are optional, and default to the value set in the monster's base data. |
| `InheritDrops` | *(Optional)* Whether to inherit drops defined in the monster's base `Drops` field. Default true. Specifically: * If `true`, the variant will combine the base `Drops` and variant `Drops` into one list. * If `false`, only drops in the variant `Drops` field are applied. |

|-
!colspan="2"| advanced
|-
| `CustomFields`
| *(Optional)* The custom fields for this entry.
|}

For example, this Content Patcher code adds a new 'Pufferchick' monster which drops pufferfish when defeated. The monster's spritesheet is automatically loaded from `Monsters/<ID>`.

**Note:** this only adds the monster data. Currently this does nothing unless you have another mod which spawns instances of the monster in-game.

    "Changes": [
        // add data
        {
            "Action": "EditData",
            "Target": "Data/Monsters",
            "Entries": {
                "{{ModId}}_Pufferchick": {
                    "DisplayName": "{{i18n: pufferchick.name}}",
                    "ExperiencePoints": 3,

                    "Health": 30,
                    "Attack": 2,
                    "Defense": 5,
                    "DodgeChance": 0.3,
                    "Speed": 2,
                    "DetectionDistance": 2,

                    "Drops": [
                        {
                            "Id": "Pufferfish",
                            "ItemId": "(O)128",
                            "Chance": 0.5,
                            "RepeatingChance": 0.1
                        }
                    ]
                }
            }
        },

        // add texture
        {
            "Action": "Load",
            "Target": "Monsters/{{ModId}}_Pufferchick",
            "FromFile": "assets/pufferchick.png"
        }
    ]

### Schedule data
Schedules have been changed into data models. Previously under `Characters/schedules`, now under `Characters/ScheduleData`.

| field | effect |
| --- | --- |
| `Precedence` | The precedence of this entry. A higher precedence will be checked first. Note: the hardcoded schedules for visiting Ginger Island will override everything below precedence `100000`. |
| `Season` | *(Optional)* The season that this schedule applies to, if any. If not specified, applies to any season. |
| `DayOfMonth` | *(Optional)* The day of month that this schedule applies to, if any. If not specified, applies to any day of the month. |
| `WeekDay` | *(Optional)* The week day that this schedule applies to, if any. If not specified, applies to any day of the week. This accepts the short name of the day of week, ex. `Mon`, `Tue`, `Wed`, ... |
| `RequiredHearts` | *(Optional)* The minimum amount of friendship (in hearts) for this schedule to apply, if any. If not specified, applies regardless of friendship level. |
| `Condition<samp> | *(Optional)* A game state query for if this entry should apply, in addition to the other fields. If not specified, treated as <samp>TRUE`. |
| `StartLocationOverride<samp> | *(Optional)* Override the location to start the day in when this schedule is chosen. <samp>StartTileOverride` must also be specified for this to apply. Requires `StartTileOverride` to be specified for this to work. |
| `StartTileOverride<samp> | *(Optional)* Override the tile to start the day on when this schedule is chosen. <samp>StartLocationOverride` must also be specified for this to apply. Requires `StartLocationOverride` to be specified for this to work. |
| `StartFacingDirectionOverride<samp> | *(Optional)* Override the facing direction to start the day with when this schedule is chosen. <samp>StartLocationOverride` must also be specified for this to apply. Requires `StartLocationOverride` to be specified for this to work. |
| `StartEndOfRouteAnimation<samp> | *(Optional)* Override the end of route animation to start the day with when this schedule is chosen. <samp>StartLocationOverride` must also be specified for this to apply. Requires `StartLocationOverride` to be specified for this to work. |
| `StartEndOfRouteMessage<samp> | *(Optional)* Override the end of route message to start the day with when this schedule is chosen. <samp>StartLocationOverride` must also be specified for this to apply. Requires `StartLocationOverride` to be specified for this to work. |
| `FallbackSchedules<samp> | *(Optional)* A list of additional schedules options that, if the corresponding GSQ passes, will revert to that schedule instead. Intended for "fail conditions" (ex. one of the locations in this schedule are not currently accessible). |
| field | effect |
| <samp>Condition` | The game state query to check for this fallback option. |
| `Schedule` | The schedule to use if the condition passed for this fallback option. If this is "season", it will act check "spring", "summer", "fall", or "winter" based on the current game's season. IF this is "no_schedule", then the NPC will have no schedule for that day. |
| `CustomFields` | *(Optional)* Custom fields for mods to use. |

|-
| `Commands`
| A list of commands for the schedule.

| field | effect |
| --- | --- |
| `Type` | *(Optional)* The type of this schedule command. Defaults to `Depart` |
| type | meaning |
| `Depart` | The NPC will depart at the specified time. |
| `Arrive` | The NPC will try to arrive at the specified time. |
| `Bed` | The NPC will try to return to their bed. |

|-
| `Time`
| The time for this schedule command. This must be a time **after** the previous command's time.

Note: a time of `0` (for "0 schedules") is no longer supported. For the equivalent behavior, use `StartLocationOverride` and friends.
|-
| `DestinationLocation`
| *(Only applies for `Type` = `Depart` or `Arrive`)* The location that this command takes the NPC to.
|-
| `Destination`
| *(Only applies for `Type` = `Depart` or `Arrive`)* The tile in the location that this command takes the NPC to.
|-
| `DestinationFacingDirection`
| *(Optional, only applies for `Type` = `Depart` or `Arrive`))* The facing direction that should be used when arriving at this command's target. 
|-
| `EndOfRouteAnimation`
| *(Optional)* The animation from `Data/animationDescriptions` to play upon ending this NPC.
|-
| `EndOfRouteMessage`
| *(Optional)* The localized string key to say if a player talks to this NPC after ending this command, but before the next one.
|-
| `CustomFields`
| *(Optional)* Custom fields for mods to use.
|}
|-
| `CustomFields`
| *(Optional)* Custom fields for mods to use.
|}

The vanilla schedules were all converted using [this automated script](https://gist.github.com/spacechase0/266daa4d6d916607bec235c55653fa6b), which is under the MIT license.

### `Data/Objects`
Added new fields:

| field | effect |
| --- | --- |
| `Buffs` | This field already existed, but has a field change: |
| field | effect |
| `CustomAttributes` | This field already existed, but has one new field: |
| field | effect |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

|}
|-
| `UseCondition`
| A game state query for determining if the object is usable on right click. If not specified, equivalent to `"TRUE"`.
|-
| `UseConditionFailedMessage`
| A tokenizable string for what message to show when `UseCondition` is not met. If an empty string, no message will be shown. If not specified, equivalent to `"[LocalizedText Strings/UI:CantUseHere]"`.
|-
| `UseActions`
| A list of actions to run upon using the item. These trigger actions will have an input argument called `StackSize`, which will be the size of the stack obtained if triggered via `ConsumeOnObtain`, or `1` otherwise. (For more information on trigger action input arguments, see the relevant section on this page.)
|-
| `ConsumeOnUse`
| `true` if the item should be consumed on use, `false` otherwise. Default `true`.
|-
| `ObtainActions`
| A list of actions to run upon obtaining the item. These trigger actions will have an input argument called `StackSize`, which will be the size of the stack obtained. (For more information on trigger action input arguments, see the relevant section on this page.)
|-
| `ConsumeOnObtain`
| If the item should be automatically used *and* consumed when obtained. Items with this enabled:
<ul>
<li>Will not actually enter the inventory (since they are consumed immediately).</li>
<li>Can be collected even with a full inventory (since they are consumed immediately).</li>
<li>Will not have their `UseCondition` checked before their `UseActions` are run.</li>
<li>Will have their `ObtainActions` (if any) run before their `UseActions` are run.</li>
</ul>
|-
| `ShowNotificationOnObtain`
| `true` if the item should show a notification in the bottom left corner of the screen upon receiving the item, `false` otherwise. Default `true`.
|-
| `ForgeRandomSlot`
| The slot to randomize the enchantments for if this is used as an ingredient.
|-
| `ForgeCost`
| If greater than or equal to 0, the cinder shard cost for using this item in the forge.
|-
| `ForgeCostItem`
| If set, changes the item required for forging from cinder shards to something else.
|}

### `Data/Weapons`
Added new fields:

| field | effect |
| --- | --- |
| `DefaultEnchantments` | Enchantments to apply by default on this weapon. This is a dictionary, with the key being the enchantment ID, and the value being the level of the enchantment. Enchantments added through this are still subject to normal enchantment restrictions, and therefore must actually be applicable to this weapon. For an example of this, check out the Holy Blade. |

## Changes for all mod authors
### Switch blocks
'Switch blocks' are a new `${…}$` syntax which can be used in nearly all game text (not just dialogue). This extends the former 'gender-switch blocks' added in 1.6.0.

These are applied before most other commands or parsing, so you can safely use them in cases where characters like `^` have a different meaning (e.g. mail text).

The supported switch blocks are:

| type | syntax | description |  |
| --- | --- | --- | --- |
| gender | `${male^female}`; `${male^female^non-binary}` | Switch between text for male, female, and non-binary players. The non-binary value is only used if a mod overrides the player's gender, since the in-game UI only allows male or female. You can also use `¦` instead of `^`, in which case any `^` characters are left as-is. For example: 
```json
"Mon": "Hey ${lad^lass^kiddo}$!"
```
 |  |
| randomize | `${variant A | \| …}$` | Defines an arbitrary number of variants from which to choose each day. After randomization, any leading or trailing whitespace is trimmed. This randomization is deterministic per day (e.g. reloading the save won't change which one is shown). For example, let's say you did this: 
```js
"Rainy_Night": "I ${love ||| like ||| adore ||| somewhat enjoy ||| enjoy}$ rain.$h"
```
 That would create five variants which each have an equal probability of being selected. |

Switch blocks can be arbitrary combined and nested. For example:

```js
"Rainy_Night": "${Hello there ||| Hey ${lad^lass^kiddo}$ }$!"
```

### Context tag changes
Added new context tags:

| context tag | item types | effect |
| --- | --- | --- |
| `is_galaxy_weapon` | weapons | Marks the tool as a galaxy weapon like the galaxy hammer or galaxy sword. This is set for vanilla scythes through `Data/Weapons`. This affects logic which checks for galaxy weapons (e.g. galaxy soul enchantments). |

Additionally, there is now a data asset for context tags for supporting data (such as for using them in crafting recipes). Located in `Data/ContextTags`, this is a dictionary assets. The key is the context tag, and the value is a data model with the following fields:

| field | effect |
| --- | --- |
| `DisplayName` | A tokenizable string for the display name of this context tag. Required when using a context tag as a crafting recipe ingredient. |
| `RepresentativeItem` | The qualified item ID for the item representing this context tag. Required when using a context tag as a crafting recipe ingredient. |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

### Dialogue changes
<ul>
<li>Added a new special token in dialogue strings:

| token | description |  |
| --- | --- | --- |
| ` | \|` | This is a shortcut for the randomize switch block. Delimits an arbitrary number of dialogue variants from which to choose each day. After randomization, any leading or trailing whitespace is trimmed. This randomization is deterministic per day (e.g. reloading the save won't change which one is shown). For example, let's say you did this: 
```js
"Rainy_Night": "
    I love rain.$h|||
    I like rain.$h|||
    I adore rain.$h|||
    I hate rain.$s#$b#Just kidding. I love it!$h|||
    I enjoy rain.$h
"
```
 That would create five variants of the dialogue which each have an equal probability of being selected. |
</li>
<li>Improved some special tokens:

| token | changes |  |
| --- | --- | --- |
| ` | ` | Whitespace around the token is now ignored, so you can split text into multiple lines: 
```js
"Wed8": "
    Isn't it expensive, coming to the desert so often like you do?#$e#You must really like to see me, huh? Hmmm... $h||
    *sigh*... I do miss the beautiful flowers of Stardew Valley...$s
"
```
 |
</li>
<li>Some dialogues were merged or renamed using the new features. The previous dialogue keys are now ignored by the game.

| asset | former key | changes |  |
| --- | --- | --- | --- |
| `Characters/Dialogue/<NPC name>` | `<samp>Resort_<number>`; `Resort_Bar_<number>`; `Resort_Chair_<number>`; `Resort_Dance_<number>`; `Resort_Shore_<number>`; `Resort_Towel_<number>`; `Resort_Umbrella_<number>`; `Resort_Wander_<number>` | Merged into their base key (e.g. `Resort_<random>` into `Resort`) using the new ` | \|` random-variant token. |
| `Characters/Dialogue/MarriageDialogue`; `Characters/Dialogue/MarriageDialogue<NPC name>`; `Characters/Dialogue/MarriageDialogue<NPC name>Roommates` | `funLeave_<NPC name>`; `funReturn_<NPC name>`; `jobLeave_<NPC name>`; `jobReturn_<NPC name>`; ; `patio_<NPC name>`; `spouseRoom_<NPC name>`; ; `spring_<NPC name>`; `summer_<NPC name>`; `fall_<NPC name>`; `winter_<NPC name>` | Moved into `MarriageDialogue<NPC name>` and removed the NPC suffix (like `spouseRoom_<NPC name>` → `spouseRoom`). |  |
| `Indoor_Day_0`; `Indoor_Day_1`; `Indoor_Day_2`; `Indoor_Day_3`; `Indoor_Day_4` | Merged into `Indoor_Day`. |  |  |
| `Indoor_Night_0`; `Indoor_Night_1`; `Indoor_Night_2`; `Indoor_Night_3`; `Indoor_Night_4`; `Indoor_Night_<NPC name>` | Merged into `Indoor_Night`. |  |  |
| `NoBed_0`; `NoBed_1`; `NoBed_2`; `NoBed_3` | Merged into `NoBed`. |  |  |
| `Outdoor_0`; `Outdoor_1`; `Outdoor_2`; `Outdoor_3`; `Outdoor_4`; `Outdoor_<NPC name>` | Merged into `Outdoor`. |  |  |
| `Rainy_Day_0`; `Rainy_Day_1`; `Rainy_Day_2`; `Rainy_Day_3`; `Rainy_Day_4` | Merged into `Rainy_Day`. |  |  |
| `Rainy_Night_0`; `Rainy_Night_1`; `Rainy_Night_2`; `Rainy_Night_3`; `Rainy_Night_4`; `Rainy_Night_5`; `Rainy_Night_<NPC name>` | Merged into `Rainy_Night`. |  |  |
</li>
</ul>

### Display shops
In addition to being able to add or change what is sold on Pierre's counter, you can also create your own.

Display shops are similar to normal shops, except they show their stocked items in-world. They have a fixed size which is filled from the item pool until it is full (or there is nothing left to add).

Display shops are stocked at the beginning of the day, and not restocked until the next day. They are stocked per-farmhand so that they can be different for everyone (such as with backpack upgrades), however a `LimitedStockMode` of `Global` will sync the stock between players, just like a normal shop.

If `LimitedStockMode` is `Player`, then it will be shown before other items, and will not count towards the `ItemCountToSell`. Once a `LimitedStockMode` `Player` item is purchased, it will move to the next not-displayed `LimitedStockMode` `Player` that was stocked, and once those are out of stock it will move on to the synced item stocks.

Display shop items are always purchased one at a time - a higher stack size means it can be bought multiple times.

The data asset, located at `Data/DisplayShops`, consists of a string -> model lookup, where...
- The key is the ID of the display shop.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `OpenCondition` | *(Optional)* A Game State Query for determining if the display shop if is open (that is, if the items shown are purchasable or not). If not specified, it will always be open. |
| `ClosedMessage` | *(Optional)* A Tokenizable String for what message to show if the shop is closed. Defaults to (after token parsing) `"Closed."`. |
| `Currency` | *(Optional)* The currency to use for the price, with the same options as in `Data/Shops`. Defaults to `0` (standard gold). |
| cosmetic |  |
| `PurchaseSound` | *(Optional)* A sound to play upon making a successful purchase. Defaults to `"purchaseClick"`. |
| `ItemWorldOffset` | *(Optional)* A Vector2 of how far to move the display item from the tile that has the purchase tile action. For example, Pierre's shop counter has a value of `0 -72` so that the items show on top of the counter. Defaults to `0`. |
| stocked products |  |
| `ApplyProfitMargins` | *(Optional)* Whether or not to apply profit margins by default for items in this shop. Defaults to `null`. |
| `PriceModifiers` | *(Optional)* Price modifiers to apply to items in this shop. Behaves the same way as `Data/Shops` `PriceModifiers`. |
| `PriceModifierMode` | *(Optional)* Price modifier mod for applying price modifiers to items in this shop. Behaves the same way as `Data/Shops` `PriceModifierMode`. |
| `ItemCountToSell` | *(Optional)* The amount of items that can be sold each day. |
| `ItemPool` | *(Optional)* The items to use when populating the shop. This list is sorted by `Precedence` before being used, with entries of the same precedence being shuffled together. This list consist of values with these fields: |
| field | effect |
| *common fields* | See `Items` for Shops for the item fields supported by display shop items. Notes: * If set to an item query which returns multiple items, all of them will be valid choices for the shop. * Unlike other item spawn fields, `ObjectDisplayName` is supported for any entry (not just Object items). |
| `DisplayOffset` | *(Optional)* The amount to offset drawing this item when shown in the display shop. This is useful for non-standard-sized items. |
| `DisplayScale` | *(Optional)* How much to scale the item when it is shown. Defaults to `4` (just like most things in the game). |
| `ItemBlurb` | *(Optional)* Text to show in a dialogue box before the item prompt appears. |
| `ItemBlurbSpeaker` | *(Optional)* The NPC that will "speak" the `ItemBlurb`, if desired. |
| `Precedence` | *(Optional)* The precedence for this item to be checked, with higher being sooner. Defaults to `0`. |

|}

To make the display shop show in world, you need to use a new tile action: `DisplayShop <displayShopId> <itemIndex>`, with `displayShopId` being the ID of the entry in `Data/DisplayShops` and `itemIndex` being which index of the stocked items to make available (based on the corresponding `ItemCountToSell` value).

For example, here's how to add a new entry to Pierre's counter:

```json
{
    "Format": "2.9.0",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "Data/DisplayShops",
            "TargetField": [ "SeedShopCounter", "ItemPool" ],
            "Entries": {
                "{{ModId}}_Weeds": {
                    "ItemId": "(O)0",
                    "Price": 123,
                    "AvailableStock": 3,
                    "ItemBlurb": "wow some weeds. perfect for honey",
                    "ItemBlurbSpeaker": "Penny",
                    "Precedence": -100,
                }
            }
        }
    ]
}
```

### Enchantment data
Enchantments have been **partly** de-hardcoded into a new `Data/Enchantments` data asset. Mods can now easily add new enchantments, including enchantments applied through the forge using a prismatic shard or a custom item ID.

However, this logic is still hardcoded:
- Forging innate enchantments. The innate enchantments are in data, but the logic to choose which to apply is still hardcoded.
- Forging diamonds into a weapon to fill all its weapon stat slots.
- Several enchantments' effects when applied (though weapon buff effects are in data).

This consists of a string → model lookup, where...
- The key is the unique string ID for the enchantment.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| `DisplayName` | A tokenizable string for the enchantment's display name, shown in tooltips. This can contain `{0}` for the enchantment level, if applicable. |
| `ForgeWith` | *(Optional)* If set, this enchantment is applied to tools by forging an item with this qualified ID into it. Default none, in which case it won't be applicable through the forge. |
| `AppliesTo` | *(Optional)* The tools or weapons to which this enchantment can be applied. Defaults to all tools/weapons. This is specified as an array of context tag queries. The enchantment can be applied if at least one query matches. 
```js
"AppliesTo": [
    "melee_weapon, !is_scythe", // any non-scythe melee weapon
    "hoe, upgrade_level_4"      // any hoe upgraded to iridium or higher
]
```
 |
| `EnchantmentSlot` | *(Optional)* The 'slot' this enchantment goes in. Only one enchantment per slot can normally be applied, except for the "Forge" slot. Default is "Primary". Valid vanilla types are: |
| type | example |
| `Primary` | A normal enchantment applied through a prismatic shard. |
| `Forge` | A weapon stat enchantment applied through the forge. |
| `Transforming` | An item transformation enchantment applied through the forge with galaxy souls. |
| `Innate1` and `Innate2` | An innate enchantment applied through the forge using a dragon tooth. |

|-
| `IsSecondary`
| *(Optional)* Whether this is an innate enchantment applied through the forge using a dragon tooth. Default false.
|-
| `IsVisible`
| *(Optional)* Whether the enchantment should be shown in the tool/weapon's tooltip. Default true.
|-
| `MaximumLevel`
| *(Optional)* The number of times this enchantment can be applied to the same tool to upgrade the enchantment level (e.g. for weapon stat enchantments). A value of 1 means there's only one enchantment level, so it can't be upgraded by applying the enchantment again. Default 1.
|-
| `EquipmentEffects`
| *(Optional)* The buffs applied to the weapon when this enchantment is applied.

This consists of a list of models with the fields below. You can specify any number of entries; the first one which matches the tool will be applied.

| field | effect |
| --- | --- |
| `Id` | A unique string ID for this entry. |
| `MinLevel` | *(Optional)* The minimum enchantment level for which these stats apply. Default 0 (i.e. any level). |
| *other fields* | See the `Effects` field in `Data/Buffs`, which is equivalent to this model. |

|-
| `Transformations`
| *(Optional)* If `EnchantmentSlot` is `"Transforming"`, this contains all valid transformations for it.

This is a dictionary of qualified item IDs, with the key being the original item, and the value being the transformed item.

Transformations to and from items besides weapons are allowed, with some caveats:
- If the source item cannot be enchanted, the maximum level for the enchantment must be 1, or it will not work.
- If the source item and transformed item are different C# types, then all extra item data (such as mod data, enchantments, and tool enchantments) will be lost, since an entirely new item had to be created.
|-
!colspan="2"| advanced
|-
| `CustomFields`
| *(Optional)* The custom fields for this entry.
|-
| `ClassName`
| *(Optional, specialized)* The name of the C# class to construct within the `StardewValley.Enchantments` namespace. The class must be a subclass of `StardewValley.Enchantments.Enchantment`, and have a constructor with no arguments. For example, given a value of `BottomlessEnchantment`, the game will create `StardewValley.Enchantments.BottomlessEnchantment` instances.

This should usually be omitted, in which case the game will create a generic `Enchantment` instance which uses the data in `Data/Enchantments` for its ID.
|}

### Event changes
Added new event commands:

| command | description |
| --- | --- |
| `setDrawDepthModifier <npc> <amountInTiles> | Modify the draw depth of <npc> for this event, by <amountInTiles>. |
| <samp>setDrawDepthModifier <npc> reset | Reset the draw depth modifier of <npc> for this event. |
| <samp>ForceBreathing <npc> false | Prevent <npc> from showing a breathing animation for this event. |
| <samp>ForceBreathing <npc> true [breathPosX] [breathPosY] [breathRectX] [breathRectY] [breathRectWidth] [breathRectHeight] | Force <npc> to show a breathing animation for this event. You can optionally override the data used for the breathing rect, with the values following the same behavior as they would if specified in the <samp>Data/Characters` character data. |
| `ForceBreathing <npc> reset | Reset the forced breathing modifier of <npc> for this event. |
| <samp>SaveActorState <stateStoreId> <actor> <state>+ | Stores the specified <actor>'s current <state>(s) into the <stateStoreId> actor state store, to be loaded later. Any actor, both NPC and Farmer, can be specified. If <samp>all` is specified, them all actors will have their state saved. Multiple <state> values can be specified, separated by spaces. Valid <state> values include: |
| state type | description |
| `all` | Saves all **vanilla** state types for the specified actor. Modded state types are not included here to make it less likely that modded state types will break existing events. For using modded state types in bulk, see the bottom of this table. |
| Any state starting with `!` | This will *remove* the specified state from the current selection of ones to be saved. For example, you could specify `all !movement !animation` to save all state *except* the movement state and animation state. |
| `position` | The current position of the actor. |
| `movement` | The movement state of the actor, including its current speed, `advancedMove` data, and `ignoreCollisions` flag. |
| `animation` | The animation state of the actor, including its facing direction, `ignoreMovementAnimation` flag, and (for NPCs only) its `ForceBreathing` values. |
| `drawOffset` | Values previously specified in `drawOffset` and `setDrawDepthModifier`. |
| `name` | The NPC's current display name. |
| `portrait` | The NPC's current portrait. |
| `sprite` | The NPC's current sprite. |
| `shadow` | The NPC's current `hideShadow` value. |
| `swimming` | The NPC's current swimming state. |
| Any state ending with `/` | Specifying a state ending with `/` will result in all state types starting with that string to be added. For example, specifying `spacechase0.SpaceCore/` would add *all* states that were added by SpaceCore (assuming they were properly namespaced). This also works with "nested namespaces", ex. `spacechase0.SpaceCore/MagicalStuff/` would add all "magical stuff" states added by SpaceCore. |

|-
| `LoadActorStateStore <stateStoreId>
| Loads all actor state stored in the <stateStoreId> actor state store.
|-
| <samp>ClearSavedActorStateStore <stateStoreId>
| Clears the <stateStoreId> actor state store.
|}

### Fish pond changes
<ul>
<li>Added new fish pond translation keys:

| asset | key | usage |
| --- | --- | --- |
| <samp>Strings/UI` | `PondQuery_<key>_<item ID>`; `PondQuery_<key>_<attitude>` | All `PondQuery_*` translation keys can now be suffixed with either the qualified item ID of the fish, or the fish's general attitude based on their `fish_talk_*` context tag. For example, the 'fish are OK' message for an anchovy pond will be loaded from the first key which exists in this order: `PondQuery_StatusOk_(O)129`, `PondQuery_StatusOk_Rude`, or `PondQuery_StatusOk`. |
</li>
<li>Merged some fish pond translation keys:

| asset | former key | changes |
| --- | --- | --- |
| `Strings/UI` | `PondQuery_Status<status><number (0–6)>`; `PondQuery_StatusRequestComplete_<fish disposition><number (0–2)>`; `PondQuery_StatusRequestPending_<fish disposition><number (0–2)>`; | Removed the number key suffixes using switch blocks (e.g. merged `PondQuery_StatusOk<number>` into `PondQuery_StatusOk`). |

</li>
<li>Coral and Sea urchin now have the `fish_talk_coral` context tag instead of a hardcoded `_Coral` translation key suffixes.</li>
</ul>

### Item query changes
Added one new item query:

| query | effect |
| --- | --- |
| `SEASONAL_RACCOON_SEED` | Get a seasonal raccoon seed for the current season. This sets the stack size to the recommended amount based on the player's luck. When used in item spawn fields, you can set `MinStack` and `MaxStack` to override the stack size. |

### Item spawn field changes
<ul>
<li>Added new item spawn fields:

| field | effect |
| --- | --- |
| `Enchantments` | *(Optional)* For tools and weapons, the enchantment IDs to apply (matching their IDs in `Data/Enchantments`). This field applies the normal enchantment rules. Default none. This is specified as an array, and you can repeat secondary enchantments to boost their level. For example, this applies Vampiric and Attack (level 2): 
```js
"Enchantments": [ "Vampiric", "Attack", "Attack" ]
```
 |

</li>
</ul>

### Game state query changes
Added game state queries:

| query | effect |
| --- | --- |
| `PLAYER_INVENTORY_SIZE <player> <minSize> [maxSize]` | Whether *any one* of the specified player(s) inventory has at least <minSize> slots unlocked, and (if specified) less than or equal to [maxSize] slots unlocked. |

### `Data/MapPatches` - Dehardcoded map patches
The base game now has a data-driven conditional map patch system. At the beginning of each day, maps are reloaded, and then these are checked and applied.

| field | effect |
| --- | --- |
| `OriginalMap` | The full asset name of the original map(s) that this patch should apply too. Multiple maps can be specified, separate by commas. |
| `MapPatch` | The asset name of the map containing this patch. |
| `SourceRect` | *(Optional)* The source rectangle of `MapPatch` to apply. If not specified, the whole map will be used. |
| `DestinationPosition` | The position on `OriginalMap` to place the top left corner of the patch at. |
| `MapProperties` | *(Optional)* Map properties to apply. To remove a property, the value should be null. To append or prepend to a property, use `@MergeInto`. |
| `Condition` | *(Optional)* A game state query which indicates whether this entry applies. Default true. |
| `Precedence` | *(Optional)* The order in which this entry should be checked, where higher precedences will apply over lower precedences. |
| advanced |  |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

Previously hard-coded map patches that have been moved over are:

| location | map change | ID of entry in `Data/MapPatches` | additional notes |
| --- | --- | --- | --- |
| The Beach (including the Night Market) | The bridge to the right side of the map | `Beach_Bridge` | Mostly dehardcoded, but has special logic to apply immediately for everyone rather than at the beginning of the next day. |
| The Beach (including the Night Market) |  Town shortcut to the forest | `Beach_CommunityUpgradeShortcut_ForestEntrance` | Entirely dehardcoded. Previously, the beach map had the warps for this defined through map properties, even if you haven't unlocked community shortcuts yet. Those warps are now applied through this patch's `MapProperties` and `@MergeInto`. |
| The Beach (including the Night Market) |  Town shortcut to the east side of town | `Beach_CommunityUpgradeShortcut_TownEntrance` | Entirely dehardcoded. Previously, the beach map had the warps for this defined through map properties, even if you haven't unlocked community shortcuts yet. Those warps are now applied through this patch's `MapProperties` and `@MergeInto`. |
| The Beach (including the Night Market) | The sign leading up to SquidFest | `Beach_SquidFest_Sign` | Entirely dehardcoded |
| The Beach (including the Night Market) | SquidFest | `Beach_SquidFest` | Entirely dehardcoded |
| The Beach (including the Night Market) | Day 2 of SquidFest's price increase on the prize sign | `Beach_SquidFest_PriceSignIncrease` | Entirely dehardcoded |
| Most existing hard-coded patches have not been moved over to the new system yet, but more will be added over time. |  |  |  |

### Trigger action changes
Added new trigger actions:

| action | effect |
| --- | --- |
| `AddHealth <amount>` | Heals the local player by <amount> and plays the appropriate sound effect. |
| `IncreaseBackpackSize <amount>` | Increase the backpack size of the local player by <amount>. |
| `HoldUpItem <itemId> [message]` | Start animation to hold up the specified item above the player's head. If [] is specified, it will show that message as well. |
| `MultiplayerGlobalMessage <translationKey> [arg1] [arg2] [arg3]...` | Show a global chat message in multiplayer. The translation key needs to exist in `Strings/UI` and must be prefixed with `Chat_` within that file (meaning you would use `"SomeMessage"` in `MultiplayerGlobalMessage` and `"Chat_SomeMessage"` in `Strings/UI`). Any additional arguments after the translation key (which are parsed as tokenizable strings) will be used as substitution values using the normal notation. (ex. `MultiplayerGlobalMessage "SomeMessage" "meow" "kitty cat"` and `"Chat_SomeMessage": "{1} goes {0}"` would result in the message `"kitty cat goes meow"`) |
| `SetVariable <variableStoreId> <name> <value>` | Sets the value of the variable <name> in the <variableStoreId> store to <value>, if that variable store is writable. (For more on variables, see the relevant section in tokenizable string changes on this page.) |
| `RemoveVariable <variableStoreId> <name>` | Removes the variable <name> in the <variableStoreId> store, if that variable store is writable. (For more on variables, see the relevant section in tokenizable string changes on this page.) |

### Tokenizable string changes
#### Usable in more places
Tokenizable strings are now usable in more places.
<ul>
<li>Game state queries</li>
<li>Trigger actions (for `If`, token evaluation is delayed until which value to use is checked)</li>
<li>Event commands
<ul>
<li>Tokens in quotes (ex. a `message "[LocalizedText Strings/Events:MeowKitty]"` command) will be evaluated when that command is run</li>
<li>Any token not in quotes (ex. how wedding events have `[ContextualWeddingCelebrations]` to inject dynamically-constructed commands based on the attendees) will be evaluated when that the event is started.</li>
<li>`setSkipActions` delays token evaluation until the actions are actually run, meaning any changes that occur during the event but before the player has actually pressed "skip" will be reflected in the variables used here)</li>
</ul></li>
</ul>

Tokens are evaluated immediately, unless otherwise specified.

#### New tokens
The following tokens have been added:

| token format | output |
| --- | --- |
| `InputArgument <argumentName>` | Tokenizable strings can now accept input arguments, depending on where they were called. If the input argument <argumentName> does not exist, then token evaluation fails. |

#### Token expressions
Additionally, a new type of tokens now exists (called "expressions"), which are only usable in certain contexts. If one of these tokens is used in a place where it is not allowed, it will fail and say the token was not found. The places where expressions tokens are usable are:
<ul>
<li>Game state queries</li>
<li>Trigger actions</li>
<li>A few specific event commands:
<ul>
<li>`action`</li>
<li>`goto`, specifically for the label(s)</li>
<li>`setSkipActions`</li>
</ul></li>
</ul>

| token format | output |
| --- | --- |
| `Math Integer <expression>` ;  `Math Decimal <expression>` | Calculates the given expression as math, using either integers (when `Integer` is specified) or floating point numbers (when `Decimal` is specified). <ul> <li>For example, `[Math Integer 3 / 2]` will result in `1`, while `[Math Decimal 3 / 2]` will result in `1.5`. This is because with `Integer` specified, each operation will remove everything after the decimal point before proceeding.</li> <li>Valid operators include, in order of precedence: <ul> <li>Parenthesis (meaning "`(`" and "`)`") can be used to group parts of the epxression to be processed first, similar to how one would expect in a real math expression.</li> <li>`*` (multiplication), `/` (division), and `%` (modulus)</li> <li>`+` (addition) and `-` (subtraction)</li> </ul></li> </ul> |
| `Comparison <expression>` | Evaluates the given expression, resulting in `TRUE` if the expression is true, and `FALSE` otherwise. <ul> <li>The expression can compare numbers (whether integer or decimal), or strings.</li> <li>Valid operators include, in order of precedence: <ul> <li>Parenthesis (meaning "`(`" and "`)`") can be used to group parts of the expression to be processed first, similar to how one would expect in a real math expression.</li> <li>`!` - negation, meaning `[Comparison !TRUE]` becomes `FALSE`, `[Comparison !FALSE]` becomes `TRUE`, and something like `[Comparison !(3 >= 5)]` becomes `TRUE`</li> <li>`<` (less than), `<=` (less than or equal to), `≶` (greater than), `>=` (greater than or equal to)</li> <li>`==` (equal to) and `!=` (not equal to)</li> <li>`&&` ("and"), meaning `[Comparison 2 > 1 && 3 == 3]` becomes `FALSE`</li> <li>`\|\|` ("or"), meaning `[Comparison 2 > 1 \|\| 3 == 3]` becomes `TRUE`</li> <li>(For those who know what "logical comparison short circuiting" is, please be aware that that is currently not implemented.)</li> </ul></li> </ul> |
| `If <condition> <trueValue> [falseValue]` | If <condition> evalautes to `TRUE`, then <trueValue> is evaluated and returned. If <condition> evalutes to `FALSE`, then [falseValue] is evaluated and returned (if it was specified - otherwise, nothing is returned). |
| `GameStateQuery <gsq>` | Evaluates the given game state query, allowing GSQ usage inside `[If]`. |
| `VariableExists <variableStore> <variableName>` | Evaluates to `TRUE` if <variableStore> has a variable named <variableName>, and `FALSE` otherwise. (For more on variable stores, see later section.) |
| `GetVariable <variableStore> <variableName>` | Evaluates to the value of <variableName> in the variable store <variableStore>. If the variable does not exist, evaluation of the tokenizable string will fail. (For more on variable stores, see later section.) |
| `GetVariableWithDefault <variableStore> <variableName> <defaultValue>` | Evaluates to the value of <variableName> in the variable store <variableStore>. If the variable does not exist, <defaultValue> will be returned instead. (For more on variable stores, see later section.) |

#### Variable stores
Variables stores are a set of named variables stored in a particular location. The current available locations used as variable stores are:

| variable store ID | description |
| --- | --- |
| `Global` | A variable store attached to the save itself. <ul> <li>Any player can read and write from this.</li> <li>This variable store will be saved into the save file at the end of the day.</li> </ul> |
| `Player/<playerSpecifier>` | A variable store attached to a particular player. <ul> <li><playerSpecifier> uses the same format as game state query target players, except specifiers that work for check multiple players (such as `Any` or `All`) may not be used.</li> <li>Any player can read from this, however only the owning player can write to their variable store.</li> <li>If a player is disconnected, their variables are saved, and will be resynced when they reconnect.</li> <li>This variable store will be saved into the save file at the end of the day.</li> </ul> |
| `Local` | A variable store attached to a "calling context". Generally, this means it will always be reset between uses unless specifically kept, such as in the following contexts: <ul> <li>Events - A variable set in the `Local` variable store using the `action` event command will be usable later during that event.</li> <li>`Data/TriggerActions` - When a trigger action has multiple actions to run, all actions will be using the same `Local` variable store, meaning earlier trigger actions can save data for use by later trigger actions in the same sequence.</samp></li> </ul> |

### Debug command changes
This only details some debug command changes - mainly, ones that might be useful for mod authors.

| command | changes |
| --- | --- |
| `/tokens` | A new required parameter has been introduced as the first argument, <type>. If <type> is `Text` the string will be evaluated using non-expression tokens. If <type> is `Expression`, expression tokens will be allowed as well. |
| `/runmacro` | Commented lines are now allowed - if `--` is the beginning of a line, it will be ignored. |
| `/debugaction` (new) | A new command for trying out tokenizable string input arguments, tokenizable string local variables, and trigger actions using them. This command maintains its own set of input arguments and a local variable store, which can be manipulated using the subcommands. |
| subcommand | effect |
| `/debugaction argument <name> <value>` | Sets the input argument <name> to <value>, which can be later used in `/debugaction run` and `/debugaction evaluate`. |
| `/debugaction evaluate <expression>` | Evaluates <expression> as a tokenizable string, using input arguments and local variables previously set from this command. |
| `/debugaction run <action>` | Runs <action> as a trigger action, using input arguments and local variables previously set from this command. |
| `/debugaction clear` | Clears all previously set input arguments and local variables for `/debugaction`. |

Some examples of using this command alongside `/runmacro`, at least at the time of writing, can be found here: https://gist.github.com/spacechase0/3ca7b2e195619a17ef9039c894cc60e4
|-
| `/enchant <enchantmentId> [level]` (new)
| Enchant the currently held item with the <enchantmentId> enchantment at level [level] (or 1 if not specified).
|}

## Changes for C# mod authors
### DialogueKeyValidator
The new `DialogueKeyValidator` class analyses dialogue entries to detect dialogue keys which don't match one of the predefined patterns.

This is mainly meant to implement unit tests. For example, to validate all dialogue keys in a data asset:

```c#
string assetName = "Characters/Dialogues/Abigail";
var dialogues = Game1.content.Load<Dictionary<string, string>>(assetName);

var validator = new DialogueKeyValidator();
foreach ((string key, string dialogue) in dialogues)
{
    if (!validator.IsKnownFormat(assetName, key))
        Assert.Fail($"Validation failed for dialogue '{key}': key doesn't match one of the expected patterns.");
}
```

You can optionally extend the valid patterns by setting validator fields like `MarriageDialogueKeyPatterns`.

### Monster changes
- The monster data has been overhauled.
- The new `monster.Tags` field contains a set of metadata tags about the monster which can be checked in `Data/Monsters`. It's safe to add new tags to the list for existing monster types.
- The former `monster.objectsToDrop` field (a list of string object IDs) has been replaced by `monster.drops` (an item inventory).
- Removed `monster.getExtraDropItems()`. Drops should be added in `Data/Monsters` instead.

### Recipe changes
As part of the change for recipes using data models, `Farmer.recipesCooked` and `Farmer.cookedRecipe(itemId)` now use qualified item IDs instead of unqualified object item IDs.

### `SpecialItem` deprecation
`SpecialItem` is now deprecated, and is only kept for save compatibility. All existing uses of it have been migrated to use `Object` instead, with the following new objects being available:
<ul>
<li>`(O)BackpackUpgrade1`</li>
<li>`(O)BackpackUpgrade2`</li>
<li>`(O)ClubCard`</li>
<li>`(O)SpecialCharm`</li>
<li>`(O)SkullKey`</li>
<li>`(O)MagnifyingGlass`</li>
<li>`(O)DarkTalisman`</li>
<li>`(O)MagicInk`</li>
</ul>

### Tokenizable string changes
Tokenizable string parsing and evaluation has been separated into two separate stages. As a result, several places that use them now cache the parsed version (which is called `ParsedStringSegment`).

Any custom trigger actions, tokenizable string tokens, or event commands that want to delay tokenizable string evaluation so they can handle it themself (such as for only evaluating portions that should be used based on a condition) can attach the `[DelayArgumentTokenEvaluation]` attribute to their handler method. This will prevent the arguments from being evaluated before they reach your handler. (However, the handlers themselves currently still use `string[]` as their arguments, so this could be slightly off depending on how quotes were used.)

`TokenParser.ParseText(string text, Random random, TokenParserDelegate customParser, Farmer player)` is now deprecated, and `TokenParser.EvaluateText(string text, TokenParserContext context)` should be used instead.

### Tool changes
The following were added to `Tool`:

| member | effect |
| --- | --- |
| `virtual bool DrawEvenWhenNotUsing` | If `true`, `Game1.drawTool` (and therefore `Tool.draw`) will be called even if the `Farmer.UsingTool` is false. Default `false`. |
| `virtual bool DrawNormalToolFrames` | If `false`, `Game1.drawTool` will not draw the normal "swinging" tool frames. Default `true`. |
| `virtual bool DoAction(GameLocation location, Farmer who)` | Called when the player does a right click rather than a left click. Return `true` if the tool is doing something, and `false` otherwise. |

### Netfield changes
The following changes must now all be registered with `ProtocolSummary` before usage:
<ul>
<li>Additional net fields</li>
<li>Additional net types</li>
<li>Subclasses of any synced type</li>
</ul>
If this is not done, an exception will be thrown when adding netfields, or setting values of unknown types.

Instead of manually registering with `ProtocolSummary` or manually adding net fields using a harmony patch, everything should be registered through a new SMAPI API (coming soon) which will handle these details for you. This will ensure that if mods load in a different order on one player's computer compared to other connected players, it will not break anything.

If a farmhand's `ProtocolSummary` does not match the host's, they will get a protocol mismatch error upon connecting (rather than "No cabins available", or other obscure and unclear issues).

### C# Event changes
C# mods can add new actor state to work with the new `SaveActorState`/etc. commands (previously documented in event changes above).

On `Event` there exists a new static function, `RegisterActorStateType`, which accepts the name of the new actor state type (which you should namespace with your mod ID, ex. `spacechase0.SpaceCore/SomeCustomActorState`), a delegate for saving the state, and a delegate for loading the state. Each delegate receives the `Event` instance, the relevant actor (which could be an `NPC` or a `Farmer`), and a delegate for performing the save and load to the proper place.

### Enchantment changes
Besides the data-driven changes mentioned previously, `Enchantment`s can now <i>theoretically</i> be put on non-`Tool`s more easily. Most of the enchantment code has been moved from `Tool` to `Item`, and relevant functions accepting instances of the former now accept the latter. `Item` itself does not <i>store</i> enchantments though, so a custom type that supports enchantments will need to implement a virtual property so that they are properly stored.

There are also new hooks for enchantments to run: before/after taking damage, entering/leaving a location, and an update each tick.

### Other breaking API changes
*(This section is meant as a quick reference if you have build errors; there's no need to read through it otherwise.)*

#### Removed or renamed types
These classes renamed in 1.7:

| type | migration |
| --- | --- |
| `BaseEnchantment` | Renamed to `Enchantment`. |
| `AxeEnchantment`; `BaseWeaponEnchantment`; `FishingRodEnchantment`; `HoeEnchantment`; `MilkPailEnchantment`; `PanEnchantment`; `PickaxeEnchantment`; `ShearsEnchantment`; `WateringCanEnchantment` | Obsolete. Enchantments now target tools via data instead. |

#### Other API changes
These class members changed in 1.7:

| type | member | migration |
| --- | --- | --- |
| `BaseEnchantment`; (and its subclasses) | `hideEnchantmentName` | Replaced by `EnchantmentManager.HideEnchantmentName`. |
| `hideSecondaryEnchantName` | Replaced by `EnchantmentManager.HideSecondaryEnchantName`. |  |
| `GetEnchantmentFromItem(…)` | Replaced by `EnchantmentManager.GetEnchantmentFromItem`. |  |
| `GetAvailableEnchantments()` | Removed. <ul> <li>To check whether any enchantments can be applied to a tool through the forge, use `EnchantmentManager.CanForgeEnchant`.</li> <li>There's no directly equivalent method in 1.7, but you can mimic the former logic if needed. 
```c#
static List<Enchantment> GetAvailableEnchantments()
{
    var dataAsset = DataLoader.Enchantments(Game1.content);

    List<Enchantment> enchantments = [];
    foreach ((string id, EnchantmentData data) in dataAsset)
    {
        if (data.ForgeWith == Object.prismaticShardQID)
            enchantments.Add(EnchantmentManager.CreateEnchantment(dataAsset, id));
    }

    return enchantments;
}
```
</li> </ul> |  |
| `GetAvailableEnchantmentsForItem(…)` | Removed. <ul> <li>To check whether any enchantments can be applied to a tool through the forge, use `EnchantmentManager.CanForgeEnchant`.</li> <li>There's no directly equivalent method in 1.7, but you can mimic the former logic if needed. 
```c#
static List<Enchantment> GetAvailableEnchantmentsForItem(Item? tool, Item? item)
{
    var dataAsset = DataLoader.Enchantments(Game1.content);
    var contextTags = tool?.GetContextTags();

    List<Enchantment> enchantments = [];
    foreach ((string id, EnchantmentData data) in dataAsset)
    {
        if (data.ForgeWith == Object.prismaticShardQID && (contextTags is null || EnchantmentManager.MatchesTags(contextTags, data.AppliesTo)))
            enchantments.Add(EnchantmentManager.CreateEnchantment(dataAsset, id));
    }

    return enchantments;
}
```
</li> </ul> |  |
| `GetName()` | Removed. This inconsistently returned an ID, a display name, or `"Unknown Enchantment"` depending on the enchantment type. Use the new `Id` property or the pre-existing `GetDisplayName()` method instead. |  |
| `GetLevel()` | Removed. Use `Level` instead. |  |
| `GetType()` | This was sometimes to identify enchantments (like `enchantment.GetType() == expectedEnchantment.GetType()`). That's no longer reliable, since 1.7 introduces data-based enchantments which may not have their own C# type. You should use the new `enchantment.Id` property instead. |  |
| `ResetEnchantments()` | Removed. This is no longer needed since it no longer has a cached list of enchantment instances. |  |
| `Furniture` | `armchair`; `bed`; `bench`; `bookcase`; `chair`; `couch`; `decor`; `dresser`; `fireplace`; `lamp`; `longTable`; `other`; `painting`; `rug`; `sconce`; `table`; `torch`; `window` | Removed. Replaced by the `FurnitureType` enum (e.g. `Furniture.rug` is now `FurnitureType.Rug`). |
| `placementRestrictions` | Removed. Replaced by `CanPlaceIndoors` and `CanPlaceOutdoors`. |  |
| `GreenSlime` | `prismatic` | Removed. Replaced by `IsPrismatic`, which is just a wrapper around the monster tags field. |
| `Monster` | `getExtraDropItems()` | Removed. Drops should be added to `Data/Monsters` instead. Harmony patches which can't be moved into data can target `Monster.RollForDrops` instead. |
| `objectsToDrop` | Removed. Replaced by `drops`; see *monster changes* and *`Data/Monsters`*. |  |
| `Tool` | `GetEnchantmentLevel<T>()`; `GetEnchantmentOfType<T>()`; `hasEnchantmentOfType<T>()` | Removed. Replaced by `GetEnchantmentLevel(id)`, `GetEnchantment(id)`, and `HasEnchantment(id)` respectively. All vanilla enchantments have a constant for the ID like `EnchantmentIds.GalaxySoul`. |
| `Tool` | `ClearEnchantments()`;  `GetMaxForges()`;  `CanAddEnchantment(…)`;  `CopyEnchantments(…)`;  `GetTotalForgeLevels(…)`;  `AddEnchantment(…)`;  `RemoveEnchantment(…)`;  `CanForge(…)`;  `HasEnchantment(…)`;  `GetEnchantment(…)`;  `GetEnchantmentLevel(…)`;  `Forge(…)` | Removed. Enchantment functions have been moved to `Item` to make it easier for non-`Tool`s to support enchantments. NOTE: `Tool` is still the only type that can actually hold enchantments, since nothing else has the appropriate fields to hold them. |
| `Tool` | `drawTooltip<T>(…)`; `getExtraSpaceNeededForTooltipSpecialIcons<T>(…)`; `getDescriptionWidth<T>()`; `AddEquipmentEffects<T>()` | Removed. These were only overrides of `Item` functions that only handled enchantments, which is now handled in `Item` itself. |
| `AnimatedSprite` | `Clone` | No longer virtual. It now uses a similar model that `Item` does, with a virtual `CloneNew` for creating the instance and a virtual `CloneInto` for populating it. |
| `FarmerSprite` | `Clone` | Now returns a properly cloned `FarmerSprite`, rather than a semi-broken `AnimatedSprite` based on itself. |
| `Farmer` | `takeDamage<T>(…)` | The last argument has been changed from `Monster` to `object`, to support the ability to detect damage from non-`Monster` sources. An example of this is `BasicProjectile`, which previously passed in `null` for that parameter. |
| `BuffManager` | `BuffManager.AppliedBuffs` | More buff data is now synced, and so this dictionary is now a `NetDictionary`. |
| `Ring` | `onNewLocation(…)` ;  `onLeaveLocation(…)` | These are now overrides of a function on `Item`. All equipped items will now have this function called. |
| `Ring` | `update(…)` | This now a function called `updateWhenEquipped`, and is an override of the same virtual function in `Item`. All equipped items will have this function called. |
| `GameLocation` | `getFish(…)` | A new non-optional parameter has been added: `out Action onCatch`. Implementations of this method should no longer make changes to the world and instead be purely predictive. Any actual changes (ex. fish ponds changing how many fish it has, ginger island locations marking a golden walnut as collected, ...)  should be within `onCatch`, which will be called when the item is successfully reeled in. |

## Name translation changes for wiki maintainers
Some in-game item and monster names were retranslated in some languages for clarity or correctness.

This list is mostly provided to help wiki maintainers update pages, so it doesn't include names which only changed capitalization, changed `'` (straight apostrophe) to `’` (curly) in French, or aren't visible to vanilla players (like mine rocks).

*None yet.*

## XNB impact
### Overview
This section provides a summary of the XNB files which changed in Stardew Valley 1.7. This doesn't include new files (since they won't impact existing mods) or text changes in non-English files.

XNB mods are disproportionately affected, since...
# they replace the entire file;
# they're loaded through the MonoGame content pipeline which is less tolerant of format changes;
# they don't benefit from the compatibility rewrites Content Patcher provides for its content packs.

Content Patcher packs are typically unaffected (and Content Patcher will try to rewrite content packs automatically). However if a content pack replaces an entire asset instead of editing, it's more likely to be affected like an XNB mod.

### Changed assets
Compatibility is broadly grouped into four categories:
- "**✘ broken**": mods will remove new content or significant changes, or the mod's intended changes will no longer work. Effects may range from display bugs to crashes, depending how the game uses that specific content.
- "✘ will remove changes": mods will remove minor changes in 1.7 (e.g. typo fixes).
- "✓ mostly unaffected": mods will only be affected if they edit specific entries or fields.
- Blank: no expected impact for the vast majority of mods.

| content asset | changes in 1.7 | compatibility for pre-1.7 mods |  |
| --- | --- | --- | --- |
| XNB mods | Content Patcher packs |  |  |
| `Characters/Dialogue/Abigail` |   * Merged resort dialogue keys. * Fixed invalid dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Alex`; `Characters/Dialogue/Clint` | Merged resort dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Dwarf` | Fixed invalid dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Elliott` |   * Merged resort dialogue keys. * Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Evelyn` | Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Haley` | Merged resort dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Harvey` | Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Jodi` | Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Leah` | Merged resort dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Lewis` | Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/MarriageDialogue`; `Characters/Dialogue/MarriageDialogue*` |   * Merged some dialogue keys. * Merged some keys from the base asset into NPC-specific assets. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Maru`; `Characters/Dialogue/Penny`; `Characters/Dialogue/Sam` | Merged resort dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Sebastian` |   * Merged resort dialogue keys. * Fixed invalid dialogue key. | **✘ broken** | ✓ mostly unaffected |
| `Data/Boots` | Overhauled format. | **✘ broken** | **✘ broken**; (until Content Patcher adds runtime migrations) |
| `Data/Buffs` | Added new data field to data model. | **✘ broken** |  |
| `Data/Buildings`; `Data/Characters` | Added new item spawn field. | **✘ broken** |  |
| `Data/FishPondData`; `Data/FruitTrees` | Added new item spawn field. | **✘ broken** |  |
| `Data/Furniture` | Overhauled format. | **✘ broken** | **✘ broken**; (until Content Patcher adds runtime migrations) |
| `Data/GarbageCans`; `Data/GiantCrops` | Added new item spawn field. | **✘ broken** |  |
| `Data/Hats` | Overhauled format. | **✘ broken** | **✘ broken**; (until Content Patcher adds runtime migrations) |
| `Data/Locations` | Added new item spawn field. | **✘ broken** |  |
| `Data/Machines` | Added new item spawn field. | **✘ broken** |  |
| `Data/Monsters` | Overhauled format. | **✘ broken** | **✘ broken**; (until Content Patcher adds runtime migrations) |
| `Data/Movies` | Added new item spawn field. | **✘ broken** |  |
| `Data/Objects` |   * Added new data field to data model and new item spawn fields. * Added `fish_talk_coral` context tag for coral and sea urchin. | **✘ broken** | ✓ mostly unaffected |
| `Data/Pets`; `Data/Shops` | Added new item spawn field. | **✘ broken** |  |
| `Data/Weapons` |   * Added new item spawn field. * Added `is_galaxy_weapon` context tag for galaxy weapons. | **✘ broken** | ✓ mostly unaffected |
| `Data/WildTrees` | Added new item spawn field. | **✘ broken** |  |
| `Strings/1_6_Strings` | Moved some translations into `Strings/EnchanmentNames` and `Strings/Furniture`. |  |  |
| `Strings/EnchantmentNames` |   * Added translations moved from `Strings/1_6_Strings`. * Renamed two keys (`Auto-Hook` → `, <samp>Bug Killer` → `BugKiller`). | **✘ broken** | ✓ mostly unaffected |
| `Strings/Furniture` | Added translations for catalogue descriptions (moved from `Strings/1_6_Strings`). | **✘ broken** |  |
| `Strings/Objects` | Moved catalogue descriptions into `Strings/Furniture`. | ✓ mostly unaffected | ✓ mostly unaffected |
| `Strings/UI` | Merged some fish pond translation keys. | **✘ broken** | ✓ mostly unaffected |

### Removed or moved translation keys
These translation keys were moved or removed in 1.6.16. (For changes to the content, see *changed assets* above.)

| asset | key | changes |
| --- | --- | --- |
| `Strings/Locations` | `SeedShop_BuyBackpack_ResponseNo` | Key renamed to `GenericPurchase_Decline`. |

zh:模组:迁移至游戏本体1.7
