using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupNagSystem.xaml
    /// </summary>
    public partial class BackupNagSystem : MMBusyPanelBase, ISizeAdjustable
    {
        private bool ME1Installed { get; set; }
        private bool ME2Installed { get; set; }
        private bool ME3Installed { get; set; }
        private bool LE1Installed { get; set; }
        private bool LE2Installed { get; set; }
        private bool LE3Installed { get; set; }

        /// <summary>
        /// Row 1 items
        /// </summary>
        public ObservableCollectionExtended<GameBackupStatus> BackupStatusesOT { get; } = new();
        /// <summary>
        /// Row 2 items
        /// </summary>
        public ObservableCollectionExtended<GameBackupStatus> BackupStatusesLE { get; } = new();

        public string Title
        {
            get
            {
                int numGamesNotBackedUp = 0;
                if (Settings.GenerationSettingOT)
                {
                    if (ME1Installed && !BackupService.GetBackupStatus(MEGame.ME1).BackedUp) numGamesNotBackedUp++;
                    if (ME2Installed && !BackupService.GetBackupStatus(MEGame.ME2).BackedUp) numGamesNotBackedUp++;
                    if (ME3Installed && !BackupService.GetBackupStatus(MEGame.ME3).BackedUp) numGamesNotBackedUp++;
                }

                if (Settings.GenerationSettingLE)
                {
                    if (LE1Installed && !BackupService.GetBackupStatus(MEGame.LE1).BackedUp) numGamesNotBackedUp++;
                    if (LE2Installed && !BackupService.GetBackupStatus(MEGame.LE2).BackedUp) numGamesNotBackedUp++;
                    if (LE3Installed && !BackupService.GetBackupStatus(MEGame.LE3).BackedUp) numGamesNotBackedUp++;
                }

                if (numGamesNotBackedUp > 0)
                {
                    return M3L.GetString(M3L.string_interp_XgamesNotBackedUp, numGamesNotBackedUp);
                }

                return M3L.GetString(M3L.string_allGamesBackedUp);
            }
        }

        public ISizeAdjustable Self { get; init; }

        public BackupNagSystem(List<GameTargetWPF> availableTargets)
        {
            Self = this;
            BackupService.StaticBackupStateChanged += NotifyBackupStatusChanged;
            ME1Installed = availableTargets.Any(x => x.Game == MEGame.ME1);
            ME2Installed = availableTargets.Any(x => x.Game == MEGame.ME2);
            ME3Installed = availableTargets.Any(x => x.Game == MEGame.ME3);
            LE1Installed = availableTargets.Any(x => x.Game == MEGame.LE1);
            LE2Installed = availableTargets.Any(x => x.Game == MEGame.LE2);
            LE3Installed = availableTargets.Any(x => x.Game == MEGame.LE3);

            BackupStatusesOT.ReplaceAll(BackupService.GameBackupStatuses.Where(x => x.Game.IsOTGame() && MEGameSelector.IsGenerationEnabledGame(x.Game)));
            BackupStatusesLE.ReplaceAll(BackupService.GameBackupStatuses.Where(x => x.Game.IsLEGame() && MEGameSelector.IsGenerationEnabledGame(x.Game)));

            LoadCommands();
        }

        private void NotifyBackupStatusChanged(object sender, PropertyChangedEventArgs e)
        {
            TriggerPropertyChangedFor(nameof(Title));
        }

        public static bool ShouldShowNagScreen(List<GameTargetWPF> targets)
        {
            if (Settings.GenerationSettingOT)
            {
                if (targets.Any(x => x.Game == MEGame.ME1) && BackupService.GetGameBackupPath(MEGame.ME1) == null)
                    return true;
                if (targets.Any(x => x.Game == MEGame.ME2) && BackupService.GetGameBackupPath(MEGame.ME2) == null)
                    return true;
                if (targets.Any(x => x.Game == MEGame.ME3) && BackupService.GetGameBackupPath(MEGame.ME3) == null)
                    return true;
            }

            if (Settings.GenerationSettingLE)
            {
                if (targets.Any(x => x.Game == MEGame.LE1) && BackupService.GetGameBackupPath(MEGame.LE1) == null)
                    return true;
                if (targets.Any(x => x.Game == MEGame.LE2) && BackupService.GetGameBackupPath(MEGame.LE2) == null)
                    return true;
                if (targets.Any(x => x.Game == MEGame.LE3) && BackupService.GetGameBackupPath(MEGame.LE3) == null)
                    return true;

                // We don't really care about the launcher.
            }

            return false;
        }

        private void LoadCommands()
        {
            OpenBackupPanelCommand = new GenericCommand(OpenBackupPanel);
            CloseCommand = new GenericCommand(ClosePanel);
        }

        public ICommand CloseCommand { get; set; }
        public ICommand OpenBackupPanelCommand { get; set; }

        private void OpenBackupPanel()
        {
            Result.PanelToOpen = EPanelID.BACKUP_CREATOR;
            ClosePanel();
        }

        private void ClosePanel()
        {
            BackupService.StaticBackupStateChanged -= NotifyBackupStatusChanged;
            OnClosing(new DataEventArgs(false));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                BackupService.StaticBackupStateChanged -= NotifyBackupStatusChanged;
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            BackupService.RefreshBackupStatus();
        }

        public double Adjustment { get; set; }
        public double FullSize => mainwindow?.RootDisplayObject.ActualHeight ?? 0;
    }
}
