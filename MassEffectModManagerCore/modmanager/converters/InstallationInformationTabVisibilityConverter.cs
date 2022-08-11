using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class InstallationInformationTabVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game && parameter is string tabName)
            {
                switch (tabName)
                {
                    case "DLCMods":
                    case "Textures":
                        return game != MEGame.LELauncher && game != MEGame.UDK ? Visibility.Visible : Visibility.Collapsed;
                    case "Other":
                        // Since we dont have a LauncherDirectory class we don't have list of vanilla Dlls. 
                        // So we don't actually show the Other tab.
                        return game != MEGame.LELauncher && game != MEGame.UDK ? Visibility.Visible : Visibility.Collapsed;
                    case "OfficialDLC":
                        return game.IsOTGame() ? Visibility.Visible : Visibility.Collapsed;
                    case "SFAR":
                        return game == MEGame.ME3 ? Visibility.Visible : Visibility.Collapsed;
                    case "Basegame":
                        return game != MEGame.UDK ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}