> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events)

# Modding:Modder Guide/APIs/Events (`Modder_Guide/APIs/Events`)

SMAPI provides several C# events which let your mod respond when something happens (like when the player places an object) or run code periodically (like once per update tick).

## FAQs
### What are events?
Events let you run code when something happens. You can add any number of *event handlers* (methods to call) when an *event* (what happened) is raised. You can think of events and handlers as a *when..then* statement:

```text
WHEN the save is loaded,   <-- event
THEN run my code           <-- event handler
```

See [*Events* in the C# programming guide](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/events/) for more info.

### How do I use them?
Event handlers are usually added in your `Entry` method, though you can add and remove them anytime. For example, let's say you want to print a message when each day starts. First you would choose the appropriate event from the list below (`GameLoop.DayStarted`), then add an event handler, and do something in your method code:

```c#
/// <summary>The main entry point for the mod.</summary>
internal sealed class ModEntry : Mod
{
    /**********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        // event += method to call
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
    }

    /// <summary>The method called after a new day starts.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
       this.Monitor.Log("A new day dawns!", LogLevel.Info);
    }
}
```

Tip: you don't need to memorise the method arguments. In Visual Studio, type `helper.Events.GameLoop.SaveLoaded +=` and press TAB to auto-create a method. (There doesn't seem to be an equivalent feature in MonoDevelop.)

`sender` is part of the C# event pattern, and is unused by SMAPI.

### How do events fit into the game?
Events are raised on each game tick (when the game updates its state and renders to the screen), which is 60 times per second. An event may be raised multiple times (*e.g.,* if the player pressed two keys simultaneously), but most events won't be raised 60 times per second (*e.g.,* players are unlikely to be pressing 60 buttons per second).

Your event handlers are run *synchronously*: the game is paused and no other mod's code will run simultaneously, so there's no risk of conflicting changes. Since code runs very quickly, players won't notice any delay unless your code is unusually slow. That said, when using frequent events like `UpdateTicked` or `Rendered`, you should cache expensive operations (like loading an asset) instead of repeating them in each tick to avoid impacting performance.

### What if a mod changes what the event was raised for?
Events are raised based on a snapshot of the game state. That's usually *but not necessarily* the current game state.

For example, consider this case:
# The `GameMenu` opens.
# SMAPI raises the `MenuChanged` event, which mods A and B listen to.
# Mod A receives the event and closes the menu.
# Mod B receives the event.

Each mod is still handling the `MenuChanged` event for the opened menu, even though the first mod closed it. SMAPI will raise a new `MenuChanged` event for the closed menu on the next tick.

This rarely affects mods, but it's something to keep in mind if you need the current state (*e.g.,* check `Game1.activeClickableMenu` instead of `e.NewMenu`).

## Events
The available events are documented below.

### Content
`this.Helper.Events.Content` has events related to assets loaded from the content pipeline.

## Event: `helper.Events.Content.AssetRequested`

Raised when an asset is being requested from the content pipeline. The asset isn't necessarily being loaded yet (*e.g.* the game may be checking if it exists).

You can register the changes you want to apply using the event arguments below; they'll be applied when the asset is actually loaded. See the content API for more info.

If the asset is requested multiple times in the same tick (e.g. once to check if it exists and once to load it), SMAPI might only raise the event once and reuse the cached result.

- **Event Arguments**:
  - `e.Name` (`IAssetName`): The name of the asset being requested, including the locale code if any (like the `.fr-FR` in `Data/Bundles.fr-FR`). This includes utility methods to parse the value, like `e.Name.IsEquivalentTo("Portraits/Abigail")` (which handles any differences in formatting for you).
  - `e.NameWithoutLocale` (`IAssetName`): Equivalent to `e.Name` but without the locale code (if any). For example, if `e.Name` is `Data/Bundles.fr-FR`, this field will contain `Data/Bundles`.
  - `e.LoadFrom(...)` (*method*): Call this method to provide the initial instance for the asset, instead of trying to load it from the game's `Content` folder. For example:

