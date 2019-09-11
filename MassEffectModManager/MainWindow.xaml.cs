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
using IniParser;
using IniParser.Parser;
using MassEffectModManager.GameDirectories;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.me3tweaks;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.modmanager.usercontrols;
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

        public bool IsBusy { get; set; }
        /// <summary>
        /// Content of the current Busy Indicator modal
        /// </summary>
        public object BusyContent { get; set; }
        public string CurrentDescriptionText { get; set; } = DefaultDescriptionText;
        private static readonly string DefaultDescriptionText = "Select a mod on the left to get started";
        public string ApplyModButtonText { get; set; } = "Apply Mod";
        public string AddTargetButtonText { get; set; } = "Add Target";
        public string StartGameButtonText { get; set; } = "Start Game";
        private int lastHintIndex = -1;
        private int oldFailedBindableCount = 0;
        public string NoModSelectedText
        {
            get
            {
                var retvar = "Select a mod on the left to view it's description.";
                //TODO: Implement Tips Service
                if (LoadedTips.Count > 0)
                {
                    var randomTip = LoadedTips.RandomElement();
                    retvar += $"\n\n---------------------------------------------\n{randomTip}";
                }
                return retvar;
            }
        }

        public Visibility BusyProgressVarVisibility { get; set; } = Visibility.Visible;

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
            AttachListeners();

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

        private void AttachListeners()
        {
            FailedMods.PublicPropertyChanged += (a, b) =>
            {
                if (b.PropertyName == "BindableCount")
                {
                    bool isopening = FailedMods.BindableCount > 0 && oldFailedBindableCount == 0;
                    bool isclosing = FailedMods.BindableCount == 0 && oldFailedBindableCount > 0;
                    if (isclosing || isopening)
                    {
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            Storyboard sb = this.FindResource(isopening ? "OpenWebsitePanel" : "CloseWebsitePanel") as Storyboard;
                            Storyboard.SetTarget(sb, FailedModsPanel);
                            sb.Begin();
                        });
                    }

                    oldFailedBindableCount = FailedMods.BindableCount;
                }
            };
        }

        public ICommand ReloadModsCommand { get; set; }
        public ICommand ApplyModCommand { get; set; }
        public ICommand CheckForContentUpdatesCommand { get; set; }
        private void LoadCommands()
        {
            ReloadModsCommand = new GenericCommand(ReloadMods, CanReloadMods);
            ApplyModCommand = new GenericCommand(ApplyMod, CanApplyMod);
            CheckForContentUpdatesCommand = new GenericCommand(CheckForContentUpdates, NetworkThreadNotRunning);
        }

        public bool ContentCheckInProgress { get; set; }
        private bool NetworkThreadNotRunning() => !ContentCheckInProgress;

        private void CheckForContentUpdates()
        {
            PerformStartupNetworkFetches(false);
        }

        public bool IsLoadingMods { get; set; }
        public List<string> LoadedTips { get; } = new List<string>();

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
            PerformStartupNetworkFetches(true);
        }

        public void LoadMods(Mod modToHighlight = null)
        {
            IsLoadingMods = true;
            LoadedMods.ClearEx();
            FailedMods.ClearEx();


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
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            LoadedMods.Add(mod);

                            LoadedMods.Sort(x => x.ModName);
                        });
                    }
                    else
                    {
                        FailedMods.Add(mod);


                        //Application.Current.Dispatcher.Invoke(delegate
                        //{
                        //    Storyboard sb = this.FindResource("OpenWebsitePanel") as Storyboard;
                        //    Storyboard.SetTarget(sb, FailedModsPanel);
                        //    sb.Begin();
                        //});
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

            var otherTargetsFileME1 = Utilities.GetCachedTargetsME1();
            var otherTargetsFileME2 = Utilities.GetCachedTargetsME2();
            var otherTargetsFileME3 = Utilities.GetCachedTargetsME3();

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

        public void PerformStartupNetworkFetches(bool checkForModManagerUpdates)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupNetworkThread");
            bw.WorkerReportsProgress = true;
            bw.ProgressChanged += (sender, args) =>
            {
                if (args.UserState is List<SortableHelpElement> sortableHelpItems)
                {
                    //Replacing the dynamic help menu
                    //DynamicHelp_MenuItem.Items.RemoveAll(x=>x.Tag is string str && str == "DynamicHelp");

                    var dynamicMenuItems = RecursiveBuildDynamicHelpMenuItems(sortableHelpItems);

                    //Clear old items out
                    for (int i = HelpMenuItem.Items.Count - 1; i > 0; i--)
                    {
                        if (HelpMenuItem.Items[i] is MenuItem menuItem && menuItem.Tag is string str && str == "DynamicHelp")
                        {
                            Debug.WriteLine("Removing old dynamic item");
                            HelpMenuItem.Items.Remove(menuItem);
                        }
                    }

                    dynamicMenuItems.Reverse(); //we are going to insert these in reverse order
                    var dynamicHelpHeaderIndex = HelpMenuItem.Items.IndexOf(DynamicHelp_MenuItem) + 1;
                    foreach (var v in dynamicMenuItems)
                    {
                        HelpMenuItem.Items.Insert(dynamicHelpHeaderIndex, v);
                    }
                }
            };
            bw.DoWork += (a, b) =>
            {
                Log.Information("Start of content check network thread");
                if (checkForModManagerUpdates)
                {
                    var updateCheckTask = backgroundTaskEngine.SubmitBackgroundJob("UpdateCheck", "Checking for Mod Manager updates", "Completed Mod Manager update check");
                    var manifest = OnlineContent.FetchOnlineStartupManifest();
                    if (int.Parse(manifest["latest_build_number"]) > App.BuildNumber)
                    {
                        //Todo: Update available
                    }

                    backgroundTaskEngine.SubmitJobCompletion(updateCheckTask);
                }

                var bgTask = backgroundTaskEngine.SubmitBackgroundJob("ThirdPartyIdentificationServiceFetch", "Initializing Third Party Identification Service information", "Initialized Third Party Identification Service");
                App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest(false);


                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("EnsureStaticFiles", "Downloading static files", "Static files downloaded");
                var success = OnlineContent.EnsureStaticAssets();
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadDynamicHelp", "Loading dynamic help", "Loaded dynamic help");
                var helpItemsLoading = OnlineContent.FetchLatestHelp();
                bw.ReportProgress(0, helpItemsLoading);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadTipsService", "Loading tips service", "Loaded tips service");
                LoadedTips.ReplaceAll(OnlineContent.FetchTipsService());
                OnPropertyChanged(nameof(NoModSelectedText));
                bw.ReportProgress(0, helpItemsLoading);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                Properties.Settings.Default.LastContentCheck = DateTime.Now;
                Properties.Settings.Default.Save();
                Log.Information("End of content check network thread");
            };
            bw.RunWorkerCompleted += (a, b) => { ContentCheckInProgress = false; };
            ContentCheckInProgress = true;
            bw.RunWorkerAsync();
        }

        private List<MenuItem> RecursiveBuildDynamicHelpMenuItems(List<SortableHelpElement> sortableHelpItems)
        {
            List<MenuItem> dynamicMenuItems = new List<MenuItem>();
            foreach (var item in sortableHelpItems)
            {
                MenuItem m = new MenuItem()
                {
                    Header = item.Title,
                    ToolTip = item.ToolTip,
                    Tag = "DynamicHelp"
                };
                if (!string.IsNullOrEmpty(item.URL))
                {
                    //URL HelpItem
                    m.Click += (o, eventArgs) => Utilities.OpenWebpage(item.URL);
                }
                else if (!string.IsNullOrEmpty(item.ModalTitle))
                {
                    //Modal dialog
                    item.ModalText = Utilities.ConvertBrToNewline(item.ModalText);
                    m.Click += (o, eventArgs) =>
                    {
                        new DynamicHelpItemModalWindow(item) { Owner = this }.ShowDialog();
                    };
                }
                dynamicMenuItems.Add(m);

                if (item.Children.Count > 0)
                {
                    var children = RecursiveBuildDynamicHelpMenuItems(item.Children);
                    foreach (var v in children)
                    {
                        m.Items.Add(v);
                    }
                }
            }

            return dynamicMenuItems;
        }

        private void GenerateStarterKit_Clicked(object sender, RoutedEventArgs e)
        {
            MEGame g = MEGame.Unknown;
            if (sender == GenerateStarterKitME1_MenuItem) g = MEGame.ME1;
            if (sender == GenerateStarterKitME2_MenuItem) g = MEGame.ME2;
            if (sender == GenerateStarterKitME3_MenuItem) g = MEGame.ME3;
            new StarterKitGeneratorWindow(g) { Owner = this }.ShowDialog();
        }

        private void LaunchExternalTool_Clicked(object sender, RoutedEventArgs e)
        {
            string tool = null;
            if (sender == ALOTInstaller_MenuItem) tool = ExternalToolLauncher.ALOTInstaller;
            if (sender == MassEffectRandomizer_MenuItem) tool = ExternalToolLauncher.MER;
            if (sender == MassEffectIniModder_MenuItem) tool = ExternalToolLauncher.MEIM;
            if (sender == ME3Explorer_MenuItem) tool = ExternalToolLauncher.ME3Explorer;
            if (sender == MassEffectModder_MenuItem) tool = ExternalToolLauncher.MEM;
            LaunchExternalTool(tool);
        }

        private void LaunchExternalTool(string tool, string arguments = null)
        {
            if (tool != null)
            {
                var exLauncher = new ExternalToolLauncher(tool,arguments);
                exLauncher.Close += (a, b) =>
                {
                    IsBusy = false;
                    BusyContent = null;
                };
                //Todo: Update Busy UI Content
                BusyProgressVarVisibility = Visibility.Collapsed;
                BusyContent = exLauncher;
                IsBusy = true;
                exLauncher.StartLaunchingTool();
            }
        }

        private void ASIModManager_Click(object sender, RoutedEventArgs e)
        {
            LaunchExternalTool(ExternalToolLauncher.ME3Explorer, ExternalToolLauncher.ME3EXP_ASIMANAGER);
        }
    }
}
