using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Helpers;
using Newtonsoft.Json;
using PropertyChanged;

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
        public string APIEndpoint = @"https://api.jonatanrek.cz/NEXUS/api/search/";
        public string SearchTerm { get; set; }
        public bool QueryInProgress { get; set; }

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

        private void PerformSearch()
        {
            var searchUrl = $@"{APIEndpoint}{Uri.EscapeDataString(SearchTerm)}";
            Debug.WriteLine(searchUrl);
            QueryInProgress = true;
            try
            {

                Task.Run(() => OnlineContent.FetchRemoteString(searchUrl)).ContinueWithOnUIThread(result =>
                {
                    var i = result.Result;
                    var x = JsonConvert.DeserializeObject<SearchTopLevelResult>(i);
                    Results.ReplaceAll(x.mod_ids);
                    QueryInProgress = false;
                });
            }
            catch (Exception e)
            {
                QueryInProgress = false;
            }

        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {

        }

        private void ClosePanel(object sender, System.Windows.RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public class SearchedItemResult : INotifyPropertyChanged
        {
            public int file_id { get; set; }
            public int file_size { get; set; }
            public int mod_id { get; set; }
            public string size_unit { get; set; }
            public string mod_name { get; set; }
            public string mod_game { get; set; }
            public string file_master_title { get; set; }
            public string file_master_file { get; set; }

            public string GameIconSource { get; private set; }

            public void Onmod_gameChanged()
            {
                switch (mod_game)
                {
                    case "masseffect":
                        GameIconSource = @"/images/gameicons/ME1_48.ico";
                        break;
                    case "masseffect2":
                        GameIconSource = @"/images/gameicons/ME2_48.ico";
                        break;
                    case "masseffect3":
                        GameIconSource = @"/images/gameicons/ME3_48.ico";
                        break;
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is SearchedItemResult sir)
            {
                var outboundUrl = $@"https://nexusmods.com/{sir.mod_game}/mods/{sir.mod_id}?tab=files";
                Utilities.OpenWebpage(outboundUrl);
            }
        }

        private class SearchTopLevelResult
        {
            public List<SearchedItemResult> mod_ids { get; set; }
        }
    }


}
