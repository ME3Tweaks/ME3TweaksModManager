using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.launcher;
using System.Windows;
using System.Windows.Controls;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for LaunchParametersDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class LaunchParametersDialog : Window
    {
        public GameTargetWPF SelectedGameTarget { get; }

        /// <summary>
        /// The loaded launch package
        /// </summary>
        public LaunchOptionsPackage LaunchPackage { get; set; }

        private MainWindow mainWindow;

        /// <summary>
        /// List of languages that are supported by this game
        /// </summary>
        public ObservableCollectionExtended<LauncherLanguageOption> LanguageOptions { get; } = new();
        /// <summary>
        /// List of custom command line parameters
        /// </summary>
        public ObservableCollectionExtended<LauncherCustomParameter> CustomOptions { get; } = new();

        #region Commands
        public GenericCommand LaunchGameCommand { get; private set; }
        #endregion

        public LaunchParametersDialog(MainWindow window, GameTargetWPF selectedGameTarget, LaunchOptionsPackage package)
        {
            mainWindow = window;
            Owner = window;
            SelectedGameTarget = selectedGameTarget;

            LoadPackage(package);
            LoadCommands();
            InitializeComponent();
        }

        private void LoadPackage(LaunchOptionsPackage package)
        {
            LaunchPackage = package ?? new LaunchOptionsPackage() { Game = SelectedGameTarget.Game, ChosenLanguage = @"INT" };
            LoadLanguagesAndOptions();

            var chosenLang = LanguageOptions.FirstOrDefault(x => x.LanguageString == LaunchPackage.ChosenLanguage);
            if (chosenLang != null) chosenLang.UIIsSelected = true;
        }

        private void LoadLanguagesAndOptions()
        {
            CustomOptions.Clear();
            LanguageOptions.Clear();

            // Global options
            CustomOptions.Add(new LauncherCustomParameter() { DisplayString = M3L.GetString(M3L.string_automaticallyResumeLastSave), CommandLineText = @"-RESUME" });

            switch (LaunchPackage.Game)
            {
                case MEGame.LE1:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"English", LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French", LanguageString = @"FR" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French text, English voiceover", LanguageString = @"FE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German", LanguageString = @"DE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German text, English voiceover", LanguageString = @"GE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian", LanguageString = @"IT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian text, English voiceover", LanguageString = @"IE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Spanish text, English voiceover", LanguageString = @"ES" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Japanese text, English voiceover", LanguageString = @"JA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Polish", LanguageString = @"PL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Polish text, English voiceover", LanguageString = @"PLPC" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Russian", LanguageString = @"RA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Russian text, English voiceover", LanguageString = @"RU" });

                    // Unofficial ones in game files
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Czech text (partial), English voiceover", LanguageString = @"CS" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Hungarian text (partial), English voiceover", LanguageString = @"HU" });

                    break;
                case MEGame.LE2:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"English", LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French", LanguageString = @"FRA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French text, English voiceover", LanguageString = @"FRE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German", LanguageString = @"DEU" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German text, English voiceover", LanguageString = @"DEE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian", LanguageString = @"ITA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian text, English voiceover", LanguageString = @"ITE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Spanish text, English voiceover", LanguageString = @"ESN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Japanese text, English voiceover", LanguageString = @"JPN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Polish", LanguageString = @"POL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Polish text, English voiceover", LanguageString = @"POE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Russian text, English voiceover", LanguageString = @"RUS" });

                    // LE2 has no unofficial localizations.
                    break;
                case MEGame.LE3:
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"English", LanguageString = @"INT" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French", LanguageString = @"FRA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"French text, English voiceover", LanguageString = @"FRE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German", LanguageString = @"DEU" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"German text, English voiceover", LanguageString = @"DEE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian", LanguageString = @"ITA" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Italian text, English voiceover", LanguageString = @"ITE" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Spanish text, English voiceover", LanguageString = @"ESN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Japanese text, English voiceover", LanguageString = @"JPN" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Polish text, English voiceover", LanguageString = @"POL" });
                    LanguageOptions.Add(new LauncherLanguageOption { DisplayString = @"Russian text, English voiceover", LanguageString = @"RUS" });
                    break;

                    // LE3 has no unofficial localizations.
            }
        }

        private void LoadCommands()
        {
            LaunchGameCommand = new GenericCommand(LaunchGame, CanLaunchGame);
        }

        private bool CanLaunchGame()
        {
            //if (string.IsNullOrWhiteSpace(GetGameLang())) return false;
            return true;
        }

        public void LaunchGame()
        {
            string args = $@" -game {LaunchPackage.Game.ToMEMGameNum()} -autoterminate -NoHomeDir -Subtitles {LaunchPackage.SubtitleSize} ";

            if (LaunchPackage.Game == MEGame.LE3)
            {
                args += $@"-language={LaunchPackage.ChosenLanguage}";
            }
            else
            {
                args += $@"-OVERRIDELANGUAGE={LaunchPackage.ChosenLanguage}";
            }

            // Custom options
            foreach (var v in CustomOptions.Where(x => x.IsSelected))
            {
                args += $@" {v.CommandLineText}";
            }

            mainWindow.InternalStartGame(SelectedGameTarget, args);
        }

        private void SubtitleSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == SubSizeUp)
            {
                LaunchPackage.SubtitleSize++;
            }
            if (sender == SubSizeDown && LaunchPackage.SubtitleSize > 1)
            {
                LaunchPackage.SubtitleSize--;
            }
        }

        private void LanguageButtonSelected(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.DataContext is LauncherLanguageOption llo)
            {
                LaunchPackage.ChosenLanguage = llo.LanguageString;
            }
        }
    }
}
