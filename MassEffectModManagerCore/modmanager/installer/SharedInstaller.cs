using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.installer
{
    /// <summary>
    /// Shared code for mod installs
    /// </summary>
    public static class SharedInstaller
    {
        public enum EModPrequesitesStatus
        {
            INVALID,
            OK,
            MISSING_REQUIRED_DLC,
            MISSING_SINGLE_REQUIRED_DLC
        }

        /// <summary>
        /// Validates a mod can install. Shows a dialog if it cannot.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="mod"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool ValidateModCanInstall(Window window, Mod mod, GameTarget target)
        {
            var metas = target.GetMetaMappedInstalledDLC();
            var missingRequiredDLC = mod.ValidateRequiredModulesAreInstalled(target, metas);
            if (missingRequiredDLC.Count > 0)
            {
                M3Log.Error(@"Required DLC is missing for installation against target: " + string.Join(@", ", missingRequiredDLC));
                string dlcText = "";
                foreach (var dlc in missingRequiredDLC)
                {
                    var info = TPMIService.GetThirdPartyModInfo(dlc.DLCFolderName.Key, mod.Game);
                    dlcText += $"\n - {dlc.ToUIString(info, false)}";
                }

                ShowDialog(window, M3L.GetString(M3L.string_dialogRequiredContentMissing, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!mod.ValidateSingleOptionalRequiredDLCInstalled(target, metas))
            {
                M3Log.Error($@"Mod requires installation of at least one of the following DLC, none of which are installed: {string.Join(',', mod.OptionalSingleRequiredDLC)}");

                string dlcText = "";
                foreach (var dlc in mod.OptionalSingleRequiredDLC)
                {
                    var info = TPMIService.GetThirdPartyModInfo(dlc.DLCFolderName.Key, mod.Game);
                    dlcText += $"\n - {dlc.ToUIString(info, false)}";
                }

                ShowDialog(window, M3L.GetString(M3L.string_interp_error_singleRequiredDlcMissing, mod.ModName, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            //Check/warn on official headers
            if (target.Game.IsOTGame() && !PrecheckHeaders(window, mod, target))
            {
                //logs handled in precheck
                return false;
            }

            if (mod.Game == MEGame.ME1 && mod.RequiresAMD && !App.IsRunningOnAMD)
            {
                M3Log.Error(@"This mod can only be installed on AMD processors, as it does nothing for Intel users.");
                M3L.ShowDialog(window, M3L.GetString(M3L.string_modRequiresAMDProcessor), M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Mod can install
            return true;
        }

        /// <summary>
        /// Just to clean up code a bit.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="message"></param>
        /// <param name="caption"></param>
        /// <param name="buttons"></param>
        /// <param name="icon"></param>
        private static void ShowDialog(Window window, string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                M3L.ShowDialog(window, message, caption, buttons, icon);
            });
        }

        /// <summary>
        /// Checks if DLC specified by the job installation headers exist and prompt user to continue or not if the DLC is not found. This is only used jobs that are not CUSTOMDLC.'
        /// </summary>
        /// <param name="installationJobs">List of jobs to look through and validate</param>
        /// <returns></returns>
        private static bool PrecheckHeaders(Window window, Mod mod, GameTarget target)
        {
            foreach (var job in mod.InstallationJobs)
            {
                if (job.Header == ModJob.JobHeader.ME1_CONFIG)
                {
                    //Make sure config files exist.
                    var destFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOEngine.ini");
                    if (!File.Exists(destFile))
                    {
                        ShowDialog(Window.GetWindow(window), M3L.GetString(M3L.string_dialogRunGameOnceFirst), M3L.GetString(M3L.string_gameMustBeRunAtLeastOnce), MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    continue;
                }

                if (!target.IsOfficialDLCInstalled(job.Header))
                {
                    M3Log.Warning($@"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                    //Prompt user
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dlcName = ModJob.GetHeadersToDLCNamesMap(mod.Game)[job.Header];
                        string resolvedName = dlcName;
                        MEDirectories.OfficialDLCNames(mod.Game).TryGetValue(dlcName, out resolvedName);
                        string message = M3L.GetString(M3L.string_interp_dialogOfficialTargetDLCNotInstalled, mod.ModName, dlcName, resolvedName);
                        if (job.RequirementText != null)
                        {
                            message += M3L.GetString(M3L.string_dialogJobDescriptionMessageHeader);
                            message += $"\n{job.RequirementText}"; //Do not localize
                        }

                        message += M3L.GetString(M3L.string_dialogJobDescriptionMessageFooter);
                        MessageBoxResult result = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_dialogJobDescriptionMessageTitle, MEDirectories.OfficialDLCNames(mod.Game)[ModJob.GetHeadersToDLCNamesMap(mod.Game)[job.Header]]), MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.No)
                        {
                            cancel = true;
                            return;
                        }

                    });
                    if (cancel)
                    {
                        M3Log.Error(@"User canceling installation");

                        return false;
                    }

                    M3Log.Warning(@"User continuing installation anyways");
                }
                else
                {
                    M3Log.Information(@"Official headers check passed for header " + job.Header, Settings.LogModInstallation);
                }
            }

            return true;
        }
    }
}
