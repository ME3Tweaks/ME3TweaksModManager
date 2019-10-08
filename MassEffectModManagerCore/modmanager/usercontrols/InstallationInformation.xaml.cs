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
using MassEffectModManager;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for InstallationInformation.xaml
    /// </summary>
    public partial class InstallationInformation : UserControl, INotifyPropertyChanged
    {
        public string ALOTStatusString { get; set; }
        public GameTarget SelectedTarget { get; set; }
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<InstalledDLCMod> DLCModsInstalled { get; } = new ObservableCollectionExtended<InstalledDLCMod>();
        public InstallationInformation(List<GameTarget> targetsList, GameTarget selectedTarget)
        {
            DataContext = this;
            InstallationTargets.AddRange(targetsList);
            LoadCommands();
            InitializeComponent();
            InstallationTargets_ComboBox.SelectedItem = selectedTarget;
        }

        private void LoadCommands()
        {

        }

        public event EventHandler<DataEventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            DLCModsInstalled.ClearEx();

            //Get installed mod information
            if (e.AddedItems.Count > 0)
            {
                SelectedTarget = e.AddedItems[0] as GameTarget;
                PopulateUI();
            }
            else
            {
                SelectedTarget = null;
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
            SelectedTarget.PopulateModifiedBasegameFiles();
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
                    InstalledBy = "Installed by Mod Manager";
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            OnClosing(new DataEventArgs());
        }
    }
}
