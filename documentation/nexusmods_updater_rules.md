![Documentation Image](images/documentation_header.png)

ME3Tweaks Mod Manager (which will be written as M3 from now on in this document) can check for updates to your mod on NexusMods, provided that you, as a developer, agree to follow them. These rules are in place to ensure a good user experience for users of M3. M3 will only check for updates on NexusMods if the mod is whitelisted by ME3Tweaks. NexusMods updates are not like ME3Tweaks Updater Service updates, which determine which local files need updated and can be updated directly within M3.

1. Your 'latest mod version' MUST always be accurate, and be a version number in the format of X.X[.X[.X]] (same as ModDesc 6 and above). If you don't use this versioning scheme, your mod will not be whitelisted.
2. If you update your mod, the file versions should match the mod version. There are two separate versions available on Nexus.
3. You must set your `modsite` attribute in your moddesc.ini to your nexusmods page, or set the `nexuscode` to your mod's ID for your game, which is the number in the URL of your NexusMods page.
4. You must notify Mgamerz you want to participate in this service.

Breaking rules 1 or 2 may lead users to get infinite update prompts, which is why this whitelist is in place. It may seem 'inconvenient' to you that you have to upload your mod again to fix the moddesc, but users do not enjoy endless update prompts and confusing out of sync version numbering because the version information on NexusMods and the local version numbers don't match. If your mod violates the rules, it will be removed from the whitelist, and it will no longer check for updates on NexusMods.

## What I have more than one mod on my page?
If you host more than one mod on your page, the versions MUST be all in sync, otherwise rule 1 and 2 will not be true, as only the mod page is checked for version information - file versions are not checked. Either split the mod to another page so you have another ID, or update the moddesc.ini files to not check for updates by adding `nexusupdatecheck=false` to the `[UPDATES]` header.

If you need assistance developing a moddesc.ini for your mod, please come to the [ME3Tweaks Discord](https://discord.gg/s8HA6dc).
