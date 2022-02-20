﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects.nexusfiledb;
using ME3TweaksModManager.ui;
using Pathoschild.FluentNexus.Models;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusFileQueryPanel.xaml
    /// </summary>
    public partial class NexusFileQueryPanel : MMBusyPanelBase, INotifyPropertyChanged
    {
        /// <summary>
        /// If the database loaded or not
        /// </summary>
        private bool QPLoaded;
        public string SearchTerm { get; set; }
        public bool QueryInProgress { get; set; } = true; // Defaults to true on open.
        public string BusyStatusText { get; set; } = M3L.GetString(M3L.string_pleaseWait);
        public string StatusText { get; set; }
        public bool SearchME1 { get; set; }
        public bool SearchME2 { get; set; }
        public bool SearchME3 { get; set; }
        public bool SearchLE { get; set; }

        private void UpdateFilters()
        {
            if (!QPLoaded)
                return;
            var newNames = new List<string>();
            IEnumerable<string> getFilenamesForGame(string domain)
            {
                return LoadedDatabases[domain].FileInstances.Select(x => LoadedDatabases[domain].NameTable[x.Key]);
            }
            if (SearchME1) { newNames.AddRange(getFilenamesForGame(@"masseffect")); }
            if (SearchME2) { newNames.AddRange(getFilenamesForGame(@"masseffect2")); }
            if (SearchME3) { newNames.AddRange(getFilenamesForGame(@"masseffect3")); }
            if (SearchLE) { newNames.AddRange(getFilenamesForGame(@"masseffectlegendaryedition")); }

            newNames = newNames.Distinct().OrderBy(x => x).ToList();
            AllSearchableNames.ReplaceAll(newNames);
        }

        public void OnSearchME1Changed() { UpdateFilters(); }
        public void OnSearchME2Changed() { UpdateFilters(); }
        public void OnSearchME3Changed() { UpdateFilters(); }
        public void OnSearchLEChanged() { UpdateFilters(); }

        public ObservableCollectionExtended<string> AllSearchableNames { get; } = new ObservableCollectionExtended<string>();

        public NexusFileQueryPanel()
        {
            FileCategories = new ObservableCollectionExtended<FileCategory>(Enum.GetValues<FileCategory>());
            LoadCommands();
        }

        public RelayCommand DownloadModCommand { get; private set; }
        public GenericCommand SearchCommand { get; set; }

        private void LoadCommands()
        {
            DownloadModCommand = new RelayCommand(DownloadMod, CanDownloadMod);
            SearchCommand = new GenericCommand(PerformSearch, CanSearch);
        }


        public ObservableCollectionExtended<SearchedItemResult> Results { get; } = new ObservableCollectionExtended<SearchedItemResult>();
        public ObservableCollectionExtended<FileCategory> FileCategories { get; }
        public ObservableCollectionExtended<FileCategory> SelectedFileCategories { get; } = new ObservableCollectionExtended<FileCategory>(Enum.GetValues<FileCategory>()); // all by default

        private bool CanSearch() => !QueryInProgress && !string.IsNullOrWhiteSpace(SearchTerm) && (SearchME1 || SearchME2 || SearchME3 || SearchLE) && HasCategory();

        private bool HasCategory()
        {
            if (CategoryOptionsCBL != null)
            {
                return CategoryOptionsCBL.GetSelectedItems().OfType<FileCategory>().Any();
            }

            return false;
        }

        private Dictionary<string, GameDatabase> LoadedDatabases = new Dictionary<string, GameDatabase>();

        private void PerformSearch()
        {
            Results.ClearEx();
            var categories = CategoryOptionsCBL.GetSelectedItems().OfType<FileCategory>().ToList();
            var searchGames = new List<string>();
            if (SearchME1) searchGames.Add(@"masseffect");
            if (SearchME2) searchGames.Add(@"masseffect2");
            if (SearchME3) searchGames.Add(@"masseffect3");
            if (SearchLE) searchGames.Add(@"masseffectlegendaryedition");
            QueryInProgress = true;
            Task.Run(() =>
            {

                try
                {
                    foreach (var domain in searchGames)
                    {
                        var db = LoadedDatabases[domain];
                        // Check if the name exists in filenames. If it doesn't, it will never find it
#if DEBUG
                        /*
                        var ignoredItems = new List<string>()
                        {
                            @"DLC_MOD_FMRM_Patches",
                            @"DLC_MOD_FJRM_Patches",
                            @"DLC_ASH_MiniSkirt_Mods",
                            @"DLC_Explorer",
                            @"DLC_LIA_RA4_MeshOnly",
                            @"DLC_ASH_Shorts_Mod",
                            @"DLC_ASH_Alt_Mods",
                            @"DLC_ASH_Socks_Mod",
                            @"DLC_ASH_Topless_Mod",
                            @"DLC_GAR_FRM_Altered_Face_Legs_Mod",
                            @"DLC_GAR_GFC_Altered_Face_Legs_Mod",
                            @"DLC_LIA_NKDSlippers_Mod",
                            @"DLC_LIA_NKDSnickers_Mod",
                            @"DLC_GAR_GFC_New_Version",
                            @"DLC_GAR_GFC_Old_Version",
                            @"DLC_GAR_FRM_Textures",
                            @"DLC_MIR_Shorts_Mod",
                            @"DLC_MOD_IT_RUS",
    
                        };
                        var dlcNames = db.NameTable.Values.Where(x => !ignoredItems.Contains(x) && x.StartsWith(@"DLC_") && Path.GetExtension(x) == string.Empty && !x.Contains(" ") && TPMIService.GetThirdPartyModInfo(x, MEGame.ME3) == null).Select(x => x.Trim()).Distinct().ToList();
                        var xx = new List<string>();
                        foreach (var i in db.FileInstances.Values)
                        {
                            foreach (var f in i)
                            {
                                if (f.ParentPathID > 0)
                                {
                                    var path = db.Paths[f.ParentPathID].GetFullPath(db);
                                    if (path.ContainsAny(dlcNames, StringComparison.Ordinal))
                                    {
                                        var finfo = $@"https://nexusmods.com/masseffect/mods/{f.ModID}";
                                        xx.Add(db.NameTable[db.ModFileInfos[f.FileID].NameID] + " " + finfo);
                                    }
    
                                }
                            }
                        }
                        File.WriteAllLines(@"D:\dlcNames.txt", dlcNames);
                        File.WriteAllLines(@"D:\mods.txt", xx);*/
#endif
                        var match = db.NameTable.FirstOrDefault(x =>
                            x.Value.Equals(SearchTerm, StringComparison.InvariantCultureIgnoreCase));

                        if (match.Key != 0)
                        {
                            // Found
                            var instances = db.FileInstances[match.Key].Where(x => categories.Contains(db.ModFileInfos[x.FileID].Category));
                            //Application.Current.Dispatcher.Invoke(() =>
                            //{
                            Results.AddRange(instances.Select(x => new SearchedItemResult()
                            {
                                Instance = x,
                                Domain = domain,
                                Filename = db.NameTable[x.FilenameId],
                                AssociatedDB = db
                            }).ToList()); // We do tolist because it forces all items to be added at once
                            //});
                        }
                    }

                    StatusText = M3L.GetString(M3L.string_interp_resultsCount, Results.Count);
                    QueryInProgress = false;

                    //foreach (var res in Results)
                    //{
                    //    Debug.WriteLine($"{res.Instance.ModID} {res.Instance.FileID}");
                    //}
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not perform search: {e.Message}");
                    QueryInProgress = false;
                }
            });

        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        private bool CanDownloadMod(object obj) => NexusModsUtilities.UserInfo != null && NexusModsUtilities.UserInfo.IsPremium;

        private void DownloadMod(object obj)
        {
            if (obj is SearchedItemResult sir)
            {
                string nxmlink = $@"nxm://{sir.Domain}/mods/{sir.Instance.ModID}/files/{sir.Instance.FileID}";
                OnClosing(new DataEventArgs(nxmlink));
            }
        }


        public override void OnPanelVisible()
        {
            InitializeComponent();
            Task.Run(() =>
            {
                try
                {
                    GameDatabase.EnsureDatabaseFile(true);

                    // Load DBs
                    foreach (var domain in NexusModsUtilities.AllSupportedNexusDomains)
                    {
                        if (!LoadedDatabases.TryGetValue(domain, out var db))
                        {
                            db = GameDatabase.LoadDatabase(domain);
                            if (db != null)
                            {
                                LoadedDatabases[domain] = db;
                            }
                        }
                    }

                    QPLoaded = true;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not ensure the nexus database: {e.Message}");
                }

                // No longer busy
                QueryInProgress = false;
                BusyStatusText = M3L.GetString(M3L.string_searching);
            }).ContinueWithOnUIThread(x =>
            {
                CategoryOptionsCBL.SetSelectedItems(Enum.GetValues<FileCategory>().Where(x => x != FileCategory.Archived && x != FileCategory.Deleted).OfType<object>());
                if (Settings.GenerationSettingLE && !Settings.GenerationSettingOT)
                {
                    SearchLE = true; // Automatically set this as it's the only option in this mode
                }
            });
        }


        private void ClosePanel(object sender, System.Windows.RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        [AddINotifyPropertyChangedInterface]
        public class SearchedItemResult
        {
            public FileInstance Instance { get; internal set; }
            public string Domain { get; internal set; }
            public string Filename { get; internal set; }
            public GameDatabase AssociatedDB { get; internal set; }

            public string GameIconSource
            {
                get
                {
                    switch (Domain)
                    {
                        case @"masseffect":
                            return @"/images/gameicons/ME1_48.ico";
                        case @"masseffect2":
                            return @"/images/gameicons/ME2_48.ico";
                        case @"masseffect3":
                            return @"/images/gameicons/ME3_48.ico";
                        case @"masseffectlegendaryedition":
                            {
                                if (Instance.LEGames != null && Instance.LEGames.Length == 1)
                                {
                                    switch (Instance.LEGames[0])
                                    {
                                        case LegendaryExplorerCore.Packages.MEGame.LE1:
                                            return @"/images/gameicons/LE1_48.ico";
                                        case LegendaryExplorerCore.Packages.MEGame.LE2:
                                            return @"/images/gameicons/LE2_48.ico";
                                        case LegendaryExplorerCore.Packages.MEGame.LE3:
                                            return @"/images/gameicons/LE3_48.ico";
                                    }
                                }
                                // Don't have the info. Set it to the launcher icon as we have no idea what game this file is for
                                return @"/images/gameicons/LEL_Icon.ico";

                            }
                    }

                    return null;
                }
            }

            /// <summary>
            /// The full file path in the archive
            /// </summary>
            public string FullPath
            {
                get
                {
                    if (Instance.ParentPathID == 0) return Filename;
                    return AssociatedDB.Paths[Instance.ParentPathID].GetFullPath(AssociatedDB, Filename);
                }
            }

            /// <summary>
            /// The name of the mod page that has this instance
            /// </summary>
            public string ModName => AssociatedDB.NameTable[Instance.ModNameId];
            /// <summary>
            /// The title of the file on NexusMods that contains this instance within it
            /// </summary>
            public string ModFileTitle => AssociatedDB.NameTable[FileInfo.NameID];

            public NMFileInfo FileInfo => AssociatedDB.ModFileInfos[Instance.FileID];

            public string DownloadModText => NexusModsUtilities.UserInfo != null && NexusModsUtilities.UserInfo.IsPremium ? M3L.GetString(M3L.string_tooltip_downloadThisMod) : M3L.GetString(M3L.string_tooltip_premiumRequiredForDownload);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is SearchedItemResult sir)
            {
                var outboundUrl = $@"https://nexusmods.com/{sir.Domain}/mods/{sir.Instance.ModID}?tab=files"; // do not localize
                M3Utilities.OpenWebpage(outboundUrl);
            }
        }

        private void UIElement_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void HandleMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // This forces scrolling to bubble up
            // cause expander eats it
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = (((Control)sender).TemplatedParent ?? ((Control)sender).Parent) as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }
    }
}