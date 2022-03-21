![Documentation Image](images/documentation_header.png)

## Adding emails to Mass Effect 2
In ME2 and LE2, the email system is controlled through a very complicated set of Kismet sequences in the BioD_Nor_103Messages.pcc file. Adding or modifying emails is a complicated process, and since it's all in one package file, it is a compatibility nightmare. Mod Manager 8 ships with a new Game 2 Email Merge feature to make it much easier to add emails in Game 2 without compatibility issues. The merge feature makes the necessary edits to the sequence to add new emails for you.

To enable email merge, you must add a file to the `CookedPC` (ME2) or `CookedPCConsole` (LE2) folder of your DLC mod with the filename `EmailMergeInfo.emm`. This will be your email merge manifest in a JSON structure, similar to how [Squadmate Outfit Merge](squadmate_outfit_merge.md) works. No other files are needed for email merge to work, although you will need to add strings to your DLC mod's TLK file for the subject and text of your email. The rest of this document outlines the structure of this JSON file.

An example `EmailMergeInfo.emm` file:

```{
{    
    "game": "LE2",
    "modName": "Another Email Test Mod",
    "emails": [
        {
            "emailName": "FemshepEmail",
            "statusPlotInt": 941,
            "triggerConditional": "local BioGlobalVariableTable gv;gv = bioWorld.GetGlobalVariables();return gv.GetInt(941) == 0 && gv.GetBool(66) == TRUE;",
            "titleStrRef": 18919895,
            "descStrRef": 18919896
        },
        {
            "emailName": "MShepEmail",
            "statusPlotInt": 942,
            "triggerConditional": "local BioGlobalVariableTable gv;gv = bioWorld.GetGlobalVariables();return gv.GetInt(942) == 0 && gv.GetBool(66) == FALSE;",
            "titleStrRef": 18919897,
            "descStrRef": 18919898
        }
    ]
}
```

### JSON Structure

**game**

The game this merge manifest is for. Must be either `LE2` or `ME2`

**modName**

A string to use internally for your mod's name. This text is never shown in-game, it is merely used inside the sequence to keep things organized.

**inMemoryBool**

Optionally, the ID of an in-memory plot bool that will be true if your mod is installed. If this bool is false, emails from your mod will not be sent. This can be used as an extra sanity check. You do not have to provide a value for the merge to work.

**emails**

Array of emails to be installed

### Email Structure

**emailName**

A string to use internally for the title of this email. This text is never shown in-game, it is used inside the sequence to keep things organized.

**statusPlotInt**

The ID of a plot integer that will be used to keep track of the status of this email. Each email should have a unique plot integer that does not conflict with any other mod, as this plot int will be written to save files. The merge will handle the sequencing to set this int properly, but you must determine the int ID.

| Int Value | Email Status   |
| --------- | -------------- |
| 0         | Email Not Sent |
| 1         | Email In Inbox |
| 2         | Marked as Read |

**triggerConditional**

The inner UnrealScript code of a conditional that will be used to trigger when this email should be sent. This conditional MUST check whether the `statusPlotInt` is 0, other plot variables would also be checked in this conditional to have your email be sent at a certain point. For example, if your `statusPlotInt` is 800, your triggerConditional must be, at the minimum, `local BioGlobalVariableTable gv;gv = bioWorld.GetGlobalVariables();return gv.GetInt(800) == 0;`. This would cause your email to be sent immediately at the start of the game. 

**titleStrRef**

The TLK string ID of the in-game subject of your email. This must be added to your DLC mod's TLK file.

**descStrRef**

The TLK string ID of the in-game text of your email. This must be added to your DLC mod's TLK file.

**readTransition**

Optional: The ID of a plot transition to be fired when your email is read.

### When merge takes place
Upon installation of any mod, if any in-game DLC folders contain a `EmailMergeInfo.emm` file, email merge will take place to ensure the game reflects the installed email merge files. When disabling or removing a DLC mod, email merge also will re-run.

Emails from all installed mods will be merged into a single BioD_Nor_103Messages.pcc file, and this file will be placed in a high-mounting DLC folder created by M3.
