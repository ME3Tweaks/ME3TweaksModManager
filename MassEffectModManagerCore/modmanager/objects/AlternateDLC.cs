using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay("AlternateDLC | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, DestDLC: {DestinationDLCFolder}, AltDLC: {AlternateDLCFolder}")]
    public class AlternateDLC : INotifyPropertyChanged
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
        public bool IsManual => Condition == AltDLCCondition.COND_MANUAL;
        public double UIOpacity => (!IsManual && !IsSelected) ? .5 : 1;
        public bool UIRequired => !IsManual && IsSelected;
        public bool UINotApplicable => !IsManual && !IsSelected;

        public string FriendlyName { get; private set; }
        public string Description { get; private set; }
        public List<string> ConditionalDLC = new List<string>();

        /// <summary>
        /// Alternate DLC folder to process the operation from (as the source)
        /// </summary>
        public string AlternateDLCFolder { get; private set; }
        /// <summary>
        /// In-mod path that the AlternateDLCFolder will apply to
        /// </summary>
        public string DestinationDLCFolder { get; private set; }

        public bool ValidAlternate;
        public string LoadFailedReason;

        public event PropertyChangedEventHandler PropertyChanged;

        public AlternateDLC(string alternateDLCText, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateDLCText);
            //todo: if statements to check these.
            if (properties.TryGetValue("FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }

            Enum.TryParse(properties["Condition"], out Condition);
            Enum.TryParse(properties["ModOperation"], out Operation);
            if (properties.TryGetValue("Description", out string description))
            {
                Description = description;
            }

            if (properties.TryGetValue("ModAltDLC", out string altDLCFolder))
            {
                AlternateDLCFolder = altDLCFolder.Replace('/', '\\');
            }
            else
            {
                Log.Error("Alternate DLC does not specify ModAltDLC but is required");
                ValidAlternate = false;
                LoadFailedReason = $"Alternate DLC {FriendlyName} does not declare ModAltDLC but it is required for all Alternate DLC.";
                return;
            }

            if (properties.TryGetValue("ModDestDLC", out string destDLCFolder))
            {
                DestinationDLCFolder = destDLCFolder.Replace('/', '\\');
            }
            else
            {
                Log.Error("Alternate DLC does not specify ModDestDLC but is required");
                ValidAlternate = false;
                LoadFailedReason = $"Alternate DLC {FriendlyName} does not declare ModDestDLC but it is required for all Alternate DLC.";
                return;
            }
            //todo: Validate target in mod folder

            if (properties.TryGetValue("ConditionalDLC", out string conditionalDlc))
            {
                var conditionalList = StringStructParser.GetSemicolonSplitList(conditionalDlc);
                foreach (var dlc in conditionalList)
                {
                    //if (modForValidating.Game == Mod.MEGame.ME3)
                    //{
                        if (Enum.TryParse(dlc, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                        {
                            ConditionalDLC.Add(foldername);
                            continue;
                        }
                    //}
                    if (!dlc.StartsWith("DLC_"))
                    {
                        Log.Error("An item in Alternate DLC's ConditionalDLC doesn't start with DLC_");
                        LoadFailedReason = $"Alternate DLC ({FriendlyName}) specifies conditional DLC but no values match the allowed headers or start with DLC_.";
                        return;
                    }
                    else
                    {
                        ConditionalDLC.Add(dlc);
                    }

                }
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

        public bool IsSelected { get; set; }
        public void SetupInitialSelection(GameTarget target)
        {
            IsSelected = false; //Reset
            if (Condition == AltDLCCondition.COND_MANUAL) return;
            var installedDLC = MEDirectories.GetInstalledDLC(target);
            switch (Condition)
            {
                case AltDLCCondition.COND_DLC_NOT_PRESENT:
                case AltDLCCondition.COND_ANY_DLC_NOT_PRESENT:
                    IsSelected = !ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_DLC_PRESENT:
                case AltDLCCondition.COND_ANY_DLC_PRESENT:
                    IsSelected = ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_ALL_DLC_NOT_PRESENT:
                    IsSelected = !ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_ALL_DLC_PRESENT:
                    IsSelected = ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
            }
        }
    }
}