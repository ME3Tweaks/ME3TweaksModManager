using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager.helpers;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManager.modmanager.me3tweaks
{
    class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber;
        private static readonly string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/mods/dlc_mods/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";

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
        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> FetchThirdPartyIdentificationManifest(bool overrideThrottling)
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
            else
            {
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(File.ReadAllText(Utilities.GetThirdPartyIdentificationCachedFile()));
            }
        }

        public static bool EnsureStaticAssets()
        {
            string[] objectInfoFiles = {"ME1ObjectInfo.json", "ME2ObjectInfo.json", "ME3ObjectInfo.json"};
            string baseURL = "https://raw.githubusercontent.com/ME3Tweaks/MassEffectModManager/master/MassEffectModManager/staticfiles/";
            string localBaseDir = Utilities.GetObjectInfoFolder();
            foreach (var info in objectInfoFiles)
            {
                var localPath = Path.Combine(localBaseDir, info);
                if (!File.Exists(localPath))
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        var fullURL = baseURL + info;
                        Log.Information("Downloading static asset: "+fullURL);
                        wc.DownloadFile(fullURL, localPath);
                    }
                }
            }

            return true;
        }
    }
}
