> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Stats)

# Modding:Stats (`Stats`)

← Index

This page documents **stats**, which track simple numeric values in the game state. Stats are tracked separately for each player, though some stats represent global values.

## Overview
### Format
Each stat is identified by a unique string ID (i.e. the *stat key*). Stat keys are case-insensitive, so `stepsTaken` and `STEPSTAKEN` are equivalent. 

Stat values are positive integers from 0 to [4.2 billion](https://learn.microsoft.com/en-us/dotnet/api/system.uint32.maxvalue) (default 0).

### Reference stats in mod code
You can check stats...
- using a game state query like `PLAYER_STAT Current stepsTaken 500` (i.e. current player has taken 500+ steps);
- using a debug command like `debug getStat stepsTaken`;
- or in C# like `uint stepsTaken = Game1.player.stats.Get(StatKeys.StepsTaken)` (where `StatKeys` has constants for all of the base game's stats).

Mods can set stats...
- in Content Patcher packs via the `IncrementStat` trigger action or data fields like `StatsToIncrementWhenLoaded` in `Data/Machines`;
- or in C# code via the `Game1.player.stats` API (like `Game1.player.stats.Increment($"{this.ModManifest.UniqueID}_ExampleStat")`).

Stats can have arbitrary keys and are managed automatically by the game. For example:
- getting a stat which doesn't exist will just return zero;
- setting a stat which doesn't exist like `IncrementStat {{ModId}}_CustomStat` will just add it with the new value;
- and reducing a stat to zero will remove the stat (since a stat which doesn't exist has a zero value).

### Caveats
Stats are *not* synced in multiplayer, so farmhands can only access their own stats (not stats on the host or other farmhands).

## Built-in stats
This section lists the stats tracked by the base game.

### Currency & scores
| stat key | tracks |
| --- | --- |
| `SpecialOrderPrizeTickets` | The player's current number of prize tickets. |
| `SquidFestScore_<day>_<year>` | The player's SquidFest score for a specific date. |

### Player activity
| stat key | tracks |
| --- | --- |
| `ArtifactSpotsDug` | The number of artifact spots dug up by this player using a hoe. |
| `AverageBedTime` | The average in-game time when the player went to sleep each day since the save started. **Bug:** this stat tracks the average *display time*, not the actual time. For example, the average of 6am (0600) + 7am (0700) is incorrectly calculated as 0650 instead of 0630. 1.6.16 This is fixed in 1.6.16. |
| `BillboardQuestsDone` | The number of 'help wanted' billboard quests completed by the player. |
| `BoatRidesToIsland` | The number of times the player took Willy's boat to Ginger Island. |
| `ChildrenTurnedToDoves` | The number of children which the player sacrificed to the Dark Shrine of Selfishness. |
| `CompletedJunimoKart` | The number of times the player completed Junimo Kart. ⚠ This was added in 1.6.9 Stardew Valley 1.6.9, so multiple completions in previous versions count as 1. |
| `CompletedPrairieKing` | The number of times the player completed Journey of the Prairie King. ⚠ This was added in 1.6.9 Stardew Valley 1.6.9, so multiple completions in previous versions count as 1. |
| `CompletedPrairieKingWithoutDying` | The number of times the player completed Journey of the Prairie King without dying. ⚠ This was added in 1.6.9 Stardew Valley 1.6.9, so completions in previous versions aren't counted. |
| `DirtHoed` | The number of times the player tiled dirt using a hoe. |
| `ExMemoriesWiped` | The number of times the player sacrificed their ex-spouse's memories at the Dark Shrine of Memory. |
| `FishCaught`; `PreciseFishCaught` | The number of fish caught by the player while fishing. `PreciseFishCaught` is a subset of `FishCaught` which ignores some catches which don't strictly count as 'fish' (i.e. items which don't have the *Fish* category or `counts_as_fish_catch` context tag). For example, seaweed counts for `FishCaught` but not for `PreciseFishCaught`. |
| `FishingTreasures` | The number of treasure boxes opened while catching a fish. |
| `GeodesCracked` | The number of geodes **and** mystery boxes broken open by the player at Clint's shop or by placing them in a geode crusher. See also `MysteryBoxesOpened`. |
| `GiftsGiven` | The number of items which the player gave to NPCs as gifts. (This doesn't count items given to NPCs for other purposes like events or quests.) |
| `GoldenTagsTurnedIn` | The number of golden tags redeemed by the player at the Trout Derby. |
| `GoodFriends` | The number of NPCs with which the player has 8+ hearts of friendship. |
| `hardModeMonstersKilled` | The number of 'dangerous' monsters killed by the player. |
| `IndividualMoneyEarned` | The total gold earned by the player while the multiplayer separate wallets option is enabled. |
| `ItemsCooked` | The number of items created from cooking recipes by the player. |
| `ItemsCrafted` | The number of items created from crafting recipes by the player. |
| `ItemsForaged` | The number of forage items harvested by the player (including forage crops). |
| `MonstersKilled` | The number of monsters killed by the player. |
| `MossHarvested` | The number of times moss was harvested from trees by the player. |
| `MysteryBoxesOpened` | The number of mystery boxes opened by the player at Clint's shop or by placing them in a geode crusher. See also `GeodesCracked`. |
| `MysticStonesCrushed` | The number of mystic mine nodes broken by the player. |
| `OtherPreciousGemsFound` | The number of gem mine nodes broken by the player. **Bug:** this stat isn't tracked correctly in later versions of the game. 1.6.16 This is fixed in 1.6.16. |
| `PiecesOfTrashRecycled` | The number of items placed in recycling machines by the player. |
| `PreservesMade` | The number of items collected from preserves jars by the player. |
| `QuestsCompleted` | The number of quests and special orders completed by the player. For farmhands, a special order is counted if they're online when it's completed. |
| `RocksCrushed` | The number of times the player broke a rock using a pickaxe. |
| `SeedsSown` | The number of crop seeds planted by the player (including seeds planted in garden pots or in dirt). |
| `SlimesKilled` | The number of slimes killed by the player. This includes slime monsters, big slime monsters, and slimes bred by the player (e.g. in a slime hutch). |
| `StepsTaken` | The total number of steps taken by the player. |
| `StumpsChopped` | The number of large stumps and hollow logs broken by the player. |
| `TicketPrizesClaimed` | The number of times the player redeemed a prize ticket. |
| `TimesEnchanted` | The number of times the player enchanted a tool or weapon at the forge. |
| `TimesFished` | The number of times the player cast the fishing rod into water (regardless of whether they caught anything). |
| `TimesPanned` | The number of times the player found items while panning. |
| `TimesTossedBaby` | The number of times the player tossed a baby in the air. |
| `TimesUnconscious` | The number of times the player was knocked out due to their health reaching zero. |
| `TotalMoneyGifted` | The total gold which this player sent to other players through the town ledger. |
| `TrashCansChecked` | The number of times the player rummaged through a trash can. |
| `TreesChopped` | The number of wild trees chopped down by the player. |
| `WeedsEliminated` | The number of weeds destroyed by the player (including with bombs or scythes). **Bug:** this stat only tracks weeds destroyed indirectly (e.g. using a bomb), not those cut directly using a tool. 1.6.16 This is fixed in 1.6.16. |
| `WildTreesPlanted` | The number of wild tree seeds planted by the player. |

### Items collected
These track the number of individual items collected by players. Some stats under *player activity* track how many times the player performed a related action (e.g. `ItemsForaged` counts the number of times they harvested forage crops).

| stat key | tracks |
| --- | --- |
| `BeveragesMade` | The number of items collected from kegs by the player. |
| `CaveCarrotsFound` | The number of cave carrots found by the player by digging them up. (Cave carrots found any other way aren't counted.) <p>**Bug:** this stat isn't actually tracked correctly, and will always be zero. 1.6.16 This stat is removed in 1.6.16.</p> |
| `CheeseMade`; `GoatCheeseMade` | The number of items collected from cheese presses by the player. Goat cheese increments `GoatCheeseMade` (not `CheeseMade`); any other item increments `CheeseMade`. |
| `CopperFound`; `DiamondsFound`; `GoldFound`; `IridiumFound`; `IronFound`; `NotesFound`</samp>; `PrismaticShardsFound`; `StoneGathered` | The number of each item received by the player which was never previously in a player inventory. For example: copper ore collected after breaking a mine rock counts, but dropping the copper ore and picking it back up would not, nor would receiving copper ore from another player. The matching items are: * copper ore: `CopperFound`; * diamonds: `DiamondsFound`; * gold ore: `GoldFound`; * iridium ore: `IridiumFound`; * iron ore: `IronFound`; * lost books: `NotesFound` (not to be confused with secret notes); * prismatic shards: `PrismaticShardsFound`; * stone: `StoneGathered`. |

### Buffs & skills
| stat key | tracks |
| --- | --- |
| `BlessingOfWaters` | The current Blessings of Waters count. This starts at 3 when the blessing is granted, and decrements by one for each fish caught. |
| `Book_<book ID>` | Whether the player has read a power or skill book, where `0` is false and `1` (or higher values) means true. The <book ID> is the unqualified item ID for the book object. The matching books are: * Animal Catalogue: `Book_AnimalCatalogue`; * Book of Mysteries: `Book_Mystery`; * Dwarvish Safety Manual: `Book_Bombs`. * Friendship 101: `Book_Friendship`; * Horse: The Book: `Book_Horse`; * Jack Be Nimble, Jack Be Thick: `Book_Defense`; * Jewels Of The Sea: `Book_Roe`; * Mapping Cave Systems: `Book_Marlon`; * Monster Compendium: `Book_Void`; * Ol' Slitherlegs: `Book_Grass`; * Price Catalogue: `Book_PriceCatalogue`; * Queen Of Sauce Cookbook: `Book_QueenOfSauce`; * Raccoon Journal: `Book_WildSeeds`; * The Alleyway Buffet: `Book_Trash`; * The Art O' Crabbing: `Book_Crabbing`; * The Diamond Hunter: `Book_Diamonds`; * Treasure Appraisal Guide: `Book_Artifact`; * Way Of The Wind pt. 1: `Book_Speed`; * Way Of The Wind pt. 2: `Book_Speed2`; * Woody's Secret: `Book_Woodcutting`. |
| `MasteryExp` | The total mastery experience points earned by the player. |
| `MasteryLevelsSpent` | The number of mastery levels claimed by the player. |
| `Mastery_<skill ID>` | Whether the player has unlocked a mastery level for a skill. The <skill ID> is one of `0` (farming), `1` (fishing), `2` (foraging), `3` (mining), or `4` (combat). |
| `TrinketSlots` | The number of trinket slots unlocked by the player. This should be 0 or 1; any number beyond that is equivalent to 1. |

### World activity
**⚠ In multiplayer, these are primarily tracked for the main player. Make sure to read the full description before using them with a farmhand.**

| stat key | tracks |
| --- | --- |
| `BeachFarmSpawns` | The total number of beach forage which spawned on the beach farm. ⚠ For farmhands in multiplayer, the value is always zero. |
| `ChickenEggsLayed` | The number of eggs produced by chickens. ⚠ For farmhands in multiplayer, the value is always zero. |
| `CowMilkProduced` | The number of milk items produced by cows. ⚠ For farmhands in multiplayer, the value is always zero. |
| `CropsShipped` | The number of items with the *Fruit* or *Vegetable* category shipped by any players. See also `ItemsShipped`. ⚠ For farmhands in multiplayer, this is the number shipped while they were online to see the end-of-day shipping screen. |
| `DuckEggsLayed` | The number of eggs produced by ducks. ⚠ For farmhands in multiplayer, the value is always zero. |
| `DaysPlayed` | The number of days completed (i.e. times slept) by the player, including the new-game intro (even if it was skipped). ⚠ For farmhands in multiplayer, this is the value received from the host player the last time they were online when everyone went to sleep. |
| `GoatMilkProduced` | The number of milk items produced by goats. ⚠ For farmhands in multiplayer, the value is always zero. |
| `SheepWoolProduced` | The number of wool items produced by sheep. ⚠ For farmhands in multiplayer, the value is always zero. |
| `RabbitWoolProduced` | The number of wool items produced by rabbits. ⚠ For farmhands in multiplayer, the value is always zero. |
| `ItemsShipped` | The number of items shipped by any players. See also `CropsShipped`. ⚠ For farmhands in multiplayer, this is the number shipped while they were online to see the end-of-day shipping screen. |
| `TrufflesFound` | The number of truffles found by pigs. ⚠ For farmhands in multiplayer, the value is always zero. |

ru:Модификации:Статистика
zh:模组:统计数据
