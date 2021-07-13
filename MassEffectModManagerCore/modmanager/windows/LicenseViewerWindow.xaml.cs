using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.windows
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
        }

        public string LicenseText { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => Close());
        }

        public GenericCommand CloseCommand { get; set; }
    }
}
