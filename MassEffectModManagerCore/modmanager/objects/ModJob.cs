using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
    [DebuggerDisplay(@"ModJob for {Header}")]
    public class ModJob
    {
        public enum JobHeader
        {
            BASEGAME,
            CUSTOMDLC,

            //ME1 ONLY
            BRING_DOWN_THE_SKY,
            PINNACLE_STATION,
            ME1_CONFIG,

            //ME2
            AEGIS_PACK,
            APPEARANCE_PACK_1,
            APPEARANCE_PACK_2,
            ARC_PROJECTOR,
            ARRIVAL,
            BLOOD_DRAGON_ARMOR,
            CERBERUS_WEAPON_ARMOR,
            COLLECTORS_WEAPON_ARMOR,
            EQUALIZER_PACK,
            FIREPOWER_PACK,
            FIREWALKER,
            GENESIS,
            INCISOR,
            INFERNO_ARMOR,
            KASUMI,
            LAIR_OF_THE_SHADOW_BROKER,
            NORMANDY_CRASH_SITE,
            OVERLORD,
            RECON_HOOD,
            SENTRY_INTERFACE,
            TERMINUS_WEAPON_ARMOR,
            UMBRA_VISOR,
            ZAEED,
            //ME2_COALESCED,
            ME2_RCWMOD, //RCW Mod Manager coalesced mods

            //ME3 ONLY
            BALANCE_CHANGES,
            //COALESCED,
            RESURGENCE,
            REBELLION,
            EARTH,
            RETALIATION,
            RECKONING,
            PATCH1,
            PATCH2,
            FROM_ASHES,
            EXTENDED_CUT,
            LEVIATHAN,
            OMEGA,
            CITADEL,
            CITADEL_BASE,
            APPEARANCE,
            FIREFIGHT,
            GROUNDSIDE,
            GENESIS2,
            COLLECTORS_EDITION,
            TESTPATCH
        }

        /// <summary>
        /// RCWMod file. ONLY USED WITH RCWMOD HEADER.
        /// </summary>
        public RCWMod RCW { get; set; }

        public static IReadOnlyDictionary<string, JobHeader> ME3OfficialDLCFolderToHeaderMapping = new Dictionary<string, JobHeader>()
        {
            { @"DLC_CON_MP1", JobHeader.RESURGENCE },
            { @"DLC_CON_MP2", JobHeader.REBELLION },
            { @"DLC_CON_MP3", JobHeader.EARTH },
            { @"DLC_CON_MP4", JobHeader.RETALIATION },
            { @"DLC_CON_MP5", JobHeader.RECKONING },
            { @"DLC_UPD_Patch01", JobHeader.PATCH1 },
            { @"DLC_UPD_Patch02", JobHeader.PATCH2 },
            //{ @"DLC_TestPatch", JobHeader.TESTPATCH }, //This is effectively not used.
            { @"DLC_HEN_PR", JobHeader.FROM_ASHES },
            { @"DLC_CON_END", JobHeader.EXTENDED_CUT },
            { @"DLC_EXP_Pack001", JobHeader.LEVIATHAN },
            { @"DLC_EXP_Pack002", JobHeader.OMEGA },
            { @"DLC_EXP_Pack003", JobHeader.CITADEL },
            { @"DLC_EXP_Pack003_Base", JobHeader.CITADEL_BASE },
            { @"DLC_CON_APP01", JobHeader.APPEARANCE },
            { @"DLC_CON_GUN01", JobHeader.FIREFIGHT },
            { @"DLC_CON_GUN02", JobHeader.GROUNDSIDE },
            { @"DLC_OnlinePassHidCE", JobHeader.COLLECTORS_EDITION },
            { @"DLC_CON_DH1", JobHeader.GENESIS2 }
        };

        /// <summary>
        /// Maps in-game relative paths to the file that will be used to install to that location. The key is the target, the value is the source file that will be used.
        /// </summary>
        public Dictionary<string, string> FilesToInstall = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// List of files that will be removed from the game relative to this job's header directory.
        /// </summary>
        List<string> FilesToRemove = new List<string>();
        /// <summary>
        /// CUSTOMDLC folder mapping. The key is the source (mod folder), the value is the destination (dlc directory in game)
        /// </summary>
        public Dictionary<string, string> CustomDLCFolderMapping = new Dictionary<string, string>();

        /// <summary>
        /// List of ME1-only supported headers. BASEGAME, BRING_DOWN_THE_SKY, and PINNACLE_STATION.
        /// </summary>
        internal static readonly JobHeader[] ME1SupportedNonCustomDLCJobHeaders =
        {
            JobHeader.BASEGAME,
            JobHeader.BRING_DOWN_THE_SKY,
            JobHeader.PINNACLE_STATION
        };

        /// <summary>
        /// List of ME2-only supported headers such as ARRIVAL or LAIR_OF_THE_SHADOW_BROKER. Does not include CUSTOMDLC or BALANCE_CHANGES, does include BASEGAME
        /// </summary>
        internal static readonly JobHeader[] ME2SupportedNonCustomDLCJobHeaders =
        {
            JobHeader.BASEGAME,
            JobHeader.AEGIS_PACK,
            JobHeader.APPEARANCE_PACK_1,
            JobHeader.APPEARANCE_PACK_2,
            JobHeader.ARC_PROJECTOR,
            JobHeader.ARRIVAL,
            JobHeader.BLOOD_DRAGON_ARMOR,
            JobHeader.CERBERUS_WEAPON_ARMOR,
            JobHeader.COLLECTORS_WEAPON_ARMOR,
            JobHeader.EQUALIZER_PACK,
            JobHeader.FIREPOWER_PACK,
            JobHeader.FIREWALKER,
            JobHeader.GENESIS,
            JobHeader.INCISOR,
            JobHeader.INFERNO_ARMOR,
            JobHeader.KASUMI,
            JobHeader.LAIR_OF_THE_SHADOW_BROKER,
            JobHeader.NORMANDY_CRASH_SITE,
            JobHeader.OVERLORD,
            JobHeader.RECON_HOOD,
            JobHeader.SENTRY_INTERFACE,
            JobHeader.TERMINUS_WEAPON_ARMOR,
            JobHeader.UMBRA_VISOR,
            JobHeader.ZAEED
        };

        /// <summary>
        /// List of ME3-only supported headers such as CITADEL or RESURGENCE. Does not include CUSTOMDLC or BALANCE_CHANGES, does include BASEGAME
        /// </summary>
        internal static readonly JobHeader[] ME3SupportedNonCustomDLCJobHeaders =
        {
            JobHeader.BASEGAME,
            //JobHeader.BALANCE_CHANGES, //Must be parsed separately
            JobHeader.RESURGENCE,
            JobHeader.REBELLION,
            JobHeader.EARTH,
            JobHeader.RETALIATION,
            JobHeader.RECKONING,
            JobHeader.PATCH1,
            JobHeader.PATCH2,
            JobHeader.FROM_ASHES,
            JobHeader.EXTENDED_CUT,
            JobHeader.LEVIATHAN,
            JobHeader.OMEGA,
            JobHeader.CITADEL,
            JobHeader.CITADEL_BASE,
            JobHeader.APPEARANCE,
            JobHeader.FIREFIGHT,
            JobHeader.GROUNDSIDE,
            JobHeader.GENESIS2,
            JobHeader.COLLECTORS_EDITION,
            JobHeader.TESTPATCH
        };


        /// <summary>
        /// ModDesc.ini Header that this mod job targets
        /// </summary>
        public JobHeader Header { get; private set; }
        /// <summary>
        /// Subdirectory of the parent mod object that this job will pull files from. Note this is a relative path and not a full path.
        /// </summary>
        public string JobDirectory { get; internal set; }
        /// <summary>
        /// MultiLists are tied to multilist[x] descriptors. These are esssentially an array variable you can reference in moddesc.
        /// </summary>
        public Dictionary<int, string[]> MultiLists { get; internal set; } = new Dictionary<int, string[]>();

        /// <summary>
        /// Creates a new ModJob for the specific header.
        /// </summary>
        /// <param name="jobHeader">Header this job is for</param>
        /// <param name="mod">Mod object this job is for. This object is not saved and is only used to pull the path in and other necessary variables.</param>
        public ModJob(JobHeader jobHeader, Mod mod = null)
        {
            this.Header = jobHeader;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will replace an existing file in the mapping if the destination path is the same.
        /// </summary>
        /// <param name="destRelativePath">Relative in-game path (from game root) to install file to.</param>
        /// <param name="sourceRelativePath">Relative (to mod root) path of new file to install</param>
        /// <param name="ignoreLoadErrors">Ignore checking if new file exists on disk</param>
        /// <param name="mod">Mod to parse against</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddFileToInstall(string destRelativePath, string sourceRelativePath, Mod mod)
        {
            //Security check
            if (!checkExtension(sourceRelativePath, out string failReason))
            {
                return failReason;
            }
            string checkingSourceFile;
            if (JobDirectory != null)
            {
                checkingSourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, JobDirectory, sourceRelativePath);
            }
            else
            {
                //root (legacy)
                checkingSourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, sourceRelativePath);
            }
            if (!FilesystemInterposer.FileExists(checkingSourceFile, mod.Archive))
            {
                return M3L.GetString(M3L.string_interp_validation_modjob_replacementFileSpecifiedByJobDoesntExist, checkingSourceFile);
            }
            FilesToInstall[destRelativePath.Replace('/', '\\').TrimStart('\\')] = sourceRelativePath.Replace('/', '\\');
            return null;
        }

        private bool checkExtension(string sourceRelativePath, out string failReason)
        {
            var ext = Path.GetExtension(sourceRelativePath).ToLower();
            if (ext == @".exe")
            {
                failReason = M3L.GetString(M3L.string_validation_modjob_exeFilesNotAllowed);
                return false;
            }
            if (ext == @".dll")
            {
                failReason = M3L.GetString(M3L.string_validation_modjob_dllFilesNotAllowed);
                return false;
            }
            if (ext == @".asi")
            {
                failReason = M3L.GetString(M3L.string_validation_modjob_asiFilesNotAllowed);
                return false;
            }

            failReason = null;
            return true;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will replace an existing file in the mapping if the destination path is the same. This is for automapping.
        /// </summary>
        /// <param name="destRelativePath">Relative in-game path (from game root) to install file to.</param>
        /// <param name="sourcePath">Path to parsed file</param>
        /// <param name="mod">Mod to parse against</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddPreparsedFileToInstall(string destRelativePath, string sourcePath, Mod mod)
        {
            //string checkingSourceFile;
            //if (JobDirectory != null)
            //{
            //    checkingSourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, JobDirectory, sourceRelativePath);
            //}
            //else
            //{
            //    //root (legacy)
            //    checkingSourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, sourceRelativePath);
            //}
            //if (!ignoreLoadErrors && !FilesystemInterposer.FileExists(checkingSourceFile, mod.Archive))
            //{
            //    return M3L.GetString(M3L.string_interp_validation_modjob_replacementFileSpecifiedByJobDoesntExist, checkingSourceFile);
            //}
            //Security check
            if (!checkExtension(sourcePath, out string failReason))
            {
                return failReason;
            }
            FilesToInstall[destRelativePath.Replace('/', '\\').TrimStart('\\')] = sourcePath.Replace('/', '\\');
            return null;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will not replace an existing file in the mapping if the destination path is the same, it will instead throw an error.
        /// </summary>
        /// <param name="destRelativePath">Relative in-game path (from game root) to install file to.</param>
        /// <param name="sourceRelativePath">Relative (to mod root) path of new file to install</param>
        /// <param name="mod">Mod to parse against</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddAdditionalFileToInstall(string destRelativePath, string sourceRelativePath, Mod mod)
        {
            //Security check
            if (!checkExtension(sourceRelativePath, out string failReason))
            {
                return failReason;
            }
            var checkingSourceFile = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, JobDirectory, sourceRelativePath);
            if (!FilesystemInterposer.FileExists(checkingSourceFile, mod.Archive))
            {
                return M3L.GetString(M3L.string_interp_validation_modjob_additionalFileSpecifiedByJobDoesntExist, checkingSourceFile);
            }
            if (FilesToInstall.ContainsKey(destRelativePath))
            {
                return M3L.GetString(M3L.string_interp_validation_modjob_additionalFileAlreadyMarkedForModification, destRelativePath);
            }
            FilesToInstall[destRelativePath.Replace('/', '\\').TrimStart('\\')] = sourceRelativePath.Replace('/', '\\');
            return null;
        }


        private static IReadOnlyDictionary<JobHeader, string> ME1HeadersToDLCNamesMap = new Dictionary<JobHeader, string>()
        {
            [JobHeader.BRING_DOWN_THE_SKY] = @"DLC_UNC",
            [JobHeader.PINNACLE_STATION] = @"DLC_Vegas"
        };

        private static IReadOnlyDictionary<JobHeader, string> ME2HeadersToDLCNamesMap = new Dictionary<JobHeader, string>()
        {
            [JobHeader.AEGIS_PACK] = @"DLC_CER_02",
            [JobHeader.APPEARANCE_PACK_1] = @"DLC_CON_Pack01",
            [JobHeader.APPEARANCE_PACK_2] = @"DLC_CON_Pack02",
            [JobHeader.ARC_PROJECTOR] = @"DLC_CER_Arc",
            [JobHeader.ARRIVAL] = @"DLC_EXP_Part02",
            [JobHeader.BLOOD_DRAGON_ARMOR] = @"DLC_PRE_DA",
            [JobHeader.CERBERUS_WEAPON_ARMOR] = @"DLC_PRE_Cerberus",
            [JobHeader.COLLECTORS_WEAPON_ARMOR] = @"DLC_PRE_Collectors",
            [JobHeader.EQUALIZER_PACK] = @"DLC_MCR_03",
            [JobHeader.FIREPOWER_PACK] = @"DLC_MCR_01",
            [JobHeader.FIREWALKER] = @"DLC_UNC_Hammer01",
            [JobHeader.GENESIS] = @"DLC_DHME1",
            [JobHeader.INCISOR] = @"DLC_PRE_Incisor",
            [JobHeader.INFERNO_ARMOR] = @"DLC_PRE_General",
            [JobHeader.KASUMI] = @"DLC_HEN_MT",
            [JobHeader.LAIR_OF_THE_SHADOW_BROKER] = @"DLC_EXP_Part01",
            [JobHeader.NORMANDY_CRASH_SITE] = @"DLC_UNC_Moment01",
            [JobHeader.OVERLORD] = @"DLC_UNC_Pack01",
            [JobHeader.RECON_HOOD] = @"DLC_PRO_Pepper02",
            [JobHeader.SENTRY_INTERFACE] = @"DLC_PRO_Gulp01",
            [JobHeader.TERMINUS_WEAPON_ARMOR] = @"DLC_PRE_Gamestop",
            [JobHeader.UMBRA_VISOR] = @"DLC_PRO_Pepper01",
            [JobHeader.ZAEED] = @"DLC_HEN_VT"
        };

        private static IReadOnlyDictionary<JobHeader, string> ME3HeadersToDLCNamesMap = new Dictionary<JobHeader, string>()
        {
            [JobHeader.COLLECTORS_EDITION] = @"DLC_OnlinePassHidCE",
            [JobHeader.RESURGENCE] = @"DLC_CON_MP1",
            [JobHeader.REBELLION] = @"DLC_CON_MP2",
            [JobHeader.EARTH] = @"DLC_CON_MP3",
            [JobHeader.RETALIATION] = @"DLC_CON_MP4",
            [JobHeader.RECKONING] = @"DLC_CON_MP5",
            [JobHeader.PATCH1] = @"DLC_UPD_Patch01",
            [JobHeader.PATCH2] = @"DLC_UPD_Patch02",
            [JobHeader.FROM_ASHES] = @"DLC_HEN_PR",
            [JobHeader.EXTENDED_CUT] = @"DLC_CON_END",
            [JobHeader.LEVIATHAN] = @"DLC_EXP_Pack001",
            [JobHeader.OMEGA] = @"DLC_EXP_Pack002",
            [JobHeader.CITADEL_BASE] = @"DLC_EXP_Pack003_Base",
            [JobHeader.CITADEL] = @"DLC_EXP_Pack003",
            [JobHeader.FIREFIGHT] = @"DLC_CON_GUN01",
            [JobHeader.GROUNDSIDE] = @"DLC_CON_GUN02",
            [JobHeader.APPEARANCE] = @"DLC_CON_APP01",
            [JobHeader.GENESIS2] = @"DLC_CON_DH1",
            [JobHeader.TESTPATCH] = @"DLC_TestPatch" //This is not actually a DLC folder. This is the internal path though that the DLC would use if it worked unpacked.
        };

        internal static IReadOnlyDictionary<JobHeader, string> GetHeadersToDLCNamesMap(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1HeadersToDLCNamesMap;
                case Mod.MEGame.ME2:
                    return ME2HeadersToDLCNamesMap;
                case Mod.MEGame.ME3:
                    return ME3HeadersToDLCNamesMap;
                default:
                    throw new Exception(@"Can't get supported list of headers for unknown game type.");
            }
        }
        public string RequirementText;
        public List<AlternateFile> AlternateFiles = new List<AlternateFile>();
        public List<AlternateDLC> AlternateDLCs = new List<AlternateDLC>();
        public List<string> ReadOnlyIndicators = new List<string>();

        internal static JobHeader[] GetSupportedNonCustomDLCHeaders(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1SupportedNonCustomDLCJobHeaders;
                case Mod.MEGame.ME2:
                    return ME2SupportedNonCustomDLCJobHeaders;
                case Mod.MEGame.ME3:
                    return ME3SupportedNonCustomDLCJobHeaders;
                default:
                    throw new Exception(@"Can't get supported list of headers for unknown game type.");
            }
        }

        internal string AddReadOnlyIndicatorForFile(string sourceRelativePath, Mod mod)
        {
            if (!FilesToInstall.Any(x => x.Value.Equals(sourceRelativePath, StringComparison.InvariantCultureIgnoreCase)))
            {
                return M3L.GetString(M3L.string_interp_validation_modjob_readOnlyTargetNotSpeficiedInAddFilesList, sourceRelativePath);
            }

            ReadOnlyIndicators.Add(sourceRelativePath);
            return null;
        }

        public bool ValidateAltFiles(out string failureReason)
        {
            var optionGroups = AlternateFiles.Select(x => x.GroupName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();

            foreach (var group in optionGroups)
            {
                var checkedByDefaultForGroups = AlternateFiles.Count(x => x.CheckedByDefault && x.GroupName == group);
                if (checkedByDefaultForGroups == 0)
                {
                    Log.Error($@"Alternate Files that use the OptionGroup feature must have at least one AlternateFile struct set in their group with the CheckedByDefault option, as at least one option must always be chosen. The failing option group name is '{group}'");
                    failureReason = M3L.GetString(M3L.string_interp_validation_modjob_optionGroupMustHaveAtLeastOneItemWithCheckedByDefault, group);
                    return false;
                }
                if (checkedByDefaultForGroups > 1)
                {
                    Log.Error($@"Alternate Files that use the OptionGroup feature may only have one AlternateFile struct set with the CheckedByDefault option within their group. The failing option group name is '{group}'");
                    failureReason = M3L.GetString(M3L.string_interp_validation_modjob_optionGroupMayOnlyHaveOneItemWithCheckedByDefault, group);
                    return false;
                }
            }

            failureReason = null;
            return true; //validated
        }
    }
}
