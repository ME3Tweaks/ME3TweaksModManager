# Alternates - conditional file installation both manual and automatic
Alternates are a feature of M3 that allow developers to provide alternative installation options for a mod. Examples of this would be to reconfigure what files are installed based on other installed mods, or providing the user with options on what to install (or not install), all while being in a supported configuration by the developer.

![Autoconfig Dialog](https://i.imgur.com/yGEOgaj.png)

**Note that these only take place at install time. So installation order for automatic configuration can matter! If a conditional DLC is installed after your mod, it may make the mod install differently than if it was installed before.**

Note that there are two alternate systems: Alternate Files (altfile), and AlternateDLC (altdlc). Alternate Files were developed first, which were expanded on for the CUSTOMDLC job.

When to use altfiles:
  - When you're creating alternates for any header but CUSTOMDLC (it works on all directory headers - BASEGAME, Official DLC, CUSTOMDLC)

When to use altdlc:
 - When you are creating alternates for the CUSTOMDLC header (this descriptor only works on CUSTOMDLC)


## Variables common to both altfiles and altdlc
The altfiles and altdlc structs share some common variables that work in both, and are described in the below table. For constructing a struct for your altfile/altdlc, please see their respective section further down this document.

| Variable Name         | Value          | Purpose & Notes                                                                                                                                                                                                                                                                                                                                     | Required                | Supported cmmver |
|-----------------------|----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------|------------------|
| FriendlyName          | String         | Sets the title text of the alternate when displayed in the installation dialog.                                                                                                                                                                                                                                                                     | Yes                     | 4.2+             |
| Description           | String         | This variable sets the description what the alternate's purpose is.                                                                                                                                                                                                                                                                                 | Yes                     | 4.2+             |
| CheckedByDefault      | Boolean String | This value only is applied if using `COND_MANUAL`. It is used to determine the default selection state of the alternate. If this value is missing, the default value is false.                                                                                                                                                                      | No                      | 6.0+             |
| OptionGroup           | String         | Sets the group this alternate struct is part of. All alternate structs that have the same OptionGroup value will be part of the same group. Groups can only have one option picked out of the group. Essentially, this makes the selector become a radio button. When using groups, one item in the group must have `CheckedByDefault` set to true. | No                      | 6.0+             |
| ApplicableAutoText    | String         | Sets the string that appears when the alternate is applicable. This item is not used if the condition is `COND_MANUAL`. If this value is not set, the default value is 'Auto Applied'. Do not localize this string into anything except English for your mod.                                                                                       | No                      | 6.0+             |
| NotApplicableAutoText | String         | Sets the string that appears when the alternate is not applicable to the game target the user is installing the mod against. If this value is not set, the default value is 'Not applicable'. Do not localize this string into anything except English for your mod.                                                                                | No                      | 6.0+             |
| ImageAssetName        | Integer        | Specifies the name of the image asset that will be shown for this alternate in the mod installation dialog. See [Mod images](modimages.md) for information on how to add images to your mod that will be shown at install time.                                                                                                                     | No                      | 6.2+             |
| ImageHeight           | Integer        | Specifies the height of the image that will be displayed by ImageAssetName. See [Mod images](modimages.md) for information on how to add images to your mod that will be shown at install time.                                                                                                                                                     | If using ImageAssetName | 6.2+             |


## altfiles specification
The altfiles descriptor allows you to substitute, add, or remove files from your BASEGAME, Official DLC, or CUSTOMDLC job based on the existence, or non existence, of one or more other DLC. If you are working on a Custom DLC, you will want to use the `altdlc` system instead, as it has many features that only work in the context of Custom DLC mods.

### altfiles struct variables
|Variable Name|Value|Purpose & Notes|Required|
|--- |--- |--- |--- |
|Condition|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`COND_DLC_PRESENT` - listed `ConditionalDLC` is installed<br>`COND_DLC_NOT_PRESENT` - listed `ConditionalDLC` is not installed<br>`COND_MANUAL` - user must manually choose this alternate file at install time<br>`COND_ALWAYS` - an always true condition. It is used if you want to convey that something will always be installed, like a 'core' package that is simply the base mod. It should be used with `ModOperation` `OP_NOTHING`.|Yes|
|ConditionalDLC|ModDesc Task Header or DLC Folder Name (String)|Conditional DLC to check for. This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The condition is checked against this DLC in the game's DLC folder and will cause this alt file to be applied or not applied when the mod is installed.|if any `Condition` except `COND_MANUAL` or `ModOperation` except `OP_NOTHING`|
|ModOperation|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`OP_SUBSTITUTE` - change a file that is being installed<br>`OP_INSTALL` - add a file to the list of files to be installed<br>`OP_NOINSTALL` - Prevent installation of a specific file<br>`OP_APPLY_MULTILISTFILES` - adds a list of files to install as defined by the `multilistid` variable. See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this attribute.<br> `OP_APPLY_MERGEMODS`  - Installs a list of .m3m merge mods as defined by the `MergeFiles` variable.<br>`OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'Version A or Version B'. Version A would be the default installation, while Version B would be an altfiles option - Version A would do nothing, but make the user feel like they have a choice between A and B.<br>These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager.|Yes|
|ModFile|Relative Installation Target Path (String)|This variable points to the relative target file that the Mod Operation will be performed on. Its use changes a bit depending on the condition:<br>`OP_SUBSTITUTE` or `OP_INSTALL` - relative file path from the mod's folder to the file to perform on (e.g. DLC_CON_XBX/CookedPCConsole/BioP_Char.pcc). The file path listed by this key will be added or replaced by `ModAltFile`.|if any `ModOperation` except `OP_NOTHING` ,`OP_APPLY_MULTILISTFILES`, or `OP_APPLY_MERGEMODS`|
|ModAltFile|Relative Alternate File Path (String)|Points to a replacement file or a new file that will be substituted or added to the game. This is a path to your new file, relative to the mod's folder in Mod Manager. In the above example, the alt file that will be used is in the GENESIS2 folder, named BioP_Char.pcc.|if using `ModOperation` `OP_INSTALL` or `OP_SUBSTITUTE`|
|MultiListId|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|MultiListRootPath|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|MultiListTargetPath|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|DLCRequirements|Semicolon Separated List (String)|Defines a list of DLC folders that must be installed in order for this option to be selectable by the user. This variable only does something when `Condition` is `COND_MANUAL`. You should ensure this value is not also selected with `CheckedByDefault`, as it may make an unselectable checkbox/radiobutton become checked without the ability for the user to uncheck it. You can use this variable to disable manual options that are not applicable to the user.|No|
|MergeFiles|Semicolon Separated List (String)|This variable defines the list of .m3m merge mod files from your mods MergeMods folder that will be installed when this option is selected.|if using `ModOperation` `OP_APPLY_MERGEMODS`|

### Altfile examples
Attributes have been put onto newlines for readabilty.

```
[BASEGAME]
...
altfiles=((Condition=COND_MANUAL,
ModOperation=OP_SUBSTITUTE,
ModFile=\BIOGame\CookedPCConsole\Startup.pcc,
AltFile=BASEGAME\Startup-NoBranding.pcc,
Description="Removes SP Controller Support text from splash/main menu. This text is present to indicate the mod is installed.",
FriendlyName="Remove Splash/Main Menu Branding", 
ImageAssetName=alt_nobranding.jpg, 
ImageHeight=221))
```
This altfile allows the user to select if they want branding on the splash screen in ME3's SP Controller Support mod. This is acheived by setting the condition to `COND_MANUAL`, and specifying the operation is `OP_SUBSTITUTE`. The file that will be installed instead is defined in `AltFile` and will be installed to `ModFile`. It also specifies an image asset to show the user named `alt_nobranding.jpg`, which is located in the M3Images folder. It is displayed at a height of 221px.

## altdlc specification
altdlc allows you to add a folder of files to a CustomDLC based on the installed game state. You can alternatively add an entire Custom DLC folder to the game using this specification, or using multilists you can cherry pick files from multiple folders to add to your DLC folder. The altdlc descriptor is mainly used for for automatically applying compatibility packs if your mod has known incompatibilities with another, as you can detect that and automatically reconfigure your mod to work around it. You can also have manual options to allow users to add their own developer-provided options, like lower resolution asset files.

The altdlc descriptor only works on the CUSTOMDLC job.

### altdlc struct variables
| Variable Name | Value | Purpose & Notes | Required | Supported cmmver |
|-|-|-|-|-|
| Condition | String | Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows: `COND_DLC_PRESENT` - listed ConditionalDLC is installed `COND_DLC_NOT_PRESENT` - listed ConditionalDLC is not installed `COND_ANY_DLC_NOT_PRESENT` - any of the items in the `ConditionalDLC` list are not present. The `ConditionalDLC` parameter is a list when this condition is used. `COND_ALL_DLC_PRESENT` - all items in the `ConditionalDLC` list are present. The `ConditionalDLC` parameter is a list when this condition is used. `COND_ANY_DLC_PRESENT` - any items in the ConditionalDLC list are present. The `ConditionalDLC` parameter is a list when this condition is used. `COND_ALL_DLC_NOT_PRESENT` - all items in the ConditionalDLC list are not present. The ConditionalDLC parameter is a list when this condition is used. `COND_SPECIFIC_SIZED_FILES` - Specific files must exist and their sizes must match the values. The files are stored by `RequiredFileRelativePaths` and the size values by `RequiredFileSizes` `COND_SPECIFIC_DLC_SETUP` - A specific set of DLC is installed or not installed. Using this condition changes how `ConditionalDLC` is parsed, see the `ConditionalDLC` section. `COND_MANUAL` - user must manually choose this alternate file from the installation dialog | Yes | 4.4+ |
| ConditionalDLC | Unquoted Semicolon Separated List (String) | Conditional DLC to check for. An example value would be  `ConditionalDLC=CITADEL;LEVIATHAN;OMEGA`. If using the conditions that only support a single DLC, you cannot use more than one item in the list.  This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The Condition combined with the ConditionalDLC is checked against the user's current DLC folder when being installed.  If using `Condition` `COND_SPECIFIC_DLC_SETUP`, each item must be prefixed by a + or -, with + indicating it is installed, and - indicating it is not installed. | <sup><sub>if any `Condition` except `COND_MANUAL`, `COND_ALWAYS` and `COND_SPECIFIC_SIZED_FILES` or `ModOperation` except `OP_NOTHING`</sub></sup> | 4.4+ |
| ModOperation | String | Specifies what operation to perform if the `Condition` for the `ConditionalDLC` is true. Valid values are as follows: `OP_ADD_CUSTOMDLC` - adds a CustomDLC folder to the mod when installing. `OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - adds the contents of a folder to the destination Custom DLC directory. `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC` - adds a list of files to install as defined by the `multilistid` variable. See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this attribute. `OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'choose your ending mod', even though your mod does not reconfigure for that ending mod. These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager. | Yes | 4.4+, extended in 6.1+ |
| ModAltDLC | Relative Path To Alternate Directory (String) | This variable points to the folder that the Mod Operation will use to add files from. For example, if you were detecting that a specific mod was installed and you had patch files in a subdirectory `Patches\CompatPatches`, you would use the value of `Patches\CompatPatches`. It should point directly to the root of the files if you are using `OP_ADD_FOLDERFILES_TO_CUSTOMDLC`. If you using `OP_ADD_CUSTOMDLC`, it should point to the root of the DLC such as `DLC_MOD_PATCHDLC`. | <sup><sub>if any `Condition` except `OP_NOTHING` or `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`</sub></sup> | 4.4+ |
| ModDestDLC | Relative Folder Installation Path (String) | Points to the destination you will be copying the `ModAltDLC` item to. The value you use changes depending on what your operation is: `OP_ADD_CUSTOMDLC` - The destination foldername in the DLC folder. Typically this will be the same as the DLC name itself, e.g. `DLC_MOD_PATCHDLC`. `OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - Relative path from the DLC directory to where the files will be installed to. For example, if I was adding files to part of this mod's `DLC_MOD_SuperCompatPatch` directory, I would specify `DLC_MOD_SuperCompatPatch/CookedPCConsole`. Files from within the ModAltDLC folder path will be copied into `DLC_MOD_SuperCompatPatch/CookedPCConsole` folder within the game directory's DLC folder at install time. | <sup><sub>if any `Condition` except `OP_NOTHING`</sub></sup> | 4.4+ |
| MultiListId | String | See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this variable. | <sup><sub>if using `ModOperation` `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`</sub></sup> | 6.1+ |
| <sub><sup>MultiListRootPath</sub></sup> | String | See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this variable. | <sup><sub>if using `ModOperation` `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`</sub></sup> | 6.1+ |
| <sub><sup>RequiredFileRelativePaths</sup></sub> | Unquoted Semicolon Separated List (String) | List of filepaths from the game root that must exist, and have a specific filesize at the same index in `RequiredFileSizes` | <sup><sub>only if using `Condition` `COND_SPECIFIC_SIZED_FILES`</sub></sup> | 6.1+ |
| RequiredFileSizes | Unquoted Semicolon Separated List (Integer) | List of filesizes in the same order and count as `RequiredFileRelativePaths`. If any files do not match their listed size, the condition will evaluate to false and the alternate will not be applicable. | <sup><sub>only if using `Condition` `COND_SPECIFIC_SIZED_FILES`</sub></sup> | 6.1+ |
| OptionKey | String | A unique identifier for this alternate that can be used to identify the alternate, even after installation. All option keys must be unique. If not set, an option key will be automatically generated based on the FriendlyName of the alternate. | No (but recommended) | 8.0+ |
| DependsOnKeys | PlusMinusKey list of OptionKeys | A list of other alternates' OptionKey's to depend on for state changing, separated by a semicolon. Values selected are specified with a prefixed '+', values not selected are prefixed with a '-'. The mod will fail to load if the alternate DependsOnKeys includes a reference to its own OptionKey. | No | 8.0+ |
| DependsOnMetAction | String | The action to take when the DependsOnKeys condition is evaluates to true. See (#altdlc-dependson-system)[the DependsOn values] for what values can be put here. | If `DependsOnKeys` is specified | 8.0+ |
| DependsOnActionNotMet | String | The action to take when the DependsOnKeys condition is evaluates to false. See (#altdlc-dependson-system)[the DependsOn values] for what values can be put here. | If `DependsOnKeys` is specified |  8.0+ |
| SortIndex | Integer | The sorting index for this alternate. The value must be unique across ALL alternates. This is used to sort alternates displayed to the user. Values don't need to be in incremental order; values can be skipped. | No |  8.0+ |
| Hidden | Boolean | Hides an option in the options selector. This is used in conjunction with the DependsOn system to allow you to depend on DLC being present or not present, by using the autos system that uses `ConditionalDLC` attributes. On options set to hidden, you typically use `OP_NOTHING` as it is only used to pivot. | No |  8.0+ |

### altdlc DependsOn system
In ModDesc 8.0, the `DependsOn` system was added. This allows options to depend on one or more other options, either checked or unchecked (or a mix). When the conditons are _all_ met, the met action is performed; when not all dependent options are not in a matching state, the not met action is run. 

The following values can be used to define the action:

| DependsOnAction                | Description                                                                                                                                                    |
|--------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ACTION_ALLOW_SELECT            | Unlocks the option for the user to choose their preference. This action deselects the selection on unlock, but the user can choose to select it if they wish.  |
| ACTION_ALLOW_SELECT_CHECKED    | Unlocks the option for the user to choose their preference. This options selects the alternate on unlock, but the user can choose to deselect it if they wish. |
| ACTION_DISALLOW_SELECT         | Locks the option so the user cannot choose a preference. This action forcibly deselects the alternate and the user cannot change it.                           |
| ACTION_DISALLOW_SELECT_CHECKED | Locks the option so the user cannot choose a preference. This action forcibly selects the alternate and the user cannot change it.                             |

### altdlc Examples
Attributes have been put onto newlines for readability. 

Example 1: Installing a folder of files only if a specific sized file is found. This would be used if you want to install only if a variant of a mod is installed.
```
(Condition = COND_SPECIFIC_SIZED_FILES, 
ModOperation = OP_ADD_FOLDERFILES_TO_CUSTOMDLC, 
ModAltDLC = Patches_Alternates/TALI_ME2, 
ModDestDLC = DLC_MOD_ALOV_Patches/Movies, 
Description = "Automatically installs files for the Tali Remastered mod in the ME2 like variant.", 
FriendlyName = "Tali Remastered ME2 Like", 
RequiredFileRelativePaths = \BIOGame\DLC\DLC_CON_TaliMaster\Movies\End03_Flashback_Tali.bik, 
RequiredFileSizes = 13834296)
```
This condition will automatically apply if the file `BIOGame\DLC\DLC_CON_TaliMaster\Movies\End03_Flashback_Tali.bik` is found and is of size `13834296`.

Example 2: Installing files from a folder if a specific official DLC is present. This example is from MEUITM2's Miranda Mesh fixes mod.
```
(Condition=COND_DLC_PRESENT, # We will trigger if a specific DLC is installed
ConditionalDLC=DLC_DHME1, # We will trigger if DLC_DHME1 is found installed
ModOperation=OP_ADD_FOLDERFILES_TO_CUSTOMDLC,  # We will add a folder of files to our Custom DLC
ModAltDLC=GENESIS, # Where the files we will add are located, relative to the root
ModDestDLC=DLC_MOD_MirandaMEUITM2MeshFixes/CookedPC #Where the files will go IN the game, relative to the DLC root directory
FriendlyName="Genesis DLC compatibility", #Name that is shown to the user
Description="Installs Genesis DLC versions of files if Genesis DLC is installed" # The text that is shown to the user
)
```

## MultiLists
MultiLists are a feature of moddesc 6.0 that enable a developer to provide alternate installation options that use the same file in at least 2 or more variations. This feature was developed initially for PEOM to enable ending compatibility across 4 different ending options, with different options using different files but some common to multiple options. This feature prevents having to pack additional copies of files into the archive.

MultiLists are defined as descriptors for official DLC headers as well as CUSTOMDLC as `multilistX`, where X is an integer counting up from one. The moddesc.ini parser will stop parsing as soon as the next integer is not found in the ini, so if you do `multilist1`, `multilist2`, and `multilist4`, M3 will only see `multilist1` and `multilist2`.

Due to the ini file format,  these lists can be a bit messy. I suggest defining your list of files line by line and then using a text editor to replace the newlines with semicolons. 

This example from PEOM applies to the CUSTOMDLC header:
```
[CUSTOMDLC]
sourcedirs = DLC_CON_PEOM
destdirs = DLC_CON_PEOM

;Vanilla ending list (all files)
multilist1 = CookedPCConsole\BioA_End002_Space.pcc;CookedPCConsole\BioD_End002.pcc;CookedPCConsole\BioD_End002_100Opening.pcc;CookedPCConsole\BioD_End002_100Opening_LOC_INT.pcc;CookedPCConsole\BioD_End002_200Tunnel.pcc;CookedPCConsole\BioD_End002_200Tunnel_LOC_INT.pcc;CookedPCConsole\BioD_End002_300TIMConflict.pcc;CookedPCConsole\BioD_End002_300TIMConflict_LOC_INT.pcc;CookedPCConsole\BioD_End002_400Guardian.pcc;CookedPCConsole\BioD_End002_400Guardian_LOC_INT.pcc;CookedPCConsole\BioD_End002_500Choice.pcc;CookedPCConsole\BioD_End002_710MemorialRed.pcc;CookedPCConsole\BioD_End002_710MemorialRed_LOC_INT.pcc;CookedPCConsole\BioD_End002_720MemorialBlue.pcc;CookedPCConsole\BioD_End002_720MemorialBlue_LOC_INT.pcc;CookedPCConsole\BioD_End002_730MemorialGreen.pcc;CookedPCConsole\BioD_End002_730MemorialGreen_LOC_INT.pcc;Movies\End04_Andromeda_Teaser.bik;Movies\Extended_Refusal.bik

;Vanilla Ending + Thanemod
multilist2 = CookedPCConsole\BioA_End002_Space.pcc;CookedPCConsole\BioD_End002.pcc;CookedPCConsole\BioD_End002_100Opening.pcc;CookedPCConsole\BioD_End002_100Opening_LOC_INT.pcc;CookedPCConsole\BioD_End002_200Tunnel.pcc;CookedPCConsole\BioD_End002_200Tunnel_LOC_INT.pcc;CookedPCConsole\BioD_End002_300TIMConflict.pcc;CookedPCConsole\BioD_End002_300TIMConflict_LOC_INT.pcc;CookedPCConsole\BioD_End002_400Guardian.pcc;CookedPCConsole\BioD_End002_400Guardian_LOC_INT.pcc;CookedPCConsole\BioD_End002_500Choice.pcc;Movies\End04_Andromeda_Teaser.bik;Movies\Extended_Refusal.bik

;MEHEM, JAM
multilist3 = CookedPCConsole\BioA_End002_Space.pcc;CookedPCConsole\BioD_End002.pcc;CookedPCConsole\BioD_End002_100Opening.pcc;CookedPCConsole\BioD_End002_100Opening_LOC_INT.pcc;CookedPCConsole\BioD_End002_200Tunnel.pcc;CookedPCConsole\BioD_End002_200Tunnel_LOC_INT.pcc

;LIME (Nothing)
```

MultiLists are used in the alternate structs `altdlc` and `altfiles`.

### AltDLC MultiLists
The following is an example from the PEOM moddesc.ini. The attributes have been put onto newlines for readability.
```
(Condition=COND_MANUAL, 
OptionGroup=EndingCompat, 
FriendlyName="Compatibility - Vanilla Ending + ThaneMod", 
ModOperation=OP_ADD_MULTILISTFILES_TO_CUSTOMDLC, 
DLCRequirements=DLC_CON_BackOff, 
MultiListId=2, 
MultiListRootPath="Compatibility/Endings", 
ModDestDLC=DLC_CON_PEOM,	
Description="Makes PEOM compatible with ThaneMod when the vanilla, original extended cut ending of the game is installed.")
```

The `ModOperation` specifies that you will be adding a list of files from a multilist to the a custom DLC folder. The `DLCRequirements` specifies what DLC folder(s) must exist in order for the option to be selectable by the user. This value is a semicolon separated list, just like `ConditionalDLC` is in normal altdlc structs. The `MultiListId` is used to select which list you are going to be using for filepaths. `MultiListRootPath` is a relative path from the root of the mod folder to where your alternate files are stored. This path plus the paths in the multilist determine the source path of each file that M3 will add to the mod at install time. `ModDestDLC` is used to determine what DLC folder the files will be placed into - the filepath for each item in the multilist is appended to this. You can specify a subfolder of your DLC by doing something like `DLC_CON_PEOM/CookedPCConsole`.

### AltFile MultiLists
`altfile` MultiLists work on Official Headers only. The `altfile` operation OP_APPLY_MULTILISTFILES will throw an error if it is used on the CUSTOMDLC header. You can use the `altdlc` version for a CUSTOMDLC version of the same functionality.

The following is an example from the ME2 - No Minigames moddesc.ini. The attributes have been put onto newlines for readability.
```
[BASEGAME]
moddir = Vanilla\BioGame\CookedPC
; This is the default, keyboard + mouse setup
newfiles = SFXGame.pcc;Startup_BRA.pcc;Startup_DEU.pcc;Startup_FRA.pcc;Startup_INT.pcc;Startup_ITA.pcc;Startup_POL.pcc
replacefiles = BIOGame\CookedPC\SFXGame.pcc;BIOGame\CookedPC\Startup_BRA.pcc;BIOGame\CookedPC\Startup_DEU.pcc;BIOGame\CookedPC\Startup_FRA.pcc;BIOGame\CookedPC\Startup_INT.pcc;BIOGame\CookedPC\Startup_ITA.pcc;BIOGame\CookedPC\Startup_POL.pcc

; Multilist1: Controller version
multilist1=SFXGame.pcc;Startup_BRA.pcc;Startup_DEU.pcc;Startup_FRA.pcc;Startup_INT.pcc;Startup_ITA.pcc;Startup_POL.pcc

; The first alternate file group does nothing to provide user the default choice.
; The second one uses the controller version by applying the files specified in multilist1 which fully supercedes the fileset
altfiles=(
(Condition=COND_MANUAL, 
FriendlyName="No Mini Games - Keyboard/Mouse", Description="Select this option if you're playing on keyboard and mouse.", ModOperation=OP_NOTHING, OptionGroup=InputMethod, CheckedByDefault = true),

(Condition=COND_MANUAL, 
FriendlyName="No Mini Games - Controller", Description="Select this option if you're playing with a controller. You must install the ME2 Controller mod BEFORE this one, or this mod will not work.",
ModOperation=OP_APPLY_MULTILISTFILES, 
MultiListRootPath=ME2Controller\BioGame\CookedPC, 
MultiListTargetPath=BIOGame\CookedPC, 
MultiListId=1, 
OptionGroup=InputMethod))
```

The `ModOperation` specifies that you wil lbe adding a list of files from a multilist to task. The `MultiListId` is used to select which list you are going to be using for filepaths. `MultiListRootPath` is a relative path from the root of the mod folder to where your alternate files are stored. This path plus the paths in the multilist determine the source path of each file that M3 will add to the mod at install time. `MultiListTargetPath` is the in-game path that will be installed to - the filepath for each item in the multilist is appended to this.
