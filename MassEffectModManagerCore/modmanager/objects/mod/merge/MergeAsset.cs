using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Exceptions;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod.merge.v1;
using Newtonsoft.Json;
using System.Text;

namespace ME3TweaksModManager.modmanager.objects.mod.merge
{
    /// <summary>
    /// Asset file packaged in a merge mod
    /// </summary>
    public class MergeAsset : IComparable
    {

        // Concurrency
        private static object syncObj = new object();

        [JsonIgnore]
        public MergeMod1 OwningMM;

        /// <summary>
        /// Filename of the asset. Only used during serialization and for logging errors
        /// </summary>
        [JsonProperty(@"filename")]
        public string FileName { get; set; }

        /// <summary>
        /// Size of the asset (uncompressed)
        /// </summary>
        [JsonProperty(@"filesize")]
        public int FileSize { get; set; }

        /// <summary>
        /// If the asset is compressed
        /// </summary>
        [JsonProperty(@"compressed")]
        public bool IsCompressed { get; set; }

        /// <summary>
        /// Size of the asset (compressed) in the file
        /// </summary>
        [JsonProperty(@"compressedsize")]
        public int? CompressedSize { get; set; }

        /// <summary>
        /// Asset binary data. This is only loaded when needed - mods loaded from archive will have this populated, mods loaded from disk will not.
        /// </summary>
        [JsonIgnore]
        public byte[] AssetBinary;

        /// <summary>
        /// Where the data for this asset begins in the stream (post magic). Used when loading from disk on demand. Mods loaded from archive will load it when the file is parsed
        /// </summary>
        [JsonIgnore]
        public int FileOffset;

        /// <summary>
        /// Ensures an asset is loaded for use. Throws an exception if the asset cannot be loaded.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void EnsureAssetLoaded()
        {
            if (AssetBinary != null)
                return; // It's loaded

            if (OwningMM.LoadedFromPath == null)
                throw new Exception(@"Cannot load asset data from an m3m that was not loaded from disk at a time after initial load. This is a bug, please report it.");

            LoadAsset();
        }

        /// <summary>
        /// Reads the asset binary data into the <see cref="AssetBinary"/> byte array. Seeks and reads from the specified stream.
        /// </summary>
        /// <param name="mergeFileStream">The stream to read from.</param>
        public void ReadAssetBinary(Stream mergeFileStream)
        {
            if (FileOffset != 0)
                mergeFileStream.Seek(FileOffset, SeekOrigin.Begin);
            if (IsCompressed)
            {
                var compressed = mergeFileStream.ReadToBuffer(CompressedSize.Value);
                AssetBinary = new byte[FileSize];
                AssetBinary = LZMA.Decompress(compressed, (uint)FileSize);
            }
            else
            {
                // Mod Manager 8.x only supported uncompressed assets
                AssetBinary = mergeFileStream.ReadToBuffer(FileSize);
            }
        }

        /// <summary>
        /// Returns the AssetBinary as a string.
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            EnsureAssetLoaded();

            if (AssetBinary == null)
                throw new Exception(@"AssetBinary was null in this MergeAsset! The m3m was not loaded. This is a bug.");

            // This is how File.ReadAllText() works
            using StreamReader sr = new StreamReader(new MemoryStream(AssetBinary), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }

        private void LoadAsset()
        {
            lock (syncObj)
            {
                if (AssetBinary != null)
                    return; // Already loaded.
                if (OwningMM.LoadedFromPath == null)
                    return; // Cannot load asset at a later time if the loaded from path is not set.

                using var fs = File.OpenRead(OwningMM.LoadedFromPath);
                if (fs.Length != OwningMM.SizeAtLoadTime)
                {
                    throw new NoTelemetryException(M3L.GetString(M3L.string_interp_mergeModFileChangedReload, Path.GetFileName(OwningMM.LoadedFromPath)));
                }
                ReadAssetBinary(fs);
            }
        }

        public MergeAsset() { }

        public MergeAsset(string assetName, bool compressAsset)
        {
            FileName = assetName;
            IsCompressed = compressAsset;
        }

        public int CompareTo(object obj)
        {
            if (obj is MergeAsset o)
                return FileName.CompareTo(o.FileName);
            return 1;
        }

        public override bool Equals(object o)
        {
            if (ReferenceEquals(this, o)) return true;
            if (ReferenceEquals(o, null)) return false;
            if (GetType() != o.GetType()) return false;
            return FileName == (o as MergeAsset).FileName;
        }

        public int GetHashCode(MergeAsset obj)
        {
            return (obj.FileName != null ? obj.FileName.GetHashCode() : 0);
        }
    }
}
