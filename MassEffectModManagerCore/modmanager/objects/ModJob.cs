using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IniParser.Model;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.merge;

namespace ME3TweaksModManager.modmanager.objects
{
    [DebuggerDisplay(@"ModJob for {Header}")]
    public class ModJob : IMDParameterMap
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
            ME2_RCWMOD, //RCW Mod Manager coalesced mods

            //ME3 ONLY
            BALANCE_CHANGES, //For replacing the balance changes file
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
            TESTPATCH,

            LOCALIZATION, //For ME2/3 Localization TLKs

            // LEGENDARY
            LELAUNCHER,

            // GAME 1
            GAME1_EMBEDDED_TLK // Embedded TLK files for merge
        }

        /// <summary>
        /// RCWMod file. ONLY USED WITH RCWMOD HEADER.
        /// </summary>
        public RCWMod RCW { get; set; }
        /// <summary>
        /// List of Merge Mods this job can install. Only apply to BASEGAME job
        /// </summary>
        public List<IMergeMod> MergeMods { get; set; } = new();

        public static IReadOnlyDictionary<string, JobHeader> ME3OfficialDLCFolderToHeaderMapping = new Dictionary<string, JobHeader>
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
        /// CUSTOMDLC folder mapping. The key is the source (mod folder), the value is the destination (dlc directory in game). This is only used for always installed folders, not alternates
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
        /// List of LE games-only supported headers. These games always have all the official DLC and as such do not support anything but BASEGAME
        /// </summary>
        internal static readonly JobHeader[] LESupportedNonCustomDLCJobHeaders =
        {
            JobHeader.BASEGAME,
        };

        /// <summary>
        /// LELAUNCHER only supports LELAUNCHER header
        /// </summary>
        internal static readonly JobHeader[] LELauncherSupportedNonCustomDLCJobHeaders =
        {
            JobHeader.LELAUNCHER,
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
        /// MultiLists are tied to multilist[x] descriptors. These are essentially an array variable you can reference in moddesc.
        /// </summary>
        public Dictionary<int, string[]> MultiLists { get; internal set; } = new Dictionary<int, string[]>();

        /// <summary>
        /// Creates a new ModJob for the specific header.
        /// </summary>
        /// <param name="jobHeader">Header this job is for</param>
        /// <param name="mod">Mod object this job is for. This object is not saved and is only used to pull the path in and other necessary variables.</param>
        public ModJob(JobHeader jobHeader, Mod mod = null)
        {
            Header = jobHeader;
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
            // Security check
            if (!checkExtension(sourceRelativePath, out string failReason))
            {
                return failReason;
            }

            if (destRelativePath.Contains(@".."))
            {
                return M3L.GetString(M3L.string_interp_validation_modjob_cannotContainDotDot, destRelativePath);
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

            var calculatedDest = destRelativePath.Replace('/', '\\').TrimStart('\\').TrimStart('.'); //.TrimStart(.) added 6/11/2021 for security check
            calculatedDest = calculatedDest.Replace(@"\.\", @"\"); // Security filtering
            FilesToInstall[calculatedDest] = sourceRelativePath.Replace('/', '\\');
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

            // Add .bat, .cmd files?

            failReason = null;
            return true;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will replace an existing file in the mapping if the destination path is the same. This is for automapping. This does not check the security!
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

            var calculatedDest = destRelativePath.Replace('/', '\\').TrimStart('\\').TrimStart('.'); //.TrimStart(.) added 6/11/2021 for security check
            calculatedDest = calculatedDest.Replace(@"\.\", @"\"); // Security filtering
            FilesToInstall[calculatedDest] = sourcePath.Replace('/', '\\');
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


        private readonly static IReadOnlyDictionary<JobHeader, string> ME1HeadersToDLCNamesMap = new Dictionary<JobHeader, string>
        {
            [JobHeader.BRING_DOWN_THE_SKY] = @"DLC_UNC",
            [JobHeader.PINNACLE_STATION] = @"DLC_Vegas"
        };

        private readonly static IReadOnlyDictionary<JobHeader, string> ME2HeadersToDLCNamesMap = new Dictionary<JobHeader, string>
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

        private readonly static IReadOnlyDictionary<JobHeader, string> ME3HeadersToDLCNamesMap = new Dictionary<JobHeader, string>
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

        /// <summary>
        /// There are no supported headers since all DLC is assumed to be installed already
        /// </summary>
        private readonly static IReadOnlyDictionary<JobHeader, string> LEHeadersToDLCNamesMap = new Dictionary<JobHeader, string> { };

        internal static IReadOnlyDictionary<JobHeader, string> GetHeadersToDLCNamesMap(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1HeadersToDLCNamesMap;
                case MEGame.ME2:
                    return ME2HeadersToDLCNamesMap;
                case MEGame.ME3:
                    return ME3HeadersToDLCNamesMap;
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                case MEGame.LELauncher:
                    return LEHeadersToDLCNamesMap;
                default:
                    throw new Exception(@"Can't get supported list of headers for unknown game type.");
            }
        }
        /// <summary>
        /// String to display to the user when this job cannot be applied due to missing vanilla DLC for the specified header.
        /// </summary>
        public string RequirementText { get; set; }
        /// <summary>
        /// List of alternate file objects for this header
        /// </summary>
        public ObservableCollection<AlternateFile> AlternateFiles { get; } = new ObservableCollection<AlternateFile>();
        /// <summary>
        /// List of Alternate dlc objects for this header. This is only supported on the CUSTOMDLC header.
        /// </summary>
        public ObservableCollection<AlternateDLC> AlternateDLCs { get; } = new ObservableCollection<AlternateDLC>();
        /// <summary>
        /// List of files that should be set to read-only on install. Used for exec files for EGM
        /// </summary>
        public List<string> ReadOnlyIndicators = new List<string>();

        /// <summary>
        /// List of xml files in the Game1Tlk job directory. Ensure you check for null before accessing this variable.
        /// </summary>
        public List<string> Game1TLKXmls;

        /// <summary>
        /// Gets list of headers that does not include CUSTOMDLC. Includes BASEGAME.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static JobHeader[] GetSupportedNonCustomDLCHeaders(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1SupportedNonCustomDLCJobHeaders;
                case MEGame.ME2:
                    return ME2SupportedNonCustomDLCJobHeaders;
                case MEGame.ME3:
                    return ME3SupportedNonCustomDLCJobHeaders;
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                    return LESupportedNonCustomDLCJobHeaders;
                case MEGame.LELauncher:
                    return LELauncherSupportedNonCustomDLCJobHeaders;
                default:
                    throw new Exception(@"Can't get supported list of headers for unknown game type.");
            }
        }

        /// <summary>
        /// Gets list of official DLC headers for the specified game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static JobHeader[] GetSupportedOfficialDLCHeaders(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1SupportedNonCustomDLCJobHeaders.Except(new[] { JobHeader.BASEGAME }).ToArray();
                case MEGame.ME2:
                    return ME2SupportedNonCustomDLCJobHeaders.Except(new[] { JobHeader.BASEGAME }).ToArray();
                case MEGame.ME3:
                    return ME3SupportedNonCustomDLCJobHeaders.Except(new[] { JobHeader.BASEGAME }).ToArray();
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                case MEGame.LELauncher:
                    return new JobHeader[] { }; // LE does not support any DLC headers
                default:
                    throw new Exception(@"Can't get supported list of dlc headers for unknown game type.");
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

        public bool ValidateAlternates(Mod modForValidating, out string failureReason)
        {
            // Validate option groups
            var optionGroups = AlternateFiles.Select(x => x.GroupName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            foreach (var group in optionGroups)
            {
                var checkedByDefaultForGroups = AlternateFiles.Count(x => x.CheckedByDefault && x.GroupName == group);
                if (checkedByDefaultForGroups == 0)
                {
                    M3Log.Error($@"Alternate Files that use the OptionGroup feature must have at least one AlternateFile struct set in their group with the CheckedByDefault option, as at least one option must always be chosen. The failing option group name is '{group}'");
                    failureReason = M3L.GetString(M3L.string_interp_validation_modjob_optionGroupMustHaveAtLeastOneItemWithCheckedByDefault, group);
                    return false;
                }
                if (checkedByDefaultForGroups > 1)
                {
                    M3Log.Error($@"Alternate Files that use the OptionGroup feature may only have one AlternateFile struct set with the CheckedByDefault option within their group. The failing option group name is '{group}'");
                    failureReason = M3L.GetString(M3L.string_interp_validation_modjob_optionGroupMayOnlyHaveOneItemWithCheckedByDefault, group);
                    return false;
                }
            }

            if (Header == JobHeader.CUSTOMDLC)
            {
                optionGroups = AlternateDLCs.Select(x => x.GroupName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

                foreach (var group in optionGroups)
                {
                    var checkedByDefaultForGroups = AlternateDLCs.Count(x => x.CheckedByDefault && x.GroupName == group);
                    if (checkedByDefaultForGroups == 0)
                    {
                        //needs localized
                        M3Log.Error($@"Alternate DLC that use the OptionGroup feature must have at least one AlternateDLC struct set in their group with the CheckedByDefault option, as at least one option must always be chosen. The failing option group name is '{group}'");
                        failureReason = M3L.GetString(M3L.string_interp_validation_modjob_altdlc_optionGroupMustHaveAtLeastOneItemWithCheckedByDefault, group);
                        return false;
                    }

                    if (checkedByDefaultForGroups > 1)
                    {
                        //needs localized
                        M3Log.Error($@"Alternate DLC that use the OptionGroup feature may only have one AlternateDLC struct set with the CheckedByDefault option within their group. The failing option group name is '{group}'");
                        failureReason = M3L.GetString(M3L.string_interp_validation_modjob_altdlc_optionGroupMayOnlyHaveOneItemWithCheckedByDefault, group);
                        return false;
                    }
                }
            }

            // Validate OptionKeys are all unique
            var optionKeys = AlternateFiles.Select(x => x.OptionKey).ToList();
            if (Header == JobHeader.CUSTOMDLC)
            {
                optionKeys.AddRange(AlternateDLCs.Select(x => x.OptionKey));
            }

            var duplicates = optionKeys.GroupBy(x => x).Where(g => g.Count() > 1).ToList();
            if (duplicates.Any())
            {
                // On Moddesc 8.0 and higher this will cause the mod to fail to load
                if (modForValidating.ModDescTargetVersion >= 8.0)
                {
                    M3Log.Error($@"There are alternates with duplicate OptionKey values. This is due to them either having a duplicate OptionKey values set on them, or different options have the same FriendlyName value. The following values have duplicates: {string.Join(',', duplicates)}");
                    failureReason = $"There are alternates with duplicate OptionKey values. This is due to them either having a duplicate OptionKey values set on them, or different options have the same FriendlyName value. The following values have duplicates: {string.Join(',', duplicates)}";
                    return false;
                }
                else
                {
                    // Moddesc 7.0 and below didn't have option keys, so there was no way to enforce unique option names.
                    M3Log.Warning($@"There are alternates with duplicate OptionKey values. This is due to them either having a duplicate OptionKey values set on them, or different options have the same FriendlyName value. The following values have duplicates: {string.Join(',', duplicates)}. This may result in broken saved alternate choices!");
                }
            }

            failureReason = null;
            return true; //validated
        }

        /// <summary>
        /// Serializes this job into the specified IniData object
        /// </summary>
        /// <param name="ini"></param>
        public void Serialize(IniData ini, Mod associatedMod)
        {
            var header = Header.ToString();
            if (!string.IsNullOrWhiteSpace(JobDirectory))
            {
                ini[header][@"moddir"] = JobDirectory;
            }

            if (Header == JobHeader.CUSTOMDLC)
            {
                if (CustomDLCFolderMapping.Any())
                {
                    ini[header][@"sourcedirs"] = string.Join(';', CustomDLCFolderMapping.Keys);
                    ini[header][@"destdirs"] = string.Join(';', CustomDLCFolderMapping.Values);
                }
            }
            else if (IsVanillaJob(this, associatedMod.Game))
            {
                foreach (var v in ParameterMap)
                {
                    if (!string.IsNullOrWhiteSpace(v.Value))
                    {
                        ini[header][v.Key] = v.Value;
                    }
                }
            }
            else if (Header == JobHeader.BALANCE_CHANGES)
            {

            }
            else if (Header == JobHeader.ME1_CONFIG)
            {

            }
            else if (Header == JobHeader.LOCALIZATION)
            {

            }
        }

        public static bool IsVanillaJob(ModJob job, MEGame game)
        {
            var officialHeaders = GetHeadersToDLCNamesMap(game);
            return officialHeaders.ContainsKey(job.Header) || job.Header == JobHeader.BASEGAME;
        }

        /// <summary>
        /// Gets a list of allowable and not-allowable directories that can be installed to by this job. Disallowed always overrides allowed and if an item is not listed in allowed it is implicitly not allowed
        /// </summary>
        /// <param name="job"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static SiloScopes GetScopedSilos(ModJob job, MEGame game)
        {
            switch (job.Header)
            {
                case JobHeader.LOCALIZATION:
                case JobHeader.ME1_CONFIG:
                case JobHeader.ME2_RCWMOD:
                case JobHeader.BALANCE_CHANGES:
                    return null; // There are no scopes for these headers.
            }

            SiloScopes scopes = new SiloScopes();
            var dlcDir = MEDirectories.GetDLCPath(game, "") + Path.DirectorySeparatorChar;

            if (job.Header == JobHeader.BASEGAME)
            {
                // There are specific directories we allow installation to.
                if (game == MEGame.ME3)
                {
                    scopes.DisallowedSilos.Add($@"Binaries{Path.DirectorySeparatorChar}Win32");
                }
                else if (game is MEGame.ME1 or MEGame.ME2)
                {
                    scopes.DisallowedSilos.Add($@"Binaries{Path.DirectorySeparatorChar}asi");
                }
                else if (game.IsLEGame())
                {
                    scopes.DisallowedSilos.Add($@"Binaries{Path.DirectorySeparatorChar}Win64"); //You are not allowed to install files into the game executable directory or subdirectories
                    scopes.DisallowedSilos.Add($@"BioGame{Path.DirectorySeparatorChar}Config"); // You are not allowed to overwrite ini files or anything in config
                    scopes.DisallowedFileSilos.Add($@"BioGame{Path.DirectorySeparatorChar}CookedPCConsole{Path.DirectorySeparatorChar}PlotManager.pcc"); // You must use PMU feature for this

                    if (game == MEGame.LE3)
                    {
                        scopes.DisallowedFileSilos.Add($@"BioGame{Path.DirectorySeparatorChar}CookedPCConsole{Path.DirectorySeparatorChar}Conditionals.cnd"); // You must use override feature
                    }
                }

                if (game == MEGame.LELauncher)
                {
                    scopes.DisallowedFileSilos.Add(""); // Root directory
                    scopes.DisallowedFileSilos.Add(@"."); // Root directory
                }

                if (game != MEGame.LELauncher)
                {
                    scopes.AllowedSilos.Add(@"Binaries" + Path.DirectorySeparatorChar); //Exec files
                    scopes.AllowedSilos.Add(@"BioGame" + Path.DirectorySeparatorChar); // Stuff in biogame
                }

                if (game is MEGame.ME1 or MEGame.ME2)
                {
                    scopes.AllowedSilos.Add(@"data" + Path.DirectorySeparatorChar); // Contains config tool
                }

                scopes.DisallowedSilos.Add(dlcDir); // BASEGAME is not allowed into DLC
                scopes.AllowedSilos.Add(@"Engine" + Path.DirectorySeparatorChar); //Shaders
            }
            else if (GetHeadersToDLCNamesMap(game).TryGetValue(job.Header, out var dlcFoldername))
            {
                // It's an official DLC
                var relativeDlcDir = Path.Combine(MEDirectories.GetDLCPath(game, ""), dlcFoldername) + Path.DirectorySeparatorChar;
                scopes.AllowedSilos.Add(relativeDlcDir); //Silos are folders. We should ensure they end with a slash
            }
            else if (job.Header == JobHeader.CUSTOMDLC)
            {
                // Have to get all resolved target folders. These are the scopes.AllowedSilos
                foreach (var cdlcn in job.CustomDLCFolderMapping.Values)
                {
                    scopes.AllowedSilos.Add(Path.Combine(dlcDir, cdlcn) + Path.DirectorySeparatorChar);
                }

                // Get alternate dlc targets
                foreach (var adlc in job.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC))
                {
                    scopes.AllowedSilos.Add(Path.Combine(dlcDir, adlc.DestinationDLCFolder) + Path.DirectorySeparatorChar);
                }
            }

            return scopes;
        }

        /// <summary>
        /// Pair of allowed silos and disallowed silos for installing mod files
        /// </summary>
        public class SiloScopes
        {
            /// <summary>
            /// Directories that can be installed to
            /// </summary>
            public List<string> AllowedSilos = new List<string>();
            /// <summary>
            /// Directories that cannot be installed to
            /// </summary>
            public List<string> DisallowedSilos = new List<string>();
            /// <summary>
            /// Specific file paths that can not be installed to
            /// </summary>
            public List<string> DisallowedFileSilos = new List<string>();
        }

        /// <summary>
        /// If this header is scoped to only allow installation to certain areas. Items that are not scoped are scoped in other ways in the mod parser, such as never reading a list of values or predefined target files
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public static bool IsJobScoped(ModJob job)
        {
            switch (job.Header)
            {
                case JobHeader.GAME1_EMBEDDED_TLK:
                case JobHeader.LOCALIZATION:
                case JobHeader.ME1_CONFIG:
                case JobHeader.ME2_RCWMOD:
                case JobHeader.BALANCE_CHANGES:
                    return false; // There are no scopes for these headers.
            }

            return true;
        }

        public bool IsOfficialDLCJob(MEGame game) => GetSupportedOfficialDLCHeaders(game).Contains(Header);

        #region Raw values for editor
        public string NewFilesRaw { get; set; }
        public string ReplaceFilesRaw { get; set; }
        public string AddFilesRaw { get; set; }
        public string AddFilesTargetsRaw { get; set; }
        public bool GameDirectoryStructureRaw { get; set; }
        public string LocalizationFilesStrRaw { get; set; }
        public string BalanceChangesFileRaw { get; set; }
        public string ConfigFilesRaw { get; set; }
        #endregion

        public void BuildParameterMap(Mod mod)
        {
            Dictionary<string, object> parameterDictionary = new Dictionary<string, object>();
            if (IsVanillaJob(this, mod.Game))
            {
                if (Header == JobHeader.BASEGAME && mod.Game == MEGame.ME3 && mod.LegacyModCoal)
                {
                    // moddesc 2 supported this flag. In MM3 it auto converted the
                    // meaning of this into basegame coalesced job. We 
                    // should convert it here since there are no raw values 
                    // cached into raw

                    parameterDictionary[@"moddir"] = @".";
                    parameterDictionary[@"newfiles"] = @"Coalesced.bin";
                    parameterDictionary[@"replacefiles"] = @"BIOGame\CookedPCConsole\Coalesced.bin";

                    // Technically this doesn't support more on this version of moddesc.
                    // But since we can't save older moddesc formats we will allow
                    // additional parameters and not show the modcoal flag in the UI.
                }
                else
                {
                    parameterDictionary[@"moddir"] = JobDirectory;
                    parameterDictionary[@"newfiles"] = NewFilesRaw;
                    parameterDictionary[@"replacefiles"] = ReplaceFilesRaw;
                }

                if (mod.Game == MEGame.ME3 || Header == JobHeader.BASEGAME)
                {
                    // Add files
                    parameterDictionary[@"addfiles"] = AddFilesRaw;
                    parameterDictionary[@"addfilestargets"] = AddFilesTargetsRaw;
                    parameterDictionary[@"addfilesreadonlytargets"] = ReadOnlyIndicators;
                }

                if (Header == JobHeader.BASEGAME)
                {
                    parameterDictionary[@"mergemods"] = string.Join(';', MergeMods.Select(x => x.MergeModFilename));
                }

                parameterDictionary[@"gamedirectorystructure"] = GameDirectoryStructureRaw ? @"True" : null;
                parameterDictionary[@"jobdescription"] = RequirementText;
            }
            else if (Header == JobHeader.CUSTOMDLC)
            {
                // These are serialized in special way by the editor
                // Do not put them into the parameter map
                //parameterDictionary[@"sourcedirs"] = CustomDLCFolderMapping.Keys;
                //parameterDictionary[@"destdirs"] = CustomDLCFolderMapping.Values;

                parameterDictionary[@"outdatedcustomdlc"] = mod.OutdatedCustomDLC;
                parameterDictionary[@"incompatiblecustomdlc"] = mod.IncompatibleDLC;
                // NOT MAPPED: HUMAN READABLE NAMES
                // CONFIGURED DIRECTLY BY EDITOR UI
            }
            else if (Header == JobHeader.LOCALIZATION)
            {
                parameterDictionary[@"files"] = FilesToInstall.Values;
                parameterDictionary[@"dlcname"] = mod.RequiredDLC.FirstOrDefault();
            }
            else if (Header == JobHeader.BALANCE_CHANGES)
            {
                parameterDictionary[@"moddir"] = JobDirectory;
                parameterDictionary[@"newfiles"] = FilesToInstall.Values;
            }
            else if (Header == JobHeader.ME1_CONFIG)
            {
                parameterDictionary[@"moddir"] = JobDirectory;
                // files raw is handled by ui
            }

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary, Header.ToString()));
        }

        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        /// <summary>
        /// Gets a list of all AlternateFiles and AlternateDLC objects attached to this job.
        /// </summary>
        /// <returns></returns>
        public List<AlternateOption> GetAllAlternates()
        {
            List<AlternateOption> options = new List<AlternateOption>();
            options.AddRange(AlternateFiles);
            if (Header == JobHeader.CUSTOMDLC)
                options.AddRange(AlternateDLCs);
            return options;
        }
    }
}
