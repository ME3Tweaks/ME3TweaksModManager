using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.converters
{

    [Localizable(false)]
    public class ButtonHighlightOnOffStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var highlightStyle = (bool)value;
            foreach (var res in Application.Current.Resources.MergedDictionaries)
            {
                foreach (var res2 in res.Keys)
                {
                    Debug.WriteLine(res2.ToString());
                }
            }
            var resources = Application.Current.FindResource("AccentButton");
            return App.Current.Resources[""];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null; //not back
        }

    }
}