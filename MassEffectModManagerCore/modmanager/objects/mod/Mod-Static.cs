using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Misc;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    // Static methods for Mod class
    public partial class Mod
    {
        /// <summary>
        /// Reads the 'modver' descriptor from a mod manager moddesc file. Used to query a version on disk without loading the mod.
        /// </summary>
        /// <param name="modDescPath">Path to the moddesc</param>
        /// <returns>Version (proper), null if failed to parse</returns>
        public static Version GetModVersionFromIni(string modDescPath)
        {
            DuplicatingIni ini = DuplicatingIni.LoadIni(modDescPath);
            var version = ini[@"ModInfo"][@"modver"];
            if (version != null && version.HasValue)
            {
                if (ProperVersion.TryParse(version.Value, out var result))
                {
                    return result;
                }
            }
            return null;
        }
    }
}
