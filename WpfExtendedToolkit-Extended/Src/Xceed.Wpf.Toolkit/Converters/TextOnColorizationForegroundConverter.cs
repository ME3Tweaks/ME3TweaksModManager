using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace Xceed.Wpf.Toolkit.Converters
{
  public class TextOnColorizationForegroundConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is Color col)
      {
        return (384 - col.R - col.G - col.B) > 0 ? Colors.White : Colors.Black;
      }

      return Colors.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return false; //don't need this
    }
  }
}
