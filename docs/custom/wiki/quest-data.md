> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Quest_data)

# Modding:Quest data (`Quest_data`)

← Index

This page explains how the game stores and parses quests. This is an advanced guide for mod developers.

## Raw data
Scripted quests are stored in `Content\Data\Quests.xnb`, which can be unpacked for editing. Here's the raw data as of 1.6.15 for reference:

**Data (Sample)**:

```json
{
  "1": "Location/Meet The Wizard/You received a letter from the local wizard. He claims to have information regarding the old community center./Enter the wizard's tower./WizardHouse/-1/0/-1/false",
  "2": "Basic/The Mysterious Qi/Within a secret lock-box, you found a note with peculiar instructions. It's signed by a 'Mr. Qi'./Leave a rainbow shell in the box at the train platform./null/-1/0/-1/false",
  "3": "Basic/The Mysterious Qi/You've found another note written by 'Mr. Qi'. The request is even more unusual this time./Place 10 beets inside Mayor Lewis' fridge./null/-1/0/-1/false",
  "4": "Basic/The Mysterious Qi/You've found yet another strange note within the Mayor's fridge. This time, the instructions are more cryptic./\"Give the sand dragon his final meal.\"/null/-1/0/-1/false",
  "5": "Basic/The Mysterious Qi/You found another note in the sand dragon's eye. It seems Mr. Qi's strange scavenger hunt has come to an end./Inspect the lumber pile beside your house./null/-1/0/-1/false",
  "6": "ItemHarvest/Getting Started/If you want to become a farmer, you have to start with the basics. Use your hoe to till the soil, then use a seed packet on the tilled soil to sow a crop. Water every day until the crop is ready for harvest./Cultivate and harvest a parsnip./(O)24/h7 8/100/-1/true",
  "7": "Building/Raising Animals/Robin, the local carpenter, lives north of town. In exchange for raw materials and money, she'll construct new buildings on your farm. You'll need her to build a coop or barn so that you can raise animals./Build a Coop./Coop/-1/100/-1/true",
  "8": "Crafting/Advancement/As you gain experience, you'll discover new crafting recipes to increase profit and make life easier. A scarecrow, for example, will prevent crows from snacking on your precious crops./Reach Farming level 1 and craft a scarecrow./(BC)8/-1/100/-1/true",
  "9": "Social/Introductions/It would be a nice gesture to introduce yourself around town. Some people might be anxious to meet the new farmer././null/25/0/-1/true",
  "10": "Basic/Copper Ore/You found a piece of copper ore. You're not sure what you can use it for, but the local blacksmith ought to know./Bring a piece of copper ore to Clint's blacksmith shop././-1/0/-1/false",
  "11": "Crafting/Forging Ahead/If you're going to keep mining, you should build a furnace./Craft a Furnace./(BC)13/12/0/-1/true",
  "12": "ItemHarvest/Smelting/Now that you've built a furnace, you can smelt some metal. According to Clint's instructions, if you place 5 copper ore and 1 piece of coal in the furnace, it should produce a copper bar./Use your furnace to smelt a Copper Bar./(O)334 1/-1/0/-1/true",
  "13": "Basic/To The Beach/Someone named Willy invited you to visit the beach south of town. He says he has something to give you./Visit the beach south of town before 5:00pm./-1/-1/0/-1/true",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Format
The entry for each quest contains these slash-delimited quest fields:

| Index | Field | Example Value |
| --- | --- | --- |
| Key | A unique string ID for the quest. | `MyQuestId` |
| 0 | The quest type. | Valid types are `Basic`, `Crafting`, `Location`, `Building`, `ItemDelivery`, `Monster`, `ItemHarvest`, `LostItem`, `SecretLostItem`*, and `Social`; ; If a quest uses the `Social` type, the completion requirement will always be to speak to everyone in town (like the Introductions quest and Emily's Say Hello quest), regardless of what you write in field 4. |
| 1 | The title that will be in the player's journal. | `A Cool Quest` |
| 2 | Description that will appear in the quest log. | `Abigail wants an amethyst. You should probably deliver one to her before she gets impatient.` |
| 3 | *(Optional)* Objective instructions that will appear in the quest log | `Deliver an amethyst to Abigail.` |
| 4 | *(Optional)* Completion Requirements | The format of this field will vary depending on the quest `Type`. See completion requirements. |
| 5 | *(Optional)* Next Quest Ids; Upon completing this quest, each of these next quests will be added to the player's quest log. Leave blank or use `0` or `-1` for none.; If a quest Id is prefixed with `h` and the player is not the host, they will not receive the quest. The `h` will be removed automatically. | `30 MySecondQuest hMyHostOnlyQuest` |
| 6 | The amount of money given upon completing the quest. | `500` |
| 7 | *(Unused)* Reward description; Leave this blank or less than 2 characters long or else it will show a blank reward when `Money Reward` is less than or equal to 0. | `-1` |
| 8 | *(Optional)* Can be cancelled (default false) | `true` |
| 9 | *(Optional)* Reaction dialogue; Required for `ItemDelivery`, `LostItem`, and `SecretLostItem` quests, as well as `Monster` quests when an NPC is specified. Not used in other quest types. | `Wow, thanks for the Amethyst! |

