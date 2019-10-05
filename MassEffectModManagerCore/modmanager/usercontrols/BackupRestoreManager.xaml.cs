using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using System.Windows.Input;
using System.Diagnostics;
using Serilog;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using MassEffectModManagerCore.GameDirectories;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupRestoreManager.xaml
    /// </summary>
    public partial class BackupRestoreManager : UserControl, INotifyPropertyChanged
    {

        public bool ME3BackupButtonVisible => !BackupService.ME3BackedUp && ME3InstallationTargets.Count > 0 && InstallationTargetsME3_ComboBox.SelectedItem != null;

        public bool AnyGameMissingBackup => !BackupService.ME1BackedUp || !BackupService.ME2BackedUp || !BackupService.ME3BackedUp;
        public ObservableCollectionExtended<GameTarget> ME3InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME2InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME1InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();

        public GameBackup ME3Backup { get; set; }
        public GameBackup ME2Backup { get; set; }
        public GameBackup ME1Backup { get; set; }
        public BackupRestoreManager(List<GameTarget> targetsList, GameTarget selectedTarget)
        {
            DataContext = this;
            ME1Backup = new GameBackup(Mod.MEGame.ME1, targetsList.Where(x => x.Game == Mod.MEGame.ME1));
            ME2Backup = new GameBackup(Mod.MEGame.ME2, targetsList.Where(x => x.Game == Mod.MEGame.ME2));
            ME3Backup = new GameBackup(Mod.MEGame.ME3, targetsList.Where(x => x.Game == Mod.MEGame.ME3));

            LoadCommands();
            InitializeComponent();
            //InstallationTargets_ComboBox.SelectedItem = selectedTarget;
        }

        private void LoadCommands()
        {
            //ME3BackupCommand = new GenericCommand(BackupME3, CanBackupME3);
        }


        public event EventHandler<DataEventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(new DataEventArgs());
        }

        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == InstallationTargetsME3_ComboBox)
            {
                ME3Backup.BackupSourceTarget = InstallationTargetsME3_ComboBox.SelectedItem as GameTarget;
            }
        }

        public class GameBackup : INotifyPropertyChanged
        {
            private Mod.MEGame Game;
            public ObservableCollectionExtended<GameTarget> AvailableBackupSources { get; } = new ObservableCollectionExtended<GameTarget>();

            public GameBackup(Mod.MEGame game, IEnumerable<GameTarget> availableBackupSources)
            {
                this.Game = game;
                this.AvailableBackupSources.AddRange(availableBackupSources);
                LoadCommands();
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        break;
                    case Mod.MEGame.ME2:
                        break;
                    case Mod.MEGame.ME3:
                        GameTitle = "Mass Effect 3";
                        GameIconSource = "/images/gameicons/ME3_48.ico";
                        BackupLocation = Utilities.GetGameBackupPath(Mod.MEGame.ME3);
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
                    ProgressVisible = true;
                    ProgressIndeterminate = true;
                    BackupStatus = "Validating backup source";
                    VanillaDatabaseService.LoadDatabaseFor(Mod.MEGame.ME3);
                    bool isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(BackupSourceTarget, nonVanillaFileFoundCallback);
                    if (isVanilla)
                    {
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
                        if (end) { return; }
                        void fileCopiedCallback()
                        {
                            ProgressValue++;
                        }
                        void aboutToCopyCallback(string file)
                        {
                            string f = Path.GetFileName(file);
                            if (f == "Default.sfar")
                            {
                                string foldername = Path.GetFileName(Directory.GetParent(Directory.GetParent(file).FullName).FullName);
                                if (ME3Directory.OfficialDLCNames.TryGetValue(foldername, out var name))
                                {
                                    BackupStatusLine2 = "Backing up " + name;
                                } else
                                {
                                    BackupStatusLine2 = null;
                                }
                            }
                            else
                            {
                                BackupStatusLine2 = null;
                            }
                        }
                        void totalFilesToCopyCallback(int total)
                        {
                            ProgressValue = 0;
                            ProgressIndeterminate = false;
                            ProgressMax = total;
                        }
                        BackupStatus = "Creating backup";

                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(BackupSourceTarget.TargetPath), new DirectoryInfo(backupPath), 
                            totalItemsToCopyCallback: totalFilesToCopyCallback, 
                            aboutToCopyCallback: aboutToCopyCallback, 
                            fileCopiedCallback: fileCopiedCallback, 
                            ignoredExtensions: new string[] { "*.pdf", "*.mp3" });
                        //Todo: Write registry key
                    }
                    else
                    {
                        //Show UI for non vanilla
                    }
                    EndBackup();
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
