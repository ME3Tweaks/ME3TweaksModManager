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
using MassEffectModManagerCore.modmanager.localizations;

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
        public ICommand RemoveTargetCommand { get; set; }

        private void LoadCommands()
        {
            RestoreAllModifiedSFARs = new GenericCommand(RestoreAllSFARs, CanRestoreAllSFARs);
            RestoreAllModifiedBasegame = new GenericCommand(RestoreAllBasegame, CanRestoreAllBasegame);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
            RemoveTargetCommand = new GenericCommand(RemoveTarget, CanRemoveTarget);
        }

        private bool CanRemoveTarget() => SelectedTarget != null && !SelectedTarget.RegistryActive;

        private void RemoveTarget()
        {
            Utilities.RemoveCachedTarget(SelectedTarget);
            OnClosing(new DataEventArgs(@"ReloadTargets"));
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool RestoreAllBasegameInProgress;

        private bool CanRestoreAllBasegame()
        {
            return !Utilities.IsGameRunning(SelectedTarget.Game) && SelectedTarget.ModifiedBasegameFiles.Count > 0 && !RestoreAllBasegameInProgress; //check if ifles being restored
        }

        private void RestoreAllBasegame()
        {
            bool restore = false;


            if (SelectedTarget.ALOTInstalled)
            {
                if (!Settings.DeveloperMode)
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogRestoringFilesWhileAlotIsInstalledNotAllowed), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogRestoringFilesWhileAlotIsInstalledNotAllowedDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoreAllModifiedFilesQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            }
            if (restore)
            {
                NamedBackgroundWorker bw = new NamedBackgroundWorker(@"RestoreAllBasegameFilesThread");
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
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    restore = res == MessageBoxResult.Yes;

                }
            }
            else
            {
                restore = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoreAllModifiedSfarsQuestion), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

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
            return !Utilities.IsGameRunning(SelectedTarget.Game) && SelectedTarget.ModifiedSFARFiles.Count > 0 && !SFARBeingRestored;
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
                    BackupLocationString = M3L.GetString(M3L.string_interp_backupAtX, backupLoc);
                }
                else
                {
                    BackupLocationString = M3L.GetString(M3L.string_noBackupForThisGame);
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
                if (Utilities.IsGameRunning(SelectedTarget.Game))
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_cannotDeleteModsWhileXIsRunning, Utilities.GetGameName(SelectedTarget.Game)), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                if (SelectedTarget.ALOTInstalled)
                {
                    var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_deletingXwhileAlotInstalledUnsupported, mod.ModName), M3L.GetString(M3L.string_deletingWillPutAlotInUnsupportedConfig), MessageBoxButton.YesNo, MessageBoxImage.Error);
                    return res == MessageBoxResult.Yes;
                }
                return M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_removeXFromTheGameInstallationQuestion, mod.ModName), M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }
            void notifyDeleted()
            {
                PopulateUI();
            }
            SelectedTarget.PopulateDLCMods(deleteConfirmationCallback, notifyDeleted);

            bool restoreBasegamefileConfirmationCallback(string filepath)
            {
                if (Utilities.IsGameRunning(SelectedTarget.Game))
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_cannotRestoreFilesWhileXIsRunning, Utilities.GetGameName(SelectedTarget.Game)), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                if (SelectedTarget.ALOTInstalled && filepath.RepresentsPackageFilePath())
                {
                    if (!Settings.DeveloperMode)
                    {
                        M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_restoringXWhileAlotInstalledIsNotAllowed, Path.GetFileName(filepath)), M3L.GetString(M3L.string_cannotRestorePackageFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    else
                    {
                        var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_restoringXwhileAlotInstalledLikelyBreaksThingsDevMode, Path.GetFileName(filepath)), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        return res == MessageBoxResult.Yes;

                    }
                }
                return M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_restoreXquestion, Path.GetFileName(filepath)), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            bool restoreSfarConfirmationCallback(string sfarPath)
            {
                if (Utilities.IsGameRunning(SelectedTarget.Game))
                {
                    M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_cannotRestoreFilesWhileXIsRunning, Utilities.GetGameName(SelectedTarget.Game)), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (SelectedTarget.ALOTInstalled)
                {
                    if (!Settings.DeveloperMode)
                    {
                        M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotBlocked), M3L.GetString(M3L.string_cannotRestoreSfarFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    else
                    {
                        var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_restoringSfarsAlotDevMode), M3L.GetString(M3L.string_invalidTexturePointersWarning), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        return res == MessageBoxResult.Yes;

                    }
                }
                //Todo: warn of unpacked file deletion
                return M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_restoreXquestion, sfarPath), M3L.GetString(M3L.string_confirmRestoration), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
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
            public string EnableDisableText
            {
                get
                {
                    return DLCFolderName.StartsWith("xDLC") ? "Enable" : "Disable";
                }
            }

            public string ModName { get; }
            public string DLCFolderName { get; private set; }
            public string DLCFolderNameString { get; }
            public string InstalledBy { get; }
            public string Version { get; }
            public string InstallerInstanceGUID { get; }
            public string InstallerInstanceBuild { get; }
            private Mod.MEGame game;
            public SolidColorBrush TextColor
            {
                get
                {
                    if (DLCFolderName.StartsWith('x'))
                    {
                        return Brushes.IndianRed;
                    }
                    return null;
                }
            }

            public InstalledDLCMod(string dlcFolderPath, Mod.MEGame game, Func<InstalledDLCMod, bool> deleteConfirmationCallback, Action notifyDeleted)
            {
                this.dlcFolderPath = dlcFolderPath;
                this.game = game;
                DLCFolderName = DLCFolderNameString = Path.GetFileName(dlcFolderPath);
                DLCFolderNameString = DLCFolderNameString.TrimStart('x'); //this string is not to show "Disabled"
                if (App.ThirdPartyIdentificationService[game.ToString()].TryGetValue(DLCFolderName, out var tpmi))
                {
                    ModName = tpmi.modname;
                }
                else
                {
                    ModName = DLCFolderName;
                }
                var metaFile = Path.Combine(dlcFolderPath, @"_metacmm.txt");
                if (File.Exists(metaFile))
                {
                    InstalledBy = M3L.GetString(M3L.string_installedByModManager); //Default value when finding metacmm.
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
                                    DLCFolderNameString += $@" ({ModName})";
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
                                    InstalledBy = M3L.GetString(M3L.string_installedByModManager);
                                }
                                else
                                {
                                    InstalledBy = M3L.GetString(M3L.string_interp_installedByX, InstallerInstanceBuild);
                                }
                                break;
                            case 3:
                                InstallerInstanceGUID = line;
                                break;
                            default:
                                Log.Error($@"Unsupported line number in _metacmm.txt: {i}");
                                break;
                        }
                        i++;
                    }
                }
                else
                {
                    InstalledBy = M3L.GetString(M3L.string_notInstalledByModManager);
                }
                this.deleteConfirmationCallback = deleteConfirmationCallback;
                this.notifyDeleted = notifyDeleted;
                DeleteCommand = new RelayCommand(DeleteDLCMod, CanDeleteDLCMod);
                EnableDisableCommand = new GenericCommand(ToggleDLC, CanToggleDLC);

            }

            private void ToggleDLC()
            {
                var source = dlcFolderPath;
                var dlcdir = Directory.GetParent(dlcFolderPath).FullName;
                var newdlcname = DLCFolderName.StartsWith("xDLC") ? DLCFolderName.TrimStart('x') : "x" + DLCFolderName;
                var target = Path.Combine(dlcdir, newdlcname);
                try
                {
                    Directory.Move(source, target);
                    DLCFolderName = newdlcname;
                    dlcFolderPath = target;
                }
                catch (Exception e)
                {
                    Log.Error("Unable to toggle DLC: " + e.Message);
                }
                //TriggerPropertyChangedFor(nameof(DLCFolderName));
            }
            private void TriggerPropertyChangedFor(string propertyname)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
            }
            private bool CanToggleDLC() => !Utilities.IsGameRunning(game);

            private Func<InstalledDLCMod, bool> deleteConfirmationCallback;
            private Action notifyDeleted;

            public ICommand DeleteCommand { get; set; }
            public GenericCommand EnableDisableCommand { get; set; }
            private bool CanDeleteDLCMod(object obj) => !Utilities.IsGameRunning(game);

            private void DeleteDLCMod(object obj)
            {
                if (obj is GameTarget gt)
                {
                    var confirmDelete = deleteConfirmationCallback?.Invoke(this);
                    if (confirmDelete.HasValue && confirmDelete.Value)
                    {
                        Log.Information(@"Deleting DLC mod from target: " + dlcFolderPath);
                        Utilities.DeleteFilesAndFoldersRecursively(dlcFolderPath);
                        notifyDeleted?.Invoke();
                    }
                }
            }
        }


        private void OpenALOTInstaller_Click(object sender, RequestNavigateEventArgs e)
        {
            OnClosing(new DataEventArgs(@"ALOTInstaller"));
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
