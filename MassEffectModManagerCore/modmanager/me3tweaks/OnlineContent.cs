using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.windows;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    //Localizable(false) //Leave this here for localizer tool!
    partial class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber + "&M3=true";
        private const string StartupManifestBackupURL = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/startupmanifest.json";
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL_Github = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/";
        private const string StaticFilesBaseURL_ME3Tweaks = "https://me3tweaks.com/modmanager/tools/staticfiles/";

        private const string ME3TweaksStaticFilesBaseURL_Github = "https://github.com/ME3Tweaks/ME3TweaksAssets/releases/download/";

        private const string ThirdPartyImportingServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyimportingservice?allgames=true";
        private const string BasegameFileIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/basegamefileidentificationservice";
        private const string BasegameFileIdentificationServiceBackupURL = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/basegamefileidentificationservice.json";

        private const string ThirdPartyModDescURL = "https://me3tweaks.com/mods/dlc_mods/importingmoddesc/";
        private const string ExeTransformBaseURL = "https://me3tweaks.com/mods/dlc_mods/importingexetransforms/";
        private const string ModInfoRelayEndpoint = "https://me3tweaks.com/modmanager/services/relayservice";
        private const string TipsServiceURL = "https://me3tweaks.com/modmanager/services/tipsservice";
        private const string ModMakerTopModsEndpoint = "https://me3tweaks.com/modmaker/api/topmods";
        private const string LocalizationEndpoint = "https://me3tweaks.com/modmanager/services/livelocalizationservice";

        private const string TutorialServiceURL = "https://me3tweaks.com/modmanager/services/tutorialservice";
        private const string TutorialServiceBackupURL = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/tutorialservice.json";


        public static readonly string ModmakerModsEndpoint = "https://me3tweaks.com/modmaker/download.php?id=";

        /// <summary>
        /// List of static files endpoints in order of preference
        /// </summary>
        public static string[] StaticFilesBaseEndpoints =
        {
            StaticFilesBaseURL_Github,
            StaticFilesBaseURL_ME3Tweaks
        };

        /// <summary>
        /// List of static files endpoints in order of preference. These endpoints are for the ME3TweaksStaticAssets and are not mirroed from github onto me3tweaks.
        /// </summary>
        public static string[] ME3TweaksStaticFilesBaseEndpoints =
        {
            ME3TweaksStaticFilesBaseURL_Github,
            StaticFilesBaseURL_ME3Tweaks
        };


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
            string[] ulrs = new[] { StartupManifestURL, StartupManifestBackupURL };
            foreach (var staticurl in ulrs)
            {
                Uri myUri = new Uri(staticurl);
                string host = myUri.Host;

                var fetchUrl = staticurl;
                if (betamode && host == @"me3tweaks.com") fetchUrl += "&beta=true"; //only me3tweaks source supports beta. fallback will always just use whatever was live when it synced

                try
                {
                    using var wc = new ShortTimeoutWebClient();
                    string json = wc.DownloadString(fetchUrl);
                    App.ServerManifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    Log.Information($@"Fetched startup manifest from endpoint {host}");
                    return App.ServerManifest;
                }
                catch (Exception e)
                {
                    Log.Error($@"Unable to fetch startup manifest from endpoint {host}: {e.Message}");
                }
            }

            Log.Error(@"Failed to fetch startup manifest.");
            return new Dictionary<string, string>();
        }

        public static (MemoryStream download, string errorMessage) DownloadStaticAsset(string assetName, Action<long, long> progressCallback = null)
        {
            (MemoryStream, string) result = (null, @"Could not download file: No attempt was made, or errors occurred!");
            foreach (var staticurl in StaticFilesBaseEndpoints)
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
                    Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
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
            foreach (var staticurl in ME3TweaksStaticFilesBaseEndpoints)
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
                    Log.Error($@"Could not download {assetName} from endpoint {staticurl}: {e.Message}");
                }
            }

            return result;
        }

        public static Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> FetchBasegameFileIdentificationServiceManifest(bool overrideThrottling = false)
        {
            Log.Information(@"Fetching basegame file identification manifest");

            //read cached first.
            string cached = null;
            if (File.Exists(Utilities.GetBasegameIdentificationCacheFile()))
            {
                try
                {
                    cached = File.ReadAllText(Utilities.GetBasegameIdentificationCacheFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content" },
                        {@"Service", @"Basegame File Identification Service" },
                        {@"Message", e.Message }
                    }, attachments.ToArray());
                }
            }


            if (!File.Exists(Utilities.GetBasegameIdentificationCacheFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                var urls = new[] { BasegameFileIdentificationServiceURL, BasegameFileIdentificationServiceBackupURL };
                foreach (var staticurl in urls)
                {
                    Uri myUri = new Uri(staticurl);
                    string host = myUri.Host;
                    try
                    {
                        using var wc = new ShortTimeoutWebClient();

                        string json = wc.DownloadStringAwareOfEncoding(staticurl);
                        File.WriteAllText(Utilities.GetBasegameIdentificationCacheFile(), json);
                        return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>>(json);
                    }
                    catch (Exception e)
                    {
                        //Unable to fetch latest help.
                        Log.Error($"Error fetching online basegame file identification service from endpoint {host}: {e.Message}");
                    }
                }

                if (cached == null)
                {
                    Log.Error("Unable to load basegame file identification service and local file doesn't exist. Returning a blank copy.");
                    Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> d = new Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>
                    {
                        ["ME1"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                        ["ME2"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                        ["ME3"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>()
                    };
                    return d;
                }
            }
            Log.Information("Using cached BGFIS instead");

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error("Could not parse cached basegame file identification service file. Returning blank BFIS data instead. Reason: " + e.Message);
                return new Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>>
                {
                    ["ME1"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                    ["ME2"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>(),
                    ["ME3"] = new CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>()
                };
            }
        }

        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()))
            {
                try
                {
                    cached = File.ReadAllText(Utilities.GetThirdPartyIdentificationCachedFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {"Error type", "Error reading cached online content" },
                        {"Service", "Third Party Identification Service" },
                        {"Message", e.Message }
                    }, attachments.ToArray());
                }
            }


            if (!File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();

                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyIdentificationServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyIdentificationCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    Log.Error("Error fetching online third party identification service: " + e.Message);

                    if (cached != null)
                    {
                        Log.Warning("Using cached third party identification service  file instead");
                    }
                    else
                    {
                        Log.Error("Unable to load third party identification service and local file doesn't exist. Returning a blank copy.");
                        Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> d = new Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>
                        {
                            ["ME1"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                            ["ME2"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                            ["ME3"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>()
                        };
                        return d;
                    }
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error("Could not parse cached third party identification service file. Returning blank TPMI data instead. Reason: " + e.Message);
                return new Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>
                {
                    ["ME1"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                    ["ME2"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>(),
                    ["ME3"] = new CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>()
                };
            }
        }

        public static string FetchRemoteString(string url, string authorizationToken = null)
        {
            try
            {
                using var wc = new ShortTimeoutWebClient();
                if (authorizationToken != null)
                {
                    wc.Headers.Add("Authorization", authorizationToken);
                }
                return wc.DownloadStringAwareOfEncoding(url);
            }
            catch (Exception e)
            {
                Log.Error("Error downloading string: " + e.Message);
                return null;
            }
        }

        public static string FetchThirdPartyModdesc(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            string moddesc = wc.DownloadStringAwareOfEncoding(ThirdPartyModDescURL + name);
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
                    Log.Error("Error converting top mods response to json: " + e.Message);
                }
            }

            return new List<ServerModMakerModInfo>();
        }

        public static string FetchExeTransform(string name)
        {
            using var wc = new ShortTimeoutWebClient();
            string moddesc = wc.DownloadStringAwareOfEncoding(ExeTransformBaseURL + name);
            return moddesc;
        }

        public static Dictionary<string, List<string>> FetchTipsService(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(Utilities.GetTipsServiceFile()))
            {
                try
                {
                    cached = File.ReadAllText(Utilities.GetTipsServiceFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {"Error type", "Error reading cached online content" },
                        {"Service", "Tips Service" },
                        {"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            if (!File.Exists(Utilities.GetTipsServiceFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();

                    string json = wc.DownloadStringAwareOfEncoding(TipsServiceURL);
                    File.WriteAllText(Utilities.GetTipsServiceFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    Log.Error("Error fetching latest tips service file: " + e.Message);
                    if (cached != null)
                    {
                        Log.Warning("Using cached tips service file instead");
                    }
                    else
                    {
                        Log.Error("Unable to fetch latest tips service file from server and local file doesn't exist. Returning a blank copy.");
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
                Log.Error("Unable to parse cached tips service file: " + e.Message);
                return new Dictionary<string, List<string>>();
            }
        }

        public static Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>> FetchThirdPartyImportingService(bool overrideThrottling = false)
        {
            string cached = null;
            if (File.Exists(Utilities.GetThirdPartyImportingCachedFile()))
            {
                try
                {
                    cached = File.ReadAllText(Utilities.GetThirdPartyImportingCachedFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {"Error type", "Error reading cached online content" },
                        {"Service", "Third Party Importing Service" },
                        {"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            if (!File.Exists(Utilities.GetThirdPartyImportingCachedFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                try
                {
                    using var wc = new ShortTimeoutWebClient();

                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyImportingServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyImportingCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>>(json);
                }
                catch (Exception e)
                {
                    //Unable to fetch latest help.
                    Log.Error("Error fetching latest importing service file: " + e.Message);

                    if (cached != null)
                    {
                        Log.Warning("Using cached third party importing service file instead");
                    }
                    else
                    {
                        Log.Error("Unable to fetch latest third party importing service file from server and local file doesn't exist. Returning a blank copy.");
                        return new Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>();
                    }
                }
            }
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>>(cached);
            }
            catch (Exception e)
            {
                Log.Error("Unable to parse cached importing service file: " + e.Message);
                return new Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>();
            }
        }

        /// <summary>
        /// Touches up any existing tutorial assets or downloads missing ones.
        /// </summary>
        public static void TouchupTutorial()
        {
            var fileRootPath = Utilities.GetTutorialServiceCache();
            foreach (var step in App.TutorialService)
            {
                var imagePath = Path.Combine(fileRootPath, step.imagename);
                bool download = !File.Exists(imagePath) || Utilities.CalculateMD5(imagePath) != step.imagemd5;
                if (download)
                {
                    foreach (var endpoint in StaticFilesBaseEndpoints)
                    {
                        Uri myUri = new Uri(endpoint);
                        string host = myUri.Host;

                        var fullurl = endpoint + "tutorial/" + step.imagename;
                        Log.Information($"Downloading {step.imagename} from endpoint {host}");
                        var downloadedImage = OnlineContent.DownloadToMemory(fullurl, null, step.imagemd5);
                        if (downloadedImage.errorMessage == null)
                        {
                            try
                            {
                                downloadedImage.result.WriteToFile(imagePath);
                            }
                            catch (Exception e)
                            {
                                Log.Error($@"Error writing tutorial image {imagePath}: {e.Message}");
                            }

                            break;
                        }
                        else
                        {
                            Log.Error($@"Unable to download {step.imagename} from endpoint {host}: {downloadedImage.errorMessage}");
                        }
                    }
                }
            }
        }

        public static Dictionary<string, string> QueryModRelay(string md5, long size)
        {
            //Todo: Finish implementing relay service
            string finalRelayURL = $"{ModInfoRelayEndpoint}?modmanagerversion={App.BuildNumber}&md5={md5.ToLowerInvariant()}&size={size}";
            try
            {
                using (var wc = new ShortTimeoutWebClient())
                {
                    Debug.WriteLine(finalRelayURL);
                    string json = wc.DownloadStringAwareOfEncoding(finalRelayURL);
                    //todo: Implement response format serverside
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }
            catch (Exception e)
            {
                Log.Error("Error querying relay service from ME3Tweaks: " + App.FlattenException(e));
            }

            return null;
        }

        public static bool EnsureCriticalFiles()
        {
            try
            {
                /*
                //7-zip
                string sevenZDLL = Utilities.Get7zDllPath();
                if (!File.Exists(sevenZDLL) || Utilities.CalculateMD5(sevenZDLL) != "72491c7b87a7c2dd350b727444f13bb4")
                {
                    foreach (var staticurl in StaticFilesBaseEndpoints)
                    {
                        try
                        {
                            using var wc = new ShortTimeoutWebClient();
                            {
                                var fullURL = staticurl + "7z.dll";
                                Log.Information("Downloading 7z.dll: " + fullURL);
                                wc.DownloadFile(fullURL, sevenZDLL);
                                break; //No more loops
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Could not download 7z.dll from endpoint {staticurl} {e.Message}");
                        }
                    }
                }

                if (File.Exists(sevenZDLL))
                {
                    Log.Information("Setting 7z dll path: " + sevenZDLL);
                    var p = Path.GetFullPath(sevenZDLL);
                    SevenZip.SevenZipBase.SetLibraryPath(sevenZDLL);
                }
                else
                {
                    Log.Fatal("Unable to load 7z dll! File doesn't exist: " + sevenZDLL);
                    return false;
                }*/
            }
            catch (Exception e)
            {
                Log.Error("Exception ensuring critical files: " + App.FlattenException(e));
                return false;
            }

            return true;
        }

        public static bool EnsureStaticAssets()
        {
            // This is not really used anymore. Just kept around in case new static assets are necessary.
            // Used to download objectt infos. These are embedded into ME3ExplorerCore.
            (string filename, string md5)[] objectInfoFiles = { };
            string localBaseDir = Utilities.GetObjectInfoFolder();

            try
            {
                bool downloadOK = false;

                foreach (var info in objectInfoFiles)
                {
                    var localPath = Path.Combine(localBaseDir, info.filename);
                    bool download = !File.Exists(localPath);
                    if (!download)
                    {
                        var calcedMd5 = Utilities.CalculateMD5(localPath);
                        download = calcedMd5 != info.md5;
                        if (download) Log.Warning($@"Invalid hash for local asset {info.filename}: got {calcedMd5}, expected {info.md5}. Redownloading");
                    }
                    else
                    {
                        Log.Information($"Local asset missing: {info.filename}, downloading");
                    }

                    if (download)
                    {
                        foreach (var staticurl in StaticFilesBaseEndpoints)
                        {
                            var fullURL = staticurl + "objectinfos/" + info.filename;

                            try
                            {
                                using var wc = new ShortTimeoutWebClient();
                                Log.Information("Downloading static asset: " + fullURL);
                                wc.DownloadFile(fullURL, localPath);
                                downloadOK = true;
                                break;
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Could not download {info} from endpoint {fullURL} {e.Message}");
                            }
                        }
                    }
                    else downloadOK = true; //say we're OK
                }

                if (!downloadOK)
                {
                    throw new Exception("At least one static asset failed to download. Mod Manager will not properly function without these assets. See logs for more information");
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception trying to ensure static assets: " + e.Message);
                Crashes.TrackError(new Exception(@"Could not download static supporting files: " + e.Message));
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
                Debug.WriteLine("Download file to memory: " + url);
                wc.DownloadDataAsync(new Uri(url), syncObject);
                //This will block the thread until download completes
                Monitor.Wait(syncObject);
            }

            return (responseStream, downloadError);
        }

        /// <summary>
        /// Downloads from a URL to memory. This is a blocking call and must be done on a background thread.
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="progressCallback">Progress information clalback</param>
        /// <param name="hash">Hash check value (md5). Leave null if no hash check</param>
        /// <returns></returns>

        public static (MemoryStream result, string errorMessage) DownloadToMemory(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null)
        {
            var resultV = DownloadToStreamInternal(url, progressCallback, hash, logDownload);
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
        public static (Stream result, string errorMessage) DownloadToStream(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null)
        {
            return DownloadToStreamInternal(url, progressCallback, hash, logDownload, destStreamOverride);
        }

        private static (Stream result, string errorMessage) DownloadToStreamInternal(string url, Action<long, long> progressCallback = null, string hash = null, bool logDownload = false, Stream destStreamOverride = null)
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
                    Log.Information($@"Downloading to {destType}: " + url);
                }
                else
                {
                    Debug.WriteLine($"Downloading to {destType}: " + url);
                }

                try
                {
                    using var remoteStream = wc.OpenRead(new Uri(url));
                    long.TryParse(wc.ResponseHeaders["Content-Length"], out var totalSize);
                    var buffer = new byte[4096];
                    int bytesReceived;
                    while ((bytesReceived = remoteStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        responseStream.Write(buffer, 0, bytesReceived);
                        progressCallback?.Invoke(responseStream.Position, totalSize); // Progress
                    }

                    // Check hash
                    if (hash != null)
                    {
                        var md5 = Utilities.CalculateMD5(responseStream);
                        responseStream.Position = 0;
                        if (md5 != hash)
                        {
                            responseStream = null;
                            downloadError = $"Hash of downloaded item ({url}) does not match expected hash. Expected: {hash}, got: {md5}"; //needs localized
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

        public static List<IntroTutorial.TutorialStep> FetchTutorialManifest(bool overrideThrottling = false)
        {
            Log.Information(@"Fetching tutorial manifest");
            string cached = null;
            // Read cached first.
            if (File.Exists(Utilities.GetTutorialServiceCacheFile()))
            {
                try
                {
                    cached = File.ReadAllText(Utilities.GetTutorialServiceCacheFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content" },
                        {@"Service", @"Tutorial Service" },
                        {@"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            if (!File.Exists(Utilities.GetTutorialServiceCacheFile()) || overrideThrottling || OnlineContent.CanFetchContentThrottleCheck())
            {
                string[] urls = new[] { TutorialServiceURL, TutorialServiceBackupURL };
                foreach (var staticurl in urls)
                {
                    Uri myUri = new Uri(staticurl);
                    string host = myUri.Host;

                    try
                    {
                        using var wc = new ShortTimeoutWebClient();
                        string json = wc.DownloadStringAwareOfEncoding(staticurl);
                        File.WriteAllText(Utilities.GetTutorialServiceCacheFile(), json);
                        return JsonConvert.DeserializeObject<List<IntroTutorial.TutorialStep>>(json);
                    }
                    catch (Exception e)
                    {
                        //Unable to fetch latest help.
                        Log.Error($@"Error fetching latest tutorial service file from endpoint {host}: {e.Message}");
                    }
                }

                if (cached == null)
                {
                    Log.Error(@"Unable to fetch latest tutorial service file from server and local file doesn't exist. Returning a blank copy.");
                    return new List<IntroTutorial.TutorialStep>();
                }
            }

            Log.Information(@"Using cached tutorial service file");

            try
            {
                return JsonConvert.DeserializeObject<List<IntroTutorial.TutorialStep>>(cached);
            }
            catch (Exception e)
            {
                Log.Error(@"Unable to parse cached importing service file: " + e.Message);
                return new List<IntroTutorial.TutorialStep>();
            }
        }
    }
}
