using MassEffectModManagerCore.modmanager.usercontrols.interfaces;
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

    public class GenerationsSizePercentageConverter : IValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fullSize)
            {
                if (parameter == null)
                    return 0.7 * fullSize;

                var sizeParms = ParseGenParms((string) parameter);

                var calculatedSize = fullSize * sizeParms.percentLimiter;
                if (sizeParms.minSize > 0)
                {
                    calculatedSize = Math.Max(sizeParms.minSize, calculatedSize);
                    if (sizeParms.maxSize > 0)
                    {
                        calculatedSize += Settings.GenerationSettingOT ? sizeParms.otSizePx : 0;
                        calculatedSize += Settings.GenerationSettingLE ? sizeParms.leSizePx : 0;
                        calculatedSize = Math.Min(sizeParms.maxSize, calculatedSize);
                    }
                }

                return calculatedSize;
            }

            // DEFAULT
            return 50;
        }

        internal (double percentLimiter, double minSize, double maxSize, double otSizePx, double leSizePx)
            ParseGenParms(string parameter)
        {
            var split = parameter.Split('_');
            double percent = double.Parse(split[0], CultureInfo.InvariantCulture);

            double min = 0;
            double max = 0;
            double otSizePx = 0;
            double leSizePx = 0;
            if (split.Length >= 2)
            {
                min = double.Parse(split[1], CultureInfo.InvariantCulture);

                if (split.Length >= 3)
                {
                    max = double.Parse(split[2], CultureInfo.InvariantCulture);
                }

                if (split.Length >= 5)
                {
                    otSizePx = double.Parse(split[3], CultureInfo.InvariantCulture);
                    leSizePx = double.Parse(split[4], CultureInfo.InvariantCulture);
                }
            }

            return (percent, min, max, otSizePx, leSizePx);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Don't need to implement this
            return null;
        }
    }

    public class GenerationsSizeAdjustedPercentageConverter : GenerationsSizePercentageConverter, IValueConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //Debug.WriteLine("Convertin");
            if (value is ISizeAdjustable adjustableControl)
            {
                if (parameter == null)
                    return 0.7 * adjustableControl.FullSize;

                var sizeParms = ParseGenParms((string) parameter);

                var calculatedSize = adjustableControl.FullSize * sizeParms.percentLimiter;
                if (sizeParms.minSize > 0)
                {
                    calculatedSize = Math.Max(sizeParms.minSize, calculatedSize);
                    calculatedSize += adjustableControl.Adjustment;
                    if (sizeParms.maxSize > 0)
                    {
                        calculatedSize += Settings.GenerationSettingOT ? sizeParms.otSizePx : 0;
                        calculatedSize += Settings.GenerationSettingLE ? sizeParms.leSizePx : 0;
                        calculatedSize = Math.Min(sizeParms.maxSize, calculatedSize);
                    }
                }

                if (calculatedSize >= adjustableControl.FullSize - 50 && adjustableControl.FullSize > 50)
                {
                    //Debug.WriteLine($@"OverCS: {adjustableControl.FullSize - 50} FS: {adjustableControl.FullSize}");
                    return adjustableControl.FullSize - 50;
                }

                //Debug.WriteLine($@"CS: {calculatedSize} FS: {adjustableControl.FullSize}");
                if (calculatedSize < 0)
                    return 50; //DEFAULT
                return calculatedSize;
            }

            // DEFAULT
            return 50;
        }
    }
}