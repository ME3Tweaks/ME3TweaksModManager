using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

// From https://stackoverflow.com/questions/952080/how-do-you-select-the-right-size-icon-from-a-multi-resolution-ico-file-in-wpf

namespace MassEffectModManagerCore.ui
{
    /// <summary>
    /// Simple extension for icon, to let you choose icon with specific size.
    /// Usage sample:
    /// Image Stretch="None" Source="{common:Icon /Controls;component/icons/custom.ico, 16}"
    /// Or:
    /// Image Source="{common:Icon Source={Binding IconResource}, Size=16}"
    /// </summary> 
    public class MippedIconExtension : MarkupExtension
    {
        // Hopefully this won't use too much memory...
        private static ConcurrentDictionary<string, BitmapFrame> DecodedCache = new ConcurrentDictionary<string, BitmapFrame>();

        private string _source;

        public string Source
        {
            get => _source;
            set =>
                // Have to make full pack URI from short form, so System.Uri recognizes it.
                _source = @"pack://application:,,,/ME3TweaksModManager;component" + value;
        }

        public int Size { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return StaticConvert(Source, Size, false);
        }

        public static BitmapFrame StaticConvert(string source, float size, bool prefixPack = true)
        {
            if (prefixPack) source = @"pack://application:,,,/ME3TweaksModManager;component" + source;
            if (DecodedCache.TryGetValue($@"{source}_{size}", out var predecoded))
            {
                return predecoded;
            }

            var decoder = BitmapDecoder.Create(new Uri(source),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnDemand);

            var result = decoder.Frames.FirstOrDefault(f => f.Width == size);
            if (result == default(BitmapFrame))
            {
                result = decoder.Frames.OrderBy(f => f.Width).First();
            }

            // Cache into memory
            DecodedCache[$@"{source}_{size}"] = result;

            return result;
        }

        public MippedIconExtension(string source, int size)
        {
            Source = source;
            Size = size;
        }

        public MippedIconExtension() { }
    }
}
