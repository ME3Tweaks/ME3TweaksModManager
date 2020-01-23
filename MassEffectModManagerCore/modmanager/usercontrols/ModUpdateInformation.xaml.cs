using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MassEffectModManagerCore;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Serilog;


namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModUpdateInformation.xaml
    /// </summary>
    public partial class ModUpdateInformation : MMBusyPanelBase
    {
        private bool AnyModUpdated;

        public ObservableCollectionExtended<OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollectionExtended<OnlineContent.ModUpdateInfo>();

        public bool OperationInProgress { get; set; }

        public ModUpdateInformation(List<OnlineContent.ModUpdateInfo> modsWithUpdates)
        {
            DataContext = this;
            modsWithUpdates.ForEach(x =>
            {
                x.ApplyUpdateCommand = new RelayCommand(ApplyUpdateToMod, CanApplyUpdateToMod);
                x.DownloadButtonText = M3L.GetString(M3L.string_downloadUpdate);
            });
            UpdatableMods.AddRange(modsWithUpdates);
            LoadCommands();
            InitializeComponent();
        }

        private bool CanApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModUpdateInfo ui)
            {
                return !ui.UpdateInProgress && ui.CanUpdate && !OperationInProgress;
            }
            return false;
        }

        private void ApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModMakerModUpdateInfo mui)
            {
                UpdateModMakerMod(mui);
            }
            else if (obj is OnlineContent.ModUpdateInfo ui)
            {
                UpdateClassicMod(ui);
            }
        }

        private void UpdateModMakerMod(OnlineContent.ModMakerModUpdateInfo mui)
        {
            //throw new NotImplementedException();
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModmakerModUpdaterThread-" + mui.mod.ModName);
            bw.DoWork += (a, b) =>
            {
                mui.DownloadButtonText = "Compiling";

                OperationInProgress = true;
                mui.UpdateInProgress = true;
                mui.Indeterminate = false;

                mui.UIStatusString = "Downloading delta";
                var normalEndpoint = OnlineContent.ModmakerModsEndpoint + mui.ModMakerId;
                var lzmaEndpoint = normalEndpoint + "&method=lzma";

                string modDelta = null;

                //Try LZMA first
                try
                {
                    var download = OnlineContent.DownloadToMemory(lzmaEndpoint);
                    if (download.errorMessage == null)
                    {
                        mui.UIStatusString = "Decompressing delta";
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
                    var download = OnlineContent.DownloadToMemory(normalEndpoint);
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

                void setOverallMax(int max)
                {
                    mui.OverallProgressMax = max;
                }
                void setOverallValue(int current)
                {
                    mui.OverallProgressValue = current;
                    if (current > mui.OverallProgressMax)
                    {
                        Debugger.Break();
                    }

                }
                void setCurrentTaskString(string str)
                {
                    mui.UIStatusString = str;
                }

                if (modDelta != null)
                {
                    var compiler = new ModMakerCompiler(mui.ModMakerId);
                    //compiler.SetCurrentMaxCallback = SetCurrentMax;
                    //compiler.SetCurrentValueCallback = SetCurrentProgressValue;
                    compiler.SetOverallMaxCallback = setOverallMax;
                    compiler.SetOverallValueCallback = setOverallValue;
                    //compiler.SetCurrentTaskIndeterminateCallback = SetCurrentTaskIndeterminate;
                    compiler.SetCurrentTaskStringCallback = setCurrentTaskString;
                    //compiler.SetModNameCallback = SetModNameOrDownloadText;
                    //compiler.SetCompileStarted = CompilationInProgress;
                    //compiler.SetModNotFoundCallback = ModNotFound;
                    Mod m = compiler.DownloadAndCompileMod(modDelta);
                    File.WriteAllText(System.IO.Path.Combine(Utilities.GetModmakerDefinitionsCache(), mui.ModMakerId + ".xml"), modDelta);
                    mui.DownloadButtonText = "Updated";
                    mui.UIStatusString = $"ModMaker Code {mui.ModMakerId}";
                    mui.UpdateInProgress = false;
                    mui.CanUpdate = false;
                    AnyModUpdated = true;
                    //b.Result = m;
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
                {
                    OperationInProgress = false;
                    CommandManager.InvalidateRequerySuggested();
                };
            bw.RunWorkerAsync();
        }

        private void UpdateClassicMod(OnlineContent.ModUpdateInfo ui)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModUpdaterThread-" + ui.mod.ModName);
            bw.DoWork += (a, b) =>
            {
                OperationInProgress = true;
                ui.UpdateInProgress = true;
                ui.Indeterminate = false;
                ui.DownloadButtonText = M3L.GetString(M3L.string_downloading);
                //void updateProgressCallback(long bytesReceived, long totalBytes)
                //{
                //    ui.By
                //}
                bool errorShown = false;
                void errorCallback(string message)
                {
                    if (!errorShown)
                    {
                        errorShown = true;
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_errorOccuredWhileUpdatingXErrorMessage, ui.mod.ModName, message), M3L.GetString(M3L.string_interp_errorUpdatingX, ui.mod.ModName), MessageBoxButton.OK, MessageBoxImage.Error); }
                        );
                    }
                }
                var stagingDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), Path.GetFileName(ui.mod.ModPath))).FullName;
                var modUpdated = OnlineContent.UpdateMod(ui, stagingDirectory, errorCallback);
                ui.UpdateInProgress = false;
                ui.CanUpdate = !modUpdated;
                AnyModUpdated |= modUpdated;
                ui.DownloadButtonText = ui.CanUpdate ? M3L.GetString(M3L.string_downloadUpdate) : M3L.GetString(M3L.string_updated);
                Utilities.DeleteFilesAndFoldersRecursively(stagingDirectory);
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            };
            bw.RunWorkerAsync();
        }


        public ICommand CloseCommand { get; set; }
        private bool TaskNotRunning() => UpdatableMods.All(x => !x.UpdateInProgress);
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(CloseDialog, TaskNotRunning);
        }

        private void CloseDialog()
        {
            OnClosing(new DataEventArgs(AnyModUpdated));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && TaskNotRunning())
            {
                e.Handled = true;
                CloseDialog();
            }
        }

        public override void OnPanelVisible()
        {

        }
    }
}
