using System.IO;
using System.Windows;
using System.Windows.Media;
using FontAwesome.WPF;

using MassEffectModManagerCore.modmanager.me3tweaks;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for DynamicHelpItemModalWindow.xaml
    /// </summary>
    public partial class DynamicHelpItemModalWindow : Window
    {
        public string ModalTitle { get; }
        public string ModalText { get; }
        public FontAwesomeIcon ModalIcon { get; }
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
                    ModalIcon = FontAwesomeIcon.ExclamationTriangle;
                    ModalColor = Brushes.DarkOrange;
                    break;
                default:
                case "INFO":
                    ModalIcon = FontAwesomeIcon.InfoCircle;
                    ModalColor = Brushes.RoyalBlue;
                    break;
                case "ERROR":
                    ModalIcon = FontAwesomeIcon.Times;
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
