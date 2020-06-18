using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;
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
using MassEffectModManagerCore.modmanager.localizations;
using Microsoft.AppCenter.Analytics;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    public partial class ASIManagerPanel : MMBusyPanelBase
    {

        public static readonly string CachedASIsFolder = Directory.CreateDirectory(Path.Combine(Utilities.GetAppDataFolder(), @"CachedASIs")).FullName;

        public static readonly string ManifestLocation = Path.Combine(CachedASIsFolder, @"manifest.xml");
        public static readonly string StagedManifestLocation = Path.Combine(CachedASIsFolder, @"manifest_staged.xml");

        private object SelectedASIObject;
        public string SelectedASIDescription { get; set; }
        public string SelectedASISubtext { get; set; }
        public string SelectedASIName { get; set; }
        public bool InstallInProgress { get; set; }
        public string InstallButtonText { get; set; }

        public bool ME1TabEnabled { get; set; }
        public bool ME2TabEnabled { get; set; }
        public bool ME3TabEnabled { get; set; }

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
            Log.Information(@"Opening ASI Manager");

            DataContext = this;
            Directory.CreateDirectory(CachedASIsFolder);
            LoadCommands();
            InitializeComponent();
        }

        public static void LoadManifest(bool async, List<ASIGame> games, Action<object> selectionStateUpdateCallback = null)
        {
            Log.Information(@"Loading ASI manager manifest. Async mode: " + async);

            if (async)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (a, b) =>
                {
                    internalLoadManifest(games, selectionStateUpdateCallback);
                };
                bw.RunWorkerAsync();
            }
            else
            {
                internalLoadManifest(games, selectionStateUpdateCallback);
            }
        }

        private static void internalLoadManifest(List<ASIGame> games, Action<object> selectionStateUpdateCallback = null)
        {
            using WebClient wc = new WebClient();
            var onlineManifest = OnlineContent.FetchRemoteString(@"https://me3tweaks.com/mods/asi/getmanifest?AllGames=1");
            if (onlineManifest != null)
            {
                try
                {
                    File.WriteAllText(StagedManifestLocation, onlineManifest);
                }
                catch (Exception e)
                {
                    Log.Error(@"Error writing cached ASI manifest to disk: " + e.Message);
                }

                ParseManifest(onlineManifest, games, true, selectionStateUpdateCallback);
            }
            else if (File.Exists(ManifestLocation))
            {
                Log.Information(@"Loading ASI local manifest");
                LoadManifestFromDisk(ManifestLocation, games, false, selectionStateUpdateCallback);
            }
            else
            {
                //can't get manifest or local manifest.
                //Todo: some sort of handling here as we are running in panel startup
                Log.Error(@"Cannot load ASI manifest: Could not fetch online manifest and no local manifest exists");
            }
        }

        public ICommand InstallCommand { get; private set; }
        public ICommand SourceCodeCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(InstallUninstallASI, CanInstallASI);
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
                Utilities.OpenWebpage(asi.SourceCodeLink);
            }
        }

        public static void ExtractDefaultASIResources()
        {
            var outpath = CachedASIsFolder;
            string[] defaultResources = { @"BalanceChangesReplacer-v3.0.asi", @"ME1-DLC-ModEnabler-v1.0.asi", @"ME3Logger_truncating-v1.0.asi", @"manifest.xml" };
            foreach (var file in defaultResources)
            {
                var outfile = Path.Combine(CachedASIsFolder, file);
                if (!File.Exists(outfile))
                {
                    Utilities.ExtractInternalFile(@"MassEffectModManagerCore.modmanager.asi." + file, outfile, true);
                }
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
                var alreadyInstalledAndUpToDate = Games.First(x => x.Game == asi.Game).ApplyASI(asi, () =>
                {
                    InstallInProgress = false;
                    RefreshASIStates();
                    UpdateSelectionTexts(SelectedASIObject);
                });
                if (!alreadyInstalledAndUpToDate)
                {
                    Games.First(x => x.Game == asi.Game).DeleteASI(asi); //UI doesn't allow you to install on top of an already installed ASI that is up to date. So we delete ith ere.
                    InstallInProgress = false;
                    RefreshASIStates();
                    UpdateSelectionTexts(SelectedASIObject);
                }
            }
        }

        private bool CanInstallASI()
        {
            if (SelectedASIObject == null) return false;
            if (SelectedASIObject is ASIMod am)
            {
                return Games.FirstOrDefault(x => x.Game == am.Game)?.GameTargets.Any() ?? false;
            }
            if (SelectedASIObject is InstalledASIMod iam)
            {
                return Games.FirstOrDefault(x => x.Game == iam.Game)?.GameTargets.Any() ?? false;
            }
            return false;
        }

        private bool ManifestASIIsSelected() => SelectedASIObject is ASIMod;

        private static void LoadManifestFromDisk(string manifestPath, List<ASIGame> games, bool isStaged = false, Action<object> selectionStateUpdateCallback = null)
        {
            ParseManifest(File.ReadAllText(manifestPath), games, isStaged, selectionStateUpdateCallback);
        }

        /// <summary>
        /// Parses a string (xml) into an ASI manifest.
        /// </summary>
        /// <param name="manifestString"></param>
        /// <param name="games"></param>
        /// <param name="isStaged"></param>
        /// <param name="selectionStateUpdateCallback"></param>
        private static void ParseManifest(string manifestString, List<ASIGame> games, bool isStaged = false, Action<object> selectionStateUpdateCallback = null)
        {
            try
            {
                XElement rootElement = XElement.Parse(manifestString);

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
                    foreach (var g in games)
                    {
                        g.SetUpdateGroups(ASIModUpdateGroups);
                    }

                    foreach (var game in games)
                    {
                        game.RefreshASIStates();
                    }
                    selectionStateUpdateCallback?.Invoke(null);
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
                    LoadManifestFromDisk(ManifestLocation, games, false);
                    return;
                }

                foreach (var game in games)
                {
                    game.RefreshASIStates();
                }
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
            public string InstallStatus => UIOnly_Outdated ? M3L.GetString(M3L.string_outdatedVersionInstalled) : (UIOnly_Installed ? M3L.GetString(M3L.string_installed) : "");
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

            public ASIMod GetLatestVersion()
            {
                return ASIModVersions.MaxBy(x => x.Version);
            }
        }

        private static Mod.MEGame intToGame(int i)
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
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, asiMod.Author, asiMod.Version);
                subtext += Environment.NewLine;
                if (asiMod.UIOnly_Outdated)
                {
                    subtext += M3L.GetString(M3L.string_installedOutdated);
                    InstallButtonText = M3L.GetString(M3L.string_updateASI);
                }
                else if (asiMod.UIOnly_Installed)
                {
                    subtext += M3L.GetString(M3L.string_installedUpToDate);
                    InstallButtonText = M3L.GetString(M3L.string_uninstallASI);

                }
                else
                {
                    subtext += M3L.GetString(M3L.string_notInstalled);
                    InstallButtonText = M3L.GetString(M3L.string_installASI);

                }
                SelectedASISubtext = subtext;
            }
            else if (v is InstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = M3L.GetString(M3L.string_unknownASIDescription);
                SelectedASIName = nonManifestAsiMod.Filename;
                SelectedASISubtext = M3L.GetString(M3L.string_SSINotPresentInManifest);
                InstallButtonText = M3L.GetString(M3L.string_uninstallASI);
            }
            else
            {
                SelectedASIDescription = "";
                SelectedASIName = M3L.GetString(M3L.string_selectAnASIToViewOptions);
                SelectedASISubtext = "";
                SelectedASIObject = null;
                InstallButtonText = M3L.GetString(M3L.string_noASISelected);
            }
        }





        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClosePanel())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            //This has to be done here as mainwindow will not be available until this is called
            Mod.MEGame[] gameEnum = new[] { Mod.MEGame.ME1, Mod.MEGame.ME2, Mod.MEGame.ME3 };
            foreach (var game in gameEnum)
            {
                var targets = mainwindow.InstallationTargets.Where(x => x.Game == game).ToList();
                if (targets.Count > 0)
                {
                    Games.Add(new ASIGame(game, targets));
                }
            }
            //Technically this could load earlier, but it's not really worth the effort for the miniscule time saved
            LoadManifest(true, Games.ToList(), UpdateSelectionTexts);
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
                    if (LoaderInstalled) return M3L.GetString(M3L.string_aSILoaderInstalledASIModsWillLoad);
                    return M3L.GetString(M3L.string_aSILoaderNotInstalledASIModsWillNotLoad);
                }
            }

            public bool LoaderInstalled { get; set; }
            public bool IsEnabled { get; set; }
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
                IsEnabled = GameTargets.Any();
            }

            /// <summary>
            /// Makes an ASI game for the specific target
            /// </summary>
            /// <param name="target"></param>
            public ASIGame(GameTarget target)
            {
                Game = target.Game;
                GameTargets.ReplaceAll(new[] { target });
                SelectedTarget = target;
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
                InstallLoaderText = LoaderInstalled ? M3L.GetString(M3L.string_loaderInstalled) : M3L.GetString(M3L.string_installLoader);
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
                        md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(File.ReadAllBytes(cachedPath))).Replace(@"-", "").ToLower();
                        if (md5 == asiToInstall.Hash)
                        {
                            Log.Information($@"Copying local ASI from library to destination: {cachedPath} -> {finalPath}");

                            File.Copy(cachedPath, finalPath, true);
                            operationCompletedCallback?.Invoke();
                            Analytics.TrackEvent(@"Installed ASI", new Dictionary<string, string>()
                            {
                                { @"Filename", Path.GetFileNameWithoutExtension(finalPath)}
                            });
                            return;
                        }
                    }
                    WebRequest request = WebRequest.Create(asiToInstall.DownloadLink);
                    Log.Information(@"Fetching remote ASI from server");

                    using WebResponse response = request.GetResponse();
                    MemoryStream memoryStream = new MemoryStream();
                    response.GetResponseStream().CopyTo(memoryStream);
                    //MD5 check on file for security
                    md5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(memoryStream.ToArray())).Replace(@"-", "").ToLower();
                    if (md5 != asiToInstall.Hash)
                    {
                        //ERROR!
                        Log.Error(@"Downloaded ASI did not match the manifest! It has the wrong hash.");
                    }
                    else
                    {
                        Log.Information(@"Fetched remote ASI from server. Installing ASI to " + finalPath);
                        memoryStream.WriteToFile(finalPath);
                        Analytics.TrackEvent(@"Installed ASI", new Dictionary<string, string>()
                        {
                            { @"Filename", Path.GetFileNameWithoutExtension(finalPath)}
                        });
                        if (!Directory.Exists(CachedASIsFolder))
                        {
                            Log.Information(@"Creating cached ASIs folder");
                            Directory.CreateDirectory(CachedASIsFolder);
                        }
                        Log.Information(@"Caching ASI to local ASI library: " + cachedPath);
                        File.WriteAllBytes(cachedPath, memoryStream.ToArray()); //cache it
                        if (oldASIToRemoveOnSuccess != null)
                        {
                            File.Delete(oldASIToRemoveOnSuccess.InstalledPath);
                        }
                    };
                };
                worker.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Error != null)
                    {
                        Log.Error(@"Error occured in ASI installer thread: " + b.Error.Message);
                    }
                    RefreshASIStates();
                    operationCompletedCallback?.Invoke();
                };

                worker.RunWorkerAsync();
            }

            internal void DeleteASI(ASIMod asi)
            {
                var installedInfo = asi.InstalledInfo;
                if (installedInfo != null)
                {
                    //Up to date - delete mod
                    Log.Information(@"Deleting installed ASI: " + installedInfo.InstalledPath);
                    File.Delete(installedInfo.InstalledPath);
                    RefreshASIStates();
                }
            }

            /// <summary>
            /// Attempts to apply the ASI. If the ASI is already up to date, false is returned.
            /// </summary>
            /// <param name="asi">ASI to apply or update</param>
            /// <param name="operationCompletedCallback">Callback when operation is done</param>
            /// <returns></returns>
            internal bool ApplyASI(ASIMod asi, Action operationCompletedCallback)
            {
                if (asi == null)
                {
                    //how can this be?
                    Log.Error(@"ASI is null for ApplyASI()!");
                }
                if (SelectedTarget.TargetPath == null)
                {
                    Log.Error(@"Selected Target is null for ApplyASI()!");
                }
                Log.Information($@"Installing {asi.Name} v{asi.Version} to target {SelectedTarget.TargetPath}");
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
                        Log.Information(@"The installed version of this ASI is already up to date.");
                        return false;
                    }
                }
                else
                {
                    InstallASI(asi, operationCompletedCallback: operationCompletedCallback);
                }
                return true;
            }

            internal void SetUpdateGroups(List<ASIModUpdateGroup> asiUpdateGroups)
            {
                ASIModUpdateGroups = asiUpdateGroups;
                DisplayedASIMods.ReplaceAll(ASIModUpdateGroups.Where(x => x.Game == Game && !x.IsHidden).Select(x => x.ASIModVersions.MaxBy(y => y.Version)).OrderBy(x => x.Name)); //latest
            }
        }
    }
}
