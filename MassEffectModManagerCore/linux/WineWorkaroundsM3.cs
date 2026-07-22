using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ME3TweaksModManager.linux
{
    /// <summary>
    /// Wine-specific workarounds for ME3TweaksModManager. These are things that are needed to make the app work properly under Wine.
    /// Changing WINE items through this class will also propogate into WineWorkarounds in ME3TweaksCore
    /// </summary>
    public static class WineWorkaroundsM3
    {
        /// <summary>
        /// Disables Wine Workarounds for M3 and ME3TweaksCore
        /// </summary>
        internal static void DisableWineWorkarounds()
        {
            WineWorkarounds.DisableWineWorkarounds();
            M3Log.Warning(@"Wine Workarounds (M3) Disabled");
        }

        /// <summary>
        /// Initializes Wine Workarounds for M3 and ME3TweaksCore
        /// </summary>
        internal static void Init()
        {
            WineWorkarounds.Init();
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            SetWineUIDefaults();
            M3Log.Information(@"Wine Workarounds (M3) Initialized");
        }

        /// <summary>
        /// Updates the default Font Family of M3
        /// </summary>
        /// <param name="fontName"></param>
        /// <returns></returns>
        public static bool UpdateFontFamily(string fontName)
        {
            return UpdateFontFamily(new FontFamily(fontName));
        }

        /// <summary>
        /// Updates the default Font Family of M3
        /// </summary>
        /// <param name="font"></param>
        /// <returns></returns>
        public static bool UpdateFontFamily(FontFamily font)
        {
            if (Application.Current.TryFindResource("M3DefaultFont") is FontFamily defaultFont)
            {
                Application.Current.Resources["M3DefaultFont"] = font;
                M3Log.Information($"Updating font to {font.Source}");
                return true;
            }
            else
            {
                M3Log.Warning($"Failed to set font to {font.Source}");
                return false;
            }
        }

        /// <summary>
        /// Sets defaults 
        /// </summary>
        private static void SetWineUIDefaults()
        {
            // Override defaults if Wine is detected
            if (WineWorkarounds.WineDetected)
            {
                if (Application.Current.TryFindResource(@"M3DefaultMenuItemMargin") is Thickness defMargin)
                {
                    // Hacky, would be nice to figure out how to get Wine to display margins (and padding) correctly
                    defMargin.Left = defMargin.Left / 2;
                    Application.Current.Resources[@"M3DefaultMenuItemMargin"] = defMargin;
                    M3Log.Information($@"Wine: Setting menu category left margin to {defMargin.Left}");
                }

                // Test if Segoe UI is available, if not, use Arial.
                // We use hardcoded string as the left side in case font.Source is null.
                var hasSegoeUi = Fonts.SystemFontFamilies.Any(font => @"Segoe UI".Equals(font.Source, StringComparison.OrdinalIgnoreCase));

                if (!hasSegoeUi)
                {
                    UpdateFontFamily(@"Arial");
                }
            }
        }
    }
}
