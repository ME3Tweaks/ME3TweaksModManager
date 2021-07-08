using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.me3tweaks;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupNagSystem.xaml
    /// </summary>
    public partial class BackupNagSystem : MMBusyPanelBase
    {
        //public bool ME1BackedUp { get; } = BackupService.GetGameBackupPath(MEGame.ME1) != null;
        //public bool ME2BackedUp { get; } = BackupService.GetGameBackupPath(MEGame.ME2) != null;
        //public bool ME3BackedUp { get; } = BackupService.GetGameBackupPath(MEGame.ME3) != null;
        public bool ME1Installed { get; set; }
        public bool ME2Installed { get; set; }
        public bool ME3Installed { get; set; }

        public string Title
        {
            get
            {
                int numGamesNotBackedUp = 0;
                if (!BackupService.ME1BackedUp && ME1Installed) numGamesNotBackedUp++;
                if (!BackupService.ME2BackedUp && ME2Installed) numGamesNotBackedUp++;
                if (!BackupService.ME3BackedUp && ME3Installed) numGamesNotBackedUp++;

                if (numGamesNotBackedUp > 1)
                {
                    return M3L.GetString(M3L.string_interp_XgamesNotBackedUp, numGamesNotBackedUp);
                }

                if (numGamesNotBackedUp > 0)
                {
                    return M3L.GetString(M3L.string_interp_XgameNotBackedUp, numGamesNotBackedUp);
                }

                return M3L.GetString(M3L.string_allGamesBackedUp);
            }
        }

        public BackupNagSystem(bool me1Installed, bool me2Installed, bool me3Installed)
        {
            BackupService.StaticBackupStateChanged += NotifyBackupStatusChanged;
            DataContext = this;
            ME1Installed = me1Installed;
            ME2Installed = me2Installed;
            ME3Installed = me3Installed;
            LoadCommands();
            InitializeComponent();
        }

        private void NotifyBackupStatusChanged(object sender, PropertyChangedEventArgs e)
        {
            TriggerPropertyChangedFor(nameof(Title));
        }

        public static bool ShouldShowNagScreen(List<GameTarget> targets)
        {
            if (targets.Any(x => x.Game == MEGame.ME1))
            {
                if (BackupService.GetGameBackupPath(MEGame.ME1) == null)
                {
                    return true;
                }
            }

            if (targets.Any(x => x.Game == MEGame.ME2))
            {
                if (BackupService.GetGameBackupPath(MEGame.ME2) == null)
                {
                    return true;
                }
            }

            if (targets.Any(x => x.Game == MEGame.ME3))
            {
                if (BackupService.GetGameBackupPath(MEGame.ME3) == null)
                {
                    return true;
                }
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
            BackupService.StaticBackupStateChanged -= NotifyBackupStatusChanged;
            OnClosing(new DataEventArgs(true));
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
            BackupService.RefreshBackupStatus(mainwindow);
        }
    }
}
