using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Class that holds static variables related to scaling
    /// </summary>
    public static class DPIScaling
    {
        /// <summary>
        /// The scaling factor for objects that should use DPI-aware assets (such as images). 
        /// </summary>
        public static int ScalingFactor { get; private set; }

        public static void SetScalingFactor(Visual visual)
        {
            if (ScalingFactor > 0) return; // We already know it
            var dpiInfo = VisualTreeHelper.GetDpi(visual);

            ScalingFactor = (int) Math.Round(dpiInfo.DpiScaleY);
        }
    }
}
