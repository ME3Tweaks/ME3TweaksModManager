using MassEffectModManagerCore.ui;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for AboutPanel.xaml
    /// </summary>
    public partial class AboutPanel : MMBusyPanelBase
    {
        public bool TelemetryKeyAvailable => APIKeys.HasAppCenterKey;
        public string BuildDate { get; set; }
        public string NetVersion => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        public AboutPanel()
        {
            InitializeComponent();
            BuildDate = App.BuildDate;
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
