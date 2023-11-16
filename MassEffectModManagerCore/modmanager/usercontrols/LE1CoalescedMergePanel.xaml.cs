using System.Windows;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge.LE1CfgMerge;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for LE1 Coalesced Merge.
    /// </summary>
    public partial class LE1CoalescedMergePanel : MMBusyPanelBase
    {
        private GameTarget CoalescedMergeTarget;

        public LE1CoalescedMergePanel(GameTarget target)
        {
            if (target?.Game != MEGame.LE1)
            {
                throw new Exception(@"Cannot run coalesced merge panel on game that is not LE1");
            }
            this.CoalescedMergeTarget = target;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            if (Settings.OneTimeMessage_LE1CoalescedOverwriteWarning)
            {
                M3Log.Information(@"Showing first LE1 coalesced merge dialog");
                var result = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_firstCoalescedMerge), M3L.GetString(M3L.string_information), MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    M3Log.Information(@"User accepted LE1 coalesced merge feature");
                    Settings.OneTimeMessage_LE1CoalescedOverwriteWarning = false;
                }
                else
                {
                    M3Log.Warning(@"User declined first LE1 coalesced merge");
                    OnClosing(DataEventArgs.Empty);
                    return;
                }
            }

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"CoalescedMerge");
            nbw.DoWork += (a, b) =>
            {
                var consoleKey = new ConsoleKeybinding() { IsSetByUser = Settings.IsLE1ConsoleKeySet, AssignedKey = Settings.IsLE1ConsoleKeySet ? Settings.LE1ConsoleKey : null };
                var miniConsoleKey = new ConsoleKeybinding() { IsSetByUser = Settings.IsLE1MiniConsoleKeySet, AssignedKey = Settings.IsLE1MiniConsoleKeySet ? Settings.LE1MiniConsoleKey : null };
                LE1ConfigMerge.RunCoalescedMerge(CoalescedMergeTarget, consoleKey, miniConsoleKey);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
