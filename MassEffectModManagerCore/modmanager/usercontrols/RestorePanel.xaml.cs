using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.Restore;
using ME3TweaksCoreWPF;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for RestorePanel.xaml
    /// </summary>
    public partial class RestorePanel : MMBusyPanelBase
    {
        public bool AnyGameMissingBackup => BackupService.AnyGameMissingBackup(MEGameSelector.GetEnabledGames());
        public ObservableCollectionExtended<GameRestore> GameRestoreControllers { get; } = new();
        private List<GameTargetWPF> targetsList;

        public RestorePanel(List<GameTargetWPF> targetsList, GameTargetWPF selectedTarget)
        {
            this.targetsList = targetsList;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }


        private void ClosePanel()
        {
            Result.ReloadTargets = GameRestoreControllers.Any(x => x.RefreshTargets);
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !GameRestoreControllers.Any(x => x.RestoreInProgress);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                ClosePanel();
            }
        }

        public override void OnPanelVisible()
        {
            if (Settings.GenerationSettingLE)
            {
                GameRestoreControllers.Add(new GameRestore(MEGame.LELauncher));
                GameRestoreControllers.Add(new GameRestore(MEGame.LE1));
                GameRestoreControllers.Add(new GameRestore(MEGame.LE2));
                GameRestoreControllers.Add(new GameRestore(MEGame.LE3));
            }

            if (Settings.GenerationSettingOT)
            {
                GameRestoreControllers.Add(new GameRestore(MEGame.ME1));
                GameRestoreControllers.Add(new GameRestore(MEGame.ME2));
                GameRestoreControllers.Add(new GameRestore(MEGame.ME3));
            }
        }
    }
}
