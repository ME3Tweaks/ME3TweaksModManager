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
        private Action runAndDoneDelegate;

        private readonly BackgroundTask BGTask;
        public string ActionText { get; }
        public RunAndDonePanel(Action runAndDoneDelegate, string actionText, string endText)
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
                runAndDoneDelegate?.Invoke();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                    Result.Error = b.Error;
                }
                BackgroundTaskEngine.SubmitJobCompletion(BGTask);
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
