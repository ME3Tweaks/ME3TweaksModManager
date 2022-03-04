using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pathoschild.FluentNexus.Models;

namespace ME3TweaksModManager.modmanager.objects.nexusfiledb
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

        private string _description;

        [JsonProperty(@"description")]
        public string Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        /// <summary>
        /// Setter for deserializing the list of LEGames. Access through <see cref="LEGames"/>.
        /// </summary>
        [JsonProperty(@"legames")]
        public string InternalLEGames
        {
            set
            {
                LEGames = value.Split(',').Select(x => Enum.Parse<MEGame>(x)).ToArray();
            }
        }

        /// <summary>
        /// The list of Legendary Edition games this mod is for. This depends on tags. This will be null if none are defined!
        /// </summary>
        [JsonIgnore]
        public MEGame[] LEGames { get; set; }
    }
}
