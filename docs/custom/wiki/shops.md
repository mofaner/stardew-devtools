> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Shops)

# Modding:Shops (`Shops`)

← Index

This page explains how to edit shop data.

## Data
### Format
You can create and change shops by editing the `Data/Shops` asset. This consists of a string → model lookup, where the key is a unique string ID for the shop, and the value is a model with these fields.

| field | effect |
| --- | --- |
| `Items` | The items to add to the shop inventory. This consists of a list of values with these fields: |
| field | effect |
| *common fields* | See item spawn fields for the generic item fields supported by shop items. Notes: * If set to an item query which returns multiple items, all of them will be added to the shop. * The `MinStack` and `MaxStack` fields apply to the item after it's purchased, and have no effect on the price or `Stock` limit. * If the player found Pierre's Missing Stocklist, season conditions in the `Condition` field are ignored in Pierre's General Store. |
| `Price` | *(Optional)* The gold price to purchase the item from the shop. Defaults to the item's normal price, or zero if `TradeItemId` is specified. |
| `TradeItemId`; `TradeItemAmount` | *(Optional)* The qualified or unqualified item ID and amount which must be traded to purchase this item. Defaults to no item and 1 respectively. If both `Price` and `TradeItemId` are specified, the player will have to provide both to get the item. |
| `ApplyProfitMargins` | *(Optional)* Whether to multiply the price by the difficulty modifier, which reduces the price for higher profit margins. This can be `true` (always apply it), `false` (never apply it), or `null` (apply for certain items like saplings). This is applied before any quantity modifiers. Default `null`. |
| `IgnoreShopPriceModifiers` | *(Optional)* Whether to ignore the shop's `PriceModifiers` field for this item. This has no effect on the item's equivalent field. Default false. |
| `AvailableStockModifiers`; `PriceModifiers` | *(Optional)* Quantity modifiers applied to the `AvailableStock` or `Price` values. Notes: * The price modifiers stack with the `PriceModifiers` field on the shop (unless `IgnoreStorePriceModifiers` is true). |
| `AvailableStockModifierMode`; `PriceModifierMode` | *(Optional)* quantity modifier modes which indicate what to do if multiple modifiers in the `AvailableStockModifiers` or `PriceModifiers` field apply at the same time. Default `Stack`. |
| `AvoidRepeat` | *(Optional)* Whether to avoid adding this item to the shop if it would duplicate one that was already added. If the item ID is randomized, this will choose a value that hasn't already been added to the shop if possible. Default false. |
| `UseObjectDataPrice` | *(Optional)* If this data produces an object and `Price` is omitted, whether to use the raw price in `Data/Objects` instead of the calculated sell-to-player price. |
| `AvailableStock` | *(Optional)* The maximum number of the item which can be purchased in one day. Default unlimited. |
| `AvailableStockLimit` | *(Optional)* If `Stock` is set, how the limit is applied in multiplayer. This has no effect on recipes. The possible values are: |
| value | effect |
| `Global` | The limit is shared by every player in the world. For example, if the `Stock` was `1` and a player bought it, no other players could buy one. |
| `Player` | The limit applies to each player separately. For example, if the `Stock` was `1`, each player could buy one. |
| `None` | The limit applies to the current instance of the shop menu. If you exit and reopen the menu, the item will reappear with the same stock. This is mainly used for items that are added conditionally. |

Default `Global`.
|-
| `ActionsOnPurchase`
| *(Optional)* A list of actions to run when the player purchases this item. These are run once per purchase click. Default none.

For example, this can be used to start a conversation topic when it's purchased:

```json
"ActionsOnPurchase": [
    "AddConversationTopic {{ModId}}_PurchasedItem 5"
]
```

|}
|-
| `SalableItemTags`
| *(Optional)* A list of context tags for items which the player can sell to to this shop. Default none.
|-
|valign="top"| `Owners`
| *(Optional)* The 'owner' data to apply when the shop is opened. This affects the portrait and dialogue shown, and whether the shop is closed. This field is checked *after* conditions specified in the `Action OpenShop` tile property.

The behavior depends on whether `Owners` is set:
- If omitted, the shop is opened regardless of whether an NPC is present, and no portrait or dialogue is shown.
- If it's specified, this consists of an *ordered* list. When the player opens the shop, the first matching entry is applied. When searching for an NPC, each entry checks either the [owner tile area] from the `Action OpenShop` tile property (if set), else the whole location. If no entries are matched, the shop isn't opened.

For example, let's say a shop has these owners:

