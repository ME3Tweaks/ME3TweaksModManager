using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SevenZip.EventArguments;

namespace ME3TweaksModManager.modmanager.objects.mod.interfaces
{
    /// <summary>
    /// Generic interface for importing mods
    /// </summary>
    public interface IImportableMod
    {
        /// <summary>
        /// If this mod/file is selected for import
        /// </summary>
        bool SelectedForImport { get; set; }

        /// <summary>
        /// The name to display to the user for this mod/file
        /// </summary>
        string ModName { get; set; }

        /// <summary>
        /// If this mod/file is valid for importing and/or use
        /// </summary>
        bool ValidMod { get; }

        /// <summary>
        /// If this mod is currently stored in an archive or not
        /// </summary>
        bool IsInArchive { get; init; }

        /// <summary>
        /// The size required to extract this file to disk
        /// </summary>
        long SizeRequiredtoExtract { get; set; }

        /// <summary>
        /// Extracts this mod/file to disk. Many of these parameters may be unused in their implementations
        /// </summary>
        /// <param name="archiveFilePath"></param>
        /// <param name="sanitizedPath"></param>
        /// <param name="compressPackages"></param>
        /// <param name="textUpdateCallback"></param>
        /// <param name="extractionProgressCallback"></param>
        /// <param name="compressedPackageCallback"></param>
        /// <param name="testRun"></param>
        /// <param name="archiveStream"></param>
        /// <param name="sourceNXMLink">The NXM link that was used to download the file, if any, for FileSourceService</param>

        void ExtractFromArchive(string archiveFilePath, string sanitizedPath, bool compressPackages,
            Action<string> textUpdateCallback = null, Action<DetailedProgressEventArgs> extractionProgressCallback = null,
            Action<string, int, int> compressedPackageCallback = null, bool testRun = false, Stream archiveStream = null,
            NexusProtocolLink sourceNXMLink = null);
    }
}
