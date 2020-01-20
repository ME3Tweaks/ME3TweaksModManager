THIS DOCUMENT IS A WORK IN PROGRESS FOR MOD MANAGER 6

Current as of Build 103.

**This document is long! Do not let that dissuade you. It is just very thorough.**

ME3Tweaks Mod Manager (which will be written as M3 from now on in this document) allows users to organize mods as well as deploy them into the each game of the Mass Effect Trilogy. It is designed to be friendly for users and powerful for developers - for example, as a developer, you do not need to worry about users not installing the binkw32 bypass or running AutoTOC - M3 takes care of this all in the background for users. You can focus on developing your mods and my program will take care of the user side of things.

The documentation in this article is specific to M3 and should not be applied to Mass Effect 3 Mod Manager, as it does not support moddesc version 6 or higher, though some of the information will be relevant to both.

# Why use M3? Why not just use a zip or an exe installer?
I've dealt with end-users for years and worked with many to help fix issues with problems arising to incorrect installation of mods. It is very easy to think like a developer and say well you just do X Y and Z, in that order. Many end users simply don't read or _do not understand_ what developers have written because a lot of documentation is written by people who assume that end users are familiar with the modding scene.

Some examples of questions that are easy for developers to answer but not for end users:
 - Do I need to install binkw32 for every mod that includes it?
 - When do I autotoc? 
 - Can I install mods after texture mods?
 - How do I restore my game if I don't like a mod?
 - How do I know what's installed?
 - How do I backup my game?
 - Do I need to unpack my DLC?

Archive distribution in the Mass Effect Trilogy scene is the standard (for non-texture mods), as long as you have decent instructions. This is the absolute bare minimum that I would say is acceptable for the end user. I have seen countless reports of broken games due to users installing mods wrong because the mods have complicated and very difficult to follow directions.


**EXE installers I do not think are acceptable for use** for the following reasons:
 - They require administrative rights to use
 - You have almost no idea what they're doing. If I want to learn how a mod works, an installer is almost fully opaque
 - Smart screen on windows will say this file is not common and make it harder for end users to run
 - It completely bypasses any sort of logical checks, like if ALOT is installed. If you install after ALOT for example, it'll completely ruin the mod you just installed
 - Uninstall entries are left in the registry that make it think it's still installed when it's actually not and will confuse end users
 - Automatically overwriting bink bypass with your own version can disable some mods because there are different variants of bink bypass that have different features

M3 mods are mods that are designed to be able to be manually installed by users - if they choose to do so, but optimally are installed through M3. M3 facilitates checks and provides features to the end user such as library management, update checks, relevant mod information, modding rule enforcement and automatic configuration of mods based on the current game state. Deployment of a mod through M3 will make the file size smaller through optimized 7z compression settings designed for the types of files we work on in the scene, as well as perform sanity checks on your mod (such as for broken textures and audio).

While you don't have to use it for your mod, all of the active developers in the scene encourage it's use because end users often don't know what they're doing.

# moddesc.ini - the definition of an M3 mod
M3 mods are defined as folders in a mod user's mod library. In the library there are three folders: ME1, ME2, and ME3. Mods for each respective game go in each of these folders. Inside of these game folders are individual mod folders. In each mod folder, there is a **moddesc.ini file**, along with the modded files and folders that make up the mod. The moddesc.ini file describes the mod and what features of M3 the mod will use for both displaying the mod and its options to the user, as well as how the mod is installed. A moddesc.ini is what makes a mod an M3 compatible mod.

