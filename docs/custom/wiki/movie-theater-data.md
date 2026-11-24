> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Movie_theater_data)

# Modding:Movie theater data (`Movie_theater_data`)

← Index

This page explains how the files associated with the Movie Theater, what each of them do, and how they are formatted. This is an advanced guide for mod developers.

## Movie data
`Data\Movies.xnb` contains the data for all movies, including their names, descriptions, genre tags, scenes, and dialogue and narration spoken within the film. For reference, below is the raw data of "The Brave Little Sapling" as of 1.6.15:

**data (Sample)**:

```json
{
  "Id": "spring_movie_0",
  "Seasons": [
    "Spring"
  ],
  "YearModulus": 2,
  "YearRemainder": 0,
  "Texture": null,
  "SheetIndex": 0,
  "Title": "[LocalizedText Strings\\Movies:BraveLittleSapling_Title]",
  "Description": "[LocalizedText Strings\\Movies:BraveLittleSapling_Description]",
  "Tags": [
    "family",
    "comedy"
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
The `Data/Movies` asset is a list of models, which have the following fields:

| key format | description |
| --- | --- |
| `Id` | A unique string ID which identifies this movie. This should only contain alphanumeric/underscore/dot characters. |
| `Seasons` | *(Optional)* The seasons when the movie should play, or omit for any season. For example: 
```json
"Seasons": [ "Spring", "Summer" ]
```
 |
| `YearModulus`; `YearRemainder` | *(Optional)* If set, plays the movie on alternating years based on the formula `theater_age % modulus == remainder`, where `theater_age` is the number of years since the movie theater was built. If omitted, the movie plays in any year. For example: 
```json
// play in the first year, and every second year thereafter
"YearModulus": 2,
"YearRemainder": 0
```
 
```json
// play in the second year, and every second year thereafter
"YearModulus": 2,
"YearRemainder": 1
```
 |
| `Texture` | *(Optional)* The asset name for the movie poster and screen images, or omit to use `LooseSprites\Movies`. This must be a spritesheet with one 490×128 pixel row per movie. A 13×19 area in the top-left corner of the row should contain the movie poster. With a 16-pixel offset from the left edge, there should be two rows of five 90×61 pixel movie screen images, with a six-pixel gap between each image. (The movie doesn't need to use all of the image slots.) |
| `SheetIndex` | Determines where the game looks for the movie's scene sprites within `LooseSprites\Movies.xnb`. This includes the movie poster and screen images for the movie. |
| `Title` | A tokenizable string for the title of the film. |
| `Description` | A tokenizable string for the description of the film, seen when interacting with its poster outside of the theater. |
| `Tags` | A list of genres that apply to the movie, used to calculate an NPC's reaction to a movie. Can be any arbitrary string; the ones currently used in the game are: `family`, `comedy`, `horror`, `art`, `action`, `sci-fi`, `classic`, `romance`, and `documentary`. |
| `CranePrizes` | *(Optional)* The items to add to the crane game prize pool on days when this movie is playing, if any. This consists of a list of models with these fields: |
| field | effect |
| *common fields* | See item spawn fields for the generic item fields supported for crane game items. If set to an item query which returns multiple items, one of them will be selected at random. |
| `Rarity` | *(Optional)* The prize pool to add the item to. The possible values are `1` (common), `2` (rare), and `3` (deluxe). Default `1`. |

|-
| `ClearDefaultCranePrizeGroups`
| *(Optional)* The prize pools whose default items to discard, so only those specified by `CranePrizes` are available. Default none.

For example:

```json
"ClearDefaultCranePrizeGroups": [ 2, 3 ]
```

|-
| `Scenes`
| A list of data blocks for the film's scenes. (See below.)
|}

#### Scenes
The `Scenes` is a list of blocks, each corresponding to a specific scene in the movie. The blocks are listed in chronological order, and the data of each one is as follows:

| key format | description |
| --- | --- |
| `Image` | Which image to show within the sequence of the movie's sprites in `LooseSprites\Movies.xnb`. |
| `Music` | The music track to play during the scene. Can be left blank. |
| `Sound` | The sound effect to play during the scene. Can be left blank. |
| `MessageDelay` | How long to hold the scene before displaying any text. |
| `Script` | Used for special visual/auditory effects. Written in the same format as event scripts. Supports tokenizable strings. |
| `Text` | The dialogue/narration of the scene. Supports tokenizable strings. |
| `Shake` | A boolean value which, if true, adds screen-shake. |
| `ResponsePoint` | If specified, an NPC's reaction data can reference the scene by this name in order to have a specific, timed reaction to it. |
| `ID` | An ID for the scene in the format of `<season><number>_<index>`. <season> and <number> are identical to those values in the ID of the movie itself. <index> is, counting from 0, what number scene this is in the movie. |

For example, this content pack adds a new 'Pufferchick II' movie which plays in winter every third year:

    "Changes": [
        {
            "Action": "EditData",
            "Target": "Data/Movies",
            "Entries": {
                "{{ModId}}_PufferchickII": {
                    "Id": "{{ModId}}_PufferchickII", // must specify ID again when creating a new entry

                    "Seasons": [ "winter" ],
                    "YearModulus": 3,
                    "YearRemainder": 0,

                    "Title": "Pufferchick II", // display text should usually use {{i18n}} for translations in actual usage
                    "Description": "A sequel to the breakthrough adventure anime Pufferchick. The world is saved; what happens next?",
                    "Tags": [ "family", "adventure" ],

                    "Texture": "{{InternalAssetKey: assets/movie.png}}", // an image in your content pack
                    "SheetIndex": 0,

                    "Scenes": [ ... ]
                }
            }
        }
    ]

## Movie reactions
`Data\MoviesReactions.xnb` contains every NPC's taste in movies and their dialogue in response to them. For reference, below is the raw data for Penny's reactions as of 1.6.15:

**data (Sample)**:

```json
[
  {
    "NPCName": "Penny",
    "Reactions": [
      {
        "Tag": "*",
        "Response": "love",
        "Whitelist": [
          "Pam"
        ],
        "SpecialResponses": {
          "BeforeMovie": {
            "ResponsePoint": null,
            "Script": null,
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
Each block of data within the file begins with an `NPCName` key, which is the name of the NPC to whom the rest of the data in the block will apply. This is followed by a list with the key `Reactions`. Each block within this list is the data for an NPC's response to a specific type of movie.

The data within each block is formatted as follows:

| key format | description |
| --- | --- |
| `Tag` | Which category of movie this response is for. In decreasing order of specificity, this can be: * `*`, in which case the response will always be applicable, unless there are other restrictions applied by `Whitelist` * a genre of movie, as listed in the `Tags` field of a film's data * the NPC's reaction to a movie, one of `love`, `like`, or `dislike`. * a specific movie's identifier, as listed at the header of its block. (i.e. `spring_movie_0` for "The Brave Little Sapling") |
| `Response` | One of `love`, `like`, `dislike`, or `reject`. Used to calculate Friendship points gained with the NPC. A loved movie earns 200 Friendship points with the NPC, a liked movie earns 100 Friendship points, and a disliked movie neither earns nor costs friendship points with the NPC. Rejected movies will turn down the ticket and say their line for RejectMovieTicket_DontWantToSeeThatMovie. |
| `Whitelist` | A list of NPC names. If not empty, the response will only be applicable if, in addition to the movie matching `Tag`, one of the listed NPCs is also present within the theater. Currently only used for Penny, who has a special reaction to watching a movie if her mother Pam is present. |
| `SpecialResponses` | Contains the dialogue spoken by an NPC during the film in a series of blocks. (See below.) Can be left null. |
| `ID` | A unique identifier in the form of `reaction_<index>`, where <index> is, counting from 0, what number block this is within the `Reactions` list. |

For unknown reasons, in the vanilla game code, when the `Tag` field is `love` or `dislike`, the `Response`field will always be `like`.

#### Special responses
Each `SpecialResponses` block contains 3 blocks, beginning with `BeforeMovie`, `DuringMovie`, and `AfterMovie`, in that order. Any of these blocks may be left null. By default, the dialogue within these blocks triggers when talking to the NPC in the movie theater lobby, at a random point during the movie, and after the movie is finished, respectively. The data within each block is as follows:

| key format | description |
| --- | --- |
| `ResponsePoint` | If not null, this dialogue will trigger at the given response point instead of its regular timing. See the scenes field documentation for an explanation of response points. |
| `Script` | A script which runs prior to `Text` being displayed. Written in the same format as event scripts. |
| `Text` | The dialogue spoken by the NPC. Supports tokenizable strings. |

If multiple response blocks are applicable to the movie, the game uses the first non-null `Response`, `BeforeMovie`, `DuringMovie`, and `AfterMovie` fields it can find. Said data does not necessarily have to come from the same response block. If the game cannot find applicable `DuringMovie` dialogue, it will use default text from `Strings\Characters.xnb`. If the game cannot find applicable `BeforeMovie` or `AfterMovie` dialogue, the NPC will show as being able to be spoken to, but interacting with them will have no effect.

### Movie Identifiers
| key | movie title |
| --- | --- |
| spring_movie_0 | The Brave Little Sapling |
| summer_movie_0 | Journey Of The Prairie King: The Motion Picture |
| fall_movie_0 | Mysterium |
| winter_movie_0 | The Miracle At Coldstar Ranch |
| spring_movie_1 | Natural Wonders: Exploring Our Vibrant World |
| summer_movie_1 | Wumbus |
| fall_movie_1 | It Howls In The Rain |
| winter_movie_1 | The Zuzu City Express |

## Concession data
`Data\Concessions.xnb` contains the data for the food purchased at the concessions stand, stored as a list of blocks. For reference, below is the raw data of the file as of 1.6.15:

**data (Sample)**:

```json
[
  {
    "Id": "0",
    "Name": "Cotton Candy",
    "DisplayName": "[LocalizedText Strings\\MovieConcessions:CottonCandy_Name]",
    "Description": "[LocalizedText Strings\\MovieConcessions:CottonCandy_Description]",
    "Price": 50,
    "Texture": "LooseSprites\\Concessions",
    "SpriteIndex": 0,
    "ItemTags": [
      "Sweet",
      "Candy"
    ]
  },
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
Each block within the list corresponds to a single concession. The data for each block is as follows:

| key format | description |
| --- | --- |
| `ID` | A unique string ID to identify this entry. |
| `Name` | The internal name of the concession. |
| `DisplayName` | A tokenizable string for the name of the concession, which is displayed to the player in-game. |
| `Description` | A tokenizable string for the description of the concession. |
| `Price` | The price of the concession in . |
| `ItemTags` | A list of tags which describe the concession, used to determine an NPC's reaction to it. Can be any arbitrary string; the ones currently used in the game are: `Sweet`, `Candy`, `Drink`, `Hot`, `Healthy`, `Cold`, `Joja`, `Sour`, `Fatty`, `Salty`, `Sandwich`, `Burger`, and `Gourmet`. |
| `Texture` | The asset name for the texture containing the concession's sprite. |
| `SpriteIndex` | The index within the `Texture` for the concession sprite, where 0 is the top-left sprite. |

For example, this content pack adds a new 'Pufferchick Pop' concession with a custom image:

    "Changes": [
        {
            "Action": "EditData",
            "Target": "Data/Concessions",
            "Entries": {
                "{{ModId}}_PufferchickPop": {
                    "Id": "{{ModId}}_PufferchickPop",   // must specify ID again when creating a new entry
                    "Name": "{{ModId}}_PufferchickPop", // best practice to match the ID, since it's sometimes used as an alternate ID
                    "DisplayName": "Pufferchick Pop",
                    "Description": "A cute cake pop shaped like a pufferchick.",
                    "Price": 25,
                    "Texture": "{{InternalAssetKey: assets/pufferchick-pop.png}}" // an image in your content pack
                    "SpriteIndex": 0
                }
            }
        }
    ]

## Concession tastes
`Data\ConcessionTastes.xnb` contains the data that determines an NPC's opinion of a concession. For reference, below is the raw data of the file:

**data (Sample)**:

```json
[
  {
    "Name": "Penny",
    "LovedTags": [
      "Cotton Candy"
    ],
    "LikedTags": [
      "Sweet"
    ],
    "DislikedTags": [
      "Sour"
    ]
  },
  {
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

### Format
Each block within the list has the following data:

| key format | description |
| --- | --- |
| `Name` | Who this block should apply to. Can be the name of an NPC or `*`, in which case the tastes will apply to every NPC. |
| `LovedTags`</br>`LikedTags`</br>`DislikedTags` | A list of concession tags that are loved, liked, or disliked. This can be an `ItemTag`, or the internal name of a specific concession. To determine an NPC's opinion of a concession, the game totals how many tags from each group apply, with `LovedTags` and `LikedTags` contributing a positive score and `DislikedTags` contributing a negative. Purchasing a loved snack earns 50 Friendship points with the NPC, a liked snack earns 25 Friendship points, and a disliked snack neither earns nor costs friendship points with the NPC. |

### Concession tags
Black Licorice is the only item that doesn't have specific concession tags attached to it. Stardrop Sorbet is a universally loved item, whereas Black Licorice, Joja Cola, and JojaCorn are universally disliked items.

| tag | applicable concessions |
| --- | --- |
| `Burger` | Salmon Burger |
| `Candy` | Cotton Candy; Jawbreaker; Rock Candy; Sour Slimes |
| `Cold` | Ice Cream Sandwich; Joja Cola |
| `Drink` | Jasmine Tea; Joja Cola; Kale Smoothie |
| `Fatty` | Fries; Nachos; Personal Pizza |
| `Gourmet` | Cappuccino Mousse Cake; Panzanella Salad; Stardrop Sorbet; Truffle Popcorn |
| `Healthy` | Apple Slices; Hummus Snack pack; Jasmine Tea; Kale Smoothie; Panzanella Salad |
| `Hot` | Chocolate Popcorn; Fries; Jasmine Tea; Nachos; Personal Pizza; Popcorn |
| `Joja` | Joja Cola; JojaCorn |
| `Salty` | Fries; Nachos; Popcorn; Salted Peanuts; Truffle Popcorn |
| `Sandwich` | Ice Cream Sandwich; Salmon Burger |
| `Sour` | Sour Slimes |
| `Sweet` | Apple Slices; Cappuccino Mousse Cake; Chocolate Popcorn; Cotton Candy; Ice Cream Sandwich; Jawbreaker; Rock Candy; Star Cookie; Stardrop Sorbet |

## Movie selection
Since the `Data/Movies` asset is a list, its order determines the order movies are shown within a season. If multiple movies can play in the same season, each movie will be shown for an equal block of days within the month (e.g. four movies will each play for seven consecutive days). If there are more movies than days, 28 movies will be chosen at random to play that month.

ru:Модификации:Кинотеатр
zh:模组:电影院数据
