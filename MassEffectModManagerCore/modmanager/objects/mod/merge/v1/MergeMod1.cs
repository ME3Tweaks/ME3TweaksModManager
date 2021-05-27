using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    /// <summary>
    /// Merge Mod V1. Merges properties, objects
    /// </summary>
    public class MergeMod1 : IMergeMod
    {
        private const string MMV1_ASSETMAGIC = @"MMV1";
        [JsonIgnore]
        public string MergeModFilename { get; set; }

        [JsonProperty("game")]
        public MEGame Game { get; set; } // Only used for sanity check

        [JsonProperty("files")]
        public List<MergeFile1> FilesToMergeInto;

        [JsonIgnore]
        public CaseInsensitiveDictionary<MergeAsset> Assets;

        private MergeMod1() { }
        public static MergeMod1 ReadMergeMod(Stream mergeFileStream, string mergeModName, bool loadAssets)
        {
            // Version and magic will already be read by main value
            var manifest = mergeFileStream.ReadUnrealString();
            var mm = JsonConvert.DeserializeObject<MergeMod1>(manifest);
            MemoryAnalyzer.AddTrackedMemoryItem($"MergeMod1 {mergeModName}", new WeakReference(mm));
            mm.MergeModFilename = mergeModName;

            // setup links
            foreach (var ff in mm.FilesToMergeInto)
            {
                ff.SetupParent(mm);
                ff.Validate();
            }

            var assetCount = mergeFileStream.ReadInt32();
            if (assetCount > 0)
            {
                for (int i = 0; i < assetCount; i++)
                {
                    var assetMag = mergeFileStream.ReadStringASCII(4);
                    if (assetMag != MMV1_ASSETMAGIC)
                    {
                        throw new Exception("Merge Mod V1 asset not prefixed by asset magic - this file is not properly built!");
                    }

                    MergeAsset ma = new MergeAsset();
                    var assetName = mergeFileStream.ReadUnrealString();
                    ma.FileSize = mergeFileStream.ReadInt32();
                    if (loadAssets)
                    {
                        // Read now
                        ma.ReadAssetBinary(mergeFileStream);
                    }
                    else
                    {
                        // Will load at install time
                        ma.FileOffset = (int)mergeFileStream.Position;
                        mergeFileStream.Skip(ma.FileSize);
                    }

                    mm.Assets ??= new CaseInsensitiveDictionary<MergeAsset>();
                    mm.Assets[assetName] = ma;
                }
            }

            return mm;
        }

        public bool ApplyMergeMod(Mod associatedMod, GameTarget target, ref int numTotalDone, int numTotalMerges, Action<int, int> mergeProgressDelegate = null)
        {
            Log.Information($@"Applying {MergeModFilename}");
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, true, gameRootOverride: target.TargetPath);

            int numDone = 0;
            foreach (var mf in FilesToMergeInto)
            {
                mf.ApplyChanges(loadedFiles, associatedMod, ref numTotalDone, numTotalMerges, mergeProgressDelegate);
                numDone++;
            }

            return true;
        }

        public int GetMergeCount() => FilesToMergeInto.Sum(x=>x.GetMergeCount());

        public static void SerializeTest(Stream outStream, string manifestFile)
        {

            var sourceDir = Directory.GetParent(manifestFile).FullName;

            var manifestText = File.ReadAllText(manifestFile);
            var mm = JsonConvert.DeserializeObject<MergeMod1>(manifestText);

            // Update manifest

            SortedSet<string> assets = new();
            // Get all assets.
            foreach (var fc in mm.FilesToMergeInto)
            {
                foreach (var mc in fc.MergeChanges)
                {
                    if (mc.AssetUpdate?.AssetName != null)
                    {
                        if (!File.Exists(Path.Combine(sourceDir, mc.AssetUpdate.AssetName)))
                        {
                            throw new Exception($"Asset does not exist in folder: {mc.AssetUpdate.AssetName}");
                        }

                        assets.Add(mc.AssetUpdate.AssetName);
                    }

                    if (mc.ScriptUpdate?.ScriptFileName != null)
                    {
                        var scriptDiskFile = Path.Combine(sourceDir, mc.ScriptUpdate.ScriptFileName);
                        if (!File.Exists(scriptDiskFile))
                        {
                            throw new Exception($"Script does not exist in folder: {mc.ScriptUpdate.ScriptFileName}");
                        }

                        mc.ScriptUpdate.ScriptText = File.ReadAllText(scriptDiskFile);
                    }
                }
            }

            outStream.WriteUnrealStringUnicode(JsonConvert.SerializeObject(mm, Formatting.None));

            outStream.WriteInt32(assets.Count);
            foreach (var asset in assets)
            {
                outStream.WriteStringASCII(MMV1_ASSETMAGIC); // MAGIC
                outStream.WriteUnrealStringUnicode(asset); // ASSET NAME
                var assetBytes = File.ReadAllBytes(Path.Combine(sourceDir, asset));
                outStream.WriteInt32(assetBytes.Length); // ASSET LENGTH
                outStream.Write(assetBytes); // ASSET DATA
            }
        }
    }
}
