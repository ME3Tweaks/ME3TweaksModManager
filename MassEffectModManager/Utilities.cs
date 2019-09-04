using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager
{
    public static class Utilities
    {
        public static string GetExecutableDirectory() => Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public static string GetModsDirectory() => Path.Combine(GetExecutableDirectory(), "mods");
        public static string GetME3ModsDirectory() => Path.Combine(GetModsDirectory(), "ME3");
        public static string GetME2ModsDirectory() => Path.Combine(GetModsDirectory(), "ME2");
        public static string GetME1ModsDirectory() => Path.Combine(GetModsDirectory(), "ME1");
    }
}
