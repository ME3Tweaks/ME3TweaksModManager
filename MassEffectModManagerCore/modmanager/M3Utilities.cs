using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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
            // Uses shared logic but passes this assembly in.
            return MUtilities.ExtractInternalFileToStream(internalResourceName, Assembly.GetExecutingAssembly());
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
            try
            {
                target.UninstallBinkBypass();
                return true;
            }
            catch (Exception ex)
            {
                M3Log.Error($@"Error uninstalling bink bypass from {target.TargetPath}: {ex.Message}");
                return false;
            }
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
        /// Opens Windows Explorer at the specified path.
        /// </summary>
        /// <param name="path">The directory path to open in Explorer.</param>
        internal static void OpenExplorer(string path)
        {
            Process.Start("explorer", $"\"{path}\"");
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

        /// <summary>
        /// Refreshes UI bindings after a delay (if wait time is not zero). This can be used sparingly to force updates to the UI button states.
        /// </summary>
        /// <param name="waitTime">Time in ms to wait before refreshing</param>
        internal static void RefreshBindings(int waitTime = 150)
        {
            if (waitTime == 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CommandManager.InvalidateRequerySuggested();
                });
            }
            else
            {
                Task.Run(() => Thread.Sleep(waitTime)).ContinueWithOnUIThread(x =>
                {
                    // Already on UI thread
                    CommandManager.InvalidateRequerySuggested();
                });
            }
        }
    }
}