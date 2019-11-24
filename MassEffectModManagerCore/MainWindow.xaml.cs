using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using AdonisUI;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.modmanager.windows;
using MassEffectModManagerCore.ui;
using ME3Explorer.Unreal;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string CurrentOperationText { get; set; } = "Starting up";

        public bool IsBusy { get; set; }

        /// <summary>
        /// Content of the current Busy Indicator modal
        /// </summary>
        public object BusyContent { get; set; }

        public string CurrentDescriptionText { get; set; } = DefaultDescriptionText;
        private static readonly string DefaultDescriptionText = "Select a mod on the left to get started";
        private readonly string[] SupportedDroppableExtensions = {".rar", ".zip", ".7z", ".exe", ".tpf", ".mod", ".mem"};
        private bool StartupCompleted;
        public string ApplyModButtonText { get; set; } = "Apply Mod";
        public string AddTargetButtonText { get; set; } = "Add Target";
        public string StartGameButtonText { get; set; } = "Start Game";
        public string InstallationTargetText { get; set; } = "Installation Target:";
        public bool ME1ASILoaderInstalled { get; set; }
        public bool ME2ASILoaderInstalled { get; set; }
        public bool ME3ASILoaderInstalled { get; set; }
        public bool ME1ModsVisible { get; set; } = true;
        public bool ME2ModsVisible { get; set; } = true;
        public bool ME3ModsVisible { get; set; } = true;
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
        private Queue<MMBusyPanelBase> queuedUserControls = new Queue<MMBusyPanelBase>();


        public Mod SelectedMod { get; set; }
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<Mod> AllLoadedMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();

        private BackgroundTaskEngine backgroundTaskEngine;

        //private ModLoader modLoader;
        public MainWindow()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            languageMenuItems = new[] {LanguageINT_MenuItem, LanguageRUS_MenuItem, LanguagePOL_MenuItem};
            PopulateTargets();
            AttachListeners();
            SetTheme();
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

        public ICommand ImportArchiveCommand { get; set; }
        public ICommand ReloadModsCommand { get; set; }
        public ICommand ApplyModCommand { get; set; }
        public ICommand RestoreCommand { get; set; }
        public ICommand CheckForContentUpdatesCommand { get; set; }
        public ICommand AddTargetCommand { get; set; }
        public ICommand RunGameConfigToolCommand { get; set; }
        public ICommand Binkw32Command { get; set; }
        public ICommand StartGameCommand { get; set; }
        public ICommand ShowinstallationInformationCommand { get; set; }
        public ICommand BackupCommand { get; set; }
        public ICommand DeployModCommand { get; set; }
        public ICommand DeleteModFromLibraryCommand { get; set; }
        public ICommand SubmitTelemetryForModCommand { get; set; }
        public ICommand SelectedModCheckForUpdatesCommand { get; set; }
        public ICommand RestoreModFromME3TweaksCommand { get; set; }
        public ICommand GrantWriteAccessCommand { get; set; }
        public ICommand AutoTOCCommand { get; set; }
        public ICommand ME3UICompatibilityPackGeneratorCommand { get; set; }
        public ICommand EnableME1ConsoleCommand { get; set; }

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
            BackupCommand = new GenericCommand(ShowBackupPane, ContentCheckNotInProgress);
            RestoreCommand = new GenericCommand(ShowRestorePane, ContentCheckNotInProgress);
            DeployModCommand = new GenericCommand(ShowDeploymentPane, CanShowDeploymentPane);
            DeleteModFromLibraryCommand = new GenericCommand(DeleteModFromLibrary, CanDeleteModFromLibrary);
            ImportArchiveCommand = new GenericCommand(OpenArchiveSelectionDialog, CanOpenArchiveSelectionDialog);
            SubmitTelemetryForModCommand = new GenericCommand(SubmitTelemetryForMod, CanSubmitTelemetryForMod);
            SelectedModCheckForUpdatesCommand = new GenericCommand(CheckSelectedModForUpdate, SelectedModIsME3TweaksUpdatable);
            RestoreModFromME3TweaksCommand = new GenericCommand(RestoreSelectedMod, SelectedModIsME3TweaksUpdatable);
            GrantWriteAccessCommand = new GenericCommand(() => CheckTargetPermissions(true, true), HasAtLeastOneTarget);
            AutoTOCCommand = new GenericCommand(RunAutoTOCOnTarget, HasME3Target);
            ME3UICompatibilityPackGeneratorCommand = new GenericCommand(RunCompatGenerator, CanRunCompatGenerator);
            EnableME1ConsoleCommand = new GenericCommand(EnableME1Console, CanEnableME1Console);
        }

        private bool CanEnableME1Console()
        {
            var installed = InstallationTargets.Any(x => x.Game == Mod.MEGame.ME1);
            if (installed)
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect", "Config", "BIOInput.ini");
                return File.Exists(iniFile);
            }

            return false;
        }

        private void EnableME1Console()
        {
            var installed = InstallationTargets.Any(x => x.Game == Mod.MEGame.ME1);
            if (installed)
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect", "Config", "BIOInput.ini");
                if (File.Exists(iniFile))
                {
                    var ini = DuplicatingIni.LoadIni(iniFile);
                    var engineConsole = ini.Sections.FirstOrDefault(x => x.Header == "Engine.Console");
                    if (engineConsole != null)
                    {
                        var consoleKey = engineConsole.Entries.FirstOrDefault(x => x.Key == "ConsoleKey");
                        if (consoleKey == null)
                        {
                            engineConsole.Entries.Add(new DuplicatingIni.IniEntry("ConsoleKey=Tilde"));
                        }

                        var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == "TypeKey");
                        if (typeKey == null)
                        {
                            engineConsole.Entries.Add(new DuplicatingIni.IniEntry("TypeKey=Tab"));
                        }


                        try
                        {
                            File.WriteAllText(iniFile, ini.ToString());
                            Analytics.TrackEvent("Enabled the ME1 console", new Dictionary<string, string>() {{"Succeeded", "true"}});
                            Xceed.Wpf.Toolkit.MessageBox.Show(this, "Console enabled.\nPress ~ to open the full size console.\nPress TAB to open the mini console.", "Console enabled");
                        }
                        catch (Exception e)
                        {
                            Log.Error("Unable to enable console: " + e.Message);
                            Analytics.TrackEvent("Enabled the ME1 console", new Dictionary<string, string>() {{"Succeeded", "false"}});
                            Crashes.TrackError(e);
                            Xceed.Wpf.Toolkit.MessageBox.Show(this, "Unable to modify bioinput.ini: " + e.Message, "Could not enable console", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void RunCompatGenerator()
        {
            var guiCompatibilityGenerator = new GUICompatibilityGenerator(SelectedGameTarget);
            guiCompatibilityGenerator.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(guiCompatibilityGenerator);
        }

        private bool CanRunCompatGenerator()
        {
            //disable in this build
            return SelectedGameTarget?.Game == Mod.MEGame.ME3;
        }

        public bool HasAtLeastOneTarget() => InstallationTargets.Any();

        private bool HasME3Target()
        {
            return InstallationTargets.Any(x => x.Game == Mod.MEGame.ME3);
        }

        private void CheckSelectedModForUpdate()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) => { CheckModsForUpdates(new List<Mod>(new[] {SelectedMod})); };
            bw.RunWorkerAsync();

        }

        private void RestoreSelectedMod()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) => { CheckModsForUpdates(new List<Mod>(new[] {SelectedMod}), true); };
            bw.RunWorkerAsync();
        }

        private bool SelectedModIsME3TweaksUpdatable() => SelectedMod?.IsUpdatable ?? false;


        private void SubmitTelemetryForMod()
        {
            var telemetryPane = new TPMITelemetrySubmissionForm(SelectedMod);
            telemetryPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(telemetryPane);
        }

        private bool CanSubmitTelemetryForMod() => SelectedMod?.GetJob(ModJob.JobHeader.CUSTOMDLC) != null;

        private void OpenArchiveSelectionDialog()
        {
            OpenFileDialog m = new OpenFileDialog
            {
                Title = "Select mod archive"
            };
            var result = m.ShowDialog(this);
            if (result.Value)
            {
                Analytics.TrackEvent("User opened mod archive for import", new Dictionary<string, string> {{"Method", "Manual file selection"}, {"Filename", Path.GetFileName(m.FileName)}});
                var archiveFile = m.FileName;
                Log.Information("Opening archive user selected: " + archiveFile);
                openModImportUI(archiveFile);
            }
        }

        private bool CanOpenArchiveSelectionDialog()
        {
            return App.ThirdPartyImportingService != null && App.ThirdPartyIdentificationService != null && !ContentCheckInProgress;
        }

        private bool CanDeleteModFromLibrary() => SelectedMod != null && !ContentCheckInProgress;

        private void DeleteModFromLibrary()
        {
            var confirmationResult = Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Delete {SelectedMod.ModName} from your mod library? This only deletes the mod from your local Mod Manager library, it does not remove it from any game installations.", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmationResult == MessageBoxResult.Yes)
            {
                Log.Information("Deleting mod from library: " + SelectedMod.ModPath);
                Utilities.DeleteFilesAndFoldersRecursively(SelectedMod.ModPath);
                LoadMods();
            }
        }

        private void ShowDeploymentPane()
        {
            var archiveDeploymentPane = new ArchiveDeployment(SelectedMod, this);
            archiveDeploymentPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(archiveDeploymentPane);
        }

        private void ShowUpdateCompletedPane()
        {
            var message = $"Mod Manager has been updated from build {App.UpdatedFrom} to version {App.AppVersionAbout}.";
            var archiveDeploymentPane = new UpdateCompletedPanel("Update completed", message);
            archiveDeploymentPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(archiveDeploymentPane);
        }

        private bool CanShowDeploymentPane()
        {
            return SelectedMod != null && Settings.DeveloperMode;
        }

        private bool ContentCheckNotInProgress()
        {
            return !ContentCheckInProgress;
        }

        /// <summary>
        /// Shows or queues the specified control
        /// </summary>
        /// <param name="control">Control to show or queue</param>
        private void ShowBusyControl(MMBusyPanelBase control)
        {
            if (queuedUserControls.Count == 0 && !IsBusy)
            {
                IsBusy = true;
                control.OnPanelVisible();
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
                var control = queuedUserControls.Dequeue();
                control.OnPanelVisible();
                BusyContent = control;
            }
        }

        private void ShowBackupPane()
        {
            var backupRestoreManager = new BackupCreator(InstallationTargets.ToList(), SelectedGameTarget, this);
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
            ShowBusyControl(backupRestoreManager);
        }

        private void ShowRestorePane()
        {
            var restoreManager = new RestorePanel(InstallationTargets.ToList(), SelectedGameTarget);
            restoreManager.Close += (a, b) =>
            {
                if (b.Data is bool refresh && refresh)
                {
                    PopulateTargets(SelectedGameTarget);
                }

                ReleaseBusyControl();
            };
            ShowBusyControl(restoreManager);
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

                    if (result == "ReloadTargets")
                    {
                        PopulateTargets();
                    }
                }
            };
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

            var game = "Mass Effect";
            if (SelectedGameTarget.Game == Mod.MEGame.ME2)
            {
                game += " 2";
            }
            else if (SelectedGameTarget.Game == Mod.MEGame.ME3)
            {
                game += " 3";
            }

            BackgroundTask gameLaunch = backgroundTaskEngine.SubmitBackgroundJob("GameLaunch", $"Launching {game}", $"Launched {game}");
            Task.Delay(TimeSpan.FromMilliseconds(4000))
                .ContinueWith(task => backgroundTaskEngine.SubmitJobCompletion(gameLaunch));
            try
            {
                Utilities.RunProcess(MEDirectories.ExecutablePath(SelectedGameTarget), (string) null, false, true);
            }
            catch (Exception e)
            {
                if (e is Win32Exception w32e)
                {
                    if (w32e.NativeErrorCode == 1223)
                    {
                        //Admin canceled.
                        return; //we don't care.
                    }
                }

                Log.Error("Error launching game: " + e.Message);
            }

            //Detect screen resolution - useful info for scene modders
            string resolution = "Could not detect";
            var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare");
            switch (SelectedGameTarget.Game)
            {
                case Mod.MEGame.ME1:
                {
                    iniFile = Path.Combine(iniFile, "Mass Effect", "Config", "BIOEngine.ini");
                    if (File.Exists(iniFile))
                    {
                        var dini = DuplicatingIni.LoadIni(iniFile);
                        var section = dini.Sections.FirstOrDefault(x => x.Header == "WinDrv.WindowsClient");
                        if (section != null)
                        {
                            var resx = section.Entries.FirstOrDefault(x => x.Key == "StartupResolutionX");
                            var resy = section.Entries.FirstOrDefault(x => x.Key == "StartupResolutionY");
                            if (resx != null && resy != null)
                            {
                                resolution = $"{resx.Value}x{resy.Value}";
                            }
                        }
                    }
                }
                    break;
                case Mod.MEGame.ME2:
                case Mod.MEGame.ME3:
                {
                    iniFile = Path.Combine(iniFile, "Mass Effect " + SelectedGameTarget.Game.ToString().Substring(2), "BIOGame", "Config", "Gamersettings.ini");
                    if (File.Exists(iniFile))
                    {
                        var dini = DuplicatingIni.LoadIni(iniFile);
                        var section = dini.Sections.FirstOrDefault(x => x.Header == "SystemSettings");
                        if (section != null)
                        {
                            var resx = section.Entries.FirstOrDefault(x => x.Key == "ResX");
                            var resy = section.Entries.FirstOrDefault(x => x.Key == "ResY");
                            if (resx != null && resy != null)
                            {
                                resolution = $"{resx.Value}x{resy.Value}";
                            }
                        }
                    }
                }
                    break;
            }

            Analytics.TrackEvent("Launched game", new Dictionary<string, string>()
            {
                {"Game", game},
                {"Screen resolution", resolution}
            });
            //var exePath = MEDirectories.ExecutablePath(SelectedGameTarget);
            //Process.Start(exePath);

        }

        /// <summary>
        /// Updates boot target and returns the HRESULT of the update command for registry.
        /// Returns -3 if no registry update was performed.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private int UpdateBootTarget(GameTarget target)
        {
            string exe = "reg";
            var args = new List<string>();
            string regPath = null;
            switch (target.Game)
            {
                case Mod.MEGame.ME1:
                {
                    var existingPath = ME1Directory.gamePath;
                    if (existingPath != null)
                    {
                        regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect";
                    }
                }
                    break;
                case Mod.MEGame.ME2:
                {
                    var existingPath = ME1Directory.gamePath;
                    if (existingPath != null)
                    {
                        regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect 2";
                    }
                }

                    break;
                case Mod.MEGame.ME3:
                {
                    var existingPath = ME1Directory.gamePath;
                    if (existingPath != null)
                    {
                        regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect 3";
                    }
                }
                    break;
            }

            if (regPath != null)
            {
                //is set in registry
                args.Add("add");
                args.Add(regPath);
                args.Add("/v");
                args.Add(target.Game == Mod.MEGame.ME3 ? "Install Dir" : "Path");
                args.Add("/t");
                args.Add("REG_SZ");
                args.Add("/d");
                args.Add(target.TargetPath);
                args.Add("/f");

                return Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true);
            }

            return -3;
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
                if (target != null && !Utilities.IsGameRunning(game))
                {
                    return File.Exists(Utilities.GetBinkw32File(target));
                }
            }

            return false;
        }

        private void ToggleBinkw32(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target == null) return; //can't toggle this
                if (Utilities.IsGameRunning(game))
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Cannot install the binkw32 DLC bypass while {Utilities.GetGameName(game)} is running.", "Game running", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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
                if (target != null)
                {
                    var configTool = Utilities.GetGameConfigToolPath(target);
                    Process.Start(configTool);
                }
            }
        }

        private bool CanRunGameConfigTool(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out Mod.MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    var configTool = Utilities.GetGameConfigToolPath(target);
                    return File.Exists(configTool);
                }
            }

            return false;
        }

        private void AddTarget()
        {
            Log.Information("User is adding new modding target");
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
                    //Test for cmmvanilla
                    if (File.Exists(Path.Combine(result, "cmmvanilla")))
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show("Unable to add this directory as a target because it has been marked as a backup (cmmvanilla file detected).", "Error adding target", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var pendingTarget = new GameTarget(gameSelected, result, false);
                    string failureReason = pendingTarget.ValidateTarget();

                    if (failureReason == null)
                    {
                        Analytics.TrackEvent("Attempted to add game target", new Dictionary<string, string>()
                        {
                            {"Game", pendingTarget.Game.ToString()},
                            {"Result", "Success"},
                            {"Supported", pendingTarget.Supported.ToString()}
                        });
                        Utilities.AddCachedTarget(pendingTarget);
                        PopulateTargets(pendingTarget);
                    }
                    else
                    {
                        Analytics.TrackEvent("Attempted to add game target", new Dictionary<string, string>()
                        {
                            {"Game", pendingTarget.Game.ToString()},
                            {"Result", "Failed, " + failureReason},
                            {"Supported", pendingTarget.Supported.ToString()}
                        });
                        Log.Error("Could not add target: " + failureReason);
                        Xceed.Wpf.Toolkit.MessageBox.Show("Unable to add this directory as a target:\n" + failureReason, "Error adding target", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!Utilities.IsGameRunning(mod.Game))
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
                    }
                    else
                    {
                        ReleaseBusyControl();
                        backgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                    }
                };
                ShowBusyControl(modInstaller);
            }
            else
            {
                Log.Error($"Blocking install of {mod.ModName} because {Utilities.GetGameName(mod.Game)} is running.");
                Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Cannot install mods while {Utilities.GetGameName(mod.Game)} is running.", "Cannot install mod", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadMods()
        {
            LoadMods();
        }

        private void CheckTargetPermissions(bool promptForConsent = true, bool showDialogEvenIfNone = false)
        {
            var targetsNeedingUpdate = InstallationTargets.Where(x => x.Selectable && !x.IsTargetWritable()).ToList();
            bool me1AGEIAKeyNotWritable = false;
            if (InstallationTargets.Any(x => x.Game == Mod.MEGame.ME1))
            {
                //Check AGEIA
                try
                {
                    var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\AGEIA Technologies", true);
                    if (key != null)
                    {
                        key.Close();
                    }
                    else
                    {
                        Log.Information("ME1 AGEIA Technologies key is not present or is not writable.");
                        me1AGEIAKeyNotWritable = true;
                    }
                }
                catch (SecurityException)
                {
                    Log.Information("ME1 AGEIA Technologies key is not writable.");
                    me1AGEIAKeyNotWritable = true;
                }
            }

            if (targetsNeedingUpdate.Count > 0 || me1AGEIAKeyNotWritable)
            {
                if (promptForConsent)
                {
                    Log.Information("Some game paths/keys are not writable. Prompting user.");
                    bool result = false;
                    Application.Current.Dispatcher.Invoke(delegate { result = Xceed.Wpf.Toolkit.MessageBox.Show(this, "Some game directories/registry keys are not writable by your user account. ME3Tweaks Mod Manager can grant write access to these directories for you for easier modding. Grant write access to these folders?", "Some targets/keys write-protected", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; });
                    if (result)
                    {
                        Analytics.TrackEvent("Granting write permissions", new Dictionary<string, string>() {{"Granted?", "Yes"}});
                        try
                        {
                            Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate, me1AGEIAKeyNotWritable);
                        }
                        catch (Exception e)
                        {
                            Log.Error("Error granting write permissions: " + App.FlattenException(e));
                        }
                    }
                    else
                    {
                        Log.Warning("User denied permission to grant write permissions");
                        Analytics.TrackEvent("Granting write permissions", new Dictionary<string, string>() {{"Granted?", "No"}});
                    }
                }
                else
                {
                    Analytics.TrackEvent("Granting write permissions", new Dictionary<string, string>() {{"Granted?", "Implicit"}});
                    Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate, me1AGEIAKeyNotWritable);
                }
            }
            else if (showDialogEvenIfNone)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show(this, "All game directory roots appear to be writable.", "Targets writable", MessageBoxButton.YesNo, MessageBoxImage.Information);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            if (App.BootingUpdate)
            {
                ShowUpdateCompletedPane();
            }

            if (!Settings.ShowedPreviewPanel)
            {
                ShowPreviewPanel();
            }

            LoadMods();
            PerformStartupNetworkFetches(true);
        }

        private void ShowPreviewPanel()
        {
            var archiveDeploymentPane = new PreviewWelcomePanel();
            archiveDeploymentPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(archiveDeploymentPane);
        }

        private void UpdateBinkStatus(Mod.MEGame game)
        {
            var target = GetCurrentTarget(game);
            if (target == null)
            {
                switch (game)
                {
                    case Mod.MEGame.ME1:
                        ME1ASILoaderInstalled = false;
                        ME1ASILoaderText = "Game not installed";
                        break;
                    case Mod.MEGame.ME2:
                        ME2ASILoaderInstalled = false;
                        ME2ASILoaderText = "Game not installed";
                        break;
                    case Mod.MEGame.ME3:
                        ME3ASILoaderInstalled = false;
                        ME3ASILoaderText = "Game not installed";
                        break;
                }

                return; //don't check or anything
            }

            switch (game)
            {
                case Mod.MEGame.ME1:
                    ME1ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(target);
                    ME1ASILoaderText = ME1ASILoaderInstalled ? binkME1InstalledText : binkME1NotInstalledText;
                    break;
                case Mod.MEGame.ME2:
                    ME2ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(target);
                    ME2ASILoaderText = ME2ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case Mod.MEGame.ME3:
                    ME3ASILoaderInstalled = Utilities.CheckIfBinkw32ASIIsInstalled(target);
                    ME3ASILoaderText = ME3ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
            }
        }

        /// <summary>
        /// Gets current target that matches the game. If selected target does not match, the first one in the list used (active). THIS CAN RETURN A NULL OBJECT!
        /// </summary>
        /// <param name="game">Game to find target for</param>
        /// <returns>Game matching target. If none is found, this return null.</returns>
        private GameTarget GetCurrentTarget(Mod.MEGame game)
        {
            if (SelectedGameTarget != null)
            {
                if (SelectedGameTarget.Game == game) return SelectedGameTarget;
            }

            return InstallationTargets.FirstOrDefault(x => x.Game == game);
        }

        public void LoadMods(Mod modToHighlight = null)
        {
            try
            {
                Utilities.EnsureModDirectories();
            }
            catch (Exception e)
            {
                Log.Error("Unable to ensure mod directories: " + e.Message);
                Crashes.TrackError(e);
                Xceed.Wpf.Toolkit.MessageBox.Show(this, "Unable to create mod library directory: " + e.Message + ".\nChoose a mod directory that you have write permissions to.", "Error creating mod library", MessageBoxButton.OK, MessageBoxImage.Error);
                var folderPicked = ChooseModLibraryPath(false);
                if (folderPicked)
                {
                    LoadMods();
                }
                else
                {
                    Log.Error("Unable to create mod library. Mod Manager will now exit.");
                    Xceed.Wpf.Toolkit.MessageBox.Show(this, "Unable to create mod library. Mod Manager will now close.");
                    Environment.Exit(1);
                }

                return;
            }

            IsLoadingMods = true;
            VisibleFilteredMods.ClearEx();
            AllLoadedMods.ClearEx();
            FailedMods.ClearEx();


            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModLoaderThread");
            bw.WorkerReportsProgress = true;
            bw.DoWork += (a, args) =>
            {
                bool canCheckForModUpdates = Utilities.CanFetchContentThrottleCheck(); //This is here as it will fire before other threads can set this value used in this session.
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
                            AllLoadedMods.Add(mod);
                            if (ME1ModsVisible && mod.Game == Mod.MEGame.ME1 || ME2ModsVisible && mod.Game == Mod.MEGame.ME2 || ME3ModsVisible && mod.Game == Mod.MEGame.ME3)
                            {
                                VisibleFilteredMods.Add(mod);
                                VisibleFilteredMods.Sort(x => x.ModName);
                            }
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
                    args.Result = VisibleFilteredMods.FirstOrDefault(x => x.ModPath == modToHighlight.ModPath);
                }

                UpdateBinkStatus(Mod.MEGame.ME1);
                UpdateBinkStatus(Mod.MEGame.ME2);
                UpdateBinkStatus(Mod.MEGame.ME3);
                backgroundTaskEngine.SubmitJobCompletion(uiTask);
                OnPropertyChanged(nameof(NoModSelectedText));

                //DEBUG ONLY - MOVE THIS SOMEWHERE ELSE IN FUTURE (or gate behind time check... or something... move to separate method)
                if (canCheckForModUpdates)
                {
                    CheckAllModsForUpdates();
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                IsLoadingMods = false;
                if (b.Result is Mod m)
                {
                    ModsList_ListBox.SelectedItem = m;
                    ModsList_ListBox.ScrollIntoView(m);

                }

                ModsLoaded = true;
            };
            bw.RunWorkerAsync();
        }

        private void CheckAllModsForUpdates()
        {
            var updatableMods = VisibleFilteredMods.Where(x => x.IsUpdatable).ToList();
            if (updatableMods.Count > 0)
            {
                CheckModsForUpdates(updatableMods);
            }
        }

        private void CheckModsForUpdates(List<Mod> updatableMods, bool restoreMode = false)
        {
            BackgroundTask bgTask = backgroundTaskEngine.SubmitBackgroundJob("ModCheckForUpdates", "Checking mods for updates", "Mod update check completed");
            var allModsInManifest = OnlineContent.CheckForModUpdates(updatableMods, restoreMode);
            if (allModsInManifest != null)
            {
                var updates = allModsInManifest.Where(x => x.applicableUpdates.Count > 0 || x.filesToDelete.Count > 0).ToList();
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
                        ShowBusyControl(modUpdatesNotificationDialog);
                    });
                }
            }
            else
            {
                bgTask.finishedUiText = "Error checking for mod updates";
            }

            backgroundTaskEngine.SubmitJobCompletion(bgTask);
        }

        private void PopulateTargets(GameTarget selectedTarget = null)
        {
            RepopulatingTargets = true;
            InstallationTargets.ClearEx();
            MEDirectories.ReloadGamePaths(); //this is redundant on the first boot but whatever.
            Log.Information("Populating game targets");

            if (ME3Directory.gamePath != null)
            {
                var target = new GameTarget(Mod.MEGame.ME3, ME3Directory.gamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    Log.Information("Current boot target for ME3: " + target.TargetPath);
                    InstallationTargets.Add(target);
                    Utilities.AddCachedTarget(target);
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
                    Utilities.AddCachedTarget(target);
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
                    Utilities.AddCachedTarget(target);
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
                int count = InstallationTargets.Count;
                InstallationTargets.AddRange(otherTargetsFileME1);
                InstallationTargets.AddRange(otherTargetsFileME2);
                InstallationTargets.AddRange(otherTargetsFileME3);
                var distinct = InstallationTargets.Distinct().ToList();
                InstallationTargets.ReplaceAll(distinct);
                if (InstallationTargets.Count > count)
                {
                    InstallationTargets.Insert(count, new GameTarget(Mod.MEGame.Unknown, "===================Other saved targets===================", false) {Selectable = false});

                }
            }

            if (selectedTarget != null)
            {
                //find new corresponding target
                var newTarget = InstallationTargets.FirstOrDefault(x => x.TargetPath == selectedTarget.TargetPath);
                if (newTarget != null)
                {
                    InstallationTargets_ComboBox.SelectedItem = newTarget;
                    SelectedGameTarget = InstallationTargets_ComboBox.SelectedItem as GameTarget;
                }
            }
            else
            {
                if (InstallationTargets.Count > 0)
                {
                    InstallationTargets_ComboBox.SelectedIndex = 0;
                    SelectedGameTarget = InstallationTargets_ComboBox.SelectedItem as GameTarget;
                }
            }

            RepopulatingTargets = false;
        }

        private void ModsList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedMod = (Mod) e.AddedItems[0];
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

        private void OpenModFolder_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                Utilities.OpenExplorer(SelectedMod.ModPath);
            }
        }

        private void OpenME3Tweaks_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage("https://me3tweaks.com/");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutPanel();
            aboutWindow.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(aboutWindow);
        }

        private void ModManagerWindow_Closing(object sender, CancelEventArgs e)
        {
            Settings.Save();
        }

        private void FailedMods_LinkClick(object sender, RequestNavigateEventArgs e)
        {
            var failedModsPanel = new FailedModsPanel(FailedMods.ToList());
            failedModsPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is Mod failedmod)
                {
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += (a, b) => { CheckModsForUpdates(new List<Mod>(new Mod[] {failedmod}), true); };
                    bw.RunWorkerAsync();
                }
            };
            ShowBusyControl(failedModsPanel);
        }

        private void OpenModsDirectory_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenExplorer(Utilities.GetModsDirectory());
        }

        private const int STARTUP_FAIL_CRITICAL_FILES_MISSING = 1;

        public void PerformStartupNetworkFetches(bool firstStartupCheck)
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

                if (firstStartupCheck)
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
                        //var coalesced = Path.Combine(InstallationTargets.FirstOrDefault(x => x.Game == Mod.MEGame.ME2).TargetPath, "BIOGame", "Config", "PC", "Cooked", "Coalesced.ini");
                        //if (File.Exists(coalesced))
                        //{
                        //    ME2Coalesced me2c = new ME2Coalesced(coalesced);
                        //    me2c.Serialize(@"C:\users\public\me2c.ini");
                        //}

                        var manifest = OnlineContent.FetchOnlineStartupManifest();
                        //#if DEBUG
                        //                    if (int.Parse(manifest["latest_build_number"]) > 0)
                        //#else
                        if (int.TryParse(manifest["latest_build_number"], out var latestServerBuildNumer))
                        {
                            if (latestServerBuildNumer > App.BuildNumber)

                                //#endif
                            {
                                Log.Information("Found update for Mod Manager: Build " + latestServerBuildNumer);

                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    var updateAvailableDialog = new ProgramUpdateNotification();
                                    updateAvailableDialog.Close += (sender, args) => { ReleaseBusyControl(); };
                                    ShowBusyControl(updateAvailableDialog);
                                });
                            }
