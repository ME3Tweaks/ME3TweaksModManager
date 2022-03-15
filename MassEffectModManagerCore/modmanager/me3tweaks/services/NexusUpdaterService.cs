using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
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
    /// Service for allowing NexusMod-hosted mods to update when the developer has opted in.
    /// </summary>
    public class NexusUpdaterService
    {
        /// <summary>
        /// Maps a NexusMods domain to the list of mod ids that are whitelisted for updating
        /// </summary>
        private static Dictionary<string, List<int>> Database;

        /// <summary>
        /// If the database has been initialized by a data source (cached or online)
        /// </summary>
        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"NexusMods Updater Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetNexusModsUpdateServiceCachedFile();


        /// <summary>
        /// Returns the whitelisting status for the specified game and nexus file code.
        /// 
        /// </summary>
        /// <param name="game">Game to check</param>
        /// <param name="nexusCode">The mod id on NexusMods</param>
        /// <returns>True if whitelisted (or service not loaded), false otherwise. The ME3Tweaks Server will enforce update check whitelisting if the service is not loaded</returns>
        public static bool IsNexusCodeWhitelisted(MEGame game, int nexusCode)
        {
            if (!ServiceLoaded || Database == null) return true; //Not loaded - fallback to everything is supported (server will enforce)gi
            // This shouldn't happen but it *might* show up as Unknown if the mod fails to load
            if (!game.IsLEGame() && !game.IsOTGame() && game != MEGame.LELauncher) return false;
            return Database[game.ToString()].Contains(nexusCode);
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
                    Database = serviceData.ToObject<Dictionary<string, List<int>>>();
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
                    Database = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(cached);
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
    }
}
