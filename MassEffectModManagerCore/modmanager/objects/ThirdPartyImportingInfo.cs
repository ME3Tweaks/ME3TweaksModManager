using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;

namespace ME3TweaksModManager.modmanager.objects
{
    public class ThirdPartyImportingInfo
    {
        public string md5 { get; set; }
        public string inarchivepathtosearch { get; set; }
        public string filename { get; set; }
        public string subdirectorydepth { get; set; }
        public string servermoddescname { get; set; }
        public MEGame game { get; set; }
        public string version { get; set; }
        public string requireddlc { get; set; }
        public string zippedexepath { get; set; }
        public string exetransform { get; set; }

        public List<string> GetParsedRequiredDLC()
        {
            if (!string.IsNullOrWhiteSpace(requireddlc))
            {
                return requireddlc.Split(';').ToList();
            }
            else
            {
                return new List<string>();
            }
        }
    }
}