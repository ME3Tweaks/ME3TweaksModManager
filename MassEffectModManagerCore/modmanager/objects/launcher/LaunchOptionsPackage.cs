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

        /// <summary>
        /// If the game should auto resume the last save (LE only)
        /// </summary>
        [JsonProperty(@"autoresume")]
        public bool AutoResumeSave { get; set; }

        /// <summary>
        /// If rumble should be enabled
        /// </summary>
        [JsonProperty(@"noforcefeedback")]
        public bool NoForceFeedback { get; set; }

        /// <summary>
        /// If minidumps should be created (LE only)
        /// </summary>
        [JsonProperty(@"enableminidumps")]
        public bool EnableMinidumps { get; set; }

        /// <summary>
        /// Other arguments specified by the user
        /// </summary>
        [JsonProperty(@"userargs")]
        public string CustomExtraArgs { get; set; }

        /// <summary>
        /// The version of this Launch Options Package, in case we need to update format in future
        /// </summary>
        [JsonProperty(@"version")]
        public int Version { get; set; }

        /// <summary>
        /// Filepath this package loaded from. Can be null if not saved or loaded from disk
        /// </summary>
        [JsonIgnore]
        public string FilePath { get; set; }

        /// <summary>
        /// The current LOP version
        /// </summary>
        public const int LATEST_LOP_VERSION = 1;

        /// <summary>
        /// The extension of the serialized file
        /// </summary>
        public const string FILE_EXTENSION = @".m3l";

        public void SetLatestVersion()
        {
            Version = LATEST_LOP_VERSION;
        }

        /// <summary>
        /// If this is a custom option, for use in UI handling
        /// </summary>
        [JsonIgnore]
        public bool IsCustomOption { get; init; }

        /// <summary>
        /// Unique identifier for this package
        /// </summary>
        [JsonProperty(@"guid")]
        public Guid PackageGuid { get; set; } = Guid.Empty;

        /// <summary>
        /// Used to set values from a generic non-hardcoded list
        /// </summary>
        /// <param name="key">The key of the value to set</param>
        /// <param name="value">The boolean value to set</param>
        public void SetOption(string key, bool value)
        {
            switch (key)
            {
                case LauncherCustomParameter.KEY_AUTORESUME:
                    AutoResumeSave = value;
                    break;
                case LauncherCustomParameter.KEY_MINIDUMPS:
                    EnableMinidumps = value;
                    break;
                case LauncherCustomParameter.KEY_NOFORCEFEEDBACK:
                    NoForceFeedback = value;
                    break;
            }
        }
    }
}
