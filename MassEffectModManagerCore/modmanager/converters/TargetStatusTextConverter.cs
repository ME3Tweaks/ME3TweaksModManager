using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

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

            var statusText = targetInfo.IsValid ? M3L.GetString(M3L.string_valid) : M3L.GetString(M3L.string_invalid);

            if (targetInfo.IsLinkedBackup)
            {
                statusText = M3L.GetString(M3L.string_linkedBackup);
            }
            else if (targetInfo.IsBackup)
            {
                statusText = M3L.GetString(M3L.string_unlinkedBackup);
            }
            else if (targetInfo.Target?.RegistryActive == true)
            {
                // Append additional flags
                statusText += @" • ";
                statusText += M3L.GetString(M3L.string_autodetected);
            }

            return statusText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
