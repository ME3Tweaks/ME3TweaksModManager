using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.sfar;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupFileFetcher.xaml
    /// </summary>
    public partial class BackupFileFetcher : MMBusyPanelBase
    {
        public BackupFileFetcher()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }
        public ICommand FetchFileCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
            FetchFileCommand = new GenericCommand(FetchFile, CanFetchFile);
        }

        private void FetchFile()
        {
            if (SelectedGameIndex == 0 && SelectedME1File != null)
            {
                FetchME1File();
            }
            if (SelectedGameIndex == 1 && SelectedME2File != null)
            {
                FetchME2File();
            }
            if (SelectedGameIndex == 2 && SelectedME3File != null)
            {
                FetchME3File();
            }
        }

        private void FetchME3File()
        {
            BackupFile fileTofetch = SelectedME3File;
            SaveFileDialog m = new SaveFileDialog
            {
                Title = "Select destination location",
                Filter = "Package file|*" + Path.GetExtension(fileTofetch.Filename),
                FileName = fileTofetch.Filename
            };
            var result = m.ShowDialog(mainwindow);
            if (result.Value)
            {
                if (fileTofetch.Module == "BASEGAME")
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME3, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchFileFromVanillaSFAR(fileTofetch.Module, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                Xceed.Wpf.Toolkit.MessageBox.Show($"File fetched and written to {m.FileName}.", "File fetched");
            }
        }

        private void FetchME2File()
        {
            throw new NotImplementedException();
        }

        private void FetchME1File()
        {
            throw new NotImplementedException();
        }

        private bool CanFetchFile()
        {
            if (SelectedGameIndex == 0 && SelectedME1File != null) return true;
            if (SelectedGameIndex == 1 && SelectedME2File != null) return true;
            if (SelectedGameIndex == 2 && SelectedME3File != null) return true;
            return false;
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public string FilterTextME1 { get; set; }
        public string FilterTextME2 { get; set; }
        public string FilterTextME3 { get; set; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }


        public override void OnPanelVisible()
        {
            LoadME3FilesList();
            ME1FilesView.Filter = FilterBackupFilesME1;
            ME2FilesView.Filter = FilterBackupFilesME2;
            ME3FilesView.Filter = FilterBackupFilesME3;
        }

        private ObservableCollectionExtended<BackupFile> ME1Files { get; } = new ObservableCollectionExtended<BackupFile>();
        private ObservableCollectionExtended<BackupFile> ME2Files { get; } = new ObservableCollectionExtended<BackupFile>();
        private ObservableCollectionExtended<BackupFile> ME3Files { get; } = new ObservableCollectionExtended<BackupFile>();
        public ICollectionView ME1FilesView => CollectionViewSource.GetDefaultView(ME1Files);
        public ICollectionView ME2FilesView => CollectionViewSource.GetDefaultView(ME2Files);
        public ICollectionView ME3FilesView => CollectionViewSource.GetDefaultView(ME3Files);
        public int SelectedGameIndex { get; set; }

        public BackupFile SelectedME1File { get; set; }
        public BackupFile SelectedME2File { get; set; }
        public BackupFile SelectedME3File { get; set; }

        //These are separate methods because I don t want to have to do a looped if statement 6000 times for me3 for example.
        private bool FilterBackupFilesME1(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME1) && obj is BackupFile bobj)
            {
                return bobj.Filename.Contains(FilterTextME1, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesME2(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME2) && obj is BackupFile bobj)
            {
                return bobj.Filename.Contains(FilterTextME2, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }

        private bool FilterBackupFilesME3(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterTextME3) && obj is BackupFile bobj)
            {
                return bobj.Filename.Contains(FilterTextME3, StringComparison.InvariantCultureIgnoreCase);
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

        private void LoadME3FilesList()
        {

            var bup = Utilities.GetGameBackupPath(Mod.MEGame.ME3);
            if (bup != null)
            {
                var target = new GameTarget(Mod.MEGame.ME3, bup, false);
                var cookedPath = MEDirectories.CookedPath(target);
                foreach (var f in Directory.EnumerateFiles(cookedPath, "*.pcc", SearchOption.TopDirectoryOnly))
                {
                    ME3Files.Add(new BackupFile("BASEGAME", Path.GetFileName(f)));
                }

                var dlcDir = MEDirectories.DLCPath(target);
                var officialDLC = VanillaDatabaseService.GetInstalledOfficialDLC(target);
                foreach (var v in officialDLC)
                {
                    var sfarPath = Path.Combine(dlcDir, v, "CookedPCConsole", "Default.sfar");
                    if (File.Exists(sfarPath))
                    {
                        DLCPackage dlc = new DLCPackage(sfarPath);
                        foreach (var f in dlc.Files)
                        {
                            if (f.FileName.EndsWith(".pcc"))
                            {
                                ME3Files.Add(new BackupFile(v, Path.GetFileName(f.FileName)));
                            }
                        }
                    }
                }
            }
            Debug.WriteLine("Num ME3 files: " + ME3Files.Count);
        }

        public class BackupFile
        {
            public string Module { get; }
            public string Filename { get; }

            public BackupFile(string module, string fileName)
            {
                Module = module;
                Filename = fileName;
            }
        }
    }
}
