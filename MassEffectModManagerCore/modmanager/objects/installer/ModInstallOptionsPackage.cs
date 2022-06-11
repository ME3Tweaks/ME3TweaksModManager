using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public GameTargetWPF InstallTarget { get; init; }
        /// <summary>
        /// If this installation is occurring in batch-mode
        /// </summary>
        public bool BatchMode { get; init; }

        /// <summary>
        /// The list of selected installation options mapped by each header
        /// </summary>
        public Dictionary<ModJob.JobHeader, List<AlternateOption>> SelectedOptions { get; init; } = new();
    }
}
