using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Pathoschild.FluentNexus.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using ME3ExplorerCore.Helpers;
using System;

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
        /// Invoked when the mod has initialized
        /// </summary>
        public event EventHandler<EventArgs> OnInitialized;

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin
        /// </summary>
        public void Initialize()
        {
            DownloadLinks.Clear();

            var nxmlink = NXMLink.Substring(6);
            var queryPos = NXMLink.IndexOf('?');

            var info = queryPos > 0 ? nxmlink.Substring(0, queryPos) : nxmlink;
            var infos = info.Split('/');

            if (queryPos > 0)
            {
                // download with manager


                string querystring = nxmlink.Substring(queryPos);
                var parameters = HttpUtility.ParseQueryString(querystring);


                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(infos[0], int.Parse(infos[2]), int.Parse(infos[4]), parameters["key"], int.Parse(parameters["expires"])).Result);

            }
            else
            {
                // premium?
                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(infos[0], int.Parse(infos[2]), int.Parse(infos[4]))?.Result);
            }

            OnInitialized?.Invoke(this, null);
        }
    }
}
