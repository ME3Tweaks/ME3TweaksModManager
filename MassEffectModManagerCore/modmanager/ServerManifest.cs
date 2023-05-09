using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Misc;
using ME3TweaksModManager.modmanager.me3tweaks.online;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager
{
    /// <summary>
    /// Class for interacting with the ME3Tweaks Mod Manager server manifest
    /// </summary>
    public class ServerManifest
    {
        #region Manifest keys
        internal const string SERVER_ALIGNMENT = @"serveralignment";
        internal const string M3_LATEST_BUILD_NUMBER = @"latest_build_number";
        internal const string M3_LATEST_VERSION_HUMAN_READABLE = @"latest_version_hr";
        internal const string MIXIN_PACKAGE_MD5 = @"mixinpackagemd5";
        internal const string LIVE_LOCALIZATION_PREFIX = @"livelocalization";
        internal const string LATEST_NEXUSDB_HASH = @"latest_nexusdb_hash";
        internal const string LEX_NIGHTLY_DOTNET_VERSION_REQ = @"legendaryexplorernightly_netversion";
        internal const string LEX_STABLE_DOTNET_VERSION_REQ = @"legendaryexplorerstable_netversion";
        internal const string LEX_NIGHTLY_LATEST_DOWNLOADLINK = @"legendaryexplorernightly_latestlink";
        internal const string LEX_NIGHTLY_LATEST_VERSION = @"legendaryexplorernightly_latestversion";
        internal const string M3_RELEASE_NOTES = @"release_notes";
        internal const string M3_PRIMARY_FULL_UPDATE_LINK = @"download_link2"; // Typically github, me3tweaks during beta
        internal const string M3_FALLBACK_FULL_UPDATE_LINK = @"download_link"; // This is super old legacy naming system, always me3tweaks
        internal const string M3_BUILD_RERELEASE_MD5 = @"build_md5";
        internal const string M3_GITHUB_PATCH_UPDATE_PREFIX = @"gh_upd-";
        internal const string M3_ME3TWEAKS_PATCH_UPDATE_PREFIX = @"upd-";
        internal const string FEATURE_FLIGHTING_PREFIX = @"flighting";
        internal const string LOCALIZATION_ENABLED_ITA = @"localization_enabled_ita";
        internal const string LOCALIZATION_ENABLED_BRA = @"localization_enabled_bra";
        internal const string LOCALIZATION_ENABLED_RUS = @"localization_enabled_rus";
        internal const string LOCALIZATION_ENABLED_DEU = @"localization_enabled_deu";
        internal const string LOCALIZATION_ENABLED_POL = @"localization_enabled_pol";
        #endregion

        #region Online fetch

        /// <summary>
        /// Startup Manifest URLs
        /// </summary>
        private static FallbackLink StartupManifestURL = new FallbackLink()
        {
            MainURL = @"https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber + @"&M3=true",
            FallbackURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/staticfiles/liveservices/services/startupmanifest.json"
        };

        /// <summary>
        /// This is so if you leave M3 open for days (e.g. you put system to sleep) it will hopefully refresh the manifest without a reboot
        /// </summary>
        private static Timer manifestRefreshTimer;


        /// <summary>
        /// Refreshes the online manifest
        /// </summary>
        /// <param name="betamode"></param>
        /// <returns></returns>
        public static bool FetchOnlineStartupManifest(bool betamode, bool usePeriodicRefresh = false)
        {
            if (usePeriodicRefresh && manifestRefreshTimer == null)
            {
                manifestRefreshTimer = new Timer();
                manifestRefreshTimer.Elapsed += OnTimedEvent;
#if DEBUG // This is so I don't accidentally screw this up in production
                manifestRefreshTimer.Interval = 1000 * 60 * 60 * 4; // 4 hour manifest refresh. It's a lot less data than combined services
#else
                manifestRefreshTimer.Interval = 1000 * 60 * 60 * 4; // 4 hour manifest refresh. It's a lot less data than combined services
#endif
                manifestRefreshTimer.Start();
            }

            foreach (var staticurl in StartupManifestURL.GetAllLinks())
            {
                Uri myUri = new Uri(staticurl);
                string host = myUri.Host;

                var fetchUrl = staticurl;
                if (betamode && host == @"me3tweaks.com") fetchUrl += @"&beta=true"; //only me3tweaks source supports beta. fallback will always just use whatever was live when it synced

                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    string json = wc.DownloadString(fetchUrl);
                    _serverManifest = JsonConvert.DeserializeObject<CaseInsensitiveDictionary<object>>(json);
                    M3Log.Information($@"Fetched startup manifest from endpoint {host}");
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        M3ServiceLoader.TouchupServerManifest(Application.Current.MainWindow as MainWindow);
                    });
                    return true;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Unable to fetch startup manifest from endpoint {host}: {e.Message}");
                }
            }

            M3Log.Error(@"Failed to fetch startup manifest.");
            return false;
        }



        // Specify what you want to happen when the Elapsed event is raised.
        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            FetchOnlineStartupManifest(Settings.BetaMode);
        }


#endregion

        #region Data storage
        public static bool HasManifest => _serverManifest != null;
        private static CaseInsensitiveDictionary<object> _serverManifest { get; set; }
        #endregion

        #region Data access
        /// <summary>
        /// Fetches a string value from the server manifest
        /// </summary>
        /// <param name="key"></param>
        /// <returns>null if not found</returns>
        public static string GetString(string key)
        {
            if (_serverManifest == null) return null;
            if (_serverManifest.TryGetValue(key, out object value) && value is string strval)
            {
                return strval;
            }

            // Key not found or of wrong type
            return null;
        }

        /// <summary>
        /// Fetches a string from the server manifest, returning true if found. Can look for a language suffixed version, that falls back to non-suffixed if not found
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool TryGetString(string key, out string value, string language = null)
        {
            value = null;
            if (language != null)
            {
                value = GetString($@"{key}-{language}");
            }

            if (value == null)
            {
                // Get non-localized version
                value = GetString(key);
            }
            return value != null;
        }

        /// <summary>
        /// Fetches a int value from the server manifest
        /// </summary>
        /// <param name="key">manifest key to fetch</param>
        /// <returns>null if not found</returns>
        public static int? GetInt(string key)
        {
            if (_serverManifest == null) return null;
            if (_serverManifest.TryGetValue(key, out object value))
            {
                if (value is int intValue)
                {
                    return intValue;
                }

                // This is workaround for json deserialization of anonymous type
                if (value is long longvalue)
                {
                    return (int)longvalue;
                }
            }

            // Key not found or of wrong type
            return null;
        }

        public static bool TryGetInt(string key, out int value)
        {
            value = 0;
            var ivalue = GetInt(key);
            if (ivalue != null)
            {
                value = ivalue.Value;
            }
            return ivalue != null;
        }

        /// <summary>
        /// Gets the list of values according to the specified predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<string, object>> GetValues(Func<KeyValuePair<string, object>, bool> predicate)
        {
            if (!HasManifest) return Enumerable.Empty<KeyValuePair<string, object>>(); // Nothing
            return _serverManifest.Where(predicate);

        }
        #endregion

        /// <summary>
        /// Tries to get a boolean from the manifest.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryGetBool(string key, out bool result, bool defaultResult = false)
        {
            result = defaultResult;
            if (TryGetString(key, out var enabledStr) && bool.TryParse(enabledStr, out result))
            {
                // Nothing, was parsed.
            }
            return result;
        }
    }
}