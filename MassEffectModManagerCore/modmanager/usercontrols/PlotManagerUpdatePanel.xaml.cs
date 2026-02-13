using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge.PlotManager;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for Plot Manager Update.
    /// </summary>
    public partial class PlotManagerUpdatePanel : MMBusyPanelBase
    {
        private GameTarget PlotManagerUpdateTarget;

        public PlotManagerUpdatePanel(GameTarget target)
        {
            this.PlotManagerUpdateTarget = target ?? throw new Exception(@"Null target specified for PlotManagerUpdatePanel");
        }

        public static bool RunPlotManagerUpdate(GameTarget target)
        {
            PlotManagerMerge.RunPlotManagerMerge(target, Settings.LogModInstallation);
            return true;
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"PlotManagerUpdate");
            nbw.DoWork += (a, b) =>
            {
                RunPlotManagerUpdate(PlotManagerUpdateTarget);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Exception(b.Error, @"Error running plot sync:");
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_errorMergingPlotManagerFiles, b.Error.Message), M3L.GetString(M3L.string_errorSyncingPlotManager), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
