using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModMakerPanel.xaml
    /// </summary>
    public partial class ModMakerPanel : MMBusyPanelBase
    {
        private bool KeepOpenWhenThreadFinishes;
        public bool ShowCloseButton { get; set; }
        public bool CanInjectKeybinds { get; set; }
        public bool LocalFileOption { get; set; }
        public string LocalFilePath { get; set; }
        public string ModMakerCode { get; set; }
        public M3OnlineContent.ServerModMakerModInfo SelectedTopMod { get; set; }
        public long CurrentTaskValue { get; private set; }
        public long CurrentTaskMaximum { get; private set; } = 100;
        public bool CurrentTaskIndeterminate { get; private set; }
        public long OverallValue { get; private set; }
        public long OverallMaximum { get; private set; } = 100;
        public bool OverallIndeterminate { get; private set; }
        public bool CompileInProgress { get; set; }
        public string DownloadAndModNameText { get; set; } = M3L.GetString(M3L.string_enterModMakerModCodeOrSelectFromTheTopMods);
        public string CurrentTaskString { get; set; }
        public ModMakerPanel()
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"ModMaker Panel", this);
            DataContext = this;
            LoadCommands();
            GetTopMods();
        }

        private void GetTopMods()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModMaker-TopModsFetch");
            nbw.DoWork += (a, b) =>
            {
                b.Result = M3OnlineContent.FetchTopModMakerMods();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null && b.Result is List<M3OnlineContent.ServerModMakerModInfo> topMods)
                {
                    TopMods.ReplaceAll(topMods);
                    TriggerResize();
                }
            };
            nbw.RunWorkerAsync();
        }

        public ObservableCollectionExtended<M3OnlineContent.ServerModMakerModInfo> TopMods { get; } = new ObservableCollectionExtended<M3OnlineContent.ServerModMakerModInfo>();

        public ICommand DownloadCompileCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand OpenModMakerCommand { get; private set; }
        public ICommand BrowseForModmakerFileCommand { get; private set; }
        public ICommand DownloadCompileLocalCommand { get; private set; }


        private void LoadCommands()
        {
            DownloadCompileCommand = new GenericCommand(StartCompiler, CanStartCompiler);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
            OpenModMakerCommand = new GenericCommand(OpenModMaker);
            BrowseForModmakerFileCommand = new GenericCommand(BrowseForLocalFile, CanBrowseForLocalFile);
        }

        private bool CanBrowseForLocalFile() => !CompileInProgress && BackupService.GetGameBackupPath(MEGame.ME3) != null;

        private void BrowseForLocalFile()
        {
            OpenFileDialog m = new OpenFileDialog
            {
                Title = M3L.GetString(M3L.string_selectModMakerModXmlFile),
                Filter = M3L.GetString(M3L.string_modMakerXMLFiles) + @"|*.xml",
                InitialDirectory = M3Filesystem.GetModmakerDefinitionsCache()
            };
            var result = m.ShowDialog(window);
            if (result.Value)
            {
                //TelemetryInterposer.TrackEvent(@"User opened mod archive for import", new Dictionary<string, string> { { @"Method", @"Manual file selection" }, { @"Filename", Path.GetFileName(m.FileName) } });
                LocalFilePath = m.FileName;
            }
        }

        private void OpenModMaker()
        {
            M3Utilities.OpenWebpage(@"https://me3tweaks.com/modmaker");
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public void OnSelectedTopModChanged()
        {
            if (SelectedTopMod != null)
            {
                ModMakerCode = SelectedTopMod.mod_id;
                LocalFileOption = false;
            }
        }

        private bool CanClose() => !CompileInProgress;

        private void StartCompiler()
        {
            CompileInProgress = true;
            //Settings.Save(); //Persist controller mixin option, keybinds injection
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModmakerCompiler");

            nbw.DoWork += (a, b) =>
            {
                string modDelta = null;

                if (int.TryParse(ModMakerCode, out var code))
                {
                    DownloadAndModNameText = M3L.GetString(M3L.string_downloadingModDeltaFromME3Tweaks);
                    var normalEndpoint = M3OnlineContent.ModmakerModsEndpoint + code;
                    var lzmaEndpoint = normalEndpoint + @"&method=lzma";
                    M3Log.Information($@"Downloading modmaker mod {code}");

                    //Try LZMA first
                    try
                    {
                        var download = M3OnlineContent.DownloadToMemory(lzmaEndpoint, (done, total) =>
                        {
                            if (total != -1)
                            {
                                var suffix = $@" {(done * 100.0 / total).ToString(@"0")}%"; //do not localize
                                DownloadAndModNameText = M3L.GetString(M3L.string_downloadingModDeltaFromME3Tweaks) + suffix;
                            }
                            else
                            {
                                DownloadAndModNameText = M3L.GetString(M3L.string_downloadingModDeltaFromME3Tweaks);
                            }
                        });
                        if (download.errorMessage == null)
                        {
                            DownloadAndModNameText = M3L.GetString(M3L.string_decompressingDelta);
                            // OK
                            modDelta = Encoding.UTF8.GetString(StreamingLZMAWrapper.DecompressLZMA(download.result));
                        }
                        else
                        {
                            M3Log.Error(@"Error downloading lzma mod delta to memory: " + download.errorMessage);
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Error(@"Error downloading LZMA mod delta to memory: " + e.Message);
                    }

                    if (modDelta == null)
                    {
                        //failed to download LZMA.
                        var download = M3OnlineContent.DownloadToMemory(normalEndpoint, (done, total) =>
                        {
                            var suffix = $" {(done * 100.0 / total).ToString(@"0")}%"; //do not localize
                            DownloadAndModNameText = M3L.GetString(M3L.string_downloadingModDeltaFromME3Tweaks) + suffix;
                        });
                        if (download.errorMessage == null)
                        {
                            //OK
                            modDelta = Encoding.UTF8.GetString(download.result.ToArray());
                        }
                        else
                        {
                            M3Log.Error(@"Error downloading decompressed mod delta to memory: " + download.errorMessage);
                        }
                    }
                }
                else if (File.Exists(LocalFilePath))
                {
                    modDelta = File.ReadAllText(LocalFilePath);
                }


                if (modDelta != null)
                {
                    KeepOpenWhenThreadFinishes = false;
                    var compiler = new ModMakerCompiler(code);
                    compiler.SetCurrentMaxCallback = SetCurrentMax;
                    compiler.SetCurrentValueCallback = SetCurrentProgressValue;
                    compiler.SetOverallMaxCallback = SetOverallMax;
                    compiler.SetOverallValueCallback = SetOverallValue;
                    compiler.SetCurrentTaskIndeterminateCallback = SetCurrentTaskIndeterminate;
                    compiler.SetCurrentTaskStringCallback = SetCurrentTaskString;
                    compiler.SetModNameCallback = SetModNameOrDownloadText;
                    compiler.SetCompileStarted = CompilationInProgress;
                    compiler.SetModNotFoundCallback = ModNotFound;
                    compiler.NotifySomeDLCIsMissing = NotifySomeDLCIsMissing;
                    compiler.ShowErrorMessageCallback = ShowErrorMessage;
                    var m = compiler.DownloadAndCompileMod(modDelta);
                    if (m != null && !LocalFileOption)
                    {
                        var sanitizedname = M3Utilities.SanitizePath(m.ModName);
                        File.WriteAllText(Path.Combine(M3Filesystem.GetModmakerDefinitionsCache(), $@"{code}-{sanitizedname}.xml"), modDelta);
                    }
                    b.Result = m;
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                CompileInProgress = false;
                if (!KeepOpenWhenThreadFinishes && b.Error == null && b.Result is Mod m)
                {
                    OnClosing(new DataEventArgs(m));
                }
                else
                {
                    CloseProgressPanel();
                    //ShowCloseButton = true;
                }
                CommandManager.InvalidateRequerySuggested();

            };
            nbw.RunWorkerAsync();
        }

        private void ShowErrorMessage(string obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                M3L.ShowDialog(window, obj, M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private bool NotifySomeDLCIsMissing(List<string> listItems)
        {
            bool result = false;
            Application.Current.Dispatcher.Invoke(delegate
            {
                var missingDLC = string.Join("\n - ", listItems); //do not localize
                missingDLC = @" - " + missingDLC; //add first -
                result = M3L.ShowDialog(window,
M3L.GetString(M3L.string_interp_modmakerDlcMissing, missingDLC),
M3L.GetString(M3L.string_dlcMissing),
MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            });
            return result;
        }

        private void ModNotFound()
        {
            KeepOpenWhenThreadFinishes = true;
        }

        private void SetModNameOrDownloadText(string obj)
        {
            DownloadAndModNameText = obj;
        }

        private void SetCurrentProgressValue(int obj)
        {
            CurrentTaskValue = obj;
        }

        private void SetCurrentTaskString(string obj)
        {
            CurrentTaskString = obj;
        }

        private void SetCurrentTaskIndeterminate(bool obj)
        {
            CurrentTaskIndeterminate = obj;
        }

        private void SetOverallMax(int obj)
        {
            OverallMaximum = obj;
        }

        private void SetOverallValue(int obj)
        {
            OverallValue = obj;
        }

        private void SetCurrentMax(int obj)
        {
            CurrentTaskMaximum = obj;
        }

        private bool CanStartCompiler() => (LocalFileOption ? File.Exists(LocalFilePath) : int.TryParse(ModMakerCode, out var _)) && !CompileInProgress && BackupService.GetGameBackupPath(MEGame.ME3) != null;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override async void OnPanelVisible()
        {
            InitializeComponent();
            CanInjectKeybinds = File.Exists(KeybindsInjectorPanel.GetDefaultKeybindsOverride(MEGame.ME3));
            if (BackupService.GetGameBackupPath(MEGame.ME3) == null)
            {
                M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_me3tweaksModMakerRequiresBackup), M3L.GetString(M3L.string_noBackupAvailable), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(100); //This is so UI control can fully load. It's kind of a hack to make the next focus call work
            ModMakerCode_TextBox.Focus();
        }

        public void CompilationInProgress()
        {
            //Close entry dialog
            Application.Current.Dispatcher.Invoke(delegate
            {
                Storyboard sb = this.FindResource(@"CloseInfoPanel") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }
                DownloadInfoPanel.Height = DownloadInfoPanel.ActualHeight;
                Storyboard.SetTarget(sb, DownloadInfoPanel);
                sb.Begin();

                //Open Progress Panel
                sb = this.FindResource(@"OpenProgressPanel") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }

                sb.Completed += (sender, args) =>
                {
                    // Animation completed
                    DisableM3AutoSizer = true;
                    TriggerResize();
                };
                DownloadingProgressPanel.Height = DownloadingProgressPanel.ActualHeight;
                Storyboard.SetTarget(sb, DownloadingProgressPanel);
                sb.Begin();
            });
        }



        public void CloseProgressPanel()
        {
            //Close entry dialog
            Application.Current.Dispatcher.Invoke(delegate
            {

                //Open Progress Panel
                var sb = this.FindResource(@"CloseProgressPanel") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }

                sb.Completed += (sender, args) =>
                {
                    // Animation completed
                    DisableM3AutoSizer = true;
                    TriggerResize();
                };

                DownloadingProgressPanel.Height = DownloadingProgressPanel.ActualHeight;
                Storyboard.SetTarget(sb, DownloadingProgressPanel);
                sb.Begin();
            });
        }


        private void ModMakerCodeTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && CanStartCompiler())
            {
                StartCompiler();
            }
        }

        // This panel changes size so do not use autosizer
        //public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
