> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Custom_wedding_events)

# Modding:Custom wedding events (`Custom_wedding_events`)

← Index

This page documents how the game stores and parses data for wedding events. This is an advanced guide for mod developers.

## Format
The wedding event can now be changed by editing the `Data\Weddings` data asset, which consists of a data model with two relevant fields (listed below).

| field | effect |
| --- | --- |
| `EventScript` | The event scripts which play the wedding. The game will use the script for the spouse NPC/player if it exists, otherwise it'll use the default script. This consists of a string → string dictionary, where... * The key is an NPC internal name (like `Abigail`), unique player ID, or `default` for the default script (which handles marrying either an NPC or player); * The value is a tokenizable string for the event script to play. The event scripts also have access to three extra tokens: |
| token | effect |
| `[SetupContextualWeddingAttendees]` | The concatenated `Setup` values for each of the `Attendees` present in the wedding. |
| `[ContextualWeddingCelebrations]` | The concatenated `Celebration` values for each of the `Attendees` present in the wedding. |
| `[SpouseActor]` | The actor ID for the NPC or other player being married (like `Abigail` for an NPC or `farmer2` for a player). This can be used in event commands like `faceDirection [SpouseActor] 1`. (You can also use `spouse` as an actor ID, but that will only work when marrying an NPC.) |

|-
| `Attendees`
| The other NPCs which should attend the wedding (unless they're the spouse). This consists of a string → model lookup, where the key is the internal NPC name, and the value is a model with these fields:

| field | effect |
| --- | --- |
| `ID` | The internal NPC name. |
| `Setup` | The NPC's tile position and facing direction when they attend. This is equivalent to field index 2 in the event basic data. |
| `Celebration` | *(Optional)* The event script to run during the celebration, like `faceDirection Pierre 3 true` which makes Pierre turn to face left. This can contain any number of script commands. |
| `Condition` | *(Optional)* A game state query which indicates whether the NPC should attend. Defaults to true. |
| `IgnoreUnlockConditions` | *(Optional)* Whether to add the NPC even if their entry in `Data/Characters` has an `UnlockConditions` field which doesn't match. Default false. |

|}

## See also
- See Modding:Event data for more information about event scripting.

ru:Модификации:Пользовательские свадебные события
zh:模组:自定义婚礼事件
