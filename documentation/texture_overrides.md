![Documentation Image](images/documentation_header.png)

**THIS DOCUMENT IS FOR UPCOMING CHANGES TO MOD MANAGER AND MAY CHANGE OR NOT MAKE IT TO RELEASE. TOPICS DISCUSSED MAY NOT YET BE AVAILABLE.**

The M3 Texture Override (M3TO) system is a new feature included in Mod Manager 9.2 and up. This system significantly changes how texture modding can be achieved in Mass Effect Legendary Edition.

### Context

For the past several years, texture modding worked as follows with Mass Effect Modder (MEM), the standard texture replacement tool for the games:

   - Textures must be installed last
   - [ORIGINAL TRILOGY ONLY] Empty mips MUST be removed in all packages in the game (or LODs could not be raised)
   - Packages (that contain textures) cannot be installed after textures are installed
   - Changes to any game's package (edit, add, remove) will desync the texture map, preventing further MEM texture modifications to the game

Preventing additional package changes also helped ensure game stability, as textures attempting to be accessed out of bounds of their TFC file would immediately crash the game, and changing packages after install could introduce this.

When the Legendary Edition came around, we were given guidance from BioWare that allowed us to avoid having to edit every package to remove empty mips, as well as not having to adjust LODs at all. If you haven't ever had to think about LODs, consider yourself lucky 🙂 The other rules remain, which can be burdensome for users, as it requires game restores in order to change what mods are installed.

MEM installed textures based on the CRC of the top mip. All textures with the same CRC would be globally replaced by a file in the .mem file targeting that specific CRC. The benefit of this is that you could introduce new files that had copies of that texture and they'd also be updated by MEM.


### M3TO: Texture overrides without package edits

The M3TO system can replace textures at runtime without the need for any in-game package edits. This is achieved by hijacking texture serialization and replacing mip data with our own on exports that have a matching full path in a Binary Texture Package (BTP) file. Using .m3to files shipped in your DLC folder, Mod Manager can compile the referenced texture overrides into single Binary Texture Package for your mod that is loaded by the new `Texture Override` ASI.

<img width="936" height="723" alt="image" src="https://github.com/user-attachments/assets/d9460c51-bb72-4411-a778-8fe4040b2e1d" />

This ASI adds new features for texture loading to the game:
 -  Texture2D export (and subclasses) are modified at runtime if the memory path of the texture matches one a loaded BTP file. If there is an override for that texture, the original data is deallocated and replaced with the new data from the manifest. To the game, nothing is different.
 - LE2/LE3: DLC TFCs now load earlier, allowing you to override textures that load before DLC mount, such as EntryMenu, Human male Eye Diff, etc. Prior to this, you'd have to ship the TFC in the basegame, otherwise the higher mips would turn black.
 -  LE3: You can now use multiple TFCs in your DLC mod instead of just `Textures_DLC_MOD_XYZ.tfc`

Mods can ship a pre-compiled BTP file, or add .m3to files to the DLC folder. You can add multiple .m3to files, and all will be combined into a single BTP file for your mod at install time, allowing you to add customization options that can be added through the conditionals system in Mod Manager, allowing users to pick texture overrides. Texture overrides follow the standard DLC mount priority system, so higher mounting texture overrides will supercede lower tier ones.


### Differences from using .mem

This new system allows you to use texture overrides in the same system as DLC mods - enabling, disabling, as well using all the standard mod manager features for DLC mods.

**Pros:**
 - Texture installation is faster by orders of magnitude
 - You don't need to reset the game to make changes to your mod configurations
 - First class support in Mod Manager, including mix/match options.
 - You can include m3to's in a standard mod to allow changing texture options in your own mod without shipping duplicate packages
 - It is compatible with MEM installed texture mods if a user uses them still, but the usual nothing-after-mem-textures rules still apply when mixed with MEM.

**Cons:**
 - File sizes compared to .mem files is slightly larger due to having to use less efficient compression techniques (about 10%)
 - Textures are overriden by MemoryPath and not by CRC, so new same-crc textures won't be overridden. Developers should not be making new copies of texture exports without changes, so if this is an issue, developers need to properly design their mods
 - MEM is well tested, this is not (yet) :)

ME3Tweaks Mod Manager 9.2 includes a 'Convert MEM to TO' feature that can convert .mem files to a basic texture override mod that vanilla's the game, installs the .mem, then extracts the changes and builds an optimized installation for you. This process takes some time but only needs to be done by developers.


### M3TO setup
> [!NOTE]
> Using the M3 Texture Override system requires you to set your moddesc version to 9.2 or higher.

