using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.usercontrols;
using ME3ExplorerCore.Misc;


namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class DeploymentListDialog : Window, INotifyPropertyChanged
    {
        public ArchiveDeployment.DeploymentChecklistItem DCI { get; }
        public string StatusText { get; set; }
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
            public string Message { get; }

            public DCIMessage(ESeverity severity, string message)
            {
                Severity = severity;
                Message = message;
            }

            public string ToRawString() => $@"{Severity}: {Message}";
        }

        public DeploymentListDialog(ArchiveDeployment.DeploymentChecklistItem dci, Window owner)
        {
            DCI = dci;
            SetupMessages();
            InitializeComponent();
            Owner = owner;
        }

        private void SetupMessages()
        {
            var dict = new Dictionary<IReadOnlyCollection<string>, DCIMessage.ESeverity>
            {
                [DCI.GetBlockingIssues()] = DCIMessage.ESeverity.BLOCKING,
                [DCI.GetSignificantIssues()] = DCIMessage.ESeverity.SIGNIFICANTISSUE,
                [DCI.GetInfoWarningIssues()] = DCIMessage.ESeverity.INFO
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
                StatusText = M3L.GetString(M3L.string_copiedToClipboard);
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogCouldNotSetDataToClipboard) + ex.Message, M3L.GetString(M3L.string_errorCopyingDataToClipboard), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
