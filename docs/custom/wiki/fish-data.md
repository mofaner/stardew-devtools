> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Fish_data)

# Modding:Fish data (`Fish_data`)

← Modding:Index

This page explains how the game stores and parses fish data. This is an advanced guide for mod developers.

## Data format
### Fish
You can edit fish data by editing the `Data/Fish` data asset. 

This consists of a string → string lookup, where...
- The key is a unique string ID for the fish.
- The value is a slash-deliminated string with the fields listed below: 

The value consists of two different formats: one for crab pot fish and one for fish that can be caught with the fishing rod.

If the value of the field at index 1 is `trap`, the fish can only be caught using a crab pot, and it uses this format:

| index | syntax | example | content |
| --- | --- | --- | --- |
| 0 | <name> | `Lobster` | The fish name. |
| 1 | <type> | `trap` | Must be `trap` to indicate a crab pot catch. |
| 2 | <chance> | `.05` | The percentage chance that this fish will be caught as a value between 0 and 1. Only applies if a fish listed earlier in the file isn't selected first. |
| 3 |  | `688 .45 689 .35 690 .35` | Unused. |
| 4 | <location> | `ocean` | The type of water body where the fish can be caught. The vanilla values are `freshwater` or `ocean`; mods may add their own region. |
| 5 | <min size> | `2` | The minimum and maximum size of the caught fish in inches for fishing stats. In non-English languages, this is converted to centimetres by multiplying by 2.54. |
| 6 | <max size> | `20` |  |

Otherwise it's a fish that can be caught while fishing and uses this format: 

| index | syntax | example | content |
| --- | --- | --- | --- |
| 0 | <name> | `Pufferfish` | The fish name. |
| 1 | <chance to dart> | `80` | How often the fish darts in the fishing minigame; between 15 (carp) and 100 (glacierfish). |
| 2 | <darting randomness> | `floater` | How the bobber behaves during the fishing minigame; one of `mixed`, `smooth`, `floater`, `sinker`, or `dart`. |
| 3 | <min size> | `1` | The minimum and maximum size of the caught fish in inches for fishing stats. In non-English languages, this is converted to centimetres by multiplying by 2.54. |
| 4 | <max size> | `36` |  |
| 5 | [<min time> <max time>]+ | `1200 1600` | The time of day when the fish can spawn. The min time is inclusive, max time is exclusive. May specify multiple ranges. |
| 6 | [<season>]+ | `summer` | Unused; seasons are taken from `Data/Locations` instead. |
| 7 | <weather> | `sunny` | The weather when the fish can spawn; one of `sunny`, `rainy`, or  `both`. |
| 8 | <locations> | `690 .4 685 .1` | Unused; locations are taken from `Data/Locations` instead. |
| 9 | <max depth> | `4` | The minimum number of tiles from land that the player must cast their rod such that the chance for catching this fish is not decreased. If the player casts closer to land than this minimum, they will accrue a penalty of `(depth multiplier * chance)` percentage points for each tile below this minimum.<ref name="checkreqs">See `GameLocation::CheckGenericFishRequirements` in the game code</ref> |
| 10 | <chance> | `.3` | The base chance for catching this fish once it has already been determined as one of the possible fish to catch at a tile. After applying buffs from fishing level and the Training Rod, this value is clamped to `0.9` before applying other buffs such as from the Curiosity Lure and Targeted Bait.<ref name="checkreqs" /> |
| 11 | <depth multiplier> | `.5` | A multiplier used when penalizing the player for not casting their rod at least `max depth` tiles from land.<ref name="checkreqs" /> |
| 12 | <fishing level> | `0` | The minimum fishing level needed for this fish to appear. |
| 13 | <first-catch tutorial eligible> | `true` | Indicates whether the fish can be selected for the first-catch tutorial. |

### Aquarium fish
Fish in fish tanks can be edited via the `Data/AquariumFish` asset.

This consists of a string → string lookup, where...
- The key is a unique string ID for the aquarium fish, which must match the non-qualified item ID of the item being deposited into the tank.
- The value is a slash-deliminated string with the fields listed below: 

| index | field | purpose |
| --- | --- | --- |
| 0 | sprite index | The index of the sprite in the `LooseSprites/AquariumFish` spritesheet, starting at 0 for the top-left sprite. The fish sprites are 24x24px. |
| 1 | type | The fish type, which influences their behavior. Possible values: |
| value | notes |  |
| `eel` | These fish have a clamped minimum velocity greater than 0, regardless of their behavior in `Data/Fish`. |  |
| `cephalopod`</samp> | These fish have no minimum velocity. They favor moving downward in the tank rather than upward. |  |
| `crawl` | These fish always stay at the bottom of the tank. They move horizontally along the tank bottom. These fish have no minimum velocity. |  |
| `ground` | These fish always stay near the bottom of the tank. They are closer to the glass than all other fish types. They move horizontally and vertically, but favor moving downward towards the tank bottom. These fish have no minimum velocity. |  |
| `fish` |  |  |
| `front_crawl` | Used for crawling fish whose sprites extend past the floor. They are closer to the glass than all other fish (besides `ground` fish). Otherwise, it has identical behavior to `crawl`. |  |
| `static` | These fish always stay at the bottom of the tank. They do not move within the tank. |  |
| `float` |  |  |

|-
| 2
3–5
| idle animation
dart animation
| The animation used by the fish when it's idling (2) or darting (3–5). This is specified as a list of space-delimited frames, where each frame is the sprite index in the `LooseSprites/AquariumFish` spritesheet to display. Each frame is displayed for 125ms. For example, Stingrays have their idle animation set to `70 70 70 71 71 72`.

The dart animation is split into three fields: start (3), hold (4), and end (5).
|-
| 6
| texture
| The asset name for the texture containing the fish's sprite. Defaults to `LooseSprites/AquariumFish`.
|-
| 7
| hat position
| The pixel position of the hat on the sprite, specified as an object with `X` and `Y` values. Custom fish in aquariums can wear hats, just like vanilla sea urchins.
|}

## Spawning Fish
Fish spawn data is stored in `Data/Locations`. Fish can be added to a location by editing the `Fish` field in the location's data entry. Distinct fishing areas can be defined within the same location (for example, a pond and a river within the same location that have different fish spawn chances) by editing the `FishAreas` field. These fishing areas can then be referenced by ID in the `Fish` field. See Modding:Location data for more information.

## References
<references />

de:Modding:Fisch Daten
es:Modding:Datos de pescado
pt:Modificações:Dados de Peixes
ru:Модификации:Рыболовство
zh:模组:鱼类数据
