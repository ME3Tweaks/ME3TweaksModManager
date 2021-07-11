using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace MassEffectModManagerCore.modmanager.save.game2.UI
{
    [Localizable(false)]
    public class LevelNameStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return StaticConvert(str);
            }

            return $"lmao idk";
        }

        public static string StaticConvert(string str)
        {
            return str.ToLower() switch
            {
                "biop_nor" => "Normandy SR-2",
                "biop_cithub" => "Citadel - Zakera Ward",
                "biop_krohub" => "Tuchanka - Urdnot Camp",
                "biop_omghub" => "Omega - Merchant District",
                "biop_twrhub" => "Illium - Nos Astra",
                "biop_pronor" => "Normandy SR-1",
                "biop_procer" => "Lazarus Research Station",
                "biop_profre" => "Freedom's Progress",
                "biop_horcr1" => "Horizon",
                "biop_shpcr2" => "Collector Vessel",
                "biop_rprgta" => "Derelict Reaper",
                "biop_endgm1" => "Tartarus Debris Field",
                "biop_endgm2" => "Collector Station",
                "biop_endgm3" => "Suicide Mission Epilogue",
                "biop_bchlml" => "Aeia - Hugo Gernsback Crash Site",
                "biop_blbgtl" => "Heretic Station",
                "biop_jnkkga" => "Korlus - Blue Suns Facility",
                "biop_omggra" => "Omega - Archangel's Base",
                "biop_omgpra" => "Omega - Slums District",
                "biop_suntla" => "Haestrom - Quarian Landing Zone",
                "biop_twrasa" => "Illium - Dantius Towers",
                "biop_twrmwa" => "llium - Commercial Spaceport",
                "biop_citasl" => "Citadel - 800 Blocks",
                "biop_citgrl" => "Citadel - Factory District",
                "biop_juncvl" => "Pragia - Teltin Facility",
                "biop_krokgl" => "Tuchanka - Urdnot Ruins",
                "biop_kroprl" => "Tuchanka - Weyrloc Facility",
                "biop_prscva" => "Prison Ship Purgatory",
                "biop_ptymtl" => "Bekenstein - Hock's Party",
                "biop_quatll" => "Migrant Fleet",
                "biop_twrvxl" => "Illium - Transport Station",
                "biop_zyavtl" => "Zorya - Eldfell-Ashland Refinery",
                "biop_unc1explore" => "Aite",
                "biop_unc1base1" => "Aite - Hermes Station",
                "biop_unc1base2" => "Aite - Vulcan Station",
                "biop_unc1base3" => "Aite - Prometheus Station",
                "biop_unc1base4" => "Aite - Atlas Station",
                "biop_exp1lvl1" => "Illium - Market District",
                "biop_exp1lvl2" => "Illium - Hotel Azure",
                "biop_exp1lvl3" => "Hagalaz - Ship Exterior",
                "biop_exp1lvl4" => "Hagalaz - Ship Base",
                "biop_exp1lvl5" => "Hagalaz - Broker Base",
                "biop_arvlvl1" => "Aratoht",
                "biop_arvlvl4" => "Project Base - Station Port",
                "biop_arvlvl3" => "Project Base - Med Bay",
                "biop_arvlvl2" => "Project Base - Generator Room",
                "biop_arvlvl5" => "Project Base - Station Exterior",
                "biop_n7bldinv1" => "Tarith - Blood Pack Comm Relay",
                "biop_n7bldinv2" => "Zada Ban - Blood Pack Base",
                "biop_n7crsh" => "Zanethu - MSV Estevanico Site",
                "biop_n7driveby" => "Corang",
                "biop_n7geth1" => "Lattesh",
                "biop_n7geth2" => "Canalus",
                "biop_n7ruins" => "Kopis - Dig Site",
                "biop_n7shipwreck" => "Zeona - Firewalker",
                "biop_n7spdr1" => "Joab - Dig Site",
                "biop_n7spdr2" => "MSV Strontium Mule",
                "biop_n7spdr3" => "Sanctum - Blue Suns Base",
                "biop_n7viq1" => "Neith - MSV Corsica Site",
                "biop_n7viq2" => "Jarrahe Station",
                "biop_n7viq3" => "Capek - Hahne-Kedar Facility",
                "biop_n7mine" => "Helyme - Eldfell-Ashland Facility",
                "biop_n7mmnt1" => "Daratar - Eclipse Cache",
                "biop_n7mmnt2" => "Gei Hinnom - Quarian Crash Site",
                "biop_n7mmnt3" => "Taitus - Mining the Canyon",
                "biop_n7mmnt4" => "MSV Broken Arrow",
                "biop_n7mmnt5" => "Lorek - Eclipse Base",
                "biop_n7mmnt6" => "Sinmara - Magnetic Shield Generator",
                "biop_n7mmnt7" => "Franklin - Javelin Mk.II Silo",
                "biop_n7mmnt8" => "Karumto",
                "biop_n7mmnt10" => "Aequitas - Mining Facility",
                "biop_n7norcrash" => "Alchera - Normandy SR-1 Site",
                _ => $"Unknown map: {str}",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}