using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MassEffectModManagerCore.modmanager.helpers;
using System.IO;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.objects.mod.editor;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay(@"AlternateDLC | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, DestDLC: {DestinationDLCFolder}, AltDLC: {AlternateDLCFolder}")]
    public sealed class AlternateDLC : AlternateOption
    {
        public enum AltDLCOperation
        {
            INVALID_OPERATION,
            OP_ADD_CUSTOMDLC,
            OP_ADD_FOLDERFILES_TO_CUSTOMDLC,
            OP_ADD_MULTILISTFILES_TO_CUSTOMDLC,
            OP_NOTHING
        }

        public enum AltDLCCondition
        {
            INVALID_CONDITION, //INVALID DEFAULT
            COND_MANUAL, //Must be manually selected by user
            COND_DLC_PRESENT, //Auto - Apply if conditional (single) dlc is present
            COND_DLC_NOT_PRESENT, //Auto - Apply if conditional (single) dlc is not present
            COND_ANY_DLC_NOT_PRESENT, //Auto - Apply if any conditional dlc is not present
            COND_ANY_DLC_PRESENT, //Auto - Apply if any conditional dlc is present
            COND_ALL_DLC_PRESENT, //Auto - Apply if all conditional dlc are present
            COND_ALL_DLC_NOT_PRESENT, //Auto - Apply if none of the conditional dlc are present
            COND_SPECIFIC_SIZED_FILES, //Auto - Apply if a specific file with a listed size is present
            COND_SPECIFIC_DLC_SETUP //Auto - Apply only if the specified DLC exists OR not exists, using +/-
        }

        public AltDLCCondition Condition;
        public AltDLCOperation Operation;

        /// <summary>
        /// Requirements for this manual option to be able to be picked
        /// </summary>
        public string[] DLCRequirementsForManual { get; }

        public override bool IsAlways => false; //AlternateDLC doesn't support this
        public List<string> ConditionalDLC = new List<string>();

        /// <summary>
        /// Alternate DLC folder to process the operation from (as the source)
        /// </summary>
        public string AlternateDLCFolder { get; private set; }

        /// <summary>
        /// In-mod path that the AlternateDLCFolder will apply to
        /// </summary>
        public string DestinationDLCFolder { get; private set; }

        /// <summary>
        /// Used by COND_SIZED_FILE_PRESENT
        /// </summary>
        public Dictionary<string, long> RequiredSpecificFiles { get; private set; } = new Dictionary<string, long>();

        /// <summary>
        /// ONLY FOR USE IN MODDESC.INI EDITOR
        /// Creates a new, blank Alternate DLC object
        /// </summary>
        /// <param name="alternateDLCFriendlyName"></param>
        public AlternateDLC(string alternateDLCFriendlyName)
        {
            FriendlyName = alternateDLCFriendlyName;
            BuildParameterMap(null); //Alternates don't need a mod, as nothing is game specific
        }

        public AlternateDLC(string alternateDLCText, mod.Mod modForValidating, ModJob job)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateDLCText);

            //todo: if statements to check these.
            if (properties.TryGetValue(@"FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }

            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(FriendlyName))
            {
                //Cannot be null.
                Log.Error(@"Alternate DLC does not specify FriendlyName. Mods targeting moddesc >= 6.0 require FriendlyName");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_validation_altdlc_oneAltDlcMissingFriendlyNameCmm6);
                return;
            }

            if (!Enum.TryParse(properties[@"Condition"], out Condition))
            {
                Log.Error($@"Alternate DLC specifies unknown/unsupported condition: {properties[@"Condition"]}"); //do not localize
                ValidAlternate = false;
                var condition = properties[@"Condition"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altdlc_unknownCondition)} {condition}";
                return;
            }

            if (!Enum.TryParse(properties[@"ModOperation"], out Operation))
            {
                Log.Error($@"Alternate DLC specifies unknown/unsupported operation: {properties[@"ModOperation"]}"); //do not localize
                ValidAlternate = false;
                var operation = properties[@"ModOperation"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altdlc_unknownOperation)} {operation}";
                return;
            }

            if (properties.TryGetValue(@"Description", out string description))
            {
                Description = description;
            }

            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(Description))
            {
                //Cannot be null.
                Log.Error($@"Alternate DLC {FriendlyName} cannot have empty Description or missing Description descriptor as it targets cmmver >= 6");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_cmmver6RequiresDescription, FriendlyName);
                return;
            }

            //OP_NOTHING can have conditions
            if (properties.TryGetValue(@"ConditionalDLC", out string conditionalDlc))
            {
                var conditionalList = StringStructParser.GetSemicolonSplitList(conditionalDlc);
                foreach (var dlc in conditionalList)
                {
                    if (Condition == AltDLCCondition.COND_MANUAL)
                    {
                        if (modForValidating.ModDescTargetVersion >= 6.3)
                        {
                            // On 6.3 trigger failure on this mod to help ensure users design mod properly
                            Log.Error($@"{modForValidating.ModName} has Alternate DLC {friendlyName} that has a value for ConditionalDLC on Condition COND_MANUAL. COND_MANUAL does not use ConditionalDLC, use DLCRequirements instead.");
                            ValidAlternate = false;
                            LoadFailedReason = $"Alternate DLC {friendlyName} that has a value for ConditionalDLC on Condition COND_MANUAL. COND_MANUAL does not use ConditionalDLC, use DLCRequirements instead.";
                            return;
                        }
                        else
                        {
                            Log.Warning($@"{modForValidating.ModName} has AlternateDLC {friendlyName} that has a value for ConditionalDLC on Condition COND_MANUAL. COND_MANUAL does not use ConditionalDLC, use DLCRequirements instead. On mods targetting moddesc 6.3 and above, this will trigger a load failure for a mod.");
                        }

                        break;
                    }
                    else if (Condition == AltDLCCondition.COND_SPECIFIC_DLC_SETUP)
                    {

                        //check +/-
                        if (!dlc.StartsWith(@"-") && !dlc.StartsWith(@"+"))
                        {
                            Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with + or -. When using the condition {Condition}, you must precede DLC names with + or -. Bad value: {dlc}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificDlcSetupMissingPlusMinus, FriendlyName, Condition, dlc);
                            return;
                        }

                        var prefix = dlc.Substring(0, 1);
                        var realname = dlc.Substring(1);

                        //official headers
                        if (Enum.TryParse(realname, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                        {
                            ConditionalDLC.Add(prefix + foldername);
                            continue;
                        }

                        //dlc mods
                        if (!realname.StartsWith(@"DLC_"))
                        {
                            Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with DLC_ or is not official header (after the +/- required by {Condition}). Bad value: {dlc}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificDlcSetupInvalidDlcName, FriendlyName, Condition, dlc);
                            return;
                        }
                        else
                        {
                            ConditionalDLC.Add(prefix + realname);
                        }
                    }
                    else
                    {
                        if (Enum.TryParse(dlc, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                        {
                            ConditionalDLC.Add(foldername);
                            continue;
                        }

                        if (!dlc.StartsWith(@"DLC_"))
                        {
                            Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with DLC_ or is not official header");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_conditionalDLCInvalidValue, FriendlyName);
                            return;
                        }
                        else
                        {
                            ConditionalDLC.Add(dlc);
                        }
                    }
                }
            }

            if (Operation != AltDLCOperation.OP_NOTHING)
            {
                int multilistid = -1;
                if (Operation == AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC)
                {
                    if (properties.TryGetValue(@"MultiListRootPath", out var rootpath))
                    {
                        MultiListRootPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error($@"Alternate DLC ({FriendlyName}) specifies operation OP_ADD_MULTILISTFILES_TO_CUSTOMDLC but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistMissingMultiListRootPath, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListId", out string multilistidstr) && int.TryParse(multilistidstr, out multilistid))
                    {
                        if (job.MultiLists.TryGetValue(multilistid, out var ml))
                        {
                            MultiListId = multilistid;
                            MultiListSourceFiles = ml.Select(x => x.TrimStart('\\', '/')).ToArray();
                        }
                        else
                        {
                            Log.Error($@"Alternate DLC ({FriendlyName}) Multilist ID does not exist as part of the {job.Header} task: multilist" + multilistid);
                            ValidAlternate = false;
                            var id = @"multilist" + multilistid;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistMissingMultiListX, FriendlyName, job.Header, id);
                            return;
                        }
                    }
                    else
                    {
                        Log.Error($@"Alternate DLC ({FriendlyName}) specifies operation OP_ADD_MULTILISTFILES_TO_CUSTOMDLC but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistIdNotIntegerOrMissing, FriendlyName);
                        return;
                    }
                }
                else
                {
                    if (properties.TryGetValue(@"ModAltDLC", out string altDLCFolder))
                    {
                        AlternateDLCFolder = altDLCFolder.Replace('/', '\\');
                    }
                    else
                    {
                        Log.Error(@"Alternate DLC does not specify ModAltDLC but is required");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_missingModAltDLC, FriendlyName);
                        return;
                    }
                }

                if (properties.TryGetValue(@"ModDestDLC", out string destDLCFolder))
                {
                    DestinationDLCFolder = destDLCFolder.Replace('/', '\\');
                }
                else
                {
                    Log.Error(@"Alternate DLC does not specify ModDestDLC but is required");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_missingModDestDLC, FriendlyName);
                    return;
                }
                //todo: Validate target in mod folder



                //Validation
                if (string.IsNullOrWhiteSpace(AlternateDLCFolder) && MultiListRootPath == null)
                {
                    Log.Error($@"Alternate DLC directory (ModAltDLC) not specified for {FriendlyName}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_sourceDirectoryNotSpecifiedForModAltDLC, FriendlyName);
                    return;
                }

                if (string.IsNullOrWhiteSpace(DestinationDLCFolder))
                {
                    Log.Error($@"Destination DLC directory (ModDestDLC) not specified for {FriendlyName}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_destinationDirectoryNotSpecifiedForModDestDLC, FriendlyName);
                    return;
                }

                if (AlternateDLCFolder != null)
                {
                    AlternateDLCFolder = AlternateDLCFolder.TrimStart('\\', '/').Replace('/', '\\');

                    //Check ModAltDLC directory exists
                    var localAltDlcDir = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AlternateDLCFolder);
                    if (!FilesystemInterposer.DirectoryExists(localAltDlcDir, modForValidating.Archive))
                    {
                        Log.Error($@"Alternate DLC directory (ModAltDLC) does not exist: {AlternateDLCFolder}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_sourceDirectoryDoesntExist, FriendlyName, AlternateDLCFolder);
                        return;
                    }
                }
                else if (MultiListRootPath != null)
                {
                    foreach (var multif in MultiListSourceFiles)
                    {
                        var path = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, MultiListRootPath, multif);
                        if (!FilesystemInterposer.FileExists(path, modForValidating.Archive))
                        {
                            Log.Error($@"Alternate DLC ({FriendlyName}) specifies a multilist (index {multilistid}) that contains file that does not exist: {multif}");
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistMissingFileInMultilist, FriendlyName, multilistid, multif);
                            return;
                        }
                    }
                }

                // Validate multilist dlc
            }

            var dlcReqs = properties.TryGetValue(@"DLCRequirements", out string _dlcReqs) ? _dlcReqs.Split(';') : null;
            if (dlcReqs != null)
            {
                var reqList = new List<string>();
                foreach (var originalReq in dlcReqs)
                {
                    var testreq = originalReq;
                    string prefix = "";
                    if (modForValidating.ModDescTargetVersion >= 6.3)
                    {
                        if (testreq.StartsWith("-") || testreq.StartsWith("+"))
                        {
                            prefix = testreq[0].ToString();
                        }
                        testreq = testreq.TrimStart('-', '+');
                    }
                    //official headers
                    if (Enum.TryParse(testreq, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                    {
                        reqList.Add(prefix + foldername);
                        continue;
                    }

                    //dlc mods
                    if (!testreq.StartsWith(@"DLC_"))
                    {
                        Log.Error($@"An item in Alternate DLC's ({FriendlyName}) DLCRequirements doesn't start with DLC_ or is not official header. Bad value: {originalReq}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_dlcRequirementInvalid, FriendlyName, originalReq);
                        return;
                    }
                    else
                    {
                        reqList.Add(originalReq);
                    }
                }


                DLCRequirementsForManual = reqList.ToArray();
            }

            if (Condition == AltDLCCondition.COND_SPECIFIC_SIZED_FILES)
            {
                var requiredFilePaths = properties.TryGetValue(@"RequiredFileRelativePaths", out string _requiredFilePaths) ? _requiredFilePaths.Split(';').ToList() : new List<string>();
                var requiredFileSizes = properties.TryGetValue(@"RequiredFileSizes", out string _requiredFileSizes) ? _requiredFileSizes.Split(';').ToList() : new List<string>();

                if (requiredFilePaths.Count() != requiredFileSizes.Count())
                {
                    Log.Error($@"Alternate DLC {FriendlyName} uses COND_SPECIFIC_SIZED_FILES but the amount of items in the RequiredFileRelativePaths and RequiredFileSizes lists are not equal");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFilesMismatchedParams, FriendlyName);
                    return;
                }

                for (int i = 0; i < requiredFilePaths.Count(); i++)
                {
                    var reqFile = requiredFilePaths[i];
                    var reqSizeStr = requiredFileSizes[i];

                    if (reqFile.Contains(@".."))
                    {
                        Log.Error($@"Alternate DLC {FriendlyName} RequiredFileRelativePaths item {reqFile} is invalid: Values cannot contain '..' for security reasons");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFilesContainsIllegalPattern, FriendlyName, reqFile);
                        return;
                    }

                    reqFile = reqFile.Replace('/', '\\').TrimStart('\\'); //standardize
                    if (long.TryParse(reqSizeStr, out var reqSize) && reqSize >= 0)
                    {
                        RequiredSpecificFiles[reqFile] = reqSize;
                    }
                    else
                    {
                        Log.Error($@"Alternate DLC {FriendlyName} RequiredFileSizes item {reqFile} is invalid: {reqSizeStr}. Values must be greater than or equal to zero.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFileMustBeLargerThanZero, FriendlyName, reqFile, reqSizeStr);
                        return;
                    }
                }

                if (!RequiredSpecificFiles.Any())
                {
                    Log.Error($@"Alternate DLC {FriendlyName} is invalid: COND_SPECIFIC_SIZED_FILES is specified as the condition but there are no values in RequiredFileRelativePaths/RequiredFileSizes");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFilesMissingRequiredParams, FriendlyName);
                    return;
                }
            }

            if (!ReadImageAssetOptions(modForValidating, properties))
            {
                return; // Failed in super call
            }

            ReadAutoApplicableText(properties);

            if (modForValidating.ModDescTargetVersion >= 6.0)
            {
                GroupName = properties.TryGetValue(@"OptionGroup", out string groupName) ? groupName : null;
            }

            if (Condition == AltDLCCondition.COND_MANUAL && properties.TryGetValue(@"CheckedByDefault", out string checkedByDefault) && bool.TryParse(checkedByDefault, out bool cbd))
            {
                CheckedByDefault = cbd;
            }

            if (Condition != AltDLCCondition.COND_MANUAL && Condition != AltDLCCondition.COND_SPECIFIC_SIZED_FILES && Condition != AltDLCCondition.INVALID_CONDITION)
            {
                //ensure conditional dlc list has at least one item.
                if (ConditionalDLC.Count == 0)
                {
                    Log.Error($@"Alternate DLC {FriendlyName} cannot have empty or missing Conditional DLC list, as it does not use COND_MANUAL or COND_SPECIFIC_SIZED_FILES.");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_emptyConditionalDLCList, FriendlyName);
                    return;
                }
            }

            CLog.Information($@"AlternateDLC loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

        public string[] MultiListSourceFiles { get; }
        public string MultiListRootPath { get; }

        public override bool IsManual => Condition == AltDLCCondition.COND_MANUAL;

        public override bool UIIsSelectable { get; set; }

        public override bool UINotApplicable
        {
            get
            {
                if (IsManual)
                {
                    return !UIIsSelectable; //SetupInitialSelection() will set this. If it's false, it means this is not applicable, so set UI to reflect that
                }
                else
                {
                    return !IsSelected;
                }
            }
        }

        internal bool HasRelativeFiles()
        {
            if (Operation == AltDLCOperation.INVALID_OPERATION) return false;
            if (Operation == AltDLCOperation.OP_NOTHING) return false;
            return AlternateDLCFolder != null || MultiListSourceFiles != null;
        }

        public void SetupInitialSelection(GameTarget target, Mod mod)
        {
            UIIsSelectable = false; //Reset
            IsSelected = false; //Reset
            if (Condition == AltDLCCondition.COND_MANUAL)
            {
                IsSelected = CheckedByDefault;
                if (DLCRequirementsForManual != null)
                {
                    var dlc = M3Directories.GetInstalledDLC(target);

                    if (mod.ModDescTargetVersion >= 6.3)
                    {
                        var requiredDLC = DLCRequirementsForManual.Where(x => !x.StartsWith(@"-") || x.StartsWith(@"+")).Select(x => x.TrimStart('+')); // none or + means 'must exist'
                        var notPresentDLCRequired = DLCRequirementsForManual.Where(x => x.StartsWith(@"-")).Select(x=>x.TrimStart('-'));
                        UIIsSelectable = dlc.ContainsAll(requiredDLC, StringComparer.InvariantCultureIgnoreCase) && dlc.ContainsNone(notPresentDLCRequired, StringComparer.InvariantCultureIgnoreCase);
                    }
                    else
                    {
                        // Previous logic. Left here to ensure nothing changes.
                        UIIsSelectable = dlc.ContainsAll(DLCRequirementsForManual, StringComparer.InvariantCultureIgnoreCase);
                    }

                    if (!UIIsSelectable && mod.ModDescTargetVersion >= 6.2)
                    {
                        // Mod Manager 6.2: If requirements are not met this option is forcibly not checked.
                        // Mods targeting Moddesc 6 or 6.1 will possibly be bugged if they used this feature
                        IsSelected = false;
                    }
                    CLog.Information($@" > AlternateDLC SetupInitialSelection() {FriendlyName}: UISelectable: {UIIsSelectable}, conducted DLCRequirements check.", Settings.LogModInstallation);

                }
                else
                {
                    UIIsSelectable = true;
                }

                return;
            }

            var installedDLC = M3Directories.GetInstalledDLC(target);
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
                case AltDLCCondition.COND_SPECIFIC_SIZED_FILES:
                    {
                        var selected = true;
                        foreach (var reqPair in RequiredSpecificFiles)
                        {
                            if (selected)
                            {
                                var targetFile = Path.Combine(target.TargetPath, reqPair.Key);
                                selected &= File.Exists(targetFile) && new FileInfo(targetFile).Length == reqPair.Value;
                            }
                        }

                        IsSelected = selected;
                    }
                    break;
                case AltDLCCondition.COND_SPECIFIC_DLC_SETUP:
                    {
                        var selected = true;
                        foreach (var condDlc in ConditionalDLC)
                        {
                            if (selected)
                            {
                                bool existenceRule = condDlc.Substring(0, 1) == @"+";
                                var dlcfoldername = condDlc.Substring(1);

                                if (existenceRule)
                                {
                                    selected &= installedDLC.Contains(dlcfoldername, StringComparer.CurrentCultureIgnoreCase);
                                }
                                else
                                {
                                    selected &= !installedDLC.Contains(dlcfoldername, StringComparer.CurrentCultureIgnoreCase);
                                }
                            }
                        }

                        IsSelected = selected;
                    }
                    break;
            }

            UIIsSelectable = false; //autos
            //IsSelected; //autos
        }

        /// <summary>
        /// Builds the editable parameter map for use in moddesc.ini editor
        /// </summary>
        /// <param name="properties"></param>
        //private void buildParameterMap(Dictionary<string, string> properties)
        //{
        //    var parms = properties.Select(x => new AlternateOption.Parameter() { Key = x.Key, Value = x.Value }).ToList();
        //    foreach (var v in AllParameters)
        //    {
        //        if (parms.All(x => x.Key != v))
        //        {
        //            parms.Add(new MDParameter(v, ""));
        //        }
        //    }

        //    ParameterMap.ReplaceAll(parms.OrderBy(x => x.Key));
        //}

        /// <summary>
        /// List of all keys in the altdlc struct that are publicly parsable
        /// </summary>
        public override void BuildParameterMap(Mod mod)
        {
            var parameterDictionary = new Dictionary<string, object>()
            {
                {@"Condition", Condition},
                {@"ConditionalDLC", ConditionalDLC},
                {@"ModOperation", Operation},
                {@"ModAltDLC", AlternateDLCFolder},
                {@"ModDestDLC", DestinationDLCFolder},
                {@"FriendlyName", FriendlyName},
                {@"Description", Description},
                {@"CheckedByDefault", CheckedByDefault ? @"True" : null}, //don't put checkedbydefault in if it is not set to true.
                {@"OptionGroup", GroupName},
                {@"ApplicableAutoText", ApplicableAutoTextRaw},
                {@"NotApplicableAutoText", NotApplicableAutoTextRaw},
                {@"MultiListId", MultiListId > 0 ? MultiListId.ToString() : null},
                {@"MultiListRootPath", MultiListRootPath},
                {@"RequiredFileRelativePaths", RequiredSpecificFiles.Keys.ToList()}, // List of relative paths
                {@"RequiredFileSizes", RequiredSpecificFiles.Values.ToList()}, // List of relative sizes
                {@"DLCRequirements", DLCRequirementsForManual},
                {@"ImageAssetName", ImageAssetName},
                {@"ImageHeight", ImageHeight > 0 ? ImageHeight.ToString() : null}
            };

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }

        ///// <summary>
        ///// Serializes this object to it's moddesc.ini representation
        ///// </summary>
        ///// <returns></returns>
        //public string Serialize()
        //{
        //    var props = new Dictionary<string, string>();
        //    props[@"Condition"] = Condition.ToString(); //always set
        //    props[@"ConditionalDLC"] = string.Join(';', ConditionalDLC);
        //    props[@"ModOperation"] = Operation.ToString(); //always set
        //    props[@"ModAltDLC"] = AlternateDLCFolder;
        //    props[@"ModDestDLC"] = DestinationDLCFolder;
        //    props[@"FriendlyName"] = FriendlyName;
        //    props[@"Description"] = Description;
        //    if (CheckedByDefault)
        //    {
        //        props[@"CheckedByDefault"] = CheckedByDefault.ToString();
        //    }

        //    if (!string.IsNullOrWhiteSpace(GroupName))
        //    {
        //        props[@"OptionGroup"] = GroupName;
        //    }
        //    if (!string.IsNullOrWhiteSpace(ApplicableAutoText))
        //    {
        //        props[@"ApplicableAutoText"] = ApplicableAutoText;
        //    }
        //    if (!string.IsNullOrWhiteSpace(NotApplicableAutoText))
        //    {
        //        props[@"NotApplicableAutoText"] = NotApplicableAutoText;
        //    }

        //    if (!string.IsNullOrWhiteSpace(MultiListRootPath))
        //    {
        //        props[@"MultiListRootPath"] = MultiListRootPath;
        //    }

        //    if (!string.IsNullOrWhiteSpace(MultiListRootPath))
        //    {
        //        props[@"MultiListId"] = MultiListId.ToString();
        //    }

        //    if (RequiredSpecificFiles.Any())
        //    {
        //        var paths = "";
        //        var sizes = "";
        //        foreach (var v in RequiredSpecificFiles)
        //        {
        //            if (paths != @"") paths += @";";
        //            if (sizes != @"") sizes += @";";
        //            paths += v.Key; // should we check for spaces? Can game files support spaces?
        //            sizes += v.Value;
        //        }
        //        props[@"RequiredFileRelativePaths"] = paths;
        //        props[@"RequiredFileSizes"] = sizes;
        //    }


        //    return StringStructParser.BuildCommaSeparatedSplitValueList(props);
        //}
    }
}