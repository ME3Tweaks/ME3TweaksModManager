using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.converters
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
                    case "Other":
                        return game != MEGame.LELauncher && game != MEGame.UDK ? Visibility.Visible : Visibility.Collapsed;
                    case "OfficialDLC":
                        return game != MEGame.LELauncher && game != MEGame.UDK && game != MEGame.LE1 ? Visibility.Visible : Visibility.Collapsed;
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