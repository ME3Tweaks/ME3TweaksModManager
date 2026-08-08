using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using Microsoft.Win32;
using ME3TweaksModManager.ui;
using System.ComponentModel;
using System.Windows.Data;
using System.Threading.Tasks;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.telemetry;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class BatchModQueueEditor : Window, IClosableWindow
    {
        // Tab constants
        private const int TAB_CONTENTMOD = 0;
        private const int TAB_ASIMOD = 1;
        private const int TAB_TEXTUREMOD = 2;

        private bool LoadComplete;
        public string NoModSelectedText { get; } = M3L.GetString(M3L.string_selectAModOnTheLeftToViewItsDescription);

        #region BusyHost
        public bool IsBusy { get; set; }
        public string BusyContent { get; set; }

        // Percentages
        public bool BusyProgressIndeterminate { get; set; } = true;
        public double BusyProgressValue { get; set; }
        #endregion

        #region Available M3 Mods
        private ObservableCollectionExtended<Mod> _availableMods { get; } = new();
        public ICollectionView AvailableModsView => CollectionViewSource.GetDefaultView(_availableMods);
        private bool FilterAvailableMods(object obj)
        {
            if (!string.IsNullOrWhiteSpace(ModSearchTerm))
            {
                if (obj is Mod m)
                {
                    // Available

                    // Filter out things that don't contain text in name and developer fields
                    return Mod.MatchesSearch(m, ModSearchTerm);
                }
            }
            else
            {
                return true; // No filter text.
            }

            // If there is filter text and the above conditions don't return then nothing matches
            return false;
        }
        /// <summary>
        /// Text used as filtering for the mods in install group list
        /// </summary>
        public string ModSearchTerm { get; set; }

        public void OnModSearchTermChanged()
        {
            AvailableModsView.Refresh();
        }
        #endregion

        #region Available ASI Mods
        private ObservableCollectionExtended<ASIMod> _availableASIMods { get; } = new();
        public ICollectionView AvailableASIModsView => CollectionViewSource.GetDefaultView(_availableASIMods);
        private bool FilterAvailableASIMods(object obj)
        {
            if (!string.IsNullOrWhiteSpace(ASISearchTerm))
            {
                if (obj is ASIMod mod && mod.LatestVersion != null)
                {
                    return mod.LatestVersion.Name.Contains(ASISearchTerm, StringComparison.InvariantCultureIgnoreCase);
                }
            }
            else
            {
                return true; // No filter text.
            }

            // If there is filter text and the above conditions don't return then nothing matches
            return false;
        }
        /// <summary>
        /// Text used as filtering for the mods in install group list
        /// </summary>
        public string ASISearchTerm { get; set; }

        public void OnASISearchTermChanged()
        {
            AvailableASIModsView.Refresh();
        }
        #endregion

        #region Available Texture Mods
        private ObservableCollectionExtended<MEMMod> _availableMEMMods { get; } = new();
        public ICollectionView AvailableMEMModsView => CollectionViewSource.GetDefaultView(_availableMEMMods);
        private bool FilterAvailableMEMMods(object obj)
        {
            if (!string.IsNullOrWhiteSpace(TextureSearchTerm))
            {
                if (obj is MEMMod mem)
                {
                    return mem.ModName.Contains(TextureSearchTerm, StringComparison.InvariantCultureIgnoreCase);
                }
            }
            else
            {
                return true; // No filter text.
            }

            // If there is filter text and the above conditions don't return then nothing matches
            return false;
        }
        /// <summary>
        /// Text used as filtering for the mods in install group list
        /// </summary>
        public string TextureSearchTerm { get; set; }

        public void OnTextureSearchTermChanged()
        {
            AvailableMEMModsView.Refresh();
        }
        #endregion

        #region Mods In Group
        /// <summary>
        /// Contains both ASI (BatchASIMod) and Content mods (BatchMod)
        /// </summary>
        private ObservableCollectionExtended<object> _modsInGroup { get; } = new();
        public ICollectionView ModsInGroupView => CollectionViewSource.GetDefaultView(_modsInGroup);
        private bool FilterShownModsInGroup(object obj)
        {
            if (!string.IsNullOrWhiteSpace(GroupModSearchTerm))
            {
                if (obj is BatchMod m)
                {
                    if (m.Mod is Mod mod)
                    {
                        // Available

                        // Filter out things that don't contain text in name and developer fields
                        return Mod.MatchesSearch(mod, GroupModSearchTerm);
                    }
                    else
                    {
                        // Not available

                        // Filter out things that don't have it in the path
                        return m.ModDescPath.Contains(GroupModSearchTerm, StringComparison.InvariantCultureIgnoreCase);
                    }
                }
                else if (obj is M3MEMMod m3mm)
                {
                    if (m3mm.ModdescMod != null && Mod.MatchesSearch(m3mm.ModdescMod, GroupModSearchTerm))
                        return true;
                    return m3mm.ModName.Contains(GroupModSearchTerm, StringComparison.InvariantCultureIgnoreCase);
                }
                else if (obj is MEMMod mem)
                {
                    return mem.ModName.Contains(GroupModSearchTerm, StringComparison.InvariantCultureIgnoreCase);
                }
                else if (obj is BatchASIMod basi && basi.AssociatedMod != null)
                {
                    return basi.AssociatedMod.Name.Contains(GroupModSearchTerm, StringComparison.InvariantCultureIgnoreCase);
                }
            }
            else
            {
                return true; // No filter text.
            }

            // If there is filter text and the above conditions don't return then nothing matches
            return false;
        }
        /// <summary>
        /// Text used as filtering for the mods in install group list
        /// </summary>
        public string GroupModSearchTerm { get; set; }

        public void OnGroupModSearchTermChanged()
        {
            ModsInGroupView.Refresh();
        }
        #endregion


        public MEGameSelector[] Games { get; init; }

        public string AvailableModText { get; set; }

        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        public string InitialFileName { get; set; }

        /// <summary>
        /// If the searchbox is currently visible
        /// </summary>
        private bool ShowingSearchBox;

        /// <summary>
        /// Then newly saved path, for showing in the calling window's UI
        /// </summary>
        public string SavedPath;



        public BatchModQueueEditor(Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Queue Editor", this);
            Owner = owner;
            DataContext = this;
            LoadCommands();
            Games = MEGameSelector.GetGameSelectorsIncludingLauncher().ToArray();
            ModsInGroupView.Filter = FilterShownModsInGroup;
            AvailableModsView.Filter = FilterAvailableMods;
            AvailableASIModsView.Filter = FilterAvailableASIMods;
            AvailableMEMModsView.Filter = FilterAvailableMEMMods;
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            if (queueToEdit != null)
            {
                SelectedGame = queueToEdit.Game;
                GroupName = queueToEdit.ModName;
                InitialFileName = queueToEdit.BackingFilename;
                GroupDescription = queueToEdit.QueueDescription;
                _modsInGroup.ReplaceAll(queueToEdit.ModsToInstall);
                _modsInGroup.AddRange(queueToEdit.ASIModsToInstall);
                _modsInGroup.AddRange(queueToEdit.TextureModsToInstall);
                _availableMods.RemoveRange(queueToEdit.ModsToInstall.Select(x => x.Mod));
                _availableASIMods.RemoveRange(queueToEdit.ASIModsToInstall.Select(x => x.AssociatedMod?.OwningMod));
                _availableMEMMods.RemoveRange(queueToEdit.TextureModsToInstall);

                // This must be done after all other content has been inserted!
                RestoreGameBeforeInstall = queueToEdit.RestoreBeforeInstall;

                // Experimental: Load mount priority value here for UI
                foreach (var mod in queueToEdit.ModsToInstall.Where(x => x.Mod != null))
                {
                    mod.Mod.EXP_GetModMountPriority();
                }
            }


        }

        public bool RestoreGameBeforeInstall { get; set; }

        public void OnRestoreGameBeforeInstallChanged()
        {
            // This ensures no duplicates are ever entered.
            _modsInGroup.Remove(new BatchGameRestore());

            if (RestoreGameBeforeInstall)
            {
                _modsInGroup.Insert(0, new BatchGameRestore());
            }
        }

        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }
        public ICommand CheckForIssuesCommand { get; set; }
        public ICommand RemoveFromInstallGroupCommand { get; set; }
        public ICommand AddToInstallGroupCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }
        public ICommand AddCustomMEMModCommand { get; set; }
        public ICommand SortByMountPriorityCommand { get; set; }
        public ICommand SearchModsCommand { get; set; }
        public ICommand CloseSearchCommand { get; set; }
        public ICommand EscapePressedCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
            CheckForIssuesCommand = new GenericCommand(CheckForIssues);
            RemoveFromInstallGroupCommand = new GenericCommand(RemoveContentModFromInstallGroup, CanRemoveFromInstallGroup);
            AddToInstallGroupCommand = new GenericCommand(AddModToInstallGroup, CanAddToInstallGroup);
            MoveUpCommand = new GenericCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new GenericCommand(MoveDown, CanMoveDown);
            AddCustomMEMModCommand = new GenericCommand(ShowMEMSelector, CanAddMEMMod);
            SortByMountPriorityCommand = new GenericCommand(SortByMountPriority, CanSortByMountPriority);
            SearchModsCommand = new GenericCommand(ShowSearchBox);
            CloseSearchCommand = new GenericCommand(CloseSearchBox);
            EscapePressedCommand = new GenericCommand(EscapePressed);
        }

        private void EscapePressed()
        {
            if (ModSearchBox.IsKeyboardFocused)
            {
                CloseSearchBox();
            }
        }

        private void CloseSearchBox()
        {
            if (ShowingSearchBox)
            {
                ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, false);
                ShowingSearchBox = false; // May have timing issues if user mashes button, but oh well
            }

            GroupModSearchTerm = null;
        }

        private void ShowSearchBox()
        {
            if (!ShowingSearchBox)
            {
                ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, true);
                ShowingSearchBox = true; // May have timing issues if user mashes button, but oh well
            }

            if (LoadComplete)
                Keyboard.Focus(ModSearchBox);
        }

        private bool CanSortByMountPriority()
        {
            return _modsInGroup.Any(x => x is BatchMod m && m.Mod != null);
        }

        private void LeftSideMod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fwe)
            {
                if (fwe.DataContext is Mod or MEMMod or ASIMod)
                {
                    AddModToInstallGroup();
                }
            }
        }

        private void SortByMountPriority()
        {
            var shouldContinue = M3L.ShowDialog(this,
                M3L.GetString(M3L.string_dialog_sortByMountPriority),
                M3L.GetString(M3L.string_experimentalFeature), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            if (!shouldContinue)
                return;



            var contentMods = _modsInGroup.OfType<BatchMod>().Where(x => x.Mod != null)
                .OrderByDescending(x => x.Mod.EXP_GetModMountPriority()).ToList(); // Just order it here too. Its reversed ordered as the order reverses again when we insert it
            _modsInGroup.RemoveRange(contentMods);
            foreach (var m in contentMods)
            {
                _modsInGroup.Insert(0, m);
            }

            // Trigger this again
            OnRestoreGameBeforeInstallChanged();
        }

        public void CheckForIssues()
        {
            List<string> possibleIssues = new List<string>();
            var installedDLC = new CaseInsensitiveDictionary<MetaCMM>();
            var mods = _modsInGroup.OfType<BatchMod>()
                .Where(x => x.Mod != null && x.Mod.ParsedModVersion != null).Select(x => x.Mod).ToList();

            // In-order pass for requirements
            foreach (var mod in mods)
            {
                var dlc = mod.GetAllPossibleCustomDLCFolders();
                foreach (var d in dlc)
                {
                    var versionedDLC = new MetaCMM() { Version = mod.ParsedModVersion.ToString() };
                    installedDLC[d] = versionedDLC;
                }

                foreach (var req in mod.RequiredDLC)
                {
                    if (!req.IsRequirementMet(null, installedDLC, checkOptionKeys: false))
                    {
                        var tpmi = TPMIService.GetThirdPartyModInfo(req.DLCFolderName.Key, mod.Game);
                        // Coded this way for localization purposes
                        var minimumVersionStr = M3L.GetString(M3L.string_withMinimumVersion);
                        possibleIssues.Add(M3L.GetString(M3L.string_interp_bqissue_minimumVersionNotFound, mod.ModName, tpmi?.modname ?? req.DLCFolderName.Key, req.MinVersion != null ? @" " + minimumVersionStr + @" " + req.MinVersion : null));
                    }
                }
            }

            // Check incompatible DLCs in second pass once we know the list of all DLC folders.
            foreach (var mod in mods)
            {
                foreach (var inc in mod.IncompatibleDLC)
                {
                    if (installedDLC.ContainsKey(inc))
                    {
                        var tpmi = TPMIService.GetThirdPartyModInfo(inc, mod.Game);
                        possibleIssues.Add(M3L.GetString(M3L.string_interp_bqissue_modNotCompatible, mod.ModName, tpmi?.modname ?? inc));
                    }
                }
            }

            if (possibleIssues.Any())
            {
                ListDialog ld = new ListDialog(possibleIssues, M3L.GetString(M3L.string_possibleIssuesFound),
                    M3L.GetString(M3L.string_bqe_issuesFound),
                    this);
                ld.ShowDialog();
            }
            else
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_bqe_noIssues), M3L.GetString(M3L.string_noIssuesDetected), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowMEMSelector()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = M3L.GetString(M3L.string_massEffectModderFiles) + @" (*.mem)|*.mem", // Todo: Localize this properly
                Title = M3L.GetString(M3L.string_selectMemFile),
                Multiselect = true,
            };

            var result = ofd.ShowDialog();
            if (result == true)
            {
                foreach (var f in ofd.FileNames)
                {
                    var memFileGame = ModFileFormats.GetGameMEMFileIsFor(f);
                    if (memFileGame != SelectedGame)
                    {
                        // TODO: UPDATE LOCALIZATION TO INCLUDE FILENAME
                        M3L.ShowDialog(this,
                            M3L.GetString(M3L.string_interp_dialog_memForDifferentGame, SelectedGame, memFileGame),
                            M3L.GetString(M3L.string_wrongGame), MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }

                    // User selected file
                    MEMMod m = new MEMMod()
                    {
                        FilePath = f
                    };

                    m.ParseMEMData();

                    _availableMEMMods
                        .Add(m); //Todo: Check no duplicates in left list (or existing already on right?)
                }
            }
        }

        private bool CanAddMEMMod()
        {
            return true;
        }

        private bool CanAutosort()
        {
            return _modsInGroup.Count > 1;
        }

        class ModDependencies
        {

            public List<string> HardDependencies = new(); // requireddlc

            /// <summary>
            /// List of DLC folders the mod can depend on for configuration.
            /// </summary>
            public List<string> DependencyDLCs = new();

            /// <summary>
            /// List of all folders the mod mapped to this can install
            /// </summary>
            public List<string> InstallableDLCFolders = new();

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

            foreach (var mod in _modsInGroup)
            {
                var depends = new ModDependencies();

                // These items MUST be installed first or this mod simply won't install.
                // Official DLC is not counted as mod manager cannot install those.
                depends.HardDependencies = mod.Mod.RequiredDLC.Where(x => !MEDirectories.OfficialDLC(mod.Game).Contains(x.DLCFolderName)).Select(x => x.DLCFolderName).ToList();
                depends.DependencyDLCs = mod.Mod.GetUIAutoConfigs().ToList(); // These items must be installed prior to install or options will be unavailable to the user.
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

            _modsInGroup.ReplaceAll(finalOrder);*/
#endif
        }

        private bool CanRemoveFromInstallGroup() => SelectedInstallGroupMod != null;

        private bool CanAddToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) return SelectedAvailableMod != null;
            if (SelectedTabIndex == TAB_ASIMOD) return SelectedAvailableASIMod != null;
            if (SelectedTabIndex == TAB_TEXTUREMOD) return SelectedAvailableMEMMod != null;
            return false;
        }

        private bool CanMoveUp()
        {
            if (SelectedInstallGroupMod is BatchMod)
            {
                var index = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0 && _modsInGroup[index - 1] is BatchMod)
                {
                    return true;
                }
            }
            else if (SelectedInstallGroupMod is MEMMod)
            {
                var index = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0 && _modsInGroup[index - 1] is MEMMod)
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
                var index = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < _modsInGroup.Count - 1 && _modsInGroup[index + 1] is BatchMod)
                {
                    return true;
                }
            }
            else if (SelectedInstallGroupMod is MEMMod)
            {
                var index = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < _modsInGroup.Count - 1 && _modsInGroup[index + 1] is MEMMod)
                {
                    return true;
                }
            }
            return false;
        }

        private void MoveDown()
        {
            int numToMove = Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1; // if holding shift, move 5
            for (int i = 0; i < numToMove; i++)
            {
                if (CanMoveDown() && SelectedInstallGroupMod is BatchMod || SelectedInstallGroupMod is MEMMod)
                {
                    var mod = SelectedInstallGroupMod;
                    var oldIndex = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                    var newIndex = oldIndex + 1;
                    _modsInGroup.RemoveAt(oldIndex);
                    _modsInGroup.Insert(newIndex, mod);
                    SelectedInstallGroupMod = mod;
                }
            }

            ScrollSelectedModIntoView();
        }

        private void MoveUp()
        {
            int numToMove = Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1; // if holding shift, move 5
            for (int i = 0; i < numToMove; i++)
            {
                if (CanMoveUp() && SelectedInstallGroupMod is BatchMod || SelectedInstallGroupMod is MEMMod)
                {
                    var mod = SelectedInstallGroupMod;
                    var oldIndex = _modsInGroup.IndexOf(SelectedInstallGroupMod);
                    var newIndex = oldIndex - 1;
                    _modsInGroup.RemoveAt(oldIndex);
                    _modsInGroup.Insert(newIndex, mod);
                    SelectedInstallGroupMod = mod;
                }
            }

            ScrollSelectedModIntoView();
        }

        private void ScrollSelectedModIntoView()
        {
            InstallGroupMods_ListBox.ScrollIntoView(SelectedInstallGroupMod);
        }

        private void AddModToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD)
            {
                foreach (var i in ListBox_ContentMods.SelectedItems.OfType<Mod>().ToList()) // ToList as this will be removing items that will change selection
                {
                    if (_availableMods.Remove(i))
                    {
                        var index = _modsInGroup.FindLastIndex(x => x is BatchMod or BatchGameRestore);
                        index++; // if not found, it'll be -1. If found, we will want to insert after.
                        _modsInGroup.Insert(index, new BatchMod(i)); // Put into specific position.
                    }
                }
            }
            else if (SelectedTabIndex == TAB_ASIMOD)
            {
                ASIMod m = SelectedAvailableASIMod;
                if (_availableASIMods.Remove(m))
                {
                    _modsInGroup.Add(new BatchASIMod(m));
                }
            }
            else if (SelectedTabIndex == TAB_TEXTUREMOD)
            {
                foreach (var i in ListBox_AvailableTextures.SelectedItems.OfType<MEMMod>().ToList()) // ToList as this will be removing items that will change selection
                {
                    if (_availableMEMMods.Remove(i))
                    {
                        if (i is M3MEMMod m3mm) // M3MEMMMod must go first
                        {
                            _modsInGroup.Add(new M3MEMMod(m3mm));
                        }
                        else if (i is MEMMod mm)
                        {
                            _modsInGroup.Add(new MEMMod(mm));
                        }
                    }
                }
            }
        }

        private void RemoveContentModFromInstallGroup()
        {
            var m = SelectedInstallGroupMod;
            var selectedIndex = _modsInGroup.IndexOf(m);

            if (SelectedInstallGroupMod is BatchMod bm && _modsInGroup.Remove(m) && bm.IsAvailableForInstall())
            {
                _availableMods.Add(bm.Mod);
            }
            else if (SelectedInstallGroupMod is BatchASIMod bai && _modsInGroup.Remove(bai))
            {
                _availableASIMods.Add(bai.AssociatedMod.OwningMod);
            }
            else if (SelectedInstallGroupMod is MEMMod m3ai && _modsInGroup.Remove(m3ai) && m3ai.IsAvailableForInstall()) // covers both types
            {
                _availableMEMMods.Add(m3ai);
            }

            // Select next object to keep UI working well
            if (_modsInGroup.Count > selectedIndex)
            {
                SelectedInstallGroupMod = _modsInGroup[selectedIndex];
            }
            else
            {
                SelectedInstallGroupMod = _modsInGroup.LastOrDefault();
            }
        }

        private async void SaveAndClose()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_groupNameCannotBeEmpty), M3L.GetString(M3L.string_validationFailed), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(GroupDescription))
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_groupDescriptionCannotBeEmpty), M3L.GetString(M3L.string_validationFailed), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            BusyContent = M3L.GetString(M3L.string_savingInstallGroup);
            IsBusy = true;

            // This could technically throw an error...
            bool result = false;
            try
            {
                result = await SaveModern();
            }
            catch (Exception e)
            {
                IsBusy = false;
                M3Log.Exception(e, @"Error saving install group:");
                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorSavingInstallGroup) + $@": {e.Message}", M3L.GetString(M3L.string_interp_errorSavingInstallGroup), MessageBoxButton.OK, MessageBoxImage.Error);
                result = false;
            }

            if (result)
            {
                M3OpenTelemetry.TrackEvent(@"Saved Batch Group", new Dictionary<string, string>()
                {
                    { @"Group name", GroupName },
                    { @"Group size", _modsInGroup.Count.ToString() },
                    { @"Game", SelectedGame.ToString() }
                });
                Close();
            }
            IsBusy = false;
        }

        /// <summary>
        /// Applies the listed progress info
        /// </summary>
        /// <param name="pi"></param>
        private void onProgress(ProgressInfo pi)
        {
            BusyProgressValue = pi.Value;
            BusyProgressIndeterminate = pi.Indeterminate;
            BusyContent = pi.Status;
        }

        private async Task<bool> SaveModern()
        {
            var queue = new BatchLibraryInstallQueue();
            queue.Game = SelectedGame;
            queue.ModName = GroupName;
            queue.QueueDescription = M3Utilities.ConvertNewlineToBr(GroupDescription);
            queue.RestoreBeforeInstall = RestoreGameBeforeInstall;

            // Content mods
            var mods = new List<BatchMod>();
            foreach (var m in _modsInGroup.OfType<BatchMod>())
            {
                mods.Add(m);
            }
            queue.ModsToInstall.ReplaceAll(mods);

            // ASI mods
            var asimods = new List<BatchASIMod>();
            foreach (var m in _modsInGroup.OfType<BatchASIMod>())
            {
                asimods.Add(m);
            }
            queue.ASIModsToInstall.ReplaceAll(asimods);

            // Texture mods
            var texturemods = new List<MEMMod>();
            foreach (var m in _modsInGroup.OfType<MEMMod>())
            {
                texturemods.Add(m);
            }
            queue.TextureModsToInstall.ReplaceAll(texturemods);

            var queueSavePath = queue.GetSaveName(queue.ModName, true);
            var destExists = File.Exists(queueSavePath);
            if (destExists && Path.GetFileName(queueSavePath) != InitialFileName) // If InitialFileName is null this will indicate new group saving over existing rather than a rename
            {
                var sContinue = Application.Current.Dispatcher.Invoke(() => {
                    var continueRes = M3L.ShowDialog(this,
                                        M3L.GetString(M3L.string_interp_existingInstallGroupFile, queue.ModName),
                                        M3L.GetString(M3L.string_fileAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (continueRes != MessageBoxResult.Yes)
                        return false;
                    return true;
                }); // Ensure we are back on UI thread

                if (!sContinue)
                    return false;
            }

            // Hashing big files can take time, so we background thread this with a dialog over the top
            SavedPath = await queue.Save(true, progressDelegate: onProgress);

            // File name was changed
            if (InitialFileName != null && !Path.GetFileName(queueSavePath).CaseInsensitiveEquals(InitialFileName))
            {
                File.Delete(Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), InitialFileName));
            }

            return true;
        }

        /// <summary>
        /// Unused - this is how MM8 (126) saved biq files. For reference only
        /// </summary>
        [Conditional(@"Debug")]
        private void SaveLegacy()
        {
            throw new Exception(@"SaveLegacy() is no longer supported");

#if LEGACYCODE
            // This is only here for reference of how it used to work
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(SelectedGame.ToString());
            sb.AppendLine(GroupName);
            sb.AppendLine(M3Utilities.ConvertNewlineToBr(GroupDescription));
            var libraryRoot = M3LoadedMods.GetModDirectoryForGame(SelectedGame);
            foreach (var m in _modsInGroup.OfType<BatchMod>())
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

            var savePath = "";// GetSaveName(GroupName);
            File.WriteAllText(savePath, sb.ToString());
            SavedPath = savePath;
#endif
        }


        private bool CanSave()
        {
            // Validation of name/desc is in save for dialog

            if (!_modsInGroup.Any()) return false;
            //if (_modsInGroup.OfType<BatchMod>().Any(x => x.Mod == null)) return false; // A batch mod could not be found // Disabled 04/25/2023 - hopefully this works properly?
            if (_modsInGroup.OfType<BatchASIMod>().Any(x => x.AssociatedMod == null)) return false; // A batch asi mod could not be found
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

        public void OnSelectedInstallGroupModChanged()
        {
            if (SelectedInstallGroupMod is BatchMod { Mod.BannerBitmap: null } m)
            {
                m.Mod.LoadBannerImage(); // Method will check if it's null
            }
        }

        /// <summary>
        /// Selected left pane mod
        /// </summary>
        public Mod SelectedAvailableMod { get; set; }

        /// <summary>
        /// Selected left pane ASI mod
        /// </summary>
        public ASIMod SelectedAvailableASIMod { get; set; }

        /// <summary>
        /// Selected left pane MEM mod. Can be MEMMod or M3MEMMod
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

        public void OnSelectedAvailableMEMModChanged()
        {
            AvailableModText = SelectedAvailableMEMMod?.GetDescription() ?? M3L.GetString(M3L.string_selectATextureMod);
        }

        public void OnSelectedTabIndexChanged()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) OnSelectedAvailableModChanged();
            if (SelectedTabIndex == TAB_ASIMOD) OnSelectedAvailableASIModChanged();
            if (SelectedTabIndex == TAB_TEXTUREMOD) OnSelectedAvailableMEMModChanged();
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
                _availableMods.ReplaceAll(M3LoadedMods.Instance.AllLoadedMods.Where(x => x.Game == SelectedGame));
                if (SelectedGame != MEGame.LELauncher)
                {
                    _availableASIMods.ReplaceAll(ASIManager.GetASIModsByGame(SelectedGame).Where(x => x.ShouldShowInUI()));
                    _availableMEMMods.ReplaceAll(M3LoadedMods.GetAllM3ManagedMEMs(SelectedGame).Where(x => x.Game == SelectedGame));
                }
                else
                {
                    _availableASIMods.ClearEx();
                    _availableMEMMods.ClearEx();
                }
            }
            else
            {
                _availableMods.ClearEx();
                _availableASIMods.ClearEx();
                _availableMEMMods.ClearEx();
            }
        }

        private void TryChangeGameTo(MEGame newgame)
        {
            if (newgame == SelectedGame) return; //don't care
            if (_modsInGroup.Count > 0 && newgame != SelectedGame)
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_changingGameWillClearGroup), M3L.GetString(M3L.string_changingGameWillClearGroup), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _modsInGroup.ClearEx();
                    ASISearchTerm = GroupModSearchTerm = ModSearchTerm = TextureSearchTerm = null;
                    RestoreGameBeforeInstall = false;
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
                ASISearchTerm = GroupModSearchTerm = ModSearchTerm = TextureSearchTerm = null;
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

        private void BatchModQueueEditor_OnLoaded(object sender, RoutedEventArgs e)
        {
            ShowSearchBox();
            LoadComplete = true;
        }

        public bool AskToClose()
        {
            if (M3L.ShowDialog(this, M3L.GetString(M3L.string_closeWithoutSavingChangesQuestion), M3L.GetString(M3L.string_applicationClosing), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                Close();
                return true;
            }

            // Denied closing.
            return false;
        }
    }

    /// <summary>
    /// This is nothing but a placeholder object to show that the game will restore before install
    /// </summary>
    public class BatchGameRestore : IBatchQueueMod
    {
        public string UIDescription => M3L.GetString(M3L.string_description_restoreGameBeforeBatchInstall);

        protected bool Equals(BatchGameRestore other)
        {
            // We are always the same as another object of this type.
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BatchGameRestore)obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        // IBatchMod interface
        public bool IsAvailableForInstall() => true;
        public string Hash { get; set; }
        public long Size { get; set; }
    }
}
