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
        /// <param name="archivePath"></param>
        /// <param name="archive"></param>
        /// <returns></returns>
        public static bool ReopenSevenZipArchive(string archivePath, ref SevenZipExtractor archive)
        {
            if (File.Exists(archivePath) && (archive == null || archive.IsDisposed()))
            {
                Debug.WriteLine(@"Re-opening file-based SVE archive");
                archive = new SevenZipExtractor(archivePath); //load archive file for inspection
                return true;
            }
            else if (archive != null && archive.GetBackingStream() is SevenZip.ArchiveEmulationStreamProxy aesp && aesp.Source is MemoryStream ms)
            {
                var isExe = archivePath.EndsWith(@".exe", StringComparison.InvariantCultureIgnoreCase);
                Debug.WriteLine(@"Re-opening memory SVE archive");
                ms.Position = 0; // Ensure position is 0
                archive = isExe ? new SevenZipExtractor(ms, InArchiveFormat.Nsis) : new SevenZipExtractor(ms);
                return true;
            }

            return false;
        }
    }
}