*A <samp>SecretLostItem` quest will be hidden from the player's quest log.

### Completion Requirements
The format of the **completion requirements** field will vary depending on what `Type` is listed in the first field:

| Type | Format | Additional Info |
| --- | --- | --- |
| `Basic` | `N/A` | A `Basic` quest is handled manually and it is up to you to complete it when appropriate. |
| `Crafting` | <Item Id> [isBigCraftable] | <Item Id> can be a qualified or unqualified item id. If it is unqualified, it will create either an Object or a Big Craftable depending on [isBigCraftable] (default false). |
| `Location` | <Location> | The player must warp to the location in order to complete the quest, even if they have already visited it before receiving the quest. |
| `Building` | <Building Type> | If the player has already constructed at least one <Building Type>, the quest will be completed immediately. |
| `ItemDelivery` | <NPC> <Item Id> [Count] | <Item Id> can be a qualified or unqualified item id. [Count] defaults to 1. |
| `Monster` | <Monster Name> <Count> [NPC] [ignoreFarmMonsters] | If the monster name has spaces in it, they must be replaced with underscores (e.g. `Green Slime` becomes `Green_Slime`). If you specify an [NPC], the player must talk to them after killing enough monsters in order to complete the quest. [ignoreFarmMonsters] defaults to `true`. |
| `ItemHarvest` | <Item Id> [Count] | <Item Id> can be a qualified or unqualified item id. [Count] defaults to 1. |
| `LostItem` | <NPC> <Item Id> <Location> <x> <y> | <NPC> is who the player must deliver the item to. <Item Id> can be a qualified or unqualified item id, however it *must* be an Object. <x> and <y> are the X and Y tile coordinates to place the item at inside the given <Location>. If provided, the NPC will use the `Reaction Text` field in the quest data, otherwise they will use default dialogue. |
| `SecretLostItem` | <NPC> <Item Id> <Friendship> [Quest Id] | <NPC> is who the player must deliver the item to. <Item Id> can be a qualified or unqualified item id. <Friendship> determines how many friendship points (not hearts) the player will receive upon delivering the item. If [Quest Id] is provided, then upon completing this quest, the game will *remove* the quest whose Id matches the given [Quest Id] from the player's active quest log without completing it. |

Here is the string in an easy to edit format:

ID: Type/Title/Description/Hint/Requirement/Next Quests/Money Reward/-1/Cancellable/Reaction Text

## See also
- Modding:Special orders

es:Modding:Datos de misiones
ru:Модификации:Задания
zh:模组:任务数据
