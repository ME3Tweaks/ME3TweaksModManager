using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using Microsoft.WindowsAPICodePack.Taskbar;
using PropertyChanged;
using NamedBackgroundWorker = ME3TweaksCore.Helpers.NamedBackgroundWorker;

namespace ME3TweaksModManager.modmanager.objects
{
    public class GameBackupWrapper : INotifyPropertyChanged
    {
        /// <summary>
        /// Game this wrapper is for
        /// </summary>
        public MEGame Game { get; }

        /// <summary>
        /// All options for backup source. Includes all targets of game (that are not texture modded), as well as a custom option to link
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTargetWPF>();

        /// <summary>
        /// Handler for performing the backup
        /// </summary>
        public GameBackup BackupHandler { get; init; }

        /// <summary>
        /// Window for centering dialogs to
        /// </summary>
        private Window window;

        public GameBackupWrapper(MEGame game, IEnumerable<GameTargetWPF> availableBackupSources, MainWindow window)
        {
            this.window = window;
            this.Game = game;
            this.AvailableBackupSources.AddRange(availableBackupSources);
            this.AvailableBackupSources.Add(new GameTargetWPF(Game, M3L.GetString(M3L.string_linkBackupToAnExistingGameCopy), false, true));
            LoadCommands();
            GameTitle = Game.ToGameName();
            BackupHandler = new GameBackup(game, window.InstallationTargets)
            {
                BlockingActionCallback = M3PromptCallbacks.BlockingActionOccurred,
                SelectGameExecutableCallback = M3PromptCallbacks.SelectGameExecutable,
                SelectGameBackupFolderDestination = M3PromptCallbacks.SelectDirectory,
                BlockingListCallback = M3PromptCallbacks.ShowErrorListCallback,
                SelectGameLanguagesCallback = SelectBackupLanguages,
                WarningActionYesNoCallback = M3PromptCallbacks.ShowWarningYesNoCallback,
            };
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
                BackupService.UnlinkBackup(Game);
                ResetBackupStatus(true);
            }
        }

        private bool CanUnlinkBackup()
        {
            return BackupStatus.MarkedBackupLocation != null;
        }

        private bool CanBeginBackup()
        {
            return BackupSourceTarget != null && !BackupHandler.BackupInProgress;
        }

        /// <summary>
        /// Gets the list of supported languages for backup
        /// </summary>
        /// <returns></returns>
        private MELanguage[] GetLanguages()
        {
            string[] allGameLangauges = BackupSourceTarget.Game != MEGame.LELauncher ? StarterKitGeneratorWindow.GetLanguagesForGame(BackupSourceTarget.Game).Select(x => x.filecode).ToArray() : null;
            List<MELanguage> langs = new List<MELanguage>();
            foreach (var lang in allGameLangauges)
            {
                langs.Add(new MELanguage(lang.GetUnrealLocalization()));
            }
            return langs.ToArray();
        }

