## Alternates - conditional file installation both manual and automatic
Alternates are a feature of M3 that allow you to change what is installed based on current game state, and manually so user can choose a supported configuration of your mod. It is a complex but powerful system, and many users enjoy these features given feedback I have received.

**Note that these only take place at install time. So installation order for automatic configuration can matter! If a conditional DLC is installed after your mod, it may make mod install different than if it it was installed before.**

### altfiles specification
The altfiles descriptor allows you to substitute, add, or remove files from your BASEGAME, Official DLC, or CUSTOMDLC job based on the existence, or non existence, of one or more other DLC. It uses a list of parenthesis objects. Below is a substitution done in SP Controller Support for example, which is targeting ModDesc 5.0.
```
[CUSTOMDLC]
sourcedirs = DLC_CON_XBX
destdirs = DLC_CON_XBX
altfiles=((Condition=COND_DLC_PRESENT, ConditionalDLC=GENESIS2, ModOperation=OP_SUBSTITUTE, ModFile=DLC_CON_XBX/CookedPCConsole/BioP_Char.pcc, AltFile=GENESIS2/BioP_Char.pcc, Description="Enables Genesis 2 DLC to work in character creation"))
```

The altfiles descriptor value is a struct list (see above). The table below shows what variables are accepted in this struct and how they work. All applicable operations are automatically applied once the user presses install unless they use condition of `COND_MANUAL`. The presense of any condition will show a dialog to the user before installation so they know this mod autoconfigures.

