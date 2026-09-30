using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// Sections of the merged config resolved by key, plus the keys that appeared more than once.
    /// </summary>
    public class SectionIndex
    {
        public IReadOnlyDictionary<string, CoalesceSection> ByKey { get; }

        /// <summary>Keys that appeared in more than one coalesced sub-file. Diagnostic.</summary>
        public IReadOnlyList<string> DuplicateKeys { get; }

        /// <summary>
        /// The coalesced sub-file each section was found in - BioGame, BioWeapon, BioDifficulty, and
        /// so on, without extension.
        ///
        /// Needed to write anything back. A mod's own coalesced merges over the game's file by file,
        /// so a section written into the wrong sub-file merges into a file the game does not read for
        /// it, and the mod builds, installs and does nothing. Which file a section lives in is data,
        /// not a naming convention, so it is recorded when the index is built rather than guessed at
        /// compile time.
        /// </summary>
        private readonly IReadOnlyDictionary<string, string> assetByKey;

        /// <summary>
        /// Built by <see cref="MergedGameConfig.BuildSectionIndex"/>. Public so that code which only
        /// needs the section-to-sub-file mapping - the compiler, and its tests - can be exercised
        /// without loading a game.
        /// </summary>
        public SectionIndex(Dictionary<string, CoalesceSection> byKey, List<string> duplicateKeys,
                            Dictionary<string, string> assetByKey)
        {
            ByKey = byKey;
            DuplicateKeys = duplicateKeys;
            this.assetByKey = assetByKey;
        }

        /// <summary>
        /// The coalesced sub-file holding a section, or null if the section is not in the index.
        /// </summary>
        public string AssetOf(string sectionKey) =>
            sectionKey == null ? null : assetByKey.GetValueOrDefault(sectionKey);

        /// <summary>The section, or null. Callers must treat null as "absent", never as "empty".</summary>
        public CoalesceSection Find(string key) => ByKey.GetValueOrDefault(key);

        /// <summary>
        /// Live values of one property of one section; empty when either is absent.
        ///
        /// A CoalesceSection is normally backed by a case-insensitive dictionary, but that depends on
        /// how it was constructed, and config property names are written in whatever case BioWare
        /// felt like. A case-sensitive miss here would return "no values" rather than fail, so the
        /// lookup falls back to an explicit case-insensitive scan.
        /// </summary>
        public IEnumerable<CoalesceValue> Values(string sectionKey, string property)
        {
            if (Find(sectionKey) is not { } section) return [];
            if (section.TryGetValue(property, out var prop)) return MergedGameConfig.Live(prop);

            foreach (var candidate in section)
            {
                if (candidate.Key.Equals(property, StringComparison.OrdinalIgnoreCase))
                    return MergedGameConfig.Live(candidate.Value);
            }
            return [];
        }
    }

    /// <summary>
    /// The effective, game-visible view of a merged config bundle, produced by
    /// <see cref="MergedGameConfig.Collapse"/>. Holds only surviving values - no <c>Remove</c> or
    /// <c>RemoveProperty</c> markers, no unsuppressed AddUnique duplicates, no empty properties or
    /// sections. This is a read-only snapshot: merging a further delta into it is meaningless, since
    /// the parse-action instructions that would drive that merge have already been discarded. See
    /// docs/15-config-collapse.md §2.
    /// </summary>
    public class CollapsedConfig
    {
        private readonly IReadOnlyDictionary<string, CoalesceAsset> assetsByName;

        internal CollapsedConfig(IReadOnlyDictionary<string, CoalesceAsset> assetsByName)
        {
            this.assetsByName = assetsByName;
        }

        /// <summary>Names of every asset (coalesced sub-file) that survived collapsing with at least one section.</summary>
        public IEnumerable<string> AssetNames => assetsByName.Keys;

        /// <summary>The collapsed asset, or null if it collapsed away to nothing.</summary>
        public CoalesceAsset GetAsset(string assetName) => assetsByName.GetValueOrDefault(assetName);

        /// <summary>Every collapsed section, across every asset, with the sub-file it came from.</summary>
        public IEnumerable<(string Asset, string Key, CoalesceSection Section)> SectionsByAsset()
        {
            foreach (var (assetName, asset) in assetsByName)
                foreach (var section in asset.Sections)
                    yield return (assetName, section.Key, section.Value);
        }

        /// <summary>
        /// The collapsed, effective values of one property, or empty if the section/property is not
        /// present in the collapsed view (which, for a property, is indistinguishable from "removed" -
        /// an FConfigSection multimap simply has no entry for it).
        /// </summary>
        public IEnumerable<CoalesceValue> Values(string assetName, string sectionKey, string property)
        {
            if (GetAsset(assetName) is not { } asset) return [];
            if (!asset.Sections.TryGetValue(sectionKey, out var section)) return [];
            return section.TryGetValue(property, out var prop) ? prop : [];
        }

        /// <summary>
        /// Section / property / value counts across the whole collapsed bundle - the whole-bundle
        /// sanity check in docs/15-config-collapse.md §5.3, and the shape to diff against the ASI
        /// dump's totals in docs/14-runtime-config-asi.md.
        /// </summary>
        public (int Sections, int Properties, int Values) Counts()
        {
            int sections = 0, properties = 0, values = 0;
            foreach (var asset in assetsByName.Values)
            foreach (var section in asset.Sections.Values)
            {
                sections++;
                properties += section.Count;
                foreach (var prop in section.Values)
                    values += prop.Count;
            }
            return (sections, properties, values);
        }
    }

    /// <summary>
    /// The game's config as the game itself sees it: the basegame coalesced with every installed
    /// DLC's merged in, in mount order, with parse actions applied - and with ModMaker's own output
    /// folder excluded.
    ///
    /// Reading the .bin files individually and concatenating their entries is wrong in three ways
    /// that all produce plausible output:
    ///
    ///  - A Remove entry cancels an earlier Add. Unmerged, both survive, and the catalogue reports
    ///    content the game does not have. This inflated three separate spike results before it was
    ///    caught (see docs/08-spike-results.md).
    ///  - A Remove matches its target by EXACT value text, tabs and inconsistent spacing included, so
    ///    the merge cannot be approximated by comparing keys.
    ///  - Config REPLACES the compiled class defaults; it does not layer on them. If config defines a
    ///    property at all, the config value is what the game uses and the default is discarded
    ///    entirely - an array of 100 defaults loses to an array of 1 in config. So the effective value
    ///    of a config property is "the merged config's value if it has one, otherwise the class
    ///    default", never a combination.
    /// </summary>
    public class MergedGameConfig
    {
        /// <summary>The merged bundle. Retains Remove entries so it can be re-serialised as a delta.</summary>
        public ConfigAssetBundle Bundle { get; }

        /// <summary>DLC folders merged in, in the order they were merged (lowest mount priority first).</summary>
        public IReadOnlyList<string> MergedDLC { get; }

        private MergedGameConfig(ConfigAssetBundle bundle, IReadOnlyList<string> mergedDlc)
        {
            Bundle = bundle;
            MergedDLC = mergedDlc;
        }

        /// <summary>
        /// Loads and merges the config for a target. Throws if the basegame coalesced cannot be read,
        /// since nothing downstream is meaningful without it; a DLC that fails to load is logged and
        /// skipped, because one bad third-party mod should not stop the catalogue from building.
        /// </summary>
        public static MergedGameConfig Load(GameTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!ModMakerCatalogue.SupportsGame(target.Game))
                throw new NotSupportedException($@"ModMaker does not support {target.Game}");

            var basePath = M3Directories.GetCoalescedPath(target);
            var bundle = ConfigAssetBundle.FromSingleFile(target.Game, basePath);
            if (bundle == null)
                throw new InvalidOperationException($@"Could not load basegame coalesced: {basePath}");

            var merged = new List<string>();
            var dlcRoot = M3Directories.GetDLCPath(target);
            if (Directory.Exists(dlcRoot))
            {
                // Mount order, not directory order. This is the order the game resolves config in.
                foreach (var folder in ModMakerCatalogue.ScannableDLCFoldersInMountOrder(target))
                {
                    var cooked = Path.Combine(dlcRoot, folder, target.Game.CookedDirName());
                    if (!Directory.Exists(cooked)) continue;

                    ConfigAssetBundle dlcBundle;
                    try
                    {
                        dlcBundle = ConfigAssetBundle.FromDLCFolder(target.Game, cooked, folder);
                    }
                    catch (Exception e)
                    {
                        MLog.Warning($@"ModMaker catalogue: could not read config for {folder}: {e.Message}");
                        continue;
                    }

                    if (dlcBundle == null) continue;
                    dlcBundle.MergeInto(bundle);
                    merged.Add(folder);
                }
            }

            return new MergedGameConfig(bundle, merged);
        }

        /// <summary>
        /// Every section in the merged config.
        /// </summary>
        public IEnumerable<KeyValuePair<string, CoalesceSection>> Sections()
        {
            foreach (var assetName in Bundle.GetAssetNames())
            {
                var asset = Bundle.GetAsset(assetName, createIfNotFound: false);
                if (asset == null) continue;
                foreach (var section in asset.Sections)
                    yield return section;
            }
        }

        /// <summary>
        /// The values of one property that survive the merge.
        ///
        /// The merged bundle keeps Remove and RemoveProperty entries so it can be re-serialised as a
        /// DLC delta, so anything reading EFFECTIVE values has to filter them out. Forgetting this
        /// reports content the game does not have.
        /// </summary>
        public static IEnumerable<CoalesceValue> Live(CoalesceProperty property) =>
            property.Where(v => v.ParseAction is not (CoalesceParseAction.Remove or CoalesceParseAction.RemoveProperty));

        /// <summary>
        /// Replays the wire-type rules of docs/15-config-collapse.md §3 against a property's current
        /// entries, from empty, to produce the values an FConfigSection multimap would hold once the
        /// engine had resolved every New/RemoveProperty/Add/AddUnique/Remove action against it.
        ///
        /// Unlike <see cref="Live"/>, which only filters markers out of whatever is already there,
        /// this re-derives the result from scratch: it clears on New and RemoveProperty, suppresses
        /// AddUnique duplicates, and applies Remove by exact value-text match. It does not assume the
        /// entries were already correctly resolved by whatever produced them - only that each carries
        /// the right wire type - which makes it usable both on a bundle merged the normal way (as a
        /// second, independent check of the result) and on a raw, unmerged concatenation of a
        /// property's entries across files.
        ///
        /// <see cref="CoalesceParseAction.None"/> has no engine meaning and nothing in the wire format
        /// produces it as a stored parse action, so an entry carrying it is dropped rather than guessed
        /// at.
        /// </summary>
        public static List<CoalesceValue> CollapseProperty(IEnumerable<CoalesceValue> entries)
        {
            var result = new List<CoalesceValue>();
            foreach (var entry in entries)
            {
                switch (entry.ParseAction)
                {
                    case CoalesceParseAction.New:
                        result.Clear();
                        result.Add(entry);
                        break;
                    case CoalesceParseAction.RemoveProperty:
                        result.Clear();
                        break;
                    case CoalesceParseAction.Add:
                        result.Add(entry);
                        break;
                    case CoalesceParseAction.AddUnique:
                        if (!result.Exists(v => v.Value == entry.Value))
                            result.Add(entry);
                        break;
                    case CoalesceParseAction.Remove:
                        result.RemoveAll(v => v.Value == entry.Value);
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// Collapses the merged bundle to the effective, game-visible view for everything that reads
        /// the game rather than emits a delta - catalogues, editors, class-default resolution. See
        /// docs/15-config-collapse.md §2.
        ///
        /// Assets and sections that collapse away to nothing (every property inside them resolved to
        /// zero values) are dropped rather than kept empty: the game never wrote anything there, so an
        /// empty entry would be a phantom the ASI dump will not have either.
        /// </summary>
        public CollapsedConfig Collapse()
        {
            var collapsedAssets = new Dictionary<string, CoalesceAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (var assetName in Bundle.GetAssetNames())
            {
                var asset = Bundle.GetAsset(assetName, createIfNotFound: false);
                if (asset == null) continue;

                CoalesceAsset collapsedAsset = null;
                foreach (var section in asset.Sections)
                {
                    CoalesceSection collapsedSection = null;
                    foreach (var property in section.Value)
                    {
                        var values = CollapseProperty(property.Value);
                        if (values.Count == 0) continue;

                        collapsedSection ??= new CoalesceSection(section.Key);
                        collapsedSection.Add(property.Key, new CoalesceProperty(property.Key, values));
                    }

                    if (collapsedSection == null) continue;
                    collapsedAsset ??= new CoalesceAsset(assetName);
                    collapsedAsset.Sections.Add(section.Key, collapsedSection);
                }

                if (collapsedAsset != null)
                    collapsedAssets[assetName] = collapsedAsset;
            }
            return new CollapsedConfig(collapsedAssets);
        }

        /// <summary>
        /// Sections by key, for callers that resolve many sections and cannot afford a scan each time.
        ///
        /// The same section key can appear in more than one coalesced sub-file, so the first
        /// occurrence wins and the rest are listed in <see cref="SectionIndex.DuplicateKeys"/>.
        /// Accumulating them instead would double every count taken from an affected section.
        /// </summary>
        public SectionIndex BuildSectionIndex()
        {
            var byKey = new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase);
            var assetByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();
            foreach (var (assetName, key, section) in SectionsByAsset())
            {
                if (!byKey.TryAdd(key, section)) { duplicates.Add(key); continue; }
                assetByKey[key] = assetName;
            }
            return new SectionIndex(byKey, duplicates, assetByKey);
        }

        /// <summary>
        /// Every section, with the coalesced sub-file it came from.
        ///
        /// The sub-file is not recoverable from a section once it has been pulled out of its asset,
        /// and a compiler has to know it - see <see cref="SectionIndex.AssetOf"/>.
        /// </summary>
        public IEnumerable<(string Asset, string Key, CoalesceSection Section)> SectionsByAsset()
        {
            foreach (var assetName in Bundle.GetAssetNames())
            {
                var asset = Bundle.GetAsset(assetName, createIfNotFound: false);
                if (asset == null) continue;
                foreach (var section in asset.Sections)
                    yield return (Path.GetFileNameWithoutExtension(assetName), section.Key, section.Value);
            }
        }

        /// <summary>
        /// Values of a named property in a named section, or empty if either is absent.
        /// </summary>
        public IEnumerable<CoalesceValue> LiveValues(string section, string property)
        {
            foreach (var s in Sections())
            {
                if (!s.Key.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
                if (s.Value.TryGetValue(property, out var prop))
                    return Live(prop).ToList();
            }
            return [];
        }

        /// <summary>
        /// Counts values that appear more than once, byte-identical, under the same section and
        /// property, where at least one arrived as AddUnique.
        ///
        /// These should not exist: an AddUnique is meant to be suppressed when an identical value is
        /// already present. LegendaryExplorerCore's ConfigMerge did not do that - its AddUnique case
        /// found a match and then `continue`d the scanning loop instead of skipping the add, so
        /// AddUnique behaved exactly like Add, for 841 spurious duplicate values on LMP. Fixed; this
        /// now returns zero.
        ///
        /// Kept as a permanent smoke test rather than deleted with the bug: this class of failure is
        /// invisible in every other way. Nothing throws and nothing looks wrong - the config simply
        /// contains values the game does not have, and every catalogue built on it inherits them.
        /// </summary>
        public int CountUnsuppressedDuplicates()
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var fromAddUnique = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assetName in Bundle.GetAssetNames())
            {
                var asset = Bundle.GetAsset(assetName, createIfNotFound: false);
                if (asset == null) continue;
                foreach (var section in asset.Sections)
                foreach (var prop in section.Value)
                foreach (var v in prop.Value)
                {
                    if (v.ParseAction is CoalesceParseAction.Remove or CoalesceParseAction.RemoveProperty) continue;
                    var key = string.Join(' ', assetName, section.Key, prop.Key, v.Value);
                    seen[key] = seen.GetValueOrDefault(key) + 1;
                    if (v.ParseAction == CoalesceParseAction.AddUnique) fromAddUnique.Add(key);
                }
            }

            return seen.Where(kv => kv.Value > 1 && fromAddUnique.Contains(kv.Key)).Sum(kv => kv.Value - 1);
        }
    }
}
