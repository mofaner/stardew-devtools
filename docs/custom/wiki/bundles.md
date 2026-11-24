> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Bundles)

# Modding:Bundles (`Bundles`)

←Index

## Raw data
Bundle data is stored in `Content\Data\Bundles.xnb`, which can be unpacked for editing. Here's the raw data as of 1.5.1 for reference:

**Data (Sample)**:

```json
{
  "Pantry/0": "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0",
  "Pantry/1": "Summer Crops/O 621 1/256 1 0 260 1 0 258 1 0 254 1 0/3",
  "Pantry/2": "Fall Crops/BO 10 1/270 1 0 272 1 0 276 1 0 280 1 0/2",
  "Pantry/3": "Quality Crops/BO 15 1/24 5 2 254 5 2 276 5 2 270 5 2/6/3",
  "Pantry/4": "Animal/BO 16 1/186 1 0 182 1 0 174 1 0 438 1 0 440 1 0 442 1 0 639 1 0 640 1 0 641 1 0 642 1 0 643 1 0/4/5",
  "Pantry/5": "Artisan/BO 12 1/432 1 0 428 1 0 426 1 0 424 1 0 340 1 0 344 1 0 613 1 0 634 1 0 635 1 0 636 1 0 637 1 0 638 1 0/1/6",
  "Crafts Room/13": "Spring Foraging/O 495 30/16 1 0 18 1 0 20 1 0 22 1 0/0",
  "Crafts Room/14": "Summer Foraging/O 496 30/396 1 0 398 1 0 402 1 0/3",
  "Crafts Room/15": "Fall Foraging/O 497 30/404 1 0 406 1 0 408 1 0 410 1 0/2",
  "Crafts Room/16": "Winter Foraging/O 498 30/412 1 0 414 1 0 416 1 0 418 1 0/6",
  "Crafts Room/17": "Construction/BO 114 1/388 99 0 388 99 0 390 99 0 709 10 0/4",
  "Crafts Room/19": "Exotic Foraging/O 235 5/88 1 0 90 1 0 78 1 0 420 1 0 422 1 0 724 1 0 725 1 0 726 1 0 257 1 0/1/5",
  "Fish Tank/6": "River Fish/O 685 30/145 1 0 143 1 0 706 1 0 699 1 0/6",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Format
All bundles share the same format, which cannot be re-ordered.  Each field is explained below.

Key format:

| index | syntax | description |
| --- | --- | --- |
| 0 | room ID | The room of the Community Center that the bundle is located in. This can be `Pantry`, `Crafts Room`, `Fish Tank`, `Boiler Room`, `Vault`, and `Bulletin Board`. |
| 1 | sprite index | The bundle's sprite in the `LooseSprites/JunimoNote` spritesheet, starting from zero in the top-left corner of the sprite area. Some index numbers will cause a menu crash if used, for unknown reasons. |

Value format:

| index | syntax | description |
| --- | --- | --- |
| 0 | bundle name | The internal name for the bundle. |
| 1 | reward | Format: `<item type> <item ID> <count>` The reward given when the bundle is completed. Arguments: * <item type>: one of `O` (object), `BO` (big craftable), `F` (furniture), `H` (hat), `C` (pants or shirt), or `R` (ring-type object). * <item ID>: the unqualified item ID. * <count>: the stack size of the produced item, if applicable for its type. Some types (e.g. big craftables or rings) aren't stackable so this field will be ignored. A bundle can only produce one item stack. For example, a bundle can provide 15 apples as a reward, but it can't provide two different items. |
| 2 | requirements | Format: `[<item ID> <count> <min quality>]+` The object-type items required to complete the bundle. You can list one to twelve items (more than twelve will crash the bundle menu). Arguments: * <item ID>: the unqualified item ID for the object-type item. * <count>: the number of the item required. * <min quality>: The minimum item quality (numeric form) that must be provided. Note that some items can't have iridium quality by default. |
| 3 | color | The index of the bundle color in the `LooseSprites/JunimoNote` spritesheet, starting from the top-left corner of the color icons area. This can be one of these values: |
| index | icon (incomplete) | icon (complete) |
| `0` | green | green |
| `1` | purple | purple |
| `2` | orange | organge |
| `3` | yellow | pink |
| `4` | red | flower; (yellow center with red & green leaves) |
| `5` | blue | blue |
| `6` | teal | teal |

|-
| 4
| item count
| The number of distinct items needed to complete the bundle, or blank to match the number of required items. This can be a value between 1 and 12 (higher numbers will break the UI layout).

For example, if you have six entries in the 'requirements' field and set this to 4, the player needs to fill the bundle with any four of the six required items.
|-
| 5
| override texture and sprite index
| If not blank, the texture and sprite index to use as the icon instead of automatically from `LooseSprites/JunimoNote`, specified in the form of `<texture asset name>:<sprite index>`.
|-
| 6
| display name
| The translated display name for this bundle (this is also shown in English).
|}

## Reward limitation
Bundles can only reward one big object (auto-grabber, crystalarium, lightning rod, etc.) at a time. If you attempt to make it reward multiple big objects by changing the quantity, it will only reward one and ignore the others. 

Bundles can also only reward one type of object at a time. Thus, it is possible to make a bundle reward 5 iridium sprinklers, but it is not possible to make a bundle reward 2 iridium sprinklers and 3 quality sprinklers.

Either way, it is not possible to reward multiple objects by editing bundles.xnb. Doing so would require modding the game code itself, and that particular mod does not exist yet.

es:Modding:Datos de lotes
ru:Модификации:Пакеты
zh:模组:收集包数据
