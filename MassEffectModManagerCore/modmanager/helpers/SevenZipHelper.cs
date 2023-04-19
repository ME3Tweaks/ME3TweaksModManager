using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.helpers
{
    internal class SevenZipHelper
    {
        /// <summary>
        /// Reopens a disposed seven zip archive if necessary
        /// </summary>
        /// <param name="ArchivePath"></param>
        /// <param name="Archive"></param>
        /// <returns></returns>
        public static bool ReopenSevenZipArchive(string ArchivePath, SevenZipExtractor Archive)
        {
            if (File.Exists(ArchivePath) && (Archive == null || Archive.IsDisposed()))
            {
                Debug.WriteLine(@"Re-opening file-based SVE archive");
                Archive = new SevenZipExtractor(ArchivePath); //load archive file for inspection
                return true;
            }
            else if (Archive != null && Archive.GetBackingStream() is SevenZip.ArchiveEmulationStreamProxy aesp && aesp.Source is MemoryStream ms)
            {
                var isExe = ArchivePath.EndsWith(@".exe", StringComparison.InvariantCultureIgnoreCase);
                Debug.WriteLine(@"Re-opening memory SVE archive");
                ms.Position = 0; // Ensure position is 0
                Archive = isExe ? new SevenZipExtractor(ms, InArchiveFormat.Nsis) : new SevenZipExtractor(ms);
                return true;
            }

            return false;
        }
    }
}
