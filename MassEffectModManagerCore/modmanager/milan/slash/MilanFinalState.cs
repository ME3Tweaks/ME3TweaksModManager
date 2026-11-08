using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge.LinkerPatches;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Records and verifies the size and hash of every file in a finished Project Milan installation, so an end user's
    /// installation can be checked against the one the slash manifest was generated from, file for file.
    /// </summary>
    internal static class MilanFinalState
    {
        /// <summary>
        /// List of files to ignore for verification - they can vary between games
        /// </summary>
        private static readonly string[] ExcludedPaths =
        [
            // Metadata file
            @"\_metacmm.txt",
            // Game exe (not renamed)
            @"Binaries\Win64\MassEffect3.exe",
            // Game exe (renamed)
            @"Binaries\Win64\MassEffectMultiplayer.exe",
            // Bink bypass. This game requires our version, so don't bother with it
            @"Binaries\Win64\bink2w64.dll",
            @"Binaries\Win64\bink2w64_original.dll",
            // ASIs are managed separately from the game content
            @"Binaries\Win64\ASI\",
            // DRM activation dlls are also excluded
            // Ignore TOC
            @"\PCConsoleTOC.bin",
            // Linker patches (not used yet)
            $@"BioGame\{LinkerPatchMerge.LINKER_PATCH_OUTPUT_FOLDERNAME}\{LinkerPatchMerge.LINKER_PATCH_BUILD_RECORD_FILENAME}",
        ];

        /// <summary>
        /// DRM activation dlls.
        /// </summary>
        private static readonly string[] DRMFileNames =
        [
            @"Activation.dll",
            @"Activation64.dll",
        ];

        /// <summary>
        /// If the file (any path, or just a file name) is a DRM activation dll; see <see cref="DRMFileNames"/>.
        /// </summary>
        public static bool IsDRMFile(string path) => DRMFileNames.Contains(Path.GetFileName(path.Replace('\\', Path.DirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// If the given game-relative path (Windows separators) is excluded from final state checks.
        /// </summary>
        public static bool IsExcluded(string relativePath)
        {
            if (IsDRMFile(relativePath))
            {
                return true;
            }

            foreach (var path in ExcludedPaths)
            {
                if (path.StartsWith('\\'))
                {
                    if (relativePath.Equals(path[1..], StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (path.EndsWith('\\'))
                {
                    if (relativePath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (relativePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Hashes every (non-excluded) file in the game, sorted by path.
        /// </summary>
        public static List<MilanSlashFinalFileEntry> Capture(string gameRoot, Action<string> setStatus = null)
        {
            var files = GetGameFiles(gameRoot);
            var entries = new ConcurrentBag<MilanSlashFinalFileEntry>();
            int done = 0;
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, file =>
            {
                entries.Add(new MilanSlashFinalFileEntry
                {
                    Filename = file.relativePath,
                    Size = new FileInfo(file.fullPath).Length,
                    Hash = MUtilities.CalculateHash(file.fullPath, @"sha256")
                });
                setStatus?.Invoke($"Recording final file hashes ({Interlocked.Increment(ref done)}/{files.Count})");
            });

            return entries.OrderBy(x => x.Filename, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Compares the game against the expected final state. Returns a list of differences; empty means the installation
        /// matches exactly (excluding <see cref="ExcludedPaths"/>). Non-INT localization files may be missing, as they depend
        /// on which localizations the user's LE3 installation has (see <see cref="MilanLocalizationFiles"/>).
        /// </summary>
        /// <param name="allowedExtraFiles">Game-relative paths (Windows separators) that are allowed to exist without being in the expected state, such as files installed only on some systems</param>
        public static List<string> Verify(IReadOnlyList<MilanSlashFinalFileEntry> expected, string gameRoot, Action<string> setStatus = null, IEnumerable<string> allowedExtraFiles = null)
        {
            var problems = new ConcurrentBag<string>();
            var actual = GetGameFiles(gameRoot).ToDictionary(x => x.relativePath, x => x.fullPath, StringComparer.OrdinalIgnoreCase);

            // Manifests generated before a path was excluded still list it
            expected = expected.Where(x => !IsExcluded(x.Filename)).ToList();

            int done = 0;
            Parallel.ForEach(expected, new ParallelOptions { MaxDegreeOfParallelism = 4 }, entry =>
            {
                setStatus?.Invoke($"Verifying installation ({Interlocked.Increment(ref done)}/{expected.Count})");
                if (!actual.TryGetValue(entry.Filename, out var fullPath))
                {
                    if (MilanLocalizationFiles.IsOptionalLocalization(entry.Filename))
                    {
                        M3Log.Information($@"Installation verification: {entry.Filename} is not installed (localization not present in the source game)");
                    }
                    else
                    {
                        problems.Add($"Missing file: {entry.Filename}");
                    }
                    return;
                }

                var size = new FileInfo(fullPath).Length;
                if (size != entry.Size)
                {
                    problems.Add($"Size mismatch: {entry.Filename} is {size} bytes, expected {entry.Size}");
                    return;
                }

                var hash = MUtilities.CalculateHash(fullPath, @"sha256");
                if (!string.Equals(hash, entry.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Hash mismatch: {entry.Filename} is {hash}, expected {entry.Hash}");
                }
            });

            var expectedNames = expected.Select(x => x.Filename).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowedExtraNames = allowedExtraFiles?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            foreach (var relativePath in actual.Keys)
            {
                if (expectedNames.Contains(relativePath))
                {
                    continue;
                }

                if (allowedExtraNames.Contains(relativePath))
                {
                    M3Log.Information($@"Installation verification: {relativePath} is not in the expected state but is allowed");
                }
                else
                {
                    problems.Add($"Unexpected file: {relativePath}");
                }
            }

            return problems.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<(string relativePath, string fullPath)> GetGameFiles(string gameRoot)
        {
            var root = Path.GetFullPath(gameRoot);
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(x => (relativePath: Path.GetRelativePath(root, x).Replace(Path.DirectorySeparatorChar, '\\'), fullPath: x))
                .Where(x => !IsExcluded(x.relativePath))
                .ToList();
        }
    }
}
