using ME3TweaksCoreWPF.LogViewer;
using ME3TweaksModManager.extensions;
using System.Windows;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for M3LogViewerWindow.xaml
    /// </summary>
    public partial class M3LogViewerWindow : Window
    {
        public M3LogViewerWindow(string logText)
        {
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();
            Content = new MLogViewerControl(logText);
        }
    }
}
