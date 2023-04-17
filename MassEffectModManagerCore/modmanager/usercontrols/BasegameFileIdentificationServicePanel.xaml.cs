using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.ui;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BasegameFileIdentificationServicePanel.xaml
    /// </summary>
    public partial class BasegameFileIdentificationServicePanel : MMBusyPanelBase
    {
        public BasegameGameDB SelectedGame { get; set; }
        public BasegameFileIdentificationServicePanel()
        {
            DataContext = this;
            LoadCommands();
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
            InitializeComponent();
            LoadBGFISDBs();
            LoadingInProgress = false;
        }

        public ObservableCollectionExtended<BasegameGameDB> Games { get; } = new();

        private void LoadBGFISDBs()
        {
            var games = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3, MEGame.LELauncher };
            foreach (var g in games)
            {
                Games.Add(new BasegameGameDB(g));
            }
        }

        public class BasegameFileUIRecord
        {
            public ObservableCollectionExtended<BasegameFileRecord> Records { get; } = new();

            public string Filename { get; }

            public BasegameFileUIRecord(string relativeFilePath, List<BasegameFileRecord> records)
            {
                Filename = relativeFilePath;
                Records.ReplaceAll(records);
            }
        }

        /// <summary>
        /// UI object that encapsulates a single game's entries from BGFIS
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class BasegameGameDB
        {
            public MEGame Game { get; }
            public string GameName => Game.ToGameName(true);
            public string SearchText { get; set; }
            private ObservableCollectionExtended<BasegameFileUIRecord> Files { get; } = new();
            public ICollectionView FilesView => CollectionViewSource.GetDefaultView(Files);
            public BasegameGameDB(MEGame game)
            {
                this.Game = game;

                var mapping = BasegameFileIdentificationService.GetEntriesForGame(game);
                foreach (var file in mapping)
                {
                    Files.Add(new BasegameFileUIRecord(file.Key, file.Value));
                }

                FilesView.Filter = FilterFiles;
            }

            public void OnSearchTextChanged()
            {
                FilesView.Refresh();
            }

            private bool FilterFiles(object obj)
            {
                if (!string.IsNullOrWhiteSpace(SearchText) && obj is BasegameFileUIRecord bobj)
                {
                    return bobj.Filename.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase);
                }
                return true;
            }
        }
    }
}
