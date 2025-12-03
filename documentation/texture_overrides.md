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

The new M3TO system can replace textures at runtime without the need for any in-game package edits. Using .m3to files shipped in your DLC folder, Mod Manager can compile the referenced texture overrides into single Binary Texture Package (BTP) that is loaded by the new `Texture Override` ASI.

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

**Cons:**
 - File sizes compared to .mem files is slightly larger due to having to use less efficient compression techniques (about 10%)
 - Textures are overriden by MemoryPath and not by CRC, so new same-crc textures won't be overridden. Developers should not be making new copies of texture exports without changes, so if this is an issue, developers need to properly design their mods

ME3Tweaks Mod Manager 9.2 includes a 'Convert MEM to TO' feature that can convert .mem files to a basic texture override mod that vanilla's the game, installs the .mem, then extracts the changes and builds an optimized installation for you. This process takes some time but only needs to be done by developers.


### M3TO setup

An M3TO mod deployment includes the following files:
 - At least one `TextureOverride-[WhateverNameYouWant].m3to` file
 - At least one `TO_` package that contains your texture overrides

Texture overrides must be stored in package files starting with `TO_`. Files not starting with `TO_` as the filename will be rejected, as they could override game files, which is not allowed by this system. If your mod isn't shipping a precompiled BTP, `TO` files should strive to be less than 30MiB, every 10MiB of a `TO_` translate to about 350MiB of system memory for a client during the build process. If your file is bigger than 30MiB, you should chunk it into a new file.

<img width="1054" height="805" alt="Image" src="https://github.com/user-attachments/assets/0777c02c-88c1-49cf-beb6-8297e4a1aedb" />


Inside your .m3to file, you specify your overrides, as well as the game the m3to is for. An m3to is just a JSON file with a custom extension.

<img width="1409" height="364" alt="Image" src="https://github.com/user-attachments/assets/990f886e-7e59-4d00-a23c-1437d9a3a4f4" />

For each texture, you must specify the source package name, relative to the CookedPCConsole folder of your DLC mod. You also must specify the `textureifp`, which is the instanced full path as shown in your package. 

**IMPORTANT:** The instanced full path and memory path must be identical in your file. All exports in a `TO_` should be Forced Export, as they are not the "original" package the object resides in at runtime.

