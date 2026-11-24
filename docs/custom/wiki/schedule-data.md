> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Schedule_data)

# Modding:Schedule data (`Schedule_data`)

← Index

This page explains how the game stores and parses NPC schedule data, which determines where NPCs go and what they do when they get there. This is an advanced guide for mod developers.

## Raw data
Schedule data is stored in `Content/Characters/schedules/*.xnb` files (one per character), which can be unpacked for editing. Here's the raw data for Abigail as of 1.5.1 for reference:

**Data (Sample)**:

```json
{
  "rain": "900 SeedShop 9 5 0/1100 SeedShop 13 20 0/1300 SeedShop 39 5 0/1500 SeedShop 7 9 2/1900 SeedShop 6 4 0/2200 SeedShop 1 9 3 abigail_sleep",
  "rain2": "900 SeedShop 34 5 0/1100 SeedShop 9 17 3/1400 Saloon 42 17 2 abigail_sit_down/2000 SeedShop 1 9 3 abigail_sleep",
  "winter_15": "900 SeedShop 39 5 0/1030 ScienceHouse 5 19 1/1430 Beach 30 34 2 \"Strings\\schedules\\Abigail:winter_15.000\"/2400 SeedShop 1 9 3 abigail_sleep",
  "marriage_Mon": "830 SeedShop 6 19 0 \"Strings\\schedules\\Abigail:marriage_Mon.000\"/1300 Town 47 87 0 \"Strings\\schedules\\Abigail:marriage_Mon.001\"/1700 Saloon 33 18 0 \"Strings\\schedules\\Abigail:marriage_Mon.002\"/2030 BusStop -1 23 3",
  "marriage_Fri": "800 SeedShop 36 9 0 \"Strings\\schedules\\Abigail:marriage_Fri.000\"/1100 Mountain 49 31 2 abigail_flute/1500 Saloon 42 17 2 abigail_sit_down \"Strings\\schedules\\Abigail:marriage_Fri.001\"/2030 BusStop -1 23 3",
  "fall_Mon": "900 SeedShop 10 5 0/1100 Beach 13 39 2/1800 SeedShop 1 9 3 abigail_sleep",
  "6": "900 SeedShop 39 5 0/1030 SeedShop 2 20 3/1500 Mountain 46 23 2 abigail_flute/2000 SeedShop 1 9 3 abigail_sleep",
  "16": "GOTO 6",
  "spring_4": "900 SeedShop 11 5 0 \"Strings\\schedules\\Abigail:spring_4.000\"/1230 Hospital 13 14 0 \"Strings\\schedules\\Abigail:spring_4.001\"/1330 Hospital 4 6 1 \"Strings\\schedules\\Abigail:spring_4.002\"/1600 SeedShop 10 5 0/2000 SeedShop 1 9 3 abigail_sleep",
  "Sun": "900 SeedShop 4 5 0/1030 SeedShop 20 5 1 \"Strings\\schedules\\Abigail:Sun.000\"/1300 Forest 14 40 2/2000 SeedShop 1 9 3 abigail_sleep",
  "11_6": "GOTO spring",
  "25_6": "GOTO spring",
  "11": "NOT friendship Sebastian 6/1000 SebastianRoom 5 4 2 abigail_sit_down/1700 SeedShop 1 9 3 abigail_sleep",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Schedule key
Each schedule has a key which is used to decide when it applies. The key must be one of the exact formats below (it's not dynamic). If multiple schedules apply, the first match in the order listed here is used.

Notes:
- Schedule keys are not case-sensitive.
- The game's logic for schedule keys is in `NPC::TryLoadSchedule()`.

### Special schedules
These are checked first, regardless of marriage status.

| syntax | summary |
| --- | --- |
| `GreenRain` | Applies on green rain days in year 1. |

### Marriage schedules
These apply if the NPC is married to a player (not necessarily the main player). **Married NPCs don't use any other schedule keys.** If the marriage keys don't match, they won't have a schedule for that day.

| syntax | summary |
| --- | --- |
| `marriage_<festival ID>_<festival day>`; `marriage_<festival ID>` | Applies when a passive festival like the Night Market is active. The <festival ID> is the key from `Data/PassiveFestivals`, and <festival day> is the number of days since the passive festival started (where the first day is 1). |
| `marriage_<season>_<day of month>` | Applies on the given date.; Example: `marriage_spring_26` |
| `marriageJob` | Used by Harvey on Tuesday/Thursday, Maru on Tuesday/Thursday, Penny on Tuesday/Wednesday/Friday. No other NPC can use `marriageJob` without C# to change the game code. |
| `marriage_<day of week>` | Applies on the given day of week when not raining.; Example: `marriage_Mon` |

### Normal schedules
These schedules apply to any non-marriage NPC. If none of these keys match, they won't have a schedule for that day.

| syntax | summary |
| --- | --- |
| `<festival ID>_<festival day>`; `<festival ID>` | Applies when a passive festival like the Night Market is active. The <festival ID> is the key from `Data/PassiveFestivals`, and <festival day> is the number of days since the passive festival started (where the first day is 1). |
| `<season>_<day of month>` | Applies on the given date.; Example: `spring_15` |
| `<day of month>_<hearts>` | Applies on the given day of month if *any* player has at least that many hearts with the NPC. If multiple schedules apply, the one with the highest heart number is used.; Example: `11_6` |
| `<day of month>` | Applies on the given day of month.; Example: `16` |
| `bus` | For Pam only, applies if the bus is repaired. |
| `rain2` | 50% chance of applying on rainy days. |
| `rain` | Applies on rainy days. |
| `<season>_<day of week>_<hearts>` | Applies in the given season and day of week, if *any* player has at least that many hearts with the NPC. If multiple schedules apply, the one with the highest heart number is used.; Example: `spring_Mon_6` |
| `<season>_<day of week>` | Applies in the given season and day of week.; Example: `spring_Mon` |
| `<day of week>_<hearts>` | Applies on the given day of week, if *any* player has at least that many hearts with the NPC. If multiple schedules apply, the one with the highest heart number is used.; Example: `Mon_6` |
| `<day of week>` | Applies in the given day of week.; Example: `Mon` |
| `<season>` | Applies in the given season.; Example: `spring` |
| `spring_<day of week>` | Applies (in any season) on the given day of week.; Example: `spring_Mon` |
| `spring` | Always applies. This schedule is also used as a default in some cases.; DO NOT REMOVE THIS ENTRY. THAT WILL BREAK THE GAME. |
| `default` | Also used as a default sometimes. It's okay for this one to not exist, spring will be used instead. |

## Schedule script
### Format
Each schedule entry has a key, and a value containing an arbitrary number of the slash-delimited commands listed below.

These can be on a single line like this:

```js
"Wed": "1000 ArchaeologyHouse 11 9 0/1800 Town 47 87 0/2200 SeedShop 1 9 3 abigail_sleep"
```

Or you can add whitespace before or after commands for readability:

```js
"Wed": "
    1000 ArchaeologyHouse 11 9 0/
    1800 Town 47 87 0/
    2200 SeedShop 1 9 3 abigail_sleep
