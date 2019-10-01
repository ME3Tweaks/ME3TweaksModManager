using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MassEffectModManagerCore.modmanager;
using Serilog;

namespace MassEffectModManagerCore.Tests
{
    public static class GlobalTest
    {
        public static string GetTestDirectory()
        {
            var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(exeDirectory).FullName).FullName).FullName).FullName;
        }

        public static string GetTestDataDirectory() => Path.Combine(GetTestDirectory(), "testdata");
        private static bool initialized;
        internal static void Init()
        {
            if (!initialized)
            {
                Settings.LogModStartup = true;
                Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
                initialized = true;
            }
        }

        public static string GetTestModsDirectory() => Path.Combine(GetTestDataDirectory(), "mods");
        public static string GetTestGameFoldersDirectory() => Path.Combine(GetTestDataDirectory(), "gamedirectories");
        public static string GetTestGameFoldersDirectory(Mod.MEGame game) => Path.Combine(GetTestGameFoldersDirectory(), game.ToString().ToLowerInvariant());

    }
}
