using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge
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
        /// Applies the merge mod to the target
        /// </summary>
        /// <param name="associatedMod">The mod that is installing this merge mod</param>
        /// <param name="target">The target to be applied to</param>
        /// <returns></returns>
        public bool ApplyMergeMod(Mod associatedMod, GameTarget target, ref int numMergesDoneTotal, int numTotalMerges, Action<int, int, string, string> mergeProgressDelegate = null);
        /// <summary>
        /// Get the number of total merge operations this mod can apply
        /// </summary>
        /// <returns></returns>
        public int GetMergeCount();

        /// <summary>
        /// Extracts this m3m file to the specified folder
        /// </summary>
        /// <param name="outputfolder"></param>
        public void ExtractToFolder(string outputfolder);
    }
}
