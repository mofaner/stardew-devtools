> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Chat_Commands)

# Modding:Chat Commands (`Chat_Commands`)

← Index

This page documents how custom chat commands are handled by the game. This is an advanced guide for mod developers.

## Registering custom commands
Chat commands are modular and extensible, with a `ChatCommands` API to manage them.

For example, you can add a custom chat command like this:

```c#
string modId = this.ModManifest.UniqueId;
ChatCommands.Register($"{modId}_echo", this.Echo, name => $"{name} [message]: show the given message in the chat box.");

...

/// <inheritdoc cref="ChatCommandHandlerDelegate" />
private void Echo(string[] command, ChatBox chat)
{
    string message = ArgUtility.GetRemainder(command, 1);

    chat.addInfoMessage(message);
}
```

## See also
- Modding:Console commands for commands that can be typed in the SMAPI console.

ru:Модификации:Чат-команды
zh:模组:聊天命令
