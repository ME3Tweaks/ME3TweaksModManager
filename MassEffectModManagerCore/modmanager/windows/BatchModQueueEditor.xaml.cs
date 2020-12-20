using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Packages;
using Microsoft.AppCenter.Analytics;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    public partial class BatchModQueueEditor : Window, INotifyPropertyChanged
    {
        private List<Mod> allMods;
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<Mod> ModsInGroup { get; } = new ObservableCollectionExtended<Mod>();
        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        private string existingFilename;
        /// <summary>
        /// Then newly saved path, for showing in the calling window's UI
        /// </summary>
        public string SavedPath;

        public BatchModQueueEditor(List<Mod> allMods, Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Queue Editor", new WeakReference(this));
            Owner = owner;
            DataContext = this;
            this.allMods = allMods;
            LoadCommands();
            InitializeComponent();
            if (queueToEdit != null)
            {
                existingFilename = queueToEdit.BackingFilename;
                SetGameRadioUI(queueToEdit.Game);
                SelectedGame = queueToEdit.Game;
                GroupName = queueToEdit.QueueName;
                GroupDescription = queueToEdit.QueueDescription;
                ModsInGroup.ReplaceAll(queueToEdit.ModsToInstall);
                VisibleFilteredMods.RemoveRange(queueToEdit.ModsToInstall);
            }
        }

        private void SetGameRadioUI(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    ME1_RadioButton.IsChecked = true;
                    break;
                case MEGame.ME2:
                    ME2_RadioButton.IsChecked = true;
                    break;
                case MEGame.ME3:
                    ME3_RadioButton.IsChecked = true;
                    break;
            }
        }

        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }
        public ICommand RemoveFromInstallGroupCommand { get; set; }
        public ICommand AddToInstallGroupCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
            RemoveFromInstallGroupCommand = new GenericCommand(RemoveFromInstallGroup, CanRemoveFromInstallGroup);
            AddToInstallGroupCommand = new GenericCommand(AddToInstallGroup, CanAddToInstallGroup);
            MoveUpCommand = new GenericCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new GenericCommand(MoveDown, CanMoveDown);
        }

        private bool CanRemoveFromInstallGroup() => SelectedInstallGroupMod != null;

        private bool CanAddToInstallGroup() => SelectedAvailableMod != null;

        private bool CanMoveUp()
        {
            if (SelectedInstallGroupMod != null)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanMoveDown()
        {

            if (SelectedInstallGroupMod != null)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < ModsInGroup.Count - 1)
                {
                    return true;
                }
            }
            return false;
        }

        private void MoveDown()
        {
            if (SelectedInstallGroupMod != null)
            {
                var mod = SelectedInstallGroupMod;
                var oldIndex = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                var newIndex = oldIndex + 1;
                ModsInGroup.RemoveAt(oldIndex);
                ModsInGroup.Insert(newIndex, mod);
                SelectedInstallGroupMod = mod;
            }
        }

        private void MoveUp()
        {
            if (SelectedInstallGroupMod != null)
            {
                var mod = SelectedInstallGroupMod;
                var oldIndex = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                var newIndex = oldIndex - 1;
                ModsInGroup.RemoveAt(oldIndex);
                ModsInGroup.Insert(newIndex, mod);
                SelectedInstallGroupMod = mod;
            }
        }

        private void AddToInstallGroup()
        {
            Mod m = SelectedAvailableMod;
            if (VisibleFilteredMods.Remove(m))
            {
                ModsInGroup.Add(m);
            }
        }

        private void RemoveFromInstallGroup()
        {
            Mod m = SelectedInstallGroupMod;
            if (ModsInGroup.Remove(m))
            {
                VisibleFilteredMods.Add(m);
            }
        }

        private void SaveAndClose()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(SelectedGame.ToString());
            sb.AppendLine(GroupName);
            sb.AppendLine(Utilities.ConvertNewlineToBr(GroupDescription));
            var libraryRoot = Utilities.GetModDirectoryForGame(SelectedGame);
            foreach (var m in ModsInGroup)
            {
                sb.AppendLine(m.ModDescPath.Substring(libraryRoot.Length + 1)); //STORE RELATIVE!
            }

            var batchfolder = Utilities.GetBatchInstallGroupsFolder();
            if (existingFilename != null)
            {
                var existingPath = Path.Combine(batchfolder, existingFilename);
                if (File.Exists(existingPath))
                {
                    File.Delete(existingPath);
                }
            }

            var savePath = getSaveName(GroupName);
            File.WriteAllText(savePath, sb.ToString());
            SavedPath = savePath;
            Analytics.TrackEvent(@"Saved Batch Group", new Dictionary<string, string>()
            {
                {@"Group name", GroupName},
                {@"Group size", ModsInGroup.Count.ToString()},
                {@"Game", SelectedGame.ToString()}
            });
            Close();
            //OnClosing(new DataEventArgs(savePath));
        }

        private string getSaveName(string groupName)
        {
            var batchfolder = Utilities.GetBatchInstallGroupsFolder();
            var newFname = Utilities.SanitizePath(groupName);
            if (string.IsNullOrWhiteSpace(newFname))
            {
                return getFirstGenericSavename(batchfolder);
            }
            var newPath = Path.Combine(batchfolder, newFname) + @".biq";
            if (File.Exists(newPath))
            {
                return getFirstGenericSavename(batchfolder);
            }

            return newPath;
        }

        private string getFirstGenericSavename(string batchfolder)
        {
            string newFname = Path.Combine(batchfolder, @"batchinstaller-");
            int i = 0;
            while (true)
            {
                i++;
                string nextGenericPath = newFname + i + @".biq";
                if (!File.Exists(nextGenericPath))
                {
                    return nextGenericPath;
                }
            }
        }

        private bool CanSave() => ModsInGroup.Any() && !string.IsNullOrWhiteSpace(GroupName) && !string.IsNullOrWhiteSpace(GroupDescription);

        private void CancelEditing()
        {
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public MEGame SelectedGame { get; set; }
        public Mod SelectedInstallGroupMod { get; set; }
        public Mod SelectedAvailableMod { get; set; }

        public void OnSelectedGameChanged()
        {
            if (SelectedGame != MEGame.Unknown)
            {
                VisibleFilteredMods.ReplaceAll(allMods.Where(x => x.Game == SelectedGame));
            }
            else
            {
                VisibleFilteredMods.ClearEx();
            }
        }

        private void ME1_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.ME1);
        }
        private void ME2_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.ME2);
        }
        private void ME3_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.ME3);
        }

        private void TryChangeGameTo(MEGame newgame)
        {
            if (newgame == SelectedGame) return; //don't care
            if (ModsInGroup.Count > 0 && newgame != SelectedGame)
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_changingGameWillClearGroup), M3L.GetString(M3L.string_changingGameWillClearGroup), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    ModsInGroup.ClearEx();
                    SelectedGame = newgame;
                }
                else
                {
                    //reset choice
                    SetGameRadioUI(SelectedGame); //reset back
                }
            }
            else
            {
                SelectedGame = newgame;
            }

        }
    }
}
