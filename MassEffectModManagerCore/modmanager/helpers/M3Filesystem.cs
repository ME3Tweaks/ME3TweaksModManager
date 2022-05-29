using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Utility class for M3-specific filesystem paths
    /// </summary>
    internal class M3Filesystem
    {
        internal static string GetAppDataFolder(bool createIfMissing = true)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"ME3TweaksModManager");
            if (createIfMissing && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        /// <summary>
        /// Builds 100-103 used a different name due to a compile bug
        /// </summary>
        /// <returns>null if pre-104 data folder is not found, the path otherwise</returns>
        internal static string GetPre104DataFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MassEffectModManager");
            if (Directory.Exists(folder))
            {
                return folder;
            }

            return null;
        }

        internal static string GetCachedTargetsFile(MEGame game)
        {
            return Path.Combine(GetAppDataFolder(), $@"GameTargets{game}.txt");
        }

        /// <summary>
        /// Gets the path where the specified static executable would be. This call does not check if that file exists.
        /// If no path is specified, it returns the cached executables directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCachedExecutablePath(string path = null)
        {
            var lpath = Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"executables")).FullName;
            if (path != null)
            {
                return Path.Combine(lpath, path);
            }
            return lpath;
        }

        /// <summary>
        /// Returns Temp/VPatchRedirects
        /// </summary>
        /// <returns></returns>
        internal static string GetVPatchRedirectsFolder()
        {
            return Path.Combine(GetTempPath(), @"VPatchRedirects");
        }

        internal static string GetDllDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"dlls")).FullName;
        }

        /// <summary>
        /// Gets the path for the nexus mods integration data folder. This is NOT where mods are stored during donwload.
        /// </summary>
        /// <returns></returns>
        internal static string GetNexusModsCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"nexusmodsintegration")).FullName;
        }

        internal static string GetExternalNexusHandlersFile()
        {
            return Path.Combine(GetNexusModsCache(), @"othernexushandlers.json");
        }

        internal static string GetBatchInstallGroupsFolder()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"batchmodqueues")).FullName;
        }

        internal static string GetME3TweaksServicesCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"ME3TweaksServicesCache")).FullName;
        }

        internal static string GetLocalHelpResourcesDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetME3TweaksServicesCache(), @"HelpResources")).FullName;
        }

        internal static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"thirdpartyimportingservice.json");
        }

        internal static string GetTipsServiceCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"tipsservice.json");
        }

        internal static string GetBlacklistingsCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"blacklistingservice.json");
        }

        public static string GetCachedLocalizationFolder()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"livelocalization")).FullName;
        }

        /// <summary>
        /// Where downloaded mods are cached if they are larger than the in-memory size.
        /// </summary>
        /// <returns></returns>
        public static string GetModDownloadCacheDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetAppDataFolder(), @"downloadedmodscache")).FullName;
        }

        internal static string GetNexusModsUpdateServiceCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"nexusmodsupdaterservice.json");
        }

        public static string GetKeybindsOverrideFolder()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetAppDataFolder(), @"keybindsoverride")).FullName;
        }

        /// <summary>
        /// Service Cache file for tutorial service
        /// </summary>
        /// <returns></returns>
        internal static string GetTutorialServiceCacheFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), @"tutorialservice.json");
        }

        /// <summary>
        /// Location where tutorial service images are stored.
        /// </summary>
        /// <returns></returns>
        public static string GetTutorialServiceCache()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"tutorialservice")).FullName;
        }

        internal static string GetDynamicHelpCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), @"cachedhelp-v2.xml");
        }

        /// <summary>
        /// Returns location where we will store the 7z.dll. Does not check for existence
        /// </summary>
        /// <returns></returns>
        internal static string Get7zDllPath()
        {
            return Path.Combine(GetDllDirectory(), @"7z.dll");
        }

        /// <summary>
        /// Gets scratch space directory for the application
        /// </summary>
        internal static string GetTempPath()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"Temp")).FullName;
        }

        /// <summary>
        /// Gets folder containing #.xml files (definition of modmaker mods)
        /// </summary>
        /// <returns></returns>
        internal static string GetModmakerDefinitionsCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetModMakerCache(), @"moddefinitions")).FullName;
        }

        /// <summary>
        /// Gets cache directory for modmaker files
        /// </summary>
        /// <returns></returns>
        private static string GetModMakerCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), @"ModMakerCache")).FullName;
        }

        public static string GetOriginOverlayDisableFile()
        {
            return Path.Combine(GetDllDirectory(), @"d3d9.dll");
        }

        internal static string GetUpdaterServiceUploadStagingPath()
        {
            return Directory.CreateDirectory(Path.Combine(GetTempPath(), @"UpdaterServiceStaging")).FullName;
        }
    }
}
