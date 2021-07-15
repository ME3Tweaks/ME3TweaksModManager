using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.me3tweaks;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;
using Serilog;
using RoboSharp;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for RestorePanel.xaml
    /// </summary>
    public partial class RestorePanel : MMBusyPanelBase
    {

        public bool AnyGameMissingBackup => !BackupService.ME1BackedUp || !BackupService.ME2BackedUp || !BackupService.ME3BackedUp;
        public ObservableCollectionExtended<GameRestoreObject> GameRestoreControllers { get; } = new ObservableCollectionExtended<GameRestoreObject>();
        private List<GameTarget> targetsList;

        public RestorePanel(List<GameTarget> targetsList, GameTarget selectedTarget)
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
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.LELauncher,
                    targetsList.Where(x => x.Game == MEGame.LELauncher), mainwindow));
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.LE1,
                    targetsList.Where(x => x.Game == MEGame.LE1), mainwindow));
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.LE2,
                    targetsList.Where(x => x.Game == MEGame.LE2), mainwindow));
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.LE3,
                    targetsList.Where(x => x.Game == MEGame.LE3), mainwindow));
            }

            if (Settings.GenerationSettingOT)
            {
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.ME1,
                    targetsList.Where(x => x.Game == MEGame.ME1), mainwindow));
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.ME2,
                    targetsList.Where(x => x.Game == MEGame.ME2), mainwindow));
                GameRestoreControllers.Add(new GameRestoreObject(MEGame.ME3,
                    targetsList.Where(x => x.Game == MEGame.ME3), mainwindow));
            }
        }

        public class GameRestoreObject : INotifyPropertyChanged
        {
            public bool RefreshTargets;

            public MEGame Game { get; }

            public bool CanOpenDropdown => !RestoreInProgress && BackupLocation != null;

            public ObservableCollectionExtended<GameTarget> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTarget>();
            private MainWindow window;

            public GameRestoreObject(MEGame game, IEnumerable<GameTarget> availableBackupSources, MainWindow window)
            {
                this.window = window;
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
                AvailableBackupSources.Add(new GameTarget(Game, M3L.GetString(M3L.string_restoreToCustomLocation), false, true));
                LoadCommands();
                GameTitle = Game.ToGameName();
                ResetRestoreStatus();
            }

            private void LoadCommands()
            {
                RestoreButtonCommand = new GenericCommand(BeginRestore, CanBeginRestore);
            }

            private bool CanBeginRestore()
            {
                return RestoreTarget != null && !RestoreInProgress && BackupLocation != null;
            }

            public enum RestoreResult
            {
                ERROR_COULD_NOT_DELETE_GAME_DIRECTORY,
                EXCEPTION_DELETING_GAME_DIRECTORY,
                RESTORE_OK,
                ERROR_COULD_NOT_CREATE_DIRECTORY
            }

            private void BeginRestore()
            {
                if (Utilities.IsGameRunning(Game))
                {
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialogCannotRestoreXWhileItIsRunning, Game.ToGameName()), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var useNewMethod = M3L.ShowDialog(window,
                    "Use the experimental new restore method? It is faster but is not yet battle tested.",
                    "Use beta feature?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                bool restore = RestoreTarget.IsCustomOption || useNewMethod; //custom option is restore to custom location
                restore = restore || M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_restoringXWillDeleteGameDir, Game.ToGameName()), M3L.GetString(M3L.string_gameTargetWillBeDeleted), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                if (restore)
                {
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(Game + @"-Restore");
                    nbw.WorkerReportsProgress = true;
                    nbw.ProgressChanged += (a, b) =>
                    {
                        if (b.UserState is double d)
                        {
                            TaskbarHelper.SetProgress(d);
                        }
                    };
                    nbw.DoWork += (a, b) =>
                    {
                        RestoreInProgress = true;
                        // Nuke the LODs
                        if (!RestoreTarget.IsCustomOption && RestoreTarget.Game.IsOTGame())
                        {
                            Log.Information($@"Resetting LODs for {RestoreTarget.Game}");
                            Utilities.SetLODs(RestoreTarget, false, false, false);
                        }

                        string restoreTargetPath = b.Argument as string;
                        string backupPath = BackupLocation;

                        if (!useNewMethod)
                        {
                            BackupStatusLine2 = M3L.GetString(M3L.string_deletingExistingGameInstallation);
                            if (Directory.Exists(restoreTargetPath))
                            {
                                if (Directory.GetFiles(restoreTargetPath).Any() ||
                                    Directory.GetDirectories(restoreTargetPath).Any())
                                {
                                    Log.Information(@"Deleting existing game directory: " + restoreTargetPath);
                                    try
                                    {
                                        bool deletedDirectory =
                                            Utilities.DeleteFilesAndFoldersRecursively(restoreTargetPath);
                                        if (deletedDirectory != true)
                                        {
                                            b.Result = RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY;
                                            return;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        //todo: handle this better
                                        Log.Error(
                                            $@"Exception deleting game directory: {restoreTargetPath}: {ex.Message}");
                                        b.Result = RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY;
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                Log.Error(@"Game directory not found! Was it removed while the app was running?");
                            }

                            var created = Utilities.CreateDirectoryWithWritePermission(restoreTargetPath);
                            if (!created)
                            {
                                b.Result = RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY;
                                return;
                            }
                        }

                        BackupStatusLine2 = M3L.GetString(M3L.string_restoringGameFromBackup);
                        if (restoreTargetPath != null)
                        {
                            //callbacks

                            #region callbacks

                            void fileCopiedCallback()
                            {
                                ProgressValue++;
                                if (ProgressMax != 0)
                                {
                                    nbw.ReportProgress(0, ProgressValue * 1.0 / ProgressMax);
                                }
                            }

                            string dlcFolderpath = MEDirectories.GetDLCPath(Game, backupPath) + '\\'; //\ at end makes sure we are restoring a subdir
                            int dlcSubStringLen = dlcFolderpath.Length;
                            Debug.WriteLine(@"DLC Folder: " + dlcFolderpath);
                            Debug.Write(@"DLC Folder path len:" + dlcFolderpath);

                            bool aboutToCopyCallback(string fileBeingCopied)
                            {
                                if (fileBeingCopied.Contains(@"\cmmbackup\")) return false; //do not copy cmmbackup files
                                Debug.WriteLine(fileBeingCopied);
                                if (fileBeingCopied.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    //It's a DLC!
                                    string dlcname = fileBeingCopied.Substring(dlcSubStringLen);
                                    int index = dlcname.IndexOf('\\');
                                    if (index > 0) //Files directly in the DLC directory won't have path sep
                                    {
                                        try
                                        {
                                            dlcname = dlcname.Substring(0, index);
                                            if (MEDirectories.OfficialDLCNames(RestoreTarget.Game).TryGetValue(dlcname, out var hrName))
                                            {
                                                BackupStatusLine2 = M3L.GetString(M3L.string_interp_restoringX, hrName);
                                            }
                                            else
                                            {
                                                BackupStatusLine2 = M3L.GetString(M3L.string_interp_restoringX, dlcname);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Crashes.TrackError(e, new Dictionary<string, string>()
                                            {
                                                {@"Source", @"Restore UI display callback"},
                                                {@"Value", fileBeingCopied},
                                                {@"DLC Folder path", dlcFolderpath}
                                            });
                                        }
                                    }
                                }
                                else
                                {
                                    //It's basegame
                                    if (fileBeingCopied.EndsWith(@".bik"))
                                    {
                                        BackupStatusLine2 = M3L.GetString(M3L.string_restoringMovies);
                                    }
                                    else if (new FileInfo(fileBeingCopied).Length > 52428800)
                                    {
                                        BackupStatusLine2 = M3L.GetString(M3L.string_interp_restoringX, Path.GetFileName(fileBeingCopied));
                                    }
                                    else
                                    {
                                        BackupStatusLine2 = M3L.GetString(M3L.string_restoringBasegame);
                                    }
                                }

                                return true;
                            }

                            void totalFilesToCopyCallback(int total)
                            {
                                ProgressValue = 0;
                                ProgressIndeterminate = false;
                                ProgressMax = total;
                            }

                            #endregion

                            BackupStatus = M3L.GetString(M3L.string_restoringGame);
                            Log.Information($@"Copying backup to game directory: {backupPath} -> {restoreTargetPath}");
                            if (useNewMethod)
                            {
                                string CurrentRCFile = null;
                                RoboCommand rc = new RoboCommand();
                                rc.CopyOptions.Destination = restoreTargetPath;
                                rc.CopyOptions.Source = backupPath;
                                rc.CopyOptions.Mirror = true;
                                rc.CopyOptions.MultiThreadedCopiesCount = 2;
                                rc.OnCopyProgressChanged += (sender, args) =>
                                {
                                    ProgressIndeterminate = false;
                                    ProgressValue = (int)args.CurrentFileProgress;
                                    ProgressMax = 100;
                                };
                                rc.OnFileProcessed += (sender, args) =>
                                {
                                    if (args.ProcessedFile.Name.StartsWith(backupPath) && args.ProcessedFile.Name.Length > backupPath.Length)
                                    {
                                        CurrentRCFile = args.ProcessedFile.Name.Substring(backupPath.Length + 1);
                                        BackupStatusLine2 = $"Copying {CurrentRCFile}";
                                    }
                                };
                                rc.Start().Wait();
                            }
                            else
                            {
                                CopyDir.CopyAll_ProgressBar(new DirectoryInfo(backupPath),
                                    new DirectoryInfo(restoreTargetPath),
                                    totalItemsToCopyCallback: totalFilesToCopyCallback,
                                    aboutToCopyCallback: aboutToCopyCallback,
                                    fileCopiedCallback: fileCopiedCallback,
                                    ignoredExtensions: new[] { @"*.pdf", @"*.mp3" });
                            }
                            Log.Information(@"Restore of game data has completed");
                            BackupCopyFinished(restoreTargetPath);
                        }


                    };
                    nbw.RunWorkerCompleted += (a, b) =>
                    {
                        if (b.Error != null)
                        {
                            Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                        }
                        TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                        if (b.Result is RestoreResult result)
                        {
                            switch (result)
                            {
                                case RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY:
                                    Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                    {
                                        {@"Game", Game.ToString()},
                                        {@"Result", @"Failure, Could not create target directory"}
                                    });
                                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogCouldNotCreateGameDirectoryAfterDeletion), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                case RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY:
                                    Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                    {
                                        {@"Game", Game.ToString()},
                                        {@"Result", @"Failure, Could not delete existing game directory"}
                                    });
                                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogcouldNotFullyDeleteGameDirectory), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                case RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY:
                                    Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                    {
                                        {@"Game", Game.ToString()},
                                        {@"Result", @"Failure, Exception deleting existing game directory"}
                                    });
                                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogErrorOccuredDeletingGameDirectory), M3L.GetString(M3L.string_errorRestoringGame), MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                case RestoreResult.RESTORE_OK:
                                    Analytics.TrackEvent(@"Restored game", new Dictionary<string, string>()
                                    {
                                        {@"Game", Game.ToString()},
                                        {@"Result", @"Success"}
                                    });
                                    break;
                            }
                        }

                        EndRestore();
                        CommandManager.InvalidateRequerySuggested();
                    };
                    var restoreTargetPath = RestoreTarget.TargetPath;
                    if (RestoreTarget.IsCustomOption)
                    {
                        CommonOpenFileDialog m = new CommonOpenFileDialog
                        {
                            IsFolderPicker = true,
                            EnsurePathExists = true,
                            Title = M3L.GetString(M3L.string_selectNewRestoreDestination)
                        };
                        if (m.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            //Check empty
                            restoreTargetPath = m.FileName;
                            if (Directory.Exists(restoreTargetPath))
                            {
                                if (Directory.GetFiles(restoreTargetPath).Length > 0 || Directory.GetDirectories(restoreTargetPath).Length > 0)
                                {
                                    Log.Warning($@"The selected restore directory is not empty: {restoreTargetPath}");
                                    //Directory not empty
                                    if (!useNewMethod)
                                    {
                                        M3L.ShowDialog(window,
                                            M3L.GetString(M3L
                                                .string_dialogDirectoryIsNotEmptyLocationToRestoreToMustBeEmpty),
                                            M3L.GetString(M3L.string_cannotRestoreToThisLocation), MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                                        return;
                                    }
                                    else
                                    {
                                        // Warn user
                                        var shouldContinue = MessageBoxResult.Yes == M3L.ShowDialog(window,
                                                                                    $"The selected directory is not empty: {restoreTargetPath}. ALL NON-GAME FILES AND FOLDERS IN THIS DIRECTORY WILL BE DELETED. Are you sure you want to continue?",
                                                                                    "Directory not empty", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                                        if (!shouldContinue)
                                            return;
                                        Log.Warning($@"The user is continuing to new-gen restore on existing directory anyways");

                                    }
                                }

                                //TODO: PREVENT RESTORING TO DOCUMENTS/BIOWARE
                            }

                            Analytics.TrackEvent(@"Chose to restore game to custom location", new Dictionary<string, string>() {
                                { @"Game", Game.ToString() },
                                { @"New-gen", useNewMethod.ToString() }
                            });

                        }
                        else
                        {
                            return;
                        }
                    }

                    RefreshTargets = true;
                    TaskbarHelper.SetProgress(0);
                    TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
                    nbw.RunWorkerAsync(restoreTargetPath);
                }
            }

            /// <summary>
            /// Call when the copying of data has finished. This will remove the CMMVanilla file
            /// </summary>
            /// <param name="restoreTargetPath"></param>
            private void BackupCopyFinished(string restoreTargetPath)
            {
                //Check for cmmvanilla file and remove it present
                string cmmVanilla = Path.Combine(restoreTargetPath, BackupService.CMM_VANILLA_FILENAME);
                if (File.Exists(cmmVanilla))
                {
                    Log.Information($@"Removing cmm_vanilla file: {cmmVanilla}");
                    File.Delete(cmmVanilla);
                }
            }

            private void EndRestore()
            {
                ResetRestoreStatus();
                ProgressIndeterminate = false;
                ProgressVisible = false;
                RestoreInProgress = false;
                return;
            }

            private void ResetRestoreStatus()
            {
                BackupLocation = BackupService.GetGameBackupPath(Game);
                BackupService.RefreshBackupStatus(window, Game);
                BackupStatus = BackupService.GetBackupStatus(Game);
                //BackupLocation != null ? M3L.GetString(M3L.string_backedUp) : M3L.GetString(M3L.string_notBackedUp);
                BackupStatusLine2 = BackupLocation ?? BackupService.GetBackupStatusTooltip(Game);
            }

            public string GameIconSource { get; }
            public string GameTitle { get; }
            //Fody uses this property on weaving
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
            public GameTarget RestoreTarget { get; set; }
            public string BackupLocation { get; set; }
            public string BackupStatus { get; set; }
            public string BackupStatusLine2 { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public ICommand RestoreButtonCommand { get; set; }
            public bool BackupOptionsVisible => BackupLocation == null;
            public bool RestoreInProgress { get; set; }

            public string RestoreButtonText
            {
                get
                {
                    if (RestoreTarget != null && BackupLocation != null) return M3L.GetString(M3L.string_restoreThisTarget);
                    if (RestoreTarget == null && BackupLocation != null) return M3L.GetString(M3L.string_selectTarget);
                    if (BackupLocation == null) return M3L.GetString(M3L.string_noBackup);
                    return M3L.GetString(M3L.string_error);
                }
            }
        }
    }
}
