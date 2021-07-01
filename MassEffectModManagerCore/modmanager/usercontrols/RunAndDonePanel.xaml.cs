using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.squadmates;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;

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
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"RunAndDoneThread");
            nbw.DoWork += (a, b) =>
            {
                runAndDoneDelegate?.Invoke();
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                    Result.Error = b.Error;
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
