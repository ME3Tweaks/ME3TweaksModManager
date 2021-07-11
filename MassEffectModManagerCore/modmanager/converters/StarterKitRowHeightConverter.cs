using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MassEffectModManagerCore.modmanager.windows;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]

    public class StarterKitRowHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game)
            {
                bool inverted = false;
                string param = (string)parameter;
                if (param.IndexOf('_') > 0)
                {
                    var splitparms = param.Split('_');
                    inverted = splitparms.Any(x => x == "Not");
                    param = splitparms.Last();
                }

                if (Enum.TryParse(param, out MEGame parameterGame))
                {
                    if (inverted ^ parameterGame == game) return StarterKitGeneratorWindow.VisibleRowHeight;
                }
                else if (param.StartsWith("Game"))
                {
                    var gameId = param[^1];
                    switch (gameId)
                    {
                        case '1':
                            if (inverted ^ game.IsGame1()) return StarterKitGeneratorWindow.VisibleRowHeight;
                            break;
                        case '2':
                            if (inverted ^ game.IsGame2()) return StarterKitGeneratorWindow.VisibleRowHeight;
                            break;
                        case '3':
                            if (inverted ^ game.IsGame3()) return StarterKitGeneratorWindow.VisibleRowHeight;
                            break;

                    }
                }
            }

            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
