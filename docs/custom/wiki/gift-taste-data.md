> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Gift_taste_data)

# Modding:Gift taste data (`Gift_taste_data`)

← Index

This page explains how the game calculates NPC gift tastes. This is an advanced guide for mod developers.

## Data
### Raw data
NPC gift tastes are stored in `Content\Data\NPCGiftTastes.xnb`, which can be unpacked to edit. Here's the raw data as of 1.6.15 for reference:

**Data (Sample)**:

```json
{
  "Universal_Love": "74 446 797 373 279",
  "Universal_Like": "-2 -7 -26 -75 -80 72 395 613 634 635 636 637 638 724 459 873 394 166",
  "Universal_Neutral": "194 216 262 304 815 book_item",
  "Universal_Dislike": "-4 -8 -12 -15 -16 -19 -22 -24 -25 -28 -74 78 169 246 247 305 309 310 311 403 419 423 535 536 537 725 726 749 271 Book_PriceCatalogue category_trinket",
  "Universal_Hate": "0 -20 -21 92 110 111 112 142 152 153 157 178 105 168 170 171 172 374 376 378 380 397 420 684 721 766 767 772 203 308 265 909 910",
  "Robin": "This is for me? Wow, I absolutely love it!!/224 426 636 Book_Woodcutting/Thanks! This is really nice!/-6 -79 424 709 SkillBook_2/Um... why?/16 330/What the...? This is terrible!//Thank you. This might come in handy.//",
  "Demetrius": "You're giving this to me? This is amazing!/207 232 233 400/Thank you! This is a very interesting specimen./-5 -79 422 107/...What is this?/80 330/This is disgusting.//That was very thoughtful of you./-4/",
  "Maru": "Is that...? Oh wow, @! This is spectacular!/72 197 190 215 222 243 336 337 400 787 910 122/This is a super gift! Thank you!/-260 62 64 66 68 70 334 335 725 726 909 edible_mushroom/Oh... That's for me? I'll just put it over here.../-4 330 414 410 404 724/Yuck! You thought I would like this?/340 342 430 416/Thanks.//",
  "Sebastian": "I really love this. How did you know?/84 227 236 575 305 FrogEgg/Thanks, I like this./267 614 276 Book_Void SkillBook_4/...?/-81 -80/...I hate this./-5 459 195 201 240 330 -26 873/...thanks./-4/",
  "Linus": "This is wonderful! You've really made my day special./88 90 234 242 280 Book_Trash/This is a great gift. Thank you!/-5 -6 -79 -81/Hmm... This doesn't really do much for me./-2 330 166/Why would you give this to me? Do you think I like junk just because I live in a tent? That's terrible.//A gift? How nice./774 -4 142/",
  "Pierre": "This is my all-time favorite! Thank you!/202 Book_PriceCatalogue/That's very kind of you. I like this./-5 -6 -7 18 22 402 418 259 227/This isn't exactly my favorite.../-2 -81 330/Please, never bring this to me again./167 -4 199 270 229 248/A present? Thanks!//",
  "Caroline": "Oh, @... you're spoiling me! I love it!/213 614 593 907/For me? This is such a thoughtful gift! Thank you!/-7 18 815 402 418 16/Oh... I see. I'll, uh... make sure to put it to good use.../-81 330 300 306 307/Um... I guess I could use it for compost.../80 296/Oh, that's sweet. Thank you.//",
  "Abigail": "I seriously love this! You're the best, @!/66 128 220 226 276 611 904 Book_Void/Hey, how'd you know I was hungry? This looks delicious!/119 109 SkillBook_4 BasiliskPaw/What am I supposed to do with this?/-5 -75 -79 16 245 246/What were you thinking? This is awful!/330/You brought me a present? Thanks.//",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
The file contains two types of data:

<ul>
<li>*Universal tastes* apply to all villagers. Their key is `Universal_<Taste>` (Love, Like, Neutral, Dislike, or Hate), and their value is a space-delimited array of reference IDs (object unqualified item ID if ≥0, category ID if <0, or context tag). For example, consider this entry:

```json
  "Universal_Dislike": "-4 -8 -12 -15 -16 -19 -22 -24 -25 -28 -74 78 169 246 247 305 309 310 311 403 419 423 535 536 537 725 726 749 271 Book_PriceCatalogue category_trinket",
```

This data means villagers should have a universal dislike for category -4, object 78, anything with the context tag category_trinket etc.</li>

<li>*Personal tastes* apply to a specific villager. Their key is the villager's internal name (like `Abigail`), and their value alternates dialogue text with a list of reference IDs in this order: love, like, dislike, hate, and neutral. For example, consider Abigail's gift tastes:

```json
  "Abigail": "I seriously love this! You're the best, @!/66 128 220 226 276 611 904 Book_Void/Hey, how'd you know I was hungry? This looks delicious!/119 109 SkillBook_4 BasiliskPaw/What am I supposed to do with this?/-5 -75 -79 16 245 246/What were you thinking? This is awful!/330/You brought me a present? Thanks.//"
