![Documentation Image](images/documentation_header.png)

# Texture mods

ME3Tweaks Mod Manager, starting with Build 128/Version 8.1, supports installation of texture mods (*.mem) for Legendary Edition games. Support for installing texture mods to Original Trilogy games is not planned.

Installation of .mem texture texture mods through Mod Manager can be facilitated in two different ways:

- Manual selection (user selects file from the file system, and it applies the correct game, based on the default target that matches that game)
   - This menu option is under the 'Mod Management' menu
- Via inclusion in a Batch Installer Install Group
   - In future Mod Manager builds there may be other ways to install, but currently it is restricted to batch installer to enforce proper installation order

Referencing texture mods in your mod manager mod is easy. You simply make a `Textures` folder in your mod, place your .mem files there, and then reference them under the [TEXTUREMODS] moddesc.ini task header, which you can do in the Textures tab of the moddesc.ini editor.

![image](https://user-images.githubusercontent.com/2738836/232241463-a2adfbda-29fa-42f5-980d-f15b4b15104e.png)

![image](https://user-images.githubusercontent.com/2738836/232241557-57b2a3c6-82d0-42df-bed4-a265c5fe208a.png)

Installing textures via a mod manager batch install queue provides a better user experience when installing multiple different texture mods at a time. Mod Manager provides the following feature set:

- Title and description of a texture mod, and the mod that contains the texture mod
- List of texture export names modified by the texture mod
- Optional image that can convey context to a user about what a specific texture mod does (e.g. a preview image of an outfit change)
- Ensures a texture mod is installed to the correct game - users don't have to try to remember if they forget
- Proper installation procedure is followed (textures always last)
- Contains same rule checks as Mass Effect Modder (GUI)
- When something goes wrong during texture installation, Mod Manager is able to provide more context than Mass Effect Modder GUI version

![image](https://user-images.githubusercontent.com/2738836/232244818-9a22874a-21c2-40ea-980e-10e6e5659e3f.png)

Mod Manager mods that contain texture mods MUST be imported to the library. They cannot be directly installed from archive. Mod Manager mods can be configured to only contain texture mods; in this case, the `Apply mod` button changes to `Not a content mod`.

## [TEXTUREMODS] descriptors
The descriptors for the [TEXTUREMODS] task header are shown below.

| Descriptor name | Value type                  | Purpose                                   | Required | Supported Moddesc versions |
|-----------------|-----------------------------|-------------------------------------------|----------|----------------------------|
| files           | List of M3MEMMod structs | Describes a list of referenced texture mod files | Yes      | 8.1                        |

### M3MEMMod struct
The descriptors that compose a M3MEMMod struct are shown below.

| Descriptor name | Value type               | Purpose                                                                                                                                                                                                                                          | Required                  | Supported Moddesc versions |
|-----------------|--------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------|----------------------------|
| Filename        | string                   | The name of the texture mod file in the Textures folder. It must end with **.mem**                                                                                                                                                                                          | Yes                       | 8.1+                       |
| Title           | string                   | The title to display to the user for the texture mod when shown in the interface                                                                                                                                                                   | Yes                       | 8.1+                       |
| Description     | string                   | Description to show under the title of the texture mod                                                                                                                                                                                             | No                        | 8.1+                       |
| ImageAsset      | string                   | The name of an image in the M3Images folder to show to the user when they are selecting the texture mod                                                                                                                                | No                        | 8.1+                       |
| ImageHeight     | integer                  | The height of the preview image, in pixels. Must be greater than 0. You will often want this to just match your image height, but you can use this to scale it down or up if you wish.                                                           | Yes (if using ImageAsset) | 8.1+                       |