```c#
e.LoadFrom(() => this.Helper.Content.Load<Texture2D>("assets/portraits.png"), AssetLoadPriority.Medium);
```

Usage notes:
- The asset doesn't need to exist in the game's `Content` folder. If any mod loads the asset, the game will see it as an existing asset as if it was in that folder.
- Each asset can logically only have one initial instance. If multiple loads apply at the same time, SMAPI will raise an error and ignore all of them. If you're making changes to the existing asset instead of replacing it, you should use the `Edit` method instead to avoid those limitations and improve mod compatibility.
  - `e.LoadFromModFile<T>(...)` (*method*): Call this method to provide the initial instance for the asset by loading a file from your mod folder. For example:

```c#
e.LoadFromModFile<Texture2D>("assets/portraits.png", AssetLoadPriority.Medium);
```

See usage notes on `e.LoadFrom`.
  - `e.Edit(...)` (*method*): Call this method to apply edits to the asset after it's loaded. For example:

```c#
e.Edit(asset =>
{
    Texture2D ribbon = this.Helper.Content.Load<Texture2D>("assets/ribbon.png");
    asset.AsImage().PatchImage(source: overlay, patchMode: PatchMode.Overlay);
});
```

Usage notes:
- Editing an asset which doesn't exist has no effect. This is applied after the asset is loaded from the game's `Content` folder, or from any mod's `LoadFrom` or `LoadFromModFile`.
- You can apply any number of edits to the asset. Each edit will be applied on top of the previous one (i.e. it'll see the merged asset from all previous edits as its input).

## Event: `helper.Events.Content.AssetsInvalidated`

Raised after one or more assets were invalidated from the content cache by a mod, so they'll be reloaded next time they're requested. If the assets will be reloaded or propagated automatically, this event is raised before that happens.

Next time the asset is loaded (including for asset propagation), the `AssetRequested` event will be raised again.

- **Event Arguments**:
  - `e.Names` (`IReadOnlySet<IAssetName>`): The asset names that were invalidated, including the locale code if any (like the `.fr-FR` in `Data/Bundles.fr-FR`). These include utility methods to parse the values, like `name.IsEquivalentTo("Portraits/Abigail")` (which handles any differences in formatting for you).
  - `e.NamesWithoutLocale` (`IReadOnlySet<IAssetName>`): Equivalent to `e.Names` but without the locale code (if any). For example, if `e.Names` contains `Data/Bundles.fr-FR`, this field will contain `Data/Bundles`.

## Event: `helper.Events.Content.AssetReady`

Raised after an asset is loaded by the content pipeline, after any mod edits specified via `AssetRequested` have been applied.

This event is only raised if something requested the asset from the content pipeline. Invalidating an asset from the content cache won't necessarily reload it automatically.

- **Event Arguments**:
  - `e.Name` (`IAssetName`): The name of the asset that was loaded, including the locale code if any (like the `.fr-FR` in `Data/Bundles.fr-FR`). This includes utility methods to parse the value, like `e.Name.IsEquivalentTo("Portraits/Abigail")` (which handles any differences in formatting for you).
  - `e.NameWithoutLocale` (`IAssetName`): Equivalent to `e.Name` but without the locale code (if any). For example, if `e.Name` is `Data/Bundles.fr-FR`, this field will contain `Data/Bundles`.

## Event: `helper.Events.Content.LocaleChanged`

Raised after the game language changes. For non-English players, this may be raised during startup when the game switches to the previously selected language.

- **Event Arguments**:
  - `e.OldLanguage` (`LanguageCode`): The previous value for the game's language enum. For a custom language, this is always `LanguageCode.mod`.
  - `e.OldLocale` (`string`): The previous value for the locale code. This is the locale code as it appears in asset names, like `fr-FR` in `Maps/springobjects.fr-FR`. The locale code for English is an empty string.
  - `e.NewLanguage` (`LanguageCode`): The new value for the game's language enum. See notes on `OldLanguage`.
  - `e.NewLocale` (`string`): The new value for the locale code. See notes on `OldLocale`.

### Display
`this.Helper.Events.Display` has events linked to UI and drawing to the screen.

## Event: `helper.Events.Display.MenuChanged`

Raised after a game menu is opened, closed, or replaced.

- **Event Arguments**:
  - `e.NewMenu` (`IClickableMenu`): The new menu instance (or `null` if none).
  - `e.OldMenu` (`IClickableMenu`): The old menu instance (or `null` if none).

## Event: `helper.Events.Display.Rendering`

Raised before the game draws anything to the screen in a draw tick, as soon as the sprite batch is opened. The sprite batch may be closed and reopened multiple times after this event is called, but it's only raised once per draw tick. This event isn't useful for drawing to the screen, since the game will draw over it.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.Rendered`

Raised after the game draws to the sprite batch in a draw tick, just before the final sprite batch is rendered to the screen. Since the game may open/close the sprite batch multiple times in a draw tick, the sprite batch may not contain everything being drawn and some things may already be rendered to the screen. Content drawn to the sprite batch at this point will be drawn over all vanilla content (including menus, HUD, and cursor).

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderingWorld`

Raised before the game world is drawn to the screen. This event isn't useful for drawing to the screen, since the game will draw over it.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderedWorld`

Raised after the game world is drawn to the sprite batch, before it's rendered to the screen. Content drawn to the sprite batch at this point will be drawn over the world, but under any active menu, HUD elements, or cursor.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderingActiveMenu`

When a menu is open (`Game1.activeClickableMenu != null`), raised before that menu is drawn to the screen. This includes the game's internal menus like the title screen. Content drawn to the sprite batch at this point will appear under the menu.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderedActiveMenu`

When a menu is open (`Game1.activeClickableMenu != null`), raised after that menu is drawn to the sprite batch but before it's rendered to the screen. Content drawn to the sprite batch at this point will appear over the menu and menu cursor.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderingHud`

Raised before drawing the HUD (item toolbar, clock, etc) to the screen. The vanilla HUD may be hidden at this point (*e.g.,* because a menu is open). Content drawn to the sprite batch at this point will appear under the HUD.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.RenderedHud`

Raised after drawing the HUD (item toolbar, clock, etc) to the sprite batch, but before it's rendered to the screen. The vanilla HUD may be hidden at this point (*e.g.,* because a menu is open). Content drawn to the sprite batch at this point will appear over the HUD.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.

## Event: `helper.Events.Display.WindowResized`

Raised after the game window is resized.

- **Event Arguments**:
  - `e.OldSize` (`Point`): The previous window width (`e.OldSize.X`) and height (`e.OldSize.Y`).
  - `e.NewSize` (`Point`): The new window width (`e.NewSize.X`) and height (`e.NewSize.Y`).

## Event: `helper.Events.Display.RenderingStep`

*(Specialized)* Raised before the game draws a specific step in the rendering cycle. This gives more granular control over render logic, but is more vulnerable to changes in game updates. Consider using one of the other render events if possible.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.
  - `e.Step` (`RenderSteps`): The step being rendered in the draw cycle.

## Event: `helper.Events.Display.RenderedStep`

*(Specialized)* Raised after the game draws a specific step in the rendering cycle.  This gives more granular control over render logic, but is more vulnerable to changes in game updates. Consider using one of the other render events if possible.

- **Event Arguments**:
  - `e.SpriteBatch` (`SpriteBatch`): The sprite batch being drawn. Add anything you want to appear on-screen to this sprite batch.
  - `e.Step` (`RenderSteps`): The step being rendered in the draw cycle.

### Game loop
`this.Helper.Events.GameLoop` has events linked to the game's update loop. The update loop runs roughly ≈60 times/second to run game logic like state changes, action handling, etc. These are often useful, but you should consider semantic events like `Input` where applicable.

## Event: `helper.Events.GameLoop.GameLaunched`

Raised after the game is launched, right before the first update tick. This happens once per game session (unrelated to loading saves). All mods are loaded and initialised at this point, so this is a good time to set up mod integrations.

## Event: `helper.Events.GameLoop.UpdateTicking, UpdateTicked`

Raised before/after the game state is updated (≈60 times per second).

- **Event Arguments**:
  - `e.Ticks` (`int`): The number of ticks elapsed since the game started, including the current tick.
  - `e.IsOneSecond` (`bool`): Whether `e.TicksElapsed` is a multiple of 60, which happens approximately once per second.
  - `e.IsMultipleOf(int number)` (*method* returns `bool`): Whether `e.TicksElapsed` is a multiple of the given number. This is mainly useful if you want to run logic intermittently (*e.g.,* `e.IsMultipleOf(30)` for every half-second).

## Event: `helper.Events.GameLoop.OneSecondUpdateTicking, OneSecondUpdateTicked`

Raised before/after the game state is updated, once per second. EventArgs subtype is OneSecondUpdateTickedEventArgs

- **Event Arguments**:
  - `e.Ticks` (`int`): The number of ticks elapsed since the game started, including the current tick.
  - `e.IsMultipleOf(int number)` (*method* returns `bool`): Whether `e.TicksElapsed` is a multiple of the given number. This is mainly useful if you want to run logic intermittently (*e.g.,* `e.IsMultipleOf(120)` for every two seconds).

## Event: `helper.Events.GameLoop.SaveCreating, SaveCreated`

Raised before/after the game creates the save file (after the new-game intro). The save won't be written until all mods have finished handling this event. This is a somewhat specialised event, since the world isn't fully initialised at this point; in most cases you should use `DayStarted`, `Saving`, or `Saved` instead.

## Event: `helper.Events.GameLoop.Saving, Saved`

Raised before/after the game writes data to save file (except the initial save creation). The save won't be written until all mods have finished handling this event. This is also raised for farmhands in multiplayer.

## Event: `helper.Events.GameLoop.SaveLoaded`

Raised after loading a save (including the first day after creating a new save), or connecting to a multiplayer world. This happens right before `DayStarted`, but after the game's own day-start initialization process; at this point the save file is read and `Context.IsWorldReady` is true.

This event isn't raised after saving; if you want to do something at the start of each day, see `DayStarted` instead.

## Event: `helper.Events.GameLoop.DayStarted`

Raised after a new in-game day starts, or after connecting to a multiplayer world. Everything has already been initialised at this point. (To run code before the game sets up the day, see `DayEnding` instead.)

## Event: `helper.Events.GameLoop.DayEnding`

Raised before the game ends the current day. This happens before it starts setting up the next day and before `Saving`.

## Event: `helper.Events.GameLoop.TimeChanged`

Raised after the in-game clock time changes, which happens in intervals of ten in-game minutes.

- **Event Arguments**:
  - `e.OldTime` (`int`): The previous time of day in 24-hour notation (like 1600 for 4pm). The clock time resets when the player sleeps, so 2am (before sleeping) is 2600.
  - `e.NewTime` (`int`): The current time of day in 24-hour notation (like 1600 for 4pm). The clock time resets when the player sleeps, so 2am (before sleeping) is 2600.

## Event: `helper.Events.GameLoop.ReturnedToTitle`

Raised after the game returns to the title screen.

### Input
`this.Helper.Events.Input` has events raised when the player uses a controller, keyboard, or mouse in some way. They can be used with the input API to access more info or suppress input.

## Event: `helper.Events.Input.ButtonsChanged`

Raised after the player pressed/released any buttons on the keyboard, mouse, or controller. This includes mouse clicks. If the player pressed/released multiple keys at once, this is only raised once.

- **Event Arguments**:
  - `e.Pressed` ([[Modding:Modder Guide/APIs/Input#SButton|`SButton[]`]]): The buttons that were pressed since the previous tick.
  - `e.Held` ([[Modding:Modder Guide/APIs/Input#SButton|`SButton[]`]]): The buttons that were held since the previous tick.
  - `e.Released` ([[Modding:Modder Guide/APIs/Input#SButton|`SButton[]`]]): The buttons that were released since the previous tick.
  - `e.Cursor` (`ICursorPosition`): The cursor position and grab tile.

**Note:** mods won't receive input sent to the chatbox.

## Event: `helper.Events.Input.ButtonPressed, ButtonReleased`

Raised after the player pressed/released a keyboard, mouse, or controller button. This includes mouse clicks. If the player pressed/released multiple keys at once, this is raised for each button pressed.

- **Event Arguments**:
  - `e.Button` (`SButton`): The button pressed or released.
  - `e.Cursor` (`ICursorPosition`): The cursor position and grab tile.
  - `e.IsDown` (*method* returns `bool`): Indicates whether a given button is currently pressed.
  - `e.IsSuppressed` (*method* returns `bool`): A method which indicates whether a given button was suppressed by a mod, so the game itself won't see it.

**Note:** mods won't receive input sent to the chatbox.

## Event: `helper.Events.Input.CursorMoved`

Raised after the player moves the in-game cursor.

- **Event Arguments**:
  - `e.OldPosition` (ICursorPosition): The previous cursor position and grab tile.
  - `e.NewPosition` (ICursorPosition): The current cursor position and grab tile.

## Event: `helper.Events.Input.MouseWheelScrolled`

Raised after the player scrolls the mouse wheel.

- **Event Arguments**:
  - `e.Position` (ICursorPosition): The current cursor position and grab tile.
  - `e.Delta` (`int`): The amount by which the mouse wheel was scrolled since the last update.
  - `e.OldValue`
`e.NewValue` (`int`): The previous and current scroll value, cumulative since the game started. Mods should generally use `e.Delta` instead.

### Multiplayer
`this.Helper.Events.Multiplayer` has events raised for multiplayer messages and connections.

## Event: `helper.Events.Multiplayer.PeerContextReceived`

Raised after the mod context for a player is received. The event is raised for any player (whether host or farmhand), including when the connecting player doesn't have SMAPI installed. This is the earliest point where messages can be sent to the player via SMAPI.

This happens immediately before the game approves the connection, so the player doesn't exist in the game yet. When connecting to the host, contextual fields like `Game1.IsMasterGame` or `Context.IsMultiplayer` may not be set yet; you can check `e.Peer.IsHost` to know whether the current player is a farmhand, since the host context will always be received first. Assuming another mod doesn't block the connection, the connection will be approved on the next tick.

- **Event Arguments**:
  - `e.Peer` (`IMultiplayerPeer`): The peer whose context was received (see Multiplayer#Get connected player info).

## Event: `helper.Events.Multiplayer.PeerConnected`

Raised after a connection from another player is approved by the game. The event is raised for any player (whether host or farmhand), including when the connecting player doesn't have SMAPI installed. This happens after `PeerContextReceived`.

The player is connected to the game at this point, so methods like `Game1.server.kick` will work.

- **Event Arguments**:
  - `e.Peer` (`IMultiplayerPeer`): The peer who connected (see Multiplayer#Get connected player info).

## Event: `helper.Events.Multiplayer.ModMessageReceived`

Raised after a mod message is received over the network.

- **Event Arguments**:
  - `e.FromPlayerID` (`long`): The unique ID of the player from whose computer the message was sent.
  - `e.FromModID` (`string`): The unique ID of the mod which sent the message.
  - `e.Type` (`string`): A message type which you can use to decide whether it's the one you want to handle. This isn't necessarily globally unique, so you should also check the `FromModID` field.
  - `e.ReadAs<TModel>()` (*method* returns `TModel`): Read the underlying message data into the given model type (like `e.ReadAs<MyMessageClass>()` or `e.ReadAs<string>()`). This will return a new instance each time.

## Event: `helper.Events.Multiplayer.PeerDisconnected`

Raised after the connection to a player is severed.

- **Event Arguments**:
  - `e.Peer` (`IMultiplayerPeer`): The peer whose connection was severed (see Multiplayer#Get connected player info).

### Player
`this.Helper.Events.Player` has events raised when the player data changes.

**Currently these events are only raised for the current player. That will likely change in a future version, so make sure to check `e.IsLocalPlayer` if you only want to handle the current player.**

## Event: `helper.Events.Player.InventoryChanged`

Raised after items are added or removed from the player inventory.

- **Event Arguments**:
  - `e.Player` (`Farmer`): The player whose inventory changed.
  - `e.Added` (`IEnumerable<Item>`): The added item stacks.
  - `e.Removed` (`IEnumerable<Item>`): The removed item stacks.
  - `e.QuantityChanged` (`IEnumerable<ItemStackSizeChange>`): The item stacks whose quantity changed. Each `ItemStackSizeChange` instance includes `Item` (the affected item stack), `OldSize` (the previous stack size), and `NewSize` (the new stack size).
  - `e.IsLocalPlayer` (`bool`): Whether the affected player is the local one.

## Event: `helper.Events.Player.LevelChanged`

Raised after a player's skill level changes. When the player levels up normally, this is raised immediately (not when the game notifies the player after they go to bed).

- **Event Arguments**:
  - `e.Player` (`Farmer`): The player whose skill level changed.
  - `e.Skill` (`SkillType`): The skill whose level changed. This is a constant like `SkillType.Combat`, which can be converted to the game's internal skill ID using `(int)e.Skill`.
  - `e.OldLevel` (`int`): The player's previous level for that skill.
  - `e.NewLevel` (`int`): The player's new level for that skill.
  - `e.IsLocalPlayer` (`bool`): Whether the affected player is the local one.

## Event: `helper.Events.Player.Warped`

Raised after the current player moves to a new location.

- **Event Arguments**:
  - `e.Player` (`Farmer`): The player who warped to a new location.
  - `e.OldLocation` (`GameLocation`): The player's previous location.
  - `e.NewLocation` (`GameLocation`): The player's new location.
  - `e.IsLocalPlayer` (`bool`): Whether the affected player is the local one.

### World
`this.Helper.Events.World` has events raised when the in-game world changes in some way.

## Event: `helper.Events.World.LocationListChanged`

Raised after a game location is added or removed (including building interiors).

- **Event Arguments**:
  - `e.Added` (`IEnumerable<GameLocation>`): A list of locations added since the last update tick.
  - `e.Removed` (`IEnumerable<GameLocation>`): A list of locations removed since the last update tick.

## Event: `helper.Events.World.BuildingListChanged`

Raised after buildings are added/removed in any location.

This event isn't raised for buildings already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added.OfType<BuildableGameLocation>()` → `buildings`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<Building>`): A list of buildings added since the last update tick.
  - `e.Removed` (`IEnumerable<Building>`): A list of buildings removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.ChestInventoryChanged`

Raised after items are added or removed from a chest's inventory.

- **Event Arguments**:
  - `e.Chest` (`Chest`): The chest whose inventory changed.
  - `e.Location` (`Location`): The location containing the chest.
  - `e.Added` (`IEnumerable<Item>`): The added item stacks.
  - `e.Removed` (`IEnumerable<Item>`): The removed item stacks.
  - `e.QuantityChanged` (`IEnumerable<ItemStackSizeChange>`): The item stacks whose quantity changed. Each `ItemStackSizeChange` instance includes `Item` (the affected item stack), `OldSize` (the previous stack size), and `NewSize` (the new stack size).

## Event: `helper.Events.World.DebrisListChanged`

Raised after debris is added/removed in any location (including dropped or spawned floating items).

This event isn't raised for debris already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `debris`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<Debris>`): A list of debris added since the last update tick.
  - `e.Removed` (`IEnumerable<Debris>`): A list of debris removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.FurnitureListChanged`

Raised after furniture are added/removed in any location.

This event isn't raised for furniture already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `furniture`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<Furniture>`): A list of furniture added since the last update tick.
  - `e.Removed` (`IEnumerable<Furniture>`): A list of furniture removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.LargeTerrainFeatureListChanged`

Raised after large terrain features (like bushes) are added/removed in any location.

This event isn't raised for large terrain features already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `largeTerrainFeatures`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<LargeTerrainFeature>`): A list of large terrain features added since the last update tick.
  - `e.Removed` (`IEnumerable<LargeTerrainFeature>`): A list of large terrain features removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.NpcListChanged`

Raised after NPCs are added/removed in any location (including villagers, horses, Junimos, monsters, and pets).

This event isn't raised for characters already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `characters`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<NPC>`): A list of NPCs added since the last update tick.
  - `e.Removed` (`IEnumerable<NPC>`): A list of NPCs removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.ObjectListChanged`

Raised after objects are added/removed in any location (including machines, fences, etc). This doesn't apply for floating items (see `DebrisListChanged`) or furniture (see `FurnitureListChanged`).

This event isn't raised for objects already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `objects`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<KeyValuePair<Vector2, Object>>`): A list of tile coordinate + object pairs added since the last update tick.
  - `e.Removed` (`IEnumerable<KeyValuePair<Vector2, Object>>`): A list of tile coordinate + object pairs removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

