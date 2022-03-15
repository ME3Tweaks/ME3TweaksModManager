using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.windows;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using ShortTimeoutWebClient = ME3TweaksModManager.modmanager.helpers.ShortTimeoutWebClient;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{

    partial class M3OnlineContent
    {
        #region FALLBACKS
        /// <summary>
        /// Startup Manifest URLs
        /// </summary>
        private static FallbackLink StartupManifestURL = new FallbackLink()
        {
            MainURL = @"https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber + @"&M3=true",
            FallbackURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/ME3TweaksModManager/staticfiles/startupmanifest.json"
        };

        /// <summary>
        /// Endpoint (base URL) for downloading static assets
        /// </summary>
        internal static FallbackLink StaticFileBaseEndpoints { get; } = new()
        {
            MainURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/",
            FallbackURL = @"https://me3tweaks.com/modmanager/tools/staticfiles/"
        };

        #endregion

        private const string ThirdPartyModDescURL = @"https://me3tweaks.com/mods/dlc_mods/importingmoddesc/";
        private const string ExeTransformBaseURL = @"https://me3tweaks.com/mods/dlc_mods/importingexetransforms/";
        private const string ModInfoRelayEndpoint = @"https://me3tweaks.com/modmanager/services/relayservice";
        private const string TipsServiceURL = @"https://me3tweaks.com/modmanager/services/tipsservice";
        private const string ModMakerTopModsEndpoint = @"https://me3tweaks.com/modmaker/api/topmods";
        private const string LocalizationEndpoint = @"https://me3tweaks.com/modmanager/services/livelocalizationservice";
        public static readonly string ModmakerModsEndpoint = @"https://me3tweaks.com/modmaker/download.php?id=";


        /// <summary>
        /// Checks if we can perform an online content fetch. This value is updated when manually checking for content updates, and on automatic 1-day intervals (if no previous manual check has occurred)
        /// </summary>
        /// <returns></returns>
        internal static bool CanFetchContentThrottleCheck()
        {
            var lastContentCheck = Settings.LastContentCheck;
            var timeNow = DateTime.Now;
            return (timeNow - lastContentCheck).TotalDays > 1;
        }

        public static Dictionary<string, string> FetchOnlineStartupManifest(bool betamode)
        {
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
                    App.ServerManifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    M3Log.Information($@"Fetched startup manifest from endpoint {host}");
                    return App.ServerManifest;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Unable to fetch startup manifest from endpoint {host}: {e.Message}");
                }
            }

            M3Log.Error(@"Failed to fetch startup manifest.");
            return new Dictionary<string, string>();
        }

        public static (MemoryStream download, string errorMessage) DownloadStaticAsset(string assetName, Action<long, long> progressCallback = null)
        {
            (MemoryStream, string) result = (null, @"Could not download file: No attempt was made, or errors occurred!");
            foreach (var staticurl in StaticFileBaseEndpoints.GetAllLinks())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    {
                        var fullURL = staticurl + assetName;
                        result = DownloadToMemory(fullURL, logDownload: true, progressCallback: progressCallback);
                        if (result.Item2 == null) return result;
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Downloads a static asset that is mirrored onto the ME3Tweaks Assets repo. This is not the same as the github version of staticfiles.
        /// </summary>
        /// <param name="assetName">The asset filename. Do not include any path information.</param>
        /// <returns></returns>
        public static (MemoryStream download, string errorMessage) DownloadME3TweaksStaticAsset(string assetName)
        {
            (MemoryStream, string) result = (null, @"Could not download file: No attempt was made, or errors occurred!");
            foreach (var staticurl in StaticFileBaseEndpoints.GetAllLinks())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    {
                        var fullURL = staticurl + Path.GetFileNameWithoutExtension(assetName) + "/" + assetName;
                        result = DownloadToMemory(fullURL, logDownload: true);
                        if (result.Item2 == null) return result;
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
                }
            }

            return result;
        }


        public static string FetchRemoteString(string url, string authorizationToken = null)
        {
            try
            {
                using var wc = new ShortTimeoutWebClient();
                if (authorizationToken != null)
                {
                    wc.Headers.Add(@"Authorization", authorizationToken);
                }
                return WebClientExtensions.DownloadStringAwareOfEncoding(wc, url);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error downloading string: " + e.Message);
                return null;
            }
        }

        public static string FetchThirdPartyModdesc(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            string moddesc = WebClientExtensions.DownloadStringAwareOfEncoding(wc, ThirdPartyModDescURL + name);
            return moddesc;
        }

        public static List<ServerModMakerModInfo> FetchTopModMakerMods()
        {
            var topModsJson = FetchRemoteString(ModMakerTopModsEndpoint);
            if (topModsJson != null)
            {
                try
                {
                    return JsonConvert.DeserializeObject<List<ServerModMakerModInfo>>(topModsJson);
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Error converting top mods response to json: " + e.Message);
                }
            }

            return new List<ServerModMakerModInfo>();
        }

        public static string FetchExeTransform(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            string moddesc = WebClientExtensions.DownloadStringAwareOfEncoding(wc, ExeTransformBaseURL + name);
            return moddesc;
        }

        public static Dictionary<string, List<string>> FetchTipsService(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(M3Utilities.GetTipsServiceFile()))
            {
                try
                {
                    cached = File.ReadAllText(M3Utilities.GetTipsServiceFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content" },
                        {@"Service", @"Tips Service" },
                        {@"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            if (!File.Exists(M3Utilities.GetTipsServiceFile()) || overrideThrottling || MOnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();

                    string json = WebClientExtensions.DownloadStringAwareOfEncoding(wc, TipsServiceURL);
                    File.WriteAllText(M3Utilities.GetTipsServiceFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    M3Log.Error(@"Error fetching latest tips service file: " + e.Message);
                    if (cached != null)
                    {
                        M3Log.Warning(@"Using cached tips service file instead");
                    }
                    else
                    {
                        M3Log.Error(@"Unable to fetch latest tips service file from server and local file doesn't exist. Returning a blank copy.");
                        return new Dictionary<string, List<string>>();
                    }
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(cached);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Unable to parse cached tips service file: " + e.Message);
                return new Dictionary<string, List<string>>();
            }
        }

     
        public static bool EnsureCriticalFiles()
        {
            // This method does nothing currently but is left here as a stub
            return true;
            try
            {
                /*
                //7-zip
                string sevenZDLL = Utilities.Get7zDllPath();
                if (!File.Exists(sevenZDLL) || Utilities.CalculateMD5(sevenZDLL) != @"72491c7b87a7c2dd350b727444f13bb4")
                {
                    foreach (var staticurl in StaticFilesBaseEndpoints)
                    {
                        try
                        {
                            using var wc = new ShortTimeoutWebClient();
                            {
                                var fullURL = staticurl + @"7z.dll";
                                M3Log.Information(@"Downloading 7z.dll: " + fullURL);
                                wc.DownloadFile(fullURL, sevenZDLL);
                                break; //No more loops
                            }
                        }
                        catch (Exception e)
                        {
                            M3Log.Error($@"Could not download 7z.dll from endpoint {staticurl} {e.Message}");
                        }
                    }
                }

                if (File.Exists(sevenZDLL))
                {
                    M3Log.Information(@"Setting 7z dll path: " + sevenZDLL);
                    var p = Path.GetFullPath(sevenZDLL);
                    SevenZip.SevenZipBase.SetLibraryPath(sevenZDLL);
                }
                else
                {
                    M3Log.Fatal(@"Unable to load 7z dll! File doesn't exist: " + sevenZDLL);
                    return false;
                }*/
            }
            catch (Exception e)
            {
                M3Log.Error(@"Exception ensuring critical files: " + App.FlattenException(e));
                return false;
            }

            return true;
        }

        public static (MemoryStream result, string errorMessage) FetchString(string url)
        {
            using var wc = new ShortTimeoutWebClient();
            string downloadError = null;
            MemoryStream responseStream = null;
            wc.DownloadDataCompleted += (a, args) =>
            {
                downloadError = args.Error?.Message;
                if (downloadError == null)
                {
                    responseStream = new MemoryStream(args.Result);
                }
                lock (args.UserState)
                {
                    //releases blocked thread
                    Monitor.Pulse(args.UserState);
                }
            };
            var syncObject = new Object();
            lock (syncObject)
            {
                Debug.WriteLine(@"Download file to memory: " + url);
                wc.DownloadDataAsync(new Uri(url), syncObject);
                //This will block the thread until download completes
                Monitor.Wait(syncObject);
            }

            return (responseStream, downloadError);
        }

        /// <summary>
        /// Queries the ME3Tweaks Mod Relay for information about a file with the specified md5 and size.
        /// </summary>
        /// <param name="md5"></param>
        /// <param name="size"></param>
        /// <returns>Dictionary of information about the file, if any.</returns>
        public static Dictionary<string, string> QueryModRelay(string md5, long size)
        {
            //Todo: Finish implementing relay service
            string finalRelayURL = $@"{ModInfoRelayEndpoint}?modmanagerversion={App.BuildNumber}&md5={md5.ToLowerInvariant()}&size={size}";
            try
            {
                using (var wc = new ShortTimeoutWebClient())
                {
                    Debug.WriteLine(finalRelayURL);
                    string json = WebClientExtensions.DownloadStringAwareOfEncoding(wc, finalRelayURL);
                    //todo: Implement response format serverside
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error querying relay service from ME3Tweaks: " + App.FlattenException(e));
            }

            return null;
        }

        /// <summary>
        /// Downloads from a URL to memory. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="progressCallback">Progress information clalback</param>
        /// <param name="hash">Hash check value (md5). Leave null if no hash check</param>
        /// <returns></returns>
        public static (MemoryStream result, string errorMessage) DownloadToMemory(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null, CancellationToken cancellationToken = default)
        {
            var resultV = DownloadToStreamInternal(url, progressCallback, hash, logDownload, cancellationToken: cancellationToken);
            return (resultV.result as MemoryStream, resultV.errorMessage);
        }

        /// <summary>
        /// Downloads a URL to the specified stream. If not stream is specifed, the stream returned is a MemoryStream. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="progressCallback"></param>
        /// <param name="hash"></param>
        /// <param name="logDownload"></param>
        /// <param name="destStreamOverride"></param>
        /// <returns></returns>
        public static (Stream result, string errorMessage) DownloadToStream(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null, CancellationToken cancellationToken = default)
        {
            return DownloadToStreamInternal(url, progressCallback, hash, logDownload, destStreamOverride, cancellationToken);
        }

        private static (Stream result, string errorMessage) DownloadToStreamInternal(string url,
            Action<long, long> progressCallback = null,
            string hash = null,
            bool logDownload = false,
            Stream destStreamOverride = null,
            CancellationToken cancellationToken = default)
        {
            using var wc = new ShortTimeoutWebClient();
            string downloadError = null;
            string destType = destStreamOverride != null ? @"stream" : @"memory";
            Stream responseStream = destStreamOverride ?? new MemoryStream();

            var syncObject = new Object();
            lock (syncObject)
            {
                if (logDownload)
                {
                    M3Log.Information($@"Downloading to {destType}: " + url);
                }
                else
                {
                    Debug.WriteLine($@"Downloading to {destType}: " + url);
                }

                try
                {
                    using var remoteStream = wc.OpenRead(new Uri(url));
                    long.TryParse(wc.ResponseHeaders[@"Content-Length"], out var totalSize);
                    var buffer = new byte[4096];
                    int bytesReceived;
                    while ((bytesReceived = remoteStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            downloadError = M3L.GetString(M3L.string_theDownloadWasCanceled);
                            return (responseStream, downloadError);
                        }
                        responseStream.Write(buffer, 0, bytesReceived);
                        progressCallback?.Invoke(responseStream.Position, totalSize); // Progress
                    }

                    // Check hash
                    if (hash != null)
                    {
                        responseStream.Position = 0;
                        var md5 = MD5.Create().ComputeHashAsync(responseStream, cancellationToken, x => progressCallback?.Invoke(x, 100)).Result;
                        responseStream.Position = 0;
                        if (md5 != hash)
                        {
                            responseStream = null;
                            downloadError = M3L.GetString(M3L.string_interp_onlineContentHashWrong, url, hash, md5); //needs localized
                        }
                    }
                }
                catch (Exception e)
                {
                    downloadError = e.Message;
                }
            }

            return (responseStream, downloadError);
        }

        [Localizable(true)]
        public class ServerModMakerModInfo
        {

            public string mod_id { get; set; }
            public string mod_name { get; set; }
            public string mod_desc { get; set; }
            public string revision { get; set; }
            public string username { get; set; }

            public string UIRevisionString => M3L.GetString(M3L.string_interp_revisionX, revision);
            public string UICodeString => M3L.GetString(M3L.string_interp_codeX, mod_id);
        }
    }
}