"
```

### Initial commands
A script may have one initial command as the first field in the script, before the first `/` character. The `GOTO` command can also appear in the next two fields after `MAIL`, or the next field after `NOT`.

| command | description |
| --- | --- |
| `GOTO <key>` | (Note: this line only applies if `GOTO` is the first command. If it's not, see `GOTO` below.); End the current script and load the schedule with the given key instead. The key does not need to be one of the standard keys and can be any arbitrary string. If the key is "season", the current season name is used instead. If the schedule doesn't exist or can't be parsed, the `spring` schedule will be used instead.; Example: `GOTO spring` |
| `NOT friendship <npc name> <hearts>` | End the current script if *any* player *does* have at least that many hearts with any of the named NPCs (can list multiple). If the script is ended, the `spring` schedule is used instead.; Example: `NOT friendship Sebastian 6 Abigail 3` (script ends if any player has 6+ hearts with Sebastian and/or 3+ hearts with Abigail). |
| `NOT` | Command ignored if the next word is not `friendship`. |
| `MAIL <letter ID>` | Runs the next command if the player did *not* receive the given letter ID or world state ID (see `HasFlag` in the [Content Patcher documentation](https://github.com/Pathoschild/StardewMods/tree/develop/ContentPatcher)); else continues from the command after that.; Example: `MAIL ccVault/GOTO spring/GOTO summer` (runs `GOTO summer` if the bus is repaired, or `GOTO spring` if it isn't; not limited to `GOTO` commands). |
| `GOTO <key>` | (Note: this line only applies if `GOTO` is after `NOT` or `MAIL`. If it's not, see `GOTO` above.); End the current script and load the schedule with the given key instead. If the key is "season", the current season name is used instead. If the key is "NO_SCHEDULE", the NPC has no schedule for the day. If the schedule doesn't exist or can't be parsed, the game crashes (or shows an error if playing with SMAPI).; Example: `GOTO NO_SCHEDULE` |

### Schedule points
The rest of the schedule script consists of slash-delimited entries, each containing space-separated fields which specify a start time, destination, and what to do when the NPC reaches it:
  `<time> [location] <tileX> <tileY> [facingDirection] [animation] [dialogue]`

If the `location` field is omitted, the rest of the fields are parsed as normal. The other optional fields must be in sequential order (*e.g.,* you can skip `animation` and `dialogue`, but you can't skip `facingDirection` and then specify `animation` and `dialogue`). The exception is `dialogue`. The `dialogue` can be specified without the `animation`.

| field | description |
| --- | --- |
| `time` | The time at which the schedule event begins, in military time (24 hour format, no colon). The time may be preceded with the single character '`a`' to indicate that the event should end (that is, the NPC should arrive at the destination) at the given time. In this case, the game will calculate when the event needs to begin. However, the event will never begin before the time specified for the previous event. In the unmodified game, this is only used to synchronise Emily's and Sandy's schedules on Fall 15. |
| `location` | <p>*(optional)* The location name the NPC should walk towards. If omitted, defaults to the previous map, or if it's the first, it defaults to the bus stop (if married to a player) or their default location.</p><p>**Note:** the game has special logic for unlockable locations. If this is JojaMart/Railroad and it's not available yet, the game gets the replacement destination from the first entry in the `<location>_Replacement` schedule if available; otherwise it switches to the `default` schedule if available, else `spring`. If this is CommunityCenter and it's not available yet, the game switches to the `default` schedule if available, else `spring`.</p><p>**Note also:** if the location is `bed`, the game will ignore the remaining parameters from this schedule point and attempt to load a "home" destination (map and coordinates): the warp out of `BusStop` back to the `Farm` for married NPCs, or for unmarried ones, the last schedule entry from the `default` or `spring` schedule (or the NPC's default location, if neither schedule can be loaded or parsed correctly). The NPC's sleeping animation (`<lowercase name>_sleep`) from `Data/animationDescriptions` will be set to play on arrival if it is defined.</p> |
| `tileX`; `tileY` | The X and Y tile coordinates the NPC should walk towards. |
| `facingDirection` | *(optional)* The direction to face after reaching the destination. The possible values are 0 (up), 1 (right), 2 (down), and 3 (left). Defaults to down. |
| `animation` | *(optional)* The animation to play when the NPC reaches the destination. This must be a key that appears in the `Content/Data/animationDescriptions.xnb` file. Each key entry has three required parts: entry frames/repeat frames/leaving frames. The numbers on them refer to the overworld sprite frame. The entry frames will play after arriving at the schedule point. Then the repeat frames will loop until the end of schedule time. Finally, the leaving frames will play before moving on to the next schedule point. Note that each frame is around 120ms so duplicates of frame is used to get desired looking animation. ; ;  The full format is `entry frames/repeat frames/leaving frames/message key/laying_down/offset X Y`. If a message key is given, the NPC will repeat that message if you talk to them while they are performing the animation. `offset X Y` lets you offset their sprite (in pixels). `laying_down` will hide the shadow. If you want to use offset or laying_down without a message, leave that spot empty. The order of `offset` and `laying_down` do not matter and either can be used without the other.; Example offset/laying down without message: `"haley_beach_towel": "20 20 21 21 21 21 21/22/21 21 20//laying_down/offset 0 16"`; Example offset without message or laying_down: `"haley_beach_towel": "20 20 21 21 21 21 21/22/21 21 20//offset 0 16"` (note there are now only 5 fields, not 6). ; ; Additionally, there are a few special animations. `sleep` anywhere in an animation name causes the NPC to go to sleep (which, among other things, prevents players from gifting or talking to the NPC while the animation is active.) The specific animation `NPCName_sleep` (with the NPCName fully lowercased) automatically used by spouses when they turn in for the night, and by NPCs coming home from Ginger Island. `square_X_Y_facing` causes NPCs to make random movements in a rectangle of at most X by Y (it can be less), centered at the schedule point. They will occasionally pause and face the `facing` direction. `change_beach` and `change_normal` cause NPCs to change into and out of their beach outfits, and is used by Ginger Island resort schedules. |
| `dialogue` | <p>*(optional)* The dialogue the NPC should use when they reach their destination. This must be an asset name and entry key in this exact format (including quotes): `"assetName:key"`. Any slashes in the asset name should be double backslashes (`\\`). The format of the entry key doesn't matter, it just needs to match one in the file.</p><p>For example, `"Strings\\schedules\\Abigail:Sun.000"` means "open the file at `Strings\schedules\Abigail.xnb` and get the text of the `Sun.000` key".</p><p>Tip: If you want schedule dialogue in marriage schedules, include the exact string `marriage` somewhere in the key, that will only show it to the npc's spouse. |

For example, consider this schedule entry in Abigail's schedule:

```text
1300 Town 47 87 0 \"Strings\\schedules\\Abigail:marriage_Mon.001\"
```

At 13:00 (1pm), Abigail will begin walking to tile (47, 87) in the Town location. When she reaches it, she'll face direction 0 (up) and say "*Hey, @. I like to relax here... it's so peaceful.*"

## Limitations
- When an NPC is added to an existing save, they generally don't follow their schedule correctly until you've slept once in-game (which triggers their first day update).
- Using a time of 600 (when the day starts) for an event may not work. Try using 610 if you want an event early in the morning and the NPC doesn't move if you use 600.

es:Modding:Datos de horarios
ru:Модификации:Расписание
zh:模组:行程数据
