![Documentation Image](images/documentation_header.png)

# Config Merge

ME3Tweaks Mod Manager, starting with Build 128/version 8.1, supports merging configuration files via the use of M3 Config Delta files, also known as `M3CD` files. They have the extension `.m3cd`, and these files are placed into DLC mod folders for Mass Effect Legendary Edition games. You can ship as many .m3cd files with your DLC folder as you want; they will all be applied. 

**Config merge features are not supported for Original Trilogy games.**

![image](https://user-images.githubusercontent.com/2738836/232246276-afbd7ced-6d0b-4135-a185-efb58e57d4ae.png)

Config Merge behavior is different between LE1 and LE2/LE3.

## Config Merge behavior with Mass Effect (Legendary Edition)
LE1 does not support configuration file merge on DLC mount like the original ME1 did. The code to perform this merge was stripped out in the PS3 release of Mass Effect.

When necessary, Mod Manager will perform a LE1 Coalesced Merge, which loads a vanilla copy of the LE1 Coalesced_INT.bin file. It then applies all properly named .m3cd files in DLC mod folders from lowest to highest mount to the Coalesced file, then commits the result to the basegame Coalesced_INT.bin file. It then copies the file to the other localized versions, if present. The localized versions of Coalesced in LE1 and LE2 appear only have differences in the editor string files, which are not used by shipping versions of the game.

## Config Merge behavior with Mass Effect 2/3 (Legendary Edition)
Since these games support config merge natively via DLC, using .m3cd files allows you to ship piecemeal changes to your DLC mod, which are applied when the mod is installed. An example use case of this is using an alternate to ship an extra .m3cd file in your DLC mod, which enables more costumes. 

## M3CD format
M3CD files are basic ini files that must follow the following specification in order to work.

- They must be named in the following format: `ConfigMerge-*.m3cd`, where * is whatever you want. If your file does not match this name, Mod Manger will ignore it.
- Section headers in this ini file use the following format:
   - [INIFILENAME SECTIONAME]
   - For example, `[BioUI.ini sfxgame.biosfhandler_browserwheel]`
     - This targets the BioUI.ini file
     - This targets the sfxgame.biosfhandler_browserwheel section in that file
     - You must use .ini files - even in LE3! Internally they are labeled as .ini. Do not use .xml as our decompilation tools use. 

## M3CD ini typings
Similar to ME2/LE2 DLC ini files, m3cd files also use _typings_ to control how the ini file is merged. Due to the M3CD feature supporting all three games, there are some differences from ME2/LE2's format, so use the following reference when designing your m3cd files.

### Data type prefixes
All entries can have a prefix with the following values.
| Prefix | Game 3 Type Number | Description                                                                  |
|--------|--------------------|------------------------------------------------------------------------------|
| +      | 3                  | Adds the value to the property if it is unique                               |
| !      | 1                  | Removes the property entirely (also known as clear). Set the value to `null` when using this.                          |
| -      | 4                  | Removes the value from the property if it is an exact match (case sensitive) |
| .      | 2                  | Ads the value to the property, regardless if an exactly copy already exists  |
| >      | 0                  | M3-specific: Clears a property, and then adds the value to it.               |

If no value is specified, the default `. add` is implied.

### Double typing
M3CD supports double typing, which can be used with LE2/LE3 config merge. This allows you to insert values to your mod's config files without actually 'merging' them. A use case for this is inserting a type `!/1` clear entry into your config file, rather than running a clear operation on your config file itself.

Double typing only works when you are doing `+` or `.` operations.

Specify a double typing by using two prefixes, the first being the operation for merging into your config file, the second being the type value to set. 

An example entry:
```ini
+!lstpages=null
```

This double typed property does the following:
 - Adds the entry `!lstpages=null` to the mod's config file. This will clear the lstpages entry when merged by the game on DLC mount
 - The double typing changes it from type `+/3` to type `!/1` when actually placed into your DLC config file.
 - The resulting config file entry would yield:

```xml
...
		<Section name="sfxgame.biosfhandler_browserwheel">
			<Property name="lstpages">
				<Value type="1">null</Value>
...
```

## Example
An example m3cd file from LE3 is shown below.
```ini
[BioUI.ini sfxgame.biosfhandler_browserwheel]
; Add an array clear to the the ini - we use double typing here + and !:
; + to add the rest of the string to the config file
; ! as the actual type set.
; Double typing like this prevents actual property merge from occuring and simply adds the value
; to the ini
+!lstpages=null

; Now we just do single typing to add our values to the config ini
; In this example, we changed the label string refs to be random
; so what you see in menu will look all out of order.
.lstpages=(Tag="Options",\t\t\tsrLabel=126262,\tType=MBW_SP_SquadRecord)
.lstpages=(Tag="Resume",\t\t\tsrLabel=126264, Type=MBW_SP_Resume)
.lstpages=(Tag="Load",\t\t\t\tsrLabel=174868,\tType=MBW_SP_Load)
.lstpages=(Tag="AreaMap",\t\t\tsrLabel=157151,\tType=MBW_SP_SquadRecord)
.lstpages=(Tag="SquadRecord",\t\tsrLabel=126265,\tType=MBW_SP_SquadRecord)
.lstpages=(Tag="Journal",\t\t\tsrLabel=126263,\tType=MBW_SP_SquadRecord)
.lstpages=(Tag="Save",\t\t\t\tsrLabel=126257,\tType=MBW_SP_Save)
.lstpages=(Tag="INVALID",\t\t\tsrLabel=126261,\tType=MBW_SP_SquadRecord)
```
