using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using MassEffectModManager.GameDirectories;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.me3tweaks;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.modmanager.windows;
using MassEffectModManager.ui;
using Serilog;
using static MassEffectModManager.modmanager.Mod;

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
        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();

        private BackgroundTaskEngine backgroundTaskEngine;
        //private ModLoader modLoader;
        public MainWindow()
        {
            DataContext = this;
            LoadCommands();
            PopulateTargets();
            InitializeComponent();
            //Must be done after UI has initialized
            if (InstallationTargets.Count > 0)
            {
                InstallationTargets_ComboBox.SelectedItem = InstallationTargets[0];
            }
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

        public ICommand ReloadModsCommand { get; set; }
        public ICommand ApplyModCommand { get; set; }
        private void LoadCommands()
        {
            ReloadModsCommand = new GenericCommand(ReloadMods, CanReloadMods);
            ApplyModCommand = new GenericCommand(ApplyMod, CanApplyMod);
        }

        public bool IsLoadingMods { get; set; }
        private bool CanReloadMods()
        {
            return !IsLoadingMods;
        }

        private bool CanApplyMod()
        {
            return false; //todo: Add checks for this.
        }

        private void ApplyMod()
        {
            throw new NotImplementedException();
        }

        private void ReloadMods()
        {
            LoadMods();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            LoadMods();
            PerformStartupNetworkFetches();
        }

        public void LoadMods(Mod modToHighlight = null)
        {
            IsLoadingMods = true;
            LoadedMods.ClearEx();
            FailedMods.ClearEx();
            {
                Storyboard sb = this.FindResource("CloseWebsitePanel") as Storyboard;
                Storyboard.SetTarget(sb, FailedModsPanel);
                sb.Begin();
            }
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModLoaderThread");
            bw.WorkerReportsProgress = true;
            bw.DoWork += (a, args) =>
            {
                var uiTask = backgroundTaskEngine.SubmitBackgroundJob("ModLoader", "Loading mods", "Loaded mods");
                CLog.Information("Loading mods from mod library: " + Utilities.GetModsDirectory(), Properties.Settings.Default.LogModStartup);
                var me3modDescsToLoad = Directory.GetDirectories(Utilities.GetME3ModsDirectory()).Select(x => (game: MEGame.ME3, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
                var me2modDescsToLoad = Directory.GetDirectories(Utilities.GetME2ModsDirectory()).Select(x => (game: MEGame.ME2, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
                var me1modDescsToLoad = Directory.GetDirectories(Utilities.GetME1ModsDirectory()).Select(x => (game: MEGame.ME1, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
                var modDescsToLoad = me3modDescsToLoad.Concat(me2modDescsToLoad).Concat(me1modDescsToLoad);
                foreach (var moddesc in modDescsToLoad)
                {
                    var mod = new Mod(moddesc.path, moddesc.game);
                    if (mod.ValidMod)
                    {
                        Application.Current.Dispatcher.Invoke(delegate {
                            LoadedMods.Add(mod);
                            
                            LoadedMods.Sort(x => x.ModName); });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            FailedMods.Add(mod);
                            Storyboard sb = this.FindResource("OpenWebsitePanel") as Storyboard;
                            Storyboard.SetTarget(sb, FailedModsPanel);
                            sb.Begin();
                        });
                    }
                }
                if (modToHighlight != null)
                {
                    args.Result = LoadedMods.FirstOrDefault(x => x.ModPath == modToHighlight.ModPath);
                }
                backgroundTaskEngine.SubmitJobCompletion(uiTask);
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                IsLoadingMods = false;
                if (b.Result is Mod m)
                {
                    ModsList_ListBox.SelectedItem = m;
                }
            };
            bw.RunWorkerAsync();
        }

        private void PopulateTargets()
        {
            if (ME3Directory.gamePath != null)
            {
                InstallationTargets.Add(new GameTarget(Mod.MEGame.ME3, ME3Directory.gamePath, true));
            }

            if (ME2Directory.gamePath != null)
            {
                InstallationTargets.Add(new GameTarget(Mod.MEGame.ME2, ME2Directory.gamePath, true));
            }
            if (ME1Directory.gamePath != null)
            {
                InstallationTargets.Add(new GameTarget(Mod.MEGame.ME1, ME1Directory.gamePath, true));
            }


            // TODO: Read cached settings.
            // TODO: Read and import java version configuration
        }

        private void ModsList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedMod = (Mod)e.AddedItems[0];
                SetWebsitePanelVisibility(SelectedMod.ModWebsite != Mod.DefaultWebsite);
                var installTarget = InstallationTargets.FirstOrDefault(x => x.Active && x.Game == SelectedMod.Game);
                if (installTarget != null)
                {
                    InstallationTargets_ComboBox.SelectedItem = installTarget;
                }
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

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void OpenOptions_Clicked(object sender, RoutedEventArgs e)
        {
            var o = new OptionsWindow();
            o.Owner = this;
            o.ShowDialog();
        }

        private void OpenModFolder_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                Process.Start(SelectedMod.ModPath);
            }
        }

        private void OpenME3Tweaks_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://me3tweaks.com");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var o = new AboutWindow();
            o.Owner = this;
            o.ShowDialog();
        }

        private void ModManagerWindow_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void FailedMods_LinkClick(object sender, RequestNavigateEventArgs e)
        {
            new FailedModsWindow(FailedMods.ToList()) { Owner = this }.ShowDialog();
        }

        private void OpenModsDirectory_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Utilities.GetModsDirectory());
        }

        public void PerformStartupNetworkFetches()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupNetworkThread");
            bw.DoWork += (a, b) =>
            {
                Log.Information("Start of startup network thread");
                var bgTask = backgroundTaskEngine.SubmitBackgroundJob("UpdateCheck", "Checking for Mod Manager updates", "Completed Mod Manager update check");
                var manifest = OnlineContent.FetchOnlineStartupManifest();
                if (int.Parse(manifest["latest_build_number"]) > App.BuildNumber)
                {
                    //Todo: Update available
                }
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("ThirdPartyIdentificationServiceFetch", "Initializing Third Party Identification Service information", "Initialized Third Party Identification Service");
                App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest(false);


                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("EnsureStaticFiles", "Downloading static files", "Static files downloaded");
                var success = OnlineContent.EnsureStaticAssets();
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                Properties.Settings.Default.LastContentCheck = DateTime.Now;
                Properties.Settings.Default.Save();
                Log.Information("End of startup network thread");
            };
            bw.RunWorkerAsync();
        }

        private void GenerateStarterKit_Clicked(object sender, RoutedEventArgs e)
        {
            MEGame g = MEGame.Unknown;
            if (sender == GenerateStarterKitME1_MenuItem) g = MEGame.ME1;
            if (sender == GenerateStarterKitME2_MenuItem) g = MEGame.ME2;
            if (sender == GenerateStarterKitME3_MenuItem) g = MEGame.ME3;
            new StarterKitGeneratorWindow(g) {Owner = this}.ShowDialog();
        }
    }
}
