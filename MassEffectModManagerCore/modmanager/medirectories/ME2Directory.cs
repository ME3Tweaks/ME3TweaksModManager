using System;
using System.IO;
using System.Collections.Generic;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.GameDirectories

{
    public static class ME2Directory
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
                    if (value.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BioGame", StringComparison.OrdinalIgnoreCase));
                }
                _gamePath = value;
            }
        }

        public static string BioGamePath => gamePath != null ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? gamePath : Path.Combine(gamePath, @"BioGame\") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath,  @"BioGame\CookedPC\") : "Not Found";
        public static string CookedPath(GameTarget target) => Path.Combine(target.TargetPath, @"BioGame\CookedPC");


        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"BioGame\DLC\") : "Not Found";

        // "C:\...\MyDocuments\BioWare\Mass Effect 2\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect 2\");
        public static string GamerSettingsIniFile => BioWareDocPath + @"BIOGame\Config\GamerSettings.ini";
        public static string ExecutablePath(string gameRoot) => Path.Combine(gameRoot, "Binaries", "MassEffect2.exe");

        static ME2Directory()
        {
            //if (!string.IsNullOrEmpty(Properties.Settings.Default.ME2Directory))
            //{
            //    gamePath = Properties.Settings.Default.ME2Directory;
            //}
            //else
            //{
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect 2";

                string keyName = hkey32 + subkey;
                string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
                if (test != null)
                {
                    gamePath = test;
                    return;
                }

                /*if (gamePath != null)
                {
                    gamePath = gamePath + "\\";
                    return;
                }*/

                keyName = hkey64 + subkey;
                gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
                if (gamePath != null)
                {
                    gamePath = gamePath + "\\";
                    return;
                } 
            //}
        }

        public static Dictionary<string, string> OfficialDLCNames = new Dictionary<string, string>
        {
            ["DLC_CER_02"] = "Aegis Pack",
            ["DLC_CER_Arc"] = "Arc Projector",
            ["DLC_CON_Pack01"] = "Alternate Appearance Pack 1",
            ["DLC_CON_Pack02"] = "Alternate Appearance Pack 2",
            ["DLC_DHME1"] = "Genesis",
            ["DLC_EXP_Part01"] = "Lair of the Shadow Broker",
            ["DLC_EXP_Part02"] = "Arrival",
            ["DLC_HEN_MT"] = "Kasumi - Stolen Memory",
            ["DLC_HEN_VT"] = "Zaeed - The Price of Revenge",
            ["DLC_MCR_01"] = "Firepower pack",
            ["DLC_MCR_03 "] = "Equalizer pack",
            ["DLC_PRE_Cerberus"] = "Cerberus Weapon and Armor",
            ["DLC_PRE_Collectors"] = "Collectors' Weapon and Armor",
            ["DLC_PRE_DA"] = "Blood Dragon Armor",
            ["DLC_PRE_Gamestop"] = "Terminus Weapon and Armor",
            ["DLC_PRE_General"] = "Inferno Armor",
            ["DLC_PRE_Incisor"] = "M-29 Incisor",
            ["DLC_PRO_Gulp01"] = "Sentry Interface",
            ["DLC_PRO_Pepper01"] = "Umbra Visor",
            ["DLC_PRO_Pepper02"] = "Recon Hood",
            ["DLC_UNC_Hammer01"] = "Firewalker Pack",
            ["DLC_UNC_Moment01"] = "Normandy Crash Site",
            ["DLC_UNC_Pack01"] = "Overlord",
        };

        public static List<string> OfficialDLC = new List<string>
        {
            "DLC_UNC_Moment01", //95
            "DLC_HEN_VT", //100
            "DLC_PRE_Cerberus", //105
            "DLC_PRE_Collectors", //106
            "DLC_PRE_DA", //107
            "DLC_PRE_Gamestop", //108
            "DLC_PRE_General",
            "DLC_PRE_Incisor",
            "DLC_PRO_Gulp01", //111
            "DLC_PRO_Pepper01", //112
            "DLC_PRO_Pepper02", //113
            "DLC_CER_Arc", //116
            "DLC_UNC_Hammer01", //118
            "DLC_HEN_MT", //119
            "DLC_CON_Pack01", //125
            "DLC_UNC_Pack01", //132
            "DLC_CER_02",
            "DLC_MCR_01", //136
            "DLC_MCR_03",
            "DLC_EXP_Part01", //300
            "DLC_DHME1", //375
            "DLC_CON_Pack02", //380
            "DLC_EXP_Part02", //400
        };
    }
}
