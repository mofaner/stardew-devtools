> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Translation)

# Modding:Modder Guide/APIs/Translation (`Modder_Guide/APIs/Translation`)

The translation API lets you translate your mod, and include all languages in one release package for SMAPI to use automatically based on the game language.

## Overview
SMAPI reads translations from files in your mod folder. When you request a translation, it will automatically handle locale fallback (*e.g.,* if a translation isn't available in `pt-BR.json`, SMAPI will check `pt.json` and `default.json`). Translations can be a simple string, or they can include tokens to inject values into the translation.

## i18n folder
### File structure
SMAPI reads translations from JSON files in an `i18n` subfolder in your mod folder:

```text
YourMod/
   i18n/
      default.json
      es.json
      fr.json
   manifest.json
   YourMod.dll
```

The `default.json` file has the default text shown if a more specific translation isn't available, and you create a separate file for each language. Each translation file should have one of these file names:

| Language | File name |
| --- | --- |
| Chinese | `zh.json` |
| French | `fr.json` |
| German | `de.json` |
| Hungarian | `hu.json` |
| Italian | `it.json` |
| Japanese | `ja.json` |
| Korean | `ko.json` |
| Portuguese | `pt.json` |
| Russian | `ru.json` |
| Spanish | `es.json` |
| Turkish | `tr.json` |

For large mods, you can optionally have a folder for each language containing any number of `.json` files. This isn't recommended for most mods though, since it's harder to keep in sync between languages and is less supported by translation tools. For example:

```text
YourMod/
   i18n/
      default/
         dialogue.json
         events.json
      fr/
         dialogue.json
         events.json
   manifest.json
   YourMod.dll
```

You can't have both top-level files (like `fr.json`) and folders (like `fr/dialogue.json`), so you'll need to use one pattern consistently.

For a custom language, use its `LanguageCode` value in the filename.

### File format
Each `.json` file should contain a flat key→value lookup with your default text. Each key is case-insensitive, and can contain alphanumeric, underscore, hyphen, and dot characters. Feel free to add JavaScript comments to organise or document your translations. For example:

```javascript
{
    // example translations
    "item-type.label": "Item type",
    "item-type.fruit-tree": "{{fruitName}} tree",
}
```

The `{{fruitName}}` in the above example is a token. You can add any tokens you want (each having only letters in the name), and replace them with a different value in code (see *reading translations* below).

### Tips for translators
- Save i18n files with UTF-8 encoding to avoid broken symbols in-game.
- Type `reload_i18n` into the SMAPI console and hit enter to reload translations without exiting the game. (If a mod internally cached a translation, it may not be updated.)

## Reading translations
### Built-in API
Once your i18n files are set up, you can read translations for the current locale:

```c#
// read a simple translation
string label = helper.Translation.Get("item-type.label");

// read a translation which uses tokens (accepts an anonymous object, dictionary, or model)
string text = helper.Translation.Get("item-type.fruit-tree", new { fruitName = "apple" });
```

The `helper.Translate(…)` method returns a fluent interface — you can keep calling methods on its return value to customise the translation. (See IntelliSense for a description of the available methods.) To get the text, just assign it to a string:

```c#
// use fluent chain
string text = helper.Translation.Get(key).Tokens(tokens).Tokens(moreTokens).Default("missing translation?");
```

All translations can use gender switch blocks. They'll be formatted based on the player's gender each time you fetch the translation.

### Strongly-typed API
The built-in API doesn't have build-time validation. For example, if you set a `fruitName` argument but the translation actually uses `{{fruit}}`, you won't know until you test each translation in-game and see the broken message. That's fine for most mods, but can be an issue with larger mods that have hundreds of translations across many different UI flows.

The optional [SMAPI mod translation class builder](https://github.com/Pathoschild/SMAPI-ModTranslationClassBuilder#readme) package lets you generate a strongly-typed class to read translations like this instead:

```c#
string label = I18n.ItemType_Label();
string text = I18n.ItemType_FruitTree(fruitName: "apple");
```

If a translation breaks, you'll know immediately since it won't compile.

## See also
[LINQ script](https://gist.github.com/Pathoschild/4e0af42158583983a4206d4d734bfc0b) to format an event for translations

ru:Модификации:Руководство мододела/API/Перевод
tr:Modlama:Mod Rehberi/API'ler/Çeviri
zh:模组:制作指南/APIs/Translation