```

By splitting the string with `/` as the delimiter, we can extract this data:

| field index | taste | reaction dialogue | reference IDs |
| --- | --- | --- | --- |
| 0, 1 | love | I seriously love this! You're the best, @! | 66 128 220 226 276 611 904 Book_Void |
| 2, 3 | like | Hey, how'd you know I was hungry? This looks delicious! | 119 109 SkillBook_4 BasiliskPaw |
| 4, 5 | dislike | What am I supposed to do with this? | -5 -75 -79 16 245 246 |
| 6, 7 | hate | What were you thinking? This is awful! | 330 |
| 8, 9 | neutral | You brought me a present? Thanks. | *none* |

This data means she should personally love item 66 (Amethyst), dislike category -5 (eggs), etc. Even though she has no items in her neutral list, she will still have items that she's neutral to if they're in the `Universal_Neutral` list. See below for an explanation of how gift tastes are determined overall.</li>
</ul>
### Multilining
NPC Gift tastes can be multilined for readability. Line breaks must be before or after the `/` delimiter. For example:

```json
  "Abigail": "
I seriously love this! You're the best, @!/66 128 220 226 276 611 904 Book_Void/
Hey, how'd you know I was hungry? This looks delicious!/119 109 SkillBook_4 BasiliskPaw/
What am I supposed to do with this?/-5 -75 -79 16 245 246/
What were you thinking? This is awful!/330/
You brought me a present? Thanks.//
"
```

## How a gift taste is determined
The data format allows tastes to conflict in multiple ways:

- between an item ID and category ID;
- between a universal taste and personal taste;
- between conflicting values (*e.g.,* Jodi both loves and hates Daffodils (item ID 18) specifically);
- and any combination of the above (*e.g.,* between a universal item ID and personal category ID).

(TODO: Add information about where context tags fit within the algorithm.)

The game uses a rather complicated algorithm to determine how much an NPC likes an gift (see `NPC::getGiftTasteForThisItem`). Here's a cleaned up version of the algorithm in pseudocode:

```text
var TASTE = neutral
bool HAS_UNIVERSAL_ID = false
bool HAS_UNIVERSAL_NEUTRAL_ID = false

// part I: universal taste by category
if category is universally loved:
   TASTE = love
else if category is universally hated:
   TASTE = hate
else if category is universally liked:
   TASTE = like
else if category is universally disliked:
   TASTE = dislike

// part II: universal taste by item ID
if itemID is universally loved:
   TASTE = love
   HAS_UNIVERSAL_ID = true
else if itemID is universally hated:
   TASTE = hate
   HAS_UNIVERSAL_ID = true
else if itemID is universally liked:
   TASTE = like
   HAS_UNIVERSAL_ID = true
else if itemID is universally disliked:
   TASTE = dislike
   HAS_UNIVERSAL_ID = true
else if itemID is universally neutral:
   TASTE = neutral
   HAS_UNIVERSAL_ID = true
   HAS_UNIVERSAL_NEUTRAL_ID = true

// part III: override neutral if it's from universal category
if TASTE is neutral and not HAS_UNIVERSAL_NEUTRAL_ID:
   if item is edible but tastes bad (-300 > edibility < 0):
      TASTE = hate
   else if item has a price < 20g:
      TASTE = dislike

// part IV: sometimes override with personal tastes
if ((npc loves itemID OR (item has a category AND npc loves category)) AND (item has no category OR npc doesn't personally love category OR no universal taste for itemID)
   return love
if ((npc hates itemID OR (item has a category AND npc hates category)) AND (item has no category OR npc doesn't personally hate category OR no universal taste for itemID)
   return hate
if ((npc like itemID OR (item has a category AND npc likes category)) AND (item has no category OR npc doesn't personally like category OR no universal taste for itemID)
   return like
if ((npc dislikes itemID OR (item has a category AND npc dislikes category)) AND (item has no category OR npc doesn't personally dislike category OR no universal taste for itemID)
   return dislike
if ((npc neutrals itemID OR (item has a category AND npc neutrals category)) AND (item has no category OR npc doesn't personally neutral category OR no universal taste for itemID)
   return neutral

// part V: return taste if not overridden
return TASTE
```

es:Modding:Datos de gustos por regalos
pt:Modificações:Dados dos gostos do presente
ru:Модификации:Подарки
zh:模组:礼物喜好数据
