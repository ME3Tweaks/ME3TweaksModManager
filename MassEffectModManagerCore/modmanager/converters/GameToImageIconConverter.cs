using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

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
        private static string leLauncherIconPath;
        private static string unknownIconPath;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            init();
            float size = 48;
            if (parameter is float sizeF)
            {
                size = sizeF;
            }
            else if (parameter is int sizeI)
            {
                size = sizeI;
            }
            else if (parameter is string sizeS)
            {
                size = float.Parse(sizeS); // WPF likes to do things as strings
            }
            else if (parameter != null)
            {
                Debug.WriteLine($"SPECIFIED MIPPED ICON SIZE IS NOT A FLOAT: {parameter}");
            }

            MEGame game = MEGame.Unknown;
            if (value is MEGameSelector sel) game = sel.Game;
            else if (value is MEGame mel) game = mel;
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
                case MEGame.LELauncher:
                    return MippedIconExtension.StaticConvert(leLauncherIconPath, size);
                case MEGame.Unknown:
                    return MippedIconExtension.StaticConvert(unknownIconPath, size);
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
            leLauncherIconPath = (string)Application.Current.Resources[@"lelaunchergameiconpath"];
            unknownIconPath = (string)Application.Current.Resources[@"unknowngameiconpath"];
            initialized = true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}