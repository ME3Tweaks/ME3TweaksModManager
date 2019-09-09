using System;
using System.Collections.Generic;
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
using FontAwesome5;
using FontAwesome5.WPF;
using MassEffectModManager.modmanager.me3tweaks;

namespace MassEffectModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for DynamicHelpItemModalWindow.xaml
    /// </summary>
    public partial class DynamicHelpItemModalWindow : Window
    {
        public string ModalTitle { get; }
        public string ModalText { get; }
        public EFontAwesomeIcon ModalIcon { get; }
        public Brush ModalColor { get; }
        public string ResourceImage { get; }

        public DynamicHelpItemModalWindow(SortableHelpElement shi)
        {
            DataContext = this;
            ModalTitle = shi.ModalTitle;
            ModalText = shi.ModalText;
            switch (shi.ModalIcon)
            {
                case "WARNING":
                    ModalIcon = EFontAwesomeIcon.Solid_ExclamationTriangle;
                    ModalColor = Brushes.DarkOrange;
                    break;
                default:
                case "INFO":
                    ModalIcon = EFontAwesomeIcon.Solid_InfoCircle;
                    ModalColor = Brushes.RoyalBlue;
                    break;
                case "ERROR":
                    ModalIcon = EFontAwesomeIcon.Solid_Times;
                    ModalColor = Brushes.Red;
                    break;
            }

            if (shi.ResourceName != null)
            {
                string resPath = Path.Combine(Utilities.GetLocalHelpResourcesDirectory(), shi.ResourceName);
                if (File.Exists(resPath))
                {
                    ResourceImage = resPath;
                }
            }
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
