using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.merge.squadmate
{
    /// <summary>
    /// Class that describes a single outfit for a single squadmate, for use with the Squadmate Outfit Merge feature.
    /// </summary>
    public class SquadmateInfoSingle
    {
        [JsonProperty(@"henchname")]
        public string HenchName { get; set; }

        [JsonProperty(@"henchpackage")]
        public string HenchPackage { get; set; }

        [JsonProperty(@"highlightimage")]
        public string HighlightImage { get; set; }

        [JsonProperty(@"availableimage")]
        public string AvailableImage { get; set; }
        T
        //[JsonProperty(@"deadimage")]
        //public string DeadImage { get; set; }

        [JsonProperty(@"silhouetteimage")]
        public string SilhouetteImage { get; set; }

        [JsonProperty(@"descriptiontext0")]
        public int DescriptionText0 { get; set; }

        [JsonProperty(@"customtoken0")]
        public int CustomToken0 { get; set; }

        /// <summary>
        /// The availability integer - defaults to -1 for always available
        /// </summary>
        [JsonProperty(@"plotflag")]
        public int PlotFlag { get; set; } = -1;

        /// <summary>
        /// The index of the conditional function to check if this outfit is the selected one when loading
        /// </summary>
        [JsonIgnore]
        public int ConditionalIndex { get; set; }

        /// <summary>
        /// The outfit index that uniquely identifies this outfit
        /// </summary>
        [JsonIgnore]
        public int AppearanceId { get; set; }

        /// <summary>
        /// The outfit index that is set in the conditionals to define this outfit
        /// </summary>
        [JsonIgnore]
        public int MemberAppearanceValue { get; set; }

        /// <summary>
        /// Used to add values to inis
        /// </summary>
        [JsonIgnore]
        public string DLCName { get; set; }

        public Dictionary<string, string> ToPropertyDictionary()
        {
            var dict = new Dictionary<string, string>
            {
                [@"AppearanceId"] = AppearanceId.ToString(),
                [@"MemberAppearanceValue"] = MemberAppearanceValue.ToString(),
                [@"MemberTag"] = $@"hench_{HenchName.ToLower()}",
                [@"MemberAppearancePlotLabel"] = $@"Appearance{HenchName}",
                [@"HighlightImage"] = HighlightImage,
                [@"AvailableImage"] = AvailableImage,
                [@"DeadImage"] = @"GUI_Henchmen_Images.PlaceHolder", // Game 3
                [@"SilhouetteImage"] = SilhouetteImage,
                [@"DescriptionText[0]"] = DescriptionText0.ToString(),
                [@"CustomToken0[0]"] = CustomToken0.ToString()
            };
            return dict;
        }
    }
}
