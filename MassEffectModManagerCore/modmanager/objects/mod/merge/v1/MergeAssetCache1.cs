using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.UnrealScript;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
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
