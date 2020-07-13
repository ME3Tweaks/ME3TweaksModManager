![Documentation Image](images/documentation_header.png)

This documentation is current as of Build 105.

ME3Tweaks Mod Manager (which will be written as M3 from now on in this document) allows users to organize mods as well as deploy them into the each game of the Mass Effect Trilogy. It is designed to be friendly for users and powerful for developers - for example, as a developer, you do not need to worry about users not installing the binkw32 bypass or running AutoTOC - M3 takes care of this all in the background for users. You can focus on developing your mods and my program will take care of the user side of things.

**The documentation in this article is specific to M3** and should not be applied to Mass Effect 3 Mod Manager, as it does not support moddesc version 6 or higher, though some of the information will be relevant to both.

## Table of contents
 - [moddesc.ini - the definition of an M3 mod](moddesc.ini.md)
 - [moddesc.ini: Data types](datatypes.md)
 - [moddesc.ini: Official DLC and Basegame Headers](officialdlc_and_basegame.md)
 - [moddesc.ini: CUSTOMDLC Header (DLC mods)](customdlc.md)
 - [moddesc.ini: Automatic and alternative mod installations using alternates](alternates.md)
 - [moddesc.ini: Localization mods](localizations.md)


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

M3 mods are mods that are designed to be able to be manually installed by users - if they choose to do so, but optimally are installed through M3. M3 facilitates checks and provides features to the end user such as library management, update checks, relevant mod information, modding rule enforcement and automatic configuration of mods based on the current game state. Deployment of a mod through M3 will make the file size smaller through optimized 7z compression settings designed for the types of files we work on in the scene, as well as perform sanity checks on your mod (such as for broken textures and audio).

While you don't have to use it for your mod, all of the active developers in the scene encourage it's use because end users often don't know what they're doing.



If you need assistance developing a moddesc.ini for your mod, please come to the [ME3Tweaks Discord](https://discord.gg/s8HA6dc).
