using System.Web;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// C# representation of an nxm:// link.
    /// </summary>
    public class NexusProtocolLink : NexusFileInfo
    {
        public static NexusProtocolLink Parse(string link)
        {
            if (!link.StartsWith(@"nxm://")) return null; // not an nxm link!

            var npl = new NexusProtocolLink() {Link = link};
            var nxmlink = link.Substring(6); // remove 'nxm://'
            var queryPos = nxmlink.IndexOf('?');

            var info = queryPos > 0 ? nxmlink.Substring(0, queryPos) : nxmlink;
            var infos = info.Split('/');
            npl.Domain = infos[0];
            npl.ModId = int.Parse(infos[2]);
            npl.FileId = int.Parse(infos[4]);
            if (queryPos > 0)
            {
                var parameters = HttpUtility.ParseQueryString(nxmlink.Substring(queryPos ));
                if (int.TryParse(parameters[@"expires"], out var exp))
                {
                    npl.KeyExpiry = exp;
                    npl.Key = parameters[@"key"];
                }
            }

            return npl;
        }

        /// <summary>
        /// The original NXM link
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// When the key expires
        /// </summary>
        public int KeyExpiry { get; set; }

        /// <summary>
        /// The download key, required for download
        /// </summary>
        public string Key { get; set; }
    }
}
