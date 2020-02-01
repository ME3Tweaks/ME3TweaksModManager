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
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;

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

        private bool CanClosePanel() => !TelemetryPackages.Any(x => x.TelemetrySubmissionInProgress);

        public Mod TelemetryMod { get; }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !TelemetryPackages.Any(x=>x.TelemetrySubmissionInProgress))
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"telemetrydatagathering");
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
            public string ModuleNumber { get; set; } = @"N/A";
            public string MountFlagHR { get; set; }


            public ICommand SubmitCommand { get; set; }

            public TelemetryPackage()
            {
                SubmitCommand = new GenericCommand(SubmitPackage, CanSubmitPackage);
            }

            public string SubmitText { get; set; } = M3L.GetString(M3L.string_submitToME3Tweaks);

            internal bool TelemetrySubmissionInProgress { get; set; }
            private bool TelemetrySubmitted { get; set; }

            private bool CanSubmitPackage() => !TelemetrySubmitted && !TelemetrySubmissionInProgress;

            public static readonly string DLC_INFO_TELEMETRY_ENDPOINT = @"https://me3tweaks.com/mods/dlc_mods/telemetry";

            public event PropertyChangedEventHandler PropertyChanged;

            public void SubmitPackage()
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += async (a, b) =>
                {
                    TelemetrySubmissionInProgress = true;
                    string endpoint = DLC_INFO_TELEMETRY_ENDPOINT;
                    var url = endpoint.SetQueryParam(@"dlc_folder_name", DLCFolderName);
                    url = url.SetQueryParam(@"mod_name", ModName);
                    url = url.SetQueryParam(@"mod_game", Game.ToString().Substring(2));
                    url = url.SetQueryParam(@"mod_author", ModAuthor);
                    url = url.SetQueryParam(@"mod_site", ModSite);
                    url = url.SetQueryParam(@"mod_mount_priority", MountPriority);
                    url = url.SetQueryParam(@"mod_mount_tlk1", ModMountTLK1);
                    url = url.SetQueryParam(@"mod_mount_flag", MountFlag);
                    if (Game == Mod.MEGame.ME2)
                    {
                        url = url.SetQueryParam(@"mod_modulenumber", ModuleNumber);
                    }
                    else
                    {
                        url = url.SetQueryParam(@"mod_modulenumber", 0);
                    }

                    SubmitText = M3L.GetString(M3L.string_submitting);
                    Log.Information($@"Submitting telemetry to ME3Tweaks for {ModName} TelemetryPackage");
                    var result = await url.GetAsync().ReceiveString();
                    SubmitText = M3L.GetString(M3L.string_submitted);
                    TelemetrySubmitted = true;
                    TelemetrySubmissionInProgress = false;
                };
                bw.RunWorkerAsync();
            }
        }

        private void GatherTelemetryDataBGThread(object sender, DoWorkEventArgs e)
        {

            List<TelemetryPackage> telemetryPackages = new List<TelemetryPackage>();
            var foldersToPrepare = TelemetryMod.GetJob(ModJob.JobHeader.CUSTOMDLC).CustomDLCFolderMapping;
            var alternates = TelemetryMod.GetJob(ModJob.JobHeader.CUSTOMDLC).AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC);
            foldersToPrepare.AddRange(alternates.ToDictionary(x => Path.GetFileName(x.AlternateDLCFolder), x => Path.GetFileName(x.DestinationDLCFolder)));
            foreach (var mapping in foldersToPrepare)
            {
                var tp = GetTelemetryPackageForModDLC(TelemetryMod, mapping.Key, mapping.Value); //this might need to be changed if it's not same source/dest dirs.
                telemetryPackages.Add(tp);
            }

            e.Result = telemetryPackages;
        }

        private TelemetryPackage GetTelemetryPackageForModDLC(Mod telemetryMod, string dlcFoldername, string inGameName)
        {
            return GetTelemetryPackageForDLC(telemetryMod.Game,
                TelemetryMod.ModPath,
                dlcFoldername,
                inGameName,
                TelemetryMod.ModName,
                TelemetryMod.ModDeveloper,
                TelemetryMod.ModWebsite,
                TelemetryMod);

        }

        public static TelemetryPackage GetTelemetryPackageForDLC(Mod.MEGame game, string dlcDirectory, string dlcFoldername, string destinationDLCName, string modName, string modAuthor, string modSite, Mod telemetryMod)
        {
            TelemetryPackage tp = new TelemetryPackage();
            var sourceDir = Path.Combine(dlcDirectory, dlcFoldername);
            tp.DLCFolderName = destinationDLCName; //this most times will be the same as dlcFoldername, but in case of alternates, it might not be
            if (telemetryMod != null && telemetryMod.HumanReadableCustomDLCNames.TryGetValue(dlcFoldername, out var modNameIni))
            {
                tp.ModName = modNameIni;
            }
            else
            {
                tp.ModName = modName;
            }

            tp.ModAuthor = modAuthor;
            tp.ModSite = modSite;
            tp.Game = game;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    {
                        var ini = new FileIniDataParser();
                        var parsedIni = ini.Parser.Parse(File.ReadAllText(Path.Combine(sourceDir, @"AutoLoad.ini")));
                        tp.MountPriority = int.Parse(parsedIni[@"ME1DLCMOUNT"][@"ModMount"]);
                        tp.ModMountTLK1 = int.Parse(parsedIni[@"GUI"][@"NameStrRef"]);
                        tp.MountFlagHR = M3L.GetString(M3L.string_me1MountFlagsNotSupportedInM3);
                        //No mount flag right now.
                    }
                    break;
                case Mod.MEGame.ME2:
                    {
                        var mountFile = Path.Combine(sourceDir, @"CookedPC", @"mount.dlc");
                        MountFile mf = new MountFile(mountFile);
                        tp.ModMountTLK1 = mf.TLKID;
                        tp.MountPriority = mf.MountPriority;
                        tp.MountFlag = (int)mf.MountFlag;
                        tp.MountFlagHR = mf.MountFlag.ToString();
                        var ini = new FileIniDataParser();
                        var parsedIni = ini.Parser.Parse(File.ReadAllText(Path.Combine(sourceDir, @"CookedPC", @"BIOEngine.ini")));
                        tp.ModuleNumber = parsedIni[@"Engine.DLCModules"][dlcFoldername];
                    }
                    break;
                case Mod.MEGame.ME3:
                    {
                        var mountFile = Path.Combine(sourceDir, @"CookedPCConsole", @"mount.dlc");
                        MountFile mf = new MountFile(mountFile);
                        tp.ModMountTLK1 = mf.TLKID;
                        tp.MountPriority = mf.MountPriority;
                        tp.MountFlag = (int)mf.MountFlag;
                        tp.MountFlagHR = mf.MountFlag.ToString();
                    }
                    break;
            }

            return tp;
        }
    }
}
