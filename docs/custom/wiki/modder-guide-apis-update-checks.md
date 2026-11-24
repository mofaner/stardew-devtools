> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks)

# Modding:Modder Guide/APIs/Update checks (`Modder_Guide/APIs/Update_checks`)

Every SMAPI mod or content pack can benefit from automatic update checks, so the player sees an alert when a new version is available. This helps ensures players always have the latest version of your mod, which improves their experience and reduces support requests.

## Introduction
### Enable update checks
You can add *update keys* to your `manifest.json` to enable update checks. An update key tells SMAPI where the mod page is. For example, `Nexus:2400` means 2400 mod ID 2400 on Nexus:

```json
"UpdateKeys": [ "Nexus:2400" ]
```

If you have multiple update keys, SMAPI will check them all and show an alert for the latest version it finds. (If multiple sites have the latest version, it will link to the earlier one in the list.)

```javascript
"UpdateKeys": [ "Chucklefish:4250", "Nexus:541", "GitHub:Pathoschild/LookupAnything" ]
```

(SMAPI also fetches mod info from the mod compatibility list, so mods on that page may get update checks even without update keys. You should still add update keys to your manifest in case that changes, and to support mod managers and other tools.)

### Valid sites
Here are the supported mod sites:

| mod site | description |
| --- | --- |
| [Chucklefish](https://community.playstarbound.com/resources/categories/stardew-valley.22/) | **(Deprecated)** Make sure you have a mod release page (with a URL containing `/resources/` instead of `/thread/`) and it has a [semantic version](http://semver.org/), then specify the mod ID (the number in the mod page URL). 
```javascript
"UpdateKeys": [ "Chucklefish:4250" ]
```
 |
| [CurseForge](https://www.curseforge.com/stardewvalley) | Make sure the latest file's display name contains a [semantic version](http://semver.org/) at the end, with a space before the version and optional file extension. For example, SMAPI will recognise the version in these display names: `1.3.0`, `Example Mod 1.10`, `Example Mod 1.10.0-prerelease.zip`. 
```javascript
"UpdateKeys": [ "CurseForge:309243" ]
```
 |
| [GitHub](https://github.com/) | Make sure your [GitHub project has at least one release](https://help.github.com/articles/creating-releases/) and the latest release's tag is a [semantic version](http://semver.org/), then specify your GitHub username and project name. 
```javascript
"UpdateKeys": [ "GitHub:Pathoschild/LookupAnything" ]
```
 SMAPI will get the version from the release tag. Note that with the way GitHub release tags work, they do not support "monorepos", when multiple mods are on the same repository with different version numbers. |
| [ModDrop](https://www.moddrop.com/stardew-valley/) | Make sure the mod page has a [semantic version](http://semver.org/), then specify the mod ID (the number in the mod page URL). 
```javascript
"UpdateKeys": [ "ModDrop:123338" ]
```
 SMAPI will get the version from files under 'Main Versions'. |
| [Nexus Mods](https://www.nexusmods.com/stardewvalley) | Make sure the Nexus mod has a [semantic version](http://semver.org/), then specify the mod ID (the number in the mod page URL). When creating a new mod on Nexus, the ID is added to the URL after the first step, before you need to upload the file. 
```javascript
"UpdateKeys": [ "Nexus:541" ]
```
 SMAPI will get the version from the mod, 'main files' downloads, or 'optional files' downloads (whichever is higher). |

## Advanced
### Beta versions
A *prerelease version* (often called an *alpha* or *beta*) has a prerelease tag in the version number. For example, `1.0.0-beta.5` is a prerelease version of an upcoming `1.0.0`.

If you have both prerelease and non-prerelease versions, update checks depend on the player's installed mod version:
- Stable channel: players with a non-prerelease version installed only see update alerts for non-prerelease versions (unless the main mod version is prerelease).
- Beta channel: players with a prerelease version see update alerts for any newer version, whether prerelease or non-prerelease.

For example, let's say your mod has two current versions: `1.7.0` and `2.0.0-beta`. A player who has `1.6.0` installed would see an update alert for `1.7.0`, and a player with `1.6.1-beta` would see an update alert for `2.0.0-beta`.

### Update subkeys
SMAPI assumes each mod page is about a single mod, so the highest file version is the latest one. That means having multiple mods on the same page can cause false update alerts. For example, let's say you have one Nexus page with two different mods in the downloads: *Geode Crusher* (version `1.0.5`) and *Diamond Crusher* (version `2.1.0`). If a player has the latest version of *Geode Crusher* installed, they would always get update alerts for version `2.1.0` even though that's a different mod.

You can add an *update subkey* at the end of your normal update key after `@`:

```json
"UpdateKeys": [ "Nexus:2400@GeodeCrusher" ]
```

When SMAPI fetches available versions from the page, it will only consider files that have `@GeodeCrusher` (**including `@`**) somewhere in the title or description.

Caveats:
- If no files match the subkey, SMAPI will ignore the subkey and perform a normal update check.
- For mods on CurseForge, SMAPI won't see subkeys in the file description since that's not available through the CurseForge API.
- For mods on GitHub, subkeys don't really do anything since only the latest release is fetched.

### Disable update checks locally
SMAPI checks for updates in the background, so disabling update checks has zero effect on load times.

To disable update checks...
- For SMAPI and all mods: edit the `smapi-internal/config.json` file and set `CheckForUpdates` to false. You'll no longer be notified about newer versions of SMAPI or installed mods, so this is not recommended.
- For a specific mod: edit the `smapi-internal/config.json` file and add the mod ID to the `SuppressUpdateChecks` field.

Note that edits to `smapi-internal/config.json` will be lost when you update SMAPI. See the instructions at the top of that file to create a permanent config file.

### Update check algorithm
Here's how SMAPI decides which update alerts to show in the SMAPI console for a mod:

# Collect update keys:
## Get the initial update keys from the mod's manifest.
## Match the mod ID with the mod compatibility list and `smapi-internal/metadata.json` to add any other update keys that aren't in the manifest.
## Apply update key overrides from the mod compatibility list.
# Get the possible updates:
## Collect versions from every matched update key (see *valid sites* for which versions are used).
## Filter the list of versions to...
##* the main mod version;
##* the optional prerelease version if the player has a beta version of SMAPI, *or* they have an older prerelease mod version installed, *or* the installed version failed to load;
##* the unofficial version from the mod compatibility list;
##* and the unofficial version for the beta, if using a prerelease version of SMAPI.
# Recommend the highest version from the filtered list (if newer than the installed version).

If multiple mod pages have the recommended update, SMAPI links to the first match in this order:
# pages listed in the mod manifest's update keys (in the order listed);
# the default update key from SMAPI's `smapi-internal/metadata.json`;
# update keys from the mod compatibility list (in the order Nexus, ModDrop, CurseForge, and Chucklefish);
# update key overrides from the mod compatibility list.

### Custom update manifest
**This is highly specialized** and supports specific edge cases (like mods which only exist in a shared GitHub repository or custom website). Most mods should use normal update keys instead.

An *update manifest* is a JSON file hosted on a website, which the SMAPI servers can download to get info about mods which aren't available on a more usual site like Nexus. For example, this manifest shows info about a mod which is only hosted on a custom website:

```js
{
    "Format": "4.0.0",
    "Mods": {
        "ExampleMod": {
            "Name": "Example Mod",
            "ModPageUrl": "https://example.org/mods/example-mod",
            "Versions": [
                { "Version": "1.0.0" }
            ]
        }
    }
}
```

Here are the expected fields (all required unless noted otherwise):

| field | usage |
| --- | --- |
| `Format` | The version of the manifest format (currently `4.0.0`). This is used to parse older manifests correctly if later versions of SMAPI change the format. |
| `Mods` | The mods for which info is provided. Any number of mods can be listed. This consists of a string → model lookup, where... * The key is the update subkey (see details below the table). * The value is a model with the fields listed below. Each mod has these fields: |
| field | usage |
| `Name` | The human-readable display name for the mod. |
| `ModPageUrl` | The URL of the page from which the player can download updates. |
| `Versions` | The versions available to download. Any number of versions can be listed, and they'll be compared to the player's installed version using the usual update check algorithm. This consists of a list of models with these fields: |
| field | usage |
| `Version` | The semantic version number. |
| `ModPageUrl` | *(Optional)* The URL of the page from which the player can download this update, if different from the `ModPageUrl` for the mod itself. |

|}
|}

To use the manifest, a mod must add an update key in the form `UpdateManifest:<full URL to json file>@<mod key>`. If the example above was hosted at `https://example.org/mod-updates.json`, then its update key would look like this:

```js
"UpdateKeys": [ "UpdateManifest:https://example.org/mod-updates.json@ExampleMod" ]
```

ru:Модификации:Руководство мододела/API/Проверка обновлений
zh:模组:制作指南/APIs/Update checks
