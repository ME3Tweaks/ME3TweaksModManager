using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string str && (str == "Inverse" || str == "Not"))
            {
                return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
            }
            return ((bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

    /// <summary>
    /// Checks that all bools are true to set visibility to visible
    /// </summary>
    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]

    public class MultiBoolToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var allBools = values.OfType<bool>().ToList();
            if (values.Length != allBools.Count)
            {
                Debug.WriteLine(@"Incorrectly setup MultiBoolToVisibilityConverter");
                return Visibility.Collapsed;
            }
            if (parameter is string str && (str is "Inverse" or "Not"))
            {
                return allBools.All(x => x) ? Visibility.Collapsed : Visibility.Visible;
            }
            return allBools.All(x => x) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToHiddenVisibleConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string str && (str == "Inverse" || str == "Not"))
            {
                return ((bool)value) ? Visibility.Hidden : Visibility.Visible;
            }
            return ((bool)value) ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }

}
