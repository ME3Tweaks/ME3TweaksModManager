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
    /// Used to update moddesc.ini files for mods that are abandoned or are known to have issues with future versions of Mod Manager than when they were developed.<br/>
    /// This service is only applied when importing a mod from archive and not to mods in the library<br/>
    /// This service is only to be used in the following conditions:<br/>
    /// 1. A mod designed for older versions of Mod Manager is not working properly with newer versions and has been abandoned by the developer<br/>
    /// 2. A mod developer requests an update to their moddesc to avoid a full mod redeployment if the only changes are for moddesc.ini - the mod must be updated in this case
    /// </summary>
    public class ModDescUpdaterService
    {
        /// <summary>
        /// Maps a moddesc.ini file hash to a ME3Tweaks database identifier that can be used to pull an updated moddesc.ini file
        /// </summary>
        private static Dictionary<string, string> Database;

        /// <summary>
        /// If the database has been initialized by a data source (cached or online)
        /// </summary>
        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"ModDesc Updater Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetModDescUpdaterServiceFile();

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
                    Database = serviceData.ToObject<Dictionary<string, string>>();
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
                    Database = JsonConvert.DeserializeObject<Dictionary<string, string>>(cached);
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

            M3Log.Warning($@"Unable to load {ServiceLoggingName}: No cached content or online content was available to load");
            return false;
        }

        public static bool HasHash(string moddescHash)
        {
            if (Database == null) return false;
            if (Database.TryGetValue(moddescHash, out var _))
                return true;
            return false;
        }

        /// <summary>
        /// Fetches a third party moddesc based on the provided (moddesc) hash
        /// </summary>
        /// <param name="moddescHash"></param>
        /// <returns></returns>
        public static string FetchUpdatedModdesc(string moddescHash, out string downloadedHash)
        {
            downloadedHash = null;
            try
            {
                if (Database.TryGetValue(moddescHash, out var mappedName))
                {
                    var onDiskPath = Path.Combine(M3Filesystem.GetModDescUpdaterServiceFolder(), mappedName);
                    if (File.Exists(onDiskPath))
                    {
                        downloadedHash = MUtilities.CalculateHash(onDiskPath);
                        return File.ReadAllText(onDiskPath);
                    }

                    // Fetch online version and cache it instead.
                    var content = M3OnlineContent.FetchThirdPartyModdesc(mappedName);
                    if (content != null)
                    {
                        File.WriteAllText(onDiskPath, content);
                        downloadedHash = MUtilities.CalculateHash(onDiskPath);
                        return content;
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $@"Error fetching updated moddesc.ini for hash {moddescHash}:");
            }

            return null;
        }
    }
}
