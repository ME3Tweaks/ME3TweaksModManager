using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Crashes;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Base class for AlternateFile and AlternateDLC (and ME1ReadOnlyConfigFileOption, technically)
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public abstract class AlternateOption : IMDParameterMap
    {
        /// <summary>
        /// Invoked when the user MANUALLY changes the selection state of the alternate. Initial setup does not propogate this.
        /// </summary>
        public event EventHandler IsSelectedChanged;

        /// <summary>
        /// Text to show next to an option when it automatically marked as applicable
        /// </summary>
        public string ApplicableAutoText { get; private set; }

        /// <summary>
        /// Text to show next to an option when it is automatically marked as not applicable
        /// </summary>
        public string NotApplicableAutoText { get; private set; }

        /// <summary>
        /// Text to show next to an option that requires other options to be selected specifically.
        /// </summary>
        public string DependsOnText { get; private set; }

        /// <summary>
        /// The action that is taken when the depends on action is met.
        /// </summary>
        public EDependsOnAction DependsOnMetAction { get; private set; }

        /// <summary>
        /// The action that is taken when the depends on action is met.
        /// </summary>
        public EDependsOnAction DependsOnNotMetAction { get; private set; }

        /// <summary>
        /// For moddesc.ini editor only
        /// </summary>
        public string ApplicableAutoTextRaw { get; private set; }

        /// <summary>
        /// For moddesc.ini editor only
        /// </summary>
        public string NotApplicableAutoTextRaw { get; private set; }

        /// <summary>
        /// If this option should be checked (in multi mode, this option is the selected one) by default when the options are presented to the user.
        /// </summary>
        public virtual bool CheckedByDefault { get; internal set; }

        /// <summary>
        /// If this option can be toggled by the user, assuming it meets other conditional checks
        /// </summary>
        public abstract bool IsManual { get; }

        /// <summary>
        /// The list of DLC to be checked against with the specified condition (defined in subclasses of AlternateOption)
        /// </summary>
        public readonly List<string> ConditionalDLC = new List<string>();

        #region UI-Specific

        /// <summary>
        /// If true, this alternate is not shown to the user. Can be used for pivoting dependencies (depending on an option that depends on an automated state)
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// UI-only
        /// </summary>
        public virtual double CheckboxOpacity => !UIIsSelectable ? .65 : 1;

        /// <summary>
        /// UI-only
        /// </summary>
        public virtual double TextOpacity
        {
            get
            {
                if (!UIIsSelectable && UIIsSelected) return 1;
                if (!UIIsSelectable && !UIIsSelected) return .5;
                return 1;
            }
        }

        /// <summary>
        /// Forcibly sets the option to not be applicable in the UI.
        /// </summary>
        public bool ForceNotApplicable { get; protected set; }

        /// <summary>
        /// If this option is required (automatic and checked). This does not apply if the condition is OP_ALWAYS. 
        /// </summary>
        public virtual bool UIRequired => !IsManual && !IsAlways && UIIsSelected;

        /// <summary>
        /// If this option can be selected on or off by the end-user
        /// </summary>
        public virtual bool UIIsSelectable
        {
            get; set;
        }

        //public abstract bool UINotApplicable { get; }

        /// <summary>
        /// If this option is not applicable to the installation
        /// </summary>
        public virtual bool UINotApplicable => ForceNotApplicable || (!IsManual && !UIIsSelected && !IsAlways);

        /// <summary>
        /// UI ONLY - If this option is currently selected for installation
        /// </summary>
        public bool UIIsSelected { get; set; }
        #endregion

        /// <summary>
        /// The root path where the multilists (if used) for this alternate is stored at
        /// </summary>
        public string MultiListRootPath { get; init; }

        /// <summary>
        /// The list of multilist source files, relative to the MultiListRootPath (if used)
        /// </summary>
        public string[] MultiListSourceFiles { get; init; }

        /// <summary>
        /// If, when applying the multilist, the output files should be flattened to the target directory, instead of retaining their relative paths
        /// </summary>
        public bool FlattenMultilistOutput { get; init; }


        /// <summary>
        /// Raises the IsSelectedChanged event.
        /// </summary>
        internal void RaiseIsSelectedChanged()
        {
            IsSelectedChanged?.Invoke(this, new DataEventArgs(UIIsSelected));
        }

        /// <summary>
        /// The MultilistId for this alternate. This is only used in the moddesc.ini editor as
        /// the list of files is built during loading of the alternate
        /// </summary>
        public int MultiListId { get; set; } = -1;

        /// <summary>
        /// If this option is always selected and forced on. Used to put an option that always is checked, often with OP_NOTHING.
        /// </summary>
        public abstract bool IsAlways { get; }

        /// <summary>
        /// The index value for sorting this alternate in the list
        /// </summary>
        public int SortIndex { get; set; }

        /// <summary>
        /// Builds the moddesc.ini parameter map
        /// </summary>
        /// <param name="mod"></param>
        public abstract void BuildParameterMap(Mod mod);

        /// <summary>
        /// The option group name for this mod. Null indicates this is not part of a mutual exclusive set
        /// </summary>
        public virtual string GroupName { get; internal set; }

        /// <summary>
        /// The friendly name to display to the user.
        /// </summary>
        public virtual string FriendlyName { get; internal set; }

        /// <summary>
        /// The description of this option to display to the user.
        /// </summary>
        public virtual string Description { get; internal set; }

        /// <summary>
        /// If this alternate is valid or not.
        /// </summary>
        public bool ValidAlternate;
        /// <summary>
        /// Why this alternate is invalid.
        /// </summary>
        public string LoadFailedReason;
        /// <summary>
        /// Names of the attached image asset for this option.
        /// </summary>
        public virtual string ImageAssetName { get; internal set; }
        /// <summary>
        /// The loaded image asset.
        /// </summary>
        public BitmapSource ImageBitmap { get; set; }

        /// <summary>
        /// The height in pixels (resolution independent) of the image to show. The aspect ratio is preserved, so this will automatically product the width.
        /// </summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// If the option key was defined by the developer, or was auto generated by Mod Manager
        /// </summary>
        public bool HasDefinedOptionKey { get; private set; }


        private string _optionKey;

        /// <summary>
        /// A key that can be used to reference this alternate. If one is not specified in the moddesc.ini, one is automatically generated from the CRC of the unicode friendlyname of the alternate.
        /// </summary>
        public string OptionKey
        {
            get
            {
                if (_optionKey != null) return _optionKey;
                // Generate one one based on the name of the alternate.
                var keydata = FriendlyName;
                // 08/24/2022: 8.0.1 beta 2: Add group name to better differentiate key.
                if (!string.IsNullOrWhiteSpace(GroupName)) keydata += GroupName;
                var data = Encoding.Unicode.GetBytes(keydata);

                _optionKey = Convert.ToHexString(Crc32.Hash(data));
                return _optionKey;
            }
            set
            {
                _optionKey = value;
                HasDefinedOptionKey = value != null;
            }
        }

        /// <summary>
        /// Updates the selection and applicability states for this alternate option.
        /// </summary>
        /// <param name="allOptionsDependedOn">List of alternate options that this one depends on, so they can be checked against for their selection state.</param>
        /// <returns>True if the selection state was changed; false if not. This is used to determine if there needs to be another call to update selections again</returns>
        internal virtual bool UpdateSelectability(IEnumerable<AlternateOption> allOptionsDependedOn, Mod mod, GameTargetWPF target)
        {
            if (DependsOnKeys.Count == 0) return false; // Nothing changes as we don't depend on any other options
            Debug.WriteLine($@"UpdateSelectability on {FriendlyName} with DependsOnKeys!");
            bool changed = false;
            bool keepParsing = true;
            // Depends On Keys
            foreach (var key in DependsOnKeys)
            {
                if (!keepParsing)
                    continue;

                var option = allOptionsDependedOn.FirstOrDefault(x => x.OptionKey == key.Key);
                if (option == null)
                {
                    // This shouldn't happen!
                    Debug.WriteLine($@"DependsOnKey not found in list of all options: {key}! This shouldn't happen.");
                    Crashes.TrackError(new Exception($@"DependsOnKey not found in list of all options: {key}! This shouldn't happen."));
                    continue;
                }

                if (option.UIIsSelected && !key.IsPlus.Value)
                {
                    // The DependsOnKey option is selected, but we need -
                    changed = ApplyDependsOnNotMet();
                    keepParsing = false;
                }
                else if (!option.UIIsSelected && key.IsPlus.Value)
                {
                    // The DependsOnKey option is not selected, but we need +
                    changed = ApplyDependsOnNotMet();
                    keepParsing = false;
                }
                else if (!option.UIIsSelected ^ key.IsPlus.Value)
                {
                    // Unlock the option
                    ApplyDependsOnMet();
                }

                // Todo: This implementation needs updated for multi-mode.
                // This should probably be moved to AlternateGroup.
                // If we are an option group, we reset to the default option
                //if (GroupName != null && changed)
                //{
                //    var defaultOption = allOptions.FirstOrDefault(x => x.CheckedByDefault && x.GroupName == GroupName);
                //    defaultOption.IsSelected = true;
                //}
            }



            return changed;
        }

        /// <summary>
        /// Called when the DependsOnKeys conditions are not met.
        /// </summary>
        /// <returns></returns>
        private bool ApplyDependsOnNotMet()
        {
            return InternalApplyDepends(DependsOnNotMetAction);
        }

        /// <summary>
        /// Called when the DependsOnKeys conditions are met.
        /// </summary>
        /// <returns></returns>
        private bool ApplyDependsOnMet()
        {
            return InternalApplyDepends(DependsOnMetAction);
        }

        /// <summary>
        /// The internal implementation of applying the Depends actions.
        /// </summary>
        /// <param name="dependsAction">Action to perform</param>
        /// <returns>True if changed states, false if not</returns>
        /// <exception cref="NotImplementedException"></exception>
        private bool InternalApplyDepends(EDependsOnAction dependsAction)
        {
            var initialSelection = UIIsSelected;

            // Can we select?
            var hasUserChoice = dependsAction is EDependsOnAction.ACTION_ALLOW_SELECT or EDependsOnAction.ACTION_ALLOW_SELECT_CHECKED;
            var alreadyHasUserChoice = UIIsSelectable;

            if (!hasUserChoice)
            {
                // We're going to lock the option.
                UIIsSelected = dependsAction == EDependsOnAction.ACTION_DISALLOW_SELECT_CHECKED;
            }
            else if (!alreadyHasUserChoice)
            {
                // If the user is gaining the ability to make a decision, we will follow the action by the developer. We don't want to modify the existing user choice if they have a choice
                // and this update doesn't change the ability for the user to make a choice
                UIIsSelected = dependsAction == EDependsOnAction.ACTION_ALLOW_SELECT_CHECKED; // Other option is unchecked.
            }

            UIIsSelectable = hasUserChoice; // Make option selectable if it provider user choice
            return initialSelection != UIIsSelected;
        }

        /// <summary>
        /// List of keys (selected or deselected) that are required in order for this alernate to be selectable
        /// </summary>
        public List<PlusMinusKey> DependsOnKeys { get; } = new List<PlusMinusKey>(0); // Default to size 0 as most mods will not use this feature

        /// <summary>
        /// Loads the image asset for this alternate. If this method is called on an archive based mod, it must be done while the archive is still open.
        /// </summary>
        /// <param name="mod">Mod that this alternate is associated with</param>
        /// <param name="initializingAssetName">The asset name to load. If null, the parsed value is used instead.</param>
        /// <returns></returns>
        public BitmapSource LoadImageAsset(Mod mod, string initializingAssetName = null)
        {
            var assetData = mod.LoadModImageAsset(initializingAssetName ?? ImageAssetName);
#if !AZURE
            if (assetData == null)
            {
                M3Log.Error($@"Alternate {FriendlyName} lists image asset {initializingAssetName}, but the asset could not be loaded.");
                if (initializingAssetName != null)
                {
                    ValidAlternate = false;
                    LoadFailedReason = M3L.GetString(M3L.string_validation_alt_imageAssetArchiveError, FriendlyName, ImageAssetName);
                }
            }
            else
            {
                ImageBitmap = assetData;
            }
#else
            M3Log.Information($@"AZURE: Skipping image load of {initializingAssetName}");
#endif
            return assetData;
        }

        /// <summary>
        /// Reads the auto applicable text options.
        /// </summary>
        /// <param name="properties"></param>
        private void ReadAutoApplicableText(Dictionary<string, string> properties)
        {
            properties.TryGetValue(@"ApplicableAutoText", out string applicableText);
            ApplicableAutoText = applicableText ?? M3L.GetString(M3L.string_autoApplied);
            ApplicableAutoTextRaw = applicableText;

            properties.TryGetValue(@"NotApplicableAutoText", out string notApplicableText);
            NotApplicableAutoText = notApplicableText ?? M3L.GetString(M3L.string_notApplicable);
            NotApplicableAutoTextRaw = notApplicableText;
        }

        /// <summary>
        /// Reads the image asset options.
        /// </summary>
        /// <param name="modForValidating"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        private bool ReadImageAssetOptions(Mod modForValidating, Dictionary<string, string> properties)
        {
            if (modForValidating.ModDescTargetVersion >= 6.2)
            {
                if (properties.TryGetValue(@"ImageAssetName", out string imageAssetName) && !string.IsNullOrWhiteSpace(imageAssetName))
                {
                    // We need to validate the file exists
                    var iap = FilesystemInterposer.PathCombine(modForValidating.Archive != null, modForValidating.ModImageAssetsPath, imageAssetName);
                    if (!FilesystemInterposer.FileExists(iap, modForValidating.Archive))
                    {
                        M3Log.Error($@"Alternate {FriendlyName} lists image asset {imageAssetName}, but the asset does not exist in the mod's {Mod.ModImageAssetFolderName} directory.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_imageAssetNotFound, FriendlyName, ImageAssetName, Mod.ModImageAssetFolderName);
                        return false;
                    }


                    if (modForValidating.Archive != null)
                    {
                        // We need to load this asset cause it's not going to have an open archive until we begin install, if user tries to do install
                        ImageBitmap = LoadImageAsset(modForValidating, imageAssetName);
#if !AZURE
                        // If we're on Azure, the images will be blank stubs.
                        // In this case we do not want it to fail to load,
                        // since it always would
                        if (ImageBitmap == null)
                        {
                            return false; // Loading failed. 
                        }
#endif
                    }

                    ImageAssetName = imageAssetName;
                }

                if (!string.IsNullOrWhiteSpace(ImageAssetName))
                {
                    // We need to ensure height is also set
                    if (properties.TryGetValue(@"ImageHeight", out string imageHeightStr) && int.TryParse(imageHeightStr, out var imageHeight))
                    {
                        if (imageHeight < 0 || imageHeight > 1040)
                        {
                            M3Log.Error($@"Alternate {FriendlyName} lists image asset height {imageHeight}, but it is not within the valid values range. ImageHeight must be between 1 and 1039 inclusive.");
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_validation_alt_imageAssetOutOfRangeHeight, FriendlyName, imageHeight);
                            return false;
                        }

                        ImageHeight = imageHeight;
                    }
                    else
                    {
                        M3Log.Error($@"Alternate {FriendlyName} specifies an image asset but does not set (or have a valid value for) ImageHeight. ImageHeight is required to be set on alternates that specify an image asset.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_imageAssetMissingHeight, FriendlyName);
                        return false;
                    }
                }
            }

            return true; //Succeeded (or older moddesc that does not support this)
        }

        /// <summary>
        /// Reads options that are both applicable to AlternateDLC and AlternateFile objects
        /// </summary>
        /// <param name="modForValidating"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public bool ReadSharedOptions(Mod modForValidating, Dictionary<string, string> properties)
        {
            if (!ReadImageAssetOptions(modForValidating, properties))
            {
                return false; // Failed in super call
            }

            ReadAutoApplicableText(properties); // This can't fail validation

            // ModDesc 6.0: Mutually exclusive options
            if (modForValidating.ModDescTargetVersion >= 6.0)
            {
                GroupName = properties.TryGetValue(@"OptionGroup", out string groupName) ? groupName : null;
            }

            // ModDesc 8.0: Read dev-defined OptionKey, SortOrder, Hidden
            if (modForValidating.ModDescTargetVersion >= 8.0)
            {
                if (properties.TryGetValue(@"Hidden", out string hiddenValue))
                {
                    if (bool.TryParse(hiddenValue, out var hidden))
                    {
                        if (hidden && GroupName != null)
                        {
                            M3Log.Error($@"Alternate {FriendlyName} cannot set 'Hidden' to true when using 'OptionGroup'.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_alt_cannotUseHiddenWithOptionGroup, FriendlyName);
                            return false;
                        }
                        IsHidden = hidden;
                    }
                    else
                    {
                        M3Log.Error($@"Alternate {FriendlyName}'s 'Hidden' value can only be 'true' or 'false'. An invalid value was provided: {hiddenValue}");
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_invalidHiddenValue, FriendlyName, hiddenValue);
                        return false;
                    }
                }

                if (properties.TryGetValue(@"OptionKey", out string optionKey) && !string.IsNullOrWhiteSpace(optionKey))
                {
                    OptionKey = optionKey; // Later validation is done at the job level to ensure there are no collisions
                }

                if (properties.TryGetValue(@"DependsOnKeys", out string dependsOnKeys) && !string.IsNullOrWhiteSpace(dependsOnKeys))
                {
                    var keyList = dependsOnKeys.Split(';');
                    foreach (var key in keyList)
                    {
                        var dependskey = new PlusMinusKey(key);

                        // Must have +/-
                        if (!dependskey.IsPlus.HasValue)
                        {
                            M3Log.Error($@"Alternate {FriendlyName} has a value in its DependsOnKeys list that does not start with +/-: {key}. Values must start with a +/-.");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_alt_dependsKeysMissingPlusMinus, FriendlyName, key);
                            return false;
                        }

                        // Check if referencing self
                        if (dependskey.Key == OptionKey)
                        {
                            M3Log.Error($@"Alternate {FriendlyName} references itself in its own DependsOnKeys list, which is not supported. OptionKey value: {key}");
                            LoadFailedReason = M3L.GetString(M3L.string_validation_alt_dependsSelfReference, FriendlyName, key);
                            return false;
                        }

                        DependsOnKeys.Add(dependskey);
                    }


                    // THERE ARE TWO ALMOST DUPLICATE BLOCKS HERE
                    // BUT THEY DO DIFFERENT THINGS!
                    // ==========================================================
                    // We have to read criteria met and not met.
                    if (properties.TryGetValue(@"DependsOnMetAction", out string dependsOnMetStr) && Enum.TryParse<EDependsOnAction>(dependsOnMetStr, out var dependsOnMet) && dependsOnMet != EDependsOnAction.ACTION_INVALID)
                    {
                        DependsOnMetAction = dependsOnMet;
                    }
                    else
                    {
                        M3Log.Error($@"Alternate {FriendlyName} uses DependsOnKeys but does not define the DependsOnMetAction attribute. This attribute is required.");
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_dependsMissingAction, FriendlyName);
                        return false;
                    }

                    if (properties.TryGetValue(@"DependsOnNotMetAction", out string dependsOnNotMetStr) && Enum.TryParse<EDependsOnAction>(dependsOnNotMetStr, out var dependsOnNotMet) && dependsOnNotMet != EDependsOnAction.ACTION_INVALID)
                    {
                        DependsOnNotMetAction = dependsOnNotMet;
                    }
                    else
                    {
                        M3Log.Error($@"Alternate {FriendlyName} uses DependsOnKeys but does not define the DependsOnNotMetAction attribute. This attribute is required.");
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_dependsMissingNotMetAction, FriendlyName);
                        return false;
                    }
                    // ==========================================================
                }

                // Read the sorting index
                if (properties.TryGetValue(@"SortIndex", out string sortIndexStr))
                {
                    if (int.TryParse(sortIndexStr, out var sortIndex) && sortIndex > 0)
                    {
                        SortIndex = sortIndex;
                    }
                    else
                    {
                        // SortIndex collisions are also validated in a later pass
                        M3Log.Error($@"Alternate {FriendlyName} specifies an invalid 'sortindex' attribute: {sortIndexStr}. Valid values are greater than 0 and less than {int.MaxValue}");
                        LoadFailedReason = $@"Alternate {FriendlyName} specifies an invalid 'sortindex' attribute: {sortIndexStr}. Valid values are greater than 0 and less than {int.MaxValue}";
                        return false;
                    }
                }
            }

            // Mod Manager 8 (not moddesc 8): FriendlyName, OptionGroup can't contain ; (it will mess up MetaCMM):
            if (FriendlyName != null && FriendlyName.Contains(@";"))
            {
                M3Log.Error($@"Alternate attribute 'FriendlyName' cannot contain a ';' character. Invalid value: {FriendlyName}");
                LoadFailedReason = $@"Alternate attribute 'FriendlyName' cannot contain a ';' character. Invalid value: {FriendlyName}";
                ValidAlternate = false;
                return false;
            }

            if (GroupName != null && GroupName.Contains(@";"))
            {
                M3Log.Error($@"Alternate attribute 'GroupName' cannot contain a ';' character. Invalid value: {GroupName}");
                LoadFailedReason = $@"Alternate attribute 'GroupName' cannot contain a ';' character. Invalid value: {GroupName}";
                ValidAlternate = false;
                return false;
            }

            return true; //Succeeded (or older moddesc that does not support this)
        }

        /// <summary>
        /// Parameter map, used for the moddesc.ini editor Contains a list of values in the alternate mapped to their string value
        /// </summary>
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        /// <summary>
        /// Drops references to image assets so they can be garbage collected at a later time.
        /// </summary>
        public void ReleaseLoadedImageAsset()
        {
            ImageBitmap = null; //Lose the reference so we don't hold memory
        }

        /// <summary>
        /// Installs shared options into the parameter map for moddesc.ini editor
        /// </summary>
        /// <param name="parameterMap"></param>
        public void BuildSharedParameterMap(Mod mod, Dictionary<string, object> parameterMap)
        {
            var dependsActions = Enum.GetValues<EDependsOnAction>().Where(x => x != EDependsOnAction.ACTION_INVALID).Select(x => x.ToString()).Prepend("").ToList();

            var sharedMap = new Dictionary<string, object>()
            {
                // ConditionalDLC is not added here since it needs to appear next to Condition, which is class specific.

                // Basic metadata
                {@"FriendlyName", FriendlyName},
                {@"Description", Description},

                // Initially selected
                {@"CheckedByDefault", new MDParameter( @"CheckedByDefault", CheckedByDefault, false)},

                // Mutually exclusive groups
                {@"OptionGroup", GroupName},

                // Auto Apply text
                { @"ApplicableAutoText", ApplicableAutoTextRaw},
                { @"NotApplicableAutoText", NotApplicableAutoTextRaw},

                // Images
                // { @"ImageAssetName", ImageAssetName },
                {@"ImageAssetName", new MDParameter(@"string", @"ImageAssetName", ImageAssetName, new [] {@""}, "") { AllowedValuesPopulationFunc = mod.PopulateImageOptions}}, // Uses image population function
                { @"ImageHeight", ImageHeight > 0 ? ImageHeight.ToString() : null },

                // DependsOn
                { @"OptionKey", HasDefinedOptionKey ? OptionKey : null },
                { @"DependsOnKeys", string.Join(';', DependsOnKeys.Select(x => x.ToString())) },
                { @"DependsOnMetAction", new MDParameter(@"string", @"DependsOnMetAction", DependsOnMetAction != EDependsOnAction.ACTION_INVALID ? DependsOnMetAction.ToString() : "", dependsActions, "") }, // do not localize
                { @"DependsOnNotMetAction", new MDParameter(@"string", @"DependsOnNotMetAction", DependsOnNotMetAction != EDependsOnAction.ACTION_INVALID ? DependsOnNotMetAction.ToString() : "", dependsActions, "") }, // do not localize

                // Sorting
                { @"SortIndex", SortIndex > 0 ? SortIndex.ToString() : "" }, // If not defined don't put into map

                // DependsOn hidden - used for pivoting
                { @"Hidden", new MDParameter(@"Hidden", IsHidden, false) }, // do not localize

            };

            // Merge the shared map into the parameter map
            parameterMap.AddRange(sharedMap);
        }

        /// <summary>
        /// Configures the <see cref="DependsOnText"/> value. Additionally validates that the key exists.
        /// </summary>
        /// <param name="allAlternates">The list of all alternates that is used to resolve the text.</param>
        public bool SetupAndValidateDependsOnText(Mod modForValidating, List<AlternateOption> allAlternates)
        {
            if (DependsOnKeys.Count == 0)
                return true; // Do nothing, validated.

            string condition = "";
            foreach (var dependsOnKey in DependsOnKeys)
            {
                var alt = allAlternates.FirstOrDefault(x => x.OptionKey == dependsOnKey.Key);
                if (alt == null)
                {
                    M3Log.Error(M3L.GetString(M3L.string_validation_alt_dependsOnKeyReferencesMissingAlternate, FriendlyName, dependsOnKey.Key));
                    modForValidating.LoadFailedReason = M3L.GetString(M3L.string_validation_alt_dependsOnKeyReferencesMissingAlternate, FriendlyName, dependsOnKey.Key);
                    return false;
                }

                // If the user has a choice in configuring this option we should show the conditions required for the user to select the option
                //if (DependsOnMetAction != EDependsOnAction.ACTION_DISALLOW_SELECT && DependsOnMetAction != EDependsOnAction.ACTION_DISALLOW_SELECT_CHECKED)
                {
                    var isPlus = dependsOnKey.IsPlus.Value;

                    if (isPlus)
                    {
                        if (alt.GroupName != null)
                        {
                            condition += M3L.GetString(M3L.string_alt_groupRequiresDepends, alt.GroupName, alt.FriendlyName);
                        }
                        else
                        {
                            condition += M3L.GetString(M3L.string_alt_singularRequiresDepends, alt.FriendlyName);
                        }
                    }
                    else
                    {
                        if (alt.GroupName != null)
                        {
                            condition += M3L.GetString(M3L.string_alt_groupRequiresDependsNot, alt.GroupName, alt.FriendlyName);
                        }
                        else
                        {
                            condition += M3L.GetString(M3L.string_alt_singularRequiresDependsNot, alt.FriendlyName);
                        }
                    }
                }
            }

            //if (DependsOnMetAction != EDependsOnAction.ACTION_DISALLOW_SELECT && DependsOnMetAction != EDependsOnAction.ACTION_DISALLOW_SELECT_CHECKED)
            //{
            DependsOnText = condition.Trim();
            //}

            return true;
        }
    }
}
