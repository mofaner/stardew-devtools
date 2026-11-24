> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Migrate_to_Stardew_Valley_1.6.16)

# Modding:Migrate to Stardew Valley 1.6.16 (`Migrate_to_Stardew_Valley_1.6.16`)

←Index

{{Modder compatibility header}}
1.6.16

This page helps mod authors prepare their mods for the upcoming Stardew Valley 1.6.16, and documents some of the changes and new functionality.

limit=3

## FAQs

### What's changing?
Stardew Valley 1.6.16...
- significantly overhauls events, which may break some mods (though most should still work fine);
- and adds many backwards-compatible changes and improvements for mod authors.

See the sections below for more details.

### Is this the modapocalypse?
Probably not. SMAPI and Content Patcher will automatically rewrite older mods for many of the changes, so they should still work as-is. Some mods may need an update. 

### How to update your mod
Here's a suggested 'quick start' guide to update a mod for Stardew Valley 1.6.16.

- **For C# mods:**: # Update for Stardew Valley 1.6.9 if you haven't already.
  # Rebuild the solution.
  # Fix any build errors and review any build warnings. You can search this page and previous migration guides for relevant info as needed.
  # Increment your mod version in `manifest.json`.
(Some mods may be added to SMAPI's compatibility blacklist due to runtime errors. Incrementing the mod version will fix that if so.)
  # Test to make sure the mod works.
  # Skim through this page, and check any section that might be relevant to the mod.

- **For Content Patcher packs:
  # Update for Stardew Valley 1.6.9 if you haven't already.
  # Skim through this page, and check any section that might be relevant to the mod.

- **For content packs which use another framework:**: See the documentation for the framework mod. Often you won't need to update the content pack if the framework mod itself was updated.

## Data asset changes
### New `Id` fields
Stardew Valley 1.6.16 adds `Id` fields for all data models which didn't have one, so content packs can easily edit them.

This affects...

| asset | added to fields |
| --- | --- |
| `Data/Buildings` | `AdditionalPlacementTiles`. |
| `Data/HomeRenovations` | `RectGroups`;; `RectGroups` → `Rects`;; `RenovateActions`;; `Requirements`. |
| `Data/Machines` | `AdditionalConsumedItems`. |
| `Data/MuseumRewards` | `TargetContextTags`. |
| `Data/Pets` | `Behaviors`;; `Behaviors` → `AnimationEndBehaviorChanges`;; `Behaviors` → `PlayerNearbyBehaviorChanges`;; `Behaviors` → `TimeoutBehaviorChanges`. |
| `Data/Shops` | `VisualTheme`. |
| `Data/SpecialOrders` | `Objectives`;; `RandomizedElements`;; `RandomizedElements` → `Values`;; `Rewards`. |
| `Data/Tools` | `UpgradeFrom`. |
| `Data/WildTrees` | `Textures`. |

### `Data/AdditionalWallpaperFlooring`
- The `Texture` asset no longer needs to be exactly 256 pixels wide.

### `Data/Achievements`
This has been overhauled into a data model asset, with...
- support for string achievement IDs;
- translations moved into a new `Strings/Achievements` asset;
- and new fields like `IsSharedAchievement` and `SendToChat` which weren't available in the old format.

The data asset now consists of a string → model lookup, where...
- The key is the unqualified item ID.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| basic info |  |
| `DisplayName`; `Description` | A tokenizable string for the achievement's in-game display name and description. |
| `IsSharedAchievement` | *(Optional)* Whether the achievement should be unlocked for all players when any player unlocks it. Default false. |
| in-game behavior |  |
| `UnlockCondition` | *(Optional)* A game state query which indicates when the achievement should be unlocked. If omitted, it must be unlocked manually (e.g. via the `addAchievement` event command). |
| `KnownBeforeUnlockCondition` | *(Optional)* If `ShowInCollection` is true, a game state query which indicates whether the achievement name and description should be shown on the collection tab before it's been unlocked; if false, it'll be shown as "???" with no description. Defaults to always shown. |
| `ShowInCollection` | *(Optional)* Whether the achievement should be listed on the collections tab. Default true. |
| `ShowNotification` | *(Optional)* Whether to show a HUD notification when the achievement is unlocked. Default true. This should usually be true for custom achievements. The field exists to support platform-only achievements in the base game. |
| `SendToChat` | *(Optional)* Whether to send a message to multiplayer chat when a player unlocks the achievement. Default false. |
| advanced |  |
| `PlatformIds` | *(Optional)* The achievement's ID within each platform, if different from the achievement ID. Default none. This generally isn't applicable for custom achievements. |
| `CustomFields` | *(Optional)* The custom fields for this entry. |

### `Data/BigCraftables`
Added one new field:

| field | effect |
| --- | --- |
| `CanFlipSpriteWhenPlaced` | *(Optional)* Whether the object has a 50% chance to horizontally flip its sprite when placed to improve visual diversity. Default false. |

### `Data/ChairTiles`
- Fixed support for tilesheet asset names containing a dot. (For example, a tileseet asset named `ExampleAuthor.ModName_Tilesheet` now correctly uses that as the data key instead of `ExampleAuthor`.)

### `Data/Characters`
<ul>
<li>Added new fields:

| field | effect |
| --- | --- |
| `FootstepSound` | *(Optional)* The sound to play when this NPC walks. This can be an audio cue ID, an empty string (`""`) for no sound, or omitted for the default sound. If omitted, the NPC plays the default `Cowboy_Footstep` sound. |
| `ForceOneTileWide` | *(Optional)* Whether the character should be one tile wide for the purposes of collisions and positioning, regardless of their sprite size. Default false. |
| `KissSpriteIndexWhenFlipped` | *(Optional)* The sprite index within the `TextureName` used when facing the opposite direction from `KissSpriteIndex`. If omitted or -1, it'll flip the sprite from `KissSpriteIndex` instead. This is assumed to be facing left, unless `KissSpriteFacingRight` is false. |
| `SpawnAsEphemeral` | *(Optional)* Whether the character is ephemeral when spawned automatically (e.g. via the `SpawnIfNeeded` field), so they're removed automatically before save and before the day ends. This doesn't affect NPCs which are already spawned, or which are spawned directly in game or mod code. Default false. |

</li>
<li>The `LoveInterest` field now affects Flower Dance dance partner selection.</li>
</ul>

### `Data/CookingRecipes`
These cooking recipe keys were changed to match their object name (like all the other recipes):

| old name | new name |
| --- | --- |
| Cheese Cauli. | Cheese Cauliflower |
| Cookies | Cookie |
| Cran. Sauce | Cranberry Sauce |
| Dish o' The Sea | Dish O' The Sea |
| Eggplant Parm. | Eggplant Parmesan |
| Vegetable Stew | Vegetable Medley |

This has no effect on the display text, but mods which reference these recipe keys will need to be updated.

### `Data/Events/*`
See *event changes* below.

### `Data/FishPondData`
- The `MaxPopulation` field is no longer limited to a maximum of 10.

### `Data/GiantCrops`
In the `HarvestItems` list, added one new field:

| field | effect |
| --- | --- |
| `ActionsOnDrop` | *(Optional)* The actions to run when the item is dropped. The actions are run once per entry drop. Default none. |

### `Data/LostItemsShop`
Added one new field:

| field | effect |
| --- | --- |
| `RequireCondition` | *(Optional)* The game state queries which must match to add this item. The `Target` player is the one being checked. This field should generally only be used with deterministic game state queries. |

If you specify multiple criteria fields, only one is applied in this order: `RequireMailReceived`, then `RequireEventSeen`, then `RequireCondition`.

### `Data/Objects`
Added one new field:

| field | effect |
| --- | --- |
| `CanFlipSpriteWhenPlaced` | *(Optional)* Whether the object has a 50% chance to horizontally flip its sprite when placed to improve visual diversity. Default false. |

### `Data/PassiveFestivals`
Added one new field:

| field | effect |
| --- | --- |
| `InLocations` | *(Optional)* The locations in which the festival happens. This affects various game logic like NPC dialogues, tent placement restrictions, weather, etc. If omitted, defaults to the keys in `MapReplacements`. |

### `Data/SpecialOrders`
<ul>
<li>Added new fields:

| field | effect |
| --- | --- |
| `CanBeCancelled` | *(Optional)* Whether the special order can be cancelled. In multiplayer, any player cancelling the special order will remove it for all players. Default false. |
| `ActionsOnReward` | *(Optional)* A list of trigger actions to run when the special order is completed. These are run locally for each player. This field accepts special order tokens. For example: 
```js
"ActionsOnReward": [
    "AddMail Current {{ModId}}_MailId Received",
    "MarkCookingRecipeKnown Current Hopper"
]
```
 You should usually prefer `ActionsOnReward` instead of `Rewards`, *except* that the `Gems` (Qi board only), `Object` (town board only), or `Money` reward types are drawn in the special order board UI. |

And improved one field:

| field | effect |
| --- | --- |
| `Duration` | This can now be set to `"Indefinite"` for a special order with no expiry. It will remain active until it's completed or cancelled. Consider enabling `CanBeCancelled` if applicable. |

</ul>

### `Data/Tools`
Added one field:

| field | effect |
| --- | --- |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |

### `Data/Trinkets`
Added one field:

| field | effect |
| --- | --- |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |

### `Data/Weapons`
Added one field:

| field | effect |
| --- | --- |
| `ContextTags` | *(Optional)* The custom context tags to add for this item (in addition to the tags added automatically based on the other item data), specified as an array. |

## Changes for all mod authors
### Cache changes
Stardew Valley 1.6.16 adds internal caching to improve performance. Typically the relevant caches will be reset right away.

Some entities have been changed to take advantage of this, which may impact some mods.

| entity type | field | effect | technical notes |
| --- | --- | --- | --- |
| Items | Sprite index | If `SpriteIndex` in the data asset is changed, existing items will be updated to match (unless something else has overridden it for that existing item, such as certain big craftables changing appearance based on the season). | Items in existing saves will be updated for this when loaded in 1.6.16, assuming their sprite index matches the data asset value. The `parentSheetIndex` netfield will have a value of -1 when the data asset value should be used. The `ParentSheetIndex` property will have the "effective" `parentSheetIndex` (meaning that if the netfield has -1, it will return the data asset value). The value from the data asset can easily retrieved from the `DataParentSheetIndex` propery. |

### Context tag changes
Added new context tags:

| context tag | item types | behavior | effect |
| --- | --- | --- | --- |
| `axe`; `error_tool`; `fishing_rod`; `generic_tool`; `hoe`; `melee_weapon`; `milk_pail`; `pan`; `pickaxe`; `shears`; `slingshot`; `wand`; `watering_can` | tools and weapons | informational | The C# type which implements the tool or melee weapon. Note that the vanilla scythes are `melee_weapon`. See also the `is_scythe` context tag below. |
| `is_edible`; `is_edible_bad`; `is_edible_good` | objects | informational | Indicates the item can be eaten/drunk. This is set automatically based on the item's `edibility` value: * -299 to -1: `is_edible is_edible_bad`; * 0: `is_edible`; * 1+: `is_edible is_edible_good`. |
| `fish_skip_minigame` | *any* | functional | If the player catches this item while fishing, it'll be caught directly without the fishing minigame. This matches the behavior when catching junk items like seaweed (which now have the context tag where needed). |
| `ignore_gift_limits` | *any giftable item* | functional | Makes this item ignore the gift limits when given as a gift (similar to stardrop tea). Items with this tag don't count towards the daily/weekly limits, and can be given even if those limits have been reached. |
| `is_scythe` | tools and weapons | functional | Marks the tool as a scythe. This is set for vanilla scythes through `Data/Weapons`. |
| `spawns_butterflies` | furniture | functional | Whether butterflies spawn around this furniture, similar to the butterfly hutch. |
| `exclude_from_furniture_catalogue` | furniture | functional | Prevents the furniture from showing up in the furniture catalogue |
| `trash_bear_ignore` | *any cooking recipe output* | functional | When the trash bear requests random items produced by cooking recipes, items with this context tag will be skipped. |
| `upgrade_level_*` | tools and weapons | informational | The tool upgrade level. These tags are added automatically based on the tool's current level. For example, a copper axe will have both `upgrade_level_0` (base axe) and `upgrade_level_1` (copper axe). |

### Debug command changes
<ul>
<li>Added new commands:

| command | description |
| --- | --- |
| `giantCrop <id> [x] [y]` | Place the specified giant crop ID at the given tile position. The ID is case-insensitive, and will list fuzzy matches if an exact match isn't found. If the position isn't specified, it'll be placed just to the right of the farmer. |
| `heldItem` | Shows info about the currently held item like its name, category, item ID, and context tags. |
| `mineAll`; `volcanoAll`; `warpAll` | Warps you through every top-level location (`warpAll`), mine levels 1–120 and the quarry mine (`mineAll`), or volcano dungeon level (`volcanoAll`). After 1.5 seconds in each location, you'll automatically warp to the next. This will continue until you've warped through every available location, at which point you'll warp one last time to your farmhouse/cabin. **⚠️ Side effects:** * `warpAll` will remove the perfection flag on your save. * `mineAll` will affect stats like 'deepest level reached' and achievements like To The Bottom. |
| `testAchievement <achievement ID>` | Shows diagnostic info about an achievement's unlock conditions and the current player's progress towards them. The <achievement ID> is the achievement's unique key in `Data/Achievements`.. |
| `placeTemporaryWarp <targetLocation> <targetTileX> <targetTileY> [positionOffsetX] [positionOffsetY]` | Places a warp at the player's position (adjusted by the amount of tiles indicated by [positionOffsetX] and [positionOffsetY], if specified) leading to (<targetTileX>, <targetTileY>) in <targetLocation>. If the player is touching the resulting warp (such as if no offset was specified), they are immediately warped. This warp does not persist across saves, and may disappear at any time based on other game code. |
| `currency <currencyType> <amount>` | Gives yourself <amount> of the specified currency type. Valid currency types are: |
| currency ID | name |
| `0` | 0 |
| `1` | 0 Token |
| `2` | 0 Qi |
| `4` | 0 Gem |

|-
| `printEvent`
| If there is an event currently running, prints the event ID and current event command.
|}</li>
<li>Improved existing commands:

| command | description |
| --- | --- |
| `achieve`; `achievement` | Merged `achieve <ID>` (unlock platform achievement) and `achievement <ID>` (unlock in-game achievement) into one command that unlocks both automatically based on the achievement data. The `achieve` command is now just an alias for `achievement`. |
| `addAllCrafting` | No longer fails if the player already knows some of the crafting recipes. |
| `animalInfo`; `characterInfo`; `daysPlayed`; `gamepad` | Now shows output in the console instead of showing a HUD notification. |
| `cookingRecipe`; `craftingRecipe` | No longer throws an exception if the player already knows the recipe. |
| `dye` | Now accepts any standard color value. |
| `experience` | Fixed validation errors for the 'skill' argument never logged correctly. |
| `fuzzyItemNamed` | Now drops multiple items if the requested count exceeds the item's max stack size. |
| `gameQuery` | Can now be used with queries which check an item; the `Input` and `Target` item are now the item selected on the player's toolbar. |
| `jump` | Fixed error if the NPC doesn't exist. |
| `perfection` | Fixed error when run on a save which already has some requirements partly met. |
| `setupBigFarm` |   * No longer fails if some of the crops, flooring, fruit trees, or grass can't be placed because the tile is occupied. * No longer fails if a building on the farm no longer has data. |
| `slimecraft` | No longer fails if you already have one or both recipes. |
| `toggleTimingOverlay` | Now shows more metrics like FPS and memory usage. |
</li>
<li>Removed some commands:

| command | description |
| --- | --- |
| `furniture`; `hat`; `pole`; `shirt`; `tv`; `wallpaper` | Replaced by generic commands like `item` and `fuzzyItemNamed`. |
| `getIndex`; `listTags`; `lookup`; `qualifiedId` | Replaced by `heldItem`. |
| `lantern`; `refuel`; `stopRafting` | These were unimplemented or cut content. |
| `shiftToolbarLeft`; `shiftToolbarRight` | Replaced by in-game hotkeys. |

</li>
</ul>

### Diagnostic chat commands
<ul>
<li>Stardew Valley 1.6.16 enables hidden 'diagnostic commands' in chat, which are enabled for all players but not shown by `/help`. These allow *specific* console debug commands to be run even if cheat commands aren't enabled:

