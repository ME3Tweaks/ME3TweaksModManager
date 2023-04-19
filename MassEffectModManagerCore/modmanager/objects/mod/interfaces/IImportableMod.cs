using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SevenZip.EventArguments;

namespace ME3TweaksModManager.modmanager.objects.mod.interfaces
{
    public interface IImportableMod
    {
        bool SelectedForImport { get; set; }
        string ModName { get; set; }
        bool ValidMod { get; }
        long SizeRequiredtoExtract { get; set; }
        void ExtractFromArchive(string archiveFilePath, string sanitizedPath, bool compressPackages, Action<string> textUpdateCallback, Action<DetailedProgressEventArgs> extractionProgressCallback, Action<string, int, int> compressedPackageCallback, bool b, Stream archiveStream);
    }
}
