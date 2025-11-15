using ME3TweaksCoreWPF.Targets;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// Used for interop betwen WPF and Milan Installer
    /// </summary>
    internal class MBIInterop
    {
        /// <summary>
        /// Invoked to update the status of the installation process.
        /// </summary>
        public Action<string> SetStatus { get; set; }

        /// <summary>
        /// Invoked when installation is finished.
        /// </summary>
        public Action<bool, string> InstallationFinished { get; internal set; }

        /// <summary>
        /// Adds the specified target
        /// </summary>
        public Action<GameTargetWPF> AddTarget { get; set; }
    }
}
