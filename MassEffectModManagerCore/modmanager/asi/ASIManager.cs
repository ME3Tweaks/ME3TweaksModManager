using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using Microsoft.AppCenter.Analytics;
using Serilog;

namespace MassEffectModManagerCore.modmanager.asi
{
    /// <summary>
    /// Backend for ASI Management
    /// </summary>
    public class ASIManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static readonly string CachedASIsFolder = Directory.CreateDirectory(Path.Combine(Utilities.GetAppDataFolder(), @"CachedASIs")).FullName;

        public static readonly string ManifestLocation = Path.Combine(CachedASIsFolder, @"manifest.xml");
        public static readonly string StagedManifestLocation = Path.Combine(CachedASIsFolder, @"manifest_staged.xml");

        public static List<ASIMod> MasterME1ASIUpdateGroups = new List<ASIMod>();
        public static List<ASIMod> MasterME2ASIUpdateGroups = new List<ASIMod>();
        public static List<ASIMod> MasterME3ASIUpdateGroups = new List<ASIMod>();

        /// <summary>
        /// Loads the ASI manifest. This should only be done at startup or when the online manifest is refreshed. ForceLocal only works if there is local ASI manifest present
        /// </summary>
        public static void LoadManifest(bool forceLocal = false)
        {
            Log.Information(@"Loading ASI Manager manifest");
            try
            {
                internalLoadManifest(forceLocal);
            }
            catch (Exception e)
            {
                Log.Error($@"Error loading ASI manifest: {e.Message}");
            }
        }

        private static void internalLoadManifest(bool forceLocal = false)
        {
            if (forceLocal && File.Exists(ManifestLocation) || !OnlineContent.CanFetchContentThrottleCheck())
            {
                LoadManifestFromDisk(ManifestLocation);
                return;
            }

            var onlineManifest = forceLocal && File.Exists(ManifestLocation) ? null : OnlineContent.FetchRemoteString(@"https://me3tweaks.com/mods/asi/getmanifest?AllGames=1");
            if (onlineManifest != null) //this cannot be triggered if forceLocal is true
            {
                onlineManifest = onlineManifest.Trim();
                try
                {
                    File.WriteAllText(StagedManifestLocation, onlineManifest);
                }
                catch (Exception e)
                {
                    Log.Error(@"Error writing cached ASI manifest to disk: " + e.Message);
                }

                try
                {
                    ParseManifest(onlineManifest, true);
                }
                catch (Exception e)
                {
                    Log.Error(@"Error parsing online manifest: " + e.Message);
                    internalLoadManifest(true); //force local load instead
                }
            }
            else if (File.Exists(ManifestLocation))
            {
                Log.Information(@"Loading ASI local manifest");
                LoadManifestFromDisk(ManifestLocation, false);
            }
            else
            {
                //can't get manifest or local manifest.
                //Todo: some sort of handling here as we are running in panel startup
                Log.Error(@"Cannot load ASI manifest: Could not fetch online manifest and no local manifest exists");
            }
        }

        /// <summary>
        /// Extracts the default ASI assets from this assembly so there is a default set of cached assets that are alway required for proper program functionality
        /// </summary>
        public static void ExtractDefaultASIResources()
        {
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

        /// <summary>
        /// Calls ParseManifest() on the given file path.
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <param name="games"></param>
        /// <param name="isStaged">If file is staged for copying the cached location</param>
        /// <param name="selectionStateUpdateCallback"></param>
        private static void LoadManifestFromDisk(string manifestPath, bool isStaged = false)
        {
            ParseManifest(File.ReadAllText(manifestPath), isStaged);
        }

        /// <summary>
        /// Converts integer to MEGame
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
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

        /// <summary>
        ///// Fetches the specified ASI by it's hash for the specified game
        /// </summary>
        /// <param name="asi"></param>
        /// <returns></returns>
        public static ASIModVersion GetASIVersionByHash(string hash, Mod.MEGame game)
        {
            List<ASIMod> relevantGroups = null;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    relevantGroups = MasterME1ASIUpdateGroups;
                    break;
                case Mod.MEGame.ME2:
                    relevantGroups = MasterME2ASIUpdateGroups;
                    break;
                case Mod.MEGame.ME3:
                    relevantGroups = MasterME3ASIUpdateGroups;
                    break;
                default:
                    return null;
            }

            if (relevantGroups.Any())
            {
                return relevantGroups.FirstOrDefault(x => x.HashMatchingHash(hash))?.Versions.First(x => x.Hash == hash);
            }

            return null;
        }

