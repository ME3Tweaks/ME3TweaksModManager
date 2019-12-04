using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]
    public class GameToImageIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var game = (Mod.MEGame) value;
            if (game == Mod.MEGame.Unknown) { return null; }
            return "/images/gameicons/" + game.ToString() + "_48.ico";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}