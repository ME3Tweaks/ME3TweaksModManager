using Dark.Net;
using ME3TweaksModManager.modmanager;
using System.Windows;

namespace ME3TweaksModManager
{
    public static class WindowExtensions
    {
        public static void ApplyDarkNetWindowStyle(this Window window)
        {
            if (Settings.SkipDarkNetHandling)
            {
                return;
            }

            DarkNet.Instance.SetWindowThemeWpf(window, Settings.DarkTheme ? Theme.Dark : Theme.Light);
        }
    }
}
