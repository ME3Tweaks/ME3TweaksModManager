using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Diagnostics.Support;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save.shared;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.modmanager.windows.input;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for LogUploaderPanel.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class LogUploaderPanel : MMBusyPanelBase
    {
        /// <summary>
        /// If log upload is in progress
        /// </summary>
        public bool UploadingLog { get; private set; }

        /// <summary>
        /// If the log should be shown in the local log viewer instead of uploaded
        /// </summary>
        public bool UseLocalLogViewer { get; set; }

        /// <summary>
        /// The message shown in the UI about what's happening
        /// </summary>
        public string CollectionStatusMessage { get; set; }

        /// <summary>
        /// If advanced diagnostics should be performed
        /// </summary>
        public bool AdvancedDiagnostics { get; set; }

        /// <summary>
        /// List of available application logs
        /// </summary>
        public ObservableCollectionExtended<LogItem> AvailableLogs { get; } = new ObservableCollectionExtended<LogItem>();

        /// <summary>
        /// List of available diagnostic targets
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> DiagnosticTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();

        /// <summary>
        /// The target to auto select when we populate the list
        /// </summary>
        private GameTarget preselectedTarget;

        public LogUploaderPanel(GameTarget preselectedTarget)
        {
            this.preselectedTarget = preselectedTarget;
            LoadCommands();
        }

        private void InitLogUploaderUI()
        {
            AvailableLogs.ClearEx();
            var directory = new DirectoryInfo(MCoreFilesystem.GetLogDir());
            var logfiles = directory.GetFiles(@"modmanagerlog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            AvailableLogs.Add(new LogItem(M3L.GetString(M3L.string_noApplicationLog)) { Selectable = false });
            AvailableLogs.AddRange(logfiles.Select(x => new LogItem(x.FullName) { IsActiveLog = x.FullName.Equals(M3Log.CurrentLogFilePath, StringComparison.InvariantCultureIgnoreCase) }));
            SelectedLog = AvailableLogs.FirstOrDefault(x => x.IsActiveLog);
            var targets = mainwindow.InstallationTargets.Where(x => x.Selectable);
            DiagnosticTargets.Add(new GameTargetWPF(MEGame.Unknown, M3L.GetString(M3L.string_selectAGameTargetToGenerateDiagnosticsFor), false, true));
            DiagnosticTargets.AddRange(targets.Where(x => x.Game != MEGame.LELauncher));

            // Select the preselected target
            if (preselectedTarget != null)
            {
                SelectedDiagnosticTarget = DiagnosticTargets.FirstOrDefault(x => x.TargetPath == preselectedTarget.TargetPath);
                preselectedTarget = null; // Lose reference
            }

            // Select the 'choose a target' if none is set
            if (SelectedDiagnosticTarget == null)
            {
                SelectedDiagnosticTarget = DiagnosticTargets.FirstOrDefault();
            }
        }

        public ICommand UploadLogCommand { get; set; }
        public ICommand CancelUploadCommand { get; set; }
        public ICommand SelectSaveCommand { get; set; }
        public LogItem SelectedLog { get; set; }
        public ISaveFile SelectedSaveFile { get; set; }
        public string SelectedSaveText { get; set; } = M3L.GetString(M3L.string_noSaveSelected);
        public GameTargetWPF SelectedDiagnosticTarget { get; set; }

        private void LoadCommands()
        {
            UploadLogCommand = new GenericCommand(StartLogUploadManual, CanUploadLog);
            CancelUploadCommand = new GenericCommand(CancelUpload, CanCancelUpload);
            SelectSaveCommand = new GenericCommand(SelectSave, CanSelectSave);
        }

        private void SelectSave()
        {
            SaveSelectorUI ssui = new SaveSelectorUI(window, SelectedDiagnosticTarget, M3L.GetString(M3L.string_selectASaveToIncludeWithDiagnostic));
            ssui.Show();
            ssui.Closed += (sender, args) =>
            {
                if (ssui.SaveWasSelected && ssui.SelectedSaveFile != null)
                {
                    SelectedSaveFile = ssui.SelectedSaveFile;
                    SelectedSaveText = Path.GetFileName(SelectedSaveFile.SaveFilePath);
                }
            };
        }

        private bool CanSelectSave()
        {
            if (UploadingLog) return false;
            if (SelectedDiagnosticTarget == null) return false;
            return SelectedDiagnosticTarget.Game.IsLEGame();
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
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"LogUpload");
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);

                }
                else if (b.UserState is MTaskbarState tbps)
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

                void updateTaskbarProgressStateCallback(MTaskbarState state)
                {
                    nbw.ReportProgress(-1, state);
                }

                LogUploadPackage package = new LogUploadPackage()
                {
                    DiagnosticTarget = SelectedDiagnosticTarget,
                    SelectedLog = SelectedLog,
                    Attachments = GetAttachments(),
                    AdvancedDiagnosticsEnabled = AdvancedDiagnostics,
                    UpdateTaskbarProgressStateCallback = updateTaskbarProgressStateCallback,
                    UpdateProgressCallback = updateProgressCallback,
                    SelectedSaveFilePath = SelectedSaveFile?.SaveFilePath,
                    UpdateStatusCallback = updateStatusCallback,
                    UseLocalLogViewer = UseLocalLogViewer
                };

                b.Result = LogCollector.SubmitDiagnosticLogAsync(package).Result;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                if (b.Error == null && b.Result is LogUploadPackage lup)
                {
                    if (lup.Response != null && lup.Response.StartsWith(@"https"))
                    {
                        M3Utilities.OpenWebpage(lup.Response);
                    }
                    else
                    {
                        OnClosing(DataEventArgs.Empty);
                        if (!UseLocalLogViewer)
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this), lup.Response, M3L.GetString(M3L.string_logUploadFailed), MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        // 12/13/2025 - Add local log viewer if ME3Tweaks is down or inaccessible
                        var localLogViewer = new M3LogViewerWindow(lup.FullLogText);
                        localLogViewer.Show();
                        return;
                    }
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        private Dictionary<string, byte[]> GetAttachments()
        {
            var attachments = new Dictionary<string, byte[]>();

            if (SelectedSaveFile != null && File.Exists(SelectedSaveFile.SaveFilePath))
            {
                attachments.Add(Path.GetFileName(SelectedSaveFile.SaveFilePath), File.ReadAllBytes(SelectedSaveFile.SaveFilePath));
            }

            return attachments;
        }

        public bool TextureCheck { get; set; } = true;

        private bool CanUploadLog() => !UploadingLog && ((SelectedDiagnosticTarget != null && SelectedDiagnosticTarget.Game > MEGame.Unknown) || (SelectedLog != null && SelectedLog.Selectable));

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !UploadingLog)
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            InitLogUploaderUI();
        }
    }
}
