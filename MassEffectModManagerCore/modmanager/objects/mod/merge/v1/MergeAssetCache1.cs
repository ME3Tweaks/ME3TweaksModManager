using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.UnrealScript;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    /// <summary>
    /// Contains assets to pass through when applying the Merge Mod
    /// </summary>
    public class MergeAssetCache1
    {
        /// <summary>
        /// FileLibs of packages that have been loaded for script editing during this merge
        /// </summary>
        public CaseInsensitiveConcurrentDictionary<FileLib> FileLibs = new();
    }
}
