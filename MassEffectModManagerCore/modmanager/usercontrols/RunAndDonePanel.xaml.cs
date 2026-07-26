using System;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    public class RunAndDoneConfig
    {
        public RunAndDoneConfig() { }

        public Action<string> UpdateAction { get; init; }
        public Action<string> UpdateTitle { get; init; }
        public PanelResult Result { get; init; }
    }

    /// <summary>
    /// Quick running task that just needs a spinner, action text and optional title
    /// </summary>
    public partial class RunAndDonePanel : MMBusyPanelBase
    {
        private Func<RunAndDoneConfig, object> runAndDoneDelegate;

        private readonly BackgroundTask BGTask;
        public string ActionText { get; private set; }
        public string TitleText { get; private set; }

        private void UpdateActionText(string message)
        {
            ActionText = message;
        }

        private void UpdateTitleText(string title)
        {
            TitleText = title;
        }

        public RunAndDonePanel(Func<RunAndDoneConfig, object> runAndDoneDelegate, string actionText = null, string endText = null, string titleText = null)
        {
            ActionText = actionText;
            TitleText = titleText;
#if DEBUG
            if (ActionText == null && TitleText == null)
            {
                throw new Exception(@"Action and Title text are null for RunAndDonePanel!");
            }
#endif
            this.runAndDoneDelegate = runAndDoneDelegate;
            BGTask = BackgroundTaskEngine.SubmitBackgroundJob($@"RunAndDone-{actionText}", actionText, endText);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"RunAndDoneThread");
            nbw.DoWork += (a, b) =>
            {
                // Config option as first param so it can be more easily expanded on later.
                var config = new RunAndDoneConfig()
                {
                    Result = Result,
                    UpdateAction = UpdateActionText,
                    UpdateTitle = UpdateTitleText,
                };
                b.Result = runAndDoneDelegate?.Invoke(config);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    // Logging is handled in nbw
                    Result.Error = b.Error;
                    TelemetryInterposer.TrackError(b.Error);
                }
                else if (b.Result is string finalStatus && BGTask != null)
                {
                    // If a run and done panel returns a message we suppress updates until all panels are closed
                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(BGTask, finalStatus);
                    BackgroundTaskEngine.SuppressStatusMessageUpdates(); // 
                }

                if (BGTask != null)
                {
                    BackgroundTaskEngine.SubmitJobCompletion(BGTask);
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
