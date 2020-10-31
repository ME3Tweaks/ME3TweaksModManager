using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using ByteSizeLib;

using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    //Localizable(false) //do not remove for localizer!
    public partial class OnlineContent
    {
        public const string UpdaterServiceManifestEndpoint = "https://me3tweaks.com/mods/getlatest_batch"; //2 = debug
        public const string UpdaterServiceCodeValidationEndpoint = "https://me3tweaks.com/mods/latestxml/updatecodevalidation";
        private const string UpdateStorageRoot = "https://me3tweaks.com/mods/updates/";


        /// <summary>
        /// Fetch latest version information (manifest attribute) from ME3Tweaks Updater Service. This should not be used for true update checks, use CheckForModUpdates() for that purpose.
        /// </summary>
        /// <param name="updatecode">Code to check</param>
        /// <returns>verison string if found and parsable, null otherwise</returns>
        public static Version GetLatestVersionOfModOnUpdaterService(int updatecode)
        {
            if (updatecode <= 0) return null; //invalid
            string updateFinalRequest = UpdaterServiceManifestEndpoint + "?classicupdatecode[]=" + updatecode;
            using var wc = new System.Net.WebClient();
            try
            {
                string updatexml = wc.DownloadStringAwareOfEncoding(updateFinalRequest);

                XElement rootElement = XElement.Parse(updatexml);
                var modUpdateInfos = (from e in rootElement.Elements("mod")
                                      select new ModUpdateInfo
                                      {
                                          versionstr = (string)e.Attribute("version")
                                      }).ToList();
                if (modUpdateInfos.Count == 1 && Version.TryParse(modUpdateInfos[0].versionstr, out var ver))
                {
                    return ver;
                }
            }
            catch (Exception e)
            {
                Log.Error("Unable to fetch latest version of mod on updater service: " + e.Message);
            }
            return null;
        }

        /// <summary>
        /// Checks mods for updates. ForceUpdateCheck will force the mod to validate against the server (essentially repair mode). It is not used for rate limiting!
        /// </summary>
        /// <param name="modsToCheck">Mods to have server send information about</param>
        /// <param name="forceUpdateCheck">Force update check regardless of version</param>
        /// <returns></returns>
        public static List<ModUpdateInfo> CheckForModUpdates(List<Mod> modsToCheck, bool forceUpdateCheck)
        {
            string updateFinalRequest = UpdaterServiceManifestEndpoint;
            bool first = true;
            foreach (var mod in modsToCheck)
            {
                if (mod.ModModMakerID > 0)
                {
                    //Modmaker style
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }

                    updateFinalRequest += "modmakerupdatecode[]=" + mod.ModModMakerID;

                }
                else if (mod.ModClassicUpdateCode > 0)
                {
                    //Classic style
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }
                    updateFinalRequest += "classicupdatecode[]=" + mod.ModClassicUpdateCode;
                }
                else if (mod.NexusModID > 0 && mod.NexusUpdateCheck)
                {
                    //Nexus style
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }
                    updateFinalRequest += "nexusupdatecode[]=" + mod.Game.ToString().Substring(2) + "-" + mod.NexusModID;
                }
                //else if (mod.NexusModID > 0)
                //{
                //    //Classic style
                //    if (first)
                //    {
                //        updateFinalRequest += "?";
                //        first = false;
                //    }
                //    else
                //    {
                //        updateFinalRequest += "&";
                //    }
                //    updateFinalRequest += "nexusupdatecode[]=" + mod.ModClassicUpdateCode;
                //}
            }

            using var wc = new System.Net.WebClient();
            try
            {
                Debug.WriteLine(updateFinalRequest);
                string updatexml = wc.DownloadStringAwareOfEncoding(updateFinalRequest);

                XElement rootElement = XElement.Parse(updatexml);


                #region classic mods
                var modUpdateInfos = new List<ModUpdateInfo>();
                var classicUpdateInfos = (from e in rootElement.Elements("mod")
                                          select new ModUpdateInfo
                                          {
                                              changelog = (string)e.Attribute("changelog"),
                                              versionstr = (string)e.Attribute("version"),
                                              updatecode = (int)e.Attribute("updatecode"),
                                              serverfolder = (string)e.Attribute("folder"),
                                              sourceFiles = (from f in e.Elements("sourcefile")
                                                             select new SourceFile
                                                             {
                                                                 lzmahash = (string)f.Attribute("lzmahash"),
                                                                 hash = (string)f.Attribute("hash"),
                                                                 size = (int)f.Attribute("size"),
                                                                 lzmasize = (int)f.Attribute("lzmasize"),
                                                                 relativefilepath = f.Value,
                                                                 timestamp = (Int64?)f.Attribute("timestamp") ?? (Int64)0
                                                             }).ToList(),
                                              blacklistedFiles = e.Elements("blacklistedfile").Select(x => x.Value).ToList()
                                          }).ToList();
                foreach (var modUpdateInfo in classicUpdateInfos)
                {
                    modUpdateInfo.ResolveVersionVar();
                    //Calculate update information
                    var matchingMod = modsToCheck.FirstOrDefault(x => x.ModClassicUpdateCode == modUpdateInfo.updatecode);
                    if (matchingMod != null && (forceUpdateCheck || matchingMod.ParsedModVersion < modUpdateInfo.version))
                    {
                        modUpdateInfo.mod = matchingMod;
                        modUpdateInfo.SetLocalizedInfo();
                        string modBasepath = matchingMod.ModPath;
                        foreach (var serverFile in modUpdateInfo.sourceFiles)
                        {
                            var localFile = Path.Combine(modBasepath, serverFile.relativefilepath);
                            if (File.Exists(localFile))
                            {
                                var info = new FileInfo(localFile);
                                if (info.Length != serverFile.size)
                                {
                                    modUpdateInfo.applicableUpdates.Add(serverFile);
                                }
                                else
                                {
                                    //Check hash
                                    CLog.Information("Hashing file for update check: " + localFile, Settings.LogModUpdater);
                                    var md5 = Utilities.CalculateMD5(localFile);
                                    if (md5 != serverFile.hash)
                                    {
                                        modUpdateInfo.applicableUpdates.Add(serverFile);
                                    }
                                }
                            }
                            else
                            {
                                modUpdateInfo.applicableUpdates.Add(serverFile);
                            }
                        }

                        foreach (var blacklistedFile in modUpdateInfo.blacklistedFiles)
                        {
                            var localFile = Path.Combine(modBasepath, blacklistedFile);
                            if (File.Exists(localFile))
                            {
                                Log.Information(@"Blacklisted file marked for deletion: " + localFile);
                                modUpdateInfo.filesToDelete.Add(localFile);
                            }
                        }

                        //Files to remove calculation
                        var modFiles = Directory.GetFiles(modBasepath, "*", SearchOption.AllDirectories).Select(x => x.Substring(modBasepath.Length + 1)).ToList();
                        modUpdateInfo.filesToDelete.AddRange(modFiles.Except(modUpdateInfo.sourceFiles.Select(x => x.relativefilepath), StringComparer.InvariantCultureIgnoreCase).Distinct().ToList()); //Todo: Add security check here to prevent malicious values
                        modUpdateInfo.TotalBytesToDownload = modUpdateInfo.applicableUpdates.Sum(x => x.lzmasize);
                    }
                }

                modUpdateInfos.AddRange(classicUpdateInfos);
                #endregion

                #region modmaker mods
                var modmakerModUpdateInfos = (from e in rootElement.Elements("modmakermod")
                                              select new ModMakerModUpdateInfo
                                              {
                                                  ModMakerId = (int)e.Attribute("id"),
                                                  versionstr = (string)e.Attribute("version"),
                                                  PublishDate = DateTime.ParseExact((string)e.Attribute("publishdate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                                                  changelog = (string)e.Attribute("changelog")
                                              }).ToList();
                modUpdateInfos.AddRange(modmakerModUpdateInfos);
                #endregion

                #region Nexus Mod Third Party
                var nexusModsUpdateInfo = (from e in rootElement.Elements("nexusmod")
                                           select new NexusModUpdateInfo
                                           {
                                               NexusModsId = (int)e.Attribute("id"),
                                               GameId = (int)e.Attribute("game"),
                                               versionstr = (string)e.Attribute("version"),
                                               UpdatedTime = DateTimeOffset.FromUnixTimeSeconds((long)e.Attribute("updated_timestamp")).DateTime
                                           }).ToList();
                modUpdateInfos.AddRange(nexusModsUpdateInfo);
                #endregion
                return modUpdateInfos;
            }
            catch (Exception e)
            {
                Log.Error("Error checking for mod updates: " + App.FlattenException(e));
                Crashes.TrackError(e, new Dictionary<string, string>() {
                    { "Update check URL", updateFinalRequest }
                });
            }
            return null;
        }

        [Localizable(true)]
        public static bool UpdateMod(ModUpdateInfo updateInfo, string stagingDirectory, Action<string> errorMessageCallback)
        {
            Log.Information(@"Updating mod: " + updateInfo.mod.ModName + @" from " + updateInfo.LocalizedLocalVersionString + @" to " + updateInfo.LocalizedServerVersionString);
            string modPath = updateInfo.mod.ModPath;
            string serverRoot = UpdateStorageRoot + updateInfo.serverfolder + '/';
            bool cancelDownloading = false;
            var stagedFileMapping = new ConcurrentDictionary<string, string>();
            foreach (var sf in updateInfo.applicableUpdates)
            {
                sf.AmountDownloaded = 0; //reset in the event this is a second attempt
            }
            Parallel.ForEach(updateInfo.applicableUpdates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (sourcefile) =>
            {
                if (!cancelDownloading)
                {
                    void downloadProgressCallback(long received, long totalToReceived)
                    {
                        sourcefile.AmountDownloaded = received;
                        updateInfo.RecalculateAmountDownloaded();
                    }

                    string fullurl = serverRoot + sourcefile.relativefilepath.Replace('\\', '/') + @".lzma";
                    var downloadedFile = OnlineContent.DownloadToMemory(fullurl, downloadProgressCallback, sourcefile.lzmahash, true);
                    if (downloadedFile.errorMessage != null && !cancelDownloading)
                    {
                        errorMessageCallback?.Invoke(downloadedFile.errorMessage);
                        cancelDownloading = true;
                        return;
                    }


                    if (cancelDownloading)
                    {
                        return; //Concurrency for long running download to memory
                    }
                    //Hash OK
                    string stagingFile = Path.Combine(stagingDirectory, sourcefile.relativefilepath);
                    Directory.CreateDirectory(Directory.GetParent(stagingFile).FullName);

                    //Decompress file
                    MemoryStream decompressedStream = new MemoryStream();
                    SevenZipHelper.LZMA.DecompressLZMAStream(downloadedFile.result, decompressedStream);
                    //SevenZipExtractor.DecompressStream(downloadedFile.result, decompressedStream, null, null);

                    //Hash check output
                    if (decompressedStream.Length != sourcefile.size)
                    {
                        Log.Error($@"Decompressed file ({sourcefile.relativefilepath}) is not of correct size. Expected: {sourcefile.size}, got: {decompressedStream.Length}");
                        errorMessageCallback?.Invoke(M3L.GetString(M3L.string_interp_decompressedFileNotCorrectSize, sourcefile.relativefilepath, sourcefile.size, decompressedStream.Length)); //force localize
                        cancelDownloading = true;
                        return;
                    }

                    var decompressedMD5 = Utilities.CalculateMD5(decompressedStream);
                    if (decompressedMD5 != sourcefile.hash)
                    {
                        Log.Error($@"Decompressed file ({sourcefile.relativefilepath}) has the wrong hash. Expected: {sourcefile.hash}, got: {decompressedMD5}");
                        errorMessageCallback?.Invoke(M3L.GetString(M3L.string_interp_decompressedFileWrongHash, sourcefile.relativefilepath, sourcefile.hash, decompressedMD5)); //force localize
                        cancelDownloading = true;
                        return;
                    }

                    File.WriteAllBytes(stagingFile, decompressedStream.ToArray());
                    if (sourcefile.timestamp != 0)
                    {
                        File.SetLastWriteTimeUtc(stagingFile, new DateTime(sourcefile.timestamp));
                    }
                    Log.Information(@"Wrote updater staged file: " + stagingFile);
                    stagedFileMapping[stagingFile] = Path.Combine(modPath, sourcefile.relativefilepath);
                }
            });
            if (cancelDownloading)
            {
                //callback already should have occured
                return false;
            }

            //All files have been downloaded successfully.
            updateInfo.DownloadButtonText = M3L.GetString(M3L.string_applying);
            //Apply update
            if (stagedFileMapping.Count > 0)
            {
                Log.Information(@"Applying staged update to mod directory");
                foreach (var file in stagedFileMapping)
                {
                    Log.Information($@"Applying update file: {file.Key} => {file.Value}");
                    Directory.CreateDirectory(Directory.GetParent(file.Value).FullName);
                    File.Copy(file.Key, file.Value, true);
                }
            }

            //Delete files no longer in manifest
            foreach (var file in updateInfo.filesToDelete)
            {
                var fileToDelete = Path.Combine(modPath, file);
                Log.Information(@"Deleting file for mod update: " + fileToDelete);
                File.Delete(fileToDelete);
            }

            //Delete empty subdirectories
            Utilities.DeleteEmptySubdirectories(modPath);
            Utilities.DeleteFilesAndFoldersRecursively(stagingDirectory);
            //We're done!
            return true;
        }

        [Localizable(true)]
        public static string StageModForUploadToUpdaterService(Mod mod, List<string> files, long totalAmountToCompress, Func<bool?> canceledCallback = null, Action<string> updateUiTextCallback = null, Action<double> setProgressCallback = null)
        {
            //create staging dir
            var stagingPath = Utilities.GetUpdaterServiceUploadStagingPath();
            if (Directory.Exists(stagingPath))
            {
                Utilities.DeleteFilesAndFoldersRecursively(stagingPath);
            }
            Directory.CreateDirectory(stagingPath);


            long amountDone = 0;
            //run files 
            Parallel.ForEach(files.AsParallel().AsOrdered(), new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, x =>
            {
                var canceledCheck = canceledCallback?.Invoke();
                if (canceledCheck.HasValue && canceledCheck.Value)
                {
                    return; //skip
                }
                LZMACompressFileForUpload(x, stagingPath, mod.ModPath, canceledCallback);
                GC.Collect(); //As this can use lots of memory we're going to have to run a GC collect here
                var totalDone = Interlocked.Add(ref amountDone, new FileInfo(Path.Combine(mod.ModPath, x)).Length);
                setProgressCallback?.Invoke(totalDone * 1.0 / totalAmountToCompress);
                updateUiTextCallback?.Invoke(M3L.GetString(M3L.string_interp_compressingModForUpdaterServicePercent, Math.Round(totalDone * 100.0 / totalAmountToCompress))); //force localize
            });
            return stagingPath;
        }

        private static string LZMACompressFileForUpload(string relativePath, string stagingPath, string modPath, Func<bool?> cancelCheckCallback = null)
        {
            Log.Information(@"Compressing " + relativePath);
            var destPath = Path.Combine(stagingPath, relativePath + @".lzma");
            var sourcePath = Path.Combine(modPath, relativePath);
            Directory.CreateDirectory(Directory.GetParent(destPath).FullName);

            var src = File.ReadAllBytes(sourcePath);
            var compressedBytes = SevenZipHelper.LZMA.Compress(src);
            byte[] fixedBytes = new byte[compressedBytes.Count() + 8]; //needs 8 byte header written into it (only mem version needs this)
            Buffer.BlockCopy(compressedBytes, 0, fixedBytes, 0, 5);
            fixedBytes.OverwriteRange(5, BitConverter.GetBytes((int)new FileInfo(sourcePath).Length));
            Buffer.BlockCopy(compressedBytes, 5, fixedBytes, 13, compressedBytes.Length - 5);


            File.WriteAllBytes(destPath, fixedBytes);

            //Test!
            //var decomp = SevenZipHelper.LZMA.DecompressLZMAFile(fixedBytes);
            //if (decomp == null)
            //{
            //    Debug.WriteLine("NOT LZMA");
            //}
            //else if (decomp.Length != src.Length)
            //{
            //    Debug.WriteLine("Decompressed data does not match source length!");
            //}

            return destPath;
        }

        [Localizable(true)]
        [DebuggerDisplay("ModMakerModUpdateInfo | Code {ModMakerId}")]
        public class ModMakerModUpdateInfo : ModUpdateInfo
        {
            public int ModMakerId;
            public string UIStatusString { get; set; }
            public DateTime PublishDate { get; internal set; }
            public int OverallProgressValue { get; set; }
            public int OverallProgressMax { get; set; }

            internal override void SetLocalizedInfo()
            {
                base.SetLocalizedInfo();
                UIStatusString = M3L.GetString(M3L.string_interp_ModmakerCodeX, ModMakerId);
            }
        }

        [Localizable(true)]
        public class NexusModUpdateInfo : ModUpdateInfo
        {
            public int NexusModsId;
            public int GameId;
            public string UIStatusString { get; set; }
            public DateTime UpdatedTime { get; internal set; }

            internal override void SetLocalizedInfo()
            {
                base.SetLocalizedInfo();
                var updatedTime = UpdatedTime.ToString(@"d"); //doing it this outside of statement makes it easier for localizer tool
                UIStatusString = M3L.GetString(M3L.string_interp_updatedDateX, updatedTime);
                DownloadButtonText = M3L.GetString(M3L.string_openNexusModsPage);
                changelog = M3L.GetString(M3L.string_nexusModsUpdateInstructions);
            }
        }

        [Localizable(true)]
        [DebuggerDisplay("ModUpdateInfo | {mod.ModName} with {filesToDelete.Count} FTDelete and {applicableUpdates.Count} FTDownload")]
        public class ModUpdateInfo : INotifyPropertyChanged
        {
            public Mod mod { get; set; }
            public List<SourceFile> sourceFiles;
            public List<string> blacklistedFiles;
            public string changelog { get; set; }
            public string serverfolder;
            public int updatecode;
            public string versionstr { get; set; }
            public Version version;
            public event PropertyChangedEventHandler PropertyChanged;
            public bool UpdateInProgress { get; set; }
            public ICommand ApplyUpdateCommand { get; set; }
            public long TotalBytesToDownload { get; set; }
            public long CurrentBytesDownloaded { get; set; }
            public bool Indeterminate { get; set; }
            public bool HasFilesToDownload => applicableUpdates.Count > 0;
            public bool HasFilesToDelete => filesToDelete.Count > 0;
            public string DownloadButtonText { get; set; }
            public string LocalizedLocalVersionString { get; set; }
            public string LocalizedServerVersionString { get; set; }
            public void RecalculateAmountDownloaded()
            {
                var cbDlOld = CurrentBytesDownloaded;
                CurrentBytesDownloaded = sourceFiles.Sum(x => x.AmountDownloaded);
                RemainingDataToDownload = ""; //trigger value change
                if (cbDlOld != CurrentBytesDownloaded)
                {
                    ProgressChanged?.Invoke(this, (CurrentBytesDownloaded, TotalBytesToDownload));
                }
            }
            public string FilesToDeleteUIString => M3L.GetString(M3L.string_interp_XfilesWillBeDeleted, filesToDelete.Count);

            public string FilesToDownloadUIString => M3L.GetString(M3L.string_interp_XfilesWillBeDownloaded, applicableUpdates.Count, TotalBytesHR);

            /// <summary>
            /// This mod has enough info to try to resolve version string
            /// </summary>
            public void ResolveVersionVar()
            {
                Version.TryParse(versionstr, out version);
            }

            /// <summary>
            /// This object now has enough variables set to resolve localization strings
            /// </summary>
            internal virtual void SetLocalizedInfo()
            {
                LocalizedLocalVersionString = M3L.GetString(M3L.string_interp_localVersion, mod.ModVersionString);
                LocalizedServerVersionString = M3L.GetString(M3L.string_interp_serverVersion, versionstr);
            }

            public ObservableCollectionExtended<SourceFile> applicableUpdates { get; } = new ObservableCollectionExtended<SourceFile>();
            public ObservableCollectionExtended<string> filesToDelete { get; } = new ObservableCollectionExtended<string>();
            public bool CanUpdate { get; internal set; } = true; //Default to true
            public string TotalBytesHR => ByteSize.FromBytes(TotalBytesToDownload).ToString();
            public string RemainingDataToDownload
            {
                get => (TotalBytesToDownload - CurrentBytesDownloaded) > 0 ? ByteSize.FromBytes(TotalBytesToDownload - CurrentBytesDownloaded).ToString(@"0.00") : "";
                set { } //do nothing.
            }

            public event Action<object, (long currentDl, long totalToDl)> ProgressChanged;
        }

        public class SourceFile
        {
            public string lzmahash { get; internal set; }
            public string relativefilepath { get; internal set; }
            public long lzmasize { get; internal set; }
            public long size { get; internal set; }
            public string hash { get; internal set; }
            public long timestamp { get; internal set; }
            public long AmountDownloaded;
            public SourceFile() { }
        }
    }
}
