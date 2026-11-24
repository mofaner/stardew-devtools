> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer)

# Modding:Modder Guide/APIs/Multiplayer (`Modder_Guide/APIs/Multiplayer`)

The multiplayer API provides methods to support modding in a multiplayer context. This API is still minimal, but much more will be added in later versions of SMAPI.

## Methods
### Get new ID
In some cases the game expects a 'multiplayer ID' value, which is a unique 64-bit number. This is mainly intended for cases where the game expects a multiplayer ID for data sync, such as when creating farm animals:

```c#
int animalID = this.Helper.Multiplayer.GetNewID();
var animal = new FarmAnimal("Cow", animalID, ownerID);
```

### Get active locations
In multiplayer mode, the game doesn't sync all locations to other players. Each farmhand will receive data for their current location, the farm, farmhouse, and farm buildings. You can get a list of the locations being sync'd:

```c#
foreach (GameLocation location in this.Helper.Multiplayer.GetActiveLocations())
{
   // do stuff
}
```

### Get connected player info
You can see basic info about other players connected to the same server using the `GetConnectedPlayer(playerID)` and `GetConnectedPlayers()` methods. This is available immediately when the player connects (before the game even approves the connection). Most of the information is only available for players who are running SMAPI too. You can access these fields for each player:

| field | type | description |
| --- | --- | --- |
| `PlayerID` | `long` | The player's unique multiplayer ID, which you can pass to game methods and fields that expect a unique player ID. |
| `IsHost` | `bool` | True if this player is hosting the server; false if they're a farmhand. |
| `IsSplitScreen` | `bool` | True if the player is running on the same computer in split-screen mode. |
| `HasSmapi` | `bool` | Whether this player has SMAPI installed. |
| `Platform` | `GamePlatform` | (Requires SMAPI.) The player's operating system as a `GamePlatform` enum (one of `Linux`, `Mac`, or `Windows`). |
| `GameVersion` | `ISemanticVersion` | (Requires SMAPI.) The version of the game they have installed (like link=0). |
| `ApiVersion` | `ISemanticVersion` | (Requires SMAPI.) The version of SMAPI they have installed (like `2.11`). |
| `Mods` | `IEnumerable<IMultiplayerPeerMod>` | (Requires SMAPI.) The mods they have installed. Each mod includes the name, unique ID, and version. This is null if the other player doesn't have SMAPI, or an empty list if they have SMAPI but no mods. |
| `ScreenID` | `int?` | The player's screen ID in split-screen mode, if `IsSplitScreen` is true. |
| `GetMod(id)` | *method* returns `IMultiplayerPeerMod` | (Requires SMAPI.) A method which returns the mod with the given mod ID using the same case-insensitivity rules as SMAPI, if available. For example, this can be used to check if a mod is installed: `if (peer.GetMod("Pathoschild.ContentPatcher") != null)`. |

For example, this will log information about the currently connected players:

```c#
foreach (IMultiplayerPeer peer in this.Helper.Multiplayer.GetConnectedPlayers())
{
    if (peer.HasSmapi)
    {
        // prints something like: "Found player -1634555469947451666 running Stardew Valley 1.5.2 and SMAPI 3.9.0 on Windows with 41 mods."
        this.Monitor.Log($"Found player {peer.PlayerID} running Stardew Valley {peer.GameVersion} and SMAPI {peer.ApiVersion} on {peer.Platform} with {peer.Mods.Count()} mods.");
    }
    else
    {
        // most info not available if they don't have SMAPI
        this.Monitor.Log($"Found player {peer.PlayerID} running Stardew Valley without SMAPI.");
    }
}
```

**Note:** players whose connections haven't been approved by the game yet are returned too. Although you can send/receive messages through SMAPI's APIs, the game itself may not know about them yet. If you only want players who are fully connected, you can do something like this instead:

```c#
foreach (Farmer farmer in Game1.getOnlineFarmers())
{
   IMultiplayerPeer peer = this.Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID);
}
```

### Send messages
You can send a message to mods on all connected computers (including the current one) using the `SendMessage` method. The destination can range from very narrow (*e.g.,* one mod on one connected computer) to very broad (all mods on all computers). The message won't be sent back to the mod instance that sent it, but it can be sent to the same mod on other computers.

