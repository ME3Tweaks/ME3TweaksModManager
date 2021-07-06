using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.modmanager.converters
{
    public class BootTargetSourceVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is GameTarget gt /* && gt.RegistryActive*/) return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
