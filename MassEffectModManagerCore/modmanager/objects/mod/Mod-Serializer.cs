using System.Collections.Generic;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod : IMDParameterMap
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

        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        public void BuildParameterMap()
        {
            var parameterDictionary = new Dictionary<string, object>()
            {
                // ModManager
                {@"cmmver", ModDescTargetVersion},
                {@"minbuild", MinimumSupportedBuild},

                // ModInfo
                {@"game", Game},
                {@"moddesc", ModDescription},
                {@"modver", ParsedModVersion},
                {@"moddev", ModDeveloper},
                {@"modsite", ModWebsite},
                {@"nexuscode", NexusModID},
                {@"requireddlc", RequiredDLC},
                {@"prefercompressed", PreferCompressed},
                {@"bannerimagename", BannerImageName},

                // UPDATES
                {@"serverfolder", UpdaterServiceServerFolder},
                {@"blacklistedfiles", UpdaterServiceBlacklistedFiles},
                {@"additionaldeploymentfolders", AdditionalDeploymentFolders},
                {@"additionaldeploymentfiles", AdditionalDeploymentFiles}, // List of relative paths
                {@"updatecode", ModClassicUpdateCode}, // List of relative sizes
            };

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }
    }
}
