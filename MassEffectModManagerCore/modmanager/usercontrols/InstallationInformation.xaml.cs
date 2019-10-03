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
                if (SelectedTarget.ALOTInstalled)
                {
                    ALOTStatusString = "A Lot Of Textures (ALOT) is installed\nVersion " + SelectedTarget.ALOTVersion;
                }
                else
                {
                    ALOTStatusString = "A Lot Of Textures (ALOT) is not installed";
                }
                var dlcDir = MEDirectories.DLCPath(SelectedTarget);
                var installedMods = MEDirectories.GetInstalledDLC(SelectedTarget).Where(x => !MEDirectories.OfficialDLC(SelectedTarget.Game).Contains(x, StringComparer.InvariantCultureIgnoreCase)).Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), SelectedTarget.Game)).ToList();
                DLCModsInstalled.AddRange(installedMods);
            } else
            {
                SelectedTarget = null;
            }
        }

        public class InstalledDLCMod
        {
            private string dlcFolderPath;
            public string ModName { get; }
            public string DLCFolderName { get; }
            public string InstalledBy { get; }
            public string Version { get; }

            public InstalledDLCMod(string dlcFolderPath, Mod.MEGame game)
            {
                this.dlcFolderPath = dlcFolderPath;
                DLCFolderName = Path.GetFileName(dlcFolderPath);
                if (App.ThirdPartyIdentificationService[game.ToString()].TryGetValue(DLCFolderName, out var tpmi))
                {
                    ModName = tpmi.modname;
                    var metaFile = Path.Combine(dlcFolderPath, "_metacmm.txt");
                    if (File.Exists(metaFile))
                    {

                        InstalledBy = "Installed by Mod Manager";
                    }
                    else
                    {
                        InstalledBy = "Not installed by Mod Manager";
                    }
                }
                else
                {
                    ModName = DLCFolderName;
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
