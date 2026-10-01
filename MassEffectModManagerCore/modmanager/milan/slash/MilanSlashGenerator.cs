using System.Buffers.Binary;
using System.ComponentModel;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Development tool that generates a <see cref="MilanSlashManifest"/> for a freshly built Project Milan base installation.
    /// <para>
    /// The base installation copies whole asset caches from LE3 - every basegame TFC and the full RefShaderCache - even
    /// though LMP only uses a fraction of them. To find out what the slashed game should look like, this actually slashes the
    /// development installation, then records the difference as a manifest:
    /// <list type="number">
    /// <item>The game is snapshotted, so there are original files to diff against and restore from.</item>
    /// <item>The game is transformed: the RefShaderCache is reduced to the shader maps materials in the game use, the
    /// TFCs are compacted with LEC's <see cref="TFCCompactor"/>, which builds new TFCs from the texture data the game
    /// uses and repoints every package at them, and the LE3 DLC AFCs copied into the basegame are compacted the same way.</item>
    /// <item>The before and after states are diffed. Changed packages get a JPatch delta. New files (the compacted TFCs and
    /// AFCs) are described as ranges of the original files - the offsets their textures and audio moved from - so almost
    /// no data is shipped. Removed files, and compacted caches nothing references any more, are marked for deletion.</item>
    /// <item>The game is restored from the snapshot, so the caller can apply the manifest exactly as it will be applied on
    /// users' machines.</item>
    /// </list>
    /// </para>
    /// </summary>
    [Localizable(false)]
    internal class MilanSlashGenerator
    {
        private const string RefShaderCacheName = "RefShaderCache-PC-D3D-SM5.upk";

        /// <summary>
        /// Folder in the game root used for the snapshot and staging. It is outside BioGame, so nothing scans it.
        /// </summary>
        private const string WorkFolderName = "MilanSlashWork";

        /// <summary>
        /// Name the TFC compactor gives the TFCs it builds: Textures1_LMP, Lighting1_LMP...
        /// </summary>
        private const string CompactedTFCName = "LMP";

        /// <summary>
        /// TFC compactor passes, in order. Lightmaps (root textures) and other textures go into separate TFCs, as in the
        /// Project Milan build pipeline.
        /// </summary>
        private static readonly (string tfcType, bool rootTexturesOnly)[] CompactionPasses =
        [
            ("Lighting", true),
            ("Textures", false),
        ];

        /// <summary>
        /// Base name of the AFCs audio is compacted into: Audio1_LMP, Audio2_LMP... A new one is started whenever one would
        /// go past what a WwiseStream's 32-bit offset can address.
        /// </summary>
        private const string CompactedAFCName = "Audio{0}_LMP";

        private static readonly string[] PackageExtensions = [".pcc", ".upk", ".u", ".sfm"];

        /// <summary>
        /// Test seam: replaces the TFC compactor step. Called with the game root and the names of the installed TFCs. LEC's
        /// compactor can only run on Windows (LEC's settings read the registry), so tests use this to exercise everything else.
        /// </summary>
        internal static Action<string, IReadOnlyList<string>> TFCCompactionOverride { get; set; }

        /// <summary>
        /// Texture classes whose binary <see cref="ObjectBinary.From(ExportEntry, PackageCache, int?)"/> parses as <see cref="UTexture2D"/>.
        /// </summary>
        private static readonly HashSet<string> Texture2DClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Texture2D", "LightMapTexture2D", "ShadowMapTexture2D", "TerrainWeightMapTexture", "TextureFlipBook"
        };

        private readonly string gameRoot;
        private readonly string bioGame;
        private readonly string workRoot;
        private readonly string milanDeltaFolder;
        private readonly Action<string> setStatus;
        private MilanSlashSnapshot snapshot;

        /// <summary>
        /// Every static parameter set used by a material in the game.
        /// </summary>
        private readonly HashSet<StaticParameterSet> usedStaticParameterSets = new();

        /// <summary>
        /// Materials that could not be read. Their shader maps can't be identified, so they are reported.
        /// </summary>
        private readonly List<string> unreadableMaterials = new();

        private readonly MilanSlashManifest manifest = new() { MilanSlash = true, Version = MilanSlashApplier.SupportedSlashVersion };

        private MilanSlashGenerator(string gameRoot, string milanDeltaFolder, Action<string> setStatus)
        {
            this.gameRoot = Path.GetFullPath(gameRoot);
            bioGame = Path.Combine(this.gameRoot, "BioGame");
            workRoot = Path.Combine(this.gameRoot, WorkFolderName);
            this.milanDeltaFolder = milanDeltaFolder;
            this.setStatus = setStatus;
        }

        /// <summary>
        /// Generates a slash manifest and its delta files for the game at <paramref name="gameRoot"/>, writing them into
        /// <paramref name="milanDeltaFolder"/> (replacing any previous slash output). The game is slashed while the manifest
        /// is generated, then restored, so it is unchanged when this returns.
        /// </summary>
        public static MilanSlashManifest Generate(string gameRoot, string milanDeltaFolder, Action<string> setStatus = null)
        {
            var generator = new MilanSlashGenerator(gameRoot, milanDeltaFolder, setStatus);
            return generator.Run();
        }

        /// <summary>
        /// Records the size and hash of every file in the finished installation into the manifest and re-saves it. Call this
        /// once the manifest has been applied and the installer has finished modifying the game.
        /// </summary>
        public static void RecordFinalState(MilanSlashManifest manifest, string milanDeltaFolder, string gameRoot, Action<string> setStatus = null)
        {
            manifest.FinalFiles = MilanFinalState.Capture(gameRoot, setStatus);
            manifest.Save(milanDeltaFolder);
            M3Log.Information($"Recorded final state of {manifest.FinalFiles.Count} files ({FileSize.FormatSize(manifest.FinalFiles.Sum(x => x.Size))}) into the slash manifest");
        }

        private MilanSlashManifest Run()
        {
            // Remove the previous slash output so stale deltas never ship alongside a new manifest
            var slashFolder = Path.Combine(milanDeltaFolder, MilanSlashApplier.SlashFolderName);
            if (Directory.Exists(slashFolder))
                Directory.Delete(slashFolder, true);
            var manifestPath = Path.Combine(milanDeltaFolder, MilanSlashApplier.SlashManifestFileName);
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);

            if (Directory.Exists(workRoot))
                Directory.Delete(workRoot, true);

            // Packages can be rewritten in place, so they are copied. The RefShaderCache is only ever replaced (see
            // SlashRefShaderCache), so like the TFCs it can be hard linked instead of copying 1.3GB.
            snapshot = MilanSlashSnapshot.Create(gameRoot, Path.Combine(workRoot, "Snapshot"),
                copyFile: x => IsPackage(x) && !IsRefShaderCache(x),
                excludeFromGame: IsWorkPath,
                setStatus);

            try
            {
                var originalScan = ScanGame(scanMaterials: true);
                SlashRefShaderCache(Path.Combine(bioGame, "CookedPCConsole", RefShaderCacheName));
                CompactTFCs();
                CompactAFCs(originalScan);
                var slashedScan = ScanGame(scanMaterials: false);
                RecordChanges(originalScan, slashedScan);
                manifest.Save(milanDeltaFolder); // Final file state is recorded later, once the installer has finished
            }
            finally
            {
                // Put the game back how it was, so the manifest gets applied to it the same way it will be for users
                try
                {
                    snapshot.Restore(setStatus);
                }
                finally
                {
                    if (Directory.Exists(workRoot))
                        Directory.Delete(workRoot, true);
                }
            }

            long before = manifest.Builds.SelectMany(x => x.Sources).Where(x => !x.Payload).Select(x => (x.Filename, x.Size))
                              .Concat(manifest.Deletions.Select(x => (x.Filename, x.Size)))
                              .DistinctBy(x => x.Filename, StringComparer.OrdinalIgnoreCase).Sum(x => x.Size)
                          + manifest.Patches.Sum(x => x.SourceFileSize);
            long after = manifest.Builds.Sum(x => x.DestSize) + manifest.Patches.Sum(x => x.DestSize);
            long payload = manifest.Builds.SelectMany(x => x.Sources).Where(x => x.Payload).Sum(x => x.Size);
            M3Log.Information($"Generated slash manifest: {manifest.Builds.Count} file(s) built, {manifest.Deletions.Count} file(s) deleted, {manifest.Patches.Count} file(s) patched. " +
                              $"Affected files go from {FileSize.FormatSize(before)} to roughly {FileSize.FormatSize(after)} (patched packages are measured decompressed). " +
                              $"Payload shipped for built files: {FileSize.FormatSize(payload)}");
            return manifest;
        }

        private static bool IsPackage(string relativePath) => PackageExtensions.Contains(Path.GetExtension(relativePath.Replace('\\', Path.DirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase);
        private static bool IsTFC(string relativePath) => relativePath.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase);
        private static bool IsAFC(string relativePath) => relativePath.EndsWith(".afc", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// If this is a file that textures or audio keep their data in.
        /// </summary>
        private static bool IsCacheFile(string relativePath) => IsTFC(relativePath) || IsAFC(relativePath);

        private static bool IsInBasegameCooked(string relativePath) => relativePath.StartsWith(@"BioGame\CookedPCConsole\", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// TFCs copied from LE3's basegame. These are the only TFCs that are compacted or deleted: DLC TFCs (Project Milan's)
        /// are already compacted, and basegame packages never reference them.
        /// </summary>
        private static bool IsBasegameTFC(string relativePath) => IsTFC(relativePath) && IsInBasegameCooked(relativePath);

        /// <summary>
        /// AFCs of LE3's DLC, which the base installer copies into the basegame. These are the only AFCs that are compacted
        /// or deleted.
        /// </summary>
        private static bool IsCompactableAFC(string relativePath) => IsAFC(relativePath) && IsInBasegameCooked(relativePath) && GetFileName(relativePath).StartsWith("DLC_", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Caches whose used data is compacted into new files, and which are deleted once nothing references them.
        /// </summary>
        private static bool IsCompactableCache(string relativePath) => IsBasegameTFC(relativePath) || IsCompactableAFC(relativePath);
        private static bool IsRefShaderCache(string relativePath) => relativePath.EndsWith("\\" + RefShaderCacheName, StringComparison.OrdinalIgnoreCase);
        private static bool IsWorkPath(string relativePath) => relativePath.StartsWith(WorkFolderName + "\\", StringComparison.OrdinalIgnoreCase);
        private static string GetFileName(string relativePath) => relativePath[(relativePath.LastIndexOf('\\') + 1)..];

        #region Scanning

        /// <summary>
        /// Where one export's external data is: the cache file (with extension, e.g. Textures0.tfc or DLC_EXP_Pack001_INT.afc)
        /// and the ranges in it.
        /// </summary>
        private record ExternalData(string FileName, List<(long Offset, long Length)> Ranges);

        private class GameScan
        {
            /// <summary>
            /// External data of every texture, movie and audio export we can read, keyed by (game-relative package path,
            /// export UIndex).
            /// </summary>
            public Dictionary<(string package, int uIndex), ExternalData> Data { get; } = new();

            /// <summary>
            /// File names (with extension) of every cache file referenced by any export, including classes we can't read the
            /// data of.
            /// </summary>
            public HashSet<string> ReferencedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static bool ShouldLoadExport(ExportEntry export)
        {
            var className = export.ClassName;
            return className.Contains("Texture", StringComparison.OrdinalIgnoreCase)
                   || className.Contains("Movie", StringComparison.OrdinalIgnoreCase)
                   || className == "WwiseStream"
                   || (className.Contains("Material", StringComparison.OrdinalIgnoreCase) && !className.Contains("Expression", StringComparison.OrdinalIgnoreCase));
        }

        private GameScan ScanGame(bool scanMaterials)
        {
            var scan = new GameScan();
            var packages = snapshot.EnumerateGameFiles().Where(x => IsPackage(x) && !IsRefShaderCache(x)).ToList();
            for (int i = 0; i < packages.Count; i++)
            {
                var relativePath = packages[i];
                setStatus?.Invoke($"Scanning packages ({i + 1}/{packages.Count})");
                using var package = MEPackageHandler.UnsafePartialLoad(snapshot.GetGamePath(relativePath), ShouldLoadExport);
                foreach (var export in package.Exports.Where(ShouldLoadExport))
                {
                    if (export.ClassName == "WwiseStream")
                    {
                        ScanAudioExport(scan, relativePath, export);
                        continue;
                    }

                    ScanTextureExport(scan, relativePath, export);
                    if (scanMaterials)
                        ScanMaterialExport(relativePath, export);
                }
            }

            M3Log.Information($"Slash scan: {packages.Count} packages, {scan.Data.Count} exports with external data, {scan.ReferencedFiles.Count} referenced TFCs/AFCs" +
                              (scanMaterials ? $", {usedStaticParameterSets.Count} used material static parameter sets, {unreadableMaterials.Count} unreadable materials" : ""));
            return scan;
        }

        private static void ScanTextureExport(GameScan scan, string packagePath, ExportEntry export)
        {
            var className = export.ClassName;
            if (!className.Contains("Texture", StringComparison.OrdinalIgnoreCase) && !className.Contains("Movie", StringComparison.OrdinalIgnoreCase))
                return;

            var tfcName = export.GetProperty<NameProperty>("TextureFileCacheName")?.Value.Instanced;
            if (string.IsNullOrEmpty(tfcName))
                return; // No external data
            var tfcFileName = tfcName + ".tfc";

            try
            {
                var ranges = new List<(long, long)>();
                if (Texture2DClasses.Contains(className))
                {
                    foreach (var mip in ObjectBinary.From<UTexture2D>(export).Mips)
                    {
                        if (HasExternalData(mip))
                            ranges.Add((mip.DataOffset, mip.CompressedSize));
                    }
                }
                else if (className == "TextureMovie")
                {
                    var movie = ObjectBinary.From<TextureMovie>(export);
                    if (movie.IsExternal)
                        ranges.Add((movie.DataOffset, movie.DataSize));
                }
                else
                {
                    // We can't read where this class keeps its data, so assume it uses the TFC
                    scan.ReferencedFiles.Add(tfcFileName);
                    return;
                }

                // Textures whose mips are all package stored can still have a TextureFileCacheName (LE3's GUI textures
                // often do); they don't actually use the TFC
                if (ranges.Count > 0)
                {
                    scan.ReferencedFiles.Add(tfcFileName);
                    scan.Data[(packagePath, export.UIndex)] = new ExternalData(tfcFileName, ranges);
                }
            }
            catch (Exception e)
            {
                scan.ReferencedFiles.Add(tfcFileName); // Can't tell, so assume it's used
                M3Log.Warning($"Slash scan: could not read {export.InstancedFullPath} in {packagePath}: {e.Message}");
            }
        }

        private static void ScanAudioExport(GameScan scan, string packagePath, ExportEntry export)
        {
            var afcName = export.GetProperty<NameProperty>("Filename")?.Value.Instanced;
            if (string.IsNullOrEmpty(afcName))
                return; // Package stored
            var afcFileName = afcName + ".afc";

            scan.ReferencedFiles.Add(afcFileName);
            try
            {
                var stream = ObjectBinary.From<WwiseStream>(export);
                if (stream.DataSize > 0)
                    scan.Data[(packagePath, export.UIndex)] = new ExternalData(afcFileName, [(stream.DataOffset, stream.DataSize)]);
            }
            catch (Exception e)
            {
                M3Log.Warning($"Slash scan: could not read {export.InstancedFullPath} in {packagePath}: {e.Message}");
            }
        }

        /// <summary>
        /// If a mip's data lives in a TFC. Checked by flag rather than with <see cref="UTexture2D.Texture2DMipMap.IsEmpty"/>,
        /// which masks against <see cref="StorageTypes.empty"/> (externalFile | unused) and so is true for every external mip.
        /// </summary>
        private static bool HasExternalData(UTexture2D.Texture2DMipMap mip)
        {
            var flags = (StorageFlags)mip.StorageType;
            return flags.HasFlag(StorageFlags.externalFile) && !flags.HasFlag(StorageFlags.unused);
        }

        /// <summary>
        /// Records the static parameter sets a material's shader maps are keyed by in the shader cache.
        /// </summary>
        private void ScanMaterialExport(string packagePath, ExportEntry export)
        {
            // Class default objects (Default__Material...) carry no material resource
            if (export.IsDefaultObject)
                return;

            var className = export.ClassName;
            try
            {
                if (className.Contains("MaterialInstance", StringComparison.OrdinalIgnoreCase) || IsA(export, "MaterialInstance"))
                {
                    // Without a static permutation, an instance renders with its parent material's shader maps
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        var instance = ObjectBinary.From<MaterialInstance>(export);
                        usedStaticParameterSets.Add(instance.SM3StaticParameterSet);
                        usedStaticParameterSets.Add((StaticParameterSet)instance.SM3StaticPermutationResource.ID);
                    }
                }
                else if (className is "Material" or "DecalMaterial" || IsA(export, "Material"))
                {
                    usedStaticParameterSets.Add((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID);
                }
            }
            catch (Exception e)
            {
                unreadableMaterials.Add($"{packagePath}: {export.InstancedFullPath}");
                M3Log.Error($"Slash scan: could not read material {export.InstancedFullPath} in {packagePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Catches subclasses we don't know by name. Class lookups can fail for classes the object database doesn't know
        /// about; those aren't classes we can read anyway.
        /// </summary>
        private static bool IsA(ExportEntry export, string className)
        {
            try
            {
                return export.IsA(className);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Transforms

        /// <summary>
        /// Reduces the RefShaderCache to the material shader maps (and their shaders) used by materials in the game.
        /// </summary>
        private void SlashRefShaderCache(string refShaderCachePath)
        {
            if (!File.Exists(refShaderCachePath))
            {
                M3Log.Warning("Slash: RefShaderCache is not installed; skipping shader slashing");
                return;
            }

            if (usedStaticParameterSets.Count == 0)
            {
                M3Log.Error("Slash: no materials were found in the game; the RefShaderCache will not be slashed");
                return;
            }

            if (unreadableMaterials.Count > 0)
            {
                // Their shader maps are removed. Listed together so they can be checked before the manifest ships.
                M3Log.Error($"Slash: {unreadableMaterials.Count} material(s) could not be read, so their shader maps can't be identified and will not be kept:\n  " + string.Join("\n  ", unreadableMaterials));
            }

            setStatus?.Invoke("Slashing RefShaderCache");
            byte[] slashed;
            using (var package = MEPackageHandler.OpenMEPackage(refShaderCachePath, forceLoadFromDisk: true))
            {
                var cacheExport = package.Exports.FirstOrDefault(x => x.ClassName == "ShaderCache");
                if (cacheExport == null)
                {
                    M3Log.Error("Slash: RefShaderCache has no ShaderCache export; skipping shader slashing");
                    return;
                }

                var cache = ObjectBinary.From<ShaderCache>(cacheExport);
                int originalMaps = cache.MaterialShaderMaps.Count;
                int originalShaders = cache.Shaders.Count;

                var keptMaps = new UMultiMap<StaticParameterSet, MaterialShaderMap>();
                foreach (var (staticParameterSet, shaderMap) in cache.MaterialShaderMaps)
                {
                    if (usedStaticParameterSets.Contains(staticParameterSet))
                    {
                        keptMaps.Add(staticParameterSet, shaderMap);
                    }
                }

                // Remove shaders no remaining shader map references. Done here rather than with ShaderCache.Compact(),
                // which searches a list for every shader and takes far too long on a cache this size.
                var keptShaderIds = new HashSet<Guid>();
                foreach (var (_, shaderMap) in keptMaps)
                {
                    foreach (var (_, shaderReference) in shaderMap.Shaders)
                        keptShaderIds.Add(shaderReference.Id);
                    foreach (var meshShaderMap in shaderMap.MeshShaderMaps)
                    {
                        foreach (var (_, shaderReference) in meshShaderMap.Shaders)
                            keptShaderIds.Add(shaderReference.Id);
                    }
                }

                cache.MaterialShaderMaps = keptMaps;
                cache.Shaders = new UMultiMap<Guid, Shader>(cache.Shaders.Where(x => keptShaderIds.Contains(x.Key)));
                M3Log.Information($"Slash: RefShaderCache keeps {keptMaps.Count}/{originalMaps} material shader maps and {cache.Shaders.Count}/{originalShaders} shaders " +
                                  $"({usedStaticParameterSets.Count} static parameter sets are used by materials in the game; the rest have shader maps elsewhere or none at all)");
                if (keptMaps.Count == originalMaps && cache.Shaders.Count == originalShaders)
                    return; // Nothing to remove

                cacheExport.WriteBinary(cache);

                // The game's RefShaderCache is uncompressed. Keep it that way so the delta is a raw file-to-file patch that
                // the applier never has to load the 1.3GB package to apply.
                using var saved = package.SaveToStream(compress: false);
                slashed = saved.ToArray();
            }

            // Replace rather than overwrite, so the snapshot's hard link keeps the original
            var temp = refShaderCachePath + ".slashnew";
            File.WriteAllBytes(temp, slashed);
            File.Move(temp, refShaderCachePath, overwrite: true);
        }

        /// <summary>
        /// Runs LEC's TFC compactor over the whole game, which builds new TFCs out of the texture data the game uses and
        /// repoints every package at them.
        /// </summary>
        private void CompactTFCs()
        {
            var tfcNames = snapshot.EnumerateGameFiles()
                .Where(IsBasegameTFC)
                .Select(x => Path.GetFileNameWithoutExtension(GetFileName(x)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tfcNames.Count == 0)
                return;

            if (TFCCompactionOverride != null)
            {
                TFCCompactionOverride(gameRoot, tfcNames);
                return;
            }

            // The compactor refuses to scan a folder with packages from more than one game, and LE3 packages that were
            // copied without being converted to LMP (such as the RefShaderCache) are in the game. Set them aside.
            var setAside = new List<(string original, string aside)>();
            foreach (var relativePath in snapshot.EnumerateGameFiles().Where(IsPackage))
            {
                var path = snapshot.GetGamePath(relativePath);
                MEGame game;
                using (var package = MEPackageHandler.QuickOpenMEPackage(path))
                {
                    game = package.Game;
                }

                if (game != MEGame.LMP)
                {
                    var aside = Path.Combine(workRoot, "OtherGamePackages", relativePath.Replace('\\', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(aside));
                    File.Move(path, aside);
                    setAside.Add((path, aside));
                    M3Log.Information($"Slash: {relativePath} is a {game} package; it is not included in TFC compaction");
                }
            }

            // The compactor puts the TFCs it builds in a folder named after the TFC name, deleting any TFCs already in that
            // folder. Giving it an empty folder of its own means it never deletes a source TFC another pass (or another
            // texture) still needs; TFCs that end up unused are deleted by the manifest instead.
            var outputRoot = Path.Combine(bioGame, CompactedTFCName);
            var outputCooked = Path.Combine(outputRoot, "CookedPCConsole");
            if (Directory.Exists(outputRoot))
                throw new Exception($"Cannot compact TFCs: {outputRoot} already exists");
            Directory.CreateDirectory(outputCooked);
            var staging = Directory.CreateDirectory(Path.Combine(workRoot, "CompactorStaging")).FullName;

            try
            {
                foreach (var (tfcType, rootTexturesOnly) in CompactionPasses)
                {
                    setStatus?.Invoke($"Compacting {tfcType} TFCs");
                    var errors = new List<string>();
                    var info = new TFCCompactorInfoPackage
                    {
                        Game = MEGame.LMP,
                        BaseCompactionPath = bioGame,
                        GamePath = gameRoot,
                        DLCName = CompactedTFCName,
                        StagingPath = staging,
                        TFCType = tfcType,
                        RootTexturesOnly = rootTexturesOnly,
                        UseIndexing = true,
                        TFCsToCompact = tfcNames,
                    };

                    TFCCompactor.CompactTFC(info, errors.Add, (text, done, total) =>
                    {
                        if (text != null)
                            setStatus?.Invoke(total > 0 ? $"Compacting {tfcType} TFCs: {text} ({done}/{total})" : $"Compacting {tfcType} TFCs: {text}");
                    });

                    if (errors.Count > 0)
                        throw new Exception($"TFC compaction ({tfcType}) failed: {string.Join(" ", errors)}");
                }

                // Put the new TFCs with the basegame's
                foreach (var tfc in Directory.GetFiles(outputCooked, "*.tfc"))
                {
                    var destination = Path.Combine(bioGame, "CookedPCConsole", Path.GetFileName(tfc));
                    File.Move(tfc, destination, overwrite: true);
                    M3Log.Information($"Slash: TFC compactor built {Path.GetFileName(tfc)} ({FileSize.FormatSize(new FileInfo(destination).Length)})");
                }
            }
            finally
            {
                if (Directory.Exists(outputRoot))
                    Directory.Delete(outputRoot, true);
                foreach (var (original, aside) in setAside)
                    File.Move(aside, original);
            }
        }

        /// <summary>
        /// Compacts the LE3 DLC AFCs the base installer copies into the basegame: the audio packages anywhere in the game use
        /// from them is copied into new AFCs, and those WwiseStreams are repointed at it. As with the TFC compactor, packages
        /// that don't reference these AFCs are never saved.
        /// </summary>
        private void CompactAFCs(GameScan scan)
        {
            var afcs = snapshot.EnumerateGameFiles().Where(IsCompactableAFC).ToDictionary(GetFileName, x => x, StringComparer.OrdinalIgnoreCase);
            var references = scan.Data.Where(x => afcs.ContainsKey(x.Value.FileName)).ToList();
            if (references.Count == 0)
                return;

            static (string file, long offset, long length) AudioKey(ExternalData data) => (data.FileName.ToLowerInvariant(), data.Ranges[0].Offset, data.Ranges[0].Length);

            // Build the new AFCs. Audio is copied in source order, once no matter how many exports use it.
            var cooked = Path.Combine(bioGame, "CookedPCConsole");
            var newLocations = new Dictionary<(string file, long offset, long length), (string afcName, int offset)>();
            var built = new List<(string building, string final)>();
            var audio = references.Select(x => AudioKey(x.Value)).Distinct().OrderBy(x => x.file, StringComparer.Ordinal).ThenBy(x => x.offset).ToList();
            FileStream source = null;
            FileStream output = null;
            try
            {
                string sourceName = null;
                string outputName = null;
                var header = new byte[4];
                for (int i = 0; i < audio.Count; i++)
                {
                    var item = audio[i];
                    setStatus?.Invoke($"Compacting AFCs ({i + 1}/{audio.Count})");
                    if (sourceName != item.file)
                    {
                        source?.Dispose();
                        source = new FileStream(snapshot.GetGamePath(afcs[item.file]), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                        sourceName = item.file;
                    }

                    // Same checks as LEC's AFC compactor. Anything that fails them is left pointing at the original AFC, which
                    // then stays in the game.
                    if (item.offset < 0 || item.length < header.Length || item.offset + item.length > source.Length)
                    {
                        M3Log.Warning($"Slash: audio at 0x{item.offset:X8} (0x{item.length:X} bytes) is outside of {item.file}; it is not compacted");
                        continue;
                    }
                    source.Position = item.offset;
                    source.ReadExactly(header);
                    if (!header.AsSpan().SequenceEqual("RIFF"u8))
                    {
                        M3Log.Warning($"Slash: audio at 0x{item.offset:X8} in {item.file} does not start with RIFF; it is not compacted");
                        continue;
                    }

                    if (output == null || output.Position + item.length > int.MaxValue)
                    {
                        output?.Dispose();
                        outputName = string.Format(CompactedAFCName, built.Count + 1);
                        var final = Path.Combine(cooked, outputName + ".afc");
                        if (File.Exists(final))
                            throw new Exception($"Cannot compact AFCs: {final} already exists");
                        built.Add((final + ".building", final));
                        output = new FileStream(final + ".building", FileMode.CreateNew, FileAccess.Write);
                    }

                    newLocations[item] = (outputName, (int)output.Position);
                    source.Position = item.offset;
                    CopyExactly(source, output, item.length);
                }
            }
            finally
            {
                source?.Dispose();
                output?.Dispose();
            }

            // Repoint the audio. If anything goes wrong, the snapshot restore removes the partially built AFCs.
            var packages = references.Where(x => newLocations.ContainsKey(AudioKey(x.Value))).GroupBy(x => x.Key.package).ToList();
            int repointed = 0;
            for (int i = 0; i < packages.Count; i++)
            {
                var relativePath = packages[i].Key;
                setStatus?.Invoke($"Repointing audio ({i + 1}/{packages.Count})");
                using var package = MEPackageHandler.OpenMEPackage(snapshot.GetGamePath(relativePath), forceLoadFromDisk: true);
                bool compressed = package.IsCompressed;
                foreach (var ((_, uIndex), data) in packages[i])
                {
                    var export = package.GetUExport(uIndex);
                    var filename = export.GetProperty<NameProperty>("Filename");
                    var stream = export.ClassName == "WwiseStream" ? ObjectBinary.From<WwiseStream>(export) : null;
                    if (stream == null || filename == null || !(filename.Value.Instanced + ".afc").Equals(data.FileName, StringComparison.OrdinalIgnoreCase)
                        || stream.DataOffset != data.Ranges[0].Offset || stream.DataSize != data.Ranges[0].Length)
                    {
                        throw new Exception($"Slash: {export.InstancedFullPath} in {relativePath} changed while slashing");
                    }

                    var (afcName, offset) = newLocations[AudioKey(data)];
                    filename.Value = afcName;
                    export.WriteProperty(filename);

                    // The offset is the last thing in an external WwiseStream's binary (as LEC's AFC compactor writes it)
                    var exportData = export.Data;
                    BinaryPrimitives.WriteInt32LittleEndian(exportData.AsSpan(exportData.Length - 4), offset);
                    export.Data = exportData;
                    repointed++;
                }
                package.Save(compress: compressed);
            }

            foreach (var (building, final) in built)
            {
                File.Move(building, final);
                M3Log.Information($"Slash: AFC compaction built {Path.GetFileName(final)} ({FileSize.FormatSize(new FileInfo(final).Length)})");
            }
            M3Log.Information($"Slash: compacted {newLocations.Count} audio streams from {afcs.Count} DLC AFC(s); {repointed} WwiseStreams in {packages.Count} package(s) repointed");
        }

        #endregion

        #region Diffing

        /// <summary>
        /// Records everything that differs between the snapshot and the transformed game in the manifest.
        /// </summary>
        private void RecordChanges(GameScan before, GameScan after)
        {
            var current = snapshot.EnumerateGameFiles();
            var currentSet = current.ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < current.Count; i++)
            {
                var relativePath = current[i];
                setStatus?.Invoke($"Recording changes ({i + 1}/{current.Count})");
                bool existed = snapshot.Files.ContainsKey(relativePath);
                if (existed && IsPackage(relativePath))
                {
                    var original = File.ReadAllBytes(snapshot.GetSnapshotPath(relativePath));
                    var modified = File.ReadAllBytes(snapshot.GetGamePath(relativePath));
                    if (!original.AsSpan().SequenceEqual(modified))
                        AddPackagePatch(relativePath, original, modified);
                }
                else if (!existed || !snapshot.IsUnchanged(relativePath))
                {
                    AddBuild(relativePath, before, after);
                }
            }

            foreach (var (relativePath, (size, _)) in snapshot.Files)
            {
                if (!currentSet.Contains(relativePath))
                    AddDeletion(relativePath, size, "removed while slashing");
            }

            // Compacted caches whose data has all been moved out, or that nothing used to begin with
            foreach (var relativePath in current.Where(x => IsCompactableCache(x) && snapshot.Files.ContainsKey(x) && snapshot.IsUnchanged(x)))
            {
                var name = GetFileName(relativePath);
                if (after.ReferencedFiles.Contains(name))
                    continue;

                if (before.ReferencedFiles.Contains(name))
                {
                    var destinations = manifest.Builds
                        .Where(b => b.Sources.Any(s => !s.Payload && s.Filename.Equals(relativePath, StringComparison.OrdinalIgnoreCase)))
                        .Select(b => GetFileName(b.Filename))
                        .ToList();
                    AddDeletion(relativePath, snapshot.Files[relativePath].Size, destinations.Count > 0 ? $"orphaned: used data moved to {string.Join(", ", destinations)}" : "orphaned: no longer referenced");
                }
                else
                {
                    AddDeletion(relativePath, snapshot.Files[relativePath].Size, "not used by anything");
                }
            }
        }

        private void AddDeletion(string relativePath, long size, string reason)
        {
            M3Log.Information($"Slash: {relativePath} will be deleted ({reason})");
            manifest.Deletions.Add(new MilanSlashDeletionEntry { Comment = reason, Filename = relativePath, Size = size });
        }

        /// <summary>
        /// Records a JPatch delta that turns the original package into the modified one. If either is compressed, the delta
        /// uses package mode (the same decompress/patch/recompress convention as MilanDelta); otherwise raw file bytes are
        /// patched.
        /// </summary>
        private void AddPackagePatch(string relativePath, byte[] original, byte[] modified)
        {
            bool originalCompressed = IsCompressedPackage(original, relativePath);
            bool modifiedCompressed = IsCompressedPackage(modified, relativePath);
            bool packageMode = originalCompressed || modifiedCompressed;

            byte[] sourceData = packageMode ? Decompress(original) : original;
            byte[] destData = packageMode ? Decompress(modified) : modified;
            if (sourceData.AsSpan().SequenceEqual(destData))
            {
                M3Log.Information($"Slash: {relativePath} only differs in compression; no delta needed");
                return;
            }

            var deltaRelPath = MilanSlashApplier.SlashFolderName + "\\" + relativePath + ".jsf";
            var deltaPath = MilanSlashApplier.Resolve(milanDeltaFolder, deltaRelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(deltaPath));
            using (var deltaStream = new FileStream(deltaPath, FileMode.Create, FileAccess.Write))
            {
                JsfDeltaBuilder.Build(sourceData, destData, deltaStream);
            }

            var mode = packageMode ? MilanSlashPatchModes.Package : MilanSlashPatchModes.Raw;
            manifest.Patches.Add(new MilanSlashPatchEntry
            {
                Comment = IsRefShaderCache(relativePath) ? "RefShaderCache reduced to shader maps used by materials in the game" : "Package modified while slashing (textures or audio repointed to compacted TFCs/AFCs)",
                Filename = relativePath,
                Delta = deltaRelPath,
                Mode = mode,
                Compressed = modifiedCompressed,
                SourceFileSize = original.LongLength,
                SourceFileHash = MUtilities.CalculateHash(original, "sha256"),
                SourceSize = sourceData.LongLength,
                SourceHash = MUtilities.CalculateHash(sourceData, "sha256"),
                DestSize = destData.LongLength,
                DestHash = MUtilities.CalculateHash(destData, "sha256"),
            });

            M3Log.Information($"Slash: {relativePath} delta is {FileSize.FormatSize(new FileInfo(deltaPath).Length)} ({mode}, {FileSize.FormatSize(sourceData.LongLength)} -> {FileSize.FormatSize(destData.LongLength)})");
        }

        private static bool IsCompressedPackage(byte[] data, string relativePath)
        {
            using var package = MEPackageHandler.OpenMEPackageFromStream(new MemoryStream(data), relativePath, quickLoad: true);
            return package.IsCompressed;
        }

        private static byte[] Decompress(byte[] packageData)
        {
            using var decompressed = MEPackage.GetDecompressedPackageStream(new MemoryStream(packageData));
            return decompressed.ToArray();
        }

        /// <summary>
        /// Describes a new or changed file as ranges of the original files. For a TFC or AFC, the compactor copied each
        /// texture's or audio stream's data from its old location, so every range the exports now reference is sourced from
        /// the offsets they used to reference. Anything that can't be sourced (headers, recompressed data, non-TFC files) goes in a payload
        /// file shipped with the manifest.
        /// </summary>
        private void AddBuild(string relativePath, GameScan before, GameScan after)
        {
            var path = snapshot.GetGamePath(relativePath);
            long length = new FileInfo(path).Length;

            // Where this file's data came from: (new offset, length, original cache file, original offset)
            var claims = new List<(long newOffset, long length, string sourceFile, long sourceOffset)>();
            if (IsCacheFile(relativePath))
            {
                var name = GetFileName(relativePath);
                foreach (var (key, now) in after.Data)
                {
                    if (!now.FileName.Equals(name, StringComparison.OrdinalIgnoreCase) || !before.Data.TryGetValue(key, out var was) || was.Ranges.Count != now.Ranges.Count)
                        continue;

                    for (int i = 0; i < now.Ranges.Count; i++)
                    {
                        if (now.Ranges[i].Length == was.Ranges[i].Length)
                            claims.Add((now.Ranges[i].Offset, now.Ranges[i].Length, was.FileName, was.Ranges[i].Offset));
                    }
                }
            }

            var build = new MilanSlashBuildEntry { Filename = relativePath };
            var payloadRelPath = MilanSlashApplier.SlashFolderName + "\\" + relativePath + ".payload";
            var payloadPath = MilanSlashApplier.Resolve(milanDeltaFolder, payloadRelPath);
            MilanSlashSourceFile payloadSource = null;
            FileStream payload = null;
            long sourcedBytes = 0;

            void addSegment(int sourceIndex, long offset, long segmentLength)
            {
                var segments = build.Segments;
                if (segments.Count > 0 && segments[^1].SourceIndex == sourceIndex && segments[^1].Offset + segments[^1].Length == offset)
                    segments[^1] = segments[^1] with { Length = segments[^1].Length + segmentLength };
                else
                    segments.Add(new MilanSlashSegment(sourceIndex, offset, segmentLength));
            }

            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            void addPayload(long offset, long payloadLength)
            {
                if (payloadLength <= 0)
                    return;
                if (payload == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(payloadPath));
                    payload = new FileStream(payloadPath, FileMode.Create, FileAccess.Write);
                    payloadSource = new MilanSlashSourceFile { Filename = payloadRelPath, Payload = true };
                    build.Sources.Add(payloadSource);
                }

                long payloadOffset = payload.Position;
                file.Position = offset;
                CopyExactly(file, payload, payloadLength);
                addSegment(build.Sources.IndexOf(payloadSource), payloadOffset, payloadLength);
            }

            try
            {
                long position = 0;
                foreach (var claim in claims.OrderBy(x => x.newOffset).ThenByDescending(x => x.length))
                {
                    if (claim.newOffset < position)
                        continue; // Already covered (textures sharing data)

                    addPayload(position, claim.newOffset - position);

                    var sourceRelPath = FindOriginalCacheFile(claim.sourceFile);
                    if (sourceRelPath != null && RangesMatch(file, claim.newOffset, snapshot.GetSnapshotPath(sourceRelPath), claim.sourceOffset, claim.length))
                    {
                        int sourceIndex = build.Sources.FindIndex(x => !x.Payload && x.Filename.Equals(sourceRelPath, StringComparison.OrdinalIgnoreCase));
                        if (sourceIndex < 0)
                        {
                            sourceIndex = build.Sources.Count;
                            build.Sources.Add(new MilanSlashSourceFile { Filename = sourceRelPath, Size = snapshot.Files[sourceRelPath].Size });
                        }
                        addSegment(sourceIndex, claim.sourceOffset, claim.length);
                        sourcedBytes += claim.length;
                    }
                    else
                    {
                        addPayload(claim.newOffset, claim.length); // e.g. the compactor recompressed it
                    }

                    position = claim.newOffset + claim.length;
                }

                addPayload(position, length - position);
            }
            finally
            {
                payload?.Dispose();
            }

            if (payloadSource != null)
                payloadSource.Size = new FileInfo(payloadPath).Length;

            build.DestSize = length;
            build.DestHash = MUtilities.CalculateHash(path, "sha256");

            // Prove the description reproduces the file from the original game files
            var verifyPath = Path.Combine(workRoot, "BuildVerification.tmp");
            using (var verifyStream = new FileStream(verifyPath, FileMode.Create, FileAccess.Write))
            {
                MilanSlashApplier.AssembleBuild(build, source => source.Payload ? payloadPath : snapshot.GetSnapshotPath(source.Filename), verifyStream);
            }
            var reproduced = new FileInfo(verifyPath).Length == build.DestSize && MUtilities.CalculateHash(verifyPath, "sha256") == build.DestHash;
            File.Delete(verifyPath);
            if (!reproduced)
                throw new Exception($"Slash: the description of {relativePath} does not reproduce it");

            manifest.Builds.Add(build);
            M3Log.Information($"Slash: {relativePath} ({FileSize.FormatSize(length)}) is built from {FileSize.FormatSize(sourcedBytes)} of existing data in {build.Sources.Count(x => !x.Payload)} file(s) and {FileSize.FormatSize(payloadSource?.Size ?? 0)} of payload");
        }

        /// <summary>
        /// Game-relative path of an original TFC or AFC by file name, or null if there isn't exactly one.
        /// </summary>
        private string FindOriginalCacheFile(string fileName)
        {
            var matches = snapshot.Files.Keys.Where(x => IsCacheFile(x) && GetFileName(x).Equals(fileName, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private static bool RangesMatch(FileStream file, long offset, string otherPath, long otherOffset, long length)
        {
            using var other = new FileStream(otherPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            if (otherOffset + length > other.Length || offset + length > file.Length)
                return false;

            var a = new byte[Math.Min(length, 1024 * 1024)];
            var b = new byte[a.Length];
            file.Position = offset;
            other.Position = otherOffset;
            long remaining = length;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(a.Length, remaining);
                file.ReadExactly(a, 0, chunk);
                other.ReadExactly(b, 0, chunk);
                if (!a.AsSpan(0, chunk).SequenceEqual(b.AsSpan(0, chunk)))
                    return false;
                remaining -= chunk;
            }
            return true;
        }

        private static void CopyExactly(Stream input, Stream output, long length)
        {
            var buffer = new byte[Math.Min(length, 1024 * 1024)];
            while (length > 0)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                if (read <= 0)
                    throw new EndOfStreamException();
                output.Write(buffer, 0, read);
                length -= read;
            }
        }

        #endregion
    }
}
