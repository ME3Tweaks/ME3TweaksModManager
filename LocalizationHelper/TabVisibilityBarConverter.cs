using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LocalizationHelper
{
    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class TabVisibilityBarConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int cIndex && parameter is string str && int.TryParse(str, out var index))
            {
                return index == cIndex ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Don't need any convert back
            return null;
        }
    }
}