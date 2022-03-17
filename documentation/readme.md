![Documentation Image](images/documentation_header.png)

This documentation is current as of Build 123.

ME3Tweaks Mod Manager (which will be written as M3 from now on in this document) allows users to organize mods as well as install them into the each game of the Mass Effect Trilogy. It is designed to be friendly for users and powerful for developers - for example, as a developer, you do not need to worry about users not installing the binkw32 bypass or running AutoTOC - M3 takes care of this all in the background for users. You can focus on developing your mods and my program will take care of the user side of things.

**The documentation in this article is specific to M3** and should not be applied to Mass Effect 3 Mod Manager, that program is no longer supported.

## Features for end users
 - [Power user features](powerusers.md)
   - Details power user features that allow you to perform certain tasks faster and easier 

## moddesc.ini documentation for mod developers
- [moddesc.ini: Data types](datatypes.md)
   - Describes headers, descriptors, and other data types that are used in moddesc.ini files
- [moddesc.ini - the definition of an M3 mod](moddesc.ini.md)
   - Includes information on moddesc.ini versioning, how a mod is defined, mod restrictions, as well as descriptors for \[ModManager], \[ModInfo], and \[UPDATES]
- [moddesc.ini: Official DLC and Basegame Headers](officialdlc_and_basegame.md)
   - Contains all the options available for use on mods targeting replacing files in the BASEGAME and Official DLC headers
- [moddesc.ini: CUSTOMDLC Header (DLC mods)](customdlc.md)
   - Contains all the options available for Custom DLC mods, which are the most common type of mod in the trilogy
- [moddesc.ini: Automatic and alternative mod installations using alternates](alternates.md)
   - Contains all the options available for use in alternative installations, such as automatic patch installs
- [moddesc.ini: Localization mods](localizations.md)
   - Contains information about how to make a localization TLK mod for ME2 and ME3 mods
- [Merge Mods: Creating an .m3m file](merge_mods.md)
   - Contains information on how to create a merge mod, a format which can update individual exports in common basegame files with few compatibility issues.
- [Overriding Conditionals with .pmu Files](pmu_files.md)
   - Contains information on using the Plot Manager Update system to override basegame conditionals in ME1 and ME2, OT and LE.
- [Replacing strings in ME1/LE1 using TLK merge](game1_tlk_merge.md)
   - Contains information about how to use the GAME1_EMBEDDED_TLK header to replace strings in ME1/LE1, due to it having embedded TLK files
- [Adding outfits to squadmates in Game 3 using Squadmate Outfit Merge](squadmate_outfit_merge.md)
   - Contains information about the squadmate merge feature, and how to use it so you can add outfits without replacing existing ones for Mass Effect 3 (OT + LE)
- [Adding emails in Game 2 using Game 2 Email Merge](game2_email_merge.md)
   - Contains information on using the Game 2 email merge feature


Mod Manager includes a built-in moddesc editor that can be used on existing mods. Right click your mod while in developer mode and select `moddesc.ini editor`. Currently this only works on mods that are loaded, and cannot be used for creating the initial mod.

## Why use M3? Why not just use a zip or an exe installer?
I've dealt with end-users for years and worked with many to help fix issues with problems arising to incorrect installation of mods. It is very easy to think like a developer and say well you just do X Y and Z, in that order. Many end users simply don't read or _do not understand_ what developers have written because a lot of documentation is written by people who assume that end users are familiar with the modding scene.

Some examples of questions that are easy for developers to answer but not for end users:
 - Do I need to install binkw32 for every mod that includes it?
 - When do I autotoc? 
 - Can I install mods after texture mods?
 - How do I restore my game if I don't like a mod?
 - How do I know what's installed?
 - How do I backup my game?
 - Do I need to unpack my DLC?
 - What is the install order of mods?

Archive distribution in the Mass Effect Trilogy scene is the standard (for non-texture mods), as long as you have decent instructions.  This is the absolute bare minimum that I would say is acceptable for the end user. I have seen countless reports of broken games due to users installing mods wrong because the mods have complicated and very difficult to follow directions, such as using ME3Explorer to AutoTOC, which is a tool not designed for end users.


**I do not think EXE installers are acceptable for use** for the following reasons:
 - They require administrative rights to use
 - You (an end user) have almost no idea what they're doing. If I want to learn how a mod works, an installer is almost fully opaque
 - Smart screen on windows will say this file is not common and make it harder for end users to run
 - It completely bypasses any sort of logical checks, like if ALOT is installed. If you install after ALOT for example, it'll completely ruin the mod you just installed
 - Uninstall entries are left in the registry that make it think it's still installed when it's actually not and will confuse end users
 - Automatically overwriting bink bypass with your own version can disable some mods because there are different variants of bink bypass that have different features
 - Most installers I have found (like MEHEM and PEOM prior to 1.5.2) pollute the game directory with additional files that serve no purpose once installation has completed
 - Installers in the scene typically force administrative rights which completely messed up modding tools because the permissions on the installed files are set to administrator only, even if they're placed in a directory that inherits permissions for user to modify. This is often very frustrating for users and wastes lots of their time

M3 mods are mods that are designed to be able to be manually installed by users - if they choose to do so, but optimally are installed through M3. M3 facilitates checks and provides features to the end user such as library management, update checks, relevant mod information, modding rule enforcement and automatic configuration of mods based on the current game state. Deployment of a mod through M3 will make the file size smaller through optimized 7z compression settings designed for the types of files we work on in the scene, as well as perform sanity checks on your mod (such as for broken textures and audio).

While you don't have to use it for your mod, all of the active developers in the scene encourage it's use because end users often don't know what they're doing.



If you need assistance developing a moddesc.ini for your mod, please come to the [ME3Tweaks Discord](https://discord.gg/s8HA6dc).
