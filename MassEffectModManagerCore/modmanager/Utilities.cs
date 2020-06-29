using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using MassEffectModManagerCore;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using Microsoft.Win32;
using Serilog;
using static MassEffectModManagerCore.modmanager.gameini.DuplicatingIni;

namespace MassEffectModManagerCore
{
    [Localizable(false)]
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

        private static readonly string MEMendFileMarker = "ThisIsMEMEndOfFile";
        /// <summary>
        /// Checks if the specified file has been tagged as part of an ALOT Installation. This is not the version marker.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool HasALOTMarker(string file)
        {
            using var s = File.OpenRead(file);
            return HasALOTMarker(s);
        }

        public static bool HasALOTMarker(Stream stream)
        {
            bool returnValue = false;
            var pos = stream.Position;
            stream.Seek(-MEMendFileMarker.Length, SeekOrigin.End);
            string marker = stream.ReadStringASCII(MEMendFileMarker.Length);
            if (marker == MEMendFileMarker)
                returnValue = true;
            stream.Seek(pos, SeekOrigin.Begin);
            return returnValue;
        }

        public static bool IsWindows10OrNewer()
        {
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT &&
                   (os.Version.Major >= 10);
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

        internal static string GetBatchInstallGroupsFolder()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "batchmodqueues")).FullName;
        }

        // Pinvoke for API function
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        public static bool DriveFreeBytes(string folderName, out ulong freespace)
        {
            freespace = 0;
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            ulong free = 0, dummy1 = 0, dummy2 = 0;

            if (GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
            {
                freespace = free;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static byte[] HexStringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static bool CreateDirectoryWithWritePermission(string directoryPath, bool forcePermissions = false)
        {
            if (!forcePermissions && Directory.Exists(Directory.GetParent(directoryPath).FullName) && Utilities.IsDirectoryWritable(Directory.GetParent(directoryPath).FullName))
            {
                Directory.CreateDirectory(directoryPath);
                return true;
            }

            try
            {
                //try first without admin.
                if (forcePermissions) throw new UnauthorizedAccessException(); //just go to the alternate case.
                Directory.CreateDirectory(directoryPath);
                return true;
            }
            catch (UnauthorizedAccessException uae)
            {
                //Must have admin rights.
                Log.Information("We need admin rights to create this directory");
                string exe = GetCachedExecutablePath("PermissionsGranter.exe");
                try
                {
                    Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                }
                catch (Exception e)
                {
                    Log.Error("Error extracting PermissionsGranter.exe: " + e.Message);

                    Log.Information("Retrying with appdata temp directory instead.");
                    try
                    {
                        exe = Path.Combine(Path.GetTempPath(), "PermissionsGranter");
                        Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Retry failed! Unable to make this directory writable due to inability to extract PermissionsGranter.exe. Reason: " + ex.Message);
                        return false;
                    }
                }

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
        }

        public static long GetSizeOfDirectory(string dir)
        {
            String[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            long totalSize = 0;
            Parallel.For(0, files.Length,
                index =>
                {
                    FileInfo fi = new FileInfo(files[index]);
                    long size = fi.Length;
                    Interlocked.Add(ref totalSize, size);
                });
            return totalSize;
        }

        internal static void SetReadOnly(string file)
        {
            new FileInfo(file).IsReadOnly = true;
        }

        /// <summary>
        /// Clears the readonly flag, if any was set. Returns true if the file was originally readonly, false otherwise.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        internal static bool ClearReadOnly(string file)
        {
            var fi = new FileInfo(file);
            var res = fi.IsReadOnly;
            fi.IsReadOnly = false;
            return res;
        }

        public static bool EnableWritePermissionsToFolders(List<string> folders, bool me1ageia)
        {
            string args = "";
            if (folders.Any() || me1ageia)
            {
                foreach (var target in folders)
                {
                    if (args != "")
                    {
                        args += " ";
                    }

                    args += $"\"{target}\"";
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
            try
            {
                System.IO.File.Create(Path.Combine(dir, "temp_m3.txt")).Close();
                System.IO.File.Delete(Path.Combine(dir, "temp_m3.txt"));
                return true;
            }
            catch (UnauthorizedAccessException)
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

        internal static string GetUpdaterServiceUploadStagingPath()
        {
            return Directory.CreateDirectory(Path.Combine(GetTempPath(), "UpdaterServiceStaging")).FullName;
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

        public static bool DeleteFilesAndFoldersRecursively(string targetDirectory, bool throwOnFailed = false)
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
                    if (throwOnFailed)
                    {
                        throw;
                    }

                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(targetDirectory))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir, throwOnFailed);
            }

            Thread.Sleep(10); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                Directory.Delete(targetDirectory);
            }
            catch (Exception e)
            {
                Log.Error($"Unable to delete directory: {targetDirectory}. It may be open still or may not be actually empty: {e.Message}");
                if (throwOnFailed)
                {
                    throw;
                }

                return false;
            }

            return result;
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
                Debug.WriteLine("Hashing file " + filename);
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

        /// <summary>
        /// Reads all lines from a file, attempting to do so even if the file is in use by another process
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] WriteSafeReadAllLines(String path)
        {
            using var csv = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(csv);
            List<string> file = new List<string>();
            while (!sr.EndOfStream)
            {
                file.Add(sr.ReadLine());
            }

            return file.ToArray();
        }

        internal static string GetTipsServiceFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "tipsservice.json");
        }

        internal static void InstallASIByGroupID(GameTarget gameTarget, string nameForLogging, int updateGroup)
        {
            var asigame = new ASIManagerPanel.ASIGame(gameTarget);
            ASIManagerPanel.LoadManifest(false, new List<ASIManagerPanel.ASIGame>(new[] { asigame }));
            var dlcModEnabler = asigame.ASIModUpdateGroups.FirstOrDefault(x => x.UpdateGroupId == updateGroup); //DLC mod enabler is group 16
            if (dlcModEnabler != null)
            {
                Log.Information($"Installing {nameForLogging} ASI");
                var asiLockObject = new object();

                void asiInstalled()
                {
                    lock (asiLockObject)
                    {
                        Monitor.Pulse(asiLockObject);
                    }
                }

                var asiNotInstalledAlready = asigame.ApplyASI(dlcModEnabler.GetLatestVersion(), asiInstalled);
                if (asiNotInstalledAlready)
                {
                    lock (asiLockObject)
                    {
                        Monitor.Wait(asiLockObject, 3500); //3.5 seconds max time.
                    }
                }
            }
            else
            {
                Log.Error($"Could not install {nameForLogging} ASI!!");
            }
        }

        internal static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "thirdpartyimportingservice.json");
        }

        internal static string GetBasegameIdentificationCacheFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "basegamefileidentificationservice.json");
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

        /// <summary>
        /// Returns location where we will store the 7z.dll. Does not check for existence
        /// </summary>
        /// <returns></returns>
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


        private static (bool isRunning, DateTime lastChecked) me1RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static (bool isRunning, DateTime lastChecked) me2RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static (bool isRunning, DateTime lastChecked) me3RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static int TIME_BETWEEN_PROCESS_CHECKS = 5;

        /// <summary>
        /// Determines if a specific game is running. This method only updates every 3 seconds due to the huge overhead it has
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public static bool IsGameRunning(Mod.MEGame gameID)
        {
            (bool isRunning, DateTime lastChecked) runningInfo = (false, DateTime.MinValue.AddSeconds(5));
            switch (gameID)
            {
                case Mod.MEGame.ME1:
                    runningInfo = me1RunningInfo;
                    break;
                case Mod.MEGame.ME2:
                    runningInfo = me2RunningInfo;
                    break;
                case Mod.MEGame.ME3:
                    runningInfo = me3RunningInfo;
                    break;
            }

            var time = runningInfo.lastChecked.AddSeconds(TIME_BETWEEN_PROCESS_CHECKS);
            //Debug.WriteLine(time + " vs " + DateTime.Now);
            if (time > DateTime.Now)
            {
                //Debug.WriteLine("CACHED");
                return runningInfo.isRunning; //cached
            }
            //Debug.WriteLine("IsRunning: " + gameID);

            var processNames = MEDirectories.ExecutableNames(gameID).Select(x => Path.GetFileNameWithoutExtension(x));
            runningInfo.isRunning = Process.GetProcesses().Any(x => processNames.Contains(x.ProcessName));
            runningInfo.lastChecked = DateTime.Now;
            switch (gameID)
            {
                case Mod.MEGame.ME1:
                    me1RunningInfo = runningInfo;
                    break;
                case Mod.MEGame.ME2:
                    me2RunningInfo = runningInfo;
                    break;
                case Mod.MEGame.ME3:
                    me3RunningInfo = runningInfo;
                    break;
            }

            return runningInfo.isRunning;
        }

        internal static string GetAppDataFolder(bool createIfMissing = true)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ME3TweaksModManager");
            if (createIfMissing && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        internal static string GetPre104DataFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MassEffectModManager");
            if (Directory.Exists(folder))
            {
                return folder;
            }

            return null;
        }

        public static Stream GetResourceStream(string assemblyResource)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }

        internal static string GetGameName(Mod.MEGame game)
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
                    if (File.Exists(destination))
                    {
                        FileInfo fi = new FileInfo(destination);
                        if (fi.IsReadOnly)
                        {
                            fi.IsReadOnly = false; //clear read only. might happen on some binkw32 in archives, maybe
                        }
                    }

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

        internal static List<string> GetListOfInstalledAV()
        {
            List<string> av = new List<string>();
            // for Windows Vista and above '\root\SecurityCenter2'
            using (var searcher = new ManagementObjectSearcher(@"\\" +
                                                               Environment.MachineName +
                                                               @"\root\SecurityCenter2",
                "SELECT * FROM AntivirusProduct"))
            {
                var searcherInstance = searcher.Get();
                foreach (var instance in searcherInstance)
                {
                    av.Add(instance["displayName"].ToString());
                }
            }

            return av;
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

        internal static List<GameTarget> GetCachedTargets(Mod.MEGame game, List<GameTarget> existingTargets = null)
        {
            var cacheFile = GetCachedTargetsFile(game);
            if (File.Exists(cacheFile))
            {
                OrderedSet<GameTarget> targets = new OrderedSet<GameTarget>();
                foreach (var file in Utilities.WriteSafeReadAllLines(cacheFile))
                {
                    //Validate game directory
                    if (existingTargets != null && existingTargets.Any(x => x.TargetPath.Equals(file, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue; //don't try to load an existing target
                    }

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

                return targets.ToList();
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
            bool creatingFile = !File.Exists(cachefile);
            var savedTargets = creatingFile ? new List<string>() : Utilities.WriteSafeReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(target.TargetPath); //standardize
            try
            {
                if (!savedTargets.Contains(path, StringComparer.InvariantCultureIgnoreCase))
                {
                    savedTargets.Add(path);
                    Log.Information($"Saving new entry into targets cache for {target.Game}: " + path);
                    try
                    {
                        File.WriteAllLines(cachefile, savedTargets);
                    }
                    catch (Exception e)
                    {
                        Thread.Sleep(300);
                        try
                        {
                            File.WriteAllLines(cachefile, savedTargets);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Could not save cached targets on retry: " + e.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Unable to read/add cached target: " + e.Message);
            }
        }


        internal static void RemoveCachedTarget(GameTarget target)
        {
            var cachefile = GetCachedTargetsFile(target.Game);
            if (!File.Exists(cachefile)) return; //can't do anything.
            var savedTargets = Utilities.WriteSafeReadAllLines(cachefile).ToList();
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


        /// <summary>
        /// Looks up the user's ALOT Installer texture library directory. If the user has not set one or run ALOT Installer, this will not be populated.
        /// </summary>
        /// <returns></returns>
        public static string GetALOTInstallerTextureLibraryDirectory()
        {
            var path = Utilities.GetRegistrySettingString(@"HKEY_CURRENT_USER\SOFTWARE\ALOTAddon", "LibraryDir");
            if (path == null || !Directory.Exists(path))
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

        internal static bool SetLODs(GameTarget target, bool highres, bool limit2k, bool softshadows)
        {
            var game = target.Game;
            if (game != Mod.MEGame.ME1 && (softshadows || limit2k))
            {
                throw new Exception("Cannot use softshadows or limit2k parameter of SetLODs() with a game that is not ME1");
            }

            try
            {
                string settingspath = null;
                switch (game)
                {
                    case Mod.MEGame.ME1:
                        settingspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect", "Config", "BIOEngine.ini");
                        break;
                    case Mod.MEGame.ME2:
                        settingspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect 2", "BioGame", "Config", "GamerSettings.ini");
                        break;
                    case Mod.MEGame.ME3:
                        settingspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect 3", "BioGame", "Config", "GamerSettings.ini");
                        break;
                }

                ;

                if (!File.Exists(settingspath) && game == Mod.MEGame.ME1)
                {
                    Log.Error("Cannot raise/lower LODs on file that doesn't exist (ME1 bioengine file must already exist or exe will overwrite it)");
                    return false;
                }
                else if (!File.Exists(settingspath))
                {
                    Directory.CreateDirectory(Directory.GetParent(settingspath).FullName); //ensure directory exists.
                    File.Create(settingspath).Close();
                }

                DuplicatingIni ini = DuplicatingIni.LoadIni(settingspath);
                if (game > Mod.MEGame.ME1)
                {
                    #region setting systemsetting for me2/3

                    string operation = null;
                    var iniList = game == Mod.MEGame.ME2 ? ME2HighResLODs : ME3HighResLODs;
                    var section = ini.Sections.FirstOrDefault(x => x.Header == "SystemSettings");
                    if (section == null && highres)
                    {
                        //section missing, and we are setting high res
                        ini.Sections.Add(new Section()
                        {
                            Entries = iniList,
                            Header = "SystemSettings"
                        });
                        operation = "Set high-res lod settings on blank gamersettings.ini";
                    }
                    else if (highres)
                    {
                        //section exists, upgrading still, overwrite keys
                        foreach (var newItem in iniList)
                        {
                            var matchingKey = section.Entries.FirstOrDefault(x => x.Key == newItem.Key);
                            if (matchingKey != null)
                            {
                                matchingKey.Value = newItem.Value; //overwrite value
                            }
                            else
                            {
                                section.Entries.Add(newItem); //doesn't exist, add new item.
                            }
                        }



                        operation = "Set high-res lod settings in gamersettings.ini";

                    }
                    else if (section != null)
                    {
                        //section exists, downgrading
                        section.Entries.RemoveAll(x => iniList.Any(i => i.Key == x.Key));
                        operation = "Removed high-res lod settings from gamersettings.ini";
                    }

                    #endregion

                    //Update GFx (non LOD) settings
                    if (highres)
                    {
                        var hqKeys = target.Game == Mod.MEGame.ME2 ? ME2HQGraphicsSettings : ME3HQGraphicsSettings;
                        var hqSection = ini.GetSection(@"SystemSettings");
                        foreach (var entry in hqKeys)
                        {
                            var matchingKey = hqSection.Entries.FirstOrDefault(x => x.Key == entry.Key);
                            if (matchingKey != null)
                            {
                                matchingKey.Value = entry.Value; //overwrite value
                            }
                            else
                            {
                                hqSection.Entries.Add(entry); //doesn't exist, add new item.
                            }
                        }
                    }

                    File.WriteAllText(settingspath, ini.ToString());
                    Log.Information(operation);
                }
                else if (game == Mod.MEGame.ME1)
                {
                    var section = ini.Sections.FirstOrDefault(x => x.Header == "TextureLODSettings");
                    if (section == null && highres)
                    {
                        Log.Error("TextureLODSettings section cannot be null in ME1. Run the game to regenerate the bioengine file.");
                        return false; //This section cannot be null
                    }

                    var iniList = highres ? limit2k ? ME1_2KLODs : ME1HighResLODs : ME1_DefaultLODs;

                    //section exists, upgrading still, overwrite keys
                    foreach (var newItem in iniList)
                    {
                        var matchingKey = section.Entries.FirstOrDefault(x => x.Key == newItem.Key);
                        if (matchingKey != null)
                        {
                            matchingKey.Value = newItem.Value; //overwrite value
                        }
                        else
                        {
                            section.Entries.Add(newItem); //doesn't exist, add new item.
                        }
                    }

                    //Update GFx (non LOD) settings
                    if (highres)
                    {
                        var me1hq = GetME1HQSettings(target.MEUITMInstalled, softshadows);
                        foreach (var hqSection in me1hq)
                        {
                            var existingSect = ini.GetSection(hqSection);
                            if (existingSect != null)
                            {
                                foreach (var item in hqSection.Entries)
                                {
                                    var matchingKey = existingSect.Entries.FirstOrDefault(x => x.Key == item.Key);
                                    if (matchingKey != null)
                                    {
                                        matchingKey.Value = item.Value; //overwrite value
                                    }
                                    else
                                    {
                                        section.Entries.Add(item); //doesn't exist, add new item.
                                    }
                                }
                            }
                            else
                            {
                                //!!! Error
                                Log.Error(@"Error: Could not find ME1 high quality settings key in bioengine.ini: " + hqSection.Header);
                            }
                        }
                    }

                    File.WriteAllText(settingspath, ini.ToString());
                    Log.Information("Set " + (highres ? limit2k ? "2K lods" : "4K lods" : "default LODs") + " in BioEngine.ini file for ME1");
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Error setting LODs: " + e.Message);
                return false;
            }

            return true;
        }

        #region LODs

        private static List<Section> GetME1HQSettings(bool meuitmMode, bool softShadowsME1)
        {
            //Engine.Engine
            var engineEngine = new Section()
            {
                Header = "Engine.Engine",
                Entries = new List<IniEntry>()
                {
                    new IniEntry("MaxShadowResolution=2048"),
                    new IniEntry("bEnableBranchingPCFShadows=True")
                }
            };


            var engineGameEngine = new Section()
            {
                Header = "Engine.GameEngine",
                Entries = new List<IniEntry>()
                {

                    new IniEntry("MaxShadowResolution=2048"),
                    new IniEntry("bEnableBranchingPCFShadows=True")
                }
            };

            var systemSettings = new Section
            {
                Header = "SystemSettings",
                Entries = new List<IniEntry>()
                {
                    new IniEntry("ShadowFilterQualityBias=2"),
                    new IniEntry("MaxAnisotropy=16"),
                    new IniEntry("DynamicShadows=True"),
                    new IniEntry("Trilinear=True"),
                    new IniEntry("MotionBlur=True"),
                    new IniEntry("DepthOfField=True"),
                    new IniEntry("Bloom=True"),
                    new IniEntry("QualityBloom=True"),
                    new IniEntry("ParticleLODBias=-1"),
                    new IniEntry("SkeletalMeshLODBias=-1"),
                    new IniEntry("DetailMode=2")
                }
            };

            var textureStreaming = new Section()
            {
                Header = "TextureStreaming",
                Entries = new List<IniEntry>()
                {
                    new IniEntry("PoolSize=1536"),
                    new IniEntry("MinTimeToGuaranteeMinMipCount=0"),
                    new IniEntry("MaxTimeToGuaranteeMinMipCount=0")
                }
            };

            var windrvWindowsclient = new Section()
            {
                Header = "WinDrv.WindowsClient",
                Entries = new List<IniEntry>()
                {
                    new IniEntry("EnableDynamicShadows=True"),
                    new IniEntry("TextureLODLevel=3"),
                    new IniEntry("FilterLevel=2")
                }
            };



            //if soft shadows and MEUITM
            if (softShadowsME1 && meuitmMode)
            {
                engineEngine.Entries.Add(new IniEntry("DepthBias=0.006000"));
                engineGameEngine.Entries.Add(new IniEntry("DepthBias=0.006000e"));
            }
            else
            {
                engineEngine.Entries.Add(new IniEntry("DepthBias=0.030000"));
                engineGameEngine.Entries.Add(new IniEntry("DepthBias=0.030000"));
            }

            //if soft shadows
            if (softShadowsME1)
            {
                engineEngine.Entries.Add(new IniEntry("MinShadowResolution=16"));
                engineGameEngine.Entries.Add(new IniEntry("MinShadowResolution=16"));
                engineEngine.Entries.Add(new IniEntry("ShadowFilterRadius=2"));
                engineGameEngine.Entries.Add(new IniEntry("ShadowFilterRadius=2"));
            }
            else
            {
                engineEngine.Entries.Add(new IniEntry("ShadowFilterRadius=4"));
                engineGameEngine.Entries.Add(new IniEntry("ShadowFilterRadius=4"));
                engineEngine.Entries.Add(new IniEntry("MinShadowResolution=64"));
                engineGameEngine.Entries.Add(new IniEntry("MinShadowResolution=64"));
            }

            return new List<Section>()
            {
                engineEngine,
                engineGameEngine,
                systemSettings,
                windrvWindowsclient,
                textureStreaming
            };
        }

        /// <summary>
        /// Gets folder containing #.xml files (definition of modmaker mods)
        /// </summary>
        /// <returns></returns>
        internal static string GetModmakerDefinitionsCache()
        {
            return Directory.CreateDirectory(Path.Combine(Utilities.GetModMakerCache(), "moddefinitions")).FullName;
        }

        /// <summary>
        /// Gets cache directory for modmaker files
        /// </summary>
        /// <returns></returns>
        private static string GetModMakerCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "ModMakerCache")).FullName;
        }

        private static List<IniEntry> ME1_DefaultLODs = new List<IniEntry>()
        {
            //ME1 requires default lods to be restored or it'll just overwrite entire file
            new IniEntry("TEXTUREGROUP_World=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=32,MaxLODSize=64,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=8,MaxLODSize=64,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=8,MaxLODSize=128,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=8,MaxLODSize=256,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=8,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_GUI=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=32,MaxLODSize=256,LODBias=0)")
        };

        private static List<IniEntry> ME1_2KLODs = new List<IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new IniEntry("TEXTUREGROUP_World=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_GUI=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)")
        };

        private static List<IniEntry> ME1HighResLODs = new List<IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new IniEntry("TEXTUREGROUP_World=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_GUI=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)")
        };

        private static List<IniEntry> ME2HQGraphicsSettings = new List<IniEntry>()
        {
            new IniEntry("MaxShadowResolution=2048"),
            new IniEntry("MinShadowResolution=64"),
            new IniEntry("ShadowFilterQualityBias=2"),
            new IniEntry("ShadowFilterRadius=4"),
            new IniEntry("bEnableBranchingPCFShadows=True"),
            new IniEntry("MaxAnisotropy=16"),
            new IniEntry("Trilinear=True"),
            new IniEntry("MotionBlur=True"),
            new IniEntry("DepthOfField=True"),
            new IniEntry("Bloom=True"),
            new IniEntry("QualityBloom=True"),
            new IniEntry("ParticleLODBias=-1"),
            new IniEntry("SkeletalMeshLODBias=-1"),
            new IniEntry("DetailMode=2")
        };

        private static List<IniEntry> ME2HighResLODs = new List<IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=4096,LODBias=0)")
        };

        private static List<IniEntry> ME3HQGraphicsSettings = new List<IniEntry>()
        {
            //Apply only. Do not unapply
            new IniEntry("MaxShadowResolution=2048"),
            new IniEntry("MinShadowResolution=64"),
            new IniEntry("ShadowFilterQualityBias=2"),
            new IniEntry("ShadowFilterRadius=4"),
            new IniEntry("bEnableBranchingPCFShadows=True"),
            new IniEntry("MaxAnisotropy=16"),
            new IniEntry("MotionBlur=True"),
            new IniEntry("DepthOfField=True"),
            new IniEntry("Bloom=True"),
            new IniEntry("QualityBloom=True"),
            new IniEntry("ParticleLODBias=-1"),
            new IniEntry("SkeletalMeshLODBias=-1"),
            new IniEntry("DetailMode=2")
        };

        private static List<IniEntry> ME3HighResLODs = new List<IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_WorldSpecular=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_ShadowMap=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=4096,LODBias=0)")
        };

        #endregion

        /// <summary>
        /// Prompts the user to select a game executable, with the specified list of accepted games. Logs if the user selected or did not seelct it.
        /// </summary>
        /// <param name="acceptedGames"></param>
        /// <returns></returns>
        public static string PromptForGameExecutable(Mod.MEGame[] acceptedGames)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = M3L.GetString(M3L.string_selectGameExecutable);
            string executableNames = "";
            foreach (var v in acceptedGames)
            {
                if (executableNames.Length > 0) executableNames += ";";
                switch (v)
                {
                    case Mod.MEGame.ME1:
                        executableNames += "MassEffect.exe";
                        break;
                    case Mod.MEGame.ME2:
                        executableNames += "MassEffect2.exe";
                        break;
                    case Mod.MEGame.ME3:
                        executableNames += "MassEffect3.exe";
                        break;
                }
            }


            string filter = $@"{M3L.GetString(M3L.string_gameExecutable)}|{executableNames}"; //only partially localizable.
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                Log.Information($@"Executable path selected: {ofd.FileName}");
                return ofd.FileName;
            }
            Log.Information(@"User aborted selecting executable");
            return null;
        }
        /// <summary>
        /// Given a game and executable path, returns the basepath of the installation.
        /// </summary>
        /// <param name="game">What game this exe is for</param>
        /// <param name="exe">Executable path</param>
        /// <returns></returns>
        public static string GetGamePathFromExe(Mod.MEGame game, string exe)
        {
            string result = Path.GetDirectoryName(Path.GetDirectoryName(exe)); //binaries, <GAME>

            if (game == Mod.MEGame.ME3)
                result = Path.GetDirectoryName(result); //up one more because of win32 directory.
            return result;
        }

        public static string GetKeybindsOverrideFolder()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "keybindsoverride")).FullName;
        }

        internal static string GetTutorialServiceCacheFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "tutorialservice.json");
        }

        public static string GetTutorialServiceCache()
        {
            return Directory.CreateDirectory(Path.Combine(GetME3TweaksServicesCache(), "tutorialservice")).FullName;
        }

        public static string GetOriginOverlayDisableFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "d3d9.dll");
        }
    }
}