using System;
using System.IO;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmanager.merge.dlc
{
    public class M3MergeDLC
    {
        #region INSTANCE
        /// <summary>
        /// Target this merge DLC is for.
        /// </summary>
        public GameTargetWPF Target { get; set; }

        /// <summary>
        /// The location where the MergeDLC should exist for the target this instanced was created with.
        /// </summary>
        public string MergeDLCPath { get; set; }

        /// <summary>
        /// Generates information about a merge DLC for a target. Use the static methods to access a game's information about a merge.
        /// </summary>
        /// <param name="target"></param>
        public M3MergeDLC(GameTargetWPF target)
        {
            Target = target;
            MergeDLCPath = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME);
        }
        #endregion

        public const string MERGE_DLC_FOLDERNAME = @"DLC_MOD_M3_MERGE";
        private const string MERGE_DLC_GUID_ATTRIBUTE_NAME = @"MergeDLCGUID";

        /// <summary>
        /// Removes a merge DLC from a target if it exists.
        /// </summary>
        /// <param name="target"></param>
        public static void RemoveMergeDLC(GameTargetWPF target)
        {
            var mergePath = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME);
            if (Directory.Exists(mergePath))
            {
                M3Utilities.DeleteFilesAndFoldersRecursively(mergePath);
            }
        }

        /// <summary>
        /// Gets the current GUID of the merge DLC. This can be used to track a unique instance of a merge DLC.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static Guid? GetCurrentMergeGuid(GameTargetWPF target)
        {
            var metaPath = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME, @"_metacmm.txt");
            if (File.Exists(metaPath))
            {
                MetaCMM m = new MetaCMM(metaPath);
                if (m.ExtendedAttributes.TryGetValue(MERGE_DLC_GUID_ATTRIBUTE_NAME, out var guidStr))
                {
                    try
                    {
                        return new Guid(guidStr);
                    }
                    catch
                    {
                        M3Log.Warning($@"Could not convert to M3 Merge Guid: {guidStr}. Will return null guid");
                    }
                }
            }

            return null; // Not found!
        }

        /// <summary>
        /// Generates a new merge DLC folder and assigns it a new Guid.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="guid"></param>
        public void GenerateMergeDLC()
        {
            // Generate M3 DLC Folder

            // Does not work for LE1/ME1!
            var sko = new StarterKitGeneratorWindow.StarterKitOptions()
            {
                ModGame = Target.Game,
                GenerateModdesc = false,
                OutputFolderOverride = M3Directories.GetDLCPath(Target),
                ModDescription = null,
                ModInternalName = @"ME3Tweaks Mod Manager Merge DLC",
                ModInternalTLKID = 1928304430,
                ModMountFlag = Target.Game.IsGame3() ? new MountFlag(EME3MountFileFlag.LoadsInSingleplayer) : new MountFlag(0, true),
                ModDeveloper = @"ME3Tweaks Mod Manager",
                ModMountPriority = 1900000000,
                ModDLCFolderNameSuffix = MERGE_DLC_FOLDERNAME.Substring(@"DLC_MOD_".Length),
                ModModuleNumber = 48955 // GAME 2
            };

            StarterKitGeneratorWindow.CreateStarterKitMod(sko, null);
            MetaCMM mcmm = new MetaCMM()
            {
                ModName = @"ME3Tweaks Mod Manager Auto-Generated Merge DLC",
                Version = @"1.0",
                ExtendedAttributes =
                {
                    { MERGE_DLC_GUID_ATTRIBUTE_NAME, Guid.NewGuid().ToString() }// A new GUID is generated
                }
            };
            mcmm.WriteMetaCMM(Path.Combine(M3Directories.GetDLCPath(Target), MERGE_DLC_FOLDERNAME, @"_metacmm.txt"), App.AppVersionHR);

            Generated = true;
        }

        /// <summary>
        /// If the merge DLC was generated
        /// </summary>
        public bool Generated { get; set; }

        public const int STARTING_CONDITIONAL = 10000;
        public const int STARTING_TRANSITION = 90000;

        /// <summary>
        /// These can be used by MUST BE INCREMENTED ON USE
        /// </summary>
        public int CurrentConditional = STARTING_CONDITIONAL;
        /// <summary>
        /// These can be used by MUST BE INCREMENTED ON USE
        /// </summary>
        public int CurrentTransition = STARTING_TRANSITION;

        /// <summary>
        /// Adds the plot data into the config folder if necessary
        /// </summary>
        /// <param name="mergeDLC"></param>
        public static void AddPlotDataToConfig(M3MergeDLC mergeDLC)
        {
            var configBundle = ConfigAssetBundle.FromDLCFolder(mergeDLC.Target.Game, Path.Combine(mergeDLC.Target.GetDLCPath(), M3MergeDLC.MERGE_DLC_FOLDERNAME, mergeDLC.Target.Game.CookedDirName()), M3MergeDLC.MERGE_DLC_FOLDERNAME);

            // add startup file
            var bioEngine = configBundle.GetAsset(@"BIOEngine.ini");
            var startupSection = bioEngine.GetOrAddSection(@"Engine.StartupPackages");
            startupSection.AddEntryIfUnique(new CoalesceProperty(@"DLCStartupPackage", new CoalesceValue($@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}", CoalesceParseAction.AddUnique)));
            if (mergeDLC.Target.Game.IsGame2())
            {
                // Game 3 uses Conditionals.cnd
                // Game 1 merges into basegame directly
                startupSection.AddEntryIfUnique(new CoalesceProperty(@"Package",
                    new CoalesceValue($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}",
                        CoalesceParseAction.AddUnique)));
                startupSection.AddEntryIfUnique(new CoalesceProperty(@"Package",
                    new CoalesceValue($@"PlotManagerAuto{M3MergeDLC.MERGE_DLC_FOLDERNAME}",
                        CoalesceParseAction.AddUnique)));


                // Add conditionals 
                var bioGame = configBundle.GetAsset(@"BIOGame.ini");
                var bioWorldInfoConfig = bioGame.GetOrAddSection(@"SFXGame.BioWorldInfo");
                bioWorldInfoConfig.AddEntryIfUnique(new CoalesceProperty(@"ConditionalClasses",
                    new CoalesceValue($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals",
                        CoalesceParseAction.AddUnique)));
            }

            configBundle.CommitDLCAssets();
        }
    }
}
