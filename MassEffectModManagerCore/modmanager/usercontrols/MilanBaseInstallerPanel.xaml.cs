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
        public bool MigrateMe3Profile { get; set; }

        /// <summary>
        /// Development builds only: generate a new slash manifest for the Project Milan mod instead of applying the existing one.
        /// </summary>
        public bool GenerateSlashManifest { get; set; }

        /// <summary>
        /// The install controller for the running install.
        /// </summary>
        private MilanBaseInstaller InstallController { get; set; }
        private GameTargetWPF builtTarget;

        #region Commands
        public GenericCommand InstallCommand { get; private set; }
        public GenericCommand CancelCommand { get; private set; }
        public GenericCommand BrowseLocationCommand { get; private set; }
        #endregion

        /// <summary>
        /// Prechecks and then opens the milan installer panel
        /// </summary>
        /// <param name="mw"></param>
        internal static void BuildBaseMilan(MainWindow mw, Mod milanMod)
        {
            if (milanMod == null)
            {
                M3L.ShowDialog(MainWindow.Instance, "You must download and import Project Milan into your mod library in order to make a game target for LMP.", "Project Milan not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var mbip = new MilanBaseInstallerPanel();
            mbip.Close += (a, b) => { mw.ReleaseBusyControl(); };
            mw.ShowBusyControl(mbip);
        }

        public MilanBaseInstallerPanel()
        {
            // Should this be here? Or should we have this in OnPanelVisible so queued panels
            // don't get bound?
            LoadCommands();
        }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(Install, CanInstall);
            CancelCommand = new GenericCommand(Cancel, CanCancel);
            //BrowseLocationCommand = new GenericCommand(SelectTargetFolder, );
        }

        private void Install()
        {
            if (PerformPrecheck())
            {
                // Initialize and begin installation.
                var interop = new MBIInterop
                {
                    SetStatus = (status) => Dispatcher.Invoke(() => StatusText = status),
                    InstallationFinished = installFinished,
                    AddTarget = target => builtTarget = target
                };
                var controller = new MilanBaseInstaller();
                var sourceTarget = SelectedTarget;
                var destination = InstallLocation;
                builtTarget = null;
                InstallController = controller;
                SourceTargetSelector.IsEnabled = false;
                MigrateMe3ProfileCheckBox.IsEnabled = false;
                var generateSlashManifest = App.IsDebug && GenerateSlashManifest;
                Task.Run(() =>
                {
                    InstallController.CreateInstallation(SelectedTarget, InstallLocation, interop, generateSlashManifest);
                });
            }
        }

        private static void addTarget(MainWindow owner, GameTargetWPF target)
        {
            // Keep the installed target in the selector, replacing an earlier entry for the same installation.
            var existing = owner.InstallationTargets.FirstOrDefault(x => x.Game == target.Game &&
                string.Equals(x.TargetPath, target.TargetPath, StringComparison.OrdinalIgnoreCase));
            var existingIndex = existing == null ? -1 : owner.InstallationTargets.IndexOf(existing);
            if (existingIndex != -1)
            {
                owner.InstallationTargets.RemoveAt(existingIndex);
                owner.InstallationTargets.Insert(existingIndex, target);
            }
            else
            {
                owner.InstallationTargets.Add(target);
            }
            owner.SelectedGameTarget = target;
        }

        // This method should be async cause it's going to be long running for hash checks.
        private bool PerformPrecheck()
        {
            try
            {
                Directory.CreateDirectory(InstallLocation);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Unable to create installation directory:");
                return false;
            }

            if (!Directory.Exists(InstallLocation))
            {
                M3Log.Error($"Installation path '{InstallLocation}' does not exist.");
                return false;
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

        private void installFinished(bool success, string message)
        {
            InstallController = null;
            if (success)
            {
                M3Log.Information("Milan base installation completed successfully.");
                Dispatcher.Invoke(() =>
                {
                    var owner = mainwindow;
                    var me3Target = owner.GetME3TargetForProfileMigration();
                    var migrate = MigrateMe3Profile;
                    OnClosing(DataEventArgs.Empty);
                    if (builtTarget != null)
                        addTarget(owner, builtTarget);
                    if (migrate && me3Target != null)
                        _ = Me3ProfileMigration.StartAsync(owner, me3Target);
                    else if (migrate)
                        M3L.ShowDialog(owner, "Project Milan was installed, but Mod Manager could not find an original ME3 installation for profile migration.", "ME3 target not found", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            else
            {
                M3Log.Error($"Milan base installation failed: {message}");
                Dispatcher.Invoke(() =>
                {
                    SourceTargetSelector.IsEnabled = true;
                    MigrateMe3ProfileCheckBox.IsEnabled = true;
                    StatusText = "Installation failed.";
                    M3L.ShowDialog(window, $"The installation failed with the following error:\n\n{message}", "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private bool CanInstall()
        {
            // We check install path when you press install to improve performance
            return InstallController == null && SelectedTarget != null && !string.IsNullOrWhiteSpace(InstallLocation);
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

            MigrateMe3ProfileCheckBox.Visibility = !File.Exists(LMPDirectory.LocalProfilePath)
                ? Visibility.Visible : Visibility.Collapsed;

            LE3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == MEGame.LE3 && !x.TextureModded));
            SelectedTarget = LE3Targets.FirstOrDefault();

            if (SelectedTarget != null)
            {
                var rootPath = Directory.GetParent(SelectedTarget.TargetPath)?.FullName;
                if (rootPath != null && Path.GetFileName(rootPath) == "Game")
                {
                    InstallLocation = Path.Combine(rootPath, @"LMP");
                }
            }
        }
    }
}
