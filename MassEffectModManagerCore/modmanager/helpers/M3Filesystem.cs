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
    }
}
