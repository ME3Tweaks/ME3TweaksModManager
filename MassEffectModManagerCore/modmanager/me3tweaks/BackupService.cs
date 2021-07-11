using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FontAwesome.WPF;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.Win32;
using Serilog;
using Application = System.Windows.Application;


namespace MassEffectModManagerCore.modmanager.me3tweaks
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
            get => GetGameBackupPath(MEGame.ME1, true) != null;
            private set => SetProperty(ref _me1BackedUp, value);
        }

        private static bool _me2BackedUp;
        public static bool ME2BackedUp
        {
            get => GetGameBackupPath(MEGame.ME2, true) != null;
            private set => SetProperty(ref _me2BackedUp, value);
        }

        private static bool _me3BackedUp;
        public static bool ME3BackedUp
        {
            get => GetGameBackupPath(MEGame.ME3, true) != null;
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

        private static bool _le1Installed;
        public static bool LE1Installed
        {
            get => _me1Installed;
            private set => SetProperty(ref _le1Installed, value);
        }

        private static bool _le2Installed;
        public static bool LE2Installed
        {
            get => _le2Installed;
            private set => SetProperty(ref _le2Installed, value);
        }

        private static bool _le3Installed;
        public static bool LE3Installed
        {
            get => _le3Installed;
            private set => SetProperty(ref _le3Installed, value);
        }

        private static bool _lelInstalled;
        public static bool LELInstalled
        {
            get => _lelInstalled;
            private set => SetProperty(ref _lelInstalled, value);
        }


        //Todo: Maybe cache this so we aren't doing so many reads. Not sure how often this gets hit since it is used in some commands

        private static bool _le1BackedUp;
        public static bool LE1BackedUp
        {
            get => GetGameBackupPath(MEGame.LE1, true) != null;
            private set => SetProperty(ref _le1BackedUp, value);
        }

        private static bool _le2BackedUp;
        public static bool LE2BackedUp
        {
            get => GetGameBackupPath(MEGame.LE2, true) != null;
            private set => SetProperty(ref _le2BackedUp, value);
        }

        private static bool _le3BackedUp;
        public static bool LE3BackedUp
        {
            get => GetGameBackupPath(MEGame.LE3, true) != null;
            private set => SetProperty(ref _le3BackedUp, value);
        }

        private static bool _lelBackedUp;
        public static bool LELBackedUp
        {
            get => GetGameBackupPath(MEGame.LELauncher, true) != null;
            private set => SetProperty(ref _lelBackedUp, value);
        }

        private static bool _le1BackupActivity;
        public static bool LE1BackupActivity
        {
            get => _le1BackupActivity;
            private set => SetProperty(ref _le1BackupActivity, value);
        }

        private static bool _le2BackupActivity;
        public static bool LE2BackupActivity
        {
            get => _le2BackupActivity;
            private set => SetProperty(ref _le2BackupActivity, value);
        }

        private static bool _le3BackupActivity;
        public static bool LE3BackupActivity
        {
            get => _le3BackupActivity;
            private set => SetProperty(ref _le3BackupActivity, value);
        }

        private static FontAwesomeIcon _le1ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon LE1ActivityIcon
        {
            get => _le1ActivityIcon;
            private set => SetProperty(ref _le1ActivityIcon, value);
        }

        private static FontAwesomeIcon _le2ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon LE2ActivityIcon
        {
            get => _le2ActivityIcon;
            private set => SetProperty(ref _le2ActivityIcon, value);
        }

        private static FontAwesomeIcon _le3ActivityIcon = FontAwesomeIcon.TimesCircle;
        public static FontAwesomeIcon LE3ActivityIcon
        {
            get => _le3ActivityIcon;
            private set => SetProperty(ref _le3ActivityIcon, value);
        }

        public static bool AnyGameMissingBackup => (!ME1BackedUp && ME1Installed) || (!ME2BackedUp && ME2Installed) || (!ME3BackedUp && ME3Installed)
                                                   || (LE1Installed && (LE1BackedUp || !LE2BackedUp || !LE3BackedUp));


        /// <summary>
        /// Refreshes the status strings of a backup. This method will run on the UI thread.
        /// </summary>
        /// <param name="window">Main window which houses the installation targets list. If htis is null, the game will behave as if it was installed.</param>
        /// <param name="game">Game to refresh. If not specified all strings will be updated</param>
        public static void RefreshBackupStatus(MainWindow window, MEGame game = MEGame.Unknown)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {

                if (game is MEGame.ME1 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.ME1, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.ME1),
                        ME1BackedUp, msg => ME1BackupStatus = msg, msg => ME1BackupStatusTooltip = msg);
                }

                if (game is MEGame.ME2 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.ME2, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.ME2),
                        ME2BackedUp, msg => ME2BackupStatus = msg, msg => ME2BackupStatusTooltip = msg);
                }
                if (game is MEGame.ME3 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.ME3, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.ME3), ME3BackedUp,
                        msg => ME3BackupStatus = msg, msg => ME3BackupStatusTooltip = msg);
                }
                if (game is MEGame.LE1 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.LE1, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.LE1), LE1BackedUp,
                        msg => LE1BackupStatus = msg, msg => LE1BackupStatusTooltip = msg);
                }
                if (game is MEGame.LE2 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.LE2, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.LE2), LE2BackedUp,
                        msg => LE2BackupStatus = msg, msg => LE2BackupStatusTooltip = msg);
                }
                if (game is MEGame.LE3 or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.LE3, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.LE3), LE3BackedUp,
                        msg => LE3BackupStatus = msg, msg => LE3BackupStatusTooltip = msg);
                }
                if (game is MEGame.LELauncher or MEGame.Unknown)
                {
                    RefreshBackupStatus(MEGame.LELauncher, window == null || window.InstallationTargets.Any(x => x.Game == MEGame.LELauncher), LELBackedUp,
                        msg => LELBackupStatus = msg, msg => LELBackupStatusTooltip = msg);
                }
            });
        }

        private static void RefreshBackupStatus(MEGame game, bool installed, bool backedUp, Action<string> setStatus, Action<string> setStatusToolTip)
        {
            //if (installed)
            //{
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
                else if (!installed)
                {
                    setStatus(M3L.GetString(M3L.string_notInstalled));
                    setStatusToolTip(M3L.GetString(M3L.string_gameNotInstalledHasItBeenRunOnce));
                }
                else
                {
                    // This is just generic error. It shouldn't occur
                    setStatus(M3L.GetString(M3L.string_notBackedUp));
                    setStatusToolTip(M3L.GetString(M3L.string_gameHasNotBeenBackedUp));
                }
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

        private static string _le1BackupStatus;
        public static string LE1BackupStatus
        {
            get => _le1BackupStatus;
            private set => SetProperty(ref _le1BackupStatus, value);
        }

        private static string _le2BackupStatus;
        public static string LE2BackupStatus
        {
            get => _le2BackupStatus;
            private set => SetProperty(ref _le2BackupStatus, value);
        }

        private static string _le3BackupStatus;
        public static string LE3BackupStatus
        {
            get => _le3BackupStatus;
            private set => SetProperty(ref _le3BackupStatus, value);
        }

        private static string _lelBackupStatus;
        public static string LELBackupStatus
        {
            get => _lelBackupStatus;
            private set => SetProperty(ref _lelBackupStatus, value);
        }

        private static string _le1BackupStatusTooltip;
        public static string LE1BackupStatusTooltip
        {
            get => _le1BackupStatusTooltip;
            private set => SetProperty(ref _le1BackupStatusTooltip, value);
        }

        private static string _le2BackupStatusTooltip;
        public static string LE2BackupStatusTooltip
        {
            get => _le2BackupStatusTooltip;
            private set => SetProperty(ref _le2BackupStatusTooltip, value);
        }

        private static string _le3BackupStatusTooltip;
        public static string LE3BackupStatusTooltip
        {
            get => _le3BackupStatusTooltip;
            private set => SetProperty(ref _le3BackupStatusTooltip, value);
        }

        private static string _lelBackupStatusTooltip;
        public static string LELBackupStatusTooltip
        {
            get => _lelBackupStatusTooltip;
            private set => SetProperty(ref _lelBackupStatusTooltip, value);
        }

        public const string CMM_VANILLA_FILENAME = @"cmm_vanilla";

        /// <summary>
        /// Fetches the backup status string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string GetBackupStatus(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1: return ME1BackupStatus;
                case MEGame.ME2: return ME2BackupStatus;
                case MEGame.ME3: return ME3BackupStatus;
                case MEGame.LE1: return LE1BackupStatus;
                case MEGame.LE2: return LE2BackupStatus;
                case MEGame.LE3: return LE3BackupStatus;
                case MEGame.LELauncher: return LELBackupStatus;
            }

            return null;
        }

        /// <summary>
        /// Fetches the backup status tooltip string for the specific game. The status must be refreshed before the values will be initially set
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static string GetBackupStatusTooltip(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1: return ME1BackupStatusTooltip;
                case MEGame.ME2: return ME2BackupStatusTooltip;
                case MEGame.ME3: return ME3BackupStatusTooltip;
                case MEGame.LE1: return LE1BackupStatusTooltip;
                case MEGame.LE2: return LE2BackupStatusTooltip;
                case MEGame.LE3: return LE3BackupStatusTooltip;
                case MEGame.LELauncher: return LELBackupStatusTooltip;
            }

            return null;
        }

        /// <summary>
        /// Sets the status of a backup.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="checkingBackup"></param>
        /// <param name="pleaseWait"></param>
        public static void SetStatus(MEGame game, string status, string tooltip)
        {
            switch (game)
            {
                case MEGame.ME1:
                    ME1BackupStatus = status;
                    ME1BackupStatusTooltip = tooltip;
                    break;
                case MEGame.ME2:
                    ME2BackupStatus = status;
                    ME2BackupStatusTooltip = tooltip;
                    break;
                case MEGame.ME3:
                    ME3BackupStatus = status;
                    ME3BackupStatusTooltip = tooltip;
                    break;
                case MEGame.LE1:
                    LE1BackupStatus = status;
                    LE1BackupStatusTooltip = tooltip;
                    break;
                case MEGame.LE2:
                    LE2BackupStatus = status;
                    LE2BackupStatusTooltip = tooltip;
                    break;
                case MEGame.LE3:
                    LE3BackupStatus = status;
                    LE3BackupStatusTooltip = tooltip;
                    break;
            }
        }

        public static void SetActivity(MEGame game, bool p1)
        {
            switch (game)
            {
                case MEGame.ME1:
                    ME1BackupActivity = p1;
                    break;
                case MEGame.ME2:
                    ME2BackupActivity = p1;
                    break;
                case MEGame.ME3:
                    ME3BackupActivity = p1;
                    break;
                case MEGame.LE1:
                    LE1BackupActivity = p1;
                    break;
                case MEGame.LE2:
                    LE2BackupActivity = p1;
                    break;
                case MEGame.LE3:
                    LE3BackupActivity = p1;
                    break;
            }

        }

        public static void SetIcon(MEGame game, FontAwesomeIcon p1)
        {
            switch (game)
            {
                case MEGame.ME1:
                    ME1ActivityIcon = p1;
                    break;
                case MEGame.ME2:
                    ME2ActivityIcon = p1;
                    break;
                case MEGame.ME3:
                    ME3ActivityIcon = p1;
                    break;
                case MEGame.LE1:
                    LE1ActivityIcon = p1;
                    break;
                case MEGame.LE2:
                    LE2ActivityIcon = p1;
                    break;
                case MEGame.LE3:
                    LE3ActivityIcon = p1;
                    break;
            }
        }

        private static Dictionary<MEGame, string> GameBackupPathCache = new Dictionary<MEGame, string>();
        private static Dictionary<MEGame, DateTime> GameBackupCheckTimes = new();

        public static string GetGameBackupPath(MEGame game, bool forceCmmVanilla = true, bool logReturnedPath = false, bool forceReturnPath = false, bool refresh = false)
        {
            var cachedTimeExists = GameBackupCheckTimes.TryGetValue(game, out var lastCheck);
            if (!refresh && !forceReturnPath && !logReturnedPath && cachedTimeExists && DateTime.Now < lastCheck.AddSeconds(10) && GameBackupPathCache.ContainsKey(game))
            {
                return GameBackupPathCache[game];
            }

            string path = Utilities.GetRegistrySettingString(App.REGISTRY_KEY_ME3TWEAKS, $@"{game}VanillaBackupLocation");

            if (forceReturnPath) return path; // do not check it

            if (logReturnedPath)
            {
                Log.Information($@" >> Backup path lookup in registry for {game} returned: {path}");
            }

            GameBackupCheckTimes[game] = DateTime.Now;
            if (path == null || !Directory.Exists(path))
            {
                if (logReturnedPath)
                {
                    Log.Information(@" >> Path is null or directory doesn't exist.");
                }

                GameBackupPathCache[game] = null;
                return null;
            }

            //Super basic validation
            if (game == MEGame.LELauncher)
            {
                if (!Directory.Exists(Path.Combine(path, @"Content")))
                {
                    if (logReturnedPath)
                    {
                        Log.Warning(@" >> " + path + @" is missing Content subdirectory, invalid backup");
                    }

                    GameBackupPathCache[game] = null;
                    return null;
                }
            }
            else if (!Directory.Exists(Path.Combine(path, @"BIOGame")) || !Directory.Exists(Path.Combine(path, @"Binaries")))
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is missing biogame/binaries subdirectory, invalid backup");
                }
                GameBackupPathCache[game] = null;
                return null;
            }

            if (forceCmmVanilla && !File.Exists(Path.Combine(path, @"cmm_vanilla")))
            {
                if (logReturnedPath)
                {
                    Log.Warning(@" >> " + path + @" is not marked as a vanilla backup. This backup will not be considered vanilla and thus will not be used by Mod Manager");
                }
                GameBackupPathCache[game] = null;
                return null; //do not accept alot installer backups that are missing cmm_vanilla as they are not vanilla.
            }

            if (logReturnedPath)
            {
                Log.Information(@" >> " + path + @" is considered a valid backup path");
            }
            GameBackupPathCache[game] = path;
            return path;
        }

        public static void ResetIcon(MEGame game)
        {
            SetIcon(game, FontAwesomeIcon.TimesCircle);
        }

        public static void SetBackedUp(MEGame game, bool b)
        {
            switch (game)
            {
                case MEGame.ME1:
                    ME1BackedUp = b;
                    break;
                case MEGame.ME2:
                    ME2BackedUp = b;
                    break;
                case MEGame.ME3:
                    ME3BackedUp = b;
                    break;
                case MEGame.LE1:
                    LE1BackedUp = b;
                    break;
                case MEGame.LE2:
                    LE2BackedUp = b;
                    break;
                case MEGame.LE3:
                    LE3BackedUp = b;
                    break;
            }
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
            StaticBackupStateChanged?.Invoke(null, null);
        }

        public static void SetInstallStatuses(ObservableCollectionExtended<GameTarget> installationTargets)
        {
            ME1Installed = installationTargets.Any(x => x.Game == MEGame.ME1);
            ME2Installed = installationTargets.Any(x => x.Game == MEGame.ME2);
            ME3Installed = installationTargets.Any(x => x.Game == MEGame.ME3);
            LE1Installed = installationTargets.Any(x => x.Game == MEGame.LE1);
            LE2Installed = installationTargets.Any(x => x.Game == MEGame.LE2);
            LE3Installed = installationTargets.Any(x => x.Game == MEGame.LE3);
            LELInstalled = installationTargets.Any(x => x.Game == MEGame.LELauncher);
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(AnyGameMissingBackup)));
            StaticBackupStateChanged?.Invoke(null, null);
        }

        /// <summary>
        /// Deletes the registry key used to store the backup location
        /// </summary>
        /// <param name="game"></param>
        public static void RemoveBackupPath(MEGame game)
        {
            RegistryHandler.DeleteRegistryKey(Registry.CurrentUser, @"Software\ME3Tweaks", game + @"VanillaBackupLocation");
        }


        private const string REGISTRY_KEY_ME3CMM = @"HKEY_CURRENT_USER\Software\Mass Effect 3 Mod Manager";

        private const string REGISTRY_KEY_ALOT = @"HKEY_CURRENT_USER\Software\ALOTAddon"; //Shared. Do not change

        /// <summary>
        /// Copies ME1/ME2/ME3 backup paths to the new location if they are not defined there.
        /// </summary>
        public static void MigrateBackupPaths()
        {
            if (GetGameBackupPath(MEGame.ME1) == null)
            {
                var storedPath = Utilities.GetRegistrySettingString(REGISTRY_KEY_ALOT, $@"ME1VanillaBackupLocation");
                if (storedPath != null)
                {
                    Log.Information(@"Migrating ALOT key backup location for ME1");
                    Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3TWEAKS, @"ME1VanillaBackupLocation", storedPath);
                }
            }
            if (GetGameBackupPath(MEGame.ME2) == null)
            {
                var storedPath = Utilities.GetRegistrySettingString(REGISTRY_KEY_ALOT, $@"ME2VanillaBackupLocation");
                if (storedPath != null)
                {
                    Log.Information(@"Migrating ALOT key backup location for ME2");
                    Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3TWEAKS, @"ME2VanillaBackupLocation", storedPath);
                }
            }
            if (GetGameBackupPath(MEGame.ME3) == null)
            {
                var storedPath = Utilities.GetRegistrySettingString(REGISTRY_KEY_ME3CMM, $@"VanillaCopyLocation");
                if (storedPath != null)
                {
                    Log.Information(@"Migrating ME3CMM key backup location for ME3");
                    Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3TWEAKS, @"ME3VanillaBackupLocation", storedPath);
                }
            }
        }
    }
}
