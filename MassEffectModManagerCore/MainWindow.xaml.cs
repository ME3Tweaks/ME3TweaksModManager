using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using AdonisUI;
using CommandLine;
using FontAwesome5;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.online;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.merge.dlc;
using ME3TweaksModManager.modmanager.merge.game2email;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using ME3TweaksModManager.modmanager.objects.tlk;
using ME3TweaksModManager.modmanager.squadmates;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using Pathoschild.FluentNexus.Models;
using Extensions = WinCopies.Util.Extensions;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;
using Mod = ME3TweaksModManager.modmanager.objects.mod.Mod;
using XCopy = ME3TweaksCore.Helpers.XCopy;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string CurrentOperationText { get; set; } = M3L.GetString(M3L.string_startingUp);

        public bool IsBusy { get; set; }

        /// <summary>
        /// Content of the current Busy Indicator modal
        /// </summary>
        public object BusyContentM3 { get; set; }

#if DEBUG

        public void OnBusyContentM3Changed(object old, object newB)
        {
            if (newB is SingleItemPanel2 sip2)
            {
                Debug.WriteLine($@"Changing busy panels to {sip2.Content}");
            }
        }

#endif

        public string CurrentDescriptionText { get; set; } = DefaultDescriptionText;
        private static readonly string DefaultDescriptionText = M3L.GetString(M3L.string_selectModOnLeftToGetStarted);
        private readonly string[] SupportedDroppableExtensions =
        {
            @".rar", @".zip", @".7z", @".exe", @".tpf", @".mod", @".mem", @".me2mod", @".xml", @".bin", @".tlk", @".par", @".m3m", @".json", @".extractedbin", @".m3za"
        };
        public string ApplyModButtonText { get; set; } = M3L.GetString(M3L.string_applyMod);
        public string InstallationTargetText { get; set; } = M3L.GetString(M3L.string_installationTarget);

        public bool ME1ASILoaderInstalled { get; set; }
        public bool ME2ASILoaderInstalled { get; set; }
        public bool ME3ASILoaderInstalled { get; set; }
        public bool LE1ASILoaderInstalled { get; set; }
        public bool LE2ASILoaderInstalled { get; set; }
        public bool LE3ASILoaderInstalled { get; set; }

        public bool ME1NexusEndorsed { get; set; }
        public bool ME2NexusEndorsed { get; set; }
        public bool ME3NexusEndorsed { get; set; }
        public bool LENexusEndorsed { get; set; }

        public string VisitWebsiteText { get; set; }
        public string ME1ASILoaderText { get; set; }
        public string ME2ASILoaderText { get; set; }
        public string ME3ASILoaderText { get; set; }
        public string LE1ASILoaderText { get; set; }
        public string LE2ASILoaderText { get; set; }
        public string LE3ASILoaderText { get; set; }

        /// <summary>
        /// Suppresses the logic of FilterMods(), used to prevent multiple invocations on global changes
        /// </summary>
        private bool SuppressFilterMods;

        /// <summary>
        /// Used to prevent duplicate opening/closing animations for the 'visit mod web site' panel. True = fully open, False = fully closed
        /// </summary>
        private bool WebsitePanelStatus;

        /// <summary>
        /// Single-instance argument handling
        /// </summary>
        /// <param name="args"></param>
        internal void HandleInstanceArguments(string[] args)
        {
            // Fix pass through in debug mode which uses a .dll arg
            if (args.Any() && args[0].EndsWith(@".dll"))
            {
                args = args.Skip(1).Take(args.Length - 1).ToArray();
            }

            var result = Parser.Default.ParseArguments<Options>(args);
            if (result is Parsed<Options> parsedCommandLineArgs)
            {
                if (parsedCommandLineArgs.Value.RelevantGame != null)
                    CommandLinePending.PendingGame = parsedCommandLineArgs.Value.RelevantGame.Value;
                if (parsedCommandLineArgs.Value.NXMLink != null)
                    CommandLinePending.PendingNXMLink = parsedCommandLineArgs.Value.NXMLink;
                if (parsedCommandLineArgs.Value.AutoInstallModdescPath != null)
                    CommandLinePending.PendingAutoModInstallPath = parsedCommandLineArgs.Value.AutoInstallModdescPath;
                if (parsedCommandLineArgs.Value.GameBoot)
                    CommandLinePending.PendingGameBoot = parsedCommandLineArgs.Value.GameBoot;
                if (parsedCommandLineArgs.Value.AutoInstallASIGroupID > 0)
                    CommandLinePending.PendingInstallASIID = parsedCommandLineArgs.Value.AutoInstallASIGroupID;
                if (parsedCommandLineArgs.Value.AutoInstallBink != false)
                    CommandLinePending.PendingInstallBink = parsedCommandLineArgs.Value.AutoInstallBink;
                handleInitialPending();
            }
        }

        private void showNXMDownloader(string nxmLink)
        {
            var npl = NexusProtocolLink.Parse(nxmLink);
            if (npl == null) return;

            if (NexusDomainHandler.HandleExternalLink(npl))
            {
                return; // Handled by external handler.
            }

            if (NexusModsUtilities.UserInfo == null)
            {
                // Not logged in
                Activate(); //bring to front
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_nexusLoginRequiredForDownload), M3L.GetString(M3L.string_notSignedIn), MessageBoxButton.OK, MessageBoxImage.Error);
                ShowNexusPanel();
                return;
            }

            var mDownloader = new NexusModDownloader(nxmLink);
            mDownloader.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is List<ModDownload> items)
                {
                    foreach (var ii in items)
                    {
                        ii.DownloadedStream.Position = 0;
                        if (ii.DownloadedStream is FileStream fs)
                        {
                            // Open the file instead
                            fs.Dispose(); // Ensure it's closed
                            openModImportUI(fs.Name, priority: true); // Open the archive itself
                        }
                        else
                        {
                            openModImportUI(ii.ModFile.FileName, ii.DownloadedStream, true);
                        }
                    }
                }
            };
            ShowBusyControl(mDownloader, ShouldShowNXMDownloadImmediately());
        }

        /// <summary>
        /// When an NXM link is fetched, should the nxm panel take priority?
        /// </summary>
        /// <returns></returns>
        private bool ShouldShowNXMDownloadImmediately()
        {

            if (BusyContentM3 is SingleItemPanel2 sip2)
            {
                if (sip2.Content is ModUpdateInformationPanel muip)
                {
                    muip.RefreshContentsOnDisplay();
                    return true;
                }
            }

            return false;

        }

        public string EndorseM3String { get; set; } = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);

        private int oldFailedBindableCount = 0;

        public string NoModSelectedText
        {
            get
            {
                var retvar = M3L.GetString(M3L.string_selectModOnLeftToGetStarted);
                var localizedTip = TipsService.GetTip(App.CurrentLanguage);
                if (localizedTip != null)
                {
                    retvar += $"\n\n---------------------------------------------\n{localizedTip}"; //do not localize
                }

                return retvar;
            }
        }

        public string FailedModsString { get; set; }
        public string NexusLoginInfoString { get; set; } // BLANK TO START = M3L.GetString(M3L.string_loginToNexusMods);

        /// <summary>
        /// The current coalesce-d panel result that is pending handling
        /// </summary>
        private PanelResult BatchPanelResult;
        /// <summary>
        /// If the next call to HandlePanelResult() should process BatchPanelResult
        /// </summary>
        internal bool HandleBatchPanelResult;

        /// <summary>
        /// User controls that are queued for displaying when the previous one has closed.
        /// </summary>
        private ConcurrentQueue<MMBusyPanelBase> queuedUserControls = new ConcurrentQueue<MMBusyPanelBase>();

        /// <summary>
        /// The backend libraries and game targets have initially loaded
        /// </summary>
        public bool StartedUp { get; set; }

        /// <summary>
        /// The currently selected mod
        /// </summary>
        public Mod SelectedMod { get; set; }

        public ObservableCollectionExtended<GameTargetWPF> InstallationTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        /// <summary>
        /// List of all loaded targets, even ones for different generations
        /// </summary>
        private List<GameTargetWPF> InternalLoadedTargets { get; } = new();

        //private ModLoader modLoadSer;
        public MainWindow()
        {
            if (CommandLinePending.UpgradingFromME3CMM/* || true*/)
            {
                App.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                //Show migration window before we load the main UI
                M3Log.Information(@"Migrating from ME3CMM - showing migration dialog");
                new ME3CMMMigrationWindow().ShowDialog();
                App.Current.MainWindow = this;
                App.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            InitializeSingletons();
            LoadCommands();
            SetTheme();
            InitializeComponent();


            //Change language if not INT
            if (App.InitialLanguage != @"int")
            {
                // Sync version
                SetApplicationLanguage(App.InitialLanguage, true);
            }

            // Setup game filters
            var enabledFilters = Enum.GetValues<MEGame>();
            if (!string.IsNullOrWhiteSpace(Settings.SelectedFilters))
            {
                var nEnabledGames = new List<MEGame>();
                var split = Settings.SelectedFilters.Split(',');
                foreach (var s in split)
                {
                    if (Enum.TryParse<MEGame>(s, out var parsedGame))
                    {
                        nEnabledGames.Add(parsedGame);
                    }
                }

                if (nEnabledGames.Any())
                    enabledFilters = nEnabledGames.ToArray();
            }

            foreach (var g in Enum.GetValues<MEGame>())
            {
                if (g is MEGame.UDK or MEGame.Unknown)
                    continue;
                var gf = new GameFilterLoader(g);
                if (enabledFilters.Any() && !enabledFilters.Contains(g))
                {
                    gf.IsEnabled = false;
                }

                Settings.StaticPropertyChanged += gf.NotifyGenerationChanged; // Notify of generation change
                gf.PropertyChanged += ModGameVisibilityChanged;
                M3LoadedMods.Instance.GameFilters.Add(gf);
            }

            // Loa

            CheckProgramDataWritable();
            AttachListeners();
            //Must be done after UI has initialized
            //if (InstallationTargets.Count > 0)
            //{
            //    SelectedGameTarget = InstallationTargets[0];
            //}
        }

        private void InitializeSingletons()
        {
            // TASK ENGINE
            Storyboard openLoadingSpinner = null, closeLoadingSpinner = null;
            BackgroundTaskEngine.InitializeTaskEngine(updateText =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentOperationText = updateText;
                });
            },
                () =>
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        if (openLoadingSpinner == null)
                        {
                            openLoadingSpinner = FindResource(@"OpenLoadingSpinner") as Storyboard;
                        }

                        Storyboard.SetTarget(openLoadingSpinner, LoadingSpinner_Image);
                        openLoadingSpinner.Begin();
                    });
                },
                () =>
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        if (closeLoadingSpinner == null)
                        {
                            closeLoadingSpinner = FindResource(@"CloseLoadingSpinner") as Storyboard;
                        }

                        Storyboard.SetTarget(closeLoadingSpinner, LoadingSpinner_Image);
                        closeLoadingSpinner.Begin();
                    });
                }
            );

            // MOD LIST
            M3LoadedMods.InitializeModLoader(this, x =>
            {
                if (x != null && MEGameSelector.IsGenerationEnabledGame(x.Game))
                {
                    var matchingFilter = M3LoadedMods.Instance.GameFilters.FirstOrDefault(y => y.Game == x.Game);
                    if (matchingFilter != null)
                    {
                        // Turn on the filter.
                        matchingFilter.IsEnabled = true;
                    }
                }
                SelectedMod = x;
                ModsList_ListBox.ScrollIntoView(x);
            });

            // MOD UPDATER
            ModUpdater.InitializeModUpdater(this);
        }

        private void ModGameVisibilityChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameFilter.IsEnabled))
            {
                Settings.SelectedFilters = string.Join(',', M3LoadedMods.Instance.GameFilters.Where(x => x.IsEnabled).Select(x => x.Game));
                M3LoadedMods.Instance.FilterMods();
            }
        }

        private void CheckProgramDataWritable()
        {
            M3Log.Information(@"Checking settings.ini is writable (ProgramData check)...");
            var settingsResult = Settings.SaveTest();
            if (settingsResult == Settings.SettingsSaveResult.FAILED_UNAUTHORIZED)
            {
                M3Log.Error(@"No permissions to appdata! Prompting for user to grant consent");
                var result = M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_multiUserProgramDataWindowsRestrictions), M3L.GetString(M3L.string_grantingWritePermissions), MessageBoxButton.OKCancel, MessageBoxImage.Error);
                if (result == MessageBoxResult.OK)
                {
                    bool done = M3Utilities.CreateDirectoryWithWritePermission(M3Filesystem.GetAppDataFolder(), true);
                    if (done)
                    {
                        M3Log.Information(@"Granted permissions to ProgramData");
                    }
                    else
                    {
                        M3Log.Error(@"User declined consenting permissions to ProgramData!");
                        M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_programWillNotRunCorrectly), M3L.GetString(M3L.string_programDataAccessDenied), MessageBoxButton.OK, MessageBoxImage.Error);

                    }
                }
                else
                {
                    M3Log.Error(@"User denied granting permissions!");
                    M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_programWillNotRunCorrectly), M3L.GetString(M3L.string_programDataAccessDenied), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                M3Log.Information(@"settings.ini is writable");
            }
        }

        /// <summary>
        /// Updates the Nexus Login status
        /// </summary>
        /// <param name="languageUpdateOnly">If we should only update the language text instead of a full update of API keys</param>
        public async void RefreshNexusStatus(bool languageUpdateOnly = false)
        {
            if (NexusModsUtilities.HasAPIKey)
            {
                if (!languageUpdateOnly)
                {
                    var loggedIn = await AuthToNexusMods();
                    if (loggedIn == null)
                    {
                        M3Log.Error(@"Error authorizing to NexusMods, did not get response from server or issue occurred while checking credentials. Setting not authorized");
                        SetNexusNotAuthorizedUI();
                    }
                }

                if (NexusModsUtilities.UserInfo != null)
                {
                    NexusLoginInfoString = NexusModsUtilities.UserInfo.Name;
                    //M3L.GetString(M3L.string_nexusLoggedIn);
                }

                //prevent reseting ui to not authorized
                return;
            }

            SetNexusNotAuthorizedUI();
        }

        private void SetNexusNotAuthorizedUI()
        {
            NexusLoginInfoString = M3L.GetString(M3L.string_loginToNexusMods);
            ME1NexusEndorsed = ME2NexusEndorsed = ME3NexusEndorsed = LENexusEndorsed = false;
            EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
        }

        private async Task<User> AuthToNexusMods(bool languageUpdateOnly = false)
        {
            if (languageUpdateOnly)
            {
                if (NexusModsUtilities.UserInfo != null)
                {
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed || LENexusEndorsed) ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods) : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                }
                else
                {
                    EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                }
                return null;
            }

            M3Log.Information(@"Authenticating to NexusMods...");
            var userInfo = await NexusModsUtilities.AuthToNexusMods();
            if (userInfo != null)
            {
                M3Log.Information(@"Authenticated to NexusMods");

                //ME1
                var me1Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect", 149);
                ME1NexusEndorsed = me1Status ?? false;

                //ME2
                var me2Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect2", 248);
                ME2NexusEndorsed = me2Status ?? false;

                //ME3
                var me3Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect3", 373);
                ME3NexusEndorsed = me3Status ?? false;

                //LE
                var leStatus = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffectlegendaryedition", 2);
                LENexusEndorsed = leStatus ?? false;

                EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed || LENexusEndorsed) ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods) : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
            }
            else
            {
                M3Log.Information(@"Did not authenticate to NexusMods. May not be logged in or there was network issue");
                EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
            }

            return userInfo;
        }

        /// <summary>
        /// Sets up listeners for the 'mod failed to load' text, public property changed listeners
        /// </summary>
        private void AttachListeners()
        {
            // Failed mods listener
            M3LoadedMods.Instance.FailedMods.PublicPropertyChanged += (a, b) =>
            {
                if (b.PropertyName == @"BindableCount")
                {
                    bool isopening = M3LoadedMods.Instance.FailedMods.BindableCount > 0 && oldFailedBindableCount == 0;
                    bool isclosing = M3LoadedMods.Instance.FailedMods.BindableCount == 0 && oldFailedBindableCount > 0;
                    if (M3LoadedMods.Instance.FailedMods.BindableCount > 0)
                    {
                        FailedModsString = M3L.GetString(M3L.string_interp_XmodsFailedToLoad, M3LoadedMods.Instance.FailedMods.BindableCount.ToString());
                    }
                    else
                    {
                        FailedModsString = @"";
                    }

                    if (isclosing || isopening)
                    {
                        Debug.WriteLine($@"FailedMods: {isopening}");
                        ClipperHelper.ShowHideVerticalContent(FailedModsPopupPanel, isopening);
                    }

                    oldFailedBindableCount = M3LoadedMods.Instance.FailedMods.BindableCount;
                }
            };

            // Setting changed listener.
            Settings.StaticPropertyChanged += SettingChanged;
        }

        private void SettingChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.GenerationSettingOT))
                OrderAndSetTargets(InternalLoadedTargets, SelectedGameTarget);
            else if (e.PropertyName == nameof(Settings.GenerationSettingLE))
                OrderAndSetTargets(InternalLoadedTargets, SelectedGameTarget);
            else if (e.PropertyName == nameof(Settings.OneTimeMessage_ModListIsNotListOfInstalledMods))
                ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods, Settings.OneTimeMessage_ModListIsNotListOfInstalledMods);
            else if (e.PropertyName == nameof(Settings.OneTimeMessage_ModListIsNotListOfInstalledMods))
                ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods, Settings.OneTimeMessage_ModListIsNotListOfInstalledMods);

        }

        public ICommand OpenASIManagerCommand { get; set; }
        public ICommand OpenTutorialCommand { get; set; }
        public ICommand OriginInGameOverlayDisablerCommand { get; set; }
        public ICommand ModdescEditorCommand { get; set; }
        public ICommand LaunchEGMSettingsCommand { get; set; }
        public ICommand LaunchEGMSettingsLECommand { get; set; }
        public ICommand LaunchFVBCCUCommand { get; set; }
        public ICommand OfficialDLCTogglerCommand { get; set; }
        public ICommand ImportArchiveCommand { get; set; }
        public ICommand ReloadModsCommand { get; set; }
        public ICommand ModManagerOptionsCommand { get; set; }
        public ICommand ConflictDetectorCommand { get; set; }
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
        public ICommand SyncPlotManagerCommand { get; set; }
        public RelayCommand CompileCoalescedCommand { get; set; }
        public RelayCommand DecompileCoalescedCommand { get; set; }
        public ICommand AutoTOCLECommand { get; set; }
        public ICommand ConsoleKeyKeybinderCommand { get; set; }
        public ICommand LoginToNexusCommand { get; set; }
        public GenericCommand EndorseSelectedModCommand { get; set; }
        public ICommand CreateTestArchiveCommand { get; set; }
        public ICommand LaunchIniModderCommand { get; set; }
        public ICommand EndorseM3OnNexusCommand { get; set; }
        public ICommand DownloadModMakerModCommand { get; set; }
        public ICommand UpdaterServiceCommand { get; set; }
        public ICommand UpdaterServiceSettingsCommand { get; set; }
        public ICommand MixinLibraryCommand { get; set; }
        public ICommand BatchModInstallerCommand { get; set; }
        public ICommand ImportDLCModFromGameCommand { get; set; }
        public ICommand BackupFileFetcherCommand { get; set; }
        public ICommand OpenModDescCommand { get; set; }
        public ICommand CheckAllModsForUpdatesCommand { get; set; }
        public ICommand CustomKeybindsInjectorCommand { get; set; }
        public ICommand NexusModsFileSearchCommand { get; set; }
        public ICommand SearchModsCommand { get; set; }
        public ICommand CloseModSearchBoxCommand { get; set; }
        private void LoadCommands()
        {
            CloseModSearchBoxCommand = new GenericCommand(CloseSearchBox);
            SearchModsCommand = new GenericCommand(ShowSearchBox);
            ModManagerOptionsCommand = new GenericCommand(ShowOptions);
            ReloadModsCommand = new GenericCommand(ReloadMods, CanReloadMods);
            ApplyModCommand = new GenericCommand(CallApplyMod, CanApplyMod);
            CheckForContentUpdatesCommand = new GenericCommand(CheckForContentUpdates, NetworkThreadNotRunning);
            AddTargetCommand = new GenericCommand(AddTarget, () => !RepopulatingTargets);
            RunGameConfigToolCommand = new RelayCommand(RunGameConfigTool, CanRunGameConfigTool);
            Binkw32Command = new RelayCommand(ToggleBinkw32, CanToggleBinkw32);
            StartGameCommand = new GenericCommand(StartGame, CanStartGame);
            ShowinstallationInformationCommand = new GenericCommand(ShowInstallInfo, CanShowInstallInfo);
            BackupCommand = new GenericCommand(ShowBackupPane, ContentCheckNotInProgress);
            RestoreCommand = new GenericCommand(ShowRestorePane, ContentCheckNotInProgress);
            DeployModCommand = new GenericCommand(ShowDeploymentPane, CanShowDeploymentPane);
            DeleteModFromLibraryCommand = new GenericCommand(DeleteModFromLibraryWrapper, CanDeleteModFromLibrary);
            ImportArchiveCommand = new GenericCommand(OpenArchiveSelectionDialog, CanOpenArchiveSelectionDialog);
            SubmitTelemetryForModCommand = new GenericCommand(SubmitTelemetryForMod, CanSubmitTelemetryForMod);
            SelectedModCheckForUpdatesCommand = new GenericCommand(CheckSelectedModForUpdate, SelectedModIsME3TweaksUpdatable);
            RestoreModFromME3TweaksCommand = new GenericCommand(RestoreSelectedMod, SelectedModIsME3TweaksUpdatable);
            GrantWriteAccessCommand = new GenericCommand(() => CheckTargetPermissions(true, true), HasAtLeastOneTarget);
            AutoTOCCommand = new RelayCommand(RunAutoTOCOnGame, HasGameTarget);
            SyncPlotManagerCommand = new RelayCommand(SyncPlotManagerForGame, HasGameTarget);
            ConsoleKeyKeybinderCommand = new GenericCommand(OpenConsoleKeyKeybinder, CanOpenConsoleKeyKeybinder);
            LoginToNexusCommand = new GenericCommand(ShowNexusPanel, CanShowNexusPanel);
            EndorseSelectedModCommand = new GenericCommand(EndorseWrapper, CanEndorseMod);
            CreateTestArchiveCommand = new GenericCommand(CreateTestArchive, CanCreateTestArchive);
            LaunchIniModderCommand = new GenericCommand(OpenMEIM, CanOpenMEIM);
            EndorseM3OnNexusCommand = new GenericCommand(EndorseM3, CanEndorseM3);
            DownloadModMakerModCommand = new GenericCommand(OpenModMakerPanel, CanOpenModMakerPanel);
            UpdaterServiceCommand = new GenericCommand(OpenUpdaterServicePanel, CanOpenUpdaterServicePanel);
            UpdaterServiceSettingsCommand = new GenericCommand(OpenUpdaterServicePanelEditorMode);
            MixinLibraryCommand = new GenericCommand(OpenMixinManagerPanel, CanOpenMixinManagerPanel);
            BatchModInstallerCommand = new GenericCommand(OpenBatchModPanel, CanOpenBatchModPanel);
            ImportDLCModFromGameCommand = new GenericCommand(OpenImportFromGameUI, CanOpenImportFromUI);
            BackupFileFetcherCommand = new GenericCommand(OpenBackupFileFetcher);
            ConflictDetectorCommand = new GenericCommand(OpenConflictDetector);
            OfficialDLCTogglerCommand = new GenericCommand(OpenOfficialDLCToggler);
            LaunchEGMSettingsCommand = new GenericCommand(() => LaunchEGMSettings(), CanLaunchEGMSettings);
            LaunchEGMSettingsLECommand = new GenericCommand(() => LaunchEGMSettingsLE(), CanLaunchEGMSettingsLE);
            LaunchFVBCCUCommand = new GenericCommand(() => LaunchFVBCCU(), CanLaunchFVBCCU);
            OpenModDescCommand = new GenericCommand(OpenModDesc);
            CheckAllModsForUpdatesCommand = new GenericCommand(CheckAllModsForUpdatesWrapper, () => M3LoadedMods.Instance.ModsLoaded);
            CustomKeybindsInjectorCommand = new GenericCommand(OpenKeybindsInjector, () => M3LoadedMods.Instance.ModsLoaded && InstallationTargets.Any(x => x.Game == MEGame.ME3));
            ModdescEditorCommand = new GenericCommand(OpenModDescEditor, CanOpenModdescEditor);
            OriginInGameOverlayDisablerCommand = new GenericCommand(OpenOIGDisabler, () => M3LoadedMods.Instance.ModsLoaded && InstallationTargets.Any());
            OpenTutorialCommand = new GenericCommand(OpenTutorial, () => TutorialService.ServiceLoaded);
            OpenASIManagerCommand = new GenericCommand(OpenASIManager, NetworkThreadNotRunning);
            NexusModsFileSearchCommand = new GenericCommand(OpenNexusSearch); // no conditions for this
            CompileCoalescedCommand = new RelayCommand(CompileCoalesced); // no conditions for this
            DecompileCoalescedCommand = new RelayCommand(DecompileCoalesced); // no conditions for this

        }

        private void DecompileCoalesced(object obj)
        {
            if (obj is bool game3)
            {
                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"UserCoalDecompile", M3L.GetString(M3L.string_decompilingCoalescedFile), M3L.GetString(M3L.string_decompiledCoalescedFile));
                OpenFileDialog ofd = new OpenFileDialog()
                {
                    Title = M3L.GetString(M3L.string_selectCoalescedFile),
                    Filter = @"Coalesced file|*.bin",
                };
                if (game3)
                {
                    ofd.Title += @" (ME3/LE3)";
                }
                else
                {
                    ofd.Title += @" (LE1/LE2)";
                }
                var result = ofd.ShowDialog(this);
                if (result.HasValue && result.Value)
                {
                    Task.Run(() =>
                    {
                        var dir = Directory.GetParent(ofd.FileName).FullName;
                        if (game3)
                        {
                            // Game 3
                            CoalescedConverter.ConvertToXML(ofd.FileName, dir);
                        }
                        else
                        {
                            // LE1/LE2
                            LECoalescedConverter.Unpack(ofd.FileName, dir);
                        }

                        TelemetryInterposer.TrackEvent(@"Decompiled Coalesced (menu)");
                        M3Utilities.HighlightInExplorer(dir);
                        BackgroundTaskEngine.SubmitJobCompletion(task);
                    }).ContinueWithOnUIThread(x =>
                    {
                        if (x.Exception != null)
                        {
                            M3Log.Exception(x.Exception, @"Error decompiling coalesced file:");
                            task.FinishedUIText = M3L.GetString(M3L.string_errorDecompilingCoalescedFile);
                            BackgroundTaskEngine.SubmitJobCompletion(task);
                            M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorDecompilingCoalescedFile, x.Exception.Message), M3L.GetString(M3L.string_errorDecompiling), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });

                }
                else
                {
                    task.FinishedUIText = M3L.GetString(M3L.string_abortedDecompilingCoalescedFile);
                }
            }
        }

        private void CompileCoalesced(object obj)
        {
            if (obj is bool game3)
            {
                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"UserCoalCompile", M3L.GetString(M3L.string_compilingCoalescedFile), M3L.GetString(M3L.string_compiledCoalescedFile));
                OpenFileDialog ofd = new OpenFileDialog()
                {
                    Title = M3L.GetString(M3L.string_selectCoalescedManifestFile),
                };
                if (game3)
                {
                    ofd.Filter = M3L.GetString(M3L.string_game3CoalescedManifest) + @"|*.xml";
                }
                else
                {
                    ofd.Filter = M3L.GetString(M3L.string_lE1LE2CoalescedManifest) + @"|mele.extractedbin";
                }

                var result = ofd.ShowDialog(this);
                if (result.HasValue && result.Value)
                {
                    Task.Run(() =>
                    {
                        var containingDir = Directory.GetParent(ofd.FileName).FullName;
                        if (game3)
                        {
                            // Game 3
                            var dest = Path.Combine(containingDir, Path.GetFileNameWithoutExtension(ofd.FileName) + @".bin");
                            CoalescedConverter.ConvertToBin(ofd.FileName, dest);
                            M3Utilities.HighlightInExplorer(dest);
                        }
                        else
                        {
                            // LE1/LE2
                            var dest = LECoalescedConverter.GetDestinationPathFromManifest(ofd.FileName);
                            LECoalescedConverter.Pack(containingDir, dest); // takes the directory
                            M3Utilities.HighlightInExplorer(dest);
                        }
                        TelemetryInterposer.TrackEvent(@"Compiled Coalesced (menu)");
                        BackgroundTaskEngine.SubmitJobCompletion(task);
                    }).ContinueWithOnUIThread(x =>
                    {
                        if (x.Exception != null)
                        {
                            M3Log.Exception(x.Exception, @"Error compiling coalesced file:");
                            task.FinishedUIText = M3L.GetString(M3L.string_errorCompilingCoalescedFile);
                            BackgroundTaskEngine.SubmitJobCompletion(task);
                            M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorCompilingCoalescedFile, x.Exception.Message), M3L.GetString(M3L.string_errorCompiling), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });

                }
                else
                {
                    task.FinishedUIText = M3L.GetString(M3L.string_abortedCompilingCoalescedFile);
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                }
            }
        }

        private void CloseSearchBox()
        {
            ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, false);
            M3LoadedMods.Instance.ModSearchText = null;
        }

        private void ShowSearchBox()
        {
            ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, true);
            Keyboard.Focus(ModSearchBox);
        }

        private void LaunchEGMSettings(GameTargetWPF target = null)
        {
            target ??= GetCurrentTarget(MEGame.ME3);
            if (target != null)
            {
                LaunchExternalTool(ExternalToolLauncher.EGMSettings, $"\"{target.TargetPath}\""); // do not localize
            }
        }

        private void LaunchEGMSettingsLE(GameTargetWPF target = null)
        {
            target ??= GetCurrentTarget(MEGame.LE3);
            if (target != null)
            {
                LaunchExternalTool(ExternalToolLauncher.EGMSettingsLE, $"\"{target.TargetPath}\""); // do not localize
            }
        }

        private void LaunchFVBCCU(GameTargetWPF target = null)
        {
            target ??= InternalGetFVBCCCTarget();
            if (target != null)
            {
                LaunchExternalTool(ExternalToolLauncher.FVBCCU, $"\"{target.TargetPath}\""); // do not localize
            }
        }

        private void ShowOptions()
        {
            var optionsPanel = new OptionsPanel();
            optionsPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(optionsPanel);
        }

        private bool HasGameTarget(object obj)
        {
            if (obj is MEGame game)
            {
                return InstallationTargets.Any(x => x.Game == game);
            }
            return false;
        }

        private void OpenNexusSearch()
        {
            var nexusSearchPanel = new NexusFileQueryPanel();
            nexusSearchPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is string nxmlink && nxmlink.StartsWith(@"nxm://"))
                {
                    showNXMDownloader(nxmlink);
                }
            };
            ShowBusyControl(nexusSearchPanel);
        }

        private void OpenTutorial()
        {
            new IntroTutorial().Show();
        }

        private void OpenOIGDisabler()
        {
            var oigDisabler = new OIGODisabler();
            oigDisabler.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(oigDisabler);
        }

        private bool CanOpenModdescEditor() => SelectedMod != null && Settings.DeveloperMode;

        private void OpenModDescEditor()
        {
            if (SelectedMod != null)
            {
                new ModDescEditor(SelectedMod).Show();
            }
        }

        private void OpenKeybindsInjector()
        {
            var conflictDetectorPanel = new KeybindsInjectorPanel();
            conflictDetectorPanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(conflictDetectorPanel);
        }

        private void OpenModDesc()
        {
            M3Utilities.ShellOpenFile(SelectedMod.ModDescPath);
        }

        /// <summary>
        /// EGM Settings OT check
        /// </summary>
        /// <returns></returns>
        private bool CanLaunchEGMSettings()
        {
            var target = GetCurrentTarget(MEGame.ME3);
            if (target != null)
            {
                return target.GetInstalledDLC().Contains(@"DLC_MOD_EGM");
            }
            return false;
        }

        /// <summary>
        /// EGM Settings LE check
        /// </summary>
        /// <returns></returns>
        private bool CanLaunchEGMSettingsLE()
        {
            var target = GetCurrentTarget(MEGame.LE3);
            if (target != null)
            {
                return target.GetInstalledDLC().Contains(@"DLC_MOD_EGM");
            }
            return false;
        }

        /// <summary>
        /// Femshep vs BroShep: Clone Configuration Utility check
        /// </summary>
        /// <returns></returns>
        private bool CanLaunchFVBCCU()
        {
            return InternalGetFVBCCCTarget() != null;
        }

        private GameTargetWPF InternalGetFVBCCCTarget()
        {
            var firstTarget = SelectedGameTarget;
            if (firstTarget != null && firstTarget.Game.IsGame3())
            {
                // We check using the current selected target.
                if (InternalCanLaunchFVBCCC(firstTarget)) return firstTarget;
            }

            // TEST ME3
            var target = GetCurrentTarget(MEGame.ME3);
            if (target != null && firstTarget != target)
            {
                if (InternalCanLaunchFVBCCC(target)) return target;
            }

            // TEST LE3
            target = GetCurrentTarget(MEGame.LE3);
            if (target != null && firstTarget != target)
            {
                if (InternalCanLaunchFVBCCC(target)) return target;
            }

            return null;
        }

        private bool InternalCanLaunchFVBCCC(GameTargetWPF target)
        {
            var installedDLC = target.GetInstalledDLC();
            if (target.Game == MEGame.ME3) return installedDLC.Contains(@"DLC_MOD_FSvBS") || installedDLC.Contains(@"DLC_MOD_FSvBS_V");
            if (target.Game == MEGame.LE3) return installedDLC.Contains(@"DLC_MOD_FSvBSLE") || installedDLC.Contains(@"DLC_MOD_FSvBSLE_V");
            return false;
        }

        private void OpenOfficialDLCToggler()
        {
            var dlcToggler = new OfficialDLCToggler();
            dlcToggler.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(dlcToggler);
        }

        private void OpenConflictDetector()
        {
            var conflictDetectorPanel = new ConflictDetectorPanel();
            conflictDetectorPanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(conflictDetectorPanel);
        }

        private void OpenBackupFileFetcher()
        {
            var fetcher = new BackupFileFetcher();
            fetcher.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(fetcher);
        }

        private bool CanOpenConsoleKeyKeybinder()
        {
            return InstallationTargets.Any();
        }

        private void OpenConsoleKeyKeybinder()
        {
            var consoleKeybindingPanel = new ConsoleKeybindingPanel();
            consoleKeybindingPanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(consoleKeybindingPanel);
        }

        private bool CanOpenImportFromUI() => !M3LoadedMods.Instance.IsLoadingMods;

        private void OpenImportFromGameUI()
        {
            M3Log.Information(@"Opening Import DLC mod from game panel");
            var importerPanel = new ImportInstalledDLCModPanel();
            importerPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(importerPanel);
        }

        private bool CanOpenBatchModPanel()
        {
            return !M3LoadedMods.Instance.IsLoadingMods;
        }

        private void OpenBatchModPanel()
        {
            var batchLibrary = new BatchModLibrary();
            batchLibrary.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is BatchLibraryInstallQueue queue)
                {
                    BatchPanelResult = new PanelResult();
                    var target = batchLibrary.SelectedGameTarget;
                    //Install queue

                    bool continueInstalling = true;
                    int modIndex = 0;

                    //recursive. If someone is installing enough mods to cause a stack overflow exception, well, congrats, you broke my code.
                    void modInstalled(bool successful)
                    {
                        continueInstalling &= successful;
                        if (continueInstalling && queue.ModsToInstall.Count > modIndex)
                        {
                            M3Log.Information($@"Installing batch mod [{modIndex}/{queue.ModsToInstall.Count}]: {queue.ModsToInstall[modIndex].ModName}");
                            ApplyMod(queue.ModsToInstall[modIndex], target, batchMode: true, installCompressed: queue.InstallCompressed, installCompletedCallback: modInstalled);
                            modIndex++;
                        }
                        else
                        {
                            //End
                            HandleBatchPanelResult = true;
                        }
                    }

                    modInstalled(true); //kick off first installation
                }
            };
            ShowBusyControl(batchLibrary);
        }

        private void OpenMixinManagerPanel()
        {
            var mixinManager = new MixinManager();
            mixinManager.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is string moddescpath)
                {
                    M3LoadedMods.Instance.LoadMods(moddescpath, gamesToLoad: new[] { MEGame.ME3 });
                }
            };
            ShowBusyControl(mixinManager);
        }

        private bool CanOpenMixinManagerPanel()
        {
            return true;
        }

        private void OpenUpdaterServicePanelEditorMode()
        {
            var updaterServicePanel = new UpdaterServicePanel();
            updaterServicePanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(updaterServicePanel);
        }

        private void OpenUpdaterServicePanel()
        {
            var updaterServicePanel = new UpdaterServicePanel(SelectedMod);
            updaterServicePanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(updaterServicePanel);
        }

        private bool CanOpenUpdaterServicePanel() => SelectedMod != null && SelectedMod.ModClassicUpdateCode > 0;

        private void OpenModMakerPanel()
        {
            var modmakerPanel = new ModMakerPanel();
            modmakerPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is Mod m)
                {
                    M3LoadedMods.Instance.LoadMods(m, gamesToLoad: new[] { m.Game });
                }
            };
            ShowBusyControl(modmakerPanel);
        }

        private bool CanOpenModMakerPanel()
        {
            //todo: Check for backup
            return true;
        }

        private bool CanEndorseM3()
        {
            return NexusModsUtilities.UserInfo != null && (!ME1NexusEndorsed && !ME2NexusEndorsed && !ME3NexusEndorsed);
        }

        private void EndorseM3()
        {
            if (!ME1NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME1)");
                NexusModsUtilities.EndorseFile(@"masseffect", true, 149, (newStatus) =>
                {
                    ME1NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed) ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods) : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }

            if (!ME2NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME2)");
                NexusModsUtilities.EndorseFile(@"masseffect2", true, 248, (newStatus) =>
                {
                    ME2NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed) ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods) : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }

            if (!ME3NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME3)");
                NexusModsUtilities.EndorseFile(@"masseffect3", true, 373, (newStatus) =>
                {
                    ME3NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed) ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods) : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }
        }

        private void OpenMEIM()
        {
            new ME1IniModder().Show();
        }

        private bool CanCreateTestArchive() => SelectedMod != null && SelectedMod.GetJob(ModJob.JobHeader.ME2_RCWMOD) == null;

        private void CreateTestArchive()
        {
            var testArchiveGenerator = new TestArchiveGenerator(SelectedMod);
            testArchiveGenerator.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(testArchiveGenerator);
        }

        private void EndorseWrapper()
        {
            if (SelectedMod.IsEndorsed)
            {
                var unendorseresult = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_unendorseMod, SelectedMod.ModName), M3L.GetString(M3L.string_confirmUnendorsement), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (unendorseresult == MessageBoxResult.Yes)
                {
                    UnendorseMod();
                }
            }
            else
            {
                EndorseMod();
            }
        }

        private bool CanEndorseMod() => NexusModsUtilities.HasAPIKey && SelectedMod != null && SelectedMod.NexusModID > 0 && SelectedMod.CanEndorse && !IsEndorsingMod;

        private void EndorseMod()
        {
            if (SelectedMod != null)
            {
                M3Log.Information(@"Endorsing mod: " + SelectedMod.ModName);
                CurrentModEndorsementStatus = M3L.GetString(M3L.string_endorsing);
                IsEndorsingMod = true;
                SelectedMod.EndorseMod(EndorsementCallback, true);
            }
        }

        private void UnendorseMod()
        {
            if (SelectedMod != null)
            {
                M3Log.Information(@"Unendorsing mod: " + SelectedMod.ModName);
                CurrentModEndorsementStatus = M3L.GetString(M3L.string_unendorsing);
                IsEndorsingMod = true;
                SelectedMod.EndorseMod(EndorsementCallback, false);
            }
        }

        private void EndorsementCallback(Mod m, bool isModNowEndorsed, string endorsementFailedMessage)
        {
            IsEndorsingMod = false;
            if (SelectedMod == m)
            {
                UpdatedEndorsementString();
            }
            if (endorsementFailedMessage != null)
            {
                M3L.ShowDialog(this, endorsementFailedMessage, M3L.GetString(M3L.string_couldNotEndorseFile), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanShowNexusPanel()
        {
            return true; //might make some condition later.
        }

        private void ShowNexusPanel()
        {
            var nexusModsLoginPane = new NexusModsLogin();
            nexusModsLoginPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(nexusModsLoginPane);
        }

        public bool HasAtLeastOneTarget() => InstallationTargets.Any();

        private bool HasME3Target()
        {
            return InstallationTargets.Any(x => x.Game == MEGame.ME3);
        }

        private bool HasLETarget() => InstallationTargets.Any(x => x.Game.IsLEGame());

        private void CheckSelectedModForUpdate()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(CheckSelectedModForUpdate));
            bw.DoWork += (a, b) => { ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new[] { SelectedMod })); };
            bw.RunWorkerAsync();

        }

        private void RestoreSelectedMod()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(RestoreSelectedMod));
            bw.DoWork += (a, b) => { ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new[] { SelectedMod }), true); };
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
                Title = M3L.GetString(M3L.string_selectModArchive),
                Filter = M3L.GetString(M3L.string_supportedFiles) + @"|*.zip;*.rar;*.7z;*.exe;*.me2mod"
            };
            var result = m.ShowDialog(this);
            if (result.Value)
            {
                TelemetryInterposer.TrackEvent(@"User opened mod archive for import", new Dictionary<string, string> { { @"Method", @"Manual file selection" }, { @"Filename", Path.GetFileName(m.FileName) } });
                var archiveFile = m.FileName;
                M3Log.Information(@"Opening archive user selected: " + archiveFile);
                openModImportUI(archiveFile);
            }
        }

        private bool CanOpenArchiveSelectionDialog()
        {
            return TPIService.ServiceLoaded && TPMIService.ServiceLoaded;
        }

        private bool CanDeleteModFromLibrary() => SelectedMod != null && !ContentCheckInProgress;

        private void DeleteModFromLibraryWrapper()
        {
            DeleteModFromLibrary(SelectedMod);
        }

        public bool DeleteModFromLibrary(Mod selectedMod)
        {
            var confirmationResult = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogDeleteSelectedModFromLibrary, selectedMod.ModName), M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmationResult == MessageBoxResult.Yes)
            {
                M3Log.Information(@"Deleting mod from library: " + selectedMod.ModPath);
                if (M3Utilities.DeleteFilesAndFoldersRecursively(selectedMod.ModPath))
                {
                    M3LoadedMods.Instance.RemoveMod(selectedMod);
                    return true;
                }

                //LoadMods();
            }
            return false;
        }

        private void ShowDeploymentPane()
        {
            if (SelectedMod.InstallationJobs.Count == 1 && SelectedMod.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null)
            {
                M3Log.Error(M3L.GetString(M3L.string_rcwModsCannotBeDeployed));
                M3L.ShowDialog(this, M3L.GetString(M3L.string_rcwModsCannotBeDeployedDescription),
                    M3L.GetString(M3L.string_cannotDeployMe2modFiles), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // TODO: Move this into archive panel
            GameTargetWPF vt = GetCurrentTarget(SelectedMod.Game);
            if (vt == null)
            {
                M3Log.Error($@"Cannot deploy mod, no current game install for {SelectedMod.Game} is available");
                M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_interp_dialog_cannotDeployModNoTarget, SelectedMod.Game),
                    M3L.GetString(M3L.string_cannotDeployMod), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var bup = BackupService.GetGameBackupPath(SelectedMod.Game);
            if (bup != null && Directory.Exists(bup))
            {
                var archiveDeploymentPane = new ArchiveDeployment(SelectedMod);
                archiveDeploymentPane.Close += (a, b) =>
                {
                    ReleaseBusyControl();
                    if (b.Data is List<Mod> modsForTPMI)
                    {
                        // Show form for each mod
                        foreach (var m in modsForTPMI)
                        {
                            var telemetryPane = new TPMITelemetrySubmissionForm(m);
                            telemetryPane.Close += (a, b) => { ReleaseBusyControl(); };
                            ShowBusyControl(telemetryPane);
                        }
                    }
                };
                ShowBusyControl(archiveDeploymentPane);
            }
            else
            {
                M3Log.Error($@"Cannot deploy mod, no backup for {SelectedMod.Game} is available");
                M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_interp_dialog_cannotDeployModNoBackup, SelectedMod.Game),
                    M3L.GetString(M3L.string_cannotDeployMod), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowUpdateCompletedPane()
        {
            var message = M3L.GetString(M3L.string_interp_modManagerHasBeenUpdatedTo, App.UpdatedFrom.ToString(), App.AppVersionAbout);
            var updateCompletedPanel = new UpdateCompletedPanel(M3L.GetString(M3L.string_updateCompleted), message);
            updateCompletedPanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(updateCompletedPanel);
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
        /// <param name="swapImmediately">If the incoming panel should be shown immediately</param>
        internal void ShowBusyControl(MMBusyPanelBase control, bool swapImmediately = false)
        {
            if (queuedUserControls.Count == 0 && !IsBusy)
            {
                IsBusy = true;
                BusyContentM3 = new SingleItemPanel2(control);
            }
            else
            {
                if (swapImmediately)
                {
                    M3Log.Information(@$"Immediately swapping to panel {control}");

                    // Rebuild the queue list with our existing open panel at the front
                    Queue<MMBusyPanelBase> rebuildQueue = new Queue<MMBusyPanelBase>();
                    if (BusyContentM3 is SingleItemPanel2 spi && spi.Content is MMBusyPanelBase mmbpb)
                    {
                        rebuildQueue.Enqueue(mmbpb); // Add the current panel
                    }

                    while (queuedUserControls.TryDequeue(out var item))
                    {
                        rebuildQueue.Enqueue(item);
                    }

                    BusyContentM3 = new SingleItemPanel2(control);

                    // Now rebuild the queue after we have shown our item
                    while (rebuildQueue.TryDequeue(out var item))
                    {
                        queuedUserControls.Enqueue(item);
                    }
                }
                else
                {
                    M3Log.Information(@$"Queueing panel {control}");
                    queuedUserControls.Enqueue(control);
                }
            }
        }

        /// <summary>
        /// Shows or queues the specified control
        /// </summary>
        /// <param name="control">Control to show or queue</param>
        /// <returns>True if a new control was queued up, false if it wasn't</returns>
        internal void ReleaseBusyControl()
        {
            if (BusyContentM3 is SingleItemPanel2 singleItemPanel)
            {
                var actualClosingPanel = singleItemPanel.Content as MMBusyPanelBase;
                singleItemPanel.DetatchControl(); // Seems to reduce memory leakage due to how ContentPresenter works in BusyHost.
                BusyContentM3 = null; // Remove existing reference to panel. Maybe reduces memory leakage...

                // If somehow an empty panel was installed
                if (actualClosingPanel != null)
                {
                    HandlePanelResult(actualClosingPanel.Result);
                }

                if (queuedUserControls.Count == 0)
                {
                    IsBusy = false;
                    Task.Factory.StartNew(() =>
                    {
                        // this is to force some items that are no longer relevant to be cleaned up.
                        // for some reason commands fire even though they are no longer attached to the interface
                        Thread.Sleep(3000);
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                    });
                }
                else
                {
                    if (queuedUserControls.TryDequeue(out var control))
                    {
                        BusyContentM3 = new SingleItemPanel2(control);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if there are any queued panels to show.
        /// </summary>
        /// <returns>True if there are any panels in the queue.</returns>
        internal bool HasQueuedPanel()
        {
            return queuedUserControls.Count > 0;
        }

        private void HandlePanelResult(PanelResult result)
        {
            if (BatchPanelResult != null)
            {
                result.MergeInto(BatchPanelResult);
                if (HandleBatchPanelResult)
                {
                    result = BatchPanelResult;

                    // Clear result
                    BatchPanelResult = null;
                    HandleBatchPanelResult = false;
                }
                else
                    return;
            }

            // This is pretty dicey with thread safety... 
            foreach (var v in result.TargetsToPlotManagerSync)
            {
                SyncPlotManagerForTarget(v);
            }

            // MERGE DLC

            // Todo: Persistence? That sounds miserable
            var targetMergeMapping = new Dictionary<GameTargetWPF, M3MergeDLC>();
            if (result.NeedsMergeDLC)
            {
                // Remove any if existing.
                foreach (var mergeTarget in result.GetMergeTargets())
                {
                    M3MergeDLC.RemoveMergeDLC(mergeTarget);

                    var mergeDLC = new M3MergeDLC(mergeTarget);
                    targetMergeMapping[mergeTarget] = mergeDLC;

                    // Generate a new one - IF NECESSARY!
                    // This is so if user deletes merge DLC it doesn't re-create itself immediately even if it's not necessary, e.g. user removed all merge DLC-eligible items.

                    bool needsGenerated =
                        SQMOutfitMerge.NeedsMergedGame3(mergeTarget)
                        || ME2EmailMerge.NeedsMergedGame2(mergeTarget);
                    if (needsGenerated)
                    {
                        mergeDLC.GenerateMergeDLC();
                    }
                }
            }


            foreach (var v in result.TargetsToSquadmateMergeSync)
            {
                ShowRunAndDone(() => SQMOutfitMerge.RunGame3SquadmateOutfitMerge(targetMergeMapping[v]), M3L.GetString(M3L.string_synchronizingSquadmateOutfits), M3L.GetString(M3L.string_synchronizedSquadmateOutfits));
            }

            foreach (var v in result.TargetsToEmailMergeSync)
            {
                ShowRunAndDone(() => ME2EmailMerge.RunGame2EmailMerge(targetMergeMapping[v]), M3L.GetString(M3L.string_synchronizingEmails), M3L.GetString(M3L.string_synchronizedEmails));
            }

            foreach (var v in result.TargetsToAutoTOC)
            {
                AutoTOCTarget(v, false);
            }

            if (result.ReloadMods)
            {
                // Scope the reload if we are reloading for mod update checks (which means a mod was just imported and we are reloading that game(s))
                var gamesToLoad = result.ModsToCheckForUpdates.Select(x => x.Game).Distinct().ToArray();
                if (gamesToLoad.Length == 0)
                    gamesToLoad = null;
                M3LoadedMods.Instance.LoadMods(result.ModToHighlightOnReload, result.ModsToCheckForUpdates.Any(), result.ModsToCheckForUpdates.ToList(), gamesToLoad);
            }

            Task.Run(() =>
            {
                if (result.ReloadTargets)
                {
                    PopulateTargets();
                }
            }).ContinueWithOnUIThread(x =>
            {

                if (result.PanelToOpen != null)
                {
                    MMBusyPanelBase control = null;
                    switch (result.PanelToOpen)
                    {
                        case EPanelID.ASI_MANAGER:
                            control = new ASIManagerPanel(result.SelectedTarget);
                            break;
                        case EPanelID.NXM_CONFIGURATOR:
                            control = new NXMHandlerConfigPanel();
                            break;
                        case EPanelID.BACKUP_CREATOR:
                            control = new BackupCreator(InstallationTargets.ToList());
                            break;
                        default:
                            throw new Exception($@"HandlePanelResult did not handle panelid {result.PanelToOpen}");
                    }

                    control.Close += (a, b) => { ReleaseBusyControl(); };
                    ShowBusyControl(control);
                    TelemetryInterposer.TrackEvent($@"Launched {result.PanelToOpen}", new Dictionary<string, string>()
                    {
                        {@"Invocation method", @"Installation Information"}
                    });
                }
                else if (result.ToolToLaunch != null)
                {
                    if (result.ToolToLaunch == ExternalToolLauncher.EGMSettings)
                    {
                        LaunchEGMSettings(result.SelectedTarget);
                    }
                    else if (result.ToolToLaunch == ExternalToolLauncher.EGMSettingsLE)
                    {
                        LaunchEGMSettingsLE(result.SelectedTarget);
                    }
                    else
                    {
                        BootToolPathPassthrough(result.ToolToLaunch, result.SelectedTarget);
                    }
                }
            });
        }

        private void ShowRunAndDone(Action action, string startStr, string endStr)
        {
            BackgroundTask task = null;
            var runAndDone = new RunAndDonePanel(action, startStr, endStr);
            runAndDone.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(runAndDone);
        }
        private void ShowBackupPane()
        {
            var backupCreator = new BackupCreator(InstallationTargets.ToList());
            backupCreator.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is string result)
                {
                    if (result == @"ALOTInstaller")
                    {
                        BootToolPathPassthrough(ExternalToolLauncher.ALOTInstaller);
                    }
                }
            };
            ShowBusyControl(backupCreator);
        }

        private void ShowRestorePane()
        {
            var restoreManager = new RestorePanel(InstallationTargets.ToList(), SelectedGameTarget);
            restoreManager.Close += (a, b) =>
            {
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
            };
            ShowBusyControl(installationInformation);
        }

        /// <summary>
        /// Boots the specified tool ID, passes through the current active targets in M3, if they are supported.
        /// </summary>
        /// <param name="toolname"></param>
        private void BootToolPathPassthrough(string toolname, GameTargetWPF forcedTarget = null)
        {
            var arguments = "";
            var me1Target = forcedTarget?.Game == MEGame.ME1 ? forcedTarget : GetCurrentTarget(MEGame.ME1);
            var me2Target = forcedTarget?.Game == MEGame.ME2 ? forcedTarget : GetCurrentTarget(MEGame.ME2);
            var me3Target = forcedTarget?.Game == MEGame.ME3 ? forcedTarget : GetCurrentTarget(MEGame.ME3);

            var le1Target = forcedTarget?.Game == MEGame.LE1 ? forcedTarget : GetCurrentTarget(MEGame.LE1);
            var le2Target = forcedTarget?.Game == MEGame.LE2 ? forcedTarget : GetCurrentTarget(MEGame.LE2);
            var le3Target = forcedTarget?.Game == MEGame.LE3 ? forcedTarget : GetCurrentTarget(MEGame.LE3);
            if (me1Target != null && me1Target.Supported)
            {
                arguments += $"--me1path \"{me1Target.TargetPath}\" "; //do not localize
            }
            if (me2Target != null && me2Target.Supported)
            {
                arguments += $"--me2path \"{me2Target.TargetPath}\" "; //do not localize
            }
            if (me3Target != null && me3Target.Supported)
            {
                arguments += $"--me3path \"{me3Target.TargetPath}\" "; //do not localize
            }

            if (le1Target != null && le1Target.Supported)
            {
                arguments += $"--le1path \"{le1Target.TargetPath}\" "; //do not localize
            }
            if (le2Target != null && le2Target.Supported)
            {
                arguments += $"--le2path \"{le2Target.TargetPath}\" "; //do not localize
            }
            if (le3Target != null && le3Target.Supported)
            {
                arguments += $"--le3path \"{le3Target.TargetPath}\" "; //do not localize
            }

            LaunchExternalTool(toolname, arguments);
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
            InternalStartGame(SelectedGameTarget);
        }

        private void InternalStartGame(GameTargetWPF target)
        {
            var game = target.Game.ToGameName();
            BackgroundTask gameLaunch = BackgroundTaskEngine.SubmitBackgroundJob(@"GameLaunch", M3L.GetString(M3L.string_interp_launching, game), M3L.GetString(M3L.string_interp_launched, game));

            try
            {
                Task.Run(() => GameLauncher.LaunchGame(target))
                    .ContinueWith(x =>
                    {
                        if (x.Exception != null)
                        {
                            M3Log.Error($@"There was an error launching the game: {x.Exception.FlattenException()}");
                        }

                        BackgroundTaskEngine.SubmitJobCompletion(gameLaunch);
                    });
            }
            catch (Exception e)
            {
                BackgroundTaskEngine.SubmitJobCompletion(gameLaunch); // This ensures message is cleared out of queue
                if (e is Win32Exception w32e)
                {
                    if (w32e.NativeErrorCode == 1223)
                    {
                        //Admin canceled.
                        return; //we don't care.
                    }
                }

                M3Log.Error(@"Error launching game: " + e.Message);
            }

            //Detect screen resolution - useful info for scene modders
            string resolution = @"Could not detect";
            var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare");
            try
            {
                switch (target.Game)
                {
                    case MEGame.ME1:
                        {
                            iniFile = Path.Combine(iniFile, @"Mass Effect", @"Config", @"BIOEngine.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"WinDrv.WindowsClient");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"StartupResolutionX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"StartupResolutionY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                    case MEGame.ME2:
                    case MEGame.ME3:
                        {
                            iniFile = Path.Combine(iniFile, @"Mass Effect " + target.Game.ToGameNum(), @"BIOGame", @"Config", @"Gamersettings.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"SystemSettings");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"ResX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"ResY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                    case MEGame.LE1:
                    case MEGame.LE2:
                    case MEGame.LE3:
                        {
                            iniFile = Path.Combine(target.TargetPath, @"BioGame", @"Config", @"Gamersettings.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"SystemSettings");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"ResX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"ResY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                }

                TelemetryInterposer.TrackEvent(@"Launched game", new Dictionary<string, string>()
                {
                    {@"Game", game.ToString()},
                    {@"Screen resolution", resolution}
                });
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error trying to detect screen resolution: " + e.Message);
            }
        }

        /// <summary>
        /// Updates boot target and returns the HRESULT of the update command for registry.
        /// Returns -3 if no registry update was performed.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private int UpdateBootTarget(GameTargetWPF target)
        {
            string exe = @"reg";
            var args = new List<string>();
            string regPath = null;
            switch (target.Game)
            {
                case MEGame.ME1:
                    {
                        var existingPath = ME1Directory.DefaultGamePath;
                        if (existingPath != null)
                        {
                            regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect";
                        }
                    }
                    break;
                case MEGame.ME2:
                    {
                        var existingPath = ME2Directory.DefaultGamePath;
                        if (existingPath != null)
                        {
                            regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect 2";
                        }
                    }

                    break;
                case MEGame.ME3:
                    {
                        var existingPath = ME3Directory.DefaultGamePath;
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
                args.Add(@"add");
                args.Add(regPath);
                args.Add(@"/v");
                args.Add(target.Game == MEGame.ME3 ? @"Install Dir" : @"Path");
                args.Add(@"/t");
                args.Add(@"REG_SZ");
                args.Add(@"/d");
                args.Add($"{target.TargetPath.TrimEnd('\\')}\\\\"); // do not localize
                                                                    // ^ Strip ending slash. Then append it to make sure there is ending slash. Reg will interpret final \ as an escape, so we do \\ (as documented on ss64)
                args.Add(@"/f");

                return M3Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true);
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
            if (obj is string str && Enum.TryParse(str, out MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target != null && !M3Utilities.IsGameRunning(game))
                {
                    return File.Exists(M3Utilities.GetBinkFile(target));
                }
            }

            return false;
        }

        private void ToggleBinkw32(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target == null) return; //can't toggle this
                if (M3Utilities.IsGameRunning(game))
                {
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogCannotInstallBinkWhileGameRunning, game.ToGameName()), M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                bool install = false;
                switch (game)
                {
                    case MEGame.ME1:
                        install = !ME1ASILoaderInstalled;
                        break;
                    case MEGame.ME2:
                        install = !ME2ASILoaderInstalled;
                        break;
                    case MEGame.ME3:
                        install = !ME3ASILoaderInstalled;
                        break;
                    case MEGame.LE1:
                        install = !LE1ASILoaderInstalled;
                        break;
                    case MEGame.LE2:
                        install = !LE2ASILoaderInstalled;
                        break;
                    case MEGame.LE3:
                        install = !LE3ASILoaderInstalled;
                        break;
                }

                if (install)
                {
                    target.InstallBinkBypass(false);
                }
                else
                {
                    M3Utilities.UninstallBinkBypass(target);
                }


                UpdateBinkStatus(target.Game);
            }
        }

        private void RunGameConfigTool(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    var configTool = M3Utilities.GetGameConfigToolPath(target);
                    try
                    {
                        M3Utilities.RunProcess(configTool, "", false, true, false, false);
                    }
                    catch (Exception e)
                    {
                        // user may have canceled running it. seems it sometimes requires admin
                        M3Log.Error($@"Error running config tool for {game}: {e.Message}");
                    }
                }
            }
        }

        private bool CanRunGameConfigTool(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    var configTool = M3Utilities.GetGameConfigToolPath(target);
                    return File.Exists(configTool);
                }
            }

            return false;
        }

        private void AddTarget()
        {
            M3Log.Information(@"User is adding new modding target");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = M3L.GetString(M3L.string_selectGameExecutable);
            string filter = $@"{M3L.GetString(M3L.string_gameExecutable)}|MassEffect.exe;MassEffect2.exe;MassEffect3.exe;MassEffectLauncher.exe;MassEffect1.exe"; //only partially localizable.
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                MEGame gameSelected = MEGame.Unknown;
                var filename = Path.GetFileName(ofd.FileName);
                M3Log.Information($@"Validating user chosen exe: {filename}");
                if (filename.Equals(@"MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase))
                    gameSelected = MEGame.ME3;
                if (filename.Equals(@"MassEffect2.exe", StringComparison.InvariantCultureIgnoreCase))
                    gameSelected = MEGame.ME2;

                if (gameSelected != MEGame.Unknown)
                {
                    // Check for LE versions
                    var version = FileVersionInfo.GetVersionInfo(ofd.FileName);
                    if (version.FileMajorPart >= 2)
                    {
                        // LE1 can't be selected this way as it has unique exe name.
                        if (gameSelected == MEGame.ME2) gameSelected = MEGame.LE2;
                        if (gameSelected == MEGame.ME3) gameSelected = MEGame.LE3;
                    }
                }
                else
                {
                    // Has unique name
                    if (filename.Equals(@"MassEffect.exe", StringComparison.InvariantCultureIgnoreCase))
                        gameSelected = MEGame.ME1;
                    if (filename.Equals(@"MassEffect1.exe", StringComparison.InvariantCultureIgnoreCase))
                        gameSelected = MEGame.LE1;

                    if (filename.Equals(@"MassEffectLauncher.exe"))
                    {
                        var version = FileVersionInfo.GetVersionInfo(ofd.FileName);
                        if (version.FileMajorPart >= 2)
                        {
                            gameSelected = MEGame.LELauncher;
                        }
                    }
                }

                if (gameSelected != MEGame.Unknown)
                {
                    string result = Path.GetDirectoryName(ofd.FileName);
                    if (gameSelected != MEGame.LELauncher)
                    {
                        // game root path for ME1/ME2
                        result = Path.GetDirectoryName(result);
                    }

                    if (gameSelected.IsLEGame() || gameSelected == MEGame.ME3)
                        result = Path.GetDirectoryName(result); //up one more because of win32/win64 directory.

                    var pendingTarget = new GameTargetWPF(gameSelected, result, false);
                    string failureReason = pendingTarget.ValidateTarget();

                    if (failureReason == null)
                    {
                        TelemetryInterposer.TrackEvent(@"Attempted to add game target", new Dictionary<string, string>()
                            {
                                {@"Game", pendingTarget.Game.ToString()},
                                {@"Result", @"Success"},
                                {@"Supported", pendingTarget.Supported.ToString()}
                            });

                        M3Utilities.AddCachedTarget(pendingTarget);
                        PopulateTargets(pendingTarget);
                    }
                    else
                    {
                        TelemetryInterposer.TrackEvent(@"Attempted to add game target", new Dictionary<string, string>()
                            {
                                {@"Game", pendingTarget.Game.ToString()},
                                {@"Result", @"Failed, " + failureReason},
                                {@"Supported", pendingTarget.Supported.ToString()}
                            });
                        M3Log.Error(@"Could not add target: " + failureReason);
                        M3L.ShowDialog(this,
                            M3L.GetString(M3L.string_interp_dialogUnableToAddGameTarget, failureReason),
                            M3L.GetString(M3L.string_errorAddingTarget), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    M3Log.Error($@"Unsupported/unknown game: {ofd.FileName}");
                }
            }

            else
            {
                M3Log.Information(@"User aborted adding new target");
            }
        }

        public bool ContentCheckInProgress { get; set; } = true; //init is content check
        private bool NetworkThreadNotRunning() => !ContentCheckInProgress;

        private void CheckForContentUpdates()
        {
            PerformStartupNetworkFetches(false);
        }

        public GameTargetWPF SelectedGameTarget { get; set; }

        private bool CanReloadMods()
        {
            return !M3LoadedMods.Instance.IsLoadingMods;
        }

        private bool CanApplyMod()
        {
            if (SelectedMod == null)
            {
                ApplyModButtonText = M3L.GetString(M3L.string_selectMod);
                return false;
            }

            if (SelectedGameTarget == null)
            {
                ApplyModButtonText = M3L.GetString(M3L.string_noTarget);
                return false;

            }

            if (SelectedGameTarget.Game != SelectedMod.Game)
            {
                ApplyModButtonText = M3L.GetString(M3L.string_cannotInstallToThisGame);
                return false;
            }

            ApplyModButtonText = M3L.GetString(M3L.string_applyMod);
            return true;
        }

        /// <summary>
        /// Applies a mod to the current or forced target. This method is asynchronous, it must run on the UI thread but it will immediately yield once the installer begins.
        /// </summary>
        /// <param name="mod">Mod to install</param>
        /// <param name="forcedTarget">Forced target to install to</param>
        /// <param name="batchMode">Causes ME3 autotoc to skip at end of install</param>
        /// <param name="installCompletedCallback">Callback when mod installation either succeeds for fails</param>

        private void ApplyMod(Mod mod, GameTargetWPF forcedTarget = null, bool batchMode = false, bool? installCompressed = null, Action<bool> installCompletedCallback = null)
        {
            if (!M3Utilities.IsGameRunning(mod.Game))
            {
                BackgroundTask modInstallTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ModInstall", M3L.GetString(M3L.string_interp_installingMod, mod.ModName), M3L.GetString(M3L.string_interp_installedMod, mod.ModName));
                var modOptionsPicker = new ModInstallOptionsPanel(mod, forcedTarget ?? SelectedGameTarget, installCompressed, batchMode: batchMode);
                //var modInstaller = new ModInstaller(mod, forcedTarget ?? SelectedGameTarget, installCompressed, batchMode: batchMode);
                modOptionsPicker.Close += (a, b) =>
                {
                    ReleaseBusyControl();
                    if (b.Data is ModInstallOptionsPackage miop)
                    {
                        ModInstaller mi = new ModInstaller(miop);
                        mi.Close += (c, d) =>
                        {

                            BackgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                            installCompletedCallback?.Invoke(mi.InstallationSucceeded);
                            ReleaseBusyControl();
                            // Todo: Batch mode?
                        };
                        ShowBusyControl(mi);
                    }
                    else
                    {
                        // User canceled the options
                        installCompletedCallback?.Invoke(false); // Canceled
                        modInstallTask.FinishedUIText = M3L.GetString(M3L.string_installationAborted);
                        BackgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                    }
                };
                ShowBusyControl(modOptionsPicker);
            }
            else
            {
                M3Log.Error($@"Blocking install of {mod.ModName} because {mod.Game.ToGameName()} is running.");
                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogCannotInstallModsWhileGameRunning, mod.Game.ToGameName()), M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadMods()
        {
            M3LoadedMods.Instance.LoadMods(SelectedMod);
        }

        private void CheckTargetPermissions(bool promptForConsent = true, bool showDialogEvenIfNone = false)
        {
            var targetsNeedingUpdate = InstallationTargets.Where(x => x.Selectable && !x.IsTargetWritable()).ToList();

            if (targetsNeedingUpdate.Count > 0)
            {
                if (promptForConsent)
                {
                    M3Log.Information(@"Some game paths/keys are not writable. Prompting user.");
                    bool result = false;
                    Application.Current.Dispatcher.Invoke(delegate { result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogUACPreConsent), M3L.GetString(M3L.string_someTargetsKeysWriteProtected), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; });
                    if (result)
                    {
                        TelemetryInterposer.TrackEvent(@"Granting write permissions", new Dictionary<string, string>() { { @"Granted?", @"Yes" } });
                        try
                        {
                            M3Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate.Select(x => x.TargetPath).ToList());
                        }
                        catch (Exception e)
                        {
                            M3Log.Error(@"Error granting write permissions: " + App.FlattenException(e));
                        }
                    }
                    else
                    {
                        M3Log.Warning(@"User denied permission to grant write permissions");
                        TelemetryInterposer.TrackEvent(@"Granting write permissions", new Dictionary<string, string>() { { @"Granted?", @"No" } });
                    }
                }
                else
                {
                    TelemetryInterposer.TrackEvent(@"Granting write permissions", new Dictionary<string, string>() { { @"Granted?", @"Implicit" } });
                    M3Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate.Select(x => x.TargetPath).ToList());
                }
            }
            else if (showDialogEvenIfNone)
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_allTargetsWritable), M3L.GetString(M3L.string_targetsWritable), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore


        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            DPIScaling.SetScalingFactor(this);
#if PRERELEASE
            // MessageBox.Show(M3L.GetString(M3L.string_prereleaseNotice));
            // MessageBox.Show(M3L.GetString(M3L.string_betaBuildDialog));
#endif
            if (App.BootingUpdate)
            {
                ShowUpdateCompletedPane();
            }

            if (!App.IsOperatingSystemSupported())
            {
                string osList = string.Join("\n - ", App.SupportedOperatingSystemVersions); //do not localize
                M3Log.Error(@"This operating system is not supported.");
                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialog_unsupportedOS, osList), M3L.GetString(M3L.string_unsupportedOperatingSystem), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Only show if OS is supported
                var lastCrash = Crashes.GetLastSessionCrashReportAsync().Result;
                if (Settings.DeveloperMode && lastCrash?.StackTrace != null)
                {
                    // Show messagebox?
                }
            }

            // Run on background thread as we don't need the result of this
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"NexusModsInitialAuthentication");
            nbw.DoWork += (a, b) => RefreshNexusStatus();
            nbw.RunWorkerAsync();

            var syncContext = TaskScheduler.FromCurrentSynchronizationContext();
            LegendaryExplorerCoreLib.SetSynchronizationContext(syncContext);
            IsEnabled = false;
            Task.Run(() =>
            {
                ME3TweaksCoreLib.Initialize(LibraryBoot.GetPackage());
                LibraryBoot.AddM3SpecificFixes();
                M3Log.Information(@"Ensuring default ASI assets are present");
                M3Utilities.ExtractDefaultASIResources();

                //debugMethod();
                CurrentOperationText = M3L.GetString(M3L.string_loadingTargets);
                PopulateTargets();
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    Debug.WriteLine(@"Exception!");
                }
                IsEnabled = true;
                if (!Settings.ShowedPreviewPanel)
                {
                    ShowFirstRunPanel();
                }
                else
                {
                    M3LoadedMods.Instance.LoadMods();
                }

                PerformStartupNetworkFetches(true);
                if (BackupNagSystem.ShouldShowNagScreen(InstallationTargets.ToList()))
                {
                    ShowBackupNag();
                }
                collectHardwareInfo();
                StartedUp = true;
            });

        }

        private void collectHardwareInfo()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"HardwareInventory");
            nbw.DoWork += (a, b) =>
            {
                var data = new Dictionary<string, string>();
                try
                {
                    ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher(@"SELECT * FROM Win32_Processor");
                    foreach (ManagementObject moProcessor in mosProcessor.Get())
                    {
                        // For seeing AMD vs Intel (for ME1 lighting)
                        if (moProcessor[@"name"] != null)
                        {
                            data[@"Processor"] = moProcessor[@"name"].ToString();
                            App.IsRunningOnAMD = data[@"Processor"].Contains(@"AMD");
                        }
                    }

                    data[@"BetaMode"] = Settings.BetaMode.ToString();
                    data[@"DeveloperMode"] = Settings.DeveloperMode.ToString();

                    App.SubmitAnalyticTelemetryEvent(@"Hardware Info", data);
                }
                catch //(Exception e)
                {

                }
            };
            nbw.RunWorkerAsync();
        }

        internal void ShowFirstRunPanel()
        {
            var previewPanel = new FirstRunPanel();
            previewPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                // if user speeds through, this might not be available yet. oh well
                if (TutorialService.ServiceLoaded)
                {
                    new IntroTutorial().Show();
                }
            };
            ShowBusyControl(previewPanel);
        }

        private void UpdateBinkStatus(MEGame game)
        {
            var target = GetCurrentTarget(game);
            if (target == null)
            {
                switch (game)
                {
                    case MEGame.ME1:
                        ME1ASILoaderInstalled = false;
                        ME1ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                    case MEGame.ME2:
                        ME2ASILoaderInstalled = false;
                        ME2ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                    case MEGame.ME3:
                        ME3ASILoaderInstalled = false;
                        ME3ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                    case MEGame.LE1:
                        LE1ASILoaderInstalled = false;
                        LE1ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                    case MEGame.LE2:
                        LE2ASILoaderInstalled = false;
                        LE2ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                    case MEGame.LE3:
                        LE3ASILoaderInstalled = false;
                        LE3ASILoaderText = M3L.GetString(M3L.string_gameNotInstalled);
                        break;
                }

                return; //don't check or anything
            }


            string binkInstalledText = null;
            string binkNotInstalledText = null;

            if (game == MEGame.ME1)
            {
                binkInstalledText = M3L.GetString(M3L.string_binkAsiLoaderInstalled);
                binkNotInstalledText = M3L.GetString(M3L.string_binkAsiLoaderNotInstalled);
            }
            else if (game is MEGame.ME2 or MEGame.ME3)
            {
                binkInstalledText = M3L.GetString(M3L.string_binkAsiBypassInstalled);
                binkNotInstalledText = M3L.GetString(M3L.string_binkAsiBypassNotInstalled);
            }
            else if (game.IsLEGame())
            {
                binkInstalledText = M3L.GetString(M3L.string_bink2AsiLoaderInstalled);
                binkNotInstalledText = M3L.GetString(M3L.string_bink2AsiLoaderNotInstalled);
            }

            switch (game)
            {
                case MEGame.ME1:
                    ME1ASILoaderInstalled = target.IsBinkBypassInstalled();
                    ME1ASILoaderText = ME1ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case MEGame.ME2:
                    ME2ASILoaderInstalled = target.IsBinkBypassInstalled();
                    ME2ASILoaderText = ME2ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case MEGame.ME3:
                    ME3ASILoaderInstalled = target.IsBinkBypassInstalled();
                    ME3ASILoaderText = ME3ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case MEGame.LE1:
                    LE1ASILoaderInstalled = target.IsBinkBypassInstalled();
                    LE1ASILoaderText = LE1ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case MEGame.LE2:
                    LE2ASILoaderInstalled = target.IsBinkBypassInstalled();
                    LE2ASILoaderText = LE2ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
                case MEGame.LE3:
                    LE3ASILoaderInstalled = target.IsBinkBypassInstalled();
                    LE3ASILoaderText = LE3ASILoaderInstalled ? binkInstalledText : binkNotInstalledText;
                    break;
            }
        }

        /// <summary>
        /// Gets current target that matches the game. If selected target does not match, the first one in the list used (active). THIS CAN RETURN A NULL OBJECT!
        /// </summary>
        /// <param name="game">Game to find target for</param>
        /// <returns>Game matching target. If none
        /// is found, this return null.</returns>
        internal GameTargetWPF GetCurrentTarget(MEGame game)
        {
            if (SelectedGameTarget != null)
            {
                if (SelectedGameTarget.Game == game) return SelectedGameTarget;
            }

            return InstallationTargets.FirstOrDefault(x => x.Game == game);
        }

        /// <summary>
        /// Calls CheckAllModsForUpdates(). This method should be called from the UI thread.
        /// </summary>
        private void CheckAllModsForUpdatesWrapper()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Mod update check");
            nbw.DoWork += (a, b) => ModUpdater.Instance.CheckAllModsForUpdates();
            nbw.RunWorkerAsync();
        }

        private void PopulateTargets(GameTargetWPF selectedTarget = null)
        {
            RepopulatingTargets = true;
            InstallationTargets.ClearEx();
            SelectedGameTarget = null;
            MEDirectories.ReloadGamePaths(true); //this is redundant on the first boot but whatever.
            M3Log.Information(@"Populating game targets");
            var targets = new List<GameTargetWPF>();
            bool foundMe1Active = false;
            bool foundMe2Active = false;
            if (ME3Directory.DefaultGamePath != null && Directory.Exists(ME3Directory.DefaultGamePath))
            {
                var target = new GameTargetWPF(MEGame.ME3, ME3Directory.DefaultGamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    M3Log.Information(@"Current boot target for ME3: " + target.TargetPath);
                    targets.Add(target);
                    M3Utilities.AddCachedTarget(target);
                }
                else
                {
                    M3Log.Error(@"Current boot target for ME3 is invalid: " + failureReason);
                }
            }

            if (ME2Directory.DefaultGamePath != null && Directory.Exists(ME2Directory.DefaultGamePath))
            {
                var target = new GameTargetWPF(MEGame.ME2, ME2Directory.DefaultGamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    M3Log.Information(@"Current boot target for ME2: " + target.TargetPath);
                    targets.Add(target);
                    M3Utilities.AddCachedTarget(target);
                    foundMe2Active = true;
                }
                else
                {
                    M3Log.Error(@"Current boot target for ME2 is invalid: " + failureReason);
                }
            }

            if (ME1Directory.DefaultGamePath != null && Directory.Exists(ME1Directory.DefaultGamePath))
            {
                var target = new GameTargetWPF(MEGame.ME1, ME1Directory.DefaultGamePath, true);
                var failureReason = target.ValidateTarget();
                if (failureReason == null)
                {
                    M3Log.Information(@"Current boot target for ME1: " + target.TargetPath);
                    targets.Add(target);
                    M3Utilities.AddCachedTarget(target);
                    foundMe1Active = true;
                }
                else
                {
                    M3Log.Error(@"Current boot target for ME1 is invalid: " + failureReason);
                }
            }

            if (!string.IsNullOrWhiteSpace(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory) && Directory.Exists(LegendaryExplorerCoreLibSettings.Instance.LEDirectory))
            {
                // Load LE targets
                void loadLETarget(MEGame game, string defaultPath)
                {
                    var target = new GameTargetWPF(game, defaultPath, true);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        M3Log.Information($@"Current boot target for {game}: {target.TargetPath}");
                        targets.Add(target);
                    }
                    else
                    {
                        M3Log.Error($@"Current boot target for {game} at {target.TargetPath} is invalid: " + failureReason);
                    }
                }

                loadLETarget(MEGame.LELauncher, LEDirectory.LauncherPath);
                loadLETarget(MEGame.LE1, LE1Directory.DefaultGamePath);
                loadLETarget(MEGame.LE2, LE2Directory.DefaultGamePath);
                loadLETarget(MEGame.LE3, LE3Directory.DefaultGamePath);
            }

            // Read steam locations
            void addSteamTarget(string targetPath, bool foundActiveAlready, MEGame game)
            {
                if (!string.IsNullOrWhiteSpace(targetPath)
                    && Directory.Exists(targetPath)
                    && !targets.Any(x => x.TargetPath.Equals(targetPath, StringComparison.InvariantCultureIgnoreCase)))
                {
                    var target = new GameTargetWPF(game, targetPath, !foundActiveAlready);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        M3Log.Information($@"Found Steam game for {game}: " + target.TargetPath);
                        // Todo: Figure out how to insert at correct index
                        targets.Add(target);
                        M3Utilities.AddCachedTarget(target);
                    }
                    else
                    {
                        M3Log.Error($@"Steam version of {game} at {targetPath} is invalid: {failureReason}");
                    }
                }
            }

            // ME1
            addSteamTarget(M3Utilities.GetRegistrySettingString(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 17460",
                @"InstallLocation"), foundMe1Active, MEGame.ME1);

            // ME2
            addSteamTarget(M3Utilities.GetRegistrySettingString(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 24980",
                @"InstallLocation"), foundMe2Active, MEGame.ME2);

            // ME3
            addSteamTarget(M3Utilities.GetRegistrySettingString(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1238020",
                @"InstallLocation"), foundMe2Active, MEGame.ME3);

            // Legendary Edition
            var legendarySteamLoc = M3Utilities.GetRegistrySettingString(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1328670",
                @"InstallLocation");
            if (!string.IsNullOrWhiteSpace(legendarySteamLoc))
            {
                addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"Launcher"), false, MEGame.LELauncher);
                addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME1"), false, MEGame.LE1);
                addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME2"), false, MEGame.LE2);
                addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME3"), false, MEGame.LE3);
            }

            M3Log.Information(@"Loading cached targets");
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME3, targets));
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME2, targets));
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME1, targets));

            // Load LE cached targets
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE3, targets));
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE2, targets));
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE1, targets));
            targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LELauncher, targets));

            OrderAndSetTargets(targets, selectedTarget);
        }

        private void OrderAndSetTargets(List<GameTargetWPF> targets, GameTargetWPF selectedTarget = null)
        {
            // ORDER THE TARGETS
            //targets = targets.Where(x => x.Game.IsEnabledGeneration()).Distinct().ToList();
            var finalList = new List<GameTargetWPF>();

            //LE
            var aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE3 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE2 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE1 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LELauncher && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);

            // OT
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME3 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME2 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME1 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);

            if (targets.Count > finalList.Count)
            {
                finalList.Add(new GameTargetWPF(MEGame.Unknown, $@"==================={M3L.GetString(M3L.string_otherSavedTargets)}===================", false, true) { Selectable = false });
            }

            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE3 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE2 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE1 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LELauncher && !x.RegistryActive));

            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME3 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME2 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME1 && !x.RegistryActive));

            if (!InternalLoadedTargets.Any())
            {
                InternalLoadedTargets.ReplaceAll(finalList.Where(x => !x.IsCustomOption));
            }

            finalList = finalList.Where(x => x.IsCustomOption || x.Game.IsEnabledGeneration()).ToList();
            if (finalList.LastOrDefaultOut(out var lastTarget) && lastTarget.IsCustomOption)
            {
                // Trim last custom option
                finalList.Remove(lastTarget);
            }

            InstallationTargets.ReplaceAll(finalList.Where(x => x.IsCustomOption || x.Game.IsEnabledGeneration()));

            if (selectedTarget != null && finalList.FirstOrDefaultOut(x => x.TargetPath == selectedTarget.TargetPath, out var selTarget))
            {
                SelectedGameTarget = selTarget;
            }
            else if (!string.IsNullOrWhiteSpace(Settings.LastSelectedTarget) && InstallationTargets.FirstOrDefaultOut(x => !x.IsCustomOption && x.TargetPath.Equals(Settings.LastSelectedTarget), out var matchingTarget))
            {
                SelectedGameTarget = matchingTarget;
            }
            else
            {
                if (InstallationTargets.Count > 0)
                {
                    var firstSelectableTarget = InstallationTargets.FirstOrDefault(x => x.Selectable);
                    if (firstSelectableTarget != null)
                    {
                        SelectedGameTarget = firstSelectableTarget;
                    }
                }
            }

            //BackupService.SetInstallStatuses(InstallationTargets);
            RepopulatingTargets = false;
        }

        public async void OnSelectedModChanged()
        {
            if (SelectedMod != null)
            {
                SetWebsitePanelVisibility(SelectedMod.ModWebsite != Mod.DefaultWebsite);

                if (SelectedGameTarget == null || SelectedGameTarget.Game != SelectedMod.Game)
                {
                    // Update the target
                    var installTarget = InstallationTargets.FirstOrDefault(x => x.RegistryActive && x.Game == SelectedMod.Game);
                    if (installTarget != null)
                    {
                        SelectedGameTarget = installTarget;
                    }
                }

                if (SelectedMod.BannerBitmap == null)
                {
                    SelectedMod.LoadBannerImage(); // Method will check if it's null
                }

                VisitWebsiteText = SelectedMod.ModWebsite != Mod.DefaultWebsite ? M3L.GetString(M3L.string_interp_visitSelectedModWebSite, SelectedMod.ModName) : "";

                if (NexusModsUtilities.HasAPIKey)
                {
                    if (SelectedMod.NexusModID > 0)
                    {
                        if (SelectedMod.IsOwnMod)
                        {
                            CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseOwnMod);
                        }
                        else
                        {
                            CurrentModEndorsementStatus = M3L.GetString(M3L.string_gettingEndorsementStatus);

                            var endorsed = await SelectedMod.GetEndorsementStatus();
                            if (endorsed != null)
                            {
                                if (SelectedMod != null)
                                {
                                    //mod might have changed since we did BG thread wait.
                                    if (SelectedMod.CanEndorse)
                                    {
                                        UpdatedEndorsementString();
                                    }
                                    else
                                    {
                                        CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseMod);
                                    }
                                }
                            }
                            else
                            {
                                // null = self mod
                                CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseOwnMod);

                            }
                        }

                        EndorseSelectedModCommand.RaiseCanExecuteChanged();
                        //CommandManager.InvalidateRequerySuggested();
                    }
                    else
                    {
                        CurrentModEndorsementStatus = $@"{M3L.GetString(M3L.string_cannotEndorseMod)} ({M3L.GetString(M3L.string_notLinkedToNexusMods)})";
                    }
                }
                else
                {
                    CurrentModEndorsementStatus = $@"{M3L.GetString(M3L.string_cannotEndorseMod)} ({M3L.GetString(M3L.string_notAuthenticated)})";
                }
                //CurrentDescriptionText = newSelectedMod.DisplayedModDescription;
            }
            else
            {
                VisitWebsiteText = "";
                SetWebsitePanelVisibility(false);
                CurrentDescriptionText = DefaultDescriptionText;
            }

            CanApplyMod(); // This sets the text. Good design MG
        }

        private void UpdatedEndorsementString()
        {
            if (SelectedMod != null)
            {
                if (SelectedMod.IsEndorsed)
                {
                    CurrentModEndorsementStatus = M3L.GetString(M3L.string_modEndorsed);
                }
                else
                {
                    CurrentModEndorsementStatus = M3L.GetString(M3L.string_endorseMod);
                }
            }
        }

        private void SetWebsitePanelVisibility(bool open)
        {
            if (open != WebsitePanelStatus)
            {
                void done()
                {
                    WebsitePanelStatus = open;
                }

                ClipperHelper.ShowHideVerticalContent(VisitWebsitePanel, open, completionDelegate: done);
            }
        }

        private void RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            M3Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenModFolder_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                M3Utilities.OpenExplorer(SelectedMod.ModPath);
            }
        }

        private void OpenME3Tweaks_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(@"https://me3tweaks.com/");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutPanel();
            aboutWindow.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(aboutWindow);
        }

        private void ModManagerWindow_Closing(object sender, CancelEventArgs e)
        {
            //Settings.Save();
        }

        private void FailedMods_LinkClick(object sender, RequestNavigateEventArgs e)
        {
            var failedModsPanel = new FailedModsPanel(M3LoadedMods.Instance.FailedMods.ToList());
            failedModsPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is Mod failedmod)
                {
                    NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(FailedMods_LinkClick));
                    bw.DoWork += (a, b) => { ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new Mod[] { failedmod }), true); };
                    bw.RunWorkerAsync();
                }
            };
            ShowBusyControl(failedModsPanel);
        }

        private void OpenModsDirectory_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenExplorer(M3Utilities.GetModsDirectory());
        }

        private const int STARTUP_FAIL_CRITICAL_FILES_MISSING = 1;

        public void PerformStartupNetworkFetches(bool firstStartupCheck)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ContentCheckNetworkThread");
            bw.WorkerReportsProgress = true;
            bw.ProgressChanged += (sender, args) =>
            {
                //Help items loading
                if (args.UserState is List<SortableHelpElement> sortableHelpItems)
                {
                    setDynamicHelpMenu(sortableHelpItems);
                }
            };
            bw.DoWork += (a, b) =>
            {
                M3Log.Information(@"Start of content check network thread. First startup check: " + firstStartupCheck);

                BackgroundTask bgTask;
                #region STARTUP ONLY (ONE TIME)
                if (firstStartupCheck)
                {
                    // First boot does this in the background
                    UpdateBinkStatus(MEGame.ME1);
                    UpdateBinkStatus(MEGame.ME2);
                    UpdateBinkStatus(MEGame.ME3);
                    UpdateBinkStatus(MEGame.LE1);
                    UpdateBinkStatus(MEGame.LE2);
                    UpdateBinkStatus(MEGame.LE3);
                    //bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"EnsureCriticalFiles", M3L.GetString(M3L.string_downloadingRequiredFiles), M3L.GetString(M3L.string_requiredFilesDownloaded));
                    //if (!M3OnlineContent.EnsureCriticalFiles())
                    //{
                    //    //Critical files not loaded!
                    //    b.Result = STARTUP_FAIL_CRITICAL_FILES_MISSING;
                    //    bgTask.FinishedUIText = M3L.GetString(M3L.string_failedToDownloadRequiredFiles);
                    //    BackgroundTaskEngine.SubmitJobCompletion(bgTask);
                    //    return;
                    //}

                    //BackgroundTaskEngine.SubmitJobCompletion(bgTask);

                    var updateCheckTask = BackgroundTaskEngine.SubmitBackgroundJob(@"UpdateCheck", M3L.GetString(M3L.string_checkingForModManagerUpdates), M3L.GetString(M3L.string_completedModManagerUpdateCheck));
                    try
                    {
                        M3OnlineContent.FetchOnlineStartupManifest(Settings.BetaMode);
                        if (App.ServerManifest != null && int.TryParse(App.ServerManifest[@"latest_build_number"], out var latestServerBuildNumer))
                        {
                            if (latestServerBuildNumer > App.BuildNumber)
                            {
                                M3Log.Information(@"Found update for Mod Manager: Build " + latestServerBuildNumer);

                                Application.Current.Dispatcher.Invoke(delegate
                                {
                                    var updateAvailableDialog = new ProgramUpdateNotification();
                                    updateAvailableDialog.Close += (sender, args) => { ReleaseBusyControl(); };
                                    ShowBusyControl(updateAvailableDialog, true);
                                });
                            }
#if !DEBUG
                            else if (latestServerBuildNumer == App.BuildNumber)
                            {
                                if (App.ServerManifest.TryGetValue(@"build_md5", out var md5) && !string.IsNullOrWhiteSpace(md5))
                                {
                                    var localmd5 = MUtilities.CalculateMD5(App.ExecutableLocation);
                                    if (localmd5 != md5)
                                    {
                                        //Update is available.
                                        {
                                            M3Log.Information(@"MD5 of local exe doesn't match server version, minor update detected.");
                                            Application.Current.Dispatcher.Invoke(delegate
                                            {
                                                var updateAvailableDialog = new ProgramUpdateNotification(localmd5);
                                                updateAvailableDialog.UpdateMessage = M3L.GetString(M3L.string_interp_minorUpdateAvailableMessage, App.BuildNumber.ToString());
                                                updateAvailableDialog.Close += (sender, args) => { ReleaseBusyControl(); };
                                                ShowBusyControl(updateAvailableDialog, true);
                                            });
                                        }
                                    }
                                }
                            }
#endif
                            else
                            {
                                M3Log.Information(@"Mod Manager is up to date");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //Error checking for updates!
                        M3Log.Error(@"Checking for updates failed: " + App.FlattenException(e));
                        updateCheckTask.FinishedUIText = M3L.GetString(M3L.string_failedToCheckForUpdates);
                    }

                    BackgroundTaskEngine.SubmitJobCompletion(updateCheckTask);

                    if (App.ServerManifest != null)
                    {
                        M3ServiceLoader.TouchupServerManifest(this);
                    }
                    else
                    {
                        // load cached (will load nothing if there is no local file)
                        MixinHandler.LoadME3TweaksPackage();
                    }
                }

                #endregion

                M3ServiceLoader.LoadServices(this, bw);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedText))); // Update localized tip shown

                if (firstStartupCheck)
                {
                    bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"WritePermissions", M3L.GetString(M3L.string_checkingWritePermissions), M3L.GetString(M3L.string_checkedUserWritePermissions));
                    CheckTargetPermissions(true);
                    BackgroundTaskEngine.SubmitJobCompletion(bgTask);
                    if (Settings.ConfigureNXMHandlerOnBoot)
                    {
                        NexusModsUtilities.SetupNXMHandling();
                    }

                    // Setup initial tutorial messages.
                    ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods, Settings.OneTimeMessage_ModListIsNotListOfInstalledMods, true);
                }

                // Todo: Move to ME3TweaksCore?
                if (MOnlineContent.CanFetchContentThrottleCheck())
                {
                    Settings.LastContentCheck = DateTime.Now;
                }

                M3Log.Information(@"End of content check network thread");
                b.Result = 0; //all good
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    // Log is handled in internal class
                }
                else if (b.Result is int i)
                {
                    if (i != 0)
                    {
                        switch (i)
                        {
                            case STARTUP_FAIL_CRITICAL_FILES_MISSING:
                                var res = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogCriticalFilesMissing), M3L.GetString(M3L.string_requiredFilesNotDownloaded), MessageBoxButton.OK, MessageBoxImage.Error);
                                Environment.Exit(1);
                                break;
                        }
                    }
                    else
                    {
                        ContentCheckInProgress = false;
                    }
                }

                if (firstStartupCheck)
                {
                    M3Utilities.WriteExeLocation();
                    handleInitialPending();
                }

                if (Settings.GenerationSettingOT)
                {
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"BackupCheck");
                    nbw.DoWork += (a, b) =>
                    {
                        var me1CheckRequired = BackupService.GetGameBackupPath(MEGame.ME1) == null && BackupService.GetGameBackupPath(MEGame.ME1, false) != null;
                        var me2CheckRequired = BackupService.GetGameBackupPath(MEGame.ME2) == null && BackupService.GetGameBackupPath(MEGame.ME2, false) != null;
                        var me3CheckRequired = BackupService.GetGameBackupPath(MEGame.ME3) == null && BackupService.GetGameBackupPath(MEGame.ME3, false) != null;

                        if (me1CheckRequired || me2CheckRequired || me3CheckRequired)
                        {
                            var bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"BackupCheck", M3L.GetString(M3L.string_checkingBackups), M3L.GetString(M3L.string_finishedCheckingBackups));
                            // TODO: NEEDS ACTIVITY SET!
                            if (me1CheckRequired) VanillaDatabaseService.CheckAndTagBackup(MEGame.ME1);
                            if (me2CheckRequired) VanillaDatabaseService.CheckAndTagBackup(MEGame.ME2);
                            if (me3CheckRequired) VanillaDatabaseService.CheckAndTagBackup(MEGame.ME3);

                            BackgroundTaskEngine.SubmitJobCompletion(bgTask);
                        }
                    };
                    nbw.RunWorkerAsync();
                }

                CommandManager.InvalidateRequerySuggested(); //refresh bindings that depend on this
            };
            ContentCheckInProgress = true;
            bw.RunWorkerAsync();
        }

        private void handleInitialPending()
        {
            // Will do nothing if there's something else that needs done.
            AttemptPendingGameBoot();

            if (CommandLinePending.PendingGame is { } testGame && !testGame.IsLEGame() && !testGame.IsOTGame())
            {
                M3Log.Error($@"Invalid game specified on the command line: {testGame}");
                CommandLinePending.PendingGameBoot = false;
                CommandLinePending.ClearGameDependencies();
            }
            try
            {
                if (CommandLinePending.PendingNXMLink != null)
                {
                    showNXMDownloader(CommandLinePending.PendingNXMLink);
                }

                if (CommandLinePending.PendingInstallBink && CommandLinePending.PendingGame != null)
                {
                    CommandLinePending.PendingInstallBink = false;
                    GameTargetWPF t = GetCurrentTarget(CommandLinePending.PendingGame.Value);
                    if (t != null)
                    {
                        M3Log.Information($@"Installing Bink Bypass (command line request) for {CommandLinePending.PendingGame.Value}");
                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"BinkInstallAutomated", M3L.GetString(M3L.string_installingBinkASILoader), M3L.GetString(M3L.string_installedBinkASILoader));
                        try
                        {
                            t.InstallBinkBypass(true);
                        }
                        catch (Exception)
                        {
                            task.FinishedUIText = "Failed to install bink ASI loader";
                        }
                        BackgroundTaskEngine.SubmitJobCompletion(task); // This is just so there's some visual feedback to the user
                    }
                    CommandLinePending.ClearGameDependencies();
                }

                if (CommandLinePending.PendingInstallASIID > 0 && CommandLinePending.PendingGame != null)
                {
                    var game = CommandLinePending.PendingGame.Value;
                    if (!game.IsOTGame() && !game.IsLEGame())
                    {
                        M3Log.Error($@"Cannot install ASI to game {game} (command line request)!");
                    }
                    else
                    {
                        GameTargetWPF t = GetCurrentTarget(CommandLinePending.PendingGame.Value);
                        if (t != null)
                        {
                            ASIManager.InstallASIToTargetByGroupID(CommandLinePending.PendingInstallASIID, @"Automated command line request", t);
                        }
                    }
                    CommandLinePending.ClearGameDependencies();
                }

                if (CommandLinePending.PendingAutoModInstallPath != null && File.Exists(CommandLinePending.PendingAutoModInstallPath))
                {
                    Mod m = new Mod(CommandLinePending.PendingAutoModInstallPath, MEGame.Unknown);
                    if (m.ValidMod)
                    {
                        GameTargetWPF t = GetCurrentTarget(m.Game);
                        if (t != null)
                        {
                            ApplyMod(m, t, installCompletedCallback: installed =>
                            {
                                CommandLinePending.PendingAutoModInstallPath = null;
                                if (installed)
                                {
                                    // Will do nothing if there is no pending game boot.
                                    AttemptPendingGameBoot();
                                }
                                CommandLinePending.ClearGameDependencies();
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error handling pending command line actions: {e.Message}");
                M3Log.Error(e.FlattenException());
            }

            //App.PendingGameBoot = null; // this is not cleared here as it will be used at end of applymod above
            CommandLinePending.PendingNXMLink = null;
        }

        /// <summary>
        /// Attempts to boot the game if there is any pending request to boot the game
        /// </summary>
        private void AttemptPendingGameBoot()
        {
            if (CommandLinePending.CanBootGame())
            {
                var bootTarget = GetCurrentTarget(CommandLinePending.PendingGame.Value);
                if (bootTarget != null)
                {
                    InternalStartGame(bootTarget);
                }

                CommandLinePending.PendingGameBoot = false;
                CommandLinePending.ClearGameDependencies();
            }
        }

        //string convertKey(string pcKey, StringRef sref)
        //{
        //    switch (pcKey)
        //    {
        //        case "[Shared_SquadMove1]":
        //            return "[XBoxB_Btn_DPadL]";
        //        case "[Shared_SquadMove2]":
        //            return "[XBoxB_Btn_DPadR]";
        //        case "[Shared_Melee]":
        //            return "[XBoxB_Btn_B]";
        //        default:
        //            Debug.WriteLine("Unknown UI key " + pcKey);
        //            break;
        //    }

        //    return null;
        //}

        private void debugMethod()
        {
            //var mixinP = @"X:\m3modlibrary\ME3\RealisticGravOLD";
            //foreach (var mp in Directory.GetFiles(mixinP, "*.pcc", SearchOption.AllDirectories))
            //{
            //    var packageName = Path.GetFileName(mp);
            //    var dirname = Directory.GetParent(packageName).Parent.Parent.Name;


            //    MemoryStream fileData = null;
            //    if (dirname == "BASEGAME")
            //    {
            //        fileData = VanillaDatabaseService.FetchBasegameFile(MEGame.ME3, packageName);
            //    }
            //    else
            //    {
            //        var map = ModJob.GetHeadersToDLCNamesMap(MEGame.ME3);
            //        var header = ModMakerCompiler.DefaultFoldernameToHeader(dirname);
            //        fileData = VanillaDatabaseService.FetchFileFromVanillaSFAR(map[header], packageName);
            //    }

            //    if (dirname == "BASEGAME")
            //    {
            //        var package = MEPackageHandler.OpenMEPackageFromStream(fileData);
            //        fileData = package.SaveToStream(false, false, true);
            //    }
            //}
        }

        /// <summary>
        /// Refreshes the dynamic help list
        /// </summary>
        /// <param name="sortableHelpItems"></param>
        private void setDynamicHelpMenu(IReadOnlyList<SortableHelpElement> sortableHelpItems)
        {
            //Replacing the dynamic help menu
            //DynamicHelp_MenuItem.Items.RemoveAll(x=>x.Tag is string str && str == "DynamicHelp");

            var dynamicMenuItems = RecursiveBuildDynamicHelpMenuItems(sortableHelpItems);

            //Clear old items out
            for (int i = HelpMenuItem.Items.Count - 1; i > 0; i--)
            {
                if (HelpMenuItem.Items[i] is MenuItem menuItem && menuItem.Tag is string str && str == @"DynamicHelp")
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

        internal void SetTipsForLanguage()
        {

        }

        private List<MenuItem> RecursiveBuildDynamicHelpMenuItems(IReadOnlyList<SortableHelpElement> sortableHelpItems)
        {
            //Todo: Localized version
            List<MenuItem> dynamicMenuItems = new List<MenuItem>();
            foreach (var item in sortableHelpItems)
            {
                MenuItem m = new MenuItem()
                {
                    Header = item.Title,
                    ToolTip = item.ToolTip,
                    Tag = @"DynamicHelp"
                };
                if (!string.IsNullOrEmpty(item.URL))
                {
                    //URL HelpItem
                    m.Click += (o, eventArgs) => M3Utilities.OpenWebpage(item.URL);
                }
                else if (!string.IsNullOrEmpty(item.ModalTitle))
                {
                    //Modal dialog
                    item.ModalText = M3Utilities.ConvertBrToNewline(item.ModalText);
                    m.Click += (o, eventArgs) => { new DynamicHelpItemModalWindow(item) { Owner = this }.ShowDialog(); };
                }

                // 06/05/2022 - Support a font awesome icon enumeration value
                if (!string.IsNullOrWhiteSpace(item.FontAwesomeIconResource) && Enum.TryParse<EFontAwesomeIcon>(item.FontAwesomeIconResource, out var icon))
                {
                    var ia = new ImageAwesome()
                    {
                        Icon = icon,
                        Height = 16,
                        Width = 16,
                        Style = (Style)FindResource(@"EnableDisableImageStyle")
                    };
                    ia.SetResourceReference(ImageAwesome.ForegroundProperty, AdonisUI.Brushes.ForegroundBrush);

                    m.Icon = ia;
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
            if (sender == GenerateStarterKitLE1_MenuItem) g = MEGame.LE1;
            if (sender == GenerateStarterKitLE2_MenuItem) g = MEGame.LE2;
            if (sender == GenerateStarterKitLE3_MenuItem) g = MEGame.LE3;
            new StarterKitGeneratorWindow(g) { Owner = this }.ShowDialog();
        }

        private void LaunchExternalTool_Clicked(object sender, RoutedEventArgs e)
        {
            string tool = null;

            // ME3Tweaks passthrough boot
            if (sender == ALOTInstaller_MenuItem)
            {
                BootToolPathPassthrough(ExternalToolLauncher.ALOTInstaller);
                return;
            }
            if (sender == MassEffect2Randomizer_MenuItem)
            {
                BootToolPathPassthrough(ExternalToolLauncher.ME2R);
                return;
            }

            // Generic boot
            if (sender == MassEffectRandomizer_MenuItem) tool = ExternalToolLauncher.MER;
            if (sender == LegendaryExplorerStable_MenuItem) tool = ExternalToolLauncher.LegendaryExplorer;
            if (sender == LegendaryExplorerBeta_MenuItem) tool = ExternalToolLauncher.LegendaryExplorer_Beta;
            if (sender == MassEffectModder_MenuItem) tool = ExternalToolLauncher.MEM;
            if (sender == MassEffectModderLE_MenuItem) tool = ExternalToolLauncher.MEM_LE;
            //if (sender == EGMSettings_MenuItem) tool = ExternalToolLauncher.EGMSettings; //EGM settings has it's own command and it not invoked through this menu
            if (tool == null) throw new Exception(@"LaunchExternalTool handler set but no relevant tool was specified! This is a bug. Please report it to Mgamerz on Discord");
            LaunchExternalTool(tool);
        }

        private void LaunchExternalTool(string tool, string arguments = null)
        {
            if (tool != null)
            {
                TelemetryInterposer.TrackEvent(@"Launched external tool", new Dictionary<string, string>()
                {
                    {@"Tool name", tool},
                    {@"Arguments", arguments}
                });
                var exLauncher = new ExternalToolLauncher(tool, arguments);
                exLauncher.Close += (a, b) => { ReleaseBusyControl(); };
                ShowBusyControl(exLauncher);
            }
        }

        private void OpenASIManager()
        {
            TelemetryInterposer.TrackEvent(@"Launched ASI Manager", new Dictionary<string, string>()
            {
                {@"Invocation method", @"Menu"}
            });
            var exLauncher = new ASIManagerPanel(SelectedGameTarget);
            exLauncher.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(exLauncher);
        }

        private bool RepopulatingTargets;

        public void OnSelectedGameTargetChanged()
        {
            if (!RepopulatingTargets && SelectedGameTarget != null)
            {
                //Settings.Save();
                if (!SelectedGameTarget.RegistryActive)
                {
                    UpdateBinkStatus(SelectedGameTarget.Game);
                    try
                    {
                        var hresult = UpdateBootTarget(SelectedGameTarget);
                        if (hresult == -3) return; //do nothing.
                        if (hresult == 0)
                        {
                            //rescan
                            PopulateTargets(SelectedGameTarget);
                            SelectedGameTarget.UpdateLODs(Settings.AutoUpdateLODs2K);
                        }

                        TelemetryInterposer.TrackEvent(@"Changed to non-active target", new Dictionary<string, string>()
                        {
                            {@"New target", SelectedGameTarget.Game.ToString()},
                            {@"ALOT Installed", SelectedGameTarget.TextureModded.ToString()}
                        });
                    }
                    catch (Win32Exception ex)
                    {
                        M3Log.Warning(@"Win32 exception occurred updating boot target. User maybe pressed no to the UAC dialog?: " + ex.Message);
                    }
                }

                Settings.LastSelectedTarget = SelectedGameTarget?.TargetPath;
            }
        }

        private void UploadLog_Click(object sender, RoutedEventArgs e)
        {
            var logUploaderUI = new LogUploader();
            logUploaderUI.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(logUploaderUI);
        }


        public string CurrentModEndorsementStatus { get; private set; } = M3L.GetString(M3L.string_endorseMod);
        public bool IsEndorsingMod { get; private set; }
        public bool CanOpenMEIM()
        {
            //ensure not already open
            foreach (var window in Application.Current.Windows)
            {
                if (window is ME1IniModder) return false;
            }

            var installed = InstallationTargets.Any(x => x.Game == MEGame.ME1);
            if (installed)
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOGame.ini");
                return File.Exists(iniFile);
            }

            return false;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool isFirst = true;
                foreach (var file in files)
                {
                    #region Remove once stable
                    if (!Settings.BetaMode && !isFirst)
                        return; // You can only do one file at a time right now if you're not in beta mode as this might have exceptions.
                    isFirst = false;
                    #endregion

                    string ext = Path.GetExtension(file).ToLower();
                    M3Log.Information(@"File dropped onto interface: " + file);
                    switch (ext)
                    {
                        case @".rar":
                        case @".7z":
                        case @".zip":
                        case @".exe":
                            App.SubmitAnalyticTelemetryEvent(@"User opened mod archive for import", new Dictionary<string, string>
                            {
                                { @"Method", @"Drag & drop" },
                                { @"Filename", Path.GetFileName(file) }
                            });
                            openModImportUI(file);
                            break;
                        //TPF, .mod, .mem
                        case @".tpf":
                        case @".mod":
                        case @".mem":
                            App.SubmitAnalyticTelemetryEvent(@"User redirected to MEM/ALOT Installer", new Dictionary<string, string> { { @"Filename", Path.GetFileName(file) } });
                            M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialog_installingTextureMod, ext), M3L.GetString(M3L.string_nonModManagerModFound), MessageBoxButton.OK, MessageBoxImage.Warning);
                            break;
                        case @".me2mod":
                            App.SubmitAnalyticTelemetryEvent(@"User opened me2mod file", new Dictionary<string, string> { { @"Filename", Path.GetFileName(file) } });
                            openModImportUI(file);
                            break;
                        case @".xaml":
                            if (Settings.DeveloperMode)
                            {
                                LoadExternalLocalizationDictionary(file);
                            }

                            break;
                        case @".extractedbin":
                            {
                                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                                //var magic = fs.ReadInt32();
                                //fs.Dispose();
                                //if (magic is 0x666D726D or 0x1B) //fmrm (backwards) (ME3), 0x1B (LE1 (sigh))
                                //{

                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Compiler");
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedCompiler", M3L.GetString(M3L.string_compilingCoalescedFile), M3L.GetString(M3L.string_compiledCoalescedFile));
                                nbw.DoWork += (a, b) =>
                                {
                                    var dest = Path.Combine(Directory.GetParent(file).FullName, File.ReadAllLines(file)[0]);
                                    M3Log.Information($@"Compiling coalesced file: {file} -> {dest}");
                                    CoalescedConverter.Convert(CoalescedConverter.CoalescedType.ExtractedBin, file, dest);
                                    M3Log.Information(@"Compiled coalesced file");
                                };
                                nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                nbw.RunWorkerAsync();
                                // }
                            }
                            break;
                        case @".bin":
                            //Check for Coalesced
                            {
                                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                                var magic = fs.ReadInt32();
                                fs.Dispose();
                                if (magic is 0x666D726D or 0x1B or 0x1C or 0x1E) //fmrm (backwards) (ME3), 0x1B (LE1), 0x1C (LE2 count or something...) 0x1E (LE2) (sigh)
                                {

                                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Decompiler");
                                    var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedDecompile", M3L.GetString(M3L.string_decompilingCoalescedFile), M3L.GetString(M3L.string_decompiledCoalescedFile));
                                    nbw.DoWork += (a, b) =>
                                    {
                                        var dest = Path.Combine(Directory.GetParent(file).FullName, Path.GetFileNameWithoutExtension(file));
                                        M3Log.Information($@"Decompiling coalesced file: {file} -> {dest}");
                                        CoalescedConverter.Convert(CoalescedConverter.CoalescedType.Binary, file, dest);
                                        M3Log.Information(@"Decompiled coalesced file");
                                    };
                                    nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                    nbw.RunWorkerAsync();
                                }
#if DEBUG && !AZURE
                                // TOC DUMP
                                else if (magic is 0x3AB70C13)
                                {
                                    TOCBinFile tbf = new TOCBinFile(file);
                                    tbf.DumpTOC();
                                }
#endif
                            }
                            break;
                        case @".xml":
                            //Check if it's ModMaker sideload, coalesced manifest, or TLK
                            {
                                try
                                {
                                    var xmldocument = XDocument.Load(file);
                                    var rootElement = xmldocument.Root;
                                    if (rootElement.Name == @"ModMaker")
                                    {
                                        //Modmaker Mod, sideload
                                        var modmakerPanel = new ModMakerPanel()
                                        {
                                            LocalFileOption = true,
                                            LocalFilePath = file
                                        };

                                        modmakerPanel.Close += (a, b) =>
                                        {
                                            ReleaseBusyControl();
                                            if (b.Data is Mod m)
                                            {
                                                M3LoadedMods.Instance.LoadMods(m);
                                            }
                                        };
                                        ShowBusyControl(modmakerPanel);
                                        break;
                                    }



                                    if (rootElement.Name == @"CoalesceFile")
                                    {
                                        bool failedToCompileCoalesced = false;

                                        void errorCompilingCoalesced(string message)
                                        {
                                            Application.Current.Dispatcher.Invoke(delegate
                                            {
                                                failedToCompileCoalesced = true;
                                                M3L.ShowDialog(this, message, M3L.GetString(M3L.string_errorCompilingCoalesced), MessageBoxButton.OK, MessageBoxImage.Error);
                                            });
                                        }

                                        //Coalesced manifest
                                        NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Compiler");
                                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedCompile", M3L.GetString(M3L.string_compilingCoalescedFile), M3L.GetString(M3L.string_compiledCoalescedFile));
                                        nbw.DoWork += (a, b) =>
                                        {
                                            var dest = Path.Combine(Directory.GetParent(file).FullName, rootElement.Attribute(@"name").Value);
                                            M3Log.Information($@"Compiling coalesced file: {file} -> {dest}");
                                            try
                                            {
                                                CoalescedConverter.ConvertToBin(file, dest);
                                                M3Log.Information(@"Compiled coalesced file");
                                            }
                                            catch (Exception e)
                                            {
                                                M3Log.Error($@"Error compiling Coalesced file: {e.Message}:");
                                                M3Log.Error(App.FlattenException(e));
                                                errorCompilingCoalesced(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingCoalsecedFileX, e.Message));
                                            }
                                        };
                                        nbw.RunWorkerCompleted += (a, b) =>
                                        {
                                            if (failedToCompileCoalesced) task.FinishedUIText = M3L.GetString(M3L.string_errorCompilingCoalesced);
                                            BackgroundTaskEngine.SubmitJobCompletion(task);
                                        };
                                        nbw.RunWorkerAsync();
                                        break;
                                    }

                                    bool failedToCompileTLK = false;

                                    void errorCompilingTLK(string message)
                                    {
                                        Application.Current.Dispatcher.Invoke(delegate
                                        {
                                            failedToCompileTLK = true;
                                            M3L.ShowDialog(this, message, M3L.GetString(M3L.string_errorCompilingTLK), MessageBoxButton.OK, MessageBoxImage.Error);
                                        });
                                    }

                                    // Tankmaster's uses a capital T where ME3Explorer used lowercase t
                                    if (rootElement.Name == @"TlkFile")
                                    {
                                        //TLK file - ensure it's the manifest one
                                        var sourceName = rootElement.Attribute(@"source");
                                        if (sourceName != null)
                                        {
                                            //This is a manifest file
                                            /*
                                             * Manifest File
                                             * Folder with same name
                                             * |-> TLK.xml files
                                             */
                                            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TLKTranspiler - CompileTankmaster");
                                            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile", M3L.GetString(M3L.string_compilingTLKFile), M3L.GetString(M3L.string_compiledTLKFile));
                                            nbw.DoWork += (a, b) => { TLKTranspiler.CompileTLKManifest(file, rootElement, errorCompilingTLK); };
                                            nbw.RunWorkerCompleted += (a, b) =>
                                            {
                                                if (failedToCompileTLK) task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                                BackgroundTaskEngine.SubmitJobCompletion(task);
                                            };
                                            nbw.RunWorkerAsync();
                                        }
                                        else
                                        {
                                            //Is this a straight up TLK?
                                            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TLKTranspiler - CompileTankmaster");
                                            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile", M3L.GetString(M3L.string_compilingTLKFile), M3L.GetString(M3L.string_compiledTLKFile));
                                            nbw.DoWork += (a, b) => { TLKTranspiler.CompileTLKManifestStrings(file, rootElement, errorCompilingTLK); };
                                            nbw.RunWorkerCompleted += (a, b) =>
                                            {
                                                if (failedToCompileTLK) task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                                BackgroundTaskEngine.SubmitJobCompletion(task);
                                            };
                                            nbw.RunWorkerAsync();
                                        }
                                    }
                                    else if (rootElement.Name == @"tlkFile") //ME3Explorer style
                                    {
                                        NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TLKTranspiler - CompileME3Exp");
                                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile", M3L.GetString(M3L.string_compilingTLKFile), M3L.GetString(M3L.string_compiledTLKFile));
                                        nbw.DoWork += (a, b) => { TLKTranspiler.CompileTLKME3Explorer(file, rootElement, errorCompilingTLK); };
                                        nbw.RunWorkerCompleted += (a, b) =>
                                        {
                                            if (failedToCompileTLK) task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                            BackgroundTaskEngine.SubmitJobCompletion(task);
                                        };
                                        nbw.RunWorkerAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    M3Log.Error(@"Error loading XML file that was dropped onto UI: " + ex.Message);
                                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorReadingXmlFileX, ex.Message), M3L.GetString(M3L.string_errorReadingXmlFile), MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            break;
                        case @".tlk":
                            {
                                //Break down into xml file
                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TLK decompiler");
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TLKDecompile", M3L.GetString(M3L.string_decompilingTLKFile), M3L.GetString(M3L.string_decompiledTLKFile));
                                nbw.DoWork += (a, b) =>
                                {
                                    var dest = Path.Combine(Directory.GetParent(file).FullName, Path.GetFileNameWithoutExtension(file) + @".xml");
                                    M3Log.Information($@"Decompiling TLK file: {file} -> {dest}");
                                    var tf = new ME2ME3TalkFile(file);
                                    tf.SaveToXML(dest);
                                    M3Log.Information(@"Decompiled TLK file");
                                };
                                nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                nbw.RunWorkerAsync();

                            }
                            break;
                        case @".par":
                            {
#if DEBUG
                                var contents = PARTools.DecodePAR(File.ReadAllBytes(file));
                                Debug.WriteLine(contents);
#endif
                            }
                            break;
                        case @".json":
                            {
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"M3MCompile", M3L.GetString(M3L.string_compilingMergemod),
                                    M3L.GetString(M3L.string_compiledMergemod));
                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"MergeModCompiler");
                                nbw.DoWork += (o, args) =>
                                {
                                    MergeModLoader.SerializeManifest(file, 1);
                                };
                                nbw.RunWorkerCompleted += (o, args) =>
                                {
                                    if (args.Error != null)
                                    {
                                        task.FinishedUIText = M3L.GetString(M3L.string_failedToCompileMergemod);
                                        BackgroundTaskEngine.SubmitJobCompletion(task);
                                        M3Log.Error($@"Error compiling m3m mod file: {args.Error.Message}");
                                        M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorCompilingm3mX, args.Error.Message),
                                            M3L.GetString(M3L.string_errorCompilingm3m), MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                                    }
                                    else
                                    {
                                        BackgroundTaskEngine.SubmitJobCompletion(task);
                                    }
                                };
                                nbw.RunWorkerAsync();
                            }
                            break;
                        case @".m3m":
                            try
                            {
                                MergeModLoader.DecompileM3M(file);
                            }
                            catch (Exception ex)
                            {
                                M3Log.Error($@"Error decompiling m3m mod file: {ex.Message}");
                                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorCompilingm3mX, ex.Message), M3L.GetString(M3L.string_errorCompilingm3m), MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            break;
                        case @".m3za":
                            try
                            {
                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"M3ZA decompressor");
                                var fname = Path.GetFileName(file);
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"M3ZADecompress", M3L.GetString(M3L.string_interp_decompressingFname, fname), M3L.GetString(M3L.string_interp_decompressedFname, fname));
                                nbw.DoWork += (a, b) =>
                                {
                                    void progress(int done, int total)
                                    {
                                        int percent = (int)Math.Round(done * 100.0f / total);
                                        BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, M3L.GetString(M3L.string_interp_decompressingFnamePercent, fname, percent));
                                    }

                                    using var f = File.OpenRead(file);
                                    var archive = CompressedTLKMergeData.ReadCompressedTlkMergeFile(f, true);
                                    var completed = archive.DecompressArchiveToDisk(Directory.GetParent(file).FullName, archive.LoadedCompressedData, progress);
                                    if (!completed)
                                    {
                                        task.FinishedUIText = M3L.GetString(M3L.string_interp_failedToDecompressFname, fname);
                                    }
                                };
                                nbw.RunWorkerCompleted += (a, b) =>
                                {

                                    BackgroundTaskEngine.SubmitJobCompletion(task);
                                };
                                nbw.RunWorkerAsync();

                            }
                            catch (Exception ex2)
                            {
                                M3Log.Exception(ex2, $@"Error decompressing {file}:");
                            }

                            break;
                    }
                }
            }
        }

        private void openModImportUI(string archiveFile, Stream archiveStream = null, bool priority = false)
        {
            M3Log.Information(@"Opening Mod Archive Importer for file " + archiveFile);
            var modInspector = new ModArchiveImporter(archiveFile, archiveStream);
            modInspector.Close += (a, b) =>
            {
                if (!HasQueuedPanel())
                {
                    // No more batch panels so we should handle the result on Release
                    HandleBatchPanelResult = true;
                }

                // Mods that have been imported will be in ModsToCheckForUpdates, which is handled by PanelResult
                ReleaseBusyControl();

                // This is kind of a hack for mod inspector, but it doesn't really fit in panel result's purpose
                if (b.Data is (Mod compressedModToInstall, bool compressed))
                {
                    var installTarget = InstallationTargets.FirstOrDefault(x => x.Game == compressedModToInstall.Game);
                    if (installTarget != null)
                    {
                        SelectedGameTarget = installTarget;
                        ApplyMod(compressedModToInstall, installCompressed: compressed);
                    }
                    else
                    {
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_cannotInstallModGameNotInstalled, compressedModToInstall.Game.ToGameName()), M3L.GetString(M3L.string_gameNotInstalled), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            BatchPanelResult = new PanelResult();
            HandleBatchPanelResult = false;
            ShowBusyControl(modInspector, priority);
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
                    if (!Settings.DeveloperMode || ext != @".xaml") //dev mode supports .xaml file drop for localization
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }


        private void SyncPlotManagerForGame(object obj)
        {
            if (obj is MEGame game)
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    SyncPlotManagerForTarget(target);
                }
                else
                {
                    M3Log.Error(@"SyncPlotManagerForGame game target was null! This shouldn't be possible");
                }
            }
        }

        private void SyncPlotManagerForTarget(GameTargetWPF target)
        {
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"SyncPlotManager",
                M3L.GetString(M3L.string_interp_syncingPlotManagerForGame, target.Game.ToGameName()),
                M3L.GetString(M3L.string_interp_syncedPlotManagerForGame, target.Game.ToGameName()));
            var pmuUI = new PlotManagerUpdatePanel(target);
            pmuUI.Close += (a, b) =>
            {
                BackgroundTaskEngine.SubmitJobCompletion(task);
                ReleaseBusyControl();
            };
            ShowBusyControl(pmuUI);
        }

        private void RunAutoTOCOnGame(object obj)
        {
            if (obj is MEGame game)
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    AutoTOCTarget(target);
                }
                else
                {
                    M3Log.Error(@"AutoTOC game target was null! This shouldn't be possible");
                }
            }
        }

        private void AutoTOCTarget(GameTargetWPF target, bool showInStatusBar = true)
        {
            BackgroundTask task = showInStatusBar ? BackgroundTaskEngine.SubmitBackgroundJob(@"AutoTOC", M3L.GetString(M3L.string_runningAutoTOC),
                    M3L.GetString(M3L.string_ranAutoTOC)) : null;
            var autoTocUI = new AutoTOC(target);
            autoTocUI.Close += (a, b) =>
            {
                if (showInStatusBar)
                {
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                }
                ReleaseBusyControl();
            };
            ShowBusyControl(autoTocUI);
        }

        internal void SetTheme()
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, Settings.DarkTheme ? ResourceLocator.DarkColorScheme : ResourceLocator.LightColorScheme);
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(@"https://github.com/ME3Tweaks/ME3TweaksModManager/tree/master/documentation");
        }

        private void OpenMemoryAnalyzer_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            new MemoryAnalyzer().Show();
