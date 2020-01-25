using System;
using System.Collections.Generic;
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
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupNagSystem.xaml
    /// </summary>
    public partial class BackupNagSystem : MMBusyPanelBase
    {
        public bool ME1BackedUp { get; } = Utilities.GetGameBackupPath(Mod.MEGame.ME1) != null;
        public bool ME2BackedUp { get; } = Utilities.GetGameBackupPath(Mod.MEGame.ME2) != null;
        public bool ME3BackedUp { get; } = Utilities.GetGameBackupPath(Mod.MEGame.ME3) != null;
        public bool ME1Installed { get; set; }
        public bool ME2Installed { get; set; }
        public bool ME3Installed { get; set; }
        public bool AnyGameMissingBackup  => (!ME1BackedUp && ME1Installed) || (!ME2BackedUp && ME2Installed) || (!ME3BackedUp && ME3Installed);

        public string Title
        {
            get
            {
                int numGamesNotBackedUp = 0;
                if (!ME1BackedUp && ME1Installed) numGamesNotBackedUp++;
                if (!ME2BackedUp && ME2Installed) numGamesNotBackedUp++;
                if (!ME3BackedUp && ME3Installed) numGamesNotBackedUp++;

                if (numGamesNotBackedUp > 1)
                {
                    return $"{numGamesNotBackedUp} games not backed up";
                }

                if (numGamesNotBackedUp > 0)
                {
                    return $"{numGamesNotBackedUp} game not backed up";
                }

                return "All games backed up";
            }
        }

        public BackupNagSystem(bool me1Installed, bool me2Installed, bool me3Installed)
        {
            DataContext = this;
            ME1Installed = me1Installed;
            ME2Installed = me2Installed;
            ME3Installed = me3Installed;
            LoadCommands();
            InitializeComponent();
        }

        public static bool ShouldShowNagScreen(List<GameTarget> targets)
        {
            if (targets.Any(x => x.Game == Mod.MEGame.ME1))
            {
                if (Utilities.GetGameBackupPath(Mod.MEGame.ME1) == null)
                {
                    return true;
                }
            }

            if (targets.Any(x => x.Game == Mod.MEGame.ME2))
            {
                if (Utilities.GetGameBackupPath(Mod.MEGame.ME2) == null)
                {
                    return true;
                }
            }

            if (targets.Any(x => x.Game == Mod.MEGame.ME3))
            {
                if (Utilities.GetGameBackupPath(Mod.MEGame.ME3) == null)
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
            OnClosing(new DataEventArgs(true));
        }

        private void ClosePanel()
        {
            OnClosing(new DataEventArgs(false));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

        }
    }
}
