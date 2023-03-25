using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.me3tweaks.online;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace ME3TweaksModManager.Tests
{
    public static class GlobalTest
    {

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        private static bool initialized;
        internal const string TESTDATA_FOLDER_NAME = "testdata";
        internal const string TESTDATA_REPO_NAME = "ModTestingData";

        /// <summary>
        /// The directory 'testdata' that has been found
        /// </summary>
        private static string TestDataPath;

        internal static void Init()
        {
            if (!initialized)
            {
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.sevenzipwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "sevenzipwrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.lzo2wrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "lzo2wrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.zlibwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "zlibwrapper.dll"), false, Assembly.GetAssembly(typeof(GameTarget)));
                //SetDllDirectory(Utilities.GetDllDirectory());
                FindTestData();
                ME3TweaksCoreLibInitPackage package = new ME3TweaksCoreLibInitPackage()
                {
                    RunOnUiThreadDelegate = x => x(),
                };
                ME3TweaksCoreLib.Initialize(package);
                // LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
                Settings.LogModStartup = true;
                App.BuildNumber = 127; //THIS NEEDS TO BE UPDATED FOR EVERY MOD THAT TARGETS A NEWER RELEASE. Not really a convenient way to update it constantly though...
#if !AZURE
                Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.Debug().CreateLogger();
#else
                Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
#endif
                DeleteScratchDir();

                //BackupService.RefreshBackupStatus(null); // used in mixin testing

                CombinedServiceData = JsonConvert.DeserializeObject<JToken>(MOnlineContent.FetchRemoteString(M3ServiceLoader.CombinedServiceFetchURL.MainURL));
                initialized = true;
            }
        }

        private static void FindTestData()
        {
            // Find 'ModTestingData', which is a git repo name. It should be in a parent-side directory at some point up the chain
            // This really only works on Mgamerz and Azure setups, sorry other testers...

            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, TESTDATA_REPO_NAME);
                if (Directory.Exists(testDataPath))
                {
                    var testdata = Path.Combine(testDataPath, TESTDATA_FOLDER_NAME);
                    if (Directory.Exists(testdata))
                    {
                        TestDataPath = testdata;
                        return;
                    }
                }
            }

            throw new Exception($@"Cannot find testing data repo '{TESTDATA_REPO_NAME}'. It must be a parent of {Directory.GetParent(Assembly.GetExecutingAssembly().Location)}");
        }

        public static string GetTestModsDirectory() => Path.Combine(TestDataPath, "mods");
        public static string GetTestGameFoldersDirectory() => Path.Combine(TestDataPath, "gamedirectories");
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

        /// <summary>
        /// Returns the subfolder of the TestDataPath combined with the parameter
        /// </summary>
        /// <param name="testdataDirName"></param>
        /// <returns></returns>
        public static string GetTestingDataDirectoryFor(string testdataDirName)
        {
            return Path.Combine(TestDataPath, testdataDirName);
        }

        /// <summary>
        /// The cached combined services data
        /// </summary>
        public static JToken CombinedServiceData { get; set; }

        public static void PrintDiff<T>(this T[] first, T[] second)
        {
            var max = Math.Min(first.Length, second.Length);

            for (int i = 0; i < max; i++)
            {
                var val1 = first[i];
                var val2 = second[i];

                if (!val1.Equals(val2))
                {
                    Console.WriteLine($@"Difference at position {i}, first value: {val1}, second value {val2}");
                    return;
                }

            }

            // We hit the end
            Console.WriteLine(
                $@"Arrays are the same up the minimum length: first len {first.Length}, second len {second.Length}");

        }
    }
}
