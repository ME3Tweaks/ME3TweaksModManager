using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MassEffectModManagerCore.modmanager.objects.nexusfiledb
{
    public class GameDatabase
    {
        public static GameDatabase LoadDatabase(string domain)
        {
            var fpath = Path.Combine(Utilities.GetNexusModsCache(), domain + @".json");
            if (File.Exists(fpath))
            {
                return JsonConvert.DeserializeObject<GameDatabase>(File.ReadAllText(fpath));
            }

            return null; // Database not found!
        }

        [JsonProperty("last_indexing_timestamp")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime IndexingTime { get; set; }

        [JsonProperty("filenames")]
        public Dictionary<int, string> NameTable { get; set; }

        [JsonProperty("paths")]
        public Dictionary<int, PathInstance> Paths { get; set; }

        [JsonProperty("fileinstances")]
        public Dictionary<int, List<FileInstance>> FileInstances { get; set; }

        /// <summary>
        /// Information about a single download file (file id)
        /// </summary>
        [JsonProperty("fileinfos")]
        public Dictionary<int, NMFileInfo> ModFileInfos { get; set; }

    }
}
