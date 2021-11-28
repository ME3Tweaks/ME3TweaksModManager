using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class ImageTooltipVerticalOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int imageHeight)
            {
                return (-imageHeight / 2) + 15; // We add 15 cause it's aligned to the top of the alternate option grid item. This isn't perfect but it'll do well enough
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}