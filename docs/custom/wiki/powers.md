> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Powers)

# Modding:Powers (`Powers`)

← Index

This page explains how the game stores and parses data for Special Items & Powers. This is an advanced guide for mod developers.

## Overview
Powers are unique "items" that can be obtained only once per player, and permanently unlock new abilities. Information for powers is stored in `Data/Powers`. This does **not**, by itself, add any abilities to the player. It contains information used to display powers in the menu, and unlock them when needed. 

## Data format
You can add/edit Special Items & Powers by editing the `Data/Powers` data asset.

This consists of a string → model lookup, where...
- The key is a unique string ID for the entry.
- The value is a model with the fields listed below.

| Field | Description |
| --- | --- |
| `DisplayName` | A tokenizable string used to display the translated name of the item, once unlocked. |
| `Description` *(Optional)* | A tokenizable string used to display the translated description of the item, once unlocked. |
| `TexturePath` | The asset name for the power's icon texture. |
| `TexturePosition` | The pixel coordinates of the upper left corner of the sprite in the texture, provided as an object with `X` and `Y` fields. Sprites are always 16 by 16. |
| `UnlockedCondition` | A game state query used to determine whether or not the item has been unlocked. |
| `CustomFields` *(Optional)* | The custom fields for this entry. |

For example, this Content Patcher pack adds a custom pufferfish power, which is unlocked after the player catches a pufferfish:

    "Changes": [
        // load the icon for the powers tab
        {
            "Action": "Load",
            "Target": "Mods/{{ModId}}/PufferIcon",
            "FromFile": "assets/PufferIcon.png"
        },

        // add the power
        {
            "Action": "EditData",
            "Target": "Data/Powers",
            "Entries": {
                "{{ModId}}_Pufferpower": {
                    "DisplayName": "Puffer's Blessing",
                    "Description": "You feel as if you have become one with the pufferfish...",
                    "TexturePath": "Mods/{{ModId}}/PufferIcon",
                    "TexturePosition": {
                        "X": 0,  // top-left corner of the canvas, assuming that this is the only sprite in the icon image
                        "Y": 0
                    },
                    "UnlockedCondition": "PLAYER_HAS_CAUGHT_FISH Current 128",  // the player has caught a pufferfish
                }
            }
        }
    ]

ru:Модификации:Способности
zh:模组:能力
