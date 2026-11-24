> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Context_tags)

# Modding:Context tags (`Context_tags`)

← Index

This page explains *context tags*, arbitrary data label attached to items. These can produce various effects in-game, or may be informational only.

## Format
Context tags generally contain only alphanumeric, underscore, and parentheses characters like `category_gem` or `item_apple`. They're usually case-insensitive, but custom tags should be lowercase to be safe.

Many context tags are generated automatically based on the item data (like the item quality), and others are defined in an asset like `Data/Objects`.

## List of context tags
Here's an *incomplete* list of context tags added or used in the base game. Mods can add custom context tags, which aren't listed here.

### Added automatically
These are added for all items:

| context tag | effect |
| --- | --- |
| `category_<category>` | Added automatically based on the item category. See the 'context tag' column in the item category list for possible values. |
| `fish_<metadata>` | Added automatically for a fish item based on its metadata in `Data/Fish`. For crab pot fish (*i.e.* those with type `trap`): |
| context tag | notes |
| `fish_trap_location_<water type>` | Based on field 4 ('location'). For example, `fish_trap_location_ocean`. |

For fishing rod fish (*i.e.* those whose type is *not* `trap`):

| context tag | notes |
| --- | --- |
| `fish_difficulty_<difficulty>` | Based on field 1 ('chance to dart'), where <difficulty> is one of `easy` (0–33), `medium` (34–66), `hard` (67–100), or `extremely_hard` (101+). For example, `fish_difficulty_hard`. |
| `fish_motion_<motion>` | Based on field 2 ('darting randomness'). For example, `fish_motion_floater`. |
| `fish_favor_weather_<weather>` | Based on field 7 ('weather'). For example, `fish_favor_weather_sunny`. |

|-
| `id_<type>_<identifier>`
| Added automatically as an alternative ID. The <type> is one of `B` (boots), `BBL` (big craftable recipe), `BL` (object recipe), `BO` (big craftable), `C` (clothing), `F` (furniture), `H` (hat), `O` (object), `R` (ring), `W` (melee weapon), else blank. The <identifier> is the item's parent sheet index. For example, pufferfish has value `id_o_128` (object #128).
|-
| `item_<name>`
| Added automatically based on the item name. The name is trimmed, lowercase, with spaces replaced with underscores, and with single quotes removed. For example, '1000 Years From Now' has context tag `item_1000_years_from_now`.
|}

These are added for object-type items:

| context tag | effect |
| --- | --- |
| `item_type_<type>` | Added automatically based on the `Type` field in `Data/Objects`. The tag is checked in a few places (*e.g.* the museum to check if an item is an artifact or mineral). |
| `jelly_item`; `juice_item`; `pickle_item`; `wine_item` | For items produced by the keg or preserves jar, the preserved item type. |
| `preserve_sheet_index_<id>` | For items produced by the keg or preserves jar, the parent sheet index for the original item that was produced. For example, blueberry wine has `preserve_sheet_index_258`, where 258 is the blueberry item's index. |
| `quality_none`; `quality_silver`; `quality_gold`; `quality_iridium` | Added automatically based on the item quality. |
| `quality_qi` | Added automatically for items cooked while the *Qi's Cuisine* special order is active. |

### Colors
`color_*` tags are used by a number of game functions, including when the player dyes clothing at Emily's house, when tinting certain machine products such as Wine and Pickles, and when displaying the Book reading animation. For tinting purposes, each tag has a specific RGB value that is used, except when using Emily's Dye Pots, in which case they are grouped into one of the following categories.

| dye pot | context tags |
| --- | --- |
| red | `color_red`, `color_salmon`, `color_dark_red`, `color_pink` |
| orange | `color_orange`, `color_dark_orange`, `color_dark_brown`, `color_brown`, `color_copper` |
| yellow | `color_yellow`, `color_dark_yellow`, `color_gold`, `color_sand` |
| green | `color_green`, `color_dark_green`, `color_lime`, `color_yellow_green`, `color_jade`, `color_sea_green` |
| blue | `color_blue`, `color_dark_blue`, `color_dark_cyan`, `color_light_cyan`, `color_cyan`, `color_aquamarine` |
| purple | `color_purple`, `color_dark_purple`, `color_dark_pink`, `color_pale_violet_red`, `color_poppyseed`, `color_iridium` |
| no color | `color_black`, `color_gray`, `color_dark_gray`, `color_white`, `color_iron`, `color_prismatic` |

