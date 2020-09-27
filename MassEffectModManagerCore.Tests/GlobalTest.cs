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

        private static bool initialized;
        internal const string TESTDATA_FOLDER_NAME = "testdata";

        internal static void Init()
        {
            if (!initialized)
            {
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
                Settings.LogModStartup = true;
                App.BuildNumber = 105; //THIS NEEDS TO BE UPDATED FOR EVERY MOD THAT TARGETS A NEWER RELEASE
                Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
                initialized = true;
            }
        }

        public static string GetTestModsDirectory() => Path.Combine(FindDirectoryInParentDirectories(TESTDATA_FOLDER_NAME), "mods");
        public static string GetTestGameFoldersDirectory() => Path.Combine(FindDirectoryInParentDirectories(TESTDATA_FOLDER_NAME), "gamedirectories");
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

        /// <summary>
        /// Finds the named directory in an upward directory search. Looks at children of each level for a directory named directoryname.
        /// </summary>
        /// <returns></returns>
        public static string FindDirectoryInParentDirectories(string directoryname)
        {
            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, directoryname);
                if (Directory.Exists(testDataPath)) return testDataPath;
            }

            throw new Exception($"Could not find {directoryname} directory in any parent path's children!");
        }
    }
}
