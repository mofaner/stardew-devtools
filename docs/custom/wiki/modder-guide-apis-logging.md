> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Logging)

# Modding:Modder Guide/APIs/Logging (`Modder_Guide/APIs/Logging`)

The monitor API, available through `this.Monitor` in your mod class, mainly lets you write messages to the console window and log file.

## Logging
### Overview
You can log messages by calling `this.Monitor.Log` with a string message and a log level. These messages will appear in the SMAPI console and log file.

For example, this code:

```c#
this.Monitor.Log("a trace message", LogLevel.Trace);
this.Monitor.Log("a debug message", LogLevel.Debug);
this.Monitor.Log("an info message", LogLevel.Info);
this.Monitor.Log("a warning message", LogLevel.Warn);
this.Monitor.Log("an error message", LogLevel.Error);
```

...will produce log entries with a timestamp, log level, mod name, and message:

[18:00:00 TRACE NameOfMod] a trace message

[18:00:00 DEBUG NameOfMod] a debug message

[18:00:00 INFO  NameOfMod] an info message

[18:00:00 WARN  NameOfMod] a warning message

[18:00:00 ERROR NameOfMod] an error message

The text won't appear in color in the log file, but will appear in color in SMAPI console window as the game runs.

### Log levels
The log level is *semantic*, meaning that the level itself conveys information (a `Trace` message means something different from an `Error` message). You should use the log level that's closest to your message's purpose:

| level | purpose |
| --- | --- |
| `Trace` | Tracing info intended for developers, usually low-level troubleshooting details that are useful when someone sends you their error log. Trace messages won't appear in the console window by default (unless you have the "SMAPI for developers" version), though they're always written to the log file. |
| `Debug` | Troubleshooting details that may be relevant to the player. |
| `Info` | Info relevant to the player. This should be used judiciously. |
| `Warn` | Potential problems that the player should be aware of. This should be used rarely. |
| `Error` | A message indicating something went wrong. |
| `Alert` | Important information to highlight for the player when player action is needed (*e.g.,* new version available). This is mainly meant for SMAPI itself, and should almost never be used by mods. |

### Log once
The `LogOnce` method lets you log a message just like the above, but only once per game launch. For example, you can use it to show a compatibility warning for an API call:

```c#
this.Monitor.LogOnce("Some Mod Name used the deprecated X API.", LogLevel.Warn);
```

### Verbose logging
You can have messages appear in the log (and console in the *for developers* version) only if SMAPI's verbose logging option is enabled. This is meant for diagnostic information that's sometimes needed to troubleshoot, but doesn't need to be logged in most cases.

There are two ways to use it:

```c#
// log a TRACE message if verbose logging is enabled
this.Monitor.VerboseLog("This will only appear if verbose logging is enabled.");

// check the verbose option
if (this.Monitor.IsVerbose)
   this.Monitor("This will only appear if verbose logging is enabled.", LogLevel.Trace);
```

Players can enable verbose logging by adding your mod ID to the `VerboseLogging` field in the `smapi-internal/StardewModdingAPI.config.json` (or  `smapi-internal/config.user.json` file. For example, this enables it for SMAPI and Content Patcher:

```js
"VerboseLogging": [ "SMAPI", "Pathoschild.ContentPatcher" ],
```

## FAQs
### Where is the log file created?
It's output to the game's `ErrorLogs` data folder. See [smapi.io/log](https://smapi.io/log/) for help finding the log file, and you can also upload your log there to see what it looks like. The log file is always created (even if there are no errors) and it's written continuously (so you don't need to exit the game to see the log so far).

ru:Модификации:Руководство мододела/API/Журнал
zh:模组:制作指南/APIs/Logging
