![Documentation Image](images/documentation_header.png)

# Localization "mods"

ME3Tweaks Mod Manager, starting with Build 108/Version 6.1, support installation of localization "mods". The term mods here is in quotes as it is not really a mod, it's more of an addition to an existing mod. 

![Localization Example](images/documentation_localizations.png)

Localization mods must be standalone mods. You cannot combine a LOCALIZATION task header with any other task headers that install things.

| Descriptor name | Purpose | Required | Supported Moddesc versions |
|-----------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|----------------------------|
| dlcname | The name of the target DLC that this localization is for. You cannot target anything not starting with DLC_ and you cannot target official DLC folders. | Yes | 6.1+ |
| files | Semicolon separated list of files that will be added to the specified `dlcname`. These files must all begin with the same value as `dlcname`, followed by an underscore, the language code, and end with .tlk. No other file types are allowed. This list is of filepaths relative to the root of the mod. | Yes | 6.1+ |

An example of this would be the Italian translation of Ark Mod, which doesn't ship with the ITA localization files. A third party developed this localization, and with permission, distributed this as a separate download on NexusMods. This file could be installed with the LOCALIZATION task header in Mod Manager.


And example moddesc for this would be:
```
[ModManager]
cmmver = 6.1

[ModInfo]
modname = Ark Mod - ITA
moddev = Sinistro91
moddesc = Traduzione italiana dell'Ark MOD di Kinkojiro, considerata una espansione dell'acclamata mod EGM - Expanded Galaxy.
modver = 0.84
modsite = https://www.nexusmods.com/masseffect3/mods/844

[LOCALIZATION]
dlcname = DLC_MOD_EGM_Ark
files = DLC_MOD_EGM_Ark_ITA.tlk

```