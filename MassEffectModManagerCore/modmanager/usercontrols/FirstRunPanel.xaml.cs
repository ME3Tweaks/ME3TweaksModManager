using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FontAwesome5;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.loaders;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Pathoschild.Http.Client;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for PreviewWelcomePanel.xaml
    /// </summary>
    public partial class FirstRunPanel : MMBusyPanelBase
    {
        public EFontAwesomeIcon ActiveIcon { get; set; }
        public bool SpinIcon { get; set; }
        public bool VisibleIcon { get; set; }


        public FirstRunPanel()
        {
            DataContext = this;
            LibraryDir = M3LoadedMods.GetCurrentModLibraryDirectory();
            LoadCommands();
        }

        public bool IsAuthorized { get; set; }
        public ICommand ChangeLibraryDirCommand { get; set; }
        public GenericCommand AuthorizeCommand { get; set; }
        public GenericCommand CloseCommand { get; set; }
        public bool IsAuthorizing { get; private set; }

        private void LoadCommands()
        {
            AuthorizeCommand = new GenericCommand(AuthorizeWithNexus, CanAuthorizeWithNexus);
            CloseCommand = new GenericCommand(CloseInternal, CanClose);
            ChangeLibraryDirCommand = new GenericCommand(ChangeLibraryDir);
        }

        private bool CanClose() => !IsAuthorizing;
        private bool CanAuthorizeWithNexus() => !IsAuthorized && !IsAuthorizing;

        private void ChangeLibraryDir()
        {
            if (M3LoadedMods.ChooseModLibraryPath(window, false))
            {
                LibraryDir = Settings.ModLibraryPath;
            }
        }

        public string LibraryDir { get; set; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                e.Handled = true;
                CloseInternal();
            }
        }

        private void CloseInternal()
        {
            M3Log.Information(@"Completing first run panel onboarding");
            if (Settings.EnableTelemetry)
            {
                // Start app center
                App.InitAppCenter();
            }
            App.FlushTelemetryItems(); // Push through any pending telemetry items
            Result.ReloadMods = true; // User has now chosen their mod library
            OnClosing(new DataEventArgs(true));
            Settings.ShowedPreviewPanel = true;
            //Settings.Save();
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            M3Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            CloseInternal();
        }

        private void ChangeLang_DEU_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"deu");

        }

        private void ChangeLang_RUS_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"rus");
        }

        private void ChangeLang_POL_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"pol");
        }

        private void ChangeLanguage(string lang)
        {
            mainwindow.SetApplicationLanguageAsync(lang, false);
            SetAuthorized(IsAuthorized);
            //Settings.Save();
        }

        private void ChangeLang_INT_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"int");
        }

        private void ChangeLang_ITA_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"ita");
        }

        private void ChangeLang_FRA_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"fra");
        }

        private void ChangeLang_CZE_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"fra");
        }

        private void ChangeLang_ESN_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"esn");
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
                mainwindow.SetTheme();
            }
        }

        private string AuthorizedToNexusUsername; //for localization

        public string AuthorizeToNexusText { get; set; } = M3L.GetString(M3L.string_authenticateToNexusMods);

        //this is copied from NexusModsLogin.xaml.cs cause I'm too lazy to make it shared code for what will likely never change
        private void AuthorizeWithNexus()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"NexusAPICredentialsCheck");
            nbw.DoWork += async (a, b) =>
            {
                IsAuthorizing = true;
                VisibleIcon = true;
                SpinIcon = true;
                ActiveIcon = EFontAwesomeIcon.Solid_Spinner;
                AuthorizeCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
                AuthorizeToNexusText = M3L.GetString(M3L.string_pleaseWait);

                var apiKeyReceived = await NexusModsUtilities.SetupNexusLogin(x => Debug.WriteLine(x));
                Application.Current.Dispatcher.Invoke(delegate { mainwindow.Activate(); });
                if (!string.IsNullOrWhiteSpace(apiKeyReceived))
                {
                    //Check api key
                    AuthorizeToNexusText = M3L.GetString(M3L.string_checkingKey);
                    try
                    {
                        var authInfo = NexusModsUtilities.AuthToNexusMods(apiKeyReceived).Result;
                        if (authInfo != null)
                        {
                            using FileStream fs = new FileStream(System.IO.Path.Combine(M3Filesystem.GetNexusModsCache(), @"nexusmodsapikey"), FileMode.Create);
                            File.WriteAllBytes(System.IO.Path.Combine(M3Filesystem.GetNexusModsCache(), @"entropy"), NexusModsUtilities.EncryptStringToStream(apiKeyReceived, fs));
                            fs.Close();
                            SetAuthorized(true);
                            mainwindow.RefreshNexusStatus();
                            AuthorizedToNexusUsername = authInfo.Name;
                            TelemetryInterposer.TrackEvent(@"Authenticated to NexusMods");
                        }
                        else
                        {
                            M3Log.Error(@"Error authenticating to nexusmods, no userinfo was returned, possible network issue");
                            SetAuthorized(false);
                            mainwindow.RefreshNexusStatus();
                        }
                    }
                    catch (ApiException apiException)
                    {
                        M3Log.Error(@"Error authenticating to NexusMods: " + apiException.ToString());
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_nexusModsReturnedAnErrorX, apiException.ToString()), M3L.GetString(M3L.string_errorAuthenticatingToNexusMods), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    catch (Exception e)
                    {
                        M3Log.Error(@"Other error authenticating to NexusMods: " + e.Message);
                    }
                }
                else
                {
                    M3Log.Error(@"No API key - setting authorized to false for NM");
                    SetAuthorized(false);
                }

                IsAuthorizing = false;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                VisibleIcon = IsAuthorized;
                if (IsAuthorized)
                {
                    ActiveIcon = EFontAwesomeIcon.Solid_CheckCircle;
                }
                SpinIcon = false;
                AuthorizeCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
            };
            nbw.RunWorkerAsync();
        }
        private void SetAuthorized(bool authorized)
        {
            IsAuthorized = authorized;
            string authenticatedString = M3L.GetString(M3L.string_authenticateToNexusMods);
            if (authorized && NexusModsUtilities.UserInfo != null)
            {
                authenticatedString = M3L.GetString(M3L.string_interp_authenticatedAsX, NexusModsUtilities.UserInfo.Name);
            }
            VisibleIcon = authorized;
            if (authorized)
            {
                ActiveIcon = EFontAwesomeIcon.Solid_CheckCircle;
            }
            AuthorizeToNexusText = authenticatedString;
        }

        private void ChangeLang_BRA_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"bra");
        }
    }
}
