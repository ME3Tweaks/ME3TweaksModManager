using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

using MassEffectModManagerCore.modmanager.helpers;
using System.Windows.Input;
using System.Diagnostics;
using Serilog;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.windows;
using Microsoft.Win32;

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

            //InstallationTargets_ComboBox.SelectedItem = selectedTarget;
        }

        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(Close, CanClose);
        }

        private bool CanClose() => !GameBackups.Any(x => x.BackupInProgress);

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
            GameBackups.Add(new GameBackup(Mod.MEGame.ME1, targetsList.Where(x => x.Game == Mod.MEGame.ME1), window));
            GameBackups.Add(new GameBackup(Mod.MEGame.ME2, targetsList.Where(x => x.Game == Mod.MEGame.ME2), window));
            GameBackups.Add(new GameBackup(Mod.MEGame.ME3, targetsList.Where(x => x.Game == Mod.MEGame.ME3), window));
        }

        public class GameBackup : INotifyPropertyChanged
        {
            private Mod.MEGame Game;
            public ObservableCollectionExtended<GameTarget> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTarget>();
            private Window window;
            public GameBackup(Mod.MEGame game, IEnumerable<GameTarget> availableBackupSources, Window window)
            {
                this.window = window;
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
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
                ResetBackupStatus();
            }

            private void LoadCommands()
            {
                BackupButtonCommand = new GenericCommand(BeginBackup, CanBeginBackup);
            }

            private bool CanBeginBackup()
            {
                return BackupSourceTarget != null && !BackupInProgress;
            }

            private void BeginBackup()
            {
                NamedBackgroundWorker bw = new NamedBackgroundWorker(Game.ToString() + "Backup");
                bw.DoWork += (a, b) =>
                {
                    BackupInProgress = true;
                    List<string> nonVanillaFiles = new List<string>();
                    void nonVanillaFileFoundCallback(string filepath)
                    {
                        Log.Error("Non-vanilla file found: " + filepath);
                        nonVanillaFiles.Add(filepath);
                    }

                    List<string> inconsistentDLC = new List<string>();
                    void inconsistentDLCFoundCallback(string filepath)
                    {
                        if (BackupSourceTarget.Supported)
                        {
                            Log.Error("DLC is in an inconsistent state: " + filepath);
                            inconsistentDLC.Add(filepath);
                        }
                        else
                        {
                            Log.Error("Detected an inconsistent DLC, likely due to an unofficial copy of the game");
                        }
                    }
                    ProgressVisible = true;
                    ProgressIndeterminate = true;
                    BackupStatus = "Validating backup source";
                    VanillaDatabaseService.LoadDatabaseFor(Game);
                    bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(BackupSourceTarget, nonVanillaFileFoundCallback);
                    bool isDLCConsistent = VanillaDatabaseService.ValidateTargetDLCConsistency(BackupSourceTarget, inconsistentDLCCallback: inconsistentDLCFoundCallback);
                    List<string> dlcModsInstalled = VanillaDatabaseService.GetInstalledDLCMods(BackupSourceTarget);


                    if (isVanilla && isDLCConsistent && dlcModsInstalled.Count == 0)
                    {
                        BackupStatus = "Waiting for user input";
                        string backupPath = null;
                        bool end = false;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            CommonOpenFileDialog m = new CommonOpenFileDialog
                            {
                                IsFolderPicker = true,
                                EnsurePathExists = true,
                                Title = "Select backup destination"
                            };
                            if (m.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                //Check empty
                                backupPath = m.FileName;
                                if (Directory.Exists(backupPath))
                                {
                                    if (Directory.GetFiles(backupPath).Length > 0 || Directory.GetDirectories(backupPath).Length > 0)
                                    {
                                        //Directory not empty
                                        MessageBox.Show("Directory is not empty. A backup destination must be empty.");
                                        end = true;
                                        EndBackup();
                                        return;
                                    }
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

                        #region callbacks
                        void fileCopiedCallback()
                        {
                            ProgressValue++;
                        }

                        string dlcFolderpath = MEDirectories.DLCPath(BackupSourceTarget) + '\\';
                        int dlcSubStringLen = dlcFolderpath.Length;
                        bool aboutToCopyCallback(string file)
                        {
                            if (file.Contains("\\cmmbackup\\")) return false; //do not copy cmmbackup files
                            if (file.StartsWith(dlcFolderpath))
                            {
                                //It's a DLC!
                                string dlcname = file.Substring(dlcSubStringLen);
                                dlcname = file.Substring(0, file.IndexOf('\\'));
                                if (MEDirectories.OfficialDLCNames(BackupSourceTarget.Game).TryGetValue(dlcname, out var hrName))
                                {
                                    BackupStatusLine2 = "Backing up " + hrName;
                                }
                                else
                                {
                                    BackupStatusLine2 = "Backing up " + dlcname;
                                }
                            }
                            else
                            {
                                //It's basegame
                                if (file.EndsWith(".bik"))
                                {
                                    BackupStatusLine2 = "Backing up Movies";
                                }
                                else if (new FileInfo(file).Length > 52428800)
                                {
                                    BackupStatusLine2 = "Backing up " + Path.GetFileName(file);
                                }
                                else
                                {
                                    BackupStatusLine2 = "Backing up BASEGAME";
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
                        BackupStatus = "Creating backup";

                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(BackupSourceTarget.TargetPath), new DirectoryInfo(backupPath),
                            totalItemsToCopyCallback: totalFilesToCopyCallback,
                            aboutToCopyCallback: aboutToCopyCallback,
                            fileCopiedCallback: fileCopiedCallback,
                            ignoredExtensions: new[] { "*.pdf", "*.mp3" });
                        switch (Game)
                        {
                            case Mod.MEGame.ME1:
                            case Mod.MEGame.ME2:
                                Utilities.WriteRegistryKey(App.BACKUP_REGISTRY_KEY, Game + "VanillaBackupLocation", backupPath);
                                break;
                            case Mod.MEGame.ME3:
                                Utilities.WriteRegistryKey(App.REGISTRY_KEY_ME3CMM, "VanillaCopyLocation", backupPath);
                                break;
                        }
                        EndBackup();
                        return;
                    }

                    if (!isVanilla)
                    {
                        //Show UI for non vanilla
                        b.Result = (nonVanillaFiles, "Cannot backup modified game", "The following files do not match the vanilla database and appear to be modified. Due to these files being modified, a backup cannot be taken of this installation.");
                    }
                    else if (!isDLCConsistent)
                    {
                        if (BackupSourceTarget.Supported)
                        {
                            b.Result = (inconsistentDLC, "Inconsistent DLC detected", "The following DLC are in an inconsistent state; they have a packed SFAR file but contain unpacked game files. The configuration of these DLC are not supported, the unpacked files must be manually removed.");
                        }
                        else
                        {
                            b.Result = ("Inconsistent DLC detected", "Inconsistent DLC was detected. This may be due to using an unofficial copy of the game.");
                        }
                    }
                    else if (dlcModsInstalled.Count > 0)
                    {
                        b.Result = (dlcModsInstalled, "DLC mods are installed", "The following DLC folders were detected that are not part of the official game by BioWare. Backups cannot include DLC mods and must be unmodified.");

                    }
                    EndBackup();
                };
                bw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Result is (List<string> listItems, string title, string text))
                    {
                        ListDialog ld = new ListDialog(listItems, title, text, window);
                        ld.Show();
                    }
                    else if (b.Result is (string errortitle, string message))
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(message, errortitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    CommandManager.InvalidateRequerySuggested();
                };
                bw.RunWorkerAsync();
            }

            private void EndBackup()
            {
                ResetBackupStatus();
                ProgressIndeterminate = false;
                ProgressVisible = false;
                BackupInProgress = false;
                return;
            }

            private void ResetBackupStatus()
            {
                BackupLocation = Utilities.GetGameBackupPath(Game);
                BackupStatus = BackupLocation != null ? "Backed up" : "Not backed up";
                BackupStatusLine2 = BackupLocation;
            }

            public string GameIconSource { get; }
            public string GameTitle { get; }
            public event PropertyChangedEventHandler PropertyChanged;
            public GameTarget BackupSourceTarget { get; set; }
            public string BackupLocation { get; set; }
            public string BackupStatus { get; set; }
            public string BackupStatusLine2 { get; set; }
            public int ProgressMax { get; set; } = 100;
            public int ProgressValue { get; set; } = 0;
            public bool ProgressIndeterminate { get; set; } = true;
            public bool ProgressVisible { get; set; } = false;
            public ICommand BackupButtonCommand { get; set; }
            public bool BackupOptionsVisible => BackupLocation == null;
            public bool BackupInProgress { get; set; }

        }
    }
}
