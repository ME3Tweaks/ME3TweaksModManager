using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Utility class for M3-specific filesystem paths
    /// </summary>
    internal class M3Filesystem
    {
        public static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"thirdpartyimportingservice.json");
        }

        internal static string GetBlacklistingsCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"blacklistingservice.json");
        }

        public static string GetCachedLocalizationFolder()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), "livelocalization")).FullName;
        }

        /// <summary>
        /// Where downloaded mods are cached if they are larger than the in-memory size.
        /// </summary>
        /// <returns></returns>
        public static string GetModDownloadCacheDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetAppDataFolder(), "downloadedmodscache")).FullName;
        }

        internal static string GetNexusModsUpdateServiceCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"nexusmodsupdaterservice.json");
        }

        public static string GetKeybindsOverrideFolder()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetAppDataFolder(), "keybindsoverride")).FullName;
        }

        /// <summary>
        /// Service Cache file for tutorial service
        /// </summary>
        /// <returns></returns>
        internal static string GetTutorialServiceCacheFile()
        {
#if DEBUG
            // DEBUG ONLY!
            return @"C:\ProgramData\ME3TweaksModManager\ME3TweaksServicesCache\tutorialservice\new\tutorialservice.json";
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), "tutorialservice.json");
#elif PRERELEASE
            return Path.Combine(GetME3TweaksServicesCache(), "tutorialservice.json");
#else 
            You forgot to fix me bruh

#endif
        }

        /// <summary>
        /// Location where tutorial service images are stored.
        /// </summary>
        /// <returns></returns>
        public static string GetTutorialServiceCache()
        {
            return Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), "tutorialservice")).FullName;
        }

    }
}
