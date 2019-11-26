using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.converters
{
    public class NullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && parameter is string str)
            {
                if (str == "Reversed")
                {
                    return value != null ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

    public class NullHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && parameter is string str)
            {
                if (str == "Reversed")
                {
                    return value != null ? Visibility.Hidden : Visibility.Visible;
                }
            }
            return value == null ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
