using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay(@"AlternateFile | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
    public class AlternateFile : INotifyPropertyChanged
    {
        public enum AltFileOperation
        {
            INVALID_OPERATION,
            OP_SUBSTITUTE,
            OP_NOINSTALL,
            OP_INSTALL,
            OP_NOTHING //Used for alt groups
        }

        public enum AltFileCondition
        {
            INVALID_CONDITION,
            COND_MANUAL,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }

        public AltFileCondition Condition;
        public AltFileOperation Operation;

        public bool CheckedByDefault { get; }
        public bool IsManual => Condition == AltFileCondition.COND_MANUAL;
        public double UIOpacity => (!IsManual && !IsSelected) ? .5 : 1;
        public bool UIRequired => !IsManual && IsSelected;
        public bool UINotApplicable => !IsManual && !IsSelected;

        public string GroupName { get; }
        public string FriendlyName { get; private set; }
        public string Description { get; private set; }
        public List<string> ConditionalDLC = new List<string>();

        /// <summary>
        /// Alternate file to use, if the operation uses an alternate file
        /// </summary>
        public string AltFile { get; private set; }
        /// <summary>
        /// In-game relative path that will be operated on according to the specified operation
        /// </summary>
        public string ModFile { get; private set; }

        /// <summary>
        /// BACKWARDS COMPATIBLILITY ONLY: ModDesc 4.5 used SubstituteFile but was removed from support in 5.0
        /// </summary>
        public string SubstituteFile;


        //public const string OPERATION_SUBSTITUTE = "OP_SUBSTITUTE"; //swap a file in a job
        //public const string OPERATION_NOINSTALL = "OP_NOINSTALL"; //do not install a file
        //public const string OPERATION_INSTALL = "OP_INSTALL"; //install a file
        //public const string CONDITION_MANUAL = "COND_MANUAL"; //user must choose alt
        //public const string CONDITION_DLC_PRESENT = "COND_DLC_PRESENT"; //automatically choose alt if DLC listed is present
        //public const string CONDITION_DLC_NOT_PRESENT = "COND_DLC_NOT_PRESENT"; //automatically choose if DLC is not present
        public bool ValidAlternate;
        public string LoadFailedReason;
        public string MultiMappingFile;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ApplicableAutoText { get; }
        public string NotApplicableAutoText { get; }

        public AlternateFile(string alternateFileText, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            if (properties.TryGetValue(@"FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }
            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(FriendlyName))
            {
                //Cannot be null.
                Log.Error($@"Alternate File does not specify FriendlyName. Mods targeting moddesc >= 6.0 cannot have empty FriendlyName");
                ValidAlternate = false;
                LoadFailedReason = $"At least one specified Alternate File does not specify a FriendlyName, which is required for mods targeting cmmver >= 6.0.";
                return;
            }

            if (!Enum.TryParse(properties[@"Condition"], out Condition))
            {
                Log.Error($@"Alternate File specifies unknown/unsupported condition: {properties[@"Condition"]}");
                ValidAlternate = false;
                LoadFailedReason = "Alternate File specifies unknown/unsupported condition: " + properties[@"Condition"];
                return;
            }

            if (properties.TryGetValue(@"ConditionalDLC", out string conditionalDlc))
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
                        Log.Error(@"An item in Alternate Files's ConditionalDLC doesn't start with DLC_");
                        LoadFailedReason = $"Alternate File ({FriendlyName}) specifies conditional DLC but no values match the allowed headers or start with DLC_.";
                        return;
                    }
                    else
                    {
                        ConditionalDLC.Add(dlc);
                    }

                }
            }


            if (!Enum.TryParse(properties[@"ModOperation"], out Operation))
            {
                Log.Error(@"Alternate File specifies unknown/unsupported operation: " + properties[@"ModOperation"]);
                ValidAlternate = false;
                LoadFailedReason = "Alternate File specifies unknown/unsupported operation: " + properties[@"ModOperation"];
                return;
            }

            if (properties.TryGetValue(@"Description", out string description))
            {
                Description = description;
            }

            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(Description))
            {
                //Cannot be null.
                Log.Error($@"Alternate File {FriendlyName} with mod targeting moddesc >= 6.0 cannot have empty Description or missing description");
                ValidAlternate = false;
                LoadFailedReason = $"Alternate File  {FriendlyName} does not specify a Description, which is required for mods targeting cmmver >= 6.0.";
                return;
            }

            if (Operation != AltFileOperation.OP_NOTHING)
            {
                if (properties.TryGetValue(@"ModFile", out string modfile))
                {
                    ModFile = modfile.TrimStart('\\', '/');
                }
                else
                {
                    Log.Error($@"Alternate file in-mod target (ModFile) required but not specified. This value is required for all Alternate files. Friendlyname: {FriendlyName}");
                    ValidAlternate = false;
                    LoadFailedReason = $"Alternate file {FriendlyName} does not declare ModFile but it is required for all Alternate Files.";
                    return;
                }

                //todo: implement multimap
                if (properties.TryGetValue(@"MultiMappingFile", out string multifilemapping))
                {
                    MultiMappingFile = multifilemapping.TrimStart('\\', '/');
                }

                if (properties.TryGetValue(@"AltFile", out string altfile))
                {
                    AltFile = altfile;
                }
                else if (AltFile == null && properties.TryGetValue(@"ModAltFile", out string maltfile))
                {
                    AltFile = maltfile;
                }

                properties.TryGetValue(@"SubstituteFile", out SubstituteFile); //Only used in 4.5. In 5.0 and above this became AltFile.

                //workaround for 4.5
                if (modForValidating.ModDescTargetVersion == 4.5 && Operation == AltFileOperation.OP_SUBSTITUTE && SubstituteFile != null)
                {
                    AltFile = SubstituteFile;
                }

                if (!string.IsNullOrEmpty(AltFile))
                {
                    AltFile = AltFile.Replace('/', '\\'); //Standardize paths
                }

                //This needs reworked from java's hack implementation
                //Need to identify mods using substitution features

                if (Operation == AltFileOperation.OP_INSTALL || Operation == AltFileOperation.OP_SUBSTITUTE)
                {
                    if (MultiMappingFile == null)
                    {
                        //Validate file
                        var altPath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AltFile);
                        var altFileSourceExists = FilesystemInterposer.FileExists(altPath, modForValidating.Archive);
                        if (!altFileSourceExists)
                        {
                            Log.Error(@"Alternate file source (AltFile) does not exist: " + AltFile);
                            ValidAlternate = false;
                            LoadFailedReason = $"Alternate file is specified with operation {Operation}, but required file doesn't exist: {AltFile}";
                            return;
                        }

                        //Ensure it is not part of  DLC directory itself.
                        var modFile = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                        //Todo
                    }
                    else
                    {
                        //Multimapping, Todo
                    }
                }
            }

            ApplicableAutoText = properties.TryGetValue(@"ApplicableAutoText", out string applicableText) ? applicableText : "Auto Applied";

            NotApplicableAutoText = properties.TryGetValue(@"NotApplicableAutoText", out string notApplicableText) ? notApplicableText : "Not applicable";

            if (modForValidating.ModDescTargetVersion >= 6.0)
            {
                GroupName = properties.TryGetValue(@"OptionGroup", out string groupName) ? groupName : null;
            }


            if (Condition == AltFileCondition.COND_MANUAL && properties.TryGetValue(@"CheckedByDefault", out string checkedByDefault) && bool.TryParse(checkedByDefault, out bool cbd))
            {
                CheckedByDefault = cbd;
            }

            CLog.Information($@"Alternate file loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

        public bool IsSelected { get; set; }
        public void SetupInitialSelection(GameTarget target)
        {
            IsSelected = CheckedByDefault; //Reset
            if (Condition == AltFileCondition.COND_MANUAL)
            {
                IsSelected = CheckedByDefault;
                return;
            }
            if (Condition == AltFileCondition.COND_MANUAL) return;
            var installedDLC = MEDirectories.GetInstalledDLC(target);
            switch (Condition)
            {
                case AltFileCondition.COND_DLC_NOT_PRESENT:
                    IsSelected = !ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltFileCondition.COND_DLC_PRESENT:
                    IsSelected = ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                    //The following conditions don't exist right now
                    //case AltFileCondition.COND_ALL_DLC_NOT_PRESENT:
                    //    IsSelected = !ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
                    //case AltFileCondition.COND_ALL_DLC_PRESENT:
                    //    IsSelected = ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
            }
        }
    }
}