> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Input)

# Modding:Modder Guide/APIs/Input (`Modder_Guide/APIs/Input`)

The input API lets you check and suppress controller/keyboard/mouse state.

## APIs
### Check button state
<dl>
<dt>`IsDown`</dt>
<dd>
You can check if any controller/keyboard/mouse button is currently pressed by calling the `IsDown(button)` method. For example:

```c#
bool isShiftPressed = this.Helper.Input.IsDown(SButton.LeftShift) || this.Helper.Input.IsDown(SButton.RightShift);
```

</dd>

<dt>`GetState`</dt>
<dd>
For more finetuned control, you can check the button state relative to the previous game tick:

```c#
SButtonState state = this.Helper.Input.GetState(SButton.LeftShift);
bool isDown = (state == SButtonState.Pressed || state == SButtonState.Held);
```

Available button states:

| previous tick | current tick | resulting state |
| --- | --- | --- |
| up | up | `None` |
| up | down | `Pressed` |
| down | down | `Held` |
| down | up | `Released` |

</dd>
</dl>

### Check cursor position
The `GetCursorPosition()` method provides the cursor position in three coordinate systems.

For example:

```c#
// draw text at the cursor position
ICursorPosition cursorPos = this.Helper.Input.GetCursorPosition();
Game1.spriteBatch.DrawString(Game1.smallFont, "some text", cursorPos.ScreenPixels, Color.Black);
```

### Send input
You can send controller/keyboard/mouse button input programmatically, which is equivalent to the player physically pressing the button.

| method | effect |
| --- | --- |
| `Press` | Mark a button as pressed. |

For example, this will make the player move left once:

```c#
SButton moveLeft = Game1.options.moveLeftButton[0].ToSButton();
this.Helper.Input.Press(moveLeft);
```

The button will be released on the next input tick by default; it can be 'pressed' again each tick to hold it down.

### Suppress input
You can prevent the game from handling any controller/keyboard/mouse button press (including clicks) by *suppressing* it. This suppression will remain in effect until the player releases the button. This won't prevent other mods from handling it.

| method | effect |
| --- | --- |
| `Suppress` | Suppress a specified `SButton` value. |
| `SuppressActiveKeybinds` | Suppress every button that's part of a specified `KeybindList`'s activated keybinds. |
| `SuppressScrollWheel` | Suppress a mouse wheel scroll. |
| `IsSuppressed` | Get whether a specified `SButton` value is currently suppressed. |

For example:

```c#
// prevent game from seeing that LeftShift is pressed
this.Helper.Input.Suppress(SButton.LeftShift);

// that works for clicks too:
this.Helper.Input.Suppress(SButton.MouseLeft);

// check if a button is being suppressed:
bool suppressed = this.Helper.Input.IsSuppressed(SButton.LeftShift);
```

Side-effects:
<ul>
<li>The `ButtonReleased` event will be raised on the next tick for the suppressed input.</li>
<li>Methods like `helper.Input.IsDown(button)` and `helper.Input.GetState(button)` will show the button as released for the duration of the suppression, even if it's physically still pressed. You can use `helper.Input.IsSuppressed(button)` to check if that's the case (it will only be true until the button is physically released):

```c#
bool isPhysicallyDown = helper.Input.IsDown(button) || helper.Input.IsSuppressed(button);
```
</li>
</ul>

## Data structures
### SButton
SMAPI's `SButton` is a constant which includes every [controller](https://docs.microsoft.com/en-us/previous-versions/windows/xna/bb975202(v%3dxnagamestudio.40)), [keyboard](https://docs.microsoft.com/en-us/previous-versions/windows/xna/bb197781(v%3dxnagamestudio.40)), and [mouse](https://docs.microsoft.com/en-us/previous-versions/windows/xna/bb198097(v%3dxnagamestudio.40)) button. SMAPI events use this to let you handle button presses without needing separate code for each. See Modding:Player Guide/Key Bindings for a list of values.

SMAPI provides extensions to convert any of the other constants to `SButton`:

```c#
SButton key = Keys.A.ToSButton(); // SButton.A
SButton button = Buttons.A.ToSButton(); // SButton.ControllerA
SButton input = new InputButton(true).ToSButton(); // SButton.MouseLeft
```

