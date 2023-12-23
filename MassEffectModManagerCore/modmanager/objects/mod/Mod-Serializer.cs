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
                {MODDESC_DESCRIPTOR_MODMANAGER_CMMVER, App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture)}, // Is set read only in mapper
                {MODDESC_DESCRIPTOR_MODMANAGER_MINBUILD, MinimumSupportedBuild > 102 ? MinimumSupportedBuild.ToString() : null}, // MinimumSupportBuild was only supported starting in build 102.
            };

            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, MODDESC_HEADERKEY_MODMANAGER));

            // ModInfo
            parameterDictionary = new Dictionary<string, object>()
            {
                {MODDESC_DESCRIPTOR_MODINFO_GAME, Game.ToString().ToUpper()}, // Upper for LELAUNCHER
                {MODDESC_DESCRIPTOR_MODINFO_NAME, ModName},
                {MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION, ModDescription},
                {MODDESC_DESCRIPTOR_MODINFO_VERSION, ParsedModVersion},
                {MODDESC_DESCRIPTOR_MODINFO_DEVELOPER, ModDeveloper},
                {MODDESC_DESCRIPTOR_MODINFO_SITE, ModWebsite == Mod.DefaultWebsite ? "" : ModWebsite},
                {MODDESC_DESCRIPTOR_MODINFO_UPDATECODE, ModClassicUpdateCode > 0 ? ModClassicUpdateCode.ToString() : null},
                {MODDESC_DESCRIPTOR_MODINFO_NEXUSMODSDOMAINID, NexusModID > 0 ? NexusModID.ToString() : null},
                {MODDESC_DESCRIPTOR_MODINFO_REQUIREDDLC, string.Join(';',RequiredDLC.Select(x=>x.Serialize(false)).Concat(OptionalSingleRequiredDLC.Select(x=>x.Serialize(true))))},
                {MODDESC_DESCRIPTOR_MODINFO_BANNERIMAGENAME, new MDParameter(@"string", BannerImageName, BannerImageName, new [] {@""}, @"") {Header = MODDESC_HEADERKEY_MODINFO, AllowedValuesPopulationFunc = PopulateImageFileOptions}}, // Uses image population function // do not localize
                {MODDESC_DESCRIPTOR_MODINFO_SORTALTERNATES, new MDParameter(@"string", MODDESC_DESCRIPTOR_MODINFO_SORTALTERNATES, SortAlternateOptions ? @"" : MODDESC_VALUE_FALSE, new [] {@"", MODDESC_VALUE_TRUE, MODDESC_VALUE_FALSE}, @"") {Header = MODDESC_HEADERKEY_MODINFO}}, //don't put checkedbydefault in if it is not set to true. // do not localize
                {MODDESC_DESCRIPTOR_MODINFO_REQUIRESENHANCEDBINK, new MDParameter(@"string", MODDESC_DESCRIPTOR_MODINFO_REQUIRESENHANCEDBINK, !RequiresEnhancedBink ? @"" : MODDESC_VALUE_TRUE, new [] {@"", MODDESC_VALUE_TRUE, MODDESC_VALUE_FALSE}, @"") {Header = MODDESC_HEADERKEY_MODINFO}}, // don't populate if not used // do not localize
            };


            // NON PUBLIC OPTIONS
            if (RequiresAMD)
            {
                parameterDictionary[Mod.MODDESC_DESCRIPTOR_MODINFO_REQUIRESAMD] = RequiresAMD;
            }

            if (!string.IsNullOrWhiteSpace(PostInstallToolLaunch))
            {
                // This is a non-public property but is used by one mod
                parameterDictionary[MODDESC_DESCRIPTOR_MODINFO_POSTINSTALLTOOL] = PostInstallToolLaunch;
            }
            // END NON PUBLIC OPTIONS

            if (Game is MEGame.ME2 or MEGame.ME3)
            {
                // This flag only makes a difference for ME2/3
                parameterDictionary[Mod.MODDESC_DESCRIPTOR_MODINFO_COMPRESSPACKAGESBYDEFAULT] = PreferCompressed ? MODDESC_VALUE_TRUE : null;
            }

            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, MODDESC_HEADERKEY_MODINFO));

            // UPDATES
            parameterDictionary = new Dictionary<string, object>()
            {
                {MODDESC_DESCRIPTOR_UPDATES_SERVERFOLDER, UpdaterServiceServerFolder},
                {MODDESC_DESCRIPTOR_UPDATES_BLACKLISTEDFILES, UpdaterServiceBlacklistedFiles},
                {MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FOLDERS, AdditionalDeploymentFolders},
                {MODDESC_DESCRIPTOR_UPDATES_ADDITIONAL_FILES, AdditionalDeploymentFiles},
            };

            ParameterMap.AddRange(MDParameter.MapIntoParameterMap(parameterDictionary, MODDESC_HEADERKEY_UPDATES));
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
