using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.converters
{
    public class PanelDimensionConverter : IMultiValueConverter
    {
        /// <summary>
        /// The max window dimension default
        /// </summary>
        private static readonly double defaultMaxDimensionMultiplier = 0.8;

        /// <summary>
        /// The 'MaxWidth/MaxHeight' value.
        /// </summary>
        private static readonly double defaultMaxDimensionValue = double.NaN;


        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 3)
                return double.NaN; // Default I guess

            if (values[0] is SingleItemPanel2 sip2 && sip2.Content is MMBusyPanelBase panel && !panel.DisableM3AutoSizer && values[1] is double windowDimension && parameter is string axis)
            {
                var window = panel.window;
                Size windowSize = new Size(window.ActualWidth, window.ActualHeight);
                panel.Measure(windowSize); // Update DesiredSize.

                bool isWidth = axis == @"W";
                var panelDesiredDimension = isWidth ? panel.DesiredSize.Width : panel.DesiredSize.Height;

                if (panel.MaxWindowHeightPercent > 0 || panel.MaxWindowWidthPercent > 0)
                {
                    // This panel has specific limits set on it.
                    var maxWindowDimension = windowDimension * (isWidth ? panel.MaxWindowWidthPercent : panel.MaxWindowHeightPercent);
                    var result = Math.Min(panelDesiredDimension, maxWindowDimension);
                    return result;
                }
                else
                {
                    // Default implementation
                    var maxWindowDimension = windowDimension * defaultMaxDimensionMultiplier;
                    return Math.Min(panelDesiredDimension, maxWindowDimension); // If the desired size is less than the max window dimension, use that. Otherwise use the maximum.
                }


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
