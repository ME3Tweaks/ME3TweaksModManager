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

        /// <summary>
        /// Development builds only: set when a Project Milan build is driving us. The install is left unslashed and this is
        /// called to have Project Milan generate the slash manifest for it, which is then applied. Throws if it fails.
        /// </summary>
        public Action<MilanSlashRequest> GenerateSlash { get; set; }

        /// <summary>
        /// Install exactly like a user's (release) Mod Manager would, even in a development build: no skipping a stale slash
        /// manifest, and the result has to match the recorded final state. Used to test a Project Milan build before it ships.
        /// </summary>
        public bool UserInstall { get; set; }

        /// <summary>
        /// Callback that lets the user confirm/decline install if some languages will not be available.
        /// </summary>
        public Func<IReadOnlyList<MilanInstallPlan.UnavailableLanguage>, bool> ConfirmSkipUnavailableLanguages { get; set; }
    }
}
