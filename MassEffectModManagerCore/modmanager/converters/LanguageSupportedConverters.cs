using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class LanguageSupportedVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string lang)
            {
                return App.IsLanguageSupported(lang) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Set up incorrectly.
            Debug.WriteLine($@"Incorrect setup for {nameof(LanguageSupportedVisibilityConverter)}!");
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
