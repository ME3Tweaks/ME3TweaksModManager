using System;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.diagnostics;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Quick running task that just needs a spinner
    /// </summary>
    public partial class RunAndDonePanel : MMBusyPanelBase
    {
        private Action runAndDoneDelegate;
        public string ActionText { get; }
        public RunAndDonePanel(Action runAndDoneDelegate, string actionText)
        {
            ActionText = actionText;
            this.runAndDoneDelegate = runAndDoneDelegate;
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
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
