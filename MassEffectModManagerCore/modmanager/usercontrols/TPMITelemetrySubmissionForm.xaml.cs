using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Flurl;
using Flurl.Http;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.diagnostics;

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
            TelemetryMod = telemetryMod;
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
            if (e.Key == Key.Escape && !TelemetryPackages.Any(x => x.TelemetrySubmissionInProgress))
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"telemetrydatagathering");
            nbw.DoWork += GatherTelemetryDataBGThread;
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                List<TelemetryPackage> list = (List<TelemetryPackage>)b.Result;
                TelemetryPackages.ReplaceAll(list);
            };
            nbw.RunWorkerAsync();
        }

        public class TelemetryPackage : INotifyPropertyChanged
        {
            public string DLCFolderName { get; set; }
            public string ModName { get; set; }
            public string ModAuthor { get; set; }
            public string ModSite { get; set; }
            public MEGame Game { get; set; }
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

            //Fody uses this property on weaving
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

            public void SubmitPackage()
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += async (a, b) =>
                {
                    TelemetrySubmissionInProgress = true;
                    string endpoint = DLC_INFO_TELEMETRY_ENDPOINT;
                    var url = endpoint.SetQueryParam(@"dlc_folder_name", DLCFolderName);
                    url = url.SetQueryParam(@"mod_name", ModName);
                    url = url.SetQueryParam(@"mod_game", Game.ToGameNum());
                    url = url.SetQueryParam(@"mod_author", ModAuthor);
                    url = url.SetQueryParam(@"mod_site", ModSite);
                    url = url.SetQueryParam(@"mod_mount_priority", MountPriority);
                    url = url.SetQueryParam(@"mod_mount_tlk1", ModMountTLK1);
                    url = url.SetQueryParam(@"mod_mount_flag", MountFlag);
                    if (Game.IsGame2())
                    {
                        url = url.SetQueryParam(@"mod_modulenumber", ModuleNumber);
                    }
                    else
                    {
                        url = url.SetQueryParam(@"mod_modulenumber", 0);
                    }

                    SubmitText = M3L.GetString(M3L.string_submitting);
                    M3Log.Information($@"Submitting telemetry to ME3Tweaks for {ModName} TelemetryPackage");
                    try
                    {
                        var result = await url.GetAsync().ReceiveString();
                    }
                    catch (Exception e)
                    {
                        // DO ON UI THREAD

                        // Message needs URL stripped.
                        M3L.ShowDialog(null, $"Error submitting mod information: {e.Message}", "Error submitting",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }

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
                if (tp != null)
                {
                    telemetryPackages.Add(tp);
                }
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
                TelemetryMod.ModWebsite == Mod.DefaultWebsite ? "" : TelemetryMod.ModWebsite,
                TelemetryMod);
        }

        public static TelemetryPackage GetTelemetryPackageForDLC(MEGame game, string dlcDirectory, string dlcFoldername, string destinationDLCName, string modName, string modAuthor, string modSite, Mod telemetryMod)
        {
            try
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
                    case MEGame.LE1:
                    case MEGame.ME1:
                        {
                            var parsedIni = DuplicatingIni.LoadIni(Path.Combine(sourceDir, @"AutoLoad.ini"));
                            tp.MountPriority = int.Parse(parsedIni[@"ME1DLCMOUNT"][@"ModMount"]?.Value);
                            tp.ModMountTLK1 = int.Parse(parsedIni[@"GUI"][@"NameStrRef"]?.Value);
                            tp.MountFlagHR = M3L.GetString(M3L.string_me1MountFlagsNotSupportedInM3);
                            //No mount flag right now.
                        }
                        break;
                    case MEGame.ME2:
                    case MEGame.LE2:
                        {
                            var mountFile = Path.Combine(sourceDir, game.CookedDirName(), @"mount.dlc");
                            MountFile mf = new MountFile(mountFile);
                            tp.ModMountTLK1 = mf.TLKID;
                            tp.MountPriority = mf.MountPriority;
                            tp.MountFlag = (int)mf.MountFlags.FlagValue;
                            tp.MountFlagHR = mf.MountFlags.ToHumanReadableString();
                            var ini = DuplicatingIni.LoadIni(Path.Combine(sourceDir, game.CookedDirName(), @"BIOEngine.ini"));
                            tp.ModuleNumber = ini[@"Engine.DLCModules"][dlcFoldername]?.Value;
                        }
                        break;
                    case MEGame.ME3:
                    case MEGame.LE3:
                        {
                            var mountFile = Path.Combine(sourceDir, @"CookedPCConsole", @"mount.dlc");
                            MountFile mf = new MountFile(mountFile);
                            tp.ModMountTLK1 = mf.TLKID;
                            tp.MountPriority = mf.MountPriority;
                            tp.MountFlag = mf.MountFlags.FlagValue;
                            tp.MountFlagHR = mf.MountFlags.ToHumanReadableString();
                        }
                        break;
                }

                return tp;
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error building telemetry package for {dlcFoldername}: {e.Message}.");
                return null;
            }
        }
    }
}
