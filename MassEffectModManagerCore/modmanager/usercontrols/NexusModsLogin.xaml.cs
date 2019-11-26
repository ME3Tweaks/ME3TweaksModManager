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
            LoadCommands();
            InitializeComponent();
        }

        private void SetAuthorized(bool v)
        {
            IsAuthorized = v;
            AuthorizeToNexusText = v ? "Authenticated " + (mainwindow.NexusUsername != null ? "as " + mainwindow.NexusUsername : "to NexusMods") : "Authenticate to NexusMods";
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


        private async void AuthorizeWithNexus()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("NexusAPICredentialsCheck");
            bw.DoWork += async (a, b) =>
            {
                //Check api key
                AuthorizeToNexusText = "Checking key...";
                IsAuthorizing = true;
                try
                {
                    var authInfo = await NexusModsUtilities.AuthToNexusMods(APIKeyText);
                    if (authInfo != null)
                    {
                        NexusModsUtilities.EncryptAPIKeyToDisk(APIKeyText);
                        mainwindow.NexusUsername = authInfo.Name;
                        mainwindow.NexusUserID = authInfo.UserID;
                        SetAuthorized(true);
                        Analytics.TrackEvent("Authenticated to NexusMods");

                    }
                }
                catch (ApiException apiException)
                {
                    Log.Error("Error authenticating to NexusMods: " + apiException.ToString());
                    Application.Current.Dispatcher.Invoke(delegate { Xceed.Wpf.Toolkit.MessageBox.Show(window, "NexusMods return an error:\n" + apiException.ToString(), "Error authenticating to NexusMods", MessageBoxButton.OK, MessageBoxImage.Error); });
                }

                IsAuthorizing = false;
            };
            bw.RunWorkerCompleted += (a, b) => { CommandManager.InvalidateRequerySuggested(); };
            bw.RunWorkerAsync();
        }

        private void UnlinkFromNexus()
        {
            APIKeyText = "";
            NexusModsUtilities.WipeKeys();
            mainwindow.NexusUsername = null;
            mainwindow.NexusUserID = 0;
            SetAuthorized(false);
        }

        private bool CanUnlinkWithNexus() => IsAuthorized;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            //throw new NotImplementedException();
        }
    }
}
