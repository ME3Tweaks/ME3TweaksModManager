using System.IO;
using System.Windows;
using System.Windows.Media;
using Dark.Net;
using FontAwesome5;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.services;

namespace ME3TweaksModManager.modmanager.windows
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
                case @"WARNING":
                    ModalIcon = EFontAwesomeIcon.Solid_ExclamationTriangle;
                    ModalColor = Brushes.DarkOrange;
                    break;
                default:
                case @"INFO":
                    ModalIcon = EFontAwesomeIcon.Solid_InfoCircle;
                    ModalColor = Brushes.RoyalBlue;
                    break;
                case @"ERROR":
                    ModalIcon = EFontAwesomeIcon.Solid_Times;
                    ModalColor = Brushes.Red;
                    break;
            }

            if (shi.ResourceName != null)
            {
                string resPath = Path.Combine(M3Filesystem.GetLocalHelpResourcesDirectory(), shi.ResourceName);
                if (File.Exists(resPath))
                {
                    ResourceImage = resPath;
                }
            }
            InitializeComponent();
            this.ApplyDefaultTheming();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
