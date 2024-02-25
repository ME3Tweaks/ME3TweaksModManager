using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Image = LegendaryExplorerCore.Textures.Image;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;

namespace ME3TweaksModManager.lecextended
{
    public static class ImageExtended
    {
        [SupportedOSPlatform("windows")]
        public static Bitmap convertRawToBitmapARGB(byte[] src, int w, int h, PixelFormat format, bool clearAlpha = true)
        {
            byte[] tmpData = Image.convertRawToARGB(src, ref w, ref h, format, clearAlpha);
            var bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            Marshal.Copy(tmpData, 0, bitmapData.Scan0, tmpData.Length);
            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }
    }
}
