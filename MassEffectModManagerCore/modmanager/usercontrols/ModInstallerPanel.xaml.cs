
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.installer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstallerPanel.xaml
    /// </summary>
    public partial class ModInstallerPanel : MMBusyPanelBase, INotifyPropertyChanged
    {
        /// <summary>
        /// The time between percent updates in ms.
        /// </summary>
        public const int PERCENT_REFRESH_COOLDOWN = 125;

        /// <summary>
        /// Options for the installer. Contains results of installation as well
        /// </summary>
        public ModInstallOptionsPackage InstallOptionsPackage { get; private set; }

        /// <summary>
        /// The mod installer backend
        /// </summary>
        private ModInstaller Installer { get; set; }

        /// <summary>
        /// If installation of the mod succeeded
        /// </summary>
        public bool InstallationSucceeded { get; private set; }

        /// <summary>
        /// If installation of the mod was canceled
        /// </summary>
        public bool InstallationCancelled { get; private set; }

        /// <summary>
        /// If a mod is currently installing
        /// </summary>
        public bool ModIsInstalling { get; private set; }

        /// <summary>
        /// Initializes the Mod Installer panel.
        /// </summary>
        /// <param name="package">The installation options package</param>
        public ModInstallerPanel(ModInstallOptionsPackage package)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Mod Installer", this);
            InstallOptionsPackage = package;
            LoadCommands();
            lastPercentUpdateTime = DateTime.Now;
            if (!package.BatchMode)
            {
                // Don't reload between batch installs to improve performance.
                package.InstallTarget.ReloadGameTarget(false); //Reload so we can have consistent state with disk
            }

            Action = M3L.GetString(M3L.string_preparingToInstall);
        }

        private void LoadCommands()
        {
            // Has no commands anymore
        }

        /// <summary>
        /// Used to gate percent updates to avoid UI flooding.
        /// </summary>
        private DateTime lastPercentUpdateTime;

        /// <summary>
        /// The current ongoing action to display to the user.
        /// </summary>
        public string Action { get; set; }
        /// <summary>
        /// The current percentage to show the user.
        /// </summary>
        public int Percent { get; set; }

        /// <summary>
        /// The bound percentage visibility.
        /// </summary>
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;

        private async void BeginInstallingMod()
        {
            Installer = new ModInstaller(InstallOptionsPackage);
            Installer.SetPercent = SetPercent;
            Installer.SetAction = SetAction;
            Installer.SetPercentVisibility = SetPercentVisibility;

            M3Log.Information($@"BeginInstallingMod(): {InstallOptionsPackage.ModBeingInstalled.ModName}");
            Installer.PerformPrecheck();
            if (Installer.InstallationResult.Aborted == true)
            {
                // Precheck failed
                ModInstallationCompleted(null);
                return;
            }


            Exception error = null;
            try
            {
                // Contains synchronous work so must run on background thread still.
                await Task.Run(async () => await Installer.InstallMod());
            }
            catch (Exception ex)
            {
                error = ex;
            }

            ModInstallationCompleted(error);
        }

        private void SetPercent(int percent)
        {
            DateTime now = DateTime.Now;
            if (percent == 0 || percent == 100 || (now - lastPercentUpdateTime).Milliseconds > PERCENT_REFRESH_COOLDOWN)
            {
                //Don't update UI too often
                Percent = percent;
                lastPercentUpdateTime = now;
            }
        }

        private void SetAction(string action)
        {
            Action = action;
        }

        private void SetPercentVisibility(bool visible)
        {
            PercentVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ModInstallationCompleted(Exception error)
        {
            SystemSleepManager.AllowSleep(); // We can go back to sleep again.

            var installerResult = Installer.InstallationResult;
            if (error != null)
            {
                M3Log.Error(@"An error occurred during mod installation.");
                M3Log.Error(App.FlattenException(error));
                installerResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER; // Set result code for telemetry
                M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_errorOccuredDuringInstallation, App.FlattenException(error)), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Didn't hit unhandled exception
                if (installerResult.Result != EModInstallerResult.NO_RESULT_CODE)
                {
                    // Installer set a result code
                    Result.SelectedTarget = InstallOptionsPackage.InstallTarget;

                    InstallationSucceeded = installerResult.Result is EModInstallerResult.INSTALL_SUCCESSFUL or EModInstallerResult.INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS;
                    InstallationCancelled = installerResult.Aborted;

                    if (installerResult.ErrorMessage != null)
                    {
                        M3L.ShowDialog(mainwindow, installerResult.ErrorMessage, installerResult.ErrorTitle ?? installerResult.Result.ToString(), MessageBoxButton.OK, installerResult.ErrorImage);
                    }

                    // Set post launch tool if applicable
                    if (InstallationSucceeded && !string.IsNullOrWhiteSpace(InstallOptionsPackage.ModBeingInstalled.PostInstallToolLaunch))
                    {
                        Result.ToolToLaunch = InstallOptionsPackage.ModBeingInstalled.PostInstallToolLaunch;
                    }
                }
                else
                {
                    M3Log.Fatal(@"The application is going to crash due to a sanity check failure in the mod installer (no result!). Please report this to ME3Tweaks so this can be fixed.");

                    // Once this issue has been fixed these lines can be commented out or removed (June 14 2020)
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_appAboutToCrashYouFoundBug), M3L.GetString(M3L.string_appCrash), MessageBoxButton.OK, MessageBoxImage.Error);
                    M3Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
                    // End bug message
                    M3Log.Fatal(@"Mod installer did not have result code. This should be caught and handled, but it wasn't!");
                    throw new Exception(@"Mod installer did not have result code. This should be caught and handled, but it wasn't!");
                }
            }

            // This must go after handling of result so the variable is properly set
            // Only make changes if user didn't cancel
            if (!InstallationCancelled)
            {
                Result.AddTargetMerges(InstallOptionsPackage.InstallTarget);
            }

            var telemetryInfo = new Dictionary<string, string>()
            {
                {@"Mod name", $@"{InstallOptionsPackage.ModBeingInstalled.ModName} {InstallOptionsPackage.ModBeingInstalled.ModVersionString}"},
                {@"Installed from", InstallOptionsPackage.ModBeingInstalled.IsInArchive ? @"Archive" : @"Library"},
                {@"Type", InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null ? @"RCW .me2mod" : @"Standard"},
                {@"Game", InstallOptionsPackage.ModBeingInstalled.Game.ToString()},
                {@"Result", installerResult.Result.ToString()},
                {@"Author", InstallOptionsPackage.ModBeingInstalled.ModDeveloper}
            };

            string alternateOptionsPicked = "";
            foreach (var job in InstallOptionsPackage.ModBeingInstalled.InstallationJobs)
            {
                foreach (var af in job.AlternateFiles)
                {
                    if (string.IsNullOrWhiteSpace(af.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{af.FriendlyName}={af.UIIsSelected.ToString()}";
                }
                foreach (var ad in job.AlternateDLCs)
                {
                    if (string.IsNullOrWhiteSpace(ad.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{ad.FriendlyName}={ad.UIIsSelected.ToString()}";
                }
            }

            if (!string.IsNullOrWhiteSpace(alternateOptionsPicked))
            {
                telemetryInfo[@"Alternate Options Selected"] = alternateOptionsPicked;
            }

            TelemetryInterposer.TrackEvent(@"Installed a mod", telemetryInfo);
            OnClosing(DataEventArgs.Empty);
        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            InstallationSucceeded = false;
            InstallationCancelled = true;
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !ModIsInstalling)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            M3Log.Information($@">>>>>>> Initializing mod installer panel for mod: {InstallOptionsPackage.ModBeingInstalled.ModName} {InstallOptionsPackage.ModBeingInstalled.ModVersionString} for game {InstallOptionsPackage.ModBeingInstalled.Game}. Install source: {(InstallOptionsPackage.ModBeingInstalled.IsInArchive ? @"Archive" : @"Library (disk)")}"); //do not localize
            GC.Collect(); //this should help with the oddities of missing radio buttons somehow still in the visual tree from busyhost
            InitializeComponent();
            BeginInstallingMod();
        }

        protected override void OnClosing(DataEventArgs e)
        {
            // Ensure we can go to sleep still. This probably isn't necessary, but probably isn't a bad idea either.
            SystemSleepManager.AllowSleep();

            if (InstallOptionsPackage.ModBeingInstalled.Archive != null)
            {
                InstallOptionsPackage.ModBeingInstalled.Archive.Dispose();
                InstallOptionsPackage.ModBeingInstalled.Archive = null;
            }

            var mergeMods = InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.BASEGAME)?.MergeMods;
            if (mergeMods != null)
            {
                foreach (var mm in mergeMods)
                {
                    mm.ReleaseAssets();
                }
            }

            base.OnClosing(DataEventArgs.Empty);
        }

        private void DebugPrintInstallationQueue_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            //if (InstallOptionsPackage.ModBeingInstalled != null)
            //{
            //    var queues = InstallOptionsPackage.ModBeingInstalled.GetInstallationQueues(InstallOptionsPackage.InstallTarget);
            //    Debug.WriteLine(@"Installation Queue:");
            //    foreach (var job in queues.Item1)
            //    {
            //        foreach (var file in job.Value.unpackedJobMapping)
            //        {
            //            Debug.WriteLine($@"[UNPACKED {job.Key.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }

            //    foreach (var job in queues.Item2)
            //    {
            //        foreach (var file in job.Item3)
            //        {
            //            Debug.WriteLine($@"[SFAR {job.job.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }
            //}
#endif
        }

        public override bool CanBeForceClosed()
        {
            // Cannot be force closed
            return false;
        }

        // ISizeAdjustable Interface
        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
