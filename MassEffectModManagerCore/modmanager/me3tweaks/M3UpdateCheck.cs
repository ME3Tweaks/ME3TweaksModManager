using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.online;
using ME3TweaksModManager.modmanager.usercontrols;
using System.Windows;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    class M3UpdateCheck
    {

        private static int declineCountSkipsRemaining = 0;

        public static bool ForceStableDowngrade { get; private set; }

        public static void SetUpdateDeclined()
        {
            declineCountSkipsRemaining = 6; // We will skip the next 6 manifest refreshes
        }

        /// <summary>
        /// Checks if the manifest has an update for the program. If so, shows the update panel.
        /// </summary>
        /// <param name="window">Window to show update panel on</param>
        /// <returns>True if update found, false otherwise.</returns>
        public static bool CheckManifestForUpdates(MainWindow window)
        {
            if (declineCountSkipsRemaining > 0)
            {
                declineCountSkipsRemaining--;
                return false; // We don't check for updates if the decline count is still above zero
                // This is how many times manifest has refreshed since the user declined since we don't want to spam
                // user with update prompts, but we want more server refreshes
            }

            // Is the panel already showing, e.g. computer was left on overnight? We don't want to spam panels
            if (window.HasAnyQueuedPanelsOfType(typeof(ProgramUpdateNotification)))
            {
                M3Log.Information(@"Program update notification panel is visible or queued; not showing again on periodic refresh");
                return false;
            }


            if (ServerManifest.TryGetInt(ServerManifest.M3_LATEST_BUILD_NUMBER, out var latestServerBuildNumer))
            {
                var isDowngrading = ForceStableDowngrade && latestServerBuildNumer < App.BuildNumber;
                if (latestServerBuildNumer > App.BuildNumber || isDowngrading)
                {
                    if (isDowngrading)
                    {
                        M3Log.Information(@"Found stable downgrade for Mod Manager: Build " + latestServerBuildNumer);
                    }
                    else
                    {
                        M3Log.Information(@"Found update for Mod Manager: Build " + latestServerBuildNumer);
                    }

                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var updateAvailableDialog = new ProgramUpdateNotification();
                        if (isDowngrading)
                        {
                            updateAvailableDialog.UpdateMessage = M3L.GetString(M3L.string_desc_downgrade);
                        }
                        updateAvailableDialog.Close += (sender, args) => { window.ReleaseBusyControl(); };
                        window.ShowBusyControl(updateAvailableDialog, true);
                    });
                    return true;
                }
#if !DEBUG
                // Same-version patch update
                else if (latestServerBuildNumer == App.BuildNumber)
                {
                    if (ServerManifest.TryGetString(ServerManifest.M3_BUILD_RERELEASE_MD5, out var md5) && !string.IsNullOrWhiteSpace(md5))
                    {
                        var localmd5 = MUtilities.CalculateHash(App.ExecutableLocation);
                        if (localmd5 != md5)
                        {
                            //Update is available.
                            {
                                M3Log.Information(@"MD5 of local exe doesn't match server version, minor update detected.");
                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    var updateAvailableDialog = new ProgramUpdateNotification(localmd5);
                                    updateAvailableDialog.UpdateMessage = M3L.GetString(M3L.string_interp_minorUpdateAvailableMessage, App.BuildNumber.ToString());
                                    updateAvailableDialog.Close += (sender, args) => { window.ReleaseBusyControl(); };
                                    window.ShowBusyControl(updateAvailableDialog, true);
                                });
                                return true;
                            }
                        }
                    }
                }
#endif  
                else
                {
                    M3Log.Information(@"Mod Manager is up to date");
                }

            }

            // No update found
            return false;
        }

        internal static void DowngradeToStable()
        {
            declineCountSkipsRemaining = 0;
            ForceStableDowngrade = true;
            ServerManifest.FetchOnlineStartupManifest(false, performTouchup: false);
            Application.Current.Dispatcher.Invoke(() =>
            {
                var hasUpdate = CheckManifestForUpdates(MainWindow.Instance);
                ForceStableDowngrade = false;

                if (!hasUpdate)
                {
                    M3L.ShowDialog(MainWindow.Instance, M3L.GetString(M3L.string_interp_noDowngradeAvailable, App.AppVersionHR), M3L.GetString(M3L.string_title_downgradeCheck));
                }
            });
        }

#if DEBUG
        void ForceReferences()
        {
            // This forces a reference to keep it in the file
            M3L.GetString(M3L.string_ok);
            MUtilities.CalculateHash(@"");
        }
#endif
    }
}
