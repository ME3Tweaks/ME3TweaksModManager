using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Interposer for GameTarget -> MEDirectories, some convenience methods
    /// </summary>
    public static class M3Directories
    {
        #region INTERPOSERS
        public static string GetBioGamePath(GameTarget target) => MEDirectories.GetBioGamePath(target.Game, target.TargetPath);
        public static string GetDLCPath(GameTarget target) => MEDirectories.GetDLCPath(target.Game, target.TargetPath);
        public static string GetCookedPath(GameTarget target) => MEDirectories.GetCookedPath(target.Game, target.TargetPath);
        public static string GetExecutablePath(GameTarget target) => MEDirectories.GetExecutablePath(target.Game, target.TargetPath);
        public static string GetExecutableDirectory(GameTarget target) => MEDirectories.GetExecutableDirectory(target.Game, target.TargetPath);
        public static string GetLODConfigFile(GameTarget target) => MEDirectories.GetLODConfigFile(target.Game);
        public static string GetTextureMarkerPath(GameTarget target) => MEDirectories.GetTextureModMarkerPath(target.Game, target.TargetPath);
        public static string GetASIPath(GameTarget target) => MEDirectories.GetASIPath(target.Game, target.TargetPath);
        public static string GetTestPatchSFARPath(GameTarget target)
        {
            if (target.Game != MEGame.ME3) throw new Exception(@"Cannot fetch TestPatch SFAR for games that are not ME3");
            return ME3Directory.GetTestPatchSFARPath(target.TargetPath);
        }
        public static string GetCoalescedPath(GameTarget target)
        {
            if (target.Game != MEGame.ME2 && target.Game != MEGame.ME3) throw new Exception(@"Cannot fetch Coalesced path for games that are not ME2/ME3");
            if (target.Game == MEGame.ME2) return Path.Combine(GetBioGamePath(target), @"Config", @"PC", @"Cooked", "Coalesced.ini");
            return Path.Combine(GetCookedPath(target), @"Coalesced.bin");
        }
        public static bool IsInBasegame(string file, GameTarget target) => MEDirectories.IsInBasegame(file, target.Game, target.TargetPath);
        public static bool IsInOfficialDLC(string file, GameTarget target) => MEDirectories.IsInOfficialDLC(file, target.Game, target.TargetPath);
        internal static List<string> EnumerateGameFiles(GameTarget validationTarget)
        {
            return MEDirectories.EnumerateGameFiles(validationTarget.Game, validationTarget.TargetPath);
        }
        #endregion

        public static Dictionary<string, int> GetMountPriorities(GameTarget selectedTarget)
        {
            //make dictionary from basegame files
            var dlcmods = VanillaDatabaseService.GetInstalledDLCMods(selectedTarget);
            var mountMapping = new Dictionary<string, int>();
            foreach (var dlc in dlcmods)
            {
                var mountpath = Path.Combine(M3Directories.GetDLCPath(selectedTarget), dlc);
                try
                {
                    mountMapping[dlc] = MELoadedFiles.GetMountPriority(mountpath, selectedTarget.Game);
                }
                catch (Exception e)
                {
                    Log.Error($@"Exception getting mount priority from file: {mountpath}: {e.Message}");
                }
            }

            return mountMapping;
        }

        /// <summary>
        /// Gets a list of superceding package files from the DLC of the game. Only files in DLC mods are returned
        /// </summary>
        /// <param name="target">Target to get supercedances for</param>
        /// <returns>Dictionary mapping filename to list of DLCs that contain that file, in order of highest priority to lowest</returns>
        public static Dictionary<string, List<string>> GetFileSupercedances(GameTarget target)
        {
            //make dictionary from basegame files
            var fileListMapping = new CaseInsensitiveDictionary<List<string>>();
            var directories = MELoadedFiles.GetEnabledDLCFolders(target.Game, target.TargetPath).OrderBy(dir => MELoadedFiles.GetMountPriority(dir, target.Game));
            foreach (string directory in directories)
            {
                var dlc = Path.GetFileName(directory);
                if (MEDirectories.OfficialDLC(target.Game).Contains(dlc)) continue; //skip
                foreach (string filePath in MELoadedFiles.GetCookedFiles(target.Game, directory, false))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null && fileName.RepresentsPackageFilePath())
                    {
                        if (fileListMapping.TryGetValue(fileName, out var supercedingList))
                        {
                            supercedingList.Insert(0, dlc);
                        }
                        else
                        {
                            fileListMapping[fileName] = new List<string>(new[] { dlc });
                        }
                    }
                }
            }

            return fileListMapping;
        }

        // Todo: Move to GameTarget
        internal static List<string> GetInstalledDLC(GameTarget target, bool includeDisabled = false)
        {
            var dlcDirectory = GetDLCPath(target);
            if (Directory.Exists(dlcDirectory))
            {
                return Directory.GetDirectories(dlcDirectory).Where(x => Path.GetFileName(x).StartsWith(@"DLC_") || (includeDisabled && Path.GetFileName(x).StartsWith(@"xDLC_"))).Select(x => Path.GetFileName(x)).ToList();
            }

            return new List<string>();
        }

        internal static bool IsOfficialDLCInstalled(ModJob.JobHeader header, GameTarget gameTarget)
        {
            if (header == ModJob.JobHeader.BALANCE_CHANGES) return true; //Don't check balance changes
            if (header == ModJob.JobHeader.ME2_RCWMOD) return true; //Don't check
            if (header == ModJob.JobHeader.ME1_CONFIG) return true; //Don't check
            if (header == ModJob.JobHeader.BASEGAME) return true; //Don't check basegame
            if (header == ModJob.JobHeader.CUSTOMDLC) return true; //Don't check custom dlc
            if (header == ModJob.JobHeader.LOCALIZATION) return true; //Don't check localization

            if (header == ModJob.JobHeader.TESTPATCH)
            {
                return File.Exists(GetTestPatchSFARPath(gameTarget));
            }
            else
            {
                return M3Directories.GetInstalledDLC(gameTarget).Contains(ModJob.GetHeadersToDLCNamesMap(gameTarget.Game)[header]);
            }
        }

        /// <summary>
        /// Maps each DLC folder to it's MetaCMM file, if one exists. Otherwise it is mapped to null
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static Dictionary<string, MetaCMM> GetMetaMappedInstalledDLC(GameTarget target)
        {
            var installedDLC = GetInstalledDLC(target);
            var metamap = new Dictionary<string, MetaCMM>();
            var dlcpath = GetDLCPath(target);
            foreach (var v in installedDLC)
            {
                var meta = Path.Combine(dlcpath, v, @"_metacmm.txt");
                MetaCMM mf = null;
                if (File.Exists(meta))
                {
                    mf = new MetaCMM(meta);
                }

                metamap[v] = mf;
            }

            return metamap;
        }
    }
}
