using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Records and verifies the size and hash of every file in a finished Project Milan installation, so an end user's
    /// installation can be checked against the one the slash manifest was generated from, file for file.
    /// </summary>
    internal static class MilanFinalState
    {
        /// <summary>
        /// Files that are expected to differ between otherwise identical installations. These are neither recorded nor
        /// verified. Paths are relative to the game root. An entry starting with a separator matches that file name in any
        /// folder; an entry ending with a separator matches everything in that folder.
        /// </summary>
        private static readonly string[] ExcludedPaths =
        [
            // Records the install time, the Mod Manager build, and the mod's library path
            @"\_metacmm.txt",
            // Bink bypass - supplied by Mod Manager, not the game or the mod; changes with Mod Manager versions
            @"Binaries\Win64\bink2w64.dll",
            @"Binaries\Win64\bink2w64_original.dll",
            // ASIs are managed separately from the game content
            @"Binaries\Win64\ASI\",
        ];

        /// <summary>
        /// If the given game-relative path (Windows separators) is excluded from final state checks.
        /// </summary>
        public static bool IsExcluded(string relativePath)
        {
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
                    Hash = MilanSlashHashing.HashFile(file.fullPath)
                });
                setStatus?.Invoke($"Recording final file hashes ({Interlocked.Increment(ref done)}/{files.Count})");
            });

            return entries.OrderBy(x => x.Filename, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Compares the game against the expected final state. Returns a list of differences; empty means the installation
        /// matches exactly (excluding <see cref="ExcludedPaths"/>).
        /// </summary>
        public static List<string> Verify(IReadOnlyList<MilanSlashFinalFileEntry> expected, string gameRoot, Action<string> setStatus = null)
        {
            var problems = new ConcurrentBag<string>();
            var actual = GetGameFiles(gameRoot).ToDictionary(x => x.relativePath, x => x.fullPath, StringComparer.OrdinalIgnoreCase);
            var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int done = 0;
            Parallel.ForEach(expected, new ParallelOptions { MaxDegreeOfParallelism = 4 }, entry =>
            {
                setStatus?.Invoke($"Verifying installation ({Interlocked.Increment(ref done)}/{expected.Count})");
                if (!actual.TryGetValue(entry.Filename, out var fullPath))
                {
                    problems.Add($"Missing file: {entry.Filename}");
                    return;
                }

                var size = new FileInfo(fullPath).Length;
                if (size != entry.Size)
                {
                    problems.Add($"Size mismatch: {entry.Filename} is {size} bytes, expected {entry.Size}");
                    return;
                }

                var hash = MilanSlashHashing.HashFile(fullPath);
                if (!string.Equals(hash, entry.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Hash mismatch: {entry.Filename} is {hash}, expected {entry.Hash}");
                }
            });

            foreach (var entry in expected)
            {
                expectedNames.Add(entry.Filename);
            }

            foreach (var relativePath in actual.Keys)
            {
                if (!expectedNames.Contains(relativePath))
                    problems.Add($"Unexpected file: {relativePath}");
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
