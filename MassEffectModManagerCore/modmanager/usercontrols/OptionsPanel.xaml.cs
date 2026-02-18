using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.usercontrols.options;
using ME3TweaksModManager.ui;
using Microsoft.Win32;

#if WITH_APPCENTER
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
#endif

using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OptionsPanel.xaml (V2)
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OptionsPanel : MMBusyPanelBase
    {
        private bool optingIntoBetaFlowActive;

        public OptionsPanel()
        {
            LoadCommands();

            // INITIALIZE SETTINGS
            var settingsType = typeof(Settings);

            SettingGroups =
            [
                // Main options
                new M3SettingGroup()
                {
                    GroupName = M3L.GetString(M3L.string_mainOptions),
                    GroupDescription = M3L.GetString(M3L.string_standardOptionsForME3TweaksModManager),
                    AllSettings = [
                        new M3DirectorySetting(settingsType, nameof(Settings.ModLibraryPath), M3L.string_modLibraryLocation, M3L.string_description_modsImportedAreStoredInLibrary, GetModLibraryWatermark, M3L.string_configure, ChangeLibraryDir),
                        new M3DirectorySetting(settingsType, nameof(Settings.ModDownloadCacheFolder), M3L.string_nexusModsDownloadFolder, M3L.string_description_nexusModsDownloadFolder, GetNexusDownloadLocationWatermark, M3L.string_configure, ChangeNexusModsDownloadCacheDir),

                        new M3BooleanSetting(settingsType, nameof(Settings.DeveloperMode), M3L.string_Developermode, M3L.string_wp_description_developermod),
                        new M3BooleanSetting(settingsType, nameof(Settings.EnableTelemetry), M3L.string_Enabletelemetry, M3L.string_wp_description_telemetry, ChangingTelemetrySetting),
                        new M3BooleanSetting(settingsType, nameof(Settings.BetaMode), M3L.string_optIntoBetaUpdates, M3L.string_tooltip_optIntoBetaUpdates, ChangingBetaSetting),
                        new M3BooleanSetting(settingsType, nameof(Settings.ConfigureNXMHandlerOnBoot), M3L.string_configureNxmHandlerOnBoot, M3L.string_tooltip_configureNxmHandlerOnBoot),
                        new M3BooleanSetting(settingsType, nameof(Settings.DoubleClickModInstall), M3L.string_doubleClickModInLibraryToInstall, M3L.string_description_doubleClickModInLibraryToInstall),
                        new M3BooleanSetting(settingsType, nameof(Settings.PlayDialogSounds), M3L.string_playDialogSounds, M3L.string_description_playDialogSounds),

                        // Add new settings here


                        new M3ImageOptionsSetting(M3L.string_applicationTheme,
                            new SingleImageOption(@"/images/lighttheme.png", M3L.string_light, SetLightTheme),
                            new SingleImageOption(@"/images/darktheme.png", M3L.string_dark, SetDarkTheme))

                    ]
                },

                // Beta mode settings
                new M3SettingGroup()
                {
                    GroupName = M3L.GetString(M3L.string_betaModeSettings),
                    GroupDescription = M3L.GetString(M3L.string_theseSettingsAreAvailableWhenTheApplicationIsInBetaMode),
                    VisibilityDelegate = () => Settings.BetaMode,
                    AllSettings = [
                       new M3BooleanSetting(settingsType, nameof(Settings.ShowInstalledModsInLibrary), M3L.string_showModInstallationStatusInLbrary, M3L.string_description_showInstalledModsInLibrary),
                    ]
                },

                // Legendary Edition options
                new M3SettingGroup()
                {
                    GroupName = M3L.GetString(M3L.string_legendaryEditionOptions),
                    GroupDescription = M3L.GetString(M3L.string_description_leOptions),
                    VisibilityDelegate = () => Settings.GenerationSettingLE,
                    AllSettings = [
                        new M3BooleanSetting(settingsType, nameof(Settings.SkipLELauncher), M3L.string_launcherAutobootSelectedGame, M3L.string_description_autobootLE),
                        new M3BooleanSetting(settingsType, nameof(Settings.EnableLE1CoalescedMerge), M3L.string_lE1EnableCoalescedMerge, M3L.string_description_le1CoalescedMergeOption),
                        new M3BooleanSetting(settingsType, nameof(Settings.EnableLE12DAMerge), M3L.string_LE1Enable2DAMerge, M3L.string_description_LE1Enable2DAMerge),
                    ]
                },

                // Original Trilogy Options
                 new M3SettingGroup()
                {
                    GroupName = M3L.GetString(M3L.string_originalTrilogySettings),
                    GroupDescription = M3L.GetString(M3L.string_description_originalTrilogySettings),
                    VisibilityDelegate = () => Settings.GenerationSettingOT,
                    AllSettings = [
                        new M3BooleanSetting(settingsType, nameof(Settings.PreferCompressingPackages), M3L.string_preferCompressingPackages, M3L.string_description_preferCompressingPackages),
                        new M3BooleanSetting(settingsType, nameof(Settings.AutoUpdateLODs4K), M3L.string_autoUpdateLODs4K, M3L.string_tooltip_autoLods4K),
                        new M3BooleanSetting(settingsType, nameof(Settings.AutoUpdateLODs2K), M3L.string_autoUpdateLODs2K, M3L.string_tooltip_autoLods2K),
                    ]
                },

                // Logging options
                new M3SettingGroup()
                {
                    GroupName = M3L.GetString(M3L.string_Logging),
                    GroupDescription = M3L.GetString(M3L.string_description_logging),
                    AllSettings = [
                        new M3BooleanSetting(settingsType, nameof(Settings.LogModStartup), M3L.string_LogModStartup, M3L.string_tooltip_logModLoading),
                        new M3BooleanSetting(settingsType, nameof(Settings.LogModInstallation), M3L.string_LogModInstallation, M3L.string_tooltip_logModInstaller),
                        new M3BooleanSetting(settingsType, nameof(Settings.LogModUpdater), M3L.string_LogModUpdater, M3L.string_tooltip_logModUpdater),
                        new M3BooleanSetting(settingsType, nameof(Settings.LogBackupAndRestore), M3L.string_logAllFilesCopiedDuringRestore, M3L.string_description_logOptionLotsa),
                        new M3BooleanSetting(settingsType, nameof(Settings.LogModMakerCompiler), M3L.string_LogModMakerCompiler, M3L.string_tooltip_logModMakerCompiler),

                    ]
                }
            ];
        }


        private bool ChangingBetaSetting()
        {
            // Did the setting just change as they opted in?
            if (Settings.BetaMode)
            {
                optingIntoBetaFlowActive = true;

                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_optingIntoBeta),
                    M3L.GetString(M3L.string_enablingBetaMode), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.BetaMode = false; //turn back off.
                }
                optingIntoBetaFlowActive = false;
            }
            else if (!optingIntoBetaFlowActive)
            {
                var result = M3L.ShowDialog(mainwindow,
                    M3L.GetString(M3L.string_downgradeModManagerToTheCurrentStableVersionQuestion),
                    M3L.GetString(M3L.string_disablingBetaMode),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    Task.Run(() =>
                    {
                        M3UpdateCheck.DowngradeToStable();
                    });
                }
            }
            return true;
        }

        private bool ChangingTelemetrySetting()
        {
#if WITH_APPCENTER
            if (!Settings.EnableTelemetry)
            {
                //user trying to turn it off 
                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialogTurningOffTelemetry), M3L.GetString(M3L.string_turningOffTelemetry), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.EnableTelemetry = true; //keep on.
                    return true;
                }

                M3Log.Warning(@"Turning off telemetry :(");

                // Immediately turn off telemetry per user request
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
            }
            else
            {
                //turning telemetry on
                M3Log.Information(@"Turning on telemetry :)");

                // Immediately turn on telemetry per user request
                Analytics.SetEnabledAsync(true);
                Crashes.SetEnabledAsync(true);
            }
