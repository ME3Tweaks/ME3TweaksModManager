using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.usercontrols.interfaces
{
    /// <summary>
    /// Standard interface for working with M3Images - an object can store a single image
    /// </summary>
    internal interface IM3ImageEnabled
    {
        /// <summary>
        /// The associated moddesc mod which is used to determine the filepath of the image
        /// </summary>
        public Mod ModdescMod { get; set; }

        /// <summary>
        /// Loaded image asset
        /// </summary>
        public BitmapSource ImageBitmap { get; set; }

        /// <summary>
        /// Asset name of the ImageAsset
        /// </summary>
        public string ImageAssetName { get; set; }
    }
}
