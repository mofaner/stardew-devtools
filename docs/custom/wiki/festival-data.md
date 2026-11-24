> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Festival_data)

# Modding:Festival data (`Festival_data`)

← Index

This page explains how the game stores and uses festival data. This is an advanced guide for mod developers.

## Data file
Each festival has a data file located at `Data/Festivals/<season><day of month>`. The game uses this asset to determine whether there's a festival today (if the asset exists), where and when it happens, and NPC placement/dialogue/behavior. The game also has a data file located at `Data/Festivals/FestivalDates` which contains the names and dates of festivals. Mod authors who wish to add a custom festival will need to add an entry to it as well.

### Raw data
The festival data file can be unpacked for editing. For example, here's the raw data from `Data/Festivals/fall27` as of 1.6.15 for reference:

**Data (Sample)**:

```json
{
  "name": "Spirit's Eve",
  "conditions": "Town/2200 2350",
  "set-up": "spirits_eve/-1000 -1000/farmer 12 54 1/changeToTemporaryMap Town-Halloween/viewport -1000 -1000/loadActors Set-Up/advancedMove Maru true 0 2 2 0 0 3 -11 0 0 -7 5 0 0 2 4 0/advancedMove Alex true -4 0 4 4000 4 0 4 5000/advancedMove Jas true 1 1500 4 1000/advancedMove Pam true 0 1 1 5000 0 -1 1 5000/advancedMove Shane true -7 0 0 2 2 9000 0 -2 7 0 4 9000/advancedMove Demetrius true 3 18000 4 3000/addTemporaryActor Skeleton 16 32 30 59 2 false Monster/advancedMove Skeleton true 0 1 3 4000 2 0 0 -1 4 2000 -1 0 3 1000 2 2000 0 -1 4 4000 -1 0 0 1 2 5000/addTemporaryActor Skeleton 16 32 35 58 2 false Monster/playerControl halloween/viewport player clamp true unfreeze/globalFadeToClear",
  "Abigail_spouse": "You'll have to tell me what's beyond, I can't go on... spiders...$s",
  "Leah_spouse": "I may have had a little too much pumpkin ale...",
  "Maru_spouse": "I'm confused... you go on ahead and let me figure this out.",
  "Penny_spouse": "Oh... These vapors... Hmmm... You look nice tonight.$l",
  "Haley_spouse": "I get scared easily... I think I'm just going to stay right here.$s",
  "Sebastian_spouse": "It would be interesting to have one of these on the farm.",
  "Sam_spouse": "Hey, I was wondering when you'd show up!$h",
  "Harvey_spouse": "Hi... I was hoping you'd find me here...$l",
  "Elliott_spouse": "Mmph... I believe I've eaten a few too many slices of pumpkin pie.",
  "Abigail": "I can't go on... spiders...$s",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
| field key | explanation |
| --- | --- |
| `name` | The festival name. This is used in display text like the calendar tooltip or the festival-is-ready message. |
| `conditions` | When and where the festival takes place. The format is `<location>/<start time> <end time>`, where <location> is the internal location name and the times are specified in 26-hour format (*i.e.,* 600 for 6am to 2600 for 2am). |
| `mainEvent` | The main event script. This is triggered when the player asks the festival host (usually Lewis) to begin the festival. This is absent for events that do not have a prompt or where speaking to Lewis does not trigger a new scene. |
| `shop` | The items the festival shop sells. See *shop format* below. |
| `set-up` | The event script that's run when the player first enters the festival. This includes moving the player to the festival map. |
| `Set-Up_additionalCharacters` | NPCs to spawn when the festival loads. This is specified as four space-delimited fields in the form `<NPC name> <tile X> <tile Y> <facing direction>`, repeated for each NPC to add with a `/` between each NPC. The <facing direction></samp> can be one of `up` or `0`, `down` or `2`, `left` or `3`, and `right` or `1`. These are additional to the NPCs spawned via the map file. For example, this adds Abigail and Leah side-by-side facing down: 
```javascript
"Set-Up_additionalCharacters": "Abigail 15 6 down/Leah 16 6 down"
```
 |
| `startedMessage` | A tokenizable string for a custom festival-started notification message. |
| `MainEvent_additionalCharacters`, or; `Main-Event_additionalCharacters` for the Festival of Ice | NPCs to spawn when the festival's `mainEvent` script is run. Just like `mainEvent`, this is absent for events that do not have a prompt to start a new scene. The format is identical to `Set-Up_additionalCharacters`, above. |
| `<NPC name>_roommate` | The dialog line the named NPC will say when the player talks to them, if they're roommates with the player. This field doesn't work on the Flower Dance festival and doesn't support `#$e#` breaks. If not specified, the `<NPC name>_spouse` will be used next. |
| `<NPC name>_spouse` | The dialog line the named NPC will say when the player talks to them, if they're married to the player. This field doesn't work on the Flower Dance festival and doesn't support `#$e#` breaks. |
| `<NPC name>` | The dialog line the named NPC will say when the player talks to them, if `<NPC name>_spouse`</samp> doesn't exist or apply. |
| `locationDisplayName` | The location name in the festival-started message (*e.g.,* "*The Luau has begun on the beach*") by default shows the internal name for non-vanilla festival locations. You can add this field to set the display name. |

