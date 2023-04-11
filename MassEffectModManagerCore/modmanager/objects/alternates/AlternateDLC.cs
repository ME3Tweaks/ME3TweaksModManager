using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    [DebuggerDisplay(@"AlternateDLC | {FriendlyName} | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, DestDLC: {DestinationDLCFolder}, AltDLC: {AlternateDLCFolder}")]
    [AddINotifyPropertyChangedInterface]
    public sealed class AlternateDLC : AlternateOption
    {
        public enum AltDLCOperation
        {
            INVALID_OPERATION,
            OP_ADD_CUSTOMDLC,
            OP_ADD_FOLDERFILES_TO_CUSTOMDLC,
            OP_ADD_MULTILISTFILES_TO_CUSTOMDLC,
            //// <summary>
            //// On mod install, an ini file(s) is merged into the DLCs. This is game dependent.
            //// </summary>
            //OP_MERGE_INI,
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

        public AltDLCCondition Condition { get; set; }
        public AltDLCOperation Operation { get; set; }

        /// <summary>
        /// Requirements for this manual option to be able to be picked
        /// </summary>
        public PlusMinusKey[] DLCRequirementsForManual { get; }

        public override bool IsAlways => false; //AlternateDLC doesn't support this

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
        public AlternateDLC(string friendlyName, AltDLCCondition condition, AltDLCOperation operation)
        {
            FriendlyName = friendlyName;
            Condition = condition;
            Operation = operation;
            BuildParameterMap(null); //Alternates don't need a mod, as nothing is game specific
        }

        public AlternateDLC(string alternateDLCText, Mod modForValidating, ModJob job)
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
                M3Log.Error(@"Alternate DLC does not specify FriendlyName. Mods targeting moddesc >= 6.0 require FriendlyName");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_validation_altdlc_oneAltDlcMissingFriendlyNameCmm6);
                return;
            }

            if (!Enum.TryParse<AltDLCCondition>(properties[@"Condition"], out var cond) || cond == AltDLCCondition.INVALID_CONDITION)
            {
                M3Log.Error($@"Alternate DLC specifies unknown/unsupported condition: {properties[@"Condition"]}"); //do not localize
                ValidAlternate = false;
                var condition = properties[@"Condition"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altdlc_unknownCondition)} {condition}";
                return;
            }

            Condition = cond;

            if (!Enum.TryParse<AltDLCOperation>(properties[@"ModOperation"], out var op) || op == AltDLCOperation.INVALID_OPERATION)
            {
                M3Log.Error($@"Alternate DLC specifies unknown/unsupported operation: {properties[@"ModOperation"]}"); //do not localize
                ValidAlternate = false;
                var operation = properties[@"ModOperation"];
                LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altdlc_unknownOperation)} {operation}";
                return;
            }

            Operation = op;

            if (properties.TryGetValue(@"Description", out string description))
            {
                Description = description;
            }

            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(Description))
            {
                //Cannot be null.
                M3Log.Error($@"Alternate DLC {FriendlyName} cannot have empty Description or missing Description descriptor as it targets cmmver >= 6");
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
                            M3Log.Error($@"{modForValidating.ModName} has Alternate DLC {friendlyName} that has a value for ConditionalDLC on Condition COND_MANUAL. COND_MANUAL does not use ConditionalDLC, use DLCRequirements instead.");
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_condManualWithConditionalDLC, friendlyName);
                            return;
                        }
                        else
                        {
                            M3Log.Warning($@"{modForValidating.ModName} has AlternateDLC {friendlyName} that has a value for ConditionalDLC on Condition COND_MANUAL. COND_MANUAL does not use ConditionalDLC, use DLCRequirements instead. On mods targetting moddesc 6.3 and above, this will trigger a load failure for a mod.");
                        }

                        break;
                    }
                    else if (Condition == AltDLCCondition.COND_SPECIFIC_DLC_SETUP)
                    {

                        //check +/-
                        if (!dlc.StartsWith(@"-") && !dlc.StartsWith(@"+"))
                        {
                            M3Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with + or -. When using the condition {Condition}, you must precede DLC names with + or -. Bad value: {dlc}");
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
                            M3Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with DLC_ or is not official header (after the +/- required by {Condition}). Bad value: {dlc}");
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
                            M3Log.Error($@"An item in Alternate DLC's ({FriendlyName}) ConditionalDLC doesn't start with DLC_ or is not official header");
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
                    // ModDesc 8.0 change: Require MultiListRootPath not be an empty string.
                    // This checks because EGM LE did not set it so this would break loading that mod on future builds
                    if (properties.TryGetValue(@"MultiListRootPath", out var rootpath) && (modForValidating.ModDescTargetVersion < 8.0 || !string.IsNullOrWhiteSpace(rootpath)))
                    {
                        MultiListRootPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        M3Log.Error($@"Alternate DLC ({FriendlyName}) specifies operation OP_ADD_MULTILISTFILES_TO_CUSTOMDLC but does not specify the required item MultiListRootPath.");
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
                            M3Log.Error($@"Alternate DLC ({FriendlyName}) Multilist ID does not exist as part of the {job.Header} task: multilist" + multilistid);
                            ValidAlternate = false;
                            var id = @"multilist" + multilistid;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistMissingMultiListX, FriendlyName, job.Header, id);
                            return;
                        }
                    }
                    else
                    {
                        M3Log.Error($@"Alternate DLC ({FriendlyName}) specifies operation OP_ADD_MULTILISTFILES_TO_CUSTOMDLC but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_multilistIdNotIntegerOrMissing, FriendlyName);
                        return;
                    }

                    // ModDesc 8.0: Allow flattening output of multilist output.
                    // Backported to 7.0 125 build for EGM - it must work on 125 7.0 and above.
                    if ((modForValidating.ModDescTargetVersion >= 7.0 && modForValidating.MinimumSupportedBuild >= 125)
                        || modForValidating.ModDescTargetVersion >= 8.0)
                    {
                        if (properties.TryGetValue(@"FlattenMultiListOutput", out var multiListFlattentStr) && !string.IsNullOrWhiteSpace(multiListFlattentStr))
                        {
                            if (bool.TryParse(multiListFlattentStr, out var multiListFlatten))
                            {
                                FlattenMultilistOutput = multiListFlatten;
                            }
                            else
                            {
                                M3Log.Error($@"Alternate DLC ({FriendlyName}) specifies 'FlattenMultiListOutput' descriptor, but the value is not 'true' or 'false': {multiListFlattentStr}");
                                ValidAlternate = false;
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_flattentNotTrueOrFalse, FriendlyName, multiListFlattentStr);
                                return;
                            }
                        }
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
                        M3Log.Error(@"Alternate DLC does not specify ModAltDLC but is required");
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
                    M3Log.Error(@"Alternate DLC does not specify ModDestDLC but is required");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_missingModDestDLC, FriendlyName);
                    return;
                }
                //todo: Validate target in mod folder



                //Validation
                if (string.IsNullOrWhiteSpace(AlternateDLCFolder) && MultiListRootPath == null)
                {
                    M3Log.Error($@"Alternate DLC directory (ModAltDLC) not specified for {FriendlyName}");
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_sourceDirectoryNotSpecifiedForModAltDLC, FriendlyName);
                    return;
                }

                if (string.IsNullOrWhiteSpace(DestinationDLCFolder))
                {
                    M3Log.Error($@"Destination DLC directory (ModDestDLC) not specified for {FriendlyName}");
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
                        M3Log.Error($@"Alternate DLC directory (ModAltDLC) does not exist: {AlternateDLCFolder}");
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
                            M3Log.Error($@"Alternate DLC ({FriendlyName}) specifies a multilist (index {multilistid}) that contains file that does not exist: {multif}");
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
                var reqList = new List<PlusMinusKey>();
                foreach (var originalReq in dlcReqs)
                {
                    var testreq = new PlusMinusKey(originalReq);
                    if (modForValidating.ModDescTargetVersion < 6.3)
                    {
                        // ModDesc < 6.3 did not support +/-, so we strip it off.
                        testreq.IsPlus = null;
                    }
                    //official headers
                    if (Enum.TryParse(testreq.Key, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                    {
                        reqList.Add(testreq);
                        continue;
                    }

                    // Moddesc 8: You can no longer DLCRequirements on vanilla LE DLC, since they're always present,
                    // and removing vanilla DLCs is not supported.
                    if (modForValidating.ModDescTargetVersion >= 8.0 && modForValidating.Game.IsLEGame() && MEDirectories.OfficialDLC(modForValidating.Game).Contains(testreq.Key, StringComparer.InvariantCultureIgnoreCase))
                    {
                        M3Log.Error($@"Alternate DLC ({FriendlyName}) DLCRequirements specifies a DLC that ships in Legendary Edition. Legendary Edition mods targeting moddesc 8.0 and higher cannot set DLCRequirements on vanilla DLC, as Mod Manager does not support games that do not have the vanilla DLC. Unsupported value: {originalReq}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_dlcrequirementsHasOfficialLEDLC, FriendlyName, originalReq);
                        return;
                    }

                    //dlc mods
                    if (!testreq.Key.StartsWith(@"DLC_"))
                    {
                        M3Log.Error($@"An item in Alternate DLC's ({FriendlyName}) DLCRequirements doesn't start with DLC_ or is not official header. Bad value: {originalReq}");
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_dlcRequirementInvalid, FriendlyName, originalReq);
                        return;
                    }
                    else
                    {
                        reqList.Add(testreq);
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
                    M3Log.Error($@"Alternate DLC {FriendlyName} uses COND_SPECIFIC_SIZED_FILES but the amount of items in the RequiredFileRelativePaths and RequiredFileSizes lists are not equal");
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
                        M3Log.Error($@"Alternate DLC {FriendlyName} RequiredFileRelativePaths item {reqFile} is invalid: Values cannot contain '..' for security reasons");
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
                        M3Log.Error($@"Alternate DLC {FriendlyName} RequiredFileSizes item {reqFile} is invalid: {reqSizeStr}. Values must be greater than or equal to zero.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFileMustBeLargerThanZero, FriendlyName, reqFile, reqSizeStr);
                        return;
                    }
                }

                if (!RequiredSpecificFiles.Any())
                {
                    M3Log.Error($@"Alternate DLC {FriendlyName} is invalid: COND_SPECIFIC_SIZED_FILES is specified as the condition but there are no values in RequiredFileRelativePaths/RequiredFileSizes");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altdlc_specificSizedFilesMissingRequiredParams, FriendlyName);
                    return;
                }
            }


            if (!ReadSharedOptions(modForValidating, properties))
            {
                return; // Failed in super call
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
                    M3Log.Error($@"Alternate DLC {FriendlyName} cannot have empty or missing Conditional DLC list, as it does not use COND_MANUAL or COND_SPECIFIC_SIZED_FILES.");
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_interp_altdlc_emptyConditionalDLCList, FriendlyName);
                    return;
                }
            }

            M3Log.Information($@"AlternateDLC loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }

        public override bool IsManual => Condition == AltDLCCondition.COND_MANUAL;

        //public override bool UINotApplicable
        //{
        //    get
        //    {
        //        if (IsManual)
        //        {
        //            return !UIIsSelectable; //SetupInitialSelection() will set this. If it's false, it means this is not applicable, so set UI to reflect that
        //        }
        //        else
        //        {
        //            return !IsSelected;
        //        }
        //    }
        //}

        internal bool HasRelativeFiles()
        {
            if (Operation == AltDLCOperation.INVALID_OPERATION) return false;
            if (Operation == AltDLCOperation.OP_NOTHING) return false;
            return AlternateDLCFolder != null || MultiListSourceFiles != null;
        }

        public void SetupInitialSelection(GameTargetWPF target, Mod mod)
        {
            UIIsSelectable = false; //Reset
            UIIsSelected = false; //Reset
            if (Condition == AltDLCCondition.COND_MANUAL)
            {
                UIIsSelected = CheckedByDefault;
                // Mod Manager 8: DLC Requirements was moved to SetupSelectability.

                return;
            }

            var installedDLC = target.GetInstalledDLC();
            switch (Condition)
            {
                case AltDLCCondition.COND_DLC_NOT_PRESENT:
                case AltDLCCondition.COND_ANY_DLC_NOT_PRESENT:
                    UIIsSelected = !ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_DLC_PRESENT:
                case AltDLCCondition.COND_ANY_DLC_PRESENT:
                    UIIsSelected = ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_ALL_DLC_NOT_PRESENT:
                    UIIsSelected = !ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltDLCCondition.COND_ALL_DLC_PRESENT:
                    UIIsSelected = ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
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

                        UIIsSelected = selected;
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

                        UIIsSelected = selected;
                    }
                    break;
            }

            UIIsSelectable = false; //autos
            //IsSelected; //autos
        }

        internal override bool UpdateSelectability(IEnumerable<AlternateOption> allOptionsDependedOn, Mod mod, GameTargetWPF target)
        {
            if (DLCRequirementsForManual != null)
            {
                var dlc = target.GetInstalledDLC();

                if (mod.ModDescTargetVersion >= 6.3)
                {
                    // ModDesc 6.3: +/- system allowed different DLC setups.
                    var requiredDLC = DLCRequirementsForManual.Where(x => x.IsPlus == null || x.IsPlus.Value).Select(x => x.Key); // none or + means 'must exist'
                    var notPresentDLCRequired = DLCRequirementsForManual.Where(x => x.IsPlus != null && !x.IsPlus.Value).Select(x => x.Key);
                    UIIsSelectable = dlc.ContainsAll(requiredDLC, StringComparer.InvariantCultureIgnoreCase) && dlc.ContainsNone(notPresentDLCRequired, StringComparer.InvariantCultureIgnoreCase);
                }
                else
                {
                    // Previous logic. Left here to ensure nothing changes.
                    // ModDesc 6: All DLC must be present
                    UIIsSelectable = dlc.ContainsAll(DLCRequirementsForManual.Select(x => x.ToString()), StringComparer.InvariantCultureIgnoreCase);
                }

                if (!UIIsSelectable && mod.ModDescTargetVersion >= 6.2)
                {
                    // Mod Manager 6.2: If requirements are not met this option is forcibly not checked.
                    // Mods targeting Moddesc 6 or 6.1 would possibly be bugged if they used this feature
                    // so this change does not affect mods targeting those versions
                    UIIsSelected = false;
                    ForceNotApplicable = true; // This option is not applicable
                }
                else
                {
                    ForceNotApplicable = false; // This option is not forced not-applicable
                }

                M3Log.Information($@" > AlternateDLC UpdateSelectability for {FriendlyName}: UISelectable: {UIIsSelectable}, conducted DLCRequirements check.", Settings.LogModInstallation);
            }
            else
            {
                // If this is a manual we make it selectable; otherwise it's an auto, in which case we are
                // not selectable by the user.
                UIIsSelectable = Condition == AltDLCCondition.COND_MANUAL;
            }

            if (DLCRequirementsForManual != null && !UIIsSelectable)
                return false; // The user can't change the selection so we don't update the selectability states since this option is locked by DLC requirements.

            return base.UpdateSelectability(allOptionsDependedOn, mod, target);
        }

        /// <summary>
        /// List of all keys in the altdlc struct that are publicly parsable
        /// </summary>
        public override void BuildParameterMap(Mod mod)
        {
            var conditions = Enum.GetValues<AltDLCCondition>().Where(x => x != AltDLCCondition.INVALID_CONDITION).Select(x => x.ToString());
            var operations = Enum.GetValues<AltDLCOperation>().Where(x => x != AltDLCOperation.INVALID_OPERATION).Select(x => x.ToString());

            var parameterDictionary = new Dictionary<string, object>()
            {
                // List of available conditions
                {@"Condition", new MDParameter(@"string", @"Condition", Condition.ToString(), conditions, AltDLCCondition.COND_MANUAL.ToString())},
                {@"ConditionalDLC", ConditionalDLC},
                {@"ModOperation", new MDParameter(@"string", @"ModOperation", Operation.ToString(), operations, AltDLCOperation.OP_NOTHING.ToString())},
                {@"ModAltDLC", AlternateDLCFolder},
                {@"ModDestDLC", DestinationDLCFolder},

                {@"MultiListId", MultiListId > 0 ? MultiListId.ToString() : null},
                {@"MultiListRootPath", MultiListRootPath},
                {@"FlattenMultiListOutput", new MDParameter(@"FlattenMultiListOutput", FlattenMultilistOutput, false)},
                {@"RequiredFileRelativePaths", RequiredSpecificFiles.Keys.ToList()}, // List of relative paths
                {@"RequiredFileSizes", RequiredSpecificFiles.Values.ToList()}, // List of relative sizes
                {@"DLCRequirements", DLCRequirementsForManual},
            };

            BuildSharedParameterMap(mod, parameterDictionary);
            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }
    }
}
