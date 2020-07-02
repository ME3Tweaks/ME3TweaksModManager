using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Octokit;
using Serilog;
using Application = System.Windows.Application;


namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Contains methods and bindable variables for accessing and displaying info about game backups 
    /// </summary>
    public static class BackupService
    {
        #region Static Property Changed

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public static event PropertyChangedEventHandler StaticBackupStateChanged;

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion

        private static bool _me1Installed;
        public static bool ME1Installed
        {
            get => _me1Installed;
            private set => SetProperty(ref _me1Installed, value);
        }

        private static bool _me2Installed;
        public static bool ME2Installed
        {
            get => _me2Installed;
            private set => SetProperty(ref _me2Installed, value);
        }

        private static bool _me3Installed;
        public static bool ME3Installed
        {
            get => _me3Installed;
            private set => SetProperty(ref _me3Installed, value);
        }


        //Todo: Maybe cache this so we aren't doing so many reads. Not sure how often this gets hit since it is used in some commands

        private static bool _me1BackedUp;
        public static bool ME1BackedUp
        {
            get => GetGameBackupPath(Mod.MEGame.ME1, true) != null;
            private set => SetProperty(ref _me1BackedUp, value);
        }

        private static bool _me2BackedUp;
        public static bool ME2BackedUp
        {
            get => GetGameBackupPath(Mod.MEGame.ME2, true) != null;
            private set => SetProperty(ref _me2BackedUp, value);
        }

        private static bool _me3BackedUp;
        public static bool ME3BackedUp
        {
            get => GetGameBackupPath(Mod.MEGame.ME3, true) != null;
            private set => SetProperty(ref _me3BackedUp, value);
        }

        private static bool _me1BackupActivity;
        public static bool ME1BackupActivity
        {
            get => _me1BackupActivity;
            private set => SetProperty(ref _me1BackupActivity, value);
        }

        private static bool _me2BackupActivity;
        public static bool ME2BackupActivity
        {
            get => _me2BackupActivity;
            private set => SetProperty(ref _me2BackupActivity, value);
        }

        private static bool _me3BackupActivity;
        public static bool ME3BackupActivity
        {
            get => _me3BackupActivity;
            private set => SetProperty(ref _me3BackupActivity, value);
        }

        private static FontAwesomeIcon _me1ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME1ActivityIcon
        {
            get => _me1ActivityIcon;
            private set => SetProperty(ref _me1ActivityIcon, value);
        }

        private static FontAwesomeIcon _me2ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME2ActivityIcon
        {
            get => _me2ActivityIcon;
            private set => SetProperty(ref _me2ActivityIcon, value);
        }

        private static FontAwesomeIcon _me3ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon ME3ActivityIcon
        {
            get => _me3ActivityIcon;
            private set => SetProperty(ref _me3ActivityIcon, value);
        }

        public static bool AnyGameMissingBackup => (!ME1BackedUp && ME1Installed) || (!ME2BackedUp && ME2Installed) || (!ME3BackedUp && ME3Installed);


        /// <summary>
        /// Refreshes the status strings of a backup. This method will run on the UI thread.
        /// </summary>
        /// <param name="window">Main window which houses the installation targets list. If htis is null, the game will behave as if it was installed.</param>
        /// <param name="game">Game to refresh. If not specified all strings will be updated</param>
        public static void RefreshBackupStatus(MainWindow window, Mod.MEGame game = Mod.MEGame.Unknown)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {

                if (game == Mod.MEGame.ME1 || game == Mod.MEGame.Unknown)
                {
                    RefreshBackupStatus(Mod.MEGame.ME1, window == null || window.InstallationTargets.Any(x => x.Game == Mod.MEGame.ME1),
                        ME1BackedUp, msg => ME1BackupStatus = msg, msg => ME1BackupStatusTooltip = msg);
                }

                if (game == Mod.MEGame.ME2 || game == Mod.MEGame.Unknown)
                {
                    RefreshBackupStatus(Mod.MEGame.ME2, window == null || window.InstallationTargets.Any(x => x.Game == Mod.MEGame.ME2),
                        ME2BackedUp, msg => ME2BackupStatus = msg, msg => ME2BackupStatusTooltip = msg);
                }
                if (game == Mod.MEGame.ME3 || game == Mod.MEGame.Unknown)
                {
                    RefreshBackupStatus(Mod.MEGame.ME3, window == null || window.InstallationTargets.Any(x => x.Game == Mod.MEGame.ME3), ME3BackedUp,
                        msg => ME3BackupStatus = msg, msg => ME3BackupStatusTooltip = msg);
                }
            });
        }

        private static void RefreshBackupStatus(Mod.MEGame game, bool installed, bool backedUp, Action<string> setStatus, Action<string> setStatusToolTip)
        {
            if (installed)
            {
                var bPath = GetGameBackupPath(game, forceReturnPath: true);
                if (backedUp)
                {
                    setStatus(M3L.GetString(M3L.string_backedUp));
                    setStatusToolTip(M3L.GetString(M3L.string_interp_backupStatusStoredAt, bPath));
                }
                else if (bPath == null)
                {

                    setStatus(M3L.GetString(M3L.string_notBackedUp));
                    setStatusToolTip(M3L.GetString(M3L.string_gameHasNotBeenBackedUp));
                }
                else if (!Directory.Exists(bPath))
                {
                    setStatus(M3L.GetString(M3L.string_backupUnavailable));
                    setStatusToolTip(M3L.GetString(M3L.string_interp_backupStatusNotAccessible, bPath));
                }
                else
                {
                    var nonVanillaPath = GetGameBackupPath(game, forceCmmVanilla: false);
                    if (nonVanillaPath != null)
                    {
                        setStatus(M3L.GetString(M3L.string_backupNotVanilla));
                        setStatusToolTip(M3L.GetString(M3L.string_interp_backupStatusNotVanilla, nonVanillaPath));
                    }
                }
            }
            else
            {
                setStatus(M3L.GetString(M3L.string_notInstalled));
                setStatusToolTip(M3L.GetString(M3L.string_gameNotInstalledHasItBeenRunOnce));
            }
        }

        private static string _me1BackupStatus;
        public static string ME1BackupStatus
        {
            get => _me1BackupStatus;
            private set => SetProperty(ref _me1BackupStatus, value);
        }

        private static string _me2BackupStatus;
        public static string ME2BackupStatus
        {
            get => _me2BackupStatus;
            private set => SetProperty(ref _me2BackupStatus, value);
        }

        private static string _me3BackupStatus;
        public static string ME3BackupStatus
        {
            get => _me3BackupStatus;
            private set => SetProperty(ref _me3BackupStatus, value);
        }

        private static string _me1BackupStatusTooltip;
        public static string ME1BackupStatusTooltip
        {
            get => _me1BackupStatusTooltip;
            private set => SetProperty(ref _me1BackupStatusTooltip, value);
        }

        private static string _me2BackupStatusTooltip;
        public static string ME2BackupStatusTooltip
        {
            get => _me2BackupStatusTooltip;
            private set => SetProperty(ref _me2BackupStatusTooltip, value);
        }

        private static string _me3BackupStatusTooltip;
        public static string ME3BackupStatusTooltip
        {
            get => _me3BackupStatusTooltip;
            private set => SetProperty(ref _me3BackupStatusTooltip, value);
        }

        /// <summary>
        /// Fetches the backup status string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string GetBackupStatus(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1: return ME1BackupStatus;
                case Mod.MEGame.ME2: return ME2BackupStatus;
                case Mod.MEGame.ME3: return ME3BackupStatus;
            }

            return null;
        }

        /// <summary>
        /// Fetches the backup status tooltip string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string GetBackupStatusTooltip(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1: return ME1BackupStatusTooltip;
                case Mod.MEGame.ME2: return ME2BackupStatusTooltip;
                case Mod.MEGame.ME3: return ME3BackupStatusTooltip;
            }

            return null;
        }

        /// <summary>
        /// Sets the status of a backup.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="checkingBackup"></param>
        /// <param name="pleaseWait"></param>
        public static void SetStatus(Mod.MEGame game, string status, string tooltip)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1BackupStatus = status;
                    ME1BackupStatusTooltip = tooltip;
                    break;
                case Mod.MEGame.ME2:
                    ME2BackupStatus = status;
                    ME2BackupStatusTooltip = tooltip;
                    break;
                case Mod.MEGame.ME3:
                    ME3BackupStatus = status;
                    ME3BackupStatusTooltip = tooltip;
                    break;
            }
        }

        public static void SetActivity(Mod.MEGame game, bool p1)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1BackupActivity = p1;
                    break;
                case Mod.MEGame.ME2:
                    ME2BackupActivity = p1;
                    break;
                case Mod.MEGame.ME3:
                    ME3BackupActivity = p1;
                    break;
            }

        }

        public static void SetIcon(Mod.MEGame game, FontAwesomeIcon p1)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1ActivityIcon = p1;
                    break;
                case Mod.MEGame.ME2:
                    ME2ActivityIcon = p1;
                    break;
                case Mod.MEGame.ME3:
                    ME3ActivityIcon = p1;
                    break;
            }
        }


        public static string GetGameBackupPath(Mod.MEGame game, bool forceCmmVanilla = true, bool logReturnedPath = false, bool forceReturnPath = false)
        {
            string path;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, @"ME1VanillaBackupLocation");
                    break;
                case Mod.MEGame.ME2:
                    path = Utilities.GetRegistrySettingString(App.BACKUP_REGISTRY_KEY, @"ME2VanillaBackupLocation");
                    break;
                case Mod.MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    path = Utilities.GetRegistrySettingString(App.REGISTRY_KEY_ME3CMM, @"VanillaCopyLocation");
                    break;
                default:
                    return null;
            }

            if (forceReturnPath) return path; // do not check it

            if (logReturnedPath)
            {
                Log.Information($@" >> Backup path lookup in registry for {game} returned: {path}");
            }

            if (path == null || !Directory.Exists(path))
            {
                if (logReturnedPath)
                {
                    Log.Information(@" >> Path is null or directory doesn't exist.");
                }

                return null;
            }

            //Super basic validation
            if (!Directory.Exists(Path.Combine(path, @"BIOGame")) || !Directory.Exists(Path.Combine(path, @"Binaries")))
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is missing biogame/binaries subdirectory, invalid backup");
                }

                return null;
            }

            if (forceCmmVanilla && !File.Exists(Path.Combine(path, @"cmm_vanilla")))
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is not marked as a vanilla backup. This backup will not be considered vanilla and thus will not be used by Mod Manager");
                }

                return null; //do not accept alot installer backups that are missing cmm_vanilla as they are not vanilla.
            }

            if (logReturnedPath)
            {
                Log.Information(@" >> " + path + @" is considered a valid backup path");
            }

            return path;
        }

        public static void ResetIcon(Mod.MEGame game)
        {
            SetIcon(game, FontAwesomeIcon.TimesCircle);
        }

        public static void SetBackedUp(Mod.MEGame game, bool b)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1BackedUp = b;
                    break;
                case Mod.MEGame.ME2:
                    ME2BackedUp = b;
                    break;
                case Mod.MEGame.ME3:
                    ME3BackedUp = b;
                    break;
            }
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
            StaticBackupStateChanged?.Invoke(null, null);
        }

        public static void SetInstallStatuses(ObservableCollectionExtended<GameTarget> installationTargets)
        {
            ME1Installed = installationTargets.Any(x => x.Game == Mod.MEGame.ME1);
            ME2Installed = installationTargets.Any(x => x.Game == Mod.MEGame.ME2);
            ME3Installed = installationTargets.Any(x => x.Game == Mod.MEGame.ME3);
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
            StaticBackupStateChanged?.Invoke(null, null);
        }
    }
}
