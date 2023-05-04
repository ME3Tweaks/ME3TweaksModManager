using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Unreal;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save;

namespace ME3TweaksModManager.modmanager.converters
{
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
                    return M3L.GetString(M3L.string_autoSave);
                case ESFXSaveGameType.SaveGameType_Quick:
                    return M3L.GetString(M3L.string_quickSave);
                case ESFXSaveGameType.SaveGameType_Chapter:
                    return M3L.GetString(M3L.string_restartMission);
                case ESFXSaveGameType.SaveGameType_Manual:
                    return M3L.GetString(M3L.string_interp_saveX, sf.SaveNumber);
                case ESFXSaveGameType.SaveGameType_Export:
                    return M3L.GetString(M3L.string_exportSave);
                case ESFXSaveGameType.SaveGameType_Legend:
                    return M3L.GetString(M3L.string_legendSave);
            }

            return M3L.GetString(M3L.string_interp_unknownSaveTypeX, sf.SaveGameType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}