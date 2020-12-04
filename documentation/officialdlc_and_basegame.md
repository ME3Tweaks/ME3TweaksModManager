![Documentation Image](images/documentation_header.png)

### Official DLC Task Headers
Mod Manager 2.0 (cmmver 2) and above added support for modding official game DLC. M3 added support for ME1 and ME2 modding, which has an additional set of headers that are supported. 

The following headers are supported, with their supported descriptors in the table below. BASEGAME is technically not a DLC (vanilla game) but keeps the same format. There are a few special task headers that will be explained further down in this document.

#### Supported Headers for Mass Effect 3
|Header name|DLC Folder|Supported Versions|
|--- |--- |--- |
|BASEGAME|BIOGame/CookedPCConsole|3.0+|
|RESURGENCE|BIOGame/DLC/DLC_CON_MP1|2.0+|
|REBELLION|BIOGame/DLC/DLC_CON_MP2|2.0+|
|EARTH|BIOGame/DLC/DLC_CON_MP3|2.0+|
|RETALIATION|BIOGame/DLC/DLC_CON_MP4|2.0+|
|RECKONING|BIOGame/DLC/DLC_CON_MP5|2.0+|
|PATCH1|BIOGame/DLC/DLC_UPD_Patch01|2.0+|
|PATCH2|BIOGame/DLC/DLC_UPD_Patch02|2.0+|
|FROM_ASHES|BIOGame/DLC/DLC_HEN_PR|2.0+|
|EXTENDED_CUT|BIOGame/DLC/DLC_CON_END|2.0+|
|LEVIATHAN|BIOGame/DLC/DLC_EXP_Pack001|2.0+|
|OMEGA|BIOGame/DLC/DLC_EXP_Pack002|2.0+|
|CITADEL|BIOGame/DLC/DLC_EXP_Pack003|2.0+|
|CITADEL_BASE|BIOGame/DLC/DLC_EXP_Pack003_Base|2.0+|
|APPEARANCE|BIOGame/DLC/DLC_CON_APP01|2.0+|
|FIREFIGHT|BIOGame/DLC/DLC_CON_GUN01|2.0+|
|GROUNDSIDE|BIOGame/DLC/DLC_CON_GUN02|2.0+|
|GENESIS2|BIOGame/DLC/DLC_CON_DH1|2.0+|
|COLLECTORS_EDITION|BIOGame/DLC/DLC_OnlinePassHidCE|2.0+|
|TESTPATCH|BIOGame/Patches/PCConsole|3.0+|
|BALANCE_CHANGES|See below notes|4.3+|

`BALANCE_CHANGES` is special in that it is an special header that is used to install a balance changes coalesced file, that is used when the Balance Changes Replacer ASI is installed. When using this header, upon mod installation, the Balance Changes Replacer ASI is automatically installed. This task only uses the `newfiles` descriptor, and only one item is allowed in the list. It must end with the .bin extension.


```
[BALANCE_CHANGES]
moddir = BALANCE_CHANGES
newfiles = ServerCoalesced.bin
```
**WARNING: DO NOT USE ANY COALESCED FILE THAT IS NOT DERIVED FROM THE ORIGINAL SERVER COALESCED FILE. You may completely ruin your online profile with no way to recover it without purchasing a new copy of Mass Effect 3.**

#### Supported Headers for Mass Effect 2
|Header name|DLC Folder|Supported Versions|
|--- |--- |--- |
|BASEGAME|BIOGame/CookedPC|6.0+|
|AEGIS_PACK|BIOGame/DLC/DLC_CER_02|6.0+|
|APPEARANCE_PACK_1|BIOGame/DLC/DLC_CON_Pack01|6.0+|
|APPEARANCE_PACK_2|BIOGame/DLC/DLC_CON_Pack02|6.0+|
|ARC_PROJECTOR|BIOGame/DLC/DLC_CER_Arc|6.0+|
|ARRIVAL|BIOGame/DLC/DLC_EXP_Part02|6.0+|
|BLOOD_DRAGON_ARMOR|BIOGame/DLC/DLC_PRE_DA|6.0+|
|CERBERUS_WEAPON_ARMOR|BIOGame/DLC/DLC_PRE_Cerberus|6.0+|
|COLLECTORS_WEAPON_ARMOR|BIOGame/DLC/DLC_PRE_Collectors|6.0+|
|EQUALIZER_PACK|BIOGame/DLC/DLC_MCR_03|6.0+|
|FIREPOWER_PACK|BIOGame/DLC/DLC_MCR_01|6.0+|
|GENESIS|BIOGame/DLC/DLC_DHME1|6.0+|
|INCISOR|BIOGame/DLC/DLC_PRE_Incisor|6.0+|
|INFERNO_ARMOR|BIOGame/DLC/DLC_PRE_General|6.0+|
|KASUMI|BIOGame/DLC/DLC_HEN_MT|6.0+|
|LAIR_OF_THE_SHADOW_BROKER|BIOGame/DLC/DLC_EXP_Part01|6.0+|
|NORMANDY_CRASH_SITE|BIOGame/DLC/DLC_UNC_Moment01|6.0+|
|OVERLORD|BIOGame/DLC/DLC_UNC_Pack01|6.0+|
|RECON_HOOD|BIOGame/DLC/DLC_PRO_Pepper02|6.0+|
|SENTRY_INTERFACE|BIOGame/DLC/DLC_PRO_Gulp01|6.0+|
|TERMINUS_WEAPON_ARMOR|BIOGame/DLC/DLC_PRE_Gamestop|6.0+|
|UMBRA_VISOR|BIOGame/DLC/DLC_PRO_Pepper01|6.0+|
|ZAEED|BIOGame/DLC/DLC_HEN_VT|6.0+|
|ME2_RCWMOD|See below notes|6.0+|

