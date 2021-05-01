using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pathoschild.FluentNexus.Models;

namespace MassEffectModManagerCore.modmanager.objects.nexusfiledb
{
    public class NMFileInfo
    {
        [JsonProperty(@"upload_date")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset UploadDate { get; set; }

        [JsonProperty(@"version")]
        public string Version { get; set; }

        [JsonProperty(@"nameid")]
        public int NameID { get; set; }

        [JsonProperty(@"category")]
        public FileCategory Category { get; set; }

        [JsonProperty(@"description")]
        public string Description { get; set; }
    }
}
