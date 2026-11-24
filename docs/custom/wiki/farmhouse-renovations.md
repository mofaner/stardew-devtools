> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Farmhouse_Renovations)

# Modding:Farmhouse Renovations (`Farmhouse_Renovations`)

← Index

This page explains how to create custom farmhouse renovations. This is an advanced guide for mod developers.

You can add custom farmhouse renovations that will be applied automatically when the specified mail flag is set. This is based on a new map property in the farmhouse map:

| valid in | map property | usage |
| --- | --- | --- |
| farmhouse | `AdditionalRenovations <renovation>+` | A comma-separated list of renovations to apply, each in the form `<map patch ID> <required mail flag> <map asset if active> <map asset if inactive> [area rectangle]`. Fields: |
| field | effect |  |
| `map patch ID` | A unique ID for the patch being applied. This is usually similar to the renovation name, but can be any unique value. If a renovation applies multiple map patches, each one must have unique ID. |  |
| `required mail flag` | The mail flag that is checked on the house's owner  to decide whether the patch should be applied. |  |
| `map asset if active` | The asset name for the map applied when the mail flag is set, relative to the `Maps` folder. |  |
| `map asset if inactive` | The asset name for the map applied when the mail flag is not set, relative to the `Maps` folder. |  |
| `area rectangle` | *(Optional)* The tile area in the map to patch, specified as `<X> <Y> <width> <height>`. If <X> isn't set, this defaults to the top-left corner of the map. If it is, you must specify all four values. |  |

|}

For example, this Content Patcher pack would add a renovation to the fully-upgraded farmhouse based on a custom `ExampleAuthor_PineapplesEverywhere_HasRenovation` flag. Note that `TextOperations` is used to avoid overwriting any renovations added by another mod, and `{{ModId}}` is automatically replaced by Content Patcher to avoid conflicts with other mods.

    "Changes": [
        // add renovation
        {
            "Action": "EditMap",
            "Target": "Maps/FarmHouse2, Maps/FarmHouse2_marriage",
            "TextOperations": [
                {
                    "Operation": "Append",
                    "Target": [ "MapProperties", "AdditionalRenovations" ],
                    "Value": "{{ModId}}_KitchenUpgrade {{ModId}}_HasRenovation {{ModId}}_Kitchen {{ModId}}_Empty 0 19 3 3",
                    "Delimiter": "," // if there are already renovations, add a comma before this one
                }
            ]
        },

        // load renovation map patch
        {
            "Action": "Load",
            "Target": "Maps/{{ModId}}_Kitchen",
            "FromFile": "assets/kitchen-upgrade.tmx"
        },

        // load empty map (applied if renovation is inactive)
        {
            "Action": "Load",
            "Target": "Maps/{{ModId}}_Empty",
            "FromFile": "assets/kitchen-upgrade-empty.tmx"
        }
    ]

ru:Модификации:Ремонт фермерского дома
zh:模组:农舍升级
