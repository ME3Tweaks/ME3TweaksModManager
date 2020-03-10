This documentation is current as of Build 104.

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
M3 is backwards compatible with Mass Effect 3 Mod Manager mods and it's older versions. This is done through a system called **moddesc version targeting**. At the top of every moddesc.ini file, there is a section header of **[ModManager]**, and underneath is a descriptor named _cmmver_. The value you assign to cmmver will change how M3 handles your mod. I strive to ensure all mods targeting old versions remain functional.

![cmmver](https://i.imgur.com/nmHxJIs.png)

#### What target do I choose?
Targetting different versions indicates which versions of M3 or Mass Effect 3 Mod Manager can use your mod and what features/mod operations you can describe in your moddesc file. For the most part it is best to use the latest version, as the automatic updater in M3 and Mass Effect 3 Manager brings most users to the latest version. If you are making simple mods, sometimes using an older moddesc format is easier. I describe the features of each version of moddesc.ini below.

As of November 10 2019, if you are making ME3 mods, I suggest not targeting higher than 5.1 as ME3Tweaks Mod Manager is still a work in progress. Once it reaches a stable build I will begin upgrading users to M3, after which you can start using M3 features in Moddesc 6.

#### Mod restrictions
M3 mods cannot include .exe, .dll, or .asi files. Mods will always fail validation if these files are found. Do not include them in your mod.

#### ME3Tweaks ModMaker mods
ME3Tweaks ModMaker mods are compiled against the the most recently supported version of moddesc that the compiling Mod Manager version supports, which is typically the base number (e.g. 4.4.1 compiles against moddesc 4.4). ModMaker mods are designed on ME3Tweaks ModMaker, so you won't need to worry about anything ModMaker related when building a moddesc.ini file.

### Using the ME3Tweaks Mod Updater Service
M3 has a mod updating mechanism for mods that allows all users to update their mods (that are registered and serviced on ME3Tweaks) without having to manually have the user download and import the mod. I have surveyed some end users and many say this is one of the best features of M3.

If you want to use this service, you can contact me on the ME3Tweaks Discord and we can discuss using the service for your mod. All mods that are on the updater service must be M3 mods (because it's the only way they can do so) and will be listed on the mods page. The main download does not need to be hosted on ME3Tweaks. This service is free as long as your mod is a reasonable size (and my web host doesn't complain).



# Mod Manager Advanced Moddesc Format
M3 supports many advanced features which are documented below. You don't need to use all of them, but putting a bit more work into your moddesc.ini might make for a better end user experience!



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



If you need assistance developing a moddesc.ini for your mod, please come to the ME3Tweaks Discord.
