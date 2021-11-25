using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

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