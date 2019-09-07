using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager;

namespace MassEffectModManager
{
    public static class Utilities
    {
        public static string GetExecutableDirectory() => Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public static string GetModsDirectory()
        {
            var libraryPath = Properties.Settings.Default.ModLibraryPath;
            if (Directory.Exists(libraryPath))
            {
                return libraryPath;
            }
            else
            {
                return Path.Combine(GetExecutableDirectory(), "mods");
            }
        }

        public static string GetModDirectoryForGame(Mod.MEGame game)
        {
            if (game == Mod.MEGame.ME1) return GetME1ModsDirectory();
            if (game == Mod.MEGame.ME2) return GetME2ModsDirectory();
            if (game == Mod.MEGame.ME3) return GetME3ModsDirectory();
            return null;
        }

        internal static void EnsureDirectories()
        {
            Directory.CreateDirectory(GetME3ModsDirectory());
            Directory.CreateDirectory(GetME2ModsDirectory());
            Directory.CreateDirectory(GetME1ModsDirectory());
        }

        internal static string GetME3TweaksServicesCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "ME3TweaksServicesCache")).FullName;
        }

        internal static bool CanFetchContentThrottleCheck()
        {
            var lastContentCheck = Properties.Settings.Default.LastContentCheck;
            var timeNow = DateTime.Now;
            return (timeNow - lastContentCheck).TotalDays > 1;
        }

        internal static string GetThirdPartyIdentificationCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "thirdpartyidentificationservice.json");
        }

        /**
	 * Replaces all break (br between <>) lines with a newline character. Used
	 * to add newlines to ini4j.
	 * 
	 * @param string
	 *            String to parse
	 * @return String that has been fixed
	 */
        public static string ConvertBrToNewline(string str) => str?.Replace("<br>", "\n");
        public static string ConvertNewlineToBr(string str) => str?.Replace("\n", "<br>");


        public static string GetME3ModsDirectory() => Path.Combine(GetModsDirectory(), "ME3");
        public static string GetME2ModsDirectory() => Path.Combine(GetModsDirectory(), "ME2");
        public static string GetME1ModsDirectory() => Path.Combine(GetModsDirectory(), "ME1");

        public static void OpenWebpage(string uri)
        {
            Process.Start(uri);
        }

        internal static string GetAppCrashHandledFile()
        {
            return Path.Combine(Utilities.GetAppDataFolder(), "APP_CRASH_HANDLED");
        }

        internal static string GetAppCrashFile()
        {
            return Path.Combine(Utilities.GetAppDataFolder(), "APP_CRASH");
        }

        internal static string GetAppDataFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MassEffectModManager");
            Directory.CreateDirectory(folder);
            return folder;
        }

    }
}
