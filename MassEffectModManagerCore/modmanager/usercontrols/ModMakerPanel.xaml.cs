using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModMakerPanel.xaml
    /// </summary>
    public partial class ModMakerPanel : MMBusyPanelBase
    {
        private bool KeepOpenWhenThreadFinishes;
        public string ModMakerCode { get; set; }
        public long CurrentTaskValue { get; private set; }
        public long CurrentTaskMaximum { get; private set; } = 100;
        public bool CurrentTaskIndeterminate { get; private set; }
        public long OverallValue { get; private set; }
        public long OverallMaximum { get; private set; } = 100;
        public bool OverallIndeterminate { get; private set; }
        public bool CompileInProgress { get; set; }
        public string DownloadAndModNameText { get; set; } = "Enter a ModMaker mod code";
        public string CurrentTaskString { get; set; }
        public ModMakerPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public ICommand DownloadCompileCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            DownloadCompileCommand = new GenericCommand(StartCompiler, CanStartCompiler);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !CompileInProgress;

        private void StartCompiler()
        {
            CompileInProgress = true;
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModmakerCompiler");

            bw.DoWork += (a, b) =>
            {
                //Todo: Add checkbox to use local version instead
                if (int.TryParse(ModMakerCode, out var code))
                {
                    DownloadAndModNameText = "Downloading mod delta from ME3Tweaks";
                    var normalEndpoint = OnlineContent.ModmakerModsEndpoint + code;
                    var lzmaEndpoint = normalEndpoint + "&method=lzma";

                    string modDelta = null;

                    //Try LZMA first
                    try
                    {
                        var download = OnlineContent.DownloadToMemory(lzmaEndpoint, (done, total) =>
                        {
                            if (total != -1)
                            {
                                DownloadAndModNameText = $"Downloading mod delta from ME3Tweaks {(done * 100.0 / total).ToString("0")}%";
                            }
                            else
                            {
                                DownloadAndModNameText = $"Downloading mod delta from ME3Tweaks";
                            }
                        });
                        if (download.errorMessage == null)
                        {
                            DownloadAndModNameText = "Decompressing delta";
                            // OK
                            var decompressed = SevenZipHelper.LZMA.DecompressLZMAFile(download.result.ToArray());
                            modDelta = Encoding.UTF8.GetString(decompressed);
                            // File.WriteAllText(@"C:\users\mgamerz\desktop\decomp.txt", modDelta);
                        }
                        else
                        {
                            Log.Error("Error downloading lzma mod delta to memory: " + download.errorMessage);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error downloading LZMA mod delta to memory: " + e.Message);
                    }

                    if (modDelta == null)
                    {
                        //failed to download LZMA.
                        var download = OnlineContent.DownloadToMemory(normalEndpoint, (done, total) =>
                        {
                            DownloadAndModNameText = $"Downloading mod delta from ME3Tweaks {(done * 100.0 / total).ToString("0")}%";
                        });
                        if (download.errorMessage == null)
                        {
                            //OK
                            modDelta = Encoding.UTF8.GetString(download.result.ToArray());
                        }
                        else
                        {
                            Log.Error("Error downloading decompressed mod delta to memory: " + download.errorMessage);
                        }
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
                        Mod m = compiler.DownloadAndCompileMod(modDelta);
                        File.WriteAllText(System.IO.Path.Combine(Utilities.GetModmakerDefinitionsCache(), code + ".xml"), modDelta);
                        b.Result = m;
                    }
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                CompileInProgress = false;
                if (!KeepOpenWhenThreadFinishes && b.Result is Mod m)
                {
                    OnClosing(new DataEventArgs(m));
                }

            };
            bw.RunWorkerAsync();
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

        private bool CanStartCompiler() => int.TryParse(ModMakerCode, out var _) && !CompileInProgress;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

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
                DownloadingProgressPanel.Height = DownloadingProgressPanel.ActualHeight;
                Storyboard.SetTarget(sb, DownloadingProgressPanel);
                sb.Begin();
            });
        }
    }
}
