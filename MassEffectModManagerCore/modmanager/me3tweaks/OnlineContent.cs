using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManagerCore;
using MassEffectModManagerCore.modmanager.me3tweaks;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManager.modmanager.me3tweaks
{
    partial class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber;
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/mods/dlc_mods/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL = "https://raw.githubusercontent.com/ME3Tweaks/MassEffectModManager/master/MassEffectModManager/staticfiles/";
        private const string ThirdPartyImportingServiceURL = "https://me3tweaks.com/mods/dlc_mods/thirdpartyimportingservice?allgames=true";
        private static readonly string TipsServiceURL = StaticFilesBaseURL + "tipsservice.json";
        public static Dictionary<string, string> FetchOnlineStartupManifest()
        {
            using (var wc = new System.Net.WebClient())
            {
                string json = wc.DownloadString(StartupManifestURL);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }
        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {

                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyIdentificationServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyIdentificationCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
                }
            }
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(File.ReadAllText(Utilities.GetThirdPartyIdentificationCachedFile()));
        }

        public static List<string> FetchTipsService(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetTipsServiceFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {
                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(TipsServiceURL);
                    File.WriteAllText(Utilities.GetTipsServiceFile(), json);
                    return JsonConvert.DeserializeObject<List<string>>(json);
                }
            }
            return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Utilities.GetTipsServiceFile()));
        }

        public static Dictionary<long, List<ThirdPartyImportingInfo>> FetchThirdPartyImportingService(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetThirdPartyImportingCachedFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {
                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyImportingServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyImportingCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyImportingInfo>>>(json);
                }
            }
            return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyImportingInfo>>>(File.ReadAllText(Utilities.GetThirdPartyImportingCachedFile()));
        }

        private const string ModInfoRelayEndpoint = "https://me3tweaks.com/modmanager/relayservice/queryrelay";
        public static object QueryModRelay(string md5)
        {
            //Todo: Finish implementing relay service
            string finalRelayURL = $"{ModInfoRelayEndpoint}?ModManagerVersion={App.BuildNumber}&MD5={md5.ToLowerInvariant()}";
            using (var wc = new System.Net.WebClient())
            {
                string json = wc.DownloadStringAwareOfEncoding(finalRelayURL);
                //todo: Implement response format serverside
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
            }
            return null;
        }

        public static bool EnsureCriticalFiles()
        {
            //7-zip
            try
            {
                string sevenZDLL = Utilities.Get7zDllPath();
                if (!File.Exists(sevenZDLL))
                {
                    using (var wc = new System.Net.WebClient())
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
            foreach (var info in objectInfoFiles)
            {
                var localPath = Path.Combine(localBaseDir, info);
                if (!File.Exists(localPath))
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        var fullURL = StaticFilesBaseURL + "objectinfos/" + info;
                        Log.Information("Downloading static asset: " + fullURL);
                        wc.DownloadFile(fullURL, localPath);
                    }
                }
            }

            return true;
        }
    }
}
