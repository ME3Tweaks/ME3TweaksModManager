using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.objects
{
    /// <summary>
    /// CompressedMod is a wrapper class for mods stored in archive files and includes information about where the mod is located in the archive.
    /// </summary>
    public class CompressedMod
    {
        public Mod Mod { get; private set; } //this will fail to be valid because files don't exist on disk
        public bool IsUnofficiallySupported;
        public int InArchiveFileIndex;

        public CompressedMod(Mod Mod)
        {
            this.Mod = Mod;
        }
    }
}
