using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Pathoschild.FluentNexus.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Helpers;

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

        public ModDownload(string nxmlink)
        {
            NXMLink = nxmlink;
        }

        public void StartDownload()
        {
            Task.Run(() =>
            {
                if (ProgressMaximum < 100 * FileSize.MebiByte)
                {
                    DownloadedStream = new MemoryStream();
                }
                else
                {
                    DownloadedStream = new FileStream(Path.Combine(Utilities.GetModDownloadCacheDirectory(), ModFile.FileName), FileMode.Create);
                }

                OnlineContent.DownloadToStream(DownloadLinks[0].Uri.ToString(), OnDownloadProgress, null, true, DownloadedStream);
                Downloaded = true;
                OnModDownloaded?.Invoke(this, new DataEventArgs(DownloadedStream));
            });
        }

        private void OnDownloadProgress(long done, long total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            ProgressIndeterminate = false;
            DownloadStatus = $"{FileSize.FormatSize(ProgressValue)}/{FileSize.FormatSize(ProgressMaximum)}";
        }

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin.
        /// THIS IS A BLOCKING CALL DO NOT RUN ON THE UI
        /// </summary>
        public void Initialize()
        {
            Task.Run(() =>
            {
                DownloadLinks.Clear();

                var nxmlink = NXMLink.Substring(6);
                var queryPos = NXMLink.IndexOf('?');

                var info = queryPos > 0 ? nxmlink.Substring(0, queryPos) : nxmlink;
                var infos = info.Split('/');
                var domain = infos[0];
                var modid = int.Parse(infos[2]);
                var fileid = int.Parse(infos[4]);

                if (queryPos > 0)
                {
                    // download with manager


                    string querystring = nxmlink.Substring(queryPos);
                    var parameters = HttpUtility.ParseQueryString(querystring);

                    // Check if parameters are correct!
                    DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(domain, modid, fileid, parameters["key"], int.Parse(parameters["expires"])).Result);

                }
                else
                {
                    // premium?
                    DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(domain, modid, fileid)?.Result);
                }

                ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(domain, modid, fileid).Result;
                ProgressMaximum = ModFile.Size * 1024; // Bytes
                Initialized = true;
                OnInitialized?.Invoke(this, null);
            });
        }

    }
}
