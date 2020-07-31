using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;

namespace MassEffectModManagerCore.modmanager.asi
{
    /// <summary>
    /// ASI Manager for single game
    /// </summary>
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

        public List<InstalledASIMod> InstalledASIs;

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
                if (ASIModUpdateGroups != null)
                {
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
            InstalledASIs = GetInstalledASIMods(Game);
            MapInstalledASIs();
            //UpdateSelectionTexts(SelectedASI);
        }

        /// <summary>
        /// Gets a list of installed ASI mods.
        /// </summary>
        /// <param name="game">Game to filter results by. Enter 1 2 or 3 for that game only, or anything else to get everything.</param>
        /// <returns></returns>
        public List<InstalledASIMod> GetInstalledASIMods(Mod.MEGame game = Mod.MEGame.Unknown)
        {
            List<InstalledASIMod> results = new List<InstalledASIMod>();
            try
            {
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
            }
            catch (Exception e)
            {
                Log.Error(@"Error fetching list of installed ASIs: " + e.Message);
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
                if (ASIModUpdateGroups != null)
                {
                    RefreshASIStates();
                }
            }
        }

        private void InstallASI(ASIMod asiToInstall, InstalledASIMod oldASIToRemoveOnSuccess = null, Action operationCompletedCallback = null)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (a, b) =>
            {
                ASIModUpdateGroup g = getUpdateGroupByMod(asiToInstall);
                string destinationFilename = $@"{asiToInstall.InstalledPrefix}-v{asiToInstall.Version}.asi";
                string cachedPath = Path.Combine(ASIManager.CachedASIsFolder, destinationFilename);
                string destinationDirectory = MEDirectories.ASIPath(SelectedTarget);
                if (!Directory.Exists(destinationDirectory))
                {
                    Log.Information(@"Creating ASI directory: " + destinationDirectory);
                    Directory.CreateDirectory(destinationDirectory);
                }
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
                        Log.Information($@"Installed ASI to {finalPath}");
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
                    Log.Information(@"ASI successfully installed.");
                    Analytics.TrackEvent(@"Installed ASI", new Dictionary<string, string>()
                        {
                            { @"Filename", Path.GetFileNameWithoutExtension(finalPath)}
                        });
                    if (!Directory.Exists(ASIManager.CachedASIsFolder))
                    {
                        Log.Information(@"Creating cached ASIs folder");
                        Directory.CreateDirectory(ASIManager.CachedASIsFolder);
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

        internal void DeleteASI(ASIMod asi, Action<Exception> exceptionCallback = null)
        {
            var installedInfo = asi.InstalledInfo;
            if (installedInfo != null)
            {
                //Up to date - delete mod
                Log.Information(@"Deleting installed ASI: " + installedInfo.InstalledPath);
                try
                {
                    File.Delete(installedInfo.InstalledPath);
                    RefreshASIStates();
                }
                catch (Exception e)
                {
                    Log.Error($@"Error deleting asi: {e.Message}");
                    exceptionCallback?.Invoke(e);
                }
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
                return false;
            }
            if (SelectedTarget == null)
            {
                Log.Error(@"SelectedTarget is null for ApplyASI()!");
                return false; //prevent crash
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