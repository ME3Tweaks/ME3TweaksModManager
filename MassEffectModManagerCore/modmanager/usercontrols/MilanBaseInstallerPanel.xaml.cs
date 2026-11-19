using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.milan;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.ui;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MilanBaseInstallerPanel.xaml
    /// </summary>
    public partial class MilanBaseInstallerPanel : MMBusyPanelBase
    {
        public ObservableCollectionExtended<GameTargetWPF> LE3Targets { get; } = new();
        public GameTarget SelectedTarget { get; set; }
        public string InstallLocation { get; set; }
        public string StatusText { get; set; } = @"";
        public bool StartFreshProfile { get; set; } = true;
        public bool MigrateMe3Profile { get; set; }
        public bool ImportLempProfile { get; set; }

        /// <summary>
        /// LEMP (not LMP) mp_save.asi text save to convert after install
        /// </summary>
        public string LempSavePath { get; set; }

        /// <summary>
        /// The install controller for the running install.
        /// </summary>
        private MilanBaseInstaller InstallController { get; set; }
        private GameTargetWPF builtTarget;
        private readonly Mod milanMod;

        /// <summary>
        /// Set when a Project Milan build is driving this install. It runs without asking anything and Project Milan generates the slash manifest.
        /// </summary>
        private readonly MilanBuildSession session;
        private readonly string sessionDestination;
        private readonly string sessionLE3Target;
        private readonly bool sessionGeneratesSlash;

        /// <summary>
        /// Existing LMP install to rebuild in place. User already agreed to the update so it starts on its own.
        /// </summary>
        private readonly string updateDestination;

        #region Commands
        public GenericCommand InstallCommand { get; private set; }
        public GenericCommand CancelCommand { get; private set; }
        public GenericCommand BrowseLocationCommand { get; private set; }
        public GenericCommand BrowseLempSaveCommand { get; private set; }
        #endregion

        /// <summary>
        /// Prechecks and then opens the milan installer panel
        /// </summary>
        /// <param name="mw"></param>
        /// <param name="session">Set when a Project Milan build is driving this; destination and le3Target can then override the defaults</param>
        /// <param name="updateDestination">Existing LMP install to update. Install starts automatically</param>
        internal static void BuildBaseMilan(MainWindow mw, Mod milanMod, MilanBuildSession session = null, string destination = null, string le3Target = null, bool generateSlash = true, string updateDestination = null)
        {
            if (milanMod == null)
            {
                // This shouldn't be available anymore now that alpha menu is gone.
                M3L.ShowDialog(MainWindow.Instance, "You must download and import Legendary Edition Multiplayer into your mod library in order to make a game target for LMP.", "Legendary Edition Multiplayer not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var mbip = new MilanBaseInstallerPanel(milanMod, session, destination, le3Target, generateSlash, updateDestination);
            mbip.Close += (a, b) => { mw.ReleaseBusyControl(); };
            // Don't let a startup notice block the build, keep it queued behind the installer
            bool deferUpdateNotice = session != null && mw.BusyContentM3 is SingleItemPanel2 { Content: ModUpdateInformationPanel };
            mw.ShowBusyControl(mbip, swapImmediately: deferUpdateNotice);
        }

        private MilanBaseInstallerPanel(Mod milanMod, MilanBuildSession session, string destination, string le3Target, bool generateSlash, string updateDestination)
        {
            this.milanMod = milanMod;
            this.updateDestination = updateDestination;
            this.session = session;
            sessionGeneratesSlash = generateSlash;
            sessionDestination = destination;
            sessionLE3Target = le3Target;
            StatusText = $"Version {milanMod.ModVersionString}";
            // Should this be here? Or should we have this in OnPanelVisible so queued panels
            // don't get bound?
            LoadCommands();
        }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(Install, CanInstall);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
            BrowseLempSaveCommand = new GenericCommand(BrowseLempSave, () => InstallController == null);
            //BrowseLocationCommand = new GenericCommand(SelectTargetFolder, );
        }

        private void BrowseLempSave()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select a LEMP multiplayer save",
                Filter = "LEMP multiplayer save|*.txt|All files|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog(window) != true)
            {
                return;
            }

            // Check it now so a bad file doesn't show up after a long install
            try
            {
                LmpProfileImport.ConvertLempSave(ofd.FileName);
                LempSavePath = ofd.FileName;
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $"Selected LEMP save {ofd.FileName} can't be converted");
                M3L.ShowDialog(window, $"This file can't be converted to an LMP multiplayer profile:\n\n{e.Message}", "Invalid LEMP save", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Install()
        {
            // Project Milan pipelines rebuild the same dev install over and over, so don't ask.
            if (session != null && !EnsureInstallDirectory(out var directoryError))
            {
                // Show error message on failure
                session.Finished(false, directoryError);
                OnClosing(DataEventArgs.Empty);
                return;
            }
            if (session != null || PerformPrecheck())
            {
                // Initialize and begin installation.
                var interop = new MBIInterop
                {
                    SetStatus = (status) =>
                    {
                        Dispatcher.Invoke(() => StatusText = status);
                        session?.SetStatus(status);
                    },
                    InstallationFinished = installFinished,
                    AddTarget = target => builtTarget = target,
                    GenerateSlash = session != null && sessionGeneratesSlash ? session.GenerateSlash : null,
                    UserInstall = session != null && !sessionGeneratesSlash,
                    ConfirmSkipUnavailableLanguages = session == null ? ConfirmSkipUnavailableLanguages : null
                };
                userCancelledInstall = false;
                var controller = new MilanBaseInstaller();
                var sourceTarget = SelectedTarget;
                var destination = InstallLocation;
                builtTarget = null;
                InstallController = controller;
                SourceTargetSelector.IsEnabled = false;
                ProfileImportPanel.IsEnabled = false;
                Task.Run(() =>
                {
                    controller.CreateInstallation(milanMod, sourceTarget, destination, interop);
                });
            }
        }

        /// <summary>
        /// Checks the install directory and its top level folders are writable
        /// </summary>
        private bool IsInstallDirectoryWritable()
        {
            if (!MUtilities.IsDirectoryWritable(InstallLocation))
            {
                return false;
            }

            foreach (var subDir in Directory.GetDirectories(InstallLocation))
            {
                if (!MUtilities.IsDirectoryWritable(subDir))
                {
                    M3Log.Warning($"Installation subdirectory {subDir} is not writable");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Makes sure the install directory exists and is writable. If it isn't, runs PermissionsGranter to create it
        /// </summary>
        private bool EnsureInstallDirectory(out string error)
        {
            error = null;
            try
            {
                if (Directory.Exists(InstallLocation))
                {
                    if (IsInstallDirectoryWritable())
                    {
                        return true;
                    }

                    // Could have been made by an admin
                    M3Log.Warning($"Installation directory {InstallLocation} is not writable, requesting permissions");
                    if (MUtilities.EnableWritePermissionsToFolders(new List<string> { InstallLocation }) && IsInstallDirectoryWritable())
                    {
                        return true;
                    }
                }
                else
                {
                    if (MUtilities.CreateDirectoryWithWritePermission(InstallLocation) && MUtilities.IsDirectoryWritable(InstallLocation))
                    {
                        return true;
                    }
                }

                error = $"Mod Manager could not create or write to the installation directory:\n\n{InstallLocation}\n\nThis usually means Mod Manager doesn't have permission to write there. If you cancelled the administrator prompt, try again and allow it.";
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Unable to create installation directory:");
                error = $"Mod Manager could not create the installation directory:\n\n{InstallLocation}\n\n{e.Message}";
            }

            M3Log.Error($"Unable to create or write to installation directory {InstallLocation}");
            return false;
        }

        // This method should be async cause it's going to be long running for hash checks
        private bool PerformPrecheck()
        {
            if (!EnsureInstallDirectory(out var directoryError))
            {
                M3L.ShowDialog(window, directoryError, "Unable to create installation directory", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!Directory.Exists(InstallLocation))
            {
                M3Log.Error($"Installation path '{InstallLocation}' does not exist.");
                M3L.ShowDialog(window, $"The installation directory does not exist:\n\n{InstallLocation}", "Unable to create installation directory", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Updating an existing install, it's supposed to have stuff in it
            if (updateDestination != null)
            {
                return true;
            }

            bool nukeContents = false;
            var filesInDir = Directory.GetFiles(InstallLocation);
            if (filesInDir.Length > 0)
            {
                var userResult = M3L.ShowDialog(window, $"The install location is not empty. If you proceed, all contents will be deleted and a new installation will be placed at the specified location:\n\n{InstallLocation}", "Warning: Directory not empty", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (userResult == MessageBoxResult.OK)
                {
                    M3Log.Information($"Installation path '{InstallLocation}' is not empty. The user indicated they want to use it anyways!");
                }
                else
                {
                    M3Log.Error($"Installation path '{InstallLocation}' is not empty. It contains {filesInDir.Length} files. This location cannot be used.");
                    return false;
                }
            }

            if (!nukeContents)
            {
                var foldersInDir = Directory.GetDirectories(InstallLocation);
                if (foldersInDir.Length > 0)
                {
                    var userResult = M3L.ShowDialog(window, $"The install location is not empty. If you proceed, all contents will be deleted and a new installation will be placed at the specified location:\n\n{InstallLocation}", "Warning: Directory not empty", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (userResult == MessageBoxResult.OK)
                    {
                        M3Log.Information($"Installation path '{InstallLocation}' is not empty. The user indicated they want to use it anyways!");
                    }
                    else
                    {
                        M3Log.Error($"Installation path '{InstallLocation}' is not empty. It contains {foldersInDir.Length} subfolders. This location cannot be used.");
                        return false;
                    }
                }
            }

            if (nukeContents)
            {
                MUtilities.DeleteFilesAndFoldersRecursively(InstallLocation, deleteDirectoryItself: false);
            }

            // Todo: Hash check files.
            return true;
        }

        /// <summary>
        /// Set when the user backs out of the install from a prompt, so it isn't reported as a failure
        /// </summary>
        private bool userCancelledInstall;

        /// <summary>
        /// Called from the install thread. Tells the user which languages can't be installed and asks whether to continue without them
        /// </summary>
        private bool ConfirmSkipUnavailableLanguages(IReadOnlyList<MilanInstallPlan.UnavailableLanguage> languages)
        {
            var lines = languages.OrderBy(x => x.Language.ToString()).Select(x =>
            {
                var reasons = new List<string>();
                if (x.ModifiedFiles.Count > 0)
                {
                    reasons.Add($"{x.ModifiedFiles.Count} modified file(s) with no unmodified copy in your backup");
                }
                if (x.MissingFiles.Count > 0)
                {
                    reasons.Add($"{x.MissingFiles.Count} missing file(s)");
                }
                return $"  {x.Language}: {string.Join(", ", reasons)}";
            });

            var message = "The following languages are missing unmodified files from LE3 and won't be available in your LMP install:\n\n" +
                          string.Join("\n", lines) +
                          "\n\nInstall without these languages?";

            bool proceed = false;
            Dispatcher.Invoke(() =>
            {
                proceed = M3L.ShowDialog(window, message, "Languages unavailable", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            });

            if (!proceed)
            {
                M3Log.Information(@"User aborted Milan install due to unavailable languages");
                userCancelledInstall = true;
            }
            return proceed;
        }

        private void installFinished(bool success, string message)
        {
            InstallController = null;
            session?.Finished(success, message);
            if (success)
            {
                M3Log.Information("Milan base installation completed successfully.");
                Dispatcher.Invoke(() =>
                {
                    var owner = mainwindow;
                    var me3Target = owner.GetME3TargetForProfileMigration();
                    var migrate = MigrateMe3Profile;
                    var lempSave = ImportLempProfile ? LempSavePath : null;
                    var n7hq = ImportN7HqProfile;
                    OnClosing(DataEventArgs.Empty);
                    var refresh = owner.RefreshAfterLMPInstall(builtTarget, milanMod);
                    // Don't tell Project Milan we're done until the target list is back, or its next build request sees no LE3 targets
                    if (session != null)
                    {
                        refresh.ContinueWith(_ => session.Finished(true, message));
                    }
                    if (lempSave != null)
                    {
                        LmpProfileImport.ImportLempSave(owner, lempSave);
                    }
                    else if (n7hq)
                    {
                        N7HqProfileImport.Import(owner);
                    }
                    else if (migrate && me3Target != null)
                    {
                        _ = Me3ProfileMigration.StartAsync(owner, me3Target);
                    }
                    else if (migrate)
                    {
                        M3L.ShowDialog(owner, $"{milanMod.ModName} was installed, but Mod Manager could not find an original ME3 installation for profile migration.", "ME3 target not found", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            else
            {
                session?.Finished(false, message);
                M3Log.Error($"Milan base installation failed: {message}");
                Dispatcher.Invoke(() =>
                {
                    SourceTargetSelector.IsEnabled = true;
                    ProfileImportPanel.IsEnabled = true;
                    StatusText = userCancelledInstall ? "Installation cancelled" : "Installation failed";
                    if (session != null)
                    {
                        OnClosing(DataEventArgs.Empty); // Project Milan shows the error
                    }
                    else if (userCancelledInstall)
                    {
                        // Not a failure
                    }
                    else
                    {
                        M3L.ShowDialog(window, $"The installation failed with the following error:\n\n{message}", "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        private bool CanInstall()
        {
            // We check install path when you press install to improve performance
            return InstallController == null && SelectedTarget != null && !string.IsNullOrWhiteSpace(InstallLocation)
                   && (!ImportLempProfile || LempSavePath != null);
        }

        /// <summary>
        /// Gets the Game folder LE3 is in. Null if it can't be used as a source.
        /// </summary>
        private static string GetLEGameDirectory(GameTarget le3Target)
        {
            if (string.IsNullOrWhiteSpace(le3Target?.TargetPath))
            {
                return null;
            }

            var gameDir = Directory.GetParent(Path.TrimEndingDirectorySeparator(Path.GetFullPath(le3Target.TargetPath)));
            if (gameDir == null)
            {
                return null;
            }

            if (!le3Target.Supported && !le3Target.TextureModded)
            {
                return gameDir.FullName;
            }

            return Directory.Exists(Path.Combine(gameDir.FullName, @"Launcher")) ? gameDir.FullName : null;
        }

        // Install next to the other LE games
        public void OnSelectedTargetChanged()
        {
            var gameDir = GetLEGameDirectory(SelectedTarget);
            InstallLocation = gameDir != null ? Path.Combine(gameDir, @"LMP") : null;
        }

        private bool CanCancel()
        {
            return InstallController == null;
        }

        private void Cancel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            // Escape can close this, if the installation is not in progress.
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            ProfileImportPanel.Visibility = !File.Exists(LMPDirectory.LocalProfilePath)
                ? Visibility.Visible : Visibility.Collapsed;

            // Texture/modded files get pulled from the backup instead, so those targets are only usable if there is one
            var hasLE3Backup = BackupService.GetGameBackupPath(MEGame.LE3) != null;
            LE3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == MEGame.LE3
                                                                            && x.Selectable
                                                                            && (hasLE3Backup || !x.TextureModded)
                                                                            && GetLEGameDirectory(x) != null));

            SelectedTarget = LE3Targets.FirstOrDefault();
            if (LE3Targets.Count == 0)
            {
                M3Log.Error($"Aborting LMP install: No suitable LE3 targets available and no backup");
                const string noSourceMessage = "Unable to find an installation source for Legenary Edition Multiplayer. Legendary Edition Multiplayer requires a backup or a copy of Mass Effect 3 Legendary Edition that is not texture modded.";
                if (session != null)
                {
                    // Project Milan is waiting on us, so it has to hear about this
                    session.Finished(false, noSourceMessage);
                }
                else
                {
                    M3L.ShowDialog(MainWindow.Instance, noSourceMessage, "Unable to install LMP", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Close panel
                OnClosing(DataEventArgs.Empty);
                return;
            }

            // Update rebuilds the existing install in place
            if (updateDestination != null)
            {
                InstallLocation = updateDestination;
                if (CanInstall())
                {
                    Install();
                }
                return;
            }

            if (session != null)
            {
                if (sessionLE3Target != null)
                {
                    SelectedTarget = LE3Targets.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.TargetPath), Path.GetFullPath(sessionLE3Target), StringComparison.OrdinalIgnoreCase))
                                     ?? new GameTargetWPF(MEGame.LE3, sessionLE3Target, false, isCustomOption: true, skipInit: true);
                }
                if (sessionDestination != null)
                {
                    InstallLocation = sessionDestination;
                }

                if (CanInstall())
                {
                    Install();
                }
                else
                {
                    session.Finished(false, "Mod Manager couldn't determine out which LE3 to build from or where to put the installation");
                    OnClosing(DataEventArgs.Empty);
                }
            }
        }
    }
}
