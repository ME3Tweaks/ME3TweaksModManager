![Documentation Image](images/documentation_header.png)

Mod Manager includes several power user features that are not documented in the interface due to their ability to mess up your game without warning. These features should _only_ be used by users who know what they are doing.

## Shift Key
The following actions are modified when holding the shift key and performing the action:

### Main Interface
 - Clicking on a game filter above the mod library list: Turns off all other games, activates the clicked game filter

### Installation Information Panel (Manage Target)
The follow behavior changes in each of the specified tabs.

#### Installed DLC mods
 - Clicking 'Delete': Skips the prompt and immediately deletes the DLC folder

#### Modified basegame files
 - Clicking 'Restore' on a file: Skips the prompt and immediately restores the file
 - Clicking 'Restore all modified basegame files': Skips the prompt and immediately restore all files (Mod Manager 7.1 and above)

#### Modified SFAR archives
 - Clicking 'Restore' in the 'Modified DLC archives (SFAR)' tab: Skips the prompt and immediately restores the SFAR
 - Clicking 'Restore modified MP SFARs' in the 'Modified DLC archives (SFAR)' tab: Skips the prompt and immediately restores the SFARs (Mod Manager 7.1 and above)
 - Clicking 'Restore modified SP SFARs' in the 'Modified DLC archives (SFAR)' tab: Skips the prompt and immediately restores the SFARs (Mod Manager 7.1 and above)
 - Clicking 'Restore all modified SFARs' in the 'Modified DLC archives (SFAR)' tab: Skips the prompt and immediately restores the SFARs (Mod Manager 7.1 and above)

## Drag & Drop
Mod Manager supports many different file types that can be drag and dropped onto the interface beyond just mod archives.

### Supported mod archive formats
Only .7z files are fully supported. Older original trilogy mods that were shipped before Mod Manager supported them are supported via .rar and .zip formats.
- .7z
- .rar
- .zip
- .exe (only specific ones)
- .me2mod

### Texture mod files
Dropping the following file formats onto the interface will redirect you to install the files with ALOT Installer, as ME3Tweaks Mod Manager does not install textures.
 - .tpf
 - .mod
 - .mem

### Compiling and decompiling features for developers
Mod Manager for nearly a decade has supported drag and drop for Coalesced and TLK formats to make it easy to generate these files.
 - .bin (ME3, LE Coalesced files) -> Decompiled to their formats
 - .tlk (ME2, ME3, LE) -> Decompiled to XML files
 - .extractedbin (LE) -> Recompiled to Coalesced of the same name
 - ME3 XML Coalesced manifest -> Recompiled to Coalesced of the same name
 - Sideload ModMaker XML mod definition files
 - DEBUG BUILDS ONLY: .bin (TOC file): Dumps TOC information to the debug console


### MergeMod Files
Compilation and decompilation of merge mods is done via drag and drop onto the interface.
 - .json -> Compiles mergemod to .m3m file
 - .m3m -> Decompiles the file to its .json and support asset files (if any)



 - 
