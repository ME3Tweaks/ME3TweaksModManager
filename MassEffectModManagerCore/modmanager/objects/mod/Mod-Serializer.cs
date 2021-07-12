using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Misc;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod : IMDParameterMap, IEquatable<Mod>
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
                {@"game", Game.ToString().ToUpper()}, // Upper for LELAUNCHER
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


            // NON PUBLIC OPTIONS
            if (RequiresAMD)
            {
                parameterDictionary[@"amdprocessoronly"] = RequiresAMD;
            }

            if (!string.IsNullOrWhiteSpace(PostInstallToolLaunch))
            {
                // This is a non-public property but is used by one mod
                parameterDictionary[@"postinstalltool"] = PostInstallToolLaunch;
            }
            // END NON PUBLIC OPTIONS
            
            if (Game > MEGame.ME1)
            {
                // This flag only makes a difference for ME2/3
                parameterDictionary[@"prefercompressed"] = PreferCompressed ? @"True" : null;
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

        public bool Equals(Mod other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (ModPath == null) return false; // RCW mods will not set a mod path
            return ModPath.Equals(other.ModPath, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Mod) obj);
        }

        public override int GetHashCode()
        {
            return (ModPath != null ? ModPath.GetHashCode() : 0);
        }
    }
}
