using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Class that enables/disables certain features based on the manifest
    /// </summary>
    public static class Flighting
    {
        /// <summary>
        /// Tests if a feature by name is enabled or not
        /// </summary>
        /// <param name="featurename"></param>
        /// <returns></returns>
        public static bool IsFeatureEnabled(string featurename)
        {
            if (App.ServerManifest != null)
            {
                if (App.ServerManifest.TryGetValue($"flighting_{featurename}", out var enabled) && bool.TryParse(enabled, out var enabledVal))
                {
                    return enabledVal;
                }
            }
            return false;
        }
    }

    public class FlightingVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string str)
            {
                return Flighting.IsFeatureEnabled(str) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
