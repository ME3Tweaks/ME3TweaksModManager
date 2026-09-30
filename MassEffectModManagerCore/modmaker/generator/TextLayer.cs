using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>What curation says should happen to a discovered class.</summary>
    public enum CurationDecision
    {
        /// <summary>A generic form page.</summary>
        Include = 0,

        /// <summary>Not shown.</summary>
        Exclude = 1,

        /// <summary>Owned by a structured editor rather than a form.</summary>
        Editor = 2,

        /// <summary>A page, but only the properties in the allowlist.</summary>
        Partial = 3,
    }

    public class CurationEntry
    {
        [JsonProperty(@"decision")] public string Decision { get; set; }
        [JsonProperty(@"reason")] public string Reason { get; set; }
        [JsonProperty(@"propertyAllowlist")] public List<string> PropertyAllowlist { get; set; }
        [JsonProperty(@"editor")] public string Editor { get; set; }
        [JsonProperty(@"group")] public string Group { get; set; }
    }

    public class FamilyDefault
    {
        [JsonProperty(@"group")] public string Group { get; set; }
        [JsonProperty(@"decision")] public string Decision { get; set; }
        [JsonProperty(@"reason")] public string Reason { get; set; }
    }

    public class TextGroup
    {
        [JsonProperty(@"id")] public string Id { get; set; }
        [JsonProperty(@"title")] public string Title { get; set; }
        [JsonProperty(@"description")] public string Description { get; set; }
        [JsonProperty(@"parent")] public string Parent { get; set; }
        [JsonProperty(@"order")] public int Order { get; set; }
    }

    public class DescriptionEntry
    {
        [JsonProperty(@"label")] public string Label { get; set; }
        [JsonProperty(@"unit")] public string Unit { get; set; }
        [JsonProperty(@"text")] public string Text { get; set; }
    }

    public class PropertyTextEntry
    {
        [JsonProperty(@"label")] public string Label { get; set; }
        [JsonProperty(@"unit")] public string Unit { get; set; }
        [JsonProperty(@"describedBy")] public string DescribedBy { get; set; }
        [JsonProperty(@"descriptionAppend")] public string DescriptionAppend { get; set; }
        [JsonProperty(@"description")] public string Description { get; set; }
        [JsonProperty(@"hidden")] public bool Hidden { get; set; }
    }

    public class ClassTextEntry
    {
        [JsonProperty(@"title")] public string Title { get; set; }
        [JsonProperty(@"description")] public string Description { get; set; }
        [JsonProperty(@"image")] public string Image { get; set; }
    }

    /// <summary>
    /// What a wavelist is called, and which enemies belong to it.
    ///
    /// Keyed by the wavelist's section - `sfxgamecontent_milan.sfxwave_horde_rejects`. Two facts the
    /// game does not state anywhere a tool can read:
    ///
    /// - <see cref="Title"/>: the faction's name. The class is called `SFXWave_Horde_Cerberus2`, and
    ///   nothing in it says "Cerberus, the second roster".
    /// - <see cref="EnemyPrefix"/>: the three-letter tag its enemy types carry, `REJ` for
    ///   `WAVE_REJ_Something`. The abbreviations are not derivable - CER is Cerberus, RPR is Reapers,
    ///   GTH is Geth - so a mod adding a faction states its own rather than hoping a heuristic guesses.
    /// </summary>
    public class FactionText
    {
        [JsonProperty(@"title")] public string Title { get; set; }
        [JsonProperty(@"enemyPrefix")] public string EnemyPrefix { get; set; }
    }

    public class AnchorEntry
    {
        [JsonProperty(@"value")] public string Value { get; set; }
        [JsonProperty(@"kind")] public string Kind { get; set; }
        [JsonProperty(@"usedBy")] public string UsedBy { get; set; }
        [JsonProperty(@"note")] public string Note { get; set; }
    }

    public class EnemyStatGroup
    {
        [JsonProperty(@"title")] public string Title { get; set; }
        [JsonProperty(@"description")] public string Description { get; set; }
        [JsonProperty(@"image")] public string Image { get; set; }
        [JsonProperty(@"stats")] public List<string> Stats { get; set; } = new();
    }

    public class EnemyCategoryText
    {
        [JsonProperty(@"title")] public string Title { get; set; }
        [JsonProperty(@"intro")] public string Intro { get; set; }
        [JsonProperty(@"image")] public string Image { get; set; }
        [JsonProperty(@"inheritsFrom")] public string InheritsFrom { get; set; }
        [JsonProperty(@"groups")] public List<EnemyStatGroup> Groups { get; set; } = new();

        /// <summary>
        /// Per-stat text for THIS category, overriding the shared entry.
        ///
        /// 33 stat names carry more than one label across the legacy pages and the variation is
        /// usually meaningful rather than sloppy: an armoured unit's MaxHealth is "Armor", a biotic's
        /// MaxShields are "Barriers", and a drone's MeleeAttackInterval is a zap. The shared entry
        /// carries the modal wording and this carries the exceptions.
        /// </summary>
        [JsonProperty(@"stats")]
        public Dictionary<string, EnemyStatText> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public EnemyStatText StatText(string statName) =>
            statName != null && Stats != null && Stats.TryGetValue(statName, out var t) ? t : null;
    }

    /// <summary>What a difficulty level is called, for the level the game numbers.</summary>
    public class EnemyLevelText
    {
        [JsonProperty(@"label")] public string Label { get; set; }
    }

    public class EnemyStatText
    {
        [JsonProperty(@"label")] public string Label { get; set; }
        [JsonProperty(@"unit")] public string Unit { get; set; }
        [JsonProperty(@"description")] public string Description { get; set; }
    }

    public class EnemyStatsText
    {
        [JsonProperty(@"categories")]
        public Dictionary<string, EnemyCategoryText> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"stats")]
        public Dictionary<string, EnemyStatText> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Names for the difficulty levels, keyed by the number the game gives them.
        ///
        /// A map rather than a list, so this is not a fourth way of counting to four: a level nobody
        /// has named is shown by its number, and one somebody adds needs an entry rather than a code
        /// change. The game itself carries these names only in TLK strings reached through the
        /// challenge banners, which is why they are authored here.
        /// </summary>
        [JsonProperty(@"levels")]
        public Dictionary<string, EnemyLevelText> Levels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public EnemyStatText Stat(string statName) =>
            statName != null && Stats != null && Stats.TryGetValue(statName, out var t) ? t : null;

        public EnemyLevelText Level(int level) =>
            Levels != null && Levels.TryGetValue(level.ToString(CultureInfo.InvariantCulture), out var t) ? t : null;

        /// <summary>
        /// A category's text, with anything it inherits already folded in.
        ///
        /// BansheeAlt is the "Original Banshee" the Banshee page describes without separately editing,
        /// so it carries a title of its own and nothing else. Resolving here rather than in the editor
        /// keeps the fallback in one place; the chain is followed rather than assumed to be one deep,
        /// and a cycle stops rather than hanging.
        /// </summary>
        public EnemyCategoryText Category(string name)
        {
            if (name == null || Categories == null || !Categories.TryGetValue(name, out var entry)) return null;
            if (string.IsNullOrWhiteSpace(entry.InheritsFrom)) return entry;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
            var resolved = new EnemyCategoryText
            {
                Title = entry.Title,
                Intro = entry.Intro,
                Image = entry.Image,
                Groups = entry.Groups,
                Stats = entry.Stats,
            };

            var cursor = entry;
            while (!string.IsNullOrWhiteSpace(cursor.InheritsFrom) && seen.Add(cursor.InheritsFrom)
                   && Categories.TryGetValue(cursor.InheritsFrom, out var parent))
            {
                resolved.Intro ??= parent.Intro;
                resolved.Image ??= parent.Image;
                if (resolved.Groups == null || resolved.Groups.Count == 0) resolved.Groups = parent.Groups;
                if (resolved.Stats == null || resolved.Stats.Count == 0) resolved.Stats = parent.Stats;
                cursor = parent;
            }

            return resolved;
        }
    }

    /// <summary>
    /// The half of the catalogue that ships with ModMaker and is maintained by hand: which classes
    /// become pages, how they are grouped, and every word a user reads.
    ///
    /// The other half - classes, properties, defaults, instances, images - is generated on the user's
    /// machine from their installed game and cannot be authored ahead of time. The two join on class
    /// name and property name, which is why everything here is keyed by game identifiers rather than
    /// by position: the file survives regeneration against a different install.
    ///
    /// Content this file has never heard of still appears, with a fallback title and no prose. That is
    /// not a degraded mode, it is the design - an exclusion list cannot anticipate someone else's mod,
    /// so unrecognised content is included and flagged rather than dropped.
    /// </summary>
    public class TextLayer
    {
        [JsonProperty(@"format")] public int Format { get; set; }
        [JsonProperty(@"version")] public string Version { get; set; }

        [JsonProperty(@"curation")]
        public Dictionary<string, CurationEntry> Curation { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"groups")]
        public List<TextGroup> Groups { get; set; } = new();

        [JsonProperty(@"familyDefaults")]
        public Dictionary<string, FamilyDefault> FamilyDefaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"descriptions")]
        public Dictionary<string, DescriptionEntry> Descriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"propertyText")]
        public Dictionary<string, PropertyTextEntry> PropertyText { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"classText")]
        public Dictionary<string, ClassTextEntry> ClassText { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Wavelist metadata, keyed by section. See <see cref="FactionText"/>.
        ///
        /// The first thing a mod ships in its own `ModMaker` folder that is not prose: a mod adding a
        /// faction is the case where ModMaker cannot work the answer out and the mod always can.
        /// </summary>
        [JsonProperty(@"factions")]
        public Dictionary<string, FactionText> Factions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(@"enemyStats")]
        public EnemyStatsText EnemyStats { get; set; } = new();

        [JsonProperty(@"legacyPages")]
        public Dictionary<string, string> LegacyPages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The class, property and section names the generator looks for, in one place rather than
        /// scattered through the code. Every one is a base-game name that mods extend rather than
        /// replace, which is what makes discovery safe.
        /// </summary>
        [JsonProperty(@"anchors")]
        public Dictionary<string, AnchorEntry> Anchors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The highest format this build understands. A newer file is refused rather than partially
        /// parsed, because a silently ignored curation decision shows content that was meant to be
        /// hidden.
        /// </summary>
        public const int SupportedFormat = 1;

        /// <summary>
        /// Newtonsoft rather than System.Text.Json: it is what the rest of Mod Manager uses, it takes
        /// the JSONC comments this layer is authored with, and it matches property names
        /// case-insensitively by default.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        /// <summary>
        /// Folds another layer's text on top of this one, in place.
        ///
        /// For bundles a mod ships in `DLC_MOD_Whatever/ModMaker/textlayer.json`. Applied in ASCENDING
        /// mount order, so the same DLC that wins in the game wins here: a mod that overrides another
        /// mod's gun describes it too.
        ///
        /// Only text is taken. Curation, family defaults and anchors are deliberately not merged:
        /// curation decides what becomes a page at all, and anchors are the class and property names
        /// the scanners search for, so a bundle carrying either could hide content or send discovery
        /// somewhere else - and a mod does not need that power to describe what it adds. Unrecognised
        /// content already appears without a curation entry, flagged, which is the whole point of that
        /// design.
        ///
        /// Merge is per KEY rather than per file: an entry the bundle does not mention keeps whatever
        /// it had, so describing one new weapon does not blank out the other hundred and one.
        /// </summary>
        public void Overlay(TextLayer other)
        {
            if (other == null) return;

            Absorb(ClassText, other.ClassText);
            Absorb(Factions, other.Factions);
            Absorb(Descriptions, other.Descriptions);
            Absorb(PropertyText, other.PropertyText);
            Absorb(LegacyPages, other.LegacyPages);

            if (other.EnemyStats != null)
            {
                EnemyStats ??= new EnemyStatsText();
                Absorb(EnemyStats.Categories, other.EnemyStats.Categories);
                Absorb(EnemyStats.Stats, other.EnemyStats.Stats);
                Absorb(EnemyStats.Levels, other.EnemyStats.Levels);
            }

            // Groups are a list keyed by id rather than a map, so they are matched on the id and
            // replaced whole. A group nobody has heard of is added; ordering stays with `order`.
            foreach (var group in other.Groups ?? new List<TextGroup>())
            {
                if (group?.Id == null) continue;
                Groups ??= new List<TextGroup>();
                var at = Groups.FindIndex(g => string.Equals(g?.Id, group.Id, StringComparison.OrdinalIgnoreCase));
                if (at >= 0) Groups[at] = group;
                else Groups.Add(group);
            }
        }

        private static void Absorb<T>(Dictionary<string, T> into, Dictionary<string, T> from)
        {
            if (into == null || from == null) return;
            foreach (var pair in from) into[pair.Key] = pair.Value;
        }

        public static TextLayer Load(string path) => Parse(File.ReadAllText(path), path);

        public static TextLayer Parse(string json, string source = null)
        {
            var layer = JsonConvert.DeserializeObject<TextLayer>(json, Settings)
                        ?? throw new InvalidDataException($@"Text layer is empty: {source}");

            if (layer.Format > SupportedFormat)
                throw new InvalidDataException(
                    $@"Text layer format {layer.Format} is newer than this build understands ({SupportedFormat}): {source}");

            // Whether a deserializer keeps the comparer a dictionary property was initialised with is
            // its own business, and depends on the serializer and on how the property is declared.
            // Section and property names are written in whatever case each source used, and a
            // case-sensitive miss looks exactly like "no text was authored for this" - invisible, and
            // it would affect most lookups. So the comparer is imposed here rather than assumed.
            layer.Curation = Recase(layer.Curation);
            layer.FamilyDefaults = Recase(layer.FamilyDefaults);
            layer.Descriptions = Recase(layer.Descriptions);
            layer.PropertyText = Recase(layer.PropertyText);
            layer.ClassText = Recase(layer.ClassText);
            layer.Factions = Recase(layer.Factions);
            layer.LegacyPages = Recase(layer.LegacyPages);
            layer.Anchors = Recase(layer.Anchors);
            if (layer.EnemyStats != null)
            {
                layer.EnemyStats.Categories = Recase(layer.EnemyStats.Categories);
                layer.EnemyStats.Stats = Recase(layer.EnemyStats.Stats);
                layer.EnemyStats.Levels = Recase(layer.EnemyStats.Levels);
                foreach (var category in layer.EnemyStats.Categories.Values)
                    category.Stats = Recase(category.Stats);
            }

            return layer;
        }

        private static Dictionary<string, T> Recase<T>(Dictionary<string, T> source) =>
            source == null ? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase)
                           : new Dictionary<string, T>(source, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// An anchor's value, or <paramref name="fallback"/> when the layer does not define it.
        ///
        /// The fallback is the built-in constant, so a text layer that omits an anchor still works;
        /// what it must never do is return null and let a scanner search for nothing.
        /// </summary>
        public string Anchor(string name, string fallback) =>
            Anchors.TryGetValue(name, out var a) && !string.IsNullOrWhiteSpace(a.Value) ? a.Value : fallback;
    }
}
