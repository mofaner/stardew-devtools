> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/Release)

# Modding:Modder Guide/Release (`Modder_Guide/Release`)

Once you're ready to share your mod, it's time to release it!

## Prepare the release package
### For SMAPI mods
The 'release package' for a SMAPI mod is just a `.zip` file containing a mod with your compiled files, `manifest.json`, any `i18n` files, etc. The NuGet package will create it for you automatically.

#### Generate package
# Edit your mod's `manifest.json` to increase the version.  See [semantic version](http://semver.org/) for more information on version numbering.
# Click *Build > Rebuild Solution* (Visual Studio) or *Build > Rebuild All* (MonoDevelop) to make sure the project is compiled.
# Open your mod project's `bin/Debug` or `bin/Release` folder (depending on your build configuration).
# There should be a `.zip` file there for your mod version.

That `.zip` file is the release package for your mod, ready to upload.

#### Troubleshoot package
Here's how to fix common issues with the generated `.zip` file.

- The `.zip` file is missing:
  - Make sure you're looking at the right folder (either `bin/Debug` or `bin/Release`).
  - Make sure there are no build errors.

- Some mod files aren't in the package:
  - If it's a mod file: right-click on the file in Visual Studio or MonoDevelop, choose *Properties*, and change 'Copy to Output Directory' to 'copy if newer'.
  - If it's an assembly reference: right-click on the reference in Visual Studio or MonoDevelop, choose *Properties*, and change 'Copy Local' or 'Local Copy' to true.

- Some mod files that shouldn't be in the package are included:
  - If it's a mod file: right-click on the file in Visual Studio or MonoDevelop, choose *Properties*, and change 'Copy to Output Directory' to 'do not copy'.
  - If it's an assembly reference: right-click on the reference in Visual Studio or MonoDevelop, choose *Properties*, and change 'Copy Local' or 'Local Copy' to false.

### For content pack mods
The 'release package' for a content pack mod is just a `.zip` file containing a mod with your files, which must include at least a `manifest.json` and a `content.json`.  See the specific guidelines for your framework mod that you are using. 

#### Create a content pack
# Create a folder with the name you want (see folder naming convention).
# Inside the folder:
#* Add a `manifest.json` file with the `ContentPackFor` field (see manifest format).
#* Add the files needed by the mod that will read it. (See the instructions for the mod for which you're creating the content pack.)
# Launch the game, and make sure your new content pack appears under "Loaded X content packs" in the SMAPI console.

#### Release your content pack
# Add an update key to your manifest (if you haven't already).
# Update the `Version` field in the manifest. (Increase it for each release! SMAPI will use it for update and compatibility checks.)  See [semantic version](http://semver.org/) for more information on version numbering.
# Create a `.zip` file containing the content pack's folder.
# Upload that file to your chosen release site.  See below guide to sites.

## Release the mod
### Using Nexus Mods
#### Create a mod page
If you haven't already, create a mod page on [Nexus Mods](http://www.nexusmods.com/stardewvalley). Ideally your mod description should...
- explain what the mod does and how to use it;
- provide clear install steps;
- explain which version of the game it works with;
- say whether it works in multiplayer (and list any multiplayer limitations);
- link to release notes, source code, discussion thread, etc if applicable.

Here's a recommended template for the mod description:

<ol>
<li>Click the "[BBCODE]" button above the mod description on Nexus. This will switch to raw text mode.</li>
<li>Paste this template in:

```text
This mod adds pineapples everywhere in the game. Replace this line with a few sentences explaining your mod.

[size=5]Install[/size]
[list=1]
[*][url=https://smapi.io]Install the latest version of SMAPI[/url].
[*]Download this mod and unzip it into [font=Courier New]Stardew Valley/Mods[/font].
[*]Run the game using SMAPI.
[/list]

[size=5]How to use[/size]
Provide a few sentences explaining how to use your mod. For example, the default buttons to press, where to find a menu, etc. You can remove this section if it's self-evident.

[size=5]Compatibility[/size]
[list]
[*]Works with Stardew Valley 1.5.6 on Linux/macOS/Windows.
[*]Works in single player, multiplayer, and split-screen mode. If there are multiplayer limitations, explain them here.
[/list]

[size=5]See also[/size]
Add links below for your mod, and remove any that don't apply. Don't forget to remove this sentence.

[list]
[*]Official discussion thread
[*]Release notes
[*]Source code
[/list]
```

