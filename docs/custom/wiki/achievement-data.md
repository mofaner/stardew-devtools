> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Achievement_data)

# Modding:Achievement data (`Achievement_data`)

← Index

This page explains how the game stores and parses achievement data. This is an advanced guide for mod developers.

## Raw data
Achievement data is stored in `Content\Data\Achievements.xnb`, which can be unpacked for editing. Here's the raw data as of 1.6 for reference:

**Data (Sample)**:

```json
{
  "0": "Greenhorn (15k)^Earn 15,000g^true^-1^18",
  "1": "Cowpoke (50k)^Earn 50,000g^true^0^21",
  "2": "Homesteader (250k)^Earn 250,000g^true^1^20",
  "3": "Millionaire (1mil)^Earn 1,000,000g^true^2^1",
  "4": "Legend (10mil)^Earn 10,000,000g^false^-1^3",
  "5": "A Complete Collection^Complete the museum collection.^true^28^0",
  "6": "A New Friend^Reach a 5-heart friend level with someone.^true^-1^14",
  "7": "Best Friends^Reach a 10-heart friend level with someone.^true^6^31",
  "9": "The Beloved Farmer^Reach a 10-heart friend level with 8 people.^true^7^32",
  "11": "Cliques^Reach a 5-heart friend level with 4 people.^true^6^26",
  "12": "Networking^Reach a 5-heart friend level with 10 people.^true^11^25",
  "13": "Popular^Reach a 5-heart friend level with 20 people.^true^12^11",
  "15": "Cook^Cook 10 different recipes.^true^18^12",
  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)
```

## Format
| Index | Field | Example Value |
| --- | --- | --- |
| 0 | name | *Moving Up* |
| 1 | description | *Upgrade your house.* |
| 2 | display achievement on collections tab before it's earned | *true* |
| 3 | prerequisite achievement | *-1* |
| 4 | hat earned (index from `hats.xnb`) | *13* |

If index 2 is "true" and index 3 is "-1" then the achievement star will be displayed on the collections tab (greyed out if not yet achieved).  Otherwise, index 3 shows the number of the achievement that must be earned before the achievement will be displayed on the Collections tab.  (Example: you must earn achievement 28 "Treasure Trove" before achievement 5 "A Complete Collection" will be displayed.)

The only achievement with a value of "false" in index 2 is the secret achievement that doesn't display until it's earned.

For a list of hats, see hat data.

## See also
- Achievements

es:Modding:Datos de logros
ru:Модификации:Достижения
tr:Modlama:Başarım Verisi
