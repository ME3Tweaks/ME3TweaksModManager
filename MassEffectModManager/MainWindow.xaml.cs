using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using MassEffectModManager.modmanager;

namespace MassEffectModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string CurrentOperationText { get; set; }
        public string ApplyModButtonText { get; set; } = "Apply Mod";
        public string AddTargetButtonText { get; set; } = "Add Target";
        public string StartGameButtonText { get; set; } = "Start Game";
        private BackgroundTaskEngine backgroundTaskEngine;
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            backgroundTaskEngine = new BackgroundTaskEngine((updateText) => CurrentOperationText = updateText,
                () =>
                {
                    Storyboard sb = this.FindResource("OpenLoadingSpinner") as Storyboard;
                    Storyboard.SetTarget(sb, LoadingSpinner_Image);
                    sb.Begin();
                },
                () =>
                {
                    Storyboard sb = this.FindResource("CloseLoadingSpinner") as Storyboard;
                    Storyboard.SetTarget(sb, LoadingSpinner_Image);
                    sb.Begin();
                }
            );
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            backgroundTaskEngine.SubmitBackgroundJob("ModLoader", "Loading mods");
        }
    }
}
