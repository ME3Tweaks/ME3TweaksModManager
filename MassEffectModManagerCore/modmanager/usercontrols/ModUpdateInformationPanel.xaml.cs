using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.WindowsAPICodePack.Taskbar;
using PropertyChanged;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModUpdateInformation.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ModUpdateInformationPanel : MMBusyPanelBase
    {

        public ObservableCollectionExtended<M3OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollectionExtended<M3OnlineContent.ModUpdateInfo>();

        private List<Mod> updatedMods = new();
        private bool RefreshContentsOnVisible = false;
        public bool OperationInProgress { get; set; }

        public ModUpdateInformationPanel(List<M3OnlineContent.ModUpdateInfo> modsWithUpdates)
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
        }

        private bool CanApplyUpdateToMod(object obj)
        {
            if (obj is M3OnlineContent.ModUpdateInfo ui)
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
            if (obj is M3OnlineContent.ModMakerModUpdateInfo mui)
            {
                UpdateModMakerMod(mui, null);
            }
            else if (obj is M3OnlineContent.ModUpdateInfo ui)
            {
                if (ui.updatecode > 0)
                {
                    UpdateClassicMod(ui, null);
                }
                else if (ui is M3OnlineContent.NexusModUpdateInfo nmui)
                {
                    var domain = @"masseffectlegendaryedition";
                    switch (nmui.GameId)
                    {
                        case 1:
                            domain = @"masseffect";
                            break;
                        case 2:
                            domain = @"masseffect2";
                            break;
                        case 3:
                            domain = @"masseffect3";
                            break;
                    }

                    var url = $@"https://nexusmods.com/{domain}/mods/{nmui.NexusModsId}?tab=files";
                    M3Utilities.OpenWebpage(url);
                }
            }
        }

        private void UpdateModMakerMod(M3OnlineContent.ModMakerModUpdateInfo mui, Action downloadCompleted)
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
                var normalEndpoint = M3OnlineContent.ModmakerModsEndpoint + mui.ModMakerId;
                var lzmaEndpoint = normalEndpoint + @"&method=lzma";

                string modDelta = null;

                //Try LZMA first
                try
                {
                    var download = M3OnlineContent.DownloadToMemory(lzmaEndpoint);
                    if (download.errorMessage == null)
                    {
                        mui.UIStatusString = M3L.GetString(M3L.string_decompressingDelta);
                        // OK
                        var decompressed = StreamingLZMAWrapper.DecompressLZMA(download.result);
                        modDelta = Encoding.UTF8.GetString(decompressed);
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
                    var download = M3OnlineContent.DownloadToMemory(normalEndpoint);
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
                            File.WriteAllText(Path.Combine(M3Filesystem.GetModmakerDefinitionsCache(), mui.ModMakerId + @".xml"), modDelta);
                        }
                        catch (Exception e)
                        {
                            M3Log.Error(@"Couldn't cache modmaker xml file: " + e.Message);
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
                TelemetryInterposer.TrackEvent(@"Updated mod", new Dictionary<string, string>()
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

        private void UpdateClassicMod(M3OnlineContent.ModUpdateInfo ui, Action downloadCompleted)
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
                var stagingDirectory = Directory.CreateDirectory(Path.Combine(M3Filesystem.GetTempPath(), Path.GetFileName(ui.mod.ModPath))).FullName;
                var modUpdated = M3OnlineContent.UpdateMod(ui, stagingDirectory, errorCallback);
                ui.UpdateInProgress = false;
                ui.CanUpdate = !modUpdated;
                updatedMods.Add(ui.mod);
                ui.DownloadButtonText = ui.CanUpdate ? M3L.GetString(M3L.string_downloadUpdate) : M3L.GetString(M3L.string_updated);
                M3Utilities.DeleteFilesAndFoldersRecursively(stagingDirectory);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                TelemetryInterposer.TrackEvent(@"Updated mod", new Dictionary<string, string>()
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

        /// <summary>
        /// If the download all button can be pressed
        /// </summary>
        /// <returns></returns>
        private bool CanDownloadAll() => !OperationInProgress && ShowDownloadAllButton;

        /// <summary>
        /// If the download all button should be shown at all to the user
        /// </summary>
        public bool ShowDownloadAllButton => UpdatableMods.Any(x => x.CanUpdate && (x.mod.ModClassicUpdateCode > 0 || x.mod.ModModMakerID > 0));

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
                    if (update is M3OnlineContent.ModMakerModUpdateInfo mui)
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
            InitializeComponent();
            if (RefreshContentsOnVisible)
            {
                foreach (var v in UpdatableMods.ToList()) // To list
                {
                    if (File.Exists(v.mod.ModDescPath))
                    {
                        var modVer = Mod.GetModVersionFromIni(v.mod.ModDescPath);
                        if (modVer != null && ProperVersion.IsGreaterThan(modVer, v.mod.ParsedModVersion))
                        {
                            UpdatableMods.Remove(v); // Mod was updated.
                        }
                    }
                }
            }

            RefreshContentsOnVisible = false;

            // If a mod was imported via nxm and panel was swapped we should just not open the dialog any further
            if (!UpdatableMods.Any())
            {
                // This is an ugly hack to delay it by a frame
                Task.Run(() =>
                {
                    Thread.Sleep(1);
                    return true;
                }).ContinueWithOnUIThread(x =>
                {
                    CloseDialog();
                });
            }
        }

        /// <summary>
        /// Indicates the panel should have contents updated on display
        /// </summary>
        public void RefreshContentsOnDisplay()
        {
            RefreshContentsOnVisible = true;
        }
    }
}
