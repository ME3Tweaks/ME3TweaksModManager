using MassEffectModManager.modmanager.helpers;
using MassEffectModManagerCore.modmanager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.objects
{
    [DebuggerDisplay("AlternateDLC | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
    public class AlternateDLC
    {
        public enum AltDLCOperation
        {
            OP_ADD_CUSTOMDLC,
            OP_ADD_FOLDERFILES_TO_CUSTOMDLC
        }

        public enum AltDLCCondition
        {
            COND_MANUAL, //Must be manually selected by user
            COND_DLC_PRESENT, //Auto - Apply if conditional (single) dlc is present
            COND_DLC_NOT_PRESENT, //Auto - Apply if conditional (single) dlc is not present
            COND_ANY_DLC_NOT_PRESENT, //Auto - Apply if any conditional dlc is not present
            COND_ANY_DLC_PRESENT, //Auto - Apply if any conditional dlc is present
            COND_ALL_DLC_PRESENT, //Auto - Apply if all conditional dlc are present
            COND_ALL_DLC_NOT_PRESENT //Auto - Apply if none of the conditional dlc are present
        }

        public AltDLCCondition Condition;
        public AltDLCOperation Operation;
        public string FriendlyName;
        public string Description;
        public List<string> ConditionalDLC = new List<string>();

        /// <summary>
        /// Alternate DLC folder to process the operation from (as the source)
        /// </summary>
        public string AlternateDLCFolder;
        /// <summary>
        /// In-mod path that the AlternateDLCFolder will apply to
        /// </summary>
        public string DestinationDLCFolder;

        public bool ValidAlternate;
        public string LoadFailedReason;
        public AlternateDLC(string alternateDLCText, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateDLCText);
            //todo: if statements to check these.
            Enum.TryParse(properties["Condition"], out Condition);
            Enum.TryParse(properties["ModOperation"], out Operation);
            properties.TryGetValue("FriendlyName", out FriendlyName);
            properties.TryGetValue("Description", out Description);

            properties.TryGetValue("ModAltDLC", out AlternateDLCFolder);
            properties.TryGetValue("ModDestDLC", out DestinationDLCFolder);
            if (properties.TryGetValue("ConditionalDLC", out string conditionalDlc))
            {
                Debug.WriteLine(conditionalDlc);
            }

            //Validation
            if (string.IsNullOrWhiteSpace(AlternateDLCFolder))
            {
                Log.Error("Alternate DLC directory (ModAltDLC) not specified");
                LoadFailedReason = $"Alternate DLC for AltDLC ({FriendlyName}) is specified, but source directory (ModAltDLC) was not specified.";
                return;
            }

            if (string.IsNullOrWhiteSpace(DestinationDLCFolder))
            {
                Log.Error("Destination DLC directory (ModDestDLC) not specified");
                LoadFailedReason = $"Destination DLC for AltDLC ({FriendlyName}) is specified, but source directory (ModDestDLC) was not specified.";
                return;
            }

            AlternateDLCFolder = AlternateDLCFolder.TrimStart('\\', '/').Replace('/', '\\');

            //Check ModAltDLC directory exists
            var localAltDlcDir = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AlternateDLCFolder);
            if (!FilesystemInterposer.DirectoryExists(localAltDlcDir, modForValidating.Archive))
            {
                Log.Error("Alternate DLC directory (ModAltDLC) does not exist: " + AlternateDLCFolder);
                LoadFailedReason = $"Alternate DLC ({FriendlyName}) is specified, but source for alternate DLC directory does not exist: {AlternateDLCFolder}";
                return;
            }

            CLog.Information($"AlternateDLC loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

    }
}