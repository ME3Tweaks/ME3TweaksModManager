using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    class M3UpdateCheck
    {

        private static int declineCountSkipsRemaining = 0;

        public static void SetUpdateDeclined()
        {
            declineCountSkipsRemaining = 6; // We will skip the next 6 manifest refreshes
        }

        public static void CheckManifestForUpdates(MainWindow window)
        {
            if (declineCountSkipsRemaining > 0)
            {
                declineCountSkipsRemaining--;
                return; // We don't check for updates if the decline count is still above zero
                // This is how many times manifest has refreshed since the user declined since we don't want to spam
                // user with update prompts, but we want more server refreshes
            }

            // Is the panel already showing, e.g. computer was left on overnight? We don't want to spam panels
            if (window.HasAnyQueuedPanelsOfType(typeof(ProgramUpdateNotification)))
            {
                M3Log.Information(@"Program update notification panel is visible or queued; not showing again on periodic refresh");
                return;
            }


            if (ServerManifest.TryGetInt(ServerManifest.M3_LATEST_BUILD_NUMBER, out var latestServerBuildNumer))
            {
                if (latestServerBuildNumer > App.BuildNumber)
                {
                    M3Log.Information(@"Found update for Mod Manager: Build " + latestServerBuildNumer);

                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var updateAvailableDialog = new ProgramUpdateNotification();
                        updateAvailableDialog.Close += (sender, args) => { window.ReleaseBusyControl(); };
                        window.ShowBusyControl(updateAvailableDialog, true);
                    });
                }
#if !DEBUG
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
        }
    }
}
