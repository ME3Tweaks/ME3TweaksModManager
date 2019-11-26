using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using MassEffectModManagerCore;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using Microsoft.Win32;
using Serilog;

namespace MassEffectModManagerCore
{
    public static class Utilities
    {
        public static string GetMMExecutableDirectory() => Path.GetDirectoryName(App.ExecutableLocation);

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

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static string GetNexusModsCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "nexusmodsintegration")).FullName;
        }

        public static bool CreateDirectoryWithWritePermission(string directoryPath)
        {
            if (Utilities.IsDirectoryWritable(Directory.GetParent(directoryPath).FullName))
            {
                Directory.CreateDirectory(directoryPath);
                return true;
            }

            //Must have admin rights.
            Log.Information("We need admin rights to create this directory");
            string exe = GetCachedExecutablePath("PermissionsGranter.exe");
            Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
            string args = "\"" + System.Security.Principal.WindowsIdentity.GetCurrent().Name + "\" -create-directory \"" + directoryPath.TrimEnd('\\') + "\"";
            try
            {
                int result = Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true, noWindow: true);
                if (result == 0)
                {
                    Log.Information("Elevated process returned code 0, restore directory is hopefully writable now.");
                    return true;
                }
                else
                {
                    Log.Error("Elevated process returned code " + result + ", directory likely is not writable");
                    return false;
                }
            }
            catch (Exception e)
            {
                if (e is Win32Exception w32e)
                {
                    if (w32e.NativeErrorCode == 1223)
                    {
                        //Admin canceled.
                        return false;
                    }
                }
                Log.Error("Error creating directory with PermissionsGranter: " + e.Message);
                return false;

            }
        }


        public static bool EnableWritePermissionsToFolders(List<GameTarget> targets, bool me1ageia)
        {
            string args = "";
            if (targets.Any() || me1ageia)
            {
                foreach (var target in targets)
                {
                    if (args != "")
                    {
                        args += " ";
                    }
                    args += $"\"{target.TargetPath}\"";
                }

                if (me1ageia)
                {
                    args += " -create-hklm-reg-key \"SOFTWARE\\WOW6432Node\\AGEIA Technologies\"";
                }
                string exe = GetCachedExecutablePath("PermissionsGranter.exe");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                args = $"\"{System.Security.Principal.WindowsIdentity.GetCurrent().Name}\" " + args;
                //need to run write permissions program
                if (IsAdministrator())
                {
                    int result = Utilities.RunProcess(exe, args, true, false);
                    if (result == 0)
                    {
                        Log.Information("Elevated process returned code 0, directories are hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        Log.Error("Elevated process returned code " + result + ", directories probably aren't writable.");
                        return false;
                    }
                }
                else
                {
                    //string message = "Some game folders/registry keys are not writeable by your user account. ALOT Installer will attempt to grant access to these folders/registry with the PermissionsGranter.exe program:\n";
                    //if (required)
                    //{
                    //    message = "Some game paths and registry keys are not writeable by your user account. These need to be writable or ALOT Installer will be unable to install ALOT. Please grant administrative privledges to PermissionsGranter.exe to give your account the necessary privileges to the following:\n";
                    //}
                    //foreach (String str in directories)
                    //{
                    //    message += "\n" + str;
                    //}
                    //if (me1ageia)
                    //{
                    //    message += "\nRegistry: HKLM\\SOFTWARE\\WOW6432Node\\AGEIA Technologies (Fixes an ME1 launch issue)";
                    //}
                    int result = Utilities.RunProcess(exe, args, true, true);
                    if (result == 0)
                    {
                        Log.Information("Elevated process returned code 0, directories are hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        Log.Error("Elevated process returned code " + result + ", directories probably aren't writable.");
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the path where the specified static executable would be. This call does not check if that file exists.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCachedExecutablePath(string path)
        {
            return Path.Combine(Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "executables")).FullName, path);
        }

        /// <summary>
        /// Returns Temp/VPatchRedirects
        /// </summary>
        /// <returns></returns>
        internal static string GetVPatchRedirectsFolder()
        {
            return Path.Combine(Utilities.GetTempPath(), "VPatchRedirects");
        }

        //(Exception e)
        //    {
        //        Log.Error("Error checking for write privledges. This may be a significant sign that an installed game is not in a good state.");
        //        Log.Error(App.FlattenException(e));
        //        await this.ShowMessageAsync("Error checking write privileges", "An error occured while checking write privileges to game folders. This may be a sign that the game is in a bad state.\n\nThe error was:\n" + e.Message);
        //        return false;
        //}
        //    return true;
        //}

        /// <summary> Checks for write access for the given file.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        /// <returns>true, if write access is allowed, otherwise false</returns>
        public static bool IsDirectoryWritable(string dir)
        {
            var files = Directory.GetFiles(dir);
            try
            {
                System.IO.File.Create(Path.Combine(dir, "temp_m3.txt")).Close();
                System.IO.File.Delete(Path.Combine(dir, "temp_m3.txt"));
                return true;
            }
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Error("Error checking permissions to folder: " + dir);
                Log.Error("Directory write test had error that was not UnauthorizedAccess: " + e.Message);
            }
            return false;
        }

        internal static void WriteRegistryKey(string subpath, string value, string data)
        {
            int i = 0;
            List<string> subkeys = subpath.Split('\\').ToList();
            RegistryKey subkey;
            if (subkeys[0] == "HKEY_CURRENT_USER")
            {
                subkeys.RemoveAt(0);
                subkey = Registry.CurrentUser;
            }
            else
            {
                throw new Exception("Currently only HKEY_CURRENT_USER keys are supported for writing.");
            }

            while (i < subkeys.Count)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        internal static MemoryStream ExtractInternalFileToStream(string internalResourceName)
        {
            Log.Information("Extracting embedded file: " + internalResourceName + " to memory");
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif


            using (Stream stream = Utilities.GetResourceStream(internalResourceName))
            {
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        internal static int GetDisplayableVersionFieldCount(Version parsedModVersion)
        {
            int fieldCount = 2;
            if (parsedModVersion.Build > 0)
            {
                fieldCount = 3;
            }

            if (parsedModVersion.Revision > 0)
            {
                fieldCount = 4;
            }

            return fieldCount;
        }

        internal static void HighlightInExplorer(string filePath)
        {
            string argument = "/select, \"" + filePath + "\"";

            System.Diagnostics.Process.Start("explorer.exe", argument);
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

        public static string ReadLockedTextFile(string file)
        {
            try
            {
                using (FileStream fileStream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    using (StreamReader streamReader = new StreamReader(fileStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static int RunProcess(string exe, string args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true)
        {
            return RunProcess(exe, null, args, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow);
        }

        public static int RunProcess(string exe, List<string> args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true)
        {
            return RunProcess(exe, args, null, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow);
        }


        private static int RunProcess(string exe, List<string> argsL, string argsS, bool waitForProcess, bool allowReattemptAsAdmin, bool requireAdmin, bool noWindow)
        {
            var argsStr = argsS;
            if (argsStr == null && argsL != null)
            {
                argsStr = "";
                foreach (var arg in argsL)
                {
                    if (arg != "") argsStr += " ";
                    if (arg.Contains(" "))
                    {
                        argsStr += $"\"{arg}\"";
                    }
                    else
                    {
                        argsStr += arg;
                    }
                }
            }
            if (requireAdmin)
            {
                Log.Information($"Running process as admin: {exe} {argsStr}");
                //requires elevation
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = exe;
                    p.StartInfo.UseShellExecute = true;
                    p.StartInfo.CreateNoWindow = noWindow;
                    p.StartInfo.Arguments = argsStr;
                    p.StartInfo.Verb = "runas";
                    p.Start();
                    if (waitForProcess)
                    {
                        p.WaitForExit();
                        return p.ExitCode;
                    }

                    return -1;
                }
            }
            else
            {
                Log.Information($"Running process: {exe} {argsStr}");
                try
                {
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = exe;
                        p.StartInfo.UseShellExecute = true;
                        p.StartInfo.CreateNoWindow = noWindow;
                        p.StartInfo.Arguments = argsStr;
                        p.Start();
                        if (waitForProcess)
                        {
                            p.WaitForExit();
                            return p.ExitCode;
                        }

                        return -1;
                    }
                }
                catch (Win32Exception w32e)
                {
                    Log.Warning("Win32 exception running process: " + w32e.ToString());
                    if (w32e.NativeErrorCode == 740 && allowReattemptAsAdmin)
                    {
                        Log.Information("Attempting relaunch with administrative rights.");
                        //requires elevation
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = exe;
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.CreateNoWindow = noWindow;
                            p.StartInfo.Arguments = argsStr;
                            p.StartInfo.Verb = "runas";
                            p.Start();
                            if (waitForProcess)
                            {
                                p.WaitForExit();
                                return p.ExitCode;
                            }

                            return -1;
                        }
                    }
                    else
                    {
                        throw w32e; //rethrow to higher.
                    }
                }
            }
        }

        public static bool DeleteFilesAndFoldersRecursively(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Debug.WriteLine("Directory to delete doesn't exist: " + targetDirectory);
                return true;
            }
            bool result = true;
            foreach (string file in Directory.GetFiles(targetDirectory))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Log.Error($"Unable to delete file: {file}. It may be open still: {e.Message}");
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(targetDirectory))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(10); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + targetDirectory);

                Directory.Delete(targetDirectory);
            }
            catch (Exception e)
            {
                Log.Error($"Unable to delete directory: {targetDirectory}. It may be open still or may not be actually empty: {e.Message}");
                return false;
            }
            return result;
        }


        internal static void InstallEmbeddedASI(string asiFname, double installingVersion, GameTarget gameTarget)
        {
            string asiTargetDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetExecutableDirectory(gameTarget), "asi")).FullName;

            var existingmatchingasis = Directory.GetFiles(asiTargetDirectory, asiFname.Substring(0, asiFname.LastIndexOf('-')) + "*").ToList();
            if (existingmatchingasis.Count > 0)
            {
                foreach (var v in existingmatchingasis)
                {
                    string shortName = Path.GetFileNameWithoutExtension(v);
                    var asiVersion = shortName.Substring(shortName.LastIndexOf('-') + 2); //Todo: Try catch this as it might explode if for some reason filename is like ASIMod-.asi
                    if (double.TryParse(asiVersion, out double version) && version > installingVersion)
                    {
                        Log.Information("A newer version of a supporting ASI is installed: " + shortName + ". Not installing ASI.");
                        return;
                    }
                }
            }

            //Todo: Use ASI manifest to identify malformed names
            string asiPath = "MassEffectModManagerCore.modmanager.asi." + asiFname + ".asi";
            Utilities.ExtractInternalFile(asiPath, Path.Combine(asiTargetDirectory, asiFname + ".asi"), true);
        }

        public static string GetModDirectoryForGame(Mod.MEGame game)
        {
            if (game == Mod.MEGame.ME1) return GetME1ModsDirectory();
            if (game == Mod.MEGame.ME2) return GetME2ModsDirectory();
            if (game == Mod.MEGame.ME3) return GetME3ModsDirectory();
            return null;
        }

        internal static string GetDllDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "dlls")).FullName;
        }

        internal static void EnsureModDirectories()
        {
            //Todo: Ensure these are not under any game targets.
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
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filename);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (IOException e)
            {
                Log.Error("I/O ERROR CALCULATING CHECKSUM OF FILE: " + filename);
                Log.Error(App.FlattenException(e));
                return "";
            }
        }

        public static string CalculateMD5(Stream stream)
        {
            try
            {
                using var md5 = MD5.Create();
                stream.Position = 0;
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception e)
            {
                Log.Error("I/O ERROR CALCULATING CHECKSUM OF STREAM");
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
            return Path.Combine(GetDllDirectory(), "7z.dll");
        }

        public static string GetME1ModsDirectory() => Path.Combine(GetModsDirectory(), "ME1");

        public static void OpenWebpage(string uri)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Log.Error("Exception trying to open web page from system (typically means browser default is incorrectly configured by Windows): " + e.Message + ". Try opening the URL manually: " + uri);
            }
        }

        /// <summary>
        /// Determines if a specific game is running. 
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public static bool IsGameRunning(Mod.MEGame gameID)
        {
            if (gameID == Mod.MEGame.ME1)
            {
                Process[] pname = Process.GetProcessesByName("MassEffect");
                return pname.Length > 0;
            }
            if (gameID == Mod.MEGame.ME2)
            {
                Process[] pname = Process.GetProcessesByName("MassEffect2");
                Process[] pname2 = Process.GetProcessesByName("ME2Game");
                return pname.Length > 0 || pname2.Length > 0;
            }
            else
            {
                Process[] pname = Process.GetProcessesByName("MassEffect3");
                return pname.Length > 0;
            }
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

        internal static object GetGameName(Mod.MEGame game)
        {
            if (game == Mod.MEGame.ME1) return "Mass Effect";
            if (game == Mod.MEGame.ME2) return "Mass Effect 2";
            if (game == Mod.MEGame.ME3) return "Mass Effect 3";
            return "Error: Unknown game";
        }

        internal static string ExtractInternalFile(string internalResourceName, string destination, bool overwrite)
        {
            Log.Information("Extracting embedded file: " + internalResourceName + " to " + destination);
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif
            if (!File.Exists(destination) || overwrite || new FileInfo(destination).Length == 0)
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
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw23.dll", obinkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME2)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw23.dll", obinkPath, true);

            }
            else if (target.Game == Mod.MEGame.ME3)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw23.dll", obinkPath, true);
            }
            else
            {
                Log.Error("Unknown game for gametarget (InstallBinkBypass)");
                return false;
            }
            Log.Information($"Installed Binkw32 bypass for {target.Game}");
            return true;
        }

        /// <summary>
        /// Gets scratch space directory
        /// </summary>
        /// <returns>AppData/Temp</returns>
        internal static string GetTempPath()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "Temp")).FullName;
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
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw23.dll", binkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME2)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw23.dll", binkPath, true);
            }
            else if (target.Game == Mod.MEGame.ME3)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw23.dll");
                File.Delete(obinkPath);

                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw23.dll", binkPath, true);
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
                    var failureReason = target.ValidateTarget();
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
                case Mod.MEGame.ME1:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffectConfig.exe");
                case Mod.MEGame.ME2:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffect2Config.exe");
                case Mod.MEGame.ME3:
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


        internal static void RemoveCachedTarget(GameTarget target)
        {
            var cachefile = GetCachedTargetsFile(target.Game);
            if (!File.Exists(cachefile)) return; //can't do anything.
            var savedTargets = File.ReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(target.TargetPath); //standardize

            int numRemoved = savedTargets.RemoveAll(x => string.Equals(path, x, StringComparison.InvariantCultureIgnoreCase));
            if (numRemoved > 0)
            {
                Log.Information("Removed " + numRemoved + " targets matching name " + path);
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
        /// Gets a string value frmo the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }

        public static string GetGameBackupPath(Mod.MEGame game)
        {
            string path;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, "ME1VanillaBackupLocation");
                    break;
                case Mod.MEGame.ME2:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, "ME2VanillaBackupLocation");
                    break;
                case Mod.MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    path = Utilities.GetRegistrySettingString(App.REGISTRY_KEY_ME3CMM, "VanillaCopyLocation");
                    break;
                default:
                    return null;
            }
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }
            //Super basic validation
            if (!Directory.Exists(path + @"\BIOGame") || !Directory.Exists(path + @"\Binaries"))
            {
                return null;
            }
            return path;
        }

        /// <summary>
        /// Checks if the specified DLC folder name is protected (official DLC names and __metadata)
        /// </summary>
        /// <param name="dlcFolderName">DLC folder name (DLC_CON_MP2)</param>
        /// <param name="game">Game to test against</param>
        /// <returns>True if protected, false otherwise</returns>
        internal static bool IsProtectedDLCFolder(string dlcFolderName, Mod.MEGame game) => dlcFolderName.Equals("__metadata", StringComparison.InvariantCultureIgnoreCase) && MEDirectories.OfficialDLC(game).Contains(dlcFolderName, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Gets the path to the testpatch DLC file for the specified game target
        /// </summary>
        /// <param name="target">target to resolve path for</param>
        /// <returns>Null if gametarget game is not ME3. Path where SFAR should be if ME3.</returns>
        public static string GetTestPatchPath(GameTarget target)
        {
            if (target.Game != Mod.MEGame.ME3) return null;
            return Path.Combine(target.TargetPath, @"BIOGame\Patches\PCConsole\Patch_001.sfar");
        }

        /// <summary>
        /// Recursively deletes all empty subdirectories.
        /// </summary>
        /// <param name="startLocation"></param>
        public static void DeleteEmptySubdirectories(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                DeleteEmptySubdirectories(directory);
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Log.Information("Deleting empty directory: " + directory);
                    Directory.Delete(directory, false);
                }
            }
        }

        //Step 1: https://stackoverflow.com/questions/2435894/net-how-do-i-check-for-illegal-characters-in-a-path
        private static string RemoveSpecialCharactersUsingCustomMethod(this string expression, bool removeSpecialLettersHavingASign = true, bool allowPeriod = false)
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
            if (!allowPeriod)
            {
                expression = expression.Replace(@".", newCharacter);
            }
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

        internal static void OpenExplorer(string path)
        {
            Process.Start("explorer", path);
        }

        /// <summary>
        /// Sanitizes a path by removing disallowed characters
        /// </summary>
        /// <param name="path">Path string</param>
        /// <returns>Sanitized path string</returns>
        public static string SanitizePath(string path, bool allowPeriod = false)
        {
            path = path.RemoveSpecialCharactersUsingCustomMethod(allowPeriod: allowPeriod);
            if (path.ContainsAnyInvalidCharacters())
            {
                path = path.RemoveSpecialCharactersUsingFrameworkMethod();
            }
            return path;
        }

        internal static List<string> GetPackagesInDirectory(string path, bool subdirectories)
        {
            return Directory.EnumerateFiles(path, "*.*", subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".pcc", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".sfm", StringComparison.InvariantCultureIgnoreCase)
                 || s.EndsWith(".u", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".upk", StringComparison.InvariantCultureIgnoreCase)).ToList();
        }
    }
}