Building a mod that uses the M3TO system requires the following files:
 - At least one `TextureOverride-[WhateverNameYouWant].m3to` file
 - At least one `TO_` package that contains your texture overrides
 - .tfc files if your overrides use external streaming mips

Texture overrides must be stored in package files that have filenames starting with `TO_`. Files not starting with `TO_` as the filename will be rejected, as they could override game files, which is not allowed by this system. If your mod isn't shipping a precompiled BTP, `TO` files should strive to be between 5MiB and 25MiB, to improve compile performance for users. If your mod doesn't make up that size, that's fine, but for larger ones, consider chunking your files up. Every 10MiB of a `TO_` translates to about 350MiB of system memory for a client during the build process.

<img width="1054" height="805" alt="Image" src="https://github.com/user-attachments/assets/0777c02c-88c1-49cf-beb6-8297e4a1aedb" />


Inside your .m3to file(s), you specify your overrides, as well as the game the m3to is for. An m3to is just a JSON file with a custom extension.

<img width="1409" height="364" alt="Image" src="https://github.com/user-attachments/assets/990f886e-7e59-4d00-a23c-1437d9a3a4f4" />

For each texture, you must specify the source package name, relative to the CookedPCConsole folder of your DLC mod. You also must specify the `textureifp`, which is the instanced full path as shown in your package. 

> [!WARNING]
> The instanced full path and memory path must be identical in your file. All exports in a `TO_` should be Forced Export, as they are not the "original" package the object resides in at runtime. Textures must **NEVER** be at the root of your TO file!

> [!IMPORTANT]
> `TO_` packages are only used when mod installation completes. Mod Manager will automatically delete the installed `TO_` files after BTP compile unless it is in Developer Mode.

In your moddesc file, you must specify the ASI group id for Texture Override so that Mod Manager installs it with your mod, otherwise textures will not be overridden:

| Game | ASI Group ID |
|------|--------------|
| LE1  | TBD          |
| LE2  | TBD          |
| LE3  | TBD          |


## Deployment options
M3 Texture Overrides can be deployed in one of two ways (they cannot be combined):

1. A DLC folder with `.m3to` files that trigger a BTP compile on install
2. A DLC folder with a precompiled `CombinedTexturesOverride.btp` and matching `BTPMetadata.btm` files

### Option 1
The preferred option for most developers will be to simply ship the .m3to files and let Mod Manager compile the BTP file for you. This lets you ship source override assets directly and enables you to use the alternates system in Mod Manager to allow users to pick and choose options that each deploy different .m3to files.

### Option 2
A mod shipping a precompiled BTP file has only a single configuration which cannot be changed by the user. The benefit of this method is skipping the compile step, which for large texture mods may take significant time for users.

To use this option, you develop your mod in the style of Option 1, install your mod, then copy both the `CombinedTexturesOverride.btp` and `BTPMetadata.btm` file that reside directly under your DLC folder (not in CookedPCConsole!) into the version in your mod library. You can then move your `.m3to` and `TO_` files out of your mod. It is best to do this at the very end of development to ensure your BTP is in sync with your TFC files (if any).

<img width="751" height="275" alt="image" src="https://github.com/user-attachments/assets/3cbd81c0-50cc-4e52-aac3-7507e6bff8d9" />

> [!IMPORTANT]
> You **MUST** include the matching `BTPMetadata.btm` file with your deployment or Mod Manager will refuse to deploy or load your mod. This file contains the data to reconstitute package files from your BTP file. This is mandatory to keep the BTP file format in line with other custom formats that allow reversing changes and allow data recovery if the source packages are lost. To regenerate your package files, drag and drop the .btp file onto Mod Manager.


### Debugging at runtime
You can view the `TextureOverride.log` file next to the game executable to see the log for the last session that had the TextureOverride ASI run in it. 

#### In-game console commands
- `to.enable`
  - Enables texture overrides. Exports must be unloaded and reloaded for this to take effect, so you'll typically need to reload a map. Persistent objects in memory won't ever be affected by this, like textures from Startup or SFXGame.
- `to.disable`
  - Disables texture overrides. Exports must be unloaded and reloaded for this to take effect, so you'll typically need to reload a map. Persistent objects in memory won't ever be affected by this, like textures from Startup or SFXGame. 

#### Command line options
You can add there command line options in a custom launch configuration to help further debug issues.

- `-disabletextureoverride`
  - Disables overrides at boot, only loading the manifests, but not performing texture replacements
- `-to-trace`
  - Enables trace level logs which can help track down issues such as seeing what manifest items loaded, which BTP files were found, etc

