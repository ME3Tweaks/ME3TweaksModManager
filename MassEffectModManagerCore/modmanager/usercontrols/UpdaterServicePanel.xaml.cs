using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using System.Xml.Linq;
using static MassEffectModManagerCore.modmanager.me3tweaks.OnlineContent;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for UpdaterServicePanel.xaml
    /// </summary>
    public partial class UpdaterServicePanel : MMBusyPanelBase
    {
        public Mod mod { get; }
        public string CurrentActionText { get; private set; }
        public UpdaterServicePanel(Mod mod)
        {
            DataContext = this;
            this.mod = mod;
            LoadCommands();
            InitializeComponent();
        }

        public bool OperationInProgress { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
        }

        private bool CanClosePanel() => !OperationInProgress;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"UpdaterService-SyncFetch");
            nbw.DoWork += (a, b) =>
            {
                StartPreparingMod();
            };
            nbw.RunWorkerAsync();
        }

        private void StartPreparingMod()
        {
            #region online fetch
            //Fetch current production manifest for mod (it may not exist)
            using var wc = new System.Net.WebClient();
            try
            {
                CurrentActionText = "Fetching current production manifest";
                string updateUrl = UpdaterServiceManifestEndpoint + @"?classicupdatecode[]=" + mod.ModClassicUpdateCode;
                string updatexml = wc.DownloadStringAwareOfEncoding(updateUrl);

                XElement rootElement = XElement.Parse(updatexml);
                var modUpdateInfos = (from e in rootElement.Elements(@"mod")
                                      select new ModUpdateInfo
                                      {
                                          changelog = (string)e.Attribute(@"changelog"),
                                          versionstr = (string)e.Attribute(@"version"),
                                          updatecode = (int)e.Attribute(@"updatecode"),
                                          serverfolder = (string)e.Attribute(@"folder"),
                                          sourceFiles = (from f in e.Elements(@"sourcefile")
                                                         select new SourceFile
                                                         {
                                                             lzmahash = (string)f.Attribute(@"lzmahash"),
                                                             hash = (string)f.Attribute(@"hash"),
                                                             size = (int)f.Attribute(@"size"),
                                                             lzmasize = (int)f.Attribute(@"lzmasize"),
                                                             relativefilepath = f.Value,
                                                             timestamp = (long?)f.Attribute(@"timestamp") ?? (long)0
                                                         }).ToList(),
                                      }).ToList();
                Debug.WriteLine(@"Found info: " + modUpdateInfos.Count);

                CurrentActionText = "Fetching current server file listing";

            }
            catch (Exception ex)
            {
                Log.Error(@"Error fetching server manifest: " + ex.Message);
            }

            #endregion

            #region hashing mod
            void updateCurrentTextCallback(string newText)
            {
                CurrentActionText = newText;
            }

            OnlineContent.StageModForUploadToUpdaterService(mod, updateCurrentTextCallback);
            #endregion
        }
    }
}
