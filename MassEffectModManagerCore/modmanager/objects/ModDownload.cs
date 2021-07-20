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
using PropertyChanged;
using MemoryAnalyzer = MassEffectModManagerCore.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Class for information about a mod that is being downloaded
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class ModDownload
    {
        public string NXMLink { get; set; }
        public List<ModFileDownloadLink> DownloadLinks { get; } = new List<ModFileDownloadLink>();
        public ModFile ModFile { get; private set; }
        public NexusProtocolLink ProtocolLink { get; private set; }
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
                        {@"Domain", ProtocolLink?.Domain},
                        {@"File", ModFile?.Name},
                        {@"Result", $@"Failed, {downloadResult.errorMessage}"},
                    });
                }
                else
                {
                    Analytics.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", ProtocolLink?.Domain},
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

                    ProtocolLink = NexusProtocolLink.Parse(NXMLink);
                    if (ProtocolLink == null) return; // Parse failed.

                    if (!NexusModsUtilities.AllSupportedNexusDomains.Contains(ProtocolLink?.Domain))
                    {
                        Log.Error($@"Cannot download file from unsupported domain: {ProtocolLink?.Domain}. Open your preferred mod manager from that game first");
                        Initialized = true;
                        ProgressIndeterminate = false;
                        OnModDownloadError?.Invoke(this,
                            M3L.GetString(M3L.string_interp_dialog_modNotForThisModManager, ProtocolLink.Domain));
                        return;
                    }

                    ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(ProtocolLink.Domain, ProtocolLink.ModId, ProtocolLink.FileId).Result;
                    if (ModFile != null)
                    {
                        if (ModFile.Category != FileCategory.Deleted)
                        {
                            if (ProtocolLink.Key != null)
                            {
                                // Website click
                                if (ProtocolLink.Domain is @"masseffect" or @"masseffect2" && !IsDownloadWhitelisted(ProtocolLink.Domain, ModFile))
                                {
                                    // Check to see file has moddesc.ini the listing
                                    var fileListing = NexusModsUtilities.GetFileListing(ModFile);
                                    if (fileListing == null || !HasModdescIni(fileListing))
                                    {
                                        Log.Error($@"This file is not whitelisted for download and does not contain a moddesc.ini file, this is not a mod manager mod: {ModFile.FileName}");
                                        Initialized = true;
                                        ProgressIndeterminate = false;
                                        OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_interp_nexusModNotCompatible, ModFile.Name));
                                        return;
                                    }
                                }


                                // download with manager was clicked.

                                // Check if parameters are correct!
                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink).Result);
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

                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink)
                                    ?.Result);
                            }

                            ProgressMaximum = ModFile.Size * 1024; // Bytes
                            Initialized = true;
                            Log.Information($@"ModDownload has initialized: {ModFile.FileName}");
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

        private static readonly int[] WhitelistedME1FileIDs = new[]
        {
            116, // skip intro movies
            117, // skip to main menu
            120, // controller skip intro movies
            121, // controll skip to main menu
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
            foreach (var e in entry.Children.Where(x => x.Type == ContentPreviewEntryType.Directory))
            {
                return HasModdescIniRecursive(e);
            }

            return entry.Name == @"moddesc.ini";
        }

    }
}
