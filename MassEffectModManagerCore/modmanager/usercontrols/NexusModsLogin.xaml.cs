using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Pathoschild.FluentNexus;
using Pathoschild.FluentNexus.Models;
using Pathoschild.Http.Client;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusModsLogin.xaml
    /// </summary>
    public partial class NexusModsLogin : MMBusyPanelBase
    {
        public string _hack_apikey
        {
            get => null;
            set => value = APIKeyText;
        }
        public string APIKeyText { get; set; }
        public bool IsAuthorized { get; set; }
        public string AuthorizeToNexusText { get; set; }
        public NexusModsLogin()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private void SetAuthorized(bool v)
        {
            IsAuthorized = v;
            string authenticatedString = M3L.GetString(M3L.string_authenticateToNexusMods);
            if (v && mainwindow.NexusUsername != null)
            {
                authenticatedString = M3L.GetString(M3L.string_interp_authenticatedAsX, mainwindow.NexusUsername);
            }

            AuthorizeToNexusText = authenticatedString;
        }

        public ICommand AuthorizeCommand { get; set; }
        public ICommand UnlinkCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        public bool IsAuthorizing { get; private set; }

        private void LoadCommands()
        {
            AuthorizeCommand = new GenericCommand(AuthorizeWithNexus, CanAuthorizeWithNexus);
            UnlinkCommand = new GenericCommand(UnlinkFromNexus, CanUnlinkWithNexus);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private bool CanClose() => !IsAuthorizing;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanAuthorizeWithNexus() => !IsAuthorized && !IsAuthorizing && !string.IsNullOrWhiteSpace(APIKeyText);


        private void AuthorizeWithNexus()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"NexusAPICredentialsCheck");
            bw.DoWork += async (a, b) =>
            {
                //Check api key
                AuthorizeToNexusText = M3L.GetString(M3L.string_checkingKey);
                IsAuthorizing = true;
                try
                {
                    var authInfo = NexusModsUtilities.AuthToNexusMods(APIKeyText).Result;
                    if (authInfo != null)
                    {
                        NexusModsUtilities.EncryptAPIKeyToDisk(APIKeyText);
                        mainwindow.NexusUsername = authInfo.Name;
                        mainwindow.NexusUserID = authInfo.UserID;
                        SetAuthorized(true);
                        mainwindow.RefreshNexusStatus();
                        Analytics.TrackEvent(M3L.GetString(M3L.string_authenticatedToNexusMods));
                    } else
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
                IsAuthorizing = false;
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                CommandManager.InvalidateRequerySuggested();
            };
            bw.RunWorkerAsync();
        }

        private void UnlinkFromNexus()
        {
            APIKeyText = "";
            NexusModsUtilities.WipeKeys();
            mainwindow.NexusUsername = null;
            mainwindow.NexusUserID = 0;
            SetAuthorized(false);
            mainwindow.RefreshNexusStatus();
        }

        private bool CanUnlinkWithNexus() => IsAuthorized;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
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
    }
}
