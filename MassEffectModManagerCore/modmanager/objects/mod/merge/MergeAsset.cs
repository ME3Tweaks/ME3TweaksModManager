using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge
{
    /// <summary>
    /// Asset file packaged in a merge mod
    /// </summary>
    public class MergeAsset
    {
        /// <summary>
        /// Filename of the asset. Only used during serialization and for logging errors
        /// </summary>
        [JsonProperty(@"filename")]
        public string FileName { get; set; }

        /// <summary>
        /// Size of the asset
        /// </summary>
        [JsonProperty(@"filesize")] 
        public int FileSize { get; set; }

        /// <summary>
        /// Asset binary data
        /// </summary>
        [JsonIgnore]
        public byte[] AssetBinary;

        /// <summary>
        /// Where the data for this asset begins in the stream (post magic). Used when loading from disk on demand. Mods loaded from archive will load it when the file is parsed
        /// </summary>
        [JsonIgnore] 
        public int FileOffset;

        public void ReadAssetBinary(Stream mergeFileStream)
        {
            if (FileOffset != 0)
                mergeFileStream.Seek(FileOffset, SeekOrigin.Begin);
            AssetBinary = mergeFileStream.ReadToBuffer(FileSize);
        }
    }
}
