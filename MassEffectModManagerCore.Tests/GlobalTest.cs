using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MassEffectModManagerCore.modmanager;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;

namespace MassEffectModManagerCore.Tests
{
    public static class GlobalTest
    {
        /// <summary>
        /// Looks in parent folders for folder containing a folder named "testdata" as Azure DevOps seems to build project differently than on a VS installation
        /// </summary>
        /// <returns></returns>
        public static string GetTestDataDirectory()
        {
            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, "testdata");
                if (Directory.Exists(testDataPath)) return testDataPath;
            }

            throw new Exception("Could not find testdata directory!");
        }

        private static bool initialized;
        internal static void Init()
        {
            if (!initialized)
            {
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
                Settings.LogModStartup = true;
                Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
                initialized = true;
            }
        }

        public static string GetTestModsDirectory() => Path.Combine(GetTestDataDirectory(), "mods");
        public static string GetTestGameFoldersDirectory() => Path.Combine(GetTestDataDirectory(), "gamedirectories");
        public static string GetTestGameFoldersDirectory(Mod.MEGame game) => Path.Combine(GetTestGameFoldersDirectory(), game.ToString().ToLowerInvariant());

        public static (string md5, int size, int nummodsexpected) ParseRealArchiveAttributes(string filename)
        {
            string fname = Path.GetFileNameWithoutExtension(filename);
            string[] parts = fname.Split('-');
            string md5 = parts.Last();
            int size = int.Parse(parts[^2]);
            int nummodsexpected = int.Parse(parts[^3]);
            return (md5, size, nummodsexpected);
        }
    }
}
