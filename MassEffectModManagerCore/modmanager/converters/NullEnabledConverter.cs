using System;
using System.Globalization;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    public class NullEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}