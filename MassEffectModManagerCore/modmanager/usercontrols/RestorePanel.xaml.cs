using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.windows;
using MassEffectModManagerCore.ui;
using Microsoft.WindowsAPICodePack.Dialogs;
using Serilog;


namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for RestorePanel.xaml
    /// </summary>
    public partial class RestorePanel : MMBusyPanelBase
    {

        public bool AnyGameMissingBackup => !BackupService.ME1BackedUp || !BackupService.ME2BackedUp || !BackupService.ME3BackedUp;
        public ObservableCollectionExtended<GameRestoreObject> GameRestoreControllers { get; } = new ObservableCollectionExtended<GameRestoreObject>();

        //public GameBackup ME3Backup { get; set; }
        //public GameBackup ME2Backup { get; set; }
        //public GameBackup ME1Backup { get; set; }
        private List<GameTarget> targetsList;

        public RestorePanel(List<GameTarget> targetsList, GameTarget selectedTarget)
        {
            DataContext = this;
            this.targetsList = targetsList;
            LoadCommands();
            InitializeComponent();

            //InstallationTargets_ComboBox.SelectedItem = selectedTarget;
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(Close, CanClose);
        }

        private bool CanClose() => !GameRestoreControllers.Any(x => x.RestoreInProgress);

        private void Close()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                Close();
            }
        }

        public override void OnPanelVisible()
        {
            GameRestoreControllers.Add(new GameRestoreObject(Mod.MEGame.ME1, targetsList.Where(x => x.Game == Mod.MEGame.ME1), window));
            GameRestoreControllers.Add(new GameRestoreObject(Mod.MEGame.ME2, targetsList.Where(x => x.Game == Mod.MEGame.ME2), window));
            GameRestoreControllers.Add(new GameRestoreObject(Mod.MEGame.ME3, targetsList.Where(x => x.Game == Mod.MEGame.ME3), window));
        }

        public class GameRestoreObject : INotifyPropertyChanged
        {
            private Mod.MEGame Game;
            public ObservableCollectionExtended<GameTarget> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTarget>();
            private Window window;

            public GameRestoreObject(Mod.MEGame game, IEnumerable<GameTarget> availableBackupSources, Window window)
            {
                this.window = window;
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
                AvailableBackupSources.Add(new GameTarget(Game, "Restore to custom location", false, true));
                LoadCommands();
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        GameTitle = "Mass Effect";
                        GameIconSource = "/images/gameicons/ME1_48.ico";
                        break;
                    case Mod.MEGame.ME2:
                        GameTitle = "Mass Effect 2";
                        GameIconSource = "/images/gameicons/ME2_48.ico";
                        break;
                    case Mod.MEGame.ME3:
                        GameTitle = "Mass Effect 3";
                        GameIconSource = "/images/gameicons/ME3_48.ico";
                        break;
                }

                ResetRestoreStatus();
            }

            private void LoadCommands()
            {
                BackupButtonCommand = new GenericCommand(BeginRestore, CanBeginRestore);
            }

            private bool CanBeginRestore()
            {
                return RestoreTarget != null && !RestoreInProgress;
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
                NamedBackgroundWorker bw = new NamedBackgroundWorker(Game.ToString() + "Backup");
                bw.DoWork += (a, b) =>
                {
                    RestoreInProgress = true;


                    string restoreTargetPath = b.Argument as string;
                    string backupPath = BackupLocation;
                    BackupStatusLine2 = "Deleting existing game installation";
                    if (Directory.Exists(restoreTargetPath))
                    {
                        if (Directory.GetFiles(restoreTargetPath).Any() || Directory.GetDirectories(restoreTargetPath).Any())
                        {
                            Log.Information("Deleting existing game directory: " + restoreTargetPath);
                            try
                            {
                                bool deletedDirectory = Utilities.DeleteFilesAndFoldersRecursively(restoreTargetPath);
                                if (deletedDirectory != true)
                                {
                                    b.Result = RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY;
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                //todo: handle this better
                                Log.Error("Exception deleting game directory: " + restoreTargetPath + ": " + ex.Message);
                                b.Result = RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY;
                                return;
                            }
                        }
                    }
                    else
                    {
                        Log.Error("Game directory not found! Was it removed while the app was running?");
                    }

                    //Todo: Revert LODs, remove IndirectSound settings (MEUITM)
                    //Log.Information("Reverting lod settings");
                    //string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                    //string args = "--remove-lods --gameid " + BACKUP_THREAD_GAME;
                    //Utilities.runProcess(exe, args);

                    //if (BACKUP_THREAD_GAME == 1)
                    //{
                    //    string iniPath = IniSettingsHandler.GetConfigIniPath(1);
                    //    if (File.Exists(iniPath))
                    //    {
                    //        Log.Information("Reverting Indirect Sound ini fix for ME1");
                    //        IniFile engineConf = new IniFile(iniPath);
                    //        engineConf.DeleteKey("DeviceName", "ISACTAudio.ISACTAudioDevice");
                    //    }
                    //}

                    var created = Utilities.CreateDirectoryWithWritePermission(restoreTargetPath);
                    if (!created)
                    {
                        b.Result = RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY;
                        return;
                    }
                    BackupStatusLine2 = "Restoring game from backup";
                    if (restoreTargetPath != null)
                    {
                        //callbacks
                        #region callbacks
                        void fileCopiedCallback()
                        {
                            ProgressValue++;
                        }

                        string dlcFolderpath = MEDirectories.DLCPath(backupPath, Game) + '\\'; //\ at end makes sure we are restoring a subdir
                        int dlcSubStringLen = dlcFolderpath.Length;
                        Debug.WriteLine("DLC Folder: " + dlcFolderpath);
                        Debug.Write("DLC Fodler path len:" + dlcFolderpath);
                        bool aboutToCopyCallback(string fileBeingCopied)
                        {
                            if (fileBeingCopied.Contains("\\cmmbackup\\")) return false; //do not copy cmmbackup files
                            Debug.WriteLine(fileBeingCopied);
                            if (fileBeingCopied.StartsWith(dlcFolderpath))
                            {
                                //It's a DLC!
                                string dlcname = fileBeingCopied.Substring(dlcSubStringLen);
                                dlcname = dlcname.Substring(0, dlcname.IndexOf('\\'));
                                if (MEDirectories.OfficialDLCNames(RestoreTarget.Game).TryGetValue(dlcname, out var hrName))
                                {
                                    BackupStatusLine2 = "Restoring " + hrName;
                                }
                                else
                                {
                                    BackupStatusLine2 = "Restoring " + dlcname;
                                }
                            }
                            else
                            {
                                //It's basegame
                                if (fileBeingCopied.EndsWith(".bik"))
                                {
                                    BackupStatusLine2 = "Restoring Movies";
                                }
                                else if (new FileInfo(fileBeingCopied).Length > 52428800)
                                {
                                    BackupStatusLine2 = "Restoring " + Path.GetFileName(fileBeingCopied);
                                }
                                else
                                {
                                    BackupStatusLine2 = "Restoring BASEGAME";
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
                        BackupStatus = "Restoring game";
                        Log.Information("Copying backup to game directory: " + backupPath + " -> " + restoreTargetPath);
                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(backupPath), new DirectoryInfo(restoreTargetPath),
                            totalItemsToCopyCallback: totalFilesToCopyCallback,
                            aboutToCopyCallback: aboutToCopyCallback,
                            fileCopiedCallback: fileCopiedCallback,
                            ignoredExtensions: new[] { "*.pdf", "*.mp3" });
                        Log.Information("Restore of game data has completed");
                    }

                    //Check for cmmvanilla file and remove it present

                    string cmmVanilla = Path.Combine(restoreTargetPath, "cmm_vanilla");
                    if (File.Exists(cmmVanilla))
                    {
                        Log.Information("Removing cmm_vanilla file");
                        File.Delete(cmmVanilla);
                    }

                    Log.Information("Restore thread wrapping up");
                    b.Result = RestoreResult.RESTORE_OK;
                };
                bw.RunWorkerCompleted += (a, b) =>
                    {
                        if (b.Result is RestoreResult result)
                        {
                            switch (result)
                            {
                                case RestoreResult.ERROR_COULD_NOT_CREATE_DIRECTORY:
                                    Xceed.Wpf.Toolkit.MessageBox.Show("Could not create the game directory after it was deleted due to an error. View the logs from the help menu for more information.", "Error restoring game", MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                case RestoreResult.ERROR_COULD_NOT_DELETE_GAME_DIRECTORY:
                                    Xceed.Wpf.Toolkit.MessageBox.Show("Could not fully delete the game directory. It may have files or folders still open from various programs. Part of the game may have been deleted. View the logs from the help menu for more information.", "Error restoring game", MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                                case RestoreResult.EXCEPTION_DELETING_GAME_DIRECTORY:
                                    Xceed.Wpf.Toolkit.MessageBox.Show("An error occured while deleting the game directory. View the logs from the help menu for more information.", "Error restoring game", MessageBoxButton.OK, MessageBoxImage.Error);
                                    break;
                            }
                        }

                        EndRestore();
                        CommandManager.InvalidateRequerySuggested();
                    };
                var restTarget = RestoreTarget.TargetPath;
                if (RestoreTarget.IsCustomOption)
                {
                    CommonOpenFileDialog m = new CommonOpenFileDialog
                    {
                        IsFolderPicker = true,
                        EnsurePathExists = true,
                        Title = "Select new restore destination"
                    };
                    if (m.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        //Check empty
                        restTarget = m.FileName;
                        if (Directory.Exists(restTarget))
                        {
                            if (Directory.GetFiles(restTarget).Length > 0 || Directory.GetDirectories(restTarget).Length > 0)
                            {
                                //Directory not empty
                                Xceed.Wpf.Toolkit.MessageBox.Show("Directory is not empty. A backup destination must be empty.", "Cannot restore to this location", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                bw.RunWorkerAsync(restTarget);
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
                BackupLocation = Utilities.GetGameBackupPath(Game);
                BackupStatus = BackupLocation != null ? "Backed up" : "Not backed up";
                BackupStatusLine2 = BackupLocation;
            }

            public string GameIconSource { get; }
            public string GameTitle { get; }
            public event PropertyChangedEventHandler PropertyChanged;
            public GameTarget RestoreTarget { get; set; }
            public string BackupLocation { get; set; }
            public string BackupStatus { get; set; }
            public string BackupStatusLine2 { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public ICommand BackupButtonCommand { get; set; }
            public bool BackupOptionsVisible => BackupLocation == null;
            public bool RestoreInProgress { get; set; }

            public string RestoreButtonText
            {
                get
                {
                    if (RestoreTarget != null && BackupLocation != null) return "Restore this target";
                    if (RestoreTarget == null && BackupLocation != null) return "Select target";
                    if (BackupLocation == null) return "No backup";
                    return "Error!";
                }
            }
        }
    }
}