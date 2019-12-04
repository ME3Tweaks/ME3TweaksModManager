using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using MassEffectModManagerCore.modmanager.windows;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]

    public class StarterKitRowHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverted = false;
            string param = (string)parameter;
            if (param.IndexOf('_') > 0)
            {
                var splitparms = param.Split('_');
                inverted = splitparms.Any(x => x == "Not");
                param = splitparms.Last();
            }
            if (Enum.TryParse(param, out Mod.MEGame parameterGame))
            {
                if (inverted ^ parameterGame == (Mod.MEGame)value) return StarterKitGeneratorWindow.VisibleRowHeight;
            }

            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
