> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Modder_Guide/Test_and_Troubleshoot)

# Modding:Modder Guide/Test and Troubleshoot (`Modder_Guide/Test_and_Troubleshoot`)

This page helps you test your mods and solve common issues. **For issues *using* mods, see Modding:Player Guide/Troubleshooting.**

## Test the mod
### Basic testing
Testing is pretty straightforward for most mods:
# Click *Build > Rebuild Solution* (Visual Studio) or *Build > Rebuild All* (MonoDevelop).
# Make sure there are no build errors and the mod gets copied to your `Mods` folder.
# Try the mod in-game.
# Make sure there are no errors or warnings for your mod in the SMAPI console.

In general, if a mod works on one platform it'll work fine on the others.

### Testing in multiplayer
You can test mods in multiplayer on the same computer, by launching two instances of the game:

# Prepare player one:
## Launch SMAPI like usual.
## From the title screen: click *co-op*, then *host*.
## Start a new save slot (unless you've already created one). Make sure to set 'starting cabins' to at least one (you'll need one cabin per extra player).
# Prepare player two:
## Launch SMAPI again. (This will automatically create a separate log file.)
## From the title screen: click *co-op*, then *join LAN game*.
## Leave the 'Enter IP...' box empty and click OK.

### Testing on all platforms
For complex mods, you may need to test your mod on all platforms. The game is mostly the same on Linux/Mac, so you only need to test your mod twice: once on Windows, and again on Linux or Mac. You can do that by testing one version on your computer, and the other in a virtual machine.

