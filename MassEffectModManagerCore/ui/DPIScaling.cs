using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Win32;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Class that holds static variables related to scaling
    /// </summary>
    public static class DPIScaling
    {
        /// <summary>
        /// The scaling factor for objects that should use DPI-aware assets (such as images). 
        /// </summary
        public static int ScalingFactor { get; private set; }

        /// <summary>
        /// The Windows text scale factor (from Accessibility settings). Value is decimal (1.0 = 100%, 1.25 = 125%, etc.)
        /// </summary>
        public static double TextScaleFactor { get; private set; } = 1.0;

        public static void SetScalingFactor(Visual visual)
        {
            if (ScalingFactor > 0) return; // We already know it
            var dpiInfo = VisualTreeHelper.GetDpi(visual);

            ScalingFactor = (int) Math.Round(dpiInfo.DpiScaleY);

            // Also read the text scale factor when setting up scaling
            ReadTextScaleFactor();
        }

        /// <summary>
        /// Reads the Windows Text Scale Factor from the registry
        /// </summary>
        private static void ReadTextScaleFactor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
                if (key != null)
                {
                    var value = key.GetValue(@"TextScaleFactor");
                    if (value is int textScaleFactorInt)
                    {
                        // The registry value is stored as an integer representing percentage (100 = 100%, 125 = 125%, etc.)
                        TextScaleFactor = textScaleFactorInt / 100.0;
                    }
                }
            }
            catch
            {
                // If we can't read the registry, default to 1.0 (no scaling)
                TextScaleFactor = 1.0;
            }
        }
    }
}
