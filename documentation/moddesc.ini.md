## The 4 main components of moddesc.ini
moddesc.ini files have 4 main components: 

 - The [ModManager] header, which contains the targeting information
 - The [ModInfo] header, which contains display information and information about the mod
 - The [UPDATES] header which contains deployment and updater service information
 - The mod task headers such as [CUSTOMDLC] or [RETALIATION]

![Moddesc](https://i.imgur.com/xCMVcLn.png)

### [ModManager] Header
The [ModManager] header is a required header for all moddesc.ini files. It supports a few descriptors that are used to change how the moddesc.ini parser works.

| Descriptor | Data type | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                        | Required | Supported versions |
|------------|-----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|--------------------|
| cmmver     | Float     | This descriptor is set to a specific version to tell Mod Manager how to parse the file, and what features may or may not be used by the parser. You may see this referred to as **moddesc version**. You assign this value to ensure forwards compatibility, in the event I have to change how moddesc parsing works - I will always strive to ensure a version targeting a previous version will remain usable in the future. | Yes      | 2.0+               |
| importedby | Integer   | As a mod developer you should never set this value. This is a compatibility shim for mods imported into M3 before Build 103 to indicate they should force target Mass Effect 3. After Build 103 was created, the 'game' descriptor was forced to always be present, and this flag indicates that it should use ME3 for those mods.                                                                                             | No       | 6.0 (Build 103+)   |
| minbuild   | Integer   | This descriptor is used to specify that a mod is only allowed to load on a specific build. For example, if a mod depends on features only present in Build 104, minbuild can be used to ensure users on Build 103 or lower cannot attempt to load the mod.                                                                                                                                                                     | No       | 6.0 (Build 104+)   |

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
| Descriptor  | Value                                            | Purpose & Notes                                                                                                                                                                                                                                                                                                                                                                                         | Required                                     | Supported Versions |
|-------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------|--------------------|
| modname     | String                                           | Mod name displayed in Mod Manager. This value should not be too long or the user will have to scroll the mod list to read the entire title of your mod.                                                                                                                                                                                                                                                 | Yes                                          | All                |
| game        | String                                           | Game this mod is for. Valid values are ME1, ME2 and ME3. If cmmver is less than 6 and this descriptor is not set, a default value of ME3 is assumed. You should simply always set this value.                                                                                                                                                                                                           | Yes if cmmver >= 6.0, Optional if cmmver < 6 | 6.0+               |
| moddesc     | String                                           | Mod description shown on Mod Manager. Newlines can be inserted by adding `<br>` where you want the newline.                                                                                                                                                                                                                                                                                             | Yes                                          | All                |
| modver      | Float                                            | Mod version, as shown in Mod Manager. This value is also used to detect updates. Required for mod deployment and use of the updater service.                                                                                                                                                                                                                                                            | Yes                                          | All                |
| moddev      | String                                           | Mod developer(s). Shown in the mod description panel.                                                                                                                                                                                                                                                                                                                                                   | Yes                                          | All                |
| modcoal     | Integer                                          | Any value other than zero indicates that there is a coalesced swap job for Mod Manager 2.0 mods. A file named Coalesced.bin must be in the same folder as moddesc.ini. This variable only works with moddesc targeting version 2.0. Moddesc 1.0 only does coalesced swap, and Moddesc 3.0 and above is done by adding a [BASEGAME] header with a replacement of /BIOGame/CookedPCConsole/Coalesced.bin. | No                                           | 2.0 only           |
| modsite     | String (URL)                                     | If present, a clickable link anchored at the bottom of the mod description panel will go to this URL. You should put the page that users can go to for support as this is the main reason they will go there. Using a proper nexusmods URL will also enable your mod to check for updates if your mod is whitelisted for update checks. See above for the definition of a proper NexusMods URL.         | No                                           | All                |
| modid       | Integer                                          | ModMaker Mod ID. Should not be manually added to any mods. Value is shown in the mod description panel and used for checking for updates.                                                                                                                                                                                                                                                               | No                                           | All                |
| nexuscode   | Integer                                          | Allows you to define your NexusMods mod ID specifically. If you are using a proper NexusMods URL as your modsite, this value is already set. This is for mods that do not use a NexusMods URL as their modsite (such as ME3Tweaks mods). If you are using a proper NexusMods URL or don't have a modsite value set, this value is ignored.                                                              | No                                           | 6.0+               |
| requireddlc | Unquoted Semicolon Separated List (DLC folder names) | Specifies a list of DLC folders that must exist in order for this mod to allow installation. For example, Spectre Expansion Mod requires Expanded Galaxy Mod, so it sets this value to `DLC_MOD_EGM`. If the mod also required MEHEM, it would be `DLC_MOD_EGM;DLC_CON_MEHEM`.                                                                                                                                                                                                    | No                                           | 5.0+               |


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