![Autoconfig Dialog](https://i.imgur.com/yGEOgaj.png)

#### altfiles struct variables
|Variable Name|Value|Purpose & Notes|Required|
|--- |--- |--- |--- |
|Condition|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`COND_DLC_PRESENT` - listed `ConditionalDLC` is installed<br>`COND_DLC_NOT_PRESENT` - listed `ConditionalDLC` is not installed<br>`COND_MANUAL` - user must manually choose this alternate file at install time<br>`COND_ALWAYS` - an always true condition. It is used if you want to convey that something will always be installed, like a 'core' package that is simply the base mod. It should be used with `ModOperation` `OP_NOTHING`.|Yes|
|ConditionalDLC|ModDesc Task Header or DLC Folder Name (String)|Conditional DLC to check for. This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The condition is checked against this DLC in the game's DLC folder and will cause this alt file to be applied or not applied when the mod is installed.|if any `Condition` except `COND_MANUAL` or `ModOperation` except `OP_NOTHING`|
|ModOperation|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`OP_SUBSTITUTE` - change a file that is being installed<br>`OP_INSTALL` - add a file to the list of files to be installed<br>`OP_NOINSTALL` - Prevent installation of a specific file<br>`OP_APPLY_MULTILISTFILES` - adds a list of files to install as defined by the `multilistid` variable. See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this attribute.<br>`OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'Version A or Version B'. Version A would be the default installation, while Version B would be an altfiles option - Version A would do nothing, but make the user feel like they have a choice between A and B.<br>These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager.|Yes|
|ModFile|Relative Installation Target Path (String)|This variable points to the relative target file that the Mod Operation will be performed on. Its use changes a bit depending on the condition:<br>`OP_SUBSTITUTE` or `OP_INSTALL` - relative file path from the mod's folder to the file to perform on (e.g. DLC_CON_XBX/CookedPCConsole/BioP_Char.pcc). The file path listed by this key will be added or replaced by `ModAltFile`.|if any `ModOperation` except `OP_NOTHING` or `OP_APPLY_MULTILISTFILES`|
|ModAltFile|Relative Alternate File Path (String)|Points to a replacement file or a new file that will be substituted or added to the game. This is a path to your new file, relative to the mod's folder in Mod Manager. In the above example, the alt file that will be used is in the GENESIS2 folder, named BioP_Char.pcc.|if using `ModOperation` `OP_INSTALL` or `OP_SUBSTITUTE`|
|FriendlyName|String|Sets the title text of the alternate when displayed in the installation dialog.|Yes|
|Description|String|This variable sets the description what the alternate's purpose is.|Yes|
|CheckedByDefault|Boolean String|This value only is applied if using `COND_MANUAL`. It is used to determine the default selection state of the alternate. If this value is missing, the default value is false.|No|
|OptionGroup|String|Sets the group this alternate file struct is part of. All alternate file structs that have the same OptionGroup value will be part of a group. Option groups can only have one option picked out of the group. Essentailly, this makes the selector become a radio button. Ensure at least one item in the group has `CheckedByDefault` set.|No|
|ApplicableAutoText|String|Sets the string that appears when the alternate is applicable. This item is not used if the condition is `COND_MANUAL`. If this value is not set, the default value is 'Auto Applied'. Do not localize this string into anything except English for your mod.|No|
|NotApplicableAutoText|String|Sets the string that appears when the alternate is not applicable to the game target the user is installing the mod against. If this value is not set, the default value is 'Not applicable'. Do not localize this string into anything except English for your mod.|No|
|MultiListId|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|MultiListRootPath|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|MultiListTargetPath|String|See the [AltFile MultiList](#AltFile-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_APPLY_MULTILISTFILES`|
|DLCRequirements|Semicolon Separated List (String)|Defines a list of DLC folders that must be installed in order for this option to be selectable by the user. This variable only does something when `Condition` is `COND_MANUAL`. You should ensure this value is not also selected with `CheckedByDefault`, as it may make an unselectable checkbox/radiobutton become checked without the ability for the user to uncheck it. You can use this variable to disable manual options that are not applicable to the user.|No|

### altdlc specification
altdlc allows you to add a folder of files to a CustomDLC based on the installed game state. You can alternatively add an entire Custom DLC folder to the game using this specification. This is useful for automatically applying compatibility packs if your mod has known incompatibilities with another, as you can detect that and automatically reconfigure your mod to work around it. You can also have manual options to allow users to add their own developer-provided options, like lower resolution asset files.

#### altdlc struct variables
|Variable Name|Value|Purpose & Notes|Required|
|--- |--- |--- |--- |
|Condition|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`COND_DLC_PRESENT` - listed ConditionalDLC is installed<br>`COND_DLC_NOT_PRESENT` - listed ConditionalDLC is not installed<br>`COND_ANY_DLC_NOT_PRESENT` - any of the items in the `ConditionalDLC` list are not present. The `ConditionalDLC` parameter is a list when this condition is used.<br>`COND_ALL_DLC_PRESENT` - all items in the `ConditionalDLC` list are present. The `ConditionalDLC` parameter is a list when this condition is used.<br>`COND_ANY_DLC_PRESENT` - any items in the ConditionalDLC list are present. The `ConditionalDLC` parameter is a list when this condition is used.<br>`COND_ALL_DLC_NOT_PRESENT` - all items in the ConditionalDLC list are not present. The ConditionalDLC parameter is a list when this condition is used.<br>`COND_SPECIFIC_SIZED_FILES` - Specific files must exist and their sizes must match the values. The files are stored by `RequiredFileRelativePaths` and the size values by `RequiredFileSizes`<br>`COND_SPECIFIC_DLC_SETUP` - A specific set of DLC is installed or not installed. Using this condition changes how `ConditionalDLC` is parsed, see the `ConditionalDLC` section.<br>`COND_MANUAL` - user must manually choose this alternate file from the installation dialog|Yes|
|ConditionalDLC|Unquoted Semicolon Separated List (String)|Conditional DLC to check for. An example value would be  `ConditionalDLC=CITADEL;LEVIATHAN;OMEGA`. If using the conditions that only support a single DLC, you cannot use more than one item in the list.<br><br>This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The Condition combined with the ConditionalDLC is checked against the user's current DLC folder when being installed.<br><br>If using `Condition` `COND_SPECIFIC_DLC_SETUP`, each item must be prefixed by a + or -, with + indicating it is installed, and - indicating it is not installed.|if any `Condition` except `COND_MANUAL`, `COND_ALWAYS` and `COND_SPECIFIC_SIZED_FILES` or `ModOperation` except `OP_NOTHING`|
|ModOperation|String|Specifies what operation to perform if the `Condition` for the `ConditionalDLC` is true. Valid values are as follows:<br>`OP_ADD_CUSTOMDLC` - adds a CustomDLC folder to the mod when installing.<br>`OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - adds the contents of a folder to the destination Custom DLC directory.<br>`OP_ADD_MULTILISTFILES_TO_CUSTOMDLC` - adds a list of files to install as defined by the `multilistid` variable. See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this attribute.<br>`OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'choose your ending mod', even though your mod does not reconfigure for that ending mod.<br>These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager.|Yes|
|ModAltDLC|Relative Path To Alternate Directory (String)|This variable points to the folder that the Mod Operation will use to add files from. For example, if you were detecting that a specific mod was installed and you had patch files in a subdirectory `Patches\CompatPatches`, you would use the value of `Patches\CompatPatchesc`. It should point directly to the root of the files if you are using `OP_ADD_FOLDERFILES_TO_CUSTOMDLC`. If you using `OP_ADD_CUSTOMDLC`, it should point to the root of the DLC such as `DLC_MOD_PATCHDLC`.|if any `Condition` except `OP_NOTHING` or `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`|
|ModDestDLC|Relative Folder Installation Path (String)|Points to the destination you will be copying the `ModAltDLC` item to. The value you use changes depending on what your operation is:<br>`OP_ADD_CUSTOMDLC` - The destination foldername in the DLC folder. Typically this will be the same as the DLC name itself, e.g. `DLC_MOD_PATCHDLC`.<br>`OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - Relative path from the DLC directory to where the files will be installed to. For example, if I was adding files to part of this mod's `DLC_MOD_SuperCompatPatch` directory, I would specify `DLC_MOD_SuperCompatPatch/CookedPCConsole`. Files from within the ModAltDLC folder path will be copied into `DLC_MOD_SuperCompatPatch/CookedPCConsole` folder within the game directory's DLC folder at install time.|if any `Condition` except `OP_NOTHING`|
|FriendlyName|String|Sets the title text of the alternate when displayed in the installation dialog.|Yes|
|Description|String|This variable sets the description what the alternate's purpose is.|Yes|
|CheckedByDefault|Boolean String|This value only is applied if using `Condition` `COND_MANUAL`. It is used to determine the default selection state of the alternate. If this value is missing, the default value is false.|No|
|OptionGroup|String|Sets the group this alternate dlc is part of. All alternate dlc that have the same OptionGroup value will be part of a group. Option groups can only have one option picked out of the group. Essentailly, this makes the selector become a radio button. Ensure at least one item in the group has `CheckedByDefault` set.|No|
|ApplicableAutoText|String|Sets the string that appears when the alternate is applicable. This item is not used if the condition is `COND_MANUAL`. If this value is not set, the default value is 'Auto Applied'. Do not localize this string into anything except English for your mod.|No|
|NotApplicableAutoText|String|Sets the string that appears when the alternate is not applicable to the game target the user is installing the mod against. If this value is not set, the default value is 'Not applicable'. Do not localize this string into anything except English for your mod.|No|
|MultiListId|String|See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`|
|MultiListRootPath|String|See the [AltDLC MultiList](#AltDLC-MultiLists) documentation on how to use this variable.|if using `ModOperation` `OP_ADD_MULTILISTFILES_TO_CUSTOMDLC`|
|RequiredFileRelativePaths|Unquoted Semicolon Separated List (String)|List of filepaths from the game root that must exist, and have a specific filesize at the same index in `RequiredFileSizes`|only if using `Condition` `COND_SPECIFIC_SIZED_FILES`|
|RequiredFileRelativePaths|Unquoted Semicolon Separated List (Integer)|List of filesizes in the same order and count as `RequiredFileRelativePaths`. If any files do not match their listed size, the condition will evaluate to false and the alternate will not be applicable.|only if using `Condition` `COND_SPECIFIC_SIZED_FILES`|

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
