using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.nexusmodsintegration
{
    /// <summary>
    /// Used to fetch a specific download out of the downloads list.
    /// </summary>
    public class DownloadManagerKey
    {
        protected bool Equals(DownloadManagerKey other)
        {
            return Domain == other.Domain && FileID == other.FileID;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DownloadManagerKey)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Domain, FileID);
        }

        public string Domain { get; set; }
        public int FileID { get; set; }
    }

    /// <summary>
    /// Manager class for mod downloads
    /// </summary>
    public static class DownloadManager
    {
        /// <summary>
        /// Synchronization object for the download queue
        /// </summary>
        private static readonly object _queueSyncObj = new object();

        /// <summary>
        /// Synchronization object for attempting to start a download to make sure something doesn't try itself twice
        /// </summary>
        private static readonly object _downloadStateSyncObj = new object();

        /// <summary>
        /// The list of downloads. They may not all be actively downloading, but in a queued state.
        /// </summary>
        private static readonly ConcurrentDictionary<string, ModDownload> Downloads = new();

        private static readonly ConcurrentDictionary<ModArchiveImport, ModDownload> ImportFlowLookup = new();

        public static ObservableCollectionExtended<ModDownload> FailedDownloads { get; } = new();

        /// <summary>
        /// Get the downloads list
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<string, ModDownload> GetDownloads() => Downloads;

        /// <summary>
        /// Invoked when a mod's metadata has been downloaded for display
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadMetadataLoaded;

        /// <summary>
        /// Invoked when a download has been added to the manager.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadAdded;

        /// <summary>
        /// Invoked when a download has been removed from the manager. The sender may be null if this is just a notification.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadRemoved;

        /// <summary>
        /// Invoked when a download has been completed by the manager (not import!)
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadCompleted;

        /// <summary>
        /// Invoked when a download has begun being scanned for mods.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadScanning;

        /// <summary>
        /// Invoked when a download has been scanned for mods.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadScanCompleted;

        /// <summary>
        /// Invoked when a download is being imported into the library
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadImporting;

        /// <summary>
        /// Invoked when a download has been imported into the library and the download process is fully complete.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadImported;

        /// <summary>
        /// Invoked when a download was attempted to be imported into the library, but failed.
        /// </summary>
        public static event EventHandler<EventArgs> OnDownloadImportFailed;

        /// <summary>
        /// Adds a new download via nxm link
        /// </summary>
        /// <param name="nxmLink"></param>
        /// <param name="customStateChanged"></param>
        public static ModDownload AddNXMDownload(string nxmLink)
        {
            NexusModDownload dl = null;

            // We lock this to ensure we cannot add duplicates
            lock (_queueSyncObj)
            {
                M3Log.Information($@"Queueing nxmlink for download: {nxmLink}");
                dl = new NexusModDownload(nxmLink);
                if (!NexusModsUtilities.UserInfo.IsPremium && dl.ProtocolLink.UserId != NexusModsUtilities.UserInfo.UserID)
                {
                    M3Log.Error($@"The user id for this NXM link is not the same one as the user that is signed into Mod Manager. Cannot generate download links.");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        M3L.ShowDialog(MainWindow.Instance, M3L.GetString(M3L.string_dialog_mismatchingLocalNexusUsernames), M3L.GetString(M3L.string_cannotDownload), MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return null;
                }

                if (Downloads.TryGetValue(dl.CreateDownloadKey(), out var existingDownload)
                    && existingDownload is NexusModDownload { DownloadState: EModDownloadState.WAITINGFORUSER } waitingDownload)
                {
                    // The user started a download we were waiting on from the website. Use the existing download object.
                    waitingDownload.AdoptNXMLink(nxmLink);
                    dl = waitingDownload;
                }
                else if (existingDownload != null)
                {
                    M3Log.Information(@"Rejecting nxm download: Already being handled by the download manager.");
                    // Add event and then invoke event to update the UI, then
                    // remove the event
                    dl.IsDuplicate = true;
                    dl.DownloadStateChanged += DownloadStateChanged;
                    try
                    {
                        dl.DownloadState = EModDownloadState.FAILED; // Will remove download
                    }
                    finally
                    {
                        // If setting state triggers exception we still want this to run
                        dl.DownloadStateChanged -= DownloadStateChanged;
                    }
                    return dl;
                }
                else
                {
                    // Attach listeners for when object changes states so the manager can handle it
                    dl.DownloadStateChanged += DownloadStateChanged;

                    AddDownload(dl);
                }
            }

            // Initialize the metadata for the NexusMod object.
            dl.Initialize();
            return dl;
        }

        /// <summary>
        /// Adds downloads for specific NexusMods files that will be imported into the library automatically once downloaded.
        /// Premium users will begin downloading immediately; other users will have to start each download from the website.
        /// Files that are already in the download manager are skipped.
        /// </summary>
        /// <param name="files">The files to download, and the names to show until file information is available</param>
        /// <returns>The downloads that were added</returns>
        public static List<NexusModDownload> AddAutoImportDownloads(IEnumerable<(NexusFileInfo fileInfo, string displayName)> files)
        {
            var requiresUserToStart = NexusModsUtilities.UserInfo?.IsPremium != true;
            var added = new List<NexusModDownload>();
            lock (_queueSyncObj)
            {
                foreach (var file in files)
                {
                    var dl = NexusModDownload.CreateAutoImportDownload(file.fileInfo, file.displayName, requiresUserToStart);
                    if (Downloads.ContainsKey(dl.CreateDownloadKey()))
                    {
                        M3Log.Information($@"Not adding download for {dl.CreateDownloadKey()}: Already being handled by the download manager");
                        continue;
                    }

                    M3Log.Information($@"Queueing {(requiresUserToStart ? @"user-started" : @"automatic")} download: {dl.NXMLink} ({file.displayName})");
                    dl.DownloadStateChanged += DownloadStateChanged;
                    AddDownload(dl);
                    added.Add(dl);
                }
            }

            foreach (var dl in added)
            {
                if (requiresUserToStart)
                {
                    dl.LoadPendingMetadata();
                }
                else
                {
                    dl.Initialize();
                }
            }

            return added;
        }

        /// <summary>
        /// Cancels all downloads that can currently be canceled.
        /// </summary>
        public static void CancelAllDownloads()
        {
            M3Log.Information(@"Canceling all downloads");
            foreach (var dl in Downloads.Values.OrderByDescending(x => x.QueueOrder).ToList())
            {
                // Newest first so canceling does not start queued downloads that are about to be canceled
                dl.Cancel();
            }
        }

        /// <summary>
        /// If any download can currently be canceled
        /// </summary>
        public static bool CanCancelAnyDownload() => Downloads.Values.Any(x => x.CanCancel);

        /// <summary>
        /// Adds a download object to the manager and notifies listeners.
        /// </summary>
        /// <param name="dl">Download to add to the manager</param>
        private static void AddDownload(ModDownload dl)
        {
            if (Downloads.TryAdd(dl.CreateDownloadKey(), dl))
            {
                // Download was added.
                M3Log.Information($@"Added to DownloadManager: {dl.CreateDownloadKey()}");
                OnDownloadAdded?.Invoke(dl, EventArgs.Empty);
            }
        }


        /// <summary>
        /// Invoked when the state of a mod download has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void DownloadStateChanged(object sender, EventArgs e)
        {
            if (sender is ModDownload item)
            {
                M3Log.Information($@"ModDownload '{item.FileName ?? item.CreateDownloadKey()}' state changed to {item.DownloadState}");
                PrintDownloadManagerToConsole();

                if (item.DownloadState == EModDownloadState.QUEUED)
                {
                    // Download has initialized and is now queued for download.
                    // Notify listeners that we have metadata available about the download now available in the event they
                    // need to use it before we move on
                    OnDownloadMetadataLoaded?.Invoke(item, EventArgs.Empty);
                    item.Status = M3L.GetString(M3L.string_downloadQueued);
                }

                // Attempt to a new start download, as states have changed.
                // Attempt when our state has just changed to one of the following:
                if (item.DownloadState is EModDownloadState.QUEUED
                    or EModDownloadState.FINISHED
                    or EModDownloadState.FAILED
                    or EModDownloadState.DOWNLOADCANCELED
                    || (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE && !item.AutoImport)) // Waiting on user to import, no longer uses a slot
                {
                    TryStartDownload();

                    // refresh bindings
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    });
                }

                if (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE)
                {
                    // Signal download has completed.
                    OnDownloadCompleted?.Invoke(item, EventArgs.Empty);

                    if (item.AutoImport && !item.IsCancelRequested)
                    {
                        // We must obtain a reference to the current active panel, if any.
                        // This is because we must handle the result of import - e.g. reloading mods
                        // when the panel closes. Since auto import doesn't show a UI we must pass the current panel's
                        // result into the archive importer so it can set proper values on it.

                        var filename = item.FileName ?? @"Autoimport.7z"; // 7z is a guess here, it might be wrong. But we should always have a filename...

                        // Mod is set to auto-import
                        ModArchiveImport mai = new ModArchiveImport()
                        {
                            AutomatedMode = true,
                            ArchiveStream = item.DownloadedStream,

                            // We need to figure out how to handle a panelresult from here...
                            GetPanelResult = () =>
                            {
                                if (MainWindow.Instance.GetCurrentPanel() is MMBusyPanelBase mmBusyPanel)
                                {
                                    return mmBusyPanel.Result;
                                }

                                // Hopefully we will not get in a state like this - M3 should block the UI
                                // if a download is importing, always, for UI consistency.

                                // How to handle a panel result when there are no panels showing?
                                return new PanelResult();
                            },
                            ArchiveFilePath = filename,
                        };

                        // Associate NexusMod information
                        if (item is NexusModDownload nmd)
                        {
                            mai.SourceNXMLink = nmd.ProtocolLink;
                            mai.ArchiveFilePath = nmd.ModFile.FileName;
                            mai.UpdateModObject = nmd;
                        }

                        // Subscribe to changes in import status so we can be
                        // notified things are happening. This is what triggers state changes
                        // this object.
                        mai.ImportStateChanged += OnImportStateChange;

                        item.ImportFlow = mai;
                        ImportFlowLookup[mai] = item; // O(1) reverse lookup
                        mai.ProgressChanged += ImportProgressChanged;
                        mai.BeginScan();
                    }
                }

                if (item.DownloadState is EModDownloadState.FAILED
                    or EModDownloadState.FINISHED
                    or EModDownloadState.DOWNLOADCANCELED)
                {
                    // No longer downloading
                    RemoveDownload(item);
                }
            }
        }

        private static void ImportProgressChanged(object sender, M3ProgressEventArgs e)
        {
            if (sender is ModArchiveImport mai && ImportFlowLookup.TryGetValue(mai, out var md))
            {
                md.ProgressMaximum = e.TotalAmount;
                md.ProgressValue = e.AmountCompleted;
                md.ProgressIndeterminate = e.IsIndeterminate;
            }
        }

        /// <summary>
        /// Invoked when the importing state has changed for a download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnImportStateChange(object sender, EventArgs e)
        {
            if (sender is not ModArchiveImport mai)
                return;

            if (!ImportFlowLookup.TryGetValue(mai, out var matchingObj))
                return;

            switch (mai.CurrentState)
            {
                case EModArchiveImportState.FAILED:
                    {
                        OnDownloadImportFailed?.Invoke(matchingObj, EventArgs.Empty);
                        // Status must be set before state as the download is moved to the failed list on state change
                        matchingObj.Status = mai.AutomatedFailureReason ?? M3L.GetString(M3L.string_importFailed);
                        matchingObj.DownloadState = EModDownloadState.FAILED;
                    }
                    break;
                case EModArchiveImportState.CANCELED:
                    {
                        // Import was canceled before anything was extracted
                        OnDownloadImportFailed?.Invoke(matchingObj, EventArgs.Empty);
                        matchingObj.CleanupDownloadedData(Settings.ModDownloadCacheFolder == null);
                        matchingObj.MarkCanceled();
                    }
                    break;
                case EModArchiveImportState.SCANNING:
                    {
                        OnDownloadScanning?.Invoke(matchingObj, EventArgs.Empty);
                        matchingObj.DownloadState = EModDownloadState.IMPORTING;
                        matchingObj.Status = M3L.GetString(M3L.string_scanning);
                    }
                    break;
                case EModArchiveImportState.SCANCOMPLETED:
                    {
                        OnDownloadScanCompleted?.Invoke(matchingObj, EventArgs.Empty);
                        matchingObj.DownloadState = EModDownloadState.IMPORTING;
                        if (!mai.IsCancelRequested)
                        {
                            matchingObj.Status = M3L.GetString(M3L.string_importQueued);
                        }
                    }
                    break;
                case EModArchiveImportState.IMPORTING:
                    {
                        OnDownloadImporting?.Invoke(matchingObj, EventArgs.Empty);
                        matchingObj.DownloadState = EModDownloadState.IMPORTING;
                        matchingObj.Status = M3L.GetString(M3L.string_importingMods);
                    }
                    break;
                case EModArchiveImportState.COMPLETE:
                    {
                        OnDownloadImported?.Invoke(matchingObj, EventArgs.Empty);
                        matchingObj.DownloadState = EModDownloadState.FINISHED;
                        matchingObj.Status = M3L.GetString(M3L.string_importComplete);
                    }
                    break;
            }

            // Import state changes do not always change the download state, which is what normally updates this
            matchingObj.UpdateCanCancel();
        }

        /// <summary>
        /// Attempts to start a new download if any are in queued state
        /// </summary>
        private static void TryStartDownload()
        {
            M3Log.Information("TryStartDownload()");

            lock (_downloadStateSyncObj)
            {
                var queue = Downloads.Where(x => x.Value.DownloadState == EModDownloadState.QUEUED).ToList();
                if (queue.Count == 0)
                {
                    M3Log.Information($"No downloads to start.");
                    return; // Nothing to do
                }

                // Enforce cap on number of downloads / imports we can concurrently run.
                // We also count download complete if it's set to auto import cause its about to conduct import.
                // Download complete without auto import is idle until the user imports it, so it does not count.
                // Importing counts as it uses significant resources.
                var currentDownloadCount = Downloads.Count(x => x.Value.IsDownloadActive
                                                                || x.Value.DownloadState is EModDownloadState.IMPORTING
                                                                || (x.Value.DownloadState is EModDownloadState.DOWNLOADCOMPLETE && x.Value.AutoImport));

                M3Log.Information($"TryStartDownload() - Current active download count: {currentDownloadCount}");
                foreach (var dl in queue)
                {
                    if (currentDownloadCount >= Settings.MaxConcurrentImportOperations)
                    {
                        // Cannot exceed download count.
                        M3Log.Information($"TryStartDownload() - We are at max concurrent import operations limit ({Settings.MaxConcurrentImportOperations}), cannot start a new download");
                        return;
                    }

                    M3Log.Information($@"Starting download for file: {dl.Value.FileName}");
                    dl.Value.StartDownload(); // force download to disk is not defined here, may need to put that onto download object itself...
                    currentDownloadCount++;
                }
            }

            M3Log.Information("TryStartDownload() - Unlock");

        }

        [Conditional(@"DEBUG")]
        private static void PrintDownloadManagerToConsole()
        {
            Debug.WriteLine(@":::DownloadManager:::");
            Debug.WriteLine($@"::{Downloads.Count} current items");
            foreach (var dl in Downloads)
            {
                Debug.WriteLine($":{dl.Value.DownloadState}\t{dl.Value.FileName}"); // do not localize
            }
        }

        private static void DownloadError(object sender, string e)
        {
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    M3L.ShowDialog(window, e, M3L.GetString(M3L.string_downloadError), MessageBoxButton.OK, MessageBoxImage.Error);
            //    OnClosing(DataEventArgs.Empty);
            //});
        }

        /// <summary>
        /// Terminates all downloads.
        /// </summary>
        public static void TerminateManager()
        {
            foreach (var dl in Downloads.Values)
            {
                // This will allow M3 to clean up on exit
                dl.CancelDownload();
                DisassociateDownload(dl);
            }

            Downloads.Clear();
        }

        /// <summary>
        /// Removes all listeners that link manager and the download object
        /// </summary>
        /// <param name="md"></param>
        private static void DisassociateDownload(ModDownload md)
        {
            md.DownloadStateChanged -= DownloadStateChanged;
            md.OnModDownloadError -= DownloadError;

            // Import-flow cleanup
            if (md.ImportFlow != null)
            {
                md.ImportFlow.ImportStateChanged -= OnImportStateChange;
                md.ImportFlow.ProgressChanged -= ImportProgressChanged;
                ImportFlowLookup.TryRemove(md.ImportFlow, out _);
            }
        }

        /// <summary>
        /// Removes downloads that will not complete.
        /// </summary>
        public static void ClearAbortedDownloads()
        {
            var numRemoved = Downloads.RemoveAll(x => x.Value.DownloadState is EModDownloadState.DOWNLOADCANCELED or EModDownloadState.FAILED);
            if (numRemoved > 0)
            {
                M3Log.Information($@"Failed downloads were cleaned up");
                OnDownloadRemoved?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes a download from the manager and notifies listeners
        /// </summary>
        /// <param name="downloadedMod"></param>
        public static void RemoveDownload(ModDownload downloadedMod)
        {
            DisassociateDownload(downloadedMod);

            // If it's a duplicate we don't want to remove the existing key
            // or that will mess up the download queue
            if (!downloadedMod.IsDuplicate)
            {
                var key = downloadedMod.CreateDownloadKey();
                if (Downloads.TryRemove(key, out _))
                {
                    M3Log.Information($@"DownloadManager::Download was removed: {key} {downloadedMod.FileName}");
                    if (downloadedMod.DownloadState == EModDownloadState.FAILED)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FailedDownloads.AddEx(downloadedMod);
                        });
                    }
                    OnDownloadRemoved?.Invoke(downloadedMod, EventArgs.Empty);

                    // A slot may have opened up. This must be done here as downloads that are removed by
                    // manual import will not go through a state change that triggers this.
                    TryStartDownload();
                }
            }
        }

        /// <summary>
        /// Constructor for the background task for downloads - not handled by manager though, to separate UI logic from the manager.
        /// </summary>
        /// <returns></returns>
        public static BackgroundTask GenerateBackgroundTask()
        {
            return BackgroundTaskEngine.SubmitBackgroundJob(@"DownloadManager", M3L.GetString(M3L.string_modsAreDownloading), M3L.GetString(M3L.string_modDownloadsComplete));
        }
    }
}
