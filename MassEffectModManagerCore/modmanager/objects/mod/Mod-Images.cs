using System;
using System.IO;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.diagnostics;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        private const double RequiredBannerAspectRatio = 12.3404255319; //580 x 47
        private const double RequiredAspectRatioTolerance = 0.08;
        public const string ModImageAssetFolderName = @"M3Images";
        public string ModImageAssetsPath => FilesystemInterposer.PathCombine(Archive != null, ModPath, ModImageAssetFolderName);

        /// <summary>
        /// Bitmap data for the banner of the mod
        /// </summary>
        public BitmapSource BannerBitmap { get; set; }

        /// <summary>
        /// Relative path to the banner image, from the root of the folder
        /// </summary>
        public string BannerImageName { get; set; }

        /// <summary>
        /// Mapping of current loaded assets names to their bitmap data.
        /// Call InvalidateBitmapCache(); to empty this list.
        /// </summary>
        private CaseInsensitiveDictionary<BitmapImage> LoadedImageAssets { get; } = new CaseInsensitiveDictionary<BitmapImage>();

        public void InvalidateBitmapCache()
        {
            LoadedImageAssets.Clear();
        }

        /// <summary>
        /// Loads the requested image 
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public BitmapSource LoadModImageAsset(string assetName)
        {
            if (assetName == null) return null;
            if (assetName.StartsWith(@"/") || assetName.StartsWith(@"\\") || assetName.Contains(@".."))
            {
                M3Log.Error($@"Image assets cannot contain .. or start with / or \. The asset will not be loaded: {assetName}");
                return null;
            }
            if (LoadedImageAssets.TryGetValue(assetName, out var loaded)) return loaded;

            Stream loadStream = null;

            try
            {
                var imagePathFull = FilesystemInterposer.PathCombine(Archive != null, ModImageAssetsPath, assetName);
                if (FilesystemInterposer.FileExists(imagePathFull, Archive))
                {
                    // Load the image
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
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // Fixes crashes on things like ICC, maybe?
                    bitmap.StreamSource = loadStream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    LoadedImageAssets[assetName] = bitmap;
                    return bitmap; // This is so xaml doesn't trigger possibly before this code block has executed
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error loading image asset {assetName}: {e.Message}. The asset will not be loaded");
            }
            finally
            {
                // Ensure file is closed
                if (loadStream is FileStream fs)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }

            return null;
        }

        public void RemoveAssetFromCache(string assetName)
        {
            LoadedImageAssets.Remove(assetName);
        }

        public void LoadBannerImage()
        {
            if (!string.IsNullOrEmpty(BannerImageName))
            {
                var bitmap = LoadModImageAsset(BannerImageName);
                if (bitmap != null)
                {
                    var aspectRatio = bitmap.Width / bitmap.Height;
                    var aspectRatioDiff = RequiredBannerAspectRatio - aspectRatio;
                    if (Math.Abs(aspectRatioDiff) > RequiredAspectRatioTolerance)
                    {
                        // Must have specific aspect ratio.limes
                        M3Log.Error($@"{ModName}'s banner image is not the correct aspect ratio. Aspect ratio should be 580x47. The banner won't be displayed.");
                        RemoveAssetFromCache(BannerImageName);
                    }
                    else
                    {
                        BannerBitmap = bitmap;
                    }
                }
            }
        }
    }
}
