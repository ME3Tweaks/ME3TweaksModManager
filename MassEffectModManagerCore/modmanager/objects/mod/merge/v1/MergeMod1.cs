using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    /// <summary>
    /// Merge Mod V1. Merges properties, objects
    /// </summary>
    public class MergeMod1 : IMergeMod
    {
        private const string MMV1_ASSETMAGIC = "MMV1";

        public MEGame Game; // Only used for sanity check

        [JsonProperty("files")]
        public List<MergeFile1> FilesToMergeInto;

        [JsonProperty("assets")]
        public List<MergeAsset> Assets;

        private MergeMod1() { }
        public static MergeMod1 ReadMergeMod(Stream mergeFileStream)
        {
            // Version and magic will already be read by main value
            var manifest = mergeFileStream.ReadUnrealString();
            var mm = JsonConvert.DeserializeObject<MergeMod1>(manifest);

            if (mm.Assets != null)
            {
                var assets = mm.Assets.OrderBy(x => x.FileIndex).ToList();
                for (int i = 0; i < assets.Count; i++)
                {
                    if (mergeFileStream.ReadStringASCII(4) != MMV1_ASSETMAGIC)
                    {
                        throw new Exception("Merge Mod V1 asset not prefixed by asset magic - this file is not properly built!");
                    }
                }
            }

            return mm;
        }

        public bool ApplyMergeMod(GameTarget target)
        {
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, true, gameRootOverride: target.TargetPath);

            foreach (var mf in FilesToMergeInto)
            {
                if (loadedFiles.TryGetValue(mf.FileName, out var fullpath))
                {
                    var package = MEPackageHandler.OpenMEPackage(fullpath);
                    foreach (var pc in mf.MergeChanges)
                    {
                        pc.ApplyChanges(package, this);
                    }
                    package.Save(compress: true);
                }
            }

            return true;
        }

        public static void SerializeTest(Stream outStream)
        {
            outStream.WriteUnrealStringUnicode(File.ReadAllText(@"C:\Users\Mgame\Desktop\fps_le2.json")); // manifest
        }
    }
}
