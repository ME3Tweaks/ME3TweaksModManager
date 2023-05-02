using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Unreal;

namespace ME3TweaksModManager.modmanager.save.game2.UI
{
    [Localizable(false)]
    public class SaveGameNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ISaveFile sf)
            {
                return StaticConvert(sf);
            }

            return null; // Setup wrong!
        }

        public static string StaticConvert(ISaveFile sf)
        {
            switch (sf.SaveGameType)
            {
                case ESFXSaveGameType.SaveGameType_Auto:
                    return "Auto Save";
                case ESFXSaveGameType.SaveGameType_Quick:
                    return "Quick Save";
                case ESFXSaveGameType.SaveGameType_Chapter:
                    return "Restart Mission";
                case ESFXSaveGameType.SaveGameType_Manual:
                    return $"Save {sf.SaveNumber}";
            }

            return $"Unknown save type: {sf.SaveGameType}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}