#if DEBUG
using System.Collections.Concurrent;
using System.Diagnostics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.nexusmodsintegration
{
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
    /// Manager class for NexusMod downloads
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

        private static void ModDownloaded(object sender, DataEventArgs e)
        {
            if (sender is ModDownload md)
            {
                md.OnModDownloaded -= ModDownloaded;
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    if (cancellationTokenSource.IsCancellationRequested)
                //    {
                //        // Canceled
                //        OnClosing(DataEventArgs.Empty);
                //    }
                //    else
                //    {
                //        OnClosing(new DataEventArgs(new List<ModDownload>(new[] { md }))); //maybe someday i'll support download queue or something.
                //    }
                //});
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

                if (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE && item.AutoImport)
                {
                    ModArchiveImport mai = new ModArchiveImport()
                    {
                        AutomatedMode = true,
                        ArchiveStream = item.DownloadedStream,
#if !DEBUG
            YOU DIDN'T FIX THIS!!
#endif
                        GetPanelResult = () => new PanelResult(), // TEMPORARY, DO NOT RELY ON THIS
                        ArchiveFilePath = @"Placeholder.7z",
                    };
                    if (item is NexusModDownload nmd)
                    {
                        mai.SourceNXMLink = nmd.ProtocolLink;
                        mai.ArchiveFilePath = nmd.ModFile.FileName;
                        mai.UpdateModObject = nmd;
                    }
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
                            matchingObj.DownloadState = EModDownloadState.FAILED;
                            matchingObj.Status = M3L.GetString(M3L.string_importFailed);
                        }
                        break;
                    case EModArchiveImportState.SCANNING:
                        {
                            matchingObj.DownloadState = EModDownloadState.IMPORTING;
                            matchingObj.Status = M3L.GetString(M3L.string_scanning);
                        }
                        break;
                    case EModArchiveImportState.SCANCOMPLETED:
                        {
                            matchingObj.DownloadState = EModDownloadState.IMPORTING;
                            matchingObj.Status = M3L.GetString(M3L.string_importQueued);
                        }
                        break;
                    case EModArchiveImportState.IMPORTING:
                        {
                            matchingObj.DownloadState = EModDownloadState.IMPORTING;
                            matchingObj.Status = M3L.GetString(M3L.string_importingMods);
                        }
                        break;
                    case EModArchiveImportState.COMPLETE:
                        {
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
            md.OnModDownloaded -= ModDownloaded;
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
    }
}
#endif
