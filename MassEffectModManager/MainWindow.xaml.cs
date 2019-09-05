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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MassEffectModManager.modmanager;
using MassEffectModManager.ui;

namespace MassEffectModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string CurrentOperationText { get; set; }

        public string CurrentDescriptionText { get; set; } = DefaultDescriptionText;
        private static readonly string DefaultDescriptionText = "Select a mod on the left to get started";
        public string ApplyModButtonText { get; set; } = "Apply Mod";
        public string AddTargetButtonText { get; set; } = "Add Target";
        public string StartGameButtonText { get; set; } = "Start Game";
        public Mod SelectedMod { get; set; }
        public ObservableCollectionExtended<Mod> LoadedMods { get; } = new ObservableCollectionExtended<Mod>();
        private BackgroundTaskEngine backgroundTaskEngine;
        //private ModLoader modLoader;
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            backgroundTaskEngine = new BackgroundTaskEngine((updateText) => CurrentOperationText = updateText,
                () =>
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        Storyboard sb = this.FindResource("OpenLoadingSpinner") as Storyboard;
                        Storyboard.SetTarget(sb, LoadingSpinner_Image);
                        sb.Begin();
                    });
                },
                () =>
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        Storyboard sb = this.FindResource("CloseLoadingSpinner") as Storyboard;
                        Storyboard.SetTarget(sb, LoadingSpinner_Image);
                        sb.Begin();
                    });
                }
            );
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            //modLoader.LoadAllMods();
            LoadME3Mods();
            //LoadME2Mods();
            //LoadME1Mods();
        }

        private void LoadME3Mods()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork += (a, args) =>
            {
                var uiTask = backgroundTaskEngine.SubmitBackgroundJob("ModLoader", "Loading mods");

                var modDescsToLoad = Directory.GetDirectories(Utilities.GetME3ModsDirectory()).Select(x => Path.Combine(x, "moddesc.ini")).Where(File.Exists);
                foreach (var moddesc in modDescsToLoad)
                {
                    var mod = new Mod(moddesc);
                    if (mod.ValidMod)
                    {
                        Application.Current.Dispatcher.Invoke(delegate { LoadedMods.Add(mod); });
                    }
                }
                backgroundTaskEngine.SubmitJobCompletion(uiTask);
            };
            bw.RunWorkerAsync();
        }

        private void ModsList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedMod = (Mod)e.AddedItems[0];
                SetWebsitePanelVisibility(SelectedMod.ModWebsite != Mod.DefaultWebsite);

                //CurrentDescriptionText = newSelectedMod.DisplayedModDescription;
            }
            else
            {
                SelectedMod = null;
                SetWebsitePanelVisibility(false);
                CurrentDescriptionText = DefaultDescriptionText;
            }
        }

        private void SetWebsitePanelVisibility(bool open)
        {
            Storyboard sb = this.FindResource(open ? "OpenWebsitePanel" : "CloseWebsitePanel") as Storyboard;
            Storyboard.SetTarget(sb, VisitWebsitePanel);
            sb.Begin();
        }

        private void RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }
    }
}
