> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Console)

# Modding:Modder Guide/APIs/Console (`Modder_Guide/APIs/Console`)

SMAPI can add custom commands to the SMAPI console, which players can type to do certain actions. These are also available in the in-game chat for players using the 2092 Chat Commands mod.

## Intro
The SMAPI console is the window that opens alongside the game, which displays messages from SMAPI and mods in a text-only format. Players can enter commands directly into that window to interact with mods. For example, you can type `help` to see a list of available commands. Note that most players aren't comfortable with a command-line interface, so you should prefer in-game interfaces for player-facing features in most cases.

## Add a custom command
Each console command must have:
- A name which the player types to invoke the command.
- A description shown when the player uses the `help` command. This should explain what the command does, how to use it, and what arguments it accepts. The example below shows the recommended convention.
- The method to call when the command is entered.

This code creates a minimal `player_setmoney` command:

```c#
/// <summary>The main entry point for the mod.</summary>
public class ModEntry : Mod
{
    /*********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        helper.ConsoleCommands.Add("player_setmoney", "Sets the player's money.\n\nUsage: player_setmoney <value>\n- value: the integer amount.", this.SetMoney);
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Set the player's money when the 'player_setmoney' command is invoked.</summary>
    /// <param name="command">The name of the command invoked.</param>
    /// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
    private void SetMoney(string command, string[] args)
    {
        Game1.player.Money = int.Parse(args[0]);
        this.Monitor.Log($"OK, set your money to {args[0]}.", LogLevel.Info);
    }
}
```

Here's how the player would use it:

```text
help player_setmoney
> player_setmoney: Sets the player's money.
> 
> Usage: player_setmoney <value>
> - value: the integer amount.

player_setmoney 5000
> OK, set your money to 5000.
```

## See also
- Integration APIs
- Logging API

ru:Модификации:Руководство мододела/API/Консоль
zh:模组:制作指南/APIs/Console
