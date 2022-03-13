using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using Newtonsoft.Json;
using ShortTimeoutWebClient = ME3TweaksCore.Misc.ShortTimeoutWebClient;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Defines a blacklisted file for importing
    /// </summary>
    public class Blacklisting
    {
        /// <summary>
        /// The size of the archive
        /// </summary>
        [JsonProperty(@"size")]
        public long FileSize { get; set; }

        /// <summary>
        /// The MD5 of the archive used to verify the incoming file is blacklisted
        /// </summary>
        [JsonProperty(@"hash")]
        public string MD5 { get; set; }

        /// <summary>
        /// NexusMods domain this file is for (NXM protocol)
        /// </summary>
        [JsonProperty(@"domain")]
        public string Domain { get; set; }

        /// <summary>
        /// NexusMods File ID
        /// </summary>
        [JsonProperty(@"fileid")]
        public int FileID { get; set; }
    }

    /// <summary>
    /// Service for preventing mod archives from being able to be imported.
    /// ONLY USED FOR MODS THAT BREAK GAMES OR DO NOT WORK AND HAVE BEEN ABANDONED
    /// </summary>
    public class BlacklistingService
    {
        /// <summary>
        /// Maps a archive size to a list of importing informations about archives of that size.
        /// </summary>
        private static List<Blacklisting> Database;
        public static bool ServiceLoaded { get; set; }

        private const string BlackingServiceURL = @"https://me3tweaks.com/modmanager/services/blacklistingservice";

        /// <summary>
        /// Fetches blacklisting that match the specified archive size.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for blacklisting</param>
        /// <returns>List of potential blacklistings</returns>
        public static List<Blacklisting> GetBlacklistings(long archiveSize)
        {
            if (Database == null) return new List<Blacklisting>(); //Not loaded
            return Database.Where(x => x.FileSize == archiveSize).ToList();
        }

        public static bool LoadService(bool forceRefresh = false)
        {
            Database = FetchBlacklistingManifest(forceRefresh);
            ServiceLoaded = true;
            return true;
        }

        private static List<Blacklisting> FetchBlacklistingManifest(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(M3Filesystem.GetBlacklistingsCachedFile()))
            {
                try
                {
                    cached = File.ReadAllText(M3Filesystem.GetBlacklistingsCachedFile());
                }
                catch (Exception e)
                {
                    var relevantInfo = new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content"},
                        {@"Service", @"Blacklisting Service"},
                        {@"Message", e.Message}
                    };
                    TelemetryInterposer.UploadErrorLog(e, relevantInfo);
                }
            }

            if (!File.Exists(M3Filesystem.GetBlacklistingsCachedFile()) || overrideThrottling || MOnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    string json = MExtensions.DownloadStringAwareOfEncoding(wc, BlackingServiceURL);
                    File.WriteAllText(M3Filesystem.GetBlacklistingsCachedFile(), json);
                    return JsonConvert.DeserializeObject<List<Blacklisting>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    M3Log.Error(@"Error fetching latest blacklisting service file: " + e.Message);

                    if (cached != null)
                    {
                        M3Log.Warning(@"Using cached blacklisting service file instead");
                    }
                    else
                    {
                        M3Log.Error(@"Unable to fetch latest blacklisting service file from server and local file doesn't exist. Returning a blank copy.");
                        return new List<Blacklisting>();
                    }
                }
            }
            try
            {
                return JsonConvert.DeserializeObject<List<Blacklisting>>(cached);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Unable to parse cached blacklisting service file: " + e.Message);
                return new List<Blacklisting>();
            }
        }

        /// <summary>
        /// Checks if the listed protocol link is blacklisted.
        /// </summary>
        /// <param name="protocolLink">The protocol to check</param>
        /// <returns>True if blacklisted, false otherwise</returns>
        internal static bool IsNXMBlacklisted(NexusProtocolLink protocolLink)
        {
            if (Database == null) return false;
            return Database.Any(x => x.Domain == protocolLink.Domain && x.FileID == protocolLink.FileId);
        }
    }
}
