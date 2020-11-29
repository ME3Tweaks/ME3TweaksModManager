using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Media.Imaging;
using MassEffectModManagerCore.modmanager.objects.mod;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{
    public abstract class AlternateOption : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
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

        public bool IsSelected { get; set; }
        public virtual bool UIRequired => !IsManual && !IsAlways && IsSelected;
        public abstract bool UINotApplicable { get; }
        public abstract bool UIIsSelectable { get; set; }
        public abstract bool IsAlways { get; }
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
                Log.Error($@"Alternate {FriendlyName} lists image asset {initializingAssetName}, but the asset could not be loaded.");
                if (initializingAssetName != null)
                {
                    ValidAlternate = false;
                    LoadFailedReason = $"Alternate {FriendlyName} lists image asset {ImageAssetName}, but the asset could not be read from the archive. The log will contain additional information.";
                }
            }
            else
            {
                ImageBitmap = assetData;
            }

            return assetData;
        }

        public ObservableCollection<AlternateOption.Parameter> ParameterMap { get; } = new ObservableCollection<AlternateOption.Parameter>();

        /// <summary>
        /// Parameter for the alternate. Used in the editor, because we don't have bindable dictionary
        /// </summary>
        public class Parameter
        {
            public Parameter()
            {

            }
            public Parameter(string key, string value)
            {
                Key = key;
                Value = value;
            }

            // This class exists cause we can't bind to a dictionary
            public string Key { get; set; }
            public string Value { get; set; }
        }

        public void ReleaseLoadedImageAsset()
        {
            ImageBitmap = null; //Lose the reference so we don't hold memory
        }
    }
}
