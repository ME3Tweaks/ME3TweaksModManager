using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public partial class OnlineContent
    {
        private const string UpdaterEndpoint = "https://me3tweaks.com/mods/getlatest_batch";
        public static void CheckForModUpdates(List<Mod> modsToCheck)
        {
            string updateFinalRequest = UpdaterEndpoint;
            bool first = true;
            foreach (var mod in modsToCheck)
            {
                if (mod.ModModMakerID > 0)
                {
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }
                    //Modmaker style
                    updateFinalRequest += "modmakerupdatecode[]=" + mod.ModModMakerID;

                }
                else if (mod.ModClassicUpdateCode > 0)
                {
                    //Classic style
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }
                    updateFinalRequest += "classicupdatecode[]=" + mod.ModClassicUpdateCode;
                }
            }
            Debug.WriteLine("URL to check for updates: " + updateFinalRequest);
        }
    }
}
