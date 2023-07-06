using System.Windows;
using System.Windows.Input;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OptionsPanel.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OptionsPanel : MMBusyPanelBase
    {
        public OptionsPanel()
        {
            LibraryDir = M3LoadedMods.GetCurrentModLibraryDirectory();
            NexusModsDownloadCache = Settings.ModDownloadCacheFolder;
            LoadCommands();
        }


        public string LibraryDir { get; set; }
        public string NexusModsDownloadCache { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand ChangeLibraryDirCommand { get; set; }
        public ICommand ChangeNexusModsDownloadCacheCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
            ChangeLibraryDirCommand = new GenericCommand(ChangeLibraryDir);
            ChangeNexusModsDownloadCacheCommand = new GenericCommand(ChangeNexusModsDownloadCacheDir);
        }

        private void ChangeLibraryDir()
        {

            if (M3LoadedMods.ChooseModLibraryPath(window, false))
            {
                Result.ReloadMods = true;
                LibraryDir = Settings.ModLibraryPath;
            }
        }

        private void ChangeNexusModsDownloadCacheDir()
        {
            var choseTempCache = M3L.ShowDialog(window,
                M3L.GetString(M3L.string_dialog_selectDownloadCacheType),
                M3L.GetString(M3L.string_chooseCacheType), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                yesContent: M3L.GetString(M3L.string_customDirectory), M3L.GetString(M3L.string_temporaryCache)) == MessageBoxResult.No;
            if (choseTempCache)
            {
                Settings.ModDownloadCacheFolder = NexusModsDownloadCache = null;
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
                    Settings.ModDownloadCacheFolder = NexusModsDownloadCache = m.FileName;

                }
            }
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
        }

        private void ChangeTheme_Dark_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeTheme(true);
        }

        private void ChangeTheme_Light_Clicked(object sender, RoutedEventArgs e)
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

        private void ChangeSetting_Clicked(object sender, RoutedEventArgs e)
        {
            //When this method is called, the value has already changed. So check against the opposite boolean state.
            var callingMember = sender as FrameworkElement;

            if (callingMember == BetaMode_MenuItem && Settings.BetaMode)
            {
                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_optingIntoBeta), M3L.GetString(M3L.string_enablingBetaMode), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.BetaMode = false; //turn back off.
                    return;
                }
            }
            else if (callingMember == EnableTelemetry_MenuItem && !Settings.EnableTelemetry)
            {
                //user trying to turn it off 
                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialogTurningOffTelemetry), M3L.GetString(M3L.string_turningOffTelemetry), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.EnableTelemetry = true; //keep on.
                    return;
                }

                M3Log.Warning(@"Turning off telemetry :(");
                //Turn off telemetry.
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
            }
            else if (callingMember == EnableTelemetry_MenuItem)
            {
                //turning telemetry on
                M3Log.Information(@"Turning on telemetry :)");
                Analytics.SetEnabledAsync(true);
                Crashes.SetEnabledAsync(true);
            }
            else
            {
                //unknown caller. Might just be settings on/off for logging.
            }
        }
    }
}
