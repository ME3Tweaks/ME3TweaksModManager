using System;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCoreWPF;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmanager.merge.dlc
{
    public class M3MergeDLC
    {
        public const string MERGE_DLC_FOLDERNAME = @"DLC_MOD_M3_MERGE";
        public static void RemoveMergeDLC(GameTargetWPF target)
        {
            var mergePath = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME);
            if (Directory.Exists(mergePath))
            {
                M3Utilities.DeleteFilesAndFoldersRecursively(mergePath);
            }
        }

        public static void GenerateMergeDLC(GameTargetWPF target, Guid guid)
        {
            // Generate M3 DLC Folder

            // Does not work for LE1/ME1!
            var sko = new StarterKitGeneratorWindow.StarterKitOptions()
            {
                ModGame = target.Game,
                GenerateModdesc = false,
                OutputFolderOverride = M3Directories.GetDLCPath(target),
                ModDescription = null,
                ModInternalName = @"ME3Tweaks Mod Manager Merge DLC",
                ModInternalTLKID = 1928304430,
                ModMountFlag = target.Game.IsGame3() ? new MountFlag(EME3MountFileFlag.LoadsInSingleplayer) : new MountFlag(0, true),
                ModDeveloper = @"ME3Tweaks Mod Manager",
                ModMountPriority = 1900000000,
                ModDLCFolderNameSuffix = MERGE_DLC_FOLDERNAME.Substring(@"DLC_MOD_".Length)
            };

            StarterKitGeneratorWindow.CreateStarterKitMod(sko, null);
            MetaCMM mcmm = new MetaCMM()
            {
                ModName = @"ME3Tweaks Mod Manager Auto-Generated Merge DLC",
                Version = @"1.0",
                ExtendedAttributes =
                {
                    { @"MergeDLCGUID", Guid.NewGuid().ToString() }// A new GUID is generated
                }
            };
            mcmm.WriteMetaCMM(Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME, @"_metacmm.txt"), App.AppVersionHR);
        }
    }
}
