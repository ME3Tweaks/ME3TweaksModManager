using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.objects.mod.editor;

namespace ME3TweaksModManager.modmanager.objects.mod
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
                // The editor only supports saving to the current moddesc spec. So don't show the wrong version that will be edited.
                {@"cmmver", App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture)}, // Is set read only in mapper
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
                {@"requireddlc", string.Join(';',RequiredDLC.Select(x=>x.Serialize(false)).Concat(OptionalSingleRequiredDLC.Select(x=>x.Serialize(true))))},
                {@"bannerimagename", BannerImageName},
                {@"sortalternates", new MDParameter(@"string", @"sortalternates", SortAlternateOptions ? @"" : @"False", new [] {@"", @"True", @"False"}, "") {Header = @"ModInfo"}}, //don't put checkedbydefault in if it is not set to true. // do not localize
                {@"requiresenhancedbink", new MDParameter(@"string", @"requiresenhancedbink", !RequiresEnhancedBink ? @"" : @"False", new [] {@"", @"True", @"False"}, "") {Header = @"ModInfo"}}, // don't populate if not used
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

            if (Game is MEGame.ME2 or MEGame.ME3)
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
            return Equals((Mod)obj);
        }

        public override int GetHashCode()
        {
            return (ModPath != null ? ModPath.GetHashCode() : 0);
        }
    }
}
