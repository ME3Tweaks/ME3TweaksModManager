![Documentation Image](images/documentation_header.png)

# LE1 TLK Override Feature

Starting in ME3Tweaks Mod Manager 9.2.1, Autoload Enabler v13 ASI is the standard version installed with any LE1 mod. This ASI enables a host of features for modding LE1, but v13 specifically adds the ability to override TLK strings globally, similar to how LE2 and LE3 do.

The vanilla TLK lookup system in LE1 is a list of `BioTlkFile` objects. When a string is looked up, the list is enumerated in reverse order, and each TLK is queried for that ID until a match is found. As conversations use local package `BioTlkFile` exports, these are always loaded last, and unload with the file. This makes text local to the conversation (which is relatively convenient), but unable to be overridden with editing the package (not convenient). The [Game 1 TLK Merge feature](game1_tlk_merge.md) helped deal with this by letting developer ship only TLK edits, but it is still invasive, generating package edits.

The LE1 TLK Override feature changes this logic to the following:
1. The TLK list is enumerated in reverse order, only looking in TLK objects that contain the object flag `NotForServer`.
2. If none string is found, the original logic is used.

This system prevents ecosystem disruption by requiring opt in to use the feature.

## Setup
To use this functionality, your global TLK files in your mod must have the object flag `NotForServer` set on the `BioTlkFile` exports in them. This flag name may not seem intuitive, but it allows use of existing functionality in the game that is otherwise not used.

If you are using a global TLK override, all `BioTlkFile` entries in the localization set must have matching `NotForServer` flags. For example, given the following files:

 - DLC_MOD_TLKTest.GlobalTlk.pcc
 - DLC_MOD_TLKTest.GlobalTlk_ES.pcc
 - DLC_MOD_TLKTest.GlobalTlk_RU.pcc
 - ...

If only the first file's `BioTlkFile` exports are marked `NotForServer`, the mod will fail deployment, as they all need to match to ensure consistent behavior across languages.
