![Documentation Image](images/documentation_header.png)

# Headmorph mods

ME3Tweaks Mod Manager, starting with Build 128/Version 8.1, supports installation of headmorph mods. These types of mods have one of the following file extensions:
 - .me2headmorph
 - .me3headmorph
 - .ron

These files are produced from either Gibbed's save editors (for ME2/ME3), or Trilogy Save Editor (all games) by Karlitos. Headmorphs are installed via [Trilogy Save Editor - CLI version](https://github.com/KarlitosVII/trilogy-save-editor-cli). **Mod Manager does not support installing headmorphs to the original Mass Effect (2008).**

Installation of headmorphs through Mod Manager can be facilitated in two different ways:

- Manual selection (user selects file from the file system, and it applies to the current target's game)
   - This menu option is under the 'Mod Management' menu
- Via 'Install Headmorph' button (the selected mod must reference headmorphs)
   - The option to install a headmorph appears next to the 'Apply Mod' button when the selected mod includes headmorph references

![2023-04-12_23h46_58](https://user-images.githubusercontent.com/2738836/232230688-8dccd919-c365-4001-99ed-642265b05025.png)

Referencing headmorphs in your mod manager mod is easy. You simply make a `Headmorphs` folder in your mod, place your headmorph files there, and then reference them under the [HEADMORPHS] moddesc.ini task header, which you can do in the Headmorphs tab of the moddesc.ini editor.

![image](https://user-images.githubusercontent.com/2738836/232234917-6a7a6325-42b3-4a3e-a497-9c3dbe0386a3.png)

![image](https://user-images.githubusercontent.com/2738836/232234894-915e57db-c874-46c3-8c47-f71f9df1e9eb.png)

Headmorphs in a mod manager mod provide a better user experience than manually installing headmorph files. It provides the following feature set:

- Title and description
- Selector dialog with optional image, like alternates
- DLC requirements for install (to prevent users from just installing the headmorph when it depends on something)
- Ensures a headmorph is installed to the correct game
- Provides a visual save selector, in a similar style to the game

![image](https://user-images.githubusercontent.com/2738836/232246533-a01d3f89-c6f4-415b-99f9-1699a5888f90.png)

## [HEADMORPHS] descriptors
The descriptors for the [HEADMORPHS] task header are shown below.

| Descriptor name | Value type                  | Purpose                                   | Required | Supported Moddesc versions |
|-----------------|-----------------------------|-------------------------------------------|----------|----------------------------|
| files           | List of M3HeadMorph structs | Describes a list of referenced headmorphs | Yes      | 8.1                        |

### M3HeadMorph struct
The descriptors that compose a M3HeadMorph struct are shown below.

| Descriptor name | Value type               | Purpose                                                                                                                                                                                                                                          | Required                  | Supported Moddesc versions |
|-----------------|--------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------|----------------------------|
| Filename        | string                   | The name of the headmorph file in the Headmorphs folder                                                                                                                                                                                          | Yes                       | 8.1+                       |
| Title           | string                   | The title to display to the user for the headmorph when shown in the interface                                                                                                                                                                   | Yes                       | 8.1+                       |
| Description     | string                   | Description to show under the title of the headmorph                                                                                                                                                                                             | No                        | 8.1+                       |
| ImageAsset      | string                   | The name of an image in the M3Images folder to show to the user when they are selecting a headmorph from your mod                                                                                                                                | No                        | 8.1+                       |
| ImageHeight     | integer                  | The height of the preview image, in pixels. Must be greater than 0. You will often want this to just match your image height, but you can use this to scale it down or up if you wish.                                                           | Yes (if using ImageAsset) | 8.1+                       |
| RequiredDLC     | List of DLC folder names | The list of DLC mod folders that must exist in the installation target for this headmorph to be installed. If your headmorph depends on a DLC for assets, you should ensure you put it here, to help prevent the user from breaking their games. | No                        | 8.1+                       |

### Example
The following example is taken from Fanciful EDI Armor Variations (LE2/LE3) version 1.2.

```ini
[HEADMORPHS]
files = ((Filename=AeonFlux.me3headmorph,Title=AeonFlux,Description="Headmorph to give you the look of Aeon Flux, from the 2005 film.",ImageAsset=headmorph_aeon.jpg,ImageHeight=189),(Filename=gamora.ron,Title=Gamora,Description="Headmorph to give you the look of Gamora from the Marvel Comic series. Installing this headmorph will make this save require this mod to be installed; the game will crash if this mod is not installed and a save with this headmorph is loaded.",ImageAsset=headmorph_gamora.jpg,ImageHeight=189,RequiredDLC=DLC_MOD_FancifulEDI[1.2]),(Filename=shiro.me3headmorph,Title=Shiro,Description="Headmorph to give you the look of Shiro from No Game No Life.",ImageAsset=headmorph_shiro.jpg,ImageHeight=189))
```
