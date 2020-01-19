using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public class ShortTimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = 5 * 1000;
            return w;
        }
    }
}
