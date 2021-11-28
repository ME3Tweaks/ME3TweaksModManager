using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.WindowsAPICodePack.Taskbar;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupRestoreManager.xaml
    /// </summary>
    public partial class BackupCreator : MMBusyPanelBase, ISizeAdjustable
    {
        public bool AnyGameMissingBackup => BackupService.AnyGameMissingBackup(MEGameSelector.GetEnabledGames()); // We do not check the launcher.
        public ObservableCollectionExtended<GameBackup> GameBackups { get; } = new ObservableCollectionExtended<GameBackup>();

        private List<GameTargetWPF> targetsList;
        public BackupCreator(List<GameTargetWPF> targetsList, GameTargetWPF selectedTarget, Window window)
        {
            this.targetsList = targetsList;
            LoadCommands();
            Self = this;
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

        protected override void OnClosing(DataEventArgs args)
        {
            window.SizeChanged -= OnBackupStatusChanged;
            base.OnClosing(args);
        }

        private void OnBackupStatusChanged()
        {
            Adjustment = -26 * GameBackups.Count(x => !x.BackupOptionsVisible);
            TriggerPropertyChangedFor(nameof(Self));
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            window.SizeChanged += OnBackupStatusChanged;
            if (Settings.GenerationSettingLE)
            {
                GameBackups.Add(new GameBackup(MEGame.LELauncher, targetsList.Where(x => x.Game == MEGame.LELauncher), mainwindow, OnBackupStatusChanged));
                GameBackups.Add(new GameBackup(MEGame.LE1, targetsList.Where(x => x.Game == MEGame.LE1), mainwindow, OnBackupStatusChanged));
                GameBackups.Add(new GameBackup(MEGame.LE2, targetsList.Where(x => x.Game == MEGame.LE2), mainwindow, OnBackupStatusChanged));
                GameBackups.Add(new GameBackup(MEGame.LE3, targetsList.Where(x => x.Game == MEGame.LE3), mainwindow, OnBackupStatusChanged));
            }

            if (Settings.GenerationSettingOT)
            {
                GameBackups.Add(new GameBackup(MEGame.ME1, targetsList.Where(x => x.Game == MEGame.ME1), mainwindow, OnBackupStatusChanged));
                GameBackups.Add(new GameBackup(MEGame.ME2, targetsList.Where(x => x.Game == MEGame.ME2), mainwindow, OnBackupStatusChanged));
                GameBackups.Add(new GameBackup(MEGame.ME3, targetsList.Where(x => x.Game == MEGame.ME3), mainwindow, OnBackupStatusChanged));
            }
            OnBackupStatusChanged();
        }

        private void OnBackupStatusChanged(object sender, SizeChangedEventArgs e)
        {
            OnBackupStatusChanged();
        }

        [AddINotifyPropertyChangedInterface]
        public class GameBackup
        {
            public MEGame Game { get; }
            public ObservableCollectionExtended<GameTargetWPF> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTargetWPF>();
            private MainWindow window;
            private Action backupStatusChangedDelegate;

            public GameBackup(MEGame game, IEnumerable<GameTargetWPF> availableBackupSources, MainWindow window, Action backupStatusChangedDelegate)
            {
                this.window = window;
                this.backupStatusChangedDelegate = backupStatusChangedDelegate;
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
                this.AvailableBackupSources.Add(new GameTargetWPF(Game, M3L.GetString(M3L.string_linkBackupToAnExistingGameCopy), false, true));
                LoadCommands();
                GameTitle = Game.ToGameName();
                ResetBackupStatus(true);
            }

            private void LoadCommands()
            {
                BackupButtonCommand = new GenericCommand(BeginBackup, CanBeginBackup);
                UnlinkBackupCommand = new GenericCommand(UnlinkBackup, CanUnlinkBackup);
            }

            private void UnlinkBackup()
            {
                var gbPath = BackupService.GetGameBackupPath(Game, forceReturnPath: true);
                M3Log.Information($@"User is attempting to unlink backup for {Game}");
                var message = M3L.GetString(M3L.string_dialog_unlinkingBackup, Game.ToGameName(), gbPath, Game.ToGameName());
                var shouldUnlink = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_unlinkingBackup), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                if (shouldUnlink)
                {
                    // Unlink
                    ME3TweaksCore.Services.Backup.BackupService.UnlinkBackup(Game);
                }
            }

            private bool CanUnlinkBackup()
            {
                return BackupLocation != null;
            }

            private bool CanBeginBackup()
            {
                return BackupSourceTarget != null && !BackupInProgress;
            }

            private void BeginBackup()
            {
                /*string[] allGameLangauges = BackupSourceTarget.Game != MEGame.LELauncher ? StarterKitGeneratorWindow.GetLanguagesForGame(BackupSourceTarget.Game).Select(x=>x.filecode).ToArray() : null;
                string[] languages = null;
                var targetToBackup = BackupSourceTarget;
                if (!targetToBackup.IsCustomOption)
                {
                    if (Utilities.IsGameRunning(targetToBackup.Game))
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotBackupGameWhileRunning, BackupSourceTarget.Game.ToGameName()), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Language selection
                    if (Game != MEGame.LELauncher)
                    {
                        CheckBoxDialog cbw = new CheckBoxDialog(window,
                            M3L.GetString(M3L.string_dialog_selectWhichLanguagesToIncludeInBackup),
                            M3L.GetString(M3L.string_selectLanguages), allGameLangauges,
                            new[] { @"INT" }, new[] { @"INT" }, 450, 300);
                        cbw.ShowDialog();
                        languages = cbw.GetSelectedItems().OfType<string>().ToArray();
                    }
                }
                else
                {
                    // Point to existing game installation
                    M3Log.Information(@"BeginBackup() with IsCustomOption.");
                    var linkWarning = M3L.ShowDialog(window,
                        M3L.GetString(M3L.string_dialog_linkTargetWontBeModdable), M3L.GetString(M3L.string_linkWarning), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (linkWarning == MessageBoxResult.Cancel)
                    {
                        M3Log.Information(@"User aborted linking due to dialog");
                        return;
                    }

                    M3Log.Information(@"Prompting user to select executable of link target");
                    var gameexe = Utilities.PromptForGameExecutable(new[] { Game });
                    if (gameexe == null) { return; }
                    targetToBackup = new GameTargetWPF(Game, Utilities.GetGamePathFromExe(Game, gameexe), false, true);
                    if (AvailableBackupSources.Any(x => x.TargetPath.Equals(targetToBackup.TargetPath, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // Can't point to an existing modding target
                        M3Log.Error(@"This target is not valid to point to as a backup: It is listed a modding target already, it must be removed as a target first");
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedAlreadyATarget), M3L.GetString(M3L.string_cannotLinkGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var validationFailureReason = targetToBackup.ValidateTarget(ignoreCmmVanilla: true);
                    if (!targetToBackup.IsValid)
                    {
                        M3Log.Error(@"This installation is not valid to point to as a backup: " + validationFailureReason);
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedInvalidTarget, validationFailureReason), M3L.GetString(M3L.string_invalidGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                */
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
                    // TODO: Changeover to M3Core
                    /*
                    M3Log.Information(@"Starting the backup thread. Checking path: " + targetToBackup.TargetPath);
                    BackupInProgress = true;
                    bool end = false;

                    List<string> nonVanillaFiles = new List<string>();

                    void nonVanillaFileFoundCallback(string filepath)
                    {
                        M3Log.Error($@"Non-vanilla file found: {filepath}");
                        nonVanillaFiles.Add(filepath);
                    }

                    List<string> inconsistentDLC = new List<string>();

                    void inconsistentDLCFoundCallback(string filepath)
                    {
                        if (targetToBackup.Supported)
                        {
                            M3Log.Error($@"DLC is in an inconsistent state: {filepath}");
                            inconsistentDLC.Add(filepath);
                        }
                        else
                        {
                            M3Log.Error(@"Detected an inconsistent DLC, likely due to an unofficial copy of the game");
                        }
                    }

                    ProgressVisible = true;
                    ProgressIndeterminate = true;
                    BackupStatus = M3L.GetString(M3L.string_validatingBackupSource);
                    M3Log.Information(@"Checking target is vanilla");
                    bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(targetToBackup, nonVanillaFileFoundCallback, false);

                    M3Log.Information(@"Checking DLC consistency");
                    bool isDLCConsistent = VanillaDatabaseService.ValidateTargetDLCConsistency(targetToBackup, inconsistentDLCCallback: inconsistentDLCFoundCallback);

                    M3Log.Information(@"Checking only vanilla DLC is installed");
                    List<string> dlcModsInstalled = VanillaDatabaseService.GetInstalledDLCMods(targetToBackup).Select(x =>
                    {
                        var tpmi = TPMIService.GetThirdPartyModInfo(x, targetToBackup.Game);
                        if (tpmi != null) return $@"{x} ({tpmi.modname})";
                        return x;
                    }).ToList();
                    var installedDLC = VanillaDatabaseService.GetInstalledOfficialDLC(targetToBackup);
                    var allOfficialDLC = targetToBackup.Game == MEGame.LELauncher ? null : MEDirectories.OfficialDLC(targetToBackup.Game);

                    if (allOfficialDLC != null && installedDLC.Count() < allOfficialDLC.Count())
                    {
                        var dlcList = string.Join("\n - ", allOfficialDLC.Except(installedDLC).Select(x => $@"{MEDirectories.OfficialDLCNames(targetToBackup.Game)[x]} ({x})")); //do not localize
                        dlcList = @" - " + dlcList;
                        M3Log.Information(@"The following dlc will be missing in the backup if user continues: ");
                        M3Log.Information(dlcList);

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

                    M3Log.Information(@"Checking for TexturesMEM TFCs");
                    var memTextures = Directory.GetFiles(targetToBackup.TargetPath, @"TexturesMEM*.tfc", SearchOption.AllDirectories);

                    if (end) return;
                    if (isVanilla && isDLCConsistent && !Enumerable.Any(dlcModsInstalled) && !Enumerable.Any(memTextures))
                    {
                        BackupStatus = M3L.GetString(M3L.string_waitingForUserInput);

                        string backupPath = null;
                        if (!targetToBackup.IsCustomOption)
                        {
                            // Creating a new backup
                            nbw.ReportProgress(0, TaskbarProgressBarState.Paused);

                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                M3Log.Information(@"Prompting user to select backup destination");

                                CommonOpenFileDialog m = new CommonOpenFileDialog
                                {
                                    IsFolderPicker = true,
                                    EnsurePathExists = true,
                                    Title = M3L.GetString(M3L.string_selectBackupDestination)
                                };
                                if (m.ShowDialog() == CommonFileDialogResult.Ok)
                                {
                                    backupPath = m.FileName;
                                    M3Log.Information(@"Backup path chosen: " + backupPath);

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

                            M3Log.Information(@"Linking existing backup at " + targetToBackup.TargetPath);
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

                                    if (languages != null)
                                    {
                                        // Check language of file
                                        var fileName = Path.GetFileNameWithoutExtension(file);
                                        if (fileName != null && fileName.LastIndexOf("_", StringComparison.InvariantCultureIgnoreCase) > 0)
                                        {
                                            var suffix = fileName.Substring(fileName.LastIndexOf("_", StringComparison.InvariantCultureIgnoreCase) + 1); // INT, ESN, PLPC
                                            if (allGameLangauges != null && allGameLangauges.Contains(suffix, StringComparer.InvariantCultureIgnoreCase) && !languages.Contains(suffix, StringComparer.InvariantCultureIgnoreCase))
                                            {
                                                Debug.WriteLine($@"Skipping non-selected localized file for backup: {file}");
                                                return false; // Do not back up this file
                                            }
                                        }
                                    }

                                    if (file.StartsWith(dlcFolderpath, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        #region Backing up DLC files
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
                                        #endregion
                                    }
                                    else
                                    {
                                        #region Backing up movies or big files
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
                                        #endregion
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
                            M3Log.Information($@"Backing up {targetToBackup.TargetPath} to {backupPath}");
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
                        Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3TWEAKS, $@"{Game}VanillaBackupLocation", backupPath);


                        var cmmvanilla = Path.Combine(backupPath, @"cmm_vanilla");
                        if (!File.Exists(cmmvanilla))
                        {
                            M3Log.Information($@"Writing cmm_vanilla to " + cmmvanilla);
                            File.Create(cmmvanilla).Close();
                        }

                        M3Log.Information($@"Backup completed.");

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
                    EndBackup();*/
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Error != null)
                    {
                        M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
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

            private bool validateBackupPath(string backupPath, GameTargetWPF targetToBackup)
            {
                //Check empty
                if (!targetToBackup.IsCustomOption && Directory.Exists(backupPath))
                {
                    if (Directory.GetFiles(backupPath).Length > 0 ||
                        Directory.GetDirectories(backupPath).Length > 0)
                    {
                        //Directory not empty
                        M3Log.Error(@"Selected backup directory is not empty.");
                        M3L.ShowDialog(window,
                            M3L.GetString(M3L.string_directoryIsNotEmptyMustBeEmpty),
                            M3L.GetString(M3L.string_directoryNotEmpty), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                //Check is Documents folder
                var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare");
                if (backupPath.Equals(docsPath, StringComparison.InvariantCultureIgnoreCase) || backupPath.IsSubPathOf(docsPath))
                {
                    M3Log.Error(@"User chose path in the documents path for the game - not allowed as game can load files from here.");
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedSubdirectoryOfGameDocumentsFolder),
                        M3L.GetString(M3L.string_locationNotAllowedForBackup), MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                //Check space
                if (!targetToBackup.IsCustomOption)
                {
                    M3Utilities.GetDiskFreeSpaceEx(backupPath, out var freeBytes, out var totalBytes,
                        out var totalFreeBytes);
                    var requiredSpace = (ulong)(M3Utilities.GetSizeOfDirectory(targetToBackup.TargetPath) * 1.1); //10% buffer
                    M3Log.Information(
                        $@"Backup space check. Backup size: {FileSize.FormatSize(requiredSpace)}, free space: {FileSize.FormatSize(freeBytes)}");
                    if (freeBytes < requiredSpace)
                    {
                        //Not enough space.
                        M3Log.Error(
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
                    var writable = M3Utilities.IsDirectoryWritable(backupPath);
                    if (!writable)
                    {
                        //Not enough space.
                        M3Log.Error(
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
                        M3Log.Error(
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
                M3Log.Information($@"EndBackup()");
                ResetBackupStatus(true);
                ProgressIndeterminate = false;
                ProgressVisible = false;
                BackupInProgress = false;
                return;
            }

            private void ResetBackupStatus(bool forceRefresh)
            {
                BackupLocation = BackupService.GetGameBackupPath(Game, refresh: forceRefresh);
                BackupService.RefreshBackupStatus(game: Game);
                BackupStatus = BackupService.GetBackupStatus(Game); // this is dynamic object that should be bound to in ui
                backupStatusChangedDelegate?.Invoke();
            }

            public string GameTitle { get; }
            public GameTargetWPF BackupSourceTarget { get; set; }
            public string BackupLocation
            {
                get;
                set;
            }
            public GameBackupStatus BackupStatus { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public ICommand BackupButtonCommand { get; set; }
            public ICommand UnlinkBackupCommand { get; set; }

            [DependsOn(nameof(BackupLocation))]
            public bool BackupOptionsVisible => BackupLocation == null;
            public bool BackupInProgress { get; set; }

        }

        public double Adjustment { get; set; }
        public double FullSize => mainwindow?.RootDisplayObject.ActualHeight ?? 0;
        public ISizeAdjustable Self { get; init; }
    }
}
