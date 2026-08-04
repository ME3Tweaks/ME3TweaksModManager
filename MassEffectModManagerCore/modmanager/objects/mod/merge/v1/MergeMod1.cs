using System.Diagnostics;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    /// <summary>
    /// Merge Mod V1. Merges properties, objects
    /// </summary>

    public class MergeMod1 : IMergeMod, IMergeModCommentable
    {
        /// <summary>
        /// The minimum size to compress in merge mod format 2. Used only in certain update classes.
        /// </summary>
        private const int TEXT_MIN_SIZE_TO_COMPRESS = 80;

        /// <summary>
        /// The data block identifier for merge mods in BFGIS. DO NOT CHANGE THIS.
        /// </summary>
        public const string MERGEMOD_BGFIS_DATA_BLOCK = @"BFGIS-MergeMods";

        /// <summary>
        /// The magic string for assets in merge mod v1
        /// </summary>
        private const string MMV1_ASSETMAGIC = @"MMV1";

        [JsonIgnore]
        public string MergeModFilename { get; set; }

        [JsonProperty(@"game")]
        [JsonConverter(typeof(StringEnumConverter))]
        public MEGame Game { get; set; } // Only used for sanity check

        [JsonProperty(@"files")]
        public List<MergeFile1> FilesToMergeInto;

        [JsonIgnore]
        public CaseInsensitiveDictionary<MergeAsset> Assets { get; set; }

        /// <summary>
        /// The version of the merge mod file format
        /// </summary>
        [JsonIgnore]
        public int MergeModVersion { get; set; }

        /// <summary>
        /// Reads an unreal string from the stream. Behavior changes based on the merge mod version.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="mergeMod"></param>
        /// <returns></returns>
        public static string ReadMergeModString(Stream stream, IMergeMod mergeMod)
        {
            return ReadMergeModString(stream, mergeMod.MergeModVersion);
        }

        /// <summary>
        /// Reads an unreal string from the stream. Behavior changes based on the merge mod version.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="mergeModVersion"></param>
        /// <returns></returns>
        public static string ReadMergeModString(Stream stream, int mergeModVersion)
        {
            if (mergeModVersion >= 2)
            {
                return stream.ReadCompressedUnrealString();
            }

            // V1 did not support compressed strings.
            return stream.ReadUnrealString();
        }

        /// <summary>
        /// Conditionally writes a compressed string to the stream if this is a v2 merge mod or newer
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="str">String to write</param>
        /// <param name="mergeModVersion"></param>
        /// <returns></returns>
        public static void WriteMergeModString(Stream stream, string str, int mergeModVersion)
        {
            if (mergeModVersion >= 2)
            {
                stream.WriteCompressedUnrealString(str);
                return;
            }

            // V1 did not support compressed strings.
            stream.WriteUnrealString(str, MEGame.LE3);
        }

        private MergeMod1() { }
        public static MergeMod1 ReadMergeMod(Stream mergeFileStream, string mergeModName, bool loadAssets, int mergeModVersion = 1, bool validate = true)
        {
            // Version and magic will already be read by main value
            string manifest = ReadMergeModString(mergeFileStream, mergeModVersion);

            var mm = JsonConvert.DeserializeObject<MergeMod1>(manifest);
            M3MemoryAnalyzer.AddTrackedMemoryItem($@"MergeMod1 {mergeModName}", mm);
            mm.MergeModFilename = mergeModName;
            mm.MergeModVersion = mergeModVersion;
            if (mergeFileStream is FileStream fs)
            {
                mm.LoadedFromPath = fs.Name;
                mm.SizeAtLoadTime = fs.Length;
            }

            // setup links
            foreach (var ff in mm.FilesToMergeInto)
            {
                ff.SetupParent(mm);
                if (validate)
                    ff.Validate();
            }

            var assetCount = mergeFileStream.ReadInt32();
            mm.Assets = new CaseInsensitiveDictionary<MergeAsset>(assetCount);
            if (assetCount > 0)
            {
                for (int i = 0; i < assetCount; i++)
                {
                    var assetMag = mergeFileStream.ReadStringASCII(4);
                    if (assetMag != MMV1_ASSETMAGIC)
                    {
                        throw new Exception(M3L.GetString(M3L.string_error_mergefile_badMagic));
                    }

                    MergeAsset ma = new MergeAsset() { OwningMM = mm };
                    var assetName = mergeFileStream.ReadUnrealString(); // Uncompressed as it will be short.

                    // Uncompressed asset size
                    ma.FileSize = mergeFileStream.ReadInt32();

                    // M3Mv2 - compression flag
                    if (mergeModVersion >= 2)
                    {
                        ma.IsCompressed = mergeFileStream.ReadBoolByte();
                    }

                    if (ma.IsCompressed)
                    {
                        // If asset is compressed, it will also list the compressed size of data to follow.
                        ma.CompressedSize = mergeFileStream.ReadInt32();
                    }

                    if (loadAssets)
                    {
                        // Read now
                        ma.ReadAssetBinary(mergeFileStream);
                    }
                    else
                    {
                        // Will load at install time
                        ma.FileOffset = (int)mergeFileStream.Position;
                        if (ma.IsCompressed)
                        {
                            mergeFileStream.Skip((uint)ma.CompressedSize);
                        }
                        else
                        {
                            mergeFileStream.Skip(ma.FileSize);
                        }
                    }

                    mm.Assets[assetName] = ma;
                }
            }

            if (mergeFileStream.Position != mergeFileStream.Length)
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_serialSizeMismatch, mergeFileStream.Position, mergeFileStream.Length));
            }

            return mm;
        }


        public bool ApplyMergeMod(MergeModPackage mmp, Action<int> mergeWeightDelegate)
        {
            M3Log.Information($@"Applying {MergeModFilename}.");
            Debug.WriteLine($@"Expected weight: {GetMergeWeight()}");

            int numDone = 0;
            foreach (var mf in FilesToMergeInto)
            {
                mf.ApplyChanges(mmp, mergeWeightDelegate);
                numDone++;
            }

            return true;
        }
        /// <summary>
        /// Gets the number of files to merge into
        /// </summary>
        /// <returns></returns>
        public int GetMergeCount() => FilesToMergeInto.Sum(x => x.GetMergeCount());
        /// <summary>
        /// Gets the weight of changes for more accurate progress tracking
        /// </summary>
        /// <returns></returns>
        public int GetMergeWeight() => FilesToMergeInto.Sum(x => x.GetMergeWeight());
        public void ExtractToFolder(string outputfolder)
        {
            // scripts and assets
            foreach (var mc in FilesToMergeInto.SelectMany(x => x.MergeChanges))
            {
                if (mc.PropertyUpdates is not null)
                {
                    foreach (PropertyUpdate1 propertyUpdate in mc.PropertyUpdates)
                    {
                        if (!string.IsNullOrEmpty(propertyUpdate.PropertyAsset))
                        {
                            File.WriteAllText(Path.Combine(outputfolder, Path.GetFileName(propertyUpdate.PropertyAsset)), propertyUpdate.PropertyValue);
                            propertyUpdate.PropertyValue = null;
                        }
                    }
                }

                if (mc.ScriptUpdate != null)
                {
                    File.WriteAllText(Path.Combine(outputfolder, Path.GetFileName(mc.ScriptUpdate.ScriptFileName)), mc.ScriptUpdate.GetScriptText());
                    mc.ScriptUpdate.ScriptText = null;
                }

                if (mc.AssetUpdate != null)
                {
                    File.WriteAllBytes(Path.Combine(outputfolder, Path.GetFileName(mc.AssetUpdate.AssetName)), Assets[mc.AssetUpdate.AssetName].AssetBinary);
                }

                if (mc.AddToClassOrReplace != null)
                {
                    for (int i = 0; i < mc.AddToClassOrReplace.ScriptFileNames.Length; i++)
                    {
                        var scriptname = mc.AddToClassOrReplace.ScriptFileNames[i];
                        var scripttext = mc.AddToClassOrReplace.GetScriptText(scriptname);
                        File.WriteAllText(Path.Combine(outputfolder, Path.GetFileName(scriptname)), scripttext);
                    }
                    mc.AddToClassOrReplace.Scripts = null;
                }

                if (mc.ClassUpdate != null)
                {
                    File.WriteAllBytes(Path.Combine(outputfolder, Path.GetFileName(mc.ClassUpdate.AssetName)), Assets[mc.ClassUpdate.AssetName].AssetBinary);
                }
            }

            // assets
            Assets = null;

            // json
            File.WriteAllText(Path.Combine(outputfolder, $@"{Path.GetFileNameWithoutExtension(MergeModFilename)}.json"),
                JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore
                }));
        }

        public IEnumerable<string> GetMergeFileTargetFiles()
        {
            List<string> targets = new List<string>();
            foreach (var v in FilesToMergeInto)
            {
                targets.Add(v.FileName);

                if (v.ApplyToAllLocalizations)
                {
                    var targetnameBase = Path.GetFileNameWithoutExtension(v.FileName);
                    var targetExtension = Path.GetExtension(v.FileName);
                    var localizations = GameLanguage.GetLanguagesForGame(Game);

                    // Ensure end name is not present on base
                    foreach (var l in localizations)
                    {
                        if (targetnameBase.EndsWith($@"_{l.FileCode}", StringComparison.InvariantCultureIgnoreCase))
                            targetnameBase = targetnameBase.Substring(0, targetnameBase.Length - (l.FileCode.Length + 1));

                        targets.Add($@"{targetnameBase}_{l.FileCode}{targetExtension}");
                    }
                }
            }


            return targets;
        }

        /// <summary>
        /// Releases memory used by assets
        /// </summary>
        public void ReleaseAssets()
        {
            if (LoadedFromPath != null)
            {
                foreach (var asset in Assets)
                {
                    asset.Value.AssetBinary = null;
                }
            }
        }

        public CaseInsensitiveDictionary<List<string>> GetMergeModTargetExports()
        {
            var res = new CaseInsensitiveDictionary<List<string>>();
            foreach (var mf in FilesToMergeInto)
            {
                if (!res.TryGetValue(mf.FileName, out var targetList))
                {
                    res[mf.FileName] = new List<string>();
                    targetList = res[mf.FileName];
                }

                foreach (var t in mf.MergeChanges)
                {
                    targetList.Add(t.ExportInstancedFullPath);
                }
            }

            return res;
        }

        /// <summary>
        /// Converts this merge mod object to binary form in the given stream
        /// </summary>
        /// <param name="outStream"></param>
        /// <param name="manifestFile"></param>
        /// <param name="mergeModVersion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IList<string> Serialize(Stream outStream, string manifestFile, int mergeModVersion = 1)
        {
            M3Log.Information($@"M3MCompiler: Beginning M3M V{mergeModVersion} serialization");
            var sourceDir = Directory.GetParent(manifestFile).FullName;

            var manifestText = File.ReadAllText(manifestFile);
            M3Log.Information($@"M3MCompiler: Source json text: {manifestText}", Settings.LogModInstallation); // This is as close as I can get...

            // VALIDATE JSON SCHEMA
            var schemaText =
                new StreamReader(M3Utilities.ExtractInternalFileToStream(GetSchemaPath(mergeModVersion)))
                    .ReadToEnd();
            var schemaFailureMessages = JsonSchemaValidator.ValidateSchema(manifestText, schemaText);
            if (schemaFailureMessages != null && schemaFailureMessages.Any())
            {
                return schemaFailureMessages;
            }

            var mm = JsonConvert.DeserializeObject<MergeMod1>(manifestText);
            M3Log.Information($@"M3MCompiler: Json is valid for schema");

            // Update manifest

            SortedSet<MergeAsset> assets = new();
            // Get all assets.
            foreach (var fc in mm.FilesToMergeInto)
            {
                foreach (var mc in fc.MergeChanges)
                {
                    if (mc.PropertyUpdates is not null)
                    {
                        foreach (PropertyUpdate1 propertyUpdate in mc.PropertyUpdates)
                        {
                            if (propertyUpdate.PropertyType is @"ArrayProperty")
                            {
                                var assetFilePath = Path.Combine(sourceDir, propertyUpdate.PropertyAsset);
                                if (Directory.GetParent(assetFilePath).FullName != sourceDir)
                                {
                                    throw new Exception(M3L.GetString(M3L.string_interp_mergeModAssetsSingleFolder, propertyUpdate.PropertyAsset));
                                }

                                M3Log.Information($@"M3MCompiler: ArrayProperty file being added into m3m: {assetFilePath}");
                                if (!File.Exists(assetFilePath))
                                {
                                    throw new Exception(M3L.GetString(M3L.string_interp_error_mergefile_scriptNotFoundX, propertyUpdate.PropertyAsset));
                                }
                                propertyUpdate.PropertyValue = File.ReadAllText(assetFilePath);
                            }
                        }
                    }

                    if (mc.AssetUpdate?.AssetName != null)
                    {
                        var assetPath = Path.Combine(sourceDir, mc.AssetUpdate.AssetName);
                        if (Directory.GetParent(assetPath).FullName != sourceDir)
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_mergeModAssetsSingleFolder, mc.AssetUpdate.AssetName));
                        }
                        if (!File.Exists(assetPath))
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_error_mergefile_assetNotFoundX, mc.AssetUpdate.AssetName));
                        }
                        M3Log.Information($@"M3MCompiler: Adding merge asset file to m3m: {assetPath}");

                        assets.Add(new MergeAsset(mc.AssetUpdate.AssetName, true));
                    }

                    if (mc.ScriptUpdate?.ScriptFileName != null)
                    {
                        var scriptDiskFile = Path.Combine(sourceDir, mc.ScriptUpdate.ScriptFileName);
                        if (Directory.GetParent(scriptDiskFile).FullName != sourceDir)
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_mergeModAssetsSingleFolder, mc.ScriptUpdate.ScriptFileName));
                        }

                        if (!File.Exists(scriptDiskFile))
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_error_mergefile_scriptNotFoundX, mc.ScriptUpdate.ScriptFileName));
                        }
                        if (new FileInfo(scriptDiskFile).Length == 0)
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_mergemodAssetIsZeroBytes, mc.ScriptUpdate.ScriptFileName));
                        }
                        M3Log.Information($@"M3MCompiler: Adding script update file to m3m: {scriptDiskFile}");
                        if (mergeModVersion >= 2)
                        {
                            assets.Add(new MergeAsset(mc.ScriptUpdate.ScriptFileName, true));
                        }
                        else
                        {
                            mc.ScriptUpdate.ScriptText = File.ReadAllText(scriptDiskFile);
                        }
                    }

                    if (mc.AddToClassOrReplace?.ScriptFileNames is { Length: > 0 } fileNames)
                    {
                        mc.AddToClassOrReplace.Scripts = new string[fileNames.Length];
                        for (int i = 0; i < fileNames.Length; i++)
                        {
                            string fileName = fileNames[i];
                            string scriptDiskFile = Path.Combine(sourceDir, fileName);

                            if (Directory.GetParent(scriptDiskFile).FullName != sourceDir)
                            {
                                throw new Exception(M3L.GetString(M3L.string_interp_mergeModAssetsSingleFolder, fileName));
                            }

                            if (!File.Exists(scriptDiskFile))
                            {
                                throw new Exception(M3L.GetString(M3L.string_interp_error_mergefile_scriptNotFoundX, fileName));
                            }

                            if (new FileInfo(scriptDiskFile).Length == 0)
                            {
                                throw new Exception(M3L.GetString(M3L.string_interp_mergemodAssetIsZeroBytes, fileName));
                            }

                            M3Log.Information($@"M3MCompiler: Adding AddToClassOrReplace file to m3m: {scriptDiskFile}");

                            if (mergeModVersion >= 2)
                            {
                                assets.Add(new MergeAsset(fileName, new FileInfo(scriptDiskFile).Length > TEXT_MIN_SIZE_TO_COMPRESS));
                            }
                            else
                            {
                                // V1 uses the 'scripts' in json
                                mc.AddToClassOrReplace.Scripts[i] = File.ReadAllText(scriptDiskFile);
                            }
                        }
                    }

                    if (mc.ClassUpdate?.AssetName != null)
                    {
                        var classDiskFile = Path.Combine(sourceDir, mc.ClassUpdate.AssetName);
                        if (Directory.GetParent(classDiskFile).FullName != sourceDir)
                        {
                            throw new Exception(M3L.GetString(M3L.string_interp_mergeModAssetsSingleFolder, mc.ClassUpdate.AssetName));
                        }
                        if (!File.Exists(classDiskFile))
                        {
                            // Todo: Update to asset
                            throw new Exception(M3L.GetString(M3L.string_interp_error_mergefile_scriptNotFoundX, mc.ClassUpdate.AssetName));
                        }
                        M3Log.Information($@"M3MCompiler: Adding class update file to m3m: {classDiskFile}");
                        // Class udpates are always compressed. As they will typically be many characters.
                        assets.Add(new MergeAsset(mc.ClassUpdate.AssetName, true));
                    }
                }
            }

            M3Log.Information($@"M3MCompiler: Serializing merge mod object");
            M3Log.Information($@"Serializing using V{mergeModVersion} format");
            var manifest = JsonConvert.SerializeObject(mm, Formatting.None);
            WriteMergeModString(outStream, manifest, mergeModVersion);

            M3Log.Information($@"M3MCompiler: Serializing {assets.Count} referenced assets");
            outStream.WriteInt32(assets.Count);
            foreach (var asset in assets)
            {
                M3Log.Information($@"M3MCompiler: Serializing {asset.FileName} into m3m");
                outStream.WriteStringASCII(MMV1_ASSETMAGIC); // MAGIC
                outStream.WriteUnrealStringUnicode(asset.FileName); // ASSET NAME
                var assetBytes = File.ReadAllBytes(Path.Combine(sourceDir, asset.FileName));
                if (assetBytes.Length == 0)
                {
                    throw new Exception(M3L.GetString(M3L.string_interp_mergemodAssetIsZeroBytes, asset.FileName));
                }
                outStream.WriteInt32(assetBytes.Length); // DECOMPRESSED ASSET LENGTH

                if (mergeModVersion >= 2)
                {
                    // Assets could be compressed in V2
                    // First determine if compression benefits us at all


                    // Handle compression
                    if (asset.IsCompressed)
                    {
                        var assetBytesCompressed = LZMA.Compress(assetBytes);
                        var hasCompressionBenefits = assetBytesCompressed.Length + (assetBytes.Length * 0.20) < assetBytes.Length;
                        outStream.WriteBoolByte(hasCompressionBenefits);
                        if (hasCompressionBenefits)
                        {
                            M3Log.Information($@"M3MCompiler: Asset {asset.FileName} is being stored compressed {assetBytes.Length} -> {assetBytesCompressed.Length} ({assetBytesCompressed.Length * 100f / Math.Max(1, assetBytes.Length)}%)");
                            outStream.WriteInt32(assetBytesCompressed.Length); // COMPRESSED ASSET LENGTH
                            outStream.Write(assetBytesCompressed); // COMPRESSED ASSET DATA
                        }
                        else
                        {
                            M3Log.Information($@"M3MCompiler: Asset {asset.FileName} does not shrink enough from compression to warrant compressing it. It is being stored uncompressed {assetBytes.Length} -> {assetBytesCompressed.Length} ({assetBytesCompressed.Length * 100f / Math.Max(1, assetBytes.Length)}%)");
                            outStream.Write(assetBytes); // UNCOMPRESSED ASSET DATA
                        }
                    }
                    else
                    {
                        M3Log.Information($@"M3MCompiler: Asset {asset.FileName} is being stored uncompressed");
                        outStream.WriteBoolByte(false); // Not compressed
                        outStream.Write(assetBytes); // UNCOMPRESSED ASSET DATA
                    }
                }
                else
                {
                    // Assets are not compressed at all in V1
                    M3Log.Information($@"M3MCompiler: Asset {asset.FileName} is being stored uncompressed (V1)");
                    outStream.Write(assetBytes); // ASSET DATA
                }

            }

            M3Log.Information($@"M3MCompiler: V{mergeModVersion} serialization complete");
            return null;
        }

        private static string GetSchemaPath(int mergeModVersion) => $@"ME3TweaksModManager.modmanager.objects.mod.merge.v{mergeModVersion}.schema.json";

        /// <summary>
        /// Allows for user-made comments.
        /// </summary>
        [JsonProperty(@"comment")]
        public string Comment { get; set; }

        /// <summary>
        /// The path this merge mod loaded from. Only works if on disk and from m3m form.
        /// </summary>
        [JsonIgnore]
        public string LoadedFromPath { get; set; }

        /// <summary>
        /// The size of the m3m file on disk when it was originally loaded.
        /// </summary>
        [JsonIgnore]
        public long SizeAtLoadTime { get; internal set; }
    }
}
