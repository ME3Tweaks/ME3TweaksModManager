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
using Newtonsoft.Json.Linq;
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

        /// <summary>
        /// If the database has been initialized by a data source (cached or online)
        /// </summary>
        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"Blacklisting Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetBlacklistingsCachedFile();


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

        public static bool LoadService(JToken data)
        {
            return InternalLoadService(data);
        }

        private static bool InternalLoadService(JToken serviceData)
        {
            // Online first
            if (serviceData != null)
            {
                try
                {
                    Database = serviceData.ToObject<List<Blacklisting>>();
                    ServiceLoaded = true;
#if DEBUG
                    File.WriteAllText(GetServiceCacheFile(), serviceData.ToString(Formatting.Indented));
#else
                    File.WriteAllText(GetServiceCacheFile(), serviceData.ToString(Formatting.None));
#endif
                    M3Log.Information($@"Loaded online {ServiceLoggingName}");
                    return true;
                }
                catch (Exception ex)
                {
                    if (ServiceLoaded)
                    {
                        M3Log.Error($@"Loaded online {ServiceLoggingName}, but failed to cache to disk: {ex.Message}");
                        return true;
                    }
                    else
                    {
                        M3Log.Error($@"Failed to load {ServiceLoggingName}: {ex.Message}");
                        return false;
                    }

                }
            }

            // Use cached if online is not available
            if (File.Exists(GetServiceCacheFile()))
            {
                try
                {
                    var cached = File.ReadAllText(GetServiceCacheFile());
                    Database = JsonConvert.DeserializeObject<List<Blacklisting>>(cached);
                    ServiceLoaded = true;
                    M3Log.Information($@"Loaded cached {ServiceLoggingName}");
                    return true;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Failed to load cached {ServiceLoggingName}: {e.Message}");
                    var relevantInfo = new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content"},
                        {@"Service", ServiceLoggingName},
                        {@"Message", e.Message}
                    };
                    TelemetryInterposer.UploadErrorLog(e, relevantInfo);
                }
            }

            M3Log.Information($@"Unable to load {ServiceLoggingName}: No cached content or online content was available to load");
            return false;
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
