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
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using MassEffectModManagerCore.modmanager.me3tweaks;
using PropertyChanged;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BackupFileFetcher.xaml
    /// </summary>
    public partial class BackupFileFetcher : MMBusyPanelBase
    {
        public string LoadingText { get; set; }
        public ObservableCollectionExtended<BackupFetcherGame> Games { get; } = new();
        public BackupFileFetcher()
        {
            DataContext = this;
            LoadCommands();
            if (Settings.GenerationSettingOT)
            {
                Games.Add(new BackupFetcherGame(MEGame.ME1, window));
                Games.Add(new BackupFetcherGame(MEGame.ME2, window));
                Games.Add(new BackupFetcherGame(MEGame.ME3, window));
            }
            if (Settings.GenerationSettingLE)
            {
                Games.Add(new BackupFetcherGame(MEGame.LE1, window));
                Games.Add(new BackupFetcherGame(MEGame.LE2, window));
                Games.Add(new BackupFetcherGame(MEGame.LE3, window));
            }
            InitializeComponent();
        }

        public bool LoadingInProgress { get; set; } = true;
        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
        }

        [AddINotifyPropertyChangedInterface]
        public class BackupFetcherGame
        {
            public BackupFetcherGame(MEGame game, Window mainWindow)
            {
                this.mainWindow = mainWindow;
                this.Game = game;
                FilesView.Filter = FilterBackupFiles;
                FetchFileCommand = new GenericCommand(FetchFile, CanFetchFile);
            }

            public MEGame Game { get; set; }
            public string GameName => Game.ToGameName(true);
#if DEBUG
            public bool ShowMixinSourceOption => Game == MEGame.ME3;
            public bool ExtractAsMixinSource { get; set; }

#else
        public bool ShowMixinSourceOption => false;
        public bool ExtractAsMixinSource {get => false; set { }}
#endif
            public BackupFile SelectedFile { get; set; }
            public string FilterText { get; set; }
            public ObservableCollectionExtended<BackupFile> BackupFiles { get; } = new();
            public ICollectionView FilesView => CollectionViewSource.GetDefaultView(BackupFiles);
            private bool FilterBackupFiles(object obj)
            {
                if (!string.IsNullOrWhiteSpace(FilterText) && obj is BackupFile bobj)
                {
                    return bobj.Filename.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase);
                }
                return true;
            }

            public ICommand FetchFileCommand { get; set; }

            private Window mainWindow;

            public void OnFilterTextChanged()
            {
                FilesView.Refresh();
            }

            private bool CanFetchFile() => SelectedFile != null;

            public void FetchFile()
            {
                SaveFileDialog m = new SaveFileDialog
                {
                    Title = M3L.GetString(M3L.string_selectDestinationLocation),
                    Filter = M3L.GetString(M3L.string_packageFile) + @"|*" + Path.GetExtension(SelectedFile.Filename), //TODO CHANGE FILTER
                    FileName = SelectedFile.Filename
                };
                var result = m.ShowDialog(mainWindow);
                if (result.Value)
                {
                    if (SelectedFile.Module == @"BASEGAME")
                    {
                        var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(Game, SelectedFile.Filename);

                        if (Game == MEGame.ME3 && ExtractAsMixinSource)
                        {
                            // Decompress and save as mixin rules
                            var p = MEPackageHandler.OpenMEPackageFromStream(fetchedfilestream);
                            p.Save(m.FileName, includeAdditionalPackagesToCook: false, includeDependencyTable: true);
                        }
                        else
                        {
                            fetchedfilestream.WriteToFile(m.FileName);
                        }
                    }
                    else
                    {
                        MemoryStream fetchedfilestream;
                        if (Game.IsGame1() || Game.IsGame2() || Game == MEGame.LE3)
                        {
                            fetchedfilestream = VanillaDatabaseService.FetchME1ME2DLCFile(Game, SelectedFile.Module, SelectedFile.Filename);
                        }
                        else
                        {
                            // ME3
                            fetchedfilestream = VanillaDatabaseService.FetchFileFromVanillaSFAR(SelectedFile.Module, SelectedFile.Filename);
                        }
                        fetchedfilestream.WriteToFile(m.FileName);
                    }
                    M3L.ShowDialog(mainWindow, M3L.GetString(M3L.string_interp_fileFetchedAndWrittenToX, m.FileName), M3L.GetString(M3L.string_fileFetched));
                }
            }

            private const string FileExtensions = @"\.u|\.upk|\.sfm|\.pcc|\.afc|\.bin|\.tlk|\.usf|\.dlc\.ini";
            public void LoadBackupFiles()
            {
                var gameFiles = new List<BackupFile>();
                var bup = BackupService.GetGameBackupPath(Game);
                if (bup != null)
                {
                    var target = new GameTarget(Game, bup, false, skipInit: true);
                    var cookedPath = M3Directories.GetCookedPath(target);
                    foreach (var f in Extensions.GetFiles(cookedPath, FileExtensions, SearchOption.AllDirectories))
                    {
                        gameFiles.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                    }

                    gameFiles.Sort(); //sort basegame

                    var dlcDir = M3Directories.GetDLCPath(target);
                    var officialDLC = VanillaDatabaseService.GetInstalledOfficialDLC(target);

                    foreach (var dlcName in officialDLC)
                    {
                        if (Game == MEGame.ME3)
                        {
                            var sfarPath = Path.Combine(dlcDir, dlcName, @"CookedPCConsole", @"Default.sfar");
                            if (File.Exists(sfarPath))
                            {
                                var filesToAdd = new List<BackupFile>();
                                DLCPackage dlc = new DLCPackage(sfarPath);
                                foreach (var f in dlc.Files)
                                {
                                    filesToAdd.Add(new BackupFile(dlcName, Path.GetFileName(f.FileName)));
                                }

                                filesToAdd.Sort();
                                gameFiles.AddRange(filesToAdd);
                            }
                        }
                        else
                        {
                            // Non ME3
                            var cookedDLCPath = Path.Combine(dlcDir, dlcName, Game.CookedDirName());
                            if (Directory.Exists(cookedDLCPath))
                            {
                                var filesToAdd = new List<BackupFile>();

                                foreach (var f in Extensions.GetFiles(cookedDLCPath, FileExtensions,
                                    SearchOption.AllDirectories))
                                {
                                    filesToAdd.Add(new BackupFile(dlcName, Path.GetFileName(f)));
                                }

                                filesToAdd.Sort();
                                gameFiles.AddRange(filesToAdd);
                            }

                        }
                    }

                    //TESTPATCH
                    if (Game == MEGame.ME3)
                    {
                        var tpPath = M3Directories.GetTestPatchSFARPath(target);
                        if (File.Exists(tpPath))
                        {
                            var filesToAdd = new List<BackupFile>();
                            DLCPackage dlc = new DLCPackage(tpPath);
                            foreach (var f in dlc.Files)
                            {
                                filesToAdd.Add(new BackupFile(@"TESTPATCH", Path.GetFileName(f.FileName)));
                            }

                            filesToAdd.Sort();
                            gameFiles.AddRange(filesToAdd);
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    BackupFiles.ReplaceAll(gameFiles);
                });
                Debug.WriteLine($@"Num {Game} files: " + BackupFiles.Count);
            }
        }

        /*
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
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(MEGame.ME2, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchME1ME2DLCFile(MEGame.ME2, fileTofetch.Module, fileTofetch.Filename);
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
                    var fetchedfilestream = VanillaDatabaseService.FetchBasegameFile(MEGame.ME1, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
                else
                {
                    var fetchedfilestream = VanillaDatabaseService.FetchME1ME2DLCFile(MEGame.ME1, fileTofetch.Module, fileTofetch.Filename);
                    fetchedfilestream.WriteToFile(m.FileName);
                }
            }
            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_fileFetchedAndWrittenToX, m.FileName), M3L.GetString(M3L.string_fileFetched));
        }*/

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
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"BackupFileFetcher-Load");
            nbw.DoWork += (a, b) =>
            {
                foreach (var g in Games)
                {
                    LoadingText = M3L.GetString(M3L.string_interp_loadingBackupFilesForGame, g.Game.ToGameName());
                    g.LoadBackupFiles();
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                LoadingInProgress = false;
            };
            nbw.RunWorkerAsync();
        }
        /*
        private void LoadME1FilesList()
        {
            var me1files = new List<BackupFile>();
            var bup = BackupService.GetGameBackupPath(MEGame.ME1);
            if (bup != null)
            {
                var target = new GameTarget(MEGame.ME1, bup, false);
                var cookedPath = M3Directories.GetCookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.u|\.upk|\.sfm", SearchOption.AllDirectories))
                {
                    me1files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me1files.Sort(); //sort basegame

                var dlcDir = M3Directories.GetDLCPath(target);
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

            var bup = BackupService.GetGameBackupPath(MEGame.ME2);
            if (bup != null)
            {
                var target = new GameTarget(MEGame.ME2, bup, false);
                var cookedPath = M3Directories.GetCookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.pcc|\.tfc|\.afc|\.bin|\.tlk", SearchOption.AllDirectories))
                {
                    me2files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me2files.Sort(); //sort basegame

                var dlcDir = M3Directories.GetDLCPath(target);
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

        private void LoadME3FilesList()
        {
            var me3files = new List<BackupFile>();
            var bup = BackupService.GetGameBackupPath(MEGame.ME3);
            if (bup != null)
            {
                var target = new GameTarget(MEGame.ME3, bup, false);
                var cookedPath = M3Directories.GetCookedPath(target);
                foreach (var f in Extensions.GetFiles(cookedPath, @"\.pcc|\.tfc|\.afc|\.bin|\.tlk", SearchOption.AllDirectories))
                {
                    me3files.Add(new BackupFile(@"BASEGAME", Path.GetFileName(f)));
                }
                me3files.Sort(); //sort basegame

                var dlcDir = M3Directories.GetDLCPath(target);
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
                var tpPath = M3Directories.GetTestPatchSFARPath(target);
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
        */
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
