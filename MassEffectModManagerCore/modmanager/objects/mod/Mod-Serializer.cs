using System;
using System.Collections.Generic;
using System.Text;
using IniParser.Model;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        /// <summary>
        /// Generates the corresponding moddesc.ini text for this mod
        /// </summary>
        /// <returns></returns>
        public string SerializeModdesc()
        {
            IniData moddessc = new IniData();
            moddessc[@"ModManager"][@"cmmver"] = ModDescTargetVersion.ToString();
            if (MinimumSupportedBuild > 0)
            {
                moddessc[@"ModManager"][@"minbuild"] = MinimumSupportedBuild.ToString();
            }

            moddessc[@"ModInfo"][@"modname"] = ModName;
            moddessc[@"ModInfo"][@"game"] = Game.ToString();
            moddessc[@"ModInfo"][@"moddev"] = ModDeveloper;
            moddessc[@"ModInfo"][@"modver"] = ModVersionString;
            moddessc[@"ModInfo"][@"modsite"] = ModWebsite;
            moddessc[@"ModInfo"][@"moddesc"] = Utilities.ConvertNewlineToBr(ModDescription);

            foreach (var job in InstallationJobs)
            {
                job.Serialize(moddessc);
            }


            return moddessc.ToString();

        }
    }
}
