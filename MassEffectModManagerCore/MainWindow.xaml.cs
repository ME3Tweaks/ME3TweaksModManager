using AdonisUI;
using CliWrap.EventStream;
using CommandLine;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Helpers.MEM;
using ME3TweaksCore.Localization;
using ME3TweaksCore.ME3Tweaks.M3Merge;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.deployment;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.online;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.launcher;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using ME3TweaksModManager.modmanager.textures;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Media;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;
using Mod = ME3TweaksModManager.modmanager.objects.mod.Mod;
using StarterKitContentSelector = ME3TweaksModManager.modmanager.windows.dialog.StarterKitContentSelector;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Reference to the main window of ME3Tweaks Mod Manager, once loaded
        /// </summary>
        public static MainWindow Instance { get; private set; }


        /// <summary>
        /// If set to true, the app will automatically close itself after performing some cleanup
        /// </summary>
        public bool IsOnTrackToClose { get; set; }

        /// <summary>
        /// If we are exiting. If true, further attempts to try and close the window by the user will be ignored
        /// </summary>
        public bool ExitInProgress { get; set; }

        /// <summary>
        /// If set to true, the app has cleaned up and is ready for termination
        /// </summary>
        public bool AppExiting { get; set; }

        public string CurrentOperationText { get; set; } = M3L.GetString(M3L.string_startingUp);

        public bool IsBusy { get; set; }

        public ObservableCollectionExtended<MEGame> MenuAvailableGames { get; } = new();

        /// <summary>
        /// If the search box is open.
        /// </summary>
        public bool SearchBoxOpen { get; set; }

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

        /// <summary>
        /// Task used when downloads are in progress. Set to null once background downloads have finished 
        /// </summary>
        public BackgroundTask DownloadingTask { get; set; }

        /// <summary>
        /// Flag to indicate if we have checked for Microsoft Visual C++ this session
        /// </summary>
        private bool hasCheckedForMSVC { get; set; } = false;

        public string CurrentDescriptionText { get; set; } = DefaultDescriptionText;
        private static readonly string DefaultDescriptionText = M3L.GetString(M3L.string_selectModOnLeftToGetStarted);


        public string ApplyModButtonText { get; set; } = M3L.GetString(M3L.string_applyMod);

        public string InstallationTargetText { get; set; } = M3L.GetString(M3L.string_installationTarget);

        public bool ME1ASILoaderInstalled { get; set; }
        public bool ME2ASILoaderInstalled { get; set; }
        public bool ME3ASILoaderInstalled { get; set; }
        public bool LE1ASILoaderInstalled { get; set; }
        public bool LE2ASILoaderInstalled { get; set; }
        public bool LE3ASILoaderInstalled { get; set; }
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
        /// <param name="args">Command line arguments passed</param>
        /// <returns>True if window should be brought to the foreground, false otherwise</returns>
        internal async Task<bool> HandleInstanceArguments(string[] args)
        {
            // Fix pass through in debug mode which uses a .dll arg
            if (args.Any() && args[0].EndsWith(@".dll"))
            {
                args = args.Skip(1).Take(args.Length - 1).ToArray();
            }
            var result = Parser.Default.ParseArguments<CLIOptions>(args);
            if (result is Parsed<CLIOptions> parsedCommandLineArgs)
            {
                if (parsedCommandLineArgs.Value.RelevantGame != null)
                    CommandLinePending.PendingGame = parsedCommandLineArgs.Value.RelevantGame.Value;
                if (parsedCommandLineArgs.Value.NXMLink != null)
                    CommandLinePending.PendingNXMLink = parsedCommandLineArgs.Value.NXMLink;
                if (parsedCommandLineArgs.Value.M3Link != null)
                    CommandLinePending.PendingM3Link = parsedCommandLineArgs.Value.M3Link;
                if (parsedCommandLineArgs.Value.AutoInstallModdescPath != null)
                    CommandLinePending.PendingAutoModInstallPath = parsedCommandLineArgs.Value.AutoInstallModdescPath;
                if (parsedCommandLineArgs.Value.GameBoot)
                    CommandLinePending.PendingGameBoot = parsedCommandLineArgs.Value.GameBoot;
                if (parsedCommandLineArgs.Value.AutoInstallASIGroupID > 0)
                    CommandLinePending.PendingInstallASIID = parsedCommandLineArgs.Value.AutoInstallASIGroupID;
                if (parsedCommandLineArgs.Value.AutoInstallASIVersion > 0)
                    CommandLinePending.PendingInstallASIVersion = parsedCommandLineArgs.Value.AutoInstallASIVersion;
                if (parsedCommandLineArgs.Value.AutoInstallBink != false)
                    CommandLinePending.PendingInstallBink = parsedCommandLineArgs.Value.AutoInstallBink;
                if (parsedCommandLineArgs.Value.CreateMergeDLC != false)
                    CommandLinePending.PendingMergeDLCCreation = parsedCommandLineArgs.Value.CreateMergeDLC;
                if (parsedCommandLineArgs.Value.MergeModManifestToCompile != null)
                    CommandLinePending.PendingMergeModCompileManifest = parsedCommandLineArgs.Value.MergeModManifestToCompile;
                if (parsedCommandLineArgs.Value.FeatureLevel > 0)
                    CommandLinePending.PendingFeatureLevel = parsedCommandLineArgs.Value.FeatureLevel;
                return await handleInitialPending();
            }

            return false;
        }

        private void ShowDownloadManager()
        {
            // Todo: Figure this out. 03/28/2025
            // npl = protocol link for when this was to queue a download
            //if (NexusDomainHandler.HandleExternalLink(npl))
            //{
            //    return; // Handled by external handler.
            //}

            if (NexusModsUtilities.UserInfo == null)
            {
                // Not logged in
                Activate(); //bring to front
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_nexusLoginRequiredForDownload), M3L.GetString(M3L.string_notSignedIn), MessageBoxButton.OK, MessageBoxImage.Error);
                ShowNexusPanel();
                return;
            }

            if (BusyContentM3 is SingleItemPanel2 sip2 && sip2.Content is DownloadManagerPanel dp)
            {
                // Do nothing, it's already visible.
            }
            else
            {
                // Show download panel
                var mDownloader = new DownloadManagerPanel();
                mDownloader.Close += (a, b) =>
                {
                    ReleaseBusyControl();
                    if (b.Data is NexusModDownload downloadedMod)
                    {
                        ShowModArchiveImportForDownload(downloadedMod);
                    }
                    // If there are ever other types of download support we should handle them here.
                };
                ShowBusyControl(mDownloader, ShouldShowNXMDownloadImmediately());
            }
        }

        /// <summary>
        /// Shows the archive import UI for a downloaded mod.
        /// </summary>
        /// <param name="downloadedMod"></param>
        public void ShowModArchiveImportForDownload(ModDownload downloadedMod, bool priority = true)
        {
            downloadedMod.DownloadedStream.Position = 0;
            App.SubmitAnalyticTelemetryEvent(@"User opened mod archive for import",
                new Dictionary<string, string>
                {
                    { @"Filename", downloadedMod.FileName }
                });

            // Remove this download, we are now handling it.
            DownloadManager.RemoveDownload(downloadedMod);

            // Handle the UI
            var nexusInfo = downloadedMod as NexusModDownload;
            if (downloadedMod.DownloadedStream is FileStream fs)
            {
                // Open the file instead
                fs.Dispose(); // Ensure it's closed
                openModImportUI(fs.Name, priority: priority, sourceLink: nexusInfo?.ProtocolLink); // Open the archive itself
            }
            else
            {
                openModImportUI(downloadedMod.FileName, downloadedMod.DownloadedStream, priority, sourceLink: nexusInfo?.ProtocolLink);
            }
        }

        /// <summary>
        /// When an NXM link is fetched, should the download panel take priority?
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

                if (sip2.Content is BatchModLibrary bml)
                {
                    bml.RefreshContentsOnDisplay();
                    return true;
                }
            }

            return false;

        }

        private int oldFailedBindableCount = 0;
        public string NoModSelectedRichText => InternalNoModSelectedText(true);

        private string InternalNoModSelectedText(bool richText)
        {
            if (!M3SupportedOS.hasShownUnsupportedMessage && (!M3SupportedOS.IsSupportedOperatingSystem()))
            {
                M3SupportedOS.hasShownUnsupportedMessage = true;
                string osList = string.Join("\n", M3SupportedOS.GetSupportedOperatingSystems().Select(x => $@" - {x.ToMinimumSupportedString()}")); //do not localize
                var finalString = M3L.GetString(M3L.string_interp_dialog_unsupportedOS, osList);
                finalString = RichTextHelper.ConvertUnicode(finalString);
                return RichTextHelper.GetHeader() + RichTextHelper.ConvertNewlines(finalString) + RichTextHelper.GetFooter();
            }

            var retvar = M3L.GetString(M3L.string_selectModOnLeftToGetStarted);
            var localizedTip = TipsService.GetTip(App.CurrentLanguage);
            if (localizedTip != null)
            {
                retvar += $"\n\n---------------------------------------------\n{localizedTip}"; //do not localize
            }

            if (richText)
            {
                // This probably doesn't work properly on non english language settings
                var finalString = RichTextHelper.ConvertUnicode(retvar);
                return RichTextHelper.GetHeader() + RichTextHelper.ConvertNewlines(finalString) + RichTextHelper.GetFooter();
            }
            else
            {
                return retvar;
            }
        }

        /// <summary>
        /// The current selected launch option
        /// </summary>
        public LaunchOptionsPackage SelectedLaunchOption { get; set; } = M3LoadedMods.GetDefaultLaunchOptionsPackage();

        /// <summary>
        /// Text for the 'X mods failed to load'
        /// </summary>
        public string FailedModsString { get; set; }

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

        public MainWindow()
        {
            if (CommandLinePending.UpgradingFromME3CMM /* || true*/)
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
            SetTheme(true);
            SetWineUIDefaults(); // Setting before anything is drawn
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

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

            // Setup settings listeners
            Settings.StaticPropertyChanged += SettingsChangeListener.OnSettingChanged;


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
            BackgroundTaskEngine.InitializeTaskEngine(
                updateText => { Application.Current?.Dispatcher.Invoke(() => { CurrentOperationText = updateText; }); },
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
                ModsList_ListBox.ScrollIntoView(SelectedMod);
                // This makes the ApplyMod button refresh states so it shows up properly as clickable
                CommandManager.InvalidateRequerySuggested();
            });

            // MOD UPDATER
            ModUpdater.InitializeModUpdater(this);
        }

        private void ModGameVisibilityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameFilter.IsEnabled))
            {
                Settings.SelectedFilters = string.Join(',',
                    M3LoadedMods.Instance.GameFilters.Where(x => x.IsEnabled).Select(x => x.Game));
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
                var result = M3L.ShowDialog(null,
                    M3L.GetString(M3L.string_dialog_multiUserProgramDataWindowsRestrictions),
                    M3L.GetString(M3L.string_grantingWritePermissions), MessageBoxButton.OKCancel,
                    MessageBoxImage.Error);
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
                        M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_programWillNotRunCorrectly),
                            M3L.GetString(M3L.string_programDataAccessDenied), MessageBoxButton.OK,
                            MessageBoxImage.Error);

                    }
                }
                else
                {
                    M3Log.Error(@"User denied granting permissions!");
                    M3L.ShowDialog(null, M3L.GetString(M3L.string_dialog_programWillNotRunCorrectly),
                        M3L.GetString(M3L.string_programDataAccessDenied), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                M3Log.Information(@"settings.ini is writable");
            }
        }

        /// <summary>
        /// Updates the default Font Family of M3
        /// </summary>
        /// <param name="fontName"></param>
        /// <returns></returns>
        public static bool UpdateFontFamily(string fontName)
        {
            return UpdateFontFamily(new FontFamily(fontName));
        }

        /// <summary>
        /// Updates the default Font Family of M3
        /// </summary>
        /// <param name="font"></param>
        /// <returns></returns>
        public static bool UpdateFontFamily(FontFamily font)
        {
            if (Application.Current.TryFindResource("M3DefaultFont") is FontFamily defaultFont)
            {
                Application.Current.Resources["M3DefaultFont"] = font;
                M3Log.Information($"Updating font to {font.Source}");
                return true;
            }
            else
            {
                M3Log.Warning($"Failed to set font to {font.Source}");
                return false;
            }
        }

        /// <summary>
        /// Sets defaults 
        /// </summary>
        private static void SetWineUIDefaults()
        {
            // Override defaults if Wine is detected
            if (WineWorkarounds.WineDetected)
            {
                if (Application.Current.TryFindResource(@"M3DefaultMenuItemMargin") is Thickness defMargin)
                {
                    // Hacky, would be nice to figure out how to get Wine to display margins (and padding) correctly
                    defMargin.Left = defMargin.Left / 2;
                    Application.Current.Resources[@"M3DefaultMenuItemMargin"] = defMargin;
                    M3Log.Information($@"Wine: Setting menu category left margin to {defMargin.Left}");
                }
                UpdateFontFamily(@"Arial");
            }
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
                        FailedModsString = M3L.GetString(M3L.string_interp_XmodsFailedToLoad,
                            M3LoadedMods.Instance.FailedMods.BindableCount.ToString());
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

            // Subscribe to download manager's add/remove so we can control the background task notification. 
            DownloadManager.OnDownloadCompleted += DM_OnDownloadCompleted;
            DownloadManager.OnDownloadRemoved += DM_OnDownloadRemoved;
            DownloadManager.OnDownloadAdded += DM_OnDownloadAdded;

            // Also subscribe to all importing events because the UI should be locked when importing is occurring
            // for consistency, or panel results may go unhandled or UI state changes when filesystem data is
            // being changed.
            //DownloadManager.OnDownloadScanning += DM_OnModScanning; // This technically isn't required because background stuff isn't changing...
            DownloadManager.OnDownloadImporting += DM_OnModImporting;
            DownloadManager.OnDownloadImported += DM_OnImportProcessComplete;
            DownloadManager.OnDownloadImportFailed += DM_OnImportProcessComplete;

        }

        /// <summary>
        /// Invoked when a mod import has finished - it may be in a failed state!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DM_OnImportProcessComplete(object sender, EventArgs e)
        {
            if (sender is ModDownload md && GetCurrentPanel() is UILockoutPanel lockout)
            {
                // Unlock the UI if there is nothing currently ongoing.
                var somethingImporting = DownloadManager.GetDownloads().Any(x => x.Value.DownloadState is EModDownloadState.IMPORTING);
                if (!somethingImporting)
                {
                    lockout.UnlockUI();
                }
            }
        }

        /// <summary>
        /// Invoked when an automatic mod import has begun.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DM_OnModImporting(object sender, EventArgs e)
        {
            LockUIIfNecessary();
        }

        /// <summary>
        /// Puts a lockout panel into the busy queue if nothing is showing.
        /// </summary>
        private void LockUIIfNecessary()
        {
            if (GetCurrentPanel() == null)
            {
                // Lock up the interface for consistency.
                UILockoutPanel uiLockout = new UILockoutPanel();
                uiLockout.Close += (a, b) => { ReleaseBusyControl(); };
                ShowBusyControl(uiLockout);
            }
        }


        private void DM_OnDownloadAdded(object sender, EventArgs e)
        {
            DownloadingTask ??= DownloadManager.GenerateBackgroundTask();
        }

        private void DM_OnDownloadCompleted(object sender, EventArgs e)
        {
            // A download has completed
            // Mod may be importing still
            if (DownloadingTask != null)
            {
                var downloads = DownloadManager.GetDownloads();
                if (downloads.Count(x => x.Value.IsDownloading) == 0)
                {
                    BackgroundTaskEngine.SubmitJobCompletion(DownloadingTask);
                    DownloadingTask = null;
                }
            }
        }

        private void DM_OnDownloadRemoved(object sender, EventArgs e)
        {
            if (DownloadingTask != null)
            {
                var downloads = DownloadManager.GetDownloads();
                if (downloads.Count == 0)
                {
                    // No more downloads - complete the task.
                    BackgroundTaskEngine.SubmitJobCompletion(DownloadingTask);
                    DownloadingTask = null;
                }
            }
        }

        private void SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.GenerationSettingOT))
                OrderAndSetTargets(InternalLoadedTargets, SelectedGameTarget);
            else if (e.PropertyName == nameof(Settings.GenerationSettingLE))
                OrderAndSetTargets(InternalLoadedTargets, SelectedGameTarget);
            else if (e.PropertyName == nameof(Settings.OneTimeMessage_ModListIsNotListOfInstalledMods))
                ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods,
                    Settings.OneTimeMessage_ModListIsNotListOfInstalledMods);
            else if (e.PropertyName == nameof(Settings.OneTimeMessage_ModListIsNotListOfInstalledMods))
                ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods,
                    Settings.OneTimeMessage_ModListIsNotListOfInstalledMods);

        }

        public ICommand GenerateStarterKitCommand { get; set; }
        public ICommand StartGameSpecificSaveCommand { get; set; }
        public ICommand ChangeCurrentLaunchConfigCommand { get; set; }
        public ICommand OpenASIManagerCommand { get; set; }
        public ICommand OpenTutorialCommand { get; set; }
        public ICommand OriginInGameOverlayDisablerCommand { get; set; }
        public ICommand ModdescEditorCommand { get; set; }
        public ICommand LaunchEGMSettingsCommand { get; set; }
        public ICommand LaunchEGMSettingsLECommand { get; set; }
        public ICommand LaunchFVBCCUCommand { get; set; }
        public ICommand OfficialDLCTogglerCommand { get; set; }
        public ICommand ImportArchiveCommand { get; set; }
        public ICommand OpenDownloadManagerCommand { get; set; }
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
        public ICommand ShowInstallationInformationCommand { get; set; }
        public ICommand ManageCachedTargetsCommand { get; set; }
        public ICommand BackupCommand { get; set; }
        public ICommand DeployModCommand { get; set; }
        public ICommand DeleteModFromLibraryCommand { get; set; }
        public ICommand SubmitTelemetryForModCommand { get; set; }
        public ICommand SelectedModCheckForUpdatesCommand { get; set; }
        public ICommand RestoreModFromME3TweaksCommand { get; set; }
        public ICommand GrantWriteAccessCommand { get; set; }
        public RelayCommand AutoTOCCommand { get; set; }
        public RelayCommand CompileCoalescedCommand { get; set; }
        public RelayCommand DecompileCoalescedCommand { get; set; }
        public ICommand ConsoleKeyKeybinderCommand { get; set; }
        public ICommand CreateTestArchiveCommand { get; set; }
        public ICommand LaunchIniModderCommand { get; set; }
        public ICommand DownloadModMakerModCommand { get; set; }
        public ICommand MixinLibraryCommand { get; set; }
        public ICommand BatchModInstallerCommand { get; set; }
        public ICommand ImportDLCModFromGameCommand { get; set; }
        public ICommand BackupFileFetcherCommand { get; set; }
        public ICommand OpenModDescCommand { get; set; }
        public ICommand ShowAlternateOptionKeysCommand { get; set; }
        public ICommand CheckAllModsForUpdatesCommand { get; set; }
        public ICommand CheckNonWhitelistedModsForUpdates { get; set; }
        public ICommand CustomKeybindsInjectorCommand { get; set; }
        public ICommand NexusModsFileSearchCommand { get; set; }
        public ICommand SearchModsCommand { get; set; }
        public ICommand CloseModSearchBoxCommand { get; set; }
        public ICommand InstallMEMFileCommand { get; set; }
        public ICommand TrilogySaveEditorCommand { get; set; }
        public ICommand AddStarterKitContentCommand { get; set; }
        public ICommand ConvertMEMToTOCommand { get; set; }


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
            ShowInstallationInformationCommand = new GenericCommand(ShowInstallInfoPanel, CanShowInstallInfo);
            ManageCachedTargetsCommand = new GenericCommand(ShowCachedTargetsPanel);
            BackupCommand = new GenericCommand(ShowBackupPanel, ContentCheckNotInProgress);
            RestoreCommand = new GenericCommand(ShowRestorePanel, ContentCheckNotInProgress);
            DeployModCommand = new GenericCommand(ShowDeploymentPane, IsModSelectedInDevMode);
            DeleteModFromLibraryCommand = new GenericCommand(DeleteModFromLibraryWrapper, CanDeleteModFromLibrary);
            OpenDownloadManagerCommand = new GenericCommand(OpenDownloadManager, CanShowDownloadManager);
            ImportArchiveCommand = new GenericCommand(OpenArchiveSelectionDialog, CanOpenArchiveSelectionDialog);
            SubmitTelemetryForModCommand = new GenericCommand(SubmitTelemetryForMod, CanSubmitTelemetryForMod);
            SelectedModCheckForUpdatesCommand = new GenericCommand(CheckSelectedModForUpdate, SelectedModIsUpdatable);
            RestoreModFromME3TweaksCommand = new GenericCommand(RestoreSelectedMod, SelectedModIsME3TweaksUpdatable);
            GrantWriteAccessCommand = new GenericCommand(() => CheckTargetPermissions(true, true), HasAtLeastOneTarget);
            AutoTOCCommand = new RelayCommand(RunAutoTOCOnGame, CanAutoTOC);
            ConsoleKeyKeybinderCommand = new GenericCommand(OpenConsoleKeyKeybinder, CanOpenConsoleKeyKeybinder);
            CreateTestArchiveCommand = new GenericCommand(CreateTestArchive, CanCreateTestArchive);
            LaunchIniModderCommand = new GenericCommand(OpenMEIM, CanOpenMEIM);
            DownloadModMakerModCommand = new GenericCommand(OpenModMakerPanel, CanOpenModMakerPanel);
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
            ShowAlternateOptionKeysCommand = new GenericCommand(ShowAlternateKeys);
            CheckAllModsForUpdatesCommand = new GenericCommand(CheckAllModsForUpdatesWrapper, () => M3LoadedMods.Instance.ModsLoaded);
            CheckNonWhitelistedModsForUpdates = new GenericCommand(CheckNonWhitelistedModsForUpdatesWrapper, () => M3LoadedMods.Instance.ModsLoaded);
            CustomKeybindsInjectorCommand = new GenericCommand(OpenKeybindsInjector, () => M3LoadedMods.Instance.ModsLoaded && InstallationTargets.Any(x => x.Game == MEGame.ME3));
            ModdescEditorCommand = new GenericCommand(OpenModDescEditor, CanOpenModdescEditor);
            OriginInGameOverlayDisablerCommand = new GenericCommand(OpenOIGDisabler, () => M3LoadedMods.Instance.ModsLoaded && InstallationTargets.Any());
            OpenTutorialCommand = new GenericCommand(OpenTutorial, () => TutorialService.ServiceLoaded);
            OpenASIManagerCommand = new GenericCommand(OpenASIManager, NetworkThreadNotRunning);
            NexusModsFileSearchCommand = new GenericCommand(OpenNexusSearch); // no conditions for this
            CompileCoalescedCommand = new RelayCommand(CompileCoalesced); // no conditions for this
            DecompileCoalescedCommand = new RelayCommand(DecompileCoalesced); // no conditions for this
            InstallMEMFileCommand = new GenericCommand(InstallMEMFiles, CanInstallMEMFile);
            ChangeCurrentLaunchConfigCommand = new GenericCommand(OpenLaunchOptionSelector, () => SelectedGameTarget?.Game.IsLEGame() ?? false);
            TrilogySaveEditorCommand = new GenericCommand(OpenTSE);
            AddStarterKitContentCommand = new GenericCommand(OpenStarterKitContentSelector, IsModSelectedInDevMode);
            StartGameSpecificSaveCommand = new GenericCommand(SelectSpecificSaveForBoot, () => SelectedGameTarget != null && SelectedGameTarget.Game.IsLEGame());
            GenerateStarterKitCommand = new RelayCommand(GenerateStarterKit);
            ConvertMEMToTOCommand = new GenericCommand(ConvertMEMToTextureOverride, () => M3LoadedMods.Instance.ModsLoaded);
            LoadNexusCommands();
            LoadHeadmorphCommands();
            LoadMergeCommands();
        }

        private void OpenDownloadManager()
        {
            var mDownloader = new DownloadManagerPanel();
            mDownloader.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(mDownloader);
        }

        private bool CanShowDownloadManager()
        {
            return DownloadManager.GetDownloads().Any();
        }

        private void GenerateStarterKit(object obj)
        {
            if (obj is MEGame game)
            {
                new StarterKitGeneratorWindow(game) { Owner = this }.ShowDialog();
            }
        }

        private void CheckNonWhitelistedModsForUpdatesWrapper()
        {
            if (NexusModsUtilities.UserInfo == null)
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_youMustBeAuthenticatedToNexusModsInME3TweaksModManagerToUseThisFeature), M3L.GetString(M3L.string_notLoggedIn), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Non-Whitelisted Mod update check");
            nbw.DoWork += (a, b) => ModUpdater.Instance.CheckNonWhitelistedNexusModsForUpdates();
            nbw.RunWorkerAsync();
        }

        private void ShowAlternateKeys()
        {

            var dlcJob = SelectedMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (dlcJob == null)
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_selectedModNoAltKeysNoCUSTOMDLCTask, SelectedMod.ModName), M3L.GetString(M3L.string_message), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var alternates = dlcJob.GetAllAlternates();
            if (alternates.Count == 0)
            {

                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_modDoesNotHaveAlternatesOnCustomDLC, SelectedMod.ModName), M3L.GetString(M3L.string_message), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var mapping = alternates.Select(x => $@"{(x.GroupName != null ? $@"{x.GroupName} - " : @"")}{x.FriendlyName} => {x.OptionKey}").ToList(); // do not localize

            ListDialog ld = new ListDialog(mapping, M3L.GetString(M3L.string_interp_modNameDLCOptionKeys, SelectedMod.ModName), M3L.GetString(M3L.string_dialog_modUsesTheseOptionKeys, SelectedMod.ModName), this);
            ld.Show();
        }

        private void OpenStarterKitContentSelector()
        {
            var starterKitSelector = new StarterKitContentSelector(this, SelectedMod);
            starterKitSelector.ShowDialog();
            if (starterKitSelector.ReloadMod)
            {
                M3LoadedMods.Instance.LoadMods(SelectedMod, gamesToLoad: new[] { SelectedMod.Game }, scopedModsToReload: new List<string>(new[] { SelectedMod.ModDescPath }));
            }
        }

        public void OpenTSE()
        {
            TrilogySaveEditorHelper.OpenTSE(this);
        }

        private bool CanInstallMEMFile()
        {
            return SelectedGameTarget != null && SelectedGameTarget.Game.IsLEGame() && !MRunningGameInfo.IsGameRunning(SelectedGameTarget.Game);
        }


        private void CloseSearchBox()
        {
            if (SearchBoxOpen)
            {
                ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, false);
                SearchBoxOpen = false;
            }

            M3LoadedMods.Instance.ModSearchText = null;
        }

        private void ShowSearchBox()
        {
            if (!SearchBoxOpen)
            {
                ClipperHelper.ShowHideVerticalContent(ModListSearchBoxPanel, true);
                SearchBoxOpen = true;
            }

            Keyboard.Focus(ModSearchBox);
        }

        private void LaunchEGMSettings(GameTarget target = null)
        {
            target ??= GetCurrentTarget(MEGame.ME3);
            if (target != null)
            {
                LaunchExternalTool(ExternalToolLauncher.EGMSettings, $"\"{target.TargetPath}\""); // do not localize
            }
        }

        private void LaunchEGMSettingsLE(GameTarget target = null)
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
            optionsPanel.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(optionsPanel);
        }

        private bool CanAutoTOC(object obj)
        {
            if (obj is MEGame game && game.SupportsAutoTOC())
            {
                return HasGameTarget(game);
            }

            return false;
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
                    if (DownloadManager.AddNXMDownload(nxmlink) != null)
                    {
                        ShowDownloadManager();
                    }
                }
            };
            ShowBusyControl(nexusSearchPanel);
        }

        private void OpenTutorial()
        {
            var tutorial = new IntroTutorial(this);
            tutorial.Show();
            tutorial.Activate();
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
            var result = M3Utilities.ShellOpenFile(SelectedMod.ModDescPath);
            if (result != null)
            {
                // Issue opening the file.
                M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorOpeningModdesciniFileResult, result), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            if (target.Game == MEGame.ME3)
                return installedDLC.Contains(@"DLC_MOD_FSvBS") || installedDLC.Contains(@"DLC_MOD_FSvBS_V");
            if (target.Game == MEGame.LE3)
                return installedDLC.Contains(@"DLC_MOD_FSvBSLE") || installedDLC.Contains(@"DLC_MOD_FSvBSLE_V");
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
            importerPanel.Close += (a, b) => { ReleaseBusyControl(); };
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
                    InstallBatchQueue(batchLibrary.SelectedGameTarget, queue);
                }
            };
            ShowBusyControl(batchLibrary);
        }

        /// <summary>
        /// Installs a batch library install queue to the given target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="queue"></param>
        private void InstallBatchQueue(GameTarget target, BatchLibraryInstallQueue queue)
        {
            bool isFirstInstall = true;
            BatchPanelResult = new PanelResult();
            HandleBatchPanelResult = false; // Panel results should merge instead of running one after another
            //Install queue

            bool continueInstalling = true;
            int modIndex = 0;

            //recursive. If someone is installing enough mods to cause a stack overflow exception, well, congrats, you broke my code.
            void modInstalled(bool successful, bool isfirst = false)
            {
                if (!isfirst)
                {
                    M3Log.Information($@"ModInstalled() being called - successful: {successful}");

                    // Sync HasPromptedForBackup from the just-completed mod back to the queue so subsequent mods skip the backup prompt
                    if (modIndex > 0)
                        queue.HasPromptedForBackup |= queue.ModsToInstall[modIndex - 1].HasPromptedForBackup;
                }
                else
                {

                    // Do an initial reload since we don't reload targets in the mod options panel anymore.
                    target.ReloadGameTarget(false);

                    if (queue.ContainsTextureMods() && (queue.UseSavedOptions ||
                                                        // If all options are standalone we don't really care if there are saved options so just show it here
                                                        queue.ModsToInstall.Where(x => !x.ModMissing).All(x => x.IsStandalone)))
                    {
                        // We use batch text if this contains content mods due to the timing difference
                        continueInstalling = TextureInstallerPanel.ShowTextureInstallWarning(this, queue.ContainsContentMods());
                    }
                }

                continueInstalling &= successful && !IsOnTrackToClose;
                if (continueInstalling && queue.ModsToInstall.Count > modIndex)
                {
                    var bm = queue.ModsToInstall[modIndex];
                    modIndex++;
                    if (bm.IsAvailableForInstall())
                    {
                        M3Log.Information($@"Installing batch mod [{modIndex}/{queue.ModsToInstall.Count}]: {bm.Mod.ModName}");
                        bm.UseSavedOptions = queue.UseSavedOptions;
                        bm.IsFirstBatchMod = isFirstInstall;
                        bm.HasPromptedForBackup = queue.HasPromptedForBackup; // pass through if user skipped restore option earlier
                        ApplyMod(bm.Mod, target, batchMod: bm, installCompressed: queue.InstallCompressed, installCompletedCallback: modInstalled);
                        isFirstInstall = false;
                    }
                    else
                    {
                        M3Log.Warning($@"Skipping unavailable batch mod {bm.ModDescPath}");
                        modInstalled(true); // Trigger next install
                    }
                }
                else if (continueInstalling && queue.ModsToInstall.Count == modIndex) // We are at the end of the content mod list
                {
                    if (queue.ASIModsToInstall.Any())
                    {
                        ShowRunAndDone(
                            (config) => InstallBatchASIs(target, queue).Result,
                            M3L.GetString(M3L.string_installingASIMods),
                            M3L.GetString(M3L.string_installedASIMods), () => HandleBatchTextureInstall(target, queue));
                    }
                    else
                    {
                        HandleBatchTextureInstall(target, queue);
                    }
                }
                else
                {
                    // Install failed or was aborted
                    M3Log.Warning($@"Batch install was aborted or one failed, setting HandleBatchPanelResult to true");
                    HandleBatchPanelResult = true;
                }
            }

            if (queue.RestoreBeforeInstall)
            {
                // Will trigger target reload automatically
                RunBatchRestore(queue, target, modInstalled);
            }
            else
            {
                modInstalled(true, true); //kick off first installation
            }
        }

        /// <summary>
        /// Handles the initial batch installer game restore request and subsequent initial installation.
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="target"></param>
        /// <param name="modInstalled">Function, (succeeded, isFirst) </param>
        private void RunBatchRestore(BatchLibraryInstallQueue queue, GameTarget target, Action<bool, bool> modInstalled)
        {
            if (!BackupService.GetBackupStatus(queue.Game).BackedUp)
            {
                var shouldRestore = M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_dialog_restoreRequestedButUnavailable, queue.ModName),
                    M3L.GetString(M3L.string_backupNotAvailable),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (shouldRestore == MessageBoxResult.No)
                    return; // Total cancellation.

                if (shouldRestore == MessageBoxResult.Yes)
                {
                    // successful, isfirst
                    queue.HasPromptedForBackup = true;
                    modInstalled(true, true);
                }
            }
            else
            {
                var shouldRestore = M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_dialog_restoreRequestedConfirmation, queue.ModName),
                    M3L.GetString(M3L.string_gameRestoreRequested),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes); // Should this be cancel so if you click X?

                if (shouldRestore == MessageBoxResult.Cancel)
                    return; // Total cancellation.

                if (shouldRestore == MessageBoxResult.No)
                {
                    // successful, isfirst
                    modInstalled(true, true);
                    return;
                }

                if (shouldRestore == MessageBoxResult.Yes)
                {
                    AutoGameRestorePanel agrp = new AutoGameRestorePanel(target);
                    agrp.Close += (sender, args) =>
                    {
                        ReleaseBusyControl(); // This is so the panel is closed
                        modInstalled(agrp.RestoreSucceeded, true);
                    };
                    ShowBusyControl(agrp);
                }
            }
        }

        private async void HandleBatchTextureInstall(GameTarget target, BatchLibraryInstallQueue queue)
        {
            if (queue.TextureModsToInstall.Any(x => x.IsAvailableForInstall()))
            {
                // This must be done first since this could run a merge which will 
                // desync the texture map state. So this must be run before
                HandleBatchPanelResult = true;
                HandlePanelResult(BatchPanelResult);
                HandleBatchPanelResult = false; // Flip back after things get queued

                TextureInstallerPanel tip = new TextureInstallerPanel(target, queue.TextureModsToInstall.Where(x => x.IsAvailableForInstall()).Select(x => x.GetFilePathToMEM()).ToList())
                {
                    // Show if:
                    // Not using saved options
                    // and
                    // There is at least one mod that is not standalone
                    ShowTextureWarning = !queue.UseSavedOptions && queue.ModsToInstall.Where(x => !x.ModMissing).Any(x => !x.IsStandalone)
                };
                tip.Close += async (sender, args) =>
                {
                    ReleaseBusyControl(); // This is so the panel is closed
                    await FinishBatchInstall(queue); // This can throw a dialog. So it will have to manually trigger the batch panel result as none may be showing.
                };
                ShowBusyControl(tip);
            }
            else
            {
                HandleBatchPanelResult = true; // We should handle the results
                await FinishBatchInstall(queue); // Advance to next step
            }

        }

        private async Task FinishBatchInstall(BatchLibraryInstallQueue queue)
        {
            // 11/18/2023 - batch installer with ASI mods was not clearing out queue
            // This should force merges to occur.
            if (!queuedUserControls.Any() && BatchPanelResult != null && !IsBusy && HandleBatchPanelResult)
            {
                HandlePanelResult(BatchPanelResult);
            }
            if (!queue.UseSavedOptions && queue.HasAnyRecordedOptions())
            {
                var shouldSave = M3L.ShowDialog(this, M3L.GetString(M3L.string_saveChosenOptionsToThisBatchGroup),
                    M3L.GetString(M3L.string_saveOptions), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (shouldSave)
                {
                    M3Log.Information($@"Commiting batch queue with chosen options: {queue.BackingFilename}");
                    // This should be pretty fast since it doesn't have to hash. So we don't run this
                    // async.
                    await queue.Save(true);
                    M3Log.Information($@"Batch queue saved.");
                }
            }
        }

        private async Task<string> InstallBatchASIs(GameTarget target, BatchLibraryInstallQueue queue)
        {
            string result = null;
            foreach (var asi in queue.ASIModsToInstall)
            {
                if (asi.IsAvailableForInstall())
                {
                    await ASIManager.InstallASIToTarget(asi.AssociatedMod, target);
                }
                else
                {
                    M3Log.Warning($@"Not installing ASI with update group {asi.UpdateGroup} - not found in manifest");
                    result = M3L.GetString(M3L.string_someASIModsWereNotInstalled);
                }
            }

            return result;
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

        private void OpenMEIM()
        {
            new ME1IniModder().Show();
        }

        private bool CanCreateTestArchive() =>
            SelectedMod != null && SelectedMod.GetJob(ModJob.JobHeader.ME2_RCWMOD) == null;

        private void CreateTestArchive()
        {
            var testArchiveGenerator = new TestArchiveGenerator(SelectedMod);
            testArchiveGenerator.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(testArchiveGenerator);
        }

        public bool HasAtLeastOneTarget() => InstallationTargets.Any();

        private void CheckSelectedModForUpdate()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(CheckSelectedModForUpdate));
            bw.DoWork += (a, b) => { ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new[] { SelectedMod })); };
            bw.RunWorkerAsync();

        }

        private void RestoreSelectedMod()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(RestoreSelectedMod));
            bw.DoWork += (a, b) =>
            {
                ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new[] { SelectedMod }), true);
            };
            bw.RunWorkerAsync();
        }

        private bool SelectedModIsME3TweaksUpdatable() => SelectedMod?.IsME3TweaksUpdatable ?? false;
        private bool SelectedModIsUpdatable() => SelectedMod?.IsUpdatable ?? false;


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
                TelemetryInterposer.TrackEvent(@"User opened mod archive for import",
                    new Dictionary<string, string>
                        { { @"Method", @"Manual file selection" }, { @"Filename", Path.GetFileName(m.FileName) } });
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

        private async void DeleteModFromLibraryWrapper()
        {
            await DeleteModFromLibrary(SelectedMod);
        }

        public async Task<bool> DeleteModFromLibrary(Mod selectedMod)
        {
            if (selectedMod == null)
            {
                return false;
            }

            var confirmationResult = M3L.ShowDialog(this,
                M3L.GetString(M3L.string_interp_dialogDeleteSelectedModFromLibrary, selectedMod.ModName),
                M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.Yes);
            if (confirmationResult == MessageBoxResult.Yes)
            {
                M3Log.Information(@"Deleting mod from library: " + selectedMod.ModPath);

                // Submit background task
                var deleteTask = BackgroundTaskEngine.SubmitBackgroundJob(
                    @"ModDelete",
                    M3L.GetString(M3L.string_interp_deletingModFromLibrary, selectedMod.ModName),
                    M3L.GetString(M3L.string_interp_deletedModFromLibrary, selectedMod.ModName));

                // Set loading state to disable UI
                M3LoadedMods.Instance.SetLoadingState(true);

                try
                {
                    // Perform deletion on background thread
                    bool deletionSuccess = await Task.Run(() => MUtilities.DeleteFilesAndFoldersRecursively(selectedMod.ModPath));

                    if (deletionSuccess)
                    {
                        M3Log.Information($@"Successfully deleted mod from library: {selectedMod.ModName}");
                        M3LoadedMods.Instance.RemoveMod(selectedMod);
                        return true;
                    }
                    else
                    {
                        M3Log.Error($@"Failed to delete mod from library: {selectedMod.ModName}");

                        // Update task completion text to indicate failure
                        deleteTask.FinishedUIText = M3L.GetString(M3L.string_interp_failedToDeleteModFromLibrary, selectedMod.ModName);

                        // Show error dialog
                        M3L.ShowDialog(this,
                            M3L.GetString(M3L.string_interp_failedToDeleteModFromLibrary, selectedMod.ModName),
                            M3L.GetString(M3L.string_error),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    M3Log.Error($@"Exception occurred while deleting mod from library: {selectedMod.ModName}. {ex.Message}");

                    // Update task completion text to indicate failure
                    deleteTask.FinishedUIText = M3L.GetString(M3L.string_interp_failedToDeleteModFromLibrary, selectedMod.ModName);

                    // Show error dialog with exception details
                    M3L.ShowDialog(this,
                        $@"{M3L.GetString(M3L.string_interp_failedToDeleteModFromLibrary, selectedMod.ModName)}\n\n{ex.Message}",
                        M3L.GetString(M3L.string_error),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;
                }
                finally
                {
                    // Always restore UI state and complete task
                    M3LoadedMods.Instance.SetLoadingState(false);
                    BackgroundTaskEngine.SubmitJobCompletion(deleteTask);
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

            var canDeployMod = ArchiveDeployment.CanDeployMod(SelectedMod);
            if (canDeployMod)
            {
                var archiveDeploymentPane = new ArchiveDeploymentPanel(SelectedMod);
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

        private bool IsModSelectedInDevMode()
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
                M3Log.Information(@$"Showing panel {control.GetType().Name}");
                BusyContentM3 = new SingleItemPanel2(control);
            }
            else
            {
                if (swapImmediately)
                {
                    M3Log.Information(@$"Immediately swapping to panel {control.GetType().Name}");

                    // Rebuild the queue list with our existing open panel at the front
                    Queue<MMBusyPanelBase> rebuildQueue = new Queue<MMBusyPanelBase>();
                    if (BusyContentM3 is SingleItemPanel2 spi && spi.Content is MMBusyPanelBase mmbpb)
                    {
                        rebuildQueue.Enqueue(mmbpb); // Add the current panel
                        mmbpb.Result.MergeInto(control.Result); // 07/18/2024 - Merge the panel result into the one we are showing now for consistency
                    }

                    // Queue the remaining items
                    while (queuedUserControls.TryDequeue(out var item))
                    {
                        rebuildQueue.Enqueue(item);
                    }

                    // Show the immediately requested panel
                    BusyContentM3 = new SingleItemPanel2(control);

                    // Now rebuild the queue after we have shown our item
                    while (rebuildQueue.TryDequeue(out var item))
                    {
                        queuedUserControls.Enqueue(item);
                    }
                }
                else
                {
                    M3Log.Information(@$"Queueing panel {control.GetType().Name}");
                    queuedUserControls.Enqueue(control);
                }
            }
        }

        /// <summary>
        /// Removes the currently shown busy control and shows the next queued one, if any, additionally handling batch panel results.
        /// </summary>
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

                    // If we are on track to close, try closing the window again
                    if (IsOnTrackToClose)
                    {
                        ExitApp();
                    }

                    Task.Factory.StartNew(() =>
                    {
                        // this is to force some items that are no longer relevant to be cleaned up.
                        // for some reason commands fire even though they are no longer attached to the interface
                        Thread.Sleep(3000);
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                    });
                    // No more panels, we can show message updates now.
                    BackgroundTaskEngine.AllowMessageUpdates();

                    // 04/05/2025 - Re-lock UI when queue becomes empty to prevent user from doing things when 
                    // the UI should not allow the user to do stuff.
                    // 06/14/2025 - I am sure this was put here for a good reason,
                    // but I cannot remember it
                    // LockUIIfNecessary();
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
                if (IsOnTrackToClose || HandleBatchPanelResult)
                {
                    result = BatchPanelResult;

                    // Clear result
                    BatchPanelResult = null;
                    HandleBatchPanelResult = false;
                }
                else
                {
                    return;
                }
            }

            // This is pretty dicey with thread safety... 
            if (!Settings.SessionOnly_SuppressDLCMerge)
            {
                HandleBasegameTargetMerges(result);
                HandleDLCTargetMerges(result);
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
                M3LoadedMods.Instance.LoadMods(result.ModToHighlightOnReload, result.ModsToCheckForUpdates.Any(),
                    result.ModsToCheckForUpdates.ToList(), gamesToLoad, result.ModifiedModdescFiles);
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
                        { @"Invocation method", @"Installation Information" }
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


        private void ShowRunAndDone(Func<RunAndDoneConfig, object> action, string startStr, string endStr, Action finishAction = null, Action<Exception> errorOccurred = null)
        {
            var runAndDone = new RunAndDonePanel(action, startStr, endStr);
            runAndDone.Close += (a, b) =>
            {
                ReleaseBusyControl();
                errorOccurred?.Invoke(runAndDone.Result.Error); // Might just be null
                finishAction?.Invoke();
            };
            ShowBusyControl(runAndDone);
        }

        private void ShowBackupPanel()
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

        private void ShowRestorePanel()
        {
            var restoreManager = new RestorePanel(InstallationTargets.ToList(), SelectedGameTarget);
            restoreManager.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(restoreManager);
        }

        private void ShowInstallInfoPanel()
        {
            var installationInformation = new InstallationInformation(InstallationTargets.ToList(), SelectedGameTarget);
            installationInformation.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(installationInformation);
        }

        private void ShowCachedTargetsPanel()
        {
            var cachedTargetsPanel = new CachedTargetsPanel();
            cachedTargetsPanel.Close += (a, b) =>
            {
                if (cachedTargetsPanel.Result.ReloadTargets)
                {
                    PopulateTargets(SelectedGameTarget);
                }
                ReleaseBusyControl();
            };
            ShowBusyControl(cachedTargetsPanel);
        }

        /// <summary>
        /// Boots the specified tool ID, passes through the current active targets in M3, if they are supported.
        /// </summary>
        /// <param name="toolname"></param>
        private void BootToolPathPassthrough(string toolname, GameTarget forcedTarget = null)
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
            return SelectedGameTarget != null && SelectedGameTarget.IsValid && SelectedGameTarget.Selectable &&
                   !ContentCheckInProgress;
        }

        private void CallApplyMod()
        {
            ApplyMod(SelectedMod);
        }
        private bool CanToggleBinkw32(object obj)
        {
            if (obj is string str && Enum.TryParse(str, out MEGame game))
            {
                var target = GetCurrentTarget(game);
                if (target != null && !MRunningGameInfo.IsGameRunning(game))
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
                if (MRunningGameInfo.IsGameRunning(game))
                {
                    M3L.ShowDialog(this,
                        M3L.GetString(M3L.string_interp_dialogCannotInstallBinkWhileGameRunning, game.ToGameName()),
                        M3L.GetString(M3L.string_gameRunning), MessageBoxButton.OK, MessageBoxImage.Error);
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


        public bool ContentCheckInProgress { get; set; } = true; //init is content check
        private bool NetworkThreadNotRunning() => !ContentCheckInProgress;

        private void CheckForContentUpdates()
        {
            PerformStartupNetworkFetches(false);
        }


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

            // Check we have 'content' mod data to install
            var nonDirectInstallJobs = SelectedMod.InstallationJobs.Where(x => x.Header != ModJob.JobHeader.TEXTUREMODS && x.Header != ModJob.JobHeader.HEADMORPHS).ToList();
            if (nonDirectInstallJobs.Count == 0)
            {
                ApplyModButtonText = M3L.GetString(M3L.string_notAContentMod);
                return false;
            }

            ApplyModButtonText = M3L.GetString(M3L.string_applyMod);
            return true;
        }

        /// <summary>
        /// Checks whether the required Microsoft Visual C++ Redistributable (x64, version 14.50 or higher) is installed
        /// and prompts the user to install it if necessary.
        /// </summary>
        /// <remarks>This method is required for enabling ASI mod support in Legendary Edition modding. If
        /// the application is running under Wine, this check is skipped. The user is prompted to install the
        /// redistributable either automatically or manually if it is not detected.</remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task CheckForMSVCPP()
        {
            if (WineWorkarounds.WineDetected)
            {
                // We don't use this method for msvc on wine
                return;
            }

            // 02/07/2026 - Check MSVC++ v145 or higher is available for LE
            if (!hasCheckedForMSVC)
            {
                var msvcInstalled = MSVCPP.IsVCRedist2015To2026x64Installed();
                if (msvcInstalled)
                {
                    // Prereq met, mark checked and done
                    hasCheckedForMSVC = true;
                    return;
                }

                // Prereq not met
                MessageBoxResult res = M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_dialog_msvcVersionMissing),
                    M3L.GetString(M3L.string_mSVCPPRequired),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes,
                    M3L.GetString(M3L.string_installForMe),
                    M3L.GetString(M3L.string_installManually),
                    M3L.GetString(M3L.string_doNotInstall));

                if (res == MessageBoxResult.Yes)
                {
                    // Install auto
                    M3Log.Information(@"User choosing to install Microsoft Visual C++ Redistributable automatically");
                    await Task.Run(async () => { await InstallMSVCPP(); });
                    hasCheckedForMSVC = true;
                }
                else if (res == MessageBoxResult.No)
                {
                    M3Log.Information(@"User choosing to install Microsoft Visual C++ Redistributable manually");
                    M3L.ShowDialog(this,
                        M3L.GetString(M3L.string_dialog_msvcManualDirections),
                        M3L.GetString(M3L.string_mSVCPPRequired),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information,
                        MessageBoxResult.OK);

                    // Recheck now
                    await CheckForMSVCPP();
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    M3Log.Warning(@"User declined to install Microsoft Visual C++ Redistributable");
                    hasCheckedForMSVC = true;
                }
            }
        }


        /// <summary>
        /// Applies a mod to the current or forced target. This method is asynchronous, it must run on the UI thread but it will immediately yield once the installer begins.
        /// </summary>
        /// <param name="mod">Mod to install</param>
        /// <param name="forcedTarget">Forced target to install to</param>
        /// <param name="batchMod"></param>
        /// <param name="installCompressed"></param>
        /// <param name="installCompletedCallback">Callback when mod installation either succeeds for fails</param>
        /// <param name="recordOptionsToBM">If options chosen should be saved back to the BatchMod object</param>
        /// <param name="useSavedBatchOptions">If options saved in the BatchMod object should be used</param>
        private void ApplyMod(Mod mod, GameTarget forcedTarget = null, BatchMod batchMod = null,
            bool? installCompressed = null, Action<bool, bool> installCompletedCallback = null)
        {
            if (!MRunningGameInfo.IsGameRunning(mod.Game))
            {
                if (forcedTarget == null && SelectedGameTarget == null)
                {
                    TelemetryInterposer.TrackError(new Exception(@"ApplyMod: target and selected target is null!"));
                }
                BackgroundTask modInstallTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ModInstall", M3L.GetString(M3L.string_interp_installingMod, mod.ModName), M3L.GetString(M3L.string_interp_installedMod, mod.ModName));
                var modOptionsPicker = new ModInstallOptionsPanel(mod, forcedTarget ?? SelectedGameTarget, installCompressed, batchMod);
                //var modInstaller = new ModInstaller(mod, forcedTarget ?? SelectedGameTarget, installCompressed, batchMode: batchMode);
                modOptionsPicker.Close += (a, b) =>
                {
                    if (b.Data is ModInstallOptionsPackage miop)
                    {
                        // We are continuing to the mod installer

                        // Release the mod install options panel
                        ReleaseBusyControl();

                        ModInstallerPanel mi = new ModInstallerPanel(miop);
                        mi.Close += (c, d) =>
                        {
                            if (mi.InstallationCancelled || !mi.InstallationSucceeded)
                            {
                                modInstallTask.FinishedUIText = M3L.GetString(M3L.string_interp_failedToInstallMod, mod.ModName);
                            }
                            BackgroundTaskEngine.SubmitJobCompletion(modInstallTask);

                            // Propagate HasDoneBackupCheck back to the BatchMod so the queue can update for subsequent mods
                            if (batchMod != null && miop.HasDoneBackupCheck)
                                batchMod.HasPromptedForBackup = true;

                            ReleaseBusyControl(); // Release the mod installer. This may cause merges to occur if batch panel handling is set to false.

                            // This must go after releasing the control because in batch mode it will begin setting up the next panel.
                            // We do not want it to set up the next panel (e.g. textures) and then try to handle merges from the mod installer after that panel
                            installCompletedCallback?.Invoke(mi.InstallationSucceeded && !mi.InstallationCancelled, false);
                        };
                        ShowBusyControl(mi);
                    }
                    else
                    {
                        // User canceled the options
                        installCompletedCallback?.Invoke(false, false); // Canceled
                        HandleBatchPanelResult = true; // If we're in a batch it's important we handle this.
                        modInstallTask.FinishedUIText = M3L.GetString(M3L.string_installationAborted);
                        // We release the busy control here after setting handle batch panel result to true, so it handles it.
                        ReleaseBusyControl();
                        BackgroundTaskEngine.SubmitJobCompletion(modInstallTask);
                    }
                };
                ShowBusyControl(modOptionsPicker);
            }
            else
            {
                M3Log.Error($@"Blocking install of {mod.ModName} because {mod.Game.ToGameName()} is running.");
                M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_interp_dialogCannotInstallModsWhileGameRunning, mod.Game.ToGameName()),
                    M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogUACPreConsent),
                            M3L.GetString(M3L.string_someTargetsKeysWriteProtected), MessageBoxButton.YesNo,
                            MessageBoxImage.Question) == MessageBoxResult.Yes;
                    });
                    if (result)
                    {
                        TelemetryInterposer.TrackEvent(@"Granting write permissions",
                            new Dictionary<string, string>() { { @"Granted?", @"Yes" } });
                        try
                        {
                            M3Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate.Select(x => x.TargetPath)
                                .ToList());
                        }
                        catch (Exception e)
                        {
                            M3Log.Error(@"Error granting write permissions: " + App.FlattenException(e));
                        }
                    }
                    else
                    {
                        M3Log.Warning(@"User denied permission to grant write permissions");
                        TelemetryInterposer.TrackEvent(@"Granting write permissions",
                            new Dictionary<string, string>() { { @"Granted?", @"No" } });
                    }
                }
                else
                {
                    TelemetryInterposer.TrackEvent(@"Granting write permissions",
                        new Dictionary<string, string>() { { @"Granted?", @"Implicit" } });
                    M3Utilities.EnableWritePermissionsToFolders(targetsNeedingUpdate.Select(x => x.TargetPath)
                        .ToList());
                }
            }
            else if (showDialogEvenIfNone)
            {
                M3L.ShowDialog(this, M3L.GetString(M3L.string_allTargetsWritable),
                    M3L.GetString(M3L.string_targetsWritable), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        //Fody uses this property on weaving
        public event PropertyChangedEventHandler PropertyChanged;

        private void ModManager_ContentRendered(object sender, EventArgs e)
        {
            // We set this only after initialization. It should not be used before.
            if (Instance == null)
            {
                // This should never occur in our programming style, but who knows...
                Instance = this;
            }

            DPIScaling.SetScalingFactor(this);
            if (WineWorkarounds.WineDetected)
            {
                var message = M3L.GetString(M3L.string_dialog_wineDetected);
#if DEBUG
                if (WineWorkarounds.WineDetectedVersion != null)
                {
                    // Localization disabled on these strings as it's debug only
                    message += $"\n\nWine version: {WineWorkarounds.WineDetectedVersion}"; // do not localize
                    message += $"\nKernel: {WineWorkarounds.WineHostKernelName} {WineWorkarounds.WineHostKernelVersion}"; // do not localize
                }
#endif
                M3L.ShowDialog(this, message, M3L.GetString(M3L.string_wineDetected), MessageBoxButton.OK);
            }
            else
            {
                // Dialog removed 01/26/2026;
                // Now forces NoModSelectedText.
                //if (!App.IsOperatingSystemSupported())
                //{
                //    string osList = string.Join("\n - ", App.SupportedOperatingSystemVersions); //do not localize
                //    M3Log.Warning(@"This operating system is not supported");
                //    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialog_unsupportedOS, osList),
                //        M3L.GetString(M3L.string_unsupportedOperatingSystem), MessageBoxButton.OK, MessageBoxImage.Warning);
                //}
                //else
                //{
                //    // Unimplemented last crash dialog code removed 03/28/2025
                //}
            }

            // Run on background thread as we don't need the result of this
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"NexusModsInitialAuthentication");
            nbw.DoWork += (a, b) => RefreshNexusStatus();
            nbw.RunWorkerAsync();

            var syncContext = TaskScheduler.FromCurrentSynchronizationContext();
            LegendaryExplorerCoreLib.SetSynchronizationContext(syncContext);
            IsEnabled = false;

            // Initialize ASI manager variables before the service loads
            ASIManager.Options.DevMode = Settings.DeveloperMode;
            // Beta is handled by ME3TweaksCore boot

            Task.Run(() =>
            {
                ME3TweaksCoreLib.Initialize(LibraryBoot.GetPackage());
                LibraryBoot.AddM3SpecificFixes();

                //debugMethod();
                CurrentOperationText = M3L.GetString(M3L.string_loadingTargets);
                PopulateTargets();
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    M3Log.Exception(x.Exception, @"An error occurred during startup: ");
                }

                IsEnabled = true;
                if (!Settings.ShowedPreviewPanel)
                {
                    ShowFirstRunPanel();
                }
                else
                {
                    M3LoadedMods.Instance.LoadLaunchOptions();
                    UpdateSelectedLaunchOption();
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
                    ManagementObjectSearcher mosProcessor =
                        new ManagementObjectSearcher(@"SELECT * FROM Win32_Processor");
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

                    App.SubmitAnalyticTelemetryEvent(@"Version and Hardware Info", data);
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
                    var tutorial = new IntroTutorial(this);
                    if (tutorial.TutorialSteps.Any()) // if somehow we get into a phase where there are no steps we cannot show it
                    {
                        tutorial.Show();
                        tutorial.Activate();
                    }
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
        /// Calls CheckAllModsForUpdates(). This method should be called from the UI thread.
        /// </summary>
        private void CheckAllModsForUpdatesWrapper()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Mod update check");
            nbw.DoWork += (a, b) => ModUpdater.Instance.CheckAllModsForUpdates();
            nbw.RunWorkerAsync();
        }

        public async void OnSelectedModChanged()
        {
            if (SelectedMod != null)
            {
                SetWebsitePanelVisibility(SelectedMod.ModWebsite != Mod.DefaultWebsite);

                if (SelectedGameTarget == null || SelectedGameTarget.Game != SelectedMod.Game)
                {
                    // Update the target
                    var installTarget =
                        InstallationTargets.FirstOrDefault(x => x.RegistryActive && x.Game == SelectedMod.Game);
                    if (installTarget != null)
                    {
                        SelectedGameTarget = installTarget;
                    }
                }

                if (SelectedMod.BannerBitmap == null)
                {
                    SelectedMod.LoadBannerImage(); // Method will check if it's null
                }

                VisitWebsiteText = SelectedMod.ModWebsite != Mod.DefaultWebsite
                    ? M3L.GetString(M3L.string_interp_visitSelectedModWebSite, SelectedMod.ModName)
                    : "";

                // Do not await
                UpdateModEndorsementStatus();
            }
            else
            {
                VisitWebsiteText = "";
                SetWebsitePanelVisibility(false);
                CurrentDescriptionText = DefaultDescriptionText;
            }

            CanApplyMod(); // This sets the text. Good design MG
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
            e.Cancel = true;

            // App is actually closing now.
            if (AppExiting)
            {
                e.Cancel = false;
                return;
            }

            Dispatcher.InvokeAsync(HandleMainWindowClosing, DispatcherPriority.Normal);
        }

        /// <summary>
        /// Handles the logic for closing the main application window, including user confirmation, cleanup checks, and
        /// cancellation of pending operations as needed.
        /// </summary>
        /// <remarks>This method coordinates the shutdown process by checking for user overrides, ongoing
        /// background operations, and open windows that may block closure. It prompts the user for confirmation if
        /// critical tasks are in progress and ensures that all necessary cleanup is performed before the application
        /// exits. If the Shift key is held during the close request, the application will close immediately, bypassing
        /// standard cleanup checks.</remarks>
        private void HandleMainWindowClosing()
        {
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                // User override - always close
                MEMProcessHandler.TerminateAll();
                M3Log.Information(@"User override - skipping cleanup checks before close.");
                ExitApp();
                return;
            }

            if (IsOnTrackToClose && AppExiting)
            {
                MEMProcessHandler.TerminateAll();
                M3Log.Information(@"Cleanup complete, application will now close.");
                ExitApp();
                return; // We're done here
            }

            // Ignore user request.
            if (ExitInProgress)
            {
                M3Log.Information(@"Ignoring window closing request: application cleanup in progress");
                return;
            }


            foreach (var w in Application.Current.Windows.OfType<IClosableWindow>())
            {
                if (w.AskToClose() == false)
                {
                    M3Log.Information($@"Aborting application close - open window {w} indicates user does not want app to close.");
                    IsOnTrackToClose = false;
                    return;
                }
            }

            // Texture installing
            if (!MEMProcessHandler.CanTerminate())
            {
                M3Log.Information(@"Important texture operation in progress, prompting user to confirm cancellation");

                var reason = MEMProcessHandler.GetReasonShouldNotTerminate();
                if (reason != null)
                {
                    reason += "\n\n" // do not localize
                              + M3L.GetString(M3L.string_continueClosingTheApplicationQuestion);
                }

                var dialog = M3L.ShowDialog(this, reason, M3L.GetString(M3L.string_backgroundProcessRunning), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (dialog == MessageBoxResult.No)
                {
                    // Do not cancel.
                    M3Log.Information(@"User chose to not close the app");
                    IsOnTrackToClose = false;
                    return;
                }

                M3Log.Information(@"User chose to close the app");
            }

            // Mod installing
            if (!IsOnTrackToClose && BusyContentM3 is SingleItemPanel2 sip2 && sip2.Content is MMBusyPanelBase bpb)
            {
                if (!bpb.CanBeForceClosed() || bpb.Result.DoesResultModifyGame())
                {
                    M3Log.Information(@"The current panel result indicates there is pending merges. We are performing the merges and then the app will close.");
                    IsOnTrackToClose = true;
                    ExitInProgress = true;
                    queuedUserControls.Clear();
                    HandleBatchPanelResult = true;
                    if (bpb.CanBeForceClosed())
                    {
                        ReleaseBusyControl();
                        Title += @" - "
                        + M3L.GetString(M3L.string_cleaningUpPleaseWait);

                        M3L.ShowDialog(this, M3L.GetString(M3L.string_modManagerIsPerformingCleanupOperations) + "\n\n" + M3L.GetString(M3L.string_howToForceCloseM3), // do not localize
                            M3L.GetString(M3L.string_operationInProgress), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        bpb.SignalAppClosing();
                        M3Log.Information(@"Cannot force close current open panel, we will wait until it closes to perform cleanup");

                        Title += @" - "
                        + M3L.GetString(M3L.string_cleaningUpPleaseWait);
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_modManagerWillAutocloseBackgroundTasks) + "\n\n" + M3L.GetString(M3L.string_howToForceCloseM3), // do not localize
                            M3L.GetString(M3L.string_operationInProgress), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }
            }

            // Check download manager
            var downloads = DownloadManager.GetDownloads().Values;

            // Do downloads before imports because the dialog for downloads may stall long enough for an import to begin.
            var downloadInProgress = downloads.Any(x => x.IsDownloading);
            if (downloadInProgress)
            {
                M3Log.Information(@"Mods are currently downloading - prompting user if they really want to close");

                var abortResult = M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_dialog_exitWhileDownloading), M3L.GetString(M3L.string_downloadsInProgress),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (abortResult == MessageBoxResult.No)
                {
                    M3Log.Information(@"Mods are currently downloading - prompting user if they really want to close");
                    return;
                }
                else
                {
                    M3Log.Information(@"Continuing application close request");
                }
            }

            var importInProgress = downloads.Any(x => x.IsImporting);
            if (importInProgress)
            {
                M3Log.Warning(@"Cannot safely close app while mods are importing, aborting application exit request");
                M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_dialog_exitWhileImporting)
                    + "\n\n" // do not localize
                    + M3L.GetString(M3L.string_howToForceCloseM3), M3L.GetString(M3L.string_modsCurrentlyImporting),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ;
                return;
            }



            // Nothing pending.
            M3Log.Information(@"No pending tasks. The application will now close.");
            MEMProcessHandler.TerminateAll();
            ExitApp();
        }

        /// <summary>
        /// Marks that the app is ready to close and closes the window.
        /// </summary>
        private void ExitApp()
        {
            AppExiting = true;
            Close();
        }

        private void FailedMods_LinkClick(object sender, RoutedEventArgs e)
        {
            var failedModsPanel = new FailedModsPanel(M3LoadedMods.Instance.FailedMods.ToList());
            failedModsPanel.Close += (a, b) =>
            {
                ReleaseBusyControl();
                if (b.Data is Mod failedmod)
                {
                    NamedBackgroundWorker bw = new NamedBackgroundWorker(nameof(FailedMods_LinkClick));
                    bw.DoWork += (a, b) =>
                    {
                        ModUpdater.Instance.CheckModsForUpdates(new List<Mod>(new Mod[] { failedmod }), true);
                    };
                    bw.RunWorkerAsync();
                }
            };
            ShowBusyControl(failedModsPanel);
        }

        private void OpenModsDirectory_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenExplorer(M3LoadedMods.GetCurrentModLibraryDirectory());
        }

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

                    var updateCheckTask = BackgroundTaskEngine.SubmitBackgroundJob(@"UpdateCheck",
                        M3L.GetString(M3L.string_checkingForModManagerUpdates),
                        M3L.GetString(M3L.string_completedModManagerUpdateCheck));
                    try
                    {
                        ServerManifest.FetchOnlineStartupManifest(Settings.BetaMode, usePeriodicRefresh: true);
                    }
                    catch (Exception e)
                    {
                        //Error checking for updates!
                        M3Log.Exception(e, @"Checking for updates failed: ");
                        updateCheckTask.FinishedUIText = M3L.GetString(M3L.string_failedToCheckForUpdates);
                    }


                    if (!ServerManifest.HasManifest)
                    {
                        // load cached (will load nothing if there is no local file)
                        MixinHandler.LoadME3TweaksPackage();
                    }

                    BackgroundTaskEngine.SubmitJobCompletion(updateCheckTask);
                }

                #endregion

                M3ServiceLoader.LoadServices(bw, Settings.ForcePullContentNextBoot);
                Settings.ForcePullContentNextBoot = false; // We have pulled content now
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedText))); // Update localized tip shown
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedRichText))); // Update localized tip shown
                M3SupportedOS.StartupCompleted = true;

                if (firstStartupCheck)
                {
                    bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"WritePermissions",
                        M3L.GetString(M3L.string_checkingWritePermissions),
                        M3L.GetString(M3L.string_checkedUserWritePermissions));
                    CheckTargetPermissions(true);
                    BackgroundTaskEngine.SubmitJobCompletion(bgTask);
                    M3ProtocolHandler.SetupProtocolHandler();
                    if (Settings.ConfigureNXMHandlerOnBoot)
                    {
                        NexusModsUtilities.SetupNXMHandling();
                    }

                    // Setup initial tutorial messages.
                    ClipperHelper.ShowHideVerticalContent(OneTimeMessagePanel_HowToManageMods,
                        Settings.OneTimeMessage_ModListIsNotListOfInstalledMods, true);
                }

                if (MOnlineContent.CanFetchContentThrottleCheck())
                {
                    Settings.LastContentCheck = DateTime.Now;
                }

                M3Log.Information(@"End of content check network thread");
                b.Result = 0; //all good
            };
            bw.RunWorkerCompleted += async (a, b) =>
            {
                if (b.Error != null)
                {
                    // Log is handled in internal class
                }

                ContentCheckInProgress = false;

                if (firstStartupCheck)
                {
                    M3Utilities.WriteExeLocation();
                    await handleInitialPending();
                }

                if (Settings.GenerationSettingOT)
                {
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"BackupCheck");
                    nbw.DoWork += (a, b) =>
                    {
                        var me1CheckRequired = BackupService.GetGameBackupPath(MEGame.ME1) == null &&
                                               BackupService.GetGameBackupPath(MEGame.ME1, false) != null;
                        var me2CheckRequired = BackupService.GetGameBackupPath(MEGame.ME2) == null &&
                                               BackupService.GetGameBackupPath(MEGame.ME2, false) != null;
                        var me3CheckRequired = BackupService.GetGameBackupPath(MEGame.ME3) == null &&
                                               BackupService.GetGameBackupPath(MEGame.ME3, false) != null;

                        if (me1CheckRequired || me2CheckRequired || me3CheckRequired)
                        {
                            var bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"BackupCheck",
                                M3L.GetString(M3L.string_checkingBackups),
                                M3L.GetString(M3L.string_finishedCheckingBackups));
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

        /// <summary>
        /// First time handling pending when app initially boots.
        /// </summary>
        /// <returns>If the main window should be brought to the foreground or not.</returns>
        private async Task<bool> handleInitialPending()
        {
            bool shouldBringToFG = false;

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
                if (CommandLinePending.PendingM3Link != null)
                {
                    shouldBringToFG = true;
                    handleM3Link(CommandLinePending.PendingM3Link);
                    CommandLinePending.PendingM3Link = null;
                }
                if (CommandLinePending.PendingNXMLink != null)
                {
                    shouldBringToFG = true;
                    Activate();

                    if (NexusModsUtilities.UserInfo == null)
                    {
                        // Not logged in
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_nexusLoginRequiredForDownload), M3L.GetString(M3L.string_notSignedIn), MessageBoxButton.OK, MessageBoxImage.Error);
                        ShowNexusPanel();
                    }
                    else
                    {
                        if (DownloadManager.AddNXMDownload(CommandLinePending.PendingNXMLink) != null)
                        {
                            ShowDownloadManager();
                        }
                    }
                }
                if (CommandLinePending.PendingInstallBink && CommandLinePending.PendingGame != null)
                {
                    shouldBringToFG = true;
                    CommandLinePending.PendingInstallBink = false;
                    GameTargetWPF t = GetCurrentTarget(CommandLinePending.PendingGame.Value);
                    if (t != null)
                    {
                        M3Log.Information(
                            $@"Installing Bink Bypass (command line request) for {CommandLinePending.PendingGame.Value}");
                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"BinkInstallAutomated",
                            M3L.GetString(M3L.string_installingBinkASILoader),
                            M3L.GetString(M3L.string_installedBinkASILoader));
                        try
                        {
                            t.InstallBinkBypass(true);
                        }
                        catch (Exception)
                        {
                            task.FinishedUIText = M3L.GetString(M3L.string_failedToInstallBinkASILoader);
                        }

                        BackgroundTaskEngine
                            .SubmitJobCompletion(task); // This is just so there's some visual feedback to the user
                    }

                    CommandLinePending.ClearGameDependencies();
                }

                if (CommandLinePending.PendingInstallASIID > 0 && CommandLinePending.PendingGame != null)
                {
                    M3Log.Information($@"ASI installation requested by command line: {CommandLinePending.PendingInstallASIID} to {CommandLinePending.PendingGame}");
                    if (CommandLinePending.PendingInstallASIVersion > 0)
                    {
                        M3Log.Information($@"Requested version: {CommandLinePending.PendingInstallASIVersion}");
                    }
                    else
                    {
                        M3Log.Information($@"Requested version: Latest");
                    }

                    shouldBringToFG = true;
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
                            CurrentOperationText = M3L.GetString(M3L.string_interp_installingASIMod);
                            var result = await ASIManager.InstallASIToTargetByGroupID(CommandLinePending.PendingInstallASIID, @"Automated command line request", t, CommandLinePending.PendingInstallASIVersion, includeHiddenASIs: true);

                            if (result)
                            {
                                M3Log.Information($@"ASI installed successfully (command line request)!");
                                CurrentOperationText = M3L.GetString(M3L.string_installedASIModByCommandLineRequest);
                            }
                            else
                            {
                                M3Log.Error($@"ASI failed to install (command line request)!");
                                CurrentOperationText = M3L.GetString(M3L.string_failedToInstallASIModByCommandLineRequest);
                            }
                        }
                    }

                    // Install-ASI is its own command
                    CommandLinePending.PendingGame = null;
                    CommandLinePending.PendingInstallASIID = 0;
                    CommandLinePending.ClearGameDependencies();
                }

                if (CommandLinePending.PendingMergeDLCCreation && CommandLinePending.PendingGame != null)
                {
                    GameTargetWPF t = GetCurrentTarget(CommandLinePending.PendingGame.Value);
                    if (t != null)
                    {
                        // Need standard entry to merge DLC
                        // Todo: This might need to be put into a run and done to ensure it executes in-order
                        var result = new PanelResult();
                        result.AddTargetMerges(t);

                        // Handle the panel result
                        HandlePanelResult(result);
                        CommandLinePending.PendingMergeDLCCreation = false;
                    }
                }

                if (CommandLinePending.PendingAutoModInstallPath != null &&
                    File.Exists(CommandLinePending.PendingAutoModInstallPath))
                {
                    shouldBringToFG = true;
                    Mod m = new Mod(CommandLinePending.PendingAutoModInstallPath, MEGame.Unknown);
                    if (m.ValidMod)
                    {
                        GameTargetWPF t = GetCurrentTarget(m.Game);
                        if (t != null)
                        {
                            ApplyMod(m, t, installCompletedCallback: (installed, isFirst) =>
                            {
                                // isFirst is not used
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

                if (CommandLinePending.PendingMergeModCompileManifest != null && CommandLinePending.PendingFeatureLevel > 0
                    && File.Exists(CommandLinePending.PendingMergeModCompileManifest))
                {
                    shouldBringToFG = true;
                    CompileMergeMod(CommandLinePending.PendingMergeModCompileManifest, CommandLinePending.PendingFeatureLevel);
                    CommandLinePending.PendingMergeModCompileManifest = null;
                    CommandLinePending.PendingFeatureLevel = 0;
                }

            }
            catch (Exception e)
            {
                M3Log.Error($@"Error handling pending command line actions: {e.Message}");
                M3Log.Error(e.FlattenException());
            }

            //App.PendingGameBoot = null; // this is not cleared here as it will be used at end of applymod above
            CommandLinePending.PendingNXMLink = null;
            return shouldBringToFG;
        }


        private void handleM3Link(string pendingM3Link)
        {
            M3ProtocolHandler.HandleLink(pendingM3Link, this);
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
            if (tool == null)
                throw new Exception(
                    @"LaunchExternalTool handler set but no relevant tool was specified! This is a bug. Please report it to Mgamerz on Discord");
            LaunchExternalTool(tool);
        }

        private void LaunchExternalTool(string tool, string arguments = null)
        {
            if (tool != null)
            {
                TelemetryInterposer.TrackEvent(@"Launched external tool", new Dictionary<string, string>()
                {
                    { @"Tool name", tool },
                    { @"Arguments", arguments }
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
                { @"Invocation method", @"Menu" }
            });
            var exLauncher = new ASIManagerPanel(SelectedGameTarget);
            exLauncher.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(exLauncher);
        }
        private void UploadLog_Click(object sender, RoutedEventArgs e)
        {
            ShowLogUploadPanel(null);
        }

        internal void ShowLogUploadPanel(GameTarget selectedTarget)
        {
            var logUploaderUI = new LogUploaderPanel(selectedTarget);
            logUploaderUI.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(logUploaderUI);
        }

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

        private void CompileMergeMod(string file, double featureLevel = 0)
        {
            var version = MergeModLoader.GetMergeModVersionForCompile(this, file);
            if (version == null)
                return; // User canceled.

            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"M3MCompile", M3L.GetString(M3L.string_compilingMergemod), M3L.GetString(M3L.string_compiledMergemod));
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"MergeModCompiler");
            nbw.DoWork += (o, args) =>
            {
                MergeModLoader.SerializeManifest(file, version.Value);
            };
            nbw.RunWorkerCompleted += (o, args) =>
            {
                if (args.Error != null)
                {
                    task.FinishedUIText = M3L.GetString(M3L.string_failedToCompileMergemod);
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                    M3Log.Error($@"Error compiling m3m mod file: {args.Error.Message}");
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorCompilingm3mX, args.Error.Message),
                        M3L.GetString(M3L.string_errorCompilingm3m), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                }
            };
            nbw.RunWorkerAsync();
        }

        private void openModImportUI(string archiveFile, Stream archiveStream = null, bool priority = false, NexusProtocolLink sourceLink = null)
        {
            M3Log.Information(@"Opening Mod Archive Importer for file " + archiveFile);
            var modInspector = new ModArchiveImporterPanel(archiveFile, archiveStream, link: sourceLink);
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

                if (modInspector.MAI.ImportedLETextureMod)
                {
                    Settings.GenerationSettingLE = true; // Force on
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_textureModsImportedHowToUse), M3L.GetString(M3L.string_textureModsImported), MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (modInspector.MAI.ImportedOTTextureMod)
                {
                    Settings.GenerationSettingOT = true; // Force on
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_otTexturesImported), M3L.GetString(M3L.string_mustUseExternalTool), MessageBoxButton.OK, MessageBoxImage.Warning);
                }


                if (modInspector.MAI.ImportedBatchQueue)
                {
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_importedBatchInstallGroup), M3L.GetString(M3L.string_installGroupImported), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            // 05/29/2024 - Change to null-only assignment as it would wipe out an existing BatchPanelResult, such as from the Mod Update list.
            // This only is used 
            BatchPanelResult ??= new PanelResult();
            HandleBatchPanelResult = false;
            ShowBusyControl(modInspector, priority);
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

        private void AutoTOCTarget(GameTarget target, bool showInStatusBar = true)
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

        internal void SetTheme(bool isFirstBoot)
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, Settings.DarkTheme ? ResourceLocator.DarkColorScheme : ResourceLocator.LightColorScheme);
            if (!isFirstBoot)
            {
                foreach (Window w in Application.Current.Windows)
                {
                    try
                    {
                        w.ApplyDarkNetWindowTheme();
                    }
                    catch
                    {
                        // Visual Studio adds an 'AdornerWindow' which doesn't like this call
                    }
                }
            }
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(M3OnlineContent.MODDESC_DOCUMENTATION_LINK);
        }

        private void OpenMemoryAnalyzer_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            new M3MemoryAnalyzer().Show();
