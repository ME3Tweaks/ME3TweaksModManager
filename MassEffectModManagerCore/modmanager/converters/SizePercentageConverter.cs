using System;
using System.Globalization;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.converters
{
    public class SizePercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fullSize)
            {
                if (parameter == null)
                    return 0.7 * fullSize;

                var sizeParms = ParseParms((string) parameter);

                var calculatedSize = fullSize * sizeParms.percentLimiter;
                if (sizeParms.minSize > 0)
                {
                    calculatedSize = Math.Max(sizeParms.minSize, calculatedSize);
                    if (sizeParms.maxSize > 0)
                    {
                        calculatedSize = Math.Min(sizeParms.maxSize, calculatedSize);
                    }
                }

                return calculatedSize;
            }

            // DEFAULT
            return 50;
        }

        private static (double percentLimiter, double minSize, double maxSize) ParseParms(string parameter)
        {
            var split = parameter.Split('_');
            double percent = double.Parse(split[0], CultureInfo.InvariantCulture);

            double min = 0;
            double max = 0;
            if (split.Length >= 2)
            {
                min = double.Parse(split[1], CultureInfo.InvariantCulture);

                if (split.Length >= 3)
                {
                    max = double.Parse(split[2], CultureInfo.InvariantCulture);
                }
            }

            return (percent, min, max);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Don't need to implement this
            return null;
        }
    }
}