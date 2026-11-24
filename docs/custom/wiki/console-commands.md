> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Console_commands)

# Modding:Console commands (`Console_commands`)

←Index

type=warning text=**Console commands aren't meant for general use. Be very careful using them.**

They can have permanent side-effects on your save data, break your save, or have other unintended effects. If you don't understand the command, don't use it on a save you care about.**

SMAPI provides access to hundreds of console commands with a wide variety of effects, ranging from useful tools to cheats to specialized test commands. These are documented on this page.

{{TOC}}

## Format used on this page
To avoid repeating text, this page uses a few conventions to convey common information:

- Required parameters are listed in angle brackets, and optional parameters are listed in square brackets. For example, `speed` <I:value> [I:duration] means the command requires an integer value parameter and has an optional integer duration parameter. Details such as default values should be listed in the description.
- The command names are case-insensitive, but their parameters might be case-sensitive.
- If a command description says that a parameter uses "fuzzy" match, that means that it can match on a case-insensitive partial name. For example, `abi` would match Abigail if it's fuzzy.

## Console commands
### How to enter console commands
You can enter console commands directly into the SMAPI console window. You can type `help` to get a list of console commands (including commands added by other mods).

### Items & money
## Command: `list_items`
- **Syntax**: `list_items [S:search text]`
- **Description**: Shows a list of every item in the game (including those added by mods). The optional search text limits results to those which have all of the search words somewhere in their ID + type + name.
- **Example**:

```text
> list_items iridium

                name |                    id
-------------------- | ---------------------
   Iridium Fireplace |               (F)1796
      Iridium Krobus |               (F)2396
         Iridium Pan |      (H)IridiumPanHat
        Iridium Band |                (O)527
         Iridium Bar |                (O)337
...
```

## Command: `player_add name`
- **Syntax**: `player_add name <S:item name> [I:count] [I:quality]`
- **Description**: Adds an item to your inventory based on its name.

Parameters:
- <S:item name> is the default or translated item name to spawn.
- [I:count] is how many items to add to your inventory (defaults to 1).
- [I:quality] is the quality of the item to create (defaults to normal). This can be one of `0` (normal), `1` (silver), `2` (gold), or 4 (`iridium`).
- **Example**:
`player_add name "Galaxy Sword"` will create a Galaxy Sword.; `player_add name "Stir Fry" 10 4` will create 10 iridium-quality stir fry dishes.

## Command: `player_add`
- **Syntax**: `player_add <I:item ID> [I:count] [I:quality]`
- **Description**: Adds an item to your inventory based on its item ID.

Parameters:
- <I:item ID> is the unique ID for the item to add (as shown by the `list_items` command).
- [I:count] is how many items to add to your inventory (defaults to 1).
- [I:quality] is the quality of the item to create (defaults to normal). This can be one of `0` (normal), `1` (silver), `2` (gold), or 4 (`iridium`).
- **Example**:
`player_add (O)246` will create a wheat flour item.;`player_add (O)128 10 4` would create 10 iridium-quality pufferfish.

## Command: `player_setmoney`
- **Syntax**: `player_setmoney <I:amount>`
- **Description**: Changes the player's total money to the given amount of gold.
- **Example**:
`player_setmoney 5000` will change your total money to 5000g.

### Player
## Command: `player_changecolor`
- **Syntax**: `player_changecolor <S:feature> <S:color>`
- **Description**: Sets a color for your character's sprite.

Parameters:
- <S:feature> is one of `hair`, `eyes`, or `pants`.
- <S:color> is an RGB color, specified as three comma-separated channels from 0 (no color) to 255 (max color).
- **Example**:
`player_changecolor eyes 255,0,0` would make your character's eyes red.

## Command: `player_changestyle`
- **Syntax**: `player_changestyle <S:target> <I:style ID>`
- **Description**: Sets a style for your character's sprite.

Parameters:
- <S:target> is the part of the player's style to change. This is one of `hair`, `shirt`, `skin`, `acc` (accessory), `shoe`, `swim` (whether the player is wearing a swim suit), or `gender`.
- <I:style ID> is the integer ID for the style to use. For `swim`, this can be `0` (not in a swim suit) or `1` (in a swim suit). For `gender`, this can be `0` (male) or `1` (female).
- **Example**:
`player_setstyle swim 1` would change the player into their swim suit.

## Command: `player_sethealth`
- **Syntax**: `player_sethealth <I:amount>`
- **Description**: Sets the player's current health.
- **Example**:
`player_sethealth 200` would set you to 200 health points.

## Command: `player_setimmunity`
- **Syntax**: `player_setimmunity <I:amount>`
- **Description**: Sets the player's total immunity. This is permanent and is affected by immunity buffs. For example, if you set immunity 10 while Genie Shoes (+6 immunity) are equipped, removing the shoes would set your immunity to 4 (10 - 6). You can reset immunity to normal by removing everything that improves immunity, then entering `player_setimmunity 0`.
- **Example**:
`player_setimmunity 10` would give you 10 immunity points, or a 100% chance of avoiding buff effects.

## Command: `player_setmaxhealth`
- **Syntax**: `player_setmaxhealth <I:amount>`
- **Description**: Sets the player's maximum health. This permanently changes the baseline; for example, if you set your max health to 500 and then drink Iridium Snake Milk, your max health will increase to 525.
- **Example**:
`player_setmaxhealth 500` would give you a maximum of 500 health points.

## Command: `player_setmaxstamina`
- **Syntax**: `player_setmaxstamina <I:amount>`
- **Description**: Sets the player's maximum stamina. This permanently changes the baseline; for example, if you set your max stamina to 300 and then collect a Stardrop, your max stamina will increase to 334.
- **Example**:
`player_setmaxstamina 500` would give you a maximum of 500 stamina points.

## Command: `player_setname`
- **Syntax**: `player_setname <S:target><S:name>`
- **Description**: Sets the name of the current player or their farm, depending on the <S:target> value (`player` or `farm`).
- **Example**:
`player_setname player Malon` would change your player name to Malon.;`player_setname farm "Lon Lon"` would change your farm name to Lon Lon Farm.

## Command: `player_setstamina`
- **Syntax**: `player_setstamina <I:amount>`
- **Description**: Sets the player's current stamina.
- **Example**:
`player_setstamina 200` would give you 200 stamina points.

### World
## Command: `hurry_all`
- **Syntax**: `hurry_all`
- **Description**: Immediately warps all NPCs to their scheduled positions. (To hurry a single NPC, use `debug hurry npc-name` instead.)

## Command: `set_farm_type list`
- **Syntax**: `set_farm_type list`
- **Description**: Shows a list of farm types you can use with the `set_farm_type` command.

## Command: `set_farm_type`
- **Syntax**: `set_farm_type <S:farm type>`
- **Description**: Sets the player's current farm type, where <I:farm type> is one of `0` (Standard), `1` (Riverlands), `2` (forest), `3` (Hilltop), `4` (Combat), `5` (Four Corners), `6` (Beach), `7` (Meadowlands) or a custom farm type ID. You can enter `set_farm_type list` for a list of valid farm type IDs.
- **Example**:
`set_farm_type 1` will set the farm type to riverlands.

## Command: `world_clear`
- **Syntax**: `world_clear <S:location><S:entity type>`
- **Description**: Removes all entities of the given type from a location.

Parameters:
- <S:location> is the internal name for a location, or `current` for the one you're in.
- <S:entity type> is one of `crops`, `debris`, `fruit-trees`, `furniture`, `grass`, `trees`, `removable` (everything that can be removed or destroyed during normal gameplay), or `everything` (everything including permanent bushes).
- **Example**:
`world_clear current debris` will remove all debris (sticks, stones, and small plants).

## Command: `world_downminelevel`
- **Syntax**: `world_downminelevel`
- **Description**: Goes down one mine level. If you're not in the mines, warps you to the first mine level.

## Command: `world_freezetime`
- **Syntax**: `world_freezetime <I:action>`
- **Description**: Freezes or resumes the time. The [I:action] can be `0` (resume time) or `1` (freeze time); if omitted, time is toggled.

## Command: `world_setday`
- **Syntax**: `world_setday <I:day>`
- **Description**: Sets the day of month.
- **Example**:
`world_setday 30` will set the date to the 30th of the current season.

## Command: `world_setminelevel`
- **Syntax**: `world_setminelevel <I:mine level>`
- **Description**: Warps you to the given mine level. This can be the regular mines (levels 1–120), Skull Cavern (121+), or Quarry Mine (77377).
- **Example**:
`world_setminelevel 80` warps to mine level 80.

## Command: `world_setseason`
- **Syntax**: `world_setseason <S:season>`
- **Description**: Sets the season (one of `spring`, `summer`, `fall`, or `winter`).
- **Example**:
`world_setseason spring` will set the season to spring.

## Command: `world_settime`
- **Syntax**: `world_settime <I:time>`
- **Description**: Sets the time of day, using the game's 26-hour time format (from 0600 for 6am at the start of day, to 2600 for 2am at the end of day).
- **Example**:
`world_settime 1430` will set time to 2:30pm.

## Command: `world_setyear`
- **Syntax**: `world_setyear <I:year>`
- **Description**: Sets the year number.
- **Example**:
`world_setyear 10` will set the the game to the 10th year.

### Other
## Command: `harmony_summary`
- **Syntax**: `harmony_summary [S:search]`
- **Description**: Lists the Harmony patches added by SMAPI and other mods. If [S:search] is given, only patched method names matching any of the search terms will be listed.
- **Example**:
`harmony_summary MeleeWeapon` will list patches which affect the `MeleeWeapon` class in the game code.

## Command: `help`
- **Syntax**: `help [S:command name]`
- **Description**: Provides documentation for console commands. If called without an argument, shows general help text and a list of available commands. If called with a command name, shows the documentation for that command.

## Command: `log_context`
- **Syntax**: `log_context`
- **Description**: Enables logging more contextual info like buttons pressed, menus changed, etc. For example, it can be used to get key binding codes or to simplify troubleshooting. This is enabled until you restart the game, or run the command again to disable it.

## Command: `reload_i18n`
- **Syntax**: `reload_i18n`
- **Description**: Reload translation files for all mods. This is mainly useful when translating mods. (Note that if a mod caches the text, it may show the old version until it updates.)

## Command: `set_verbose`
- **Syntax**: `set_verbose [enable] [mod ID]+`
- **Description**: <p>Toggles whether more detailed information is written to the SMAPI log file (and console in developer mode). This may impact performance. This doesn't affect mods manually set to verbose in the config file.</p>

Parameters:
- [enable]: Whether to enable (`true` or `1`) or disable (`false` or `0`) verbose logs. If omitted, it toggles the current mode.
- [mod ID]+: the mods whose verbose logs to change (or `SMAPI` for SMAPI itself). If omitted, applies to SMAPI and all mods.
- **Example**:
`set_verbose`: toggle verbose logs for SMAPI and all mods.;`set_verbose 1`: enable verbose logs for SMAPI and all mods.;`set_verbose 1 SMAPI Pathoschild.ContentPatcher`: enable verbose logs for SMAPI and Content Patcher.

## Command: `show_data_files`
- **Syntax**: `show_data_files`
- **Description**: Opens the folder containing the save and log files.

## Command: `show_game_files`
- **Syntax**: `show_game_files`
- **Description**: Opens the game folder.

Mods may also add their own commands. For example, Content Patcher adds the `patch` command, documented [here](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/troubleshooting.md).

### Advanced
⚠️ These may corrupt or make permanent changes to your save. DO NOT USE THESE unless you're absolutely sure.

## Command: `apply_save_fix`
- **Syntax**: `apply_save_fix <S:fix ID>`
- **Description**: Applies one of the game's save migrations to the currently loaded save. Parameters:
- <s:fix ID> is the unique identifier for the save fix to apply. You can specify `list` to show a list of save fix IDs.
- **Example**:
`apply_save_fix list` would show a list of save fixes.;`apply_save_fix AddCampfireKit` would add Cookout Kit to the player's crafting recipes if they meet the requirements for it.

## Command: `debug`
- **Syntax**: `debug <*:command text>`
- **Description**: Executes one of the game's debug commands. See *debug commands* below for more info.

## Command: `regenerate_bundles`
- **Syntax**: `regenerate_bundles [S:type] [*:flags]`
- **Description**: Regenerates your community center bundle data. This will **reset all bundle progress**, and may have unintended effects if you've already completed bundles. Parameters:
- [S:type]: the bundle type that will be generated (defaults to the current bundle type). Possible values: `Default` or `Remixed`.
- `confirm`: confirms that you're aware of the potential consequences. Running `regenerate_bundles` without this option will show a warning message instead.
- `ignore_seed` (*optional*): if the type is `Remixed`, causes remixed bundles to be re-randomized without using the save seed.
- **Example**:
`regenerate_bundles confirm` will regenerate bundles using the save's current settings.;`regenerate_bundles remixed ignore_seed confirm` will regenerate using randomized remixed bundles.

## Debug commands
type=error text=**Don't use these commands unless you're aware of the possible consequences.**
These are meant for the game developers, not players. They may crash your game, permanently corrupt or break your save, or cause other problems. Using these in a save you care about is not recommended.

### How to enter debug commands
The game itself has hundreds of hidden debug commands used to test the game. These require the Console Commands mod too, but **every command should be prefixed with `debug`** like this:

```text
debug where Robin
> Robin is at Farm, 21,4
```

The example above returned output, but many commands don't. If there's no output, SMAPI will say *Sent debug command to the game, but there was no output*.