        private void BeginBackup()
        {
            //string[] allGameLangauges = BackupSourceTarget.Game != MEGame.LELauncher ? StarterKitGeneratorWindow.GetLanguagesForGame(BackupSourceTarget.Game).Select(x => x.filecode).ToArray() : null;
            //var languages = GetLanguages();
            //var targetToBackup = BackupSourceTarget;
            //if (!targetToBackup.IsCustomOption)
            //{
            //    if (MUtilities.IsGameRunning(targetToBackup.Game))
            //    {
            //        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotBackupGameWhileRunning, BackupSourceTarget.Game.ToGameName()), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
            //        return;
            //    }

            //    // Language selection
            //    if (Game != MEGame.LELauncher && languages != null)
            //    {
            //        CheckBoxDialog cbw = new CheckBoxDialog(window,
            //            M3L.GetString(M3L.string_dialog_selectWhichLanguagesToIncludeInBackup),
            //            M3L.GetString(M3L.string_selectLanguages), allGameLangauges,
            //            new[] { languages.First() }, new[] { languages.First() }, 450, 300);
            //        cbw.ShowDialog();
            //        languages = cbw.GetSelectedItems().OfType<BackupLanguage>().ToArray();
            //    }
            //}
            //else
            //{
            //    // Point to existing game installation
            //    M3Log.Information(@"BeginBackup() with IsCustomOption.");
            //    var linkWarning = M3L.ShowDialog(window,
            //        M3L.GetString(M3L.string_dialog_linkTargetWontBeModdable), M3L.GetString(M3L.string_linkWarning), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            //    if (linkWarning == MessageBoxResult.Cancel)
            //    {
            //        M3Log.Information(@"User aborted linking due to dialog");
            //        return;
            //    }

            //    M3Log.Information(@"Prompting user to select executable of link target");
            //    var gameexe = M3Utilities.PromptForGameExecutable(new[] { Game });
            //    if (gameexe == null) { return; }
            //    targetToBackup = new GameTargetWPF(Game, M3Utilities.GetGamePathFromExe(Game, gameexe), false, true);
            //    if (AvailableBackupSources.Any(x => x.TargetPath.Equals(targetToBackup.TargetPath, StringComparison.InvariantCultureIgnoreCase)))
            //    {
            //        // Can't point to an existing modding target
            //        M3Log.Error(@"This target is not valid to point to as a backup: It is listed a modding target already, it must be removed as a target first");
            //        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedAlreadyATarget), M3L.GetString(M3L.string_cannotLinkGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
            //        return;
            //    }

            //    var validationFailureReason = targetToBackup.ValidateTarget(ignoreCmmVanilla: true);
            //    if (!targetToBackup.IsValid)
            //    {
            //        M3Log.Error(@"This installation is not valid to point to as a backup: " + validationFailureReason);
            //        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedInvalidTarget, validationFailureReason), M3L.GetString(M3L.string_invalidGameCopy), MessageBoxButton.OK, MessageBoxImage.Error);
            //        return;
            //    }
            //}

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

            var targets = AvailableBackupSources.OfType<GameTarget>().ToList();
            nbw.DoWork += (a, b) =>
            {
                b.Result = BackupHandler.PerformBackup(BackupSourceTarget, targets);
                ResetBackupStatus(true); // Updates some WPF things
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        private MELanguage[] SelectBackupLanguages(string title, string message)
        {
            var availableLangs = GetLanguages();
            var forcedOptions = new[] { availableLangs.First(x => x.Localization == MELocalization.INT) }; // INT is first
            CheckBoxDialog cbd = new CheckBoxDialog(window, message, title, availableLangs, forcedOptions, forcedOptions);
            cbd.ShowDialog();
            return cbd.GetSelectedItems().OfType<MELanguage>().ToArray();
        }

        //private bool validateBackupPath(string backupPath, GameTargetWPF targetToBackup)
        //{
        //    //Check empty
        //    if (!targetToBackup.IsCustomOption && Directory.Exists(backupPath))
        //    {
        //        if (Directory.GetFiles(backupPath).Length > 0 ||
        //            Directory.GetDirectories(backupPath).Length > 0)
        //        {
        //            //Directory not empty
        //            M3Log.Error(@"Selected backup directory is not empty.");
        //            M3L.ShowDialog(window,
        //                M3L.GetString(M3L.string_directoryIsNotEmptyMustBeEmpty),
        //                M3L.GetString(M3L.string_directoryNotEmpty), MessageBoxButton.OK,
        //                MessageBoxImage.Error);
        //            return false;
        //        }
        //    }

        //    //Check is Documents folder
        //    var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare");
        //    if (backupPath.Equals(docsPath, StringComparison.InvariantCultureIgnoreCase) || backupPath.IsSubPathOf(docsPath))
        //    {
        //        M3Log.Error(@"User chose path in the documents path for the game - not allowed as game can load files from here.");
        //        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_linkFailedSubdirectoryOfGameDocumentsFolder),
        //            M3L.GetString(M3L.string_locationNotAllowedForBackup), MessageBoxButton.OK,
        //            MessageBoxImage.Error);
        //        return false;
        //    }

        //    //Check space
        //    if (!targetToBackup.IsCustomOption)
        //    {
        //        M3Utilities.GetDiskFreeSpaceEx(backupPath, out var freeBytes, out var totalBytes,
        //            out var totalFreeBytes);
        //        var requiredSpace = (ulong)(M3Utilities.GetSizeOfDirectory(targetToBackup.TargetPath) * 1.1); //10% buffer
        //        M3Log.Information(
        //            $@"Backup space check. Backup size: {FileSize.FormatSize(requiredSpace)}, free space: {FileSize.FormatSize(freeBytes)}");
        //        if (freeBytes < requiredSpace)
        //        {
        //            //Not enough space.
        //            M3Log.Error(
        //                $@"Not enough disk space to create backup at {backupPath}. Required space: {FileSize.FormatSize(requiredSpace)} Free space: {FileSize.FormatSize(freeBytes)}");
        //            M3L.ShowDialog(window,
        //                M3L.GetString(M3L.string_dialogInsufficientDiskSpace,
        //                    Path.GetPathRoot(backupPath), FileSize.FormatSize(freeBytes).ToString(),
        //                    FileSize.FormatSize(requiredSpace).ToString()),
        //                M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK,
        //                MessageBoxImage.Error);
        //            return false;
        //        }

        //        //Check writable
        //        var writable = M3Utilities.IsDirectoryWritable(backupPath);
        //        if (!writable)
        //        {
        //            //Not enough space.
        //            M3Log.Error(
        //                $@"Backup destination selected is not writable.");
        //            M3L.ShowDialog(window,
        //                M3L.GetString(M3L.string_dialog_userAccountDoesntHaveWritePermissionsBackup),
        //                M3L.GetString(M3L.string_cannotCreateBackup), MessageBoxButton.OK,
        //                MessageBoxImage.Error);
        //            return false;
        //        }
        //    }

        //    //Check it is not subdirectory of the game (we might want to check its not subdir of a target)
        //    foreach (var target in AvailableBackupSources)
        //    {
        //        if (backupPath.IsSubPathOf(target.TargetPath))
        //        {
        //            //Not enough space.
        //            M3Log.Error(
        //                $@"A backup cannot be created in a subdirectory of a game. {backupPath} is a subdir of {targetToBackup.TargetPath}");
        //            M3L.ShowDialog(window,
        //                M3L.GetString(M3L.string_dialogBackupCannotBeSubdirectoryOfGame,
        //                    backupPath, target.TargetPath),
        //                M3L.GetString(M3L.string_cannotCreateBackup), MessageBoxButton.OK,
        //                MessageBoxImage.Error);
        //            return false;
        //        }
        //    }

        //    return true;
        //}


        //private void EndBackup()
        //{
        //    M3Log.Information($@"EndBackup()");
        //    ResetBackupStatus(true);
        //    ProgressIndeterminate = false;
        //    ProgressVisible = false;
        //    BackupInProgress = false;
        //    return;
        //}

        private void ResetBackupStatus(bool forceRefresh)
        {
            BackupService.RefreshBackupStatus(game: Game);
            BackupStatus = BackupService.GetBackupStatus(Game); // this is dynamic object that should be bound to in ui
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackupOptionsVisible)));
        }

        public string GameTitle { get; }
        public GameTargetWPF BackupSourceTarget { get; set; }
        public GameBackupStatus BackupStatus { get; set; }
        //public int ProgressMax { get; set; } = 100;
        //public int ProgressValue { get; set; } = 0;
        //public bool ProgressIndeterminate { get; set; } = true;
        //public bool ProgressVisible { get; set; } = false;
        public ICommand BackupButtonCommand { get; set; }
        public ICommand UnlinkBackupCommand { get; set; }
        public bool BackupOptionsVisible => BackupStatus.MarkedBackupLocation == null;
        public bool BackupInProgress { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

}
