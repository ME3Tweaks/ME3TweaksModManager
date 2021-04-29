using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Pathoschild.FluentNexus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Class for information about a mod that is being downloaded
    /// </summary>
    public class ModDownload
    {
        public async static Task<ModFileDownloadLink[]> FromNXMLink(string nxmlink)
        {
            nxmlink = nxmlink.Substring(6);
            var queryPos = nxmlink.IndexOf('?');

            var info = nxmlink.Substring(0, queryPos);
            string querystring = nxmlink.Substring(queryPos);
            var parameters = HttpUtility.ParseQueryString(querystring);

            var infos = info.Split('/');

            return await NexusModsUtilities.GetDownloadLinkForFile(infos[0], int.Parse(infos[2]), int.Parse(infos[4]), parameters["key"], int.Parse(parameters["expires"]));
        }
    }
}
