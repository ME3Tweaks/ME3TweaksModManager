![Documentation Image](images/documentation_header.png)

The M3 Global Shader (M3GS) merge system is a new feature included in Mod Manager 9.2 and up. This system enables
developers to replace shaders that are stored in the `GlobalShaderCache-PC-D3D-SM5.bin` files in the Legendary Edition
games. The post processing shaders and other non-material shaders are stored in this file.

>[!NOTE]
>You cannot edit material shaders using this system.

M3GS merge occurs as part of target merge after mod installs and game modifications and applies shaders in 
mount priority order, with the highest priority ones being used when there are overrides.


### Dumping global shader HLSL for editing
In Legendary Explorer builds newer than 01/14/2026, global shader caches can be dumped via the Experiments menu 
of Package Editor. Selecting this will prompt you to pick a game and an output folder, after which several hundred
decompiled shader files (.hlsl) will be dumped. The dumped files are the starting point for editing shaders in the global
shader cache.

<img width="1183" height="418" alt="image" src="https://github.com/user-attachments/assets/97d10d3c-c631-4d32-afcc-711ddabee78d" />

### Editing shaders
To edit shaders, see the basic [shader editing guide on the LEX Wiki](https://github.com/ME3Tweaks/LegendaryExplorer/wiki/Shader-Editing). Shader edits should be saved in .hlsl files.

### Compiling shaders
To compile shaders with M3, you must have the [Windows SDK](https://learn.microsoft.com/en-us/windows/apps/windows-sdk/) installed [provide guide on which options to pick].
Compiled shader files should have the .m3gs extension.

To target a specific shader in the global shader cache, it must have the following naming standard:

`GlobalShader-#-*.m3gs`

where # is the shader index in the global shader cache. Using the extraction experiment will produce files that already meet this naming system.

.m3gs files should be placed in your DLC mod's `CookedPCConsole` folder. The shader will be merged as part of target merge when the DLC configuration changes for the game.
