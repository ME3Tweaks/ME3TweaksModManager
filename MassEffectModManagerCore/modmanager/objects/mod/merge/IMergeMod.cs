using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.me3tweakscoreextended;

namespace ME3TweaksModManager.modmanager.objects.mod.merge
{
    public interface IMergeMod
    {
        /// <summary>
        /// Name of the merge mod file, relative to the root of the MergeMods folder in the mod directory
        /// </summary>
        public string MergeModFilename { get; set; }

        /// <summary>
        /// Game this merge mod is for
        /// </summary>
        public MEGame Game { get; set; }

        /// <summary>
        /// List of asset files that are part of this merge mod
        /// </summary>
        public CaseInsensitiveDictionary<MergeAsset> Assets { get; set; }

        /// <summary>
        /// Applies the merge mod to the target
        /// </summary>
        /// <param name="associatedMod">The mod that is installing this merge mod</param>
        /// <param name="target">The target to be applied to</param>
        /// <returns></returns>
        public bool ApplyMergeMod(Mod associatedMod, GameTarget target, Action<int> mergeWeightDelegate, Action<string, string> addBasegameTrackedFile, CaseInsensitiveConcurrentDictionary<string> originalFileMD5Map);
        /// <summary>
        /// Get the number of total merge operations this mod can apply
        /// </summary>
        /// <returns></returns>
        public int GetMergeCount();
        /// <summary>
        /// Get the weight of all merges for this merge mod for accurate progress tracking.
        /// </summary>
        /// <returns></returns>
        public int GetMergeWeight();

        /// <summary>
        /// Extracts this m3m file to the specified folder
        /// </summary>
        /// <param name="outputfolder"></param>
        public void ExtractToFolder(string outputfolder);

        /// <summary>
        /// Returns a list of strings of files that will be modified by this merge file.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetMergeFileTargetFiles();
    }
}
