using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.deployment.checks;
using ME3TweaksModManager.modmanager.usercontrols;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class DeploymentListDialog : Window
    {
        public DeploymentChecklistItem DCI { get; }
        public string StatusTextPrefix { get; set; }
        public int PercentDone { get; set; }

        private bool LEXLaunchInProgress;
        public ObservableCollectionExtended<DCIMessage> Messages { get; } = new ObservableCollectionExtended<DCIMessage>();

        public class DCIMessage
        {
            public enum ESeverity
            {
                INFO,
                SIGNIFICANTISSUE,
                BLOCKING
            }

            public ESeverity Severity { get; }
            public EntryStringPair Message { get; }

            public DCIMessage(ESeverity severity, EntryStringPair message)
            {
                Severity = severity;
                if (message.Entry != null)
                {
                    message.Openable = new LEXOpenable(message.Entry);
                    message.Entry = null; // Drop the reference
                }
                Message = message;
            }

            public string ToRawString() => $@"{Severity}: {Message}";
        }

        public DeploymentListDialog(DeploymentChecklistItem dci, Window owner)
        {
            DCI = dci;
            SetupMessages();
            InitializeComponent();
            Owner = owner;
        }

        private void SetupMessages()
        {
            var dict = new Dictionary<IReadOnlyCollection<EntryStringPair>, DCIMessage.ESeverity>
            {
                [DCI.GetBlockingErrors()] = DCIMessage.ESeverity.BLOCKING,
                [DCI.GetSignificantIssues()] = DCIMessage.ESeverity.SIGNIFICANTISSUE,
                [DCI.GetInfoWarnings()] = DCIMessage.ESeverity.INFO
            };

            foreach (var severitylist in dict)
            {
                Messages.AddRange(severitylist.Key.Select(x => new DCIMessage(severitylist.Value, x)));
            }
        }

        private void CopyItemsToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            string toClipboard = string.Join(Environment.NewLine, Messages.Select(x => x.ToRawString()));
            try
            {
                Clipboard.SetText(toClipboard);
                StatusTextPrefix = M3L.GetString(M3L.string_copiedToClipboard);
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogCouldNotSetDataToClipboard) + ex.Message, M3L.GetString(M3L.string_errorCopyingDataToClipboard), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string StatusText
        {
            get
            {
                string str = StatusTextPrefix;
                if (PercentDone > 0)
                    str += $@" {PercentDone}%";
                return str;
            }
        }

        private void OpenInLEX_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!LEXLaunchInProgress)
            {
                LEXLaunchInProgress = true;
                if (sender is FrameworkElement ex && ex.DataContext is DCIMessage m)
                {
                    void currentTaskCallback(string s) => StatusTextPrefix = s;
                    void setPercentDoneCallback(int percent) => PercentDone = percent;

                    LEXLauncher.LaunchLEX(Application.Current.MainWindow, m.Message.Openable.FilePath, m.Message.Openable.EntryUIndex, currentTaskCallback, setPercentDoneCallback, () => LEXLaunchInProgress = false);
                }
            }
        }
    }
}
