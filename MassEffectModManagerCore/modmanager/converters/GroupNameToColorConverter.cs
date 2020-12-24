using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]
    public class GroupNameToColorConverter : IValueConverter
    {
        private static SolidColorBrush[] GroupBrushesLight =
        {
            Brushes.Brown,
            Brushes.DarkMagenta,
            Brushes.DarkBlue,
            Brushes.SeaGreen,
            Brushes.OliveDrab,
            Brushes.DarkGoldenrod
        };

        private static SolidColorBrush[] GroupBrushesDark =
        {
            Brushes.LightBlue,
            Brushes.Cyan,
            Brushes.Gold,
            Brushes.Chartreuse,
            Brushes.Tomato,
            Brushes.LightSeaGreen
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                // Calculate a 'hash' of the name of the string
                int sum = 0;
                foreach (var c in str)
                {
                    sum += (int) c;
                }

                return Settings.DarkTheme ? GroupBrushesDark[sum % GroupBrushesDark.Length] : GroupBrushesLight[sum % GroupBrushesLight.Length];

            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
