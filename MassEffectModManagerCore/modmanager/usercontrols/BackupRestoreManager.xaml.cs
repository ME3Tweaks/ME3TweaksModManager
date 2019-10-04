using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;


namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupRestoreManager.xaml
    /// </summary>
    public partial class BackupRestoreManager : UserControl, INotifyPropertyChanged
    {
        public string ALOTStatusString { get; set; }
        public GameTarget SelectedTarget { get; set; }
        public string ME3BackupStatus => BackupService.ME3BackedUp ? $"Backed up\n{Utilities.GetGameBackupPath(Mod.MEGame.ME3)}" : "Not backed up";
        public string ME2BackupStatus => BackupService.ME2BackedUp ? $"Backed up\n{Utilities.GetGameBackupPath(Mod.MEGame.ME2)}" : "Not backed up";
        public string ME1BackupStatus => BackupService.ME1BackedUp ? $"Backed up\n{Utilities.GetGameBackupPath(Mod.MEGame.ME1)}" : "Not backed up";
        public Visibility ME3ProgressbarVisibility { get; set; } = Visibility.Collapsed;
        public Visibility ME2ProgressbarVisibility { get; set; } = Visibility.Collapsed;
        public Visibility ME1ProgressbarVisibility { get; set; } = Visibility.Collapsed;
        public Visibility ME1BackupButtonVisibility { get; set; } = Visibility.Collapsed;
        public Visibility ME2BackupButtonVisibility { get; set; } = Visibility.Collapsed;
        public bool ME3BackupButtonVisible => !BackupService.ME3BackedUp && ME3InstallationTargets.Count > 0 && InstallationTargetsME3_ComboBox.SelectedItem != null;

        public bool AnyGameMissingBackup => !BackupService.ME1BackedUp || !BackupService.ME2BackedUp || !BackupService.ME3BackedUp;
        public ObservableCollectionExtended<GameTarget> ME3InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME2InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME1InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public BackupRestoreManager(List<GameTarget> targetsList, GameTarget selectedTarget)
        {
            DataContext = this;
            ME1InstallationTargets.AddRange(targetsList.Where(x=>x.Game == Mod.MEGame.ME1));
            ME2InstallationTargets.AddRange(targetsList.Where(x => x.Game == Mod.MEGame.ME2));
            ME3InstallationTargets.AddRange(targetsList.Where(x => x.Game == Mod.MEGame.ME3));
            LoadCommands();
            InitializeComponent();
            //InstallationTargets_ComboBox.SelectedItem = selectedTarget;
        }

        private void LoadCommands()
        {

        }

        public event EventHandler<DataEventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(new DataEventArgs());
        }

        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
