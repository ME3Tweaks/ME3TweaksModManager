using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.ui;
using Pathoschild.FluentNexus.Models;
using System.Threading;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksCore.Objects;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.telemetry;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Describes the current state of a download. INIT -> QUEUED -> DOWNLOADING -> DOWNLOADCOMPLETE -> [(Optionally) WAITINGFORIMPORT -> IMPORTING -> FINISHED]
    /// </summary>
    public enum EModDownloadState
    {
        UNKNOWN,
        /// <summary>
        /// Download failed
        /// </summary>
        FAILED,
        /// <summary>
        /// User aborted download in progress
        /// </summary>
        DOWNLOADCANCELED,
        /// <summary>
        /// Download is gathering necessary information before it is ready to download
        /// </summary>
        INITIALIZING,
        /// <summary>
        /// Download is currently waiting to start
        /// </summary>
        QUEUED,
        /// <summary>
        /// Mod is currently downloading
        /// </summary>
        DOWNLOADING,
        /// <summary>
        /// Download has finished
        /// </summary>
        DOWNLOADCOMPLETE,
        /// <summary>
        /// Mod is queued for automatic import
        /// </summary>
        WAITINGFORIMPORT,
        /// <summary>
        /// The mod is currently importing
        /// </summary>
        IMPORTING,
        /// <summary>
        /// Everything has completed
        /// </summary>
        FINISHED,
        /// <summary>
        /// The download is waiting for the user to start it from the website, as it cannot be started from within Mod Manager
        /// </summary>
        WAITINGFORUSER,
    }

    /// <summary>
    /// Class for information about a mod that is being downloaded, and optionally imported
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public abstract class ModDownload
    {
        /// <summary>
        /// The maximum size of an archive that can be downloaded and loaded from memory.
        /// </summary>
        private protected static readonly long DOWNLOAD_TO_MEMORY_SIZE_CAP = 100 * FileSize.MebiByte;

        #region Useful UI stuff
        /// <summary>
        /// The current state of the download
        /// </summary>
        public EModDownloadState DownloadState { get; set; }

        /// <summary>
        /// Invoked when DownloadState has changed
        /// </summary>
        private void OnDownloadStateChanged()
        {
            // Import is only set to true if download is done and we aren't set to auto import
            CanImport = DownloadState == EModDownloadState.DOWNLOADCOMPLETE && !AutoImport;

            switch (DownloadState)
            {
                // Download has stopped.
                case EModDownloadState.FAILED:
                case EModDownloadState.DOWNLOADCANCELED:
                case EModDownloadState.DOWNLOADCOMPLETE:
                case EModDownloadState.FINISHED:
                case EModDownloadState.QUEUED:
                case EModDownloadState.WAITINGFORUSER:
                    IsDownloadActive = false;
                    break;

                // Download is actively doing something
                case EModDownloadState.UNKNOWN:
                case EModDownloadState.INITIALIZING:
                case EModDownloadState.DOWNLOADING:
                case EModDownloadState.WAITINGFORIMPORT:
                case EModDownloadState.IMPORTING:
                    IsDownloadActive = true;
                    break;
                default:
                    // All cases should be covered!
                    IsDownloadActive = false;
                    break;
            }

            UpdateCanCancel();

            // Fire state changed after we update the active variable
            DownloadStateChanged?.Invoke(this, null);
        }

        /// <summary>
        /// If download is in state where user could press 'Import' button.
        /// </summary>
        public bool CanImport { get; set; }

        /// <summary>
        /// If this download is in a state where it can be canceled by the user.
        /// </summary>
        public bool CanCancel { get; private set; }

        /// <summary>
        /// Recalculates <see cref="CanCancel"/>. Importing can only be canceled before mods start extracting.
        /// </summary>
        internal void UpdateCanCancel()
        {
            if (CancellationController.IsCancellationRequested)
            {
                CanCancel = false;
                return;
            }

            CanCancel = DownloadState switch
            {
                EModDownloadState.INITIALIZING => true,
                EModDownloadState.QUEUED => true,
                EModDownloadState.WAITINGFORUSER => true,
                EModDownloadState.DOWNLOADING => true,
                EModDownloadState.DOWNLOADCOMPLETE => true,
                EModDownloadState.IMPORTING => ImportFlow is { IsCancelRequested: false, CurrentState: EModArchiveImportState.SCANNING or EModArchiveImportState.SCANCOMPLETED },
                _ => false
            };
        }

        /// <summary>
        /// Name to show in the UI for this download
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Counter used to generate <see cref="QueueOrder"/>
        /// </summary>
        private static long _queueOrderCounter;

        /// <summary>
        /// Order this download was created in, for displaying downloads in the order they were added
        /// </summary>
        public long QueueOrder { get; } = Interlocked.Increment(ref _queueOrderCounter);

        public event EventHandler<EventArgs> DownloadStateChanged;


        public long ProgressValue { get; protected internal set; }
        public long ProgressMaximum { get; protected internal set; }
        public bool ProgressIndeterminate { get; protected internal set; } = true;

        /// <summary>
        /// Set to true if this download is currently doing something (downloading, importing)
        /// </summary>
        public bool IsDownloadActive { get; private set; }

        /// <summary>
        /// UI string to display. Should be short.
        /// </summary>
        public string Status { get; protected internal set; }

        public void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, DataEventArgs.Empty);
        }

        #endregion

        /// <summary>
        /// The downloaded stream data
        /// </summary>
        public Stream DownloadedStream { get; private protected set; }

        /// <summary>
        /// When the source is canceled, the download will be interrupted, if any.
        /// </summary>
        protected CancellationTokenSource CancellationController { get; }

        /// <summary>
        /// If this download has been canceled
        /// </summary>
        public bool IsCancelRequested => CancellationController.IsCancellationRequested;

        #region AutoImport
        /// <summary>
        /// If import should be automatically attempted
        /// </summary>
        public bool AutoImport { get; set; }

        /// <summary>
        /// Associated mod importing flow for this download - if there is no import flow this will be null.
        /// </summary>
        public ModArchiveImport ImportFlow { get; set; }

        #endregion

        /// <summary>
        /// The download filename (may not exist on disk)
        /// </summary>
        public string FileName { get; set; }

        public bool IsDownloading => DownloadState is EModDownloadState.DOWNLOADING;
        public bool IsImporting => DownloadState is EModDownloadState.IMPORTING;

        /// <summary>
        /// If this has been marked as duplicate and removal shouldn't use the download key
        /// </summary>
        public bool IsDuplicate { get; internal set; }

        #region Event handlers
        /// <summary>
        /// Invoked when the mod has initialized
        /// </summary>
        public event EventHandler<EventArgs> OnInitialized;
        /// <summary>
        /// Invoked when a mod download has completed
        /// </summary>
        public event EventHandler<DataEventArgs> OnModDownloaded;
        /// <summary>
        /// Invoked when a mod download has an error
        /// </summary>
        public event EventHandler<string> OnModDownloadError;

        /// <summary>
        /// When the UIStatus string has changed.
        /// </summary>
        public event EventHandler StatusChanged;
        #endregion

        /// <summary>
        /// Command to remove this download from failed downloads
        /// </summary>
        public GenericCommand RemoveDownloadCommand {get;set;}

        /// <summary>
        /// Constructor - sets state to Initializing
        /// </summary>
        protected ModDownload()
        {
            // Must be created before state is set as state changes check it
            CancellationController = new CancellationTokenSource();
            DownloadState = EModDownloadState.INITIALIZING;
            RemoveDownloadCommand = new GenericCommand(()=>DownloadManager.FailedDownloads.Remove(this), () => true);
        }

        private protected void OnDownloadProgress(long done, long total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            ProgressIndeterminate = false;
            Status = $@"{FileSize.FormatSize(ProgressValue)}/{FileSize.FormatSize(ProgressMaximum)}";
        }

        public abstract void StartDownload(bool forceDownloadToDisk = false);

        protected void InternalOnModDownloadError(string str)
        {
            OnModDownloadError?.Invoke(this, str);
        }

        private protected void InternalOnModDownloaded(DataEventArgs args)
        {
            OnModDownloaded?.Invoke(this, args);
        }

        private protected void InternalOnInitialized()
        {
            OnInitialized?.Invoke(this, null);
        }

        public abstract string CreateDownloadKey();

        /// <summary>
        /// Signals the download to stop without changing its state. Used when the application is closing.
        /// </summary>
        public void CancelDownload()
        {
            CancellationController.Cancel();
        }

        /// <summary>
        /// Synchronization object so a download can only be canceled once
        /// </summary>
        private readonly object _cancelSyncObj = new object();

        /// <summary>
        /// Cancels this download, if it is in a state that can be canceled. Downloads that are in progress, or imports that are scanning, will
        /// move to the canceled state once they stop.
        /// </summary>
        public void Cancel()
        {
            lock (_cancelSyncObj)
            {
                if (!CanCancel)
                    return;

                M3Log.Information($@"Canceling download {DisplayName ?? FileName ?? CreateDownloadKey()} in state {DownloadState}");
                var state = DownloadState;
                CancellationController.Cancel();
                switch (state)
                {
                    case EModDownloadState.INITIALIZING:
                    case EModDownloadState.QUEUED:
                    case EModDownloadState.WAITINGFORUSER:
                        // Nothing has been downloaded yet
                        MarkCanceled();
                        break;
                    case EModDownloadState.DOWNLOADING:
                        // The download thread will see the cancellation and clean up
                        Status = M3L.GetString(M3L.string_canceling);
                        UpdateCanCancel();
                        break;
                    case EModDownloadState.DOWNLOADCOMPLETE when ImportFlow == null:
                        // Downloaded but not imported
                        CleanupDownloadedData(Settings.ModDownloadCacheFolder == null);
                        MarkCanceled();
                        break;
                    case EModDownloadState.DOWNLOADCOMPLETE:
                    case EModDownloadState.IMPORTING:
                        // The importer will see the cancellation once it has finished scanning
                        ImportFlow?.RequestCancel();
                        Status = M3L.GetString(M3L.string_canceling);
                        UpdateCanCancel();
                        break;
                }
            }
        }

        /// <summary>
        /// Moves this download to the canceled state.
        /// </summary>
        internal void MarkCanceled()
        {
            ProgressIndeterminate = false;
            ProgressValue = 0;
            Status = M3L.GetString(M3L.string_canceled);
            DownloadState = EModDownloadState.DOWNLOADCANCELED;
        }

        /// <summary>
        /// Closes the downloaded data stream and optionally deletes the file it was downloaded to, if it was downloaded to disk.
        /// </summary>
        /// <param name="deleteFile">If the file on disk should be deleted</param>
        internal void CleanupDownloadedData(bool deleteFile)
        {
            var stream = DownloadedStream;
            if (stream == null)
                return;

            string filePath = (stream as FileStream)?.Name;
            try
            {
                stream.Dispose();
            }
            catch (Exception e)
            {
                M3Log.Warning($@"Error closing download stream: {e.Message}");
            }

            if (deleteFile && filePath != null && File.Exists(filePath))
            {
                try
                {
                    M3Log.Information($@"Deleting download file {filePath}");
                    File.Delete(filePath);
                }
                catch (Exception e)
                {
                    M3Log.Warning($@"Could not delete download file {filePath}: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Class for downloading a mod from NexusMods
    /// </summary>
    public class NexusModDownload : ModDownload
    {
        // These files are whitelisted as they will not see a moddesc.ini file but get one from serverside ----------
        private static readonly int[] WhitelistedME1FileIDs = new[]
        {
            116, // skip intro movies
            117, // skip to main menu
            120, // controller skip intro movies
            121, // controller skip to main menu
            245, // ME1 Controller 1.2.2
            326, // MAKO MOD
            327, // Mako Mod v2
            328, // Mako mod v3
            569, // mass effect ultrawide
        };

        private static readonly int[] WhitelistedME2FileIDs = new[]
        {
            3, // cheat console
            44, // faster load screens animated
            338, // Controller Mod 1.7.2
            365, // no minigames 2.0.2
        };

        private static readonly int[] WhitelistedME3FileIDs = new[]
        {
            0,
        };

        // End whitelisting-------------------------------------------------------------------------------------------------

        /// <summary>
        /// Linked used to construct this nexus download
        /// </summary>
        public string NXMLink { get; set; }

        /// <summary>
        /// Associated nexus ModFile object.
        /// </summary>
        public ModFile ModFile { get; private set; }

        /// <summary>
        /// Parsed NexusProtocolLink from NXMLink
        /// </summary>
        public NexusProtocolLink ProtocolLink { get; private set; }

        /// <summary>
        /// List of available download URLs for this file
        /// </summary>
        public List<ModFileDownloadLink> DownloadLinks { get; } = new List<ModFileDownloadLink>();

        /// <summary>
        /// When <see cref="DownloadLinks"/> were generated. Links expire, so downloads that were queued for a long time need new ones.
        /// </summary>
        private DateTime DownloadLinksGeneratedAt;

        /// <summary>
        /// How long generated download links are used for before new ones are requested
        /// </summary>
        private static readonly TimeSpan DownloadLinkMaxAge = TimeSpan.FromMinutes(30);

        public NexusModDownload(string nxmLink) : base()
        {
            NXMLink = nxmLink;
            ProtocolLink = NexusProtocolLink.Parse(NXMLink);
        }

        /// <summary>
        /// Creates a download for a specific NexusMods file that will be imported automatically once it is downloaded, such as for mods in an install group.
        /// </summary>
        /// <param name="fileInfo">The file to download</param>
        /// <param name="displayName">Name to show until the file information has been fetched from NexusMods</param>
        /// <param name="requiresUserToStart">If the user must start the download from the website (non-premium users). The download will be put into the <see cref="EModDownloadState.WAITINGFORUSER"/> state.</param>
        /// <returns></returns>
        public static NexusModDownload CreateAutoImportDownload(NexusFileInfo fileInfo, string displayName, bool requiresUserToStart)
        {
            var dl = new NexusModDownload($@"nxm://{fileInfo.Domain}/mods/{fileInfo.ModId}/files/{fileInfo.FileId}")
            {
                DisplayName = displayName,
                AutoImport = true
            };

            if (requiresUserToStart)
            {
                dl.Status = M3L.GetString(M3L.string_waitingForYouToStartDownload);
                dl.ProgressIndeterminate = false;
                dl.DownloadState = EModDownloadState.WAITINGFORUSER;
            }

            return dl;
        }

        /// <summary>
        /// Opens the NexusMods page for this file.
        /// </summary>
        /// <param name="modManagerDownload">If the page should offer the 'Mod Manager Download' button, which sends the download to Mod Manager</param>
        public void OpenFilePage(bool modManagerDownload)
        {
            if (ProtocolLink == null)
                return;

            var link = ProtocolLink.ToNexusDownloadPageLink();
            if (modManagerDownload)
            {
                link += @"&nmm=1";
            }

            M3Utilities.OpenWebpage(link);
        }

        /// <summary>
        /// Checks the domain and blacklist for this file. Sets the download to failed if it cannot be downloaded.
        /// </summary>
        /// <returns>True if the file is allowed to be downloaded</returns>
        private bool ValidateDownloadAllowed()
        {
            if (!NexusModsUtilities.AllSupportedNexusDomains.Contains(ProtocolLink?.Domain))
            {
                M3Log.Error($@"Cannot download file from unsupported domain: {ProtocolLink?.Domain}. Open your preferred mod manager from that game first");
                SetFailed(M3L.GetString(M3L.string_interp_dialog_modNotForThisModManager, ProtocolLink?.Domain));
                return false;
            }

            // Mod Manager 8: Blacklisting files
            if (BlacklistingService.IsNXMBlacklisted(ProtocolLink))
            {
                M3Log.Error($@"File is blacklisted by ME3Tweaks: {ProtocolLink?.Domain} file {ProtocolLink.FileId}");
                SetFailed(M3L.GetString(M3L.string_description_blacklistedMod));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets this download to the failed state, unless it has been canceled.
        /// </summary>
        /// <param name="error">Error message</param>
        private void SetFailed(string error)
        {
            if (CancellationController.IsCancellationRequested)
                return; // Canceled; this is no longer relevant

            ProgressIndeterminate = false;
            Status = error;
            DownloadState = EModDownloadState.FAILED;
            InternalOnModDownloadError(error);
        }

        /// <summary>
        /// Fetches information about the file from NexusMods for a download that is waiting on the user to start it. This does not generate download links.
        /// If the file cannot be downloaded, such as it being deleted, the download is set to failed.
        /// </summary>
        public void LoadPendingMetadata()
        {
            Task.Run(() =>
            {
                try
                {
                    if (ProtocolLink == null || !ValidateDownloadAllowed())
                        return;

                    var modFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(ProtocolLink.Domain, ProtocolLink.ModId, ProtocolLink.FileId).Result;
                    if (modFile == null || CancellationController.IsCancellationRequested || DownloadState != EModDownloadState.WAITINGFORUSER)
                        return;

                    ModFile = modFile;
                    DisplayName = modFile.Name;
                    FileName = modFile.FileName;
                    if (modFile.Category == FileCategory.Deleted)
                    {
                        M3Log.Error($@"Cannot download {modFile.FileName}: File deleted from NexusMods");
                        SetFailed(M3L.GetString(M3L.string_dialog_cannotDownloadDeletedFile));
                    }
                }
                catch (Exception e)
                {
                    // Not fatal - the user can still try the download
                    M3Log.Warning($@"Could not fetch file information for {NXMLink}: {e.Message}");
                }
            });
        }

        /// <summary>
        /// Uses the given nxm link, which was generated by the website, for a download that is waiting on the user to start it.
        /// The download is moved back to initializing. Call <see cref="Initialize"/> after this.
        /// </summary>
        /// <param name="nxmLink">Link from the website</param>
        public void AdoptNXMLink(string nxmLink)
        {
            M3Log.Information($@"Using nxm link from website for download that was waiting on the user: {nxmLink}");
            NXMLink = nxmLink;
            ProtocolLink = NexusProtocolLink.Parse(nxmLink);
            Status = null;
            ProgressIndeterminate = true;
            DownloadState = EModDownloadState.INITIALIZING;
        }

        /// <summary>
        /// Requests download links for this file from NexusMods.
        /// </summary>
        private void GenerateDownloadLinks()
        {
            DownloadLinks.Clear();
            DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink).Result);
            DownloadLinksGeneratedAt = DateTime.Now;
        }


        private bool IsDownloadWhitelisted(string domain, ModFile modFile)
        {
            switch (domain)
            {
                case @"masseffect":
                    return WhitelistedME1FileIDs.Contains(modFile.FileID);
                case @"massseffect2":
                    return WhitelistedME2FileIDs.Contains(modFile.FileID);
                case @"masseffect3":
                    return WhitelistedME3FileIDs.Contains(modFile.FileID);
            }

            return false;
        }

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin.
        /// THIS IS A BLOCKING CALL DO NOT RUN ON THE UI
        /// </summary>
        public void Initialize()
        {
            M3Log.Information($@"Initializing {NXMLink}");
            Task.Run(() =>
            {
                try
                {
                    DownloadLinks.Clear();
                    if (ProtocolLink == null) return; // Parse failed.

                    if (!ValidateDownloadAllowed())
                        return;

                    ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(ProtocolLink.Domain, ProtocolLink.ModId, ProtocolLink.FileId).Result;
                    if (ModFile != null)
                    {
                        DisplayName = ModFile.Name;
                        if (ModFile.SizeInBytes != null && ModFile.SizeInBytes.Value > DOWNLOAD_TO_MEMORY_SIZE_CAP && MUtilities.GetDiskFreeSpaceEx(M3Filesystem.GetModDownloadCacheDirectory(), out var free, out var total, out var totalFree))
                        {
                            // Check free disk space.
                            var spaceRequiredWithBuffer = ModFile.SizeInBytes * 1.2;// 20% buffer.
                            if (totalFree < spaceRequiredWithBuffer)
                            //if (totalFree < ModFile.SizeInBytes * 100000.0) // Debug code
                            {
                                M3Log.Error($@"There is not enough free space on {Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory())} to download {ModFile.FileName}. We need {FileSize.FormatSize((long)spaceRequiredWithBuffer)} but only {FileSize.FormatSize(totalFree)} is available.");
                                SetFailed(M3L.GetString(M3L.string_interp_notEnoughFreeSpaceForDownload, Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory()), ModFile.FileName, FileSize.FormatSize((long)spaceRequiredWithBuffer), FileSize.FormatSize(totalFree)));
                                return;
                            }
                        }


                        if (ModFile.Category != FileCategory.Deleted)
                        {
                            if (ProtocolLink.Key != null)
                            {
                                // Website click
                                if (ProtocolLink.Domain is @"masseffect" or @"masseffect2" && !IsDownloadWhitelisted(ProtocolLink.Domain, ModFile))
                                {
                                    // Check to see file has moddesc.ini the listing
                                    var fileListing = NexusModsUtilities.GetFileListing(ModFile);

                                    // 02/27/2022: We check for TPISService SizeInBytes. It's not 100% accurate since we don't have an MD5 to check against. But it's pretty likely it's supported.
                                    if (fileListing == null || !HasModdescIni(fileListing) && ModFile.SizeInBytes != null && TPIService.GetImportingInfosBySize(ModFile.SizeInBytes.Value).Count == 0)
                                    {
                                        M3Log.Error($@"This file is not whitelisted for download and does not contain a moddesc.ini file, this is not a mod manager mod: {ModFile.FileName}");
                                        SetFailed(M3L.GetString(M3L.string_interp_nexusModNotCompatible, ModFile.Name));
                                        return;
                                    }
                                }


                                // download with manager was clicked.

                                // Check if parameters are correct!
                                GenerateDownloadLinks();
                            }
                            else
                            {
                                // premium? no parameters were supplied...
                                if (!NexusModsUtilities.UserInfo.IsPremium)
                                {
                                    M3Log.Error($@"Cannot download {ModFile.FileName}: User is not premium, but this link is not generated from NexusMods");
                                    SetFailed(M3L.GetString(M3L.string_dialog_mustBePremiumUserToDownload));
                                    return;
                                }

                                GenerateDownloadLinks();
                            }

                            FileName = ModFile.FileName;
                            ProgressMaximum = ModFile.SizeInBytes ?? ModFile.SizeInKilobytes * 1024L; // SizeKb is the original version. They added SizeInBytes at my request
                            M3Log.Information($@"ModDownload '{ModFile.FileName}' metadata is now available");

                            if (CancellationController.IsCancellationRequested)
                                return; // Canceled while initializing

                            InternalOnInitialized();
                            DownloadState = EModDownloadState.QUEUED;
                        }
                        else
                        {
                            M3Log.Error($@"Cannot download {ModFile.FileName}: File deleted from NexusMods");
                            SetFailed(M3L.GetString(M3L.string_dialog_cannotDownloadDeletedFile));
                        }
                    }
                }
                catch (Exception e)
                {
                    M3Log.Exception(e, $@"Error downloading {ModFile?.FileName}:");
                    if (CancellationController.IsCancellationRequested)
                        return; // Canceled while initializing
                    DownloadState = EModDownloadState.FAILED;
                    if (Status == null)
                    {
                        Status = e.Message;
                    }
                    ProgressIndeterminate = false;
                    InternalOnModDownloadError(M3L.GetString(M3L.string_interp_errorDownloadingModX, e.Message));
                }
            });
        }

        /// <summary>
        /// Begins downloading of the mod to disk or memory.
        /// </summary>
        /// <param name="cancellationToken">Token to indicate the download has been canceled.</param>
        public override void StartDownload(bool forceDownloadToDisk = false)
        {
            DownloadState = EModDownloadState.DOWNLOADING;
            var stack = Environment.StackTrace;
            Task.Run(() =>
            {
                if (CancellationController.Token.IsCancellationRequested)
                {
                    // Canceled before the download started
                    MarkCanceled();
                    return;
                }

                // Links generated without a website key may have been generated a long time ago if this download
                // was queued behind many others; they expire, so get new ones.
                if (ProtocolLink?.Key == null && DateTime.Now - DownloadLinksGeneratedAt > DownloadLinkMaxAge)
                {
                    try
                    {
                        M3Log.Information($@"Download links for {ModFile?.FileName} are old, requesting new ones");
                        GenerateDownloadLinks();
                    }
                    catch (Exception e)
                    {
                        M3Log.Warning($@"Could not refresh download links, using existing ones: {e.Message}");
                    }
                }

                if (!forceDownloadToDisk && ProgressMaximum < DOWNLOAD_TO_MEMORY_SIZE_CAP && Settings.ModDownloadCacheFolder == null) // Mod Manager 8.0.1: If cache is set, always download to disk    
                {
                    DownloadedStream = new MemoryStream();
                    M3MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download MemoryStream", DownloadedStream);
                }
                else
                {
                    DownloadedStream = new FileStream(Path.Combine(M3Filesystem.GetModDownloadCacheDirectory(), ModFile.FileName), FileMode.Create);
                    M3MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download FileStream", DownloadedStream);
                }

                // 06/19/2024 - try all download links in order
                var useNextUri = true;
                for (int i = 0; i < DownloadLinks.Count && useNextUri; i++)
                {
                    var isLastAttempt = i == DownloadLinks.Count - 1;
                    var downloadUri = DownloadLinks[i].Uri;
                    M3Log.Information($@"Download attempt {i + 1}: {downloadUri}");
                    if (i > 0)
                    {
                        Status = M3L.GetString(M3L.string_interp_startingDownloadAttemptX, i + 1);
                    }
                    else
                    {
                        Status = M3L.GetString(M3L.string_startingDownload);
                    }

                    var downloadResult = M3OnlineContent.DownloadToStream(downloadUri.ToString(), OnDownloadProgress,
                        null, true, DownloadedStream, CancellationController.Token);
                    if (downloadResult.errorMessage != null)
                    {
                        if (CancellationController.Token.IsCancellationRequested)
                        {
                            // Aborted download. Do not try any other links; remove the partial download.
                            M3Log.Information($@"Download of {ModFile?.FileName} was canceled");
                            CleanupDownloadedData(true);
                            MarkCanceled();
                            return;
                        }

                        // Reset the stream
                        if (DownloadedStream.CanWrite)
                        {
                            DownloadedStream.Position = 0;
                            DownloadedStream.SetLength(0);
                        }

                        if (isLastAttempt)
                        {
                            // Exhausted links
                            DownloadedStream?.Dispose();
                        }

                        if (CancellationController.Token.IsCancellationRequested)
                        {
                            // Aborted download.
                            isLastAttempt = true;
                        }
                        else
                        {
                            M3Log.Error($@"Download failed: {downloadResult.errorMessage}");
                            if (isLastAttempt)
                            {
                                InternalOnModDownloadError(downloadResult.errorMessage);
                            }
                            else
                            {
                                M3Log.Information(@"Trying next download link");
                            }
                        }

                        if (isLastAttempt && !CancellationController.Token.IsCancellationRequested)
                        {
                            // Download didn't work!
                            M3OpenTelemetry.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                            {
                                { @"Domain", ProtocolLink?.Domain },
                                { @"File", ModFile?.Name },
                                { @"Result", $@"Failed, {downloadResult.errorMessage}" },
                            });
                            if (downloadResult.result != null)
                            {
                                // Ensure download stream has been closed
                                downloadResult.result.Close();
                                if (downloadResult.result is FileStream fs && File.Exists(fs.Name))
                                {
                                    try
                                    {
                                        File.Delete(fs.Name);
                                    }
                                    catch
                                    {
                                        // Don't really care here...
                                    }
                                }
                            }
                            Status = downloadResult.errorMessage;
                            DownloadState = EModDownloadState.FAILED;
                            return;
                        }
                    }
                    else
                    {
                        // Verify
                        useNextUri = false;
                        ProgressIndeterminate = true;
                        Status = M3L.GetString(M3L.string_verifyingDownload);
                        try
                        {
                            // Todo: If verification fails, should we let user try to continue anyways?

                            ProgressMaximum = 100;
                            void onProgress(ProgressInfo pi)
                            {
                                ProgressValue = (long)pi.Value;
                                ProgressIndeterminate = false;
                            }
                            var hash = MUtilities.CalculateHash(DownloadedStream, progressDelegate: onProgress);
                            var matchingHashedFiles = NexusModsUtilities.MD5Search(ProtocolLink.Domain, hash);
                            if (matchingHashedFiles.All(x => x.Mod.ModID != ProtocolLink.ModId))
                            {
                                // Our hash does not match
                                M3Log.Error(@"Download failed: File does not appear to match file on NexusMods");
                                InternalOnModDownloadError(
                                    M3L.GetString(M3L.string_fileDidNotVerifyDownloadMayBeCorrupt));
                                M3OpenTelemetry.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                                {
                                    { @"Domain", ProtocolLink?.Domain },
                                    { @"File", ModFile?.Name },
                                    { @"Result", @"Corrupt download" },
                                });
                                DownloadState = EModDownloadState.FAILED;
                                return;
                            }

                            M3Log.Information(
                                @"File verified OK, nexus MD5 search returned correct result (NexusMods: Please make MD5 available as part of download API! This is a ridiculous way of verifying files)");
                        }
                        catch (Exception ex)
                        {
                            M3Log.Warning(
                                $@"An error occurred while attempting to verify the file: {ex.Message}. Skipping verification for this download.");
                        }

                        M3OpenTelemetry.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                        {
                            { @"Domain", ProtocolLink?.Domain },
                            { @"File", ModFile?.Name },
                            { @"Result", @"Success" },
                        });
                    }
                }

                ProgressIndeterminate = false;
                ProgressValue = 1;
                ProgressMaximum = 1;

                if (CancellationController.Token.IsCancellationRequested)
                {
                    // Canceled during verification. The download itself is complete, so keep it if it is in the user's download cache.
                    CleanupDownloadedData(Settings.ModDownloadCacheFolder == null);
                    MarkCanceled();
                }
                else
                {
                    Status = M3L.GetString(M3L.string_downloadComplete);
                    DownloadState = EModDownloadState.DOWNLOADCOMPLETE;
                    InternalOnModDownloaded(new DataEventArgs(DownloadedStream));
                }
            });
        }

        public override string CreateDownloadKey()
        {
            return $@"{ProtocolLink.Domain}-{ProtocolLink.FileId}";
        }

        /// <summary>
        /// Determines if the file listing has any moddesc.ini files in it.
        /// </summary>
        /// <param name="fileListing"></param>
        /// <returns></returns>
        private bool HasModdescIni(ContentPreview fileListing)
        {
            foreach (var e in fileListing.Children)
            {
                if (HasModdescIniRecursive(e))
                    return true;
            }

            return false;
        }

        private bool HasModdescIniRecursive(ContentPreviewEntry entry)
        {
            // Directory
            if (entry.Type == ContentPreviewEntryType.Directory)
            {
                foreach (var e in entry.Children)
                {
                    if (HasModdescIniRecursive(e))
                        return true;
                }

                return false;
            }

            // File
            return entry.Name == @"moddesc.ini";
        }
    }
}
