using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge.LinkerPatches;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.milan.slash;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// Everything the base installer will do, worked out before the installation directory is touched: which LE3 files are
    /// copied where, which MilanDelta and slash operations apply to them, and which slash operations target files the
    /// Project Milan mod installs. Building the plan checks everything that can be checked cheaply up front, so an install
    /// that cannot succeed fails in seconds rather than partway through copying several GB.
    /// <para>
    /// Slash is applied inline: packages are patched as they are copied, compacted TFCs/AFCs are assembled straight from
    /// LE3's files, and files the slash manifest deletes are never copied at all.
    /// </para>
    /// </summary>
    internal class MilanInstallPlan
    {
        /// <summary>
        /// A file copied from LE3 into the installation.
        /// </summary>
        internal class CopyEntry
        {
            /// <summary>
            /// Path of the file in the installation, relative to its root.
            /// </summary>
            public string DestRelativePath { get; init; }

            /// <summary>
            /// The file in the LE3 installation being copied from. It is resolved to a vanilla copy when it is copied.
            /// </summary>
            public string SourcePath { get; init; }

            /// <summary>
            /// Size of the file as it will be copied: the size of the source, or of the LE3 backup's copy if the source's size
            /// is not vanilla.
            /// </summary>
            public long SourceSize { get; set; }

            /// <summary>
            /// MilanDelta patch applied to the file as it is copied, if any.
            /// </summary>
            public MilanDeltaFileEntry Delta { get; set; }

            /// <summary>
            /// Slash patch applied to the file once it has been copied (and MilanDelta patched, and converted to LMP), if any.
            /// </summary>
            public MilanSlashPatchEntry SlashPatch { get; set; }

            /// <summary>
            /// If the file is copied as-is, so its data in the installation is identical to the LE3 source's.
            /// </summary>
            public bool IsUnmodifiedCopy => Delta == null && SlashPatch == null && !LMPLicensee.AppliesTo(DestRelativePath);
        }

        /// <summary>
        /// Files to copy from LE3.
        /// </summary>
        public List<CopyEntry> Copies { get; } = new();

        /// <summary>
        /// Every file resolved from LE3, keyed by its path in the installation - including files that are not copied because
        /// slash deletes or rebuilds them. Slash builds read their sources from here.
        /// </summary>
        public CaseInsensitiveDictionary<CopyEntry> LE3Files { get; } = new();

        /// <summary>
        /// Files that are part of the installation but are not copied into it, because the slash generator reads them from
        /// LE3 where they are: the basegame TFCs, which are many GB, and which slash almost entirely deletes once their used
        /// data has been compacted. Only used when generating a slash manifest. These are also in <see cref="LE3Files"/>.
        /// </summary>
        public List<CopyEntry> LeftInLE3 { get; } = new();

        /// <summary>
        /// Paths, relative to the installation root, of every file the Project Milan mod installs.
        /// </summary>
        public HashSet<string> ModOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The slash manifest being applied, or null if the installation is not slashed.
        /// </summary>
        public MilanSlashManifest Slash { get; private set; }

        /// <summary>
        /// Slash builds assembled straight from LE3 files while copying.
        /// </summary>
        public List<MilanSlashBuildEntry> InlineBuilds { get; } = new();

        /// <summary>
        /// Slash patches applied to files the Project Milan mod installs, as it installs them, keyed by installation path.
        /// </summary>
        public CaseInsensitiveDictionary<MilanSlashPatchEntry> ModPatches { get; } = new();

        /// <summary>
        /// Slash builds that need files the Project Milan mod installs, run once it has been installed.
        /// </summary>
        public List<MilanSlashBuildEntry> PostInstallBuilds { get; } = new();

        /// <summary>
        /// Slash deletions of files the Project Milan mod installs, run once it has been installed.
        /// </summary>
        public List<MilanSlashDeletionEntry> PostInstallDeletions { get; } = new();

        /// <summary>
        /// Files in the finished installation that the linker patch merge writes (BioGame\LinkerPatches), from the linker
        /// patch manifests the Project Milan mod installs. Paths are relative to the installation root.
        /// </summary>
        public HashSet<string> LinkerPatchOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

        #region Building

        /// <summary>
        /// Builds and validates the plan. Nothing is written.
        /// </summary>
        /// <param name="sources">The LE3 installation being copied from.</param>
        /// <param name="manifests">What to copy from LE3.</param>
        /// <param name="voLanguages">Languages whose localized packages are copied along with the packages that are requested.</param>
        /// <param name="milanDeltas">MilanDelta entries, keyed by file name.</param>
        /// <param name="milanDeltaFolder">Folder of the MilanDelta manifest, its patches, and the slash manifest and its files.</param>
        /// <param name="slash">The slash manifest to apply inline, or null to not slash the installation.</param>
        /// <param name="modOutputs">Paths, relative to the installation root, of every file the Project Milan mod installs.</param>
        /// <param name="leaveTFCsInLE3">If the basegame TFCs should be left in LE3 for the slash generator to read (<see cref="LeftInLE3"/>) rather than copied. Only for generating a slash manifest; <paramref name="slash"/> must be null.</param>
        /// <exception cref="Exception">The installation cannot succeed. Every problem found is logged; the message lists them.</exception>
        public static MilanInstallPlan Build(LE3SourceResolver sources, IEnumerable<MilanBaseInstaller.CopyManifest> manifests, IReadOnlyList<MELocalization> voLanguages,
            IReadOnlyDictionary<string, MilanDeltaFileEntry> milanDeltas, string milanDeltaFolder, MilanSlashManifest slash, IEnumerable<string> modOutputs, bool leaveTFCsInLE3 = false, bool allowStaleSlash = false)
        {
            if (leaveTFCsInLE3 && slash != null)
                throw new ArgumentException(@"TFCs can only be left in LE3 when generating a slash manifest, not when applying one");

            var plan = new MilanInstallPlan();
            plan.ModOutputs.UnionWith(modOutputs);

            var problems = new List<string>();
            plan.ResolveCopies(sources, manifests, voLanguages, problems);
            plan.CheckSources(sources, problems);
            plan.AttachMilanDeltas(milanDeltas, milanDeltaFolder, problems);

            // Files the mod installs replace whatever was copied; there's no point copying them
            foreach (var copy in plan.Copies.Where(x => plan.ModOutputs.Contains(x.DestRelativePath)).ToList())
            {
                M3Log.Information($@"Not copying {copy.DestRelativePath} from LE3: it is installed by Project Milan");
                plan.Copies.Remove(copy);
                plan.LE3Files.Remove(copy.DestRelativePath);
            }

            if (leaveTFCsInLE3)
            {
                foreach (var copy in plan.Copies.Where(x => IsBasegameTFC(x.DestRelativePath) && x.IsUnmodifiedCopy).ToList())
                {
                    plan.Copies.Remove(copy);
                    plan.LeftInLE3.Add(copy);
                }
                M3Log.Information($@"Not copying {plan.LeftInLE3.Count} TFC(s) ({FileSize.FormatSize(plan.LeftInLE3.Sum(x => x.SourceSize))}) from LE3: the slash generator reads them from LE3, and only the ones the slashed game still uses are copied");
            }

            if (slash != null)
            {
                var slashProblems = plan.PlanSlash(slash, milanDeltaFolder);
                if (slashProblems.Count > 0)
                {
                    if (allowStaleSlash)
                    {
                        // In development the mod is rebuilt far more often than its slash manifest is regenerated. Don't fail the
                        // whole install over a stale manifest; just skip it. Release builds treat this as an invalid build.
                        foreach (var problem in slashProblems)
                        {
                            M3Log.Warning($@"Slash planning: {problem}");
                        }
                        M3Log.Warning(@"The slash manifest does not match this installation, most likely because the Project Milan mod was rebuilt since it was generated. Skipping slash; regenerate the slash manifest to fix this.");
                        plan.ClearSlash();
                    }
                    else
                    {
                        problems.AddRange(slashProblems);
                    }
                }
            }

            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    M3Log.Error($@"Installation planning: {problem}");
                }
                throw new Exception($"Project Milan cannot be installed from this LE3 installation ({problems.Count} problem(s); see the log for all of them). First problem: {problems[0]}");
            }

            M3Log.Information($@"Installation plan: {plan.Copies.Count} file(s) copied from LE3 ({plan.Copies.Count(x => x.Delta != null)} MilanDelta patched, {plan.Copies.Count(x => x.SlashPatch != null)} slash patched), {plan.LeftInLE3.Count} left in LE3 for the slash generator, " +
                              $@"{plan.InlineBuilds.Count} slash build(s) from LE3 files, {plan.ModPatches.Count} slash patch(es) of Project Milan files, " +
                              $@"{plan.PostInstallBuilds.Count} post-install slash build(s), {plan.PostInstallDeletions.Count} post-install slash deletion(s), {plan.LinkerPatchOutputs.Count} file(s) from the linker patch merge");
            return plan;
        }

        /// <summary>
        /// If the file is a linker patch manifest in a DLC's LinkerPatches folder, which the linker patch merge builds BioGame\LinkerPatches from.
        /// </summary>
        private static bool IsLinkerPatchManifest(string relativePath)
        {
            var parts = relativePath.Replace('/', '\\').Split('\\');
            return parts.Length == 5 && parts[0].Equals(@"BioGame", StringComparison.OrdinalIgnoreCase) && parts[1].Equals(@"DLC", StringComparison.OrdinalIgnoreCase)
                   && parts[3].Equals(LinkerPatchMerge.LINKER_PATCH_DLC_FOLDERNAME, StringComparison.OrdinalIgnoreCase)
                   && parts[4].EndsWith(LinkerPatchMerge.LINKER_PATCH_MANIFEST_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// If the file is written by the linker patch merge.
        /// </summary>
        private static bool IsLinkerPatchOutput(string relativePath) =>
            relativePath.Replace('/', '\\').StartsWith($@"BioGame\{LinkerPatchMerge.LINKER_PATCH_OUTPUT_FOLDERNAME}\", StringComparison.OrdinalIgnoreCase);

        private static bool IsBasegameTFC(string relativePath) =>
            relativePath.Replace('/', '\\').StartsWith(@"BioGame\CookedPCConsole\", StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith(@".tfc", StringComparison.OrdinalIgnoreCase);

        private void AddCopy(string destRelativePath, string sourcePath)
        {
            if (LE3Files.TryGetValue(destRelativePath, out var existing))
            {
                if (existing.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase))
                    return;

                // Later manifests win, as they did when files were copied in manifest order
                M3Log.Warning($@"{destRelativePath} is copied from both {existing.SourcePath} and {sourcePath}; using {sourcePath}");
                Copies.Remove(existing);
            }

            var entry = new CopyEntry { DestRelativePath = destRelativePath, SourcePath = sourcePath };
            Copies.Add(entry);
            LE3Files[destRelativePath] = entry;
        }

        /// <summary>
        /// Works out every file that is copied from LE3, and where it goes.
        /// </summary>
        private void ResolveCopies(LE3SourceResolver sources, IEnumerable<MilanBaseInstaller.CopyManifest> manifests, IReadOnlyList<MELocalization> voLanguages, List<string> problems)
        {
            // Pre-scan the entire source folder tree once into a case-insensitive lookup table by file name
            var filesInSourceFolder = MELoadedFiles.GetAllFilesLoadedInGame(MEGame.LE3, forceReload: true, includeModDLC: false, gameRootOverride: sources.SourceRoot);

            foreach (var manifest in manifests)
            {
                var sourceBasePath = Path.Combine(sources.SourceRoot, manifest.SourceSubFolder);
                if (!Directory.Exists(sourceBasePath))
                {
                    problems.Add($"{manifest.SourceSubFolder} does not exist in {sources.SourceRoot}");
                    continue;
                }

                string destPath(string file) => Path.Combine(manifest.DestSubFolder, manifest.UseRelativePathing ? Path.GetRelativePath(sourceBasePath, file) : Path.GetFileName(file));

                // 1. Explicitly requested files, falling back to their localized variants (e.g. File_INT.pcc)
                foreach (var fileName in manifest.Files)
                {
                    if (filesInSourceFolder.TryGetValue(fileName, out var file))
                    {
                        AddCopy(destPath(file), file);

                        // Requested packages and AFCs bring their localizations with them
                        var extension = Path.GetExtension(file);
                        if (extension.Equals(@".pcc", StringComparison.OrdinalIgnoreCase) || extension.Equals(@".afc", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var localizedFile in GetLocalizationsOfFile(file, voLanguages))
                            {
                                AddCopy(Path.Combine(manifest.DestSubFolder, Path.GetFileName(localizedFile)), localizedFile);
                            }
                        }
                        continue;
                    }

                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    var localizedMatches = filesInSourceFolder.Values.Where(f =>
                    {
                        var fn = Path.GetFileName(f);
                        return Path.GetFileNameWithoutExtension(fn).StartsWith(baseName + @"_", StringComparison.OrdinalIgnoreCase)
                               && Path.GetExtension(fn).Equals(ext, StringComparison.OrdinalIgnoreCase)
                               && sources.IsInVanillaDatabase(f); // Not files mods left in the game
                    }).ToList();

                    if (localizedMatches.Count == 0)
                    {
                        // 11/07/2026 - Skip localization files that are missing except INT
                        if (MilanLocalizationFiles.IsOptionalLocalization(fileName))
                        {
                            M3Log.Information($@"Not copying missing localization file {fileName}");
                            continue;
                        }

                        problems.Add($"{fileName} does not exist in {sources.SourceRoot}");
                        continue;
                    }

                    foreach (var match in localizedMatches)
                    {
                        AddCopy(destPath(match), match);
                    }
                }

                // 2. Additional wildcards (e.g., *.tfc). Only files that are part of vanilla LE3 are copied; mods can leave
                // their own files in the basegame folders (such as Textures_DLC_MOD_*.tfc)
                if (manifest.ExtraPatterns != null)
                {
                    foreach (var pattern in manifest.ExtraPatterns)
                    {
                        foreach (var file in Directory.EnumerateFiles(sourceBasePath, pattern, SearchOption.AllDirectories))
                        {
                            if (sources.IsInVanillaDatabase(file))
                            {
                                AddCopy(destPath(file), file);
                            }
                            else
                            {
                                M3Log.Information($@"Not copying {file}: it is not a vanilla LE3 file");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Localized versions of a file that exist next to it in LE3, both LOC_ and _LANG styles.
        /// </summary>
        private static IEnumerable<string> GetLocalizationsOfFile(string file, IReadOnlyList<MELocalization> voLanguages)
        {
            var dir = Directory.GetParent(file).FullName;
            foreach (var loc in voLanguages)
            {
                foreach (var locPrefix in new[] { true, false })
                {
                    var testPath = Path.Combine(dir, Path.GetFileName(file.SetUnrealLocalization(MEGame.LMP, loc, locPrefix)));
                    if (File.Exists(testPath))
                        yield return testPath;
                }
            }
        }

        /// <summary>
        /// Checks every source has a copy of the right size, either in the LE3 installation or in the LE3 backup. Full hash
        /// checks are done when files are copied.
        /// </summary>
        private void CheckSources(LE3SourceResolver sources, List<string> problems)
        {
            foreach (var copy in Copies)
            {
                if (sources.TryCheckSize(copy.SourcePath, out var size, out var problem))
                {
                    copy.SourceSize = size;
                }
                else
                {
                    problems.Add(problem);
                }
            }
        }

        private void AttachMilanDeltas(IReadOnlyDictionary<string, MilanDeltaFileEntry> milanDeltas, string milanDeltaFolder, List<string> problems)
        {
            if (milanDeltas == null)
                return;

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var copy in Copies)
            {
                if (!milanDeltas.TryGetValue(Path.GetFileName(copy.DestRelativePath), out var delta))
                    continue;

                copy.Delta = delta;
                used.Add(delta.Filename);
                if (!File.Exists(Path.Combine(milanDeltaFolder, delta.Delta)))
                    problems.Add($"Milan delta patch file is missing: {delta.Delta}");
            }

            foreach (var unused in milanDeltas.Keys.Where(x => !used.Contains(x)))
            {
                M3Log.Warning($@"MilanDelta has a patch for {unused}, but that file is not copied from LE3; it will not be applied");
            }
        }

        /// <summary>
        /// Decides where each slash operation is applied, and checks it against the files it will be applied to. Returns a
        /// list of problems; the plan is only changed if there are none.
        /// </summary>
        private List<string> PlanSlash(MilanSlashManifest slash, string milanDeltaFolder)
        {
            var problems = MilanSlashApplier.ValidateStructure(slash, milanDeltaFolder);
            if (problems.Count > 0)
                return problems;

            var patchedCopies = new List<(CopyEntry copy, MilanSlashPatchEntry patch)>();
            var notCopied = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Deleted, or built by slash
            var inlineBuilds = new List<MilanSlashBuildEntry>();
            var modPatches = new CaseInsensitiveDictionary<MilanSlashPatchEntry>();
            var postInstallBuilds = new List<MilanSlashBuildEntry>();
            var postInstallDeletions = new List<MilanSlashDeletionEntry>();

            foreach (var deletion in slash.Deletions)
            {
                if (LE3Files.TryGetValue(deletion.Filename, out var copy))
                {
                    if (copy.Delta == null && copy.SourceSize != deletion.Size)
                        problems.Add($"{deletion.Filename} is {copy.SourceSize} bytes, but the slash manifest expects {deletion.Size}");
                    notCopied.Add(deletion.Filename);
                }
                else if (ModOutputs.Contains(deletion.Filename))
                {
                    postInstallDeletions.Add(deletion);
                }
                // Otherwise it isn't in the installation to begin with
            }

            foreach (var build in slash.Builds)
            {
                bool needsModFiles = ModOutputs.Contains(build.Filename);
                foreach (var source in build.Sources.Where(x => !x.Payload))
                {
                    if (ModOutputs.Contains(source.Filename))
                    {
                        needsModFiles = true;
                    }
                    else if (LE3Files.TryGetValue(source.Filename, out var copy))
                    {
                        // Built straight from LE3's file, so the installation's copy must be identical to it
                        if (!copy.IsUnmodifiedCopy)
                            problems.Add($"{build.Filename} is built from {source.Filename}, which is modified when it is installed; slash can only build from files that are copied as-is");
                        else if (copy.SourceSize != source.Size)
                            problems.Add($"{source.Filename} is {copy.SourceSize} bytes, but the slash manifest expects {source.Size}");
                    }
                    else
                    {
                        problems.Add($"{build.Filename} is built from {source.Filename}, which is not part of the installation");
                    }
                }

                if (needsModFiles)
                {
                    postInstallBuilds.Add(build);
                }
                else
                {
                    inlineBuilds.Add(build);
                    if (LE3Files.ContainsKey(build.Filename))
                        notCopied.Add(build.Filename); // The build replaces it
                }
            }

            foreach (var patch in slash.Patches)
            {
                if (ModOutputs.Contains(patch.Filename))
                {
                    modPatches[patch.Filename] = patch;
                }
                else if (LE3Files.TryGetValue(patch.Filename, out var copy))
                {
                    // The licensee change doesn't change the size; a MilanDelta patch does, and is verified when applied
                    if (copy.Delta == null && copy.SourceSize != patch.SourceFileSize)
                        problems.Add($"{patch.Filename} is {copy.SourceSize} bytes, but the slash manifest expects {patch.SourceFileSize}");
                    patchedCopies.Add((copy, patch));
                }
                else if (MilanLocalizationFiles.IsOptionalLocalization(patch.Filename))
                {
                    // Users' LE3 installations may not have every localization; those files are simply skipped
                    M3Log.Information($@"Slash: skipping {patch.Filename}, a localization this installation does not have");
                }
                else
                {
                    problems.Add($"Slash patches {patch.Filename}, which is not part of the installation");
                }
            }

            // The final state recorded in the manifest is every file a finished installation has. LE3 files that aren't in it
            // are never used - TFCs and AFCs nothing references, the originals of compacted ones, files mods left in LE3 - so
            // they aren't copied at all. Slash builds read their sources straight from LE3, so they don't need copies either.
            var unused = new List<string>();
            var linkerPatchOutputs = new List<string>();
            if (slash.FinalFiles.Count > 0)
            {
                var finalFiles = slash.FinalFiles.Select(x => x.Filename).ToHashSet(StringComparer.OrdinalIgnoreCase);
                unused.AddRange(Copies.Select(x => x.DestRelativePath)
                    .Where(x => !notCopied.Contains(x) && !finalFiles.Contains(x) && !MilanFinalState.IsExcluded(x)));

                // Everything in the final state has to come from somewhere; if it doesn't, the installation can't match it
                var produced = new HashSet<string>(Copies.Select(x => x.DestRelativePath), StringComparer.OrdinalIgnoreCase);
                produced.ExceptWith(notCopied);
                produced.UnionWith(slash.Builds.Select(x => x.Filename));
                produced.UnionWith(ModOutputs);

                // The linker patch merge builds BioGame\LinkerPatches once the mod is installed, if the mod ships linker patches
                bool modHasLinkerPatches = ModOutputs.Any(IsLinkerPatchManifest);
                foreach (var file in finalFiles)
                {
                    if (produced.Contains(file) || MilanFinalState.IsExcluded(file) || MilanLocalizationFiles.IsOptionalLocalization(file))
                        continue;

                    if (modHasLinkerPatches && IsLinkerPatchOutput(file))
                        linkerPatchOutputs.Add(file);
                    else
                        problems.Add($"{file} is part of a finished installation, but nothing installs it");
                }
            }
            else
            {
                M3Log.Warning(@"The slash manifest has no recorded final state, so every LE3 file it doesn't delete is copied");
            }

            if (problems.Count > 0)
                return problems;

            // Commit
            Slash = slash;
            foreach (var (copy, patch) in patchedCopies)
            {
                copy.SlashPatch = patch;
            }
            Copies.RemoveAll(x => notCopied.Contains(x.DestRelativePath));
            InlineBuilds.AddRange(inlineBuilds);
            foreach (var (path, patch) in modPatches)
            {
                ModPatches[path] = patch;
            }
            PostInstallBuilds.AddRange(postInstallBuilds);
            PostInstallDeletions.AddRange(postInstallDeletions);
            LinkerPatchOutputs.UnionWith(linkerPatchOutputs);

            foreach (var path in notCopied)
            {
                M3Log.Information($@"Not copying {path} from LE3: slash {(InlineBuilds.Any(x => x.Filename.Equals(path, StringComparison.OrdinalIgnoreCase)) ? @"rebuilds" : @"deletes")} it");
            }

            Copies.RemoveAll(x => unused.Contains(x.DestRelativePath, StringComparer.OrdinalIgnoreCase));
            foreach (var path in unused)
            {
                M3Log.Information($@"Not copying {path} from LE3: it is not part of a finished installation");
            }
            return problems;
        }

        private void ClearSlash()
        {
            Slash = null;
            foreach (var copy in Copies)
            {
                copy.SlashPatch = null;
            }
            InlineBuilds.Clear();
            ModPatches.Clear();
            PostInstallBuilds.Clear();
            LinkerPatchOutputs.Clear();
            PostInstallDeletions.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Resolves files in the LE3 installation Project Milan is built from to vanilla copies of them. Everything Project Milan
    /// builds on - the MilanDelta patches, the slash manifest and the final state it records - expects vanilla LE3 files.
    /// </summary>
    internal class LE3SourceResolver
    {
        /// <summary>
        /// Files copied from LE3 that don't have to be vanilla: the executable
        /// (it is never tracked or patched), and the bink dll (the bink bypass install replaces it). Relative to the LE3 root.
        /// </summary>
        private static readonly string[] UncheckedSourceFiles =
        [
            @"Binaries\Win64\MassEffect3.exe",
            @"Binaries\Win64\bink2w64.dll",
        ];

        /// <summary>
        /// Root of the LE3 installation files are copied from.
        /// </summary>
        public string SourceRoot { get; }

        /// <summary>
        /// Root of the LE3 vanilla backup, if there is one (and it isn't the source). Used for files the source has modified.
        /// </summary>
        public string BackupRoot { get; }

        private readonly CaseInsensitiveConcurrentDictionary<List<(int size, string md5)>> vanillaDatabase = VanillaDatabaseService.LoadDatabaseFor(MEGame.LE3);

        public LE3SourceResolver(string sourceRoot, string backupRoot)
        {
            SourceRoot = Path.GetFullPath(sourceRoot);
            if (backupRoot != null && Path.GetFullPath(backupRoot).TrimEnd('\\').Equals(SourceRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                backupRoot = null; // Building from the backup itself
            }
            BackupRoot = backupRoot;
        }

        public string GetRelativePath(string sourceFile) => Path.GetRelativePath(SourceRoot, sourceFile);

        public bool IsInVanillaDatabase(string sourceFile) => vanillaDatabase.ContainsKey(GetRelativePath(sourceFile));

        private static bool IsUnchecked(string relativePath) => UncheckedSourceFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cheaply checks there is a vanilla copy of a file by size alone
        /// </summary>
        /// <param name="size">The size of the copy that will be used.</param>
        /// <param name="problem">Why there is no usable copy, if there isn't one.</param>
        public bool TryCheckSize(string sourceFile, out long size, out string problem)
        {
            problem = null;
            var relativePath = GetRelativePath(sourceFile);
            var info = new FileInfo(sourceFile);
            if (!info.Exists)
            {
                size = 0;
                problem = $"{relativePath} does not exist in {SourceRoot}";
                return false;
            }

            size = info.Length;
            if (IsUnchecked(relativePath) || !vanillaDatabase.TryGetValue(relativePath, out var vanillaVersions))
            {
                return true; // Not checked
            }

            if (vanillaVersions.Any(x => x.size == info.Length))
            {
                return true;
            }

            if (BackupRoot != null)
            {
                var backupInfo = new FileInfo(Path.Combine(BackupRoot, relativePath));
                if (backupInfo.Exists && vanillaVersions.Any(x => x.size == backupInfo.Length))
                {
                    size = backupInfo.Length;
                    return true;
                }
            }

            problem = $"Unable to obtain an unmodified copy of {relativePath}. Your version is modified, and there is no vanilla copy of it in an LE3 backup. Project Milan must be built from vanilla LE3 files, repair the game to install Project Milan.";
            return false;
        }

        /// <summary>
        /// Returns a vanilla copy of a file from the LE3 installation being copied from: the file itself if it is vanilla,
        /// otherwise the LE3 backup's copy. A modified file is caught here rather than failing the installation at the end.
        /// Throws if there is no vanilla copy.
        /// </summary>
        public string GetVanillaSourceFile(string sourceFile)
        {
            var relativePath = GetRelativePath(sourceFile);
            if (IsUnchecked(relativePath))
            {
                return sourceFile;
            }

            if (!vanillaDatabase.ContainsKey(relativePath))
            {
                M3Log.Warning($@"{relativePath} is not in the LE3 vanilla database; it cannot be checked for modifications");
                return sourceFile;
            }

            if (IsVanillaLE3File(sourceFile, relativePath))
            {
                return sourceFile;
            }

            if (BackupRoot != null)
            {
                var backupFile = Path.Combine(BackupRoot, relativePath);
                if (File.Exists(backupFile) && IsVanillaLE3File(backupFile, relativePath))
                {
                    M3Log.Warning($@"{relativePath} is modified in {SourceRoot}; using the vanilla copy in the LE3 backup instead");
                    return backupFile;
                }
            }

            throw new Exception($"{relativePath} in your LE3 installation is modified, and there is no vanilla copy of it in an LE3 backup. Project Milan must be built from vanilla LE3 files: restore this file, or create an LE3 backup.");
        }

        /// <summary>
        /// Checks a file against the LE3 vanilla database. TFCs and AFCs are only checked by size: they are many GB in total,
        /// and mods that change them append to them.
        /// </summary>
        private static bool IsVanillaLE3File(string path, string relativePath)
        {
            var extension = Path.GetExtension(relativePath);
            bool sizeOnly = extension.Equals(@".tfc", StringComparison.OrdinalIgnoreCase) || extension.Equals(@".afc", StringComparison.OrdinalIgnoreCase);
            return VanillaDatabaseService.IsFileVanilla(MEGame.LE3, path, relativePath, false, md5check: !sizeOnly);
        }
    }
}
