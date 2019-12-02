using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    public partial class ASIManagerPanel : MMBusyPanelBase
    {

        public static readonly string CachedASIsFolder = Path.Combine(Utilities.GetAppDataFolder(), "CachedASIs");

        public static readonly string ManifestLocation = Path.Combine(CachedASIsFolder, "manifest.xml");
        public static readonly string StagedManifestLocation = Path.Combine(CachedASIsFolder, "manifest_staged.xml");
        private bool DeselectingDueToOtherList;



        private object SelectedASIObject;
        public string SelectedASIDescription { get; set; }
        public string SelectedASISubtext { get; set; }
        public string SelectedASIName { get; set; }
        public bool InstallInProgress { get; set; }
        public string InstallButtonText { get; set; }

        public string InstallLoaderText { get; set; }


        public ObservableCollectionExtended<ASIGame> Games { get; } = new ObservableCollectionExtended<ASIGame>();



        /// <summary>
        /// This ASI Manager is a feature ported from ME3CMM and maintains synchronization with Mass Effect 3 Mod Manager's code for 
        /// managing and installing ASIs. ASIs are useful for debugging purposes, which is why this feature is now 
        /// part of ME3Explorer.
        /// 
        /// Please do not change the logic for this code (at least, for Mass Effect 3) as it may break compatibility with Mass
        /// Effect 3 Mod Manager (e.g. dual same ASIs are installed) and the ME3Tweaks serverside components.
        /// </summary>
        public ASIManagerPanel()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"ASI Manager", new WeakReference(this));
            DataContext = this;
            Directory.CreateDirectory(CachedASIsFolder);
            LoadCommands();
            InitializeComponent();
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (a, b) =>
            {
                using WebClient wc = new WebClient();
                var onlineManifest = OnlineContent.FetchRemoteString(@"https://me3tweaks.com/mods/asi/getmanifest?AllGames=1");
                if (onlineManifest != null)
                {
                    File.WriteAllText(StagedManifestLocation, onlineManifest);
                    LoadManifest(StagedManifestLocation, true);
                }
                else if (File.Exists(ManifestLocation))
                {
                    LoadManifest(ManifestLocation, false);
                }
                else
                {
                    //can't get manifest or local manifest.
                    //Todo: some sort of handling here as we are running in panel startup
                }
            };
            bw.RunWorkerAsync();
        }

        public ICommand InstallCommand { get; private set; }
        public ICommand SourceCodeCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(InstallUninstallASI, ASIIsSelected);
            SourceCodeCommand = new GenericCommand(ViewSourceCode, ManifestASIIsSelected);
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClosePanel() => !InstallInProgress;

        private void ViewSourceCode()
        {
            if (SelectedASIObject is ASIMod asi)
            {
                Process.Start(asi.SourceCodeLink);
            }
        }

        private void InstallUninstallASI()
        {
            if (SelectedASIObject is InstalledASIMod instASI)
            {
                //Unknown ASI
                File.Delete(instASI.InstalledPath);
                RefreshASIStates();
            }
            else if (SelectedASIObject is ASIMod asi)
            {

                InstallInProgress = true;
                Games.First(x => x.Game == asi.Game).ApplyASI(asi, () => { InstallInProgress = false; });
            }
        }


        private bool ASIIsSelected() => SelectedASIObject != null;

        private bool ManifestASIIsSelected() => SelectedASIObject is ASIMod;


        private void LoadManifest(string manifestToLoad, bool isStaged = false)
        {
            try
            {
                XElement rootElement = XElement.Load(manifestToLoad);

                //I Love Linq
                var ASIModUpdateGroups = (from e in rootElement.Elements(@"updategroup")
                                          select new ASIModUpdateGroup
                                          {
                                              UpdateGroupId = (int)e.Attribute(@"groupid"),
                                              Game = intToGame((int)e.Attribute(@"game")),
                                              IsHidden = e.Attribute(@"hidden") != null && (bool)e.Attribute(@"hidden"),
                                              ASIModVersions = e.Elements(@"asimod").Select(z => new ASIMod
                                              {
                                                  Name = (string)z.Element(@"name"),
                                                  InstalledPrefix = (string)z.Element(@"installedname"),
                                                  Author = (string)z.Element(@"author"),
                                                  Version = (string)z.Element(@"version"),
                                                  Description = (string)z.Element(@"description"),
                                                  Hash = (string)z.Element(@"hash"),
                                                  SourceCodeLink = (string)z.Element(@"sourcecode"),
                                                  DownloadLink = (string)z.Element(@"downloadlink"),
                                                  Game = intToGame((int)e.Attribute(@"game")) // use e element to pull from outer group
                                              }).ToList()
                                          }).ToList();

                //Must run on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var g in Games)
                    {
                        g.SetUpdateGroups(ASIModUpdateGroups);
                    }

                    RefreshASIStates();
                    UpdateSelectionTexts(null);
                });
                if (isStaged)
                {
                    File.Copy(StagedManifestLocation, ManifestLocation, true); //this will make sure cached manifest is parsable.
                }
            }
            catch (Exception e)
            {
                if (isStaged && File.Exists(ManifestLocation))
                {
                    //try cached instead
                    LoadManifest(ManifestLocation, false);
                    return;
                }

                RefreshASIStates();
                throw new Exception(@"Error parsing the ASI Manifest: " + e.Message);
            }

        }

        private void RefreshASIStates()
        {
            foreach (var game in Games)
            {
                game.RefreshASIStates();
            }
        }

        /// <summary>
        /// Object containing information about an ASI mod in the ASI mod manifest
        /// </summary>
        public class ASIMod : INotifyPropertyChanged
        {
            private static Brush installedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0xFF, 0));
            private static Brush outdatedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0));
            public string DownloadLink { get; internal set; }
            public string SourceCodeLink { get; internal set; }
            public string Hash { get; internal set; }
            public string Version { get; internal set; }
            public string Author { get; internal set; }
            public string InstalledPrefix { get; internal set; }
            public string Name { get; internal set; }
            public Mod.MEGame Game { get; set; }
            public string Description { get; internal set; }

            public event PropertyChangedEventHandler PropertyChanged;

            public bool UIOnly_Installed { get; set; }
            public bool UIOnly_Outdated { get; set; }
            public string InstallStatus => UIOnly_Outdated ? "Outdated version installed" : (UIOnly_Installed ? "Installed" : "");
            public InstalledASIMod InstalledInfo { get; set; }

            public Brush BackgroundColor
            {
                get
                {
                    if (UIOnly_Outdated)
                    {
                        return outdatedBrush;
                    }
                    else if (UIOnly_Installed)
                    {
                        return installedBrush;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }


        /// <summary>
        /// Object describing an installed ASI file. It is not a general ASI mod object but it can be mapped to one
        /// </summary>
        public class InstalledASIMod
        {
            public InstalledASIMod(string asiFile, Mod.MEGame game)
            {
                Game = game;
                InstalledPath = asiFile;
                Filename = Path.GetFileNameWithoutExtension(asiFile);
                Hash = BitConverter.ToString(System.Security.Cryptography.MD5.Create()
                    .ComputeHash(File.ReadAllBytes(asiFile))).Replace(@"-", "").ToLower();
            }

            public Mod.MEGame Game { get; }
            public string InstalledPath { get; set; }
            public string Hash { get; set; }
            public string Filename { get; set; }
        }

        public class ASIModUpdateGroup
        {
            public List<ASIMod> ASIModVersions { get; internal set; }
            public int UpdateGroupId { get; internal set; }
            public Mod.MEGame Game { get; internal set; }
            public bool IsHidden { get; set; }
        }

        private Mod.MEGame intToGame(int i)
        {
            switch (i)
            {
                case 1:
                    return Mod.MEGame.ME1;
                case 2:
                    return Mod.MEGame.ME2;
                case 3:
                    return Mod.MEGame.ME3;
                default:
                    return Mod.MEGame.Unknown;
            }
        }

        private void ASIManagerLists_SelectedChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                UpdateSelectionTexts(e.AddedItems[0]);
                SelectedASIObject = e.AddedItems[0];
            }
            else
            {
                UpdateSelectionTexts(null);
                SelectedASIObject = null;
            }
        }

        private void UpdateSelectionTexts(object v)
        {
            if (v is ASIMod asiMod)
            {
                SelectedASIDescription = asiMod.Description;
                SelectedASIName = asiMod.Name;
                string subtext = $"By {asiMod.Author} | Version {asiMod.Version}\n";
                if (asiMod.UIOnly_Outdated)
                {
                    subtext += "Installed, outdated";
                    InstallButtonText = "Update ASI";
                }
                else if (asiMod.UIOnly_Installed)
                {
                    subtext += "Installed, up to date";
                    InstallButtonText = "Uninstall ASI";

                }
                else
                {
                    subtext += "Not installed";
                    InstallButtonText = "Install ASI";

                }
                SelectedASISubtext = subtext;
            }
            else if (v is InstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = "Unknown ASI mod. You should be careful with this ASI as it may contain malicious code.";
                SelectedASIName = nonManifestAsiMod.Filename;
                SelectedASISubtext = "ASI not present in manifest";
                InstallButtonText = "Uninstall ASI";
            }
            else
            {
                SelectedASIDescription = "";
                SelectedASIName = "Select an ASI to view options";
                SelectedASISubtext = "";
                SelectedASIObject = null;
                InstallButtonText = "No ASI selected";
            }
        }





        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            Games.Add(new ASIGame(Mod.MEGame.ME1, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME1).ToList()));
            Games.Add(new ASIGame(Mod.MEGame.ME2, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2).ToList()));
            Games.Add(new ASIGame(Mod.MEGame.ME3, mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3).ToList()));
            UpdateSelectionTexts(null);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var newtab = e.AddedItems[0];

                //    var selectedItem = lb.SelectedItem;
                //    UpdateSelectionTexts(selectedItem);
            }
        }

        public class ASIGame : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public Mod.MEGame Game { get; }
            public ObservableCollectionExtended<GameTarget> GameTargets { get; } = new ObservableCollectionExtended<GameTarget>();
            public ObservableCollectionExtended<object> DisplayedASIMods { get; } = new ObservableCollectionExtended<object>();
            public GameTarget SelectedTarget { get; set; }
            public object SelectedASI { get; set; }
            public string InstallLoaderText { get; set; }

            public string ASILoaderText
            {
                get
                {
                    if (LoaderInstalled) return "ASI loader installed. ASI mods will load";
                    return "ASI loader not installed. ASI mods will not load";
                }
            }

            public bool LoaderInstalled { get; set; }
            public string GameName => Utilities.GetGameName(Game);
            public List<ASIModUpdateGroup> ASIModUpdateGroups { get; internal set; }

            private List<InstalledASIMod> InstalledASIs;

            public ICommand InstallLoaderCommand { get; }
            public ASIGame(Mod.MEGame game, List<GameTarget> targets)
            {
                Game = game;
                GameTargets.ReplaceAll(targets);
                SelectedTarget = targets.FirstOrDefault(x => x.RegistryActive);
                InstallLoaderCommand = new GenericCommand(InstallLoader, CanInstallLoader);
            }

            private bool CanInstallLoader() => SelectedTarget != null && !LoaderInstalled;


            private void InstallLoader()
            {
                Utilities.InstallBinkBypass(SelectedTarget);
                RefreshBinkStatus();
            }

            private void RefreshBinkStatus()
            {
                LoaderInstalled = SelectedTarget != null && Utilities.CheckIfBinkw32ASIIsInstalled(SelectedTarget);
                InstallLoaderText = LoaderInstalled ? "Loader installed" : "Install loader";
            }

            private void MapInstalledASIs()
            {
                //Find what group contains our installed ASI.
                foreach (InstalledASIMod asi in InstalledASIs)
                {
                    bool mapped = false;
                    foreach (ASIModUpdateGroup amug in ASIModUpdateGroups)
                    {
                        var matchingAsi = amug.ASIModVersions.FirstOrDefault(x => x.Hash == asi.Hash);
                        if (matchingAsi != null)
                        {
                            //We have an installed ASI in the manifest
                            var displayedItem = amug.ASIModVersions.MaxBy(y => y.Version);
                            displayedItem.UIOnly_Installed = true;
                            displayedItem.UIOnly_Outdated = displayedItem != matchingAsi; //is the displayed item (the latest) the same as the item we found?
                            displayedItem.InstalledInfo = asi;
                            mapped = true;
                            break;
                        }

                    }
                    if (!mapped)
                    {
                        DisplayedASIMods.Add(asi);
                    }
                }
            }

            public void RefreshASIStates()
            {
                //Remove installed ASIs
                //Must run on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DisplayedASIMods.ReplaceAll(DisplayedASIMods.Where(x => x is ASIMod).ToList());
                });
                //Clear installation states
                foreach (var asi in DisplayedASIMods)
                {
                    if (asi is ASIMod a)
                    {
                        a.UIOnly_Installed = false;
                        a.UIOnly_Outdated = false;
                        a.InstalledInfo = null;
                    }
                }
                InstalledASIs = getInstalledASIMods();
                MapInstalledASIs();
                //UpdateSelectionTexts(SelectedASI);
            }

            /// <summary>
            /// Gets a list of installed ASI mods.
            /// </summary>
            /// <param name="game">Game to filter results by. Enter 1 2 or 3 for that game only, or anything else to get everything.</param>
            /// <returns></returns>
            private List<InstalledASIMod> getInstalledASIMods(Mod.MEGame game = Mod.MEGame.Unknown)
            {
                List<InstalledASIMod> results = new List<InstalledASIMod>();
                if (SelectedTarget != null)
                {
                    string asiDirectory = MEDirectories.ASIPath(SelectedTarget);
                    string gameDirectory = ME1Directory.gamePath;
                    if (asiDirectory != null && Directory.Exists(gameDirectory))
                    {
                        if (!Directory.Exists(asiDirectory))
                        {
                            Directory.CreateDirectory(asiDirectory);
                            return results; //It won't have anything in it if we are creating it
                        }
                        var asiFiles = Directory.GetFiles(asiDirectory, @"*.asi");
                        foreach (var asiFile in asiFiles)
                        {
                            results.Add(new InstalledASIMod(asiFile, game));
                        }
                    }
                }
                return results;
            }

            private ASIMod getManifestModByHash(string hash)
            {
                foreach (var updateGroup in ASIModUpdateGroups)
                {
                    var asi = updateGroup.ASIModVersions.FirstOrDefault(x => x.Hash == hash);
                    if (asi != null) return asi;
                }
                return null;
            }

            private ASIModUpdateGroup getUpdateGroupByMod(ASIMod mod)
            {
                foreach (var updateGroup in ASIModUpdateGroups)
                {
                    var asi = updateGroup.ASIModVersions.FirstOrDefault(x => x == mod);
                    if (asi != null) return updateGroup;
                }
                return null;
            }


            //Do not delete - fody will link this
            public void OnSelectedTargetChanged()
            {
                if (SelectedTarget != null)
                {
                    RefreshBinkStatus();
                }
            }

            private void InstallASI(ASIMod asiToInstall, InstalledASIMod oldASIToRemoveOnSuccess = null, Action operationCompletedCallback = null)
            {
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += (a, b) =>
                {
                    ASIModUpdateGroup g = getUpdateGroupByMod(asiToInstall);
                    string destinationFilename = $@"{asiToInstall.InstalledPrefix}-v{asiToInstall.Version}.asi";
                    string cachedPath = Path.Combine(CachedASIsFolder, destinationFilename);
                    string destinationDirectory = MEDirectories.ASIPath(SelectedTarget);
                    string finalPath = Path.Combine(destinationDirectory, destinationFilename);
                    string md5;
                    if (File.Exists(cachedPath))
                    {
                        //Check hash first
                        md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(File.ReadAllBytes(cachedPath))).Replace("-", "").ToLower();
                        if (md5 == asiToInstall.Hash)
                        {
                            File.Copy(cachedPath, finalPath);
                            operationCompletedCallback?.Invoke();
                            return;
                        }
                    }
                    WebRequest request = WebRequest.Create(asiToInstall.DownloadLink);

                    using WebResponse response = request.GetResponse();
                    MemoryStream memoryStream = new MemoryStream();
                    response.GetResponseStream().CopyTo(memoryStream);
                    //MD5 check on file for security
                    md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(memoryStream.ToArray())).Replace(@"-", "").ToLower();
                    if (md5 != asiToInstall.Hash)
                    {
                        //ERROR!
                    }
                    else
                    {

                        File.WriteAllBytes(finalPath, memoryStream.ToArray());
                        if (!Directory.Exists(CachedASIsFolder))
                        {
                            Directory.CreateDirectory(CachedASIsFolder);
                        }
                        File.WriteAllBytes(cachedPath, memoryStream.ToArray()); //cache it
                        if (oldASIToRemoveOnSuccess != null)
                        {
                            File.Delete(oldASIToRemoveOnSuccess.InstalledPath);
                        }
                    };
                };
                worker.RunWorkerCompleted += (a, b) =>
                {
                    RefreshASIStates();
                    operationCompletedCallback?.Invoke();
                };

                worker.RunWorkerAsync();
            }

            internal void ApplyASI(ASIMod asi, Action operationCompletedCallback)
            {
                //Check if this is actually installed or not (or outdated)
                var installedInfo = asi.InstalledInfo;
                if (installedInfo != null)
                {
                    var correspondingAsi = getManifestModByHash(installedInfo.Hash);
                    if (correspondingAsi != asi)
                    {
                        //Outdated - update mod
                        InstallASI(asi, installedInfo, operationCompletedCallback);
                    }
                    else
                    {
                        //Up to date - delete mod
                        File.Delete(installedInfo.InstalledPath);
                        RefreshASIStates();
                        operationCompletedCallback?.Invoke();
                    }
                }
                else
                {
                    InstallASI(asi, operationCompletedCallback: operationCompletedCallback);
                }
            }

            internal void SetUpdateGroups(List<ASIModUpdateGroup> asiUpdateGroups)
            {
                ASIModUpdateGroups = asiUpdateGroups;
                DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == Game && !x.IsHidden).Select(x => x.ASIModVersions.MaxBy(y => y.Version)).OrderBy(x => x.Name)); //latest
            }
        }
    }
}
