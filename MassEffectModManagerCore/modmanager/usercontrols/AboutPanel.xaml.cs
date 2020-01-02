using AuthenticodeExaminer;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for AboutPanel.xaml
    /// </summary>
    public partial class AboutPanel : MMBusyPanelBase
    {
        public bool TelemetryKeyAvailable => APIKeys.HasAppCenterKey;
        public string BuildDate { get; set; }
        public AboutPanel()
        {
            DataContext = this;
            Debug.WriteLine(TelemetryKeyAvailable);
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("AboutAuthenticode");
            nbw.DoWork += (a, b) =>
            {
                var info = new FileInspector(App.ExecutableLocation);
                var signTime = info.GetSignatures().FirstOrDefault()?.TimestampSignatures.FirstOrDefault()?.TimestampDateTime?.UtcDateTime;

                if (signTime != null)
                {
                    BuildDate = signTime.Value.ToString(@"MMMM dd, yyyy");
                }
                else
                {
                    BuildDate = "WARNING: This build is not signed by ME3Tweaks";
                }
            };
            nbw.RunWorkerAsync();
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            Utilities.OpenWebpage(@"https://me3tweaks.com");
        }

        private void About_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void OnPanelVisible()
        {
        }

        private void Navigate_Click(object sender, RequestNavigateEventArgs e)
        {
            Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }

        private void ClosePanel(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