```js
"Owners": [
    {
        "Name": "AnyOrNone",
        "ClosedMessage": "The shop opens from 1pm to 2pm.",
        "Condition": "!TIME 1300 1400"
    },
    {
        "Name": "Any",
        "Dialogues": [ "Hi there!" ]
    },
    {
        "Name": "Abigail",
        "Dialogues": [ "This entry is never selected." ]
    },
]
```

Here's what happens when the player clicks the shop:

# If it's not 1pm to 2pm, the first entry matches. The `ClosedMessage` shows a matching dialogue box and the shop isn't opened.
# Else if any NPC is present, the second entry applies. The third entry is always ignored, since Abigail would match the `"Name": "Any"` check.
# Else the shop isn't opened.

Each model in the list can specify these fields:

| field | effect |
| --- | --- |
| `Name` | One of... * the internal name for the NPC who must be in range to use this entry; * `AnyOrNone` to use this entry regardless of whether any NPC is within the shop area; * `Any` to use this entry if *any* NPC is within the shop area; * `None` to use this entry if *no* NPC is within the shop area. |
| `Id` | *(Optional)* An ID for this entry within the shop. This only needs to be unique within the current shop's owner list. Defaults to the `Name` value. |
| `Condition` | *(Optional)* A game state query which indicates whether this owner entry is available. If omitted, it's always available. |
| `Portrait` | *(Optional)* One of... * the internal name of the NPC whose portrait to show; * the asset name of the texture to display; * or an empty string (or other value which doesn't match an NPC or texture asset name) to disable the portrait. Defaults to the portrait for the NPC matching the `Name` field (if any). If drawn, it'll use the 64x64 pixel area in the top-left corner of the texture. Note : it is possible that name of "none" prevents any portrait to be used. |
| `Dialogues` | *(Optional)* A list of possible dialogue lines the shop will display for this Owner. The first entry with a matching condition will be chosen. Each entry consists of a model with these fields: |
| field | effect |
| `Id` | The unique string ID for this dialogue within the current list. |
| `Dialogue` | The dialogue text to show, as a tokenizable string. The resulting text is parsed using the dialogue format. You can use an empty string (like `"Dialogue": ""`) to remove the little dialogue box. |
| `RandomDialogue` | *(Optional)* A list of dialogue lines to randomly choose from, using the same format as `Dialogue`. If set, `Dialogue` is optional and ignored. Each entry in the list has an equal probability of being chosen every time the shop is opened (see `RandomizeDialogueOnOpen` below for how to change this to randomize daily). For example: 
```json
"RandomDialogue": [
    "[LocalizedText Strings\StringsFromCSFiles:ShopMenu.cs.11469]",
    "[LocalizedText Strings\StringsFromCSFiles:ShopMenu.cs.11470]",
    "[LocalizedText Strings\StringsFromCSFiles:ShopMenu.cs.11471]"
]
```
 |
| `Condition` | *(Optional)* A game state query which indicates whether the dialogue should be available. If omitted, the dialogue is always available. |

This can be set to an empty list (`"Dialogues": []`) to disable the dialogue text entirely. If omitted, defaults to a generic "*Have a look at my wares*" text.
|-
| `RandomizeDialogueOnOpen`
| *(Optional)* If a `Dialogues` entry is using `RandomDialogue`, whether to re-randomize which line is selected each time the shop is opened (`true`) or once per day (`false`). Default `true`.
|-
| `ClosedMessage`
| *(Optional)* If set, a tokenizable string for a 'shop is closed'-style message to display when this entry is selected.

**Notes:**
- This only applies if the `Owners` list is checked (i.e. the `Action OpenShop` conditions match). If you want to customize the message shown for the shop opening hours, remove the conditions from `Action OpenShop` and set them on this entry instead.
- If the selected entry sets this field, the shop is closed regardless of any other fields or whether the shop owner is present. When you set the `Name` field, that affects whether this entry is selected (i.e. whether the shop is closed).
|}
|-
| `Currency`
| *(Optional)* The currency in which all items in the shop should be priced. The valid values are 0 (money), 1 (star tokens), 2 (Qi coins), and 4 (Qi gems). Default 0. For item trading, see `TradeItemId` for each item.
|-
| `ApplyProfitMargins`
| *(Optional)* The default value for `ApplyProfitMargins` under `Items`, if set. This can be `true` (always apply it), `false` (never apply it), or `null` (apply for certain items like saplings). This is applied before any quantity modifiers. Default `null`.
|-
| `StackSizeVisibility`
| *(Optional)* How to draw stack size numbers in the shop list by default. If omitted, the default shop logic is applied (usually equivalent to `Show`).

The possible values are:

| value | effect |
| --- | --- |
| `Hide` | Always hide the stack size. |
| `Show` | Always draw the stack size. |
| `ShowIfMultiple` | Draw the stack size if it's more than one. |

