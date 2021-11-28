using System;
using System.Net;

namespace ME3TweaksModManager.modmanager.helpers
{
    public class ShortTimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = Settings.WebClientTimeout * 1000;
            return w;
        }
    }
}