#if !DEBUG
                            else if (latestServerBuildNumer == App.BuildNumber)
                            {
                                if (manifest.TryGetValue("build_md5", out var md5) && md5 != null)
                                {
                                    var localmd5 = Utilities.CalculateMD5(App.ExecutableLocation);
                                    if (localmd5 != md5)
                                    {
                                        //Update is available.
                                        {
                                            Log.Information("MD5 of local exe doesn't match server version, minor update detected.");
                                            Application.Current.Dispatcher.Invoke(delegate
                                            {
                                                var updateAvailableDialog = new ProgramUpdateNotification();
                                                updateAvailableDialog.UpdateMessage = $"An updated version of ME3Tweaks Mod Manager Build {App.BuildNumber} is available. Minor updates commonly fix small bugs in the program that do not merit a full re-release.";
                                                updateAvailableDialog.Close += (sender, args) => { ReleaseBusyControl(); };
                                                ShowBusyControl(updateAvailableDialog);
                                            });
                                        }
                                    }
                                }
                            }
#endif
                            else
                            {
                                Log.Information("Mod Manager is up to date");
                            }
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
                if (!success)
                {
                    Application.Current.Dispatcher.Invoke(delegate { Xceed.Wpf.Toolkit.MessageBox.Show(this, "Could not download static supporting files to ME3Tweaks Mod Manager. Mod Manager may be unstable due to these files not being present. Ensure you are able to connect to Github.com so these assets may be downloaded.", "Missing assets", MessageBoxButton.OK, MessageBoxImage.Error); });
                    bgTask.finishedUiText = "Failed to download static files";
                }

                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadDynamicHelp", "Loading dynamic help", "Loaded dynamic help");
                var helpItemsLoading = OnlineContent.FetchLatestHelp(!firstStartupCheck);
                bw.ReportProgress(0, helpItemsLoading);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);

                bgTask = backgroundTaskEngine.SubmitBackgroundJob("ThirdPartyIdentificationServiceFetch", "Loading Third Party Identification Service", "Loaded Third Party Identification Service");
                App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest(!firstStartupCheck);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);


                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadThirdPartyImportingService", "Loading Third Party Importing Service", "Loaded Third Party Importing Service");
                App.ThirdPartyImportingService = OnlineContent.FetchThirdPartyImportingService(!firstStartupCheck);
                backgroundTaskEngine.SubmitJobCompletion(bgTask);


                bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadTipsService", "Loading tips service", "Loaded tips service");
                LoadedTips.ReplaceAll(OnlineContent.FetchTipsService(!firstStartupCheck));
                OnPropertyChanged(nameof(NoModSelectedText));
                backgroundTaskEngine.SubmitJobCompletion(bgTask);
                if (firstStartupCheck)
                {
                    bgTask = backgroundTaskEngine.SubmitBackgroundJob("LoadObjectInfo", "Loading package information database", "Loaded package information database");

                    ME3UnrealObjectInfo.loadfromJSON();
                    ME2UnrealObjectInfo.loadfromJSON();
                    ME1UnrealObjectInfo.loadfromJSON();
                    backgroundTaskEngine.SubmitJobCompletion(bgTask);


                    bgTask = backgroundTaskEngine.SubmitBackgroundJob("WritePermissions", "Checking write permissions", "Checked user write permissions");
                    CheckTargetPermissions(true);
                    backgroundTaskEngine.SubmitJobCompletion(bgTask);
                }

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

                //byte[] bytes = File.ReadAllBytes(@"C:\Users\mgame\Source\Repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Releases\ME3TweaksModManagerExtractor_6.0.0.99.exe");
                //MemoryStream ms = new MemoryStream(bytes);
                //SevenZipExtractor sve = new SevenZipExtractor(ms);
                //sve.ExtractArchive(@"C:\users\public\documents");
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
                    m.Click += (o, eventArgs) => { new DynamicHelpItemModalWindow(item) {Owner = this}.ShowDialog(); };
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
            new StarterKitGeneratorWindow(g) {Owner = this}.ShowDialog();
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
                Analytics.TrackEvent("Launched external tool", new Dictionary<string, string>()
                {
                    {"Tool name", tool},
                    {"Arguments", arguments}
                });
                var exLauncher = new ExternalToolLauncher(tool, arguments);
                exLauncher.Close += (a, b) => { ReleaseBusyControl(); };
                //Todo: Update Busy UI Content
                ShowBusyControl(exLauncher);
            }
        }

        private void ASIModManager_Click(object sender, RoutedEventArgs e)
        {
            LaunchExternalTool(ExternalToolLauncher.ME3Explorer, ExternalToolLauncher.ME3EXP_ASIMANAGER);
        }

        private bool RepopulatingTargets;
        private MenuItem[] languageMenuItems;

        private void InstallationTargets_ComboBox_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!RepopulatingTargets)
            {
                var oldTarget = SelectedGameTarget;
                SelectedGameTarget = InstallationTargets_ComboBox.SelectedItem as GameTarget;
                if (!SelectedGameTarget.RegistryActive)
                {
                    try
                    {
                        var hresult = UpdateBootTarget(SelectedGameTarget);
                        if (hresult == -3) return; //do nothing.
                        if (hresult == 0)
                        {
                            //rescan
                            PopulateTargets(SelectedGameTarget);
                        }

                        Analytics.TrackEvent("Changed to non-active target", new Dictionary<string, string>()
                        {
                            {"New target", SelectedGameTarget.Game.ToString()},
                            {"ALOT Installed", SelectedGameTarget.ALOTInstalled.ToString()}
                        });
                    }
                    catch (Win32Exception ex)
                    {
                        Log.Warning("Win32 exception occured updating boot target. User maybe pressed no to the UAC dialog?: " + ex.Message);
                    }
                }
            }
        }

        private void UploadLog_Click(object sender, RoutedEventArgs e)
        {
            var logUploaderUI = new LogUploader();
            logUploaderUI.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(logUploaderUI);
        }

        public Visibility BusyProgressBarVisibility { get; set; } = Visibility.Visible;
        public ulong BusyProgressBarMaximum { get; set; } = 100;
        public ulong BusyProgressBarValue { get; set; } = 0;
        public bool BusyProgressBarIndeterminate { get; set; } = true;

        ///// <summary>
        ///// Updates the progressbar that the user controls use
        ///// </summary>
        ///// <param name="update"></param>
        //internal void UpdateBusyProgressBarCallback(ProgressBarUpdate update)
        //{
        //    switch (update.UpdateType)
        //    {
        //        case ProgressBarUpdate.UpdateTypes.SET_VISIBILITY:
        //            BusyProgressBarVisibility = update.GetDataAsVisibility();
        //            break;
        //        case ProgressBarUpdate.UpdateTypes.SET_MAX:
        //            BusyProgressBarMaximum = update.GetDataAsULong();
        //            break;
        //        case ProgressBarUpdate.UpdateTypes.SET_VALUE:
        //            BusyProgressBarValue = update.GetDataAsULong();
        //            break;
        //        case ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE:
        //            BusyProgressBarIndeterminate = update.GetDataAsBool();
        //            break;

        //    }
        //}

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                switch (ext)
                {
                    case ".rar":
                    case ".7z":
                    case ".zip":
                    case ".exe":
                        Analytics.TrackEvent("User opened mod archive for import", new Dictionary<string, string> {{"Method", "Drag & drop"}, {"Filename", Path.GetFileName(files[0])}});
                        openModImportUI(files[0]);
                        break;
                    //TPF, .mod, .mem
                    case ".tpf":
                    case ".mod":
                    case ".mem":
                        Analytics.TrackEvent("User redirected to MEM/ALOT Installer", new Dictionary<string, string> {{"Filename", Path.GetFileName(files[0])}});
                        Xceed.Wpf.Toolkit.MessageBox.Show(this, $"{ext} files can be installed with ALOT Installer or Mass Effect Modder (MEM), both available in the tools menu.\n\nWARNING: These types of mods change game file pointers. They must be installed AFTER all other DLC/content mods. Installing content/DLC mods after will cause various issues in the game. Once these types of mods are installed, ME3Tweaks Mod Manager will refuse to install further mods without a restore of the game.", "Non-Mod Manager mod found", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                }
            }
        }

        private void openModImportUI(string archiveFile)
        {
            Log.Information("Opening Mod Archive Importer for file " + archiveFile);
            var modInspector = new ModArchiveImporter(archiveFile);
            modInspector.Close += (a, b) =>
            {

                if (b.Data is List<Mod> modsImported)
                {
                    ReleaseBusyControl();
                    LoadMods(modsImported.Count == 1 ? modsImported.First() : null);
                }
                else if (b.Data is Mod compressedModToInstall)
                {
                    ReleaseBusyControl();
                    var installTarget = InstallationTargets.FirstOrDefault(x => x.RegistryActive && x.Game == compressedModToInstall.Game);
                    if (installTarget != null)
                    {
                        InstallationTargets_ComboBox.SelectedItem = installTarget;
                        ApplyMod(compressedModToInstall);
                    }
                    else
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Cannot install mod: {compressedModToInstall.Game} is not installed.", "Game not installed", MessageBoxButton.OK, MessageBoxImage.Error);
                        ReleaseBusyControl();
                    }
                }
                else
                {
                    ReleaseBusyControl();
                }
            };
            ShowBusyControl(modInspector); //todo: Set progress bar params
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (!SupportedDroppableExtensions.Contains(ext))
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void RunAutoTOCOnTarget()
        {
            var target = GetCurrentTarget(Mod.MEGame.ME3);
            if (target != null)
            {
                var task = backgroundTaskEngine.SubmitBackgroundJob("AutoTOC", "Running AutoTOC", "Ran AutoTOC");
                var autoTocUI = new AutoTOC(target);
                autoTocUI.Close += (a, b) =>
                {
                    backgroundTaskEngine.SubmitJobCompletion(task);
                    ReleaseBusyControl();
                };
                ShowBusyControl(autoTocUI);
            }
            else
            {
                Log.Error("AutoTOC game target was null! This shouldn't be possible");
            }
        }

        private void ChangeSetting_Clicked(object sender, RoutedEventArgs e)
        {
            var callingMember = (MenuItem) sender;
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
                ChooseModLibraryPath(true);
            }
            else if (callingMember == DarkMode_MenuItem)
            {
                SetTheme();
            }
            else if (callingMember == EnableTelemetry_MenuItem && !Settings.EnableTelemetry)
            {
                //user trying to turn it off 
                var result = Xceed.Wpf.Toolkit.MessageBox.Show(this, "Disabling telemetry makes it more difficult to fix bugs and develop new features for ME3Tweaks Mod Manager. " +
                                                                     "Telemetry data collected is used to identify crashes, errors, see what demand there is for various mod support, and see what tasks are difficult for users (such as lack of guidance on a particular enforced rule of modding).\n\n" +
                                                                     "Telemetry data is deleted automatically after 90 days. You can continue to turn it off, but it provides valuable metrics to the developers of this program.\n\nTurn off telemetry?", "Turning off telemetry", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.EnableTelemetry = true; //keep on.
                    return;
                }

                Log.Warning("Turning off telemetry :(");
                //Turn off telemetry.
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
            }
            else if (callingMember == EnableTelemetry_MenuItem)
            {
                //turning telemetry on
                Log.Information("Turning on telemetry :)");
                Analytics.SetEnabledAsync(true);
                Crashes.SetEnabledAsync(true);
            }
            else
            {
                //unknown caller
                return;
            }

            Settings.Save();
        }

        private void SetTheme()
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, Settings.DarkTheme ? ResourceLocator.DarkColorScheme : ResourceLocator.LightColorScheme);
        }

        private bool ChooseModLibraryPath(bool loadModsAfterSelecting)
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
                if (loadModsAfterSelecting)
                {
                    LoadMods();
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage("https://me3tweaks.com/modmanager/documentation/moddesc");
        }

        private void OpenMemoryAnalyzer_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            new MemoryAnalyzer().Show();
