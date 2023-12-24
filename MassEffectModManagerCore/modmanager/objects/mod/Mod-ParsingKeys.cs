namespace ME3TweaksModManager.modmanager.objects.mod
{
    // Contains ModDesc parsing keys for consistency.
    public partial class Mod
    {
        // NEVER
        // CHANGE
        // EXISTING
        // VALUES
        // OF
        // THESE

        // Values - These are so we write the values consistently to avoid case sensitivity bugs
        // in the future.
        public static readonly string MODDESC_VALUE_FALSE = @"false";
        public static readonly string MODDESC_VALUE_TRUE = @"true";


        // [ModManager]
        public static readonly string MODDESC_HEADERKEY_MODMANAGER = @"ModManager";
        public static readonly string MODDESC_DESCRIPTOR_MODMANAGER_MINBUILD = @"minbuild";     // Minimum build supported by this mod. 
        public static readonly string MODDESC_DESCRIPTOR_MODMANAGER_IMPORTEDBY = @"importedby"; // What version of Mod Manager imported this mod if it is an unofficially supported mod (ME3 only)
        public static readonly string MODDESC_DESCRIPTOR_MODMANAGER_CMMVER = @"cmmver";         // Parser version to use.

        // [ModInfo]
        public static readonly string MODDESC_HEADERKEY_MODINFO = @"ModInfo";
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_GAME = @"game";                                    // Game this mod is for. If null, ME3 is assumed.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_NAME = @"modname";                                 // The name of the mod
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_DEVELOPER = @"moddev";                             // The developer of the mod
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_SITE = @"modsite";                                 // The site associated with the mod. Using a NexusMods link nets additional parsing of the nexuscode from it
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION = @"moddesc";                          // The description of the mod. <br> can be used to make linebreaks.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_UNOFFICIAL = @"unofficial";                        // If this mod is unofficially supported. Mods imported directly from the game will be marked with this.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_UPDATECODE = @"updatecode";                        // The ME3Tweaks Updater Service update code.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_VERSION = @"modver";                               // The version of the mod. Prior to Mod Manage 6.0 this was a float. 6.0 and above use semantic.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_REQUIREDDLC = @"requireddlc";                      // DLC that must be present in order to install this mod.
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_MODMAKERID = @"modid";                             // ModMaker unique identifier for the mod
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_MODMAKERSERVERVERSION = @"compiledagainst";        // Version of ModMaker that was used to compile the server manifest that was then used to generate this mod
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_NEXUSMODSDOMAINID = @"nexuscode";                  // The unique mod id for the game's NexusMods domain
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_REQUIRESAMD = @"amdprocessoronly";                 // OT - If this mod can only install on AMD processors
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_COMPRESSPACKAGESBYDEFAULT = @"prefercompressed";   // OT - If mods should install compressed by default
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_SORTALTERNATES = @"sortalternates";                // If alternates should be automatically sorted
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_BANNERIMAGENAME = @"bannerimagename";              // The filename of the banner image in M3Images
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_LEGACY_COALESCED = @"modcoal";                     // cmmver 2.0 only: If the mod modifies the basegame ME3 Coalesced.bin file
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_POSTINSTALLTOOL = @"postinstalltool";              // The internal tool name to launch after mod install completes
        public static readonly string MODDESC_DESCRIPTOR_MODINFO_REQUIRESENHANCEDBINK = @"requiresenhancedbink";    // LE - If this mod MUST have the enhanced bink dll to work

        // [BASEGAME] and Official DLC task headers (ME3)
        public static readonly string MODDESC_DESCRIPTOR_JOB_DIR = @"moddir";                                    // Directory in the mod manager folder that this job is operating on. A dot '.' is a valid value if you do not actually use files but a directory is required.
        public static readonly string MODDESC_DESCRIPTOR_JOB_NEWFILES = @"newfiles";                             // List of semicolon separated filenames in moddir folder that will be mapped
        public static readonly string MODDESC_DESCRIPTOR_JOB_REPLACEFILES = @"replacefiles";                     // List of semicolon separated file paths to copy matching newfiles to in the game
        public static readonly string MODDESC_DESCRIPTOR_JOB_GAMEDIRECTORYSTRUCTURE = @"gamedirectorystructure"; // If files in newfiles folder should use their relative paths to map directly into the game via replacefiles
        public static readonly string MODDESC_DESCRIPTOR_JOB_ADDFILES = @"addfiles";                             // Deprecated: List of semicolon files to add to ME3/Basegame (use newfiles instead)
        public static readonly string MODDESC_DESCRIPTOR_JOB_ADDFILESTARGETS = @"addfilestargets";               // Deprecated: List of file targets for addfiles
        public static readonly string MODDESC_DESCRIPTOR_BASEGAME_MERGEMODS = @"mergemods";                      // List of semicolon separated mergemod filenames to install from MergeMods folder
        public static readonly string MODDESC_DESCRIPTOR_JOB_JOBDESCRIPTION = @"jobdescription";                 // OT - String that is shown if official DLC is not installed and mod targets official DLC

        // [ME2_RCW]
        public static readonly string MODDESC_HEADERKEY_ME2RCW = @"ME2_RCWMOD";
        public static readonly string MODDESC_DESCRIPTOR_ME2RCW_MODFILE = @"modfile"; // Filename of the RCW .me2mod file

        // [ME1CONFIG]
        public static readonly string MODDESC_DESCRIPTOR_ME1CONFIG_CONFIGFILES = @"configfiles"; // Semicolon separated list of config files to install

        // [CUSTOMDLC]
        public static readonly string MODDESC_HEADERKEY_CUSTOMDLC = @"CUSTOMDLC";                               // Note: source and dest dirs values should always be the same due to bugs in the parser over the years. 
                                                                                                                // Mismatched values probably won't work!
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS = @"sourcedirs";                  // The name of the source directory in the mod folder
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS = @"destdirs";                      // The name of the destination directory to install to in the game's DLC folder
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_INCOMPATIBLEDLC = @"incompatiblecustomdlc";  // List of DLC foldernames that are not compatible with this mod
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_REQUIREDDLC = @"requiredcustomdlc";          // DLC must must be installed in order to install this mod
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_OUTDATEDDLC = @"outdatedcustomdlc";          // Old DLC foldernames. On install these will be prompted to the user for deletion. Used for DLC folder name changes.
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_ALTFILES = @"altfiles";                      // List of AlternateFile structs that define alternate installation options for this mod (use altdlc if you are alternating on CustomDLC!)
        public static readonly string MODDESC_DESCRIPTOR_CUSTOMDLC_ALTDLC = @"altdlc";                      // List of AlternateDLC structs that define alternate installation options for CustomDLC in this mod

        // Alternates (shared)
        public static readonly string MODDESC_DESCRIPTOR_ALTERNATE_MULTILIST = @"multilist"; // Base non-indexed string that identifies a multilist struct

        // [LOCALIZATION]
        public static readonly string MODDESC_DESCRIPTOR_LOCALIZATION_FILES = @"files";          //  
        public static readonly string MODDESC_DESCRIPTOR_LOCALIZATION_TARGETDLC = @"dlcname";    // The DLC name that this localization job is for

        // [TEXTURES]
        public static readonly string MODDESC_DESCRIPTOR_TEXTURESMODS_FILES = @"files"; // LE - List of .mem filenames in the Textures folder

        // [HEADMORPHS]
        public static readonly string MODDESC_DESCRIPTOR_HEADMORPH_FILES = @"files"; // LE - List of headmorph filenames in the Headmorphs folder

        // [GAME1_EMBEDDED_TLK]
        public static readonly string MODDESC_DESCRIPTOR_GAME1TLK_USESFEATURE = @"usesfeature"; // LE1 - If this TLK replacement feature is used by this mod

        // [ASIMODS]
        public static readonly string MODDESC_HEADERKEY_ASIMODS = @"ASIMODS";
        public static readonly string MODDESC_DESCRIPTOR_ASI_ASIMODSTOINSTALL = @"asimodstoinstall";   // List of ASIVersion structs that define what ASI mods to install

        // [UPDATES]
        public static readonly string MODDESC_HEADERKEY_UPDATES = @"UPDATES";
        public static readonly string MODDESC_DESCRIPTOR_UPDATES_SERVERFOLDER = @"serverfolder";                      // Folder on ME3Tweaks.com that houses update dat
        public static readonly string MODDESC_DESCRIPTOR_UPDATES_BLACKLISTEDFILES = @"blacklistedfiles";              // Files that will be removed from the mod when the update is applied
        public static readonly string MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FOLDERS = @"additionaldeploymentfolders"; // Additional folders to include in mod deployment (to both 7z and ME3Tweaks.com)
        public static readonly string MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FILES = @"additionaldeploymentfiles";     // Additional root files to include in mod deployment (to both 7z and ME3Tweaks.com)
        public static readonly string MODDESC_DESCRIPTOR_UPDATES_NEXUSUPDATECHECK = @"nexusupdatecheck";              // Whether to check for updates on NexusMods for this mod. Used if multiple mods are on the same page with different versions.

    }
}
