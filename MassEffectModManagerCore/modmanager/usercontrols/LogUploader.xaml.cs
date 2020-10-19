using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using ByteSizeLib;
using Flurl.Http;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.WindowsAPICodePack.Taskbar;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for LogUploader.xaml
    /// </summary>
    public partial class LogUploader : MMBusyPanelBase
    {
        public bool UploadingLog { get; private set; }
        public string CollectionStatusMessage { get; set; }
        //public string TopText { get; private set; } = M3L.GetString(M3L.string_selectALogToView);
        public ObservableCollectionExtended<LogItem> AvailableLogs { get; } = new ObservableCollectionExtended<LogItem>();
        public ObservableCollectionExtended<GameTarget> DiagnosticTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public LogUploader()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public bool ForceTrue
        {
            get => true;
            set
            {

            }
        }


        private void InitLogUploaderUI()
        {
            AvailableLogs.ClearEx();
            var directory = new DirectoryInfo(App.LogDir);
            var logfiles = directory.GetFiles(@"modmanagerlog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            AvailableLogs.Add(new LogItem(M3L.GetString(M3L.string_selectAnApplicationLog)) { Selectable = false });
            AvailableLogs.AddRange(logfiles.Select(x => new LogItem(x.FullName)));
            SelectedLog = AvailableLogs.FirstOrDefault();
            var targets = mainwindow.InstallationTargets.Where(x => x.Selectable);
            DiagnosticTargets.Add(new GameTarget(Mod.MEGame.Unknown, M3L.GetString(M3L.string_selectAGameTargetToGenerateDiagnosticsFor), false));
            DiagnosticTargets.AddRange(targets);
            SelectedDiagnosticTarget = DiagnosticTargets.FirstOrDefault();
            //if (LogSelector_ComboBox.Items.Count > 0)
            //{
            //    LogSelector_ComboBox.SelectedIndex = 0;
            //}
        }

        public ICommand UploadLogCommand { get; set; }
        public ICommand CancelUploadCommand { get; set; }
        public LogItem SelectedLog { get; set; }
        public GameTarget SelectedDiagnosticTarget { get; set; }

        private void LoadCommands()
        {
            UploadLogCommand = new GenericCommand(StartLogUploadManual, CanUploadLog);
            CancelUploadCommand = new GenericCommand(CancelUpload, CanCancelUpload);
        }

        private void StartLogUploadManual()
        {
            StartLogUpload();
        }

        private void CancelUpload()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanCancelUpload()
        {
            return !UploadingLog;
        }

        private void StartLogUpload(bool isPreviousCrashLog = false)
        {
            UploadingLog = true;
            //TopText = M3L.GetString(M3L.string_collectingLogInformation);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"LogUpload");
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);

                }
                else if (b.UserState is TaskbarProgressBarState tbps)
                {
                    TaskbarHelper.SetProgressState(tbps);
                }
            };
            nbw.DoWork += (a, b) =>
            {
                void updateStatusCallback(string status)
                {
                    CollectionStatusMessage = status;
                }

                void updateProgressCallback(int progress)
                {
                    nbw.ReportProgress(0, progress / 100.0);
                }

                void updateTaskbarProgressStateCallback(TaskbarProgressBarState state)
                {
                    nbw.ReportProgress(-1, state);
                }
                StringBuilder logUploadText = new StringBuilder();
                if (SelectedDiagnosticTarget != null && SelectedDiagnosticTarget.Game > Mod.MEGame.Unknown)
                {
                    Debug.WriteLine(@"Selected game target: " + SelectedDiagnosticTarget.TargetPath);
                    logUploadText.Append("[MODE]diagnostics\n"); //do not localize
                    logUploadText.Append(LogCollector.PerformDiagnostic(SelectedDiagnosticTarget, TextureCheck, updateStatusCallback, updateProgressCallback, updateTaskbarProgressStateCallback));
                    logUploadText.Append("\n"); //do not localize
                }

                if (SelectedLog != null && SelectedLog.Selectable)
                {
                    Debug.WriteLine(@"Selected log: " + SelectedLog.filepath);
                    logUploadText.Append("[MODE]logs\n"); //do not localize
                    logUploadText.AppendLine(LogCollector.CollectLogs(SelectedLog.filepath));
                    logUploadText.Append("\n"); //do not localize
                }

                var logtext = logUploadText.ToString();
                if (logtext != null)
                {
                    CollectionStatusMessage = M3L.GetString(M3L.string_compressingForUpload);
                    var lzmalog = SevenZipHelper.LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(logtext));
                    try
                    {
                        //this doesn't need to technically be async, but library doesn't have non-async method.
                        //DEBUG ONLY!!!
                        CollectionStatusMessage = M3L.GetString(M3L.string_uploadingToME3Tweaks);

#if DEBUG
                        string responseString = @"https://me3tweaks.com/modmanager/logservice/logupload2.php".PostUrlEncodedAsync(new { LogData = Convert.ToBase64String(lzmalog), ModManagerVersion = App.BuildNumber, CrashLog = isPreviousCrashLog }).ReceiveString().Result;
#else
                        string responseString = @"https://me3tweaks.com/modmanager/logservice/logupload.php".PostUrlEncodedAsync(new { LogData = Convert.ToBase64String(lzmalog), ModManagerVersion = App.BuildNumber, CrashLog = isPreviousCrashLog }).ReceiveString().Result;
#endif
                        Uri uriResult;
                        bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                                      && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                        if (result)
                        {
                            //should be valid URL.
                            //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                            //e.Result = responseString;
                            Log.Information(@"Result from server for log upload: " + responseString);
                            b.Result = responseString;
                            return;
                        }
                        else
                        {
                            Log.Error(@"Error uploading log. The server responded with: " + responseString);
                            b.Result = M3L.GetString(M3L.string_interp_serverRejectedTheUpload, responseString);
                        }
                    }
                    catch (AggregateException e)
                    {
                        Exception ex = e.InnerException;
                        string exmessage = ex.Message;
                        b.Result = M3L.GetString(M3L.string_interp_logWasUnableToUpload, exmessage);
                    }
                    catch (FlurlHttpTimeoutException)
                    {
                        // FlurlHttpTimeoutException derives from FlurlHttpException; catch here only
                        // if you want to handle timeouts as a special case
                        Log.Error(@"Request timed out while uploading log.");
                        b.Result = M3L.GetString(M3L.string_interp_requestTimedOutUploading);

                    }
                    catch (Exception ex)
                    {
                        // ex.Message contains rich details, inclulding the URL, verb, response status,
                        // and request and response bodies (if available)
                        Log.Error(@"Handled error uploading log: " + App.FlattenException(ex));
                        string exmessage = ex.Message;
                        var index = exmessage.IndexOf(@"Request body:");
                        if (index > 0)
                        {
                            exmessage = exmessage.Substring(0, index);
                        }

                        b.Result = M3L.GetString(M3L.string_interp_logWasUnableToUpload, exmessage);
                    }
                }
                else
                {
                    //Log pull failed
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }

                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                if (b.Result is string response)
                {
                    if (response.StartsWith(@"http"))
                    {
                        Utilities.OpenWebpage(response);
                    }
                    else
                    {
                        OnClosing(DataEventArgs.Empty);
                        var res = M3L.ShowDialog(Window.GetWindow(this), response, M3L.GetString(M3L.string_logUploadFailed), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        public bool TextureCheck { get; set; } = true;

        private bool CanUploadLog() => !UploadingLog && ((SelectedDiagnosticTarget != null && SelectedDiagnosticTarget.Game > Mod.MEGame.Unknown) || (SelectedLog != null && SelectedLog.Selectable));

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !UploadingLog)
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }

            if (e.Key == Key.Space)
            {
                Debugger.Break();
            }
        }

        public override void OnPanelVisible()
        {
            InitLogUploaderUI();
        }

        public class LogItem
        {
            public bool Selectable { get; set; } = true;
            public string filepath;
            public LogItem(string filepath)
            {
                this.filepath = filepath;
            }

            public override string ToString()
            {
                if (!Selectable) return filepath;
                return Path.GetFileName(filepath) + @" - " + ByteSize.FromBytes(new FileInfo(filepath).Length);
            }
        }
    }
}
