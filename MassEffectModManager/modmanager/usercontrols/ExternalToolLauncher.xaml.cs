using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ExternalToolLauncher : UserControl, INotifyPropertyChanged
    {
        public const string ME3Explorer = "ME3Explorer";
        public const string ALOTInstaller = "ALOT Installer";
        public const string MEIM = "Mass Effect INI Modder";
        public const string MEM = "Mass Effect Modder";
        public const string MER = "Mass Effect Randomizer";
        public const string f = "ff";
        public Visibility PercentVisibility { get; set; }= Visibility.Collapsed;
        private string localFolderName;

        public string Action { get; set; }
        public int PercentDownloaded { get; set; }
        public ExternalToolLauncher(string tool)
        {
            DataContext = this;
            var toolName = tool.Replace(" ", "");
            var localToolFolderName = Path.Combine(Utilities.GetDataDirectory(), "ExternalTools", toolName);
            InitializeComponent();
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) =>
            {
                Action = "Checking for updates";
            };

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
