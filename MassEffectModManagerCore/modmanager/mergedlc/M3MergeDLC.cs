using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.windows;

namespace MassEffectModManagerCore.modmanager.mergedlc
{
    public class M3MergeDLC
    {
        public const string MERGE_DLC_FOLDERNAME = @"DLC_MOD_M3_MERGE";
        public static void RemoveMergeDLC(GameTarget target)
        {
            var mergePath = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME);
            if (Directory.Exists(mergePath))
            {
                Utilities.DeleteFilesAndFoldersRecursively(mergePath);
            }
        }

        public static void GenerateMergeDLC(GameTarget target, Guid guid)
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
        }
    }
}
