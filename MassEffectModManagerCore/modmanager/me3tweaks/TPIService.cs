using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Serilog;
using ShortTimeoutWebClient = ME3TweaksCore.Misc.ShortTimeoutWebClient;

namespace ME3TweaksModManager.modmanager.me3tweaks
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

        private const string ThirdPartyImportingServiceURL = @"https://me3tweaks.com/modmanager/services/thirdpartyimportingservice?allgames=true";

        /// <summary>
        /// Looks up importing information for mods through the third party mod importing service. This returns all candidates, the client code must determine which is the appropriate value.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for information</param>
        /// <returns>List of candidates</returns>
        public static List<ThirdPartyImportingInfo> GetImportingInfosBySize(long archiveSize)
        {
            if (Database == null) return new List<ThirdPartyImportingInfo>(); //Not loaded
            if (Database.TryGetValue(archiveSize, out var result))
            {
                return result;
            }

            return new List<ThirdPartyImportingInfo>();
        }

        public static bool LoadService(bool forceRefresh = false)
        {
            Database = FetchThirdPartyIdentificationManifest(forceRefresh);
            ServiceLoaded = true;
            return true;
        }

        private static Dictionary<long, List<ThirdPartyImportingInfo>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(M3Filesystem.GetThirdPartyImportingCachedFile()))
            {
                try
                {
                    cached = File.ReadAllText(M3Filesystem.GetThirdPartyImportingCachedFile());
                }
                catch (Exception e)
                {
                    var relevantInfo = new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content"},
                        {@"Service", @"Third Party Importing Service"},
                        {@"Message", e.Message}
                    };
                    TelemetryInterposer.UploadErrorLog(e, relevantInfo);
                }
            }

            if (!File.Exists(M3Filesystem.GetThirdPartyImportingCachedFile()) || overrideThrottling || MOnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();

                    string json = MExtensions.DownloadStringAwareOfEncoding(wc, ThirdPartyImportingServiceURL);
                    File.WriteAllText(M3Filesystem.GetThirdPartyImportingCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyImportingInfo>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    Log.Error(@"Error fetching latest importing service file: " + e.Message);

                    if (cached != null)
                    {
                        Log.Warning(@"Using cached third party importing service file instead");
                    }
                    else
                    {
                        Log.Error(@"Unable to fetch latest third party importing service file from server and local file doesn't exist. Returning a blank copy.");
                        return new Dictionary<long, List<ThirdPartyImportingInfo>>();
                    }
                }
            }
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyImportingInfo>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error(@"Unable to parse cached importing service file: " + e.Message);
                return new Dictionary<long, List<ThirdPartyImportingInfo>>();
            }
        }
    }
}