When sending a message, you must specify three things:
- The data you want to send. This can be a simple value (like a number or string), or a class instance. When using a class instance with custom constructors, make sure it has a default constructor too.
- A message type, so the destination mods can know which message it is. This doesn't need to be globally unique, since mods should check the originating mod ID.
- Who should receive the message. You can specify any combination of player IDs and/or mod IDs. By default, the message is sent to all mods and players.

For example:

```c#
// broadcast a message to all mods on all computers
MyMessage message = new MyMessage(...); // create your own class with the data to send
this.Helper.Multiplayer.SendMessage(message, "MyMessageType");
```

```c#
// send a message to a specific mod on all computers
MyMessage message = new MyMessage(...);
this.Helper.Multiplayer.SendMessage(message, "MyMessageType", modIDs: new[] { this.ModManifest.UniqueID });
```

### Receive messages
You can receive messages by listening to the `helper.Events.Multiplayer.ModMessageReceived` event. The event arguments specify who sent the message, and let you read the message into a matching data model.

For example:

```c#
public override void Entry(IModHelper helper)
{
   helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
}

public void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
{
   if (e.FromModID == this.ModManifest.UniqueID && e.Type == "ExampleMessageType")
   {
      MyMessageClass message = e.ReadAs<MyMessageClass>();
      // handle message fields here
   }
}
```

## Per-screen data
Split-screen multiplayer swaps the entire game state between each player, so each mod runs across every player. For example, with four local players the `UpdateTicked` event would be raised four times per tick. The game manages its own state automatically (*e.g.,* `Game1.activeClickableMenu`), but if your mod stores info in its own fields you may need to handle those yourself.

SMAPI provides `PerScreen<T>` to make that easy. You use it by creating **readonly** fields for your values (which can be any value from `int` to entire class instances):

```c#
private readonly PerScreen<int> LastPlayerId = new PerScreen<int>(); // defaults to 0
private readonly PerScreen<SButton> LastButtonPressed = new PerScreen<SButton>(createNewState: () => SButton.None); // defaults to the given value
```

Then you can just use its properties and methods:

| member | description |
| --- | --- |
| `Value` | Get or set the value for the current screen. If needed, this creates the value automatically. |
| `GetActiveValues()` | Get the screen IDs and values for every active screen which has a value. |
| `GetValueForScreen(screenId)`; `SetValueForScreen(screenId, value)` | Get or set the value for a specific screen ID, creating it if needed. |
| `ResetAllScreens` | Clear the values saved for every screen. |

### Example
For example, this mod plays a simple game: the main player presses a key, and farmhands need to guess which key was pressed.

```c#
internal class ModEntry : Mod
{
    private readonly PerScreen<SButton> LastButtonPressed = new PerScreen<SButton>();
    private readonly PerScreen<int> Score = new PerScreen<int>();

    public override void Entry(IModHelper helper)
    {
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }

    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        // main player changes key
        if (Context.IsMainPlayer)
        {
            this.LastButtonPressed.Value = e.Button;
            this.Monitor.Log("The main player changed the key. Can you guess it?", LogLevel.Info);
        }

        // farmhands try to guess the key
        else
        {
            SButton correctButton = this.LastButtonPressed.GetValueForScreen(0);
            if (correctButton != SButton.None)
            {
                if (e.Button == correctButton)
                {
                    this.Score.Value++;
                    this.LastButtonPressed.SetValueForScreen(0, SButton.None);
                    this.Monitor.Log($"Player #{Context.ScreenId + 1} correctly guessed the key: {e.Button}! Their score is now {this.Score.Value}.", LogLevel.Info);
                }
                else
                    this.Monitor.Log($"Player #{Context.ScreenId + 1} guessed incorrectly: {e.Button}.", LogLevel.Debug);
            }
        }
    }
}
```

**Tip:** you should almost always mark a per-screen field `readonly`. Overwriting the entire field (instead of setting the `Value` property) will clear the data for all players, instead of setting it for the current one.

## See also
- Multiplayer events

ru:Модификации:Руководство мододела/API/Мультиплеер
zh:模组:制作指南/APIs/Multiplayer
