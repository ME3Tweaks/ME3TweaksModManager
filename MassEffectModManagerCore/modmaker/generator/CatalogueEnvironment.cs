using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// Fingerprint of the install a catalogue's data layer was generated from.
    ///
    /// The catalogue is generated on the user's machine, not downloaded, so it is only valid for the
    /// install it was built against. This records enough to notice when that stops being true, and
    /// the user is offered a manual rebuild.
    ///
    /// Regeneration is deliberately not automatic. What counts as a change that matters is not
    /// settled - a DLC mounting at a different priority certainly does, a texture pack certainly does
    /// not - and rebuilding redefines "default" for every value in every open project.
    ///
    /// Schema: modmaker/schema/definitions.schema.json, `environment`.
    /// </summary>
    public class CatalogueEnvironment
    {
        [JsonProperty(@"gamePath")]
        public string GamePath { get; set; }

        [JsonProperty(@"gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty(@"generatedUtc")]
        public DateTime GeneratedUtc { get; set; }

        /// <summary>
        /// A generator change invalidates the cache as surely as a game change does.
        /// </summary>
        [JsonProperty(@"generatorVersion")]
        public string GeneratorVersion { get; set; }

        /// <summary>
        /// Version of the shipped text-and-curation layer this data layer was joined against.
        /// </summary>
        [JsonProperty(@"textLayerVersion")]
        public string TextLayerVersion { get; set; }

        /// <summary>
        /// Installed DLC in mount order, as scanned. ModMaker's own output folder is never present -
        /// see <see cref="ModMakerCatalogue.IsCompilerOutput"/>.
        /// </summary>
        [JsonProperty(@"dlc")]
        public List<DlcEntry> Dlc { get; set; } = new();

        /// <summary>
        /// Identity of the scanned inputs, so a changed install is detectable without rescanning it.
        /// </summary>
        [JsonProperty(@"files")]
        public List<FileEntry> Files { get; set; } = new();

        /// <summary>
        /// Anchors the generator looked for and did not find. An empty wavelist editor is otherwise
        /// indistinguishable from a game that has no factions, so this has to surface as a named
        /// warning rather than as absent content.
        /// </summary>
        [JsonProperty(@"anchorsMissing")]
        public List<string> AnchorsMissing { get; set; } = new();

        public class DlcEntry
        {
            [JsonProperty(@"folder")] public string Folder { get; set; }
            [JsonProperty(@"mountPriority")] public int MountPriority { get; set; }
            [JsonProperty(@"version")] public string Version { get; set; }
        }

        public class FileEntry
        {
            [JsonProperty(@"path")] public string Path { get; set; }
            [JsonProperty(@"size")] public long Size { get; set; }
            [JsonProperty(@"modifiedUtc")] public DateTime ModifiedUtc { get; set; }
            [JsonProperty(@"hash")] public string Hash { get; set; }
        }

        /// <summary>
        /// Builds a fingerprint for the given target.
        ///
        /// Only the coalesced files are given per-file identity, not all ~800 packages. Package
        /// content changing without its DLC folder or the coalesced changing is possible but is not a
        /// case worth an eight-hundred-file stat on every startup; the DLC set and mount order carry
        /// almost all of the signal, and the rebuild is manual anyway. If that assumption turns out to
        /// be wrong, this is the one place to widen.
        /// </summary>
        public static CatalogueEnvironment Build(GameTarget target, string generatorVersion, string textLayerVersion = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var env = new CatalogueEnvironment
            {
                GamePath = target.TargetPath,
                GameVersion = target.ExecutableHash,
                GeneratedUtc = DateTime.UtcNow,
                GeneratorVersion = generatorVersion,
                TextLayerVersion = textLayerVersion,
            };

            var dlcRoot = M3Directories.GetDLCPath(target);
            foreach (var folder in ModMakerCatalogue.ScannableDLCFoldersInMountOrder(target))
            {
                var dir = Path.Combine(dlcRoot, folder);
                env.Dlc.Add(new DlcEntry
                {
                    Folder = folder,
                    MountPriority = MELoadedDLC.GetMountPriority(dir, target.Game),
                });
                AddFileIfPresent(env, Path.Combine(dir, target.Game.CookedDirName(), $@"Default_{folder}.bin"));
            }

            AddFileIfPresent(env, M3Directories.GetCoalescedPath(target));
            return env;
        }

        private static void AddFileIfPresent(CatalogueEnvironment env, string path)
        {
            if (path == null) return;
            var fi = new FileInfo(path);
            if (!fi.Exists) return;
            env.Files.Add(new FileEntry
            {
                Path = path,
                Size = fi.Length,
                ModifiedUtc = fi.LastWriteTimeUtc,
            });
        }

        /// <summary>
        /// Whether a cached catalogue built with <paramref name="cached"/> still describes the install
        /// described by <paramref name="current"/>. Returns the human-readable reasons it does not, so
        /// the user is told what changed rather than just that something did.
        /// </summary>
        public static List<string> DescribeDifferences(CatalogueEnvironment cached, CatalogueEnvironment current)
        {
            var reasons = new List<string>();
            if (cached == null || current == null)
            {
                reasons.Add(@"No cached catalogue for this target.");
                return reasons;
            }

            if (!string.Equals(cached.GamePath, current.GamePath, StringComparison.OrdinalIgnoreCase))
                reasons.Add($@"Game path changed: {cached.GamePath} -> {current.GamePath}");

            if (cached.GameVersion != current.GameVersion)
                reasons.Add(@"The game executable changed.");

            if (cached.GeneratorVersion != current.GeneratorVersion)
                reasons.Add($@"Catalogue generator updated ({cached.GeneratorVersion} -> {current.GeneratorVersion}).");

            if (cached.TextLayerVersion != current.TextLayerVersion)
                reasons.Add($@"ModMaker's catalogue text updated ({cached.TextLayerVersion} -> {current.TextLayerVersion}).");

            var was = cached.Dlc.ToDictionary(x => x.Folder, x => x.MountPriority, StringComparer.OrdinalIgnoreCase);
            var now = current.Dlc.ToDictionary(x => x.Folder, x => x.MountPriority, StringComparer.OrdinalIgnoreCase);

            foreach (var added in now.Keys.Where(k => !was.ContainsKey(k)))
                reasons.Add($@"DLC installed: {added}");
            foreach (var removed in was.Keys.Where(k => !now.ContainsKey(k)))
                reasons.Add($@"DLC removed: {removed}");
            foreach (var kv in now.Where(kv => was.TryGetValue(kv.Key, out var p) && p != kv.Value))
                reasons.Add($@"DLC mount priority changed: {kv.Key} ({was[kv.Key]} -> {kv.Value})");

            var wasFiles = cached.Files.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var f in current.Files)
            {
                if (!wasFiles.TryGetValue(f.Path, out var old))
                {
                    reasons.Add($@"New config file: {Path.GetFileName(f.Path)}");
                    continue;
                }
                if (old.Size != f.Size || old.ModifiedUtc != f.ModifiedUtc)
                    reasons.Add($@"Config file changed: {Path.GetFileName(f.Path)}");
            }

            return reasons;
        }
    }
}
