using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager;
using ME3TweaksModManager.modmanager;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;

namespace MassEffectModManagerCore.Tests
{
    public static class GlobalTest
    {

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        private static bool initialized;
        internal const string TESTDATA_FOLDER_NAME = "testdata";

        internal static void Init()
        {
            if (!initialized)
            {
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.sevenzipwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "sevenzipwrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.lzo2wrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "lzo2wrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.zlibwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "zlibwrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //SetDllDirectory(Utilities.GetDllDirectory());

                LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
                Settings.LogModStartup = true;
                App.BuildNumber = 105; //THIS NEEDS TO BE UPDATED FOR EVERY MOD THAT TARGETS A NEWER RELEASE
                Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.Debug().CreateLogger();
                DeleteScratchDir();

                //BackupService.RefreshBackupStatus(null); // used in mixin testing

                initialized = true;
            }
        }

        public static string GetTestModsDirectory() => Path.Combine(FindDirectoryInParentDirectories(TESTDATA_FOLDER_NAME), "mods");
        public static string GetTestGameFoldersDirectory() => Path.Combine(FindDirectoryInParentDirectories(TESTDATA_FOLDER_NAME), "gamedirectories");
        public static string GetTestGameFoldersDirectory(MEGame game) => Path.Combine(GetTestGameFoldersDirectory(), game.ToString().ToLowerInvariant());
        public static string GetScratchDir() => Path.Combine(Directory.GetParent(GetTestModsDirectory()).FullName, "Scratch");

        public static void CreateScratchDir() => Directory.CreateDirectory(GetScratchDir());

        public static void DeleteScratchDir()
        {
            if (Directory.Exists(GetScratchDir()))
            {
                M3Utilities.DeleteFilesAndFoldersRecursively(GetScratchDir());
            }
        }

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
