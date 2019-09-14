using System;
using System.Collections.Generic;
using System.IO;

namespace MassEffectModManager.modmanager
{
    public class ModJob
    {
        public enum JobHeader
        {
            BASEGAME,
            CUSTOMDLC,

            //The following are ME3 only.
            BALANCE_CHANGES,
            COALESCED,
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

        public static IReadOnlyDictionary<string, JobHeader> ME3OfficialDLCFolderToHeaderMapping = new Dictionary<string, JobHeader>()
        {
            { "DLC_CON_MP1", JobHeader.RESURGENCE },
            { "DLC_CON_MP2", JobHeader.REBELLION },
            { "DLC_CON_MP3", JobHeader.EARTH },
            { "DLC_CON_MP4", JobHeader.RETALIATION },
            { "DLC_CON_MP5", JobHeader.RECKONING },
            { "DLC_UPD_Patch01", JobHeader.PATCH1 },
            { "DLC_UPD_Patch02", JobHeader.PATCH2 },
            //{ "DLC_TestPatch", JobHeader.TESTPATCH }, //This is effectively not used.
            { "DLC_HEN_PR", JobHeader.FROM_ASHES },
            { "DLC_CON_END", JobHeader.EXTENDED_CUT },
            { "DLC_EXP_Pack001", JobHeader.LEVIATHAN },
            { "DLC_EXP_Pack002", JobHeader.OMEGA },
            { "DLC_EXP_Pack003", JobHeader.CITADEL },
            { "DLC_EXP_Pack003_Base", JobHeader.CITADEL_BASE },
            { "DLC_CON_APP01", JobHeader.APPEARANCE },
            { "DLC_CON_GUN01", JobHeader.FIREFIGHT },
            { "DLC_CON_GUN02", JobHeader.GROUNDSIDE },
            { "DLC_OnlinePassHidCE", JobHeader.COLLECTORS_EDITION },
            { "DLC_CON_DH1", JobHeader.GENESIS2 }
        };

        /// <summary>
        /// Maps in-game relative paths to the file that will be used to install to that location
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
        /// List of ME3-only supported headers such as CITADEL or RESURGENCE. Does not include CUSTOMDLC or BALANCE_CHANGES, does include BASEGAME (which will work for ME1/ME2)
        /// </summary>
        internal static readonly JobHeader[] SupportedNonCustomDLCJobHeaders =
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
        /// Internal path used for resolving where files are for this job.
        /// </summary>
        private readonly string jobParentPath;

        /// <summary>
        /// ModDesc.ini Header that this mod job targets
        /// </summary>
        public JobHeader Header { get; private set; }
        /// <summary>
        /// Subdirectory of the parent mod object that this job will pull files from. Note this is a relative path and not a full path.
        /// </summary>
        public string JobDirectory { get; internal set; }

        /// <summary>
        /// Creates a new ModJob for the specific header.
        /// </summary>
        /// <param name="jobHeader">Header this job is for</param>
        /// <param name="mod">Mod object this job is for. This object is not saved and is only used to pull the path in and other necessary variables.</param>
        public ModJob(JobHeader jobHeader, Mod mod = null)
        {
            this.Header = jobHeader;
            if (mod != null)
            {
                jobParentPath = mod.ModPath;
            }
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will replace an existing file in the mapping if the destination path is the same.
        /// </summary>
        /// <param name="destRelativePath">Relative in-game path (from game root) to install file to.</param>
        /// <param name="sourceFullPath">Full path of new file to install</param>
        /// <param name="ignoreLoadErrors">Ignore checking if new file exists on disk</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddFileToInstall(string destRelativePath, string sourceFullPath, bool ignoreLoadErrors)
        {
            if (!ignoreLoadErrors && !File.Exists(sourceFullPath))
            {
                return $"Failed to add file to mod job: {sourceFullPath} does not exist but is specified by the job";
            }
            FilesToInstall[destRelativePath] = sourceFullPath;
            return null;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will not replace an existing file in the mapping if the destination path is the same, it will instead throw an error.
        /// </summary>
        /// <param name="destRelativePath">Relative in-game path (from game root) to install file to.</param>
        /// <param name="sourceFullPath">Full path of new file to install</param>
        /// <param name="ignoreLoadErrors">Ignore checking if new file exists on disk</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddAdditionalFileToInstall(string destRelativePath, string sourceFullPath, bool ignoreLoadErrors)
        {
            if (!ignoreLoadErrors && !File.Exists(sourceFullPath))
            {
                return $"Failed to add file to mod job: {sourceFullPath} does not exist but is specified by the job";
            }
            if (FilesToInstall.ContainsKey(destRelativePath))
            {
                return $"Failed to add file to mod job: {destRelativePath} already is marked for modification. Files that are in the addfiles descriptor cannot overlap each other or replacement files.";
            }
            FilesToInstall[destRelativePath] = sourceFullPath;
            return null;
        }

        internal static IReadOnlyDictionary<JobHeader, string> HeadersToDLCNamesMap = new Dictionary<JobHeader, string>()
        {
            [JobHeader.COLLECTORS_EDITION] = "DLC_OnlinePassHidCE",
            [JobHeader.RESURGENCE] = "DLC_CON_MP1",
            [JobHeader.REBELLION] = "DLC_CON_MP2",
            [JobHeader.EARTH] = "DLC_CON_MP3",
            [JobHeader.RETALIATION] = "DLC_CON_MP4",
            [JobHeader.RECKONING] = "DLC_CON_MP5",
            [JobHeader.PATCH1] = "DLC_UPD_Patch01",
            [JobHeader.PATCH2] = "DLC_UPD_Patch02",
            [JobHeader.FROM_ASHES] = "DLC_HEN_PR",
            [JobHeader.EXTENDED_CUT] = "DLC_CON_END",
            [JobHeader.LEVIATHAN] = "DLC_EXP_Pack001",
            [JobHeader.OMEGA] = "DLC_EXP_Pack002",
            [JobHeader.CITADEL_BASE] = "DLC_EXP_Pack003_Base",
            [JobHeader.CITADEL] = "DLC_EXP_Pack003",
            [JobHeader.FIREFIGHT] = "DLC_CON_GUN01",
            [JobHeader.GROUNDSIDE] = "DLC_CON_GUN02",
            [JobHeader.APPEARANCE] = "DLC_CON_APP01",
            [JobHeader.GENESIS2] = "DLC_CON_DH1",
            [JobHeader.TESTPATCH] = "DLC_TestPatch" //This is not actually a DLC folder. This is the internal path though that the DLC would use if it worked unpacked.
        };
        public string RequirementText;

        /// <summary>
        /// Adds a file to the removal sequence. Checks to make sure the installation lists don't include any files that are added.
        /// </summary>
        /// <param name="filesToRemove">List of files to remove (in-game relative path)</param>
        /// <returns>Failure string if any, null otherwise</returns>
        internal string AddFilesToRemove(List<string> filesToRemove)
        {
            foreach (var f in filesToRemove)
            {
                if (FilesToInstall.ContainsKey(f))
                {
                    return $"Failed to file removal task mod job {Header}: {f} is marked for installation. A mod cannot specify both installation and removal of the same file.";
                }
                FilesToRemove.Add(f);
            }
            return null;
        }
    }
}