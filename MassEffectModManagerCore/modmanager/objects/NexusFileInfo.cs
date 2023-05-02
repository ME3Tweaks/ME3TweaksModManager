using Pathoschild.FluentNexus.Models;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Object that contains various pieces of NexusMods information about a mod, for passing around
    /// </summary>
    public class NexusFileInfo
    {
        /// <summary>
        /// ID of the file
        /// </summary>
        public int FileId { get; set; }

        /// <summary>
        /// The ID of the mod under the domain
        /// </summary>
        public int ModId { get; set; }

        /// <summary>
        /// The domain this file is for
        /// </summary>
        public string Domain { get; set; }

        public string ToNexusDownloadPageLink()
        {
            //https://www.nexusmods.com/masseffectlegendaryedition/mods/13?&file_id=6528
            // Add nmm=1 to trigger nxm:// when user clicks
            return $@"https://nexusmods.com/{Domain}/mods/{ModId}?tab=files&file_id={FileId}";
        }

        /// <summary>
        /// Parses the NexusMods ModId from the given modsite descriptor (link to main mod page). FileId is not populated.
        /// </summary>
        /// <param name="game">Game this link is for</param>
        /// <param name="link">The original modsite link</param>
        /// <returns></returns>
        public static NexusFileInfo FromModSite(MEGame game, string link, string preknownDomain = null)
        {
            NexusFileInfo info = new NexusFileInfo();
            if (preknownDomain == null)
            {
                if (game == MEGame.ME1) info.Domain = @"masseffect";
                if (game == MEGame.ME2) info.Domain = @"masseffect2";
                if (game == MEGame.ME3) info.Domain = @"masseffect3";
                if (game == MEGame.LELauncher || game.IsLEGame()) info.Domain = @"masseffectlegendaryedition";
            }
            else
            {
                info.Domain = preknownDomain;
            }

            try
            {
                //try to extract nexus mods ID
                var nexusIndex = link.IndexOf(@"nexusmods.com/", StringComparison.InvariantCultureIgnoreCase);
                if (nexusIndex > 0)
                {
                    string nexusId = link.Substring(nexusIndex + @"nexusmods.com/".Length); // http:/

                    if (preknownDomain != null)
                    {
                        // Cut out the domain
                        nexusId = nexusId.Substring(preknownDomain.Length);
                    }
                    else
                    {
                        // Cut out the domain
                        nexusId = nexusId.Substring(@"masseffect".Length);
                        if (game == MEGame.ME2 || game == MEGame.ME3)
                        {
                            nexusId = nexusId.Substring(1); //number
                        }
                        else if (game.IsLEGame())
                        {
                            nexusId = nexusId.Substring(16); // legendaryedition
                        }
                    }

                    nexusId = nexusId.Substring(6).TrimEnd('/'); // /mods/ and any / after number in the event url has that in it.

                    int questionMark = nexusId.IndexOf(@"?", StringComparison.InvariantCultureIgnoreCase);
                    if (questionMark > 0)
                    {
                        nexusId = nexusId.Substring(0, questionMark);
                    }

                    if (int.TryParse(nexusId, out var nid))
                    {
                        info.ModId = nid;
                    }

                    return info;
                }
            }
            catch (Exception)
            {
                //don't bother.
            }

            return null;
        }

        /// <summary>
        /// Parses out the MASS EFFECT domain from a NexusMods URL. Does not work for other games.
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        private static string GetDomainFromLink(string link)
        {
            if (link.Contains(@"nexusmods.com/masseffectlegendaryedition")) return @"masseffectlegendaryedition";
            if (link.Contains(@"nexusmods.com/masseffect3")) return @"masseffect3";
            if (link.Contains(@"nexusmods.com/masseffect2")) return @"masseffect2";
            if (link.Contains(@"nexusmods.com/masseffect")) return @"masseffect";
            return null; // Unknown ME domain
        }

        public static NexusFileInfo FromNexusDownloadPageLink(string link)
        {
            // https://www.nexusmods.com/masseffectlegendaryedition/mods/13?tab=files&file_id=6528
            var domain = GetDomainFromLink(link);
            var nfi = FromModSite(MEGame.Unknown, link, domain);
            var parameters = HttpUtility.ParseQueryString(link);


            return null; // Todo
        }
    }
}
