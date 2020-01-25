using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

using MassEffectModManagerCore.modmanager.helpers;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    partial class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber + "&M3=true";
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL = "https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/MassEffectModManagerCore/staticfiles/";
        private const string ThirdPartyImportingServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyimportingservice?allgames=true";
        private const string ThirdPartyModDescURL = "https://me3tweaks.com/mods/dlc_mods/importingmoddesc/";
        private const string ExeTransformBaseURL = "https://me3tweaks.com/mods/dlc_mods/importingexetransforms/";
        private const string ModInfoRelayEndpoint = "https://me3tweaks.com/modmanager/services/relayservice";
        private const string TipsServiceURL = "https://me3tweaks.com/modmanager/services/tipsservice";
        private const string ModMakerTopModsEndpoint = "https://me3tweaks.com/modmaker/api/topmods";

        public static readonly string ModmakerModsEndpoint = "https://me3tweaks.com/modmaker/download.php?id=";

        public static Dictionary<string, string> FetchOnlineStartupManifest()
        {
            using var wc = new ShortTimeoutWebClient();
            string json = wc.DownloadString(StartupManifestURL);
            App.ServerManifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return App.ServerManifest;
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
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < ByteSizeLib.ByteSize.BytesInMegaByte * 7)
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


            if (!File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()) || overrideThrottling || Utilities.CanFetchContentThrottleCheck())
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

        public static string FetchRemoteString(string url)
        {
            try
            {
                using var wc = new ShortTimeoutWebClient();
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
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < ByteSizeLib.ByteSize.BytesInMegaByte * 7)
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

            if (!File.Exists(Utilities.GetTipsServiceFile()) || overrideThrottling || Utilities.CanFetchContentThrottleCheck())
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
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < ByteSizeLib.ByteSize.BytesInMegaByte * 7)
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

            if (!File.Exists(Utilities.GetThirdPartyImportingCachedFile()) || overrideThrottling || Utilities.CanFetchContentThrottleCheck())
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
            //7-zip
            try
            {
                string sevenZDLL = Utilities.Get7zDllPath();
                if (!File.Exists(sevenZDLL) || Utilities.CalculateMD5(sevenZDLL) != "72491c7b87a7c2dd350b727444f13bb4")
                {
                    using (var wc = new ShortTimeoutWebClient())
                    {
                        var fullURL = StaticFilesBaseURL + "7z.dll";
                        Log.Information("Downloading 7z.dll: " + fullURL);
                        wc.DownloadFile(fullURL, sevenZDLL);
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
                }
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
            string[] objectInfoFiles = { "ME1ObjectInfo.json", "ME2ObjectInfo.json", "ME3ObjectInfo.json" };
            string localBaseDir = Utilities.GetObjectInfoFolder();
            try
            {
                foreach (var info in objectInfoFiles)
                {
                    var localPath = Path.Combine(localBaseDir, info);
                    if (!File.Exists(localPath))
                    {
                        using (var wc = new ShortTimeoutWebClient())
                        {
                            var fullURL = StaticFilesBaseURL + "objectinfos/" + info;
                            Log.Information("Downloading static asset: " + fullURL);
                            wc.DownloadFile(fullURL, localPath);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception trying to ensure static assets: " + e.Message);
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
        /// Downloads from a URL to memory. This is a blocking call and should be done on a background thread.
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="progressCallback">Progress information clalback</param>
        /// <param name="hash">Hash check value (md5). Leave null if no hash check</param>
        /// <returns></returns>

        public static (MemoryStream result, string errorMessage) DownloadToMemory(string url, Action<long, long> progressCallback = null, string hash = null)
        {
            using var wc = new ShortTimeoutWebClient();
            string downloadError = null;
            MemoryStream responseStream = null;
            wc.DownloadProgressChanged += (a, args) => { progressCallback?.Invoke(args.BytesReceived, args.TotalBytesToReceive); };
            wc.DownloadDataCompleted += (a, args) =>
            {
                downloadError = args.Error?.Message;
                if (downloadError == null)
                {
                    responseStream = new MemoryStream(args.Result);
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

        public class ServerModMakerModInfo
        {

            public string mod_id { get; set; }
            public string mod_name { get; set; }
            public string mod_desc { get; set; }
            public string revision { get; set; }
            public string username { get; set; }

            public string UIRevisionString => $" Revision {revision}";
            public string UICodeString => $"Code {mod_id}";
        }
    }
}
