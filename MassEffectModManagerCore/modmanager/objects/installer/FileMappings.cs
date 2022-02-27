using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.installer
{
    /// <summary>
    /// Describes file mappings for the installer.
    /// </summary>
    public class InstallMapping
    {
        /// <summary>
        /// Mapping of jobs to the list of files for that job to copy disk to disk.
        /// </summary>
        internal Dictionary<ModJob, UnpackedFileMapping> UnpackedJobMappings { get; } = new();

        /// <summary>
        /// List of SFAR mappings to apply to the game.
        /// </summary>
        internal List<SFARFileMapping> SFARJobs { get; } = new();
    }
    /// <summary>
    /// Describes a file mapping for disk to disk installation.
    /// </summary>
    internal class UnpackedFileMapping
    {
        public CaseInsensitiveDictionary<Mod.InstallSourceFile> FileMapping { get; } = new();
        public List<string> DLCFoldersBeingInstalled { get; } = new();
    }

    /// <summary>
    /// Describes a file mapping for disk to SFAR archive installation. Only used by ME3 with packed SFAR files.
    /// </summary>
    internal class SFARFileMapping
    {
        public ModJob Job { get; init; } 
        public string SFARPath { get; init; }
        public Dictionary<string, Mod.InstallSourceFile> SFARInstallationMapping { get; } = new();
    }
}
