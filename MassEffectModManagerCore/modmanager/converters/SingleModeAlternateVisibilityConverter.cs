using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    public class SingleModeAlternateVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 2)
            {
#if DEBUG
                Debug.WriteLine($@"IMPROPERLY USED CONVERTER: SINGLE MODE ALTERNATE VISIBILITY CONVERTER");
#endif
                return Visibility.Collapsed;
            }

            // Param 0: GroupName STRING
            // Param 1: Hidden BOOL

            // We check for not null first because if parameter is wrong type, just checking 'is string' will return false even if it's set to not null (e.g. we pass wrong param, like an integer)
            if (values[0] != null && values[0] is string) return Visibility.Visible; // groups are always visible 
            if (values[1] is bool hidden && hidden) return Visibility.Collapsed; // if hidden we hide

            return Visibility.Visible; // default is show
        }

        public object[] ConvertBack(object value,
            Type[] targetTypes,
            object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