You can also convert `SButton` to the other constants. This uses a `TryGet` approach since `SButton` is a superset of the others (*e.g.,* you can't convert `SButton.ControllerA` to a keyboard value):

```c#
SButton value = SButton.A;
if (value.TryGetKeyboard(out Keys key))
   ...;
if (value.TryGetController(out Buttons button))
   ...;
if (value.TryGetStardewInput(out InputButton input))
   ...;
```

Two last extensions let you check how the button is mapped in the game:

```c#
SButton button = SButton.MouseLeft;
if (button.IsUseToolButton())
   // use tool
else if (button.IsActionButton())
   // perform action
```

You can use `SButton` values directly in your config model, but `KeybindList` is recommended instead in most cases.

### KeybindList
SMAPI's `KeybindList` utility lets you manage an arbitrary set of keybindings. A *keybind list* has any number of *keybinds*, each of which has any number of button codes. For example, the keybind list `"F2, LeftShift + S"` would be pressed if (a) `F2` is pressed, *or* (b) both `LeftShift` and `S` are pressed.

You can use a `KeybindList` directly in your `config.json` model:

| C# model |   | JSON file |
| --- | --- | --- |
| 
```c#
class ModConfig
{
   public KeybindList ToggleKey { get; set; } = KeybindList.Parse("LeftShift + F2");
}
```
 | → | 
```json
{
   "ToggleKey": "LeftShift + F2"
}
```
 |

And you can then check whether it's pressed directly in your code. For example, in a `ButtonsChanged` event handler:

```c#
private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
{
   if (this.Config.ToggleKey.JustPressed())
   {
      // perform desired action
   }
}
```

The `KeybindList` provides a number of methods depending on your use case:

| member | description |
| --- | --- |
| `KeybindList.Parse(…)`; `KeybindList.TryParse(…)` | Parse a keybind string like `"F2, LeftShift + S"` into a keybind list. |
| `IsBound` | Whether the keybind list has any keys bound. For example, this would be false for the strings `"None"` or `""`. |
| `Keybinds` | The individual keybinds. In most cases you shouldn't use these directly. |
| `GetState()` | Get the overall keybind state relative to the previous tick. This state is transitive across keybinds; *e.g.,* if the player releases one keybind and immediately presses another within the list, the overall state is `Held`. |
| `IsDown()` | Get whether any keybind in the list is currently down (*i.e.,* the player pressed or is holding down the keys). |
| `JustPressed()` | Get whether the player just activated the keybind during the current tick (*i.e.,* `GetState()` returns `Pressed` instead of `Held`). |
| `GetKeybindCurrentlyDown()` | Get the individual `Keybind` in the list which is currently down, if any. If there are multiple keybinds down, the first one is returned. |
| `ToString()` | Get a string representation of the input binding (*e.g.,* `"F2, LeftShift + S"`). |

Caveats:
- **Don't use the `ButtonPressed` event to check keybinds**, since it's raised once for each button pressed. If the player presses two keys at once, your keybind would be activated twice. Use `ButtonsChanged` instead.

### ICursorPosition
SMAPI's `ICursorPosition` provides the cursor position in four coordinate systems:
- `AbsolutePixels` is the pixel position relative to the top-left corner of the in-game map, adjusted for zoom but not UI scaling.
- `ScreenPixels` is the pixel position relative to the top-left corner of the visible screen, adjusted for zoom but not UI scaling.
- `Tile` is the tile position under the cursor.
- `GrabTile` is the tile position that the game considers under the cursor for the purposes of clicking actions. This automatically accounts for controller mode. This may be different than `Tile` if that's too far from the player.

This is returned by the `this.Helper.Input.GetCursorPosition()` method and in the event args for some input events.

**The pixel positions are *not* adjusted for UI scaling** (*i.e.,* they're non-UI mode). Whether you need UI or non-UI positions depends how you're using them, so you can use `cursorPos.GetScaledAbsolutePixels()` or `cursorPos.GetScaledScreenPixels()` to adjust them automatically for the current mode or `Utility.ModifyCoordinatesForUIScale` to always get UI mode coordinates.

## See also
- Input events
- Modding:Player Guide/Key Bindings for a list of valid `SButton` values

ru:Модификации:Руководство мододела/API/Ввод
zh:模组:制作指南/APIs/Input