## Event: `helper.Events.World.TerrainFeatureListChanged`

Raised after terrain features are added/removed in any location (including trees, hoed dirt, and flooring). For bushes, see `LargeTerrainFeatureListChanged`.

This event isn't raised for terrain features already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added` → `terrainFeatures`.

- **Event Arguments**:
  - `e.Location` (`GameLocation`): The location which changed.
  - `e.Added` (`IEnumerable<KeyValuePair<Vector2, TerrainFeature>>`): A list of tile coordinate + terrain feature pairs added since the last update tick.
  - `e.Removed` (`IEnumerable<KeyValuePair<Vector2, TerrainFeature>>`): A list of tile coordinate + terrain feature pairs removed since the last update tick.
  - `e.IsCurrentLocation` (`bool`): Whether `e.Location` is the location containing the local player.

### Specialised
`this.Helper.Events.Specialised` has events for specialised edge cases. These shouldn't be used by most mods.

## Event: `helper.Events.Specialised.LoadStageChanged`

Raised when the low-level stage in the game's loading process has changed, for mods which need to run code at specific points in the loading process. The available stages or when they happen might change without warning in future versions (*e.g.,* due to changes in the game's load process), so **mods using this event are more likely to break or have bugs**. Most mods should use the game loop events instead.

- **Event Arguments**:
  - `e.NewStage` (`LoadStage`): The new load stage. The possible values are...

