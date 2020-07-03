using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
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
using AuthenticodeExaminer;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Octokit;
using Pathoschild.FluentNexus.Models;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OIGODisabler.xaml
    /// </summary>
    public partial class OIGODisabler : MMBusyPanelBase
    {

        public OIGODisabler()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Origin in-game overlay disabler panel", new WeakReference(this));
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ObservableCollectionExtended<OIGGame> Games { get; } = new ObservableCollectionExtended<OIGGame>();

        public class OIGGame : INotifyPropertyChanged
        {
            public Mod.MEGame Game { get; private set; }
            public string GameIconSource { get; private set; }
            public string GameTitle { get; private set; }
            public string D3D9Status { get; private set; }
            public string DisablerButtonText { get; private set; }
            public ObservableCollectionExtended<GameTarget> Targets { get; } = new ObservableCollectionExtended<GameTarget>();
            public GameTarget SelectedTarget { get; set; }
            public ICommand ToggleDisablerCommand { get; private set; }
            public OIGGame(Mod.MEGame game, IEnumerable<GameTarget> targets)
            {
                Game = game;
                Targets.ReplaceAll(targets);
                ToggleDisablerCommand = new GenericCommand(ToggleDisabler, CanToggleDisabler);
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        GameTitle = @"Mass Effect";
                        GameIconSource = @"/images/gameicons/ME1_48.ico";
                        break;
                    case Mod.MEGame.ME2:
                        GameTitle = @"Mass Effect 2";
                        GameIconSource = @"/images/gameicons/ME2_48.ico";
                        break;
                    case Mod.MEGame.ME3:
                        GameTitle = @"Mass Effect 3";
                        GameIconSource = @"/images/gameicons/ME3_48.ico";
                        break;
                }
                SelectedTarget = Targets.FirstOrDefault();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void OnSelectedTargetChanged()
            {
                SetupDisablerButtonText();
            }

            private void SetupDisablerButtonText()
            {
                if (SelectedTarget != null)
                {
                    var d3d9Path = Path.Combine(MEDirectories.ExecutableDirectory(SelectedTarget), @"d3d9.dll");
                    if (File.Exists(d3d9Path))
                    {
                        // See if it ME3Tweaks disabler or some other tool
                        var fi = new FileInspector(d3d9Path);
                        foreach (var sig in fi.GetSignatures())
                        {
                            foreach (var signChain in sig.AdditionalCertificates)
                            {
                                try
                                {
                                    var outStr = signChain.Subject.Substring(3); //remove CN=
                                    outStr = outStr.Substring(0, outStr.IndexOf(','));
                                    if (outStr == @"Michael Perez") //My signing cert name
                                    {
                                        D3D9Status = M3L.GetString(M3L.string_overlayDisablerInstalled);
                                        DisablerButtonText = M3L.GetString(M3L.string_uninstallDisabler);
                                        DisablerButtonEnabled = true;
                                        return;
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        D3D9Status = M3L.GetString(M3L.string_otherD3d9dllInstalledOverlayDisabled);
                        DisablerButtonText = M3L.GetString(M3L.string_cannotUninstallOtherD3d9File);
                        DisablerButtonEnabled = false;
                        return;
                    }

                    DisablerButtonEnabled = true;
                    D3D9Status = M3L.GetString(M3L.string_overlayDisablerNotInstalled);
                    DisablerButtonText = M3L.GetString(M3L.string_installDisabler);
                }
                else
                {
                    DisablerButtonEnabled = false;
                    DisablerButtonText = M3L.GetString(M3L.string_installDisabler);

                    if (Targets.Any())
                    {
                        D3D9Status = M3L.GetString(M3L.string_noTargetSelected);
                    }
                    else
                    {
                        D3D9Status = M3L.GetString(M3L.string_noOriginBasedGameTargets);
                    }
                }
            }

            public bool DisablerButtonEnabled { get; set; }

            private bool CanToggleDisabler() => DisablerButtonEnabled;

            private void ToggleDisabler()
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"OIGDisablerThread");
                nbw.DoWork += async (a, b) =>
                {
                    if (!Utilities.IsGameRunning(Game))
                    {
                        var d3d9Path = Path.Combine(MEDirectories.ExecutableDirectory(SelectedTarget), @"d3d9.dll");
                        if (!File.Exists(d3d9Path))
                        {
                            if (File.Exists(Utilities.GetOriginOverlayDisableFile()))
                            {
                                Log.Information(@"Installing origin overlay disabler from cache to " + d3d9Path);
                                try
                                {
                                    File.Copy(Utilities.GetOriginOverlayDisableFile(), d3d9Path);
                                }
                                catch (Exception e)
                                {
                                    Log.Error($@"Error installing d3d9.dll: {e.Message}");
                                }
                            }
                            else
                            {

                                var client = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
                                try
                                {
                                    var releases = await client.Repository.Release.GetAll(@"ME3Tweaks", @"d3d9-blank-proxy");
                                    if (releases.Count > 0)
                                    {
                                        Log.Information(@"Parsing release information from github");

                                        //The release we want to check is always the latest with assets that is not a pre-release
                                        var latestRel = releases.FirstOrDefault(x => !x.Prerelease && x.Assets.Count > 0);
                                        if (latestRel != null)
                                        {
                                            var downloadUrl = latestRel.Assets[0].BrowserDownloadUrl;
                                            var downloadedZipAsset = OnlineContent.DownloadToMemory(downloadUrl);
                                            using var zf = new ZipArchive(downloadedZipAsset.result);
                                            var d3d9 = zf.Entries.First(x => x.FullName == @"d3d9.dll");
                                            if (d3d9 != null)
                                            {
                                                await using var data = d3d9.Open();
                                                var memStream = new MemoryStream();
                                                data.CopyTo(memStream);
                                                try
                                                {
                                                    Log.Information(@"Installing origin overlay disabler from memory to " + d3d9Path);
                                                    memStream.WriteToFile(d3d9Path); //install
                                                    Log.Information(@"Caching d3d9 disabler");
                                                    memStream.WriteToFile(Utilities.GetOriginOverlayDisableFile());
                                                }
                                                catch (Exception e)
                                                {

                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Error(@"Error checking for tool update: " + e);
                                }
                            }
                        }
                        else
                        {
                            Log.Information(@"Deleting " + d3d9Path);
                            try
                            {
                                File.Delete(d3d9Path);
                            }
                            catch (Exception e)
                            {
                                Log.Error($@"Error deleting d3d9.dll: {e.Message}");
                            }
                        }
                    }
                };
                nbw.RunWorkerCompleted += (await, b) =>
                {
                    SetupDisablerButtonText();
                };
                nbw.RunWorkerAsync();
            }
        }

        public ICommand CloseCommand { get; set; }

        public void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        public void ClosePanel() => OnClosing(DataEventArgs.Empty);

        private bool CanClose()
        {
            return true;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            Games.Add(new OIGGame(Mod.MEGame.ME1, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME1 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains(M3L.GetString(M3L.string_origin)))));
            Games.Add(new OIGGame(Mod.MEGame.ME2, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains(M3L.GetString(M3L.string_origin)))));
            Games.Add(new OIGGame(Mod.MEGame.ME3, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3 && !x.IsCustomOption && x.GameSource != null && x.GameSource.Contains(M3L.GetString(M3L.string_origin)))));
        }
    }
}
