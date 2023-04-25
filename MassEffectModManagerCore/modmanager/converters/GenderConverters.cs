using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using FontAwesome5;

namespace ME3TweaksModManager.modmanager.converters
{
    public class GenderToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFemale)
            {
                return isFemale ? "Female" : "Male";
            }
            // Something is wrong, data should be a bool
            return "Undefined";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

    public class GenderToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFemale)
            {
                return isFemale ? EFontAwesomeIcon.Solid_Venus : EFontAwesomeIcon.Solid_Mars;
            }
            return EFontAwesomeIcon.Solid_Question;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

    public class GenderToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFemale)
            {
                return isFemale ? Colors.HotPink : Colors.DodgerBlue;
            }

            return Colors.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}