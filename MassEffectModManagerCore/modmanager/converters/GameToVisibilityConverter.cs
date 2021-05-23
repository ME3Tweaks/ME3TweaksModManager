using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]
    public class GameToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game && parameter is string gameStr)
            {
                bool inverted = false;
                if (gameStr.IndexOf('_') > 0)
                {
                    var splitparms = gameStr.Split('_');
                    inverted = splitparms.Any(x => x == "Not");
                    gameStr = splitparms.Last();
                }
                if (Enum.TryParse(gameStr, out MEGame parameterGame))
                {
                    if (inverted ^ parameterGame == game) return Visibility.Visible;
                }
                else if (gameStr.StartsWith("Game"))
                {
                    var gameId = gameStr[^1];
                    switch (gameId)
                    {
                        case '1':
                            if (inverted ^ game.IsGame1()) return Visibility.Visible;
                            break;
                        case '2':
                            if (inverted ^ game.IsGame2()) return Visibility.Visible;
                            break;
                        case '3':
                            if (inverted ^ game.IsGame3()) return Visibility.Visible;
                            break;
                    }
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}