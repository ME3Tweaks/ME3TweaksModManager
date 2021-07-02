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
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using Microsoft.Win32;
using Serilog;

namespace MassEffectModManagerCore.modmanager
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

        public static bool IsNetRuntimeInstalled(int minVer)
        {
            var baseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions");
            if (baseKey.SubKeyCount == 0)
                return false;

            foreach (var platformKey in baseKey.GetSubKeyNames())
            {
                using (var platform = baseKey.OpenSubKey(platformKey))
                {
                    Debug.WriteLine($"Platform: {platform.Name.Substring(platform.Name.LastIndexOf("\\") + 1)}");
                    if (platform.SubKeyCount == 0)
                        continue;

                    var sharedHost = platform.OpenSubKey("sharedhost");
                    foreach (var version in sharedHost.GetValueNames())
                    {
                        var v = (string)sharedHost.GetValue(@"Version");
                        if (v != null && Version.TryParse(v, out var netVersion))
                        {
                            if (netVersion > new Version($"{minVer}.0.0.0"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
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
            catch (UnauthorizedAccessException)
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

        public static bool EnableWritePermissionsToFolders(List<string> folders)
        {
            string args = "";
            if (folders.Any())
            {
                foreach (var target in folders)
                {
                    if (args != "")
                    {
                        args += " ";
                    }

                    args += $"\"{target}\"";
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
        /// If no path is specified, it returns the cached executables directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetCachedExecutablePath(string path = null)
        {
            var lpath = Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "executables")).FullName;
            if (path != null)
            {
                return Path.Combine(lpath, path);
            }
            return lpath;
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
        //        await this.ShowMessageAsync("Error checking write privileges", "An error occurred while checking write privileges to game folders. This may be a sign that the game is in a bad state.\n\nThe error was:\n" + e.Message);
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
            catch (Exception)
            {
                return null;
            }
        }

        internal static string GetUpdaterServiceUploadStagingPath()
        {
            return Directory.CreateDirectory(Path.Combine(GetTempPath(), "UpdaterServiceStaging")).FullName;
        }

        /// <summary>
        /// Runs a process and does not wait for it.
        /// </summary>
        /// <param name="exe"></param>
        /// <returns></returns>
        public static int RunProcess(string exe)
        {
            return RunProcess(exe, null, null, false, false, false, false, null, null);
        }

        public static int RunProcess(string exe, string args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true, Dictionary<string, string> environmentVariables = null, string workingDir = null)
        {
            return RunProcess(exe, null, args, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow, environmentVariables: environmentVariables, workingDir: workingDir);
        }

        public static int RunProcess(string exe, List<string> args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true, Dictionary<string, string> environmentVariables = null, string workingDir = null)
        {
            return RunProcess(exe, args, null, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow, environmentVariables: environmentVariables, workingDir: workingDir);
        }


        private static int RunProcess(string exe, List<string> argsL, string argsS, bool waitForProcess, bool allowReattemptAsAdmin, bool requireAdmin, bool noWindow, Dictionary<string, string> environmentVariables, string workingDir = null)
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
                    p.StartInfo.UseShellExecute = environmentVariables == null || !environmentVariables.Any();
                    p.StartInfo.CreateNoWindow = noWindow;
                    p.StartInfo.WorkingDirectory = workingDir ?? Directory.GetParent(exe).FullName;
                    p.StartInfo.Arguments = argsStr;
                    p.StartInfo.Verb = "runas";
                    if (environmentVariables != null)
                    {
                        foreach (var ev in environmentVariables)
                        {
                            p.StartInfo.EnvironmentVariables.Add(ev.Key, ev.Value);
                        }
                    }
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
                        p.StartInfo.UseShellExecute = environmentVariables == null || !environmentVariables.Any();
                        p.StartInfo.CreateNoWindow = noWindow;
                        p.StartInfo.WorkingDirectory = workingDir ?? Directory.GetParent(exe).FullName;
                        p.StartInfo.Arguments = argsStr;
                        if (environmentVariables != null)
                        {
                            foreach (var ev in environmentVariables)
                            {
                                p.StartInfo.EnvironmentVariables.Add(ev.Key, ev.Value);
                            }
                        }
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
                            p.StartInfo.UseShellExecute = true; // If we are running as admin, we cannot shell execute without a wrapper
                            p.StartInfo.CreateNoWindow = noWindow;
                            p.StartInfo.WorkingDirectory = workingDir ?? Directory.GetParent(exe).FullName;
                            p.StartInfo.Arguments = argsStr;
                            p.StartInfo.Verb = "runas";
                            //if (environmentVariables != null)
                            //{
                            //    foreach (var ev in environmentVariables)
                            //    {
                            //        p.StartInfo.EnvironmentVariables.Add(ev.Key, ev.Value);
                            //    }
                            //}
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
                        throw; //rethrow to higher.
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

        public static string GetModDirectoryForGame(MEGame game)
        {
            if (game == MEGame.ME1) return GetME1ModsDirectory();
            if (game == MEGame.ME2) return GetME2ModsDirectory();
            if (game == MEGame.ME3) return GetME3ModsDirectory();
            if (game == MEGame.LE1) return GetLE1ModsDirectory();
            if (game == MEGame.LE2) return GetLE2ModsDirectory();
            if (game == MEGame.LE3) return GetLE3ModsDirectory();
            if (game == MEGame.LELauncher) return GetLELauncherModsDirectory();
            return null;
        }

        public static string GetDllDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "dlls")).FullName;
        }

        internal static void EnsureModDirectories()
        {
            //Todo: Ensure these are not under any game targets.
            Directory.CreateDirectory(GetME3ModsDirectory());
            Directory.CreateDirectory(GetME2ModsDirectory());
            Directory.CreateDirectory(GetME1ModsDirectory());
            Directory.CreateDirectory(GetLE1ModsDirectory());
            Directory.CreateDirectory(GetLE2ModsDirectory());
            Directory.CreateDirectory(GetLE3ModsDirectory());
            Directory.CreateDirectory(GetLELauncherModsDirectory());
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
                stream.Position = 0; // reset stream
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

        internal static string GetThirdPartyImportingCachedFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "thirdpartyimportingservice.json");
        }

        internal static string GetBasegameIdentificationCacheFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "basegamefileidentificationservice.json");
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

        public static string ConvertNewlineToBr(string str) => str?.Replace("\r\n", "<br>")?.Replace("\n", "<br>");


        public static string GetME3ModsDirectory() => Path.Combine(GetModsDirectory(), "ME3");
        public static string GetME2ModsDirectory() => Path.Combine(GetModsDirectory(), "ME2");
        public static string GetME1ModsDirectory() => Path.Combine(GetModsDirectory(), "ME1");
        public static string GetLE3ModsDirectory() => Path.Combine(GetModsDirectory(), "LE3");
        public static string GetLE2ModsDirectory() => Path.Combine(GetModsDirectory(), "LE2");
        public static string GetLE1ModsDirectory() => Path.Combine(GetModsDirectory(), "LE1");
        public static string GetLELauncherModsDirectory() => Path.Combine(GetModsDirectory(), "LELAUNCHER");

        /// <summary>
        /// Returns location where we will store the 7z.dll. Does not check for existence
        /// </summary>
        /// <returns></returns>
        internal static string Get7zDllPath()
        {
            return Path.Combine(GetDllDirectory(), "7z.dll");
        }


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

        // ME2 and ME3 have same exe names.
        private static (bool isRunning, DateTime lastChecked) le1RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static (bool isRunning, DateTime lastChecked) me1RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static (bool isRunning, DateTime lastChecked) me2RunningInfo = (false, DateTime.MinValue.AddSeconds(5));
        private static (bool isRunning, DateTime lastChecked) me3RunningInfo = (false, DateTime.MinValue.AddSeconds(5));


        private static int TIME_BETWEEN_PROCESS_CHECKS = 5;

        /// <summary>
        /// Determines if a specific game is running. This method only updates every 3 seconds due to the huge overhead it has
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public static bool IsGameRunning(MEGame gameID)
        {
            (bool isRunning, DateTime lastChecked) runningInfo = (false, DateTime.MinValue.AddSeconds(5));
            switch (gameID)
            {
                case MEGame.ME1:
                    runningInfo = me1RunningInfo;
                    break;
                case MEGame.LE1:
                    runningInfo = le1RunningInfo;
                    break;
                case MEGame.LE2:
                case MEGame.ME2:
                    runningInfo = me2RunningInfo;
                    break;
                case MEGame.LE3:
                case MEGame.ME3:
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
                case MEGame.ME1:
                    me1RunningInfo = runningInfo;
                    break;
                case MEGame.LE1:
                    le1RunningInfo = runningInfo;
                    break;
                case MEGame.ME2:
                case MEGame.LE2:
                    me2RunningInfo = runningInfo;
                    break;
                case MEGame.ME3:
                case MEGame.LE3:
                    me3RunningInfo = runningInfo;
                    break;
            }

            return runningInfo.isRunning;
        }

        /// <summary>
        /// Checks if a process is running. This should not be used in bindings, as it's a very expensive call!
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static bool IsProcessRunning(string processName)
        {
            return Process.GetProcesses().Any(x => x.ProcessName.Equals(processName, StringComparison.InvariantCultureIgnoreCase));
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

        public static Stream GetResourceStream(string assemblyResource, Assembly assembly = null)
        {
            assembly ??= System.Reflection.Assembly.GetExecutingAssembly();
            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }

        public static string ExtractInternalFile(string internalResourceName, string destination, bool overwrite, Assembly assembly = null)
        {
            Log.Information("Extracting embedded file: " + internalResourceName + " to " + destination);
            assembly ??= Assembly.GetExecutingAssembly();
#if DEBUG
            var resources = assembly.GetManifestResourceNames();
#endif
            if (!File.Exists(destination) || overwrite || new FileInfo(destination).Length == 0)
            {

                using (Stream stream = Utilities.GetResourceStream(internalResourceName, assembly))
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
            var binkPath = GetBinkFile(target);
            var targetPath = target.TargetPath;
            try
            {
                return InstallBinkBypass(binkPath, targetPath, target.Game);
            }
            catch (Exception e)
            {
                Log.Error(@"Error installing binkw32 bypass files: " + e.Message);
                return false;
            }
        }

        internal static bool InstallBinkBypass(string binkPath, string TargetPath, MEGame Game)
        {

            Log.Information($"Installing Bink bypass for {Game} to {binkPath}");
            if (Game == MEGame.ME1)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw23.dll", obinkPath, true);
            }
            else if (Game == MEGame.ME2)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw23.dll", obinkPath, true);

            }
            else if (Game == MEGame.ME3)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "win32", "binkw23.dll");
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw23.dll", obinkPath, true);
            }
            else if (Game.IsLEGame())
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "Win64", "bink2w64_original.dll"); // Where the original bink should go
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw64.bink2w64.dll", binkPath, true);  // Bypass proxy
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw64.bink2w64_original.dll", obinkPath, true); // 
            }
            else if (Game == MEGame.LELauncher)
            {
                var obinkPath = Path.Combine(TargetPath, "bink2w64_original.dll"); // Where the original bink should go
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw64.bink2w64.dll", binkPath, true);  // Bypass proxy
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw64.bink2w64_original.dll", obinkPath, true); // 
            }
            else
            {
                Log.Error("Unknown game for gametarget (InstallBinkBypass)");
                return false;
            }

            Log.Information($"Installed Bink bypass for {Game}");
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

        internal static string GetBinkFile(GameTarget target)
        {
            if (target == null) return null;
            if (target.Game == MEGame.ME1 || target.Game == MEGame.ME2) return Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
            if (target.Game == MEGame.ME3) return Path.Combine(target.TargetPath, "Binaries", "win32", "binkw32.dll");
            if (target.Game.IsLEGame()) return Path.Combine(target.TargetPath, "Binaries", "Win64", "bink2w64.dll");
            if (target.Game == MEGame.LELauncher) return Path.Combine(target.TargetPath, "bink2w64.dll");
            return null;
        }

        internal static bool UninstallBinkBypass(GameTarget target)
        {
            if (target == null) return false;
            var binkPath = GetBinkFile(target);
            if (target.Game == MEGame.ME1)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me1.binkw23.dll", binkPath, true);
            }
            else if (target.Game == MEGame.ME2)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me2.binkw23.dll", binkPath, true);
            }
            else if (target.Game == MEGame.ME3)
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "win32", "binkw23.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw32.me3.binkw23.dll", binkPath, true);
            }
            else if (target.Game.IsLEGame())
            {
                var obinkPath = Path.Combine(target.TargetPath, "Binaries", "Win64", "bink2w64_original.dll");
                File.Delete(obinkPath);
                Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.binkw64.bink2w64_original.dll", binkPath, true);

            }

            return true;
        }

        internal static string GetCachedTargetsFile(MEGame game)
        {
            return Path.Combine(GetAppDataFolder(), $"GameTargets{game}.txt");
        }

        //internal static string GetCachedLETargetsFile()
        //{
        //    return Path.Combine(GetAppDataFolder(), "GameTargetsLE.txt");
        //}

        /// <summary>
        /// Loads cached targets from the cache list
        /// </summary>
        /// <param name="game"></param>
        /// <param name="existingTargets"></param>
        /// <param name="legendaryLoad">If this should load in legendary mode, which loads 3 targets per directory</para>
        /// <returns></returns>
        internal static List<GameTarget> GetCachedTargets(MEGame game, List<GameTarget> existingTargets = null)
        {
            var cacheFile = GetCachedTargetsFile(game);
            if (File.Exists(cacheFile))
            {
                OrderedSet<GameTarget> targets = new OrderedSet<GameTarget>();
                foreach (var gameDir in Utilities.WriteSafeReadAllLines(cacheFile))
                {
                    //Validate game directory
                    if (existingTargets != null && existingTargets.Any(x => x.TargetPath.Equals(gameDir, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue; //don't try to load an existing target
                    }

                    if (Directory.Exists(gameDir))
                    {
                        GameTarget target = new GameTarget(game, gameDir, false);
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
                    else
                    {
                        Log.Warning($@"Cached target directory does not exist, skipping: {gameDir}");
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
                case MEGame.ME1:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffectConfig.exe");
                case MEGame.ME2:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffect2Config.exe");
                case MEGame.ME3:
                    return Path.Combine(target.TargetPath, "Binaries", "MassEffect3Config.exe");
            }
            // LE games do not have configs.
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
                    catch (Exception)
                    {
                        Thread.Sleep(300);
                        try
                        {
                            File.WriteAllLines(cachefile, savedTargets);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Could not save cached targets on retry: " + ex.Message);
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
        private const string LEASILoaderHash = "1bbcbaf8c393030cca7f20f7ea7db77f"; // Will need changed as game is updated

        internal static bool CheckIfBinkw32ASIIsInstalled(GameTarget target)
        {
            if (target == null) return false;
            string binkPath = GetBinkFile(target);
            string expectedHash = null;
            if (target.Game == MEGame.ME1) expectedHash = ME1ASILoaderHash;
            else if (target.Game == MEGame.ME2) expectedHash = ME2ASILoaderHash;
            else if (target.Game == MEGame.ME3) expectedHash = ME3ASILoaderHash;
            else if (target.Game.IsLEGame()) expectedHash = LEASILoaderHash;

            if (File.Exists(binkPath))
            {
                return CalculateMD5(binkPath) == expectedHash;
            }

            return false;
        }

        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }

        /// <summary>
        /// Gets a DWORD value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns>The value if read, or the specified default value (or -1 if the default value is not specified)</returns>
        public static int GetRegistrySettingInt(string key, string name, int? defaultValue = null)
        {
            return (int)Registry.GetValue(key, name, defaultValue ?? -1);
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
        internal static bool IsProtectedDLCFolder(string dlcFolderName, MEGame game) => dlcFolderName.Equals("__metadata", StringComparison.InvariantCultureIgnoreCase) && MEDirectories.OfficialDLC(game).Contains(dlcFolderName, StringComparer.InvariantCultureIgnoreCase);


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
            if (game != MEGame.ME1 && softshadows)
            {
                throw new Exception("Cannot use softshadows parameter of SetLODs() with a game that is not ME1");
            }

            if (target.Game.IsLEGame())
            {
                Log.Information(@"Settings LODs for Legendary Edition is not currently supported");
                return true; // fake saying we did it
            }

            Log.Information($@"Settings LODS for {target.Game}, highres: {highres}, 2K: {limit2k}, SS: {softshadows}");

            try
            {
                string settingspath = MEDirectories.GetLODConfigFile(game);

                if (!File.Exists(settingspath) && game == MEGame.ME1)
                {
                    Log.Error("Cannot raise/lower LODs on file that doesn't exist (ME1 bioengine file must already exist or exe will overwrite it)");
                    return false;
                }
                else if (!File.Exists(settingspath))
                {
                    Directory.CreateDirectory(Directory.GetParent(settingspath).FullName); //ensure directory exists.
                    File.Create(settingspath).Close();
                }

                bool configFileReadOnly = false;
                if (game == MEGame.ME1)
                {
                    try
                    {
                        // Get read only state for config file. It seems sometimes they get set read only.
                        FileInfo fi = new FileInfo(settingspath);
                        configFileReadOnly = fi.IsReadOnly;
                        if (configFileReadOnly)
                        {
                            Log.Information(@"Removing read only flag from ME1 bioengine.ini");
                            fi.IsReadOnly = false; //clear read only. might happen on some binkw32 in archives, maybe
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($@"Error removing readonly flag from ME1 bioengine.ini: {e.Message}");
                    }
                }

                DuplicatingIni ini = DuplicatingIni.LoadIni(settingspath);
                if (game > MEGame.ME1 && game.IsOTGame())
                {
                    #region setting systemsetting for me2/3

                    string operation = null;
                    var iniList = game == MEGame.ME2 ? (limit2k ? ME2_2KLODs : ME2HighResLODs) : (limit2k ? ME3_2KLODs : ME3HighResLODs);
                    var section = ini.Sections.FirstOrDefault(x => x.Header == "SystemSettings");
                    if (section == null && highres)
                    {
                        //section missing, and we are setting high res
                        ini.Sections.Add(new DuplicatingIni.Section()
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
                        var hqKeys = target.Game == MEGame.ME2 ? ME2HQGraphicsSettings : ME3HQGraphicsSettings;
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
                else if (game == MEGame.ME1)
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

                if (configFileReadOnly)
                {
                    try
                    {
                        Log.Information(@"Re-setting the read only flag on ME1 bioengine.ini");
                        FileInfo fi = new FileInfo(settingspath);
                        fi.IsReadOnly = true;
                    }
                    catch (Exception e)
                    {
                        Log.Error($@"Error re-setting readonly flag from ME1 bioengine.ini: {e.Message}");
                    }
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

        private static List<DuplicatingIni.Section> GetME1HQSettings(bool meuitmMode, bool softShadowsME1)
        {
            //Engine.Engine
            var engineEngine = new DuplicatingIni.Section()
            {
                Header = "Engine.Engine",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
                    new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True")
                }
            };


            var engineGameEngine = new DuplicatingIni.Section()
            {
                Header = "Engine.GameEngine",
                Entries = new List<DuplicatingIni.IniEntry>()
                {

                    new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
                    new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True")
                }
            };

            var systemSettings = new DuplicatingIni.Section
            {
                Header = "SystemSettings",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
                    new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
                    new DuplicatingIni.IniEntry("DynamicShadows=True"),
                    new DuplicatingIni.IniEntry("Trilinear=True"),
                    new DuplicatingIni.IniEntry("MotionBlur=True"),
                    new DuplicatingIni.IniEntry("DepthOfField=True"),
                    new DuplicatingIni.IniEntry("Bloom=True"),
                    new DuplicatingIni.IniEntry("QualityBloom=True"),
                    new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
                    new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
                    new DuplicatingIni.IniEntry("DetailMode=2")
                }
            };

            var textureStreaming = new DuplicatingIni.Section()
            {
                Header = "TextureStreaming",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("PoolSize=1536"),
                    new DuplicatingIni.IniEntry("MinTimeToGuaranteeMinMipCount=0"),
                    new DuplicatingIni.IniEntry("MaxTimeToGuaranteeMinMipCount=0")
                }
            };

            var windrvWindowsclient = new DuplicatingIni.Section()
            {
                Header = "WinDrv.WindowsClient",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("EnableDynamicShadows=True"),
                    new DuplicatingIni.IniEntry("TextureLODLevel=3"),
                    new DuplicatingIni.IniEntry("FilterLevel=2")
                }
            };



            //if soft shadows and MEUITM
            if (softShadowsME1 && meuitmMode)
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.006000"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.006000e"));
            }
            else
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.030000"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.030000"));
            }

            //if soft shadows
            if (softShadowsME1)
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=16"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=16"));
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=2"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=2"));
            }
            else
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=4"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=4"));
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=64"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=64"));
            }

            return new List<DuplicatingIni.Section>()
            {
                engineEngine,
                engineGameEngine,
                systemSettings,
                windrvWindowsclient,
                textureStreaming
            };
        }

        private static List<DuplicatingIni.IniEntry> ME1_DefaultLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 requires default lods to be restored or it'll just overwrite entire file
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=32,MaxLODSize=64,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=8,MaxLODSize=64,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=8,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=8,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=8,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=32,MaxLODSize=256,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME1_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME1HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME2HQGraphicsSettings = new List<DuplicatingIni.IniEntry>()
        {
            new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
            new DuplicatingIni.IniEntry("MinShadowResolution=64"),
            new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
            new DuplicatingIni.IniEntry("ShadowFilterRadius=4"),
            new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True"),
            new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
            new DuplicatingIni.IniEntry("Trilinear=True"),
            new DuplicatingIni.IniEntry("MotionBlur=True"),
            new DuplicatingIni.IniEntry("DepthOfField=True"),
            new DuplicatingIni.IniEntry("Bloom=True"),
            new DuplicatingIni.IniEntry("QualityBloom=True"),
            new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
            new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
            new DuplicatingIni.IniEntry("DetailMode=2")
        };


        private static List<DuplicatingIni.IniEntry> ME2_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };


        private static List<DuplicatingIni.IniEntry> ME2HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME3HQGraphicsSettings = new List<DuplicatingIni.IniEntry>()
        {
            //Apply only. Do not unapply
            new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
            new DuplicatingIni.IniEntry("MinShadowResolution=64"),
            new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
            new DuplicatingIni.IniEntry("ShadowFilterRadius=4"),
            new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True"),
            new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
            new DuplicatingIni.IniEntry("MotionBlur=True"),
            new DuplicatingIni.IniEntry("DepthOfField=True"),
            new DuplicatingIni.IniEntry("Bloom=True"),
            new DuplicatingIni.IniEntry("QualityBloom=True"),
            new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
            new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
            new DuplicatingIni.IniEntry("DetailMode=2")
        };

        private static List<DuplicatingIni.IniEntry> ME3_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldSpecular=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_ShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME3HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldSpecular=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_ShadowMap=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=4096,LODBias=0)")
        };

        #endregion

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

        /// <summary>
        /// Prompts the user to select a game executable, with the specified list of accepted games. Logs if the user selected or did not seelct it.
        /// </summary>
        /// <param name="acceptedGames"></param>
        /// <returns></returns>
        public static string PromptForGameExecutable(MEGame[] acceptedGames)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = M3L.GetString(M3L.string_selectGameExecutable);
            string executableNames = "";
            foreach (var v in acceptedGames)
            {
                if (executableNames.Length > 0) executableNames += ";";
                switch (v)
                {
                    case MEGame.ME1:
                        executableNames += "MassEffect.exe";
                        break;
                    case MEGame.LE1:
                        executableNames += "MassEffect1.exe";
                        break;
                    case MEGame.LE2:
                    case MEGame.ME2:
                        executableNames += "MassEffect2.exe";
                        break;
                    case MEGame.LE3:
                    case MEGame.ME3:
                        executableNames += "MassEffect3.exe";
                        break;
                    case MEGame.LELauncher:
                        executableNames += "MassEffectLauncher.exe";
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
        public static string GetGamePathFromExe(MEGame game, string exe)
        {
            string result = Path.GetDirectoryName(exe);
            if (game == MEGame.LELauncher)
                return result;
            result = Path.GetDirectoryName(result); //binaries, <GAME>
            if (game == MEGame.ME3 || game.IsLEGame())
                result = Path.GetDirectoryName(result); //up one more because of win32/win64 directory.
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

        public static string GetCachedLocalizationFolder()
        {
            return Directory.CreateDirectory(Path.Combine(GetME3TweaksServicesCache(), "livelocalization")).FullName;
        }

        public static string GetModDownloadCacheDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "downloadedmodscache")).FullName;
        }

        public static string GetLocalBasegameIdentificationServiceFile()
        {
            return Path.Combine(GetME3TweaksServicesCache(), "localbasegamefileidentificationservice.json");
        }

        public static MEGame GetGameFromNumber(string gameNum)
        {
            return GetGameFromNumber(int.Parse(gameNum));
        }

        /// <summary>
        /// Converts server game ID to Enum
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static MEGame GetGameFromNumber(int number) => number switch
        {
            1 => MEGame.ME1,
            2 => MEGame.ME2,
            3 => MEGame.ME3,
            4 => MEGame.LE1,
            5 => MEGame.LE2,
            6 => MEGame.LE3,
            7 => MEGame.LELauncher,
            _ => MEGame.Unknown
        };

        /// <summary>
        /// Directory for LE Launcher stuff
        /// </summary>
        /// <returns></returns>
        public static string GetLELaunchToolsDir()
        {
            return Directory.CreateDirectory(Path.Combine(GetAppDataFolder(), "LELauncherTools")).FullName;
        }

        /// <summary>
        /// Directory for LE Launcher stuff
        /// </summary>
        /// <returns></returns>
        public static string GetLELaunchToolsGameBootDir()
        {
            return Directory.CreateDirectory(Path.Combine(GetLELaunchToolsDir(), "GameBoot")).FullName;
        }
    }
}