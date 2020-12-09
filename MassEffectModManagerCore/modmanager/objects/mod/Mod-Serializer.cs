using System.Collections.Generic;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod : IMDParameterMap
    {
        // Class for editing the mod in moddesc.ini editor and related variables

        /// <summary>
        /// If the mod specified the modcoal flag (MD 2.0 only)
        /// </summary>
        public bool LegacyModCoal { get; set; }

        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        public void BuildParameterMap(Mod _)
        {
            ParameterMap.ClearEx();

            var parameterDictionary = new Dictionary<string, object>()
            {
                // ModManager
                {@"cmmver", ModDescTargetVersion},
                {@"minbuild", MinimumSupportedBuild > 102 ? MinimumSupportedBuild.ToString() : null},
            };
            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, @"ModManager"));

            // ModInfo
            parameterDictionary = new Dictionary<string, object>()
            {
                {@"game", Game},
                {@"modname", ModName},
                {@"moddesc", ModDescription},
                {@"modver", ParsedModVersion},
                {@"moddev", ModDeveloper},
                {@"modsite", ModWebsite == Mod.DefaultWebsite ? "" : ModWebsite},
                {@"updatecode", ModClassicUpdateCode > 0 ? ModClassicUpdateCode.ToString() : null},
                {@"nexuscode", NexusModID > 0 ? NexusModID.ToString() : null},
                {@"requireddlc", RequiredDLC},
                {@"bannerimagename", BannerImageName},
            };

            if (Game > MEGame.ME1)
            {
                // This flag only makes a difference for ME1
                parameterDictionary[@"prefercompressed"] = PreferCompressed ? "True" : null;
            }

            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, @"ModInfo"));

            // UPDATES
            parameterDictionary = new Dictionary<string, object>()
                {
                {@"serverfolder", UpdaterServiceServerFolder},
                {@"blacklistedfiles", UpdaterServiceBlacklistedFiles},
                {@"additionaldeploymentfolders", AdditionalDeploymentFolders},
                {@"additionaldeploymentfiles", AdditionalDeploymentFiles},
            };

            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, @"UPDATES"));
        }
    }
}
