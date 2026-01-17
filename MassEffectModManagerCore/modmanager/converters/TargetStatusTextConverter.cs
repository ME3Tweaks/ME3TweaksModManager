using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.modmanager.converters
{
    /// <summary>
    /// Converts a TargetCacheInfo object to a status text string that describes the target's state.
    /// The text includes validity status and additional flags like "Backup" and "Registry Active".
    /// </summary>
    [ValueConversion(typeof(TargetCacheInfo), typeof(string))]
    public class TargetStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TargetCacheInfo targetInfo)
                return string.Empty;

            var statusText = targetInfo.IsValid ? "Valid" : "Invalid";

            if (targetInfo.IsLinkedBackup)
            {
                statusText = "Linked backup";
            }
            else if (targetInfo.IsBackup)
            {
                statusText = "Unlinked backup";
            }
            else if (targetInfo.Target?.RegistryActive == true)
            {
                // Append additional flags
                statusText += @" • ";
                statusText += "Autodetected";
            }

            return statusText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