- `None`: a save is not loaded or loading. (For example, the player is on the title screen.)
- When creating a new save slot:
  - `CreatedBasicInfo`: the game has initialised the basic save info.
  - `CreatedInitialLocations`: the game has added the location instances, but hasn't fully initialized them yet.
  - `CreatedLocations`: the game has initialised the in-game locations.
  - `CreatedSaveFile`: the game has created the physical save files.
- When loading an existing save slot:
  - `SaveParsed`: the game has read the raw save data into `StardewValley.SaveGame.loaded`. Not applicable when connecting to a multiplayer host. This is equivalent to `StardewValley.SaveGame.getLoadEnumerator` value 20.
  - `SaveLoadedBasicInfo`: the game has applied the basic save info (including player data). Not applicable when connecting to a multiplayer host. Note that some basic info (like daily luck) is not initialised at this point. This is equivalent to `StardewValley.SaveGame.getLoadEnumerator` value 36.
  - `SaveAddedLocations`: the game has added the location instances to the game, but hasn't restored their save data yet.
  - `SaveLoadedLocations`: the game has restored the in-game location data. Not applicable when connecting to a multiplayer host. This is equivalent to `StardewValley.SaveGame.getLoadEnumerator` value 50.
- `Preloaded`: the final metadata has been loaded from the save file. This happens before the game applies problem fixes, checks for achievements, starts music, etc. Not applicable when connecting to a multiplayer host.
- `Loaded`: the save is fully loaded, but the world may not be fully initialised yet.
- `Ready`: the save is fully loaded, the world has been initialised, and `Context.IsWorldReady` is now true.
- `ReturningToTitle`: The game is exiting the loaded save and returning to the title screen. This happens before it returns to title; the stage *after* it returns to title is `None`.
  - `e.OldStage` (`LoadStage`): The previous load stage. See comments for `e.NewStage`.

