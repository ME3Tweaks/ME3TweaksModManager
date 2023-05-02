using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Helpers;

namespace ME3TweaksModManager.modmanager.converters
{
    public class StarterKitAddinAvailableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game && parameter is MEGame[] supportedGames)
            {
                return supportedGames.Contains(game) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}