using System.ComponentModel;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    public static class WineWorkarounds
    {
        public static bool WineDetected { get; set; }
        public static Version WineDetectedVersion { get; set; }
        public static string WineHostKernelName { get; set; }
        public static Version WineHostKernelVersion { get; set; }

    }
}