## Event: `helper.Events.Specialised.UnvalidatedUpdateTicking, UnvalidatedUpdateTicked`

Raised before/after the game updates its state (≈60 times per second), regardless of normal SMAPI validation. This event is not thread-safe and may be invoked while game logic is running asynchronously. Changes to game state in this method may crash the game or corrupt an in-progress save. **Do not use this event unless you're fully aware of the context in which your code will be run. Using this event will trigger a warning in the SMAPI console.**

- **Event Arguments**:
  - `e.Ticks` (`int`): The number of ticks elapsed since the game started, including the current tick.
  - `e.IsOneSecond` (`bool`): Whether `e.TicksElapsed` is a multiple of 60, which happens approximately once per second.
  - `e.IsMultipleOf(int number)` (*method* returns `bool`): Whether `e.TicksElapsed` is a multiple of the given number. This is mainly useful if you want to run logic intermittently (*e.g.,* `e.IsMultipleOf(30)` for every half-second).

## Advanced
### Change monitoring
You may want to handle a change that doesn't have its own event (*e.g.,* an NPC's heart event ends, a letter is added to the mailbox, etc). You can usually do that by handling a general event like `UpdateTicked`, and detecting when the value(s) you're watching changed. For example, here's a complete mod which logs a message when an in-game event ends:

