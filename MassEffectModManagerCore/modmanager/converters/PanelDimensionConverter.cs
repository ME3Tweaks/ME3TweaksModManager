using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.converters
{
    public class PanelDimensionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 2)
                return double.NaN; // Default I guess

            if (values[0] is SingleItemPanel2 sip2 && sip2.Content is MMBusyPanelBase panel && values[1] is double windowDimension && parameter is string axis)
            {
                var window = panel.window;
                Size windowSize = new Size(window.ActualWidth, window.ActualHeight);
                
                panel.Measure(windowSize);
                var panelDesiredSize = panel.DesiredSize; // Requested content size.
                var maxWindowDimension = windowDimension * 0.8;
                var panelDesiredDimension = axis == @"W" ? panelDesiredSize.Width : panelDesiredSize.Height;
                
                return Math.Min(panelDesiredDimension, maxWindowDimension); // If the desired size is less than the max window dimension, use that. Otherwise use the maximum.
            }

            return double.NaN; // Not compatible.

            //if (values[0] is double elementDimension && values[1] is double windowDimension && values[2] is double windowDimensionMaxPercent)
            //{
            //    var maxWindowSize = windowDimension * windowDimensionMaxPercent;
            //    var value = Math.Min(elementDimension, maxWindowSize) + 1; // Margins are 8,8,8,8. This is kind of a hack but I doubt this will change.
            //    Debug.WriteLine($@"PanelDimensionConverter: {elementDimension} vs {maxWindowSize}. Result: {value}");
            //    return value;
            //}
            //return 20f;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
