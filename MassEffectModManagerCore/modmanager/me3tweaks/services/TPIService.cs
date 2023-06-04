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
using Renci.SshNet.Messages;
using Serilog;
using ShortTimeoutWebClient = ME3TweaksCore.Misc.ShortTimeoutWebClient;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Third Party Importing Service, used for importing mods that do not include a moddesc.ini
    /// </summary>
    public class TPIService
    {
        /// <summary>
        /// Maps a archive size to a list of importing informations about archives of that size.
        /// </summary>
        private static Dictionary<long, List<ThirdPartyImportingInfo>> Database;

        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The amount of loaded entries in total (used for testing)
        /// </summary>
        public static int EntryCount => ServiceLoaded ? Database.Sum(x => x.Value.Count) : 0;

        private const string ServiceLoggingName = @"Third Party Importing Service";
        private static string GetServiceCacheFile() => M3Filesystem.GetThirdPartyImportingCachedFile();


        /// <summary>
        /// Looks up importing information for mods through the third party mod importing service. This returns all candidates, the client code must determine which is the appropriate value.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for information</param>
        /// <returns>List of candidates</returns>
        public static List<ThirdPartyImportingInfo> GetImportingInfosBySize(long archiveSize)
        {
            if (Database == null) return new List<ThirdPartyImportingInfo>(0); //Not loaded
            if (Database.TryGetValue(archiveSize, out var result))
            {
                return result;
            }

            return new List<ThirdPartyImportingInfo>();
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
                    Database = serviceData.ToObject<Dictionary<long, List<ThirdPartyImportingInfo>>>();
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
                    Database = JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyImportingInfo>>>(cached);
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
            
            M3Log.Warning($@"Unable to load {ServiceLoggingName} service: No cached content or online content was available to load");
            return false;
        }
    }
}
