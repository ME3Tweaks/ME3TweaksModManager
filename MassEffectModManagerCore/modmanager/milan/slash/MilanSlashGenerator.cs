using System.ComponentModel;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Development tool that generates a <see cref="MilanSlashManifest"/> for a freshly built Project Milan base installation.
    /// <para>
    /// The base installation copies whole asset caches from LE3 - every basegame TFC and the full RefShaderCache - even
    /// though LMP only uses a fraction of them. This scans every package in the installed game to find out what is actually
    /// used, then works out how to transform the installation into one that contains only that:
    /// <list type="bullet">
    /// <item>TFCs are compacted to only the byte ranges referenced by textures in the game. Each compacted TFC is described
    /// as a list of (source, offset, length) ranges of the existing TFCs, so the shipped manifest carries no texture data.
    /// TFC names and GUIDs are kept, so packages only need their mip offsets changed. TFCs nothing uses are deleted.</item>
    /// <item>Packages whose texture offsets moved get a JPatch delta.</item>
    /// <item>The RefShaderCache is reduced to the material shader maps (and their shaders) used by materials in the game,
    /// and gets a JPatch delta.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The game is not modified here. The caller applies the generated manifest with <see cref="MilanSlashApplier"/>, which
    /// both transforms the development installation and proves the manifest works before it ships.
    /// </para>
    /// </summary>
    [Localizable(false)]
    internal class MilanSlashGenerator
    {
        private const string RefShaderCacheName = "RefShaderCache-PC-D3D-SM5.upk";
        private const int TFCHeaderSize = 16; // TFCs start with their GUID

        private static readonly string[] PackageExtensions = [".pcc", ".upk", ".u", ".sfm"];

        /// <summary>
        /// Texture classes whose binary <see cref="ObjectBinary.From(ExportEntry, PackageCache, int?)"/> parses as <see cref="UTexture2D"/>.
        /// </summary>
        private static readonly HashSet<string> Texture2DClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Texture2D", "LightMapTexture2D", "ShadowMapTexture2D", "TerrainWeightMapTexture", "TextureFlipBook"
        };

        private readonly string gameRoot;
        private readonly string milanDeltaFolder;
        private readonly Action<string> setStatus;

        /// <summary>
        /// Installed TFCs, keyed by TFC name (filename without extension).
        /// </summary>
        private readonly Dictionary<string, TFCUsage> tfcs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Exports that reference TFC data, keyed by package path.
        /// </summary>
        private readonly Dictionary<string, List<(int uIndex, string tfcName)>> textureRefsByPackage = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Every static parameter set used by a material in the game.
        /// </summary>
        private readonly HashSet<StaticParameterSet> usedStaticParameterSets = new();

        /// <summary>
        /// Set if a material could not be read. The shader cache is left alone if we can't be sure what's used.
        /// </summary>
        private bool materialScanFailed;

        private readonly MilanSlashManifest manifest = new() { MilanSlash = true, Version = MilanSlashApplier.SupportedSlashVersion };

        private MilanSlashGenerator(string gameRoot, string milanDeltaFolder, Action<string> setStatus)
        {
            this.gameRoot = Path.GetFullPath(gameRoot);
            this.milanDeltaFolder = milanDeltaFolder;
            this.setStatus = setStatus;
        }

        /// <summary>
        /// Generates a slash manifest and its delta files for the game at <paramref name="gameRoot"/>, writing them into
        /// <paramref name="milanDeltaFolder"/> (replacing any previous slash output). Does not modify the game.
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

            var bioGame = Path.Combine(gameRoot, "BioGame");
            var packages = Directory.EnumerateFiles(bioGame, "*.*", SearchOption.AllDirectories)
                .Where(x => PackageExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
                .Where(x => !Path.GetFileName(x).Equals(RefShaderCacheName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FindTFCs(bioGame);
            ScanPackages(packages);
            PlanTFCs();
            CreateTexturePackagePatches();
            SlashRefShaderCache(Path.Combine(bioGame, "CookedPCConsole", RefShaderCacheName));

            // Final file state is recorded later by RecordFinalState, once the installer has finished writing to the game
            manifest.Save(milanDeltaFolder);

            long before = manifest.TFCs.Sum(x => x.Sources.Sum(s => s.Size)) + manifest.Deletions.Sum(x => x.Size) + manifest.Patches.Sum(x => x.SourceFileSize);
            long after = manifest.TFCs.Sum(x => x.DestSize) + manifest.Patches.Sum(x => x.DestSize);
            M3Log.Information($"Generated slash manifest: {manifest.TFCs.Count} TFC(s) compacted, {manifest.Deletions.Count} file(s) deleted, {manifest.Patches.Count} file(s) patched. " +
                              $"Affected files go from {FileSize.FormatSize(before)} to roughly {FileSize.FormatSize(after)} (patched packages are measured decompressed)");
            return manifest;
        }

        #region Scanning

        private void FindTFCs(string bioGame)
        {
            foreach (var tfcPath in Directory.EnumerateFiles(bioGame, "*.tfc", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(tfcPath);
                if (tfcs.TryGetValue(name, out var existing))
                {
                    // We can't tell which of these the game will resolve a texture to
                    existing.Lock($"multiple TFCs named {name} are installed");
                    continue;
                }

                tfcs[name] = new TFCUsage(name, tfcPath, new FileInfo(tfcPath).Length);
            }
        }

        private static bool ShouldLoadExport(ExportEntry export)
        {
            var className = export.ClassName;
            return className.Contains("Texture", StringComparison.OrdinalIgnoreCase)
                   || className.Contains("Movie", StringComparison.OrdinalIgnoreCase)
                   || (className.Contains("Material", StringComparison.OrdinalIgnoreCase) && !className.Contains("Expression", StringComparison.OrdinalIgnoreCase));
        }

        private void ScanPackages(List<string> packages)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                var packagePath = packages[i];
                setStatus?.Invoke($"Scanning packages ({i + 1}/{packages.Count})");
                using var package = MEPackageHandler.UnsafePartialLoad(packagePath, ShouldLoadExport);
                foreach (var export in package.Exports.Where(ShouldLoadExport))
                {
                    ScanTextureExport(packagePath, export);
                    ScanMaterialExport(packagePath, export);
                }
            }

            M3Log.Information($"Slash scan: {packages.Count} packages, {textureRefsByPackage.Sum(x => x.Value.Count)} exports with TFC data, {usedStaticParameterSets.Count} used material static parameter sets");
        }

        private void ScanTextureExport(string packagePath, ExportEntry export)
        {
            var className = export.ClassName;
            if (!className.Contains("Texture", StringComparison.OrdinalIgnoreCase) && !className.Contains("Movie", StringComparison.OrdinalIgnoreCase))
                return;

            var tfcName = export.GetProperty<NameProperty>("TextureFileCacheName")?.Value.Instanced;
            if (string.IsNullOrEmpty(tfcName))
                return; // No external data

            if (!tfcs.TryGetValue(tfcName, out var tfc))
            {
                M3Log.Warning($"Slash scan: {export.InstancedFullPath} in {Path.GetFileName(packagePath)} references TFC {tfcName}, which is not installed");
                return;
            }

            try
            {
                bool hasExternalData = false;
                if (Texture2DClasses.Contains(className))
                {
                    var texture = ObjectBinary.From<UTexture2D>(export);
                    foreach (var mip in texture.Mips)
                    {
                        if (!HasExternalData(mip))
                            continue;
                        tfc.AddRange(mip.DataOffset, mip.CompressedSize);
                        hasExternalData = true;
                    }
                }
                else if (className == "TextureMovie")
                {
                    var movie = ObjectBinary.From<TextureMovie>(export);
                    if (movie.IsExternal)
                    {
                        tfc.AddRange(movie.DataOffset, movie.DataSize);
                        hasExternalData = true;
                    }
                }
                else
                {
                    // We don't know how this class stores its offsets, so we can't move its data
                    tfc.Lock($"{export.InstancedFullPath} ({className}) in {Path.GetFileName(packagePath)} references it and is not a supported texture class");
                    return;
                }

                if (hasExternalData)
                {
                    if (!textureRefsByPackage.TryGetValue(packagePath, out var refs))
                    {
                        refs = new List<(int, string)>();
                        textureRefsByPackage[packagePath] = refs;
                    }
                    refs.Add((export.UIndex, tfc.Name));
                }
            }
            catch (Exception e)
            {
                tfc.Lock($"could not read {export.InstancedFullPath} in {Path.GetFileName(packagePath)}: {e.Message}");
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

        private void ScanMaterialExport(string packagePath, ExportEntry export)
        {
            var className = export.ClassName;
            try
            {
                if (className is "Material" or "DecalMaterial")
                {
                    usedStaticParameterSets.Add((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID);
                }
                else if (className.Contains("MaterialInstance", StringComparison.OrdinalIgnoreCase))
                {
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        usedStaticParameterSets.Add(ObjectBinary.From<MaterialInstance>(export).SM3StaticParameterSet);
                    }
                }
                else if (className.Contains("Material", StringComparison.OrdinalIgnoreCase) && IsMaterialSubclass(export))
                {
                    // Subclass of Material we don't have a name for
                    usedStaticParameterSets.Add((StaticParameterSet)ObjectBinary.From<Material>(export).SM3MaterialResource.ID);
                }
            }
            catch (Exception e)
            {
                materialScanFailed = true;
                M3Log.Error($"Slash scan: could not read material {export.InstancedFullPath} in {Path.GetFileName(packagePath)}: {e.Message}");
            }
        }

        /// <summary>
        /// Catches subclasses of Material we don't know by name. Class lookups can fail for classes the object database
        /// doesn't know about; those aren't materials we can read anyway.
        /// </summary>
        private static bool IsMaterialSubclass(ExportEntry export)
        {
            try
            {
                return export.IsA("Material");
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region TFC compaction

        private void PlanTFCs()
        {
            foreach (var tfc in tfcs.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                setStatus?.Invoke($"Planning {tfc.Name}.tfc");
                if (tfc.LockReason != null)
                {
                    M3Log.Warning($"Slash: not compacting {tfc.Name}.tfc: {tfc.LockReason}");
                    continue;
                }

                if (tfc.Ranges.Count == 0)
                {
                    M3Log.Information($"Slash: {tfc.Name}.tfc is not used by anything and will be deleted");
                    manifest.Deletions.Add(new MilanSlashDeletionEntry { Filename = GetRelativePath(tfc.Path), Size = tfc.Size });
                    continue;
                }

                if (!tfc.BuildLayout(TFCHeaderSize, out var layoutError))
                {
                    M3Log.Warning($"Slash: not compacting {tfc.Name}.tfc: {layoutError}");
                    continue;
                }

                if (tfc.CompactedSize >= tfc.Size)
                {
                    M3Log.Information($"Slash: {tfc.Name}.tfc is fully used");
                    continue;
                }

                var relPath = GetRelativePath(tfc.Path);
                var entry = new MilanSlashTFCEntry
                {
                    Filename = relPath,
                    Sources = [new MilanSlashSourceFile { Filename = relPath, Size = tfc.Size }],
                    Segments = [new MilanSlashTFCSegment(0, 0, TFCHeaderSize), .. tfc.Intervals.Select(x => new MilanSlashTFCSegment(0, x.Start, x.Length))],
                    DestSize = tfc.CompactedSize,
                };
                entry.DestHash = HashTFCSegments(tfc.Path, entry.Segments);
                manifest.TFCs.Add(entry);
                tfc.IsCompacted = true;
                M3Log.Information($"Slash: {tfc.Name}.tfc {FileSize.FormatSize(tfc.Size)} -> {FileSize.FormatSize(tfc.CompactedSize)} ({entry.Segments.Count} segments)");
            }
        }

        private static string HashTFCSegments(string tfcPath, List<MilanSlashTFCSegment> segments)
        {
            using var fs = new FileStream(tfcPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            foreach (var segment in segments)
            {
                fs.Position = segment.Offset;
                long remaining = segment.Length;
                while (remaining > 0)
                {
                    int read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read <= 0)
                        throw new Exception($"Unexpected end of {tfcPath}");
                    hasher.AppendData(buffer, 0, read);
                    remaining -= read;
                }
            }
            return Convert.ToHexStringLower(hasher.GetHashAndReset());
        }

        private void CreateTexturePackagePatches()
        {
            var packagesToPatch = textureRefsByPackage
                .Where(x => x.Value.Any(r => tfcs[r.tfcName].IsCompacted))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < packagesToPatch.Count; i++)
            {
                var (packagePath, refs) = (packagesToPatch[i].Key, packagesToPatch[i].Value);
                setStatus?.Invoke($"Updating texture offsets ({i + 1}/{packagesToPatch.Count})");

                var fileBytes = File.ReadAllBytes(packagePath);
                using var package = MEPackageHandler.OpenMEPackageFromStream(new MemoryStream(fileBytes), packagePath);
                foreach (var (uIndex, tfcName) in refs)
                {
                    var tfc = tfcs[tfcName];
                    if (!tfc.IsCompacted)
                        continue;

                    var export = package.GetUExport(uIndex);
                    var originalBinaryLength = export.GetBinaryData().Length;
                    var binary = ObjectBinary.From(export);
                    if (binary is UTexture2D texture)
                    {
                        foreach (var mip in texture.Mips)
                        {
                            if (HasExternalData(mip))
                                mip.DataOffset = checked((int)tfc.Remap(mip.DataOffset));
                        }
                    }
                    else if (binary is TextureMovie { IsExternal: true } movie)
                    {
                        movie.DataOffset = checked((int)tfc.Remap(movie.DataOffset));
                    }
                    else
                    {
                        throw new Exception($"Unexpected binary type {binary?.GetType().Name} for {export.InstancedFullPath} in {packagePath}");
                    }

                    export.WriteBinary(binary);

                    // Only offsets changed, so a faithful round trip keeps the binary the same size
                    if (export.GetBinaryData().Length != originalBinaryLength)
                        throw new Exception($"Re-serializing {export.InstancedFullPath} in {packagePath} changed its size; texture binary does not round trip. Cannot safely slash this package.");
                }

                AddPackagePatch(packagePath, fileBytes, package, "Texture data offsets updated for compacted TFCs");
            }
        }

        #endregion

        #region Shader cache

        private void SlashRefShaderCache(string refShaderCachePath)
        {
            if (!File.Exists(refShaderCachePath))
            {
                M3Log.Warning("Slash: RefShaderCache is not installed; skipping shader slashing");
                return;
            }

            if (materialScanFailed || usedStaticParameterSets.Count == 0)
            {
                M3Log.Error("Slash: material scan was incomplete; the RefShaderCache will not be slashed as we cannot be sure which shaders are used");
                return;
            }

            setStatus?.Invoke("Slashing RefShaderCache");
            using var package = MEPackageHandler.OpenMEPackage(refShaderCachePath, forceLoadFromDisk: true);
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

            cache.MaterialShaderMaps = keptMaps;
            cache.Compact(); // Removes shaders no remaining shader map references
            M3Log.Information($"Slash: RefShaderCache keeps {keptMaps.Count}/{originalMaps} material shader maps and {cache.Shaders.Count}/{originalShaders} shaders");
            if (keptMaps.Count == originalMaps && cache.Shaders.Count == originalShaders)
                return; // Nothing to remove

            cacheExport.WriteBinary(cache);
            cache = null;

            AddPackagePatch(refShaderCachePath, null, package, "RefShaderCache reduced to shader maps used by materials in the game");
        }

        #endregion

        #region Patch creation

        /// <summary>
        /// Diffs a modified package against the file on disk and records the delta. Compressed packages use package mode
        /// (the same decompress/patch/recompress convention as MilanDelta); uncompressed ones are patched as raw files.
        /// </summary>
        /// <param name="fileBytes">Original file bytes, if already read.</param>
        private void AddPackagePatch(string packagePath, byte[] fileBytes, IMEPackage modifiedPackage, string comment)
        {
            fileBytes ??= File.ReadAllBytes(packagePath);
            bool compressed = modifiedPackage.IsCompressed;
            string mode = compressed ? MilanSlashPatchModes.Package : MilanSlashPatchModes.Raw;

            byte[] sourceData;
            if (compressed)
            {
                using var decompressed = MEPackage.GetDecompressedPackageStream(new MemoryStream(fileBytes));
                sourceData = decompressed.ToArray();
            }
            else
            {
                sourceData = fileBytes;
            }

            byte[] destData;
            using (var saved = modifiedPackage.SaveToStream(false))
            {
                destData = saved.ToArray();
            }

            var relPath = GetRelativePath(packagePath);
            if (sourceData.AsSpan().SequenceEqual(destData))
            {
                M3Log.Information($"Slash: {relPath} is unchanged; no delta needed");
                return;
            }

            setStatus?.Invoke($"Creating delta for {Path.GetFileName(packagePath)}");
            var deltaRelPath = Path.Combine(MilanSlashApplier.SlashFolderName, relPath + ".jsf");
            var deltaPath = MilanSlashApplier.Resolve(milanDeltaFolder, deltaRelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(deltaPath));
            using (var deltaStream = new FileStream(deltaPath, FileMode.Create, FileAccess.Write))
            {
                JsfDeltaBuilder.Build(sourceData, destData, deltaStream);
            }

            manifest.Patches.Add(new MilanSlashPatchEntry
            {
                Comment = comment,
                Filename = relPath,
                Delta = deltaRelPath.Replace(Path.DirectorySeparatorChar, '\\'),
                Mode = mode,
                Compressed = compressed,
                SourceFileSize = fileBytes.LongLength,
                SourceFileHash = MilanSlashHashing.HashBytes(fileBytes),
                SourceSize = sourceData.LongLength,
                SourceHash = MilanSlashHashing.HashBytes(sourceData),
                DestSize = destData.LongLength,
                DestHash = MilanSlashHashing.HashBytes(destData),
            });

            M3Log.Information($"Slash: {relPath} delta is {FileSize.FormatSize(new FileInfo(deltaPath).Length)} ({mode}, {FileSize.FormatSize(sourceData.LongLength)} -> {FileSize.FormatSize(destData.LongLength)})");
        }

        /// <summary>
        /// Path relative to the game root, in the Windows form stored in manifests.
        /// </summary>
        private string GetRelativePath(string fullPath) => Path.GetRelativePath(gameRoot, fullPath).Replace(Path.DirectorySeparatorChar, '\\');

        #endregion

        /// <summary>
        /// Tracks which byte ranges of a TFC are used, and how they are laid out once compacted.
        /// </summary>
        private class TFCUsage
        {
            public string Name { get; }
            public string Path { get; }
            public long Size { get; }

            /// <summary>
            /// If set, this TFC must not be modified.
            /// </summary>
            public string LockReason { get; private set; }

            public List<(long Start, long Length)> Ranges { get; } = new();

            /// <summary>
            /// Merged used ranges in source order, with where each one starts in the compacted TFC.
            /// </summary>
            public List<(long Start, long Length, long NewStart)> Intervals { get; } = new();

            public long CompactedSize { get; private set; }
            public bool IsCompacted { get; set; }

            public TFCUsage(string name, string path, long size)
            {
                Name = name;
                Path = path;
                Size = size;
            }

            public void Lock(string reason) => LockReason ??= reason;

            public void AddRange(long start, long length) => Ranges.Add((start, length));

            /// <summary>
            /// Merges overlapping and adjacent ranges and lays them out sequentially after the header. Overlaps are merged
            /// rather than deduplicated so that any offset inside a used range still maps correctly.
            /// </summary>
            public bool BuildLayout(int headerSize, out string error)
            {
                error = null;
                foreach (var (start, length) in Ranges)
                {
                    if (length <= 0 || start < headerSize || start + length > Size)
                    {
                        error = $"texture data at 0x{start:X} (0x{length:X} bytes) lies outside the file ({Size} bytes)";
                        return false;
                    }
                }

                long newStart = headerSize;
                foreach (var (start, length) in Ranges.OrderBy(x => x.Start))
                {
                    if (Intervals.Count > 0)
                    {
                        var last = Intervals[^1];
                        if (start <= last.Start + last.Length)
                        {
                            long end = Math.Max(last.Start + last.Length, start + length);
                            newStart += end - (last.Start + last.Length);
                            Intervals[^1] = (last.Start, end - last.Start, last.NewStart);
                            continue;
                        }
                    }

                    Intervals.Add((start, length, newStart));
                    newStart += length;
                }

                CompactedSize = newStart;
                return true;
            }

            /// <summary>
            /// Maps an offset inside a used range to its offset in the compacted TFC.
            /// </summary>
            public long Remap(long offset)
            {
                int lo = 0, hi = Intervals.Count - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    var interval = Intervals[mid];
                    if (offset < interval.Start)
                        hi = mid - 1;
                    else if (offset >= interval.Start + interval.Length)
                        lo = mid + 1;
                    else
                        return interval.NewStart + (offset - interval.Start);
                }

                throw new Exception($"Offset 0x{offset:X} is not in a used range of {Name}.tfc");
            }
        }
    }
}
