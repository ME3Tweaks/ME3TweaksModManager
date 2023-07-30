using System.Windows;
using Dark.Net;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for LicenseViewerWindow.xaml
    /// </summary>
    public partial class LicenseViewerWindow : Window
    {
        public LicenseViewerWindow(string licenseText)
        {
            LicenseText = licenseText;
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowStyle();
        }

        public string LicenseText { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => Close());
        }

        public GenericCommand CloseCommand { get; set; }
    }
}
