using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class NotNullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverted = parameter is string str && str == "Not";
            bool retval = value != null;
            if (inverted) return !retval;
            return retval;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
