using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.installer
{
    /// <summary>
    /// Object that describes chosen mod installation options.
    /// </summary>
    public class ModInstallOptionsPackage
    {

        /// <summary>
        /// If the installation prerequesites check can be skipped. This can be done to not double install time if we immediately conducted it in the options dialog that automatically passed through.
        /// </summary>
        public bool SkipPrerequesitesCheck { get; init; }
        /// <summary>
        /// If ME1 config files should be set to read only after install
        /// </summary>
        public bool SetME1ReadOnlyConfigFiles { get; init; }
        /// <summary>
        /// The mod being installed
        /// </summary>
        public Mod ModBeingInstalled { get; init; }
        /// <summary>
        /// ME2/ME3 only: Should packages be resaved as compressed. This does nothing for other games
        /// </summary>
        public bool CompressInstalledPackages { get; init; }
        /// <summary>
        /// The target to install to
        /// </summary>
        public GameTarget InstallTarget { get; init; }
        /// <summary>
        /// If this installation is occurring in batch-mode
        /// </summary>
        public bool BatchMode { get; init; }

        /// <summary>
        /// The list of selected installation options mapped by each header
        /// </summary>
        public Dictionary<ModJob.JobHeader, List<AlternateOption>> SelectedOptions { get; init; } = new();

        /// <summary>
        /// If this is the first content mod that will be installed. Things such as bink bypass will be installed
        /// </summary>
        public bool IsFirstBatchMod { get; set; }

        /// <summary>
        /// Used for batch mode; if this flag has been set then we don't do a backup check, this way we don't waste time
        /// doing it over and over when the user already declined.
        /// </summary>
        public bool HasDoneBackupCheck { get; set; }

        /// <summary>
        /// Makes empty selection options items
        /// </summary>
        internal void SetNoOptions()
        {
            SelectedOptions.Clear();
            foreach(var job in ModBeingInstalled.InstallationJobs)
            {
                SelectedOptions[job.Header] = new List<AlternateOption>();
            }
        }
    }
}
