using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.helpers
{
    internal class M3Filesystem
    {
        public static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(MCoreFilesystem.GetME3TweaksServicesCache(), @"thirdpartyimportingservice.json");
        }
    }
}
