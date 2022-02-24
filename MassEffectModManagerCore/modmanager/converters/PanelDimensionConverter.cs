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
    public class PanelDimensionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 3)
                return 400; // Default I guess

            if (values[0] is double elementDimension && values[1] is double windowDimension && values[2] is double windowDimensionMaxPercent)
            {
                var maxWindowSize = windowDimension * windowDimensionMaxPercent;
                return Math.Min(elementDimension, maxWindowSize) + 2; // Margins are 8,8,8,8. This is kind of a hack but I doubt this will change.
            }
            return 20;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