#endif
        }

        private void ToggleME3Visibility_Click(object sender, RoutedEventArgs e)
        {
            ME3ModsVisible = !ME3ModsVisible;
            FilterMods();
        }

        private void ToggleME2Visibility_Click(object sender, RoutedEventArgs e)
        {
            ME2ModsVisible = !ME2ModsVisible;
            FilterMods();
        }

        private void ToggleME1Visibility_Click(object sender, RoutedEventArgs e)
        {
            ME1ModsVisible = !ME1ModsVisible;
            FilterMods();
        }

        private void FilterMods()
        {
            var allMods = AllLoadedMods.ToList();
            if (!ME1ModsVisible)
                allMods.RemoveAll(x => x.Game == Mod.MEGame.ME1);
            if (!ME2ModsVisible)
                allMods.RemoveAll(x => x.Game == Mod.MEGame.ME2);
            if (!ME3ModsVisible)
                allMods.RemoveAll(x => x.Game == Mod.MEGame.ME3);

            VisibleFilteredMods.ReplaceAll(allMods);
            VisibleFilteredMods.Sort(x => x.ModName);
        }

        private void ChangeLanguage_Clicked(object sender, RoutedEventArgs e)
        {
            string lang = "int";
            MenuItem clicked = sender as MenuItem;
            if (sender == LanguageINT_MenuItem)
            {
                lang = "int";
            }
            else if (sender == LanguagePOL_MenuItem)
            {
                lang = "pol";
            }
            else if (sender == LanguageRUS_MenuItem)
            {
                lang = "rus";
            }

            foreach (var item in languageMenuItems)
            {
                item.IsChecked = item == clicked;
            }

            //Set language.
            var resourceDictionary = new ResourceDictionary
            {
                // Pick uri from configuration
                Source = new Uri("pack://application:,,,/ME3TweaksModManager;component/modmanager/localizations/" + lang + ".xaml", UriKind.Absolute)
            };

            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
        }
    }
}