#endif
        }

        private void ChangeLanguage_Clicked(object sender, RoutedEventArgs e)
        {
            string lang = @"int";
            if (sender == LanguageINT_MenuItem)
            {
                lang = @"int";
            }
            else if (sender == LanguagePOL_MenuItem)
            {
                lang = @"pol";
            }
            else if (sender == LanguageRUS_MenuItem)
            {
                lang = @"rus";
            }
            else if (sender == LanguageDEU_MenuItem)
            {
                lang = @"deu";
            }
            else if (sender == LanguageBRA_MenuItem)
            {
                lang = @"bra";
            }
            else if (sender == LanguageITA_MenuItem)
            {
                lang = @"ita";
            }
            //else if (sender == LanguageESN_MenuItem)
            //{
            //    lang = @"esn";
            //}
            //else if (sender == LanguageFRA_MenuItem)
            //{
            //    lang = @"fra";
            //}
            //else if (sender == LanguageCZE_MenuItem)
            //{
            //    lang = @"cze";
            //}
            //else if (sender == LanguageKOR_MenuItem)
            //{
            //    lang = @"kor";
            //}
            SetApplicationLanguageAsync(lang, false);
        }

        /// <summary>
        /// Sets the UI language synchronously, typically before we have a way to schedule onto the UI thread (e.g. UI thread has not started)
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="startup"></param>
        /// <param name="forcedDictionary"></param>
        public void SetApplicationLanguage(string lang, bool startup, ResourceDictionary forcedDictionary = null)
        {
            M3Log.Information(@"Setting language to " + lang);
            M3Localization.InternalSetLanguage(lang, forcedDictionary, startup).Wait();
            RefreshMainUIStrings(lang, startup);
        }

        /// <summary>
        /// Sets the UI language on a background thread.
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="startup"></param>
        /// <param name="forcedDictionary"></param>
        public void SetApplicationLanguageAsync(string lang, bool startup, ResourceDictionary forcedDictionary = null)
        {
            //Application.Current.Dispatcher.Invoke(async () =>
            //{
            //Set language.
            Stopwatch sw = new Stopwatch();
            Task.Run(() =>
            {
                sw.Start();
                M3Localization.InternalSetLanguage(lang, forcedDictionary, startup).Wait();
            }).ContinueWithOnUIThread(x =>
            {
                // Debug.WriteLine($@"ChangeLangAsync time: {sw.ElapsedMilliseconds}ms");
                RefreshMainUIStrings(lang, startup);
                // Debug.WriteLine($@"ChangeLangAsync time post-ui refresh: {sw.ElapsedMilliseconds}ms");
            });
        }

        /// <summary>
        /// Triggers UI strings to rebind when a language change has occurred
        /// </summary>
        /// <param name="startup"></param>
        /// <param name="lang"></param>
        private void RefreshMainUIStrings(string lang, bool startup)
        {
            App.CurrentLanguage = Settings.Language = lang;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedText))); // Update localized tip shown
            RefreshNexusStatus(true);
            try
            {
                var localizedHelpItems = DynamicHelpService.GetHelpItems(lang);
                setDynamicHelpMenu(localizedHelpItems);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Could not set localized dynamic help: " + e.Message);
            }

            if (SelectedMod != null)
            {
                // This will force strings to update
                var sm = SelectedMod;
                SelectedMod = null;
                SelectedMod = sm;
            }

            if (!startup)
            {
                //if (forcedDictionary == null)
                //{
                //Settings.Save(); //save this language option
                //}

                AuthToNexusMods(languageUpdateOnly: true).Wait(); //this call will immediately return
                M3LoadedMods.Instance.FailedMods.RaiseBindableCountChanged();
                CurrentOperationText = M3L.GetString(M3L.string_setLanguageToX);
                VisitWebsiteText = (SelectedMod != null && SelectedMod.ModWebsite != Mod.DefaultWebsite) ? M3L.GetString(M3L.string_interp_visitSelectedModWebSite, SelectedMod.ModName) : "";
            }
        }


        private void LoadExternalLocalizationDictionary(string filepath)
        {
            string filename = Path.GetFileNameWithoutExtension(filepath);
            string extension = Path.GetExtension(filepath);
            if (M3Localization.SupportedLanguages.Contains(filename) && extension == @".xaml" && Settings.DeveloperMode)
            {
                //Load external dictionary
                try
                {
                    var extDictionary = (ResourceDictionary)XamlReader.Load(new XmlTextReader(filepath));
                    SetApplicationLanguage(filename, false, extDictionary);
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Error loading external localization file: " + e.Message);
                }
            }
        }

        internal void ShowBackupNag()
        {
            var nagPanel = new BackupNagSystem(InstallationTargets.ToList());
            nagPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(nagPanel);
        }

        private void ShowWelcomePanel_Click(object sender, RoutedEventArgs e)
        {
            ShowFirstRunPanel();
        }

        private void OpenME3TweaksModMaker_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(@"https://me3tweaks.com/modmaker");
        }

        private void Donations_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(@"https://me3tweaks.com/donations");
        }

        private void ListAllInstallableFiles_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                var files = SelectedMod.GetAllInstallableFiles();
                ListDialog l = new ListDialog(files, M3L.GetString(M3L.string_interp_allInstallableFiles, SelectedMod.ModName), M3L.GetString(M3L.string_description_allInstallableFiles), this);
                l.Show();
            }
        }

        private void ListPossibleDirectlyConflictingMods_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMod != null)
            {
                var files = SelectedMod.GetAllInstallableFiles();

                // Load Nexus Database and query it.


                ListDialog l = new ListDialog(files, M3L.GetString(M3L.string_interp_allInstallableFiles, SelectedMod.ModName), M3L.GetString(M3L.string_description_allInstallableFiles), this);
                l.Show();
            }
        }

        private void GameFilter_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Shift Click
                if (sender is ToggleButton tb && tb.DataContext is GameFilter gf)
                {
                    SuppressFilterMods = true;
                    foreach (var gameF in M3LoadedMods.Instance.GameFilters)
                    {
                        if (gameF == gf)
                        {
                            gf.IsEnabled = true;
                            continue;
                        }

                        gameF.IsEnabled = false;
                    }
                    SuppressFilterMods = false;
                    M3LoadedMods.Instance.FilterMods();
                }
            }
        }

        private void RouteDebugCall(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (sender is FrameworkElement fe)
            {
                DebugMenu.RouteDebugCall(fe.Name, this);
            }
#endif
        }

