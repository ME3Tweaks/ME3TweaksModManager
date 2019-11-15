using System;
using System.IO;
using System.Collections.Generic;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.GameDirectories
{
    public static class ME1Directory
    {

        private static string _gamePath;
        public static string gamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                    return null;
                return Path.GetFullPath(_gamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BioGame"))
                        value = value.Substring(0, value.LastIndexOf("BioGame"));
                }
                _gamePath = value;
            }
        }
        public static string bioGamePath => gamePath != null ? Path.Combine(gamePath, @"BioGame\") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath, @"BioGame\CookedPC\") : "Not Found";
        public static string CookedPath(GameTarget target) => Path.Combine(target.TargetPath, @"BioGame\CookedPC");
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"DLC\") : "Not Found";


        // "C:\...\MyDocuments\BioWare\Mass Effect\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect\");
        public static string GamerSettingsIniFile => Path.Combine(BioWareDocPath, @"BIOGame\Config\GamerSettings.ini");

        static ME1Directory()
        {
            ReloadActivePath();
        }

        public static void ReloadActivePath()
        {
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect";

            string keyName = hkey32 + subkey;
            string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
            if (test != null)
            {
                gamePath = test;
                return;
            }

            keyName = hkey64 + subkey;
            gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
        }

        public static string ExecutablePath(string gameRoot) => Path.Combine(gameRoot, "Binaries", "MassEffect.exe");


        public static Dictionary<string, string> OfficialDLCNames = new Dictionary<string, string>
        {
            ["DLC_UNC"] = "Bring Down the Sky",
            ["DLC_Vegas"] = "Pinnacle Station"
        };

        public static List<string> OfficialDLC = new List<string>
        {
            "DLC_UNC",
            "DLC_Vegas"
        };
    }
}
