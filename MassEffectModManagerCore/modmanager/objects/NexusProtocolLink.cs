using System.Web;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class NexusProtocolLink
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
        /// The original link
        /// </summary>
        public string Link { get; set; }

        public int KeyExpiry { get; set; }

        public string Key { get; set; }

        public int FileId { get; set; }

        public int ModId { get; set; }

        public string Domain { get; set; }
    }
}
