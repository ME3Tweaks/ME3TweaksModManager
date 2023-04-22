using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    public interface IBatchQueueMod
    {
        /// <summary>
        /// If the mod is available to install
        /// </summary>
        /// <returns></returns>
        public bool IsAvailableForInstall();
    }
}
