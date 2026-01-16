using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using Microsoft.Win32;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    public static class M3Utilities
    {
        /// <summary>
        /// Gets the directory where the Mod Manager executable is located.
        /// </summary>
        /// <returns>The full path to the directory containing the executable.</returns>
        public static string GetMMExecutableDirectory() => Path.GetDirectoryName(App.ExecutableLocation);


        private static readonly string MEMendFileMarker = "ThisIsMEMEndOfFile";
        /// <summary>
        /// Checks if the specified file has been tagged as part of an ALOT Installation. This is not the version marker.
        /// </summary>
        /// <param name="file">Path to the file to check.</param>
        /// <returns>True if the file has the ALOT marker, false otherwise.</returns>
        public static bool HasALOTMarker(string file)
        {
            using var s = File.OpenRead(file);
            return HasALOTMarker(s);
        }

        /// <summary>
        /// Checks if the specified stream has been tagged as part of an ALOT Installation by looking for the MEM end-of-file marker.
        /// </summary>
        /// <param name="stream">The stream to check. The stream position will be restored after checking.</param>
        /// <returns>True if the stream has the ALOT marker, false otherwise.</returns>
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

        /// <summary>
        /// Determines if the current operating system is Windows 10 or newer.
        /// </summary>
        /// <returns>True if running on Windows 10 or later, false otherwise.</returns>
        public static bool IsWindows10OrNewer()
        {
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT &&
                   (os.Version.Major >= 10);
        }

        /// <summary>
        /// Determines if the current process is running with administrator privileges.
        /// </summary>
        /// <returns>True if running as administrator, false otherwise.</returns>
        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Checks if a specific major version of the .NET runtime is installed on the system.
        /// </summary>
        /// <param name="majorVersion">The major version number to check for (e.g., 6, 7, 8).</param>
        /// <returns>True if the specified .NET runtime version is installed, false otherwise.</returns>
        public static async Task<bool> IsNetRuntimeInstalled(int majorVersion)
        {
            var versions = await DotNetRuntimeVersionDetector.GetInstalledRuntimeVersions(true);
            return versions.Any(x => x.Major == majorVersion);
        }

        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// </summary>
        /// <param name="hex">The hexadecimal string to convert (without 0x prefix).</param>
        /// <returns>A byte array representing the hexadecimal values.</returns>
        public static byte[] HexStringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Creates a directory and ensures it has write permissions for the current user. 
        /// If necessary, uses PermissionsGranter.exe with elevated privileges to set permissions.
        /// </summary>
        /// <param name="directoryPath">The full path of the directory to create.</param>
        /// <param name="forcePermissions">If true, forces use of PermissionsGranter even if the parent directory is writable.</param>
        /// <returns>True if the directory was created successfully with write permissions, false otherwise.</returns>
        public static bool CreateDirectoryWithWritePermission(string directoryPath, bool forcePermissions = false)
        {
            if (!forcePermissions && Directory.Exists(Directory.GetParent(directoryPath).FullName) && M3Utilities.IsDirectoryWritable(Directory.GetParent(directoryPath).FullName))
            {
                Directory.CreateDirectory(directoryPath);
                return true;
            }

            string exe = null;
            try
            {
                // Telemetry shows this being in the catch block can crash the app if the directory is not writable. So we put it into the try block instead.
                exe = M3Filesystem.GetCachedExecutablePath("PermissionsGranter.exe");

                //try first without admin.
                if (forcePermissions) throw new UnauthorizedAccessException(); //just go to the alternate case.
                Directory.CreateDirectory(directoryPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                if (exe == null)
                {
                    // We couldn't even get to the permissions granter file
                    M3Log.Fatal("Error accessing PermissionsGranter folder. App's data folder permissions are messed up, the app is probably going to crash.");
                    return false;
                }

                //Must have admin rights.
                M3Log.Information("We need admin rights to create this directory");
                try
                {
                    M3Utilities.ExtractInternalFile("ME3TweaksModManager.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                }
                catch (Exception e)
                {
                    M3Log.Error("Error extracting PermissionsGranter.exe: " + e.Message);

                    M3Log.Information("Retrying with appdata temp directory instead.");
                    try
                    {
                        exe = Path.Combine(Path.GetTempPath(), "PermissionsGranter");
                        M3Utilities.ExtractInternalFile("ME3TweaksModManager.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                    }
                    catch (Exception ex)
                    {
                        M3Log.Error("Retry failed! Unable to make this directory writable due to inability to extract PermissionsGranter.exe. Reason: " + ex.Message);
                        return false;
                    }
                }

                string args = "\"" + System.Security.Principal.WindowsIdentity.GetCurrent().Name + "\" -create-directory \"" + directoryPath.TrimEnd('\\') + "\"";
                try
                {
                    int result = M3Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true, noWindow: true);
                    if (result == 0)
                    {
                        M3Log.Information("Elevated process returned code 0, restore directory is hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        M3Log.Error("Elevated process returned code " + result + ", directory likely is not writable");
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

                    M3Log.Error("Error creating directory with PermissionsGranter: " + e.Message);
                    return false;

                }
            }
        }

        /// <summary>
        /// Calculates the total size of all files in a directory and its subdirectories.
        /// </summary>
        /// <param name="dir">The directory path to calculate the size for.</param>
        /// <returns>The total size in bytes of all files in the directory tree.</returns>
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

        /// <summary>
        /// Sets the read-only flag on the specified file.
        /// </summary>
        /// <param name="file">The path to the file to mark as read-only.</param>
        internal static void SetReadOnly(string file)
        {
            new FileInfo(file).IsReadOnly = true;
        }

        /// <summary>
        /// Clears the readonly flag, if any was set. Returns true if the file was originally readonly, false otherwise.
        /// </summary>
        /// <param name="file">The path to the file to clear the read-only flag from.</param>
        /// <returns>True if the file was originally read-only, false otherwise.</returns>
        internal static bool ClearReadOnly(string file)
        {
            var fi = new FileInfo(file);
            var res = fi.IsReadOnly;
            fi.IsReadOnly = false;
            return res;
        }

        /// <summary>
        /// Grants write permissions to the specified folders using PermissionsGranter.exe with elevated privileges.
        /// </summary>
        /// <param name="folders">List of folder paths that need write permissions enabled.</param>
        /// <returns>True if permissions were successfully granted, false otherwise.</returns>
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

                string exe = M3Filesystem.GetCachedExecutablePath("PermissionsGranter.exe");
                M3Utilities.ExtractInternalFile("ME3TweaksModManager.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                args = $"\"{System.Security.Principal.WindowsIdentity.GetCurrent().Name}\" " + args;
                //need to run write permissions program
                if (IsAdministrator())
                {
                    int result = M3Utilities.RunProcess(exe, args, true, false);
                    if (result == 0)
                    {
                        M3Log.Information("Elevated process returned code 0, directories are hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        M3Log.Error("Elevated process returned code " + result + ", directories probably aren't writable.");
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
                    int result = M3Utilities.RunProcess(exe, args, true, true);
                    if (result == 0)
                    {
                        M3Log.Information("Elevated process returned code 0, directories are hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        M3Log.Error("Elevated process returned code " + result + ", directories probably aren't writable.");
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks for write access for the given directory by attempting to create and delete a test file.
        /// </summary>
        /// <param name="dir">The directory path to test.</param>
        /// <returns>True if write access is allowed, false otherwise.</returns>
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
                M3Log.Error("Error checking permissions to folder: " + dir);
                M3Log.Error("Directory write test had error that was not UnauthorizedAccess: " + e.Message);
            }

            return false;
        }

        /// <summary>
        /// Writes a value to a registry key, creating subkeys as necessary.
        /// </summary>
        /// <param name="subpath">The full registry path (e.g., "HKEY_CURRENT_USER\Software\MyApp").</param>
        /// <param name="value">The name of the registry value to write.</param>
        /// <param name="data">The data to write to the registry value.</param>
        /// <exception cref="Exception">Thrown if a hive other than HKEY_CURRENT_USER is specified.</exception>
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

        /// <summary>
        /// Extracts an embedded resource file to a memory stream.
        /// </summary>
        /// <param name="internalResourceName">The fully qualified name of the embedded resource.</param>
        /// <returns>A MemoryStream containing the extracted resource data, positioned at the beginning.</returns>
        internal static MemoryStream ExtractInternalFileToStream(string internalResourceName)
        {
            M3Log.Information("Extracting embedded file: " + internalResourceName + " to memory");
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif


            using (Stream stream = M3Utilities.GetResourceStream(internalResourceName))
            {
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        /// <summary>
        /// Determines how many version fields should be displayed for a given version.
        /// </summary>
        /// <param name="parsedModVersion">The version to analyze.</param>
        /// <returns>The number of version fields to display (2-4).</returns>
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

        /// <summary>
        /// Opens Windows Explorer with the specified file highlighted.
        /// </summary>
        /// <param name="filePath">The full path to the file to highlight in Explorer.</param>
        internal static void HighlightInExplorer(string filePath)
        {
            string argument = "/select, \"" + filePath + "\"";

            System.Diagnostics.Process.Start("explorer.exe", argument);
        }

        /// <summary>
        /// Gets the path to the ObjectInfo folder in the application data directory, creating it if necessary.
        /// </summary>
        /// <returns>The full path to the ObjectInfo folder.</returns>
        internal static string GetObjectInfoFolder()
        {
            return Directory.CreateDirectory(Path.Combine(M3Filesystem.GetAppDataFolder(), "ObjectInfo")).FullName;
        }

        /// <summary>
        /// Gets the path to the data subdirectory in the Mod Manager executable directory, creating it if necessary.
        /// </summary>
        /// <returns>The full path to the data directory.</returns>
        internal static string GetDataDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(GetMMExecutableDirectory(), "data")).FullName;
        }

        /// <summary>
        /// Reads all text from a file that may be locked by another process.
        /// Uses FileShare.ReadWrite to allow reading while other processes have the file open.
        /// </summary>
        /// <param name="file">The path to the file to read.</param>
        /// <returns>The contents of the file as a string, or null if an error occurs.</returns>
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

        /// <summary>
        /// Runs a process and does not wait for it to complete.
        /// </summary>
        /// <param name="exe">The path to the executable to run.</param>
        /// <returns>Always returns -1 since the process is not waited for.</returns>
        public static int RunProcess(string exe)
        {
            return RunProcess(exe, null, null, false, false, false, false, null, null);
        }

        /// <summary>
        /// Runs a process with the specified arguments and optional configuration.
        /// </summary>
        /// <param name="exe">The path to the executable to run.</param>
        /// <param name="args">The command-line arguments as a single string.</param>
        /// <param name="waitForProcess">If true, waits for the process to exit and returns its exit code.</param>
        /// <param name="allowReattemptAsAdmin">If true and access is denied (error 740), automatically retries with admin privileges.</param>
        /// <param name="requireAdmin">If true, runs the process with elevated (administrator) privileges.</param>
        /// <param name="noWindow">If true, runs the process without creating a visible window.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to set for the process.</param>
        /// <param name="workingDir">Optional working directory for the process. Defaults to the executable's directory.</param>
        /// <returns>The exit code if waitForProcess is true, otherwise -1.</returns>
        public static int RunProcess(string exe, string args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true, Dictionary<string, string> environmentVariables = null, string workingDir = null)
        {
            return RunProcess(exe, null, args, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow, environmentVariables: environmentVariables, workingDir: workingDir);
        }

        /// <summary>
        /// Runs a process with the specified arguments list and optional configuration.
        /// </summary>
        /// <param name="exe">The path to the executable to run.</param>
        /// <param name="args">The command-line arguments as a list of strings.</param>
        /// <param name="waitForProcess">If true, waits for the process to exit and returns its exit code.</param>
        /// <param name="allowReattemptAsAdmin">If true and access is denied (error 740), automatically retries with admin privileges.</param>
        /// <param name="requireAdmin">If true, runs the process with elevated (administrator) privileges.</param>
        /// <param name="noWindow">If true, runs the process without creating a visible window.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to set for the process.</param>
        /// <param name="workingDir">Optional working directory for the process. Defaults to the executable's directory.</param>
        /// <returns>The exit code if waitForProcess is true, otherwise -1.</returns>
        public static int RunProcess(string exe, List<string> args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true, Dictionary<string, string> environmentVariables = null, string workingDir = null)
        {
            return RunProcess(exe, args, null, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow, environmentVariables: environmentVariables, workingDir: workingDir);
        }


        /// <summary>
        /// Internal implementation that runs a process with comprehensive configuration options.
        /// Handles argument formatting, admin elevation, and error recovery.
        /// </summary>
        /// <param name="exe">The path to the executable to run.</param>
        /// <param name="argsL">The command-line arguments as a list of strings (optional if argsS is provided).</param>
        /// <param name="argsS">The command-line arguments as a single string (optional if argsL is provided).</param>
        /// <param name="waitForProcess">If true, waits for the process to exit and returns its exit code.</param>
        /// <param name="allowReattemptAsAdmin">If true and access is denied (error 740), automatically retries with admin privileges.</param>
        /// <param name="requireAdmin">If true, runs the process with elevated (administrator) privileges.</param>
        /// <param name="noWindow">If true, runs the process without creating a visible window.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to set for the process.</param>
        /// <param name="workingDir">Optional working directory for the process. Defaults to the executable's directory.</param>
        /// <returns>The exit code if waitForProcess is true, otherwise -1.</returns>
        private static int RunProcess(string exe, List<string> argsL, string argsS, bool waitForProcess, bool allowReattemptAsAdmin, bool requireAdmin, bool noWindow, Dictionary<string, string> environmentVariables, string workingDir = null)
        {
            var argsStr = argsS;
            if (argsStr == null && argsL != null)
            {
                argsStr = "";
                foreach (var arg in argsL)
                {
                    if (arg != "" && argsStr != "") argsStr += " ";
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
                M3Log.Information($"Running process as admin: {exe} {argsStr}");
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
                M3Log.Information($"Running process: {exe} {argsStr}");
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
                    M3Log.Warning("Win32 exception running process: " + w32e.ToString());
                    if (w32e.NativeErrorCode == 740 && allowReattemptAsAdmin)
                    {
                        M3Log.Information("Attempting relaunch with administrative rights.");
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

        /// <summary>
        /// Reads all lines from a file, attempting to do so even if the file is in use by another process.
        /// Uses FileShare.ReadWrite to allow reading while other processes have the file open.
        /// </summary>
        /// <param name="path">The path to the file to read.</param>
        /// <returns>An array of strings containing all lines from the file.</returns>
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

        /// <summary>
        /// Replaces all HTML break tags (&lt;br&gt;) with newline characters.
        /// Used to convert stored break tags back to actual newlines.
        /// </summary>
        /// <param name="str">The string to convert.</param>
        /// <returns>The converted string with newlines, or null if the input was null.</returns>
        public static string ConvertBrToNewline(string str) => str?.Replace("<br>", "\n");

        /// <summary>
        /// Replaces all newline characters (both \r\n and \n) with HTML break tags (&lt;br&gt;).
        /// Used to store newlines in formats that don't support them directly.
        /// </summary>
        /// <param name="str">The string to convert.</param>
        /// <returns>The converted string with break tags, or null if the input was null.</returns>
        public static string ConvertNewlineToBr(string str) => str?.Replace("\r\n", "<br>")?.Replace("\n", "<br>");


        /// <summary>
        /// Opens a URI in the system's default web browser.
        /// </summary>
        /// <param name="uri">The URI to open (can be a URL or file path).</param>
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
                M3Log.Error("Exception trying to open web page from system (typically means browser default is incorrectly configured by Windows): " + e.Message + ". Try opening the URL manually: " + uri);
            }
        }


        /// <summary>
        /// Checks if a process with the specified name is currently running.
        /// Note: This should not be used for game detection, as game detection also uses version info.
        /// </summary>
        /// <param name="processName">The name of the process (without .exe extension).</param>
        /// <returns>True if the process is running, false otherwise.</returns>
        public static bool IsProcessRunning(string processName)
        {
            return Process.GetProcesses().Any(x => x.ProcessName.Equals(processName, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Gets a stream for an embedded resource from an assembly.
        /// </summary>
        /// <param name="assemblyResource">The fully qualified name of the embedded resource.</param>
        /// <param name="assembly">The assembly containing the resource. If null, uses the executing assembly.</param>
        /// <returns>A stream for reading the embedded resource.</returns>
        public static Stream GetResourceStream(string assemblyResource, Assembly assembly = null)
        {
            assembly ??= System.Reflection.Assembly.GetExecutingAssembly();

            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }

        /// <summary>
        /// Extracts an embedded resource file from an assembly to a file on disk.
        /// </summary>
        /// <param name="internalResourceName">The fully qualified name of the embedded resource.</param>
        /// <param name="destination">The destination file path where the resource should be extracted.</param>
        /// <param name="overwrite">If true, overwrites the destination file if it exists. If false, skips extraction if file exists and is not empty.</param>
        /// <param name="assembly">The assembly containing the resource. If null, uses the executing assembly.</param>
        /// <returns>The destination file path.</returns>
        public static string ExtractInternalFile(string internalResourceName, string destination, bool overwrite, Assembly assembly = null)
        {
            M3Log.Information("Extracting embedded file: " + internalResourceName + " to " + destination);
            assembly ??= Assembly.GetExecutingAssembly();
#if DEBUG
            var resources = assembly.GetManifestResourceNames();
#endif
            if (!File.Exists(destination) || overwrite || new FileInfo(destination).Length == 0)
            {

                using (Stream stream = M3Utilities.GetResourceStream(internalResourceName, assembly))
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
                M3Log.Warning("File already exists. Not overwriting file.");
            }

            return destination;
        }

        /// <summary>
        /// Gets a list of installed antivirus products on the system by querying Windows Security Center.
        /// </summary>
        /// <returns>A list of display names of installed antivirus products.</returns>
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

        /// <summary>
        /// Gets the path to the Bink video codec DLL file for a game target.
        /// Different Mass Effect games use different Bink files in different locations.
        /// </summary>
        /// <param name="target">The game target to get the Bink file path for.</param>
        /// <returns>The full path to the Bink DLL, or null if the target is null or game is not recognized.</returns>
        internal static string GetBinkFile(GameTargetWPF target)
        {
            if (target == null) return null;
            if (target.Game == MEGame.ME1 || target.Game == MEGame.ME2) return Path.Combine(target.TargetPath, "Binaries", "binkw32.dll");
            if (target.Game == MEGame.ME3) return Path.Combine(target.TargetPath, "Binaries", "win32", "binkw32.dll");
            if (target.Game.IsLEGame()) return Path.Combine(target.TargetPath, "Binaries", "Win64", "bink2w64.dll");
            if (target.Game == MEGame.LELauncher) return Path.Combine(target.TargetPath, "bink2w64.dll");
            return null;
        }

        /// <summary>
        /// Uninstalls the Bink bypass from the specified game target.
        /// </summary>
        /// <param name="target">The game target to uninstall the bypass from.</param>
        /// <returns>True if the target is not null (bypass uninstall attempted), false if target is null.</returns>
        internal static bool UninstallBinkBypass(GameTargetWPF target)
        {
            if (target == null) return false;
            target.UninstallBinkBypass();


            return true;
        }

        /// <summary>
        /// Loads cached game targets from the cache file for the specified game.
        /// Validates each cached target before returning it.
        /// </summary>
        /// <param name="game">The game to load cached targets for.</param>
        /// <param name="existingTargets">Optional list of already loaded targets to avoid duplicates.</param>
        /// <returns>A list of validated game targets loaded from the cache.</returns>
        internal static List<GameTargetWPF> GetCachedTargets(MEGame game, List<GameTargetWPF> existingTargets = null)
        {
            return GetCachedTargetsInternal(game, existingTargets, null);
        }

        /// <summary>
        /// Gets cached targets for a specific game, optionally tracking failures
        /// </summary>
        /// <param name="game">The game to get cached targets for</param>
        /// <param name="existingTargets">Targets to exclude from loading</param>
        /// <param name="failedTargets">Optional list to populate with failed targets</param>
        /// <returns>List of valid targets</returns>
        internal static List<GameTargetWPF> GetCachedTargetsInternal(MEGame game, List<GameTargetWPF> existingTargets, List<TargetCacheInfo> failedTargets)
        {
            var cacheFile = M3Filesystem.GetCachedTargetsFile(game);
            if (File.Exists(cacheFile))
            {
                var targets = new OrderedSet<GameTargetWPF>();
                foreach (var gameDir in M3Utilities.WriteSafeReadAllLines(cacheFile))
                {
                    //Validate game directory
                    if (existingTargets != null && existingTargets.Any(x => x.TargetPath.Equals(gameDir, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue; //don't try to load an existing target
                    }

                    if (Directory.Exists(gameDir))
                    {
                        var target = new GameTargetWPF(game, gameDir, false);
                        var failureReason = target.ValidateTarget();
                        if (failureReason == null)
                        {
                            targets.Add(target);
                        }
                        else
                        {
                            M3Log.Error("Cached target for " + target.Game.ToString() + " is invalid: " + failureReason);
                            failedTargets?.Add(new TargetCacheInfo(game, gameDir, false, failureReason, null));
                        }
                    }
                    else
                    {
                        M3Log.Warning($@"Cached target directory does not exist, skipping: {gameDir}");
                        failedTargets?.Add(new TargetCacheInfo(game, gameDir, false, "Directory does not exist", null));
                    }
                }

                return targets.ToList();
            }
            else
            {
                return new List<GameTargetWPF>();
            }
        }

        /// <summary>
        /// Gets all cached target information for all games, including failed targets
        /// </summary>
        /// <returns>List of all cached target information</returns>
        internal static List<TargetCacheInfo> GetAllCachedTargetInfo()
        {
            var allTargetInfo = new List<TargetCacheInfo>();
            
            foreach (MEGame game in Enum.GetValues(typeof(MEGame)))
            {
                if (game == MEGame.Unknown) continue;
                
                var cacheFile = M3Filesystem.GetCachedTargetsFile(game);
                if (File.Exists(cacheFile))
                {
                    foreach (var gameDir in M3Utilities.WriteSafeReadAllLines(cacheFile))
                    {
                        if (Directory.Exists(gameDir))
                        {
                            var target = new GameTargetWPF(game, gameDir, false);
                            var failureReason = target.ValidateTarget();
                            if (failureReason == null)
                            {
                                allTargetInfo.Add(new TargetCacheInfo(game, gameDir, true, null, target));
                            }
                            else
                            {
                                allTargetInfo.Add(new TargetCacheInfo(game, gameDir, false, failureReason, null));
                            }
                        }
                        else
                        {
                            allTargetInfo.Add(new TargetCacheInfo(game, gameDir, false, "Directory does not exist", null));
                        }
                    }
                }
            }
            
            return allTargetInfo;
        }

        /// <summary>
        /// Gets the path to the game configuration tool executable for the specified target.
        /// Legendary Edition games do not have configuration tools.
        /// </summary>
        /// <param name="target">The game target to get the config tool path for.</param>
        /// <returns>The full path to the configuration tool, or null for LE games or unrecognized games.</returns>
        internal static string GetGameConfigToolPath(GameTargetWPF target)
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

        /// <summary>
        /// Adds a game target to the cached targets file for its game.
        /// Attempts retry logic if the initial write fails.
        /// </summary>
        /// <param name="target">The game target to add to the cache.</param>
        internal static void AddCachedTarget(GameTargetWPF target)
        {
            var cachefile = M3Filesystem.GetCachedTargetsFile(target.Game);
            bool creatingFile = !File.Exists(cachefile);
            var savedTargets = creatingFile ? new List<string>() : M3Utilities.WriteSafeReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(target.TargetPath); //standardize
            try
            {
                if (!savedTargets.Contains(path, StringComparer.InvariantCultureIgnoreCase))
                {
                    savedTargets.Add(path);
                    M3Log.Information($"Saving new entry into targets cache for {target.Game}: " + path);
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
                            M3Log.Error("Could not save cached targets on retry: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error("Unable to read/add cached target: " + e.Message);
            }
        }

        /// <summary>
        /// Removes a game target from the cached targets file for its game.
        /// </summary>
        /// <param name="target">The game target to remove from the cache.</param>
        internal static void RemoveCachedTarget(GameTargetWPF target)
        {
            var cachefile = M3Filesystem.GetCachedTargetsFile(target.Game);
            if (!File.Exists(cachefile)) return; //can't do anything.
            var savedTargets = M3Utilities.WriteSafeReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(target.TargetPath); //standardize

            int numRemoved = savedTargets.RemoveAll(x => string.Equals(path, x, StringComparison.InvariantCultureIgnoreCase));
            if (numRemoved > 0)
            {
                M3Log.Information("Removed " + numRemoved + " targets matching name " + path);
                File.WriteAllLines(cachefile, savedTargets);
            }
        }

        /// <summary>
        /// Removes a cached target by game and path
        /// </summary>
        /// <param name="game">The game the target belongs to</param>
        /// <param name="targetPath">The path to the target to remove</param>
        internal static void RemoveCachedTarget(MEGame game, string targetPath)
        {
            var cachefile = M3Filesystem.GetCachedTargetsFile(game);
            if (!File.Exists(cachefile)) return; //can't do anything.
            var savedTargets = M3Utilities.WriteSafeReadAllLines(cachefile).ToList();
            var path = Path.GetFullPath(targetPath); //standardize

            int numRemoved = savedTargets.RemoveAll(x => string.Equals(path, x, StringComparison.InvariantCultureIgnoreCase));
            if (numRemoved > 0)
            {
                M3Log.Information("Removed " + numRemoved + " cached targets matching path " + path);
                File.WriteAllLines(cachefile, savedTargets);
            }
        }

        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key">The full registry key path (e.g., "HKEY_CURRENT_USER\Software\MyApp").</param>
        /// <param name="name">The name of the registry value to read.</param>
        /// <returns>The string value, or null if the key or value doesn't exist.</returns>
        public static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }

        /// <summary>
        /// Gets a DWORD value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key">The full registry key path (e.g., "HKEY_CURRENT_USER\Software\MyApp").</param>
        /// <param name="name">The name of the registry value to read.</param>
        /// <param name="defaultValue">The default value to return if the key or value doesn't exist. Defaults to -1.</param>
        /// <returns>The value if read, or the specified default value (or -1 if the default value is not specified).</returns>
        public static int GetRegistrySettingInt(string key, string name, int? defaultValue = null)
        {
            return (int)Registry.GetValue(key, name, defaultValue ?? -1);
        }


        /// <summary>
        /// Looks up the user's ALOT Installer texture library directory from the registry.
        /// If the user has not set one or run ALOT Installer, this will not be populated.
        /// </summary>
        /// <returns>The path to the texture library directory, or null if not configured or directory doesn't exist.</returns>
        public static string GetALOTInstallerTextureLibraryDirectory()
        {
            var path = M3Utilities.GetRegistrySettingString(@"HKEY_CURRENT_USER\SOFTWARE\ALOTAddon", "LibraryDir");
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }

            return path;
        }

        /// <summary>
        /// Checks if the specified DLC folder name is protected (official DLC names and __metadata).
        /// Protected folders should not be deleted or modified by mods.
        /// </summary>
        /// <param name="dlcFolderName">DLC folder name (e.g., "DLC_CON_MP2").</param>
        /// <param name="game">Game to test against.</param>
        /// <returns>True if protected, false otherwise.</returns>
        internal static bool IsProtectedDLCFolder(string dlcFolderName, MEGame game) => dlcFolderName.Equals("__metadata", StringComparison.InvariantCultureIgnoreCase) && MEDirectories.OfficialDLC(game).Contains(dlcFolderName, StringComparer.InvariantCultureIgnoreCase);


        /// <summary>
        /// Recursively deletes all empty subdirectories within the specified directory.
        /// </summary>
        /// <param name="startLocation">The root directory to start cleaning from.</param>
        public static void DeleteEmptySubdirectories(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                DeleteEmptySubdirectories(directory);
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    M3Log.Information("Deleting empty directory: " + directory);
                    Directory.Delete(directory, false);
                }
            }
        }

        /// <summary>
        /// Opens Windows Explorer at the specified path.
        /// </summary>
        /// <param name="path">The directory path to open in Explorer.</param>
        internal static void OpenExplorer(string path)
        {
            Process.Start("explorer", path);
        }

        /// <summary>
        /// Gets a list of all package files (game content files) in the specified directory.
        /// Searches for .pcc, .sfm, .u, and .upk files.
        /// </summary>
        /// <param name="path">The directory path to search.</param>
        /// <param name="subdirectories">If true, searches subdirectories recursively.</param>
        /// <returns>A list of full paths to all package files found.</returns>
        internal static List<string> GetPackagesInDirectory(string path, bool subdirectories)
        {
            return Directory.EnumerateFiles(path, "*.*", subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".pcc", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".sfm", StringComparison.InvariantCultureIgnoreCase)
                                                                                            || s.EndsWith(".u", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".upk", StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        /// <summary>
        /// Sets the Level of Detail (LOD) settings for a game target.
        /// Delegates to M3LODSettings.SetLODs for the actual implementation.
        /// </summary>
        /// <param name="target">The game target to configure.</param>
        /// <param name="highres">Enable high resolution textures.</param>
        /// <param name="limit2k">Limit textures to 2K resolution.</param>
        /// <param name="softshadows">Enable soft shadows.</param>
        /// <returns>True if the settings were applied successfully, false otherwise.</returns>
        internal static bool SetLODs(GameTargetWPF target, bool highres, bool limit2k, bool softshadows)
        {
            return M3LODSettings.SetLODs(target, highres, limit2k, softshadows);
        }

        /// <summary>
        /// Prompts the user to select a game executable file using an Open File Dialog.
        /// Logs the result of the selection.
        /// </summary>
        /// <param name="acceptedGames">Array of games whose executables should be accepted by the dialog.</param>
        /// <returns>The selected executable path, or null if the user canceled the dialog.</returns>
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
                M3Log.Information($@"Executable path selected: {ofd.FileName}");
                return ofd.FileName;
            }
            M3Log.Information(@"User aborted selecting executable");
            return null;
        }

        /// <summary>
        /// Converts a game number string to its corresponding MEGame enum value.
        /// </summary>
        /// <param name="gameNum">The game number as a string.</param>
        /// <returns>The corresponding MEGame enum value.</returns>
        public static MEGame GetGameFromNumber(string gameNum)
        {
            return GetGameFromNumber(int.Parse(gameNum));
        }

        /// <summary>
        /// Converts a server game ID to its corresponding MEGame enum value.
        /// </summary>
        /// <param name="number">The game ID number (1=ME1, 2=ME2, 3=ME3, 4=LE1, 5=LE2, 6=LE3, 7=LELauncher).</param>
        /// <returns>The corresponding MEGame enum value, or MEGame.Unknown if not recognized.</returns>
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
        /// Writes the location of this exe to the registry. This allows external tools to locate Mod Manager without having them have to specify it.
        /// </summary>
        public static void WriteExeLocation()
        {
            try
            {
                M3Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3TWEAKS, @"ExecutableLocation", App.ExecutableLocation);
            }
            catch (Exception e)
            {
                M3Log.Error($@"Could not write exe location to registry: {e.Message}");
            }
        }

        /// <summary>
        /// Opens the specified file with the default shell file handler (e.g., opens documents in their associated application).
        /// The file must exist on the filesystem.
        /// </summary>
        /// <param name="file">File path to open.</param>
        /// <returns>Error message if the operation failed, null if successful.</returns>
        public static string ShellOpenFile(string file)
        {
            if (file != null && File.Exists(file))
            {
                using Process shellOpener = new Process();
                shellOpener.StartInfo.FileName = file;
                shellOpener.StartInfo.UseShellExecute = true;
                try
                {
                    shellOpener.Start();
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Shell open failed for {file}: {e.Message}");
                    return e.Message;
                }

            }

            return null;
        }
    }
}