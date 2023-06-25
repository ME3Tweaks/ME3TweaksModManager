![Documentation Image](images/documentation_header.png)

## Adding outfits to squadmates in Mass Effect 2 (LE) and Mass Effect 3 (OT + LE)
Outfits for squadmates are controlled by a file named `BioP_Global` in Mass Effect 2 and Mass Effect 3. Mass Effect 2 uses an additional file called `BioP_EndGm_StuntHench` to stream in special versions of squadmates during the final mission that don't join your party but are loaded into the level. These file define how to stream in squadmates with the user chosen outfits.

Note: [GAME 3 ONLY] Due to casual outfits not being able to changed in-game with a UI, this feature does not cover those. Your mod will still need to ship a \_NC file override if you wish to also change that character's casual (such as EDI or Tali).

### The Problem
Since outfits are controlled via one or two files, multiple mods cannot add outfits and both work together, due to mods being full file replacement. These files also are not eligible for merge modding. With some changes to your DLC mod's config files, you could add outfits to squadmates in LE3. In LE2 it is much more difficult due to many hardcoded things related to the party selection UI.

Squadmate Outfit Merge is a feature in Mod Manager that allows you to define a manifest file about your DLC mod's outfits, and upon installation, Mod Manager will build the necessary packages to make them all work together. It will also inject a more flexible team select UI into LE2, which allows more squadmate outfits.

**This feature does not support the original Mass Effect 2 due to the amount of SWF editing involved, as well as the lack of users modding that game. Mass Effect 1 does not have squadmate outfits due to its armor system.**

### Setting up your mod

Demo mods can be downloaded and inspected for their respective games:
 - [LE3](https://www.nexusmods.com/masseffectlegendaryedition/mods/400) - ME3 would be same format, but with ME3 package files instead
 - [LE2](https://www.nexusmods.com/masseffectlegendaryedition/mods/1829)

Mod Manager MINIMUM requirements:
 - LE3: ME3Tweakd Mod Manager 7.0.1
 - LE2: ME3Tweaks Mod Manager 8.2 (Build 129)
    - LE2 currently only supports 9 unique outfits per squadmate. This is how many outfit slots were added to the TeamSelect SWF by naNuke/Mith.

This feature is not part of moddesc.ini specifically; rather it is a feature automatically run by Mod Manager at install time. You don't need to add anything to your mod's coalesced file (no appearances, no dynamic load mappings), as Mod Manager will do all of this for you. To use this feature however, you must add a file to your DLC mod's CookedPCConsole folder, named `SquadmateMergeInfo.sqm`, as well as the outfit packages, and your TFC.

#### Package files required
There are several packages that must be included:
**Game 3**
 - Your squadmate's outfit(s), including the \_Explore version, and the localization files for each. 
    - These need to be uniquely named as BioH_\[Squadmate Base Name]\_[DLC Folder Name]\_[Number].pcc. This ensures that a developer doesn't produce a same named package as another mod might. Due to these requirements, you may want to choose a short DLC mod folder name, as it will significantly eat into the 260 character limit for filepaths Windows has by default.
    - The localizations don't need to be modified except for renaming them to match the base filename.
    - Memory instances in package should probably be renamed to match your DLC, such as `SFXGameContent_DLC_MOD_MyOutfit`.
 - A package file containing the images to use for the squad selection UI must be present. This file must be named SFXHenchImages_\[DLC Folder Name].pcc

**LE2**
 - Your squadmate's outfit(s), including the \_END version, and the localization files for each. Do not simply rename the standard `BioH_` to `BioH_END_`, you WILL break the final mission of the game. 
    - These need to be uniquely named as `BioH_\[Squadmate Base Name]\_[DLC Folder Name]\_[Number].pcc`. This ensures that a developer doesn't produce a same named package as another mod might. The END version must be named `BioH_\[Squadmate Base Name]\_[DLC Folder Name]\_[Number].pcc`. Due to these requirements, you may want to choose a short DLC mod folder name, as it will significant eat into the 260 character limit for filepaths Windows has by default.
    - The localizations don't need to be modified except for renaming them to match the base filename.
    - Memory instances in package should probably be renamed to match your DLC, such as `SFXGameContent_DLC_MOD_MyOutfit`
 - A package file containing the images to use for the squad selection UI must be present. This file must be named `SFXHenchImages_\[DLC Folder Name].pcc`. Textures from this package will be copied to the locations where the squad selection screen are present, the file itself will not be used by the game

You can generate a DLC mod with starter kit that has all of this set up for you, ready to edit. You can also use the Mod Utils menu to add starter kit content to add the first outfit for each squadmate to your DLC if you have an existing mod.

#### TFC required
The TFC your packages reference must be included in the mod.

#### Squadmate merge manifest file
In your DLC mod, you must have a json file named `SquadmateMergeInfo.sqm`. This file contains the information about what to merge into `BioP_Global`.

An example **LE3** file would look like this, which is explained below:
```
{
	"game": "LE3",
	"outfits":
	[
		{
			"henchname": "Marine",
			"henchpackage": "BioH_Marine_DLC_MOD_SquadmateCheeseburger_00",
			"highlightimage": "GUI_Henchmen_Images_DLC_MOD_SquadmateCheeseburger.James0Glow",
			"availableimage": "GUI_Henchmen_Images_DLC_MOD_SquadmateCheeseburger.James0",
			"silhouetteimage": "GUI_Henchmen_Images.James0_locked",
			"descriptiontext[0]": 34014280,
			"customtoken[0]": 25
		}
	]
}
```

An example **LE2** file would look like this, which is explained below:
```
{
	"game": "LE2",
	"outfits":
	[
		{
			"henchname": "Vixen",
			"henchpackage": "BioH_Vixen_DLC_MOD_MirandaCheeseburgerArmor_00",
			"highlightimage": "MirandaGlow",
			"availableimage": "Miranda",
			"plotflag": -1
		}
	]
}
```

The list of outfits must contain the following information in each object:

**henchname**

The internal name of the henchman. This is present on all henchmen files, and is case sensitive, such as `Tali`, `Marine`, and others.

**henchpackage**

The base package name for the outfit. For Game 3, this will cover 2 packages:
 - \[henchpackage].pcc
 - \[henchpackage]_Explore.pcc

**highlightimage**

The full instanced path to the highlight image (hench selected) for display in the squadmate selection screen. This is located in `SFXHenchImages_[DLC folder name].pcc`.

**availableimage**

The full instanced path to the available image (hench not selected) for display in the squadmate selection screen. This is located in `SFXHenchImages_[DLC folder name].pcc`.

**silhouetteimage**
_Not used in LE2_
The full instanced path to the silhouette image (hench not available) for display in the squadmate selection screen. This is located in `SFXHenchImages_[DLC folder name].pcc`.

**plotflag**
_Not used in Game 3_
The plot bool that unlocks this outfit. This is how loyalty outfits are controlled in the vanilla game. Set to -1 to always have it available.

**descriptiontext[0]**
_Not used in LE2_
The TLK string ID describing the bonus provided by the armor. Note that this doesn't actually provide the bonus, just the UI string.

**customtoken[0]**
_Not used in LE2_
A custom value to place into the description text string if it has a place for a token.

### When merge takes place
Upon installation of mods, squadmate merge takes place. When disabling or removing a DLC mod, squadmate merge also will re-run.
