using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Contains a bundled MEMMod that is nested under a Moddesc mod
    /// </summary>
    public class M3MEMMod
    {
        /// <summary>
        /// The texture mod.
        /// </summary>
        public MEMMod TextureMod { get; set; }

        /// <summary>
        /// The associated moddesc mod
        /// </summary>
        public Mod ModdescMod { get; set; }

        // Todo: Implement compareto()

        public string GetDescription()
        {
            return $"{TextureMod.DisplayString}\n\nPart of {ModdescMod.ModName}";
        }
    }
}
