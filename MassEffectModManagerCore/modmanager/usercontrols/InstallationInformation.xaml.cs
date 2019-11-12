using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Serilog;

using MassEffectModManagerCore.modmanager.helpers;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for InstallationInformation.xaml
    /// </summary>
    public partial class InstallationInformation : MMBusyPanelBase
    {
        public string ALOTStatusString { get; set; }
        public GameTarget SelectedTarget { get; set; }
        public string BackupLocationString { get; set; }
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<InstalledDLCMod> DLCModsInstalled { get; } = new ObservableCollectionExtended<InstalledDLCMod>();
        public bool SFARBeingRestored { get; private set; }
        public bool BasegameFilesBeingRestored { get; private set; }

        public InstallationInformation(List<GameTarget> targetsList, GameTarget selectedTarget)
        {
            DataContext = this;
            InstallationTargets.AddRange(targetsList);
            LoadCommands();
            InitializeComponent();
            SelectedTarget = selectedTarget;
        }
        public ICommand RestoreAllModifiedSFARs { get; set; }
        public ICommand RestoreAllModifiedBasegame { get; set; }
        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            RestoreAllModifiedSFARs = new GenericCommand(RestoreAllSFARs, CanRestoreAllSFARs);
            RestoreAllModifiedBasegame = new GenericCommand(RestoreAllBasegame, CanRestoreAllBasegame);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool RestoreAllBasegameInProgress;

        private bool CanRestoreAllBasegame()
        {
            return SelectedTarget.ModifiedBasegameFiles.Count > 0 && !RestoreAllBasegameInProgress; //check if ifles being restored
        }

        private void RestoreAllBasegame()
        {
            bool restore = false;
            if (SelectedTarget.ALOTInstalled)
            {
                if (!Settings.DeveloperMode)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring files while ALOT is installed is not allowed, as it will introduce invalid texture pointers into the installation.", $"Cannot restore SFAR files", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring files while ALOT is installed will introduce invalid texture pointers into the installation, which will cause black textures and possibly cause the game to crash. Please ensure you know what you are doing before continuing.", $"Invalid texture pointers warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restore all modified files?", $"Confirm restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                NamedBackgroundWorker bw = new NamedBackgroundWorker("RestoreAllBasegameFilesThread");
                bw.DoWork += (a, b) =>
                {
                    RestoreAllBasegameInProgress = true;
                    foreach (var v in SelectedTarget.ModifiedBasegameFiles)
                    {
                        v.Restoring = true;
                    }
                    foreach (var v in SelectedTarget.ModifiedBasegameFiles.ToList()) //to list will make sure this doesn't throw concurrent modification
                    {
                        v.RestoreFile(true);
                    }


                };
                bw.RunWorkerCompleted += (a, b) =>
                {
                    RestoreAllBasegameInProgress = false;
                    CommandManager.InvalidateRequerySuggested();
                };
                bw.RunWorkerAsync();
            }
        }

        private void RestoreAllSFARs()
        {
            bool restore = false;
            if (SelectedTarget.ALOTInstalled)
            {
                if (!Settings.DeveloperMode)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring SFAR files while ALOT is installed is not allowed, as it will introduce invalid texture pointers into the installation.", $"Cannot restore SFAR files", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring SFARs while ALOT is installed will introduce invalid texture pointers into the installation, which will cause black textures and possibly cause the game to crash. This operation will also delete all unpacked files from the directory. Please ensure you know what you are doing before continuing.", $"Invalid texture pointers warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restore all modified SFARs?", $"Confirm restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                foreach (var v in SelectedTarget.ModifiedSFARFiles)
                {
                    v.RestoreSFAR(true);
                }
            }
        }

        private bool CanRestoreAllSFARs()
        {
            return SelectedTarget.ModifiedSFARFiles.Count > 0 && !SFARBeingRestored;
        }

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            DLCModsInstalled.ClearEx();

            //Get installed mod information
            if (e.AddedItems.Count > 0)
            {
                SelectedTarget = e.AddedItems[0] as GameTarget;
                PopulateUI();
                var backupLoc = Utilities.GetGameBackupPath(SelectedTarget.Game);
                if (backupLoc != null)
                {
                    BackupLocationString = "Backup at " + backupLoc;
                }
                else
                {
                    BackupLocationString = "No backup for this game";
                }
            }
            else
            {
                SelectedTarget = null;
                BackupLocationString = null;
            }
        }



        private void PopulateUI()
        {
            bool deleteConfirmationCallback(InstalledDLCMod mod)
            {
                if (SelectedTarget.ALOTInstalled)
                {
                    var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Deleting {mod.ModName} while ALOT is installed will not cause the game to become broken, however you will not be able to install updates to ALOT without a full reinstallation (unsupported configuration).\n\nAre you sure you want to delete the DLC mod?", $"Deleting will put ALOT in unsupported configuration", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    return res == MessageBoxResult.Yes;
                }
                return Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Remove {mod.ModName} from the game installation?", $"Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }
            void notifyDeleted()
            {
                PopulateUI();
            }
            SelectedTarget.PopulateDLCMods(deleteConfirmationCallback, notifyDeleted);

            bool restoreBasegamefileConfirmationCallback(string filepath)
            {
                if (SelectedTarget.ALOTInstalled && filepath.RepresentsPackageFilePath())
                {
                    if (!Settings.DeveloperMode)
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring {Path.GetFileName(filepath)} while ALOT is installed is not allowed, as it will introduce invalid texture pointers into the installation.", $"Cannot restore package files", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    else
                    {
                        var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring {Path.GetFileName(filepath)} while ALOT is installed will very likely introduce invalid texture pointers into the installation, which may cause black textures or game crashes due to empty mips. Please ensure you know what you are doing before continuing.", $"Invalid texture pointers warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        return res == MessageBoxResult.Yes;

                    }
                }
                return Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restore {Path.GetFileName(filepath)}?", $"Confirm restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            bool restoreSfarConfirmationCallback(string sfarPath)
            {
                if (SelectedTarget.ALOTInstalled)
                {
                    if (!Settings.DeveloperMode)
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring SFAR files while ALOT is installed is not allowed, as it will introduce invalid texture pointers into the installation.", $"Cannot restore SFAR files", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    else
                    {
                        var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restoring SFARs while ALOT is installed will introduce invalid texture pointers into the installation, which will cause black textures and possibly cause the game to crash. This operation will also delete all unpacked files from the directory. Please ensure you know what you are doing before continuing.", $"Invalid texture pointers warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        return res == MessageBoxResult.Yes;

                    }
                }
                //Todo: warn of unpacked file deletion
                return Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"Restore {sfarPath}?", $"Confirm restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            void notifyStartingSfarRestoreCallback()
            {
                SFARBeingRestored = true;
            }

            void notifyStartingBasegameFileRestoreCallback()
            {
                BasegameFilesBeingRestored = true;
            }

            void notifyRestoredCallback(object itemRestored)
            {
                if (itemRestored is GameTarget.ModifiedFileObject mf)
                {
                    Application.Current.Dispatcher.Invoke(delegate { SelectedTarget.ModifiedBasegameFiles.Remove(mf); });
                    bool resetBasegameFilesBeingRestored = SelectedTarget.ModifiedBasegameFiles.Count == 0;
                    if (!resetBasegameFilesBeingRestored)
                    {
                        resetBasegameFilesBeingRestored = !SelectedTarget.ModifiedBasegameFiles.Any(x => x.Restoring);
                    }
                    if (resetBasegameFilesBeingRestored)
                    {
                        BasegameFilesBeingRestored = false;
                    }
                }
                else if (itemRestored is GameTarget.SFARObject ms)
                {
                    if (!ms.IsModified)
                    {
                        SelectedTarget.ModifiedSFARFiles.Remove(ms);
                    }
                    bool resetSfarsBeingRestored = SelectedTarget.ModifiedSFARFiles.Count == 0;
                    if (!resetSfarsBeingRestored)
                    {
                        resetSfarsBeingRestored = !SelectedTarget.ModifiedSFARFiles.Any(x => x.Restoring);
                    }
                    if (resetSfarsBeingRestored)
                    {
                        SFARBeingRestored = false;
                    }
                }
                else if (itemRestored == null)
                {
                    //restore failed.
                    bool resetSfarsBeingRestored = SelectedTarget.ModifiedSFARFiles.Count == 0;
                    if (!resetSfarsBeingRestored)
                    {
                        resetSfarsBeingRestored = !SelectedTarget.ModifiedSFARFiles.Any(x => x.Restoring);
                    }
                    if (resetSfarsBeingRestored)
                    {
                        SFARBeingRestored = false;
                    }
                    bool resetBasegameFilesBeingRestored = SelectedTarget.ModifiedBasegameFiles.Count == 0;
                    if (!resetBasegameFilesBeingRestored)
                    {
                        resetBasegameFilesBeingRestored = !SelectedTarget.ModifiedBasegameFiles.Any(x => x.Restoring);
                    }
                    if (resetBasegameFilesBeingRestored)
                    {
                        BasegameFilesBeingRestored = false;
                    }
                }
            }
            SelectedTarget.PopulateModifiedBasegameFiles(restoreBasegamefileConfirmationCallback,
                restoreSfarConfirmationCallback,
                notifyStartingSfarRestoreCallback,
                notifyStartingBasegameFileRestoreCallback,
                notifyRestoredCallback);
            SFARBeingRestored = false;
        }

        public class InstalledDLCMod : INotifyPropertyChanged
        {
            private string dlcFolderPath;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ModName { get; }
            public string DLCFolderName { get; }
            public string DLCFolderNameString { get; }
            public string InstalledBy { get; }
            public string Version { get; }
            public string InstallerInstanceGUID { get; }
            public string InstallerInstanceBuild { get; }

            public InstalledDLCMod(string dlcFolderPath, Mod.MEGame game, Func<InstalledDLCMod, bool> deleteConfirmationCallback, Action notifyDeleted)
            {
                this.dlcFolderPath = dlcFolderPath;
                DLCFolderName = DLCFolderNameString = Path.GetFileName(dlcFolderPath);
                if (App.ThirdPartyIdentificationService[game.ToString()].TryGetValue(DLCFolderName, out var tpmi))
                {
                    ModName = tpmi.modname;
                }
                else
                {
                    ModName = DLCFolderName;
                }
                var metaFile = Path.Combine(dlcFolderPath, "_metacmm.txt");
                if (File.Exists(metaFile))
                {
                    InstalledBy = "Installed by Mod Manager"; //Default value when finding metacmm.
                    //Parse MetaCMM
                    var lines = File.ReadAllLines(metaFile).ToList();
                    int i = 0;
                    //This is a weird way of doing it but it helps ensure backwards compatiblity and forwards compatibility.
                    foreach (var line in lines)
                    {
                        switch (i)
                        {
                            case 0:
                                if (line != ModName)
                                {
                                    DLCFolderNameString += $" ({ModName})";
                                    ModName = line;
                                }
                                break;
                            case 1:
                                Version = line;
                                break;
                            case 2:
                                InstallerInstanceBuild = line;
                                if (int.TryParse(InstallerInstanceBuild, out var mmver))
                                {
                                    InstalledBy = "Installed by Mod Manager";
                                }
                                else
                                {
                                    InstalledBy = "Installed by " + InstallerInstanceBuild;
                                }
                                break;
                            case 3:
                                InstallerInstanceGUID = line;
                                break;
                            default:
                                Log.Error("Unsupported line number in _metacmm.txt: " + i);
                                break;
                        }
                        i++;
                    }
                }
                else
                {
                    InstalledBy = "Not installed by Mod Manager";
                }
                this.deleteConfirmationCallback = deleteConfirmationCallback;
                this.notifyDeleted = notifyDeleted;
                DeleteCommand = new RelayCommand(DeleteDLCMod, CanDeleteDLCMod);

            }

            private Func<InstalledDLCMod, bool> deleteConfirmationCallback;
            private Action notifyDeleted;

            public ICommand DeleteCommand { get; set; }
            private bool CanDeleteDLCMod(object obj) => true;

            private void DeleteDLCMod(object obj)
            {
                if (obj is GameTarget gt)
                {
                    var confirmDelete = deleteConfirmationCallback?.Invoke(this);
                    if (confirmDelete.HasValue && confirmDelete.Value)
                    {
                        Log.Information("Deleting DLC mod from target: " + dlcFolderPath);
                        Utilities.DeleteFilesAndFoldersRecursively(dlcFolderPath);
                        notifyDeleted?.Invoke();
                    }
                }
            }
        }


        private void OpenALOTInstaller_Click(object sender, RequestNavigateEventArgs e)
        {
            OnClosing(new DataEventArgs("ALOTInstaller"));
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenExplorer(SelectedTarget.TargetPath);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        private bool CanClose()
        {
            return !SFARBeingRestored && !BasegameFilesBeingRestored;
        }

        public override void OnPanelVisible()
        {
        }
    }
}
