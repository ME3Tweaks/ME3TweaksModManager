using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksModManager.modmanager.helpers;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    /// <summary>
    /// Contains helper methods for troubleshooting various things in M3
    /// </summary>
    internal static class AppDiagnostics
    {
        /// <summary>
        /// Clears Mod Manager's cached files
        /// </summary>
        public static void ClearCaches()
        {
            // Clear services cache
            MUtilities.DeleteFilesAndFoldersRecursively(M3Filesystem.GetME3TweaksServicesCache());
            
            // Clear cached mixins
            var mixinDir = Path.Combine(M3Filesystem.GetAppDataFolder(), @"Mixins");
            if (Directory.Exists(mixinDir))
            {
                MUtilities.DeleteFilesAndFoldersRecursively(mixinDir);
            }

            // ModMaker 'cache' is not actually used as a cache, it is meant for long-term servicing

            // Delete MEM executables
            MUtilities.DeleteFilesAndFoldersRecursively(MCoreFilesystem.GetMEMDir());

            // Delete cached ASIs
            MUtilities.DeleteFilesAndFoldersRecursively(ASIManager.CachedASIsFolder);

        }
    }
}
