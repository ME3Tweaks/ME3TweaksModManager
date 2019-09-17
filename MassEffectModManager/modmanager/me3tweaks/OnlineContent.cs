using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManager.modmanager.me3tweaks
{
    partial class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber;
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/mods/dlc_mods/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL = "https://raw.githubusercontent.com/ME3Tweaks/MassEffectModManager/master/MassEffectModManager/staticfiles/";
        private static readonly string TipsServiceURL = StaticFilesBaseURL + "tipsservice.json";
        public static Dictionary<string, string> FetchOnlineStartupManifest()
        {
            string contents;
            using (var wc = new System.Net.WebClient())
            {
                string json = wc.DownloadString(StartupManifestURL);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            return null;
        }
        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {

                string contents;
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

                string contents;
                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(TipsServiceURL);
                    File.WriteAllText(Utilities.GetTipsServiceFile(), json);
                    return JsonConvert.DeserializeObject<List<string>>(json);
                }
            }
            return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Utilities.GetTipsServiceFile()));
        }

        private const string ModInfoRelayEndpoint = "https://me3tweaks.com/mods/relayservice";
        public static List<RelayModInfo> QueryModRelay(string md5)
        {
            //Todo: Implement relay service serverside
            //Todo: Implement relay service locally
            return null;
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
