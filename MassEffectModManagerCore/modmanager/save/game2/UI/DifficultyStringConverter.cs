using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;

namespace ME3TweaksModManager.modmanager.save.game2.UI
{
    [Localizable(false)]
    public class DifficultyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DifficultyOptions dl)
            {
                return dl switch
                {
                   DifficultyOptions.Level1 => "Casual",
                   DifficultyOptions.Level2 => "Normal",
                   DifficultyOptions.Level3 => "Veteran",
                   DifficultyOptions.Level4 => "Hardcore",
                   DifficultyOptions.Level5 => "Insanity",
                   DifficultyOptions.Level6 => "Debug",
                   _ => null
                };
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}