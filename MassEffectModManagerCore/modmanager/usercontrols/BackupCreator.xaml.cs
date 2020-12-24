using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MassEffectModManagerCore.modmanager.helpers;
using System.Windows.Input;
using Serilog;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using MassEffectModManagerCore.modmanager.windows;
using Microsoft.AppCenter.Analytics;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupRestoreManager.xaml
    /// </summary>
    public partial class BackupCreator : MMBusyPanelBase
    {
        public bool AnyGameMissingBackup => !BackupService.ME1BackedUp || !BackupService.ME2BackedUp || !BackupService.ME3BackedUp;
        public ObservableCollectionExtended<GameBackup> GameBackups { get; } = new ObservableCollectionExtended<GameBackup>();

        //public GameBackup ME3Backup { get; set; }
        //public GameBackup ME2Backup { get; set; }
        //public GameBackup ME1Backup { get; set; }
        private List<GameTarget> targetsList;
        public BackupCreator(List<GameTarget> targetsList, GameTarget selectedTarget, Window window)
        {
            DataContext = this;
            this.targetsList = targetsList;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), CanClose);
        }

        private bool CanClose() => !GameBackups.Any(x => x.BackupInProgress);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            GameBackups.Add(new GameBackup(MEGame.ME1, targetsList.Where(x => x.Game == MEGame.ME1), mainwindow));
            GameBackups.Add(new GameBackup(MEGame.ME2, targetsList.Where(x => x.Game == MEGame.ME2), mainwindow));
            GameBackups.Add(new GameBackup(MEGame.ME3, targetsList.Where(x => x.Game == MEGame.ME3), mainwindow));
        }

        public class GameBackup : INotifyPropertyChanged
        {
            private MEGame Game;
            public ObservableCollectionExtended<GameTarget> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTarget>();
            private MainWindow window;

            public GameBackup(MEGame game, IEnumerable<GameTarget> availableBackupSources, MainWindow window)
            {
                this.window = window;
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
                this.AvailableBackupSources.Add(new GameTarget(Game, M3L.GetString(M3L.string_linkBackupToAnExistingGameCopy), false, true));
                LoadCommands();
                switch (Game)
                {
                    case MEGame.ME1:
                        GameTitle = @"Mass Effect";
                        GameIconSource = @"/images/gameicons/ME1_48.ico";
                        break;
                    case MEGame.ME2:
                        GameTitle = @"Mass Effect 2";
                        GameIconSource = @"/images/gameicons/ME2_48.ico";
                        break;
                    case MEGame.ME3:
                        GameTitle = @"Mass Effect 3";
                        GameIconSource = @"/images/gameicons/ME3_48.ico";
                        break;
                }

                ResetBackupStatus();
            }

            private void LoadCommands()
            {
                BackupButtonCommand = new GenericCommand(BeginBackup, CanBeginBackup);
                UnlinkBackupCommand = new GenericCommand(UnlinkBackup, CanUnlinkBackup);
            }

            private void UnlinkBackup()
            {
                var gbPath = BackupService.GetGameBackupPath(Game, false, false, forceReturnPath: true);
                Log.Information($@"User is attempting to unlink backup for {Game}");
                var message = M3L.GetString(M3L.string_dialog_unlinkingBackup, Utilities.GetGameName(Game), gbPath, Utilities.GetGameName(Game));
                var shouldUnlink = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_unlinkingBackup), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                if (shouldUnlink)
                {
                    // Unlink
                    Log.Information($@"Unlinking backup for {Game}");
                    if (gbPath != null)
                    {
                        var cmmVanilla = Path.Combine(gbPath, @"cmm_vanilla");
                        if (File.Exists(cmmVanilla))
                        {
                            Log.Information(@"Deleted cmm_vanilla file: " + cmmVanilla);
                            File.Delete(cmmVanilla);
                        }
                    }
                    switch (Game)
                    {
                        case MEGame.ME1:
                        case MEGame.ME2:
                            RegistryHandler.DeleteRegistryKey(Registry.CurrentUser, @"Software\ALOTAddon",
                                Game + @"VanillaBackupLocation");
                            break;
                        case MEGame.ME3:
                            RegistryHandler.DeleteRegistryKey(Registry.CurrentUser, @"Software\Mass Effect 3 Mod Manager",
                                @"VanillaCopyLocation");
                            break;
                    }
                    BackupService.RefreshBackupStatus(window, Game);
                }
            }

            private bool CanUnlinkBackup()
            {
                return BackupService.GetGameBackupPath(Game, false, false, true) != null;
            }

            private bool CanBeginBackup()
            {
                return BackupSourceTarget != null && !BackupInProgress;
            }

            private void BeginBackup()
            {
                var targetToBackup = BackupSourceTarget;
                if (!targetToBackup.IsCustomOption)
                {
                    if (Utilities.IsGameRunning(targetToBackup.Game))
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotBackupGameWhileRunning, Utilities.GetGameName(BackupSourceTarget.Game)), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Point to existing game installation
                    Log.Information(@"BeginBackup() with IsCustomOption.");
                    var linkWarning = M3L.ShowDialog(window,
                        M3L.GetString(M3L.string_dialog_linkTargetWontBeModdable), M3L.GetString(M3L.string_linkWarning), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (linkWarning == MessageBoxResult.Cancel)
                    {
                        Log.Information(@"User aborted linking due to dialog");
                        return;
                    }

                    Log.Information(@"Prompting user to select executable of link target");
                    var gameexe = Utilities.PromptForGameExecutable(new[] { Game });
                    if (gameexe == null) { return; }
                    targetToBackup = new GameTarget(Game, Utilities.GetGamePathFromExe(Game, gameexe), false, true);
                    if (AvailableBackupSources.Any(x => x.TargetPath.Equals(targetToBackup.TargetPath, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Can't point to an existing modding target
                        Log.Error(@"This target is not valid to point to as a backup: It is listed a modding target already, it must be removed as a target first");
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedAlreadyATarget), M3L.GetString(M3L.string_cannotLinkGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var validationFailureReason = targetToBackup.ValidateTarget(ignoreCmmVanilla: true);
                    if (!targetToBackup.IsValid)
                    {
                        Log.Error(@"This installation is not valid to point to as a backup: " + validationFailureReason);
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedInvalidTarget, validationFailureReason), M3L.GetString(M3L.string_invalidGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                NamedBackgroundWorker nbw = new NamedBackgroundWorker(Game + @"Backup");
                nbw.WorkerReportsProgress = true;
                nbw.ProgressChanged += (a, b) =>
                {
                    if (b.UserState is double d)
                    {
                        TaskbarHelper.SetProgress(d);
                    }
                    else if (b.UserState is TaskbarProgressBarState tbs)
                    {
                        TaskbarHelper.SetProgressState(tbs);
                    }
                };
                nbw.DoWork += (a, b) =>
                {
                    Log.Information(@"Starting the backup thread. Checking path: " + targetToBackup.TargetPath);
                    BackupInProgress = true;
                    bool end = false;

                    List<string> nonVanillaFiles = new List<string>();

                    void nonVanillaFileFoundCallback(string filepath)
                    {
                        Log.Error($@"Non-vanilla file found: {filepath}");
                        nonVanillaFiles.Add(filepath);
                    }

                    List<string> inconsistentDLC = new List<string>();

                    void inconsistentDLCFoundCallback(string filepath)
                    {
                        if (targetToBackup.Supported)
                        {
                            Log.Error($@"DLC is in an inconsistent state: {filepath}");
                            inconsistentDLC.Add(filepath);
                        }
                        else
                        {
                            Log.Error(@"Detected an inconsistent DLC, likely due to an unofficial copy of the game");
                        }
                    }

                    ProgressVisible = true;
                    ProgressIndeterminate = true;
                    BackupStatus = M3L.GetString(M3L.string_validatingBackupSource);
                    Log.Information(@"Checking target is vanilla");
                    bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(targetToBackup, nonVanillaFileFoundCallback);

                    Log.Information(@"Checking DLC consistency");
                    bool isDLCConsistent = VanillaDatabaseService.ValidateTargetDLCConsistency(targetToBackup, inconsistentDLCCallback: inconsistentDLCFoundCallback);

                    Log.Information(@"Checking only vanilla DLC is installed");
                    List<string> dlcModsInstalled = VanillaDatabaseService.GetInstalledDLCMods(targetToBackup).Select(x =>
                    {
                        var tpmi = ThirdPartyServices.GetThirdPartyModInfo(x, targetToBackup.Game);
                        if (tpmi != null) return $@"{x} ({tpmi.modname})";
                        return x;
                    }).ToList();
                    var installedDLC = VanillaDatabaseService.GetInstalledOfficialDLC(targetToBackup);
                    var allOfficialDLC = MEDirectories.OfficialDLC(targetToBackup.Game);

                    if (installedDLC.Count() < allOfficialDLC.Count())
                    {
                        var dlcList = string.Join("\n - ", allOfficialDLC.Except(installedDLC).Select(x => $@"{MEDirectories.OfficialDLCNames(targetToBackup.Game)[x]} ({x})")); //do not localize
                        dlcList = @" - " + dlcList;
                        Log.Information(@"The following dlc will be missing in the backup if user continues: ");
                        Log.Information(dlcList);

                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var cancelDueToNotAllDLC = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_notAllDLCInstalled, dlcList), M3L.GetString(M3L.string_someDlcNotInstalled), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (cancelDueToNotAllDLC == MessageBoxResult.No)
                            {
                                end = true;
                                EndBackup();
                                return;
                            }
                        });
                    }

                    Log.Information(@"Checking for TexturesMEM TFCs");
                    var memTextures = Directory.GetFiles(targetToBackup.TargetPath, @"TexturesMEM*.tfc", SearchOption.AllDirectories);

                    if (end) return;
                    if (isVanilla && isDLCConsistent && !Enumerable.Any(dlcModsInstalled) && !Enumerable.Any(memTextures))
                    {
                        BackupStatus = M3L.GetString(M3L.string_waitingForUserInput);

                        string backupPath = null;
                        if (!targetToBackup.IsCustomOption)
                        {
                            // Creating a new backup
                            nbw.ReportProgress(0, TaskDialogProgressBarState.Paused);

                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                Log.Information(@"Prompting user to select backup destination");

                                CommonOpenFileDialog m = new CommonOpenFileDialog
                                {
                                    IsFolderPicker = true,
                                    EnsurePathExists = true,
                                    Title = M3L.GetString(M3L.string_selectBackupDestination)
                                };
                                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                                {
                                    backupPath = m.FileName;
                                    Log.Information(@"Backup path chosen: " + backupPath);

                                    bool okToBackup = validateBackupPath(backupPath, targetToBackup);
                                    if (!okToBackup)
                                    {
                                        end = true;
                                        EndBackup();
                                        return;
                                    }
                                }
                                else
                                {
                                    end = true;
                                    EndBackup();
                                    return;
                                }
                            });
                            if (end)
                            {
                                return;
                            }
                            nbw.ReportProgress(0, TaskbarProgressBarState.Indeterminate);

                        }
                        else
                        {

                            Log.Information(@"Linking existing backup at " + targetToBackup.TargetPath);
                            backupPath = targetToBackup.TargetPath;
                            // Linking existing backup
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                bool okToBackup = validateBackupPath(targetToBackup.TargetPath, targetToBackup);
                                if (!okToBackup)
                                {
                                    end = true;
                                    EndBackup();
                                    return;
                                }
                            });
                        }

                        if (end) return;

                        if (!targetToBackup.IsCustomOption)
                        {
                            #region callbacks and copy code

                            // Copy to new backup
                            void fileCopiedCallback()
                            {
                                ProgressValue++;
                                if (ProgressMax > 0)
                                {
                                    nbw.ReportProgress(0, ProgressValue * 1.0 / ProgressMax);
                                }
                            }

                            string dlcFolderpath = M3Directories.GetDLCPath(targetToBackup) + '\\';
                            int dlcSubStringLen = dlcFolderpath.Length;

                            bool aboutToCopyCallback(string file)
                            {
                                try
                                {
                                    if (file.Contains(@"\cmmbackup\")) return false; //do not copy cmmbackup files
                                    if (file.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        //It's a DLC!
                                        string dlcname = file.Substring(dlcSubStringLen);
                                        var dlcFolderNameEndPos = dlcname.IndexOf('\\');
                                        if (dlcFolderNameEndPos > 0)
                                        {
                                            dlcname = dlcname.Substring(0, dlcFolderNameEndPos);
                                            if (MEDirectories.OfficialDLCNames(targetToBackup.Game)
                                                .TryGetValue(dlcname, out var hrName))
                                            {
                                                BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                                    hrName);
                                            }
                                            else
                                            {
                                                BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                                    dlcname);
                                            }
                                        }
                                        else
                                        {
                                            // Loose files in the DLC folder
                                            BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                            M3L.GetString(M3L.string_basegame));
                                        }
                                    }
                                    else
                                    {
                                        //It's basegame
                                        if (file.EndsWith(@".bik"))
                                        {
                                            BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                                M3L.GetString(M3L.string_movies));
                                        }
                                        else if (new FileInfo(file).Length > 52428800)
                                        {
                                            BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                                Path.GetFileName(file));
                                        }
                                        else
                                        {
                                            BackupStatusLine2 = M3L.GetString(M3L.string_interp_backingUpX,
                                                M3L.GetString(M3L.string_basegame));
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Crashes.TrackError(e, new Dictionary<string, string>()
                                    {
                                        {@"dlcFolderpath", dlcFolderpath},
                                        {@"dlcSubStringLen", dlcSubStringLen.ToString()},
                                        {@"file", file}
                                    });
                                }

                                return true;
                            }

                            void totalFilesToCopyCallback(int total)
                            {
                                ProgressValue = 0;
                                ProgressIndeterminate = false;
                                ProgressMax = total;
                                nbw.ReportProgress(0, TaskbarProgressBarState.Normal);
                            }

                            BackupStatus = M3L.GetString(M3L.string_creatingBackup);
                            Log.Information($@"Backing up {targetToBackup.TargetPath} to {backupPath}");
                            nbw.ReportProgress(0, TaskbarProgressBarState.Normal);
                            CopyDir.CopyAll_ProgressBar(new DirectoryInfo(targetToBackup.TargetPath),
                                new DirectoryInfo(backupPath),
                                totalItemsToCopyCallback: totalFilesToCopyCallback,
                                aboutToCopyCallback: aboutToCopyCallback,
                                fileCopiedCallback: fileCopiedCallback,
                                ignoredExtensions: new[] { @"*.pdf", @"*.mp3" });
                            #endregion
                        }

                        // Write key
                        switch (Game)
                        {
                            case MEGame.ME1:
                            case MEGame.ME2:
                                Utilities.WriteRegistryKey(App.BACKUP_REGISTRY_KEY, Game + @"VanillaBackupLocation",
                                    backupPath);
                                break;
                            case MEGame.ME3:
                                Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3CMM, @"VanillaCopyLocation",
                                    backupPath);
                                break;
                        }

                        var cmmvanilla = Path.Combine(backupPath, @"cmm_vanilla");
                        if (!File.Exists(cmmvanilla))
                        {
                            Log.Information($@"Writing cmm_vanilla to " + cmmvanilla);
                            File.Create(cmmvanilla).Close();
                        }

                        Log.Information($@"Backup completed.");

                        Analytics.TrackEvent(@"Created a backup", new Dictionary<string, string>()
                            {
                                {@"Game", Game.ToString()},
                                {@"Result", @"Success"},
                                {@"Type", targetToBackup.IsCustomOption ? @"Linked" : @"Copy"}
                            });

                        EndBackup();
                        return;
                    }


                    if (!isVanilla)
                    {
                        //Show UI for non vanilla
                        Analytics.TrackEvent(@"Created a backup", new Dictionary<string, string>()
                            {
                                {@"Game", Game.ToString()},
                                {@"Result", @"Failure, Game modified"}
                            });
                        b.Result = (nonVanillaFiles, M3L.GetString(M3L.string_cannotBackupModifiedGame),
                            M3L.GetString(M3L.string_followingFilesDoNotMatchTheVanillaDatabase));
                    }
                    else if (!isDLCConsistent)
                    {
                        Analytics.TrackEvent(@"Created a backup", new Dictionary<string, string>()
                            {
                                {@"Game", Game.ToString()},
                                {@"Result", @"Failure, DLC inconsistent"}
                            });
                        if (targetToBackup.Supported)
                        {
                            b.Result = (inconsistentDLC, M3L.GetString(M3L.string_inconsistentDLCDetected),
                                M3L.GetString(M3L.string_dialogTheFollowingDLCAreInAnInconsistentState));
                        }
                        else
                        {
                            b.Result = (M3L.GetString(M3L.string_inconsistentDLCDetected),
                                M3L.GetString(M3L.string_inconsistentDLCDetectedUnofficialGame));
                        }
                    }
                    else if (Enumerable.Any(dlcModsInstalled))
                    {
                        Analytics.TrackEvent(@"Created a backup", new Dictionary<string, string>()
                            {
                                {@"Game", Game.ToString()},
                                {@"Result", @"Failure, DLC mods found"}
                            });
                        b.Result = (dlcModsInstalled, M3L.GetString(M3L.string_dlcModsAreInstalled),
                            M3L.GetString(M3L.string_dialogDLCModsWereDetectedCannotBackup));
                    }
                    else if (Enumerable.Any(memTextures))
                    {
                        Analytics.TrackEvent(@"Created a backup", new Dictionary<string, string>()
                        {
                            {@"Game", Game.ToString()},
                            {@"Result", @"Failure, TexturesMEM files found"}
                        });
                        b.Result = (M3L.GetString(M3L.string_leftoverTextureFilesFound),
                            M3L.GetString(M3L.string_dialog_foundLeftoverTextureFiles));
                    }
                    EndBackup();
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Error != null)
                    {
                        Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                    }
                    TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                    if (b.Result is (List<string> listItems, string title, string text))
                    {
                        ListDialog ld = new ListDialog(listItems, title, text, window);
                        ld.Show();
                    }
                    else if (b.Result is (string errortitle, string message))
                    {
                        M3L.ShowDialog(window, message, errortitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    CommandManager.InvalidateRequerySuggested();
                };
                nbw.RunWorkerAsync();
            }

            private bool validateBackupPath(string backupPath, GameTarget targetToBackup)
            {
                //Check empty
                if (!targetToBackup.IsCustomOption && Directory.Exists(backupPath))
                {
                    if (Directory.GetFiles(backupPath).Length > 0 ||
                        Directory.GetDirectories(backupPath).Length > 0)
                    {
                        //Directory not empty
                        Log.Error(@"Selected backup directory is not empty.");
                        M3L.ShowDialog(window,
                            M3L.GetString(M3L.string_directoryIsNotEmptyMustBeEmpty),
                            M3L.GetString(M3L.string_directoryNotEmpty), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                //Check is Documents folder
                var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", Utilities.GetGameName(Game));
                if (backupPath.Equals(docsPath, StringComparison.InvariantCultureIgnoreCase) || backupPath.IsSubPathOf(docsPath))
                {
                    Log.Error(@"User chose path in or around the documents path for the game - not allowed as game can load files from here.");
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedSubdirectoryOfGameDocumentsFolder, Utilities.GetGameName(Game)),
                        M3L.GetString(M3L.string_locationNotAllowedForBackup), MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                //Check space
                if (!targetToBackup.IsCustomOption)
                {
                    Utilities.GetDiskFreeSpaceEx(backupPath, out var freeBytes, out var totalBytes,
                        out var totalFreeBytes);
                    var requiredSpace = (ulong) (Utilities.GetSizeOfDirectory(targetToBackup.TargetPath) * 1.1); //10% buffer
                    Log.Information(
                        $@"Backup space check. Backup size: {FileSize.FormatSize(requiredSpace)}, free space: {FileSize.FormatSize(freeBytes)}");
                    if (freeBytes < requiredSpace)
                    {
                        //Not enough space.
                        Log.Error(
                            $@"Not enough disk space to create backup at {backupPath}. Required space: {FileSize.FormatSize(requiredSpace)} Free space: {FileSize.FormatSize(freeBytes)}");
                        M3L.ShowDialog(window,
                            M3L.GetString(M3L.string_dialogInsufficientDiskSpace,
                                Path.GetPathRoot(backupPath), FileSize.FormatSize(freeBytes).ToString(),
                                FileSize.FormatSize(requiredSpace).ToString()),
                            M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }

                    //Check writable
                    var writable = Utilities.IsDirectoryWritable(backupPath);
                    if (!writable)
                    {
                        //Not enough space.
                        Log.Error(
                            $@"Backup destination selected is not writable.");
                        M3L.ShowDialog(window,
                            M3L.GetString(M3L.string_dialog_userAccountDoesntHaveWritePermissionsBackup),
                            M3L.GetString(M3L.string_cannotCreateBackup), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                //Check it is not subdirectory of the game (we might want to check its not subdir of a target)
                foreach (var target in AvailableBackupSources)
                {
                    if (backupPath.IsSubPathOf(target.TargetPath))
                    {
                        //Not enough space.
                        Log.Error(
                            $@"A backup cannot be created in a subdirectory of a game. {backupPath} is a subdir of {targetToBackup.TargetPath}");
                        M3L.ShowDialog(window,
                            M3L.GetString(M3L.string_dialogBackupCannotBeSubdirectoryOfGame,
                                backupPath, target.TargetPath),
                            M3L.GetString(M3L.string_cannotCreateBackup), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                return true;
            }


            private void EndBackup()
            {
                Log.Information($@"EndBackup()");
                ResetBackupStatus();
                ProgressIndeterminate = false;
                ProgressVisible = false;
                BackupInProgress = false;
                return;
            }

            private void ResetBackupStatus()
            {
                BackupLocation = BackupService.GetGameBackupPath(Game);
                BackupService.RefreshBackupStatus(window, Game);
                BackupStatus = BackupService.GetBackupStatus(Game);
                BackupStatusLine2 = BackupLocation ?? BackupService.GetBackupStatusTooltip(Game);
            }

            public string GameIconSource { get; }
            public string GameTitle { get; }
            //Fody uses this property on weaving
#pragma warning disable 67
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
            public GameTarget BackupSourceTarget { get; set; }
            public string BackupLocation { get; set; }
            public string BackupStatus { get; set; }
            public string BackupStatusLine2 { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public ICommand BackupButtonCommand { get; set; }
            public ICommand UnlinkBackupCommand { get; set; }
            public bool BackupOptionsVisible => BackupLocation == null;
            public bool BackupInProgress { get; set; }

        }
    }
}
