using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// One stat of one category at one difficulty level.
    /// </summary>
    public class DifficultyStat
    {
        public string StatName { get; init; }

        /// <summary>
        /// The ends of an interpolation, NOT a minimum and a maximum - either may be the larger.
        /// EvadeFrequency runs 9.0 down to 7.0 across the levels while MaxHealth runs 500 up to 750.
        /// Labelling these "min" and "max" in the editor would be wrong for every stat that improves
        /// as it falls, and would invite a UI that refuses to let X exceed Y.
        /// </summary>
        public double X { get; init; }
        public double Y { get; init; }

        /// <summary>The value text as shipped, for exact-match Remove emission.</summary>
        public string RawValue { get; init; }
    }

    /// <summary>One category - an enemy - at one difficulty level.</summary>
    public class DifficultyCategoryLevel
    {
        public int Level { get; init; }
        public List<DifficultyStat> Stats { get; } = new();

        /// <summary>
        /// The whole category entry as shipped. A category is one array element, and an edit replaces
        /// the whole array, so every category is re-emitted - untouched ones from this text verbatim.
        /// </summary>
        public string RawValue { get; init; }
    }

    /// <summary>One category across every level that defines it.</summary>
    public class DifficultyCategory
    {
        public string Name { get; init; }
        public List<DifficultyCategoryLevel> Levels { get; } = new();

        /// <summary>Distinct stat names across all levels of this category.</summary>
        public IEnumerable<string> StatNames =>
            Levels.SelectMany(l => l.Stats).Select(s => s.StatName).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One difficulty handler: a config section carrying Level&lt;n&gt;DifficultyData arrays.
    /// </summary>
    public class DifficultyTable
    {
        /// <summary>Coalesced section, e.g. sfxgamempcontent.sfxdifficultyhandlermp.</summary>
        public string Section { get; init; }

        /// <summary>Class name if the scan resolved one, else null. Curation selects on this.</summary>
        public string ClassName { get; init; }

        public List<DifficultyCategory> Categories { get; } = new();

        /// <summary>Levels this handler defines, ascending. Four in multiplayer, five in singleplayer.</summary>
        public List<int> Levels { get; } = new();

        /// <summary>Distinct (category, stat) pairs - the editable surface before levels multiply it.</summary>
        public int PairCount => Categories.Sum(c => c.StatNames.Count());

        /// <summary>Every individual editable value: (level, category, stat).</summary>
        public int ValueCount => Categories.Sum(c => c.Levels.Sum(l => l.Stats.Count));

        /// <summary>
        /// (category, stat) pairs that are not defined at every level this handler has.
        ///
        /// The grid is very nearly rectangular but not quite, and a missing cell must be shown as
        /// absent rather than filled with a neighbouring level's value - writing an invented value is
        /// indistinguishable from the user having chosen it.
        /// </summary>
        public List<(string Category, string Stat, List<int> MissingLevels)> IncompletePairs()
        {
            var result = new List<(string, string, List<int>)>();
            foreach (var cat in Categories)
            {
                foreach (var stat in cat.StatNames)
                {
                    var missing = Levels.Where(lvl => !cat.Levels.Any(
                        l => l.Level == lvl && l.Stats.Any(s => s.StatName.Equals(stat, StringComparison.OrdinalIgnoreCase)))).ToList();
                    if (missing.Count > 0) result.Add((cat.Name, stat, missing));
                }
            }
            return result;
        }
    }

    /// <summary>
    /// The enemy stat surface, out of BioDifficulty.
    ///
    /// This is why BioDifficulty looks like a rounding error in the class scan - two classes, a
    /// handful of config properties. The stats are not config properties at all; they are rows inside
    /// one, so nothing about them is discoverable from the class hierarchy and they can only be read
    /// from the shipped config text:
    ///
    ///   Level3DifficultyData=(Category="SFXPawn_Cannibal", CategoryData=(
    ///       (StatName="MaxHealth",      StatRange=(X=500.0f, Y=750.0f)),
    ///       (StatName="EvadeFrequency", StatRange=(X=9.0f,   Y=7.0f))))
    ///
    /// Handlers are found by the property-name shape rather than by section name, so a mod adding its
    /// own difficulty handler is picked up without this tool being told about it.
    /// </summary>
    public class DifficultyCatalogue
    {
        private static readonly Regex LevelPropertyRx =
            new(@"^level(?<n>\d+)difficultydata$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<DifficultyTable> Tables { get; } = new();

        /// <summary>Anything that did not read cleanly, in words a person can act on.</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>Values that did not parse and are therefore absent from this catalogue.</summary>
        public List<string> MalformedValues { get; } = new();

        public DifficultyTable Table(string sectionOrClass) =>
            Tables.FirstOrDefault(t => t.Section.Equals(sectionOrClass, StringComparison.OrdinalIgnoreCase)
                                    || (t.ClassName != null && t.ClassName.Equals(sectionOrClass, StringComparison.OrdinalIgnoreCase)));

        public static DifficultyCatalogue Build(MergedGameConfig config, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var result = new DifficultyCatalogue();
            var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (sectionKey, section) in config.Sections())
            {
                // A section key can appear in more than one coalesced sub-file; counting it twice
                // would double every stat in the table.
                if (!seenSections.Add(sectionKey)) continue;

                var levelProps = section.Where(p => LevelPropertyRx.IsMatch(p.Key)).ToList();
                if (levelProps.Count == 0) continue;

                var table = new DifficultyTable
                {
                    Section = sectionKey,
                    ClassName = classes != null && classes.TryGetValue(sectionKey, out var cls) ? cls.Name : null,
                };

                foreach (var prop in levelProps)
                {
                    int level = int.Parse(LevelPropertyRx.Match(prop.Key).Groups[@"n"].Value, CultureInfo.InvariantCulture);
                    if (!table.Levels.Contains(level)) table.Levels.Add(level);

                    foreach (var v in MergedGameConfig.Live(prop.Value))
                        result.ReadCategory(table, level, v.Value, sectionKey);
                }

                table.Levels.Sort();
                result.Tables.Add(table);
            }

            if (result.Tables.Count == 0)
                result.Warnings.Add(@"No difficulty handler sections were found; the enemy stat editor would be empty.");

            return result;
        }

        private void ReadCategory(DifficultyTable table, int level, string raw, string sectionKey)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            Dictionary<string, string> entry;
            try { entry = ConfigStructReader.ReadStruct(raw); }
            catch (FormatException e) { Malformed($@"[{sectionKey}] Level{level}DifficultyData", e); return; }

            var name = entry.GetValueOrDefault(@"Category");
            if (string.IsNullOrWhiteSpace(name))
            {
                Warnings.Add($@"[{sectionKey}] Level{level}DifficultyData has an entry with no Category; it cannot be addressed and was skipped.");
                return;
            }

            var category = table.Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                category = new DifficultyCategory { Name = name };
                table.Categories.Add(category);
            }

            var levelEntry = new DifficultyCategoryLevel { Level = level, RawValue = raw };

            List<string> statGroups;
            try { statGroups = ConfigStructReader.ReadList(entry.GetValueOrDefault(@"CategoryData")); }
            catch (FormatException e) { Malformed($@"[{sectionKey}] Level{level} {name}.CategoryData", e); return; }

            foreach (var group in statGroups)
            {
                Dictionary<string, string> stat;
                try { stat = ConfigStructReader.ReadStruct(group); }
                catch (FormatException e) { Malformed($@"[{sectionKey}] Level{level} {name}", e); continue; }

                var statName = stat.GetValueOrDefault(@"StatName");
                if (string.IsNullOrWhiteSpace(statName))
                {
                    Warnings.Add($@"[{sectionKey}] Level{level} {name} has a stat with no StatName; it was skipped.");
                    continue;
                }

                Dictionary<string, string> range;
                try { range = ConfigStructReader.ReadStruct(stat.GetValueOrDefault(@"StatRange")); }
                catch (FormatException e) { Malformed($@"[{sectionKey}] Level{level} {name}.{statName}", e); continue; }

                // Reported and skipped, never defaulted. `?? 0` here would turn a typo in the
                // config into a stat the editor shows as zero and would happily scale - and LMP
                // ships exactly that: Gunner.MaxHealth at level 4 is written `X=1687.5.0f`, which
                // parses as nothing and would have read as a Gunner with no health. A stat that is
                // not read is shown as absent, which is a state the editor already has to handle.
                var x = ParseDouble(range.GetValueOrDefault(@"X"));
                var y = ParseDouble(range.GetValueOrDefault(@"Y"));
                if (x == null || y == null)
                {
                    var bad = x == null ? @"X" : @"Y";
                    Malformed($@"[{sectionKey}] Level{level} {name}.{statName}",
                        new FormatException($@"{bad} is not a number: '{group}'"));
                    continue;
                }

                levelEntry.Stats.Add(new DifficultyStat
                {
                    StatName = statName,
                    X = x.Value,
                    Y = y.Value,
                    RawValue = group,
                });
            }

            category.Levels.Add(levelEntry);
        }

        private void Malformed(string context, Exception e)
        {
            var message = $@"{context}: malformed config value, skipped - {e.Message}";
            MalformedValues.Add(message);
            Warnings.Add(message);
        }

        /// <summary>Difficulty numbers are written with a trailing 'f' far more often than not.</summary>
        internal static double? ParseDouble(string s)
        {
            var t = s?.Trim().TrimEnd('f', 'F');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }
}