#if DEBUG
        /// <summary>
        /// This method forces the inclusion of Using statements when cleaning them up. This method is purposely never called
        /// </summary>
        private void ForceImports()
        {
            var localmd5 = MUtilities.CalculateMD5(@"null");

        }
#endif
        /// <summary>
        /// Raises the PropertyChanged event for the named property.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void RaisePropertyChangedFor(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ModLibraryMod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Double click to install feature.
            if (Settings.DoubleClickModInstall && e.ClickCount >= 2 && sender is FrameworkElement fwe && fwe.DataContext is Mod m)
            {
                GameTargetWPF t = GetCurrentTarget(m.Game);
                if (t != null)
                {
                    M3Log.Information($@"DoubleClickModInstall triggered for {m.ModName}");
                    ApplyMod(m, t);
                }
            }
        }

        /// <summary>
        /// Called when a dismiss (X) is invoked on a one-time message in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DismissOneTimeMessage(object sender, RoutedEventArgs e)
        {
            if (sender == ModLibraryNotInstalledModsDismissButton) Settings.OneTimeMessage_ModListIsNotListOfInstalledMods = false;
        }

        /// <summary>
        /// Restores one-time notification prompts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RestoreOneTimeMessages(object sender, RoutedEventArgs e)
        {
            // Put other settings here as they are added.
            Settings.OneTimeMessage_ModListIsNotListOfInstalledMods = true;
        }
    }
}
