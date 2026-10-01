using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// A restorable snapshot of a game folder, taken before the slash generator transforms the game. It gives the generator
    /// the original files to diff against, and lets the game be put back exactly as it was so the generated manifest can be
    /// applied to it the same way it will be on users' machines.
    /// <para>
    /// Files that may be rewritten in place (packages) are copied. Everything else - notably the multi-GB TFCs, which the
    /// TFC compactor only ever reads, creates, or deletes - is hard linked, which is instant and takes no space, falling
    /// back to a copy if linking isn't possible.
    /// </para>
    /// </summary>
    [Localizable(false)]
    internal class MilanSlashSnapshot
    {
        private readonly string gameRoot;
        private readonly string snapshotRoot;
        private readonly Func<string, bool> excludeFromGame;

        /// <summary>
        /// Every file in the game when the snapshot was taken, keyed by game-relative path (Windows separators).
        /// </summary>
        public Dictionary<string, (long Size, DateTime LastWriteTimeUtc)> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        private MilanSlashSnapshot(string gameRoot, string snapshotRoot, Func<string, bool> excludeFromGame)
        {
            this.gameRoot = Path.GetFullPath(gameRoot);
            this.snapshotRoot = Path.GetFullPath(snapshotRoot);
            this.excludeFromGame = excludeFromGame;
        }

        /// <summary>
        /// Snapshots every file under <paramref name="gameRoot"/> into <paramref name="snapshotRoot"/>, which must be on
        /// the same volume for hard links to work.
        /// </summary>
        /// <param name="copyFile">Files (by game-relative path) that must be copied rather than linked, because they may be modified in place.</param>
        /// <param name="excludeFromGame">Game-relative paths that are not part of the game (such as the snapshot and work folders themselves).</param>
        public static MilanSlashSnapshot Create(string gameRoot, string snapshotRoot, Func<string, bool> copyFile, Func<string, bool> excludeFromGame, Action<string> setStatus = null)
        {
            var snapshot = new MilanSlashSnapshot(gameRoot, snapshotRoot, excludeFromGame);
            if (Directory.Exists(snapshot.snapshotRoot))
                Directory.Delete(snapshot.snapshotRoot, true);

            int linkFailures = 0;
            var files = snapshot.EnumerateGameFiles();
            for (int i = 0; i < files.Count; i++)
            {
                var relativePath = files[i];
                setStatus?.Invoke($"Snapshotting game ({i + 1}/{files.Count})");
                var gamePath = snapshot.GetGamePath(relativePath);
                var snapshotPath = snapshot.GetSnapshotPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));

                var info = new FileInfo(gamePath);
                snapshot.Files[relativePath] = (info.Length, info.LastWriteTimeUtc);

                if (copyFile(relativePath))
                {
                    File.Copy(gamePath, snapshotPath);
                }
                else if (!TryCreateHardLink(snapshotPath, gamePath))
                {
                    linkFailures++;
                    File.Copy(gamePath, snapshotPath);
                }
            }

            if (linkFailures > 0)
                M3Log.Warning($"Slash snapshot: {linkFailures} file(s) could not be hard linked and were copied instead. This is slow and uses a lot of disk space; the game folder should be on an NTFS volume.");
            M3Log.Information($"Slash snapshot: {snapshot.Files.Count} files");
            return snapshot;
        }

        /// <summary>
        /// Game-relative paths (Windows separators) of every file currently in the game.
        /// </summary>
        public List<string> EnumerateGameFiles()
        {
            return Directory.EnumerateFiles(gameRoot, "*", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(gameRoot, x).Replace(Path.DirectorySeparatorChar, '\\'))
                .Where(x => !excludeFromGame(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string GetGamePath(string relativePath) => Path.Combine(gameRoot, relativePath.Replace('\\', Path.DirectorySeparatorChar));

        /// <summary>
        /// Where the snapshotted (original) copy of a game file is.
        /// </summary>
        public string GetSnapshotPath(string relativePath) => Path.Combine(snapshotRoot, relativePath.Replace('\\', Path.DirectorySeparatorChar));

        /// <summary>
        /// If a file is still the same as when the snapshot was taken. Size and modification time are compared rather than
        /// content, as TFCs are many GB and are never modified in place (they are only created or deleted).
        /// </summary>
        public bool IsUnchanged(string relativePath)
        {
            if (!Files.TryGetValue(relativePath, out var original))
                return false;
            var info = new FileInfo(GetGamePath(relativePath));
            return info.Exists && info.Length == original.Size && info.LastWriteTimeUtc == original.LastWriteTimeUtc;
        }

        /// <summary>
        /// Puts the game back exactly as it was when the snapshot was taken: files that didn't exist are deleted and every
        /// snapshotted file is moved back into place. The snapshot is consumed.
        /// </summary>
        public void Restore(Action<string> setStatus = null)
        {
            setStatus?.Invoke("Restoring game from snapshot");
            foreach (var relativePath in EnumerateGameFiles())
            {
                if (!Files.ContainsKey(relativePath))
                    File.Delete(GetGamePath(relativePath));
            }

            foreach (var relativePath in Files.Keys)
            {
                if (IsUnchanged(relativePath))
                    continue; // Never touched (a hard link to it is all the snapshot holds)

                var gamePath = GetGamePath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(gamePath));
                File.Move(GetSnapshotPath(relativePath), gamePath, overwrite: true);
            }

            Delete();
            M3Log.Information("Slash snapshot restored");
        }

        /// <summary>
        /// Deletes the snapshot without restoring it.
        /// </summary>
        public void Delete()
        {
            if (Directory.Exists(snapshotRoot))
                Directory.Delete(snapshotRoot, true);
        }

        #region Hard links

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateHardLinkW")]
        private static extern bool CreateHardLinkWindows(string newFileName, string existingFileName, IntPtr securityAttributes);

        [DllImport("libc", SetLastError = true, EntryPoint = "link")]
        private static extern int CreateHardLinkUnix(string oldPath, string newPath);

        private static bool TryCreateHardLink(string newPath, string existingPath)
        {
            try
            {
                return OperatingSystem.IsWindows()
                    ? CreateHardLinkWindows(newPath, existingPath, IntPtr.Zero)
                    : CreateHardLinkUnix(existingPath, newPath) == 0;
            }
            catch (Exception e)
            {
                M3Log.Warning($"Could not hard link {existingPath}: {e.Message}");
                return false;
            }
        }

        #endregion
    }
}
