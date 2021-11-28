using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string locKey)
            {
                return M3L.GetString(locKey, value?.ToString());
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
