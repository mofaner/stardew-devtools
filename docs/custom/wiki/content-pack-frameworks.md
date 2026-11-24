> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Content_pack_frameworks)

# Modding:Content pack frameworks (`Content_pack_frameworks`)

← Modding:Index

**Content pack frameworks** are SMAPI mods designed to simplify the process of creating content packs for Stardew Valley. While they aim to minimize the need for programming experience, some familiarity with coding concepts can be helpful. Creating mods directly in C# for SMAPI may offer insight into the way the game works. Content packs are one of the two main ways to mod Stardew Valley, alongside creating C# mods for SMAPI directly.

Here's a [list of software](https://gist.github.com/ishanjalan/c8efb21afa21f74a052293176db107f7) for pixel art and JSON text editors we recommend. With software like VS Code and Sublime, you can set up the schema so it checks the formatting of your JSON file as you write your mod. In that way, it's like a built in [json validator site](https://smapi.io/json/). More details on setting up the schema [here](https://github.com/Pathoschild/SMAPI/blob/stable/docs/technical/web.md#using-a-schema-file-directly).

## Content Patcher
The recommended framework mod to use is 1915 Content Patcher, which allows you to change how Stardew Valley loads its contents, assets, sprites, maps, and even much of its logic from its `Content` folder.

The following is a non-exhaustive list of new things you can add to the game using Content Patcher:

- Items (including cooking, crops, trees, clothing, furniture, and new artisan machines that can process inputs)
- Buildings, maps, and locations
- Farm animals and pets
- NPCs, including dialogue, sprites, schedule and events
- Mail, shops, quests, and special orders

With Content Patcher you can also patch existing assets, which includes:

- Retexturing sprites, including (but not limited to) item sprites, NPC portraits and map appearance
- Changing NPC behavior, dialogue or even their name
- Modify maps to add new locations or warps

See Modding:Content Patcher for a getting started guide.

## Other popular frameworks
While you want to use Content Patcher in most cases, other frameworks can be useful if you need features that isn't supported by the game engine itself. These framework mods usually either extend Content Patcher by adding new assets to the game for content packs to edit (in which case they don't have their own content pack formats, but instead rely on Content Patcher's own), or define their own separate formats.

Here is a non-exhaustive list of framework mods used most often to create content packs. (The parenthesis next to each name is the conventional acronym used in the folder name for mods that do not use Content Patcher.).

### Core (1000+ content packs)
| mod | is CP extension | functionality |
| --- | --- | --- |
| 9246 Alternative Textures (AT) | no | Enables texture variation for placeable objects, buildings and entities. This includes trees, animals, pets, farm buildings, plants, and more. |
| 9969 Fashion Sense (FS) | no | Allows for bigger accessories, hairstyles, hats, shirts, sleeves and pants, as well as animation support and more. |
| 1720 Json Assets (JA) | no | Create specific item types, with support for functionality specific to each item type (*e.g.,* specifying crafting/cooking/recipes or gift tastes for new items). It currently supports creating objects, big craftables, crops, fruit trees, hats, weapons, shirts, pants, and boots. Content and images added by Json Assets can also be edited through Content Patcher. See spacechase0/JsonAssets#readme its GitHub readme for documentation. **Note**: 1.6 adds the ability to add new items natively via CP, which should be the preferred method. |

### Popular (400+)
| mod | is CP extension | functionality |
| --- | --- | --- |
| 3231 Farm Type Manager (FTM) | no | Add farm-like spawning logic to any map. That includes spawning stumps, debris, forage, meteors, monsters, ores, etc. |
| 4970 Producer Framework Mod (PFM) | no | Add/change the inputs/outputs/behaviour of any machine in the game. That includes vanilla machines (*e.g.,* keg), machines added by other mods, and adding machine logic to any big craftable. **Note**: 1.6 adds the ability to add new machine rules natively via CP, which should be the preferred method. PFM still provides several additional features not found in the base game (though most of them are covered by Extra Machine Config, an addon framework on top of CP). |
| 1348 SpaceCore | yes | A framework providing many features to content pack authors. Features include: * Crafting/cooking recipes that can take ingredients based on context tags * Extra item data, such as buffs on consumption * Extra trigger actions, map actions and game state queries * Extra yields on crop harvest * Animated textures * Custom spawnables and procedurally generated dungeons. |

### Common (100+)
| mod | is CP extension | functionality |
| --- | --- | --- |
| 2080 Better Artisan Good Icons (BAGI) | no | Add custom icons to artisanal items like honey or wine based on the item or flower used to make them. **Note**: The original Nexus version is not updated for 1.6. Use chsiao58/EvenBetterArtisanGoodIcons EBAGI if you want to make 1.6 content packs. |
| 23458 Furniture Framework (FF) | no | A Framework to make "Furniture Packs" with much more freedom that what a simple Content Pack allows: with it you can define seats, table slots, layers, shops, and much more. |
| 1536 Mail Framework Mod (MFM) | no | Add new mail with functionality like custom backgrounds, repeating mail, a wider variety of mail attachments, and support for multiple attachments. **Note**: 1.6 adds the ability to add new mail natively via CP, which should be the preferred method. MFM still provides several additional features not found in the base game (such as attaching items with higher quality to mail). |

### Specialized frameworks
These have fewer than 100 content packs, but provide useful functionality for specific cases.

#### Items
These mods add new functionalities to items, including machines, furniture, and tools.

| mod | is CP extension | functionality |
| --- | --- | --- |
| 24374 Advanced Melee Framework (AMF) | no | A framework for creating custom special attacks and enchantments for individual weapons and weapon types. |
| 11115 Better Crafting (BC) | yes/no | Create custom recipes more advanced than those supported by the base game including support for controlling availability and ingredients via game state queries, running trigger actions when a player crafts the recipe, and multiple possible ingredients or outputs using item queries and spawn fields. Create custom crafting stations with a limited selection of recipes, which can optionally be excluded from other crafting menus. These crafting stations can be opened via map tile actions or trigger actions. Allows assigning map tile actions to big craftables, which allows using them with the custom crafting stations feature. |
| 20628 Calcifer | yes | A core mod providing various useful features. Current feature include custom offset for items displayed on furniture, Custom actions when interacting with furniture, custom item category names, and NPC swimming as part of their schedules. |
| 24794 Craft Anything Framework | yes | Extend crafting recipes to allow crafting furniture, weapons, tools, and anything else. |
| 20619 Custom Bush | yes | Allows custom bushes to be added to the game which work like the tea sapling. |
| 6293 Custom Crafting Stations (CCS) | no | Add customized crafting stations which opens menus with a limited selection of recipes, without those recipes cluttering up the vanilla crafting menus. The stations can be [big craftables](https://stardewvalleywiki.com/Modding:Big_craftables_data) that are vanilla or added using Json Assets (JA). The stations can also be added via tiledata added to the map. **Note**: This mod is in maintenance mode for 1.6 - Using Better Crafting might be a better idea. |
| 7431 Expanded Storage | yes | Adds custom storages (chests, cabinets, etc.) that support capacity at vanilla levels and beyond. |
| 22256 Extra Machine Configs | yes | Adds extra functionalities to machine input/output rules, such as additional fuels/byproducts for specific recipes or copying color and flavor of input to the output. |
| 31678 Furniture Machine | yes | Allows furniture items to accept machine rules, notable input-output rules. Essentially allows machine dimensions bigger than 1x2. |
| 25157 Glow Buff | yes | Adds a way for authors to apply a real glow effect to food buffs. |
| 20357 Item Extensions | yes | Gives modders the ability to create custom resources, custom mixed seeds, and interactions between objects in-menu. (1.6) |
| 22975 Machine Terrain Framework | yes | Adds the ability to define big craftables that can be placed on trees and water, as well as expanding crops behavior with extra drops, plant on water, and customizable triggers. |
| 25287 Prismatic Valley Framework | yes | Framework to allow mod authors to specify a texture color override, including Prismatic and custom prismatic palettes, to their content using custom fields in Content Patcher. Currently supported: farm animals, objects, big craftables, and boots. |
| 25055 Secret Note Framework | yes | A framework mod for adding custom Secret Notes to the game. Includes the ability to specify complex eligibility conditions or specific locations, use a different item for the note object, specify custom content and formatting (including images and text, or both), and set any trigger actions to be run when a note is first read. |
| 29073 Trinket Tinker | yes | Framework for making custom trinkets with special abilities and trinket companions via content patcher. Also allows trinkets to be summoned without requiring the slot via trigger actions. |

#### Maps and locations
These mods add new functionalities to maps and locations, including extra tile actions and properties.

| mod | is CP extension | functionality |
| --- | --- | --- |
| 27359 Buildings Included | yes | Allows map makers to add pre-built buildings to their maps and conditionally add/upgrade buildings. |
| 15792 Bush Bloom Mod (BBM) | yes/no | Allows creating custom bush blooming schedules to support whatever seasons, days, locations, and items you may want. |
| 32864 Carpentry Catalogue | yes | Allows custom builder NPCs that can construct buildings like Robin, with either their own list of buildings or shared with Robin. Also allows custom blacksmith NPCs who can sell/upgrade tools after a delay. |
| 8000 Central Station | yes | Make new locations accessible through bus, train, or boat, so you don't need to patch paths/warps into existing maps. Has backwards compatibility for 6183 Train Station and 2905 Bus Locations, and adds a new location with randomized tourist/shops which can be populated further by other mods. |
| 1255 Custom Critters (CC) | no | Add custom critters to the world, like butterflies and bees. |
| 13804 Custom Farm Loader (CFL) | no | A simplified way of adding custom farms. Supports among other things spawning items/wild crops/resource clumps/beach drops and setting fishing rules within your farms context. **Note**: 1.6 adds the ability to add new farm types natively via CP, which should be the preferred method. CFL still provides several additional features not found in the base game. |
| 9296 Esca's Modding Plugins | yes | Adds miscellaneous new features for other mods to use. Current features include bed placement and kitchens/minifridges outside of a farmhouse, more Content Patcher Tokens, custom Special Order boards, destroyable bushes, multiple fishing zones, change water color. |
| 14493 Mapping Extensions and Extra Properties | yes | Allow map authors to spice up their maps with the new custom tile properties and extra features. Features including spawning fake NPCs, closeup interactions, opening a letter and more. |
| 28423 Misc Map Action Properties | yes | Add features for locations, buildings, and furniture. Includes tile data lights on map/building/furniture, various map wide visual effects, simple critter spawning, some fixes for custom farmhouse mods, extended building draw layer definition that also works on furniture, and more. |
| 20537 Personal Indoor Farm | yes | Adds new personal FarmHouse rooms. Modders can add room designs. |
| 12311 Solid Foundations (SF) | yes/no | A framework for adding custom buildings to the game. **Note**: 1.6 adds the ability to add new buildings natively via CP, which should be the preferred method. SF still supports several features not present in the base game, including custom light sources on building exteriors and auxiliary human door. |
| 17265 Unlockable Bundles | yes | Enables custom bundle creation and allow items to be registered as wallet currency. |
| 35477 Ladder Chance Framework | yes | A framework mod to change the chances of a ladder or shaft/hole in the Mines or Skull Cavern under any of numerous conditions using Content Patcher. |

#### Miscellaneous
These mods extend game functionality in other ways, or provide under-the-hood utilities for modders to use.

| mod | is CP extension | functionality |
| --- | --- | --- |
| 27100 Button's Extra Trigger Action Stuff | yes | Adds several extra triggers, actions, and game state queries for Content Patcher mod authors to utilize. See [the documentation](https://stardew.button.gay/docs/betas) for a full list and instructions. |
| 23135 Cloudy Skies | yes | A framework for adding custom weather types to the game, with distinct visual and auditory styles. Includes a variety of trigger actions to simulate weather effects, both beneficial and harmful. |
| 3853 Content Patcher Animations (CP-A) | yes/no | Animate any texture loaded through Content Patcher, including for textures that can't normally be animated (*e.g.,* item sprites). **Note**: It's recommended that you use SpaceCore's animation feature instead. |
| 28284 Cross-Mod Compatibility Tokens | yes | Adds some special, custom Content Patcher tokens that lets mods access the config values, translations, and dynamic tokens from other mods, including mods that are not made for Content Patcher specifically. |
| 8626 Custom Companions (CC) | no | Allows creating custom entities that can be spawned on maps or follow the player around. This includes animals in the world or wandering NPCs. |
| 25328 Extra Animal Configs | yes | Extends farm animal data with additional features, such as multiple harvest types, food other than hay, and multiple choices for hatching/births. |
| 29997 Livestock Bazaar | yes | Revamps the farm animal purchase menu, and adds ways for mod to create custom animal shops. |
| 22612 More Nightly Events | yes | Adds custom night-time events, using either configurable pre-made events, or by making your own with event scripting. |
| 30138 Nature In The Valley (NiTV) | yes/no | Framework for making catchable animals, creatures, critters and beasts with varying behaviors and temperaments. Creatures can either passively spawn according to conditions such as GSQs, or be spawned actively using trigger actions. |
| 22300 Special Power Utilities | yes | A categorized special items & powers tab. For mod authors working with powers and books, this mod provides the ability to add/specify tabs for their custom powers, change the "book read" message for custom books, add books that grant recipes, and extra Game State Queries, trigger actions and CP tokens. |
| 49525 Parchment | yes | Allows mod authors to create fully customizable in-game books. |

## See also
- Modding:Index for different ways to create mods, and more detailed documentation.
- [Tutorial: Making Framework Mods](https://stardewmodding.wiki.gg/wiki/Tutorial:_Making_Framework_Mods) for an external tutorial collecting various instructions and tips on making new framework mods.

pt:Modificações:Frameworks de pacotes de conteúdo
ru:Модификации:Контент-паки и фреймворки
tr:Modlama:İçerik Paketi Framework'leri
