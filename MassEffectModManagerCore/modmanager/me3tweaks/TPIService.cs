using System.Collections.Generic;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    public class TPIService
    {
        /// <summary>
        /// Maps a archive size to a list of importing informations about archives of that size.
        /// </summary>
        private static Dictionary<long, List<ThirdPartyImportingInfo>> Database;

        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// Looks up importing information for mods through the third party mod importing service. This returns all candidates, the client code must determine which is the appropriate value.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for information</param>
        /// <returns>List of candidates</returns>
        public static List<ThirdPartyImportingInfo> GetImportingInfosBySize(long archiveSize)
        {
            if (Database == null) return new List<ThirdPartyImportingInfo>(); //Not loaded
            if (Database.TryGetValue(archiveSize, out var result))
            {
                return result;
            }

            return new List<ThirdPartyImportingInfo>();
        }

        public static void LoadService(bool forceRefresh = false)
        {
            // todo: refresh
        }
    }
}
