using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using PropertyChanged;
using WinCopies.Util;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class BatchModQueueEditor : Window
    {
        private List<Mod> allMods;
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<Mod> ModsInGroup { get; } = new ObservableCollectionExtended<Mod>();

        public MEGameSelector[] Games { get; init; }

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
            Games = MEGameSelector.GetGameSelectorsIncludingLauncher().ToArray();

            InitializeComponent();
            if (queueToEdit != null)
            {
                existingFilename = queueToEdit.BackingFilename;
                SelectedGame = queueToEdit.Game;
                GroupName = queueToEdit.QueueName;
                GroupDescription = queueToEdit.QueueDescription;
                ModsInGroup.ReplaceAll(queueToEdit.ModsToInstall);
                VisibleFilteredMods.RemoveRange(queueToEdit.ModsToInstall);
            }
        }


        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }
        public ICommand RemoveFromInstallGroupCommand { get; set; }
        public ICommand AddToInstallGroupCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }
        public ICommand AutosortCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
            RemoveFromInstallGroupCommand = new GenericCommand(RemoveFromInstallGroup, CanRemoveFromInstallGroup);
            AddToInstallGroupCommand = new GenericCommand(AddToInstallGroup, CanAddToInstallGroup);
            MoveUpCommand = new GenericCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new GenericCommand(MoveDown, CanMoveDown);
            AutosortCommand = new GenericCommand(Autosort, CanAutosort);
        }

        private bool CanAutosort()
        {
            return ModsInGroup.Count > 1;
        }

        class ModDependencies
        {

            public List<string> HardDependencies = new List<string>(); // requireddlc

            /// <summary>
            /// List of DLC folders the mod can depend on for configuration.
            /// </summary>
            public List<string> DependencyDLCs = new List<string>();

            /// <summary>
            /// List of all folders the mod mapped to this can install
            /// </summary>
            public List<string> InstallableDLCFolders = new List<string>();

            /// <summary>
            /// The mod associated with this dependencies
            /// </summary>
            public Mod mod;

            public void DebugPrint()
            {
                Debug.WriteLine($@"{mod.ModName}");
                Debug.WriteLine($@"  HARD DEPENDENCIES: {string.Join(',', HardDependencies)}");
                Debug.WriteLine($@"  SOFT DEPENDENCIES: {string.Join(',', DependencyDLCs)}");
                Debug.WriteLine($@"  DLC FOLDERS:       {string.Join(',', InstallableDLCFolders)}");
            }
        }

        private void Autosort()
        {
            // DOESN'T REALLY WORK!!!!!!!!
            // Just leaving here in the event that someday it becomes useful...

#if DEBUG
            // This attempts to order mods by dependencies on others, with mods that have are not depended on being installed first
            // This REQUIRES mod developers to properly flag their alternates!

            var dependencies = new List<ModDependencies>();

            foreach (var mod in ModsInGroup)
            {
                var depends = new ModDependencies();

                // These items MUST be installed first or this mod simply won't install.
                // Official DLC is not counted as mod manager cannot install those.
                depends.HardDependencies = mod.RequiredDLC.Where(x => !MEDirectories.OfficialDLC(mod.Game).Contains(x)).ToList();
                depends.DependencyDLCs = mod.GetAutoConfigs().ToList(); // These items must be installed prior to install or options will be unavailable to the user.
                var custDlcJob = mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (custDlcJob != null)
                {
                    var customDLCFolders = custDlcJob.CustomDLCFolderMapping.Keys.ToList();
                    customDLCFolders.AddRange(custDlcJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
                    depends.InstallableDLCFolders.ReplaceAll(customDLCFolders);
                }

                depends.mod = mod;
                dependencies.Add(depends);
            }

            var fullList = dependencies;

            List<Mod> finalOrder = new List<Mod>();

            // Mods with no dependencies go first.
            var noDependencyMods = dependencies.Where(x => x.HardDependencies.Count == 0 && x.DependencyDLCs.Count == 0).ToList();
            finalOrder.AddRange(noDependencyMods.Select(x => x.mod));
            dependencies = dependencies.Except(noDependencyMods).ToList(); // Remove the added items

            // Mods that are marked as requireddlc in other mods go next. 
            var requiredDlcs = dependencies.SelectMany(x => x.HardDependencies).ToList();
            var modsHardDependedOn = dependencies.Where(x => x.DependencyDLCs.Intersect(requiredDlcs).Any());


            finalOrder.AddRange(modsHardDependedOn);
            dependencies = dependencies.Except(modsHardDependedOn).ToList(); // Remove the added items

            // Add the rest (TEMP)
            finalOrder.AddRange(dependencies.Select(x => x.mod));


            // DEBUG: PRINT IT OUT

            foreach (var m in finalOrder)
            {
                var depend = fullList.Find(x => x.mod == m);
                depend.DebugPrint();
            }

            ModsInGroup.ReplaceAll(finalOrder);
#endif
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

        // Hack. m)ove somewhere more useful
        public static bool ContainsAny(string s, List<string> substrings)
        {
            if (string.IsNullOrEmpty(s) || substrings == null)
                return false;

            return substrings.Any(substring => s.Contains(substring, StringComparison.CurrentCultureIgnoreCase));
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
            sb.AppendLine(M3Utilities.ConvertNewlineToBr(GroupDescription));
            var libraryRoot = M3Utilities.GetModDirectoryForGame(SelectedGame);
            foreach (var m in ModsInGroup)
            {
                sb.AppendLine(m.ModDescPath.Substring(libraryRoot.Length + 1)); //STORE RELATIVE!
            }

            var batchfolder = M3Filesystem.GetBatchInstallGroupsFolder();
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
            var batchfolder = M3Filesystem.GetBatchInstallGroupsFolder();
            var newFname = M3Utilities.SanitizePath(groupName);
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

        public MEGame SelectedGame { get; set; }
        public Mod SelectedInstallGroupMod { get; set; }
        public Mod SelectedAvailableMod { get; set; }

        public void OnSelectedGameChanged()
        {
            // Set the selector
            foreach (var selector in Games)
            {
                selector.IsSelected = selector.Game == SelectedGame;
            }

            // Update the filtered list
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
                    Games.ForEach(x => x.IsSelected = x.Game == SelectedGame);
                }
            }
            else
            {
                SelectedGame = newgame;
            }

        }

        private void LE1_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.LE1);
        }

        private void LE2_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.LE2);
        }

        private void LE3_Clicked(object sender, RoutedEventArgs e)
        {
            TryChangeGameTo(MEGame.LE3);
        }

        private void GameIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fw && fw.DataContext is MEGameSelector gamesel)
            {
                SetSelectedGame(gamesel.Game);
            }
        }

        private void SetSelectedGame(MEGame game)
        {
            Games.ForEach(x => x.IsSelected = x.Game == game);
            TryChangeGameTo(game);
        }
    }
}
