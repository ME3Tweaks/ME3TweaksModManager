using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects.nexusfiledb;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Packages;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusFileQueryPanel.xaml
    /// </summary>
    public partial class NexusFileQueryPanel : MMBusyPanelBase, INotifyPropertyChanged
    {
        /// <summary>
        /// The API endpoint for searching. Append an encoded filename to search.
        /// </summary>
        public string SearchTerm { get; set; }
        public bool QueryInProgress { get; set; } = true; // Defaults to true on open.
        public string BusyStatusText { get; set; } = M3L.GetString(M3L.string_pleaseWait);
        public string StatusText { get; set; }
        public bool SearchME1 { get; set; }
        public bool SearchME2 { get; set; }
        public bool SearchME3 { get; set; }
        public NexusFileQueryPanel()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            SearchCommand = new GenericCommand(PerformSearch, CanSearch);
        }

        public GenericCommand SearchCommand { get; set; }
        public ObservableCollectionExtended<SearchedItemResult> Results { get; } = new ObservableCollectionExtended<SearchedItemResult>();

        private bool CanSearch() => !QueryInProgress && !string.IsNullOrWhiteSpace(SearchTerm) && (SearchME1 || SearchME2 || SearchME3);

        private Dictionary<string, GameDatabase> LoadedDatabases = new Dictionary<string, GameDatabase>();

        private void PerformSearch()
        {
            Results.ClearEx();
            var searchGames = new List<string>();
            if (SearchME1) searchGames.Add(@"masseffect");
            if (SearchME2) searchGames.Add(@"masseffect2");
            if (SearchME3) searchGames.Add(@"masseffect3");
            QueryInProgress = true;
            try
            {
                foreach (var domain in searchGames)
                {
                    if (!LoadedDatabases.TryGetValue(domain, out var db))
                    {
                        db = GameDatabase.LoadDatabase(domain);
                        if (db != null)
                        {
                            LoadedDatabases[domain] = db;
                        }
                    }

                    // Check if the name exists in filenames. If it doesn't, it will never find it
#if DEBUG

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
                    var dlcNames = db.NameTable.Values.Where(x => !ignoredItems.Contains(x) && x.StartsWith(@"DLC_") && Path.GetExtension(x) == string.Empty && !x.Contains(" ") && ThirdPartyServices.GetThirdPartyModInfo(x, MEGame.ME3) == null).Select(x => x.Trim()).Distinct().ToList();
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
                    File.WriteAllLines(@"D:\mods.txt", xx);
#endif
                    var match = db.NameTable.FirstOrDefault(x =>
                        x.Value.Equals(SearchTerm, StringComparison.InvariantCultureIgnoreCase));

                    if (match.Key != 0)
                    {
                        // Found
                        var instances = db.FileInstances[match.Key];
                        Results.AddRange(instances.Select(x => new SearchedItemResult()
                        {
                            Instance = x,
                            Domain = domain,
                            Filename = db.NameTable[x.FilenameId],
                            AssociatedDB = db
                        }));
                    }
                }

                StatusText = M3L.GetString(M3L.string_interp_resultsCount, Results.Count);
                QueryInProgress = false;
            }
            catch (Exception e)
            {
                Log.Error($@"Could not perform search: {e.Message}");
                QueryInProgress = false;
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
            Task.Run(() =>
            {
                try
                {
                    GameDatabase.EnsureDatabaseFile(true);
                }
                catch (Exception e)
                {
                    Log.Error($@"Could not ensure the nexus database: {e.Message}");
                }

                // No longer busy
                QueryInProgress = false;
                BusyStatusText = M3L.GetString(M3L.string_searching);
            });
        }


        private void ClosePanel(object sender, System.Windows.RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public class APIStatusResult
        {
            public string name { get; set; }
            public double value { get; set; }
        }

        public class SearchedItemResult : INotifyPropertyChanged
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


#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is SearchedItemResult sir)
            {
                var outboundUrl = $@"https://nexusmods.com/{sir.Domain}/mods/{sir.Instance.ModID}?tab=files"; // do not localize
                Utilities.OpenWebpage(outboundUrl);
            }
        }

        private class SearchTopLevelResult
        {
            public int mod_count { get; set; }
            public string searched_file { get; set; }
            public string file_name { get; set; } // Why?
            public List<string> games { get; set; }
            public List<SearchedItemResult> mod_ids { get; set; }
        }
    }
}
