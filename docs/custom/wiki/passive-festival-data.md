> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Passive_Festival_data)

# Modding:Passive Festival data (`Passive_Festival_data`)

← Index

This page explains how the game stores and uses passive festival data. This is an advanced guide for mod developers. 

## Overview
A *passive festival* is a festival like the Night Market. They replace a location for a period of time, the player can enter/leave them anytime, and time continues passing while at the festival. Passive festivals have their data stored in the `Data/PassiveFestivals` asset.

## Data format
The `Data/PassiveFestivals` asset consists of a string → model lookup, where...
- The key is a unique string ID for the festival.
- The value is a model with the fields listed below.

| field | effect |
| --- | --- |
| `DisplayName` | A tokenizable string for the display name shown on the calendar. |
| `Season` | The season when the festival becomes active. |
| `StartDay`; `EndDay` | The days of month when the festival becomes active. |
| `StartTime` | The time of day when the festival opens each day. |
| `StartMessage` | A tokenizable string for the in-game toast notification shown when the festival begins each day. |
| `MapReplacements` | The locations to swap for the duration of the festival. Despite the field name, this swaps **locations** (e.g. as added by `CustomLocations` using Content Patcher), and not the location's map asset. This is specified as a string → string lookup, where the key is the original location to replace and the value is the new location. Both use the internal location name, as shown by the 679 Debug Mode mod. For example, this swaps the `Beach` location with `BeachNightMarket` during the Night Market: 
```json
"MapReplacements": {
    "Beach": "BeachNightMarket"
}
```
 |
| `Condition` | *(Optional)* A game state query which indicates whether the festival is enabled (subject to the other fields like `StartDay` and `EndDay`). Defaults to always enabled. |
| `ShowOnCalendar` | *(Optional)* Whether the festival appears on the calendar, using the same iridium-star icon as the Night Market. Default true. |
| `DailySetupMethod`; `CleanupMethod` | *(Optional)* A C# method which applies custom logic when the day starts (`DailySetupMethod`) and/or overnight after the last day of the festival (`CleanupMethod`). These must be specified in the form `<full type name>: <method name>` (like `ExampleMod.Namespace.Type, ExampleMod: MethodName`). The methods must be static, take zero arguments, and return void. |
| `CustomFields` | The custom fields for this entry. |

## NPC schedules
When a passive festival is active, NPCs will check for a schedule entry in this order:

| syntax | summary |
| --- | --- |
| `<festival ID>_<festival day>` | Applies on the given date. The <festival day> is relative, so `1` matches the festival `StartDay`.; Example: `NightMarket_3` or `marriage_NightMarket_3` |
| `<festival ID>` | Applies if there's no date-specific entry.; Example: `NightMarket` or `marriage_NightMarket` |

If the NPC is married to a player, they'll add a `marriage_` prefix to the keys (like `marriage_<festival ID>_<festival day>`) and ignore any entry without the prefix.

## See also
Modding:Festival data for data about other types of festivals.

ru:Модификации:Пассивные фестивали
zh:模组:被动节日
