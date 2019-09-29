using System;
using System.Globalization;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.converters
{
    public class BoolEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverted = parameter is string str && str == "Not";
            bool retval = (bool) value;
            if (inverted) return !retval;
            return retval;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
