![Documentation Image](images/documentation_header.png)

# ASI Mods - Native code mods
ASI mods are highly technical, very advanced features that are effectively dlls that are injected into the game at runtime. These enable advanced features, such as enabling the autoload system for LE1, making screenshots in PNG format, and enabling various developer debugging tools.
![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/db5ae3b7-843f-41f7-8b0f-2fcc0a257478)

Due to the nature of ASI mods being native code, they can be a security risk as well as cause significant game instability. Mod Manager restricts what ASI mods can be installed through it to ones verified by ME3Tweaks group, through a system called the ASI manifest. If you have an ASI mod you want in the ASI manifest, it must be open source with its source code available, and the deployed ASI must be built by ME3Tweaks or a trusted affiliate, to ensure the build process does not differ from the source. You can then request it be added by messaging Mgamerz on the ME3Tweaks Discord.

Starting with Mod Manager 9, mods that have need can request an ASI be installed along with it to enable necessary features. For example, the LE3 Multi TFC ASI mod enables LE3 mods that ship more than 2GiB of textures to work in a single mod folder.

You can add ASI mod installation requests to your mod by adding them to moddesc under the `ASIMODS` task header. The values are a list of parenthesis split `M3ASIVersion` structs.

```ini
[ASIMODS]
asimodstoinstall = ((GroupID=33))
```

>[!WARNING]
>ME3Tweaks monitors usage of this in publicly released mods. Mods that do not need ASI mods to function may be blacklisted as ASI mods should not be installed unless requested by a user or required for modding to work. Only include ASI mods on private mods or ones that absolutely need it. 

The `M3ASIVersion` struct is uses the following parameters:
| Descriptor | Value type | Purpose                                                                                                                                   | Required | Min supported cmmver |
|------------|------------|-------------------------------------------------------------------------------------------------------------------------------------------|----------|----------------------|
| GroupID    | integer    | Specifies the ASI group to install. ASIs are grouped so that old versions can be tracked. Ensure your GroupID is for your game.           | Yes      | 9                    |
| Version    | integer    | Specifies a specific version to install. If not defined, the latest one at time of install is used instead. Versions are always integers. | No       | 9                    |

GroupIDs can be found in the [ASI manifest file](https://me3tweaks.com/mods/asi/getmanifest?AllGames=1) that Mod Manager automatically downloads as part of the combined services fetch every few hours.
