using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for RestorePanel.xaml
    /// </summary>
    public partial class RestorePanel : MMBusyPanelBase
    {
        public bool AnyGameMissingBackup => BackupService.AnyGameMissingBackup(MEGameSelector.GetEnabledGames());
        public ObservableCollectionExtended<GameRestoreWrapper> GameRestoreControllers { get; } = new();
        private List<GameTargetWPF> targetsList;

        public RestorePanel(List<GameTargetWPF> targetsList, GameTargetWPF selectedTarget)
        {
            this.targetsList = targetsList;
            LoadCommands();
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }


        private void ClosePanel()
        {
            // Restore of game should have reloaded target on completion.
            //Result.ReloadTargets = GameRestoreControllers.Any(x => x.RefreshTargets);
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !GameRestoreControllers.Any(x => x.RestoreController.RestoreInProgress);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                ClosePanel();
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            if (Settings.GenerationSettingLE)
            {
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.LELauncher, targetsList.Where(x=>x.Game == MEGame.LELauncher), mainwindow));
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.LE1, targetsList.Where(x => x.Game == MEGame.LE1), mainwindow));
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.LE2, targetsList.Where(x => x.Game == MEGame.LE2), mainwindow));
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.LE3, targetsList.Where(x => x.Game == MEGame.LE3), mainwindow));
            }

            if (Settings.GenerationSettingOT)
            {
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.ME1, targetsList.Where(x => x.Game == MEGame.ME1), mainwindow));
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.ME2, targetsList.Where(x => x.Game == MEGame.ME2), mainwindow));
                GameRestoreControllers.Add(new GameRestoreWrapper(MEGame.ME3, targetsList.Where(x => x.Game == MEGame.ME3), mainwindow));
            }
        }
    }
}
