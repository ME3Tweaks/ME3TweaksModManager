using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Receives one ME3ProfileDump export after an opted-in Milan installation
    /// </summary>
    internal sealed class Me3ProfileMigration : IDisposable
    {
        internal const string ExportFileName = "ME3MPProfile.json";

        // 8MiB. What a chungus. Most are about 115KB.
        private const int MaxJsonBytes = 8 * 1024 * 1024;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2); // Backup in case the watcher misses an event
        private static Me3ProfileMigration active;
        private readonly MainWindow window;
        private readonly CancellationTokenSource cancellation = new();
        private readonly BackgroundTask waitingTask;
        private int waitingTaskCompleted;

        private Me3ProfileMigration(MainWindow window)
        {
            this.window = window;
            waitingTask = BackgroundTaskEngine.SubmitBackgroundJob("ME3ProfileMigration",
                "Navigate to the multiplayer menu in ME3 to begin the profile migration",
                "ME3 multiplayer profile received");
        }

        public static async Task StartAsync(MainWindow window, GameTargetWPF me3Target)
        {
            if (active != null)
            {
                M3L.ShowDialog(window, "ME3 profile migration is already waiting for a profile.", "Legendary Edition Multiplayer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!EnsureTargetWritable(me3Target))
            {
                M3L.ShowDialog(window, $"ME3 target is not writable:\n\n{me3Target.TargetPath}\n\nIf you cancelled the administrator prompt, try again and allow it.", "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var asi = ASIManager.GetASIModVersion(MEGame.ME3, 499, includeHidden: true) ?? throw new InvalidOperationException("ME3 Profile Dump (group 499) was not found in the ASI manifest.");
                var installed = await Task.Run(() => ASIManager.InstallASIToTarget(asi, me3Target));
                if (!installed)
                {
                    throw new IOException("ME3 Profile Dump ASI installation did not complete successfully.");
                }

                // 11/07/2026 - Install bink bypass
                if (!me3Target.IsBinkBypassInstalled())
                {
                    me3Target.InstallBinkBypass(true);
                }
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Could not install ME3 Profile Dump ASI for profile migration");
                if (!window.AppExiting)
                {
                    M3L.ShowDialog(window, $"Legendary Edition Multiplayer was installed, but the ME3 Profile Dump ASI could not be installed:\n\n{e.Message}", "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (window.AppExiting)
            {
                return;
            }
            var migration = new Me3ProfileMigration(window);
            active = migration;
            window.Closed += migration.WindowClosed;
            RemoveStaleExport(); // Nuke old data to ensure we don't import old garbo
            _ = migration.WatchAsync();
            _ = Task.Run(() =>
            {
                try { GameLauncher.LaunchGame(me3Target); }
                catch (Exception e)
                {
                    M3Log.Exception(e, "Could not launch ME3 for profile migration");
                    migration.CompleteWaitingTask("Could not launch ME3 for profile migration.");
                    window.Dispatcher.Invoke(() =>
                        M3L.ShowDialog(window, $"Could not launch ME3: {e.Message}", "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error));
                    migration.Dispose();
                }
            });
        }

        /// <summary>
        /// Makes sure ME3's game folder is writable, prompting for admin if not. The ASI aborts if it can't write its log so we have to have permisisons
        /// </summary>
        internal static bool EnsureTargetWritable(GameTargetWPF me3Target)
        {
            if (IsTargetWritable(me3Target))
            {
                return true;
            }

            M3Log.Warning($"ME3 target {me3Target.TargetPath} is not writable, requesting permissions for profile migration");
            try
            {
                return MUtilities.EnableWritePermissionsToFolders(new List<string> { me3Target.TargetPath }) && IsTargetWritable(me3Target);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Error granting write permissions to ME3 target:");
                return false;
            }
        }

        private static bool IsTargetWritable(GameTargetWPF me3Target)
        {
            if (!me3Target.IsTargetWritable())
            {
                return false;
            }

            // ASI and its log live down in Binaries\Win32
            var asiDir = me3Target.GetASIPath();
            var exeDir = Path.GetDirectoryName(asiDir);
            if (!MUtilities.IsDirectoryWritable(exeDir))
            {
                return false;
            }

            return !Directory.Exists(asiDir) || MUtilities.IsDirectoryWritable(asiDir);
        }

        private static string ExportPath =>Path.Combine(M3Filesystem.GetAppDataFolder(), ExportFileName);

        /// <summary>
        /// Removes old transferred profile data and its temporary file too
        /// </summary>
        private static void RemoveStaleExport()
        {
            var path = ExportPath;
            try
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
            catch (Exception e)
            {
                M3Log.Warning($"Could not delete stale ME3 profile export: {e.Message}");
            }
        }

        /// <summary>
        /// Watches for the profile to be written on disk
        /// </summary>
        /// <returns></returns>
        private async Task WatchAsync()
        {
            // Use the current time to make sure we don't import something old
            var startedUtc = DateTime.UtcNow;
            try
            {
                var path = ExportPath;
                using var signal = new SemaphoreSlim(0);
                using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path), ExportFileName)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                void Changed(object s, FileSystemEventArgs e)
                {
                    if (signal.CurrentCount == 0)
                    {
                        signal.Release();
                    }
                }
                watcher.Created += Changed;
                watcher.Changed += Changed;
                watcher.Renamed += Changed; // Final transfer comes in as a rename
                watcher.EnableRaisingEvents = true;

                string json;
                while ((json = TryReadExport(path, startedUtc)) == null)
                {
                    await signal.WaitAsync(PollInterval, cancellation.Token);
                }

                CompleteWaitingTask("ME3 multiplayer profile received.");
                await window.Dispatcher.InvokeAsync(() => Import(json));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                M3Log.Exception(e, "ME3 profile migration failed");
                CompleteWaitingTask("ME3 profile migration failed.");
                if (!cancellation.IsCancellationRequested)
                {
                    await window.Dispatcher.InvokeAsync(() =>
                    M3L.ShowDialog(window, $"Could not read the ME3 profile: {e.Message}", "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            }
            finally
            {
                Dispose();
            }
        }

        /// <summary>
        /// Attempts to read the contents of the export. Returns null until it succeeds.
        /// </summary>
        private static string TryReadExport(string path, DateTime notBeforeUtc)
        {
            string data;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.LastWriteTimeUtc < notBeforeUtc)
                {
                    return null;
                }

                // Share delete so the read never blocks the ASI's replace.
                data = File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                // File is not available yet
                return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // File is currently in use
                M3Log.Warning($"ME3 profile export not readable yet, will retry: {e.Message}");
                return null;
            }

            if (data.Length == 0)
            {
                throw new InvalidDataException("The ME3 profile export file is empty!");
            }
            try
            {
                // Data read, no longer needed
                File.Delete(path);
            }
            catch (Exception e)
            {
                M3Log.Warning($"Failed to delete dumped transient profile: {e.Message}");
            }
            return data;
        }

        private void Import(string json)
        {
            string cachePath = null;
            try
            {
                var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ME3TweaksModManager", "ME3ProfileImports");
                Directory.CreateDirectory(cacheDir);
                cachePath = Path.Combine(cacheDir, $"ME3_MP_Profile_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{Guid.NewGuid():N}.json");
                File.WriteAllText(cachePath, json, new UTF8Encoding(false));
                var profileBytes = Me3MpProfileConverter.Convert(json);

                var destination = LMPDirectory.LocalProfilePath;
                var exists = File.Exists(destination);
                window.WindowState = WindowState.Normal;
                window.Activate();
                var prompt = exists
                    ? $"An ME3 multiplayer profile was received and cached at:\n\n{cachePath}\n\nYou can close ME3 now that the profile has been captured.\n\nOverwrite the existing LMP MP_Profile? A backup will be kept beside it."
                    : $"An ME3 multiplayer profile was received and cached at:\n\n{cachePath}\n\nYou can close ME3 now that the profile has been captured.\n\nConvert it to an LMP MP_Profile?";
                if (M3L.ShowDialog(window, prompt, "Import ME3 multiplayer profile", MessageBoxButton.YesNo,
                    MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                    return;

                LmpProfileImport.WriteProfile(profileBytes);
                M3L.ShowDialog(window, $"ME3 profile converted successfully to:\n\n{destination}",
                    "Profile migration complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Failed to convert ME3 multiplayer profile");
                M3L.ShowDialog(window, $"Profile conversion failed: {e.Message}" +
                    (cachePath == null ? "" : $"\n\nThe JSON remains cached at:\n{cachePath}"),
                    "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WindowClosed(object sender, EventArgs e) => Dispose();

        private void CompleteWaitingTask(string finishedText)
        {
            if (Interlocked.Exchange(ref waitingTaskCompleted, 1) != 0) return;
            waitingTask.FinishedUIText = finishedText;
            BackgroundTaskEngine.SubmitJobCompletion(waitingTask);
        }

        public void Dispose()
        {
            CompleteWaitingTask("ME3 profile migration stopped.");
            cancellation.Cancel();
            window.Closed -= WindowClosed;
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
