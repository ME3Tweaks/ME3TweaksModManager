using System;
using System.IO;
using System.Windows.Media.Imaging;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod
    {
        private const double RequiredBannerAspectRatio = 12.3404255319;
        private const double RequiredAspectRatioTolerance = 0.08;

        public BitmapImage BannerBitmap { get; set; }
        /// <summary>
        /// Relative path to the banner image, from the root of the folder
        /// </summary>
        public string BannerImageName { get; set; }

        public void LoadBannerImage()
        {
            if (!string.IsNullOrEmpty(BannerImageName))
            {
                var imagePathFull = FilesystemInterposer.PathCombine(Archive != null, ModPath, @"M3Images", BannerImageName);
                if (FilesystemInterposer.FileExists(imagePathFull, Archive))
                {
                    // Load the image

                    Stream loadStream;
                    if (Archive == null)
                    {
                        // read from disk
                        loadStream = File.OpenRead(imagePathFull);
                    }
                    else
                    {
                        // read from Archive
                        loadStream = new MemoryStream();
                        Archive.ExtractFile(imagePathFull, loadStream);
                        loadStream.Position = 0;
                    }

                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = loadStream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    if (loadStream is FileStream fs)
                    {
                        fs.Close();
                        fs.Dispose();
                    }


                    var aspectRatio = bitmap.Width / bitmap.Height;
                    var aspectRatioDiff = RequiredBannerAspectRatio - aspectRatio;
                    if (Math.Abs(aspectRatioDiff) > RequiredAspectRatioTolerance)
                    {
                         // Must have specific aspect ratio.limes
                         Log.Error($@"{ModName}'s banner image is not the correct aspect ratio. Aspect ratio should be 580x47. The banner won't be displayed.");
                    }
                    else
                    {
                        BannerBitmap = bitmap; // This is so xaml doesn't trigger possibly before this code block has executed
                    }
                }
            }
        }
    }
}
