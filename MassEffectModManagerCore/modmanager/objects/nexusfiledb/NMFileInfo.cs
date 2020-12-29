using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
    }
}
