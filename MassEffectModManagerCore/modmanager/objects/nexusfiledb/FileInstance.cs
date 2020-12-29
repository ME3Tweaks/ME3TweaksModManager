using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.usercontrols;
using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.objects.nexusfiledb
{
    [DebuggerDisplay("ParentPathId {ParentPathID}, FName: {FilenameId} , FID: {FileID}")]
    public class FileInstance
    {
        /// <summary>
        /// The id of the parent path
        /// </summary>
        [JsonProperty("parentpathid")]
        public int ParentPathID { get; set; }

        /// <summary>
        /// The ID of the mod.
        /// </summary>
        [JsonProperty("mod_id")]
        public int ModID { get; set; }

        /// <summary>
        /// The nexusmods file id that contains this specific file
        /// </summary>
        [JsonProperty("file_id")]
        public int FileID { get; set; }

        [JsonIgnore]
        public string modname { get; set; }

        [JsonIgnore]
        public string modfilename { get; set; }

        [JsonProperty("filenameid")]
        public int FilenameId { get; set; }

        [JsonProperty("modnameid")]
        public int ModNameId { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }
    }
}