`ME2_RCWMOD` is a special header that is used for **R**oad**C**rew**W**orker's ME2 Mod Manager's .me2mod files. These files are delta files for the Coalesced.ini file. M3 can handle these files using this task. The only supported descriptor for this task is `modfile`, which is the filename of the .me2mod that resides next to the moddesc.ini file. RCW mods in Mod Manager must only contain a single 'mod' in their .me2mod file description. Multiple `###mod` directives in the .me2mod file is not allowed - import the .me2mod through Mod Manager to split them into multiple single mods.

#### Supported Headers for Mass Effect
|Header name|Game Folder|Supported Versions|
|--- |--- |--- |
|BASEGAME|BIOGame/CookedPC|6.0+|
|BRING_DOWN_THE_SKY|DLC/DLC_UNC|6.0+|
|PINNACLE_STATION|DLC/DLC_Vegas|6.0+|

### Official DLC Descriptors
Note that `newfiles` and `replacefiles` behavior differs if `gamedirectorystructure` is set to true. See info after this table for more information.

|Descriptor|Value|Purpose & Notes|Required|Supported Versions|
|--- |--- |--- |--- |--- |
|moddir|Unquoted String|Directory that houses the new files that will be installed into the game. This is relative to the mod folder. For example, a value of MP1 would mean that files in the MP1 folder belong to this mod task.|if adding or replacing files|2.0+|
|newfiles|Unquoted Semicolon Separated List (String)|List of filenames in the moddir that are files that will be installed into this DLC.|if using replacefiles|2.0+|
|replacefiles|Unquoted Semicolon Separated List (String)|File targets that will be replaced in this DLC. The paths should be relative to the base Mass Effect 3 folder. For example, you could put in /BIOGame/DLC/DLC_CON_MP1/CookedPCConsole/Asari_Commando_MP.pcc. The order of these files to replace MUST match the newfiles list or they will install to the wrong place.|if using newfiles|2.0+|
|gamedirectorystructure|Boolean string|Tells M3 to parse the newfiles and replacefiles values as directory mapping. `newfiles` becomes your source directory within `moddir`, which will map to the `replacefiles` directory within the game, from the root. See below for more info.|No|6.0+|
|addfiles|Unquoted Semicolon Separated List (String)|List of filenames in the moddir that that will be added to this DLC.|if using addfilestargets|4.1+, All games BASEGAME, Official DLC ME3 only|
|addfilestargets|Unquoted Semicolon Separated List (String)|File targets that will be added to this DLC. The paths should be relative to the base Mass Effect 3 folder. For example, you could put in /BIOGame/DLC/DLC_CON_MP4/CookedPCConsole/SFXPawn_ChubbyHusk.pcc. The order of these files to replace MUST match the addfiles list or they will install to the wrong place.|if using addfiles|4.1+, All games BASEGAME, Official DLC ME3 only|
|addfilesreadonlytargets|Unquoted Semicolon Separated List (String)|File targets that should be set to read only on installation. This only works on files you are adding, not replacing. The paths should be relative to the base Mass Effect 3 folder and match items from addfilestargets exactly. The order does not matter. Making files read only makes it more difficult for users to modify them as programs will say they can't modify it, however it does not stop users from modifying them. This is useful to protect files that are used by the exec command.|No|4.3+, All games BASEGAME, Official DLC ME3 only|
|jobdescription|Unquoted String|Description of the job, and why it is necessary. This text is shown if the DLC this job applies to is not installed. An example of this being used is with Interface Scaling Mod modifying the Retaliation MP DLC - the DLC may not be installed, but if the user does not play MP, it is not relevant. You should include a string saying is okay to skip this task if this DLC is not installed.|No|All|
|multilist\[x]|Unquoted semicolon split list (relative file paths)|Use to denote a list of relative file paths to optionally add into a mod, so you can share files across alternate installation options. The \[x] denotes a number starting from 1 and counting up (yes, this is indexed starting at 1). This descriptor is only used in conjunction with `altdlc` and `altfiles` descriptors. See the section on [MultiLists](#multilists) below for how to use this advanced feature.|No|6.0+|

#### Descriptors not supported by M3 that were supported in ME3CMM
 - **removefiletargets** - This descriptor could be dangerous if used incorrectly. There are no known mods that used this descriptor.
