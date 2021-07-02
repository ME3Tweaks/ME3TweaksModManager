![Documentation Image](images/documentation_header.png)

## Update TLK files in ME1/LE1 with Mod Manager
With the `GAME1_EMBEDDED_TLK` header, you can merge TLK edits from .xml files, the same as you would if you dumped a TLK file to xml, and merge them into Mass Effect (1).


### The Problem

Mass Effect, both Original Trilogy and Legendary Edition versions, do not use global TLK Files like ME2 and ME3 do. It has a 'global' TLK file, but it's not actually global, mostly only containing non-conversational strings. The conversation strings are located in each conversation's file, which is very inconvenient to ship in a mod. For example, a new localization would have to ship gigabytes of package files for what amounts to maybe a couple of megabytes of actual new data, and it interferes significantly with using other mods that may modify these same packages.

The 'Game 1 TLK Merge' feature that is part of ME3Tweaks Mod Manager 7.0 can work around this issue by dynamically installing your TLK changes without having to perform full package file replacement.


### Setting up TLK merge

Your mod must target `cmmver 7.0` or higher for this feature to work. In your moddesc.ini file, you must add the following section:

```
[GAME1_EMBEDDED_MERGE]
usesfeature = true
```

For a very simple example of a full moddesc.ini file, you would have something like the following:

```
[ModManager]
cmmver = 7.0

[ModInfo]
game = LE1
modname =  TLK Merge Test
moddev = DevNameHere
moddesc = Tests the merging feature
modver = 1

[GAME1_EMBEDDED_TLK]
usesfeature = true
```

This will instruct Mod Manager to parse the directory named `GAME1_EMBEDDED_TLK` for TLK xml files. Files must have a specific naming pattern so that they can be found in game.ates. You can use .pmu files in the alternates system in the exact same way you can use any other file.

![Example folder setup](images/tlk_merge_foldersetup.png)

Files must have the following naming system or it will not work:

**[PackageFileNameBase].[InstancedFullPathInPackage].xml**

 - PackageFileNameBase: The name of the package, without the extension. For example `Startup_INT.pcc` would use `Startup_INT`.
 - InstancedFullPathInPackage: The instanced full path in the package. This can be found in the metadata tab of Legendary Explorer when viewing the TLK export
 
![Where to find instanced full path](images/tlk_merge_instancefullpath.png)

To get a dump of files that are already in the correct naming format, you can use my TLK dump experiment in Legendary Explorer. Go to Package Editor and make sure experiments are on in the `Debugging` menu. A new menu named `Experiments` will show up, and you can go to `Mgamerz's Programming Circus > Dump LE1 TLK to XML` to dump the entire game's TLK to properly named files. You can also filter it by extension, such as INT, DE, etc.
