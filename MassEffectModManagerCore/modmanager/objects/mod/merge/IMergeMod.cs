using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge
{
    public interface IMergeMod
    {
        /// <summary>
        /// Name of the merge mod file, relative to the root of the MergeMods folder in the mod directory
        /// </summary>
        public string MergeModFilename { get; set; }
        /// <summary>
        /// Applies the merge mod to the target
        /// </summary>
        /// <param name="associatedMod">The mod that is installing this merge mod</param>
        /// <param name="target">The target to be applied to</param>
        /// <returns></returns>
        public bool ApplyMergeMod(Mod associatedMod, GameTarget target);
    }
}