<dl>
<dt>If your main computer is Windows 10 or 11:</dt>
<dd>
<ol>
<li>[Install Windows Subsystem for Linux (WSL)](https://docs.microsoft.com/en-us/windows/wsl/install).</li>
<li>Install the required software in WSL:</li>
<ol>
<li>[Install Steam](https://linuxconfig.org/how-to-install-steam-on-ubuntu-20-04-focal-fossa-linux).</li>
<li>Launch `export TERM=xterm && steam`, then install & launch Stardew Valley through its UI. This will also install its dependencies.</li>
<li>*(optional)* Download and install your preferred IDE, if you plan to compile the code on Linux. For the [latest standalone Rider version](https://www.jetbrains.com/help/rider/Installation_guide.html#prerequisites) (not free):

```sh
wget "<download url here>" -O rider-install.tar.gz
sudo tar -xzvf rider-install.tar.gz -C /opt
ln -s "/opt/JetBrains Rider-<version>/bin/rider.sh"
./rider.sh
```
</li>
<li>Install SMAPI.</li>
</ol></li>
<li>To launch the game, launch `steam` and run the game through its UI.</li>
</ol></li>
</ul>
</dd>
</dl>

- **If your main computer is Windows 8 or earlier:**: # Install [VirtualBox](https://www.virtualbox.org/).
  # Create a [ZorinOS Core](https://zorinos.com/) VM in VirtualBox.
  #* *See [this setup guide](https://extr3metech.wordpress.com/2013/09/05/installing-zorin-os-7-in-virtual-box-screenshots) for more details. The ZorinOS installer might be a bit different than shown, but should be pretty intuitive.*
  #* *If you don't see any options for 64-bit OSes in VirtualBox, see [how to enable them](https://superuser.com/a/866963).*
  #* *When creating the virtual disk, at least 20GB is recommended.*
  # [Download the Steam installer](https://store.steampowered.com/about) in the VM and run it.
  # Launch Steam to finish installation. If nothing happens, see [these extra steps](https://askubuntu.com/questions/771032/steam-not-opening-in-ubuntu-16-04-lts) to fix it.
  # Install Stardew Valley through Steam.
  # Install SMAPI.
  # *(optional)* Install [`mono-complete`](https://www.mono-project.com/) and [MonoDevelop](http://www.monodevelop.com/download/) in your VM. This is only needed if you want to compile separately for Linux/Mac. When installing `.deb` files, use the instructions for [the Ubuntu version shown here](https://zorinos.com/help/install-apps/#deb-files). If you run into errors, may Linux have mercy on your soul.
  # *(optional)* For unlocking Mac OS only: [Virtual Machine Unlocker 2.1.1](https://www.insanelymac.com/forum/files/file/838-unlocker/) for VmWare Workstation 11/12/14, VmWare Player 7/12/14, or Fusion 7/8/10.  **This is needed to boot Mac OS on a virtual Machine**

- **If your main computer is Linux or macOS:**: # Install [VirtualBox](https://www.virtualbox.org/).
  # [Create a VM with Windows](http://www.macworld.co.uk/how-to/mac-software/run-windows-10-on-your-mac-using-virtualbox-3621650/).
  # Install Stardew Valley in your VM.
  # Install SMAPI.
  # *(optional)* Install [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) in your VM. This is only needed if you want to compile separately for Windows.

## Fix common build warnings
After building your project, you can see build warnings via *Visual Studio > View > Error List* or *MonoDevelop > View > Pads > Errors*. Here are some common ones.

### This implicitly converts...
Sample warning: "*This implicitly converts '{0}' from Net{1} to {2}, but Net{1} has unintuitive implicit conversion rules. Consider comparing against the actual value instead to avoid bugs. See https://smapi.io/buildmsg/avoid-implicit-net-field-cast for details.*"

Your code is referencing a net field, which can cause subtle bugs. This field has an equivalent non-net property, like `monster.Health` (`int`) instead of `monster.health` (`NetBool`). Change your code to use the suggested property instead.

### FieldName is a Net* field...
Sample warning: "**{0}' is a Net{1} field; consider using the {2} property instead. See https://smapi.io/buildmsg/avoid-net-field for details.*"

Your code is referencing a net field, which can cause subtle bugs. You should access the underlying value instead:
<ul>
<li>For a reference type (*i.e.,* one that can contain `null`), you can use the `.Value` property (or `.FieldDict` for a `NetDictionary`):

```c#
if (building.indoors.Value == null)
```

Or convert the value before comparison:

```c#
GameLocation indoors = building.indoors.Value;
if(indoors == null)
   // ...
```
</li>
<li>For a value type (*i.e.,* one that can't contain `null`), check if the parent is null (if needed) and compare with `.Value`:

```c#
if (item != null && item.category.Value == 0)
```
</li>
</ul>

### The FieldName field is obsolete...
Sample warning: "*The 'Character.friendships' field is obsolete and should be replaced with 'friendshipData'. See https://smapi.io/buildmsg/avoid-obsolete-field for details.*"

You're referencing a field which should no longer be used. Use the suggested field name instead to fix it.

### An instance of analyzer ... cannot be created
Update to the latest [Visual Studio](https://visualstudio.microsoft.com/vs/community/); the NuGet package uses a recent feature that isn't available in older versions.

### Feature 'global using directive' is not available in C# 9.0
Go to your solution's project file (the .csproj file) and change the <ImplicitUsings> property from "enable" to "disable".

## Other issues
### Can't target .NET 6
If the target framework list has options starting with...
- *.NET Framework*: you created the wrong type of project. Make sure you create a .NET 6 project for your mod instead. (The naming is a bit confusing.)
- *.NET Core*, *.NET Standard*, or *.NET 6+*: use .NET 6 for compatibility with the game. If you don't have that option, you can install the [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) to add it.

### Visual Studio can't find the game/SMAPI/MonoGame DLLs

Common solutions:
- Restart Visual Studio.
- Make sure the game and SMAPI are correctly installed and work fine.
- Check for an error like "*Failed to find game install path*". If it's present, you need to [specify your game path](https://smapi.io/package/custom-game-path).
- Make sure you created a .NET 6 project, **not** .NET Framework. (See how to set the target framework; if you see options starting with .NET Framework, delete the project and create a .NET 6 project instead.)
- Make sure you target .NET 6 (see how to).

If those didn't fix it:
# Click *Build > Rebuild Solution* (Visual Studio) or *Build > Rebuild All* (MonoDevelop).
# Check the *Output* pane or error list (Visual Studio), or the *Errors* pad (MonoDevelop).
# If you don't see anything relevant, post the *Output* text to [hastebin](https://hastebin.com), ask for help on Discord, and include a link to your hastebin.

## Ask for help
See Modding:Help for how to get help!

es:Modding:Guía del Modder/Prueba y solución de problemas
pt:Modificações:Guia do Modder/Teste e Solução de Problemas
ru:Модификации:Руководство мододела/Тестирование и отладка
zh:模组:制作指南/测试和疑难解答
