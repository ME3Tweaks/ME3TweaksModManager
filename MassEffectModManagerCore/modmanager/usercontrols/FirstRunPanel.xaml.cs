using MassEffectModManagerCore.ui;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FontAwesome.WPF;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Microsoft.AppCenter.Analytics;
using Pathoschild.Http.Client;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for PreviewWelcomePanel.xaml
    /// </summary>
    public partial class FirstRunPanel : MMBusyPanelBase
    {
        public FontAwesomeIcon ActiveIcon { get; set; }
        public bool SpinIcon { get; set; }
        public bool VisibleIcon { get; set; }


        public FirstRunPanel()
        {
            DataContext = this;
            LibraryDir = Utilities.GetModsDirectory();
            LoadCommands();
            InitializeComponent();
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
            if (mainwindow.ChooseModLibraryPath(false))
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
            if (Settings.EnableTelemetry)
            {
                // Start app center
                App.InitAppCenter();
            }
            App.FlushTelemetryItems(); // Push through any pending telemetry items

            OnClosing(new DataEventArgs(true));
            Settings.ShowedPreviewPanel = true;
            //Settings.Save();
        }

        public override void OnPanelVisible()
        {
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
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
            mainwindow.SetApplicationLanguage(lang, false);
            SetAuthorized(IsAuthorized);
            //Settings.Save();
        }

        private void ChangeLang_INT_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"int");
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
                ActiveIcon = FontAwesomeIcon.Spinner;
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
                            using FileStream fs = new FileStream(System.IO.Path.Combine(Utilities.GetNexusModsCache(), @"nexusmodsapikey"), FileMode.Create);
                            File.WriteAllBytes(System.IO.Path.Combine(Utilities.GetNexusModsCache(), @"entropy"), NexusModsUtilities.EncryptStringToStream(apiKeyReceived, fs));
                            fs.Close();
                            mainwindow.NexusUsername = authInfo.Name;
                            mainwindow.NexusUserID = authInfo.UserID;
                            SetAuthorized(true);
                            mainwindow.RefreshNexusStatus();
                            AuthorizedToNexusUsername = authInfo.Name;
                            Analytics.TrackEvent(@"Authenticated to NexusMods");
                        }
                        else
                        {
                            Log.Error(@"Error authenticating to nexusmods, no userinfo was returned, possible network issue");
                            mainwindow.NexusUsername = null;
                            mainwindow.NexusUserID = 0;
                            SetAuthorized(false);
                            mainwindow.RefreshNexusStatus();
                        }
                    }
                    catch (ApiException apiException)
                    {
                        Log.Error(@"Error authenticating to NexusMods: " + apiException.ToString());
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_nexusModsReturnedAnErrorX, apiException.ToString()), M3L.GetString(M3L.string_errorAuthenticatingToNexusMods), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    catch (Exception e)
                    {
                        Log.Error(@"Other error authenticating to NexusMods: " + e.Message);
                    }
                }
                else
                {
                    Log.Error(@"No API key - setting authorized to false for NM");
                    SetAuthorized(false);
                }

                IsAuthorizing = false;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                VisibleIcon = IsAuthorized;
                if (IsAuthorized)
                {
                    ActiveIcon = FontAwesomeIcon.CheckCircle;
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
            if (authorized && mainwindow.NexusUsername != null)
            {
                authenticatedString = M3L.GetString(M3L.string_interp_authenticatedAsX, mainwindow.NexusUsername);
            }
            VisibleIcon = authorized;
            if (authorized)
            {
                ActiveIcon = FontAwesomeIcon.CheckCircle;
            }
            AuthorizeToNexusText = authenticatedString;
        }

        private void ChangeLang_BRA_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage(@"bra");
        }
    }
}
