using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Misc;
using MassEffectModManagerCore.modmanager.diagnostics;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.objects
{
    public abstract class AlternateOption : INotifyPropertyChanged, IMDParameterMap
    {
        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        public string ApplicableAutoText { get; private set; }
        public string NotApplicableAutoText { get; private set; }
        /// <summary>
        /// For moddesc.ini editor only
        /// </summary>
        public string ApplicableAutoTextRaw { get; private set; }
        /// <summary>
        /// For moddesc.ini editor only
        /// </summary>
        public string NotApplicableAutoTextRaw { get; private set; }
        public virtual bool CheckedByDefault { get; internal set; }
        public abstract bool IsManual { get; }
        public virtual double CheckboxOpacity => (!UIIsSelectable) ? .5 : 1;
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
        /// The MultilistId for this alternate. This is only used in the moddesc.ini editor as
        /// the list of files is built during loading of the alternate
        /// </summary>
        public int MultiListId { get; set; } = -1;
        public bool IsSelected { get; set; }
        public virtual bool UIRequired => !IsManual && !IsAlways && IsSelected;
        public abstract bool UINotApplicable { get; }
        [SuppressPropertyChangedWarnings]
        public abstract bool UIIsSelectable { get; set; }
        public abstract bool IsAlways { get; }
        public abstract void BuildParameterMap(Mod mod);
        public virtual string GroupName { get; internal set; }
        public virtual string FriendlyName { get; internal set; }
        public virtual string Description { get; internal set; }
        /// <summary>
        /// If this alternate is valid or not
        /// </summary>
        public bool ValidAlternate;
        /// <summary>
        /// Why this alternate is invalid
        /// </summary>
        public string LoadFailedReason;
        /// <summary>
        /// Names of the attached image asset for this option.
        /// </summary>
        public virtual string ImageAssetName { get; internal set; }
        /// <summary>
        /// The loaded image asset
        /// </summary>
        public BitmapSource ImageBitmap { get; set; }

        /// <summary>
        /// The height in pixels (resolution independent) of the image to show. The aspect ratio is preserved, so this will automatically product the width.
        /// </summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// Loads the image asset for the specified mod. If this method is called on an archive based mod, it must be done while the archive is still open.
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="initializingAssetName"></param>
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

        public void ReadAutoApplicableText(Dictionary<string, string> properties)
        {
            properties.TryGetValue(@"ApplicableAutoText", out string applicableText);
            ApplicableAutoText = applicableText ?? M3L.GetString(M3L.string_autoApplied);
            ApplicableAutoTextRaw = applicableText;

            properties.TryGetValue(@"NotApplicableAutoText", out string notApplicableText);
            NotApplicableAutoText = notApplicableText ?? M3L.GetString(M3L.string_notApplicable);
            NotApplicableAutoTextRaw = notApplicableText;
        }

        public bool ReadImageAssetOptions(Mod modForValidating, Dictionary<string, string> properties)
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
        /// Parameter map, used for the moddesc.ini editor Contains a list of values in the alternate mapped to their string value
        /// </summary>
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        public void ReleaseLoadedImageAsset()
        {
            ImageBitmap = null; //Lose the reference so we don't hold memory
        }
    }
}