### Item behavior
| context tag | effect |
| --- | --- |
| `campfire_item` | Marks the item as a campfire. If the item also has the `torch_item` context tag, when it's placed in the world and turned on... * Campfire flames are drawn over it; * If the item is big craftable, light is emitted from its center instead of the top. |
| `crow_scare` | Whether this item acts as a scarecrow. Only applies to big craftables. |
| `crow_scare_radius_<radius>` | If this item is a scarecrow, the circular radius it covers. If the scarecrow doesn't have this context tag, its radius defaults to 17 if its internal name begins with `Deluxe` else 9. |
| `museum_donatable`; `not_museum_donatable` | Set whether the item can be donated to the museum, overriding the vanilla logic. |
| `not_giftable` | Prevents players from gifting this item to NPCs, who'll ignore the item entirely (e.g. as if you were holding a tool). This only affects gift-giving, it doesn't affect non-gift logic like quest goals or special order objectives. If the NPC also has a `reject_*` dialogue for the item, the dialogue takes priority. |
| `not_placeable`; `placeable` | Sets whether the item can be placed on the ground. |
| `prevent_loss_on_death` | Indicates the item can't be lost when the player dies. |
| `propose_roommate_<NPC name>` | Triggers a roommate proposal when given to the named NPC. The NPC name must be lowercase with underscores instead of spaces (e.g., `propose_roommate_dwarf`). |
| `sign_item` | Marks the item as a sign, which lets player display items on it or place it on a fish pond to show the fish count. |
| `torch_item` | Marks the item as a torch, which lets the player turn it on/off to emit light. See also `campfire_item`. |

### Machine processing
| context tag | effect |
| --- | --- |
| `crystalarium_banned` | When applied to a gem or mineral item, prevents players from placing it in a crystalarium. |
| `fish_pond_ignore` | Prevents players from adding this fish to fish ponds, even if it would otherwise match an entry in `Data/FishPondData`. |
| `geode_crusher_ignored` | Prevents breaking this item open in a geode crusher, even if the item has geode fields in `Data/Objects`. |
| `keg_juice`; `keg_wine` | Allows processing the item in a keg to produce a juice or wine variant. |
| `preserves_jelly`; `preserves_pickle` | Allows processing the item in a preserves jar to produce a jelly or pickled variant. |
| `seedmaker_banned` | When applied to a seed item, prevents players from placing it in a seed maker. |
| `tapper_item` | Marks the item as a tapper or heavy tapper. |
| `tapper_multiplier_<multiplier>` | The multiplier applied to the tapper production speed. For example, `2` will make items take half their base time (*i.e.* each item will finish in base time/speed multiplier). Defaults to 1 if omitted. |

### Informational
These tags have no effect on the game logic, they just provide metadata about the item.

| context tag | effect |
| --- | --- |
| `fish_legendary`; `fish_legendary_family` | Marks the fish as a legendary fish or legendary fish family. These are purely informational; the legendary fish behavior is determined by data fields like `CatchLimit` or `IsBossFish` in `Data/Locations`. |
| `geode` | *(Added automatically)* Marks the item as a geode item, which can be broken open at Clint's blacksmith shop or using a geode crusher. This is added automatically if the geode fields are present in `Data/Objects`. |
| `id_<item id>` | *(Added automatically)* The qualified item ID, like `id_(o)128`. This can be used to match or exclude an item by ID using context tags. Any spaces in the ID are replaced with underscores, and single quotes are removed. |
| `is_machine` | *(Added automatically)* Indicates the item has machine logic. This is added automatically based on `Data/Machines`. |
| `machine_input` | *(Added automatically)* Whether the item is a machine which accepts items from the player. This is added automatically based on the machine's fields in `Data/Machines`: * if `HasInput` is true; * *or* if any output rules have an `ItemPlacedInMachine` trigger. |
| `machine_output` | *(Added automatically)* Whether the item is a machine which produces items for the player to collect. This is added automatically based on the machine's fields in `Data/Machines`: * if `HasOutput` is true; * *or* if it has any output rules. |

### Others
The `debug listtags` console command lists all the tags of the item being held.

