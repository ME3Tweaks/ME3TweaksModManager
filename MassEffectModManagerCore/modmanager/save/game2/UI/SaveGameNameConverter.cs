using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LegendaryExplorerCore.Unreal;
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;
using MassEffectModManagerCore.modmanager.save.game3;

namespace MassEffectModManagerCore.modmanager.save.game2.UI
{
    [Localizable(false)]
    public class SaveGameNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ISaveFile sf)
            {
                switch (sf.SaveGameType)
                {
                    case ESFXSaveGameType.SaveGameType_Auto:
                        return "Auto Save";
                    case ESFXSaveGameType.SaveGameType_Quick:
                        return "Auto Save";
                    case ESFXSaveGameType.SaveGameType_Chapter:
                        return "Restart Mission";
                    case ESFXSaveGameType.SaveGameType_Manual:
                        return $"Save {sf.SaveNumber}";
                }
            }

            return "Dunno man...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}