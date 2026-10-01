using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// SHA-256 helpers shared by the slash generator and applier. Hashes are lowercase hex, matching MilanDelta.
    /// </summary>
    internal static class MilanSlashHashing
    {
        public static string HashBytes(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));

        public static string HashStream(Stream stream)
        {
            stream.Position = 0;
            var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
            stream.Position = 0;
            return hash;
        }

        public static string HashFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
            return Convert.ToHexStringLower(SHA256.HashData(fs));
        }
    }

    /// <summary>
    /// Applies a <see cref="MilanSlashManifest"/> to an installed Project Milan game, shrinking it down to the data that is
    /// actually used.
    /// <para>
    /// The process is staged so that a failure never leaves a half-slashed game: everything is validated before anything is
    /// written, every output is built next to its final location as a temporary file and verified against the manifest, and
    /// only once everything has been built are the temporary files moved into place and the deleted files removed.
    /// </para>
    /// </summary>
    internal static class MilanSlashApplier
    {
        public const string SlashManifestFileName = @"slash.json";
        public const string SlashFolderName = @"Slash";
        public const double SupportedSlashVersion = 0.3;

        private const string StagingExtension = @".slashtmp";


        /// <summary>
        /// Loads and verifies the slash manifest in the given MilanDelta folder.
        /// </summary>
        /// <returns>The manifest, or null if the folder does not contain one.</returns>
        /// <exception cref="Exception">The manifest is present but invalid, of an unsupported version, or fails its signature check.</exception>
        public static MilanSlashManifest LoadManifest(string milanDeltaFolder)
        {
            var manifestPath = Path.Combine(milanDeltaFolder, SlashManifestFileName);
            if (!File.Exists(manifestPath))
                return null;

            MilanSlashManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<MilanSlashManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to parse {SlashManifestFileName}: {e.Message}");
            }

            if (manifest == null || !manifest.MilanSlash)
                throw new Exception($"{SlashManifestFileName} is not a valid Milan slash manifest.");

            if (manifest.Version != SupportedSlashVersion)
                throw new Exception($"{SlashManifestFileName} has unsupported version {manifest.Version} (expected {SupportedSlashVersion}).");

            if (!string.Equals(manifest.ComputeSignature(), manifest.Signature, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"{SlashManifestFileName} signature does not match its contents. This build may be corrupted or tampered with.");

            return manifest;
        }

        /// <summary>
        /// Checks that the game is in the exact state the manifest was generated against, without modifying anything.
        /// Returns a list of problems; an empty list means the manifest can be applied.
        /// </summary>
        public static List<string> Validate(MilanSlashManifest manifest, string milanDeltaFolder, string gameRoot, Action<string> setStatus = null)
        {
            var problems = new List<string>();
            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void checkOutput(string relPath)
            {
                if (!outputs.Add(relPath))
                    problems.Add($"{relPath} is written by more than one slash operation");
            }

            foreach (var build in manifest.Builds)
            {
                if (!TryResolve(gameRoot, build.Filename, problems, out _))
                    continue;
                checkOutput(build.Filename);

                if (build.Sources.Count == 0)
                    problems.Add($"{build.Filename} has no sources");

                foreach (var source in build.Sources)
                {
                    if (!TryResolve(source.Payload ? milanDeltaFolder : gameRoot, source.Filename, problems, out var sourcePath))
                        continue;

                    if (!File.Exists(sourcePath))
                    {
                        problems.Add(source.Payload ? $"Slash payload file is missing: {source.Filename}" : $"{source.Filename} is missing");
                        continue;
                    }

                    var size = new FileInfo(sourcePath).Length;
                    if (size != source.Size)
                        problems.Add($"{source.Filename} is {size} bytes, expected {source.Size}");
                }

                long totalSize = 0;
                foreach (var segment in build.Segments)
                {
                    if (segment.SourceIndex < 0 || segment.SourceIndex >= build.Sources.Count || segment.Offset < 0 || segment.Length <= 0
                        || segment.Offset + segment.Length > build.Sources[segment.SourceIndex].Size)
                    {
                        problems.Add($"{build.Filename} has an invalid segment {segment}");
                        break;
                    }
                    totalSize += segment.Length;
                }

                if (totalSize != build.DestSize)
                    problems.Add($"{build.Filename} segments total {totalSize} bytes, but the manifest expects {build.DestSize}");
            }

            foreach (var patch in manifest.Patches)
            {
                if (patch.Mode != MilanSlashPatchModes.Package && patch.Mode != MilanSlashPatchModes.Raw)
                    problems.Add($"{patch.Filename} has unsupported patch mode '{patch.Mode}'");

                if (!TryResolve(milanDeltaFolder, patch.Delta, problems, out var deltaPath))
                    continue;
                if (!File.Exists(deltaPath))
                    problems.Add($"Slash delta file is missing: {patch.Delta}");

                if (!TryResolve(gameRoot, patch.Filename, problems, out var sourcePath))
                    continue;
                checkOutput(patch.Filename);

                if (!File.Exists(sourcePath))
                {
                    // Users' LE3 installations may not have every localization; those files are simply skipped
                    if (!MilanLocalizationFiles.IsOptionalLocalization(patch.Filename))
                        problems.Add($"{patch.Filename} is missing");
                    continue;
                }

                var size = new FileInfo(sourcePath).Length;
                if (size != patch.SourceFileSize)
                {
                    problems.Add($"{patch.Filename} is {size} bytes, expected {patch.SourceFileSize}");
                    continue;
                }

                setStatus?.Invoke($"Verifying {Path.GetFileName(patch.Filename)}");
                var hash = MilanSlashHashing.HashFile(sourcePath);
                if (!string.Equals(hash, patch.SourceFileHash, StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{patch.Filename} does not match the file the slash manifest was generated against (hash {hash}, expected {patch.SourceFileHash})");
            }

            foreach (var deletion in manifest.Deletions)
            {
                if (!TryResolve(gameRoot, deletion.Filename, problems, out var path))
                    continue;
                if (outputs.Contains(deletion.Filename))
                    problems.Add($"{deletion.Filename} is both written and deleted by the slash manifest");

                // A file that is already gone is fine; one that is a different size means the game isn't what we expect
                if (File.Exists(path) && new FileInfo(path).Length != deletion.Size)
                    problems.Add($"{deletion.Filename} is {new FileInfo(path).Length} bytes, expected {deletion.Size}");
            }

            return problems;
        }

        /// <summary>
        /// Validates, builds, verifies, and commits a slash manifest against the game at <paramref name="gameRoot"/>.
        /// </summary>
        /// <exception cref="Exception">Validation or building failed. The game is left unmodified in this case.</exception>
        public static void Apply(MilanSlashManifest manifest, string milanDeltaFolder, string gameRoot, Action<string> setStatus = null)
        {
            setStatus?.Invoke(@"Validating slash manifest...");
            var problems = Validate(manifest, milanDeltaFolder, gameRoot, setStatus);
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    M3Log.Error($@"Slash validation: {problem}");
                }
                throw new Exception($"The installed game does not match the slash manifest ({problems.Count} problem(s)). First problem: {problems[0]}");
            }

            // Localizations the user doesn't have are skipped (validation has already allowed this)
            var patches = new List<MilanSlashPatchEntry>();
            foreach (var patch in manifest.Patches)
            {
                if (!File.Exists(Resolve(gameRoot, patch.Filename)) && MilanLocalizationFiles.IsOptionalLocalization(patch.Filename))
                {
                    M3Log.Information($@"Slash: skipping {patch.Filename}, a localization this installation does not have");
                    continue;
                }
                patches.Add(patch);
            }

            // Build everything into staging files next to their final locations.
            var staged = new List<(string stagingPath, string finalPath)>();
            try
            {
                foreach (var build in manifest.Builds)
                {
                    setStatus?.Invoke($"Building {Path.GetFileName(build.Filename)}");
                    var finalPath = Resolve(gameRoot, build.Filename);
                    var stagingPath = finalPath + StagingExtension;
                    lock (staged) staged.Add((stagingPath, finalPath));
                    using var output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
                    var (size, hash) = AssembleBuild(build, source => Resolve(source.Payload ? milanDeltaFolder : gameRoot, source.Filename), output);
                    VerifySize(size, build.DestSize, build.Filename, @"output");
                    VerifyHash(hash, build.DestHash, build.Filename, @"output");
                }

                int patchesDone = 0;
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4) };
                Parallel.ForEach(patches, parallelOptions, patch =>
                {
                    var finalPath = Resolve(gameRoot, patch.Filename);
                    var stagingPath = finalPath + StagingExtension;
                    lock (staged) staged.Add((stagingPath, finalPath));
                    ApplyPatch(patch, Resolve(milanDeltaFolder, patch.Delta), finalPath, stagingPath);
                    var done = Interlocked.Increment(ref patchesDone);
                    setStatus?.Invoke($"Patching files ({done}/{patches.Count})");
                });
            }
            catch (Exception e)
            {
                foreach (var (stagingPath, _) in staged)
                {
                    TryDelete(stagingPath);
                }

                // Surface the actual failure rather than Parallel's "One or more errors occurred"
                if (e is AggregateException { InnerExceptions.Count: > 0 } ae)
                {
                    ExceptionDispatchInfo.Capture(ae.InnerExceptions[0]).Throw();
                }
                throw;
            }

            // Everything built and verified; commit.
            setStatus?.Invoke(@"Finalizing slashed files...");
            foreach (var (stagingPath, finalPath) in staged)
            {
                File.Move(stagingPath, finalPath, overwrite: true);
            }

            foreach (var deletion in manifest.Deletions)
            {
                var path = Resolve(gameRoot, deletion.Filename);
                if (File.Exists(path))
                {
                    M3Log.Information($@"Slash: deleting {deletion.Filename} ({deletion.Comment})");
                    File.Delete(path);
                }
            }

            M3Log.Information($@"Slash applied: {manifest.Builds.Count} file(s) built, {patches.Count} file(s) patched, {manifest.Deletions.Count} file(s) deleted");
        }

        /// <summary>
        /// Assembles a build from ranges of its sources, writing it to <paramref name="output"/> (if not null) and returning
        /// the size and SHA-256 of the result.
        /// </summary>
        /// <param name="resolveSource">Returns the path to read a source from.</param>
        internal static (long size, string hash) AssembleBuild(MilanSlashBuildEntry build, Func<MilanSlashSourceFile, string> resolveSource, Stream output)
        {
            var sources = new FileStream[build.Sources.Count];
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    sources[i] = new FileStream(resolveSource(build.Sources[i]), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                }

                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1024 * 1024];
                long size = 0;
                foreach (var segment in build.Segments)
                {
                    var source = sources[segment.SourceIndex];
                    source.Position = segment.Offset;
                    long remaining = segment.Length;
                    while (remaining > 0)
                    {
                        int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read <= 0)
                            throw new Exception($"Unexpected end of {build.Sources[segment.SourceIndex].Filename} while building {build.Filename}");
                        output?.Write(buffer, 0, read);
                        hasher.AppendData(buffer, 0, read);
                        remaining -= read;
                        size += read;
                    }
                }

                return (size, Convert.ToHexStringLower(hasher.GetHashAndReset()));
            }
            finally
            {
                foreach (var source in sources)
                {
                    source?.Dispose();
                }
            }
        }

        /// <summary>
        /// Applies a JPatch delta to a file, writing the verified result to <paramref name="stagingPath"/>.
        /// </summary>
        private static void ApplyPatch(MilanSlashPatchEntry patch, string deltaPath, string sourcePath, string stagingPath)
        {
            M3Log.Information($@"Slash: patching {patch.Filename} ({patch.Mode})");
            using var deltaStream = new MemoryStream(File.ReadAllBytes(deltaPath));

            if (patch.Mode == MilanSlashPatchModes.Raw)
            {
                // File to file: the RefShaderCache is far too large to want in memory twice. The source file hash was
                // already verified during validation.
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
                using (var outStream = new FileStream(stagingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024))
                {
                    VerifySize(sourceStream.Length, patch.SourceSize, patch.Filename, @"source");
                    if (!JPatch.ApplyJPatch(sourceStream, deltaStream, outStream))
                        throw new Exception($"Slash delta for {patch.Filename} could not be applied");

                    VerifySize(outStream.Length, patch.DestSize, patch.Filename, @"output");
                    VerifyHash(MilanSlashHashing.HashStream(outStream), patch.DestHash, patch.Filename, @"output");
                }
                return;
            }

            // Package mode - same convention as MilanDelta: patch the LEC-decompressed package, then re-save it.
            using var fileStream = new MemoryStream(File.ReadAllBytes(sourcePath));
            using var decompressed = MEPackage.GetDecompressedPackageStream(fileStream);
            VerifySize(decompressed.Length, patch.SourceSize, patch.Filename, @"source");
            VerifyHash(MilanSlashHashing.HashStream(decompressed), patch.SourceHash, patch.Filename, @"source");

            using var patched = new MemoryStream();
            if (!JPatch.ApplyJPatch(decompressed, deltaStream, patched))
                throw new Exception($"Slash delta for {patch.Filename} could not be applied");

            VerifySize(patched.Length, patch.DestSize, patch.Filename, @"output");
            VerifyHash(MilanSlashHashing.HashStream(patched), patch.DestHash, patch.Filename, @"output");

            // Save via stream so the package keeps its real file path; some serializers depend on the extension.
            patched.Position = 0;
            using var package = MEPackageHandler.OpenMEPackageFromStream(patched, sourcePath);
            using var saved = package.SaveToStream(patch.Compressed);
            File.WriteAllBytes(stagingPath, saved.ToArray());
        }

        private static void VerifySize(long actual, long expected, string fileName, string stage)
        {
            if (actual != expected)
                throw new Exception($"Slash {stage} size mismatch for {fileName}: expected {expected}, got {actual}.");
        }

        private static void VerifyHash(string actual, string expected, string fileName, string stage)
        {
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Slash {stage} hash mismatch for {fileName}: expected {expected}, got {actual}.");
        }

        /// <summary>
        /// Resolves a manifest-relative path, refusing anything that would escape the root folder.
        /// </summary>
        internal static string Resolve(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new Exception($"Invalid slash manifest path: '{relativePath}'");

            var fullRoot = Path.GetFullPath(root);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            var rootWithSeparator = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Slash manifest path escapes its root folder: '{relativePath}'");

            return fullPath;
        }

        private static bool TryResolve(string root, string relativePath, List<string> problems, out string fullPath)
        {
            try
            {
                fullPath = Resolve(root, relativePath);
                return true;
            }
            catch (Exception e)
            {
                problems.Add(e.Message);
                fullPath = null;
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                M3Log.Warning($@"Could not delete slash staging file {path}: {e.Message}");
            }
        }
    }
}
