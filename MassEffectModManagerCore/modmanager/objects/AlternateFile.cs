using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
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
            OP_APPLY_MULTILISTFILES,
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
        public string MultiListRootPath { get; }
        public string[] MultiListSourceFiles { get; }
        /// <summary>
        /// In-game relative path that will be targeted as the root. It's like ModFile but more descriptive for multilist implementations.
        /// </summary>
        public string MultiListTargetPath { get; }

        internal bool HasRelativeFile()
        {
            if (Operation == AltFileOperation.INVALID_OPERATION) return false;
            if (Operation == AltFileOperation.OP_NOINSTALL) return false;
            if (Operation == AltFileOperation.OP_APPLY_MULTILISTFILES) return true;
            return AltFile != null;
        }

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

        public AlternateFile(string alternateFileText, ModJob associatedJob, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            if (properties.TryGetValue(@"FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }
            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(FriendlyName))
            {
                //Cannot be null.
                Log.Error(@"Alternate File does not specify FriendlyName. Mods targeting moddesc >= 6.0 cannot have empty FriendlyName");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_validation_altfile_oneAltDlcMissingFriendlyNameCmm6);
                return;
            }

            if (!Enum.TryParse(properties[@"Condition"], out Condition))
            {
                Log.Error($@"Alternate File specifies unknown/unsupported condition: {properties[@"Condition"]}"); //do not localize
                ValidAlternate = false;
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownCondition)} {properties[@"Condition"]}";
                return;
            }

            if (properties.TryGetValue(@"ConditionalDLC", out string conditionalDlc))
            {
                var conditionalList = StringStructParser.GetSemicolonSplitList(conditionalDlc);
                foreach (var dlc in conditionalList)
                {
                    if (Enum.TryParse(dlc, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                    {
                        ConditionalDLC.Add(foldername);
                        continue;
                    }
                    if (!dlc.StartsWith(@"DLC_"))
                    {
                        Log.Error(@"An item in Alternate Files's ConditionalDLC doesn't start with DLC_");
                        LoadFailedReason = M3L.GetString(M3L.string_validation_altfile_conditionalDLCInvalidValue, FriendlyName);
                        return;
                    }
                    else
                    {
                        ConditionalDLC.Add(dlc);
                    }
                }
            }

            //todo: ensure ModOperation is set before trying to parse it

            if (!Enum.TryParse(properties[@"ModOperation"], out Operation))
            {
                Log.Error(@"Alternate File specifies unknown/unsupported operation: " + properties[@"ModOperation"]);
                ValidAlternate = false;
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownOperation)} { properties[@"ModOperation"]}";
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
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_cmmver6RequiresDescription, FriendlyName);
                return;
            }

            if (Operation != AltFileOperation.OP_NOTHING)
            {
                int multilistid = -1;
                if (Operation == AltFileOperation.OP_APPLY_MULTILISTFILES)
                {
                    if (properties.TryGetValue(@"MultiListRootPath", out var rootpath))
                    {
                        MultiListRootPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = $"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListRootPath.";
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var targetpath))
                    {
                        MultiListTargetPath = targetpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListTargetPath.");
                        ValidAlternate = false;
                        LoadFailedReason = $"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListTargetPath.";
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListId", out string multilistidstr) && int.TryParse(multilistidstr, out multilistid))
                    {
                        if (associatedJob.MultiLists.TryGetValue(multilistid, out var ml))
                        {
                            MultiListSourceFiles = ml;
                        }
                        else
                        {
                            Log.Error($@"Alternate File ({FriendlyName}) Multilist ID does not exist as part of the task: multilist" + multilistid);
                            ValidAlternate = false;
                            var id = @"multilist" + multilistid;
                            LoadFailedReason = $"Alternate File ({FriendlyName}) Multilist ID does not exist as part of the task:" + $@" multilist{id}";
                            return;
                        }
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = $"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.";
                        return;
                    }
                }
                else
                {
                    if (properties.TryGetValue(@"ModFile", out string modfile))
                    {
                        ModFile = modfile.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate file in-mod target (ModFile) required but not specified. This value is required for all Alternate files except when using . Friendlyname: {FriendlyName}");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_noModFileDeclared, FriendlyName);
                        return;
                    }

                    if (associatedJob.Header == ModJob.JobHeader.CUSTOMDLC)
                    {
                        //Verify target folder is reachable by the mod
                        var modFilePath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                        var pathSplit = ModFile.Split('\\');
                        if (pathSplit.Length > 0)
                        {
                            var dlcName = pathSplit[0];
                            var jobKey = associatedJob.CustomDLCFolderMapping.FirstOrDefault(x => x.Value.Equals(dlcName, StringComparison.InvariantCultureIgnoreCase));
                            if (jobKey.Key != null)
                            {
                                //todo: Find DLC target to make sure this rule can actually be applied. Somewhat difficult logic here
                            }
                            else
                            {

                                Log.Error($@"Alternate file {FriendlyName} in-mod target (ModFile) does not appear to target a DLC target this mod will (always) install: {ModFile}");
                                ValidAlternate = false;
                                LoadFailedReason = "Dummy placeholder"; //Do not localize
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (!associatedJob.FilesToInstall.TryGetValue(ModFile, out var sourceFile))
                        {
                            Log.Error($@"Alternate file {FriendlyName} in-mod target (ModFile) specified but does not exist in job: {ModFile}");
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_couldNotFindModFile, FriendlyName, ModFile);
                            return;
                        }
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
                        //Validate file
                        var altPath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AltFile);
                        var altFileSourceExists = FilesystemInterposer.FileExists(altPath, modForValidating.Archive);
                        if (!altFileSourceExists)
                        {
                            Log.Error(@"Alternate file source (AltFile) does not exist: " + AltFile);
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_specifiedAltFileDoesntExist, Operation.ToString(), AltFile);
                            return;
                        }

                        //Ensure it is not part of  DLC directory itself.
                        var modFile = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                    }
                }
            }

            ApplicableAutoText = properties.TryGetValue(@"ApplicableAutoText", out string applicableText) ? applicableText : M3L.GetString(M3L.string_autoApplied);

            NotApplicableAutoText = properties.TryGetValue(@"NotApplicableAutoText", out string notApplicableText) ? notApplicableText : M3L.GetString(M3L.string_notApplicable);

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
