> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Recipe_data)

# Modding:Recipe data (`Recipe_data`)

←Index

This page explains how the game stores and parses recipe data for cooking and crafting. (Tailoring recipes use a different format.) This is an advanced guide for mod developers.

## Raw data
### Cooking recipes
Cooking recipe data is stored in `Content\Data\CookingRecipes.xnb`, which can be unpacked for editing. Here's the raw data as of 1.6.15 for reference:

**Data (Sample)**:

```json
{
   "Fried Egg":"-5 1/10 10/194/default/",
   "Omelet":"-5 1 -6 1/1 10/195/l 10/",
   "Salad":"20 1 22 1 419 1/25 5/196/f Emily 3/",
   "Cheese Cauli.":"190 1 424 1/5 5/197/f Pam 3/",
   "Baked Fish":"145 1 132 1 246 1/6 5/198/l 12/",
   "Parsnip Soup":"24 1 -6 1 419 1/1 8/199/f Caroline 3/",
   "Vegetable Stew":"256 1 284 1/1 8/200/f Caroline 7/",
   "Complete Breakfast":"194 1 -6 1 210 1 211 1/2 2/201/l 26/",
   "Fried Calamari":"151 1 246 1 247 1/3 3/202/f Jodi 3/",
   "Strange Bun":"246 1 722 1 308 1/1 1/203/f Shane 7/",
   "Lucky Lunch":"154 1 229 1 597 1/20 1/204/s Luck 8/",
   "Fried Mushroom":"404 1 257 1 247 1/22 3/205/f Demetrius 3/",
   "Pizza":"246 1 256 1 424 1/6 4/206/l 20/",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Crafting recipes
Crafting recipe data is stored in `Content\Data\CraftingRecipes.xnb`, which can be unpacked for editing. Here's the raw data as of 1.6.15 for reference:

**Data (Sample)**:

```json
{
   "Wood Fence":"388 2/Field/322/false/default/",
   "Stone Fence":"390 2/Field/323/false/s Farming 2/",
   "Iron Fence":"335 1/Field/324 10/false/s Farming 4/",
   "Hardwood Fence":"709 1/Field/298/false/s Farming 6/",
   "Grass Starter":"771 10/Field/297/false/null/",
   "Blue Grass Starter":"771 25 Moss 10 MysticSyrup 1/Field/BlueGrassStarter/false/null/",
   "Gate":"388 10/Home/325/false/default/",
   "Chest":"388 50/Home/130/true/default/",
   "Big Chest":"388 120 334 2/Home/BigChest/true/null/",
   "Stone Chest":"390 50/Home/232/true/null/",
   "Big Stone Chest":"390 250/Home/BigStoneChest/true/null/",
   "Torch":"388 1 92 2/Field/93/false/default/",
   "Scarecrow":"388 50 382 1 771 20/Home/8/true/s Farming 1/",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Format
| Index | Field | Example Value |  |  |
| --- | --- | --- | --- | --- |
| Cooking | Crafting | Cooking | Crafting |  |
| Key | Name | *Salad* | *Stone Fence* |  |
| 0 | Ingredients | *20 1 22 1 419 1* | *390 2* |  |
| 1 | (Unused) | *25 5* | *Field* |  |
| 2 | Yield | *196* | *323* |  |
| — | 3 | Big craftable? | — | *false* |
| 3 | 4 | Unlock conditions | *f Emily 3* | *s Farming 2* |
| 4 | 5 | Display name; Tokenizable String. Defaults to the display name of the first product if not given. | *Ensalada* | *Valla de piedra* |

The values in **Index 1** are of different types for cooking and crafting recipes, but both kinds of value are unused by the game. For cooking, the field is set to a pair of numbers. For crafting, the field is set to `Home` or `Field`. The values in this field don't affect anything, so any pair of numbers can be used, but the field must be present in the recipe code.

### Ingredients and yield
The **ingredients** are a space-separated list of numbers in pairs. The first number of each pair is an object index from Data/Objects.xnb. (Negative numbers refer to categories, also listed in that article.) The second number of each pair is the quantity of that object that is required for the recipe.

The **yield** is also a space-separated list of numbers in pairs. The first number of each pair is an object or big craftable ID. The second number of each pair is the quantity of that object (or big craftable) that is created by the recipe. If there is only one object/craftable yielded, the quantity is optional and defaults to one.

### Unlock conditions
The **unlock conditions** field supports any one of these condition types, depending on recipe type:

| Syntax | Applicability | Description |
| --- | --- | --- |
| `f <NPC> <hearts>` | Cooking | When the player reaches the given number of hearts of friendship with the named NPC, a letter with the key `<NPC>Cooking` will be queued for delivery tomorrow. That letter then unlocks the recipe with the `%item cookingRecipe %%` token. |
| `s <skill> <level>` | both | When the player reaches the given level of the named skill, the recipe will be unlocked on the level up menu. The valid skill names are `Farming`, `Mining`, `Fishing`, `Foraging`, `Luck`, and `Combat` (but `Luck` is unimplemented). |
| `default` | both | This recipe is learned automatically. Any missing default recipes will be learned on day start. For example, the chest recipe is `"388 50/Home/130/true/default/"`. |
| `none`; *or any other value* | both | The recipe must be unlocked in some other way, such as an event. (See Exceptions below for starting recipes and other hard-coded unlocks.) |

### Exceptions
When the game cannot locate a recipe key in the data, the Torch recipe is loaded instead.

The Trapper profession is hard-coded to reset the ingredients for the Crab Pot recipe to 25 Wood and 2 Copper Bar.

The following recipes are hard-coded to be unlocked from the start of the game:
- Chest
- Wood Fence
- Gate
- Torch
- Campfire
- Wood Path
- Cobblestone Path
- Gravel Path
- Wood Sign
- Stone Sign
- Fried Egg

The Cask recipe is hard-coded to unlock when the cellar upgrade is complete.

Recipes that are unlocked during vanilla game events are hard-coded to be unlocked when those events are skipped.

The recipes unlocked by The Queen of Sauce TV channel are set in `Content\Data\TV\CookingChannel.xnb`.

ru:Модификации:Рецепты
zh:模组:配方数据