        /// <summary>
        /// Parses a string (xml) into an ASI manifest.
        /// </summary>
        private static void ParseManifest(string xmlText, bool isStaged = false)
        {
            bool reloadOnError = true;
            try
            {
                MasterME1ASIUpdateGroups.Clear();
                MasterME2ASIUpdateGroups.Clear();
                MasterME3ASIUpdateGroups.Clear();
                XElement rootElement = XElement.Parse(xmlText.Trim());

                //I Love Linq
                var updateGroups = (from e in rootElement.Elements(@"updategroup")
                                    select new ASIMod
                                    {
                                        UpdateGroupId = (int)e.Attribute(@"groupid"),
                                        Game = intToGame((int)e.Attribute(@"game")),
                                        IsHidden = e.Attribute(@"hidden") != null && (bool)e.Attribute(@"hidden"),
                                        Versions = e.Elements(@"asimod").Select(z => new ASIModVersion
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
                foreach (var v in updateGroups)
                {
                    switch (v.Game)
                    {
                        case Mod.MEGame.ME1:
                            MasterME1ASIUpdateGroups.Add(v);
                            break;
                        case Mod.MEGame.ME2:
                            MasterME2ASIUpdateGroups.Add(v);
                            break;
                        case Mod.MEGame.ME3:
                            MasterME3ASIUpdateGroups.Add(v);
                            break;
                    }

                    // Linq (get it?) versions to parents
                    foreach (var m in v.Versions)
                    {
                        m.OwningMod = v;
                    }
                }

                reloadOnError = false;
                if (isStaged)
                {
                    File.Copy(StagedManifestLocation, ManifestLocation, true); //this will make sure cached manifest is parsable.
                }
            }
            catch (Exception e)
            {
                if (isStaged && File.Exists(ManifestLocation) && reloadOnError)
                {
                    //try cached instead
                    LoadManifestFromDisk(ManifestLocation, false);
                    return;
                }
                if (!reloadOnError)
                {
                    return; //Don't rethrow exception as we did load the manifest still
                }

                throw new Exception(@"Error parsing the ASI Manifest: " + e.Message);
            }

        }

        /// <summary>
        /// Installs the specific version of an ASI to the specified target
        /// </summary>
        /// <param name="modVersion"></param>
        /// <param name="target"></param>
        /// <param name="forceSource">Null to let application choose the source, true to force online, false to force local cache. This parameter is used for testing</param>
        /// <returns></returns>
        public static bool InstallASIToTarget(ASIModVersion asi, GameTarget target, bool? forceSource = null)
        {
            if (asi.Game != target.Game) throw new Exception($@"ASI {asi.Name} cannot be installed to game {target.Game}");
            Log.Information($@"Processing ASI installation request: {asi.Name} v{asi.Version} -> {target.TargetPath}");
            string destinationFilename = $@"{asi.InstalledPrefix}-v{asi.Version}.asi";
            string cachedPath = Path.Combine(CachedASIsFolder, destinationFilename);
            string destinationDirectory = MEDirectories.ASIPath(target);
            if (!Directory.Exists(destinationDirectory))
            {
                Log.Information(@"Creating ASI directory in game: " + destinationDirectory);
                Directory.CreateDirectory(destinationDirectory);
            }
            string finalPath = Path.Combine(destinationDirectory, destinationFilename);

            // Delete existing ASIs from the same group to ensure we don't install the same mod
            var existingSameGroupMods = target.GetInstalledASIs().OfType<KnownInstalledASIMod>().Where(x => x.AssociatedManifestItem.OwningMod == asi.OwningMod).ToList();
            bool hasExistingVersionOfModInstalled = false;
            if (existingSameGroupMods.Any())
            {
                foreach (var v in existingSameGroupMods)
                {
                    if (v.Hash == asi.Hash && !forceSource.HasValue && !hasExistingVersionOfModInstalled) //If we are forcing a source, we should always install. Delete duplicates past the first one
                    {
                        Log.Information($@"{v.AssociatedManifestItem.Name} is already installed. We will not remove the existing correct installed ASI for this install request");
                        hasExistingVersionOfModInstalled = true;
                        continue; //Don't delete this one. We are already installed. There is no reason to install it again.
                    }
                    Log.Information($@"Deleting existing ASI from same group: {v.InstalledPath}");
                    v.Uninstall();
                }
            }

            if (hasExistingVersionOfModInstalled && !forceSource.HasValue) //Let app decide
            {
                return true; // This asi was "Installed" (because it was already installed).
            }

            // Install the ASI
            if (forceSource == null || forceSource.Value == false)
            {
                Debug.WriteLine("Hit me");
            }
            string md5;
            bool useLocal = forceSource.HasValue && !forceSource.Value; // false (forceLocal)
            if (!useLocal && !forceSource.HasValue)
            {
                useLocal = File.Exists(cachedPath);
            }
            if (useLocal)
            {
                //Check hash first
                md5 = Utilities.CalculateMD5(cachedPath);
                if (md5 == asi.Hash)
                {
                    Log.Information($@"Copying ASI from cached library to destination: {cachedPath} -> {finalPath}");

                    File.Copy(cachedPath, finalPath, true);
                    Log.Information($@"Installed ASI to {finalPath}");
                    Analytics.TrackEvent(@"Installed ASI", new Dictionary<string, string>() {
                                { @"Filename", Path.GetFileNameWithoutExtension(finalPath)}
                            });
                    return true;
                }
            }

            if (!forceSource.HasValue || forceSource.Value)
            {
                WebRequest request = WebRequest.Create(asi.DownloadLink);
                Log.Information(@"Fetching remote ASI from server");
                try
                {
                    using WebResponse response = request.GetResponse();
                    var memoryStream = new MemoryStream();
                    response.GetResponseStream().CopyTo(memoryStream);
                    //MD5 check on file for security
                    md5 = Utilities.CalculateMD5(memoryStream);
                    if (md5 != asi.Hash)
                    {
                        //ERROR!
                        Log.Error(@"Downloaded ASI did not match the manifest! It has the wrong hash.");
                        return false;
                    }

                    Log.Information(@"Fetched remote ASI from server. Installing ASI to " + finalPath);
                    memoryStream.WriteToFile(finalPath);
                    Log.Information(@"ASI successfully installed.");
                    Analytics.TrackEvent(@"Installed ASI", new Dictionary<string, string>()
                    {
                        {@"Filename", Path.GetFileNameWithoutExtension(finalPath)}
                    });

                    //Cache ASI
                    if (!Directory.Exists(CachedASIsFolder))
                    {
                        Log.Information(@"Creating cached ASIs folder");
                        Directory.CreateDirectory(CachedASIsFolder);
                    }

                    Log.Information(@"Caching ASI to local ASI library: " + cachedPath);
                    memoryStream.WriteToFile(cachedPath);
                    return true;
                }
                catch (Exception e)
                {
                    Log.Error($@"Error downloading ASI from {asi.DownloadLink}: {e.Message}");
                }
            }

            // We could not install the ASI
            return false;
        }

        /// <summary>
        /// Installs the latest version of an ASI to the specified target. If there is an existing version installed, it is updated
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool InstallASIToTarget(ASIMod mod, GameTarget target)
        {
            return InstallASIToTarget(mod.LatestVersion, target);
        }

        /// <summary>
        /// Installs the latest version of the specified ASI (by update group ID) to the target
        /// </summary>
        /// <param name="updateGroup"></param>
        /// <param name="nameForLogging"></param>
        /// <param name="gameTarget"></param>
        public static bool InstallASIToTargetByGroupID(int updateGroup, string nameForLogging, GameTarget gameTarget)
        {
            var group = GetASIModsByGame(gameTarget.Game).FirstOrDefault(x => x.UpdateGroupId == updateGroup);
            if (group == null)
            {
                // Cannot find ASI!
                Log.Error($@"Cannot find ASI with update group ID {updateGroup} for game {gameTarget.Game}");
                return false;
            }

            return InstallASIToTarget(group, gameTarget);
            //var asigame = new ASIGame(gameTarget);
            //var dlcModEnabler = asigame.ASIModUpdateGroups.FirstOrDefault(x => x.UpdateGroupId == updateGroup); //DLC mod enabler is group 16
            //if (dlcModEnabler != null)
            //{
            //    Log.Information($"Installing {nameForLogging} ASI");
            //    var asiLockObject = new object();

            //    void asiInstalled()
            //    {
            //        lock (asiLockObject)
            //        {
            //            Monitor.Pulse(asiLockObject);
            //        }
            //    }

            //    var asiNotInstalledAlready = asigame.ApplyASI(dlcModEnabler.GetLatestVersion(), asiInstalled);
            //    if (asiNotInstalledAlready)
            //    {
            //        lock (asiLockObject)
            //        {
            //            Monitor.Wait(asiLockObject, 3500); //3.5 seconds max time.
            //        }
            //    }
            //}
            //else
            //{
            //    Log.Error($"Could not install {nameForLogging} ASI!!");
            //}
        }

        /// <summary>
        /// Gets the list of ASI mods by game from the manifest
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static List<ASIMod> GetASIModsByGame(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return MasterME1ASIUpdateGroups;
                case Mod.MEGame.ME2:
                    return MasterME2ASIUpdateGroups;
                case Mod.MEGame.ME3:
                    return MasterME3ASIUpdateGroups;
                default:
                    return null;
            }
        }
    }
}
