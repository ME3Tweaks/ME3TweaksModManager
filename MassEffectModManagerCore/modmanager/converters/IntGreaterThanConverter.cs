using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class IntGreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramString && int.TryParse(paramString, out int compareValue))
            {
                return intValue > compareValue;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
