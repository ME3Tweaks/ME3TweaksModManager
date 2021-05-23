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
        /// Applies the merge mod to the target
        /// </summary>
        /// <param name="target">The target to be applied to</param>
        /// <returns></returns>
        public bool ApplyMergeMod(GameTarget target);
    }
}
