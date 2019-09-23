using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.GameDirectories;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManagerCore;
using MassEffectModManagerCore.modmanager;
using Serilog;
using static MassEffectModManager.modmanager.Mod;

namespace MassEffectModManager
{
    public static class Utilities
    {
        public static string GetMMExecutableDirectory() => Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public static string GetModsDirectory()
        {
            var libraryPath = Settings.ModLibraryPath;
            if (Directory.Exists(libraryPath))
            {
                return libraryPath;
            }
            else
            {
                return Path.Combine(GetMMExecutableDirectory(), "mods");
            }
        }

        internal static string GetLocalHelpFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "cachedhelp.xml");
        }

        internal static string GetObjectInfoFolder()
        {
            return Directory.CreateDirectory(Path.Combine(Utilities.GetAppDataFolder(), "ObjectInfo")).FullName;
        }

        internal static string GetDataDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetMMExecutableDirectory(), "data")).FullName;
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

        internal static string GetLocalHelpResourcesDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetME3TweaksServicesCache(), "HelpResources")).FullName;
        }

        public static string CalculateMD5(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (IOException e)
            {
                Log.Error("I/O ERROR CALCULATING CHECKSUM OF FILE: " + filename);
                Log.Error(App.FlattenException(e));
                return "";
            }
        }

        internal static string GetTipsServiceFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "tipsservice.json");
        }

        internal static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "thirdpartyimportingservice.json");
        }

        internal static bool CanFetchContentThrottleCheck()
        {
            var lastContentCheck = Settings.LastContentCheck;
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

        internal static string Get7zDllPath()
        {
            return Path.Combine(Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "libraries")).FullName, "7z.dll");
        }

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

        public static Stream GetResourceStream(string assemblyResource)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }

        internal static string ExtractInternalFile(string internalResourceName, string destination, bool overwrite)
        {
            Log.Information("Extracting embedded file: " + internalResourceName + " to " + destination);
            if (!File.Exists(destination) || overwrite)
            {

                using (Stream stream = Utilities.GetResourceStream(internalResourceName))
                {

                    using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(file);
                    }
                }
            }
            else
            {
                Log.Warning("File already exists. Not overwriting file.");
            }
            return destination;
        }

        internal static string GetExecutableDirectory(GameTarget target)
        {
            if (target.Game == Mod.MEGame.ME1 || target.Game == Mod.MEGame.ME2) return Path.Combine(target.TargetPath, "Binaries");
            if (target.Game == Mod.MEGame.ME3) return Path.Combine(target.TargetPath, "Binaries", "win32");
            return null;
        }

        internal static bool InstallBinkBypass(GameTarget target)
        {
            if (target == null) return false;
            var binkPath = GetBinkw32File(target);
            Log.Information($"Installing Binkw32 bypass for {target.Game} to {binkPath}");

            if (target.Game == Mod.MEGame.ME1)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me1.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me1.binkw23.dll", obinkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME2)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me2.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me2.binkw23.dll", obinkPath, true);

            }
            else if (target.Game == Mod.MEGame.ME3)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me3.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me3.binkw23.dll", obinkPath, true);
            }
            else
            {
                Log.Error("Unknown game for gametarget (InstallBinkBypass)");
                return false;
            }
            Log.Information($"Installed Binkw32 bypass for {target.Game}");
            return true;
        }

        internal static string GetBinkw32File(GameTarget target)
        {
            if (target == null) return null;
            if (target.Game == Mod.MEGame.ME1) return Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
            if (target.Game == Mod.MEGame.ME2) return Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
            if (target.Game == Mod.MEGame.ME3) return Path.Combine(target.TargetPath, "Binaries", "win32", "binkw32.dll");
            return null;
        }

        internal static bool UninstallBinkBypass(GameTarget target)
        {
            if (target == null) return false;
            var binkPath = GetBinkw32File(target);
            if (target.Game == Mod.MEGame.ME1)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me1.binkw23.dll", binkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME2)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me2.binkw23.dll", binkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME3)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw23.dll");
                File.Delete(obinkPath);

                Utilities.ExtractInternalFile("MassEffectModManager.modmanager.binkw32.me3.binkw23.dll", binkPath, true);
            }
            return true;
        }

        internal static string GetCachedTargetsFile(Mod.MEGame game)
        {
            return Path.Combine(GetAppDataFolder(), $"GameTargets{game}.txt");
        }

        internal static List<GameTarget> GetCachedTargets(Mod.MEGame game)
        {
            var cacheFile = GetCachedTargetsFile(game);
            if (File.Exists(cacheFile))
            {
                List<GameTarget> targets = new List<GameTarget>();
                foreach (var file in File.ReadAllLines(cacheFile))
                {
                    //Validate game directory
                    GameTarget target = new GameTarget(game, file, false);
                    var failureReason = ValidateGameTarget(target);
                    if (failureReason == null)
                    {
                        targets.Add(target);
                    }
                    else
                    {
                        Log.Error("Cached target for " + target.Game.ToString() + " is invalid: " + failureReason);
                    }
                }

                return targets;
            }
            else
            {
                return new List<GameTarget>();
            }
        }

        internal static string GetGameConfigToolPath(GameTarget target)
        {
            switch (target.Game)
            {
                case MEGame.ME1:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffectConfig.exe");
                case MEGame.ME2:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffect2Config.exe");
                case MEGame.ME3:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffect3Config.exe");
            }
            return null;
        }

        internal static void AddCachedTarget(GameTarget target)
        {
            var cachefile = GetCachedTargetsFile(target.Game);
            if (!File.Exists(cachefile)) File.Create(cachefile).Close();
            var savedTargets = File.ReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(target.TargetPath); //standardize

            if (!savedTargets.Contains(path, StringComparer.InvariantCultureIgnoreCase))
            {
                savedTargets.Add(path);
                Log.Information($"Saving new entry into targets cache for {target.Game}: " + path);
                File.WriteAllLines(cachefile, savedTargets);
            }
        }

        private const string ME1ASILoaderHash = "30660f25ab7f7435b9f3e1a08422411a";
        private const string ME2ASILoaderHash = "a5318e756893f6232284202c1196da13";
        private const string ME3ASILoaderHash = "1acccbdae34e29ca7a50951999ed80d5";
        internal static bool CheckIfBinkw32ASIIsInstalled(GameTarget target)
        {
            if (target == null) return false;
            string binkPath = null;
            string expectedHash = null;
            if (target.Game == Mod.MEGame.ME1)
            {
                binkPath = Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME1ASILoaderHash;
            }
            else if (target.Game == Mod.MEGame.ME2)
            {
                binkPath = Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME2ASILoaderHash;
            }
            else if (target.Game == Mod.MEGame.ME3)
            {
                binkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw32.dll");
                expectedHash = ME3ASILoaderHash;
            }
            if (File.Exists(binkPath))
            {
                return CalculateMD5(binkPath) == expectedHash;
            }
            return false;
        }

        /// <summary>
        /// Validates a game directory by checking for multiple things that should be present in a working game.
        /// </summary>
        /// <param name="target">Game target to check</param>
        /// <returns>String of failure reason, null if OK</returns>
        public static string ValidateGameTarget(GameTarget target)
        {
            string basePath = target.TargetPath;
            switch (target.Game)
            {
                case Mod.MEGame.ME1:
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "Maps", "EntryMenu.SFM"))) return "Invalid game directory: Entrymenu.sfm not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "BIOC_Base.u"))) return "Invalid game directory: BIOC_Base.u not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "Packages", "Textures", "BIOA_GLO_00_A_Opening_FlyBy_T.upk"))) return "Invalid game directory: BIOA_GLO_00_A_Opening_FlyBy_T.upk not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "Maps", "WAR", "LAY", "BIOA_WAR20_05_LAY.SFM"))) return "Invalid game directory: Entrymenu.sfm not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "Movies", "MEvisionSEQ3.bik"))) return "Invalid game directory: MEvisionSEQ3.bik not found";
                    return null;
                case Mod.MEGame.ME2:
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "BioA_BchLmL.pcc"))) return "Invalid game directory: BioA_BchLmL.pcc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "Config", "PC", "Cooked", "Coalesced.ini"))) return "Invalid game directory: Coalesced.ini not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "Wwise_Jack_Loy_Music.afc"))) return "Invalid game directory: Wwise_Jack_Loy_Music.afc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPC", "WwiseAudio.pcc"))) return "Invalid game directory: WwiseAudio.pcc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "Movies", "Crit03_CollectArrive_Part2_1.bik"))) return "Invalid game directory: Crit03_CollectArrive_Part2_1.bik not found";
                    return null;
                case Mod.MEGame.ME3:
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPCConsole", "Textures.tfc"))) return "Invalid game directory: Textures.tfc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPCConsole", "Startup.pcc"))) return "Invalid game directory: Startup.pcc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPCConsole", "Coalesced.bin"))) return "Invalid game directory: Coalesced.bin not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "Patches", "PCConsole", "Patch_001.sfar"))) return "Invalid game directory: Patch_001.sfar not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPCConsole", "Textures.tfc"))) return "Invalid game directory: Textures.tfc not found";
                    if (!File.Exists(Path.Combine(basePath, "BioGame", "CookedPCConsole", "citwrd_rp1_bailey_m_D_Int.afc"))) return "Invalid game directory: citwrd_rp1_bailey_m_D_Int.afc not found";
                    return null;
            }

            return null;
        }

        /// <summary>
        /// Checks if the specified DLC folder name is protected (official DLC names and __metadata)
        /// </summary>
        /// <param name="dlcFolderName">DLC folder name (DLC_CON_MP2)</param>
        /// <param name="game">Game to test against</param>
        /// <returns>True if protected, false otherwise</returns>
        internal static bool IsProtectedDLCFolder(string dlcFolderName, MEGame game) => dlcFolderName.Equals("__metadata", StringComparison.InvariantCultureIgnoreCase) && MEDirectories.OfficialDLC(game).Contains(dlcFolderName, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Gets the path to the testpatch DLC file for the specified game target
        /// </summary>
        /// <param name="target">target to resolve path for</param>
        /// <returns>Null if gametarget game is not ME3. Path where SFAR should be if ME3.</returns>
        public static string GetTestPatchPath(GameTarget target)
        {
            if (target.Game != MEGame.ME3) return null;
            return Path.Combine(target.TargetPath, @"BIOGame\Patches\PCConsole\Patch_001.sfar");
        }

        //Step 1: https://stackoverflow.com/questions/2435894/net-how-do-i-check-for-illegal-characters-in-a-path
        private static string RemoveSpecialCharactersUsingCustomMethod(this string expression, bool removeSpecialLettersHavingASign = true)
        {
            var newCharacterWithSpace = " ";
            var newCharacter = "";

            // Return carriage handling
            // ASCII LINE-FEED character (LF),
            expression = expression.Replace("\n", newCharacterWithSpace);
            // ASCII CARRIAGE-RETURN character (CR) 
            expression = expression.Replace("\r", newCharacterWithSpace);

            // less than : used to redirect input, allowed in Unix filenames, see Note 1
            expression = expression.Replace(@"<", newCharacter);
            // greater than : used to redirect output, allowed in Unix filenames, see Note 1
            expression = expression.Replace(@">", newCharacter);
            // colon: used to determine the mount point / drive on Windows; 
            // used to determine the virtual device or physical device such as a drive on AmigaOS, RT-11 and VMS; 
            // used as a pathname separator in classic Mac OS. Doubled after a name on VMS, 
            // indicates the DECnet nodename (equivalent to a NetBIOS (Windows networking) hostname preceded by "\\".). 
            // Colon is also used in Windows to separate an alternative data stream from the main file.
            expression = expression.Replace(@":", newCharacter);
            // quote : used to mark beginning and end of filenames containing spaces in Windows, see Note 1
            expression = expression.Replace(@"""", newCharacter);
            // slash : used as a path name component separator in Unix-like, Windows, and Amiga systems. 
            // (The MS-DOS command.com shell would consume it as a switch character, but Windows itself always accepts it as a separator.[16][vague])
            expression = expression.Replace(@"/", newCharacter);
            // backslash : Also used as a path name component separator in MS-DOS, OS/2 and Windows (where there are few differences between slash and backslash); allowed in Unix filenames, see Note 1
            expression = expression.Replace(@"\", newCharacter);
            // vertical bar or pipe : designates software pipelining in Unix and Windows; allowed in Unix filenames, see Note 1
            expression = expression.Replace(@"|", newCharacter);
            // question mark : used as a wildcard in Unix, Windows and AmigaOS; marks a single character. Allowed in Unix filenames, see Note 1
            expression = expression.Replace(@"?", newCharacter);
            expression = expression.Replace(@"!", newCharacter);
            // asterisk or star : used as a wildcard in Unix, MS-DOS, RT-11, VMS and Windows. Marks any sequence of characters 
            // (Unix, Windows, later versions of MS-DOS) or any sequence of characters in either the basename or extension 
            // (thus "*.*" in early versions of MS-DOS means "all files". Allowed in Unix filenames, see note 1
            expression = expression.Replace(@"*", newCharacter);
            // percent : used as a wildcard in RT-11; marks a single character.
            expression = expression.Replace(@"%", newCharacter);
            // period or dot : allowed but the last occurrence will be interpreted to be the extension separator in VMS, MS-DOS and Windows. 
            // In other OSes, usually considered as part of the filename, and more than one period (full stop) may be allowed. 
            // In Unix, a leading period means the file or folder is normally hidden.
            expression = expression.Replace(@".", newCharacter);
            // space : allowed (apart MS-DOS) but the space is also used as a parameter separator in command line applications. 
            // This can be solved by quoting, but typing quotes around the name every time is inconvenient.
            //expression = expression.Replace(@"%", " ");
            expression = expression.Replace(@"  ", newCharacter);

            if (removeSpecialLettersHavingASign)
            {
                // Because then issues to zip
                // More at : http://www.thesauruslex.com/typo/eng/enghtml.htm
                expression = expression.Replace(@"ê", "e");
                expression = expression.Replace(@"ë", "e");
                expression = expression.Replace(@"ï", "i");
                expression = expression.Replace(@"œ", "oe");
            }

            return expression;
        }

        /// <summary>
        /// Sanitizes a path by removing disallowed characters
        /// </summary>
        /// <param name="path">Path string</param>
        /// <returns>Sanitized path string</returns>
        public static string SanitizePath(string path)
        {
            path = path.RemoveSpecialCharactersUsingCustomMethod();
            if (path.ContainsAnyInvalidCharacters())
            {
                path = path.RemoveSpecialCharactersUsingFrameworkMethod();
            }
            return path;
        }

        internal static List<string> GetPackagesInDirectory(string path, bool subdirectories)
        {
            return Directory.EnumerateFiles(path, "*.*", subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".pcc", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".sfm",StringComparison.InvariantCultureIgnoreCase) 
                 || s.EndsWith(".u",StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".upk", StringComparison.InvariantCultureIgnoreCase)).ToList();
        }
    }
}
