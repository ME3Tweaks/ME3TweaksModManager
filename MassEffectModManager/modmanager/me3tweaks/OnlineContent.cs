using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MassEffectModManager.modmanager.me3tweaks
{
    class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber;

        public static Dictionary<string,string> FetchOnlineStartupManifest()
        {
            string contents;
            using (var wc = new System.Net.WebClient())
            {
                string json = wc.DownloadString(StartupManifestURL);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            return null;
        }
    }
}