Any other entry is event data for a cutscene during the festival. In some cases like the Luau, these are linked together when played in the game.

### Year variants
All fields in the festival data allow annual variants. These work by adding `_y<year variant>` to the end of key, where the <year variant> is an incrementing number starting at 1 with no upper limit. For example, two entries `set-up_y1` and `set-up_y2` will alternate (`set-up_y1` in year 1, `set-up_y2` in year 2, `set-up_y1` in year 3, etc). If year variants are defined for a key, the original key is ignored (*e.g.,* `set-up` will never be used if `set-up_y*` entries are defined).
**It is recommended to use the year variants for the `Set-Up_additionalCharacters` and `MainEvent_additionalCharacters` fields, even if you don't wish to spawn your custom NPC in different locations on odd and even years.**
These keys are global to the festival data, and if <strong>any</strong> mod appends to them, the game will use those keys instead of the non-variant keys. This will cause any NPCs not using the year variant keys to fail to spawn.

### Shop format
The shop data for a festival is stored as four space-separated values in the format `<item type> <item ID> <cost> <count available>`, which is repeated for each item. Field formats:

| field | explanation |
| --- | --- |
| <item type> | The item type. The valid values are `B` or `Boots` (boots), `BL` or `Blueprint` (blueprint), `BBL`, `BBl` or `BigBlueprint` (big blueprint), `BO` or `BigObject` (bigcraftable object), `F` (furniture), `H` or `Hat` (hat), `O` or `Object` (object), `R` or `Ring` (ring), or `W` or `Weapon` (weapon). |
| `<item id>` | The item's spritesheet index. |
| `<cost>` | The purchase price in gold. For seeds, this is modified based on the profit margin. |
| `<count available>` | How many of that item can be purchased from the shop, or `-1` for unlimited. |

Shop data may also be stored in `Data/Shops` and accessed via a map tile action, as non-festival shops are.

## Map
Many festivals use a separate map file located in the `Maps` folder. The map to use is specified in the data file's `set-up` field using the `changeToTemporaryMap` command.

### Spawned NPCs
**This is fragile and not recommended for custom NPCs or festivals.**
Consider using the data file's `Set-Up_additionalCharacters` field instead.

NPCs can be added to festivals using map tile indexes on a specific layer. This is enabled in the data file's `set-up` field using the `loadActors <layer name>` command. For each tile which exists on the layer, the tile index is mapped to the NPC's `FestivalVanillaActorIndex` field in the `Data\Characters` asset, with an offset which determines the facing direction (0 = up, 1 = right, 2 = down, or 3 = left).

For example, let's say the layer has a tile with tilesheet index 1. That matches Abigail's entry (`"FestivalVanillaActorIndex": 0`), with an offset of 1 so she's facing right. When the festival is loaded, Abigail will be added to that tile position facing right.

## Hardcoded logic
Several vanilla events have hardcoded scripted logic in the game code. For example, Lewis judging the farmer's grange display at the Stardew Valley Fair is handled in the game code, not the content assets documented here.

The Night Market is also handled differently from other festivals. While there are separate Night Market maps, the NPC data doesn't exist on these. Instead, NPCs dynamically enter and leave the festival via schedule data.

## See also
Modding:Passive Festival data for information about passive festivals.

ru:Модификации:Фестивали
zh:模组:节日数据
