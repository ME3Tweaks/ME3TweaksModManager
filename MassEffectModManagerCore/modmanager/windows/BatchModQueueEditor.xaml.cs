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
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Newtonsoft.Json;
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
        // Tab constants
        private const int TAB_CONTENTMOD = 0;
        private const int TAB_ASIMOD = 1;
        private const int TAB_TEXTUREMOD = 2;

        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<ASIMod> VisibleFilteredASIMods { get; } = new ObservableCollectionExtended<ASIMod>();
        public ObservableCollectionExtended<MEMMod> VisibleFilteredMEMMods { get; } = new ObservableCollectionExtended<MEMMod>();

        /// <summary>
        /// Contains both ASI (BatchASIMod) and Content mods (BatchMod)
        /// </summary>
        public ObservableCollectionExtended<object> ModsInGroup { get; } = new ObservableCollectionExtended<object>();

        public MEGameSelector[] Games { get; init; }

        public string AvailableModText { get; set; }

        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        private string existingFilename;
        /// <summary>
        /// Then newly saved path, for showing in the calling window's UI
        /// </summary>
        public string SavedPath;

        public BatchModQueueEditor(Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Queue Editor", new WeakReference(this));
            Owner = owner;
            DataContext = this;
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
                ModsInGroup.AddRange(queueToEdit.ASIModsToInstall);
                VisibleFilteredMods.RemoveRange(queueToEdit.ModsToInstall.Select(x => x.Mod));
                VisibleFilteredASIMods.RemoveRange(queueToEdit.ASIModsToInstall.Select(x => x.AssociatedMod?.OwningMod));
            }
        }

        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }
        public ICommand RemoveFromInstallGroupCommand { get; set; }
        public ICommand AddToInstallGroupCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }
        public ICommand AutosortCommand { get; set; }
        public ICommand AddCustomMEMModCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
            RemoveFromInstallGroupCommand = new GenericCommand(RemoveContentModFromInstallGroup, CanRemoveFromInstallGroup);
            AddToInstallGroupCommand = new GenericCommand(AddModToInstallGroup, CanAddToInstallGroup);
            MoveUpCommand = new GenericCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new GenericCommand(MoveDown, CanMoveDown);
            AutosortCommand = new GenericCommand(Autosort, CanAutosort);
            AddCustomMEMModCommand = new GenericCommand(ShowMEMSelector, CanAddMEMMod);
        }

        private void ShowMEMSelector()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "MassEffectModder files (*.mem)|*.mem",
                Title = "Select .mem file",
            };

            var result = ofd.ShowDialog();
            if (result == true)
            {
                // User selected file
                MEMMod m = new MEMMod()
                {
                    FilePath = ofd.FileName // Todo: Figure out relative pathing
                };

                m.ParseData();

                // Todo: Make sure it's for the current game

                VisibleFilteredMEMMods.Add(m); //Todo: Check no duplicates in left list (or existing already on right?)
            }
        }

        private bool CanAddMEMMod()
        {
            return true;
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

            /*var dependencies = new List<ModDependencies>();

            foreach (var mod in ModsInGroup)
            {
                var depends = new ModDependencies();

                // These items MUST be installed first or this mod simply won't install.
                // Official DLC is not counted as mod manager cannot install those.
                depends.HardDependencies = mod.Mod.RequiredDLC.Where(x => !MEDirectories.OfficialDLC(mod.Game).Contains(x.DLCFolderName)).Select(x => x.DLCFolderName).ToList();
                depends.DependencyDLCs = mod.Mod.GetAutoConfigs().ToList(); // These items must be installed prior to install or options will be unavailable to the user.
                var custDlcJob = mod.Mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (custDlcJob != null)
                {
                    var customDLCFolders = custDlcJob.CustomDLCFolderMapping.Keys.ToList();
                    customDLCFolders.AddRange(custDlcJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
                    depends.InstallableDLCFolders.ReplaceAll(customDLCFolders);
                }

                depends.mod = mod.Mod;
                dependencies.Add(depends);
            }

            var fullList = dependencies;

            var finalOrder = new List<BatchMod>();

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
                var depend = fullList.Find(x => x.mod == m.Mod);
                depend.DebugPrint();
            }

            ModsInGroup.ReplaceAll(finalOrder);*/
#endif
        }

        private bool CanRemoveFromInstallGroup() => SelectedInstallGroupMod != null;

        private bool CanAddToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) return SelectedAvailableMod != null;
            if (SelectedTabIndex == TAB_ASIMOD) return SelectedAvailableASIMod != null;
            return false;
        }

        private bool CanMoveUp()
        {
            if (SelectedInstallGroupMod is BatchMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0 && ModsInGroup[index - 1] is BatchMod)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanMoveDown()
        {
            if (SelectedInstallGroupMod is BatchMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < ModsInGroup.Count - 1 && ModsInGroup[index + 1] is BatchMod)
                {
                    return true;
                }
            }
            return false;
        }

        private void MoveDown()
        {
            if (SelectedInstallGroupMod is BatchMod)
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
            if (SelectedInstallGroupMod is BatchMod)
            {
                var mod = SelectedInstallGroupMod;
                var oldIndex = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                var newIndex = oldIndex - 1;
                ModsInGroup.RemoveAt(oldIndex);
                ModsInGroup.Insert(newIndex, mod);
                SelectedInstallGroupMod = mod;
            }
        }

        private void AddModToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD)
            {
                Mod m = SelectedAvailableMod;
                if (VisibleFilteredMods.Remove(m))
                {
                    ModsInGroup.Add(new BatchMod(m));
                }
            }
            else if (SelectedTabIndex == TAB_ASIMOD)
            {
                ASIMod m = SelectedAvailableASIMod;
                if (VisibleFilteredASIMods.Remove(m))
                {
                    ModsInGroup.Add(new BatchASIMod(m));
                }
            }
            else if (SelectedTabIndex == TAB_TEXTUREMOD)
            {
                MEMMod m = SelectedAvailableMEMMod;
                if (VisibleFilteredMEMMods.Remove(m))
                {
                    ModsInGroup.Add(new MEMMod(m));
                }
            }
        }

        private void RemoveContentModFromInstallGroup()
        {
            var m = SelectedInstallGroupMod;
            if (SelectedInstallGroupMod is BatchMod bm && ModsInGroup.Remove(m))
            {
                VisibleFilteredMods.Add(bm.Mod);
            }
            else if (SelectedInstallGroupMod is BatchASIMod bai && ModsInGroup.Remove(bai))
            {
                VisibleFilteredASIMods.Add(bai.AssociatedMod.OwningMod);
            }
        }

        private void SaveAndClose()
        {
            SaveModern();
            TelemetryInterposer.TrackEvent(@"Saved Batch Group", new Dictionary<string, string>()
            {
                {@"Group name", GroupName},
                {@"Group size", ModsInGroup.Count.ToString()},
                {@"Game", SelectedGame.ToString()}
            });
            Close();
            //OnClosing(new DataEventArgs(savePath));
        }

        private void SaveModern()
        {
            var queue = new BatchLibraryInstallQueue();
            queue.Game = SelectedGame;
            queue.QueueName = GroupName;
            queue.QueueDescription = M3Utilities.ConvertNewlineToBr(GroupDescription);

            // Content mods
            var mods = new List<BatchMod>();
            foreach (var m in ModsInGroup.OfType<BatchMod>())
            {
                mods.Add(m);
            }
            queue.ModsToInstall.ReplaceAll(mods);

            // ASI mods
            var asimods = new List<BatchASIMod>();
            foreach (var m in ModsInGroup.OfType<BatchASIMod>())
            {
                asimods.Add(m);
            }
            queue.ASIModsToInstall.ReplaceAll(asimods);
            SavedPath = queue.Save(true); // Todo: add warning if this overrides another object
        }

        /// <summary>
        /// Unused - this is how MM8 (126) saved biq files. For reference only
        /// </summary>
        [Conditional(@"Debug")]
        private void SaveLegacy()
        {
            throw new Exception(@"SaveLegacy() is no longer supported");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(SelectedGame.ToString());
            sb.AppendLine(GroupName);
            sb.AppendLine(M3Utilities.ConvertNewlineToBr(GroupDescription));
            var libraryRoot = M3LoadedMods.GetModDirectoryForGame(SelectedGame);
            foreach (var m in ModsInGroup.OfType<BatchMod>())
            {
                sb.AppendLine(m.ModDescPath.Substring(libraryRoot.Length + 1)); //STORE RELATIVE!
            }

            var batchfolder = M3LoadedMods.GetBatchInstallGroupsDirectory();
            if (existingFilename != null)
            {
                var existingPath = Path.Combine(batchfolder, existingFilename);
                if (File.Exists(existingPath))
                {
                    File.Delete(existingPath);
                }
            }

            var savePath = "";// getSaveName(GroupName);
            File.WriteAllText(savePath, sb.ToString());
            SavedPath = savePath;
        }


        private bool CanSave()
        {
            if (string.IsNullOrWhiteSpace(GroupDescription)) return false;
            if (string.IsNullOrWhiteSpace(GroupName)) return false;
            if (!ModsInGroup.Any()) return false;
            if (ModsInGroup.OfType<BatchMod>().Any(x => x.Mod == null)) return false; // A batch mod could not be found
            if (ModsInGroup.OfType<BatchASIMod>().Any(x => x.AssociatedMod == null)) return false; // A batch asi mod could not be found
            return true;
        }

        private void CancelEditing()
        {
            Close();
        }

        public MEGame SelectedGame { get; set; }

        /// <summary>
        /// Selected right pane mod
        /// </summary>
        public object SelectedInstallGroupMod { get; set; }

        /// <summary>
        /// Selected left pane mod
        /// </summary>
        public Mod SelectedAvailableMod { get; set; }

        /// <summary>
        /// Selected left pane ASI mod
        /// </summary>
        public ASIMod SelectedAvailableASIMod { get; set; }

        /// <summary>
        /// Selected left pane MEM mod
        /// </summary>
        public MEMMod SelectedAvailableMEMMod { get; set; }

        /// <summary>
        /// The current selected tab. 0 = content mods, 1 = ASI mods - maybe 2 in future = texture mods?
        /// </summary>
        public int SelectedTabIndex { get; set; }


        public void OnSelectedAvailableModChanged()
        {
            AvailableModText = SelectedAvailableMod?.DisplayedModDescription;
        }

        public void OnSelectedAvailableASIModChanged()
        {
            AvailableModText = SelectedAvailableASIMod?.LatestVersion?.Description;
        }

        public void OnSelectedTabIndexChanged()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) OnSelectedAvailableModChanged();
            if (SelectedTabIndex == TAB_ASIMOD) OnSelectedAvailableASIModChanged();
        }

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
                VisibleFilteredMods.ReplaceAll(M3LoadedMods.Instance.AllLoadedMods.Where(x => x.Game == SelectedGame));
                if (SelectedGame != MEGame.LELauncher)
                {
                    VisibleFilteredASIMods.ReplaceAll(ASIManager.GetASIModsByGame(SelectedGame).Where(x => !x.IsHidden));
                }
                else
                {
                    VisibleFilteredASIMods.ClearEx();
                }
            }
            else
            {
                VisibleFilteredMods.ClearEx();
                VisibleFilteredASIMods.ClearEx();
            }
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
