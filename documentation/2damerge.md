![Documentation Image](images/documentation_header.png)

# LE1 Bio2DA Merge
Starting with ME3Tweaks Mod Manager 9.0, 2DA tables shipped in DLC mods can be automatically merged into the basegame versions. This works around issues in the Bio2DANumberedRows system, where rows could not be relaibly overwritten due to changes in how 2DA tables work - either in the PS3 port of ME1 or the Legendary Edition upgrade. The issue lies in how the row is accessed; some methods properly will find the overwritten row, other accessors will not. The LE1 2DA merge feature simply bypasses the need to overwrite 2DA rows in autoload by directly overwriting them in the basegame 2DAs.

>[!CAUTION]
>The LE1 2DA Merge feature is on by default, so that mods that use it will work. For this feature to work, every time target merge occurs, Mod Manager will reset all basegame 2DAs to their original data, then apply the changes. If you are directly editing the Basegame 2DAs, it is liable to be overwritten. Be careful!


## How to use 2DA merge
ME3Tweaks Mod Manager will automatically scan DLC mods for `.m3da` files, and merge them in mount-order, alphabetically per DLC. `.m3da` files are just renamed `.json` files that describe changes to make; you can ship multiple `.m3da` files in your DLC folder using the alternates system to provide customizable 2DA overrides, similar to LE1 Config Merge. This merge takes place within the `Target Merge` feature of Mod Manager, which occurs when necessary, such as after a mod install, after enabling/disabling/removing DLC mods, etc. You can manually trigger it in the Tools menu during development.

![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/097bf9c9-181f-4a1d-9706-4aa611703b3c)


There are at minimum two files required for the LE1 2DA Merge feature to work; a `package file` containing tables with your customized rows, and a `m3da` file that describes what to install.
![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/68a9e7df-a3cb-496d-9483-2308ad2c4a19)

The only restriction on the `.m3da` filename(s) are that they must start with your DLC foldername, followed by a dash, and at least one more character.
Some examples with the mod folder name of `DLC_MOD_2DAMERGE`:
 - `DLC_MOD_2DAMERGE-My2DAs.m3da` - OK
 - `DLC_MOD_2DAMERGE-HardModeTables.m3da` - OK
 - `DLC_MOD_2DAMERGE-.m3da` - BAD
 - `DLC_MOD_2DAMERGE.m3da` - BAD
 - `My2DAs.m3da` - BAD


Your 2DA package files are referenced by these `.m3da` files and can have any name. They are not actually used by the game.

For example, this is Pinnacle Station DLC's 1.0.5 2DA merge file. In this mod, I keep the base filenames the same, so they appear next to each other in the folder.

`DLC_MOD_Vegas-2DAs.m3da`:
```json
[
    {
        "comment": "Merges Pinnacle Station's 2DA tables - Engine",
        "packagefile": "Engine.pcc",
        "mergepackagefile": "DLC_MOD_Vegas-Pinnacle2DAs.pcc",
        "mergetables": [
            "BIOG_2DA_Vegas_AreaMap_X.Areamap_AreaMap_part_5",
            "BIOG_2DA_Vegas_Equipment_X.Items_Items_part_10",
            "GalaxyMap_Map_part_5",
            "GalaxyMap_Planet_part_5",
            "GalaxyMap_PlotPlanet_part_5",
            "Rewards_Statistics_part_1",
            "BIOG_2DA_Vegas_Music_X.Music_Music_part_2",
            "Treasure_Tables_TreasureDist_part_5",
            "Treasure_Tables_TreasureEntries_part_5",
            "Treasure_Tables_TreasureTables_part_5",
        ]
    },
    {
        "comment": "Merges Pinnacle Station's 2DA tables - SFXGame",
        "packagefile": "SFXGame.pcc",
        "mergepackagefile": "Pinnacle2DAs.pcc",
        "mergetables": [
            "Images_CodexImages_part_5",
            "Images_GalaxyMapImages_part_5",
            "Images_ShopImages_part_5"
        ]
    }
]

```

`DLC_MOD_Vegas-Pinnacle2DAs.pcc`:
![2024-06-28_19h45_06](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/f4fa45c9-5274-4841-ba92-b85a0b0ac0fa)

>[!CAUTION]
>These tables should __ONLY__ contain modified rows. If you ship an entire table with just a few row changes and the rest are vanilla, you will completely override mods that mount below yours. DO NOT DO THIS: Your mod may end up being blacklisted. Take the time, do it right!

## M3DA json format
The json format for `.m3da` files is as follows:

| Key              | Value                                          | Description                                                                                                                                                                                                          | Required |
|------------------|------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|
| comment          | String                                         | Anything you want - this is not used for anything beyond human readability and context                                                                                                                               | No       |
| packagefile      | String(Filename)                               | The name of the basegame package file that contains the table you are merging into. This will typically be Engine.pcc, SFXGame.pcc, or EntryMenu.pcc.                                                                | Yes      |
| mergepackagefile | String(Filename)                               | The name of the package file in your DLC folder that contains the 2DA tables to merge                                                                                                                                | Yes      |
| mergetables      | List&lt;string&gt;(Export instanced full paths) | List of tables to merge in mergepackagefile. They must all have their object name end in `_part` and have the same basename as the target table, just like normal autoload 2DAs did. Your 2DA packages should not be referenced by autoload when using this feature. | Yes      |

The values of `mergetables` are the Instanced Full Path of your source table in your package file. This means you can have duplicates of tables that can be referenced by different m3da files - for example, putting different alternate tables for merging under different package exports with a clear package name of what that table contains. For example, the HardMode option below could be referenced in a separate m3da that is only added by an alternate option the user picks:

![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/45e39199-e2d4-4719-84eb-355954f3c403)

The following files are allowed to be merged into:
 - Engine.pcc
 - SFXGame.pcc
 - EntryMenu.pcc
 - BIOG_2DA_UNC_AreaMap_X.pcc
 - BIOG_2DA_UNC_GalaxyMap_X.pcc
 - BIOG_2DA_UNC_GamerProfile_X.pcc
 - BIOG_2DA_UNC_Movement_X.pcc
 - BIOG_2DA_UNC_Music_X.pcc
 - BIOG_2DA_UNC_Talents_X.pcc
 - BIOG_2DA_UNC_TreasureTables_X.pcc
 - BIOG_2DA_UNC_UI_X.pcc

