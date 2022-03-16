using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Service for showing localized tips when no mod is selected in the right panel of Mod Manager's interface
    /// </summary>
    public class TipsService
    {
        /// <summary>
        /// Maps a language code to a list of tips in that language
        /// </summary>
        private static Dictionary<string, List<string>> Database;

        /// <summary>
        /// If the database has been initialized by a data source (cached or online)
        /// </summary>
        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The amount of loaded tips (used for unit testing)
        /// </summary>
        public static int TipCount => Database.Sum(x => x.Value.Count);

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"Tips Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetTipsServiceCachedFile();

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
                    Database = serviceData.ToObject<Dictionary<string, List<string>>>();
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
                    Database = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(cached);
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
        /// Fetches a random localized tip
        /// </summary>
        /// <returns></returns>
        public static string GetTip(string language)
        {
            if (!ServiceLoaded || Database == null) return null;
            if (Database.TryGetValue(language, out var tips))
            {
                return tips.RandomElement();
            }
            return null;
        }
    }
}
