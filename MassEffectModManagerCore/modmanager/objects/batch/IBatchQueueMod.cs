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

        // For File Source Service lookups (if supported)
        /// <summary>
        /// The MD5 used to uniquely identify this mod
        /// </summary>
        public string Hash { get; set; }
        /// <summary>
        /// The size, in bytes, of this mod, used to speed up lookups
        /// </summary>
        public long Size { get; set; }
    }
}
