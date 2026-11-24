> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Monster_data)

# Modding:Monster data (`Monster_data`)

←Index

This page explains how the game stores and parses monster data. This is an advanced guide for mod developers.

## Raw data
Monster data is stored in `Content\Data\Monsters.xnb`, which can be unpacked for editing. Here's the raw data as of 1.5.1 for reference:

**Data (Sample)**:

```json
{
  "Green Slime": "24/5/0/0/false/1000/766 .75 766 .05 153 .1 66 .015 92 .15 96 .005 99 .001/1/.01/4/2/.00/true/3/Green Slime",
  "Dust Spirit": "40/6/0/0/false/1000/382 .5 433 .01 336 .001 84 .02 414 .02 97 .005 99 .001/2/.00/4/3/.00/true/2/Dust Spirit",
  "Bat": "24/6/0/0/true/1000/767 .9 767 .4 108 .001 287 .02 96 .005 99 .001/1/.01/4/3/.00/true/3/Bat",
  "Frost Bat": "36/7/0/0/true/1000/767 .9 767 .55 108 .001 287 .02 97 .005 99 .001/1/.01/4/3/.00/true/7/Frost Bat",
  "Lava Bat": "80/15/0/0/true/1000/767 .9 767 .7 108 .001 287 .02 98 .005 99 .001/1/.01/4/3/.00/true/15/Lava Bat",
  "Iridium Bat": "300/30/0/0/true/1000/386 .9 386 .5 386 .25 386 .1 288 .05 768 .5 773 .05 349 .05 787 .05 337 .008/1/.01/4/3/.00/true/22/Iridium Bat",
  "Stone Golem": "45/5/0/0/false/1000/390 .9 80 .1 382 .1 380 .1 96 .005 99 .001/5/.01/3/2/.00/true/5/Stone Golem",
  "Wilderness Golem": "30/5/0/0/false/1000/771 .9 771 .5 770 .5 382 .1 86 .005 72 .001/1/.01/3/2/.00/true/5/Wilderness Golem",
  "Grub": "20/4/0/0/false/1000/684 .6 273 .05 273 .05 157 .02 114 .005 96 .005 99 .001/0/.005/3/1/.00/true/2/Grub",
  "Fly": "22/6/0/0/true/1000/684 .9 157 .02 114 .005 96 .005 99 .001/1/.005/13/2/.0/true/10/Fly",
  "Frost Jelly": "106/7/0/0/false/1000/766 .75 412 .08 70 .02 98 .015 92 .5 97 .005 99 .001/0/.01/4/2/.0/true/6/Frost Jelly",
  "Sludge": "205/16/0/0/false/1000/766 .8 157 .1 -4 .1 72 .01 92 .5 98 .005 99 .001/0/.01/4/2/.0/true/10/Sludge",
  "Shadow Guy": "125/20/0/0/false/0/769 .75 769 .1 329 .02 337 .002 336 .01 335 .02 334 .04 203 .04 293 .03 108 .003 -4 .1 98 .005 99 .001/2/.01/-1/2/.0/true/15/Shadow Guy",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Format
| Index | Field | Example Value |
| --- | --- | --- |
| 0 | health (hit points) | *24* |
| 1 | damage (attack) | *5* |
| 2 | minimum coins to drop *(unimplemented)* | *0* |
| 3 | maximum coins to drop *(unimplemented)* | *0* |
| 4 | whether a "glider" (flying) | *false* |
| 5 | duration of random movements | *1000* |
| 6 | objects to drop | *766 .75 766 .05 153 .1 66 .015 92 .15 96 .005 99 .001* |
| 7 | resilience (defense) | *1* |
| 8 | jitteriness | *.01* |
| 9 | distance threshold for moving towards player | *4* |
| 10 | speed | *2* |
| 11 | chance of attacks missing | *.00* |
| 12 | whether a "mine monster" | *true* |
| 13 | experience gained | *3* |
| 14 | display name | *Green Slime* |

Index 4 designates "gliders": monsters that fly above the ground. Although Ghosts are not listed as gliders in the data, they are overridden to be gliders in the game code.

Index 6 contains a list of object ID numbers, each followed by the probability of that object being chosen. Multiple objects can be chosen from the list. "Extra drop items" in the game code supplement this list for several monster types.

Index 12 designates "mine monsters": monsters whose stats and drops are buffed for players who have reached the bottom of the mines. These include the Wilderness Golem and other monsters that do not actually occur in the mines. This field is *false* only for non-monsters that use the monster data file, such as the fireball projectile launched by the Squid Kid.

Note that many of the values stored in the monster data are altered by the game code under specific circumstances. These include being in different parts of the mines, having reached the bottom of the mines, and having a higher combat skill level.

## Monster-specific notes
### Slime sprites

Slime sprites are entirely modular, with the associated parts spread out across the sprite sheet. Every slime uses the same grey sprite, coloring it with a tint mask based on the type of slime, along with a random transparency value. Row 1 is used for the idle state and normal movement. Rows 2 through 4, while apparently identical, appear unused in most cases (row 2 appears sporadically at the end of certain attacks, but further testing is needed for exact conditions). Row 5 is used for the attack animation.

Area 6 on the sprite sheet contains assets for the "mating ritual" slimes can go through, as well as their death particles. The mating particles (going clockwise) are used for seeking a mate, rejecting a mate, and receiving a rejection.

Area 7 contains the assets for the eyes. Slime eyes are placed dynamically on their sprite based on their current action. When the slime is facing down, the eyes are placed directly on top of the sprite; when facing left, only the right eye is rendered, and moved slightly left; when facing right, only the left eye is rendered and moved slightly right; when facing up, the eyes are rendered directly behind the slime. The eyes, going clockwise, are used for their idle state, during attacks, during the "evil" state (each slime has a chance to turn "evil" when first hit, and gains a stat boost), and taking damage.

Area 8 is the antenna attached to "male" slimes. It appears to animate independently of the slime itself, rocking back and forth somewhat at random. As with the main sprite, this uses a tint mask to take on the color of the slime variant.

Area 9 is the antenna attached to "special" slimes, and animates in the same way as the "male" antenna. However, this one doesn't use the tint mask, instead using whatever raw color is on the sprite sheet.

## Monster IDs
Each monster type has a unique ID (stored in the `Name` field) which can be used to spawn it (e.g. via console commands), and is used to track kills for the Adventurer's Guild monster eradication goals. These are the IDs for vanilla monsters:

| monster | ID |
| --- | --- |
| Bat | `Bat` |
| Big Slime | `BigSlime` |
| Blue Squid | `Blue Squid` |
| Bug | `Bug` |
| Cave Fly | `Fly` |
| Duggy | `Duggy` |
| Dust Sprite | `Dust Spirit` |
| Dwarvish Sentry | `Dwarvish Sentry` |
| Ghost | `Ghost` |
| Grub | `Grub` |
| Lava Crab | `Lava Crab` |
| Lava Lurk | `Lava Lurk` |
| Metal Head | `Metal Head` |
| Mummy | `Mummy` |
| Pepper Rex | `Pepper Rex` |
| Rock Crab | `Rock Crab` |
| Serpent | `Serpent` |
| Shadow Brute | `Shadow Brute` |
| Shadow Shaman | `Shadow Shaman` |
| Shadow Sniper | `Shadow Sniper` |
| Slime | `Green Slime` |
| Spider | `Spider` |
| Spiker | `Spiker` |
| Squid Kid | `Squid Kid` |
| Stone Golem | `Stone Golem` |
| Wilderness Golem | `Wilderness Golem` |

Note that many other monsters are variants of the above (*e.g.* Haunted Skull is a variant of Bat), and have the same ID.

ru:Модификации:Монстры
zh:模组:怪物数据
