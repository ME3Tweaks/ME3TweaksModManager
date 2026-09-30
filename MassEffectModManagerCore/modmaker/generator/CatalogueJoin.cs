using System;
using System.Collections.Generic;
using System.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>Where a resolved value came from. Carried into the bundle so the UI can flag it.</summary>
    public enum TextSource
    {
        /// <summary>Nothing was authored; the value was derived from game identifiers.</summary>
        Generated = 0,

        /// <summary>Authored for this exact class or property.</summary>
        Authored = 1,

        /// <summary>Authored for every class declaring this property (the "*/property" form).</summary>
        Shared = 2,

        /// <summary>Inherited from a family default rather than authored for this class.</summary>
        Family = 3,
    }

    public class ResolvedCuration
    {
        public CurationDecision Decision { get; init; }
        public string Reason { get; init; }
        public string Group { get; init; }
        public string Editor { get; init; }
        public IReadOnlyList<string> PropertyAllowlist { get; init; }
        public TextSource Source { get; init; }

        /// <summary>
        /// False when nothing in the text layer mentions this class or its family.
        ///
        /// Unrecognised is not the same as excluded. It means included, grouped under the catch-all,
        /// and flagged - which is how a faction from someone else's mod becomes editable without a
        /// ModMaker release. An exclusion list cannot anticipate a mod, so the default has to be "show
        /// it and say we do not know what it is".
        /// </summary>
        public bool Recognised { get; init; }

        /// <summary>Whether a property survives curation. Only <see cref="CurationDecision.Partial"/> filters.</summary>
        public bool AllowsProperty(string propertyName) =>
            Decision != CurationDecision.Partial
            || (PropertyAllowlist != null && PropertyAllowlist.Contains(propertyName, StringComparer.OrdinalIgnoreCase));
    }

    public class ResolvedText
    {
        public string Label { get; init; }
        public string Unit { get; init; }
        public string Description { get; init; }
        public bool Hidden { get; init; }
        public TextSource Source { get; init; }
    }

    public class ResolvedTitle
    {
        public string Title { get; init; }
        public string Description { get; init; }
        public string Image { get; init; }
        public TextSource Source { get; init; }

        /// <summary>Which step of the fallback chain produced the title. Recorded, so it can be audited.</summary>
        public string TitleSource { get; init; }
    }

    /// <summary>
    /// Joins the generated data layer to the shipped text and curation layer.
    ///
    /// The two halves meet on class name and property name and nothing else - no positions, no
    /// indices - so the same text layer works against any install, including one with mods the layer
    /// has never heard of. Every resolution here answers "what does the user see", and every one has a
    /// defined answer when nothing was authored, because the common case in a modded install is that
    /// nothing was.
    ///
    /// See docs/02-data-model.md and docs/12-generation-and-modded-content.md.
    /// </summary>
    public class CatalogueJoin
    {
        /// <summary>Group unrecognised content lands in. Must exist in the layer's groups.</summary>
        public const string UnrecognisedGroup = @"unrecognised";

        private readonly TextLayer _text;

        public CatalogueJoin(TextLayer text) => _text = text ?? new TextLayer();

        /// <summary>Things the join noticed that a maintainer should see - dangling ids and the like.</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// What happens to a discovered class: exact curation entry, else the longest matching family
        /// prefix, else unrecognised.
        ///
        /// Longest prefix, not first match: SFXPowerCustomActionMP has to beat SFXPowerCustomAction,
        /// and a shortest-first scan would silently group every MP power under the wrong family.
        /// </summary>
        public ResolvedCuration ResolveCuration(CatalogueClass cls)
        {
            if (cls == null) throw new ArgumentNullException(nameof(cls));

            if (_text.Curation.TryGetValue(cls.Section, out var entry))
            {
                var decision = ParseDecision(entry.Decision, cls.Section);
                return new ResolvedCuration
                {
                    Decision = decision,
                    Reason = entry.Reason,
                    Group = entry.Group ?? FamilyFor(cls.Name)?.Value.Group,
                    Editor = entry.Editor,
                    PropertyAllowlist = entry.PropertyAllowlist,
                    Source = TextSource.Authored,
                    Recognised = true,
                };
            }

            if (FamilyFor(cls.Name) is { } family)
            {
                return new ResolvedCuration
                {
                    Decision = ParseDecision(family.Value.Decision, cls.Section, CurationDecision.Include),
                    Reason = family.Value.Reason,
                    Group = family.Value.Group,
                    Source = TextSource.Family,
                    Recognised = true,
                };
            }

            return new ResolvedCuration
            {
                Decision = CurationDecision.Include,
                Reason = @"Not described by this version of ModMaker - probably content from another mod.",
                Group = UnrecognisedGroup,
                Source = TextSource.Generated,
                Recognised = false,
            };
        }

        /// <summary>
        /// The words shown for one property of one class.
        ///
        /// The specific "&lt;section&gt;/&lt;property&gt;" form beats the "*/&lt;property&gt;" wildcard,
        /// because the wildcard carries the modal label and the specific form carries the exceptions -
        /// MaxHealth is "Health" on most units, "Armor" on armoured ones. Those exceptions are the
        /// correct domain distinction, not noise, so the specific form must win.
        ///
        /// With nothing authored, the label is the property's own name: honest, searchable, and
        /// clearly different from a real label.
        /// </summary>
        public ResolvedText ResolveProperty(string section, string propertyName)
        {
            var specific = _text.PropertyText.GetValueOrDefault($@"{section}/{propertyName}");
            var wildcard = _text.PropertyText.GetValueOrDefault($@"*/{propertyName}");
            var entry = specific ?? wildcard;

            if (entry == null)
            {
                return new ResolvedText { Label = propertyName, Source = TextSource.Generated };
            }

            DescriptionEntry shared = null;
            if (!string.IsNullOrWhiteSpace(entry.DescribedBy))
            {
                shared = _text.Descriptions.GetValueOrDefault(entry.DescribedBy);
                if (shared == null)
                    Warnings.Add($@"propertyText '{section}/{propertyName}' points at description '{entry.DescribedBy}', which does not exist.");
            }

            // A full description overrides the pool; an append specialises it rather than replacing
            // it, so the shared prose stays translatable in one place.
            var description = entry.Description ?? shared?.Text;
            if (!string.IsNullOrWhiteSpace(entry.DescriptionAppend))
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? entry.DescriptionAppend
                    : description + @" " + entry.DescriptionAppend;
            }

            return new ResolvedText
            {
                Label = entry.Label ?? shared?.Label ?? propertyName,
                Unit = entry.Unit ?? shared?.Unit,
                Description = description,
                Hidden = entry.Hidden,
                Source = specific != null ? TextSource.Authored : TextSource.Shared,
            };
        }

        /// <summary>
        /// A page's title, down the fallback chain from docs/02-data-model.md §1.5.1:
        ///
        ///   1. authored text, 2. the class's own DisplayName string ref via the TLK,
        ///   3. the store card table's name, 4. the class name with scaffolding stripped.
        ///
        /// Steps 2 and 3 need the name scan, which is expensive and separate, so they arrive through
        /// <paramref name="gameName"/>. Step 4 always produces something: only 468 of 2,304 candidate
        /// classes are named anywhere in the game, and a page titled "Reegar" is strictly better than
        /// no page at all. Which step won is recorded rather than inferred, so the ugly ones can be
        /// listed and fixed by authoring.
        /// </summary>
        public ResolvedTitle ResolveTitle(CatalogueClass cls, Func<CatalogueClass, (string Name, string Source)> gameName = null)
        {
            if (cls == null) throw new ArgumentNullException(nameof(cls));

            var authored = _text.ClassText.GetValueOrDefault(cls.Section);
            if (!string.IsNullOrWhiteSpace(authored?.Title))
            {
                return new ResolvedTitle
                {
                    Title = authored.Title,
                    Description = authored.Description,
                    Image = authored.Image,
                    Source = TextSource.Authored,
                    TitleSource = @"authored",
                };
            }

            var fromGame = gameName?.Invoke(cls);
            if (!string.IsNullOrWhiteSpace(fromGame?.Name))
            {
                return new ResolvedTitle
                {
                    Title = fromGame.Value.Name,
                    Description = authored?.Description,
                    Image = authored?.Image,
                    Source = TextSource.Generated,
                    // "tlk" or "storeCard" - which rung of the chain found it. The caller knows; this
                    // must not invent a third value, because the schema enumerates them.
                    TitleSource = string.IsNullOrWhiteSpace(fromGame.Value.Source) ? @"tlk" : fromGame.Value.Source,
                };
            }

            return new ResolvedTitle
            {
                Title = TitleFromClassName(cls.Name),
                Description = authored?.Description,
                Image = authored?.Image,
                Source = TextSource.Generated,
                TitleSource = @"className",
            };
        }

        /// <summary>
        /// The class name with the engine's scaffolding stripped:
        /// SFXProjectile_Reegar_MP becomes "Reegar".
        ///
        /// Never returns empty. An abstract base whose whole name is scaffolding - SFXProjectile - is
        /// left as it is, because the user asked for those to be editable for global changes and a
        /// blank page title is worse than an ugly one.
        /// </summary>
        public static string TitleFromClassName(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return className;

            var bare = className.Contains('.') ? className[(className.LastIndexOf('.') + 1)..] : className;
            var parts = bare.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) return bare;

            // Trailing MP / MP4 markers say which release added the content, not what it is.
            while (parts.Count > 1 && IsReleaseMarker(parts[^1])) parts.RemoveAt(parts.Count - 1);

            // The leading token is the family - SFXWeapon, SFXPowerCustomAction - which is the group
            // the page already sits under, so repeating it in the title says nothing.
            if (parts.Count > 1 && parts[0].StartsWith(@"SFX", StringComparison.OrdinalIgnoreCase))
                parts.RemoveAt(0);

            return string.Join(' ', parts);
        }

        private static bool IsReleaseMarker(string part) =>
            part.Length >= 2 && part.StartsWith(@"MP", StringComparison.OrdinalIgnoreCase)
                             && part.Skip(2).All(char.IsDigit)
            || part.Equals(@"MP", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The longest family prefix matching a class name.
        ///
        /// Matched on the bare class NAME rather than the section, because a family is a naming
        /// convention and the package a class was cooked into has nothing to do with it.
        /// </summary>
        private KeyValuePair<string, FamilyDefault>? FamilyFor(string className)
        {
            KeyValuePair<string, FamilyDefault>? best = null;
            foreach (var family in _text.FamilyDefaults)
            {
                if (!className.StartsWith(family.Key, StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null || family.Key.Length > best.Value.Key.Length) best = family;
            }
            return best;
        }

        private CurationDecision ParseDecision(string value, string section, CurationDecision fallback = CurationDecision.Include)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (Enum.TryParse<CurationDecision>(value, ignoreCase: true, out var parsed)) return parsed;

            // An unknown decision must not be guessed at. Defaulting to exclude would hide content
            // over a typo; defaulting to include shows it, which is visible and recoverable.
            Warnings.Add($@"Curation for '{section}' has unknown decision '{value}'; treated as include.");
            return CurationDecision.Include;
        }

        /// <summary>
        /// Checks the layer's internal references. These are authoring mistakes rather than game data
        /// problems, and each one silently produces a page in the wrong place or with no words on it.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var groupIds = _text.Groups.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (section, entry) in _text.Curation)
            {
                if (entry.Group != null && !groupIds.Contains(entry.Group))
                    problems.Add($@"Curation for '{section}' names group '{entry.Group}', which is not defined.");

                if (string.Equals(entry.Decision, @"partial", StringComparison.OrdinalIgnoreCase)
                    && (entry.PropertyAllowlist == null || entry.PropertyAllowlist.Count == 0))
                    problems.Add($@"Curation for '{section}' is 'partial' with no propertyAllowlist, which would hide every property.");

                if (string.Equals(entry.Decision, @"editor", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(entry.Editor))
                    problems.Add($@"Curation for '{section}' is 'editor' but names no editor.");
            }

            foreach (var (family, entry) in _text.FamilyDefaults)
            {
                if (entry.Group != null && !groupIds.Contains(entry.Group))
                    problems.Add($@"Family default '{family}' names group '{entry.Group}', which is not defined.");
            }

            foreach (var (key, entry) in _text.PropertyText)
            {
                if (!string.IsNullOrWhiteSpace(entry.DescribedBy) && !_text.Descriptions.ContainsKey(entry.DescribedBy))
                    problems.Add($@"propertyText '{key}' points at description '{entry.DescribedBy}', which does not exist.");
            }

            foreach (var (name, category) in _text.EnemyStats?.Categories ?? new())
            {
                if (!string.IsNullOrWhiteSpace(category.InheritsFrom)
                    && !_text.EnemyStats.Categories.ContainsKey(category.InheritsFrom))
                    problems.Add($@"Enemy category '{name}' inherits from '{category.InheritsFrom}', which does not exist.");
            }

            if (!groupIds.Contains(UnrecognisedGroup))
                problems.Add($@"No '{UnrecognisedGroup}' group is defined, so content from other mods would have nowhere to go.");

            return problems;
        }
    }
}
