using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.usercontrols;
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

        private static void internalLoadManifest(List<ASIGame> games, Action<object> selectionStateUpdateCallback = null, bool forceLocal = false)
        {
            if (forceLocal && File.Exists(ManifestLocation) || !OnlineContent.CanFetchContentThrottleCheck())
            {
                LoadManifestFromDisk(ManifestLocation, games, false, selectionStateUpdateCallback);
                return;
            }

            using WebClient wc = new WebClient();
            var onlineManifest = forceLocal ? null : OnlineContent.FetchRemoteString(@"https://me3tweaks.com/mods/asi/getmanifest?AllGames=1");
            if (onlineManifest != null) //this cannot be triggered if forceLocal is true
            {
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
                    ParseManifest(onlineManifest, games, true, selectionStateUpdateCallback);
                }
                catch (Exception e)
                {
                    Log.Error(@"Error parsing online manifest: " + e.Message);
                    internalLoadManifest(games, selectionStateUpdateCallback, true); //force local load instead
                }
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

        /// <summary>
        /// Calls ParseManifest() on the given file path.
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <param name="games"></param>
        /// <param name="isStaged">If file is staged for copying the cached location</param>
        /// <param name="selectionStateUpdateCallback"></param>
        private static void LoadManifestFromDisk(string manifestPath, List<ASIGame> games, bool isStaged = false, Action<object> selectionStateUpdateCallback = null)
        {
            ParseManifest(File.ReadAllText(manifestPath), games, isStaged, selectionStateUpdateCallback);
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
    }
}
