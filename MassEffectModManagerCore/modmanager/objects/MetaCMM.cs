using System.Linq;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Class that represents data in _metacmm.txt files - files that describe the installed mod
    /// </summary>
    public class MetaCMM
    {
        #region Info Prefixes
        public static readonly string PrefixOptionsSelectedOnInstall = @"[INSTALLOPTIONS]";
        public static readonly string PrefixIncompatibleDLC = @"[INCOMPATIBLEDLC]";
        #endregion

        public string ModName { get; set; }
        public string Version { get; set; }
        public string InstalledBy { get; set; }
        public string InstallerInstanceGUID { get; set; }
        public ObservableCollectionExtended<string> IncompatibleDLC { get; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<string> OptionsSelectedAtInstallTime { get; } = new ObservableCollectionExtended<string>();

        public MetaCMM(string metaFile)
        {
            var lines = Utilities.WriteSafeReadAllLines(metaFile).ToList();
            int i = 0;
            foreach (var line in lines)
            {
                switch (i)
                {
                    case 0:
                        ModName = line;
                        break;
                    case 1:
                        Version = line;
                        break;
                    case 2:
                        InstalledBy = line;
                        break;
                    case 3:
                        InstallerInstanceGUID = line;
                        break;
                    default:
                        // MetaCMM Extended
                        if (line.StartsWith(PrefixOptionsSelectedOnInstall))
                        {
                            var parsedline = line.Substring(PrefixOptionsSelectedOnInstall.Length);
                            OptionsSelectedAtInstallTime.ReplaceAll(StringStructParser.GetSemicolonSplitList(parsedline));
                        }
                        else if (line.StartsWith(PrefixIncompatibleDLC))
                        {
                            var parsedline = line.Substring(PrefixIncompatibleDLC.Length);
                            IncompatibleDLC.ReplaceAll(StringStructParser.GetSemicolonSplitList(parsedline));
                        }
                        break;
                }
                i++;
            }
        }
    }
}
