using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Pathoschild.FluentNexus.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Helpers;
using Serilog;
using MassEffectModManagerCore.modmanager.localizations;
using LegendaryExplorerCore.Misc;
using Microsoft.AppCenter.Analytics;
using System.Linq;
using MemoryAnalyzer = MassEffectModManagerCore.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Class for information about a mod that is being downloaded
    /// </summary>
    public class ModDownload : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string NXMLink { get; set; }
        public List<ModFileDownloadLink> DownloadLinks { get; } = new List<ModFileDownloadLink>();
        public ModFile ModFile { get; private set; }
        private string domain;
        /// <summary>
        /// If this mod has been initialized
        /// </summary>
        public bool Initialized { get; private set; }
        public long ProgressValue { get; private set; }
        public long ProgressMaximum { get; private set; }
        public bool ProgressIndeterminate { get; private set; } = true;
        public string DownloadStatus { get; private set; }
        /// <summary>
        /// If this mod has been downloaded
        /// </summary>
        public bool Downloaded { get; set; }
        /// <summary>
        /// The downloaded stream data
        /// </summary>
        public Stream DownloadedStream { get; private set; }

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

        public ModDownload(string nxmlink)
        {
            NXMLink = nxmlink;
        }

        public void StartDownload(CancellationToken cancellationToken)
        {
            Task.Run(() =>
            {
                if (ProgressMaximum < 100 * FileSize.MebiByte)
                {
                    DownloadedStream = new MemoryStream();
                    MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download MemoryStream", new WeakReference(DownloadedStream));
                }
                else
                {
                    DownloadedStream = new FileStream(Path.Combine(Utilities.GetModDownloadCacheDirectory(), ModFile.FileName), FileMode.Create);
                    MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download FileStream", new WeakReference(DownloadedStream));
                }

                var downloadUri = DownloadLinks[0].Uri;

                var downloadResult = OnlineContent.DownloadToStream(downloadUri.ToString(), OnDownloadProgress, null, true, DownloadedStream, cancellationToken);
                if (downloadResult.errorMessage != null)
                {
                    DownloadedStream?.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Aborted download.
                    }
                    else
                    {
                        Log.Error($@"Download failed: {downloadResult.errorMessage}");
                        OnModDownloadError?.Invoke(this, downloadResult.errorMessage);
                    }
                    // Download didn't work!
                    Analytics.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", domain},
                        {@"File", ModFile?.Name},
                        {@"Result", $@"Failed, {downloadResult.errorMessage}"},
                    });
                }
                else
                {
                    Analytics.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", domain},
                        {@"File", ModFile?.Name},
                        {@"Result", @"Success"},
                    });
                }
                Downloaded = true;
                OnModDownloaded?.Invoke(this, new DataEventArgs(DownloadedStream));
            });
        }

        private void OnDownloadProgress(long done, long total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            ProgressIndeterminate = false;
            DownloadStatus = $@"{FileSize.FormatSize(ProgressValue)}/{FileSize.FormatSize(ProgressMaximum)}";
        }

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin.
        /// THIS IS A BLOCKING CALL DO NOT RUN ON THE UI
        /// </summary>
        public void Initialize()
        {
            Log.Information($@"Initializing {NXMLink}");
            Task.Run(() =>
            {
                try
                {
                    DownloadLinks.Clear();

                    var nxmlink = NXMLink.Substring(6);
                    var queryPos = nxmlink.IndexOf('?');

                    var info = queryPos > 0 ? nxmlink.Substring(0, queryPos) : nxmlink;
                    var infos = info.Split('/');
                    domain = infos[0];
                    var modid = int.Parse(infos[2]);
                    var fileid = int.Parse(infos[4]);

                    if (!NexusModsUtilities.AllSupportedNexusDomains.Contains(domain))
                    {
                        Log.Error($@"Cannot download file from unsupported domain: {domain}. Open your preferred mod manager from that game first");
                        Initialized = true;
                        ProgressIndeterminate = false;
                        OnModDownloadError?.Invoke(this,
                            M3L.GetString(M3L.string_interp_dialog_modNotForThisModManager, domain));
                        return;
                    }

                    ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(domain, modid, fileid).Result;
                    if (ModFile != null)
                    {
                        if (ModFile.Category != FileCategory.Deleted)
                        {
                            if (queryPos > 0)
                            {
                                // download with manager
                                string querystring = nxmlink.Substring(queryPos);
                                var parameters = HttpUtility.ParseQueryString(querystring);

                                // Check if parameters are correct!
                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(domain, modid, fileid,
                                    parameters[@"key"], int.Parse(parameters[@"expires"])).Result);
                            }
                            else
                            {
                                // premium?
                                if (!NexusModsUtilities.UserInfo.IsPremium)
                                {
                                    Log.Error(
                                        $@"Cannot download {ModFile.FileName}: User is not premium, but this link is not generated from NexusMods");
                                    Initialized = true;
                                    ProgressIndeterminate = false;
                                    OnModDownloadError?.Invoke(this,
                                        M3L.GetString(M3L.string_dialog_mustBePremiumUserToDownload));
                                    return;
                                }

                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(domain, modid, fileid)
                                    ?.Result);
                            }

                            ProgressMaximum = ModFile.Size * 1024; // Bytes
                            Initialized = true;
                            Log.Error($@"ModDownload has initialized: {ModFile.FileName}");
                            OnInitialized?.Invoke(this, null);
                        }
                        else
                        {
                            Log.Error($@"Cannot download {ModFile.FileName}: File deleted from NexusMods");
                            Initialized = true;
                            ProgressIndeterminate = false;
                            OnModDownloadError?.Invoke(this,
                                M3L.GetString(M3L.string_dialog_cannotDownloadDeletedFile));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"Error downloading {ModFile?.FileName}: {e.Message}");
                    Initialized = true;
                    ProgressIndeterminate = false;
                    OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_interp_errorDownloadingModX, e.Message));
                }
            });
        }
    }
}
