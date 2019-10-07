using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
//using IniParser;
//using IniParser.Parser;
using MassEffectModManagerCore;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.modmanager.windows;
using MassEffectModManagerCore.ui;
using ME3Explorer.Unreal;
//using ME3Explorer.Packages;
//using ME3Explorer.Unreal;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Serilog;

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
        private readonly string[] SupportedDroppableExtensions = { ".rar", ".zip", ".7z" };
        private bool StartupCompleted;
        public string ApplyModButtonText { get; set; } = "Apply Mod";
        public string AddTargetButtonText { get; set; } = "Add Target";
        public string StartGameButtonText { get; set; } = "Start Game";
        public string InstallationTargetText { get; set; } = "Installation Target:";
        public bool ME1ASILoaderInstalled { get; set; }
        public bool ME2ASILoaderInstalled { get; set; }
        public bool ME3ASILoaderInstalled { get; set; }

        public string ME1ASILoaderText { get; set; }
        public string ME2ASILoaderText { get; set; }
        public string ME3ASILoaderText { get; set; }
        private const string binkNotInstalledText = "Binkw32 ASI bypass not installed";
        private const string binkInstalledText = "Binkw32 ASI bypass installed";
        private const string binkME1NotInstalledText = "Binkw32 ASI loader not installed";
        private const string binkME1InstalledText = "Binkw32 ASI loader installed";
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

        /// <summary>
        /// User controls that are queued for displaying when the previous one has closed.
        /// </summary>
        private Queue<UserControl> queuedUserControls = new Queue<UserControl>();


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
        public ICommand AddTargetCommand { get; set; }
        public ICommand RunGameConfigToolCommand { get; set; }
        public ICommand Binkw32Command { get; set; }
        public ICommand StartGameCommand { get; set; }
        public ICommand ShowinstallationInformationCommand { get; set; }
        public ICommand BackupCommand { get; set; }

        private void LoadCommands()
        {
            ReloadModsCommand = new GenericCommand(ReloadMods, CanReloadMods);
            ApplyModCommand = new GenericCommand(CallApplyMod, CanApplyMod);
            CheckForContentUpdatesCommand = new GenericCommand(CheckForContentUpdates, NetworkThreadNotRunning);
            AddTargetCommand = new GenericCommand(AddTarget, () => ModsLoaded);
            RunGameConfigToolCommand = new RelayCommand(RunGameConfigTool, CanRunGameConfigTool);
            Binkw32Command = new RelayCommand(ToggleBinkw32, CanToggleBinkw32);
            StartGameCommand = new GenericCommand(StartGame, CanStartGame);
            ShowinstallationInformationCommand = new GenericCommand(ShowInstallInfo, CanShowInstallInfo);
            BackupCommand = new GenericCommand(ShowBackupPane, CanShowBackupPane);
        }

        private bool CanShowBackupPane()
        {
            return !ContentCheckInProgress;
        }

        /// <summary>
        /// Shows or queues the specified control
        /// </summary>
        /// <param name="control">Control to show or queue</param>
        private void ShowBusyControl(UserControl control)
        {
            if (queuedUserControls.Count == 0 && !IsBusy)
            {
                IsBusy = true;
                BusyContent = control;
            }
            else
            {
                queuedUserControls.Enqueue(control);
            }
        }

        /// <summary>
        /// Shows or queues the specified control
        /// </summary>
        /// <param name="control">Control to show or queue</param>
        private void ReleaseBusyControl()
        {
            if (queuedUserControls.Count == 0)
            {
                BusyContent = null;
                IsBusy = false;
            }
            else
            {
                BusyContent = queuedUserControls.Dequeue();
            }
        }

        private void ShowBackupPane()
        {
            var backupRestoreManager = new BackupRestoreManager(InstallationTargets.ToList(), SelectedGameTarget, this);
            backupRestoreManager.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is string result)
                {
                    if (result == "ALOTInstaller")
                    {
                        LaunchExternalTool(ExternalToolLauncher.ALOTInstaller);
                    }
                }
            };
            UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
            ShowBusyControl(backupRestoreManager); //Todo: Support the progress bar updates in the queue
        }

        private void ShowInstallInfo()
        {
            var installationInformation = new InstallationInformation(InstallationTargets.ToList(), SelectedGameTarget);
            installationInformation.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is string result)
                {
                    if (result == "ALOTInstaller")
                    {
                        LaunchExternalTool(ExternalToolLauncher.ALOTInstaller);
                    }
                }
            };
            UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
            ShowBusyControl(installationInformation); //Todo: Support the progress bar updates in the queue
            //installationInformation.ShowInfo();
        }

        private bool CanShowInstallInfo()
        {
            return SelectedGameTarget != null && SelectedGameTarget.IsValid && SelectedGameTarget.Selectable && !ContentCheckInProgress;
        }

        private void CallApplyMod()
        {
            ApplyMod(SelectedMod);
        }

        private void StartGame()
        {
            var exePath = MEDirectories.ExecutablePath(SelectedGameTarget);
            Process.Start(exePath);
        }

        private bool CanStartGame()
        {
            //Todo: Check if this is origin game and if target will boot
            return SelectedGameTarget != null && SelectedGameTarget.Selectable /*&& SelectedGameTarget.RegistryActive*/;
        }

        private bool CanToggleBinkw32(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                return File.Exists(Utilities.GetBinkw32File(target));
            }

            return false;
        }

        private void ToggleBinkw32(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                bool install = false;
                switch (game)
                {
                    case Mod.MEGame.ME1:
                        install = !ME1ASILoaderInstalled;
                        break;
                    case Mod.MEGame.ME2:
                        install = !ME2ASILoaderInstalled;
                        break;
                    case Mod.MEGame.ME3:
                        install = !ME3ASILoaderInstalled;
                        break;
                }

                if (install)
                {
                    Utilities.InstallBinkBypass(target);
                }
                else
                {
                    Utilities.UninstallBinkBypass(target);
                }

                UpdateBinkStatus(target.Game);
            }
        }

        private void RunGameConfigTool(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                var configTool = Utilities.GetGameConfigToolPath(target);
                Process.Start(configTool);
            }
        }

        private bool CanRunGameConfigTool(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                var configTool = Utilities.GetGameConfigToolPath(target);
                return File.Exists(configTool);
            }
            return false;
        }

        private void AddTarget()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = $"Select game executable";
            string filter = $"Game executable|MassEffect.exe;MassEffect2.exe;MassEffect3.exe";
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                Mod.MEGame gameSelected = Mod.MEGame.Unknown;
                var filename = Path.GetFileName(ofd.FileName);
                if (filename.Equals("MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase)) gameSelected = Mod.MEGame.ME3;
                if (filename.Equals("MassEffect2.exe", StringComparison.InvariantCultureIgnoreCase)) gameSelected = Mod.MEGame.ME2;
                if (filename.Equals("MassEffect.exe", StringComparison.InvariantCultureIgnoreCase)) gameSelected = Mod.MEGame.ME1;

                if (gameSelected != Mod.MEGame.Unknown)
                {
                    string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                    if (gameSelected == Mod.MEGame.ME3)
                        result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                    var pendingTarget = new GameTarget(gameSelected, result, false);
                    string failureReason = pendingTarget.ValidateTarget();
                    if (failureReason == null)
                    {
                        Utilities.AddCachedTarget(pendingTarget);
                        PopulateTargets();
                    }
                    else
                    {
                        MessageBox.Show("Unable to add this directory as a target:\n" + failureReason);
                    }
                }

            }
        }

        public bool ContentCheckInProgress { get; set; } = true; //init is content check
        private bool NetworkThreadNotRunning() => !ContentCheckInProgress;

        private void CheckForContentUpdates()
        {
            PerformStartupNetworkFetches(false);
        }

        public bool IsLoadingMods { get; set; }
        public List<string> LoadedTips { get; } = new List<string>();
        public bool ModsLoaded { get; private set; } = false;
        public GameTarget SelectedGameTarget { get; private set; }

        private bool CanReloadMods()
        {
            return !IsLoadingMods;
        }

        private bool CanApplyMod()
        {
            return SelectedMod != null && InstallationTargets_ComboBox.SelectedItem is GameTarget gt && gt.Game == SelectedMod.Game;
        }

        private void ApplyMod(Mod mod)
        {
            BackgroundTask modInstallTask = backgroundTaskEngine.SubmitBackgroundJob("ModInstall", $"Installing {mod.ModName}", $"Installed {mod.ModName}");
            var modInstaller = new ModInstaller(mod, SelectedGameTarget);
            modInstaller.Close += (a, b) =>
            {

                if (!modInstaller.InstallationSucceeded)
                {
                    if (modInstaller.InstallationCancelled)
                    {
                        modInstallTask.finishedUiText = $"Installation aborted";
                        ReleaseBusyControl();
                        backgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                        return;
                    }
                    else
                    {
                        modInstallTask.finishedUiText = $"Failed to install {mod.ModName}";
                    }
                }

                //Run AutoTOC if ME3
                if (SelectedGameTarget.Game == Mod.MEGame.ME3)
                {
                    var autoTocUI = new AutoTOC(SelectedGameTarget);
                    autoTocUI.Close += (a, b) =>
                    {
                        ReleaseBusyControl();
                        backgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                    };
                    ShowBusyControl(autoTocUI);
                    ReleaseBusyControl();
                    autoTocUI.RunAutoTOC();
                }
                else
                {
                    ReleaseBusyControl();
                    backgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                }
            };
            UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
            ShowBusyControl(modInstaller);
            modInstaller.PrepareToInstallMod();
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

        private void UpdateBinkStatus(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(GetCurrentTarget(Mod.MEGame.ME1));
                    ME1ASILoaderText = ME1ASILoaderInstalled ? binkME1InstalledText : binkME1NotInstalledText;
                    break;
                case Mod.MEGame.ME2:
                    ME2ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(GetCurrentTarget(Mod.MEGame.ME2));
                    ME2ASILoaderText = ME2ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case Mod.MEGame.ME3:
                    ME3ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(GetCurrentTarget(Mod.MEGame.ME3));
                    ME3ASILoaderText = ME3ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
            }
        }

        private GameTarget GetCurrentTarget(Mod.MEGame game) => InstallationTargets.FirstOrDefault(x => x.Game == game && x.RegistryActive);

        public void LoadMods(Mod modToHighlight = null)
        {
            IsLoadingMods = true;
            LoadedMods.ClearEx();
            FailedMods.ClearEx();


            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModLoaderThread");
            bw.WorkerReportsProgress = true;
            bw.DoWork += (a, args) =>
            {
                ModsLoaded = false;
                var uiTask = backgroundTaskEngine.SubmitBackgroundJob("ModLoader", "Loading mods", "Loaded mods");
                CLog.Information("Loading mods from mod library: " + Utilities.GetModsDirectory(), Settings.LogModStartup);
                var me3modDescsToLoad = Directory.GetDirectories(Utilities.GetME3ModsDirectory()).Select(x => (game: Mod.MEGame.ME3, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
                var me2modDescsToLoad = Directory.GetDirectories(Utilities.GetME2ModsDirectory()).Select(x => (game: Mod.MEGame.ME2, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
                var me1modDescsToLoad = Directory.GetDirectories(Utilities.GetME1ModsDirectory()).Select(x => (game: Mod.MEGame.ME1, path: Path.Combine(x, "moddesc.ini"))).Where(x => File.Exists(x.path));
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
                UpdateBinkStatus(Mod.MEGame.ME1);
                UpdateBinkStatus(Mod.MEGame.ME2);
                UpdateBinkStatus(Mod.MEGame.ME3);
                backgroundTaskEngine.SubmitJobCompletion(uiTask);

                //DEBUG ONLY - MOVE THIS SOMEWHERE ELSE IN FUTURE (or gate behind time check... or something... move to separate method)
                BackgroundTask bgTask = backgroundTaskEngine.SubmitBackgroundJob("ModCheckForUpdates", "Checking mods for updates", "Mod update check completed");
                var updates = OnlineContent.CheckForModUpdates(LoadedMods.ToList(), true).Where(x => x.applicableUpdates.Count > 0 || x.filesToDelete.Count > 0).ToList();
                if (updates.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var modUpdatesNotificationDialog = new ModUpdateInformation(updates);
                        modUpdatesNotificationDialog.Close += (sender, args) =>
                        {
                            ReleaseBusyControl();
                            if (args.Data is bool reloadMods && reloadMods)
                            {
                                LoadMods(updates.Count == 1 ? updates[0].mod : null);
                            }

                        };
                        UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
                        ShowBusyControl(modUpdatesNotificationDialog);
                    });
                }
                OnPropertyChanged(nameof(NoModSelectedText));
                backgroundTaskEngine.SubmitJobCompletion(bgTask);
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                IsLoadingMods = false;
                if (b.Result is Mod m)
                {
                    ModsList_ListBox.SelectedItem = m;
                }
                ModsLoaded = true;
            };
            bw.RunWorkerAsync();
        }

        private void PopulateTargets(GameTarget selectedTarget = null)
        {
            InstallationTargets.ClearEx();
            Log.Information("Populating game targets");
            if (ME3Directory.gamePath != null)
            {
                var target = new GameTarget(Mod.MEGame.ME3, ME3Directory.gamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    Log.Information("Current boot target for ME3: " + target.TargetPath);
                    InstallationTargets.Add(target);
                }
                else
                {
                    Log.Error("Current boot target for ME3 is invalid: " + failureReason);
                }
            }

            if (ME2Directory.gamePath != null)
            {
                var target = new GameTarget(Mod.MEGame.ME2, ME2Directory.gamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    Log.Information("Current boot target for ME2: " + target.TargetPath);
                    InstallationTargets.Add(target);
                }
                else
                {
                    Log.Error("Current boot target for ME2 is invalid: " + failureReason);
                }
            }
            if (ME1Directory.gamePath != null)
            {
                var target = new GameTarget(Mod.MEGame.ME1, ME1Directory.gamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    Log.Information("Current boot target for ME1: " + target.TargetPath);
                    InstallationTargets.Add(target);
                }
                else
                {
                    Log.Error("Current boot target for ME1 is invalid: " + failureReason);
                }
            }

            // TODO: Read and import java version configuration

            var otherTargetsFileME1 = Utilities.GetCachedTargets(Mod.MEGame.ME1);
            var otherTargetsFileME2 = Utilities.GetCachedTargets(Mod.MEGame.ME2);
            var otherTargetsFileME3 = Utilities.GetCachedTargets(Mod.MEGame.ME3);

            if (otherTargetsFileME1.Any() || otherTargetsFileME2.Any() || otherTargetsFileME3.Any())
            {
                InstallationTargets.Add(new GameTarget(Mod.MEGame.Unknown, "===================Other saved targets===================", false) { Selectable = false });
                InstallationTargets.AddRange(otherTargetsFileME1);
                InstallationTargets.AddRange(otherTargetsFileME2);
                InstallationTargets.AddRange(otherTargetsFileME3);

            }
            if (selectedTarget != null)
            {
                InstallationTargets_ComboBox.SelectedItem = selectedTarget;
            }
        }

        private void ModsList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedMod = (Mod)e.AddedItems[0];
                SetWebsitePanelVisibility(SelectedMod.ModWebsite != Mod.DefaultWebsite);
                var installTarget = InstallationTargets.FirstOrDefault(x => x.RegistryActive && x.Game == SelectedMod.Game);
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
                Utilities.OpenExplorer(SelectedMod.ModPath);
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
            Settings.Save();
        }

        private void FailedMods_LinkClick(object sender, RequestNavigateEventArgs e)
        {
            new FailedModsWindow(FailedMods.ToList()) { Owner = this }.ShowDialog();
        }

        private void OpenModsDirectory_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenExplorer(Utilities.GetModsDirectory());
        }

        private const int STARTUP_FAIL_CRITICAL_FILES_MISSING = 1;

        public void PerformStartupNetworkFetches(bool checkForModManagerUpdates)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ContentCheckNetworkThread");
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



                BackgroundTask bgTask;
                bool success;

                if (checkForModManagerUpdates)
                {
                    bgTask = backgroundTaskEngine.SubmitBackgroundJob("EnsureStaticFiles", "Downloading required files", "Required files downloaded");
                    if (!OnlineContent.EnsureCriticalFiles())
                    {
                        //Critical files not loaded!

                        b.Result = STARTUP_FAIL_CRITICAL_FILES_MISSING;
                        bgTask.finishedUiText = "Failed to acquire required files";
                        backgroundTaskEngine.SubmitJobCompletion(bgTask);
                        return;
                    }

                    backgroundTaskEngine.SubmitJobCompletion(bgTask);

                    var updateCheckTask = backgroundTaskEngine.SubmitBackgroundJob("UpdateCheck", "Checking for Mod Manager updates", "Completed Mod Manager update check");
                    try
                    {
                        var manifest = OnlineContent.FetchOnlineStartupManifest();
                        if (int.Parse(manifest["latest_build_number"]) > App.BuildNumber)
                        {
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                var updateAvailableDialog = new ProgramUpdateNotification();
                                updateAvailableDialog.Close += (sender, args) => { ReleaseBusyControl(); };
                                UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
                                ShowBusyControl(updateAvailableDialog);
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        //Error checking for updates!
                        Log.Error("Checking for updates failed: " + App.FlattenException(e));
                        updateCheckTask.finishedUiText = "Failed to check for updates";
                    }

                    backgroundTaskEngine.SubmitJobCompletion(updateCheckTask);
                }

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("EnsureStaticFiles", "Downloading static files", "Static files downloaded");
                success = OnlineContent.EnsureStaticAssets();
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadDynamicHelp", "Loading dynamic help", "Loaded dynamic help");
                var helpItemsLoading = OnlineContent.FetchLatestHelp(!checkForModManagerUpdates);
                bw.ReportProgress(0, helpItemsLoading);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("ThirdPartyIdentificationServiceFetch", "Loading Third Party Identification Service", "Loaded Third Party Identification Service");
                App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest(!checkForModManagerUpdates);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);


                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadTipsService", "Loading Third Party Importing Service", "Loaded Third Party Importing Service");
                App.ThirdPartyImportingService = OnlineContent.FetchThirdPartyImportingService(!checkForModManagerUpdates);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);


                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadTipsService", "Loading tips service", "Loaded tips service");
                LoadedTips.ReplaceAll(OnlineContent.FetchTipsService(!checkForModManagerUpdates));
                OnPropertyChanged(nameof(NoModSelectedText));
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadObjectInfo", "Loading package information database", "Loaded package information database");

                ME3UnrealObjectInfo.loadfromJSON();
                ME2UnrealObjectInfo.loadfromJSON();
                ME1UnrealObjectInfo.loadfromJSON();
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                //bgTask = backgroundTaskEngine.SubmitBackgroundJob("Test", "Running test", "Test completed");

                //string testfileu = @"C:\Users\Public\uncompressed.pcc";
                //string testfilec = @"C:\Users\Public\compressed.pcc";
                //var package = MEPackageHandler.OpenMEPackage(testfileu);
                //package.save(testfilec, true);

                //testfileu = @"C:\Users\Public\uncompressed.sfm";
                //if (File.Exists(testfileu))
                //{
                //    testfilec = @"C:\Users\Public\compressed.sfm";
                //    package = MEPackageHandler.OpenMEPackage(testfileu);
                //    package.save(testfilec, true);
                //}
                //backgroundTaskEngine.SubmitJobCompletion(bgTask);

                //TODO: FIX THIS FOR .NET CORE
                //Properties.Settings.Default.LastContentCheck = DateTime.Now;
                //Properties.Settings.Default.Save();
                Log.Information("End of content check network thread");
                b.Result = 0; //all good
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is int i)
                {
                    if (i != 0)
                    {
                        switch (i)
                        {
                            case STARTUP_FAIL_CRITICAL_FILES_MISSING:
                                var res = Xceed.Wpf.Toolkit.MessageBox.Show($"Mod Manager was unable to acquire files that are required for the program to function properly.\nPlease ensure you are connected to the internet and that both GitHub and ME3Tweaks is reachable.", $"Required files unable to be downloaded", MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                        }
                    }
                    else
                    {
                        ContentCheckInProgress = false;
                    }
                }
                StartupCompleted = true;
                CommandManager.InvalidateRequerySuggested(); //refresh bindings that depend on this
            };
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
            Mod.MEGame g = Mod.MEGame.Unknown;
            if (sender == GenerateStarterKitME1_MenuItem) g = Mod.MEGame.ME1;
            if (sender == GenerateStarterKitME2_MenuItem) g = Mod.MEGame.ME2;
            if (sender == GenerateStarterKitME3_MenuItem) g = Mod.MEGame.ME3;
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
                var exLauncher = new ExternalToolLauncher(tool, arguments);
                exLauncher.Close += (a, b) =>
                {
                    ReleaseBusyControl();
                };
                //Todo: Update Busy UI Content
                UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Collapsed));
                ShowBusyControl(exLauncher);

                exLauncher.StartLaunchingTool();
            }
        }

        private void ASIModManager_Click(object sender, RoutedEventArgs e)
        {
            LaunchExternalTool(ExternalToolLauncher.ME3Explorer, ExternalToolLauncher.ME3EXP_ASIMANAGER);
        }

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedGameTarget = InstallationTargets_ComboBox.SelectedItem as GameTarget;
        }

        private void UploadLog_Click(object sender, RoutedEventArgs e)
        {
            var logUploaderUI = new LogUploader(UpdateBusyProgressBarCallback);
            logUploaderUI.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(logUploaderUI);
        }

        public Visibility BusyProgressBarVisibility { get; set; } = Visibility.Visible;
        public int BusyProgressBarMaximum { get; set; } = 100;
        public int BusyProgressBarValue { get; set; } = 0;
        public bool BusyProgressBarIndeterminate { get; set; } = true;

        /// <summary>
        /// Updates the progressbar that the user controls use
        /// </summary>
        /// <param name="update"></param>
        private void UpdateBusyProgressBarCallback(ProgressBarUpdate update)
        {
            switch (update.UpdateType)
            {
                case ProgressBarUpdate.UpdateTypes.SET_VISIBILITY:
                    BusyProgressBarVisibility = update.GetDataAsVisibility();
                    break;
                case ProgressBarUpdate.UpdateTypes.SET_MAX:
                    BusyProgressBarMaximum = update.GetDataAsInt();
                    break;
                case ProgressBarUpdate.UpdateTypes.SET_VALUE:
                    BusyProgressBarValue = update.GetDataAsInt();
                    break;
                case ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE:
                    BusyProgressBarIndeterminate = update.GetDataAsBool();
                    break;

            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                switch (ext)
                {
                    case ".rar":
                    case ".7z":
                    case ".zip":
                        openModImportUI(files[0]);
                        break;
                        //TPF, .mod, .mem

                }
            }
        }

        private void openModImportUI(string archiveFile = null)
        {
            var modInspector = new ModArchiveImporter(UpdateBusyProgressBarCallback);
            modInspector.Close += (a, b) =>
            {

                if (b.Data is List<Mod> modsImported)
                {
                    ReleaseBusyControl();
                    LoadMods(modsImported.Count == 1 ? modsImported.First() : null);
                }
                else if (b.Data is Mod compressedModToInstall)
                {
                    ApplyMod(compressedModToInstall);
                }
                else
                {
                    ReleaseBusyControl();
                }
            };
            ShowBusyControl(modInspector); //todo: Set progress bar params

            if (archiveFile != null)
            {
                modInspector.InspectArchiveFile(archiveFile);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (!SupportedDroppableExtensions.Contains(ext))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void RunAutoTOC_Clicked(object sender, RoutedEventArgs e)
        {
            var autoTocUI = new AutoTOC(GetCurrentTarget(Mod.MEGame.ME3));
            autoTocUI.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(autoTocUI);
            autoTocUI.RunAutoTOC();
        }

        private void ChangeSetting_Clicked(object sender, RoutedEventArgs e)
        {
            var callingMember = (MenuItem)sender;
            //if (callingMember == LogModStartup_MenuItem)
            //{
            //    Settings.LogModStartup = !Settings.LogModStartup; //flip
            //}
            //else if (callingMember == LogMixinStartup_MenuItem)
            //{
            //    Settings.LogMixinStartup = !Settings.LogMixinStartup; //flip
            //}
            //else 
            if (callingMember == SetModLibraryPath_MenuItem)
            {
                CommonOpenFileDialog m = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    Title = "Select mod library folder"
                };
                if (m.ShowDialog(this) == CommonFileDialogResult.Ok)
                {
                    Settings.ModLibraryPath = m.FileName;
                    Utilities.EnsureModDirectories();
                    LoadMods();
                }
            }
            else
            {
                //unknown caller
                return;
            }
            Settings.Save();
        }
    }
}
