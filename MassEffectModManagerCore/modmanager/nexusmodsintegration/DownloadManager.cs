using System.Collections.Concurrent;
using System.Diagnostics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
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
        /// Todo: Move to settings
        /// </summary>
        private static int MaxConcurrentTasks = 2;

        /// <summary>
        /// The list of downloads. They may not all be actively downloading, but in a queued state.
        /// </summary>
        private static ConcurrentDictionary<string, ModDownload> Downloads = new();

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
        public static void AddNXMDownload(string nxmLink)
        {
            M3Log.Information($@"Queueing nxmlink for download: {nxmLink}");
            var dl = new NexusModDownload(nxmLink);

            if (Downloads.ContainsKey(dl.CreateDownloadKey()))
            {
                M3Log.Information($@"Rejecting nxm download: Already being handled by the download manager.");
                return;
            }

            // Attach listeners for when object changes states so the manager can handle it
            dl.DownloadStateChanged += DownloadStateChanged;

            AddDownload(dl);

            // Initialize the metadata for the NexusMod object. 
            dl.Initialize();
        }

        /// <summary>
        /// Adds a download object to the manager and notifies listeners.
        /// </summary>
        /// <param name="dl">Download to add to the manager</param>
        private static void AddDownload(ModDownload dl)
        {
            if (Downloads.TryAdd(dl.CreateDownloadKey(), dl))
            {
                // Download was added.
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
                M3Log.Information($@"ModDownload '{item.FileName}' state changed to {item.DownloadState}");
                if (item.DownloadState == EModDownloadState.QUEUED)
                {
                    // Download has initialized and is now queued for download.
                    // Notify listeners that we have metadata available about the download now available in the event they
                    // need to use it before we move on
                    OnDownloadMetadataLoaded?.Invoke(item, EventArgs.Empty);
                }

                // Attempt to start download, as states have changed.
                TryStartDownload();

                if (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE)
                {
                    // Signal download has completed.
                    OnDownloadCompleted(item, EventArgs.Empty);
                }

                if (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE && item.AutoImport)
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
                    mai.ProgressChanged += ImportProgressChanged;
                    mai.BeginScan();
                }
            }
        }

        private static void ImportProgressChanged(object sender, M3ProgressEventArgs e)
        {
            if (sender is ModArchiveImport mai)
            {
                // Poor performance for how much progress will be called. We should probably use a lookup or simply cache the variable.
                var md = Downloads.FirstOrDefault(x => x.Value.ImportFlow == mai).Value;
                if (md != null)
                {
                    Debug.WriteLine($@"ImportProgress: {e.AmountCompleted}/{e.TotalAmount} IsIndeterminate: {e.IsIndeterminate}");
                    md.ProgressMaximum = e.TotalAmount;
                    md.ProgressValue = e.AmountCompleted;
                    md.ProgressIndeterminate = e.IsIndeterminate;
                }
            }
        }

        /// <summary>
        /// Invoked when the importing state has changed for a download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnImportStateChange(object sender, EventArgs e)
        {
            if (sender is ModArchiveImport mai)
            {
                var matchingObj = Downloads.Values.FirstOrDefault(x => x.ImportFlow == mai);
                if (matchingObj == null)
                    return;

                switch (mai.CurrentState)
                {
                    case EModArchiveImportState.FAILED:
                        {
                            OnDownloadImportFailed?.Invoke(matchingObj, EventArgs.Empty);
                            matchingObj.DownloadState = EModDownloadState.FAILED;
                            matchingObj.Status = M3L.GetString(M3L.string_importFailed);
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
                            matchingObj.Status = M3L.GetString(M3L.string_importQueued);
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
            }
        }

        private static void TryStartDownload()
        {
            if (Downloads.Count == 0 || Downloads.All(x => x.Value.DownloadState != EModDownloadState.QUEUED))
                return; // Nothing to do

            // Enforce cap on number of downloads we can concurrently run.
            var currentDownloadCount = Downloads.Count(x => x.Value.DownloadState == EModDownloadState.DOWNLOADING);

            foreach (var dl in Downloads.Where(x => x.Value.DownloadState == EModDownloadState.QUEUED))
            {
                if (currentDownloadCount > MaxConcurrentTasks)
                {
                    // Cannot exceed download count.
                    return;
                }

                dl.Value.StartDownload(); // force download to disk is not defined here, may need to put that onto download object itself...
                currentDownloadCount++;
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
        }

        /// <summary>
        /// Removes downloads that will not complete.
        /// </summary>
        public static void ClearAbortedDownloads()
        {
            var numRemoved = Downloads.RemoveAll(x => x.Value.DownloadState is EModDownloadState.DOWNLOADCANCELED or EModDownloadState.FAILED);
            if (numRemoved > 0)
            {
                OnDownloadRemoved?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes a download from the manager and notifies listeners
        /// </summary>
        /// <param name="downloadedMod"></param>
        public static void RemoveDownload(ModDownload downloadedMod)
        {
            if (Downloads.TryRemove(downloadedMod.CreateDownloadKey(), out _))
            {
                OnDownloadRemoved?.Invoke(downloadedMod, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Constructor for the background task for downloads - not handled by manager though, to separate UI logic from the manager.
        /// </summary>
        /// <returns></returns>
        public static BackgroundTask GenerateBackgroundTask()
        {
            return BackgroundTaskEngine.SubmitBackgroundJob(@"DownloadManager", "Mods are downloading", "Mod downloads complete");
        }
    }
}
