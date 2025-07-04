using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.milan;
using ME3TweaksModManager.ui;
using System.Threading.Tasks;
using System.Windows.Input;

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

        /// <summary>
        /// The install controller for the running install.
        /// </summary>
        private MilanBaseInstaller InstallController { get; set; }

        #region Commands
        public GenericCommand InstallCommand { get; private set; }
        public GenericCommand CancelCommand { get; private set; }
        #endregion

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
                    AddTarget = addTarget
                };
                InstallController = new MilanBaseInstaller();
                Task.Run(() =>
                {
                    InstallController.CreateInstallation(SelectedTarget, InstallLocation, interop);
                });
            }
        }

        private void addTarget(GameTargetWPF target)
        {
            mainwindow.InstallationTargets.Add(target);
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

            var filesInDir = Directory.GetFiles(InstallLocation);
            if (filesInDir.Length > 0)
            {
                M3Log.Error($"Installation path '{InstallLocation}' is not empty. It contains {filesInDir.Length} files. This location cannot be used.");
                return false;
            }
            var foldersInDir = Directory.GetDirectories(InstallLocation);
            if (foldersInDir.Length > 0)
            {
                M3Log.Error($"Installation path '{InstallLocation}' is not empty. It contains {foldersInDir.Length} subfolders. This location cannot be used.");
                return false;
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
                OnClosing(DataEventArgs.Empty);
            }
            else
            {
                M3Log.Error($"Milan base installation failed: {message}");
                // We need to handle the error here.
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