### Macros
Instead of entering each command directly into the SMAPI console, you can also use the `debug runmacro` command to a list of debug commands from a text file. (This doesn't work with the non-debug commands.) Here's how to use it:

# Create a text file in your game folder (with a `.txt` extension).
# Type commands in this file, one command per line. Each command should start with a slash character (`/`), but should not include `debug`.
# To run the macro, type `debug runmacro <filename>` in the SMAPI console, replacing `<filename>` with the name of your command file with the `.txt` extension.

For example, let's say you have a `quickstart.txt` file with these commands:

```text
/backpack 12
/Money 10000
/levelup 0 1
/weapon 5
```

Entering `debug runmacro quickstart` in the SMAPI console will execute all the commands, resulting in a backpack upgrade, money set to 10000g, farming skill set to level 1, and a Bone Sword added to the player's inventory. (Note: `Money` is capitalized in this example because if it is all lowercase it triggers a special chat response instead of executing the command.)

### Actions and queries
## Command: `action`
- **Syntax**: `action <S:action>`
- **Description**: Run a trigger action string.
- **Example**:
`action AddMoney 500` will add 500 g to the current player.

## Command: `gamequery,gq`
- **Syntax**: `gamequery <S:query>`
- **Description**: Check whether the given game state query matches in the current context.
- **Example**:

```text
gq !SEASON Spring, WEATHER Here Sun
> Result: true.
```

## Command: `itemquery,iq`
- **Syntax**: `itemquery <S:query>`
- **Description**: Open a shop menu with all the items matching an item query (all items free).
- **Example**:
`debug iq ALL_ITEMS` shows all items;`debug iq ALL_ITEMS (W)` shows all weapons;`debug iq (O)128` shows a pufferfish (object 128);`debug iq FLAVORED_ITEM Wine (O)128` shows Pufferfish Wine.

## Command: `search`
- **Syntax**: `search [S:term]`
- **Description**: List all debug commands that match the given search term (or all debug commands if the search term is omitted).
- **Example**:

```text
debug search backpack
> Found 2 debug commands matching search term 'backpack':
    - Backpack
    - FillBackpack (fbp, fill, fillbp)
```

## Command: `tokens`
- **Syntax**: `tokens <S:tokenizedString>`
- **Description**: Parses a tokenizable string and prints its output.
- **Example**:

```text
tokens [LocalizedText Strings\StringsFromCSFiles:MapPage.cs.11064 [EscapedText [FarmName]]]
> Result: "Lon Lon Farm"
```

### Items and inventory
#### General item search and spawning
## Command: `createDebris`
- **Syntax**: `createDebris <I:item ID>`
- **Description**: Spawns an item with the given qualified or unqualified item ID on the ground at your feet.
- **Example**:
`debug createDebris 24` would spawn a parsnip.

## Command: `doesItemExist`
- **Syntax**: `doesItemExist <S:item ID>`
- **Description**: Searches the entire world (including other players' inventories) to see if the qualified or unqualified item ID exists anywhere. If you search with the name of the item, make sure that it's Capitalized for each word in the item's name, between "quotation mark" and there's no space between the words. A global message saying *Yes* or *No* will be displayed, but there's no indication of where the item is located if it is found. See the `whereIsItem` command for more detailed output.
- **Example**:
`debug doesItemExist 24` or `debug doesItemExist (O)24` or `debug doesItemExist "Parsnip"` will search for parsnips.; `debug doesItemExist "river jelly"` would respond with "No" but `debug doesItemExist "RiverJelly"` would search for River Jelly and respond with "Yes"

## Command: `furniture,ff`
- **Syntax**: `furniture [item ID]`
- **Description**: Adds a furniture-type item to your inventory based on its unqualified item ID. If the item ID is omitted, a random furniture with ID 0–1612 will be added.
- **Example**:
`debug furniture 704` would give you an Oak Dresser.

## Command: `fuzzyItemNamed,fin,f`
- **Syntax**: `fuzzyItemNamed <S:name search>[I:amount][I:quality]`
- **Description**: Adds the specified item to your inventory based on a fuzzy search for <item name> across all item types. Only the first matching item will be spawned. The item name can be quoted to include spaces. The optional parameters are for stack size (default 1) and quality (default 0).
- **Example**:
`debug fuzzyItemNamed sturg 5 4` would give you a stack of five iridium-quality sturgeon.; `debug fin "galaxy sword"` would give you a Galaxy Sword.; `debug f grief` would give you a "Good Grief" shirt.

## Command: `getIndex`
- **Syntax**: `getIndex <S:name search>`
- **Description**: Shows the display name and qualified item ID for the item matching the fuzzy search (see `fuzzyItemNamed` for search syntax).
- **Example**:
`debug getIndex prisma` would output *Prismatic Shard's qualified ID is (O)74*.;`debug getIndex grief` would output *"Good Grief" Shirt's qualified ID is (S)1008*.

## Command: `holdItem`
- **Syntax**: `holdItem`
- **Description**: Plays the animation where the player holds an item up above their head. Uses the player's current item.

## Command: `item,i`
- **Syntax**: `item <S:item ID>[I:amount][I:quality]`
- **Description**: Adds an item to your inventory based on its qualified or unqualified item ID. The optional parameters are for stack size (default 1) and quality (default 0).
- **Example**:
`debug item 74` or `debug item (O)74` would give you a prismatic shard.

## Command: `itemNamed,in`
- **Syntax**: `itemNamed <S:item name>[I:amount][I:quality]`
- **Description**: Adds object-type items whose internal names exactly equals the <item name> (case-insensitive) to your inventory. The optional parameters are for stack size (default 1) and quality (default 0).
- **Example**:
`debug itemNamed "miner's treat"` would give you miner's treat.;`debug in "strange doll" 3` would give you 3 of each of the strange doll artifacts.

## Command: `lookup,lu`
- **Syntax**: `lookup <S:name search>`
- **Description**: Outputs the unqualified item ID of each object-type item whose internal name contains the <name search> (case-insensitive).
- **Example**:
`debug lookup diamond` would output *Diamond 72*.; `debug lu strange doll` would output *Strange Doll 126* and *Strange Doll 127*.

## Command: `qualifiedId`
- **Syntax**: `qualifiedId`
- **Description**: Shows the held item's display name and qualified item ID.

## Command: `tv`
- **Syntax**: `tv`
- **Description**: Adds a budget TV or plasma TV to your inventory (each has an equal random chance).

## Command: `wallpaper,wp`
- **Syntax**: `wallpaper [I:item ID]`
- **Description**: Adds a floorpaper or wallpaper item to the inventory. If a numeric wallpaper/flooring ID is specified, spawns that one; otherwise it randomly chooses one from floor IDs 0–39 or wallpaper IDs 0–111.

## Command: `whereIsItem, whereItem`
- **Syntax**: `whereIsItem <S:item ID>`
- **Description**: Searches the entire world (including other players' inventories) for items matching the qualified or unqualified item ID, and lists all matches.
- **Example**:

```text
> debug whereItem "Watering Can"

Found 1 item matching name 'Watering Can':
- Farm > Shed at 50, 14 > Shed149bae63-2add-4ab6-a5aa-b3bd76372004 > Chest at 4, 4 > Watering Can ((T)CopperWateringCan)
```

#### Backpack and inventory
## Command: `backpack`
- **Syntax**: `backpack <I:amount>`
- **Description**: Increases your inventory space by the specified amount; capped at 36 slots.

## Command: `clear,ci`
- **Syntax**: `clear`
- **Description**: Removes all items currently in your inventory.

## Command: `fillbackpack,fillbp,fill,fbp`
- **Syntax**: `fillbackpack`
- **Description**: Fills all empty spaces in your inventory with random Objects. Any objects spawned by this command will not be marked as found on the collections tab.

## Command: `sl,shiftToolbarLeft`
- **Syntax**: `sl`
- **Description**: Shifts inventory rows down, looping previous bottom row to top; similar to using Control-Tab with default keyboard controls. Will work with larger-than-normal inventories.

## Command: `sr,shiftToolbarRight`
- **Syntax**: `sr`
- **Description**: Shifts inventory rows up, looping previous top row to bottom; similar to using Tab with default keyboard controls. Will work with larger-than-normal inventories.

#### Clothing and tailoring
## Command: `dye`
- **Syntax**: `dye <S:itemType> <S:color> <F:strength>`
- **Description**: Dyes the specified (currently equipped) item the specified color. Item type can be `shirt` or `pants` and valid colors are `black`, `blue`, `green`, `red`, `white`, and `yellow`. Strength is a float between 0 and 1 inclusive; the higher the number, the more vibrant the color. The dye will mix with the current color so it is sometimes necessary to "reset" the item by dyeing first with white strength 1.
- **Example**:
`debug dye shirt red 0.33` would dye your current shirt a shade of pink.; `debug dye pants blue 1` would dye your current pants bright blue.

## Command: `dyeAll`
- **Syntax**: `dyeAll`
- **Description**: Seems to be intended to bring up a character customization menu with HSV sliders for both shirt and pants, but it does not function because all entered commands are forced to lower case. The two individual commands dyepants and dyeshirt can be used instead.

## Command: `dyemenu`
- **Syntax**: `dyemenu`
- **Description**: Brings up the same dyeing menu you get by interacting with the dye pots in Emily's house. Filling all six pots with appropriate items will bring up a character customization menu with HSV sliders for dyeing both your currently-equipped shirt and pants.

## Command: `dyepants`
- **Syntax**: `dyepants`
- **Description**: Brings up a character customization menu with HSV sliders for dyeing your currently-equipped pants.

## Command: `dyeshirt`
- **Syntax**: `dyeshirt`
- **Description**: Brings up a character customization menu with HSV sliders for dyeing your currently-equipped shirt.

## Command: `hat`
- **Syntax**: `hat <I:itemID>`
- **Description**: Gives and automatically equips the specified hat to your farmer; any currently equipped hat will be destroyed.  See hat data for a list of base game IDs.
- **Example**:
`debug hat 3` would give you a Sombrero and automatically equip it.

## Command: `shirt`
- **Syntax**: `shirt <I:itemID>`
- **Description**: Gives and automatically equips the specified shirt to your farmer; any currently equipped shirt will be destroyed.

## Command: `tailor`
- **Syntax**: `tailor`
- **Description**: Brings up the same tailoring menu you get by interacting with the sewing machine in Emily's house.

## Command: `tailorrecipelisttool,trlt`
- **Syntax**: `tailorrecipelisttool`
- **Description**: Brings up a special menu listing most Objects, what color they will dye an item, and what clothing item they can make when used in the sewing machine. The menu can be scrolled with the mouse wheel, hovering the mouse over the object will show the tooltip for the clothing it makes, and clicking on the object will add the clothing it makes to your inventory.

#### Tools and weapons
## Command: `bobber`
- **Syntax**: `bobber <I:type>`
- **Description**: Changes the player's fishing rod bobber style. Valid styles begin at 0.

## Command: `fish`
- **Syntax**: `fish <S:whichFish>`
- **Description**: Starts the fishing minigame with the specified fish. The player must be holding a fishing rod. Can lead to the farmer getting stuck on fishing animation frames.

## Command: `forge`
- **Syntax**: `forge`
- **Description**: Shows the Forge menu.

## Command: `pole`
- **Syntax**: `pole [I:type]`
- **Description**: Adds a Fishing Pole of the specified type to your inventory. Valid types are 0 (Bamboo Pole; the default), 1 (Training Rod), 2 (Fiberglass Rod), or 3 (Iridium Rod).

## Command: `trashcan`
- **Syntax**: `trashcan <I:level>`
- **Description**: Changes the upgrade level of your inventory Trash Can. Upgrade levels are 0 (basic), 1 (copper), 2 (steel), 3 (gold), or 4 (iridium). The can sprite may not fully update until the inventory is closed and reopened.

#### Wallet items
## Command: `clearspecials`
- **Syntax**: `clearspecials`
- **Description**: Removes most special items from your Wallet. The Rusty Key, Skull Key, Special Charm, Dark Talisman, Magic Ink, Club Card, Dwarven Translation Guide, and Magnifying Glass will all be removed, but Bear's Knowledge and Spring Onion Mastery will remain.

## Command: `darktalisman`
- **Syntax**: `darktalisman`
- **Description**: Adds the Dark Talisman to (and removes the Magic Ink from) your wallet; also removes the magic artifact blocking access to the Witch's Swamp.
**Warning: This command will also clear all received mail and hidden mail flags.**

## Command: `skullkey`
- **Syntax**: `skullkey`
- **Description**: Adds the Skull Key to your wallet.

## Command: `specialitem`
- **Syntax**: `specialitem <I:itemID>`
- **Description**: Adds the specified special item to your wallet. Which ID values are useful is currently unknown.

## Command: `specials`
- **Syntax**: `specials`
- **Description**: Adds all special items to your wallet, including Bear's Knowledge and Spring Onion Mastery. The associated events for these two are also marked as seen.

## Command: `townkey`
- **Syntax**: `townkey`
- **Description**: Adds the Key To The Town to your wallet.

#### Miscellaneous items
## Command: `fillbin,fb`
- **Syntax**: `fillbin`
- **Description**: Adds a Parsnip, Fire Quartz, LargeMouth Bass, Wild Horseradish, and Wood to the shipping bin.

## Command: `listtags`
- **Syntax**: `listtags`
- **Description**: Outputs all object context tags associated with the currently-held item to the console.
- **Example**:
`debug listtags` while holding an Acorn would output *Tags on Acorn: id_o_309 color_brown tree_seed_item item_acorn category_seeds*.

## Command: `makeinedible`
- **Syntax**: `makeinedible`
- **Description**: Makes the currently-held item inedible by setting its edibility value to -300; does not affect other instances of the same item.

## Command: `skullgear`
- **Syntax**: `skullgear`
- **Description**: Sets your current backpack size to 36 slots, equips a Savage Ring and Iridium Band, equips Space Boots, and clears inventory except for an Iridium Pickaxe, a Galaxy Sword, a stack of 20 Spicy Eels, and a stack of 20 Mega Bombs. Also sets your maximum health to 75 and gives you the Fighter profession. Any previously equipped boots and rings and all previous inventory items are lost.

### Player
#### Appearance
## Command: `copyoutfit`
- **Syntax**: `copyoutfit`
- **Description**: Copies all elements of the player's outfit in XML format to the desktop clipboard and prints the same XML to the console.

## Command: `customizemenu,customize,cmenu`
- **Syntax**: `customizemenu`
- **Description**: Opens the full character customization menu seen at the start of a new game which includes gender options and player/farm names. Changing the player name will cause the save file to change as well.

## Command: `haircolor`
- **Syntax**: `haircolor <I:R><I:G><I:B>`
- **Description**: Sets the player's hair color to the specified RGB values. Each has a range of 0-255.

## Command: `hairstyle`
- **Syntax**: `hairstyle <I:ID>`
- **Description**: Sets the player's hair style to the specified ID. Note that these IDs are one less than the values shown in the character customization menu and have a range of 0-55 in the base game.

## Command: `pants`
- **Syntax**: `pants <I:R><I:G><I:B>`
- **Description**: Sets the player's pants color to the specified RGB values. Each has a range of 0-255. This no longer has a noticeable effect since pants are now a clothing item; the dyepants command should be used instead.

## Command: `shirtcolor`
- **Syntax**: `shirtcolor <I:ID>`
- **Description**: Sets the player's shirt choice to the specified ID. This no longer has a noticeable effect since shirts are now a clothing item; the clothes or dyeshirt commands should be used instead.

## Command: `skincolor`
- **Syntax**: `skincolor <I:ID>`
- **Description**: Sets the player's skin color to the specified ID. Note that these IDs are one less than the values shown in the character customization menu and have a range of 0-23 in the base game.

#### Health, stamina, buffs, and currency
## Command: `buff`
- **Syntax**: `buff <I:buffID>`
- **Description**: Adds the specified buff to the player. Useful ID values are listed below. Also see the speed command.

<table class="wikitable"><tr><td>6 - Full (prevents eating) for 3 min</td><td>18 - Spooked (-8 attack) for 8 sec</td></tr>
<tr><td>7 - Quenched (prevents drinking) for 1 min</td><td>19 - Frozen (-8 speed) for 2 sec</td></tr>
<tr><td>12 - Goblin's Curse (-3 spd/def/att) for 6 sec</td><td>20 - Warrior Energy (+10 attack) for 5 sec</td></tr>
<tr><td>13 - Slimed (-4 speed) for 2.5-3 sec</td><td>21 - Yoba's Blessing (Invincibility) for 5 sec</td></tr>
<tr><td>14 - Jinxed (-8 defense) for 8 sec</td><td>22 - Adrenaline Rush (+2 speed) for 3 sec</td></tr>
<tr><td>17 - Tipsy (-1 speed) for 30 sec</td><td>23 - Oil of Garlic (prevents swarms) for 10 min</td></tr></table>

## Command: `clearbuffs`
- **Syntax**: `clearbuffs`
- **Description**: Removes all active buffs (both positive and negative.)

## Command: `die`
- **Syntax**: `die`
- **Description**: Sets your health to zero, resulting in passing out and awakening in the Clinic.

## Command: `energize`
- **Syntax**: `energize [I:amount]`
- **Description**: Sets your energy to the specified amount. If no amount is specified, it will be set to maximum.

## Command: `exhaust`
- **Syntax**: `exhaust`
- **Description**: Sets your energy to -15, resulting in passing out and ending the day.

## Command: `gem`
- **Syntax**: `gem <I:amount>`
- **Description**: Gives you the specified number of Qi Gems.

## Command: `gold`
- **Syntax**: `gold`
- **Description**: Gives you one million (1,000,000) gold.

## Command: `heal`
- **Syntax**: `heal`
- **Description**: Sets your health to maximum.

## Command: `invincible,inv,gm`
- **Syntax**: `invincible`
- **Description**: Toggles invincibility. When on, you will not take any damage.

## Command: `money`
- **Syntax**: `money <I:amount>`
- **Description**: Sets your money to the specified amount. To use in a macro, make sure one or more of the letters in the command is capitalized.

## Command: `testnut`
- **Syntax**: `testnut`
- **Description**: Spawns a Golden Walnut at the upper left corner of the current map which will immediately start moving towards a player for collection.

## Command: `walnut`
- **Syntax**: `walnut <I:amount>`
- **Description**: Gives your team the specified number of Golden Walnuts.

#### Movement and warping
## Command: `canmove,cm,c`
- **Syntax**: `canmove`
- **Description**: Attempts to force-enable player movement by resetting animations and dismounting the horse. Sometimes useful when the player becomes "stuck."

## Command: `farmerdayupdate`
- **Syntax**: `farmerdayupdate [I:timeOfDay]`
- **Description**: Updates the farmer for a new day (`Farmer::dayupdate`) as if they went to sleep at the current time of day. If `timeOfDay` is specified, it then re-updates the farmer as if they went to sleep at time `0` (inclusive) all the way up through time `timeOfDay - 1` (exclusive).

## Command: `minelevel`
- **Syntax**: `minelevel <I:level> [I:layout]`
- **Description**: Warps you to the specified level of the Mines using the specified layout in `Content/Maps/Mines`. Use level 77377 to warp to the Quarry Mine, and to warp to a level in the Skull Cavern, add 120 to your target level.
- **Example**:
`debug mineLevel 101 47` warps to mine level 101 using layout 47.

## Command: `printplayerpos,ppp`
- **Syntax**: `printplayerpos`
- **Description**: Prints the player's current position in both tile and pixel coordinates to the console.

## Command: `speed`
- **Syntax**: `speed <I:value>[I:duration]`
- **Description**: Gives the player a speed buff of the specified amount for the specified duration. The duration is interpreted as in-game minutes and defaults to 30; multiplying this value by 0.7 will convert to real-time seconds. This buff has a unique source of "Debug Speed" and will stack with both food and drink speed buffs.
- **Example**:
`debug speed 5 600` will give you a +5 speed buff which lasts for 7 minutes (10 game hours).

## Command: `volcano`
- **Syntax**: `volcano <I:level>`
- **Description**: Warps you to the specified level of the Volcano Dungeon.

## Command: `warp`
- **Syntax**: `warp <S:locationName>[I:X][I:Y]`
- **Description**: Warps you to the specified location at the given coordinates. The location name is a fuzzy match, and if the coordinates are not supplied the game has a list of hardcoded defaults it will use for many locations (see `Utility.getDefaultWarpLocation()` for details.)
- **Example**:
`debug warp forest 33 99` will warp you near Hat Mouse's shop in the forest.; `debug warp sci` will warp you to Robin's House (internally `ScienceHouse`) just in front of where Robin tends the shop.

## Command: `warphome,wh`
- **Syntax**: `warphome`
- **Description**: Warps you directly to your bed in your farmhouse/cabin.

## Command: `warpshop,ws`
- **Syntax**: `warpshop <S:npcName>`
- **Description**: Warps you to the shop run by the specified NPC; if necessary will also warp the NPC to the shop location. NPC names are case-insensitive and can be one of the following: `pierre`, `robin`, `krobus`, `sandy`, `marnie`, `clint`, `gus`, `willy`, `pam`, `dwarf`, and `wizard`. The `wizard` option will also add Magic Ink to your wallet and mark the event where the ink is returned as already seen.
- **Example**:
`debug warpshop marnie` will warp both you and Marnie to her animal shop.

## Command: `warptocharacter,wtc`
- **Syntax**: `warptocharacter <S:npcName>`
- **Description**: Warps you to the specified character, wherever they are. This is a fuzzy match.

## Command: `warptoplayer,wtp`
- **Syntax**: `warptoplayer <S:playerName>`
- **Description**: Warps you to the specified player, wherever they are. The match is case-insensitive and names with spaces should be surrounded by double quotes.

#### Skills and XP
## Command: `experience`
- **Syntax**: `experience <I:skillID><I:xpAmount>`
- **Description**: Adds the specified amount of experience to the specified skill. Valid skill IDs are 0 (Farming), 1 (Fishing), 2 (Foraging), 3 (Mining), 4 (Combat), and 5 (Luck).

## Command: `fishing`
- **Syntax**: `fishing <I:level>`
- **Description**: Sets your fishing skill to the specified level. This does not unlock crafting recipes or allow profession choice, nor does it change the underlying experience amount. However it does count in terms of unlocking what Willy sells and enabling legendary fish to be hooked.

## Command: `levelup`
- **Syntax**: `levelup <I:skillID><I:level>`
- **Description**: Shows the appropriate levelup window for the specified skill and level. This unlocks any associated crafting recipes and (if applicable) lets you choose a profession, but does not actually change the skill level or underlying experience amount. Valid skill IDs are 0 (Farming), 1 (Fishing), 2 (Foraging), 3 (Mining), 4 (Combat), and 5 (Luck).

## Command: `profession`
- **Syntax**: `profession <I:ID>`
- **Description**: Gives you the specified profession. Note that this just hard adds the profession, it's not a way to switch skill trees. Valid profession IDs from the base game are shown below.
<table class="wikitable"><tr><th>Farming</th><th>Fishing</th><th>Foraging</th><th>Mining</th><th>Combat</th></tr>
<tr><td>0 - Rancher</td><td>6 - Fisher</td><td>12 - Forester</td><td>18 - Miner</td><td>24 - Fighter</td></tr>
<tr><td>1 - Tiller</td><td>7 - Trapper</td><td>13 - Gatherer</td><td>19 - Geologist</td><td>25 - Scout</td></tr>
<tr><td>2 - Coopmaster</td><td>8 - Angler</td><td>14 - Lumberjack</td><td>20 - Blacksmith</td><td>26 - Brute</td></tr>
<tr><td>3 - Shepherd</td><td>9 - Pirate</td><td>15 - Tapper</td><td>21 - Prospector</td><td>27 - Defender</td></tr>
<tr><td>4 - Artisan</td><td>10 - Mariner</td><td>16 - Botanist</td><td>22 - Excavator</td><td>28 - Acrobat</td></tr>
<tr><td>5 - Agriculturist</td><td>11 - Luremaster</td><td>17 - Tracker</td><td>23 - Gemologist</td><td>29 - Desperado</td></tr></table>

## Command: `showexperience`
- **Syntax**: `showexperience <I:skillID>`
- **Description**: Outputs your total experience amount for the specified skill in the SMAPI console. Valid skill IDs are 0 (Farming), 1 (Fishing), 2 (Foraging), 3 (Mining), 4 (Combat), and 5 (Luck).

#### Statistics and achievements
## Command: `achieve`
- **Syntax**: `achieve <S:steamAchieveID>`
- **Description**: Awards the specified Steam achievement. Steam achievements which correspond to in-game achievements use the same numeric ID as listed in Achievement data with one exception: the ID for *Greenhorn* is `a0` instead of just `0`. Steam-only achievements have a much longer string ID; these are listed below.
<table class="wikitable"><tr><td>`Achievement_SingularTalent` - *Singular Talent*</td><td>`Achievement_MasterOfTheFiveWays` - *Master Of The Five Ways*</td></tr>
<tr><td>`Achievement_LocalLegend` - *Local Legend*</td><td>`Achievement_Joja` - *Joja Co. Member Of The Year*</td></tr>
<tr><td>`Achievement_FullHouse` - *Full House*</td><td>`Achievement_Stardrop` - *Mystery Of The Stardrops*</td></tr>
<tr><td>`Achievement_TheBottom` - *	The Bottom*</td><td>`Achievement_KeeperOfTheMysticRings` - *Protector Of The Valley*</td></tr>
<tr><td>`Achievement_PrairieKing` - *Prairie King*</td><td>`Achievement_FectorsChallenge` - *Fector's Challenge*</td></tr></table>
- **Example**:
`debug achieve 17` will award the Steam achievement for *Gourmet Chef*.;`debug achieve Achievement_Stardrop` will award the Steam achievement for *Mystery of the Stardrops*.

## Command: `achievement`
- **Syntax**: `achievement <I:achieveID>`
- **Description**: Awards the specified in-game achievement. See Achievement data for a list of IDs. This will also award the associated Steam achievement if you don't have it.

## Command: `catchallfish`
- **Syntax**: `catchallfish`
- **Description**: Marks every fish in the game as having been caught by the player. All fish will be marked as having size 9.

## Command: `caughtfish,fishcaught`
- **Syntax**: `caughtfish <I:value>`
- **Description**: Sets the `FishCaught` stat to the specified amount.

## Command: `daysplayed,dap`
- **Syntax**: `daysplayed`
- **Description**: Shows a global message with the current value of the `daysPlayed` stat.

## Command: `dp`
- **Syntax**: `dp <I:value>`
- **Description**: Sets the `daysPlayed` stat to the specified amount.

## Command: `getStat`
- **Syntax**: `getStat <S:stat key>`
- **Description**: Shows a stat's value in the console.

## Command: `killmonsterstat,kms`
- **Syntax**: `killmonsterstat <S:monsterName><I:value>`
- **Description**: Sets the kill stats for the specified monster to the specified value. The monster name should be the same as the keys in `Data/Monsters` and names with spaces should be in double quotes; it is case-sensitive. The command will output a buggy response to the console due to referencing the wrong string key, but the stats are correctly set.
- **Example**:
`debug kms "Dust Spirit" 499` will set the monster kill stats for Dust Sprites to 499 and output *Drink Dust Spirit?*

## Command: `mineinfo`
- **Syntax**: `mineinfo`
- **Description**: Outputs two mine-related stats to the SMAPI Console: `MineShaft.lowestLevelReached` and `player.deepestMineLevel`

## Command: `perfection`
- **Syntax**: `perfection`
- **Description**: **May be broken.** Makes a variety of changes to qualify for Perfection. These include maxing all friendships, marking all fish as caught, awarding and marking complete all cooking and crafting recipes, marking all items as shipped, setting the flags for all stardrops, setting all skill levels to 10, awarding 500 kills for all monsters, forcibly placing all 4 obelisks and the gold clock in the upper left corner of the farm, and giving 130 walnuts.

## Command: `resetachievements`
- **Syntax**: `resetachievements`
- **Description**: Resets the Steam achievements.

## Command: `setStat`
- **Syntax**: `setStat <S:stat key><I:value>`
- **Description**: Sets a stat's value to the given positive integer.
- **Example**:
`debug setstat StepsTaken 99999` would set the *Number of Steps Taken* stat to 99999.

### Collections and quests
#### Cooking and crafting
## Command: `addallcrafting`
- **Syntax**: `addallcrafting`
- **Description**: Teaches you all crafting recipes. Basically the same as crafting, but this one does not check if you already know the recipe before adding so it may output some error messages to the console about duplicate keys.

## Command: `cooking`
- **Syntax**: `cooking`
- **Description**: Teaches you all cooking recipes.

## Command: `cookingrecipe`
- **Syntax**: `cookingrecipe <S:recipeName>`
- **Description**: Teaches you the specified cooking recipe. Names are case-sensitive and may contain spaces.
- **Example**:
`debug cookingrecipe Seafoam Pudding` will give you the recipe to cook Seafoam Pudding.

## Command: `crafting`
- **Syntax**: `crafting`
- **Description**: Teaches you all crafting recipes.

## Command: `craftingrecipe,addCraftingRecipe`
- **Syntax**: `craftingrecipe <S:recipeName>`
- **Description**: Teaches you the specified crafting recipe. Names are case-sensitive and may contain spaces.
- **Example**:
`debug craftingrecipe Ancient Seeds` will give you the recipe to craft plantable ancient seeds from the artifact.

## Command: `slimecraft`
- **Syntax**: `slimecraft`
- **Description**: Teaches you the crafting recipes for Slime Incubator and Slime Egg-Press.

#### Fishing, museum, and secret notes
## Command: `clearfishcaught`
- **Syntax**: `clearfishcaught`
- **Description**: Clears all records of which fish you have caught, resetting the collection. To also change the stat which tracks how many total fish have been caught, see caughtfish.

## Command: `clearmuseum`
- **Syntax**: `clearmuseum`
- **Description**: Removes all donated items from the museum, emptying the museum displays and causing all artifacts and minerals to have the *Gunther can tell you more about this...* description. Does not affect the records of which artifacts and minerals have been found (*i.e.,* the collection pages).

## Command: `deletearch`
- **Syntax**: `deletearch`
- **Description**: Clears all records of which artifacts and minerals you have found.
**Warning: Also clears all record of which fish you have caught and clears all received mail (including hidden progress flags).**

## Command: `museumloot`
- **Syntax**: `museumloot`
- **Description**: Adds unfound artifacts and minerals to your inventory until it is full. Items added by this command will now be marked "found" on the collection pages.

## Command: `newmuseumloot`
- **Syntax**: `newmuseumloot`
- **Description**: Adds undonated artifacts and minerals to your inventory until it is full. Items added by this command increment the "Total found" count on the collection pages.

## Command: `note`
- **Syntax**: `note <I:bookID>`
- **Description**: Sets the count of Lost Books recovered to 18, even if you previously had found more, and brings up a window with the contents of the specified book. Book IDs above 18 will show the message *There's a book missing here.*

## Command: `sn,secretNote`
- **Syntax**: `sn [I:noteID]`
- **Description**: Adds specified secret note to your inventory. If no ID is specified, a random unseen note will be added. See `Data/SecretNotes` for a list of IDs.

#### Mail
## Command: `allmail`
- **Syntax**: `allmail`
- **Description**: Queues every letter defined in `Data/mail` for delivery tomorrow.

## Command: `allmailread`
- **Syntax**: `allmailread`
- **Description**: Marks every letter defined in `Data/mail` as already read. They will all be accessible in the letters tab of the collections menu.

## Command: `broadcastmail`
- **Syntax**: `broadcastmail <S:mailID>`
- **Description**: Queues specified mail for delivery tomorrow for all players. The ID is case-sensitive; see `Data/mail` for valid IDs in the base game.

## Command: `broadcastmailbox`
- **Syntax**: `broadcastmailbox <S:mailID>`
- **Description**: Immediately adds specified mail to all players' mailboxes. The ID is case-sensitive; see `Data/mail` for valid IDs in the base game.

## Command: `clearmail`
- **Syntax**: `clearmail`
- **Description**: Clears records of all received mail **(including hidden progress flags)**. This also clears the letters tab in the collections menu.

## Command: `mailfortomorrow,mft`
- **Syntax**: `mailfortomorrow <S:mailID>[noletter]`
- **Description**: Queues specified mail for delivery tomorrow. The ID is case-sensitive, and any zeros in the given ID will be replaced with underscores. See `Data/mail` for valid IDs (after replacement) in the base game. Because of the zero replacement, some letters (*e.g.,* `quest10`) are inaccessible with this command; broadcastmail may be a useful alternative in those cases. If the second parameter is present (with any value), the "noletter" flag is set, which will add the mail to your mail received list without showing a "new mail" indicator.

## Command: `seenmail`
- **Syntax**: `seenmail <S:mailID>[B:addOrRemove]`
- **Description**: Marks specified mail as already received. The ID is case-sensitive; see `Data/mail` for valid IDs in the base game. You can remove a mail received (instead of adding it) by setting the second argument to false, like `seenMail <code><id>` false</samp>.

## Command: `showmail`
- **Syntax**: `showmail <S:mailID>`
- **Description**: Brings up the letter-reading window with the specified mail. The ID is case-sensitive; see `Data/mail` for valid IDs in the base game. If a match cannot be found, a blank window will briefly display and an ArgumentOutOfRange error will be triggered. Does not set the letter as received or cause it to show in the letter tab of the collections menu.
- **Example**:
`debug showmail SeaAmulet` will show the letter Lewis sends about Mermaid's Pendants.

#### Quests and Special Orders
## Command: `clearquests`
- **Syntax**: `clearquests`
- **Description**: Removes all quests from your journal/quest log.

## Command: `collectquest`
- **Syntax**: `collectquest`
- **Description**: Starts a new random resource Gathering quest. If used multiple times on the same game day, the quest will always be the same.

## Command: `completespecialorders,cso`
- **Syntax**: `completespecialorders`
- **Description**: Completes all objectives for all currently active Special Orders and Qi Challenges. Note that this command does not remove the associated journal entries.

## Command: `completequest`
- **Syntax**: `completequest <I:questID>`
- **Description**: Completes specified quest and removes it from your journal. See `Data/Quests` for a list of IDs.

## Command: `deliveryquest`
- **Syntax**: `deliveryquest`
- **Description**: Starts a new random item Delivery quest. If used multiple times on the same game day, the quest will always be the same.

## Command: `getallquests`
- **Syntax**: `getallquests`
- **Description**: Starts every quest from `Data/Quests` that you don't already have.

## Command: `ordersboard`
- **Syntax**: `ordersboard`
- **Description**: Shows the Special Orders quest board.

## Command: `qiboard`
- **Syntax**: `qiboard`
- **Description**: Shows the Qi Challenges quest board.

## Command: `quest`
- **Syntax**: `quest <I:questID>`
- **Description**: Starts the specified quest. See `Data/Quests` for a list of IDs.

## Command: `quests`
- **Syntax**: `quests`
- **Description**: Starts every quest from `Data/Quests` that you don't already have as well as a random item Delivery quest and a random Slay Monster quest.

## Command: `removequest`
- **Syntax**: `removequest <I:questID>`
- **Description**: Silently removes specified quest from your journal. See `Data/Quests` for a list of IDs.

## Command: `slayquest`
- **Syntax**: `slayquest`
- **Description**: Starts a new random Slay Monster quest. If used multiple times on the same game day, the quest will always be the same.

## Command: `specialorder`
- **Syntax**: `specialorder <S:questID>`
- **Description**: Starts the Special Order (either town or Qi Challenge) with the specified ID. See `Data/SpecialOrders` for a list of IDs.

### NPCs
#### Children
## Command: `child,kid`
- **Syntax**: `child`
- **Description**: If you have children, advances the age of the first child to the next stage. Otherwise, spawns a new child named "Baby" with gender and skin tone randomly chosen; the child will start at stage 3 (crawling) and may initially spawn out of bounds. You do not need to be married or have an upgraded house to use this command.

## Command: `child2`
- **Syntax**: `child2`
- **Description**: If you have multiple children, advances the age of the second child to the next stage. Otherwise, spawns a new child named "Baby2" with gender and skin tone randomly chosen; the child will start at stage 3 (crawling) and may initially spawn out of bounds. You do not need to be married or have an upgraded house to use this command.

## Command: `clearchildren`
- **Syntax**: `clearchildren`
- **Description**: Removes all your children.

## Command: `pregnant`
- **Syntax**: `pregnant`
- **Description**: Sets a new baby to be born/adopted the next day. You may need to be already married (and have a house with a nursery) for this to work.

#### Spawning and removal
## Command: `addkent`
- **Syntax**: `addkent`
- **Description**: Spawns Kent if after year 1.

## Command: `characterinfo`
- **Syntax**: `characterinfo`
- **Description**: Displays a global message listing how many NPCs are in the current location.

## Command: `clearcharacters`
- **Syntax**: `clearcharacters`
- **Description**: Removes all NPCs who are in the current location.

## Command: `clone`
- **Syntax**: `clone <S:npcName>`
- **Description**: Clones specified NPC and places the copy in the current location. Name is a fuzzy match.
**Warning: cloning `farmer` will crash the game.**

## Command: `killall`
- **Syntax**: `killall <S:npcName>`
- **Description**: Removes all NPCs except for the specified character. Name is an exact match, and they are only spared from removal if they are in the current location. Will also remove NPCs inside constructed buildings.

## Command: `killnpc,removenpc`
- **Syntax**: `killnpc <S:npcName>`
- **Description**: Removes specified NPC from the game, checking all game locations and buildings. Name is an exact match. Command will output a message to the console stating whether or not the NPC was found and removed.

#### Relationships
## Command: `dating`
- **Syntax**: `dating <S:npcName>`
- **Description**: Sets your relationship status with specified NPC to `Dating`; *i.e.,* marks them as having been given a bouquet. Name is an exact match.

## Command: `divorce`
- **Syntax**: `divorce`
- **Description**: Queues your farmer to divorce their spouse overnight. The spouse room may not be fully removed until you have slept and/or returned to title.

## Command: `engaged`
- **Syntax**: `engaged <S:npcName>`
- **Description**: Increases your friendship with specified NPC by 2500 points (10 hearts) and sets relationship status to `Engaged` with a wedding for the next day. Name is an exact match.

## Command: `friendall`
- **Syntax**: `friendall [I:value]`
- **Description**: Increases friendship with all socializable NPCs by the specified amount. If no amount is given, the increase will be 2500 points (10 hearts). All normal point caps are in place, so a bachelor you aren't dating will not increase past 8 hearts. Previously unmet NPCs will also be marked as met and have friendship increased with the following exceptions:
<ul><li>Dwarf will be marked as met but not change friendship if you don't have the *Dwarven Translation Guide*.</li>
<li>Kent will not be included if you are still in year 1.</li>
<li>Krobus will not be included if they have not yet been met in game.</li></ul>

## Command: `friendship,friend`
- **Syntax**: `friendship <S:npcName><I:value>`
- **Description**: Sets friendship with specified NPC to specified value. This is a fuzzy match, and they will be marked as met if previously unmet.

## Command: `invitemovie`
- **Syntax**: `invitemovie <S:npcName>`
- **Description**: Invites specified NPC to see a movie today. This is a fuzzy match and you will still need your own ticket to enter the theatre.

## Command: `makeex`
- **Syntax**: `makeex <S:npcName>`
- **Description**: Sets your relationship status with specified NPC to `Divorced`, removing any marriage or bouquet flag and listing them as *ex-husband* or *ex-wife.* Name is an exact match.

## Command: `marry`
- **Syntax**: `marry <S:npcName>`
- **Description**: <p>Increases your friendship with the specified NPC by 2500 points (10 hearts), and sets your relationship status to `Married` with an anniversary of today. Name is a fuzzy match.</p>

Notes:
- If you haven't upgraded your farmhouse, also run `debug houseUpgrade 1` to avoid errors.
- The spouse room generally appears empty on the first day and you'll need to sleep for it and your spouse to spawn properly.

For non-vanilla marriage candidates:
<ul><li>Trying to `debug marry Krobus` will work, but will set him as a spouse rather than a roommate. Most of his dialogue and actions remain the same, but he will use some hardcoded spouse lines where he would otherwise have his own version, have a heart instead of a smiley face for his hug/kiss sprite, and the social menu will list him as your spouse. It's unclear if you're able to have children this way.</li>
<li>For other non-marriageable villagers, an empty spouse room will spawn and the NPC will move in. They will follow a basic hardcoded spouse schedule and can be kissed (although they won't have a kiss sprite), but they will rarely have dialogue.</li></ul>

## Command: `wedding`
- **Syntax**: `wedding <S:npcName>`
- **Description**: <p>Sets the specified NPC as your spouse and queues a wedding for today. Name is an exact match. If the specified NPC is not normally marriageable, the wedding will still occur but they will be invisible on the wedding day.</p>

Tip: if you want to test a normal wedding, you can run these commands and then give them the pendant (replacing Abigail with the NPC's internal name):

```text
debug houseUpgrade 2
debug friendship Abigail 2500
debug item (O)460
debug warpToCharacter Abigail
```

#### Dialogue
## Command: `clearactivedialogueevents`
- **Syntax**: `clearactivedialogueevents`
- **Description**: Clears all active conversation topics.

## Command: `db,speakto`
- **Syntax**: `db <S:npcName>`
- **Description**: Shows a dialogue box with the current dialogue for the specified NPC. Name is a fuzzy match and will default to Pierre if not supplied. This does count as having spoken to that NPC today, and if they don't have any more dialogue right now, the message *Stack empty* will be output to the console.

## Command: `dialogue,adddialogue`
- **Syntax**: `dialogue <S:npcName><S:dialogueString>`
- **Description**: Sets the dialogue for the specified character to the specified string. Name is a fuzzy match, and NPC names containing spaces should be quoted (e.g. `debug dialogue "Some NPC" Some dialogue text$h`. The dialogue string should start with a zero and everything after will be parsed. It can include tokens such as `@` for the farmer name and portrait commands; see Dialogue for format specifics.
- **Example**:
`debug dialogue Abi 0Hi @! Modding is fun!$h` would set Abigail's next dialogue to be *Hi (FarmerName)! Modding is fun!* with her happy portrait.

## Command: `loaddialogue`
- **Syntax**: `loaddialogue <S:npcName><S:dialogueKey>`
- **Description**: Sets the dialogue for the specified character using the specified asset key. Name is a fuzzy match. Key format appears to be `file:key` but exact specifics are unknown. Curly braces in the key (`{`, `}`) will be converted to angle brackets (`<`, `>`).

## Command: `phone`
- **Syntax**: `phone`
- **Description**: Brings up the Telephone menu.

## Command: `question`
- **Syntax**: `question <I:questionID>[B:answerOrForget]`
- **Description**: Marks the specified dialogue question as answered. You can forget a selected answer (instead of adding it) by setting the second argument to false, like `question <code><id>` false</samp>.

## Command: `showtextabovehead, sb`
- **Syntax**: `showtextabovehead <S:npcName>`
- **Description**: Shows a speech bubble saying *"Hello! This is a test"* above the specified NPC's head. Name is a fuzzy match.

## Command: `speech`
- **Syntax**: `speech <S:npcName><S:text>`
- **Description**: Displays a dialogue box for the specified character saying the specified string. Name is a fuzzy match and names with spaces should be in double quotes. All text after the NPC name is part of the text to display. The text can include tokens such as `@` for the farmer name and portrait commands; see Dialogue for format specifics. Useful for testing dialogue changes.
- **Example**:
`debug speech Abi 0Hi @! Modding is fun!$h` would open a dialogue box of Abigail saying *Hi (FarmerName)! Modding is fun!* with her happy portrait.

#### Movement and warping
## Command: `facedirection, face, fd`
- **Syntax**: `facedirection <S:npcName><I:direction>`
- **Description**: Sets specified character to face the specified direction. Name is a fuzzy match and also accepts `farmer`. See Event data for the valid directions.

## Command: `faceplayer`
- **Syntax**: `faceplayer <S:npcName>`
- **Description**: Sets specified character to face towards the player. Name is a fuzzy match.

## Command: `hurry`
- **Syntax**: `hurry <S:npcName>`
- **Description**: Warps specified character to the endpoint of their current schedule entry. Name is a fuzzy match.
- **Example**:
`debug hurry pam` would cause Pam to immediately warp to the bus if entered during her morning walk after the bus has been repaired.

## Command: `jump`
- **Syntax**: `jump <S:npcName>[F:velocity]`
- **Description**: Makes specified character jump with the specified velocity. Name is a fuzzy match and also accepts `farmer`. Velocity is a float and defaults to 8.0 if not supplied, which results in a jump of about half the height of the player character.

## Command: `warpcharacter, wc`
- **Syntax**: `warpcharacter <S:npcName><I:X><I:Y>[I:facingDirection]`
- **Description**: Warps specified character to the given coordinates on the current map. Name is a fuzzy match. See Event data for the valid directions; the default is `2`. Note: if you do not include enough parameters, an error message will be printed to the console which incorrectly states the default facing direction is `1`.

## Command: `warpcharacterto, wct`
- **Syntax**: `warpcharacterto <S:npcName><S:locationName><I:X><I:Y>[I:facingDirection]`
- **Description**: Warps specified character to the given coordinates on the specified map. Character name is a fuzzy match, but location is exact. See Event data for the valid directions; the default is `2`. Note: if you do not include enough parameters, an error message will be printed to the console which incorrectly states the default facing direction is `1`.
- **Example**:
`debug wct robin Farm 69 16` would warp Robin to just right of the mailbox on the Farm map, facing downwards.

## Command: `warpcharactertome,wctm`
- **Syntax**: `warpcharactertome <S:npcName>`
- **Description**: Warps the specified character directly to you; name is a fuzzy match.

## Command: `whereis,where`
- **Syntax**: `whereis <S:npcName>`
- **Description**: Outputs the location and coordinates of the specified character to the SMAPI console. Name is a fuzzy match, so the command will return all matching NPCs.

#### Farm animals
## Command: `animal`
- **Syntax**: `animal <S:type>`
- **Description**: Adds a new baby animal of the specified type to the Farm. The animal will have a random name and be assigned to the correct type of building (if there is room). The command must be run while the farmer is not inside the building or the animal will not be spawned. Type is a case-sensitive match with spaces allowed. Valid types for the base game are listed below.
<ul>
<li>`Blue Chicken`</li>
<li>`Brown Chicken`</li>
<li>`Void Chicken`</li>
<li>`White Chicken`</li>
<li>`Brown Cow`</li>
<li>`White Cow`</li>
<li>`Dinosaur`</li>
<li>`Duck`</li>
<li>`Goat`</li>
<li>`Ostrich`</li>
<li>`Pig`</li>
<li>`Rabbit`</li>
<li>`Sheep`</li>
</ul>

## Command: `animalinfo`
- **Syntax**: `animalinfo`
- **Description**: Displays a global message with the count of the total number of farm animals.

## Command: `befriendanimals`
- **Syntax**: `befriendanimals [I:amount]`
- **Description**: Sets friendship of all animals who live (and are currently present) in the current location to the given amount. Default is 1000 (max).

## Command: `displaceanimals`
- **Syntax**: `displaceanimals`
- **Description**: Goes through all buildings that can contain animals. For each animal in each building, tries to move the animal outside the building. Finally, clears the building of all animals that live there.

## Command: `fixanimals`
- **Syntax**: `fixanimals`
- **Description**: Goes through all buildings that can contain animals and removes entries for animals which no longer live in that building.

## Command: `growanimals`
- **Syntax**: `growanimals`
- **Description**: Sets all animals who live in the current location to day 1 of adulthood, unless they are already adults. May cause each of them to eat hay.

## Command: `pauseanimals`
- **Syntax**: `pauseanimals`
- **Description**: Pauses all farm animals in the current location indefinitely. Exiting and re-entering may cause them to be randomly moved to a new spot, but they will remain paused.

## Command: `unpauseanimals`
- **Syntax**: `unpauseanimals`
- **Description**: Unpauses all farm animals in the current location.

## Command: `warpanimaltome,watm`
- **Syntax**: `warpanimaltome <S:animalName>`
- **Description**: Warps the specified farm animal to you; name is a case-insensitive fuzzy search but will only work in a location that allows animals.

#### Pets, horses, and monsters
## Command: `befriendpets`
- **Syntax**: `befriendpets`
- **Description**: Gives the player 1000 friendship points with all pets.

## Command: `cat`
- **Syntax**: `cat <I:X><I:Y>[I:breed]`
- **Description**: Spawns a new Cat at the given coordinates of the current location. Breed can be `0`, `1`, `2`, `3`, `4`, or the `Id` of a custom breed added by a mod, and determines which texture to use. This is an additional pet and does not replace any current pet(s).

## Command: `changepet`
- **Syntax**: `changepet <S:petName><S:type>[S:breedId]`
- **Description**: Changes the type and breed of an existing pet on the farm with name `petName`. `type` should match an ID in `Data/Pets`. Defaults to the first pet breed if none is specified.

## Command: `createdino`
- **Syntax**: `createdino`
- **Description**: Spawns a Pepper Rex just to the right of your farmer.

## Command: `dog`
- **Syntax**: `dog <I:X><I:Y>[I:breed]`
- **Description**: Spawns a new Dog at the given coordinates of the current location. Breed can be `0`, `1`, `2`, `3`, `4`, or the `Id` of a custom breed added by a mod and determines which texture to use. This is an additional pet and does not replace any current pet(s).

## Command: `horse`
- **Syntax**: `horse <I:X><I:Y>`
- **Description**: Spawns a new Horse at the given coordinates of the current location. Horse may disappear after dismounting if there is no stable for it.

## Command: `killallhorses`
- **Syntax**: `killallhorses`
- **Description**: Removes all horses from all locations.

## Command: `monster`
- **Syntax**: `monster <S:type><I:X><I:Y>[I:facingDirection]`
- **Description**: Spawns a monster of the specified type at the given coordinates of the current location. Only certain monster types seem to work. Known valid types include `Bat`, `DinoMonster`, `DustSpirit`, `Fly`, `Ghost`, `GreenSlime`, `Grub`, `LavaCrab`, `Mummy`, `RockCrab`,  `RockGolem`,  `Serpent`,  `ShadowBrute`,  `ShadowShaman`,  `Skeleton`,  and `SquidKid`.  `Duggy` may also work depending on the terrain.

## Command: `owl`
- **Syntax**: `owl`
- **Description**: Spawns an Owl in the current location.

## Command: `pettofarm`
- **Syntax**: `pettofarm`
- **Description**: If it is not raining, warps your pet to the pet bowl location on the Farm (Tech note: Location is initially set by checking for tile 1938 on Building layer). If it is raining, warps pet to the FarmHouse. Only works for host in multiplayer.

## Command: `setpreferredpet`
- **Syntax**: `setpreferredpet <S:type>[S:breedId]`
- **Description**: Sets the player's preferred pet type and breed. `type` should match an ID in `Data/Pets`. Defaults to the first pet breed if none is specified.

## Command: `togglecatperson`
- **Syntax**: `togglecatperson`
- **Description**: Toggles your farmer's chosen pet preference between cat and dog. If you already have a pet, the inventory graphic will switch but the pet themselves will not be affected.

### Festivals and events
## Command: `endevent`
- **Syntax**: `endevent`
- **Description**: Immediately end the current event or festival, applying the event's skip logic (if any). The event is marked seen, but you can rewatch it using the `eventById` command if needed.

## Command: `event`
- **Syntax**: `event <S:locationName><I:index>[S:clearEventsSeen]`
- **Description**: Starts the specified event in the specified location. The location name is a fuzzy match, but the second parameter is an index rather than an ID. This is basically a zero-based count of the item definitions in the appropriate data file, and since these definitions can be altered by mods this a difficult command to use. Because of this and the warning below, ebi is often the better choice.
**Warning: The third parameter is default to true so this will clear the eventsSeen list unless a third parameter is specified.**

## Command: `eventbyid,ebi`
- **Syntax**: `eventbyid <I:eventID>`
- **Description**: Starts the specified event. This does take an event ID; events which have prerequisites of other events might not start if those prerequisites have not been seen.
- **Example**:
`debug ebi 992559` would trigger the event where Emily visits the farm and gives you access to her sewing machine.

## Command: `eventover`
- **Syntax**: `eventover`
- **Description**: Ends (and restarts) the current event. Seems to be essentially equivalent to ee.

## Command: `eventseen, seenevent`
- **Syntax**: `eventseen <I:eventID>[B:seeOrForget]`
- **Description**: Marks specifid event as seen by your farmer. Useful for enabling access to event-dependent areas or events. You can forget an event (instead of adding it) by setting the second argument to false, like `seenEvent <code><id>` false</samp>.

## Command: `festival`
- **Syntax**: `festival <S:festivalID>`
- **Description**: Starts the specified festival ID. The season, day, and time will be set to match the starting time, and you will be warped to the correct location. Valid IDs are listed below.
<table class="wikitable"><tr><td>`spring13` - Egg Festival</td><td>`fall16` - Stardew Valley Fair</td></tr>
<tr><td>`spring24` - Flower Dance</td><td>`fall27` - Spirit's Eve</td></tr>
<tr><td>`summer11` - Luau</td><td>`winter8` - Festival of Ice</td></tr>
<tr><td>`summer28` - Moonlight Jellies</td><td>`winter25` - Winter Star</td></tr></table>

## Command: `festivalscore`
- **Syntax**: `festivalscore <I:value>`
- **Description**: Adds the specified value to the festival score. The festival score has different meanings depending on which festival is active and includes the egg count at the Egg Hunt, number of fish caught at the Ice Festival, and star token count at the fall Fair.

## Command: `runtestevent,rte`
- **Syntax**: `runtestevent`
- **Description**: Runs an event from the file `test_event.txt` in the root game folder. The first line of the file should specify the location where the event takes place, and the rest of the file should be the same as a normal event script except that line breaks will be treated as `/` delimiters.
**Note: this file must use CRLF (Windows-style) line breaks, or it will fail to parse.** If you are on Mac or Linux, make sure you convert when saving (any text editor should be able to do this).

## Command: `setFarmEvent,sfe`
- **Syntax**: `setFarmEvent <S:eventID>`
- **Description**: Queue an overnight farm event if one doesn't plan naturally instead. The <event id> can be one of...
- `dogs`;
- `earthquake`;
- `fairy`;
- `meteorite`;
- `owl`;
- `racoon` (not a typo);
- `ufo`;
- `witch`.

Note that even if the farm event runs, it may exit without doing anything (e.g. rare events like `ufo` have extra condition checks when the start).

### Minigames and cutscenes
## Command: `boatjourney`
- **Syntax**: `boatjourney`
- **Description**: Plays the extended cutscene of the first boat trip to Ginger Island. The player will be warped to the Island Docks when the cutscene ends.

## Command: `crane`
- **Syntax**: `crane`
- **Description**: Starts the Crane Game minigame from the Movie Theater populated with prizes based on which movie is (or would be) showing for the current month.

## Command: `darts`
- **Syntax**: `darts`
- **Description**: Starts the Darts minigame from the Pirate Cove.

## Command: `fish`
- **Syntax**: `fish <I:fishID>`
- **Description**: Starts the fishing minigame with the specified fish hooked and a treasure chest available. You must have a fishing line already cast into the water before entering the command in order to actually receive the fish after completing the minigame. Additional "hits" may trigger during the game, and none of the fishing pole animations will play. See object data for a list of valid IDs; non-fish objects can be used and caught.

## Command: `minegame`
- **Syntax**: `minegame [S:mode]`
- **Description**: Starts the Junimo Kart minigame. If the second argument is `infinite` the game will play *Endless* mode; if it is anything else (or missing), the game will play *Progress* mode.

## Command: `minigame`
- **Syntax**: `minigame <S:which>`
- **Description**: Starts the specified minigame or cutscene. Valid choices are listed below.
<ul>
<li>`blastoff` - starts the *Robot Blastoff* cutscene which plays during Maru's 10-heart event.</li>
<li>`cowboy` - starts the Journey of the Prairie King minigame.</li>
<li>`fishing` - starts the fishing minigame from the Stardew Valley Fair. Score does not seem to be kept although the timer does count down; once the timer runs out the farmer will be warped to coordinates (24, 71) on the current map.</li>
<li>`grandpa` - starts the *Grandpa Story* cutscene of which plays at the start of a new game.</li>
<li>`haleyCows` - starts the *Haley Cow Pictures* cutscene which plays during Haley's 8-heart event.</li>
<li>`marucomet` - starts the *Maru Comet* cutscene which plays during Maru's 14-heart event.</li>
<li>`minecart` - starts the Junimo Kart minigame in *Progress* mode.</li>
<li>`plane` - starts the *Plane Fly-by* cutscene which plays during Harvey's 8-heart event.</li>
<li>`slots` - starts the *Calico Spin* Slot Machine minigame.</li>
<li>`target` - starts the Slingshot minigame from the Stardew Valley Fair. When the game is over the farmer will be warped to coordinates (24, 63) on the current map.</li>
</ul>

## Command: `movieSchedule`
- **Syntax**: `movieSchedule [I:year]`
- **Description**: Lists the movies that will play in the given year (default this year), with the dates they'll play.

## Command: `test`
- **Syntax**: `test`
- **Description**: Starts the *Test* minigame. This brings up a window showing a variety of flooring textures; left-click closes the window.

### Shops
## Command: `exportShops`
- **Syntax**: `exportShops`
- **Description**: Export a summary of what's in every shop in the game, taking into account their current conditions. This is saved to a file on disk, and the file path shown in the console.

## Command: `shop`
- **Syntax**: `shop <shopID>[ownerName]`
- **Description**: Open a shop defined in `Data/Shops`, regardless of whether one of the owners is nearby. Specifying [ownerName] will use that NPC, otherwise the command will find the closest valid NPC if possible (else open with no NPC).

### Locations
#### Terrain, trees, and crops
## Command: `artifactSpots`
- **Syntax**: `artifactSpots`
- **Description**: Spawn an artifact spot in each empty tile around the player.

## Command: `clearfarm`
- **Syntax**: `clearfarm`
- **Description**: Removes nearly everything from the Farm map such as grass, trees, debris, paths, and placed objects (including working machines and filled chests.)

## Command: `dayupdate`
- **Syntax**: `dayupdate [I:number]`
- **Description**: Runs the DayUpdate for the current location the specified number of times. If the number of updates is not specified, it will default to 1. This simulates days passing for some things such as grass and fruit trees growing or fish reproducing in ponds. Other things may not progress the full amount; for example crop growth only advances one day because sprinklers will not be triggered and the growcrops command should be used for that instead.

## Command: `fruittrees`
- **Syntax**: `fruittrees`
- **Description**: Adds a month of growth to all fruit trees in the current location, causing even newly-planted saplings to instantly mature.

## Command: `grass`
- **Syntax**: `grass`
- **Description**: Spawns long grass on all available tiles in the current location.

## Command: `growcrops`
- **Syntax**: `growcrops <I:number>`
- **Description**: Grows all crops in the current location the specified number of days.

## Command: `growgrass`
- **Syntax**: `growgrass <I:number>`
- **Description**: Grows long grass the specified number of times in the current location. This will cause already-placed grass to spread but will not necessarily create grass in clear areas.

## Command: `growwildtrees`
- **Syntax**: `growwildtrees`
- **Description**: Grows all wild trees (such as Oak) in the current location to maturity. This command may be broken as it can de-age fully grown trees, even ones with existing tappers.

## Command: `localinfo`
- **Syntax**: `localinfo`
- **Description**: Outputs counts of grass, trees, other terrain features, objects, and temporary sprites for the current location. May be broken.

## Command: `mushroomtrees`
- **Syntax**: `mushroomtrees`
- **Description**: Turns all wild trees in the current location into mushroom trees.

## Command: `r,resetForPlayerEntry`
- **Syntax**: `r`
- **Description**: Resets current location which essentially simulates the player leaving and reentering. Most noticeable effect is the restarting of music tracks.

## Command: `removedebris`
- **Syntax**: `removedebris`
- **Description**: Removes all dropped items from the current location.

## Command: `removedirt`
- **Syntax**: `removedirt`
- **Description**: Removes (*i.e.,* untills) all tilled dirt in the current location.
**Warning: this includes removing any currently-planted crops (including fully-grown ones).**

## Command: `removelargetf,removeLargeTerrainFeature`
- **Syntax**: `removelargetf`
- **Description**: Removes all large terrain features (such as bushes) from the current location.

## Command: `removeterrainfeatures,removetf`
- **Syntax**: `removeterrainfeatures`
- **Description**: Removes all (small) terrain features such as grass and tilled dirt from the current location.
**Warning: this includes removing any currently-planted crops (including fully-grown ones).**

## Command: `spawnweeds`
- **Syntax**: `spawnweeds <I:number>`
- **Description**: Spawns weeds the specified number of times. This will cause already-placed weeds to spread but will not necessarily create new weeds in clear areas.

## Command: `spreaddirt`
- **Syntax**: `spreaddirt`
- **Description**: Tills all unoccupied diggable tiles in the current location.

## Command: `spreadseeds`
- **Syntax**: `spreadseeds <I:seedID>`
- **Description**: Plants specified seed in all tilled dirt in the current location. The argument is the object ID for the seed item. Out of season crops can be planted this way but will not survive outside of the greenhouse.
**Warning: this will replace any currently-planted crops (including fully-grown ones) with the new seeds.**
- **Example**:
`debug spreadseeds 472` would plant parsnips on all hoed dirt tiles.

## Command: `water`
- **Syntax**: `water`
- **Description**: Waters all tilled soil on the current map.

## Command: `watercolor`
- **Syntax**: `watercolor <I:R><I:G><I:B>`
- **Description**: Tints the water color for the current location. The parameters are red, green, and blue components and the actual RGBA color used will be (R/2, G/2, B/2, 127). This affects fishing ponds as well as lakes, rivers, etc., but the effect is temporary and the color will reset to normal if you leave and re-enter the map.

## Command: `whereore`
- **Syntax**: `whereore`
- **Description**: Outputs (to the SMAPI console) the coordinates of any "shiny spots" suitable for panning on the current map. Will output `{X:0 Y:0}` if there are no active panning spots.

#### Objects and lights
## Command: `clearfurniture`
- **Syntax**: `clearfurniture`
- **Description**: Removes all furniture from the current location. Can be used in a farmhouse/cabin, or outside the farmhouse as well.

## Command: `clearlightglows`
- **Syntax**: `clearlightglows`
- **Description**: Removes all light glows from the current location.

## Command: `fencedecay`
- **Syntax**: `fencedecay <I:amount>`
- **Description**: Ages all fences in the current location the specified amount of days.
- **Example**:
`debug fencedecay 60` would age all fences by 60 days which would break any basic Wood Fences (their base health is 54-58 days).

## Command: `fillwithobject`
- **Syntax**: `fillwithobject <I:itemID>[S:isBigCraftable]`
- **Description**: Places the specified item on all open tiles in the current location. The first argument is the object or big craftable ID. If the second argument is "`true`", the ID will be interpreted as a craftable, but if it is anything else (or missing) the ID will be interpreted as an object. Note that many objects spawned this way cannot be easily removed.
- **Example**:
`debug fillwithobject 13 true` would fill the map with furnaces.

## Command: `readyforharvest,rfh`
- **Syntax**: `readyforharvest <I:X><I:Y>`
- **Description**: Sets the machine at the specified coordinates to finish processing at the next clock update. If used to target a rock in the mine, quarry, etc. the rock's health will be reduced such that it only needs 1 more hit to break. A mod such as [Debug Mode](https://www.nexusmods.com/stardewvalley/mods/679) may be useful for getting coordinates.

## Command: `listLights`
- **Syntax**: `listLights`
- **Description**: Show debug info about all currently active light sources.

## Command: `removefurniture`
- **Syntax**: `removefurniture`
- **Description**: Removes all furniture from the current location. Similar to clearfurniture but will also work in other decoratable locations such as sheds.

## Command: `removelights`
- **Syntax**: `removelights`
- **Description**: Removes all light sources from the current location. This is temporary and they will be restored if the location is reset or re-entered.

## Command: `removeobjects`
- **Syntax**: `removeobjects`
- **Description**: Removes all placed objects from the current location. This includes things like fences, machines, and chests, but does not include flooring or long grass.

#### Farm buildings
## Command: `build`
- **Syntax**: `build <S:Name>[I:X][I:Y]`
- **Description**: Builds the specified building at the given coordinates. Names are case-insensitive, and will list fuzzy matches if an exact match isn't found. If the name includes spaces, quote them (e.g. `"Junimo Hut"`). If the coordinates are not specified, it will build just to the right of your farmer. While higher-level farm buildings such as Deluxe Barns can be immediately built this way, the incubator will be missing from Big or Deluxe Coops.
- **Example**:
`debug build "Stone Cabin"` would build a new Stone Cabin next to the player.

## Command: `buildcoop,bc`
- **Syntax**: `buildcoop <I:X><I:Y>`
- **Description**: Tries to build a new coop at the specified position in the current location. Finishes construction instantly.

## Command: `finishallbuilds,fab`
- **Syntax**: `finishallbuilds`
- **Description**: Finishes all buildings under construction. Only the host player can use this command in multiplayer.

## Command: `forcebuild`
- **Syntax**: `forcebuild <S:Name>[I:X][I:Y]`
- **Description**: Equivalent to `build`, but disables all safety checks so you can build in a location that wouldn't normally allow buildings, build on top of farm animals or placed objects, etc.
- **Example**:
`debug build "Stone Cabin"` would build a new Stone Cabin next to the player.

## Command: `movebuilding`
- **Syntax**: `movebuilding <I:sourceX><I:sourceY><I:destX><I:destY>`
- **Description**: Moves building in the current location from specified source coordinates to specified destination coordinates. The coordinates are the upper-left corner of the building's footprint. The [Lookup Anything](https://www.nexusmods.com/stardewvalley/mods/541) mod is one of the easier ways to get the source coordinates of a building; they are listed under `tileX` and `tileY` in the debug info (needs `ShowDataMiningFields` enabled.)

## Command: `paintBuilding,bpm`
- **Syntax**: `paintBuilding`
- **Description**: Gets the building the player is standing in front of and opens a paint menu for that building if it can be painted. If the player is not standing in front of a building, defaults to the main farmhouse.

## Command: `removebuildings`
- **Syntax**: `removebuildings`
- **Description**: Destroys all farm buildings. Animals within any of the buildings will also be removed, but animals which are outside will not be.

## Command: `skinbuilding,bsm`
- **Syntax**: `skinbuilding`
- **Description**: If the player is standing right under a building, open a menu to change the building appearance.

## Command: `spawncoopsandbarns`
- **Syntax**: `spawncoopsandbarns <I:number>`
- **Description**: Spawns the specified number of buildings. The game will randomly choose either a Deluxe Barn full of cows or a Deluxe Coop full of chickens for each (equal chance). Their locaions are also randomly chosen and the game will try 20 times to find a spot for each before giving up. The coops created by this command will not have incubators.

#### Farmhouse
## Command: `crib`
- **Syntax**: `crib <I:mapID>`
- **Description**: Sets the current crib style to the specified value. In the base game, valid values are `0` (no crib) or `1` (default crib). Additional styles may be possible through modding as the ID is appended to the map filename. For example, crib style 1 is specified by the file `Maps/FarmHouse_Crib_1`.

## Command: `floor`
- **Syntax**: `floor [I:textureID]`
- **Description**: Sets all floors of your farmhouse to the specified texture. Valid texture numbers are `0 - 55`; see Flooring for previews but note that the IDs used by the game are 1 less than the numbers used for the wiki filenames. If no texture is specified, the game will use the next ID after the current floor texture without checking for overflow which can create bugged textures.
- **Example**:
`debug floor 22` would set all flooring in the house to the white and grey checkerboard style.

## Command: `houseupgrade,house,hu`
- **Syntax**: `houseupgrade <I:upgradeLevel>`
- **Description**: Sets upgrade level of your farmhouse/cabin to the specified value. Valid values are `0 - 3`. Furniture and placed items will not be automatically moved and may wind up out of bounds. If done while the player is inside the house, warp points may not immediately update.

## Command: `logWallAndFloorWarnings`
- **Syntax**: `logWallAndFloorWarnings`
- **Description**: Enable debug logs when applying wallpaper and flooring to a farmhouse (or other decoratable location) to help troubleshoot cases where they don't work with a custom map. You'd usually enable this before loading the save.

## Command: `renovate`
- **Syntax**: `renovate`
- **Description**: Opens the farmhouse renovation menu.

## Command: `thishouseupgrade,thishouse,thu`
- **Syntax**: `thishouseupgrade <I:upgradeLevel>`
- **Description**: Equivalent to `houseupgrade`, but can be used to upgrade another player's house by running it from inside or just south of its exterior.

## Command: `upgradehouse`
- **Syntax**: `upgradehouse`
- **Description**: Increases the upgrade level of your farmhouse/cabin to the next level (max 3). Furniture and placed items will not be automatically moved and may wind up out of bounds. If done while the player is inside the house, warp points may not immediately update.

## Command: `wall,w`
- **Syntax**: `wall [I:textureID]`
- **Description**: Sets all walls of your farmhouse to the specified texture. Valid texture numbers are `0 - 111`; see Wallpaper for previews but note that the IDs used by the game are 1 less than the numbers used for the wiki filenames. If no texture is specified, the game will use the next ID after the current wallpaper texture without checking for overflow which can create bugged textures.
- **Example**:
`debug wall 21` would set all wallpaper in the house to the Joja style.

#### Special farm setups
## Command: `farmmap`
- **Syntax**: `farmmap [I:mapID]`
- **Description**: Removes the current farm map and farmhouse from the game and creates a new farm of the specified type. The farm will be named after the type (*e.g.,* "Standard Farm"). Valid types are: `0` (Standard), `1` (Riverland), `2` (Forest), `3` (Hilltop), `4` (Wilderness), `5` (Four Corners), `6` (Beach), and `7` (Meadowlands)

## Command: `setupbigfarm`
- **Syntax**: `setupbigfarm`
- **Description**: Clears the farm and then does the following:
  <ul><li>Spawns 3 Deluxe Coops full of chickens, 2 Deluxe Barns full of cows, a mill, a stable, and a silo in specific spots.</li>
  <li>Spawns a large field of random spring crops (force-grown for 8 days) with a wooden path through it and 4 junimo huts to the south.</li>
  <li>Spawns two large patches of grass and 18 random fruit trees (cherry and apricot)</li>
  <li>Spawns large groups of torches, furnaces, preserves jars, cheese presses, and kegs; the kegs are full of ancient fruit to make wine</li></ul>

## Command: `setupfarm`
- **Syntax**: `setupfarm [S:clearMore]`
- **Description**: Removes all farm buildings and completely clears large areas of the current farm. After this, the following things are done:
  <ul><li>Spawns a coop, barn, and silo in specific spots</li>
  <li>Your money is increased by 20000</li>
  <li>All tools are upgraded to Iridium quality</li>
  <li>Shears, a milk pail, and full stacks of wood, stone, wood fence, parsnip seeds, and bean starter are added to your inventory</li></ul>

## Command: `setupfishpondfarm`
- **Syntax**: `setupfishpondfarm [I:population]`
- **Description**: Clears farm and then builds up to 96 fish ponds randomly filled with various types of fish. The population of each of the ponds will be set to the specified value and defaults to 10. The ponds are built in a large 12 x 8 grid but will not be placed in a spot blocked by other buildings, animals, or map features.

#### Community center and bundles
## Command: `addjunimo,aj,j`
- **Syntax**: `addjunimo <I:X><I:Y><I:area>`
- **Description**: Adds a junimo at the specified coordinates and assigned to the given Community Center area. Valid areas are `0` (Pantry), `1` (Crafts Room), `2` (Fish Tank), `3` (Boiler Room), `4` (Vault), or `5` (Bulletin Board).

## Command: `allbundles`
- **Syntax**: `allbundles`
- **Description**: Marks all bundles complete.

## Command: `bundle`
- **Syntax**: `bundle <I:ID>`
- **Description**: Marks the specified bundle as complete. Valid IDs are listed below.
<table class="wikitable"><tr><th>Pantry</th><th>Fish Tank</th><th>Crafts Room</th><th>Boiler Room</th><th>Vault</th><th>Bulletin Board</th><th>Joja Ruins</th></tr>
<tr><td>0 - Spring Crops</td><td>6 - River Fish</td><td>13 - Spring Foraging</td><td>20 - Blacksmith's</td><td>23 - 2,500g</td><td>31 - Chef's</td><td>36 - Missing</td></tr>
<tr><td>1 - Summer Crops</td><td>7 - Lake Fish</td><td>14 - Summer Foraging</td><td>21 - Geologist's</td><td>24 - 5,000g</td><td>32 - Field Research</td><td></td></tr>
<tr><td>2 - Fall Crops</td><td>8 - Ocean Fish</td><td>15 - Fall Foraging</td><td>22 - Adventurer's</td><td>25 - 10,000g</td><td>33 - Enchanter's</td><td></td></tr>
<tr><td>3 - Quality Crops</td><td>9 - Night Fishing</td><td>16 - Winter Foraging</td><td></td><td>26 - 25,000g</td><td>34 - Dye</td><td></td></tr>
<tr><td>4 - Animal</td><td>10 - Specialty Fish</td><td>17 - Construction</td><td></td><td></td><td>35 - Fodder</td><td></td></tr>
<tr><td>5 - Artisan</td><td>11 - Crab Pot</td><td>19 - Exotic Foraging</td><td></td><td></td><td></td><td></td></tr></table>

## Command: `ccload`
- **Syntax**: `ccload <I:area>`
- **Description**: Removes the junimo note from and restores the specified area. Valid areas are `0` (Pantry), `1` (Crafts Room), `2` (Fish Tank), `3` (Boiler Room), `4` (Vault), or `5` (Bulletin Board).

## Command: `ccloadcutscene`
- **Syntax**: `ccloadcutscene <I:area>`
- **Description**: Plays the full restoration cutscene for the specified area including junimo dance and star retrieval. Valid areas are `0` (Pantry), `1` (Crafts Room), `2` (Fish Tank), `3` (Boiler Room), `4` (Vault), or `5` (Bulletin Board).

## Command: `completecc`
- **Syntax**: `completecc`
- **Description**: Adds all of the appropriate flags for Community Center completion and restores all areas.

## Command: `completejoja`
- **Syntax**: `completejoja`
- **Description**: Adds all of the appropriate flags for Joja membership and Community Development purchases.

## Command: `junimogoodbye`
- **Syntax**: `junimogoodbye`
- **Description**: Plays the animation where 6 junimos wave goodbye in front of the hut in the Community Center and then that corner of the Community Center gets restored.

## Command: `junimonote,jn`
- **Syntax**: `junimonote <I:area>`
- **Description**: Adds a junimo note (bundle scroll) for the specified area. Valid areas are `0` (Pantry), `1` (Crafts Room), `2` (Fish Tank), `3` (Boiler Room), `4` (Vault), or `5` (Bulletin Board).

## Command: `junimostar`
- **Syntax**: `junimostar`
- **Description**: Causes a junimo to run to the hut and retrieve a star which is then placed on the plaque above the fireplace. Must be done in the Community Center.

## Command: `plaque`
- **Syntax**: `plaque`
- **Description**: Adds a star to the plaque above the Community Center fireplace.

## Command: `resetjunimonotes`
- **Syntax**: `resetjunimonotes`
- **Description**: Resets all bundles.

## Command: `shufflebundles`
- **Syntax**: `shufflebundles`
- **Description**: Regenerates bundles using remixed bundle logic and without a specific random seed.

#### Other location-specific functions
## Command: `activatecalicostatue`
- **Syntax**: `activatecalicostatue`
- **Description**: Spawns and activates a new Calico Statue from the Desert Festival at point (8, 8) on the player's current mines level. If the player is in the regular mines instead of the Skull Cavern, the statue will not visually spawn but some animations and effects will still occur.

## Command: `beachbridge`
- **Syntax**: `beachbridge`
- **Description**: Toggles the state of the beach bridge between fixed and not fixed.

## Command: `ladder,shaft`
- **Syntax**: `ladder [I:X][I:Y]`
- **Description**: Creates a ladder or mineshaft at the specified coordinates. If no coordinates are given, it will spawn 1 tile north of the player. In the regular mines, both versions of the command will create a ladder. In the Skull Caverns, `ladder` will randomly spawn either a ladder or mineshaft while `shaft` will always spawn a mineshaft.

## Command: `minedifficulty,md`
- **Syntax**: `minedifficulty [I:difficultyLevel]`
- **Description**: Sets the difficulty of the mines to the specified level. In the base game, normal difficulty is `0` and the harder difficulty corresponding to the "Danger in the Deep" quest or Shrine of Challenge activation is `1`. Higher numbers can be used. If no difficulty level is specified, command will simply print the current difficulty level to the console.

## Command: `movie`
- **Syntax**: `movie [S:movieID][S:inviteNpcName]`
- **Description**: Starts a movie. The movie ID defaults to today's movie, and the NPC name can be omitted to watch the movie without an invitee. Note that this command can create a group with up to 3 guests instead of just the single guest allowed in normal play. Valid vanilla movie IDs are listed below.
<table class="wikitable"><tr><td>`spring_movie_0` - *The Brave Little Sapling*</td><td>`spring_movie_1` - *Natural Wonders*</td></tr>
<tr><td>`summer_movie_0` - *Journey of the Prairie King*</td><td>`summer_movie_1` - *Wumbus*</td></tr>
<tr><td>`fall_movie_0` - *Mysterium*</td><td>`fall_movie_1` - *It Howls in the Rain*</td></tr>
<tr><td>`winter_movie_0` - *Miracle at Coldstar Ranch*</td><td>`winter_movie_1` - *The Zuzu City Express*</td></tr></table>
To use the command multiple times in a row you must leave the theatre after each showing.
**Warning: This command also increases the save's `uniqueID` by 1 each time it is used, which will cause the save filename to change and impact some random events.**
- **Example**:
`debug movie` would show *It Howls in the Rain* with random NPC guests.;`debug movie spring_movie_1` would show *Natural Wonders* with random NPC guests.;`debug movie summer_movie_0 Abi` would show *Journey of the Prairie King* with Abigail and possibly 1 or 2 additional random NPC guests.

## Command: `pgb,printGemBirds`
- **Syntax**: `pgb`
- **Description**: Prints the Gem Bird Puzzle solution to the console.

## Command: `resetmines`
- **Syntax**: `resetmines`
- **Description**: Resets "permanent mine changes" such as coal carts and treasure chests. Does not affect mines level progress or monster eradication goals.

## Command: `returneddonations`
- **Syntax**: `returneddonations`
- **Description**: Opens the "returned donations" menu of the Lost and Found box from the Mayor's Manor.

## Command: `skullcavedifficulty,scd`
- **Syntax**: `skullcavedifficulty [I:difficultyLevel]`
- **Description**: Sets the difficulty of the Skull Cavern to the specified level. In the base game, normal difficulty is `0` and the harder difficulty corresponding to the "Skull Cavern Invasion" quest is `1`. Higher numbers can be used. If no difficulty level is specified, command will simply print the current difficulty level to the console.
- **Example**:
`debug scd 1` would activate hard mode Skull Cavern outside of the "Skull Cavern Invasion" quest.

## Command: `train`
- **Syntax**: `train`
- **Description**: Causes a train to spawn at the Railroad.

### World
#### Date and time
## Command: `addhour`
- **Syntax**: `addhour`
- **Description**: Increases time by 1 hour.

## Command: `addminute`
- **Syntax**: `addminute`
- **Description**: Increases time by 10 minutes.

## Command: `day`
- **Syntax**: `day <I:value>`
- **Description**: Changes day of the month to the specified value; stays in current season and adjusts `daysPlayed` statistic appropriately.

## Command: `pausetime`
- **Syntax**: `pausetime`
- **Description**: Toggles game pause state. This is different from the normal chat `/pause` command in that the player still has free movement during the pause.

## Command: `season`
- **Syntax**: `season <S:name>`
- **Description**: Sets season to the specified value. The season name is case-insensitive; valid names are `spring`, `summer`, `fall`, and `winter`.

## Command: `sleep, newday, nd`
- **Syntax**: `sleep`
- **Description**: Forces end of day.

## Command: `time`
- **Syntax**: `time <I:value>`
- **Description**: Sets current time to the specified value. This is essentially 24-hour time without a colon, although the stardew clock keeps running until 2600. See examples.
- **Example**:
`debug time 2040` would set the time to 20:40 or 8:40pm.; `debug time 2550` would set the time to 1:50am.

## Command: `year`
- **Syntax**: `year <I:value>`
- **Description**: Sets the current year to the specified value.

#### Weather and world state
## Command: `debrisweather`
- **Syntax**: `debrisweather`
- **Description**: Toggles "debris" weather (*i.e.,* windy weather with floating leaves) on and off. Does not change the weather icon on the HUD.

## Command: `greenrain`
- **Syntax**: `greenrain`
- **Description**: Toggles green rain weather on and off. Will turn off debris/windy weather. Does not change the weather icon on the HUD.

## Command: `morepollen`
- **Syntax**: `morepollen <I:amount>`
- **Description**: Increases the amount of leaves flying around if in debris/windy weather.

## Command: `rain`
- **Syntax**: `rain`
- **Description**: Toggles rainy weather on and off. Will turn off debris/windy weather. Does not change the weather icon on the HUD.

## Command: `resetworldstate`
- **Syntax**: `resetworldstate`
- **Description**: Clears all world state IDs which track map changes such as whether the beach bridge is fixed, whether Trash Bear has done his thing and various small changes from heart events.
**Warning: Also clears records of found artifacts and minerals, fish caught, events seen, and mail received (including hidden progress flags.)**

### Game settings and meta info
## Command: `conventionmode`
- **Syntax**: `conventionmode`
- **Description**: Toggles convention mode on and off. When on, disables the resolution and window mode options as well as both "Exit to Title" and "Exit to Desktop" buttons.

## Command: `filterLoadMenu`
- **Syntax**: `filterLoadMenu <searchText>`
- **Description**: Filter the current list of saves to those whose player name or farm name contains the given text.

## Command: `gamemode`
- **Syntax**: `gamemode <I:mode>`
- **Description**: Sets the game mode to the specified value. Details unknown.

## Command: `gamepad`
- **Syntax**: `gamepad`
- **Description**: Toggles gamepad control options and displays a global message about whether they are being used or not.

## Command: `inputsim,is`
- **Syntax**: `inputsim <S:type>`
- **Description**: Sets input simulator to the specified type. Valid types are `spamtool` and `spamlr`. Details unknown.

## Command: `language`
- **Syntax**: `language`
- **Description**: Brings up the language selection menu.

## Command: `musicvolume, mv, m`
- **Syntax**: `musicvolume <D:value>`
- **Description**: Sets music volume to the specified value. This is a double-precision float in the range of `0 - 1`.

## Command: `nosave, ns`
- **Syntax**: `nosave`
- **Description**: Toggles end of day saving on or off. Outputs a message to the console with the current saving status.

## Command: `performTitleAction, pta`
- **Syntax**: `performTitleAction <button>`
- **Description**: While on the title screen, jump to a given title submenu.

## Command: `save`
- **Syntax**: `save`
- **Description**: Toggles saving on a new day on/off. Plays a select sound if toggled on and a deselect sound if toggled off.

## Command: `showplurals`
- **Syntax**: `showplurals`
- **Description**: Prints (to the console) the plural forms of all items listed in `Data/ObjectInformation` and `Data/BigCraftablesInformation`.

## Command: `toggleCheats`
- **Syntax**: `toggleCheats`
- **Description**: Enable or disable entering debug commands into the in-game chat (prefixed with `/`).

## Command: `toggleNetCompression`
- **Syntax**: `toggleNetCompression`
- **Description**: Disable (or re-enable) multiplayer compression

## Command: `toggletimingoverlay, tto`
- **Syntax**: `toggletimingoverlay`
- **Description**: Show an on-screen overlay with basic timing info (e.g. draw loop times) to help with performance profiling.

## Command: `steaminfo,sdkinfo`
- **Syntax**: `steaminfo`
- **Description**: Outputs information about whether Steam is running and if a user is logged in.

## Command: `version`
- **Syntax**: `version`
- **Description**: Outputs the assembly version number to the console. Note this is different from the more user-friendly version that can be accessed from the credits screen. For example, running this command on Stardew Valley 1.4.3 for Windows will output `1.3.7286.33936`

## Command: `worldMapLines`
- **Syntax**: `worldMapLines`
- **Description**: Toggles the world map's debug view.

## Command: `worldMapPosition`
- **Syntax**: `worldMapPosition [includeLog]`
- **Description**: Show detailed info to help troubleshoot world map positioning data. If [includeLog] is `true`, it will print a detailed log of how the current position was determined based on the `Data/WorldMap` entries.

### Multiplayer
#### General multiplayer
## Command: `addotherfarmer`
- **Syntax**: `addotherfarmer`
- **Description**: Creates an additional new male farmer with randomized name and appearance which spawns 1 tile to the left of your farmer. Unsure of what situation this can be used in.

## Command: `nethost`
- **Syntax**: `nethost`
- **Description**: Starts a new LAN server. Details unknown.

## Command: `netjoin`
- **Syntax**: `netjoin <IP>`
- **Description**: Connect to a given IP address.

## Command: `split`
- **Syntax**: `split [I:playerIndex]`
- **Description**: Adds another split-screen multiplayer instance for the specified player index or starts split-screen mode otherwise.

## Command: `validatenetfields`
- **Syntax**: `validatenetfields`
- **Description**: Enables net field validation.

#### Logging
## Command: `logbandwidth`
- **Syntax**: `logbandwidth`
- **Description**: Toggles bandwidth logging on and off. Can be used on either host or client.

## Command: `logFile`
- **Syntax**: `logFile`
- **Description**: Begin writing debug messages to a log file at `%appdata%/StardewValley/ErrorLogs/game-latest.txt` to simplify troubleshooting. You can also enter `/logtext` into the in-game chatbox to enable it. This does nothing if SMAPI is installed, since the debug messages are already saved to SMAPI's log.

## Command: `logSounds`
- **Syntax**: `logSounds`
- **Description**: Log info about each sound effect played to the SMAPI console window.

## Command: `netclear`
- **Syntax**: `netclear`
- **Description**: Clears network message log.

## Command: `netdump`
- **Syntax**: `netdump`
- **Description**: Outputs network message log to a file.

## Command: `netlog`
- **Syntax**: `netlog`
- **Description**: Toggles network message logging on and off.

#### Player relationships
## Command: `dateplayer`
- **Syntax**: `dateplayer`
- **Description**: Checks all other farmers and sets the first one found as dating the player.

## Command: `engageplayer`
- **Syntax**: `engageplayer`
- **Description**: Checks all other farmers and sets the first one found as engaged to the player with a wedding date of the next game day.

## Command: `testwedding`
- **Syntax**: `testwedding`
- **Description**: Immediately play the wedding event. This requires the player to be married first - to test specific NPCs, `debug marry` the NPC followed by this command.

## Command: `marryplayer`
- **Syntax**: `marryplayer`
- **Description**: Checks all online farmers and sets the first one found as married to the player with a wedding date of the current game day.

#### Shared/split money
## Command: `changewallet`
- **Syntax**: `changewallet`
- **Description**: Sets the game to toggle between shared or split money overnight. Host only.

## Command: `mergewallets`
- **Syntax**: `mergewallets`
- **Description**: Immediately switches to shared money, merging all player wallets. Host only.

## Command: `separatewallets`
- **Syntax**: `separatewallets`
- **Description**: Immediately switches to split money, separating all player wallets. Host only.

### Audio and visual effects
#### Animations
## Command: `animationpreviewtool,apt`
- **Syntax**: `animationpreviewtool`
- **Description**: Opens a menu allowing you to preview different farmer animations and change some appearance options. Useful for event modding. A note that this *Only* works on the farmer. **Warning: previewing the "passOutTired" animation will make your farmer pass out as if it's 2 am and start a new day.**

## Command: `busdriveback`
- **Syntax**: `busdriveback`
- **Description**: Plays the animation of the bus returning from the desert. Must be done on the Bus Stop map.

## Command: `busdriveoff`
- **Syntax**: `busdriveoff`
- **Description**: Plays the animation of the bus leaving the Bus Stop and driving to the desert. Will warp the player to the desert. Must be done on the Bus Stop map.

## Command: `createsplash`
- **Syntax**: `createsplash`
- **Description**: Creates a fish "bubble" spot in front of the player. Seems inconsistent.

## Command: `framebyframe, fbf`
- **Syntax**: `framebyframe`
- **Description**: Turns on "frame-by-frame" mode which pauses the game and allows you to advance time/animations one frame at a time by hitting the `G` key. Hit `Escape` key to exit.

## Command: `frameoffset, fo`
- **Syntax**: `frameoffset <I:frameID><S:X><S:Y><S:??>`
- **Description**: Sets frame offset for specified frame and specified X & Y values. The X and Y can be made negative to flip the direction. Details unknown; reference `FarmerRenderer.featureXOffsetPerFrame()` and `FarmerRenderer.featureYOffsetPerFrame()`.

## Command: `setframe, sf`
- **Syntax**: `setframe <I:frameID>`
- **Description**: Sets farmer sprite to the given animation frame. Probably best used in conjunction with frame-by-frame mode.

## Command: `sprinkle`
- **Syntax**: `sprinkle`
- **Description**: Shows an animation of a sprinkle effect around the farmer. (It's the animation used while fairy-dusting a machine).

## Command: `toss`
- **Syntax**: `toss`
- **Description**: Shows an animation of a spinning vial/beaker which rises and then falls. Likely used in one of Maru's heart events.

#### Camera, lighting, and effects
## Command: `ambientlight, al`
- **Syntax**: `ambientlight <I:R><I:G><I:B>`
- **Description**: Sets the ambient light of the current location to the inverse of the specified RGB values. This is a temporary change and the light will revert if the location is reset or re-entered. Ex. inputting `255 0 0` will set the lighting to aqua blue, `0 255 255,` rather than red.

## Command: `panmode, pm`
- **Syntax**: `panmode [options]`
- **Description**: Turns on panmode. During panmode the screen can be panned with movement keys or mouse movements and `debug panmode` or `debug exit` will turn panmode off. You can also clear with `debug panMode clear` or set a time with `debug panMode {time}`. **Warning: Do not enter the backroom of Willy's shop while in this state as it will crash your game.**

## Command: `tls,toggleLightingScale`
- **Syntax**: `tls`
- **Description**: Toggles between scaled and unscaled lighting.

## Command: `viewport`
- **Syntax**: `viewport <I:X><I:Y>`
- **Description**: Sets the viewport to the given values measured in tiles. Details unknown.

## Command: `uiscale,us`
- **Syntax**: `uiscale <I:value>`
- **Description**: Sets the UI Scale level to the specified value. This is an integer interpreted as the scale percent. Can be used to go beyond the normal limits of 75 - 125 percent.
- **Example**:
`debug us 60` would set the UI scale level to 60%.

## Command: `zoomlevel,zl`
- **Syntax**: `zoomlevel <I:value>`
- **Description**: Sets the game zoom level to the specified value. This is an integer interpreted as the zoom percent. Can be used to go beyond the normal limits of 75 - 125 percent.
- **Example**:
`debug zl 60` would set the zoom level to 60%.

#### Audio
## Command: `playmusic`
- **Syntax**: `playmusic <s:cueID>`
- **Description**: Stops any current music and starts the specified music track cue ID. If the cue ID has a space, you can quote the ID like `playMusic "Crystal Bells"`. Tracks played this way will be added to the Jukebox list if applicable.
- **Example**:
`debug playmusic spring2` would play the track *Spring (The Valley Comes Alive)*.

## Command: `playsound, ps`
- **Syntax**: `playsound <s:cueID><I:pitch>`
- **Description**: Playes the specified sound effect cue ID. The pitch argument is optional and can take values from 1 (low pitch) to 2400 (high pitch) inclusively.
- **Example**:
`debug ps purchase` would make the coin clink sound heard when buying and selling items.

### Outdated, unimplemented, or unknown
## Command: `break`
- **Syntax**: `break`
- **Description**: Does absolutely nothing as the method handler has no body.

## Command: `eventtest`
- **Syntax**: `eventtest <S:locationName><I:eventIndex>`
- **Description**: Sets `Game1.eventTest` using the specified arguments. Defaults are "" and 0 respectively. This field is not accessed elsewhere.

## Command: `eventtestspecific`
- **Syntax**: `eventtestspecific <S:whichEvents>[...]`
- **Description**: Sets `Game1.eventTest` with the specified arguments. Arguments are interpreted as an array of strings. This field is not accessed elsewhere.

## Command: `lantern`
- **Syntax**: `lantern`
- **Description**: Adds a lantern to your inventory which looks like an axe and will softlock the player when trying to use it; canmove can be used to fix the soft lock.

## Command: `refuel`
- **Syntax**: `refuel`
- **Description**: Sets lantern fuel to maximum. As the lantern was not fully implemented, this doesn't do much.

## Command: `stoprafting`
- **Syntax**: `stoprafting`
- **Description**: Sets an internal `isRafting` flag to false. As rafting was not fully implemented, this doesn't do much.

## See also
- Chat commands for information about commands used in the chat window and Modding:Chat Commands to add custom chat commands.
- Modding:Modder Guide/APIs/Console to add custom commands from a SMAPI mod.

ru:Модификации:Команды для отладки
zh:模组:控制台命令