This is ignored in some special cases (e.g. recipes can't have a stack size).
|-
| `OpenSound`
| *(Optional)* The audio cue ID to play when the shop menu is opened. Defaults to `dwop`.
|-
| `PurchaseSound`
| *(Optional)* The audio cue ID to play when an item is purchased normally. Defaults to `purchaseClick`.
|-
| `purchaseRepeatSound`
| *(Optional)* The audio cue ID to play when accumulating a stack to purchase (e.g. by holding right-click on PC). Defaults to `purchaseRepeat`.
|-
| `PriceModifiers`
| *(Optional)* Quantity modifiers applied to the sell price for items in this shop. See also `PriceModifiers` under `Items`.
|-
| `PriceModifierMode`
| *(Optional)* A quantity modifier mode which indicates what to do if multiple modifiers in the `PriceModifiers` field apply at the same time. This only affects that specific field, it won't affect price modifiers under `Items`. Default `Stack`.
|-
|valign="top"| `VisualTheme`
| *(Optional)* The visual theme to apply to the shop UI, or omit to use the default theme. The first matching theme is applied. All fields are optional and will fallback to the default theme.

This consists of a list of models with these fields:

| field | effect |
| --- | --- |
| `Condition` | *(Optional)* A game state query which indicates whether this theme should be applied. Defaults to always true. |
| `WindowBorderTexture`; `WindowBorderSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the shop window border. Defaults to `LooseSprites\Cursors` at (384, 373) with size 18×18. |
| `PortraitBackgroundTexture`; `PortraitBackgroundSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the NPC portrait background. Defaults to `LooseSprites\Cursors` at (603, 414) with size 74×74. |
| `DialogueBackgroundTexture`; `DialogueBackgroundSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the NPC dialogue background. Defaults to `Maps\MenuTiles` at (0, 256) with size 60×60. |
| `DialogueColor`; `DialogueShadowColor` | *(Optional)* The sprite text color for the dialogue text. See color format. Defaults to the game's standard text color. |
| `ItemRowBackgroundTexture`; `ItemRowBackgroundSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the item row background in the shop inventory. Defaults to `LooseSprites\Cursors` at (384, 396) with size 15×15. |
| `ItemRowBackgroundHoverColor` | *(Optional)* The color tint to apply to the item row background in the shop inventory when the cursor is hovering over it, or `White` for no tint. See color format. Defaults to `Wheat`. |
| `ItemRowTextColor` | *(Optional)* The sprite text color for the item text. See color format. Defaults to the game's standard text color. |
| `ItemIconBackgroundTexture`; `ItemIconBackgroundSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the background behind the item icons. Defaults to `LooseSprites\Cursors` at (296, 363) with size 18×18. |
| `ScrollUpTexture`; `ScrollUpSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the up arrow icon above the scrollbar. Defaults to `LooseSprites\Cursors` at (421, 459) with size 11×12. |
| `ScrollDownTexture`; `ScrollDownSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the down arrow icon beneath the scrollbar. Defaults to `LooseSprites\Cursors` at (421, 472) with size 11×12. |
| `ScrollBarFrontTexture`; `ScrollBarFrontSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the sliding scrollbar foreground. Defaults to `LooseSprites\Cursors` at (435, 463) with size 6×10. |
| `ScrollBarBackTexture`; `ScrollBarBackSourceRect` | *(Optional)* The texture asset name, and the pixel area within it, for the scrollbar background. Defaults to `LooseSprites\Cursors` at (403, 383) with size 6×6. |

|-
| `CustomFields`
| The custom fields for this entry.
|}

### Examples
You can add or replace entire shops. For example, this content pack adds a shop that sells ice cream in summer, and pufferfish all year:

    "Changes": [
        {
            "Action": "EditData",
            "Target": "Data/Shops",
            "Entries": {
                "Example.ModId_CustomShop": {
                    "Owners": [
                        {
                            "Name": "Any",
                            "Dialogues": [
                                // dialogue on sunny summer days
                                {
                                    "Id": "Example.ModId_SunnySummer",
                                    "Condition": "SEASON Summer, WEATHER Here Sun",
                                    "Dialogue": "Ice-cream is perfect for a day like this."
                                },

                                // dialogue any other time
                                {
                                    "Id": "Example.ModId_Default",
                                    "Dialogue": "Welcome to the only place in town for pufferfish!"
                                }
                            ]
                        }
                    ],

                    "Items": [
                        // ice-cream in summer, default price
                        {
                            "Id": "Example.ModId_IceCream",
                            "Condition": "SEASON Summer",
                            "ItemId": "(O)233"
                        },

                        // pufferfish for 1000g, limited to one per day per player
                        {
                            "Id": "Example.ModId_PufferFish",
                            "ItemId": "(O)128",
                            "Price": 1000,
                            "AvailableStock": 1,
                            "AvailableStockLimit": "Player"
                        }
                    ]
                }
            }
        }
    ]

