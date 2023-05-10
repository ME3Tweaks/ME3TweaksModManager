using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    /// <summary>
    /// Class for handling m3:// links
    /// </summary>
    public class M3ProtocolHandler
    {
    }

    public class M3Link
    {
        public const string COMMAND_LOG = @"GenerateLog";
        public const string DOWNLOAD_MOD = @"DownloadMod";


        /// <summary>
        /// Version of link feature
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// The command to issue
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Data for the command to execute
        /// </summary>
        public string Data { get; set; }
    }
}