```c#
/// <summary>The main entry point for the mod.</summary>
internal sealed class ModEntry : Mod
{
    /*********
    ** Fields
    *********/
    /// <summary>The in-game event detected on the last update tick.</summary>
    private Event LastEvent;

    /*********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>The method invoked when the game updates its state.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnUpdateTicked(object sender, EventArgs e)
    {
        if (this.LastEvent != null && Game1.CurrentEvent == null)
            this.Monitor.Log($"Event {this.LastEvent.id} just ended!");

        this.LastEvent = Game1.CurrentEvent;
    }
}
```

### Custom priority
SMAPI calls event handlers in the same order they're registered by default, so the first event handler registered is the first to receive the event each time. This isn't always predictable, since it depends on mod load order and when each mod registers their handlers. This order is also an implementation detail, so it's not guaranteed.

If you need more control over the order, you can specify an event priority using the `[EventPriority]` attribute: `Low` (after most handlers), `Default`, `High` (before most handlers), or a custom value (*e.g.,* `High + 1` is higher priority than `High`). **You should only do this if strictly needed**; depending on event handler order between mods is fragile (*e.g.,* the other mod might change its priority too).

```c#
/// <summary>The main entry point for the mod.</summary>
internal sealed class ModEntry : Mod
{
    /*********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>The method invoked when the game updates its state.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    [EventPriority(EventPriority.High)]
    private void OnUpdateTicked(object sender, EventArgs e)
    {
        this.Monitor.Log("Update!");
    }
}
```

ru:Модификации:Руководство мододела/API/События
zh:模组:制作指南/APIs/Events
