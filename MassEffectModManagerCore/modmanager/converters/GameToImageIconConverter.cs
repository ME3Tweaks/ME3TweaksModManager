using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.ui;
using WinCopies.Util;

namespace MassEffectModManagerCore.modmanager.converters
{
    [Localizable(false)]
    public class GameToImageIconConverter : IValueConverter
    {
        private static string me1IconPath;
        private static string me2IconPath;
        private static string me3IconPath;
        private static string le1IconPath;
        private static string le2IconPath;
        private static string le3IconPath;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            init();
            float size = 48;
            if (parameter is float sizeF)
            {
                size = sizeF;
            }
            var game = (MEGame)value;
            switch (game)
            {
                case MEGame.ME1:
                    return MippedIconExtension.StaticConvert(me1IconPath, size);
                case MEGame.ME2:
                    return MippedIconExtension.StaticConvert(me2IconPath, size);
                case MEGame.ME3:
                    return MippedIconExtension.StaticConvert(me3IconPath, size);
                case MEGame.LE1:
                    return MippedIconExtension.StaticConvert(le1IconPath, size);
                case MEGame.LE2:
                    return MippedIconExtension.StaticConvert(le2IconPath, size);
                case MEGame.LE3:
                    return MippedIconExtension.StaticConvert(le3IconPath, size);
                default:
                    return null;
            }
        }

        private bool initialized;
        private void init()
        {
            if (initialized) return;
            me1IconPath = (string)Application.Current.Resources[@"me1gameiconpath"];
            me2IconPath = (string)Application.Current.Resources[@"me2gameiconpath"];
            me3IconPath = (string)Application.Current.Resources[@"me3gameiconpath"];
            le1IconPath = (string)Application.Current.Resources[@"le1gameiconpath"];
            le2IconPath = (string)Application.Current.Resources[@"le2gameiconpath"];
            le3IconPath = (string)Application.Current.Resources[@"le3gameiconpath"];
            initialized = true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}