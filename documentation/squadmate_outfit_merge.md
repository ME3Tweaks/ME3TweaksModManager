![Documentation Image](images/documentation_header.png)

## Adding outfits to squadmates in Mass Effect 3 (OT + LE)
Outfits for squadmates are controlled by a file named `BioP_Global` in Mass Effect 2 and Mass Effect 3. This file defines how to stream in squadmates. Combined with edits to your DLC ini/coalesced files, you can add squadmate outfits.

### The Problem
Since this is all controlled by a single file, multiple mods cannot add outfits and both work together, as it is full-file replacement.

Squadmate Outfit Merge is a feature in Mod Manager 7.0.1 for Game 3 that allows you to define a manifest file about your DLC mod's outfits, and upon installation, Mod Manager will build a combined BioP_Global file and install it, which includes all manifests. This allows all outfits to exist together.

**This feature does not currently work for Game 2 due to the hench selection UI being significantly more difficult to work with**.

### Setting up your mod

This feature is not part of moddesc.ini specifically; rather it is a feature automatically run on Mod Manager 7.0.1 and higher at install time. You don't need to add anything to your mod's coalesced file (no appearances, no dynamic load mappings), as Mod Manager will do all of this for you. To use this feature however, you must add a file to your DLC mod's CookedPCConsole folder, named `SquadmateMergeInfo.sqm`, as well as the outfit packages, and your TFC.

#### Package files required
There are several packages that must be included:
 - Your squadmate's outfit(s), including the \_Explore version, and the localization files for each. 
    - These need to be uniquely named as BioH_\[Squadmate Base Name]_\[DLC Folder Name]_\[Number].pcc. This ensures that a developer doesn't produce a same named package as another mod might.
    - The localizations don't need to be modified except for renaming them to match the base filename.
    - Memory instances in package should probably be renamed to match your DLC), such as `SFXGameContent_DLC_MOD_MyOutfit`.
 - A package file containing the images to use for the squad selection UI must be present. This file must be named SFXHenchImages_\[DLC Folder Name].pcc

#### TFC required
The TFC your packages reference must be included in the mod.

#### Squadmate merge manifest file
In your DLC mod, you must have a json file named `SquadmateMergeInfo.sqm`. This file contains the information about what to merge into `BioP_Global`.

An example file would look like this, which is explained below:
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

The list of outfits must contain the following information in each object:

**henchname**
The internal name of the henchman. This is present on all henchmen files, and is case sensitive, such as `Tali`, `Marine`, and others.

**henchpackage**
The base package name for the outfit. For Game 3, this will cover 2 packages:
 - \[henchpackage].pcc
 - \[henchpackage]_Explore.pcc

**highlightimage**
The full instanced path to the highlight image (hench selected) for display in the squadmate selection screen. This is located in `SFXHenchImages_\[DLC folder name].pcc`.

**availableimage**
The full instanced path to the available image (hench not selected) for display in the squadmate selection screen. This is located in `SFXHenchImages_\[DLC folder name].pcc`.

**silhouetteimage**
The full instanced path to the silhouette image (hench not available) for display in the squadmate selection screen. This is located in `SFXHenchImages_\[DLC folder name].pcc`.

**descriptiontext[0]**
The TLK string ID describing the bonus provided by the armor. Note that this doesn't actually provide the bonus, just the UI string.

**customtoken[0]**
A custom value to place into the description text string if it has a place for a token.

### When merge takes place
Upon installation of mods, squadmate merge takes place. When disabling or removing a DLC mod, squadmate merge also will re-run.
