using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>Receives one ME3ProfileDump export after an opted-in Milan installation.</summary>
    internal sealed class Me3ProfileMigration : IDisposable
    {
        // Protocol: one UTF-8 JSON document followed by '\n' or end of connection.
        internal const string PipeName = "m3convertmpprofile";
        private const int MaxJsonBytes = 8 * 1024 * 1024;
        private static Me3ProfileMigration active;
        private readonly MainWindow window;
        private readonly CancellationTokenSource cancellation = new();
        private readonly BackgroundTask waitingTask;
        private int waitingTaskCompleted;

        private Me3ProfileMigration(MainWindow window)
        {
            this.window = window;
            waitingTask = BackgroundTaskEngine.SubmitBackgroundJob("ME3ProfileMigration",
                "Waiting for ME3 to load the multiplayer menu and send its profile...",
                "ME3 multiplayer profile received.");
        }

        public static async Task StartAsync(MainWindow window, GameTargetWPF me3Target)
        {
            if (active != null)
            {
                M3L.ShowDialog(window, "ME3 profile migration is already waiting for a profile.", "Project Milan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var asi = ASIManager.GetASIModVersion(MEGame.ME3, 499, includeHidden: true)
                    ?? throw new InvalidOperationException("ME3 Profile Dump (group 499) was not found in the ASI manifest.");
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
                    M3L.ShowDialog(window, $"Project Milan was installed, but the ME3 Profile Dump ASI could not be installed:\n\n{e.Message}",
                        "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (window.AppExiting) return;

            var migration = new Me3ProfileMigration(window);
            active = migration;
            window.Closed += migration.WindowClosed;
            _ = migration.ListenAsync(); // Bind before launching ME3.
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

        private async Task ListenAsync()
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellation.Token);
                var json = await ReadJsonAsync(pipe, cancellation.Token);
                CompleteWaitingTask("ME3 multiplayer profile received.");
                await window.Dispatcher.InvokeAsync(() => Import(json));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                M3Log.Exception(e, "ME3 profile migration failed");
                CompleteWaitingTask("ME3 profile migration failed.");
                if (!cancellation.IsCancellationRequested)
                    await window.Dispatcher.InvokeAsync(() =>
                        M3L.ShowDialog(window, $"Could not receive the ME3 profile: {e.Message}", "Profile migration failed", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally { Dispose(); }
        }

        private static async Task<string> ReadJsonAsync(Stream pipe, CancellationToken token)
        {
            using var data = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = await pipe.ReadAsync(buffer, token);
                if (count == 0) break;
                var end = Array.IndexOf(buffer, (byte)'\n', 0, count);
                var length = end < 0 ? count : end;
                if (data.Length + length > MaxJsonBytes)
                    throw new InvalidDataException("The ME3 profile export exceeds the 8 MB pipe limit.");
                data.Write(buffer, 0, length);
                if (end >= 0) break;
            }
            if (data.Length == 0) throw new InvalidDataException("The named pipe contained no profile JSON.");
            return new UTF8Encoding(false, true).GetString(data.ToArray());
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
