using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
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

        #region UI-Specific
        /// <summary>
        /// UI-only
        /// </summary>
        public virtual double CheckboxOpacity => !UIIsSelectable ? .5 : 1;

        /// <summary>
        /// UI-only
        /// </summary>
        public virtual double TextOpacity
        {
            get
            {
                if (!UIIsSelectable && IsSelected) return 1;
                if (!UIIsSelectable && !IsSelected) return .5;
                return 1;
            }
        }

        /// <summary>
        /// Raises the IsSelectedChanged event.
        /// </summary>
        internal void RaiseIsSelectedChanged()
        {
            IsSelectedChanged?.Invoke(this, new DataEventArgs(IsSelected));
        }

        /// <summary>
        /// If this option is required (automatic and checked). This does not apply if the condition is OP_ALWAYS. 
        /// </summary>
        public virtual bool UIRequired => !IsManual && !IsAlways && IsSelected;

        /// <summary>
        /// If this option can be selected on or off by the end-user
        /// </summary>
        public virtual bool UIIsSelectable { get; set; }

        //public abstract bool UINotApplicable { get; }
        /// <summary>
        /// If this option is not applicable to the installation
        /// </summary>
        public virtual bool UINotApplicable => !IsManual && !IsSelected && !IsAlways;

        #endregion

        /// <summary>
        /// The MultilistId for this alternate. This is only used in the moddesc.ini editor as
        /// the list of files is built during loading of the alternate
        /// </summary>
        public int MultiListId { get; set; } = -1;

        /// <summary>
        /// If this option is selected for installation
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// If this option is always selected and forced on. Used to put an option that always is checked, often with OP_NOTHING.
        /// </summary>
        public abstract bool IsAlways { get; }
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
                var data = Encoding.Unicode.GetBytes(FriendlyName);
                ParallelCRC crc = new ParallelCRC();
                crc.Update(data, 0, data.Length);
                _optionKey = crc.Value.ToString("X8");
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
        /// <param name="allOptions"></param>
        /// <returns>True if the selection state was changed; false if not. This is used to determine if there needs to be another call to update selections again</returns>
        internal virtual bool UpdateSelectability(IEnumerable<AlternateOption> allOptions)
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

                var option = allOptions.FirstOrDefault(x => x.OptionKey == key.Key);
                if (option == null)
                {
                    // This shouldn't happen!
                    Crashes.TrackError(new Exception($@"DependsOnKey not found in list of all options: {key}! This shouldn't happen."));
                    continue;
                }

                if (option.IsSelected && !key.IsPlus.Value)
                {
                    // The DependsOnKey option is selected, but we need -
                    UIIsSelectable = false;
                    if (IsSelected) changed = true; // We are changing states
                    IsSelected = false;
                    keepParsing = false;
                }
                else if (!option.IsSelected && key.IsPlus.Value)
                {
                    // The DependsOnKey option is not selected, but we need +
                    UIIsSelectable = false;
                    if (IsSelected) changed = true; // We are changing states
                    IsSelected = false;
                    keepParsing = false;
                }
                else if (!option.IsSelected ^ key.IsPlus.Value) 
                {
                    // Unlock the option
                    UIIsSelectable = true;
                }

                // If we are an option group, we reset to the default option
                if (GroupName != null && changed)
                {
                    var defaultOption = allOptions.FirstOrDefault(x => x.CheckedByDefault && x.GroupName == GroupName);
                    defaultOption.IsSelected = true;
                }
            }



            return changed;
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
                        M3Log.Error($@"Alternate file {FriendlyName} lists image asset {imageAssetName}, but the asset does not exist in the mods {Mod.ModImageAssetFolderName} directory.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_validation_alt_imageAssetNotFound, FriendlyName, ImageAssetName, Mod.ModImageAssetFolderName);
                        return false;
                    }


                    if (modForValidating.Archive != null)
                    {
                        // We need to load this asset cause it's not going to have an open archive until we begin install, if user tries to do install
                        ImageBitmap = LoadImageAsset(modForValidating, imageAssetName);
                        if (ImageBitmap == null)
                        {
                            return false; // Loading failed. 
                        }
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

            // ModDesc 8.0: Read dev-defined OptionKey
            if (modForValidating.ModDescTargetVersion >= 8.0)
            {
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
                            LoadFailedReason = $"Alternate {FriendlyName} has a value in its DependsOnKeys list that does not start with +/-: {key}. Values must start with a +/-.";
                            return false;
                        }

                        // Check if referencing self
                        if (dependskey.Key == OptionKey)
                        {
                            M3Log.Error($@"Alternate {FriendlyName} references itself in its own DependsOnKeys list, which is not supported. Key value: {key}");
                            LoadFailedReason = $"Alternate {FriendlyName} references itself in its own DependsOnKeys list, which is not supported. Key value: {key}";
                            return false;
                        }

                        DependsOnKeys.Add(dependskey);
                    }
                }
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
    }
}
