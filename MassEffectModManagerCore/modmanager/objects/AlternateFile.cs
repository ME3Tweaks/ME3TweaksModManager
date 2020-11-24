using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay(@"AlternateFile | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
    public class AlternateFile : AlternateOption
    {
        private static readonly string[] AllParameters =
        {
            @"Condition",
            @"ConditionalDLC",
            @"ModOperation",
            @"FriendlyName",
            @"ModFile",
            @"ModAltFile",
            @"Description",
            @"CheckedByDefault",
            @"OptionGroup",
            @"ApplicableAutoText",
            @"NotApplicableAutoText",
            @"MultiListId",
            @"MultiListRootPath",
            @"MultiListTargetPath",
            @"DLCRequirements"
        };

        public enum AltFileOperation
        {
            INVALID_OPERATION,
            OP_SUBSTITUTE,
            OP_NOINSTALL,
            OP_INSTALL,
            OP_APPLY_MULTILISTFILES,
            OP_NOINSTALL_MULTILISTFILES,
            OP_NOTHING //Used for alt groups
        }

        public enum AltFileCondition
        {
            INVALID_CONDITION,
            COND_MANUAL,
            COND_ALWAYS,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }

        public AltFileCondition Condition;
        public AltFileOperation Operation;

        public override bool IsManual => Condition == AltFileCondition.COND_MANUAL;
        public override bool IsAlways => Condition == AltFileCondition.COND_ALWAYS;
        public override bool UIRequired => !IsManual && IsSelected && !IsAlways;
        public override bool UINotApplicable => !IsManual && !IsSelected && !IsAlways;
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
            if (Operation == AltFileOperation.OP_NOINSTALL_MULTILISTFILES) return false;
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
        public string ApplicableAutoText { get; }
        public string NotApplicableAutoText { get; }
        public override bool UIIsSelectable
        {
            get => (!IsAlways && !UIRequired && !UINotApplicable) || IsManual;
            set { } //you can't set these for altfiles
        }


        public AlternateFile(string alternateFileText, ModJob associatedJob, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            buildParameterMap(properties);
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
                var condition = properties[@"Condition"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownCondition)} {condition}";
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
                var operation = properties[@"ModOperation"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownOperation)} {operation}";
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
                    if (associatedJob.Header == ModJob.JobHeader.CUSTOMDLC)
                    {
                        //This cannot be used on custom dlc
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES on the CUSTOM DLC task - this operation is not supported on this header. Use the altdlc version instead, see the moddesc.ini documentation.");
                        ValidAlternate = false;
                        LoadFailedReason = $@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES on the CUSTOM DLC task - this operation is not supported on this header. Use the altdlc version instead, see the moddesc.ini documentation.";
                        return;
                    }
                    if (properties.TryGetValue(@"MultiListRootPath", out var rootpath))
                    {
                        MultiListRootPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingMultiListRootPath, FriendlyName);
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
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingMultiListTargetPath, FriendlyName);
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
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingFileInMultiList, FriendlyName) + $@" multilist{multilistid}";
                            return;
                        }
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistIdNotIntegerOrMissing, FriendlyName);
                        return;
                    }
                }
                else if (Operation == AltFileOperation.OP_NOINSTALL_MULTILISTFILES)
                {
                    if (modForValidating.ModDescTargetVersion < 6.1)
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES, but this feature is only supported on moddesc version 6.1 or higher.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_opnoinstallmultilistfiles_requires_moddesc61, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var rootpath))
                    {
                        MultiListTargetPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistNIMissingMultiListTargetPath, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var targetpath))
                    {
                        MultiListTargetPath = targetpath.TrimStart('\\', '/').Replace('/', '\\');
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
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingFileInMultiList, FriendlyName) + $@" multilist{multilistid}";
                            return;
                        }
                    }
                    else
                    {
                        Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistIdNINotIntegerOrMissing, FriendlyName);
                        return;
                    }
                    // There's no way to verify files not being installed cause they can change at runtime
                    // Just have to trust developer on it
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
                                LoadFailedReason = @"Dummy placeholder";
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

                    //these both are the same these days i guess, I honestly can't remember which one I wanted to use
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

        /// <summary>
        /// Builds the editable parameter map for use in moddesc.ini editor
        /// </summary>
        /// <param name="properties"></param>
        private void buildParameterMap(Dictionary<string, string> properties)
        {
            var parms = properties.Select(x => new AlternateOption.Parameter() { Key = x.Key, Value = x.Value }).ToList();
            foreach (var v in AllParameters)
            {
                if (!parms.Any(x => x.Key == v))
                {
                    parms.Add(new Parameter(v, ""));
                }
            }
            ParameterMap.ReplaceAll(parms.OrderBy(x => x.Key));
        }

        public void SetupInitialSelection(GameTarget target)
        {
            IsSelected = CheckedByDefault; //Reset
            if (IsAlways)
            {
                IsSelected = true;
                return;
            }
            if (IsManual)
            {
                IsSelected = CheckedByDefault;
                return;
            }
            var installedDLC = M3Directories.GetInstalledDLC(target);
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
