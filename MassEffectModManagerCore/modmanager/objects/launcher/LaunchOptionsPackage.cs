using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.launcher
{
    /// <summary>
    /// Represents options chosen for launch.
    /// </summary>

    [AddINotifyPropertyChangedInterface]
    public class LaunchOptionsPackage
    {
        /// <summary>
        /// The string shown to the user in the dropdown
        /// </summary>
        [JsonProperty(@"title")]
        public string PackageTitle { get; set; }

        /// <summary>
        /// The game this option is for
        /// </summary>
        [JsonProperty(@"game")]
        public MEGame Game { get; set; }

        /// <summary>
        /// The chosen official language (if any)
        /// </summary>
        [JsonProperty(@"chosenlanguage")]
        public string ChosenLanguage { get; set; } = @"INT";

        /// <summary>
        /// Subtitle size option
        /// </summary>
        [JsonProperty(@"subtitlesize")]
        public int SubtitleSize { get; set; } = 20;
    }
}
