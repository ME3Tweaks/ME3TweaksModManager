using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.WindowsAPICodePack.Taskbar;
using Serilog;


namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModUpdateInformation.xaml
    /// </summary>
    public partial class ModUpdateInformation : MMBusyPanelBase
    {

        public ObservableCollectionExtended<OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollectionExtended<OnlineContent.ModUpdateInfo>();

        private List<Mod> updatedMods = new();

        public bool OperationInProgress { get; set; }

        public ModUpdateInformation(List<OnlineContent.ModUpdateInfo> modsWithUpdates)
        {
            modsWithUpdates.ForEach(x =>
            {
                x.ApplyUpdateCommand = new RelayCommand(ApplyUpdateToMod, CanApplyUpdateToMod);
                if (x.mod.ModModMakerID > 0 && BackupService.GetGameBackupPath(x.mod.Game) == null)
                {
                    x.DownloadButtonText = M3L.GetString(M3L.string_requiresBackup);
                }
                else if (x.mod.ModClassicUpdateCode > 0 || x.mod.ModModMakerID > 0)
                {
                    x.DownloadButtonText = M3L.GetString(M3L.string_downloadUpdate);
                }
                else
                {
                    x.DownloadButtonText = M3L.GetString(M3L.string_downloadUpdateFromNexusMods);
                }
            });
            UpdatableMods.ReplaceAll(modsWithUpdates);
            LoadCommands();
            InitializeComponent();
        }

        private bool CanApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModUpdateInfo ui)
            {
                if (ui.mod.ModModMakerID > 0 && BackupService.GetGameBackupPath(ui.mod.Game) == null)
                {
                    return false;
                }
                return !ui.UpdateInProgress && ui.CanUpdate && !OperationInProgress;
            }
            return false;
        }

        private void ApplyUpdateToMod(object obj)
        {
            if (obj is OnlineContent.ModMakerModUpdateInfo mui)
            {
                UpdateModMakerMod(mui, null);
            }
            else if (obj is OnlineContent.ModUpdateInfo ui)
            {
                if (ui.updatecode > 0)
                {
                    UpdateClassicMod(ui, null);
                }
                else
                {
                    Utilities.OpenWebpage(ui.mod.ModWebsite);
                }
            }
        }

        private void UpdateModMakerMod(OnlineContent.ModMakerModUpdateInfo mui, Action downloadCompleted)
        {
            //throw new NotImplementedException();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModmakerModUpdaterThread-" + mui.mod.ModName);
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);
                }
            };
            nbw.DoWork += (a, b) =>
            {
                mui.DownloadButtonText = M3L.GetString(M3L.string_compiling);

                OperationInProgress = true;
                mui.UpdateInProgress = true;
                mui.Indeterminate = false;

                mui.UIStatusString = M3L.GetString(M3L.string_downloadingDelta);
                var normalEndpoint = OnlineContent.ModmakerModsEndpoint + mui.ModMakerId;
                var lzmaEndpoint = normalEndpoint + @"&method=lzma";

                string modDelta = null;

                //Try LZMA first
                try
                {
                    var download = OnlineContent.DownloadToMemory(lzmaEndpoint);
                    if (download.errorMessage == null)
                    {
                        mui.UIStatusString = M3L.GetString(M3L.string_decompressingDelta);
                        // OK
                        var decompressed = StreamingLZMAWrapper.DecompressLZMA(download.result);
                        modDelta = Encoding.UTF8.GetString(decompressed);
                    }
                    else
                    {
                        Log.Error(@"Error downloading lzma mod delta to memory: " + download.errorMessage);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(@"Error downloading LZMA mod delta to memory: " + e.Message);
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
                        Log.Error(@"Error downloading decompressed mod delta to memory: " + download.errorMessage);
                    }
                }

                void setOverallMax(int max)
                {
                    mui.OverallProgressMax = max;
                }
                void setOverallValue(int current)
                {
                    mui.OverallProgressValue = current;
                    nbw.ReportProgress(0, current * 1.0 / mui.OverallProgressMax);
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
                    compiler.SetOverallMaxCallback = setOverallMax;
                    compiler.SetOverallValueCallback = setOverallValue;
                    compiler.SetCurrentTaskStringCallback = setCurrentTaskString;
                    var m = compiler.DownloadAndCompileMod(modDelta, mui.mod.ModPath);
                    if (m != null)
                    {
                        try
                        {
                            File.WriteAllText(System.IO.Path.Combine(Utilities.GetModmakerDefinitionsCache(), mui.ModMakerId + @".xml"), modDelta);
                        }
                        catch (Exception e)
                        {
                            Log.Error(@"Couldn't cache modmaker xml file: " + e.Message);
                        }

                        mui.DownloadButtonText = M3L.GetString(M3L.string_updated);
                        mui.UIStatusString = M3L.GetString(M3L.string_interp_modMakerCodeX, mui.ModMakerId);
                        mui.UpdateInProgress = false;
                        mui.CanUpdate = false;
                        updatedMods.Add(m);
                    }
                    else
                    {
                        mui.UpdateInProgress = false;
                        mui.DownloadButtonText = M3L.GetString(M3L.string_compilingFailed);
                        mui.UpdateInProgress = false;
                    }
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                Analytics.TrackEvent(@"Updated mod", new Dictionary<string, string>()
                {
                    {@"Type", @"ModMaker"},
                    {@"ModName", mui.mod.ModName},
                    {@"Result", mui.CanUpdate ? @"Success" : @"Failed"}
                });

                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
                downloadCompleted?.Invoke();
            };
            TaskbarHelper.SetProgress(0);
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
            nbw.RunWorkerAsync();
        }

        private void UpdateClassicMod(OnlineContent.ModUpdateInfo ui, Action downloadCompleted)
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModUpdaterThread-" + ui.mod.ModName);
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);
                }
            };
            nbw.DoWork += (a, b) =>
            {
                OperationInProgress = true;
                ui.UpdateInProgress = true;
                ui.Indeterminate = false;
                ui.DownloadButtonText = M3L.GetString(M3L.string_downloading);
                ui.ProgressChanged += (a, b) =>
                {
                    if (b.totalToDl != 0 && nbw.IsBusy) //? IsBusy needs to be here for some reason or it crashes, like progress comes in late or something.
                    {
                        nbw.ReportProgress(0, b.currentDl * 1.0 / b.totalToDl);
                    }
                };
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
                updatedMods.Add(ui.mod);
                ui.DownloadButtonText = ui.CanUpdate ? M3L.GetString(M3L.string_downloadUpdate) : M3L.GetString(M3L.string_updated);
                Utilities.DeleteFilesAndFoldersRecursively(stagingDirectory);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                Analytics.TrackEvent(@"Updated mod", new Dictionary<string, string>()
                {
                    {@"Type", @"Classic"},
                    {@"ModName", ui.mod.ModName},
                    {@"Result", ui.CanUpdate ? @"Success" : @"Failed"}
                });
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
                downloadCompleted?.Invoke();
            };
            TaskbarHelper.SetProgress(0);
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
            nbw.RunWorkerAsync();
        }


        public ICommand CloseCommand { get; set; }
        private bool TaskNotRunning() => UpdatableMods.All(x => !x.UpdateInProgress);
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(CloseDialog, TaskNotRunning);
            DownloadAllCommand = new GenericCommand(DownloadAll, CanDownloadAll);
        }

        private bool CanDownloadAll() => !OperationInProgress && UpdatableMods.Any(x => x.CanUpdate && (x.mod.ModClassicUpdateCode > 0 || x.mod.ModModMakerID > 0));

        private void DownloadAll()
        {
            var updates = UpdatableMods.Where(x => x.CanUpdate && (x.mod.ModClassicUpdateCode > 0 || x.mod.ModModMakerID > 0)).ToList();
            OperationInProgress = true;
            CommandManager.InvalidateRequerySuggested();

            Task.Run(() =>
            {
                object syncObj = new object();

                void updateDone()
                {
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }

                foreach (var update in updates)
                {
                    if (update is OnlineContent.ModMakerModUpdateInfo mui)
                    {
                        UpdateModMakerMod(mui, updateDone);
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                    }
                    else if (update.mod.ModClassicUpdateCode > 0)
                    {
                        UpdateClassicMod(update, updateDone);
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                    }
                }
            });
        }

        public GenericCommand DownloadAllCommand { get; set; }

        private void CloseDialog()
        {
            Result.ReloadMods = updatedMods.Any();
            Result.ModToHighlightOnReload = updatedMods.FirstOrDefault();
            OnClosing(DataEventArgs.Empty);
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
