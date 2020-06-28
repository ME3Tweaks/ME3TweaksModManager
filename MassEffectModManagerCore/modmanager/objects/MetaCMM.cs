using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.ui;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Class that represents data in _metacmm.txt files - files that describe the installed mod
    /// </summary>
    public class MetaCMM
    {
        public static readonly string ControllerCompatMetaPrefix = @"[ME3CONTROLLERCOMPATLIST]";
        public string ModName { get; set; }
        public string Version { get; set; }
        public string InstalledBy { get; set; }
        public string InstallerInstanceGUID { get; set; }
        public ObservableCollectionExtended<string> ME3ControllerModCompatBuiltAgainst { get; } = new ObservableCollectionExtended<string>();

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
                        if (line.StartsWith(ControllerCompatMetaPrefix))
                        {
                            var parsedline = line.Substring(ControllerCompatMetaPrefix.Length);
                            ME3ControllerModCompatBuiltAgainst.ReplaceAll(StringStructParser.GetSemicolonSplitList(parsedline));
                        }
                        break;
                }
                i++;
            }



        }
    }
}
