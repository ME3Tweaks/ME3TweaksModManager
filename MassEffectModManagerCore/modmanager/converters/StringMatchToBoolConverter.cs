using System;
using System.Globalization;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    public class StringMatchToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && parameter is string expectedValue)
            {
                if (value.ToString() == expectedValue)
                    return true;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
