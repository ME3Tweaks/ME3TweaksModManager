using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.modmanager.converters
{
    /// <summary>
    /// Converts a TargetCacheInfo object to a Brush for colorizing the status text.
    /// Returns a success brush for valid targets and an error brush for invalid targets.
    /// </summary>
    [Localizable(false)]
    [ValueConversion(typeof(TargetCacheInfo), typeof(Brush))]
    public class TargetStatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TargetCacheInfo targetInfo)
                return Application.Current.FindResource(AdonisUI.Brushes.ForegroundBrush);

            // White text for foreground brush.
            if (targetInfo.IsBackup)
                return Application.Current.FindResource(AdonisUI.Brushes.ForegroundBrush);

            return targetInfo.IsValid
                ? Application.Current.FindResource(AdonisUI.Brushes.SuccessBrush)
                : Application.Current.FindResource(AdonisUI.Brushes.ErrorBrush);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
