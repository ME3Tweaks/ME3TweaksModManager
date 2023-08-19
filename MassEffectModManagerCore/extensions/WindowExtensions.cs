using System.Diagnostics;
using System.Drawing;
using Dark.Net;
using ME3TweaksModManager.modmanager;
using System.Windows;

namespace ME3TweaksModManager.extensions
{
    public static class WindowExtensions
    {
        /// <summary>
        /// Converts a media color to a drawing color
        /// </summary>
        /// <param name="mediaColor"></param>
        /// <returns></returns>
        private static Color ToDrawingColor(this System.Windows.Media.Color mediaColor)
        {
            return Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

        public static void ApplyDarkNetWindowTheme(this Window window)
        {
            if (Settings.SkipDarkNet)
            {
                return;
            }

            var titleColor = (System.Windows.Media.Color)window.FindResource(AdonisUI.Colors.Layer0BackgroundColor);
            var textColor = (System.Windows.Media.Color)window.FindResource(AdonisUI.Colors.ForegroundColor);
            var borderColor = (System.Windows.Media.Color)window.FindResource(AdonisUI.Colors.Layer1BorderColor);


            DarkNet.Instance.SetWindowThemeWpf(window, Settings.DarkTheme ? Theme.Dark : Theme.Light, new ThemeOptions()
            {
                TitleBarBackgroundColor = titleColor.ToDrawingColor(),
                TitleBarTextColor = textColor.ToDrawingColor(),
                WindowBorderColor = borderColor.ToDrawingColor(),
            });
        }
    }
}