#endif
            return true;
        }

        private string GetNexusDownloadLocationWatermark()
        {
            if (Settings.ModDownloadCacheFolder != null)
                return Settings.ModDownloadCacheFolder;

            // Setting not defined
            return M3L.GetString(M3L.string_defaultTemporaryDownloadCache);
        }

        private string GetModLibraryWatermark()
        {
            if (!Settings.DeveloperMode && !Settings.BetaMode)
            {
                // Do not show the default instance info.
                return M3LoadedMods.GetCurrentModLibraryDirectory();
            }

            // Dev mode, allow showing default instance.
            if (M3LoadedMods.IsSharedLibrary())
            {
                return M3LoadedMods.GetCurrentModLibraryDirectory();
            }
            else
            {
                return M3L.GetString(M3L.string_localLibraryWatermark);
            }

        }

        public ICommand CloseCommand { get; set; }
        public ObservableCollectionExtended<M3SettingGroup> SettingGroups { get; init; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
        }

        private void ChangeLibraryDir(object o)
        {
            if (o is M3DirectorySetting m3ds)
            {
                if (M3LoadedMods.ChooseModLibraryPath(window, false))
                {
                    Result.ReloadMods = true;
                    m3ds.UpdateWaterMark();
                }
            }
        }

        private void ChangeNexusModsDownloadCacheDir(object o)
        {
            if (o is M3DirectorySetting m3ds)
            {
                var choseTempCache = M3L.ShowDialog(window,
                                         M3L.GetString(M3L.string_dialog_selectDownloadCacheType),
                                         M3L.GetString(M3L.string_chooseCacheType), MessageBoxButton.YesNo,
                                         MessageBoxImage.Question,
                                         MessageBoxResult.No,
                                         yesContent: M3L.GetString(M3L.string_customDirectory),
                                         M3L.GetString(M3L.string_temporaryCache)) ==
                                     MessageBoxResult.No;
                if (choseTempCache)
                {
                    Settings.ModDownloadCacheFolder = null;
                }
                else
                {
                    if (WineWorkarounds.WineDetected)
                    {
                        var m = new OpenFolderDialog
                        {
                            ValidateNames = true,
                            Title = M3L.GetString(M3L.string_selectNexusModsDownloadDirectory)
                        };
                        if (m.ShowDialog(window) == true)
                        {
                            Settings.ModDownloadCacheFolder = m.FolderName;
                        }
                    }
                    else
                    {
                        CommonOpenFileDialog m = new CommonOpenFileDialog
                        {
                            IsFolderPicker = true,
                            EnsurePathExists = true,
                            Title = M3L.GetString(M3L.string_selectNexusModsDownloadDirectory)
                        };
                        if (m.ShowDialog(window) == CommonFileDialogResult.Ok)
                        {
                            Settings.ModDownloadCacheFolder = m.FileName;
                        }
                    }
                }

                // Refresh the control
                m3ds.UpdateWaterMark();
            }
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        protected override void OnClosing(DataEventArgs args)
        {
            Settings.StaticPropertyChanged -= Settings_PropertyChanged;
            base.OnClosing(args);
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            Settings.StaticPropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Refresh visibility when settings that affect group visibility change
            if (e.PropertyName == nameof(Settings.BetaMode))
            {
                RefreshVisibilities();
            }
        }

        private void SetDarkTheme()
        {
            ChangeTheme(true);
        }

        private void SetLightTheme()
        {
            ChangeTheme(false);
        }

        private void ChangeTheme(bool dark)
        {
            if (Settings.DarkTheme ^ dark)
            {
                Settings.DarkTheme = !Settings.DarkTheme;
                //Settings.Save();
                mainwindow.SetTheme(false);
            }
        }

        private void RefreshVisibilities()
        {
            foreach (var setting in SettingGroups)
            {
                setting.RefreshVisibility();
            }
        }
    }
}
