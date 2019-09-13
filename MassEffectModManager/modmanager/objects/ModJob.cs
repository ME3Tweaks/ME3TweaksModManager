using System;
using System.Collections.Generic;
using System.IO;

namespace MassEffectModManager.modmanager
{
    internal class ModJob
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
        Dictionary<string, string> FilesToInstall = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        List<string> FilesToRemove = new List<string>();

        internal static readonly JobHeader[] SupportedNonCustomDLCJobHeaders =
        {
            //Basegame is supported by all games so it will be parsed separately
            //JobHeader.BASEGAME,
            JobHeader.BALANCE_CHANGES,
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
        public JobHeader jobHeader { get; private set; }

        public ModJob(JobHeader jobHeader)
        {
            this.jobHeader = jobHeader;
        }

        /// <summary>
        /// Adds a file to the add/replace list of files to install. This will replace an existing file in the mapping if the destination path is the same.
        /// </summary>
        /// <param name="relativePathToReplaceOrAdd">Relative in-game path (from game root) to install file to.</param>
        /// <param name="newFileToInstall">Full path of new file to install</param>
        /// <param name="ignoreLoadErrors">Ignore checking if new file exists on disk</param>
        /// <returns>string of failure reason. null if OK.</returns>
        internal string AddFileToInstall(string relativePathToReplaceOrAdd, string newFileToInstall, bool ignoreLoadErrors)
        {
            if (!ignoreLoadErrors && !File.Exists(newFileToInstall))
            {
                return "Failed to add file to mod job: {newFileToInstall} does not exist but is specified by the job";
            }
            FilesToInstall[@"BIOGame\CookedPCConsole\Coalesced.bin"] = newFileToInstall;
            return null;
        }
    }
}