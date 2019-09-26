using MassEffectModManager.modmanager;
using System;
using System.Collections.Generic;
using System.Text;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public class ThirdPartyServices
    {
        public static List<ThirdPartyImportingInfo> GetImportingInfosBySize(long size)
        {
            if (App.ThirdPartyImportingService == null) return new List<ThirdPartyImportingInfo>(); //Not loaded
            if (App.ThirdPartyImportingService.TryGetValue(size, out var result))
            {
                return result;
            }
            return new List<ThirdPartyImportingInfo>();
        }
    }


    public class ThirdPartyImportingInfo
    {
        public string md5 { get; set; }
        public string inarchivepathtosearch { get; set; }
        public string filename { get; set; }
        public string subdirectorydepth { get; set; }
        public object servermoddescname { get; set; }
        public Mod.MEGame game { get; set; }
        public object version { get; set; }
    }
}
