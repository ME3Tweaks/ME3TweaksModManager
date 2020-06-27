using System;
using System.Collections.Generic;
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
using MassEffectModManagerCore.modmanager.localizations;
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
        public string ME1BackupStatus { get; set; }
        public string ME1BackupStatusTooltip { get; set; }
        public string ME2BackupStatus { get; set; }
        public string ME2BackupStatusTooltip { get; set; }
        public string ME3BackupStatus { get; set; }
        public string ME3BackupStatusTooltip { get; set; }
        public bool AnyGameMissingBackup => (!ME1BackedUp && ME1Installed) || (!ME2BackedUp && ME2Installed) || (!ME3BackedUp && ME3Installed);

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
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            SetupStatus(Mod.MEGame.ME1, ME1Installed, ME1BackedUp, (string msg) => ME1BackupStatus = msg, (string msg) => ME1BackupStatusTooltip = msg);
            SetupStatus(Mod.MEGame.ME2, ME2Installed, ME2BackedUp, (string msg) => ME2BackupStatus = msg, (string msg) => ME2BackupStatusTooltip = msg);
            SetupStatus(Mod.MEGame.ME3, ME3Installed, ME3BackedUp, (string msg) => ME3BackupStatus = msg, (string msg) => ME3BackupStatusTooltip = msg);
        }

        private void SetupStatus(Mod.MEGame game, bool installed, bool backedUp, Action<string> setStatus, Action<string> setStatusToolTip)
        {
            if (installed)
            {
                var bPath = Utilities.GetGameBackupPath(game, forceReturnPath: true);
                if (backedUp)
                {
                    setStatus("Backed up");
                    setStatusToolTip($"Backup stored at {bPath}");
                }
                else if (bPath == null)
                {

                    setStatus("Not backed up");
                    setStatusToolTip("Game has not been backed up");
                }
                else if (!Directory.Exists(bPath))
                {
                    setStatus("Backup unavailable");
                    setStatusToolTip($"Backup stored at {bPath}\nBackup path is not accessible, was it renamed or moved?");
                }
            }
            else
            {
                setStatus("Not installed");
                setStatusToolTip("Game does not appear to be installed, or is not yet a moddable target in Mod Manager.&#10;If game from Steam, ensure it has been run at least once");
            }
        }
    }
}
