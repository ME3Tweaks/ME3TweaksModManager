using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]
    public class GameToTextureModdedIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame g)
            {
                if (g.IsOTGame()) return "/modmanager/toolicons/alot_32.png";
                if (g.IsLEGame()) return "/modmanager/toolicons/masseffectmodder_32.png";
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}