using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupRestoreManager.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class BackupCreator : MMBusyPanelBase
    {
        public bool AnyGameMissingBackup => BackupService.AnyGameMissingBackup(MEGameSelector.GetEnabledGames()); // We do not check the launcher.
        public ObservableCollectionExtended<GameBackupWrapper> GameBackups { get; } = new ObservableCollectionExtended<GameBackupWrapper>();

        private List<GameTargetWPF> targetsList;
        public BackupCreator(List<GameTargetWPF> targetsList)
        {
            this.targetsList = targetsList;
            LoadCommands();
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
        }

        private bool CanClose() => !GameBackups.Any(x => x.BackupHandler.BackupInProgress);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        protected override void OnClosing(DataEventArgs args)
        {
            BackupService.StaticBackupStateChanged -= BackupServiceStateChanged;
            window.SizeChanged -= OnBackupStatusChanged;
            base.OnClosing(args);
        }

        // This doesn't do anything anymore but is just left in case it ever needs reimplemented
        [SuppressPropertyChangedWarnings]
        private void OnBackupStatusChanged()
        {

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            // Todo: Subscribe to BackupService events so we know when BackedUp changes
            BackupService.StaticBackupStateChanged += BackupServiceStateChanged;

            //window.SizeChanged += OnBackupStatusChanged;
            if (Settings.GenerationSettingLE)
            {
                GameBackups.Add(new GameBackupWrapper(MEGame.LELauncher, targetsList.Where(x => x.Game == MEGame.LELauncher), mainwindow));
                GameBackups.Add(new GameBackupWrapper(MEGame.LE1, targetsList.Where(x => x.Game == MEGame.LE1), mainwindow));
                GameBackups.Add(new GameBackupWrapper(MEGame.LE2, targetsList.Where(x => x.Game == MEGame.LE2), mainwindow));
                GameBackups.Add(new GameBackupWrapper(MEGame.LE3, targetsList.Where(x => x.Game == MEGame.LE3), mainwindow));
            }

            if (Settings.GenerationSettingOT)
            {
                GameBackups.Add(new GameBackupWrapper(MEGame.ME1, targetsList.Where(x => x.Game == MEGame.ME1), mainwindow));
                GameBackups.Add(new GameBackupWrapper(MEGame.ME2, targetsList.Where(x => x.Game == MEGame.ME2), mainwindow));
                GameBackups.Add(new GameBackupWrapper(MEGame.ME3, targetsList.Where(x => x.Game == MEGame.ME3), mainwindow));
            }
            OnBackupStatusChanged();
        }

        private void BackupServiceStateChanged(object sender, EventArgs eventArgs)
        {
            OnBackupStatusChanged();
        }

        [SuppressPropertyChangedWarnings]
        private void OnBackupStatusChanged(object sender, SizeChangedEventArgs e)
        {
            OnBackupStatusChanged();
        }

        // ISizeAdjustable Interface
        public override double MaxWindowHeightPercent { get; set; } = 0.85;
        public override double MaxWindowWidthPercent { get; set; } = 0.85;
    }
}
