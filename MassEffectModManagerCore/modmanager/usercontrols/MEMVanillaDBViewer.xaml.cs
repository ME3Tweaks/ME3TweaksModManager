using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MEMVanillaDBViewer.xaml
    /// </summary>
    public partial class MEMVanillaDBViewer : MMBusyPanelBase
    {
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

        public string FilterTextME1 { get; set; }
        public string FilterTextME2 { get; set; }
        public string FilterTextME3 { get; set; }
        public string FilterTextLE1 { get; set; }
        public string FilterTextLE2 { get; set; }
        public string FilterTextLE3 { get; set; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


        public override void OnPanelVisible()
        {

            var db = VanillaDatabaseService.LoadDatabaseFor(MEGame.ME1, false);
            ME1Files.ReplaceAll(getDBItems(db));
            db = VanillaDatabaseService.LoadDatabaseFor(MEGame.ME2,false);
            ME2Files.ReplaceAll(getDBItems(db));
            db = VanillaDatabaseService.LoadDatabaseFor(MEGame.ME3, false);
            ME3Files.ReplaceAll(getDBItems(db));

            db = VanillaDatabaseService.LoadDatabaseFor(MEGame.LE1, false);
            LE1Files.ReplaceAll(getDBItems(db));
            db = VanillaDatabaseService.LoadDatabaseFor(MEGame.LE2, false);
            LE2Files.ReplaceAll(getDBItems(db));
            db = VanillaDatabaseService.LoadDatabaseFor(MEGame.LE3, false);
            LE3Files.ReplaceAll(getDBItems(db));

            LoadingInProgress = false;
            ME1FilesView.Filter = FilterBackupFilesME1;
            ME2FilesView.Filter = FilterBackupFilesME2;
            ME3FilesView.Filter = FilterBackupFilesME3;
            LE1FilesView.Filter = FilterBackupFilesLE1;
            LE2FilesView.Filter = FilterBackupFilesLE2;
            LE3FilesView.Filter = FilterBackupFilesLE3;

        }

        private IEnumerable<VanillaEntry> getDBItems(CaseInsensitiveDictionary<List<(int size, string md5)>> db)
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


        private ui.ObservableCollectionExtended<VanillaEntry> ME1Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        private ui.ObservableCollectionExtended<VanillaEntry> ME2Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        private ui.ObservableCollectionExtended<VanillaEntry> ME3Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        public ICollectionView ME1FilesView => CollectionViewSource.GetDefaultView(ME1Files);
        public ICollectionView ME2FilesView => CollectionViewSource.GetDefaultView(ME2Files);
        public ICollectionView ME3FilesView => CollectionViewSource.GetDefaultView(ME3Files);

        private ui.ObservableCollectionExtended<VanillaEntry> LE1Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        private ui.ObservableCollectionExtended<VanillaEntry> LE2Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        private ui.ObservableCollectionExtended<VanillaEntry> LE3Files { get; } = new ui.ObservableCollectionExtended<VanillaEntry>();
        public ICollectionView LE1FilesView => CollectionViewSource.GetDefaultView(LE1Files);
        public ICollectionView LE2FilesView => CollectionViewSource.GetDefaultView(LE2Files);
        public ICollectionView LE3FilesView => CollectionViewSource.GetDefaultView(LE3Files);

        //These are separate methods because I don t want to have to do a looped if statement 6000 times for me3 for example.
        private bool FilterBackupFilesME1(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME1) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextME1, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesME2(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME2) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextME2, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesME3(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME3) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextME3, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesLE1(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextLE1) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextLE1, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesLE2(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextLE2) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextLE2, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesLE3(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextLE3) && obj is VanillaEntry bobj)
            {
                return bobj.Filepath.Contains(FilterTextLE3, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }





        public void OnFilterTextME1Changed()
        {
            ME1FilesView.Refresh();
        }
        public void OnFilterTextME2Changed()
        {
            ME2FilesView.Refresh();
        }
        public void OnFilterTextME3Changed()
        {
            ME3FilesView.Refresh();
        }

        public void OnFilterTextLE1Changed()
        {
            LE1FilesView.Refresh();
        }
        public void OnFilterTextLE2Changed()
        {
            LE2FilesView.Refresh();
        }
        public void OnFilterTextLE3Changed()
        {
            LE3FilesView.Refresh();
        }

        public class VanillaEntry
        {
            public string Filepath { get; set; }
            public string MD5 { get; set; }
            public int Size { get; set; }
        }
    }
}
