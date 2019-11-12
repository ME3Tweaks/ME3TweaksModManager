using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using Flurl;
using Flurl.Http;
using IniParser;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for TPMITelemetrySubmissionForm.xaml
    /// </summary>
    public partial class TPMITelemetrySubmissionForm : MMBusyPanelBase
    {
        public ObservableCollectionExtended<TelemetryPackage> TelemetryPackages { get; } = new ObservableCollectionExtended<TelemetryPackage>();

        public TPMITelemetrySubmissionForm(Mod telemetryMod)
        {
            DataContext = this;
            this.TelemetryMod = telemetryMod;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClosePanel() => !TelemetryPackages.Any(x => TelemetrySubmissionInProgress);

        private bool CanSubmitTelemetry() => !TelemetrySubmissionInProgress;

        public bool TelemetrySubmissionInProgress { get; }
        public Mod TelemetryMod { get; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !TelemetrySubmissionInProgress)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("telemetrydatagathering");
            bw.DoWork += GatherTelemetryDataBGThread;
            bw.RunWorkerCompleted += (a, b) =>
            {
                List<TelemetryPackage> list = (List<TelemetryPackage>)b.Result;
                TelemetryPackages.ReplaceAll(list);
            };
            bw.RunWorkerAsync();
        }

        public class TelemetryPackage : INotifyPropertyChanged
        {
            public string DLCFolderName { get; set; }
            public string ModName { get; set; }
            public string ModAuthor { get; set; }
            public string ModSite { get; set; }
            public Mod.MEGame Game { get; set; }

            public int MountPriority { get; set; }
            public int ModMountTLK1 { get; set; }
            public int MountFlag { get; set; }
            public string ModuleNumber { get; set; } = "N/A";
            public string MountFlagHR { get; set; }


            public ICommand SubmitCommand { get; set; }

            public TelemetryPackage()
            {
                SubmitCommand = new GenericCommand(SubmitPackage, CanSubmitPackage);
            }

            public string SubmitText { get; set; } = "Submit to ME3Tweaks";

            private bool TelemetrySubmissionInProgress { get; set; }
            private bool TelemetrySubmitted { get; set; }

            private bool CanSubmitPackage() => !TelemetrySubmitted && !TelemetrySubmissionInProgress;

            private static readonly string TELEMETRY_ENDPOINT = "https://me3tweaks.com/mods/dlc_mods/telemetry";

            public event PropertyChangedEventHandler PropertyChanged;

            private void SubmitPackage()
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += async (a, b) =>
                {
                    TelemetrySubmissionInProgress = true;
                    string endpoint = TELEMETRY_ENDPOINT;
                    var url = endpoint.SetQueryParam("dlc_folder_name", DLCFolderName);
                    url = url.SetQueryParam("mod_name", ModName);
                    url = url.SetQueryParam("mod_game", Game.ToString().Substring(2));
                    url = url.SetQueryParam("mod_author", ModAuthor);
                    url = url.SetQueryParam("mod_site", ModSite);
                    url = url.SetQueryParam("mod_mount_priority", MountPriority);
                    url = url.SetQueryParam("mod_mount_tlk1", ModMountTLK1);
                    url = url.SetQueryParam("mod_mount_flag", MountFlag);
                    if (Game == Mod.MEGame.ME2)
                    {
                        url = url.SetQueryParam("mod_modulenumber", ModuleNumber);
                    }
                    else
                    {
                        url = url.SetQueryParam("mod_modulenumber", 0);
                    }

                    SubmitText = "Submitting...";
                    var result = await url.GetAsync().ReceiveString();
                    SubmitText = "Submitted";
                    TelemetrySubmitted = true;
                    TelemetrySubmissionInProgress = false;
                };
                bw.RunWorkerAsync();
            }
        }

        private void GatherTelemetryDataBGThread(object sender, DoWorkEventArgs e)
        {

            List<TelemetryPackage> telemetryPackages = new List<TelemetryPackage>();
            foreach (var mapping in TelemetryMod.GetJob(ModJob.JobHeader.CUSTOMDLC).CustomDLCFolderMapping)
            {
                TelemetryPackage tp = new TelemetryPackage();
                var sourceDir = mapping.Key;
                tp.DLCFolderName = mapping.Value;
                if (TelemetryMod.HumanReadableCustomDLCNames.TryGetValue(mapping.Value, out var modName))
                {
                    tp.ModName = modName;
                }
                else
                {
                    tp.ModName = TelemetryMod.ModName;
                }

                tp.ModAuthor = TelemetryMod.ModDeveloper;
                tp.ModSite = TelemetryMod.ModWebsite;
                tp.Game = TelemetryMod.Game;
                switch (TelemetryMod.Game)
                {
                    case Mod.MEGame.ME1:
                        {
                            var ini = new FileIniDataParser();
                            var parsedIni = ini.Parser.Parse(File.ReadAllText(Path.Combine(TelemetryMod.ModPath, mapping.Key, "AutoLoad.ini")));
                            tp.MountPriority = int.Parse(parsedIni["ME1DLCMOUNT"]["ModMount"]);
                            tp.ModMountTLK1 = int.Parse(parsedIni["GUI"]["NameStrRef"]);
                            tp.MountFlagHR = "ME1 does not support mount flags in M3 currently";
                            //No mount flag right now.
                        }
                        break;
                    case Mod.MEGame.ME2:
                        {
                            var mountFile = Path.Combine(TelemetryMod.ModPath, mapping.Key, "CookedPC", "mount.dlc");
                            MountFile mf = new MountFile(mountFile);
                            tp.ModMountTLK1 = mf.TLKID;
                            tp.MountPriority = mf.MountPriority;
                            tp.MountFlag = (int)mf.MountFlag;
                            tp.MountFlagHR = mf.MountFlag.ToString();
                            var ini = new FileIniDataParser();
                            var parsedIni = ini.Parser.Parse(File.ReadAllText(Path.Combine(TelemetryMod.ModPath, mapping.Key, "CookedPC", "BIOEngine.ini")));
                            tp.ModuleNumber = parsedIni["Engine.DLCModules"][mapping.Key];
                        }
                        break;
                    case Mod.MEGame.ME3:
                        {
                            var mountFile = Path.Combine(TelemetryMod.ModPath, mapping.Key, "CookedPCConsole", "mount.dlc");
                            MountFile mf = new MountFile(mountFile);
                            tp.ModMountTLK1 = mf.TLKID;
                            tp.MountPriority = mf.MountPriority;
                            tp.MountFlag = (int)mf.MountFlag;
                            tp.MountFlagHR = mf.MountFlag.ToString();
                        }
                        break;
                }
                telemetryPackages.Add(tp);
            }

            e.Result = telemetryPackages;
        }
    }
}