![Mod folder structure](https://i.imgur.com/vAZUjPa.png)

#### Backwards compatibility
M3 is backwards compatible with Mass Effect 3 Mod Manager mods and it's older versions. This is done through a system call **moddesc version targeting**. At the top of every moddesc.ini file, there is a section header of **[ModManager]**, and underneath is a descriptor named _cmmver_. The value you assign to cmmver will change how M3 handles your mod. I strive to ensure all mods targeting old versions remain functional.

![cmmver](https://i.imgur.com/nmHxJIs.png)

#### What target do I choose?
Targetting different versions indicates which versions of M3 or Mass Effect 3 Mod Manager can use your mod and what features/mod operations you can describe in your moddesc file. For the most part it is best to use the latest version, as the automatic updater in M3 and Mass Effect 3 Manager brings most users to the latest version. If you are making simple mods, sometimes using an older moddesc format is easier. I describe the features of each version of moddesc.ini below.

As of November 10 2019, if you are making ME3 mods, I suggest not targeting higher than 5.1 as ME3Tweaks Mod Manager is still a work in progress. Once it reaches a stable build I will begin upgrading users to M3, after which you can start using M3 features in Moddesc 6.

#### Mod restrictions
M3 mods cannot include .exe, .dll, or .asi files. Mods will always fail validation if these files are found. Do not include them in your mod.

#### ME3Tweaks ModMaker mods
ME3Tweaks ModMaker mods are compiled against the the most recently supported version of moddesc that the compiling Mod Manager version supports, which is typically the base number (e.g. 4.4.1 compiles against moddesc 4.4). ModMaker mods are designed on ME3Tweaks Modmaker, so you won't need to worry about anything ModMaker related when building a moddesc.ini file.

### Using the ME3Tweaks Mod Updater Service
M3 has a mod updating mechanism for mods that allows all users to update their mods (that are registered and serviced on ME3Tweaks) without having to manually have the user download and import the mod. I have surveyed some end users and many say this is one of the best features of M3.

If you want to use this service, you can contact me on the ME3Tweaks Discord and we can discuss using the service for your mod. All mods that are on the updater service must be M3 mods (because it's the only way they can do so) and will be listed on the mods page. The main download does not need to be hosted on ME3Tweaks. This service is free as long as your mod is a reasonable size (and my web host doesn't complain).

## Creating a mod for ME3Tweaks Mod Manager
If you are planning to create mods with ME3Tweaks Mod Manager, you should ensure your local installation of M3 is in developer mode. Turn this feature on in the Actions > Options menu. This will enable developer oriented features.

To create a mod for M3, you will need to know what kind you are making. Most developers will be creating a singleplayer-only CustomDLC (DLC mod that overrides or adds some game files), which can be very easily created with Starter Kit. You can create a fully working, blank DLC mod by going to Tools > Developer tools > Custom DLC Starter Kit. 

If you wish to make one manually, you can do so using this quick and easy template if you want to deploy a CustomDLC mod (mod that adds only a DLC to the game). Edit the variable names as necessary and save as moddesc.ini next to your mod's DLC folder.

```
[ModManager]
cmmver = 6

[ModInfo]
modname = Your Mod Name Here
moddev = Your Name Here
modver = 1.0.0.0
moddesc = Set your description that will show in Mod Manager here. The description must be done on a single line as ini files are parsed per linebreak. You can use the <br> tag to force a newline in the description in Mod Manager.
modsite = http://example.com

[CUSTOMDLC]
sourcedirs = DLC_YOUR_NAME_HERE
destdirs = DLC_YOUR_NAME_HERE
```
**modver** is be the version number of your mod.

#### If targeting cmmver 6 or higher
The value should be a version number, such as _1.0.1_, _1.2.4.11_ or _1.2_, **but not 1**). A single value without a digit is not a version number. 

#### If targeting cmmver 5.1 or lower
The value should be a floating point number, such as 1.2 or 1.33. ME3CMM does not support Version numbers, only floating point numbers.

The **modsite** value will be a clickable link in the description panel of the mod, so users can go directly to your mod's page (e.g. nexusmods page). If you don't know the NexusMods URL, you can find the base url of it once you start creating your mod page.

### Deploying your mod
You should deploy your mod through M3 by going to the Mod Utils menu and doing Developer options > Deploy Mod. Deploying a mod prepares it for distribution through a highly optimized 7z file that features high compression (so users spend less time downloading, you spend less time uploading) while having very fast parsing support when the file is dropped onto M3.

Manually 7z'ing your mod folder will still allow it to work in M3 but the larger your mod is, the longer it will take to display the import options to the user. This is because M3 appends the moddesc.ini files to the 7z archive without compression in a second pass. This ensures that M3 does not have to decompress anything to read the moddesc for your mod, and results in instantaneous display of the embedded mods to the end user.

# Mod Manager Advanced Moddesc Format
M3 supports many advanced features which are documented below. You don't need to use all of them, but putting a bit more work into your moddesc.ini might make for a better end user experience!

## Data types in moddesc.ini
There are several data types you will need to know about for moddesc.ini files.

### Headers and Descriptors
Mod Manager mods are defined by their moddesc.ini files which are located in the mod folder. The folder, mod files and this moddesc.ini file make a Mod Manager mod. The moddesc.ini format has 2 key terms: **Headers** and **Descriptors**. 

#### Headers
Headers are items encased in square brackets like [ModManager] or [CUSTOMDLC]. They do not contain spaces. Headers are case sensitive.
```
[ModInfo]
...

[CUSTOMDLC]
...
```

#### Descriptors
Underneath headers are descriptors, which describe items for most recent header above it. Descriptors are key/value pairs. Descriptor keys are case sensitive.
```
key=value
sourcedirs=DLC_MOD_UIScaling
``` 

Descriptor spacing does not matter. The values before the `=` and after it are trimmed of outer whitespace. The following all parse to equivalent items:
```
descriptor = value
descriptor=value
descriptor   =    value
```


### Structs
Some descriptors use structs, which are modeled after how BioWare's Coalesced.ini and decompiled Coalesced.bin files are. A struct is a list of keys mapped to values. The keys are always unquoted and will never contain spaces. The values may be quoted or unquoted. M3 supports both - ME3CMM has a relatively weak implementation of a struct parser and may or may not work in some instances.

```
structexample=(Key1=Value1,Key2="Value 2")
```

Any value that contains a space MUST be quoted. All key/value pairs must be separated by a comma. Text inside of "quotes" will not trigger the special characters , ) or (. You cannot include the " symbol in your strings as this is a reserved character for parsing. If you want to simulator "quoting" something, use 'single quotes'.

### Struct lists
Some descriptors use a list of structs. Lists are formed by an opening and closing parenthesis, with each struct separated by a comma. **This is an additional set of parenthesis! Structs have their own enclosing parenthesis.** However, a one item struct list does not have to be surrounded by an additional set of parenthesis. You can choose to leave them on or off for one item lists.

Some examples:

```
twoitemlist=((X=1, Y=1),(X=2,X=3))
oneitemlist=((text="hello there", speaker=obiwan))
anotheroneitemlist=(text="GENERAL KENOBI!", speaker=GeneralGreivous)
```

### String lists
Some descriptors take a list of strings. These are values separated by a `;`. 
```
outdatedcustomdlc=DLC_MOD_OldMod1;DLC_MOD_OldMod2;DLC_MOD_OldMod3
```

Note that these are separators, not end delimiters. You should only have them between elements.

### Comments
Comments are lines that being with `;`. They are essentially ignored.

### moddesc.ini parser strictness
In M3, the parser for a moddesc.ini file is more strict than it was for ME3CMM. A few examples of things that will cause a mod to fail parsing include:
 - A line without a `=` on it that is not a `;comment`, that is not a header, or is not blank. 
 - Non-matching `(` and `)` in a list descriptor.  A `)` must always be opened by a `(` unless in a quoted string. An unclosed `(` or an unexpected `)` that has no matching opening `(` will cause the parser to mark the mod as invalid.
 - Duplicate descriptor keys. The M3 moddesc.ini parser is set to not allow duplicate descriptor keys and will throw an error if you try to use one.

ME3CMM sometimes allowed these mods to still load as the code to parse various items was not as robust as it is in M3. If you want your mod to work in both ME3CMM and M3, test to ensure it loads in both and not just one or the other.

## Version Targeting
ME3Tweaks Mod Manager is fully backwards compatible with mods targeting older versions of moddesc, but upgrading a mod's moddesc version without upgrading the contents of the file may not work properly as different moddesc versions are parsed differently. 

_There is one notable exception to this backwards compatibility - files that do not fully conform to the moddesc.ini spec but worked due to a bug in the parser may break when being used with ME3Tweaks Mod Manager. **Item lists that are surrounded by ( and ) must have the correct amount of parenthesis or M3 will mark the mod as invalid!**_

M3 will refuse to load mods higher than it's listed supported moddesc version. This is not really an issue you should worry about as I typically restrict developers from releasing mods with new moddesc features until the userbase has full access to a supported version of M3 through it's built-in updater.

## The 4 main components of moddesc.ini
moddesc.ini files have 4 main components: 

 - The [ModManager] header, which contains the targeting information
 - The [ModInfo] header, which contains display information and information about the mod
 - The [UPDATES] header which contains deployment and updater service information
 - The mod task headers such as [CUSTOMDLC] or [RETALIATION]

![Moddesc](https://i.imgur.com/xCMVcLn.png)

### [ModManager] Header
The [ModManager] header is a required header for all moddesc.ini files. It supports a single descriptor, **cmmver**, which is set to a specific version to tell Mod Manager how to parse the file, and what features may or may not be used by the parser. You may see this referred to as **moddesc version**. You assign this value to ensure forwards compatibility, in the event I have to change how moddesc parsing works - I will always strive to ensure a version targeting a previous version will remain usable in the future. 

#### ME3 Mods
If you are building a mod for ME3, the lowest version I suggest using is 5.0, as this is the last 32-bit mod manager version. Otherwise use whatever the latest version is, as deployment support typically is built around the latest version.

#### ME1, ME2 Mods
If you are building a mod for ME1 or ME2 you must target a minimum of moddesc version 6, which is the first version that M3 supported. ME3CMM does not support ME1 or ME2, and will reject mods with a cmmver of 6.

Valid values are listed below with the main highlights of that release:

#### Moddesc features by version
| cmmver Version | Games supported | Release date | Release Highlights                                                                                                                                                   |
|----------------|-----------------|--------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1.0/1.1        | ME3             | 2012         | Basic Coalesced.bin swapping only                                                                                                                                    |
| 2.0            | ME3             | 2013         | Supports modifying SOME of the official game DLC (see headers table below)                                                                                           |
| 3.0            | ME3             | 2014         | Supports modifying the basegame and TESTPATCH DLC                                                                                                                    |
| 3.1/4.0        | ME3             | 2015         | Supports installation of Custom DLC mods                                                                                                                             |
| 4.1            | ME3             | 2015         | Supports adding and removing files from the game                                                                                                                     |
| 4.2            | ME3             | 2016         | Supports the altfiles descriptor for CustomDLC, supports sideloading, blacklisting, and manifest building tags                                                       |
| 4.3            | ME3             | 2016         | Supports marking added files as read only, supports modding balance changes                                                                                          |
| 4.4            | ME3             | 2016         | Supports the altdlc and outdatedcustomdlc descriptors for CustomDLC                                                                                                  |
| 4.5            | ME3             | 2017         | Supports the altfiles descriptor in OFFICIAL headers. I do not recommend targeting this version as OP_SUBSTITUTE behaves differently due to backwards compatibility. |
| 5.0            | ME3             | 2017         | Last 32-bit version. Supports the requireddlc descriptor to require specific DLC(s) to be installed before mod will install                                          |
| 5.1            | ME3             | 2018         | Last ME3CMM version. Supports the additionaldeploymentfolders descriptor to allow inclusion of additional folders that are not specifically installed                |
| 6.0            | ME1/ME2/ME3     | 2019         | First M3 version. In development. Supports customization of alternate auto/not-applicable text, multi-dlc metacmm names, game directory structured automapping of source folders and more. Plans to have multi-alternates (multiple alt operations in a single alternate) and basegame-only file delta application (for things like sfxgame) are scheduled but not implemented yet.                                                                     |

If a cmmver descriptor is not set, the default 1.0 value will be used, which has almost no features.

## [ModInfo] Header
The [ModInfo] Header is used for the description, version, and other information about the mod that the user will see in Mod Manager. It also houses the ME3Tweaks Updater Service information. 

### [ModInfo] Descriptors
|Descriptor|Value|Purpose & Notes|Required|Supported Versions|
|--- |--- |--- |--- |--- |
|modname|String|Mod name displayed in Mod Manager. This value should not be too long or the user will have to scroll the mod list to read the entire title of your mod.|Yes|All|
|game|String|Game this mod is for. Valid values are ME1, ME2 and ME3. If cmmver is less than 6 and this descriptor is not set, a default value of ME3 is assumed. You should simply always set this value. |Yes if cmmver >= 6.0, Optional if cmmver < 6|6.0+|
|moddesc|String|Mod description shown on Mod Manager. Newlines can be inserted by adding `<br>` where you want the newline.|Yes|All|
|modver|Floating Point Number|Mod version, as shown in Mod Manager. This value is also used to detect updates. Required for mod deployment and use of the updater service.|Yes|All|
|moddev|String|Mod developer(s). Shown in the mod description panel.|Yes|All|
|modcoal|Integer|Any value other than zero indicates that there is a coalesced swap job for Mod Manager 2.0 mods. A file named Coalesced.bin must be in the same folder as moddesc.ini. This variable only works with moddesc targeting version 2.0. Moddesc 1.0 only does coalesced swap, and Moddesc 3.0 and above is done by adding a [BASEGAME] header with a replacement of /BIOGame/CookedPCConsole/Coalesced.bin.|No|2.0 only|
|modsite|String (URL)|If present, a clickable link anchored at the bottom of the mod description panel will go to this URL. You should put the page that users can go to for support as this is the main reason they will go there. Using a proper nexusmods URL will also enable your mod to check for updates if your mod is whitelisted for update checks.|No|All|
|modid|Integer|ModMaker Mod ID. Should not be manually added to any mods. Value is shown in the mod description panel and used for checking for updates.|No|All|


### [UPDATES] Header
The UPDATES header is used when deploying your mod as well as information about how it is stored on the ME3Tweaks Updater service, if you choose to use it.

#### [UPDATES] Supported Descriptors
|Descriptor|Value|Purpose & Notes|Required|Supported Versions|
|--- |--- |--- |--- |--- |
|serverfolder|Unquoted String|Path to your server storage area. Typically this is 3rdparty/<username>/modname. This is only used when generating the manifest.|Required for Updater Service, Not used for Deployment|All|
|blacklistedfiles|Unquoted Semicolon Separated List (String)|Relative file paths here from the mod's root directory will be deleted upon update. This is used to delete old files that may have fallen out of the scope of the mod folders. For example, I used to ship a .cmd file with SP Controller Support, which I blacklisted to ensure it was deleted on update so it would no longer be used.|Optional for Updater Service, Not used for Deployment|4.2+|
|additionaldeploymentfolders|Unquoted Semicolon Separated List (String)|Folders specified here will be included in mod staging for deployments, and transitively for mods in the updater service. Note you can only specify top level folders with this descriptor, not files or subdirectories.|Optional for Updater Service, Optional for Deployment|5.1+|
|additionaldeploymentfiles|Unquoted Semicolon Separated List (String)|Root-level files that should be included in your server update or included in your archives when deploying your mod. Only root level files are supported.|Optional for Updater Service, Optional for Deployment|6.0+|
|updatecode|Integer|ME3Tweaks Updater Service update code. This is used to get the manifest from ME3Tweaks for classic mods. If you don't have an update code assigned from ME3Tweaks, don't use this descriptor.|No|6.0+|
|nexuscode|Integer|NexusMods mod ID. This is part of your NexusMods URL. Whitelisted mods from developers that have agreed to always have up to date downloads and semantic versioning will query ME3Tweaks mod database to see if the local mod has an update. The information on ME3Tweaks is keyed by your NexusMods ID. NexusMod codes that are not whitelisted will not check for updates.|No|6.0+|

Official DLC Task Headers
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

BALANCE_CHANGES is special in that it is an additional job that supports all of normal features, but additional code is run when this task is processed to ensure the balance changes replacer ASI is installed. You should only ever use this header to target balance changes, such as follows:

[BALANCE_CHANGES]
moddir = BALANCE_CHANGES
newfiles = ServerCoalesced.bin
replacefiles = \Binaries\win32\asi\ServerCoalesced.bin
The above headers support the following descriptors:

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

#### Descriptors not supported by M3 that were supported in ME3CMM
 - **removefiletargets** - This descriptor could be dangerous if used incorrectly. There are no known mods that used this descriptor.

## [CUSTOMDLC] Header
The [CUSTOMDLC] header is likely the most important header for mod developers. It allows Mod Manager to add a Custom DLC folder to the game, such as DLC_MOD_EGM. This header has some advanced optional descriptors that allow you to add compatibility shims and packs automatically when the mod loads in Mod Manager. This header is supported starting with moddesc 3.1.

The following descriptors are supported by this header. They are explained in more detail below the table.

### [CUSTOMDLC] Descriptors
|Descriptor|Value|Purpose & Notes|Required|Supported Versions|
|--- |--- |--- |--- |--- |
|sourcedirs|Unquoted Semicolon Separated List (String)|List of directory names in this mod folder that will be installed into the game's DLC folder.|Yes|3.1+|
|destdirs|Unquoted Semicolon Separated List (String)|List of directory names that will be created in the game's DLC folder. The corresponding folder in the sourcedirs list will be placed here. Typically the destdirs and sourcedirs list are identical, but they can be different if you prefer a different naming scheme.|Yes|3.1+|
|altfiles|Unquoted Comma Separated List (AltFile)|List of AltFile structs that define an alternative set of files to install (or not install). You can make these alternate files automatically part of the mod (such as substituting a file if a DLC exists) or have them be manually chosen by the user, like game files that use lower resolution. See the AltFile struct information below.|No|4.2+|
|altdlc|Unquoted Comma Separated List (AltDLC)|List of AltDLC structs that define an alternative set of Custom DLC to install (or not install). You can make these alternate dlcs automatically part of the mod (such as adding a DLC if another DLC is not present) or have them be manually chosen by the user, like adding more squadmates to a mod. You can also add files to an existing Custom DLC that you are going to install if you want to have a modular style CustomDLC that is customized for every user. See the AltDLC struct information below.|No|4.4+|
|outdatedcustomdlc|Unquoted Semicolon Separated List (String)|List of foldernames that should not be present in the game's DLC folder after installation. You can use this descriptor to have Mod Manager delete old versions of the mod (if you renamed it), remove outdated compatibility packs, and remove known incompatible mods. The user is prompted to delete the folders if any exist.|No|4.4+|
|requireddlc|Unquoted Semicolon Separated List (String)|List of DLC foldernames that are required to be installed before this mod can be installed. For example, if your mod depends on Extended Cut, you would put DLC_CON_END. You can also require multiple DLC to be installed.|No|5.0+|
|incompatiblecustomdlc|Unquoted Semicolon Separated List (String)|List of DLC foldernames that are known to be fully incompatible with your mod - e.g. installing both mods would break the game for the user. If any item in this DLC list is found, the mod will refuse to install. Putting the name of any official DLC will cause your mod to fail validation.|No|6.0+|

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
|Condition|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`COND_DLC_PRESENT` - listed `ConditionalDLC` is installed<br>`COND_DLC_NOT_PRESENT` - listed `ConditionalDLC` is not installed<br>`COND_MANUAL` - user must manually choose this alternate file at install time|Yes|
|ConditionalDLC|ModDesc Task Header or DLC Folder Name (String)|Conditional DLC to check for. This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The condition is checked against this DLC in the game's DLC folder and will cause this alt file to be applied or not applied when the mod is installed.|if any condition except `COND_MANUAL` or operation except `OP_NOTHING`|
|ModOperation|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:<br>`OP_SUBSTITUTE` - change a file that is being installed<br>`OP_INSTALL` - add a file to the list of files to be installed<br>`OP_NOINSTALL` - Prevent installation of a specific file<br>`OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'Version A or Version B'. Version A would be the default installation, while Version B would be an altfiles option - Version A would do nothing, but make the user feel like they have a choice between A and B.<br>These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager.|Yes|
|ModFile|Relative Installation Target Path (String)|This variable points to the relative target file that the Mod Operation will be performed on. Its use changes a bit depending on the condition:<br>`OP_SUBSTITUTE` or `OP_INSTALL` - relative file path from the mod's folder to the file to perform on (e.g. DLC_CON_XBX/CookedPCConsole/BioP_Char.pcc). The file path listed by this key will be added or replaced by `ModAltFile`.|if any `Condition` except `OP_NOTHING`|
|ModAltFile|Relative Alternate File Path (String)|Points to a replacement file or a new file that will be substituted or added to the game. This is a path to your new file, relative to the mod's folder in Mod Manager. In the above example, the alt file that will be used is in the GENESIS2 folder, named BioP_Char.pcc.|if using Operation `OP_INSTALL` or `OP_SUBSTITUTE`|
|FriendlyName|String|Sets the title text of the alternate when displayed in the installation dialog.|Yes|
|Description|String|This variable sets the description what the alternate's purpose is.|Yes|
|CheckedByDefault|Boolean String|This value only is applied if using `COND_MANUAL`. It is used to determine the default selection state of the alternate. If this value is missing, the default value is false.|No|
|OptionGroup|String|Sets the group this alternate file struct is part of. All alternate file structs that have the same OptionGroup value will be part of a group. Option groups can only have one option picked out of the group. Essentailly, this makes the selector become a radio button. Ensure at least one item in the group has `CheckedByDefault` set.|No|
|ApplicableAutoText|String|Sets the string that appears when the alternate is applicable. This item is not used if the condition is `COND_MANUAL`. If this value is not set, the default value is 'Auto Applied'. Do not localize this string into anything except English for your mod.|No|
|NotApplicableAutoText|String|Sets the string that appears when the alternate is not applicable to the game target the user is installing the mod against. If this value is not set, the default value is 'Not applicable'. Do not localize this string into anything except English for your mod.|No|

### altdlc specification
altdlc allows you to add a folder of files to a CustomDLC based on the installed game state. You can alternatively add an entire Custom DLC folder to the game using this specification. This is useful for automatically applying compatibility packs if your mod has known incompatibilities with another, as you can detect that and automatically reconfigure your mod to work around it. You can also have manual options to allow users to add their own developer-provided options, like lower resolution asset files.

#### altdlc struct variables
|Variable Name|Value|Purpose & Notes|Required|
|--- |--- |--- |--- |
|Condition|String|Specifies what operation to perform if the condition for the conditional DLC is true. Valid values are as follows:COND_DLC_PRESENT - listed ConditionalDLC is installedCOND_DLC_NOT_PRESENT - listed ConditionalDLC is not installedCOND_ANY_DLC_NOT_PRESENT - any of the items in the ConditionalDLC list are not present. The ConditionalDLC parameter is a list when this condition is used.COND_ALL_DLC_PRESENT - all items in the ConditionalDLC list are present. The ConditionalDLC parameter is a list when this condition is used.COND_ANY_DLC_PRESENT - any items in the ConditionalDLC list are present. The ConditionalDLC parameter is a list when this condition is used.COND_ALL_DLC_NOT_PRESENT - all items in the ConditionalDLC list are not present. The ConditionalDLC parameter is a list when this condition is used.COND_MANUAL - user must manually choose this alternate file from the mod utils menu|Yes|
|ConditionalDLC|List or Singular ModDesc Header or DLC Folder Name (String)|Conditional DLC to check for. The format of this parameter changes depending on which Condition is used.Singular: ConditionalDLC=CITADELList: ConditionalDLC=(CITADEL;LEVIATHAN;OMEGA)This can be a ModDesc header (as listed above) or a DLC folder name like DLC_CON_BackOff. If using a header it will be resolved to the DLC folder name. The Condition combined with the ConditionalDLC is checked against the user's current DLC folder when being installed.|if any condition except `COND_MANUAL` or operation except `OP_NOTHING`|
|ModOperation|String|Specifies what operation to perform if the `Condition` for the `ConditionalDLC` is true. Valid values are as follows:<br>`OP_ADD_CUSTOMDLC` - adds a CustomDLC folder to the mod when installing.<br>`OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - adds the contents of a folder to the destination Custom DLC directory.<br>`OP_NOTHING` - makes this alternate do nothing. This is used for things like having a default option group, or making the user feel like the option is applicable to them, such as saying 'choose your ending mod', even though your mod does not reconfigure for that ending mod.<br>These operations are applied at install time and do not modify anything in the mod's folder in Mod Manager.|Yes|
|ModAltDLC|Relative Path To Alternate Directory (String)|This variable points to the folder that the Mod Operation will use to add files from. For example, if you were detecting that a specific mod was installed and you had patch files in a subdirectory `Patches\CompatPatches`, you would use the value of `Patches\CompatPatchesc`. It should point directly to the root of the files if you are using `OP_ADD_FOLDERFILES_TO_CUSTOMDLC`. If you using `OP_ADD_CUSTOMDLC`, it should point to the root of the DLC such as `DLC_MOD_PATCHDLC`.|if any `Condition` except `OP_NOTHING`|
|ModDestDLC|Relative Folder Installation Path (String)|Points to the destination you will be copying the `ModAltDLC` item to. The value you use changes depending on what your operation is:<br>`OP_ADD_CUSTOMDLC` - The destination foldername in the DLC folder. Typically this will be the same as the DLC name itself, e.g. `DLC_MOD_PATCHDLC`.<br>`OP_ADD_FOLDERFILES_TO_CUSTOMDLC` - Relative path from the DLC directory to where the files will be installed to. For example, if I was adding files to part of this mod's `DLC_MOD_SuperCompatPatch` directory, I would specify `DLC_MOD_SuperCompatPatch/CookedPCConsole`. Files from within the ModAltDLC folder path will be copied into `DLC_MOD_SuperCompatPatch/CookedPCConsole` folder within the game directory's DLC folder at install time.|if any `Condition` except `OP_NOTHING`|
|FriendlyName|String|Sets the title text of the alternate when displayed in the installation dialog.|Yes|
|Description|String|This variable sets the description what the alternate's purpose is.|Yes|
|CheckedByDefault|Boolean String|This value only is applied if using `COND_MANUAL`. It is used to determine the default selection state of the alternate. If this value is missing, the default value is false.|No|
|OptionGroup|String|Sets the group this alternate dlc is part of. All alternate dlc that have the same OptionGroup value will be part of a group. Option groups can only have one option picked out of the group. Essentailly, this makes the selector become a radio button. Ensure at least one item in the group has `CheckedByDefault` set.|No|
|ApplicableAutoText|String|Sets the string that appears when the alternate is applicable. This item is not used if the condition is `COND_MANUAL`. If this value is not set, the default value is 'Auto Applied'. Do not localize this string into anything except English for your mod.|No|
|NotApplicableAutoText|String|Sets the string that appears when the alternate is not applicable to the game target the user is installing the mod against. If this value is not set, the default value is 'Not applicable'. Do not localize this string into anything except English for your mod.|No|
