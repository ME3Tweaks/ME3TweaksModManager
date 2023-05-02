using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Backend for MMStyles event handlers that are shared across the UI
    /// </summary>
    public partial class MMStyles : ResourceDictionary
    {
        public MMStyles()
        {
            InitializeComponent();
        }

        private void LoadImageAsset_TooltipOpening(object sender, System.Windows.Controls.ToolTipEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IM3ImageEnabled m3i)
            {
                if (m3i.ImageBitmap == null)
                {
                    // Load bitmap
                    var bitmap = m3i.ModdescMod.LoadModImageAsset(m3i.ImageAssetName);
                    if (bitmap != null)
                    {
                        m3i.ImageBitmap = bitmap;
                    }
                }
            }
        }
    }
}
