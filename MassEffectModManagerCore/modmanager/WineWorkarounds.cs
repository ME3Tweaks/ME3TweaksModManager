using System.ComponentModel;

namespace ME3TweaksModManager.modmanager
{
    /// <summary>
    /// Contains information about Wine detection and version information for running on Linux/Unix systems via Wine compatibility layer
    /// </summary>
    [Localizable(false)]
    public static class WineWorkarounds
    {
        /// <summary>
        /// Indicates whether the application is running under Wine
        /// </summary>
        public static bool WineDetected { get; set; }
        
        /// <summary>
        /// The detected version of Wine, if running under Wine
        /// </summary>
        public static Version WineDetectedVersion { get; set; }
        
        /// <summary>
        /// The name of the host operating system kernel
        /// </summary>
        public static string WineHostKernelName { get; set; }
        
        /// <summary>
        /// The version of the host operating system kernel
        /// </summary>
        public static Version WineHostKernelVersion { get; set; }

    }
}