**raw tag dump (Sample)**:
Here's a list of context tags extracted from `Data/ObjectContextTags` that aren't listed above yet: `alcohol_item`, `algae_item`, `ancient_item`, `beach_item`, `bomb_item`, `bone_item`, `book_item`, `ceramic_item`, `chicken_item`, `color_black`, `color_dark_gray`, `color_gray`, `color_iron`, `color_prismatic`, `color_white`, `cooking_item`, `cow_milk_item`, `cowboy_item`, `crop_year_2`, `dinosaur_item`, `doll_item`, `drink_item`, `dwarvish_item`, `dye_medium`, `dye_strong`, `egg_item`, `elvish_item`, `fertilizer_item`, `fish_bug_lair`, `fish_carnivorous`, `fish_crab_pot`, `fish_desert`, `fish_freshwater`, `fish_lake`, `fish_legendary`, `fish_mines`, `fish_night_market`, `fish_nonfish`, `fish_ocean`, `fish_pond`, `fish_river`, `fish_secret_pond`, `fish_semi_rare`, `fish_sewers`, `fish_swamp`, `fish_talk_demanding`, `fish_talk_rude`, `fish_talk_stiff`, `fish_upright`, `flower_item`, `food_bakery`, `food_breakfast`, `food_cake`, `food_party`, `food_pasta`, `food_salad`, `food_sauce`, `food_seafood`, `food_soup`, `food_spicy`, `food_sushi`, `food_sweet`, `forage_item`, `forage_item_beach`, `forage_item_cave`, `forage_item_desert`, `forage_item_mines`, `forage_item_secret`, `fossil_item`, `fruit_item`, `fruit_tree_item`, `furnace_item`, `ginger_item`, `goat_milk_item`, `golden_relic_item`, `honey_item`, `hunting_item`, `instrument_item`, `jelly_item`, `juice_item`, `large_egg_item`, `large_milk_item`, `light_source`, `machine_item`, `marine_item`, `mayo_item`, `medicine_item`, `milk_item`, `noble_item`, `ore_item`, `pickle_item`, `potion_item`, `prehistoric_item`, `quality_fertilizer_item`, `scroll_item`, `season_all`, `season_fall`, `season_spring`, `season_summer`, `season_winter`, `slime_egg_item`, `slime_item`, `statue_item`, `strange_doll_1`, `strange_doll_2`, `syrup_item`, `totem_item`, `toy_item`, `trash_item`, `tree_seed_item`, `wood_item`.

## Usage in data
Some game data references context tags in a generic way. For example, you can add custom tags to an item, then reference them in the fish pond data.

For example:

| game data | effects |
| --- | --- |
| fish ponds | In `Data/FishPondData`, used to match fish that can be placed in the pond (see `RequiredTags` in the fish pond data). |
| special orders | In `Data/SpecialOrders`, used to match items that meet the quest objectives (see `AcceptedContextTags` in the special order data). |
| tailoring | In `Data/TailoringRecipes`, used to match items that are needed for a recipe. |
| gift tastes | In `Data/NPCGiftTastes`, used to set character likes and dislike for every item using the context tag. |

## For C# mods
### ItemContextTagManager class
For C# mods, the `ItemContextTagManager` class simplifies working with item context tags and reduces repeated code.

This provides a few utility methods:

| method | effect |
| --- | --- |
| `GetBaseContextTags(id)` | Get the base context tags for an item based on its raw data in `Data/Objects` or `Data/BigCraftables`. This doesn't include dynamic tags added that are based on instance info (like quality), which you can get using `item.GetContextTags()`. |
| `DoesTagQueryMatch(query, tags)` | Get whether a context tag query matches the given tags. For example, `ItemContextTagManager.DoesTagQueryMatch("bone_item, !fossil_item", item.GetContextTags())` returns true if the item is a bone item but not a fossil (like the Bone Flute). |
| `DoAllTagsMatch(requiredTags, actualTags)`; `DoAnyTagsMatch(requiredTags, actualTags)` | Get whether every (`DoAllTagsMatch`) or at least one (`DoAnyTagsMatch</samp) required tag matches the actual item tags. This supports negated required tags like <samp>"!fossil_item"` too. |
| `DoesTagMatch(requiredTag, actualTags)` | Get whether a single tag matches the actual item tags. This supports negated required tags like `"!fossil_item"` too. |
| `SanitizeContextTag(tag)` | *(Specialized)* Replace characters that may appear in item names so they're valid in context tags. For example, `SanitizeContextTag("Sam's Boombox")` returns `sams_boombox`. |

zh:模组:上下文标签
