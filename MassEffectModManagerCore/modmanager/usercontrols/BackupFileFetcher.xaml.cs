using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.sfar;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
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
using Serilog;

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

        public bool LoadingInProgress { get; set; } = true;
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
                Title = M3L.GetString(M3L.string_selectDestinationLocation),
                Filter = M3L.GetString(M3L.string_packageFile) + @"|*" + Path.GetExtension(fileTofetch.Filename),
                FileName = fileTofetch.Filename
            };
            var result = m.ShowDialog(mainwindow);
            if (result.Value)
            {
                if (fileTofetch.Module == @"BASEGAME")
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME3, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchFileFromVanillaSFAR(fileTofetch.Module, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_fileFetchedAndWrittenToX, m.FileName), M3L.GetString(M3L.string_fileFetched));
            }
        }

        private void FetchME2File()
        {
            BackupFile fileTofetch = SelectedME2File;
            SaveFileDialog m = new SaveFileDialog
            {
                Title = M3L.GetString(M3L.string_selectDestinationLocation),
                Filter = M3L.GetString(M3L.string_packageFile) + @"|*" + Path.GetExtension(fileTofetch.Filename),
                FileName = fileTofetch.Filename
            };
            var result = m.ShowDialog(mainwindow);
            if (result.Value)
            {
                if (fileTofetch.Module == @"BASEGAME")
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME2, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchME1ME2DLCFile(Mod.MEGame.ME2, fileTofetch.Module, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
            }

            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_fileFetchedAndWrittenToX, m.FileName), M3L.GetString(M3L.string_fileFetched));
        }


        private void FetchME1File()
        {
            BackupFile fileTofetch = SelectedME1File;
            SaveFileDialog m = new SaveFileDialog
            {
                Title = M3L.GetString(M3L.string_selectDestinationLocation),
                Filter = M3L.GetString(M3L.string_packageFile) + @"|*" + Path.GetExtension(fileTofetch.Filename),
                FileName = fileTofetch.Filename
            };
            var result = m.ShowDialog(mainwindow);
            if (result.Value)
            {
                if (fileTofetch.Module == @"BASEGAME")
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME1, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchME1ME2DLCFile(Mod.MEGame.ME1, fileTofetch.Module, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
            }
            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_fileFetchedAndWrittenToX, m.FileName), M3L.GetString(M3L.string_fileFetched));
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
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }


        public override void OnPanelVisible()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"BackupFileFetcher-Load");
            nbw.DoWork += (a, b) =>
            {
                LoadME1FilesList();
                LoadME2FilesList();
                LoadME3FilesList();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occured in {nbw.Name} thread: {b.Error.Message}");
                }
                LoadingInProgress = false;
                ME1FilesView.Filter = FilterBackupFilesME1;
                ME2FilesView.Filter = FilterBackupFilesME2;
                ME3FilesView.Filter = FilterBackupFilesME3;
            };
            nbw.RunWorkerAsync();
        }

        private void LoadME1FilesList()
        {
            var me1files = new List<BackupFile>();
            var bup = Utilities.GetGameBackupPath(Mod.MEGame.ME1);
            if (bup != null)
            {
                var target = new GameTarget(Mod.MEGame.ME1, bup, false);
                var cookedPath = MEDirectories.CookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.u|\.upk|\.sfm", SearchOption.AllDirectories))
                {
                    me1files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me1files.Sort(); //sort basegame

                var dlcDir = MEDirectories.DLCPath(target);
                var officialDLC = VanillaDatabaseService.GetInstalledOfficialDLC(target);
                foreach (var v in officialDLC)
                {
                    var cookedDLCPath = Path.Combine(dlcDir, v, @"CookedPC");
                    if (Directory.Exists(cookedDLCPath))
                    {
                        var filesToAdd = new List<BackupFile>();

                        foreach (var f in Extensions.GetFiles(cookedDLCPath, @"\.u|\.upk|\.sfm", SearchOption.AllDirectories))
                        {
                            filesToAdd.Add(new BackupFile(v, Path.GetFileName(f)));
                        }
                        filesToAdd.Sort();
                        me1files.AddRange(filesToAdd);
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(delegate
            {
                ME1Files.ReplaceAll(me1files);
            });
            Debug.WriteLine(@"Num ME1 files: " + ME2Files.Count);
        }

        private void LoadME2FilesList()
        {
            var me2files = new List<BackupFile>();

            var bup = Utilities.GetGameBackupPath(Mod.MEGame.ME2);
            if (bup != null)
            {
                var target = new GameTarget(Mod.MEGame.ME2, bup, false);
                var cookedPath = MEDirectories.CookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.pcc|\.tfc|\.afc|\.bin|\.tlk", SearchOption.AllDirectories))
                {
                    me2files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me2files.Sort(); //sort basegame

                var dlcDir = MEDirectories.DLCPath(target);
                var officialDLC = VanillaDatabaseService.GetInstalledOfficialDLC(target);
                foreach (var v in officialDLC)
                {
                    var cookedDLCPath = Path.Combine(dlcDir, v, @"CookedPC");
                    if (Directory.Exists(cookedDLCPath))
                    {
                        var filesToAdd = new List<BackupFile>();

                        foreach (var f in Directory.EnumerateFiles(cookedDLCPath, @"*.pcc", SearchOption.TopDirectoryOnly))
                        {
                            filesToAdd.Add(new BackupFile(v, Path.GetFileName(f)));
                        }
                        filesToAdd.Sort();
                        me2files.AddRange(filesToAdd);
                    }
                }
            }
            Application.Current.Dispatcher.Invoke(delegate
            {
                ME2Files.ReplaceAll(me2files);
            });
            Debug.WriteLine(@"Num ME2 files: " + ME2Files.Count);
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
            var me3files = new List<BackupFile>();
            var bup = Utilities.GetGameBackupPath(Mod.MEGame.ME3);
            if (bup != null)
            {
                var target = new GameTarget(Mod.MEGame.ME3, bup, false);
                var cookedPath = MEDirectories.CookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.pcc|\.tfc|\.afc|\.bin|\.tlk", SearchOption.AllDirectories))
                {
                    me3files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me3files.Sort(); //sort basegame

                var dlcDir = MEDirectories.DLCPath(target);
                var officialDLC = VanillaDatabaseService.GetInstalledOfficialDLC(target);
                foreach (var v in officialDLC)
                {
                    var sfarPath = Path.Combine(dlcDir, v, @"CookedPCConsole", @"Default.sfar");
                    if (File.Exists(sfarPath))
                    {
                        var filesToAdd = new List<BackupFile>();
                        DLCPackage dlc = new DLCPackage(sfarPath);
                        foreach (var f in dlc.Files)
                        {
                            filesToAdd.Add(new BackupFile(v, Path.GetFileName(f.FileName)));
                        }
                        filesToAdd.Sort();
                        me3files.AddRange(filesToAdd);
                    }
                }

                //TESTPATCH
                var tpPath = ME3Directory.GetTestPatchPath(target);
                if (File.Exists(tpPath))
                {
                    var filesToAdd = new List<BackupFile>();
                    DLCPackage dlc = new DLCPackage(tpPath);
                    foreach (var f in dlc.Files)
                    {
                        filesToAdd.Add(new BackupFile(@"TESTPATCH", Path.GetFileName(f.FileName)));
                    }
                    filesToAdd.Sort();
                    me3files.AddRange(filesToAdd);
                }
            }
            Application.Current.Dispatcher.Invoke(delegate
            {
                ME3Files.ReplaceAll(me3files);
            });
            Debug.WriteLine(@"Num ME3 files: " + ME3Files.Count);
        }

        public class BackupFile : IComparable<BackupFile>
        {
            public string Module { get; }
            public string Filename { get; }

            public BackupFile(string module, string fileName)
            {
                Module = module;
                Filename = fileName;
            }

            public int CompareTo(BackupFile other)
            {
                if (other.Module == Module) return Filename.CompareTo(other.Filename);
                return (Module.CompareTo(other.Module));
            }
        }
    }
}
