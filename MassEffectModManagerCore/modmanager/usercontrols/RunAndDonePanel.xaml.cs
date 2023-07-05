using System;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Quick running task that just needs a spinner
    /// </summary>
    public partial class RunAndDonePanel : MMBusyPanelBase
    {
        private Func<Action<string>, object> runAndDoneDelegate;

        private readonly BackgroundTask BGTask;
        public string ActionText { get; private set; }

        private void UpdateActionText(string message)
        {
            ActionText = message;
        }

        public RunAndDonePanel(Func<Action<string>, object> runAndDoneDelegate, string actionText, string endText)
        {
            ActionText = actionText;
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
                b.Result = runAndDoneDelegate?.Invoke(UpdateActionText);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    // Logging is handled in nbw
                    Result.Error = b.Error;
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
