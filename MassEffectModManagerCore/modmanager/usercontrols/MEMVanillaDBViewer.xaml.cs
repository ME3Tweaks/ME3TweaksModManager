using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using WinCopies.Util;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MEMVanillaDBViewer.xaml
    /// </summary>
    public partial class MEMVanillaDBViewer : MMBusyPanelBase
    {
        public MemGameDB SelectedGame { get; set; }
        public MEMVanillaDBViewer()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public bool LoadingInProgress { get; set; } = true;
        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


        public override void OnPanelVisible()
        {
            LoadMEMDBs();
            LoadingInProgress = false;
        }

        public LegendaryExplorerCore.Misc.ObservableCollectionExtended<MemGameDB> Games { get; }= new();

        private void LoadMEMDBs()
        {
            var games = new[] {MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3, MEGame.LELauncher};
            foreach (var g in games)
            {
                Games.Add(new MemGameDB(g));
            }
        }

        public class MemGameDB : INotifyPropertyChanged
        {
            public MEGame Game { get; }
            public string GameName => Game.ToGameName(true);
            public string SearchText { get; set; }
            private ui.ObservableCollectionExtended<VanillaEntry> Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
            public ICollectionView FilesView => CollectionViewSource.GetDefaultView(Files);
            public MemGameDB(MEGame game)
            {
                this.Game = game;
                Files.ReplaceAll(getDBItems(VanillaDatabaseService.LoadDatabaseFor(Game, false)));
                FilesView.Filter = FilterFiles;
            }

            public void OnSearchTextChanged()
            {
                FilesView.Refresh();
            }

            private bool FilterFiles(object obj)
            {
                if (!string.IsNullOrWhiteSpace(SearchText) && obj is VanillaEntry bobj)
                {
                    return bobj.Filepath.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase);
                }
                return true;
            }

#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        }

        private static IEnumerable<VanillaEntry> getDBItems(CaseInsensitiveDictionary<List<(int size, string md5)>> db)
        {
            var files = new List<VanillaEntry>();
            foreach (var v in db)
            {
                foreach (var sf in v.Value)
                {
                    files.Add(new VanillaEntry
                    {
                        Filepath = v.Key,
                        MD5 = sf.md5,
                        Size = sf.size
                    });
                }
            }
            return files;
        }
        
        public class VanillaEntry
        {
            public string Filepath { get; set; }
            public string MD5 { get; set; }
            public int Size { get; set; }
        }
    }
}
