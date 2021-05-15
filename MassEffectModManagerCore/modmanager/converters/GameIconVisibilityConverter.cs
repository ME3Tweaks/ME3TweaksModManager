using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MassEffectModManagerCore.modmanager.objects;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.converters
{
    public class GameIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is GameTarget gt && gt.Game > MEGame.Unknown) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
