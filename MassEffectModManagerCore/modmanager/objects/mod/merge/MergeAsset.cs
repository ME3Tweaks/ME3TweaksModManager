using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge
{
    /// <summary>
    /// Asset file packaged in a merge mod
    /// </summary>
    public class MergeAsset
    {
        [JsonProperty("filename")]
        public string FileName;

        [JsonProperty("fileindex")]
        public int FileIndex;

        [JsonProperty("filesize")] 
        public int FileSize;

        [JsonIgnore]
        /// <summary>
        /// Asset binary data
        /// </summary>
        public byte[] AssetBinary;
    }
}