</li>
<li>Click the "[BBCODE]" button to switch back to the formatted view.</li>
<li>Edit the template for your mod.</li>
</ol>

#### Add update keys to your manifest
Update keys tell SMAPI where your mod is released, so it can let players know when a new version is available. This requires your mod ID, which is available as soon as you create the mod page (before uploading the mod files). See the update check docs for more info.

#### Upload the mod release

To upload a file to Nexus:
# From your mod page, go to *Manage > Files*.
# Fill in the form:
#* 'File name' should have the mod name and version (like `PineapplesEverywhere 1.0`).
#* 'File version' field should match the version in your `manifest.json`! If it doesn't, players may get incorrect update alerts.
#* Tick the "this is the latest version" checkbox so players get update alerts.
#* 'File description' is up to you. You can mention the minimum SMAPI version (if any), what changed, link to release notes, etc.
# Upload the `.zip` file you prepared above.

### Using ModDrop
ModDrop used to be an alternative location to host your mods. However, **as of February 2026 ModDrop appears to be unmoderated and potentially unsafe**. Mod Developers should, however, monitor ModDrop for stolen rereleases of their mods.

#### Release via webpage
<ol>
<li>Go to [*ModDrop*](https://www.moddrop.com/stardew-valley/) and choose `Upload Your Mod`.</li>
<li>Confirm that the mod is yours to publish to proceed.</li>
<li>Choose `Upload a File from Your Computer`.</li>
<li>Choose a name for your mod and upload the `.zip` file you prepared above. Page will display a green check next to the mod name if that name is available to use.</li>
<li>Choose the name of the mod file you want to display, the version number, release status (alpha, beta, release), add a description (see the Nexus description tips), and optionally add patch notes or notes for yourself.</li>
<li>Upload an image for your mod.</li>
<li>You'll now have a chance to create a more detailed mod page, including links to other pages where your mod can be found.  You may also add more files, images, videos, and notices to be displayed to users.  You can also choose a category for your mod.  If you are uploading on someone else's behalf (with permission), there is also a section to provide credit.</li>
<li>Confirm again that you have permission to upload the mod to publish.  You may cancel out of this page and resume at a later time.</li>
</ol>

#### Release via app
<ol>
<li>Open [*ModDrop app*](https://www.moddrop.com/app/) and click on your username (top right corner).  Choose `Forge`.</li>
<li>Choose `Publish a new mod`.</li>
<li>Follow the same steps as listed above for the site.</li>
</ol>

#### Sync Nexus mods to ModDrop
<ol>
<li>Open [*ModDrop app*](https://www.moddrop.com/app/) and click on your username (top right corner).  Choose `Forge`.</li>
<li>Choose `Sync my mods from another site`.</li>
<li>Follow the prompts to add your Nexus API key.</li>
<li>Choose `Find new mods to sync` and follow the prompts.</li>
<li>Confirm that the mod is yours to publish and that ModDrop is allowed to sync updates from your Nexus account.</li>
<li>You will receive a confirmation that the request is complete.  ModDrop reviews all submissions, so it may take some time for your mod to appear on the app/site.</li>
</ol>

#### Add update keys to your manifest
Update keys tell SMAPI where your mod is released, so it can let players know when a new version is available. This requires your mod ID, which can be found after creating the mod page. See the update check docs for more info.

## Go open-source
Making your mods open-source is highly recommended; see Modding:Open source for more info.

es:Modding:Guía del Modder/Lanzamiento
ru:Модификации:Руководство мододела/Публикация
zh:模组:制作指南/发布
