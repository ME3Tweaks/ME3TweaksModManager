using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FontAwesome5;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Pathoschild.Http.Client;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusModsLogin.xaml
    /// </summary>
    public partial class NexusModsLogin : MMBusyPanelBase, INotifyPropertyChanged
    {
        public string APIKeyText { get; private set; }
        public void OnAPIKeyTextChanged()
        {
            // must be on ui thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                APIBox.Password = APIKeyText;
            });
        }

        public bool IsAuthorized { get; set; }
        public string AuthorizeToNexusText { get; set; }
        public NexusModsLogin()
        {
            DataContext = this;
            LoadCommands();
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

        public GenericCommand AuthorizeCommand { get; set; }
        public GenericCommand UnlinkCommand { get; set; }
        public GenericCommand CloseCommand { get; set; }
        public GenericCommand ConfigureNXMCommand { get; set; }

        public bool IsAuthorizing { get; private set; }

        private void LoadCommands()
        {
            AuthorizeCommand = new GenericCommand(AuthorizeWithNexus, CanAuthorizeWithNexus);
            UnlinkCommand = new GenericCommand(UnlinkFromNexus, CanUnlinkWithNexus);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
            ConfigureNXMCommand = new GenericCommand(ConfigureNXM);
        }

        private void ConfigureNXM()
        {
            Result.PanelToOpen = EPanelID.NXM_CONFIGURATOR;
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !IsAuthorizing;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanAuthorizeWithNexus() => !IsAuthorized && !IsAuthorizing && (!ManualMode || !string.IsNullOrWhiteSpace(APIKeyText));

        public bool ManualMode { get; set; }
        public string WatermarkText { get; set; } = M3L.GetString(M3L.string_yourAPIKeyWillAppearHere);
        public EFontAwesomeIcon ActiveIcon { get; set; }
        public bool SpinIcon { get; set; }
        public bool VisibleIcon { get; set; }

        public void OnIsAuthorizedChanged() => VisibleIcon = IsAuthorized;

        public void OnManualModeChanged()
        {
            WatermarkText = ManualMode ? M3L.GetString(M3L.string_pasteYourAPIKeyHere) : M3L.GetString(M3L.string_yourAPIKeyWillAppearHere);
        }

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
                if (!ManualMode)
                {

                    var apiKeyReceived = await NexusModsUtilities.SetupNexusLogin(x => Debug.WriteLine(x));
                    APIKeyText = apiKeyReceived;
                    Application.Current.Dispatcher.Invoke(delegate { mainwindow.Activate(); });
                }

                if (!string.IsNullOrWhiteSpace(APIKeyText))
                {
                    //Check api key
                    AuthorizeToNexusText = M3L.GetString(M3L.string_checkingKey);
                    try
                    {
                        var authInfo = NexusModsUtilities.AuthToNexusMods(APIKeyText).Result;
                        if (authInfo != null)
                        {
                            using FileStream fs = new FileStream(Path.Combine(M3Utilities.GetNexusModsCache(), @"nexusmodsapikey"), FileMode.Create);
                            File.WriteAllBytes(Path.Combine(M3Utilities.GetNexusModsCache(), @"entropy"), NexusModsUtilities.EncryptStringToStream(APIKeyText, fs));
                            fs.Close();
                            SetAuthorized(true);
                            mainwindow.RefreshNexusStatus();
                            Analytics.TrackEvent(@"Authenticated to NexusMods");
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
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
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

        private void UnlinkFromNexus()
        {
            APIKeyText = "";
            NexusModsUtilities.WipeKeys();
            SetAuthorized(false);
            mainwindow.RefreshNexusStatus();
        }

        private bool CanUnlinkWithNexus() => !IsAuthorizing && !string.IsNullOrWhiteSpace(APIKeyText);

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            try
            {
                string currentKey = NexusModsUtilities.DecryptNexusmodsAPIKeyFromDisk();
                if (currentKey != null)
                {
                    APIKeyText = currentKey;
                    SetAuthorized(true);
                }
                else
                {
                    SetAuthorized(false);
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error getting current API Key: " + e.Message);
                SetAuthorized(false);
            }
        }
    }
}