| command | description |
| --- | --- |
| diagnostic commands |  |
| `daysPlayed`,; `dp` | Shows the number of in-game days since the game started (starting at 1 on spring 1 in year 1). |
| `animalInfo` | Shows the total number of farm animals in all locations. |
| `CharacterInfo` | Shows the total number of NPCs in the current location. |
| `doesItemExist` | *Syntax*: `doesItemExist <item ID>` Shows whether the given qualified or unqualified item ID exists anywhere in the world. This doesn't indicate where the item is located if any were found. See the `whereIsItem` command for more detailed output. |
| `gameQuery`,; `gq` | *Syntax*: `gameQuery <query>` Parses a game state query and shows the result. |
| `getStat` | *Syntax*: `getStat <stat key>` Shows the value of a tracked stat. |
| `heldItem` | Shows info about the currently held item like its name, category, item ID, and context tags. |
| `listLights` | Shows debug info about all currently active light sources. |
| `logBandwidth` | Toggles whether multiplayer network bandwidth info should be written to the SMAPI console or (if `/logFile` was enabled) the log file. |
| `logFile` | Begins logging information to a file on disk, if supported by the platform. This can be useful for troubleshooting errors. |
| `logSounds` | Toggles whether each sound effect played should be written to the SMAPI console or (if `/logFile` was enabled) the log file. |
| `mineInfo` | Shows two mine-related stats (lowest level reached and deepest mine level). |
| `printPlayerPos`,; `ppp` | Shows the player's current position in both tile and pixel coordinates. |
| `sdlVersion`,; `sdlv` | Shows the current SDL version. (SDL is the low-level input library used by the game.) |
| `showExperience` | *Syntax*: `showExperience <skill ID>` Shows the player's total experience points for the given skill. The <skill ID> is one of `0` (farming), `1` (fishing), `2` (foraging), `3` (mining), or `4` (combat). |
| `testAchievement` | *Syntax*: `testAchievement <achievement ID>` Shows diagnostic info about an achievement's unlock conditions and your progress towards them. The <achievement ID> is the achievement's unique key in `Data/Achievements`. |
| `toggleTimingOverlay`,; `tto` | Shows an on-screen overlay with basic info for performance monitoring. |
| `tokens` | *Syntax*: `tokens <expression>` Parses a tokenizable string and shows the result. |
| `version` | Shows the game's *assembly* version. This usually matches the version shown in-game (e.g. 1.6.15 build 24356 has assembly version `1.6.15.24356`). |
| `whereIsItem`,; `whereItem` | Searches the entire world (including other players' inventories) for items matching the qualified or unqualified item ID, and lists all matches. Unlike the equivalent console command, this doesn't list spawned objects like artifact spots and weeds. |
| other commands |  |
| `copyOutfit` | Copies your current outfit (*i.e.* pants, shirt, etc) into your desktop clipboard in an XML format, and also shows it in the chat box. |
| `holdItem` | Make your player character play the 'hold up item' animation using your currently selected item. |
| `sprinkle` | Show a sparkle animation around your player character (the same animation used when applying fairy dust to a machine). |
</li>
<li>It also removes these chat commands:

| command | reason |
| --- | --- |
| `logFile`; `sdlVersion` | Replaced by the equivalent `debug` commands above. |
</li>
</ul>

### Dialogue changes
<ul>
<li>Added `Characters/Dialogue/Default` asset. Dialogues in this asset apply to every NPC, except where the key is also defined in their own dialogue asset. This replaces some previous translations.</li>
<li>Added new dialogue keys in `Characters/Dialogue/*`:

| key | description |
| --- | --- |
| *passive festival dialogue* | Identical to location dialogue, but you can use the passive festival ID as the location name. Any passive festival dialogue has priority over location dialogue in the same location. |
| `FirstMeeting` | If set, shown the first time the player ever speaks to that NPC. |
| `AskedToDance_Prompt`; `AskedToDance_Prompt_Spouse`; `AskedToDance_Ask`; `AskedToDance_Cancel` | At the Flower Dance, the prompt which lets the player ask the NPC to dance. If omitted, defaults to the matching key in `Characters/Dialogue/Default`. |
| `TermsOfEndearment_Roommate`; `TermsOfEndearment_Spouse`; `TermsOfEndearment_Spouse_Rare`; `TermsOfEndearment_Spouse_VeryRare` | The pet names which the NPC can use when addressing the player. This affects the message shown above their head when the NPC says good night (80% of the time), and the `%endearment` and `%endearmentlower` marriage dialogue tokens (not used in any vanilla dialogues). Each translation is a comma-delimited list of possible pet names. Each time it's displayed, one of the values will be selected at random. The translations are selected in this order: # `TermsOfEndearment_Roommate` if the NPC is a roommate. # `TermsOfEndearment_Spouse_VeryRare` (8% chance) or `TermsOfEndearment_Spouse_Rare` if the player has at least 9 hearts with the NPC, and the game requested a 'happy' term. This is only used when the NPC says good night, with a 10% chance. # `TermsOfEndearment_Spouse` if the player has at least 9 hearts with the NPC. # Otherwise the NPC just uses the player's name. If omitted, defaults to the matching key in `Characters/Dialogue/Default`. |
</li>
<li>Added new conversation topics:

| id | trigger |
| --- | --- |
| `specialOrderComplete_<id>` | Player completes the given special order for the first time. |
</li>
<li>Fixed `<location>_<x>_<y>` and `<location>_<dayOfWeek>` dialogue keys never used in 1.6.0+ if a player has at least two hearts with the NPC.</li>
<li>Fixed location dialogues not recognizing the `spring` prefix (they only allowed `summer`, `fall`, and `winter`).</li>
<li>Fixed `$r` response IDs not tracked correctly if you reuse an NPC reaction key across multiple `$r` options.</li>
</ul>

See also *other translation changes*.

### Ephemeral characters
You can now mark a character or farm animal as *ephemeral* (via `SpawnAsEphemeral` in `Data/Characters` or `character.IsEphemeral` in C#), which means they'll be removed automatically before saving and before the day ends. This works for any character in the `characters` or `animals` location fields.

In particular, this allows adding custom classes to the world since they're guaranteed to be removed before the game saves. Ephemeral characters are still synchronized in multiplayer. If a monster is ephemeral, any monsters it spawns will inherit its ephemerality (e.g. slimes spawned by a big slime).

In the base game, this replaces the previous custom logic which managed...
- farm Junimos;
- community center Junimos;
- Marnie's cows (which are now in the standard `gameLocation.animals` field);
- Morris and the Old Mariner;
- movie theater patrons;
- monsters on the farm, island farm, secret woods, witch's hut, and witch's swamp;
- and SquidFest + Trout Derby contestants.

### Event changes
#### Multilingual events
All vanilla events are now multilingual, with a single copy of each event script which uses translation keys. Translations from the old per-language event files were moved into a per-language `Strings/Events` file.

For example:

| files in 1.6.15 | files in 1.6.16 |
| --- | --- |
| 
```text
Data/Events/Beach
Data/Events/Beach.de-DE
Data/Events/Beach.es-ES
Data/Events/Beach.fr-FR
Data/Events/Beach.hu-HU
Data/Events/Beach.it-IT
Data/Events/Beach.ja-JP
Data/Events/Beach.ko-KR
Data/Events/Beach.pt-BR
Data/Events/Beach.ru-RU
Data/Events/Beach.tr-TR
Data/Events/Beach.zh-CN
```
 | 
```text
Data/Events/Beach
```
 |

Content Patcher content packs should generally be unaffected, since their edits were applied to the active version of the file automatically (which will just become the base file in all languages).

In rare cases, a mod which directly targets a specific localized asset name like `Data/Events/Beach.fr-FR` may no longer work or break vanilla events (depending on how it edits them). That should be a very rare case, since the APIs for both Content Patcher and SMAPI were designed to discourage doing that.

#### Forking deprecated
All vanilla events which used forking now use goto labels instead.

**Forking still works like before** for backwards compatibility, but mod events should prefer goto labels since...
- they work more consistently and intuitively with other features like skip labels;
- it's the approach that the game will be applying and developing in future versions;
- deprecated commands will be reported as an error if you use the `EventValidator`;
- and there's no guarantee forking will always work in future versions.

The fork-related event commands are deprecated.

#### Removed manual event pause management
When a dialogue or menu is shown, or certain animations are playing, the event needs to pause until they finish. How that works changed:

| version | behavior |
| --- | --- |
| <1.6.16 | The event was paused using a combination of the `null` command (which pauses the event indefinitely), manually incrementing `event.CurrentCommand` (sometimes incremented twice to skip the `null`), and special handling in command logic while a menu was open. This was tricky and led to many edge cases in both the base game and mods (e.g. soft locks if a `null` command didn't get skipped correctly, other commands getting skipped unintentionally, commands not working correctly if a menu was opened, etc). |
| ≥1.6.16 | This is much simpler in 1.6.16+: * Events now pause automatically while any menu is up, and resume once it's closed. That guarantees that no menu is open when a command handler is called. * Characters/players add an entry to `event.PauseWhile` to pause the event until they finish certain animations (e.g. showing an emote or holding up an item). This means that `null` commands and extra `event.CurrentCommand` increments are no longer needed in most cases. |

Update note for mod events:
- You no longer need a `null` command after `awardFestivalPrize` or `itemAboveHead`. (For backwards compatibility, the game will ignore a `null` command immediately after either of those.)

**Breaking changes for custom commands added by C# mods:**
- The game no longer auto-increments `event.CurrentCommand` when closing dialogues, certain menus (`ItemGrabMenu`, `ItemListMenu`, `MenuWithInventory`, `ReadyCheckDialog`, and `TutorialMenu`), or methods which open them (like `Game1.drawDialogue`, `Game1.drawDialogueNoTyping`, `Game1.drawObjectDialogue`, and `Game1.multipleDialogues`). Custom commands which open one of those must now increment `event.CurrentCommand` like any other command.
- When a menu is opened, no further command will run until it's closed. Event updates still happen (e.g. NPCs will continue moves they already started), but command logic which checks if a menu is open will never work.

#### Precondition slash now required for numeric event IDs
In Stardew Valley 1.6.0–15, the behavior of an event key with no `/` depended on whether the ID was numeric. A numeric event ID like `10040609` was treated as an event with no precondition, while a string event ID like `PlayerKilled` was treated as a manual script. In 1.6.16+, the behavior is now consistent: any event ID without a slash (like `10040609`) is treated as a manual script, and any with a slash (like `10040609/`) is treated as an event.

In other words:

| event key format | behavior |
| --- | --- |
| `<id>`; (string ID) | manual script (unchanged) |
| `<id>`; (numeric ID) | event (1.6.15) → manual script (1.6.16) |
| `<id>/[preconditions]` | event (unchanged) |

#### New event commands
| command | description |
| --- | --- |
| `addAchievement <achievement ID>` | Unlock an achievement for the current player. The <achievement ID> can be a platform achievement ID (like `Achievement_LocalLegend`) or internal ID (like `17` for Gourmet Chef). |
| `addActor <actorName> <x> <y> <direction>` | Add a character or player to the event. This is mostly equivalent to adding them through the character positions field, but can be done at any point in the script (e.g. for use with `goto` for event variations). |
| `choose <question> [<answer> <go to label>]+` | Show a dialogue box to choose from a list of responses, then optionally skip to a label based on the selected answer. Arguments: |
| argument | usage |
| <question> | The translation key or literal text to show above the choices. This can be empty (`""`) to omit the question line. |
| <answer> | The translation key or literal text for an answer to show. |
| <go to label> | If the player chooses this answer, jump to this label in the event (see the `goto` command for more info). This can be `""` or `_` to continue to the next command instead. |

For example, here's a simple choice which doesn't affect anything:

```text
choose "Do you like apples?" "I love them." _ "I hate them." _/
```

Or you can change the course of the event based on the choice:

```text
speak Abigail "Do you... like me?$l"/
choose "" "Yes!" happy "No." heartbroken/

label heartbroken/
speak Abigail "Oh. I understand.$s"/
faceDirection Abigail left/
pause 3000/
end/

label happy/
speak Abigail "We should explore the mines together next time!$h"/
...
```

|-
| `completeQuest <id>`
| Mark a quest complete. If the quest has no reward to collect, this also removes it from the in-game journal.
|-
| `conditionSpeak <npc> [<dialogue> [condition]]+`
| Make an NPC say a dialogue based on arbitrary conditions. The first dialogue with a matching dialogue is spoken; if none match, the command is skipped.

Arguments:

| argument | usage |
| --- | --- |
| <npc> | The internal name of the NPC who should speak (which can be marked optional). |
| <dialogue> | The translation key or literal text for the dialogue to speak. If omitted or empty, the command is skipped when the condition is matched. |
| [condition] | One of: * a game state query; * or `ANSWERED <response ID>+` as a shortcut for the `PLAYER_HAS_DIALOGUE_ANSWER Current` query; * or omitted/blank to always match this dialogue if no previous one matched. |

For example, here Maru says a different line if the player suggested she tell the truth:

```text
conditionSpeak Maru "I accidentally dropped one of the samples... I'm so sorry.$s" "ANSWERED 38" "@ came in and slammed the door so loud I dropped this sample.$u"
```

|-
| `goto [<target> [condition]]+`
| Skip forward or backward to a specific position in the event script. Each <target> can be:

| target | description |
| --- | --- |
| `end` | Jump to the last `end` event command in the script. |
| *any other value* | Jump to the first `label` command with this name. |

You can list any number of label/condition pairs, and it will go to the first label with a matching condition. If a condition is omitted, it defaults to always true. If none of the conditions match, it continues to the next command normally.

For example, this event script uses `goto` to repeat a question until a specific answer is selected:

```text
.../
label question/
choose Abigail "Hey! Did you have your eye on someone?" "Abigail" correct "Penny" wrong "Alex" wrong/

label wrong/
speak Abigail "Oh?$u"/
pause 500/
goto question/

label correct/
speak Abigail \"I thought so!$l\"/
pause 500/
end
```

You can make `goto` conditional using game state queries. For example, this will go to the `divorced` label if you divorced Abigail, and continue to the next command if not:

```text
goto divorced "PLAYER_NPC_RELATIONSHIP Current Abigail Divorced"/
```

|-
| `label <label>`
| Set a named position in the event script, which you can go to using the `goto` command.

**Note:** you can't use `end` as a label name, since that automatically matches the `end` event command.
|-
| `removeSpriteById`
| Remove a temporary animated sprite (e.g. added via the `temporaryAnimatedSprite` command) using its ID.
|}

#### Improved existing commands
| command | description |
| --- | --- |
| `gainSkill` | Added argument validation. |
| `move` | Fixed event softlock if it only applies to missing optional NPCs. |
| `removeSprite` | Renamed to `removeSpriteAt`. The old name still works as an alias. |
| `skippable` | Added an optional [skip to label] argument. If set, the event will jump to that label when the player clicks the skip button (see `goto` for more info). The last `skippable` command run before the player skips is applied. This is meant to clean up or grant unlocks before the event ends. For example: 
```text
skippable beforeEnd/
.../

label beforeEnd/
gainSkill Fishing 1/
addItem (T)TrainingRod/
end
```
 Technically you can jump to any point in the event (e.g. to cancel the skip). However, the skip action will still cancel any ongoing event behavior like ongoing player/NPC movements, so you should be careful doing this. |
| `splitSpeak` | You should now specify each dialogue as a separate argument (though the old syntax will still work): 
```text
// in 1.6.15
splitSpeak Sam "You think so?~Yeah... kids should play and have fun while they still have the chance."

// in 1.6.16
splitSpeak Sam "You think so?" "Yeah... kids should play and have fun while they still have the chance."
```
 This also now works with the new `choose` command too. |
| `spriteText` | The [text] argument can now be a translation key to get the text from. |
| `temporaryAnimatedSprite` | <ul> <li>The position (index 8) and scale (index 14) fields are no longer restricted to integer values. (This applies to both forms of the command.)</li> <li>Added more flag options: |
| flag | effect |
| `id <int id>` | A unique ID for this sprite. Default none. This can be passed to other event commands (like `removeSpriteById`) or flags (like `parent_sprite`). |
| `alpha` | The sprite's transparency, as a value between `0` (transparent) and `1` (opaque). Default 1. |
| `stop_accelerating_when_velocity_is_zero` | When the current motion passes zero, whether to (a) set the X/Y accelerations to zero and (b) apply the `after_accel_stop_motion_[x\|y]` options. Default false. This is used for item-above-head sprites. |
| `after_accel_stop_motion_x <float x>`; `after_accel_stop_motion_y <float y>` | The motion value to set when `stop_accelerating_when_velocity_is_zero` is triggered. Default 0. |
| `x_stop_coordinate <float x>`; `y_stop_coordinate <float y>` | If set, the target coordinate at which motion and acceleration on that axis stop. The sprite will keep moving normally until it reaches or passes that coordinate. Default -1 (disabled). |
| `firework_type <int type>` | If set, adds the fireworks glows and (when the animation completes) explosion for the sprite. The possible types are `0` (red), `1` (blue), `2` (green). Default none. Requires the `set_owner` and `set_location` flags. |
| `vertical_flipped` | If set, mirror the sprite vertically (i.e. flip top-to-bottom). Default false. Ignored if `flipped` is set. |
| `vector_scale <float x> <float y>` | The horizontal and vertical scale to apply to the sprite. Default none. If set, the scale (index 14) argument is ignored. |
| `alpha_fade_fade <float alpha>` | The multiplier applied to the alpha change (index 13) argument each frame. For example, specifying 2 means the sprite will fade out twice as fast. Default 1. |
| `scale_change_change <float scale>` | The multiplier applied to the scale change (index 15) each frame. For example, specifying 2 means the sprite will grow/shrink twice as fast. Default 1. |
| `light_id <string id>` | If set, add a light with the given unique ID which follows this sprite. Default none. See the next flags to configure the light. |
| `light_color <string color>` | If `light_id` is set, the color to tint the light. Defaults to #004180. Setting this to `White` (or equivalent) will apply the default color. |
| `light_radius <float scale>` | If `light_id` is set, the size of the lighted area. |
| `light_fade <int value>` | If `light_id` is set, the amount to subtract from the light's opacity each draw tick. Specified as a value between 0 (none) and 1 (instantly becomes transparent). Default none. |
| `x_periodic`; `y_periodic` | If set, make the sprite oscillate horizontally and/or vertically along a sine wave (based on the next options). When both are specified, this causes the sprite to move in a circle. When set, the acceleration and motion values are ignored. |
| `x_periodic_range <float x>`; `y_periodic_range <float y>` | If `x_periodic` and/or `y_periodic` are set, the pixel amplitude of the periodic motion. Default none. |
| `x_periodic_loop_time <float x>`; `y_periodic_loop_time <float y>` | If `x_periodic` and/or `y_periodic` are set, the time in milliseconds for one full oscillation cycle. Default none. |
| `shake_intensity <float value>` | If set, the max pixel offset to apply random each frame. Default none. |
| `shake_intensity_change <float value>` | If set, apply the elapsed duration multiplied by this value to the `shake_intensity` each frame. Default none. |
| `pulse` | If set, rhythmically and continuously grow and shrink the sprite size. Default false. See the next flags to configure the pulse. |
| `pulse_amount <float value>` | If `pulse` is set, the scale multiplier to apply each tick. For example, 1.1 will grow/shrink the sprite by 10% per tick. Default `1.1`. |
| `pulse_time <float ms>` | If `pulse` is set, the time in milliseconds before the pulse switches direction. Default zero (i.e. alternates every tick, or 30 times per second). |
| `start_sound <string name>`; `end_sound <string name>` | The audio cue ID to play when the animation starts and/or ends. Default none. |
| `text <string text>` | If set, draws the given text instead of the actual sprite. The displayed text is affected by `color` and all transform fields, but unaffected by `flip` (index 11) or `vertical_flipped`. |
| `attached_actor <string name>` | The name of the event actor NPC to which to attach the sprite. Default none. If set by itself, this moves the attached NPC to follow the sprite. This may be overridden by the next flag. |
| `position_follows_attached_actor` | If `attached_actor` is set, causes the sprite to follow the attached NPC (instead of moving the NPC to follow the sprite). In that case, the sprite's position is relative to the NPC's position. |
| `parent_sprite <int id>` | The ID of the parent sprite. If set, this sprite's position is relative to the parent sprite's position. |
| `draw_above_always_front` | If set, draw this sprite above the `AlwaysFront` layer (so it's above all map layers and features). |
| `delay_before_animation_start <int delay>` | If set, a millisecond delay before the animation starts playing. |
| `local` | If set, draws this sprite relative to the viewport (top-left corner of the screen) instead of the world position. |
| `set_owner` | If set, sets the sprite owner field to the current player. This is required for some sprite behaviours like bombs and fireworks. |
| `set_location` | If set, sets the sprite location field to the current location. This is required for some sprite behaviours like bombs and fireworks. |
</li>
</ul>
|-
| `textAboveHead`
| Can now be applied to players.
|}

#### Deprecated commands
These event commands are deprecated. **They still work just like before** (except the ones at the bottom of the table), but if you use the `EventValidator` it'll report them as an error.

| event command | migration |
| --- | --- |
| `fork`; `resetVariable`; `switchEvent` | Use the `choose` or `goto` commands instead. If you used dialogue containing the `%fork` token, it should be removed and replaced with equivalent event commands. |
| `question forkX` | Use the `choose` command instead, and replace the event fork with a label. For example, this script: 
```text
// main event
question fork1 "#I believe in you!#Wow, you're really arrogant."/
fork arrogant/
speak Alex "Thanks, @. I'll remember that."/
...

// 'arrogant' event
emote Alex 12/
speak Alex "Hmmph... I think someone's a little jealous.$a"/
```
 Becomes this script: 
```text
choose "" "I believe in you!" believe "Wow, you're really arrogant." arrogant/

label believe/
speak Alex "Thanks, @. I'll remember that."/
...

label arrogant/
emote Alex 12/
speak Alex "Hmmph... I think someone's a little jealous.$a"/
...
```
 Forks often duplicated the end of the script. In that case, you can have both variants just `goto` the common ending instead. |
| `question null` | Use `choose` with `_` labels instead. (Setting a label argument to `_` means it'll just continue to the next command instead.) For example, this command: 
```text
question null "But what am I going to say to Vincent?#It's best to be honest with kids.#You did the right thing. Kids should have hope."/
```
 Would become this command: 
```text
choose "But what am I going to say to Vincent?" "It's best to be honest with kids." _ "You did the right thing. Kids should have hope." _/
```
 |
| `quickQuestion` | Use the `choose` command instead. The <question> and <answerX> have matching `choose` arguments, while the <answerX script> arguments become labels. For example, this command: 
```text
quickQuestion #I'd like to help#Nah, I'll pass...(break)emote Willy 32\speak Willy "Aye... I thought you would.$h#$b#I'll leave this door unlocked for ya... you can come drop off the materials whenever you like."(break)emote Willy 28\speak Willy "Alright, suit yourself...$s#$b#If you have a change of heart, you can always come back and help out. I'll leave this door unlocked, anyhow."/
```
 Becomes this script: 
```text
choose "" "I'd like to help" accept "Nah, I'll pass..." refuse/

label accept/
emote Willy 32/
speak Willy "Aye... I thought you would.$h#$b#I'll leave this door unlocked for ya... you can come drop off the materials whenever you like."/
goto resume/

label refuse/
emote Willy 28/
speak Willy "Alright, suit yourself...$s#$b#If you have a change of heart, you can always come back and help out. I'll leave this door unlocked, anyhow."/

label resume/
```
 |
| `cutscene bandfork`; `elliottBookTalk`; `question haleyDarkRoom`; `question shaneCliffs`; `question shaneLoan` | These commands are **deleted** and will no longer work. They're unlikely to be used by mod events, but see their relevant events for the new equivalents if needed. |

#### Changed event keys
Events were generally unified and de-hardcoded in 1.6.16. In particular...

<ul>
<li>Several events were previously defined in the game's C# code, where they couldn't be changed by content packs. Most of these were moved into `Data/Events/*` with translations in `Strings/Events`:

| event | new asset name | new event ID |
| --- | --- | --- |
| forest raccoon finished event | `Data/Events/Forest` | `RaccoonFinish` |
| forest trash bear cleanup | `Data/Events/Forest` | `777111` |
| island Gourmand crop inspection | `Data/Events/islandFarmCave` | `GourmandInspection` |
| summit perfection cutscene | `Data/Events/Summit` | `SummitPerfection` |
| summit cheat event | `Data/Events/Summit` | `SummitCheat` |
</li>
<li>Others were moved from non-event data into `Data/Events/*`:

| event | old location | new asset | new event ID |
| --- | --- | --- | --- |
| Birdie quest intro & completed | `Strings/Locations:IslandSecret_Event_BirdieIntro`; `Strings/Locations:IslandSecret_Event_BirdieFinished`; `<samp>Strings/Locations:alreadyGotNuts` | `Data/Events/IslandWest` | `-888999` (intro); `-666777` (completed) |
| field office intro & completed | `Strings/Locations:IslandFieldOffice_Intro_Event`; `Strings/Locations:FieldOfficeFinale` | `Data/Events/IslandFieldOffice` | `FieldOfficeIntro`; `FieldOfficeDone` |
| Leo intro | `Strings/Locations:IslandHut_Event_ParrotBoyIntro` | `Data/Events/IslandHut` | `LeoIntro` |
| Meowmere event | `Strings/1_6_Strings:ForestPylonEvent` | `Data/Events/WizardHouseBasement` | `ForestPylon` |
| Mr. Qi Skull Cavern level 100 | `Data/ExtraDialogue:SkullCavern_100_event`; `Data/ExtraDialogue:SkullCavern_100_event_honorable` | `Data/Events/SkullCave` | `SkullCavern100` |
| Professor Snail exits cave | `Strings/Locations:IslandNorth_Event_SafariManAppear` | `Data/Events/IslandNorth` | `ProfSnailIntro` |
</li>
<li>Other event keys were changed or merged as part of the other event changes:

| asset | entry key | change in 1.6.16 |
| --- | --- | --- |
| `Data/Events/AbandonedJojaMart` | `missingBundleComplete` | Event key changed to `192393` to match the event ID. |
| `Data/Events/Farm` | `992559/t 600 1130/n clothFound/o Emily/w sunny`; `992559/t 600 1130/n clothFound/O Emily/w sunny` | Merged into a single `992559/Time 600 1130/LocalMail clothFound/Weather sunny` event. |
| `Data/Events/QiNutRoom` | `10040609` | Event key changed to `10040609/` per the precondition change. |
| `Data/Events/SamHouse` | `94/e 93/k 95/t 1800 1950/i 136/y 1`; `94/e 93/k 95/t 1800 1950/i 136/y 2` | Merged into a single `95/SawEvent 93/!SawEvent 94/Time 1800 1950/HasItem 136` event. |
| `Data/Events/Town` | `611173/Hn pamHouseUpgrade/o Penny` | Merged into the existing `611173/Hn pamHouseUpgrade` event. |
</li>
</ul>

For events that didn't previously have an event ID, their new IDs will be added to the player's seen-events list automatically if needed when the save is loaded.

#### Other event changes
- Moved all vanilla events' on-skip behavior into their event scripts (except the pet adoption logic).

### Fish pond asset
- Fixed the translation key suffix from a custom `fish_talk_*` context tag being capitalized differently from the vanilla suffixes. (For example, `fish_talk_some_example` would incorrectly result in a `someExample` suffix instead of `SomeExample`.)

### Fish tank decorations
Decorations in fish tanks have been de-hardcoded into a new `Data/AquariumDecorations` asset.

The data asset consists of a string → model lookup, where...
- The key is the *qualified* item ID of the item which can be added to the fish tank.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| `Texture` | The asset name for the texture to draw, or `null` for the default `LooseSprites/AquariumFish` asset. (Custom decorations should use a custom texture to avoid conflicts.) |
| `SpritePosition` | The pixel position(s) in the `Texture` for the sprite to draw. If you specify multiple positions, one is randomly chosen for each sprite. |
| `SpriteCount` | *(Optional)* The number of sprites to add to the fish tank. For example, seaweed sets this to 4; so adding a seaweed to a fish tank will add four randomized seaweed decoration sprites to the tank. |

If an item has an entry in the asset, players can add one instance of the item to a fish tank to add its decoration.

Fish tanks have a limited number of decoration 'slots' available, consisting of three depth layers for each tile width. For example, a 2-tile-wide fish tank can display 6 decoration sprites at once (3 deep × 2 wide). Each sprite added based on the `SpriteCount` field fills a slot (e.g. seaweed uses 4 decoration slots). If the player adds more decorations than can be displayed, the fish tank will fill slots in the order they appear in `Data/AquariumDecorations`.

### Game state query changes
<ul>
<li>Added new game state queries:

| query | effect |
| --- | --- |
| `COMPLETED_ALL_MONSTER_SLAYER_GOALS` | Whether all monster eradication goals have been completed. |
| `IS_DEBRIS_WEATHER`; `IS_GREEN_RAINING`; `IS_LIGHTNING`; `IS_RAINING`; `IS_SNOWING` | Whether the weather in the given location sets the related weather field. For example, `IS_RAINING` is true for the `Rain`, `GreenRain`, and `Storm` weathers. |
| `IS_BOOKSELLER_DAY [day offset]` | Whether the bookseller is visiting today, with an optional [day offset] (e.g. 1 for tomorrow). |
| `IS_TRAVELING_CART_DAY [day offset]` | Whether the traveling cart is visiting today, with an optional [day offset] (e.g. 1 for tomorrow). |
| `LOCATION_MINE_DIFFICULTY <location> <min> [max]` | Whether the given location is in the mines or Skull Cavern, and its difficulty level is between <min> and [max] (default unlimited) inclusively. Difficulty 0 is the default difficulty, and modifiers like the Shrine of Challenge or some special orders each increase it by one. |
| `LOCATION_MINE_LEVEL <location> <min> [max]` | Whether the given location is in the mines or Skull Cavern, and its floor level is between <min> and [max] (default unlimited) inclusively. The Skull Cavern implicitly starts at level 121 (e.g. Skull Cavern level 10 is mine level 130). |
| `PLAYER_HAS_ALL_STARDROPS <player>` | Whether the specified player(s) have found every stardrop. |
| `PLAYER_HAS_DEEPEST_MINE_LEVEL <player> [min] [max]` | Whether the specified player(s) have reached a mine level between [min] (default 1) and [max] (default unlimited) inclusively. |
| `PLAYER_HAS_PREVIOUS_CONVERSATION_TOPIC <player> <id>+` | Whether the specified player(s) have previously had *any* of the specified conversation topics active. This doesn't include current topics (unless they're a repeat of a previous one). |
| `PLAYER_QUEST_ACTIVE <player> <id>+`; `PLAYER_QUEST_COMPLETE <player> <id>+` | Whether the specified player(s) have *any* of the specified quest IDs open (`PLAYER_QUEST_ACTIVE`) or previously completed (`PLAYER_QUEST_COMPLETE`). |
| `PLAYER_SHIPPED_ALL_BASIC_ITEMS <player>` | Whether *any one* of the specified player(s) have shipped at least one of each basic item whose shipment is tracked by the game. |
| `PLAYER_SHIPPED_MONOCULTURE <player> [min] [max]` | Whether *any one* of the specified player(s) have shipped between [min] (default 300) and [max] (default unlimited) inclusively of any crop for which `CountForMonoculture` in `Data/Crops` is true. |
| `PLAYER_SHIPPED_POLYCULTURE <player> [min]` | Whether *any one* of the specified player(s) have shipped at least [min] (default 15) of any crop for which `CountForPolyculture` in `Data/Crops` is true. |
</li>
<li>Improved some queries:

| query | changes |
| --- | --- |
| `PLAYER_HAS_ALL_ACHIEVEMENTS <player> [achievement ID]+` | Added optional [achievement ID] field to specify the list of required achievements. If omitted, defaults to all achievements in `Data/Achievements` (including the platform-only achievements which weren't in the asset before 1.6.16). |
| `PLAYER_HAS_CAUGHT_FISH <player> [fish item ID]+` | Now accepts unqualified item IDs too. |
| `PLAYER_STAT <player> <stat key> [min value] [max value]` | The [min value] argument is now optional. |
| `IS_GREEN_RAIN_DAY [day]` | The [day] argument has been added. Valid values are `today`, `tomorrow`, and a number of days offset from the current day (ex. `-1` would be yesterday). If not specified, it will default to `tomorrow`. |
</li>
</ul>

### Item spawn field changes
<ul>
<li>Added new item spawn fields:

| field | effect |
| --- | --- |
| `Color` | *(Optional)* For item types that support it (e.g. objects and clothes), a tint color to apply to the produced item. Default none. |
| `IsSpecial` | *(Optional)* Whether to mark the produced item as a special permanent item, which can't be destroyed or dropped. Default false. |
| `ObjectIsQuestItem` | *(Optional)* Whether to mark the dropped item as a quest item. This enables special rules like not being able to gift the item, and the item getting auto-recovered when it's lost in some cases. Default false. |
| `TempData` | *(Optional)* Set temporary item info that's not synchronized in multiplayer or written to the save file. This is somewhat specialized, and does nothing if there's no game logic or C# mod logic which reads it. Default none. For example, this adds a custom flag: 
```js
"TempData": {
    "SomeExampleKey": "Example Value"
}
```
 A C# code can then check for the field on the dropped items: 
```c#
if (item.TryGetTempData("SomeExampleKey", out object value))
    ...;
```
 |

</li>
<li>The `ObjectColor` field is obsolete and replaced by `Color`. Content packs which set it will still work (since the game will pass the value through to `Color` automatically).
</ul>

### Map & tile property changes
<dl>
<dt>Map properties</dd>
<dd>
<ul>
<li>Added new map properties:

| action | effect |
| --- | --- |
| `FarmHouseBed <x> <y> [item ID]`; *(valid in farm or farmhouse)* | Sets the initial bed to place when creating the farmhouse or cabin. The arguments are: * <x> <y>: the tile position at which to place it. * [item ID]: if set, the qualified or unqualified item ID for the bed furniture to place. This must match a furniture item with item type `Bed`. Defaults to `(F)2048` (single bed). If omitted, a single bed is placed at position (9, 8). |
| `@MergeInto <property> <ID> [options]` | Merge a list of space-delimited values into an existing map property (creating it if needed). The parameters in the `@MergeInto` name are: * <property>: the name of the map property to merge the values into. * <ID>: a unique string ID for the merge property. This is used to distinguish between merge properties (since each map property key must be unique), and may be used for error reporting. * [position]: where to put the new values if the property already exists (`Append` or `Prepend`). The default is `Append` for most properties, or `Prepend` for `Warp`. C# mods can set the default for custom properties via the static `GameLocation.MergeIntoPrependByDefaultProperties` field. The property value is a space-delimited list of values to merge into the target property. For example, these properties: 
```json
"Warp": "5 5 Town 10 10 5 6 Town 10 11",
"@MergeInto Warp {{ModId}}/BeachWarp Append": "20 5 Sewer 25 25",
"@MergeInto Warp {{ModId}}/TownWarp": "15 20 Beach 30 70 16 20 Beach 31 70",
```
 Would be combined into one once the map loads: 
```json
"Warp": "15 20 Beach 30 70 16 20 Beach 31 70 5 5 Town 10 10 5 6 Town 10 11 20 5 Sewer 25 25",
```
 `@MergeInto` properties are applied in the order in which they were added (e.g. based on patch priority order). |
</li>
<li>Improved map properties:

| action | changes |
| --- | --- |
| `FarmHouseFlooring`; `FarmHouseFurniture`; `FarmHouseStarterGift`; `FarmHouseStarterSeedsPosition`; `FarmHouseWallpaper` | Can now be set on the farmhouse map to override the farm map's equivalent property. |
| `FarmHouseFurniture` | Fixed property only allowing numeric item IDs. |
| `TreatAsOutdoors` | Fixed footstep sounds not always respecting `TreatAsOutdoors` for indoor locations. |
</li>
</ul>
</dd>

<dt>Tile properties</dt>
<dd>
- Removed `Treasure` tile property (unused).
</dd>
</dl>

### Quest changes
- The game now tracks completed quest IDs. You can check them using new game state queries or in C# via `Game1.player.questsCompleted`. The list is retroactively populated for pre-existing saves.
- Added `PLAYER_QUEST_ACTIVE` and `PLAYER_QUEST_COMPLETE` game state queries.
- Added `completeQuest` event command and `CompleteQuest` trigger action.
- Added `QuestIds` constants for C# mods.
- Added item spawn field to produce quest items.
- Fixed intermittent crash when an item delivery quest for a non-Wizard NPC requests a cooking-type item.

### Schedule changes
<ul>
<li>NPCs are longer affected by warps they touch while following their schedule if those warps aren't the ones they intend to use.</li>
<li>Added new schedule keys:

| syntax | summary |
| --- | --- |
| `roommate_<festival ID>_<festival day>`; `roommate_<festival ID>`; `roommate_<season>_<day of month>`; `roommate_<day of week>`; `roommateJob` | Equivalent to the marriage schedules, but only apply if the NPC is a roommate. If a roommate NPC has both `roommate*` and `marriage*` schedules, it'll use a `roommate*` schedule if any of them apply. If none apply, it'll default to `marriage*` schedules. You can avoid that by setting the fallback `roommate` schedule key. |
| `marriage`; `roommate` | A fallback schedule to use if a more specific schedule doesn't apply. If this is omitted and no other schedule applies, the NPC won't have a schedule that day. |
</li>
</ul>

### Stat changes
<ul>
<li>Added new stats:

| stat key | tracks |
| --- | --- |
| Player activity |  |
| `CookingRecipesUsed` | The number of cooking recipes which the player used at least once. |
| `CookingRecipesNotUsed` | The number of cooking recipes which the player never used. |
| `CraftingRecipesUsed` | The number of crafting recipes which the player used at least once. |
| `CraftingRecipesNotUsed` | The number of crafting recipes which the player never used. |
| `FishTypesCaught` | The unique fish species caught by the player while fishing. |
| `FishTypesNotCaught` | The unique fish species that were never caught by the player while fishing. |
| `FriendsWith5Hearts` | The number of NPCs with which the player has 5+ hearts of friendship. |
| `FriendsWith10Hearts` | The number of NPCs with which the player has 10+ hearts of friendship. |
| World activity |  |
| `ArtifactsDonated` | The number of artifacts donated to the museum by any player. |
| `ArtifactsNotDonated` | The number of artifacts missing from the museum. |
</li>
<li>Changed existing stats:

| stat key | changes |
| --- | --- |
| `AverageBedTime` | Fixed incorrect time averaging (e.g. the average of 600 + 700 was incorrectly calculated as 650 instead of 630). |
| `CaveCarrotsFound` | Removed. This was never fully implemented. |
| `OtherPreciousGemsFound` | Fixed stat not updated in later versions. |
| `WeedsEliminated` | Fixed stat only incremented when destroying weeds indirectly (e.g. with a bomb), not when cutting them with a tool. |
</li>
</ul>

### Trigger action changes
<ul>
<li>Added new trigger actions:

| action | effect |
| --- | --- |
| `CompleteQuest <quest ID>` | Complete all active quests with this ID in the player's journal. |
| `ShowPrivateChatMessage <message> [color]` | Add a message to the chat box which is only visible to the current player. The <message> is the tokenizable string to show, and [color] is the text color. |
| `AddItemFromQuery <query>` | Adds a random item from the specified item query. |

</ul>

### Tokenizable string changes
<ul>
<li>Improved an existing token:

| token format | output |
| --- | --- |
| `[SuggestedItem [interval] [sync key] [options]]` | **(For shops only.)** <ul> <li>The token is now case-insensitive.</li> <li>Added optional arguments, which can be specified in any order: |
| argument | effect |
| `@edible <include>` | Whether food/drink items can be suggested (i.e. objects with an edibility > 0). This can be `true` (only suggest edible items), `false` (only suggest non-edible items), or omitted (suggest any). |
| `@recipe <include>` | Whether recipes can be suggested (i.e. shop item entries with `IsRecipe: true`). This can be `true` (only suggest recipes), `false` (only suggest non-recipes), or omitted (suggest any). |
| `@ignoreFiltersIfNeeded` | If set, the `@edible` and `@recipe` arguments will be ignored if they'd result in no item being suggested. |
</li>
</ul>
</li>
|}
</ul>

### Other translation changes
<ul>
<li>You can now override the relationship status labels (shown in the social and profile menus) for specific NPCs:

| asset | original key | overridden by |
| --- | --- | --- |
| `Strings/StringsFromCSFiles` | `SocialPage_Relationship_Housemate_Female`; `SocialPage_Relationship_Housemate_Male` | `SocialPage_Relationship_Housemate_<NPC name>` |
| `SocialPage_Relationship_Husband`; `SocialPage_Relationship_Wife` | `SocialPage_Relationship_Spouse_<NPC name>` |  |
| `SocialPage_Relationship_Boyfriend`; `SocialPage_Relationship_Girlfriend` | `SocialPage_Relationship_<NPC name>` |  |
| `SocialPage_Relationship_ExHusband`; `SocialPage_Relationship_ExWife` | `SocialPage_Relationship_Ex_<NPC name>` |  |
| `SocialPage_Relationship_Single_Female`; `SocialPage_Relationship_Single_Male` | `SocialPage_Relationship_Single_<NPC name>` |  |
| `Strings/UI` | `SocialPage_Relationship_MarriedToOtherPlayer_FemaleNpc`; `SocialPage_Relationship_MarriedToOtherPlayer_MaleNpc` | `SocialPage_Relationship_MarriedToOtherPlayer_<NPC name>` |
</li>
</ul>

### Other changes
- Playing an empty audio ID (`null` or `""`) now does nothing instead of crashing. For required audio cue fields, you can also use the new ID `silent` to do nothing.
- Custom achievements now benefit from retroactive unlock too if they implement the new `UnlockCondition` field in `Data/Achievements`.
- In shop data:
  - Shop dialogues with `[SuggestedItem]` are now skipped automatically if there's no matching item to suggest.
  - Fixed `ActionsOnPurchase` not applied if you were already holding an item.
- For NPCs:
  - Added validation for schedule `offset` command in end-of-route animations.
  - Fixed NPCs treating a non-existent spritesheet asset as successfully loaded, so they would show an error texture. They now log a warning and continue to the next texture.
  - Fixed NPC `FlowerDanceCanDance` field ignored for non-datable NPCs with no dialogue.
  - Fixed `RejectItem_*` dialogue disabling the NPC's normal dialogue after it's shown.
- De-hardcoded the giant Qi fruit mystery box drop and Easter egg into `Data/GiantCrops`, and replaced the `QiChallenge2` special order check with a `DROP_QI_BEANS` special order rule check.
- The value of a `(O)GoldCoin` item (e.g. dropped by a pot of gold) is now fully determined by its stack size, which is no longer limited to 999. This lets you give arbitrary gold rewards via item debris.
- The game now accepts a `--saves-path some/saves/path/override` command line argument, which will change where save files are checked for.
- Fixed crash when loading a save with missing building interiors.
- Fixed farm animals in non-instanced building interiors (like the farmhouse) getting deleted on day start.
- Fixed fruit trees' falling leaves not applying the tree's cosmetic season.
- Fixed issue where a building placed inside a decoratable location would cause the flooring under it to not change when you put down a different flooring.

## Changes for C# mod authors
### Instance statics no longer affect mod assemblies
In multiplayer, split-screen mode works by swapping the values of static fields for each screen (called *instanced statics*). In previous versions, Stardew Valley would scan all loaded assemblies for static fields marked with `[InstancedStatic]` attributes except a few ignored assemblies like MonoGame.

In Stardew Valley 1.6.16, the game now only does this for the main game assembly to improve performance. If a mod was using `[InstancedStatic]`, it should use SMAPI's built-in per-screen data utility instead.

### Content manager changes
The base game's 'content manager' API changed in Stardew Valley 1.6.16. This mainly affects `LanguageCode`, `Game1.content`, and other content manager fields. This does *not* affect mods which only use SMAPI's content API.

In particular:
- The game now uses `IContentManager` instead of `LocalizedContentManager` in most places. That includes fields like `Game1.content`, methods like `Game1.content.CreateTemporary()`, etc. The new interface also includes full IntelliSense docs.
- Moved the content manager types into a new `StardewValley.ContentManagement` namespace, and removed type nesting.
- Made all methods non-static.
- Adjusted naming for clarity.
- Removed unused code.

See *other breaking API changes* for specific changes.

### Inventory add-item overhaul
1.6.16 overhauls how items are added to chest & player inventories, which fixes a wide variety of bugs when an item can't be fully collected due to a full or nearly-full inventory. The core change is a set of new methods which allow explicitly handling partial success when adding an item.

For example, the new methods let you avoid duplicate rewards when partly collecting the item stack:

```c#
if (Game1.player.TryAddToInventory(this.heldItem).FullyAdded)
{
    this.heldItem = null;
    Game1.player.gainExperience(Farmer.farmingSkill, 20); // grant XP once when the stack is fully collected
}
```

Or in cases where a partial item stack can't be left behind (like animal harvesting), you can handle any remainder:

```c#
AddToInventoryResult result = Game1.player.TryAddToInventory(producedItem);
if (!result.FullyAdded)
   Game1.player.DropItemAtFeet(result.Remainder);
```

Which you can also simplify using the convenience `CollectOr*` methods:

```c#
Game1.player.CollectOrDrop(producedItem);
```

This removes the `Farmer.addItemToInventoryBool` method, but otherwise mods should be unaffected. However, mods should consider using the newer methods to handle partial success where needed.

### Current-location tracking more consistent
The game tracks the current location using two properties: `Game1.currentLocation` and `Game1.player.currentLocation`. These were often used interchangeably (even in base-game code), but they're not always equivalent.

Stardew Valley 1.6.16 formalizes the distinction and applies it more consistently:
- `Game1.currentLocation` is the location being viewed by the local player (i.e. the "camera location"), which doesn't necessarily contain the player.
- `Game1.player.currentLocation` is the location which contains the local player.

For example, let's say the player is at Robin's shop placing a building on the farm; in that case `Game1.currentLocation.Name` is the location being viewed (i.e. `Farm`) while `Game1.player.currentLocation.Name` is the location which contains the player (i.e. `ScienceHouse`).

This changes the behavior in some cases which affect mods. For example, trigger actions with the `LocationChanged` trigger now see the new location as expected, and are no longer triggered when the camera shows a different location without moving the player.

### New system for spawning synced TAS in a more network-bandwidth-friendly way
Spawning many `TemporaryAnimatedSprite`s at once can strain network connections due to all the fields on a TAS. There is now a new system to allow spawning predefined sets of temporary animated sprites in a much more bandwidth friendly.

In a similar manner to custom tile actions, you can call the static `GameLocation.RegisterBulkSpriteType` function, with an ID and an `Action<GameLocation, SpawnBulkSpritesNetEventArgs>`:

| field | use | maximum element count |
| --- | --- | --- |
| `Id` | The ID of the bulk sprite callback that was invoked. | n/a |
| `FloatArgs` | *(Optional)* An array of floats that can be used in the resulting callback. | 31 |
| `StringArgs` | *(Optional)* An array of strings that can be used in the resulting callback. | 7 |

To trigger one of these events for everyone, call `.Fire()` on the net event field. Any callbacks will only be triggered if the currently visible location is the one they happen on. Otherwise, they are skipped.

```c#
GameLocation.RegisterBulkSpriteType("TellPeopleAnImportantMessage", (location, args) =>
{
    if ( args.FloatArgs.Length != 3 || args.StringArgs.Length != 1 )
        return;
    Vector2 pos = new Vector2( args.FloatArgs[0], args.FloatArsg[1] );
    int times = (int) args.FloatArgs[2];
    string veryImportantMessage = args.StringArgs[0];
    
    // Typically you would just use `location.playSound` once on the original player's game for this, but for demonstration purposes we are using `location.localSound` instead
    location.localSound( "cat", pos );
    
    for ( int i = 0; i < times; ++i )
    {
        Game1.log.Info( veryImportantMessage );
    }
});
```

```c#
GameLocation location = Game1.currentLocation;

location.spawnBulkSprites.Fire(new GameLocation.SpawnBulkSpritesNetEventArgs(
    "TellPeopleAnImportantMessage",
    floats: new float[] { pos.X, pos.Y, 3 },
    strings: new string[] { "cats are amazing" }
));
```

### Data model attributes
Stardew Valley 1.6.16 adds attributes to its content data models which describe what each field contains.

For example:

```c#
[DataField(FieldType.AudioCue)]
public string BarkSound;
```

The available attributes are:

| attribute | usage |
| --- | --- |
| `[DataField]` | The attribute applied to most fields, which describes the data type. Its `FieldType` property describes the data type, which can be any of... |
| field type | meaning |
| `AssetName` | A content asset name (without file extension). |
| `AudioCue` | An Audio\|audio cue ID. |
| `Chance` | A percentage probability expressed as a number between 0 (never) and 1 (always). |
| `Color` | A color in string format. |
| `ContextTag` | A context tag or list thereof. |
| `ContextTagQuery` | A context tag query or list thereof. |
| `CustomFields` | A dictionary of custom fields intended for mod use, which aren't synchronized or persisted. |
| `Direction` | A direction. This can be a standard name (up, left, right, or down) or a matching numeric constant. |
| `GameStateQuery` | A game state query which indicates dynamic conditions. |
| `ModData` | A dictionary of custom data about an instance, which are persisted in the created entities and synchronized in multiplayer. This should typically be omitted by the game. |
| `TokenizableString` | A tokenizable string. |
| `TriggerAction` | A trigger action or list thereof. |

Its optional `PlaceholderValues` property indicates special values that are replaced by the game (e.g. `MOD_FARM` in a world map texture field).
|-
| `[AssetNameField]`
| An extension of `[DataField]` applied to asset name fields, which adds two properties:

| property | usage |
| --- | --- |
| `AssetType` | The type of asset referenced by the field (one of `Data`, `BitMapFont`, `Map`, or `Texture`). |
| `PathFormat` | *(Optional)* The asset name format (where `{0}` is the field value), if the field value isn't used as-is. For example, building indoor map fields set this to `Maps/{0}` (since the `Maps/` prefix is added automatically). |

|-
| `[StrictlyOrderedWithoutId]`
| A specialized attribute applied to list fields which contain an ordered list of non-unique models. For example, this is used for pet animation frames. This is very rarely used.
|}

### Item changes
- Added `IItemDataDefinition.GetContextTags(itemData)` method, so custom item data definitions can add context tags from their data if applicable.
- Fixed `item.CopyFieldsFrom(other)` always setting the stack size for the copy to 1.
- Fixed `object.IsBreakableStone()⁩` returning false for calico egg stones in the skull cavern.

### New utility types
#### AssetNameUtility
The new `AssetNameUtility` class has methods for working with asset names:

| method | usage |
| --- | --- |
| `Normalize` | A low-level method which normalizes path separators in an asset name. For example: 
```c#
string assetName = AssetNameUtility.Normalize("Characters/\\/Abigail"); // produces "Characters/Abigail"
```
 Consider using SMAPI's `PathUtilities.NormalizeAssetName` instead, which is more thorough. |
| `TryParseTranslationKey` | Parses a translation key into its component parts. For example: 
```c#
if (AssetNameUtility.TryParseTranslationKey("Strings/UI:Confirm", out string assetName, out string fieldKey, out string error))
   ...;  // assetName: Strings/UI, fieldKey: Confirm, error: null
```
 This doesn't check whether the asset or key exists; see `Game1.content.IsValidTranslationKey` to do that. |

#### EventValidator
- **Overview**: The new `EventValidator` class analyses event scripts to detect issues like...
  * command names which don't match a registered command or alias;
  * command arguments which don't match those expected by the command;
  * and custom rule violations based on the `EventValidator.CommandValidators` field (used for commands like `goto`).

  This is mainly meant to implement unit tests for events. For example, to validate all events in a data asset:
  
```c#
string assetName = "Data/Events/Farm";
var events = Game1.content.Load<Dictionary<string, string>>(assetName);

var validator = new EventValidator();
foreach ((string key, string script) in events)
{
    foreach (var issue in validator.Validator(assetName, key, script))
        Assert.Fail($"Validation failed for event '{key}' > command '{issue.CommandText}': {issue.SuggestedError}.");
}
```

- **Command argument validation**: Argument validation is enabled if the command's handler method has an `EventArguments` attribute, which defines the argument pattern using a special syntax. Each command can list multiple patterns, and it'll be considered valid if any of them matches. If the pattern is an empty string, the validator will ensure no arguments are provided.

  For example, the default `showFrame` command is defined like this:
  
```c#
[EventArguments("int", "actor int bool?")]
public static void ShowFrame(Event @event, string[] args, EventContext context)
```

  An argument pattern consists of space-delimited *tokens*, where each token defines the format for an argument. For example, `actor int bool?` has three tokens representing three arguments: an actor name, a positive integer, and an optional boolean.

  These are the tokens recognized by the validator:
  | token | meaning |
| --- | --- |
| generic argument types |  |
| `bool` | A boolean value (`true` or `false`). |
| `float` | A positive number with optional decimal point (like `5` or `0.5`). |
| `-float` | A number with optional decimal point and optional negative sign (like `5`, `-5`, or `-0.5`). |
| `int` | A positive integer (like `5`). |
| `-int` | An integer with optional negative sign (like `5` or `-5`). |
| `string` | A quoted sequence of arbitrary text like `"some words here"`. If the argument has no quotes, this is equivalent to the `word` token. |
| `word` | A simple sequence of letters, numbers, periods, and/or underscores (usually an ID or similar value). While the command will accept quotes, they're redundant and will be rejected by the validator. |
| `remainder` | One or more arguments of any type. |
| specific string formats |  |
| `actor` | An actor name in the event format (like `Abigail` or `Kent?`). |
| `assetName` | An asset name like `Data/Achievements`. |
| `direction` | A direction which can be specified as either a number (like `0`) or name (like `up`). |
| `goToLabel` | An event label (see the `goto` and `label` event commands). This is equivalent to `word`, but is used by `EventValidator` to validate label references. |
| `itemId` | An item ID which can be qualified or unqualified (like `(O)MossySoup` or `MossySoup`). |
| `translationKey` | A translation key like `Strings/Locations:SomeKey`. |
| custom formats |  |
| `'...'` | The literal text or regex pattern to match, like `'continue'` or `'[\w-]+'`. |

  In addition, you can extend tokens with these indicators:
  * Suffix with `+` for repeating argument of that type. For example, `int+` would allow both `5` and `5 6 7`.
  * Suffix with `?` for an optional argument which can be omitted. All tokens after this token must also be optional. (You can have optional repeating arguments like `int+?`.)
  * Infix with `|` for a parameter which can be one of multiple types. For example `int|bool` will allow either integer or boolean values.

#### SourceCodeContentManager
The new `SourceCodeContentManager` isn't relevant for mod authors except as an example. It implements `IContentManager` to support reading assets from the original content assets (e.g. in `.tide` and `.xml` format) instead of the packed XNB files.

#### TickCache
The new `TickCache` class loads a value on-demand and caches it for a given number of game ticks.

It only has two public methods:

| member | usage |
| --- | --- |
| `Value` | Get the cached value, reloading it if needed. |
| `Clear()` | Reset the cache time, so it'll be reloaded next time `Value` is requested. |

For example, let's say you do this:

```c#
private readonly TickCache<int> Cached = new TickCache<int>(GetValue, cacheTicks: 3);

/// <inheritdoc />
public override void Entry(IModHelper helper)
{
    helper.Events.GameLoop.UpdateTicked += (sender, e) => _ = this.Cached.Value;
}

private int GetValue()
{
   this.Monitor.Log($"Fetching value at game tick {Game1.ticks}.");
   return 0;
}
```

When you run the mod, you would see something like...
Fetching value at game tick 100...
Fetching value at game tick 103...
Fetching value at game tick 106...

The game uses this to cache a few frequently-accessed methods like `npc.GetData()`.

#### DataCache
The new `DataCache<T>` class loads a value on-demand and caches it until a counter is incremented.

This is primarily intended for caching values from data assets until that asset is reloaded, and there are static methods on `DataCache` for doing that with several existing assets.

`DataCache<T>` instances only have two public methods:

| member | usage |
| --- | --- |
| `Value` | Get the cached value, reloading it if needed. |
| `Clear()` | Reset the cache time, so it'll be reloaded next time `Value` is requested. |

`DataCache` has several static helper methods for invalidating based on certain vanilla assets. These take a `Func<T>`, which will be called to get the appropriate value whenever the cache needs a new value. The current helper methods are:

| member | usage |
| --- | --- |
| `FarmAnimalData` | Cache is reset when `Data/FarmAnimals` is reloaded. |
| `CharacterData` | Cache is reset when `Data/Characters` is reloaded. |
| `FenceData` | Cache is reset when `Data/Fences` is reloaded. |
| `MachineData` | Cache is reset when `Data/Machines` is reloaded. |
| `ItemRegistry` | Unlike the others, these do not reset when a specific asset is reloaded. Instead, they reset when whenever the `ItemRegistry` cache is rebuilt (which SMAPI triggers when any item data asset is reloaded). |

For example, let's say you do this:

```c#
private readonly DataCache<int> Cached = DataCache.CharacterData(GetValue);

/// <inheritdoc />
public override void Entry(IModHelper helper)
{
    helper.Events.Input.TimeChanged += (sender, e) => if (e.NewTime % 100 == 0) helper.GameContent.InvalidateCache("Data/Characters");
    helper.Events.GameLoop.UpdateTicked += (sender, e) => _ = this.Cached.Value;
}

private int GetValue()
{
   this.Monitor.Log($"Fetching some character data at {Game1.timeOfDay}");
   return 0;
}
```

When you run the mod, you would see something like this (assuming no other mods are invalidating `Data/Characters`)...
Fetching some character data at 600...
Fetching some character data at 700...
Fetching some character data at 800...

The game uses this to cache a few frequently-accessed methods like `npc.GetData()`.

### Utility fields & methods
<ul>
<li>1.6.16 adds some new utility methods and fields. For example:

| type | member | usage |
| --- | --- | --- |
| `Character` | `DropItemAtFeet(…)` | Place a floating item next to the character (e.g. monster, NPC, player, etc). This includes options to set the direction, max stack size, and whether to play the 'item dropped' sound. For example, this places a pufferfish in front of the player: 
```c#
Item pufferfish = ItemRegistry.Create("(O)128");
Game1.player.DropItemAtFeet(pufferfish);
```
 This causes 100 pufferfish to 'explode' into individual stacks in random directions around a monster and play the 'item dropped' sound (once): 
```c#
Item pufferfish = ItemRegistry.Create("(O)128", 100);
monster.DropItemAtFeet(pufferfish, stackSize: 1, playSound: true);
```
 |
| `speechBubble` | A unified field which handles showing a speech bubble above the player's head. This can be set via `showTextAboveHead(…)`, `clearTextAboveHead()`, or the `textAboveHead` event command. |  |
| `Chest` | `TryAddItem(…)` | Merge an item stack into the chest if possible, and return a result which describes how much of the item as added. See the equivalent `Farmer.TryAddToInventory(…)` below for examples. |
| `FarmAnimal` | `CanPlayersInteract()` | Get whether players can interact with the animal (e.g. pet or milk them). By default, ephemeral animals are non-interactable. Mods can patch this method to change the behavior for specific animals. |
| `Farmer` | `TryAddToInventory(…)` | Merge an item stack into the player's inventory if possible, and return a result which describes how much of the item as added. The result fields are: |
| field | usage |  |
| `TotalAdded` | The stack count which was added to the inventory. |  |
| `TotalRemaining` | The stack count which couldn't be added to the inventory. |  |
| `Remainder` | A stack for the items which couldn't be added to the inventory, or `null` if it was fully added. This is always a reference to the original input item if there's any remainder. |  |
| `AnyAdded` | Whether any items were added to the inventory. |  |
| `FullyAdded` | Whether the item was fully added to the inventory. |  |

This allows handling partial failures in a more intuitive way. For example:

```c#
AddToInventoryResult result = Game1.player.TryAddToInventory(this.heldItem);
this.heldItem = result.Remainder;

if (result.FullyAdded)
    Game1.player.gainExperience(Farmer.farmingSkill, 20); // grant XP once when the stack is fully collected
```

|-
| `CollectOrDrop(…)`
| Merge an item stack into the player's inventory, and drop any remainder that doesn't fit at the player's feet. If the player isn't in a stable location (e.g. they're warping or watching an event), the item will be dropped when they resume control.

This replaces the former `Utility.CollectOrDrop(…)`, with added support for use in non-stable locations.
|-
| `CollectOrLostAndFound(Item item)`
| Merge an item stack into the player's inventory, and add any remainder to the lost & found.
|-
| `FarmHouse`
| `GetSpouseRoomRelativeTile(…)`
| Get a tile coordinate relative to the spouse room bounds. For example, the frog terrarium in Sebastian's spouse room is at `farmHouse.GetSpouseRoomRelativeTile(1, 6)`.
|-
| `Forest`
| `ShouldTravelingMerchantVisit(…)`
| Get whether the traveling cart should visit on the given date.
|-
| `FruitTree`
| `DropAllFruit()`
| Drops all fruit onto the ground (as if the player shook the tree).
|-
| `Object`
| `IsDigSpot()`
| Get whether the object is an artifact spot or seed spot which can be dug up with a hoe (or a custom modded item with similar behavior).
|-
|rowspan="3"| `PassiveFestivalData`
| `CoversDate(…)`
| Get whether the festival is active on the given date.
|-
| `GetFestivalLocations()`
| Get the locations in which the passive festival happens (based on the `InLocations` or `MapReplacements` fields in `Data/PassiveFestivals`).
|-
| `TryGetLocationReplacement(…)`
| Get the new location to load instead of a normal one when the passive festival is active. For example, the Night Market replaces `Beach` with `BeachNightMarket`.
|-
|rowspan="2"| `TextBox`
| `FilterText`
| A callback raised when the text changes, but before it's saved. It receives the textbox and new text, and returns the filtered text to save.
|-
| `OnTextChanged`
| An event raised when the text in the textbox changes (after `FilterText` is applied). It receives the textbox, the previous text, and the new text.
|-
| `Town`
| `ShouldBooksellerVisit(…)`
| Get whether the bookseller should visit on the given date.
|-
| `TriggerActionManager`
| `TryRunActions(…)`
| Run a list of actions with the game's default error logging.
|-
| `Utility`
| `TryGetPassiveFestivalDataForDayHere(…)`
| Get the passive festival which occurs on a given date within the given location, if any. This is equivalent to `Utility.TryGetPassiveFestivalDataForDay(…)`, but also checks the given location against the passive festival's `InLocation` or `MapReplacements` field.
|}</li>
<li>And improves some existing methods:

| type | member | usage |
| --- | --- | --- |
| `Item` | `getOne(…)` |   * Now copies held and inner items too. * Added an optional `copyStackSize` argument (default false). |

</ul>

### Nullable reference types
Stardew Valley 1.6.16 adds [nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references) to many of its generic utilities (like `ArgUtility`), core systems (like `ItemRegistry` and `GameStateQuery`), extensions, and various methods.

For example, if you do this in modern mod code:

```c#
ArgUtility.TryGet(args, 0, out string? argValue, out string? error);

if (argValue.Length > 0)
   ...;
```

You'll now see a build warning like "*CS8602: Dereference of a possibly null reference*" because `argValue` may be null there.

Changing the code to this would fix the warning:

```c#
if (ArgUtility.TryGet(args, 0, out string? argValue, out string? error))
{
    if (argValue.Length > 0)
       ...;
}
```

### Other breaking API changes
*(This section is meant as a quick reference if you have build errors; there's no need to read through it otherwise.)*

#### Removed types
These types were removed in 1.6.16:

| type | migration |
| --- | --- |
| `Events.DiaryEvent` | Removed (unused). |
| `Lantern` | Removed (broken tool). |
| `ObjToStr` | Removed (unused in release builds). |
| `Raft` | Removed (broken tool). |

#### Readonly structs
These structs became [readonly structs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct) in 1.6.16, which makes them much more efficient (e.g. they're no longer defensively copied on every access).

The effect on mods depends on the struct:
- For structs whose fields were already `readonly`, this has no effect on mod code (besides making it more efficient).
- For structs whose fields became readonly in 1.6.16, mod code which sets fields directly will need to use the constructor instead. These are mentioned in the *breaking API changes* column below.

| type | breaking API changes |
| --- | --- |
| `AnimationFrame` |   * Fields changed to `readonly`. * Removed `AddFrameAction(…)` and `AddFrameEndAction(…)` methods. |
| `BundleIngredientDescription` | Some fields (`preservesId` and `completed`) changed to `readonly`. |
| `ButtonCollection` | *none* |
| `FarmerPair` | Fields changed to `readonly`. |
| `Flooring.Neighbor`; `Flooring.NeighborLoc` | *none* |
| `HoeDirt.Neighbor`; `HoeDirt.NeighborLoc` | *none* |
| `InteriorDoorDictionary.DoorCollection` | *none* |
| `NetDictionary.KeysCollection`; `NetDictionary.PairsCollection`; `NetDictionary.ValuesCollection` | *none* |
| `OutgoingMessage` | Fields changed to `readonly`. |
| `SerializableDictionary.ChangeArgs` | *none* |
| `StaticDelegateBuilder.CachedDelegate` | *none* |
| `WaterTiles.WaterTileData` | Fields changed to `readonly`. |

#### Renamed or moved types
These types were renamed or moved in 1.6.16:

| type | migration |
| --- | --- |
| `LocalizedContentManager` | Moved from the `StardewValley` namespace into `StardewValley.ContentManagement`. See *content manager changes*. |
| `LocalizedContentManager.LanguageCode`; `LocalizedContentManager.LanguageChangedHandler` |   * Moved from the `StardewValley` namespace into `StardewValley.ContentManagement`. * Moved out of the nested type (e.g. change `LocalizedContentManager.LanguageCode` to just `LanguageCode` in your mod code). See *content manager changes*. |

#### Other API changes
These class members changed in 1.6.16:

| type | member | migration |
| --- | --- | --- |
| `AnimationFrame` | `AddFrameAction(…)`; `AddFrameEndAction(…)` | Removed per readonly structs. To add multiple frame actions, pass them into the constructor instead. For example, if you previously had code like this: 
```c#
var frame = new FarmerSprite.AnimationFrame(…);
frame.AddFrameEndAction(this.PerformAnimationSound);
frame.AddFrameEndAction(this.OnAnimationEnded);
```
 You can pass the combined actions into the constructor instead: 
```c#
var frame = new FarmerSprite.AnimationFrame(…, this.PerformAnimationSound + this.OnAnimationEnded);
```
 |
| `ArgUtility` | `TryGetOptionalRemainder(…)` | Removed; use `ArgUtility.GetRemainder(…)` instead. The methods are equivalent, since `TryGetOptionalRemainder` was guaranteed to return true. |
| `Boots` | `price` | Removed. This was never used, since boots prices are calculated dynamically. |
| `Buff` | `goblinsCurse`; `slimed`; `evilEye`; `tipsy`; `fear`; `frozen`; `warriorEnergy`; `yobaBlessing`; `adrenalineRush`; `avoidMonsters`; `full`; `quenched`; `spawnMonsters`; `nauseous`; `darkness`; `weakness`; `squidInkRavioli` | Replaced by `BuffIds` constants: * `Buff.avoidMonsters` → `BuffIds.OilOfGarlic`; * `Buff.evilEye` → `BuffIds.Jinxed`; * `Buff.goblinsCurse` → `BuffIds.Burnt`; * `Buff.spawnMonsters` → `BuffIds.MonsterMusk`; * `Buff.nauseous` → `BuffIds.Nauseated`; * and the others have equivalent names (e.g. `Buff.slimed` → `BuffIds.Slimed`). |
| `fullnessLength`; `quenchedLength` | Removed. Their lengths are set in `Data/Buffs` instead. |  |
| `BuffManager` | `Apply(…)` | Split into `Apply(string id)` + `ApplyLocal(Buff buff)`, to clarify that applying a custom buff model (instead of a buff ID) won't synchronize if it's not the local player. |
| `Bush` | `draw(SpriteBatch batch, int yDrawOffset)` | Removed. Set the `drawOffset` field and then call `draw(SpriteBatch)` instead. |
| `yDrawOffset` | Replaced by `drawOffset`, which is now public and a `Point`. |  |
| `FarmAnimal` | `buildingTypeILiveIn` | Removed. Use the `AnimalData.Value?.House` field instead. |
| `Farmer` | `addItemToInventoryBool(…)` | Removed. The method's return value was often misunderstood, which led to common bugs in both vanilla and mod code. You can convert code directly to the new `TryAddToInventory` method added in 1.6.16. For example: 
```diff
- if (Game1.player.addItemToInventoryBool(this.heldItem))
+ if (Game1.player.TryAddToInventory(this.heldItem).AnyAdded)
  {
      this.heldItem = null;
      Game1.player.gainExperience(Farmer.farmingSkill, 20);
  }
```
 However, note that `addItemToInventoryBool` returned whether *any* of the item stack was added to the inventory (even if it wasn't fully added). Consider whether updated code needs to handle partial success. For example: 
```c#
var result = Game1.player.TryAddToInventory(item);
this.heldItem = result.Remainder;

if (result.FullyAdded)
    Game1.player.gainExperience(Farmer.farmingSkill, 20); // grant XP once when the stack is fully collected
```
 |
| `applyBuff(Buff)` | Renamed to `applyLocalBuff`, to clarify that applying a custom buff model (instead of a buff ID) won't synchronize if it's not the local player. |  |
| `dropItem(…)` | Replaced by `DropItemAtFeet(…)`. |  |
| `nextEventcommandAfterEmote` | Removed; see *removed manual event pause management*. |  |
| `Forest` | `ShouldTravelingMerchantVisitToday()` | Replaced by the static `ShouldTravelingMerchantVisit` method: 
```c#
bool shouldVisitToday = Forest.ShouldTravelingMerchantVisit(Game1.Date);
```
 |
| `FishingRod` | `lastCatchWasJunk` | Renamed to `lastCatchSkippedMinigame` to more accurately convey what it does. |
| `Furniture` | `rotations` | Removed. You can read it from the data instead: 
```c#
string[] data = furniture.getData();
int rawRotations = ArgUtility.GetInt(data, 4);
```
 |
| `Game1` | `createMultipleItemDebris` | Added an `int stackSize = 1` argument, which sets the desired size of each dropped item stack. For example, dropping a stack of 102 with a stack size of 25 will produce 4 debris × 25 and 1 debris × 2. |
| `IKeyboardSubscriber` | `RecieveCommandInput(char command)`; `RecieveCommandInput(char command)` | Added new parameter (`KeyboardModifier modifiers`). |
| `string ClipboardCopy()`; `string ClipboardCut()`; `void SelectAll()` | Added. |  |
| `ItemRegistry` | `Create(…)` | Split into two methods: `Create` (returns an Error Item if the item doesn't exist) and `CreateOrNull` (returns null if an item doesn't exist). To migrate existing code: * If you set `allowNull: false` or omitted it, use `Create`. * If you set `allowNull: true`, use `CreateOrNull`. |
| `LocalizedContentManager` | `CurrentLanguageCode`; `CurrentLanguageLatin`; `CurrentLanguageString`; `CurrentModLanguage`; `GetDefaultLanguageCode()`; `OnLanguageChange`; `SetModLanguage(…)` | These are no longer static, so they need to be called through an instance like `Game1.content`. For example, change `LocalizedContentManager.GetDefaultLanguageCode()` in mod code to `Game1.content.GetDefaultLanguageCode()`. Some of these are also renamed (see the next rows). |
| `CurrentCulture` | Removed (it was unused). |  |
| `CurrentLanguageCode` | Renamed to `LanguageCode`. |  |
| `CurrentLanguageLatin` | Renamed to `LanguageUsesLatinFont`. |  |
| `CurrentLanguageString` | Renamed to `LanguageString`. |  |
| `CurrentModLanguage` | Renamed to `LanguageModData`. |  |
| `GetCurrentLanguage()` | Removed. Use the `LanguageCode` property instead. |  |
| `OnLanguageChange` | Renamed to `OnGlobalLanguageChanged`. |  |
| `NPC` | `birthday_Day`; `birthday_Season`; `manners`; `optimism`; `socialAnxiety` | Removed. See the equivalent properties (e.g. `Manners`) instead. |
| `Birthday_Season` |   * Changed from string to the `Season` enum. * Removed setter (now reads the value from the cached NPC data). |  |
| `Birthday_Day`; `Manners`; `Optimism`; `SocialAnxiety` | Removed setter (now reads the value from the cached NPC data). |  |
| `isDatable` | Replaced by the `CanBeRomanced` property, which gets the value from the cached NPC data. |  |
| `loveInterest` | Removed. Use the equivalent data field instead: 
```c#
string? loveInterest = npc.GetData()?.LoveInterest;
```
 |  |
| `textAboveHead`; `textAboveHeadAlpha`; `textAboveHeadColor`; `textAboveHeadPreTimer`; `textAboveHeadStyle`; `textAboveHeadTimer` | Replaced by `npc.speechBubble`. To add/remove a speech bubble, see `npc.showTextAboveHead(…)` and `npc.clearTextAboveHead()`. |  |
| `textStyle_None`; `textStyle_Shake` | Replaced by `SpeechBubble.textStyle_None` and `SpeechBubble.textStyle_Shake`. |  |
| `NPCDialogueResponse`; `Response` | `id`; `friendshipChange`; `extraArgument`; `hotkey`; `responseText` | Fields changed to get-only properties. |
| `responseKey` |   * Field changed to get-only property. * Previously `NPCDialogueResponse` stored the NPC's reaction dialogue key in this field. You should now use `npcReactionKey` for that instead. |  |
| `SetHotKey(…)` | Removed. Set the hotkey through the constructor instead. |  |
| `PetData` | `GetBreedById(…)` | Removed. To migrate existing code: * If you set `allowNull: false` or omitted it, use the new `GetPreferredBreed` method. * If you set `allowNull: true`, use `petData.Breeds.FirstOrDefault(p => p.Id == breedId)`. |
| `StartupPreferences` | `levelTenCombat`; `levelTenFishing`; `levelTenForaging`; `levelTenMining` | Removed. These were unused. |
| `TrashBear` | `itemBeingEaten`; `itemWantedIndex` | Replaced by `eatingItemId` and `wantedItemId`. Note that these contain qualified item IDs, whereas the old fields had unqualified IDs. |
| `Utility` | `CollectOrDrop(…)` | Replaced by `Game1.player.CollectOrDrop(…)`. See also *utility fields & methods* for improvements to it. |
| `getLoveInterest(…)` | Removed. Use the equivalent data field instead: 
```js
// from an NPC
string loveInterest = npc.GetData()?.LoveInterest;

// or from an NPC name
if (NPC.TryGetData(name, out var data))
{
    string loveInterest = data.LoveInterest;
}
```
 |  |
| `IsLegacyIdAbove(…)`; `IsLegacyIdBetween(…)` | Removed. No longer used by the base game. |  |
| `Wallpaper` | `GetSetData()` | Removed. The equivalent new code is: 
```js
var setData = ItemRegistry.GetData(item.QualifiedItemId)?.RawData as ModWallpaperOrFlooring;
```
 |

### Other changes
<ul>
<li>Added `event.floorSprites` field for temporary animated sprites that should be drawn on the ground (e.g. under NPC shadows).</li>
<li>Added `GameStateQuery.TryResolveCommandName` method to get the actual query name given a case-insensitive name or alias.</li>
<li>The game now uses forward slashes in all asset names (like `Maps/Town` instead of `Maps\Town`). This shouldn't affect most mods, since SMAPI already converted all asset names to forward slashes.</li>
<li>Applying or removing buffs on a remote player will now sync the change to that player. This only works for buffs in `Data/Buffs`; the other method signatures have been renamed to `ApplyLocal*` for clarity.</li>
<li>Fixed broken sprite when drawing a flooring/wallpaper item based on its `parsedItemData.GetSourceRect()`.</li>
<li>Fixed machine auto-load leaving empty slots in chests when taking a secondary ingredient.</li>
<li>Fixed issue where placing a storage furniture (e.g. a dresser or fish tank) on a table would discard any items inside it.</li>
<li>Fixed the read-book animation not setting `Farmer.canMoveNow()` as intended.</li>
<li>Fixed issues with specific APIs:

| type | member | changes |
| --- | --- | --- |
| `CollisionMask` | `Characters` | This enum now includes actors in events and festivals. |
| `Event` | `TryGetEventCommandHandler(…)` | Fixed commands not being registered if it's called before any other event method. |
| `Inventory` | `OnSlotChanged` | Fixed event raised before the new value is visible. |
| `MarriageDialogueReference` | `GetHashCode()` | Fixed the same hash sometimes returned for two keys in the same asset. |
| `NetList` | `OnElementChanged` | Fixed event raised before the new value is visible. |
| `NetMutex` | `ReleaseLock()` | When locked multiple times locally (ex. a successfully acquired callback calling another function which also locks the same `NetMutex` and both call `ReleaseLock()` in their acquired callback), fixes the lock being fully released upon the first call, rather than after all local locks are undone. |
| `MultipleMutexRequest` | general usage | Allows success if a `NetMutex` is already owned locally, matching the existing `NetMutex` behavior. |
</li>
</ul>

## Name translation changes for wiki maintainers
Some in-game item and monster names were retranslated in some languages for clarity or correctness.

This list is mostly provided to help wiki maintainers update pages, so it doesn't include names which only changed capitalization, changed `'` (straight apostrophe) to `’` (curly) in French, or aren't visible to vanilla players (like mine rocks).

### Objects
- **Hungarian**: | item | old name | new name |
| --- | --- | --- |
| Artichoke Dip | *Articsóka mártás* | *Articsókamártás* |
| Blackberry Cobbler | *Szeder kosár* | *Szedres kosárka* |
| Bone Mill | *Csontőrlő* | *Csontdaráló* |
| Chocolate Cake | *Csokis torta* | *Csokitorta* |
| Cranberry Candy | *Vörös áfonyás cukorka* | *Vörös áfonyás üdítő* |
| Geode Crusher | *Geóda zúzó* | *Geódazúzó* |
| Maple Syrup | *Juharfaszirup* | *Juharszirup* |
| Tea Sapling | *Tea csemete* | *Teacsemete* |
| Woodcutter's Weekly | *Favágok hetilapja* | *Favágók hetilapja* |

- **Russian**: | item | old name | new name |
| --- | --- | --- |
| Aged Roe (flavored) | *Вяленая икра (Альбакор)* | *Вяленая икра альбакора* |
| *Вяленая икра (Анчоус)* | *Вяленая икра анчоуса* |  |
| *Вяленая икра (Морской черт)* | *Вяленая икра Морского черта* |  |
| *Вяленая икра (Рыба-капля)* | *Вяленая икра рыбы-капли* |  |
| *Вяленая икра (Голубой дискус)* | *Вяленая икра голубого дискуса* |  |
| *Вяленая икра (Лещ)* | *Вяленая икра леща* |  |
| *Вяленая икра (Сомик)* | *Вяленая икра сомика* |  |
| *Вяленая икра (Карп)* | *Вяленая икра карпа* |  |
| *Вяленая икра (Сом)* | *Вяленая икра сома* |  |
| *Вяленая икра (Голавль)* | *Вяленая икра голавля* |  |
| *Вяленая икра (Гребешок)* | *Вяленая икра гребешка* |  |
| *Вяленая икра (Сердцевидка)* | *Вяленая икра сердцевидки* |  |
| *Вяленая икра (Краб)* | *Вяленая икра краба* |  |
| *Вяленая икра (Рак)* | *Вяленая икра рака* |  |
| *Вяленая икра (Багрянник)* | *Вяленая икра Багрянника* |  |
| *Вяленая икра (Дорада)* | *Вяленая икра дорады* |  |
| *Вяленая икра (Угорь)* | *Вяленая икра угря* |  |
| *Вяленая икра (Камбала)* | *Вяленая икра камбалы* |  |
| *Вяленая икра (Пятнистый криворот)* | *Вяленая икра пятнистого криворота* |  |
| *Вяленая икра (Ледорыб)* | *Вяленая икра Ледорыба* |  |
| *Вяленая икра (Ледорыб-младший)* | *Вяленая икра Ледорыба-младшего* |  |
| *Вяленая икра (Бычок)* | *Вяленая икра бычка* |  |
| *Вяленая икра (Палтус)* | *Вяленая икра палтуса* |  |
| *Вяленая икра (Сельдь)* | *Вяленая икра сельди* |  |
| *Вяленая икра (Ледопип)* | *Вяленая икра ледопипа* |  |
| *Вяленая икра (Большеротый окунь)* | *Вяленая икра большеротого окуня* |  |
| *Вяленая икра (Лавовый угорь)* | *Вяленая икра лавового угря* |  |
| *Вяленая икра (Легенда)* | *Вяленая икра Легенды* |  |
| *Вяленая икра (Легенда II)* | *Вяленая икра Легенды II* |  |
| *Вяленая икра (Змеезуб)* | *Вяленая икра змеезуба* |  |
| *Вяленая икра (Крылатка)* | *Вяленая икра крылатки* |  |
| *Вяленая икра (Омар)* | *Вяленая икра омара* |  |
| *Вяленая икра (Полуночный карп)* | *Вяленая икра полуночного карпа* |  |
| *Вяленая икра (Ночной кальмар)* | *Вяленая икра ночного кальмара* |  |
| *Вяленая икра (Мисс Морской черт)* | *Вяленая икра Мисс Морской черт* |  |
| *Вяленая икра (Мидия)* | *Вяленая икра мидий* |  |
| *Вяленая икра (Карп-мутант)* | *Вяленая икра Карпа-мутанта* |  |
| *Вяленая икра (Осьминог)* | *Вяленая икра осьминога* |  |
| *Вяленая икра (Устрица)* | *Вяленая икра устриц* |  |
| *Вяленая икра (Окунь)* | *Вяленая икра окуня* |  |
| *Вяленая икра (Литорина)* | *Вяленая икра литорин* |  |
| *Вяленая икра (Щука)* | *Вяленая икра щуки* |  |
| *Вяленая икра (Иглобрюх)* | *Вяленая икра иглобрюха* |  |
| *Вяленая икра (Радиоактивный карп)* | *Вяленая икра Радиоактивного карпа* |  |
| *Вяленая икра (Радужная форель)* | *Вяленая икра радужной форели* |  |
| *Вяленая икра (Барабулька)* | *Вяленая икра барабульки* |  |
| *Вяленая икра (Берикс)* | *Вяленая икра берикса* |  |
| *Вяленая икра (Лосось)* | *Вяленая икра лосося* |  |
| *Вяленая икра (Песковник)* | *Вяленая икра песковника* |  |
| *Вяленая икра (Сардина)* | *Вяленая икра сардины* |  |
| *Вяленая икра (Скорпионовый карп)* | *Вяленая икра скорпионового карпа* |  |
| *Вяленая икра (Морской огурец)* | *Вяленая икра морского огурца* |  |
| *Вяленая икра (Морской еж)* | *Вяленая икра морского ежа* |  |
| *Вяленая икра (Шэд)* | *Вяленая икра шэда* |  |
| *Вяленая икра (Креветка)* | *Вяленая икра креветок* |  |
| *Вяленая икра (Слаймджек)* | *Вяленая икра слаймджека* |  |
| *Вяленая икра (Малоротый окунь)* | *Вяленая икра малоротого окуня* |  |
| *Вяленая икра (Улитка)* | *Вяленая икра улиток* |  |
| *Вяленая икра (Малек Багрянника)* | *Вяленая икра Малька Багрянника* |  |
| *Вяленая икра (Жуть-рыба)* | *Вяленая икра жуть-рыбы* |  |
| *Вяленая икра (Кальмар)* | *Вяленая икра кальмара* |  |
| *Вяленая икра (Скат)* | *Вяленая икра ската* |  |
| *Вяленая икра (Рыба-камень)* | *Вяленая икра рыбы-камня* |  |
| *Вяленая икра (Солнечник)* | *Вяленая икра солнечника* |  |
| *Вяленая икра (Чудо-огурец)* | *Вяленая икра чудо-огурца* |  |
| *Вяленая икра (Тигровая форель)* | *Вяленая икра тигровой форели* |  |
| *Вяленая икра (Тиляпия)* | *Вяленая икра тиляпии* |  |
| *Вяленая икра (Тунец)* | *Вяленая икра тунца* |  |
| *Вяленая икра (Лосось пустоты)* | *Вяленая икра лосося пустоты* |  |
| *Вяленая икра (Желтый судак)* | *Вяленая икра желтого судака* |  |
| *Вяленая икра (Лесоплюх)* | *Вяленая икра лесоплюха* |  |
| Artifact Spot | *Место для артефакта* | *Клетка с артефактом* |
| Bait | *Приманка* | *Наживка* |
| Basic Retaining Soil | *Обычная влагосохраняющая почва* | *Обычный аквагрунт* |
| Deluxe Retaining Soil | *Влагосохраняющая почва Делюкс* | *Аквагрунт Делюкс* |
| Dried Fruit (flavored) | *Сушеная морошка* | *Сушеная малина* |
| Goat Milk | *Бутыль козьего молока* | *Козье молоко* |
| Jelly (flavored) | *Джем из морошки* | *Малиновый джем* |
| Juice (flavored) | *Сок сквоша* | *Кабачковый сок* |
| Large Goat Milk | *Бол. бутыль козьего молока* | *Большое козье молоко* |
| Large Milk | *Бидон молока* | *Большое молоко* |
| Magic Bait | *Волшебная приманка* | *Волшебная наживка* |
| Milk | *Ведерко молока* | *Молоко* |
| Quality Fertilizer | *Высококачественное удобрение* | *Улучшенное удобрение* |
| Quality Retaining Soil | *Высококачественная влагосохраняющая почва* | *Улучшенный аквагрунт* |
| Quality Sprinkler | *Высококачественный спринклер* | *Улучшенный спринклер* |
| Roe (flavored) | *Икра (Альбакор)* | *Икра альбакора* |
| *Икра (Анчоус)* | *Икра анчоуса* |  |
| *Икра (Морской черт)* | *Икра Морского черта* |  |
| *Икра (Рыба-капля)* | *Икра рыбы-капли* |  |
| *Икра (Голубой дискус)* | *Икра голубого дискуса* |  |
| *Икра (Лещ)* | *Икра леща* |  |
| *Икра (Сомик)* | *Икра сомика* |  |
| *Икра (Карп)* | *Икра карпа* |  |
| *Икра (Сом)* | *Икра сома* |  |
| *Икра (Голавль)* | *Икра голавля* |  |
| *Икра (Гребешок)* | *Икра гребешка* |  |
| *Икра (Сердцевидка)* | *Икра сердцевидки* |  |
| *Икра (Краб)* | *Икра краба* |  |
| *Икра (Рак)* | *Икра рака* |  |
| *Икра (Багрянник)* | *Икра Багрянника* |  |
| *Икра (Дорада)* | *Икра дорады* |  |
| *Икра (Угорь)* | *Икра угря* |  |
| *Икра (Камбала)* | *Икра камбалы* |  |
| *Икра (Пятнистый криворот)* | *Икра пятнистого криворота* |  |
| *Икра (Ледорыб)* | *Икра Ледорыба* |  |
| *Икра (Ледорыб-младший)* | *Икра Ледорыба-младшего* |  |
| *Икра (Бычок)* | *Икра бычка* |  |
| *Икра (Палтус)* | *Икра палтуса* |  |
| *Икра (Сельдь)* | *Икра сельди* |  |
| *Икра (Ледопип)* | *Икра ледопипа* |  |
| *Икра (Большеротый окунь)* | *Икра большеротого окуня* |  |
| *Икра (Лавовый угорь)* | *Икра лавового угря* |  |
| *Икра (Легенда)* | *Икра Легенды* |  |
| *Икра (Легенда II)* | *Икра Легенды II* |  |
| *Икра (Змеезуб)* | *Икра змеезуба* |  |
| *Икра (Крылатка)* | *Икра крылатки* |  |
| *Икра (Омар)* | *Икра омара* |  |
| *Икра (Полуночный карп)* | *Икра полуночного карпа* |  |
| *Икра (Ночной кальмар)* | *Икра ночного кальмара* |  |
| *Икра (Мисс Морской черт)* | *Икра Мисс Морской черт* |  |
| *Икра (Мидия)* | *Икра мидий* |  |
| *Икра (Карп-мутант)* | *Икра Карпа-мутанта* |  |
| *Икра (Осьминог)* | *Икра осьминога* |  |
| *Икра (Устрица)* | *Икра устриц* |  |
| *Икра (Окунь)* | *Икра окуня* |  |
| *Икра (Литорина)* | *Икра литорин* |  |
| *Икра (Щука)* | *Икра щуки* |  |
| *Икра (Иглобрюх)* | *Икра иглобрюха* |  |
| *Икра (Радиоактивный карп)* | *Икра Радиоактивного карпа* |  |
| *Икра (Радужная форель)* | *Икра радужной форели* |  |
| *Икра (Барабулька)* | *Икра барабульки* |  |
| *Икра (Берикс)* | *Икра берикса* |  |
| *Икра (Лосось)* | *Икра лосося* |  |
| *Икра (Песковник)* | *Икра песковника* |  |
| *Икра (Сардина)* | *Икра сардины* |  |
| *Икра (Скорпионовый карп)* | *Икра скорпионового карпа* |  |
| *Икра (Морской огурец)* | *Икра морского огурца* |  |
| *Икра (Морской еж)* | *Икра морского ежа* |  |
| *Икра (Шэд)* | *Икра шэда* |  |
| *Икра (Креветка)* | *Икра креветок* |  |
| *Икра (Слаймджек)* | *Икра слаймджека* |  |
| *Икра (Малоротый окунь)* | *Икра малоротого окуня* |  |
| *Икра (Улитка)* | *Икра улиток* |  |
| *Икра (Малек Багрянника)* | *Икра Малька Багрянника* |  |
| *Икра (Жуть-рыба)* | *Икра жуть-рыбы* |  |
| *Икра (Кальмар)* | *Икра кальмара* |  |
| *Икра (Скат)* | *Икра ската* |  |
| *Икра (Рыба-камень)* | *Икра рыбы-камня* |  |
| *Икра (Осетр)* | *Икра осетра* |  |
| *Икра (Солнечник)* | *Икра солнечника* |  |
| *Икра (Чудо-огурец)* | *Икра чудо-огурца* |  |
| *Икра (Тигровая форель)* | *Икра тигровой форели* |  |
| *Икра (Тиляпия)* | *Икра тиляпии* |  |
| *Икра (Тунец)* | *Икра тунца* |  |
| *Икра (Лосось пустоты)* | *Икра лосося пустоты* |  |
| *Икра (Желтый судак)* | *Икра желтого судака* |  |
| *Икра (Лесоплюх)* | *Икра лесоплюха* |  |
| Salmonberry | *Морошка* | *Малина* |
| Smoked Fish (flavored) | *Копченый {1}* | *Копчен. {1}*; (This is the default neutral prefix. All vanilla fish are; overridden to use the previous name.) |
| Summer Squash Seeds | *Семена сквоша* | *Семена кабачка* |
| Summer Squash | *Сквош* | *Кабачок* |
| Targeted Bait | *Наживка: Альбакор* | *Наживка на альбакора* |
| *Наживка: Анчоус* | *Наживка на анчоуса* |  |
| *Наживка: Морской черт* | *Наживка на Морского черта* |  |
| *Наживка: Рыба-капля* | *Наживка на рыбу-каплю* |  |
| *Наживка: Голубой дискус* | *Наживка на голубого дискуса* |  |
| *Наживка: Лещ* | *Наживка на леща* |  |
| *Наживка: Сомик* | *Наживка на сомика* |  |
| *Наживка: Карп* | *Наживка на карпа* |  |
| *Наживка: Сом* | *Наживка на сома* |  |
| *Наживка: Голавль* | *Наживка на голавля* |  |
| *Наживка: Гребешок* | *Приманка для гребешков* |  |
| *Наживка: Сердцевидка* | *Приманка для сердцевидок* |  |
| *Наживка: Краб* | *Приманка для крабов* |  |
| *Наживка: Рак* | *Приманка для раков* |  |
| *Наживка: Багрянник* | *Наживка на Багрянника* |  |
| *Наживка: Дорада* | *Наживка на дораду* |  |
| *Наживка: Угорь* | *Наживка на угря* |  |
| *Наживка: Камбала* | *Наживка на камбалу* |  |
| *Наживка: Пятнистый криворот* | *Наживка на пятнистого криворота* |  |
| *Наживка: Ледорыб* | *Наживка на Ледорыба* |  |
| *Наживка: Ледорыб-младший* | *Наживка на Ледорыба-младшего* |  |
| *Наживка: Бычок* | *Наживка на бычка* |  |
| *Наживка: Палтус* | *Наживка на палтуса* |  |
| *Наживка: Сельдь* | *Наживка на сельдь* |  |
| *Наживка: Ледопип* | *Наживка на ледопипа* |  |
| *Наживка: Большеротый окунь* | *Наживка на большеротого окуня* |  |
| *Наживка: Лавовый угорь* | *Наживка на лавового угря* |  |
| *Наживка: Легенда* | *Наживка на Легенду* |  |
| *Наживка: Легенда II* | *Наживка на Легенду II* |  |
| *Наживка: Змеезуб* | *Наживка на змеезуба* |  |
| *Наживка: Крылатка* | *Наживка на крылатку* |  |
| *Наживка: Омар* | *Приманка для омаров* |  |
| *Наживка: Полуночный карп* | *Наживка на полуночного карпа* |  |
| *Наживка: Ночной кальмар* | *Наживка на ночного кальмара* |  |
| *Наживка: Мисс Морской черт* | *Наживка на Мисс Морской черт* |  |
| *Наживка: Мидия* | *Приманка для мидий* |  |
| *Наживка: Карп-мутант* | *Наживка на Карпа-мутанта* |  |
| *Наживка: Осьминог* | *Наживка на осьминога* |  |
| *Наживка: Устрица* | *Приманка для устриц* |  |
| *Наживка: Окунь* | *Наживка на окуня* |  |
| *Наживка: Литорина* | *Приманка для литорин* |  |
| *Наживка: Щука* | *Наживка на щуку* |  |
| *Наживка: Иглобрюх* | *Наживка на иглобрюха* |  |
| *Наживка: Радиоактивный карп* | *Наживка на Радиоактивного карпа* |  |
| *Наживка: Радужная форель* | *Наживка на радужную форель* |  |
| *Наживка: Барабулька* | *Наживка на барабульку* |  |
| *Наживка: Берикс* | *Наживка на берикса* |  |
| *Наживка: Лосось* | *Наживка на лосося* |  |
| *Наживка: Песковник* | *Наживка на песковника* |  |
| *Наживка: Сардина* | *Наживка на сардину* |  |
| *Наживка: Скорпионовый карп* | *Наживка на скорпионового карпа* |  |
| *Наживка: Морской огурец* | *Наживка на морского огурца* |  |
| *Наживка: Шэд* | *Наживка на шэда* |  |
| *Наживка: Креветка* | *Приманка для креветок* |  |
| *Наживка: Слаймджек* | *Наживка на слаймджека* |  |
| *Наживка: Малоротый окунь* | *Наживка на малоротого окуня* |  |
| *Наживка: Улитка* | *Приманка для улиток* |  |
| *Наживка: Малек Багрянника* | *Наживка на Малька Багрянника* |  |
| *Наживка: Жуть-рыба* | *Наживка на жуть-рыбу* |  |
| *Наживка: Кальмар* | *Наживка на кальмара* |  |
| *Наживка: Скат* | *Наживка на ската* |  |
| *Наживка: Рыба-камень* | *Наживка на рыбу-камень* |  |
| *Наживка: Осетр* | *Наживка на осетра* |  |
| *Наживка: Солнечник* | *Наживка на солнечника* |  |
| *Наживка: Чудо-огурец* | *Наживка на чудо-огурца* |  |
| *Наживка: Тигровая форель* | *Наживка на тигровую форель* |  |
| *Наживка: Тиляпия* | *Наживка на тиляпию* |  |
| *Наживка: Тунец* | *Наживка на тунца* |  |
| *Наживка: Лосось пустоты* | *Наживка на лосося пустоты* |  |
| *Наживка: Желтый судак* | *Наживка на желтого судака* |  |
| *Наживка: Лесоплюх* | *Наживка на лесоплюха* |  |
| Tree Fertilizer | *Древесное удобрение* | *Удобрение для деревьев* |
| Wine (flavored) | *Вино из морошки* | *Малиновое вино* |

### Furniture
- **Hungarian**: | item | old name | new name |
| --- | --- | --- |
| Artist Bookcase | *Művészi könyvespolc* | *Művészi könyvszekrény* |
| Birch Double Bed | *Birs dupla ágy* | *Nyírfa dupla ágy* |
| Birch Lamp End Table | *Nyírfa lámpás éjjeliszekrény* | *Nyírfa éjjeliszekrény lámpával* |
| China Cabinet | *Kínai vitrin* | *Porcelános vitrin* |
| Coffee Table | *Kávésasztal* | *Kávézó asztal* |
| Dark Bookcase | *Sötét könyvespolc* | *Sötét könyvszekrény* |
| Gray Joja Bookcase | *Szürke Joja könyvespolc* | *Szürke Joja könyvszekrény* |
| Gray Joja Coffee Table | *Szürke Joja kávésasztal* | *Szürke Joja kávézó asztal* |
| Joja Bookcase | *Joja könyvespolc* | *Joja könyvszekrény* |
| Joja Coffee Table | *Joja kávésasztal* | *Joja kávézó asztal* |
| Junimo Bookcase | *Junimo könyvespolc* | *Junimo könyvszekrény* |
| Large Wizard Bookcase | *Nagy varázsló könyvespolc* | *Nagy varázsló könyvszekrény* |
| Luxury Bookcase | *Luxus könyvespolc* | *Luxus könyvszekrény* |
| Mahogany Lamp End Table | *Mahagóni lámpás éjjeliszekrény* | *Mahagóni éjjeliszekrény lámpával* |
| Modern Bookcase | *Modern könyvespolc* | *Modern könyvszekrény* |
| Oak Lamp End Table | *Tölgyfa lámpás éjjeliszekrény* | *Tölgyfa éjjeliszekrény lámpával* |
| Retro Bookcase | *Retro könyvespolc* | *Retro könyvszekrény* |
| Short Bookcase | *Kicsi könyvespolc* | *Kicsi könyvszekrény* |
| Short Wizard Bookcase | *Alacsony varázsló könyvespolc* | *Alacsony varázsló könyvszekrény* |
| Small Wizard Bookcase | *Kicsi varázsló könyvespolc* | *Kicsi varázsló könyvszekrény* |
| Spirits Table | *Italosasztal* | *Italos asztal* |
| Walnut Lamp End Table | *Diófa lámpás éjjeliszekrény* | *Diófa éjjeliszekrény lámpával* |
| Wizard Bookcase | *Varázsló könyvespolc* | *Varázsló könyvszekrény* |

- **Russian**: | item | old name | new name |
| --- | --- | --- |
| Corn Plant | *Кукуруза* | *Драцена* |
| Pierre's Sign | *Знак Пьера* | *Вывеска Пьера* |

### Hats
- **French**: | item | old name | new name |
| --- | --- | --- |
| Good Ol' Cap | *Veille casquette* | *Vieille casquette* |

- **Russian**: | item | old name | new name |
| --- | --- | --- |
| Blobfish Mask | *Маска рыбы-пузыря* | *Маска рыбы-капли* |
| Bluebird Mask | *Маска сиалии* | *Маска синей птицы* |
| Cowgal Hat | *Шляпа девушки-ковбоя* | *Женская ковбойская шляпа* |
| Deluxe Pirate Hat | *Элитная пиратская шляпа* | *Роскошная пиратская шляпа* |
| Dinosaur Hat | *Шляпа-динозавр* | *Шапка динозавра* |
| Official Cap | *Кепка от униформы* | *Фуражка* |
| Panda Hat | *Шляпка Панды* | *Шапка панды* |
| Party Hat | *Шляпа тусовщика* | *Праздничный колпак* |
| Santa Hat | *Шляпа Санты* | *Шапка Санты* |
| Star Helmet | *Звездный шлем* | *Шапка со звездами* |
| Sunglasses | *Солнечные очки* | *Солнцезащитные очки* |
| Tiger Hat | *Тигриная шляпа* | *Тигриная шапка* |

### Pants
- **Russian**: | item | old name | new name |
| --- | --- | --- |
| Dinosaur Pants | *Динозавровы штаны* | *Динозавровые штаны* |

### Shirts
- **Hungarian**: | item | old name | new name |
| --- | --- | --- |
| Goodnight Shirt | *Jóéjt póló* | *Jó éjt póló* |

- **Russian**: | item | old name | new name |
| --- | --- | --- |
| 80's Shirt (female) | *Майка их 80-х (Ж)* | *Майка из 80-х (Ж)* |
| 80's Shirt (male) | *Майка их 80-х (М)* | *Майка из 80-х (М)* |

### Tools
- **Hungarian**: | item | old name | new name |
| --- | --- | --- |
| Advanced Iridium Rod | *Fejlett irídium horgászbot* | *Profi irídium horgászbot* |
| Milk Pail | *Tejesvödör* | *Tejes vödör* |

### Weapons
- **Hungarian**: | item | old name | new name |
| --- | --- | --- |
| Iridium Needle | *Irídium tű* | *Irídiumtű* |
| Iridium Scythe | *Irídium kasza* | *Irídiumkasza* |

- **Russian**: | item | old name | new name |
| --- | --- | --- |
| Abby's Planchette | *Дощечка Эбби* | *Спиритическая планшетка Эбби* |
| Dark Sword | *Темный Меч* | *Темный клинок* |
| Dragontooth Club | *Драконозубья дубинка* | *Дубина из драконьего зуба* |
| Dragontooth Cutlass | *Драконозубья сабля* | *Сабля из драконьего зуба* |
| Dragontooth Shiv | *Драконозубья заточка* | *Заточка из драконьего зуба* |
| Dwarf Dagger | *Кинжал Дварфов* | *Дварфийский кинжал* |
| Dwarf Hammer | *Молот Дварфов* | *Дварфийский молот* |
| Dwarf Sword | *Меч Дварфов* | *Дварфийский меч* |
| Insect Head | *Жучья Голова* | *Жучиная голова* |
| Leah's Whittler | *Точилка Леи* | *Резец Леи* |
| Seb's Lost Mace | *Потерянная палица Себа* | *Потерянная булава Себа* |
| Wind Spire | *Башня Ветров* | *Пика ветров* |

### Monsters
- **Hungarian**: | monster | old name | new name |
| --- | --- | --- |
| Truffle Crab | *Kőrák* | *Szarvasgombarák* |

## XNB impact
### Overview
This section provides a summary of the XNB files which changed in Stardew Valley 1.6.16. This doesn't include new files (since they won't impact existing mods), text changes in non-English files, or the switch from `\` to `/` in asset names (since that's backwards compatible).

XNB mods are disproportionately affected, since...
# they replace the entire file;
# they're loaded through the MonoGame content pipeline which is less tolerant of format changes;
# they don't benefit from the compatibility rewrites Content Patcher provides for its content packs.

Content Patcher packs are typically unaffected (and Content Patcher will try to rewrite content packs automatically). However if a content pack replaces an entire asset instead of editing, it's more likely to be affected like an XNB mod.

### Changed assets
Compatibility is broadly grouped into four categories:
- "**✘ broken**": mods will remove new content or significant changes, or the mod's intended changes will no longer work. Effects may range from display bugs to crashes, depending how the game uses that specific content.
- "✘ will remove changes": mods will remove minor changes in 1.6.16 (e.g. typo fixes).
- "✓ mostly unaffected": mods will only be affected if they edit specific entries or fields.
- Blank: no expected impact for the vast majority of mods.

| content asset | changes in 1.6.16 | compatibility for pre-1.6.16 mods |  |
| --- | --- | --- | --- |
| XNB mods | Content Patcher packs |  |  |
| `Characters/Dialogue/Abigail`; `Characters/Dialogue/Alex`; `Characters/Dialogue/Clint`; samp>Characters/Dialogue/Demetrius</samp>; `Characters/Dialogue/Emily`; `Characters/Dialogue/Elliott`; `Characters/Dialogue/Evelyn`; `Characters/Dialogue/Haley`; `Characters/Dialogue/Harvey`; `Characters/Dialogue/Kent`; `Characters/Dialogue/Leah` | Removed or moved event dialogues. |  |  |
| `Characters/Dialogue/LeoMainland` | Added missing `AcceptGift_(TR)ParrotEgg` dialogue key (copied from `Characters/Dialogue/Leo`). | ✘ will remove changes |  |
| `Characters/Dialogue/Lewis` |   * Removed or moved event dialogues. * Added missing `$h` portrait in `summer_Town_57_40` dialogue. | ✘ will remove changes |  |
| `Characters/Dialogue/Marnie` | Fixed a capitalization typo. | ✘ will remove changes |  |
| `Characters/Dialogue/MarriageDialogue` | Removed some redundant dialogues. | ✘ will remove changes |  |
| `Characters/Dialogue/Maru`; `Characters/Dialogue/Pam`; `Characters/Dialogue/Penny`; `Characters/Dialogue/Pierre`; `Characters/Dialogue/Robin` | Removed or moved event dialogues. |  |  |
| `Characters/Dialogue/Sam` | Merged, removed, or moved event dialogue keys. | **✘ broken** | ✓ mostly unaffected |
| `Characters/Dialogue/Sebastian`; `Characters/Dialogue/Shane` | Removed or moved event dialogues. |  |  |
| `Data/Achievements` | Overhauled data format. | **✘ broken** | **✘ broken** |
| `Data/Buildings` |   * Added new item spawn fields and new `Id` fields. * The `ModData` field is now null by default. * Fixed some non-unique inner IDs. | **✘ broken** |  |
| `Data/Characters` |   * Added new fields and new item spawn fields. * Fixed a broken asset name for the `???` character. | **✘ broken** |  |
| `Data/CookingRecipes` | Changed some recipe keys. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/*`; `Data/Events/Festivals/*` | Major changes including multilingual events, event labels, event key changes, replacing dialogue commands with event commands, etc. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/AnimalShop`; `Data/Events/ArchaeologyHouse` | Replaced `removeQuest` commands with `completeQuest`. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/Beach` |   * Replaced `awardFestivalPrize` commands with `addItem`. * Replaced `removeQuest` commands with `completeQuest`. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/Farm`; `Data/Events/FarmHouse` | Replaced `awardFestivalPrize` commands with `addItem`. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/Forest` |   * Replaced `awardFestivalPrize` commands with `addItem`. * Fixed event commands with invalid arguments. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/HaleyHouse`; `Data/Events/Hospital` | Fixed event commands with invalid arguments. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/IslandHut` | Quoted "???" names for consistency. |  |  |
| `Data/Events/IslandSouth` |   * Updated for event skip changes. * Fixed event commands with invalid arguments. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/JoshHouse` |   * Updated for new cooking recipe keys. * Fixed a George dialogue line which wasn't shown in 1.6.9+. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/Mine` | Replaced `awardFestivalPrize` commands with `addItem`. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/Mountain` |   * Replaced `awardFestivalPrize` commands with `addItem`. * Fixed event commands with invalid arguments. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/Saloon` | Fixed event commands with invalid arguments. | ✘ will remove changes | ✓ mostly unaffected |
| `Data/Events/SamHouse` |   * Replaced `removeQuest` commands with `completeQuest`. * Updated for merged `Characters/Dialogue/Sam:Event_band[1\|2\|3\|4]` dialogues. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/Town` |   * Replaced `awardFestivalPrize` commands with `addItem`. * Replaced `removeQuest` commands with `completeQuest`. * Fixed event commands with invalid arguments. | **✘ broken** | ✓ mostly unaffected |
| `Data/Events/Woods` | Replaced `removeQuest` commands with `completeQuest`. | **✘ broken** | ✓ mostly unaffected |
| `Data/ExtraDialogue` |   * Moved hardcoded events into `Data/Events/*`. * Replaced `removeQuest` commands with `completeQuest`. * Moved `SummitEvent_*` translations into `Strings/Events` (with a new name & format). * Moved some translations into `Characters/Dialogue/Default`. | **✘ broken** | ✓ mostly unaffected |
| `Data/FarmAnimals` | Removed `UseFlippedRightForLeft` for dinosaur. | **✘ broken** | ✓ mostly unaffected |
| `Data/FishPondData` | Added new item spawn fields. | **✘ broken** |  |
| `Data/FruitTrees` |   * Added new item spawn fields. * Fixed some non-unique inner IDs. | **✘ broken** |  |
| `Data/Furniture` |   * Added `spawns_butterflies` context tag for butterfly hutch. * Disabled rotation for decorative Joja door. | **✘ broken** | ✓ mostly unaffected |
| `Data/GarbageCans`; `Data/GiantCrops` | Added new item spawn fields. | **✘ broken** |  |
| `Data/HomeRenovations` | Added new `Id` fields. | **✘ broken** |  |
| `Data/LocationContexts` | Fixed non-deterministic weather conditions. | ✘ will remove fixes | ✓ mostly unaffected |
| `Data/Locations` |   * Added new item spawn fields. * Added `FOR_TUTORIAL_CATCH` arguments to `LOCATION_FISH` queries. * Changed Legend condition to use new `IS_RAINING` query to support mods weathers. * Fixed some non-unique inner IDs. | **✘ broken** | ✓ mostly unaffected |
| `Data/LostItemsShop` | Added new field. | **✘ broken** |  |
| `Data/Machines` | Added new item spawn fields and new `Id` fields. | **✘ broken** |  |
| `Data/Movies` | Added new item spawn fields. | **✘ broken** |  |
| `Data/MuseumRewards` | Added new `Id` fields. | **✘ broken** |  |
| `Data/Objects` |   * Added new item spawn fields. * Added new context tags (`fish_skip_minigame` and `ignore_gift_limits`). | **✘ broken** | ✓ mostly unaffected |
| `Data/PassiveFestivals` |   * Added new field. * Added `InLocations` values for Trout Derby and Squidfest. | **✘ broken** | ✓ mostly unaffected |
| `Data/Pets` | Added new item spawn fields and new `Id` fields. | **✘ broken** |  |
| `Data/Quests` | Removed unused quests 10 ("*Copper Ore*") and 131 ("*Willy's Challenge*"). |  |  |
| `Data/Shops` |   * Added new item spawn fields and new `Id` fields. * Updated for new cooking recipe keys. * Fixed Pierre's shop selling duplicate corn/sunflower/wheat seeds when you gave him the missing stocklist. * Furniture catalogue's `ALL_ITEMS` item query entry now additionally excludes items with the `exclude_from_furniture_catalogue` context tag. | ✘ will remove fixes | ✓ mostly unaffected |
| `Data/SpecialOrders` | Added new fields and new `Id` fields. | **✘ broken** |  |
| `Data/Tools` |   * Added new field and new `Id` fields. * Removed `Lantern` tool. | **✘ broken** |  |
| `Data/Weapons` |   * Added new field and new item spawn fields. * Added new context tags (`is_scythe`). | **✘ broken** | ✓ mostly unaffected |
| `Data/WildTrees` |   * Added new item spawn fields and new `Id` fields. * Fixed some non-unique inner IDs. | **✘ broken** |  |
| `Data/Weddings` |   * Fixed Lewis skipping his matrimonial dialogue at the farmer's wedding. * Quoted translation keys for consistency. | ✘ will remove changes | ✓ mostly unaffected |
| `Strings/1_6_Strings` |   * Moved hardcoded events into `Data/Events/*`. * Moved some translations into `Strings/Events`. * Replaced `awardFestivalPrize` commands with `addItem`. * | ✘ will remove changes | ✓ mostly unaffected |
| `Strings/Events` |   * Added translations for multilingual events. * Added translations moved from NPC dialogue assets. | **✘ broken** |  |
| `Strings/Locations` |   * Moved hardcoded events into `Data/Events/*`. * Moved some event translations into `Strings/Events`. * Replaced `removeQuest` commands with `completeQuest`. * Quoted "???" names for consistency. | **✘ broken** | ✓ mostly unaffected |
| `Strings/MovieReactions` | Removed unused translations. |  |  |
| `Strings/StringsFromCSFiles` |   * Removed unused translations. * Moved some translations into `Characters/Dialogue/Default`. |  |  |
| `Strings/Tools` | Removed unused translations. |  |  |
| `Strings/UI` | Merged male/female variants of translation keys. | **✘ broken** | ✓ mostly unaffected |

### Removed unused assets
Stardew Valley 1.6.16 deletes assets which weren't used by the game. Mods usually aren't affected by this.

- `Maps/Bus(people)`
- `Maps/busPeople`
- `Maps/Island_Secret`

### Removed or moved translation keys
These translation keys were moved or removed in 1.6.16. (For changes to the content, see *changed assets* above.)

| asset | key | changes |
| --- | --- | --- |
| `Characters/Dialogue/Abigail` | `Event_Tragic_1`; `Event_Tragic_2`; `Event_Tragic_3`; `Event_VideoGame_No`; `Event_VideoGame_Yes` | Removed (unused). |
| `Event_Cave_1`; `Event_Cave_2`; `Event_Cave2_1`; `Event_Cave2_2`; `Event_Cave_3`; `Event_Grave1`; `Event_Grave2`; `Event_Grave3`; `Event_Grave4`; `Event_Rain_1`; `Event_Rain_2`; `Event_Rain_3` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Alex` | `Event_beach1`; `Event_beach2`; `Event_parents1`; `Event_parents2`; `Event_parents3` | Removed (unused). |
| `Event_books1`; `Event_books2`; `Event_books3`; `event_box1`; `event_box2`; `event_box3`; `event_box4` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Clint` | `event_advice1`; `event_advice2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Demetrius` | `Event_Lab_Rat`; `Event_Lab_Silence`; `Event_tomato1`; `Event_tomato2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Elliott` | `event_boat1`; `event_boat2`; `event_idea1`; `event_idea2`; `event_idea3`; `event_toast1`; `event_toast2`; `event_toast3`; `event_toast4` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Emily` | `event_dance3` | Removed (unused). |
| `event_dance1`; `event_dance2` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Evelyn` | `Event_cookies1`; `Event_cookies2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Haley` | `Event_darkroom4`; `Event_darkroom5`; `Event_darkroom6`; `Event_room1`; `Event_room2`; `Event_room3` | Removed (unused). |
| `Event_beach1`; `Event_beach2`; `Event_clean1`; `Event_clean2`; `Event_clean3`; `Event_darkroom1`; `Event_darkroom2`; `Event_darkroom3`; `Event_jar1`; `Event_jar2` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Harvey` | `event_grave1`; `event_grave2` | Removed (unused). |
| `event_aerobics1`; `event_aerobics2`; `event_george1`; `event_george2`; `event_heart1`; `event_heart2`; `event_heart3` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Kent` | `event_popcorn1`; `event_popcorn2`; `event_popcorn3` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Leah` | `event_parents1`; `event_parents2`; `event_parents3`; `event_parents4`; `event_parents5`; `event_sculpt1`; `event_sculpt2`; `event_sculpt3` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Lewis` | `event_secret1`; `event_secret2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/MarriageDialogue` | `Indoor_Day_Abigail`; `Indoor_Day_Krobus`; `Rainy_Day_Abigail`; `Rainy_Day_Krobus` | Removed (unused duplicates of keys in `Characters/Dialogue/MarriageDialogue<npc>`). |
| `Characters/Dialogue/Maru` | `Event_Cut1`; `Event_Cut2`; `Event_Hospital_1`; `Event_Hospital_2`; `Event_Hospital_3`; `event_robot1`; `event_robot2`; `event_robot3`; `event_robot4`; `event_robot_explain1`; `event_robot_explain2`; `event_robot_explain3`; `Event_Space1`; `Event_Space2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Pam` | `event_credit1`; `event_credit2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Penny` | `event_family1`; `event_family2`; `event_family3`; | Removed (unused). |
| `event_cook1`; `event_cook2`; `event_cook3`; `event_mail1`; `event_mail2`; `event_mail3`; `event_old1`; `event_old2`; `event_old3`; `event_old4`; `event_pool1`; `event_pool2`; `event_pool3`; `event_pool4`; `event_pool5`; `event_speaker_kids1`; `event_speaker_kids2`; `event_speaker_kids3`; `event_speaker_kids4`; `event_speaker_kids5`; `event_speaker_kids6`; `event_speaker_no`; `event_speaker_yes` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Pierre` | `Event_naga1`; `Event_naga2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Robin` | `event_wood1`; `event_wood2` | Moved into `Strings/Events` and renamed. |
| `Characters/Dialogue/Sam` | `event_room1`; `event_room2`; `Thu_3`; | Removed (unused). |
| `Event_band1`; `Event_band2`; `Event_band3` | Merged into single translation, moved into `Strings/Events`, and renamed. |  |
| `event_snack1`; `event_snack2`; `event_snack3` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Sebastian` | `Event_cave1`; `Event_cave2`; `Event_cave3`; `Event_game1`; `Event_game2`; `fall_Fri_bad`; `fall_Fri_good` | Removed (unused). |
| `event_city1`; `event_city2`; `event_city3`; `event_city4`; `event_garage1`; `event_garage2`; `event_garage3` | Moved into `Strings/Events` and renamed. |  |
| `Characters/Dialogue/Shane` | `event_noLoan1`; `event_noLoan2`; `event_noLoan3`; `event_room1`; `event_room2` | Removed (unused). |
| `event_apologize1`; `event_apologize2`; `event_apologize3`; `event_stadium1`; `event_stadium2` | Moved into `Strings/Events` and renamed. |  |
| `Data/ExtraDialogue` | `SkullCavern_100_event`; `SkullCavern_100_event_honorable` | Removed; replaced by the de-hardcoded `Data/Events/SkullCave:SkullCavern100` event. |
| `Spouse_MonstersInHouse` | Removed; replaced by `Characters/Dialogue/Default:Spouse_MonstersInHouse`. |  |
| `SummitEvent_closingmessage2`; `SummitEvent_closingmessage3`; `SummitEvent_closingmessage4`; `SummitEvent_closingmessage5`; `SummitEvent_closingmessage`; `SummitEvent_Dialogue1_Lewis`; `SummitEvent_Dialogue1_Morris`; `SummitEvent_Dialogue1_Spouse`; `SummitEvent_Dialogue1B_Spouse`; `SummitEvent_Dialogue2_Lewis`; `SummitEvent_Dialogue2_Morris`; `SummitEvent_Dialogue2_Spouse`; `SummitEvent_Dialogue3_Abigail`; `SummitEvent_Dialogue3_Alex`; `SummitEvent_Dialogue3_Elliott`; `SummitEvent_Dialogue3_Emily`; `SummitEvent_Dialogue3_Haley`; `SummitEvent_Dialogue3_Harvey`; `SummitEvent_Dialogue3_Leah`; `SummitEvent_Dialogue3_Maru`; `SummitEvent_Dialogue3_Penny`; `SummitEvent_Dialogue3_Sam`; `SummitEvent_Dialogue3_Sebastian`; `SummitEvent_Dialogue3_Shane`; `SummitEvent_Intro2_Spouse`; `SummitEvent_Intro2_Spouse_Gruff`; `SummitEvent_Intro_Lewis`; `SummitEvent_Intro_Morris`; `SummitEvent_Intro_Spouse`; `SummitEvent_Outro_Lewis`; `SummitEvent_Outro_Morris` | Moved into `Strings/Events` and renamed. |  |
| `Strings/1_6_Strings` | `ForestPylonEvent` | Removed; replaced by the de-hardcoded `Data/Events/WizardHouseBasement:ForestPylon` event. |
| `Strings/Locations` | `FieldOfficeFinale` | Removed; replaced by the de-hardcoded `Data/Events/IslandFieldOffice:FieldOfficeDone` event. |
| `Gourmand_Request_0_Success`; `Gourmand_Request_1_Success`; `Gourmand_Request_2_Success` | Moved into `Strings/Events` and renamed. |  |
| `IslandFieldOffice_Intro_Event` | Removed; replaced by the de-hardcoded `Data/Events/IslandFieldOffice:FieldOfficeIntro` event. |  |
| `IslandHut_Event_ParrotBoyIntro` | Removed; replaced by the de-hardcoded `Data/Events/IslandHut:LeoIntro` event. |  |
| `IslandNorth_Event_SafariManAppear` | Removed; replaced by the de-hardcoded `Data/Events/IslandNorth:ProfSnailIntro` event. |  |
| `IslandSecret_Event_BirdieFinished`; `alreadyGotNuts` | Removed; replaced by the de-hardcoded `Data/Events/IslandWest:-666777` event. |  |
| `IslandSecret_Event_BirdieIntro` | Removed; replaced by the de-hardcoded `Data/Events/IslandWest:-888999` event. |  |
| `Strings/MovieReactions` | `Penny_seen_dislike_BeforeMovie`; `Penny_seen_like_BeforeMovie`; `Penny_seen_love_BeforeMovie` | Removed (unused). |
| `Strings/StringsFromCSFiles` | `DiaryEvent.cs.6658`; `DiaryEvent.cs.6660`; `DiaryEvent.cs.6664`; `DiaryEvent.cs.6667`; `DiaryEvent.cs.6669`; `DiaryEvent.cs.6670`; `Event.cs.1257`; `Event.cs.1258`; `Event.cs.1259`; `Event.cs.1260`; `QuestionEvent.cs.6692`; `Raft.cs.14204`; `Raft.cs.14205` | Removed (unused). |
| `Event.cs.1736<samp>; <samp>Event.cs.1738<samp> | Removed; replaced by <samp>Characters/Dialogue/Default:AskedToDance_*`. |  |
| `NPC.cs.3985` | Removed; moved to `Characters/Dialogue/Default:SpouseGiftJealous`. |  |
| `NPC.cs.3990` | Removed; moved to `Characters/Dialogue/Krobus:FirstMeeting`. |  |
| `NPC.cs.4001` | Removed; moved to `Characters/Dialogue/Default:SpouseStardrop`. |  |
| `NPC.cs.4507`; `NPC.cs.4508`; `NPC.cs.4509`; `NPC.cs.4510`; `NPC.cs.4511`; `NPC.cs.4512`; `NPC.cs.4513`; `NPC.cs.4514`; `NPC.cs.4515`; `NPC.cs.4516`; `NPC.cs.4517`; `NPC.cs.4518`; `NPC.cs.4519`; `NPC.cs.4522`; `NPC.cs.4523`; | Removed; replaced by `Characters/Dialogue/Default:TermsOfEndearment_*`. |  |
| `Strings/Tools` | `Lantern_Description`; `Lantern_Name` | Removed (unused). |
| `Strings/UI` | `PondQuery_StatusRequestPending_Rude0_Female`; `PondQuery_StatusRequestPending_Rude0_Male`; `PondQuery_StatusRequestPending_Rude1_Female`; `PondQuery_StatusRequestPending_Rude1_Male`; `PondQuery_StatusRequestPending_Rude2_Female`; `PondQuery_StatusRequestPending_Rude2_Male` | Merged into new keys without gender suffix (*e.g.* `PondQuery_StatusRequestPending_Rude0`). |

zh:模组:迁移至游戏本体1.6.16