#endif
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
                M3Log.Information($@"Listing installable files for {SelectedMod.ModName}");
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
            var localmd5 = MUtilities.CalculateHash(@"null");
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
            Settings.OneTimeMessage_LE1CoalescedOverwriteWarning = true;
        }

        private void InstallMEMFiles()
        {
            string filter = M3L.GetString(M3L.string_massEffectModderFiles) + @"|*.mem";
            OpenFileDialog m = new OpenFileDialog
            {
                Title = M3L.GetString(M3L.string_selectMemFile),
                Filter = filter,
                Multiselect = true,
                CustomPlaces = M3CustomPlaces.TextureLibraryCustomPlace, // Only one
                InitialDirectory = M3LoadedMods.GetTextureLibraryDirectory(SelectedGameTarget?.Game ?? MEGame.Unknown)
            };
            var result = m.ShowDialog(this);
            if (result != true)
                return;

            MEGame game = MEGame.Unknown;
            GameTarget target = null;

            foreach (var file in m.FileNames)
            {
                var fileGame = ModFileFormats.GetGameMEMFileIsFor(file);
                if (game == MEGame.Unknown)
                {
                    game = fileGame;
                }
                if (!game.IsLEGame())
                {
                    M3Log.Error($@"User attempting to install mem to unsupported game: {game}");
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_gameUnsupportedForTextureModding, game),
                        M3L.GetString(M3L.string_unsupportedGame), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (fileGame != game)
                {
                    M3Log.Error($@"User attempting to install multiple game's mems, this is not supported.");
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_allMemFilesMustBeForTheSameGame), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                target ??= GetCurrentTarget(game);
                if (target == null)
                {
                    M3Log.Error($@"User attempting to install mem to game that is not currently a target: {game}");
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_notTargetAvailableForX, game),
                        M3L.GetString(M3L.string_gameNotAvailable), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }


            TextureInstallerPanel tip = new TextureInstallerPanel(target, m.FileNames.ToList());
            tip.Close += (a, b) =>
            {
                ReleaseBusyControl();
            };
            ShowBusyControl(tip);
        }

        private void OnWindowLostFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(@"Window has lost focus");
        }

        /// <summary>
        /// Looks at the active panel, and any queued panels, and returns if the listed type is among any of them
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool HasAnyQueuedPanelsOfType(Type type)
        {
            if (BusyContentM3 is SingleItemPanel2 sip && sip.Content.GetType() == type)
            {
                return true;
            }

            return queuedUserControls.Any(x => x.GetType() == type);
        }

        private void ConvertMEMToTextureOverride()
        {
            var converter = new MEMToTOConverter(this);
            if (converter.SetupConversion())
            {
                converter.BeginConversion();
            }
        }

        /// <summary>
        /// Gets the current active panel, if any.
        /// </summary>
        /// <returns></returns>
        public MMBusyPanelBase GetCurrentPanel()
        {
            MMBusyPanelBase result = null;

            // This must be run on the UI thread. Hopefully it doesn't
            // need more synchronization...
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (BusyContentM3 is SingleItemPanel2 sip)
                {
                    if (sip.Content is MMBusyPanelBase mmBusyPanel)
                    {
                        result = mmBusyPanel;
                    }
                }
            });

            return result;
        }

        private void OnBottomGameIDImageClick(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Settings.AlphaMode = true;
            }
        }

        /// <summary>
        /// Downloads and installs the Microsoft Visual C++ Redistributable in the background.
        /// </summary>
        /// <remarks>This method submits a background job to download and install the Microsoft Visual C++
        /// Redistributable package. The job status is updated based on the success or failure of the installation. This
        /// method is intended for internal use and should not be called directly from user code.</remarks>
        internal async Task<bool> InstallMSVCPP()
        {
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"MSVCPPInstall", LC.GetString(LC.string_downloadingMicrosoftVisualCPPRedistributable), M3L.GetString(M3L.string_installedMicrosoftVisualCPPRedistributable));
            var pi = new ProgressInfo();
            pi.OnUpdate = (upd) =>
            {
                if (upd.Indeterminate)
                {
                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, upd.Status);
                }
                else
                {
                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, upd.Status + $@" {upd.Value:F0}%");
                }
            };
            var result = await MSVCPP.DownloadAndInstallVCRedistx64Async(pi);
            if (result)
            {
                task.FinishedUIText = M3L.GetString(M3L.string_installedMicrosoftVisualCPPRedistributable);
            }
            else
            {
                task.FinishedUIText = M3L.GetString(M3L.string_failedToInstallMicrosoftVisualCPPRedistributable);
            }
            BackgroundTaskEngine.SubmitJobCompletion(task);
            return result;
        }
    }
}