You can also add, replace, edit, or reorder items in a specific shop by targeting the shop's `Items` field. For example, this removes Trout Soup (item #219) and adds Pufferfish above bait (item #685):

    "Changes": [
        {
            "Action": "EditData",
            "Target": "Data/Shops",
            "TargetField": [ "FishShop", "Items" ],
            "Entries": {
                "(O)219": null,
                "Example.ModId_Pufferfish": {
                    "Id": "Example.ModId_Pufferfish",
                    "ItemId": "(O)128",
                    "Price": 2000
                }
            },
            "MoveEntries": [
                { "Id": "Example.ModId_Pufferfish", "BeforeId": "(O)685" }
            ]
        }
    ]

### Vanilla shop IDs
The base game's shops are defined in `Data/Shops` too (except a few special cases like dressers and home renovations).

See `Data/Shops` for a full list, but here are the main shop IDs for convenience:

| shop | ID |
| --- | --- |
| Abandoned house shop | `HatMouse` |
| Adventurer's Guild | `AdventureShop` (regular shop); `AdventureGuildRecovery` (item recovery service) |
| Bookseller | `Bookseller` (regular shop); `BooksellerTrade` (book trades) |
| Casino | `Casino` |
| Clint's blacksmith shop | `Blacksmith` (regular shop); `ClintUpgrade` (tool upgrades) |
| Desert trader | `DesertTrade` |
| Dwarf's shop | `Dwarf` |
| Harvey's clinic | `Hospital` |
| Ice-cream stand | `IceCreamStand` |
| Island resort | `ResortBar` |
| Island trader | `IslandTrade` |
| Joja Mart | `Joja` |
| Krobus' shop | `ShadowShop` |
| Marnie's ranch | `AnimalShop` |
| Pierre's general store | `SeedShop` |
| Qi's gem shop | `QiGemShop<samp> |
| Raccoon Wife's Shop | <samp>Raccoon` |
| Robin's carpenter shop | `Carpenter` |
| Stardrop Saloon | `Saloon` |
| Sandy's Oasis shop | `Sandy` |
| Traveling cart | `Traveler` |
| Volcano dwarf shop | `VolcanoShop` |
| Willy's fish shop | `FishShop` |

And the main festival shops:

| festival shop | ID |
| --- | --- |
| Dance of the Moonlight Jellies | `Festival_DanceOfTheMoonlightJellies_Pierre` |
| Desert Festival (egg merchant) | `DesertFestival_EggShop` |
| Desert Festival (villager shops) | `DesertFestival_<name>`; (e.g. `DesertFestival_Abigail)` |
| Egg Festival | `Festival_EggFestival_Pierre` |
| Festival of Ice | `Festival_FestivalOfIce_TravelingMerchant` |
| Feast of the Winter Star | `Festival_FeastOfTheWinterStar_Pierre` |
| Flower Dance | `Festival_FlowerDance_Pierre` |
| Luau | `Festival_Luau_Pierre` |
| Night Market (decoration boat) | `Festival_NightMarket_DecorationBoat` |
| Night Market (magic boat) | `Festival_NightMarket_MagicBoat_Day1`; `Festival_NightMarket_MagicBoat_Day2`; `Festival_NightMarket_MagicBoat_Day3` |
| Spirit's Eve | `Festival_SpiritsEve_Pierre` |
| Stardew Valley Fair | `Festival_StardewValleyFair_StarTokens` |

And several 'shops' for the catalogues:

| item | ID |
| --- | --- |
| Catalogue | `Catalogue` |
| Furniture Catalogue | `Furniture Catalogue` |
| Joja Furniture Catalogue | `JojaFurnitureCatalogue` |
| Junimo Catalogue | `JunimoFurnitureCatalogue` |
| Retro Catalogue | `RetroFurnitureCatalogue` |
| Trash Catalogue | `TrashFurnitureCatalogue` |
| Wizard Catalogue | `WizardFurnitureCatalogue` |

## Open a custom shop
You can place an `Action OpenShop` tile property on the map, which will open the given shop ID when the player clicks it.

In C# code, you can get the inventory for a custom shop using `Utility.GetShopStock("shop id here")`, open a shop menu using `Utility.TryOpenShopMenu("shop id", …)`, and add temporary items to an open menu using `shopMenu.AddForSale(…)`. The ID of the opened shop is stored in the shop menu's `ShopId` field.

ru:Модификации:Магазины
zh:模组:商店
