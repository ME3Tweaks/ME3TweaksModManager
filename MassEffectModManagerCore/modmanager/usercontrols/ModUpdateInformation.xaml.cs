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
using System.Windows.Shell;
using MassEffectModManagerCore;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Compression;
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
        private bool AnyModUpdated;

        public ObservableCollectionExtended<OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollectionExtended<OnlineContent.ModUpdateInfo>();

        public bool OperationInProgress { get; set; }

        public ModUpdateInformation(List<OnlineContent.ModUpdateInfo> modsWithUpdates)
        {
            DataContext = this;
            modsWithUpdates.ForEach(x =>
            {
                x.ApplyUpdateCommand = new RelayCommand(ApplyUpdateToMod, CanApplyUpdateToMod);
                if (x.mod.ModModMakerID > 0 && BackupService.GetGameBackupPath(x.mod.Game) == null)
                {
                    x.DownloadButtonText = M3L.GetString(M3L.string_requiresBackup);
                }
                else
                {
                    x.DownloadButtonText = M3L.GetString(M3L.string_downloadUpdate);
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
                UpdateModMakerMod(mui);

            }
            else if (obj is OnlineContent.ModUpdateInfo ui)
            {
                if (ui.updatecode > 0)
                {
                    UpdateClassicMod(ui);
                }
                else
                {
                    Utilities.OpenWebpage(ui.mod.ModWebsite);
                }
            }
        }

        private void UpdateModMakerMod(OnlineContent.ModMakerModUpdateInfo mui)
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
                        var decompressed = LZMA.DecompressLZMAFile(download.result.ToArray());
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
                    //compiler.SetCurrentMaxCallback = SetCurrentMax;
                    //compiler.SetCurrentValueCallback = SetCurrentProgressValue;
                    compiler.SetOverallMaxCallback = setOverallMax;
                    compiler.SetOverallValueCallback = setOverallValue;
                    //compiler.SetCurrentTaskIndeterminateCallback = SetCurrentTaskIndeterminate;
                    compiler.SetCurrentTaskStringCallback = setCurrentTaskString;
                    //compiler.SetModNameCallback = SetModNameOrDownloadText;
                    //compiler.SetCompileStarted = CompilationInProgress;
                    //compiler.SetModNotFoundCallback = ModNotFound;
                    objects.mod.Mod m = compiler.DownloadAndCompileMod(modDelta);
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
                        AnyModUpdated = true;
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
            };
            TaskbarHelper.SetProgress(0);
            TaskbarHelper.SetProgressState(TaskbarProgressBarState.Normal);
            nbw.RunWorkerAsync();
        }

        private void UpdateClassicMod(OnlineContent.ModUpdateInfo ui)
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
                AnyModUpdated |= modUpdated;
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
