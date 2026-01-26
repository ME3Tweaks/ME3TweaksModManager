
namespace ME3TweaksModManager.modmanager.objects
{
    public class M3SupportedOS
    {
        private static bool _hasShownUnsupportedMessage = false;
        /// <summary>
        /// Indicates whether the unsupported message has been shown.
        /// </summary>
        public static bool hasShownUnsupportedMessage
        {
            get => _hasShownUnsupportedMessage;
            set
            {
                if (!StartupCompleted) return; // Prevent changes
                _hasShownUnsupportedMessage = value;
            }
        }
        /// <summary>
        /// Flag to suppress allowing changes to hasShownUnsupportedMessage
        /// </summary>
        public static bool StartupCompleted = false;

        /// <summary>
        /// The name to display for the OS
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The build number - e.g. Windows 10 22H2 is 19045, Windows 11 24H2 is 26100
        /// </summary>
        public int BuildNumber { get; set; }
        /// <summary>
        /// The major build number - e.g. Windows 10 is 10, Windows 11 is 10, Windows 12 will be 12 (probably, I hope)
        /// </summary>
        public int MajorVersion { get; set; }

        /// <summary>
        /// Indicates if this version of a supported OS build number won't change - e.g. it is the last version of that OS
        /// </summary>
        public bool FixedVersion { get; set; }


        // Hi. If you're reading this, and going to try and argue that this lowest version is arbitrary,
        // you should try supporting software for free, and see how well supporting every single
        // version of Windows works out for you.
        // LTSC is not supported as it is for Enterprises

        /// </summary>
        /// <returns></returns>
        public static M3SupportedOS[] GetSupportedOperatingSystems()
        {
            return [
                // Windows 10 22H2 - until October 2026
                new M3SupportedOS() { Name = "Windows 10 22H2", MajorVersion = 10, BuildNumber = 19045, FixedVersion = true },
                new M3SupportedOS() { Name = "Windows 11 24H2", MajorVersion = 10, BuildNumber = 26100, FixedVersion = false },
            ];
        }

        public static bool IsSupportedOperatingSystem()
        {
            OperatingSystem os = Environment.OSVersion;

            var supportedOSes = GetSupportedOperatingSystems();
            
            foreach (var supportedOS in supportedOSes)
            {
                if (os.Version.Major > supportedOS.MajorVersion)
                {
                    // OS major version is greater than we know about, probably
                    // a lag between this mod manager build and the OS release.
                    return true;
                }

                if (os.Version.Major == supportedOS.MajorVersion)
                {
                    if (supportedOS.FixedVersion)
                    {
                        // For fixed versions, build number must match exactly
                        if (os.Version.Build == supportedOS.BuildNumber)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // For rolling versions, build number must be >= supported build
                        if (os.Version.Build >= supportedOS.BuildNumber)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a UI string for the minimum supported version of the OS
        /// </summary>
        /// <returns></returns>
        internal string ToMinimumSupportedString()
        {
            if (FixedVersion)
            {
                return Name;
            }
            else
            {
                return $"{Name} or newer";
            }
        }
    }
}
