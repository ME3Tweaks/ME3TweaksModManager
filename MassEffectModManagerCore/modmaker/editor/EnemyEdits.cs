using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// One (level, category, stat) the user has changed.
    ///
    /// Both ends are always carried, even when only one was typed into. X and Y are one value in the
    /// game - a single StatRange struct - so an edit that stored only the end that changed would have
    /// to read the other back out of the install at compile time and would silently pick up a later
    /// Milan build's value for the half the user thought they had left alone.
    /// </summary>
    public class EnemyStatEdit
    {
        /// <summary>The handler's coalesced section, e.g. sfxgamempcontent.sfxdifficultyhandlermp.</summary>
        [JsonProperty(@"section")]
        public string Section { get; set; }

        /// <summary>The difficulty level as the game numbers it: Level&lt;n&gt;DifficultyData.</summary>
        [JsonProperty(@"level")]
        public int Level { get; set; }

        /// <summary>The Category value, e.g. Banshee. Not a class - it is a row key inside an array.</summary>
        [JsonProperty(@"category")]
        public string Category { get; set; }

        [JsonProperty(@"stat")]
        public string StatName { get; set; }

        /// <summary>
        /// The ends of the interpolation, NOT a minimum and a maximum. Either may be the larger:
        /// MaxHealth runs 500 up to 750 while EvadeFrequency runs 9.0 down to 7.0.
        /// </summary>
        [JsonProperty(@"x")]
        public double X { get; set; }

        [JsonProperty(@"y")]
        public double Y { get; set; }

        [JsonIgnore]
        public string Key => MakeKey(Section, Level, Category, StatName);

        public static string MakeKey(string section, int level, string category, string statName) =>
            string.Join('|', section?.ToLowerInvariant(), level.ToString(CultureInfo.InvariantCulture),
                        category?.ToLowerInvariant(), statName?.ToLowerInvariant());
    }

    /// <summary>
    /// The enemy editor's payload inside a project file.
    ///
    /// Sparse in exactly the way <see cref="WavelistEditDocument"/> is, and for the same reason: a
    /// stat that is not here is absent rather than "recorded as unchanged", so reverting one means
    /// removing its entry and not writing today's shipped numbers into it. Enemy stats make that
    /// sharper than waves do - a category row is re-emitted whole, so a pinned value would follow
    /// every future edit to its neighbours.
    /// </summary>
    public class EnemyEditDocument
    {
        public const int CurrentFormat = 1;

        /// <summary>The collection id this document is stored under in a project's `collections`.</summary>
        public const string CollectionId = @"enemy";

        [JsonProperty(@"format")]
        public int Format { get; set; } = CurrentFormat;

        [JsonProperty(@"stats")]
        public List<EnemyStatEdit> Stats { get; set; } = new();

        [JsonIgnore]
        public int Count => Stats.Count;

        /// <summary>
        /// The number of category rows a compile would rewrite. Lower than <see cref="Count"/>
        /// whenever the user edited more than one stat on the same enemy, which is the normal case.
        /// </summary>
        [JsonIgnore]
        public int RowCount => Stats.Select(s => string.Join('|', s.Section?.ToLowerInvariant(),
                                                             s.Level, s.Category?.ToLowerInvariant()))
                                    .Distinct().Count();

        public void Set(EnemyStatEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Stats.RemoveAll(s => s.Key == edit.Key);
            Stats.Add(edit);
        }

        public bool Revert(string section, int level, string category, string statName)
        {
            var key = EnemyStatEdit.MakeKey(section, level, category, statName);
            return Stats.RemoveAll(s => s.Key == key) > 0;
        }

        public EnemyStatEdit Find(string section, int level, string category, string statName)
        {
            var key = EnemyStatEdit.MakeKey(section, level, category, statName);
            return Stats.FirstOrDefault(s => s.Key == key);
        }

        /// <summary>The edits grouped the way they are emitted: one config array entry per group.</summary>
        public IEnumerable<IGrouping<string, EnemyStatEdit>> ByRow() =>
            Stats.GroupBy(s => string.Join('|', s.Section?.ToLowerInvariant(), s.Level,
                                           s.Category?.ToLowerInvariant()));

        public JObject ToJson() => JObject.FromObject(this);

        public string ToJsonString(bool indented = true) =>
            ToJson().ToString(indented ? Formatting.Indented : Formatting.None);

        public static EnemyEditDocument FromJson(JObject json)
        {
            if (json == null) return new EnemyEditDocument();

            var format = (int?)json[@"format"] ?? CurrentFormat;
            if (format > CurrentFormat)
                throw new NotSupportedException($@"This enemy collection is format {format}; this build understands {CurrentFormat}. Update Mod Manager.");

            var doc = json.ToObject<EnemyEditDocument>() ?? new EnemyEditDocument();
            doc.Stats ??= new List<EnemyStatEdit>();
            return doc;
        }
    }

    /// <summary>
    /// Renders an edited category row back into the config text the game reads.
    ///
    /// A category is ONE array entry - `(Category="Banshee", CategoryData=(...41 stats...))` - so
    /// changing one number means removing that whole entry by exact text and adding a replacement.
    /// Every untouched stat therefore has to come back out, and the obvious way to do that is to
    /// re-serialise the parsed row. This does not do that. It edits the shipped text IN PLACE,
    /// substituting one number at a time, because re-serialising rewrites the float renderings and the
    /// spacing of forty values nobody touched - and Milan's config carries `\t\t\t` padding, spaces
    /// around some `=` and not others, and trailing `f` on most numbers but not all. None of that
    /// matters to the game, and all of it matters to a person reading the diff of a mod.
    ///
    /// The substitution is exact and refuses to guess: a stat whose shipped text does not appear
    /// exactly once in its row throws rather than replacing the wrong occurrence.
    /// </summary>
    public static class CategoryRowWriter
    {
        /// <summary>
        /// `(X=500.0f, Y=750.0f)`, capturing the separators so the replacement keeps them.
        ///
        /// Anchored and total: a StatRange with a third member, or one written some other way, does
        /// not match and is refused. There is no shape here worth recovering from - if the game grows
        /// a StatRange this does not understand, writing a two-member one over it is a data loss.
        /// </summary>
        private static readonly Regex StatRangeRx = new(
            @"^(?<pre>\(\s*X\s*=\s*)(?<x>[^,()]*?)(?<mid>\s*,\s*Y\s*=\s*)(?<y>[^,()]*?)(?<post>\s*\))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Applies <paramref name="edits"/> to the shipped row, keyed by stat name.
        /// Returns the replacement text; identical to <paramref name="shipped"/> when nothing changed.
        /// </summary>
        public static string Write(DifficultyCategoryLevel shipped, IEnumerable<EnemyStatEdit> edits)
        {
            if (shipped == null) throw new ArgumentNullException(nameof(shipped));
            if (edits == null) throw new ArgumentNullException(nameof(edits));

            var text = shipped.RawValue;
            if (string.IsNullOrEmpty(text))
                throw new FormatException(@"This category has no shipped text, so there is nothing to replace.");

            foreach (var edit in edits)
            {
                var stat = shipped.Stats.FirstOrDefault(s =>
                    s.StatName.Equals(edit.StatName, StringComparison.OrdinalIgnoreCase));

                // Not synthesised. Thirteen (category, stat, level) triples are genuinely absent in
                // LMP and they are all KaiLeng; adding one BioWare left out is an untested change that
                // the user did not ask for and the editor does not offer.
                if (stat == null)
                    throw new FormatException($@"'{edit.StatName}' is not a stat this category defines at level {edit.Level}.");

                text = ReplaceOnce(text, stat.RawValue, WriteStat(stat, edit.X, edit.Y),
                                   $@"{edit.Category}.{edit.StatName}");
            }

            return text;
        }

        /// <summary>One stat entry with its two numbers substituted and everything else left alone.</summary>
        public static string WriteStat(DifficultyStat shipped, double x, double y)
        {
            if (shipped == null) throw new ArgumentNullException(nameof(shipped));

            var fields = ConfigStructReader.ReadStruct(shipped.RawValue);
            var range = fields.GetValueOrDefault(@"StatRange");
            if (string.IsNullOrWhiteSpace(range))
                throw new FormatException($@"'{shipped.StatName}' has no StatRange to edit.");

            var match = StatRangeRx.Match(range);
            if (!match.Success)
                throw new FormatException($@"'{shipped.StatName}' has a StatRange this build does not recognise: {range}");

            var replacement = match.Groups[@"pre"].Value
                              + Number(x, match.Groups[@"x"].Value)
                              + match.Groups[@"mid"].Value
                              + Number(y, match.Groups[@"y"].Value)
                              + match.Groups[@"post"].Value;

            return ReplaceOnce(shipped.RawValue, range, replacement, shipped.StatName);
        }

        /// <summary>
        /// Renders a number the way its neighbours are written: always with a decimal point, and
        /// keeping the trailing `f` where the shipped value had one. Nothing depends on either - the
        /// value is new text whatever happens - but a row where one number is spelled differently from
        /// the forty around it reads as a mistake.
        /// </summary>
        internal static string Number(double value, string original)
        {
            var suffix = original != null && original.Length > 0 && (original[^1] == 'f' || original[^1] == 'F')
                ? original[^1].ToString()
                : string.Empty;

            return value.ToString(@"0.0###########", CultureInfo.InvariantCulture) + suffix;
        }

        private static string ReplaceOnce(string haystack, string needle, string replacement, string what)
        {
            var first = haystack.IndexOf(needle, StringComparison.Ordinal);
            if (first < 0)
                throw new FormatException($@"The shipped text for '{what}' is not part of its row; the install may have changed under the project.");

            if (haystack.IndexOf(needle, first + 1, StringComparison.Ordinal) >= 0)
                throw new FormatException($@"The shipped text for '{what}' appears more than once in its row, so which one to edit is ambiguous.");

            return haystack[..first] + replacement + haystack[(first + needle.Length)..];
        }
    }

    /// <summary>How much a problem matters. Errors block a compile; warnings are for the user to judge.</summary>
    public enum EnemyProblemSeverity
    {
        Warning,
        Error,
    }

    public record EnemyProblem(EnemyProblemSeverity Severity, string Where, string Message)
    {
        public override string ToString() => $@"{Severity}: {Where} - {Message}";
    }

    /// <summary>
    /// Checks edited stats against the installed game.
    ///
    /// The editor's own UI keeps most of these from arising, but a project file can have been written
    /// against a different install - a handler a mod supplied and the user then removed, a category
    /// Milan renamed, a stat that moved. None of them throw when compiled; they produce a config the
    /// game reads happily and an enemy that behaves the way it always did.
    /// </summary>
    public static class EnemyEditValidator
    {
        public static List<EnemyProblem> Validate(EnemyEditDocument document, DifficultyCatalogue difficulty)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));

            var problems = new List<EnemyProblem>();

            foreach (var edit in document.Stats)
            {
                var where = $@"{edit.Category}.{edit.StatName} level {edit.Level}";
                void Error(string m) => problems.Add(new EnemyProblem(EnemyProblemSeverity.Error, where, m));
                void Warn(string m) => problems.Add(new EnemyProblem(EnemyProblemSeverity.Warning, where, m));

                var table = difficulty.Table(edit.Section);
                if (table == null)
                {
                    Error(@"this difficulty handler is not in the installed game; the edit would have nowhere to go");
                    continue;
                }

                if (!table.Levels.Contains(edit.Level))
                {
                    Error($@"the installed game's handler has no level {edit.Level}");
                    continue;
                }

                var category = table.Categories.FirstOrDefault(c =>
                    c.Name.Equals(edit.Category, StringComparison.OrdinalIgnoreCase));
                if (category == null)
                {
                    Error(@"this enemy is not in the installed game");
                    continue;
                }

                var row = category.Levels.FirstOrDefault(l => l.Level == edit.Level);
                var stat = row?.Stats.FirstOrDefault(s => s.StatName.Equals(edit.StatName, StringComparison.OrdinalIgnoreCase));
                if (stat == null)
                {
                    // Absent rather than zero. The game defines this enemy at this level and simply
                    // does not give it this stat, and writing one in is an untested change.
                    Error(@"the installed game does not define this stat for this enemy at this level");
                    continue;
                }

                if (double.IsNaN(edit.X) || double.IsInfinity(edit.X) || double.IsNaN(edit.Y) || double.IsInfinity(edit.Y))
                {
                    Error(@"a value that is not a finite number cannot be written into config");
                    continue;
                }

                // Reported rather than blocked, and only for the stats whose shipped values agree on a
                // direction. A negative health is nonsense; a negative damage scale is how the game
                // itself writes Bronze.
                if (stat.X >= 0 && stat.Y >= 0 && (edit.X < 0 || edit.Y < 0))
                    Warn(@"the shipped values are both positive and this one is negative; check the game reads it that way");
            }

            return problems;
        }
    }
}
