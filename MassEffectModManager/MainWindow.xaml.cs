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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MassEffectModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string CurrentOperationText { get; set; } = "Herpaderp";
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            bool open = false;
            dispatcherTimer.Tick += (a, b) =>
            {
                Storyboard sb = this.FindResource(open ? "OpenLoadingSpinner" : "CloseLoadingSpinner") as Storyboard;
                //Storyboard.SetTarget(sb, LoadingSpinnerContainer);
                //sb.Begin();
                //sb = this.FindResource(open ? "OpenLoadingSpinner" : "CloseLoadingSpinner") as Storyboard;
                Storyboard.SetTarget(sb, LoadingSpinner_Image);
                sb.Begin();
                open = !open;
            };
            dispatcherTimer.Interval = new TimeSpan(0, 0, 2);
            dispatcherTimer.Start();
        }
    }
}
