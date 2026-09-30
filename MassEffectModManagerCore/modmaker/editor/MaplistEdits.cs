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
    /// A map turned on or off in the lobby's picker.
    ///
    /// A separate kind of edit from <see cref="MapEntryEdit"/> because it writes a different property:
    /// `AvailableMaps` is a plain list of package names taking Add and Remove, while a map's
    /// presentation lives in a `MasterMapList` struct. The two disagree in the shipped game in both
    /// directions, so they cannot be one edit.
    /// </summary>
    public class MapAvailabilityEdit
    {
        /// <summary>The level package, which is the identity AvailableMaps uses.</summary>
        [JsonProperty(@"package")]
        public string Package { get; set; }

        [JsonProperty(@"available")]
        public bool Available { get; set; }

        [JsonIgnore]
        public string Key => Package?.ToLowerInvariant();
    }

    /// <summary>
    /// Which of the lobby's match-settings lists an option belongs to.
    ///
    /// The two are the same operation on different properties, which is why they share a kind of
    /// edit - but they do NOT share a collection. A faction turned off takes its whole wavelist out
    /// of the game with it, so the faction toggles live in the wavelist collection, beside the waves
    /// they silence. The difficulties stay here, with the maps, where the budget table is.
    /// </summary>
    public static class MatchPickers
    {
        /// <summary>
        /// The enemy picker. Config property: `EnemyTypes`.
        ///
        /// Edits live in <see cref="WavelistEditDocument.Pickers"/>. A project written before the
        /// move carries them here instead, and <see cref="ModMakerProject"/> migrates them on load.
        /// </summary>
        public const string Factions = @"factions";

        /// <summary>The difficulty picker. Config property: `ChallengeTypes`. Edits live here.</summary>
        public const string Difficulties = @"difficulties";

        public static readonly string[] All = { Factions, Difficulties };

        public static bool IsKnown(string picker) =>
            All.Contains(picker, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One of the lobby's match-settings choices, turned on or off.
    ///
    /// Factions and difficulties are the same shape and so are one kind of edit. Neither has an
    /// availability flag: being in `EnemyTypes` or `ChallengeTypes` IS being offered, so turning one
    /// off means subtracting its entry from that list rather than clearing something. That makes this
    /// a list operation like <see cref="MapAvailabilityEdit"/> and not a field change like
    /// <see cref="MapEntryEdit"/>, which is why it is its own kind and not a property on one.
    ///
    /// Keyed by picker and id together, because the ids restart per list - faction 1 and difficulty 1
    /// are both real and unrelated.
    /// </summary>
    public class PickerOptionEdit
    {
        /// <summary>One of <see cref="MatchPickers"/>. ModMaker's word, not the config's.</summary>
        [JsonProperty(@"picker")]
        public string Picker { get; set; }

        [JsonProperty(@"id")]
        public int? Id { get; set; }

        /// <summary>
        /// Something recognisable about the option, for reporting a drift rather than for finding it.
        ///
        /// A faction's wave class, a difficulty's name. The id is the identity; this is what lets a
        /// project written against one install say the ids no longer mean the same thing in another.
        /// </summary>
        [JsonProperty(@"label", NullValueHandling = NullValueHandling.Ignore)]
        public string Label { get; set; }

        [JsonProperty(@"enabled")]
        public bool Enabled { get; set; }

        [JsonIgnore]
        public string Key => $@"{Picker?.ToLowerInvariant()}/{Id?.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// One `MasterMapList` entry's presentation, as the user set it.
    ///
    /// Only the fields this build can edit safely. `PrettyName` and `Description` are TLK string refs
    /// and changing them needs the localisation overlay rather than a config write, so they are shown
    /// and not editable; adding a map needs a level package to point at and is out of scope.
    /// </summary>
    public class MapEntryEdit
    {
        /// <summary>The entry's stable numeric id. DLC re-uses an id to REPLACE a map.</summary>
        [JsonProperty(@"id")]
        public int? Id { get; set; }

        /// <summary>
        /// The package, carried as well as the id because it is what a person recognises.
        ///
        /// The id is the identity - a DLC replacing a map keeps the id and changes the package - so
        /// this is not the key, and a mismatch between the two is worth reporting rather than picking
        /// a winner.
        /// </summary>
        [JsonProperty(@"package")]
        public string Package { get; set; }

        [JsonProperty(@"gawX", NullValueHandling = NullValueHandling.Ignore)]
        public double? GalaxyAtWarX { get; set; }

        [JsonProperty(@"gawY", NullValueHandling = NullValueHandling.Ignore)]
        public double? GalaxyAtWarY { get; set; }

        /// <summary>A Wwise event name. A closed vocabulary taken from the shipped values, never free text.</summary>
        [JsonProperty(@"music", NullValueHandling = NullValueHandling.Ignore)]
        public string MusicEventName { get; set; }

        [JsonIgnore]
        public string Key => Id?.ToString(CultureInfo.InvariantCulture) ?? Package?.ToLowerInvariant();
    }

    /// <summary>
    /// The maplist editor's payload inside a project file.
    ///
    /// Two sparse lists rather than one, because the two halves of "editing a map" write to two
    /// different config properties. Sparse in the usual way: an entry that is not here is absent
    /// rather than unchanged, and reverting removes it.
    /// </summary>
    public class MaplistEditDocument
    {
        public const int CurrentFormat = 1;

        /// <summary>The collection id this document is stored under in a project's `collections`.</summary>
        public const string CollectionId = @"maplist";

        [JsonProperty(@"format")]
        public int Format { get; set; } = CurrentFormat;

        [JsonProperty(@"availability")]
        public List<MapAvailabilityEdit> Availability { get; set; } = new();

        [JsonProperty(@"entries")]
        public List<MapEntryEdit> Entries { get; set; } = new();

        /// <summary>
        /// Match-settings choices turned off in the lobby.
        ///
        /// Difficulties, in a project this build wrote. A project written before the faction toggles
        /// moved to the wavelist collection also carries factions here, and is migrated on load - so
        /// everything that reads this list still reads it by picker rather than assuming.
        /// </summary>
        [JsonProperty(@"pickers")]
        public List<PickerOptionEdit> Pickers { get; set; } = new();

        [JsonIgnore]
        public int Count => Availability.Count + Entries.Count + Pickers.Count;

        public void Set(MapAvailabilityEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Availability.RemoveAll(a => a.Key == edit.Key);
            Availability.Add(edit);
        }

        public void Set(MapEntryEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Entries.RemoveAll(e => e.Key == edit.Key);
            Entries.Add(edit);
        }

        public void Set(PickerOptionEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Pickers.RemoveAll(f => f.Key == edit.Key);
            Pickers.Add(edit);
        }

        public bool RevertPickerOption(string picker, int? id)
        {
            var key = new PickerOptionEdit { Picker = picker, Id = id }.Key;
            return Pickers.RemoveAll(f => f.Key == key) > 0;
        }

        /// <summary>Options turned off in one picker.</summary>
        public IEnumerable<PickerOptionEdit> For(string picker) =>
            Pickers.Where(e => string.Equals(e.Picker, picker, StringComparison.OrdinalIgnoreCase));

        public bool RevertAvailability(string package) =>
            Availability.RemoveAll(a => a.Key == package?.ToLowerInvariant()) > 0;

        public bool RevertEntry(int? id, string package)
        {
            var key = id?.ToString(CultureInfo.InvariantCulture) ?? package?.ToLowerInvariant();
            return Entries.RemoveAll(e => e.Key == key) > 0;
        }

        public JObject ToJson() => JObject.FromObject(this);

        public string ToJsonString(bool indented = true) =>
            ToJson().ToString(indented ? Formatting.Indented : Formatting.None);

        public static MaplistEditDocument FromJson(JObject json)
        {
            if (json == null) return new MaplistEditDocument();

            var format = (int?)json[@"format"] ?? CurrentFormat;
            if (format > CurrentFormat)
                throw new NotSupportedException($@"This maplist collection is format {format}; this build understands {CurrentFormat}. Update Mod Manager.");

            var doc = json.ToObject<MaplistEditDocument>() ?? new MaplistEditDocument();
            doc.Availability ??= new List<MapAvailabilityEdit>();
            doc.Entries ??= new List<MapEntryEdit>();
            doc.Pickers ??= new List<PickerOptionEdit>();
            return doc;
        }
    }

    /// <summary>
    /// Renders an edited `MasterMapList` entry back into the config text the game reads.
    ///
    /// In-place substitution, for the same reason <see cref="CategoryRowWriter"/> uses it: an entry
    /// carries a dozen fields, of which this build edits two, and re-serialising the rest would
    /// rewrite the TLK refs, the image path and the spacing of values nobody touched. A map entry has
    /// the additional wrinkle that its untouched fields include the ones this build deliberately does
    /// not model - so regenerating the entry would not merely reformat them, it would drop them.
    /// </summary>
    public static class MapEntryWriter
    {
        /// <summary>`(X=424,Y=216)`, capturing the separators so a replacement keeps them.</summary>
        private static readonly Regex PositionRx = new(
            @"^(?<pre>\(\s*X\s*=\s*)(?<x>[^,()]*?)(?<mid>\s*,\s*Y\s*=\s*)(?<y>[^,()]*?)(?<post>\s*\))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public const string PositionField = @"GalaxyAtWarMapPosition";
        public const string MusicField = @"MusicEventName";

        /// <summary>
        /// Applies an edit to the shipped entry. Returns the replacement text, identical to the
        /// shipped text when nothing changed.
        /// </summary>
        public static string Write(MapEntry shipped, MapEntryEdit edit)
        {
            if (shipped == null) throw new ArgumentNullException(nameof(shipped));
            if (edit == null) throw new ArgumentNullException(nameof(edit));

            var text = shipped.RawValue;
            if (string.IsNullOrEmpty(text))
                throw new FormatException(@"This map entry has no shipped text, so there is nothing to replace.");

            if (edit.GalaxyAtWarX != null || edit.GalaxyAtWarY != null)
                text = WritePosition(text, edit.GalaxyAtWarX ?? shipped.GalaxyAtWarX, edit.GalaxyAtWarY ?? shipped.GalaxyAtWarY);

            if (edit.MusicEventName != null)
                text = WriteMusic(text, edit.MusicEventName);

            return text;
        }

        private static string WritePosition(string entry, double? x, double? y)
        {
            var current = Field(entry, PositionField)
                          ?? throw new FormatException(
                              $@"This map has no {PositionField}, and adding one is not something this build does.");

            var match = PositionRx.Match(current);
            if (!match.Success)
                throw new FormatException($@"This map's {PositionField} is written in a shape this build does not recognise: {current}");

            var replacement = match.Groups[@"pre"].Value
                              + Number(x ?? 0, match.Groups[@"x"].Value)
                              + match.Groups[@"mid"].Value
                              + Number(y ?? 0, match.Groups[@"y"].Value)
                              + match.Groups[@"post"].Value;

            return ReplaceOnce(entry, current, replacement, PositionField);
        }

        private static string WriteMusic(string entry, string music)
        {
            var current = Field(entry, MusicField)
                          ?? throw new FormatException(
                              $@"This map has no {MusicField}, and adding one is not something this build does.");

            // Quoted the way the shipped value is. The vocabulary is closed and comes from the shipped
            // values, so this is never arbitrary text - but it is still text going into a quoted
            // field, and a value carrying a quote would end the field early.
            if (music.Contains('"'))
                throw new FormatException($@"'{music}' is not a Wwise event name this build will write.");

            return ReplaceOnce(entry, current, $@"""{music}""", MusicField);
        }

        /// <summary>
        /// A top-level field's verbatim text, or null. Read with the config reader rather than by
        /// regex over the whole entry, so a nested `X=` inside another struct cannot be mistaken for
        /// the position's.
        /// </summary>
        private static string Field(string entry, string name) =>
            ConfigStructReader.TryReadStructRaw(entry, out var fields)
                ? fields.GetValueOrDefault(name)
                : null;

        /// <summary>
        /// Galaxy at War positions are whole numbers in every shipped entry, so a fractional one is
        /// written only if the user asked for one. Nothing depends on the spelling; a map's
        /// coordinates reading `424` beside its neighbours' `216` is just easier to diff.
        /// </summary>
        internal static string Number(double value, string original)
        {
            var suffix = original is { Length: > 0 } && (original[^1] == 'f' || original[^1] == 'F')
                ? original[^1].ToString()
                : string.Empty;

            var body = Math.Abs(value - Math.Round(value)) < 1e-9
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString(@"0.0###########", CultureInfo.InvariantCulture);

            return body + suffix;
        }

        private static string ReplaceOnce(string haystack, string needle, string replacement, string what)
        {
            var first = haystack.IndexOf(needle, StringComparison.Ordinal);
            if (first < 0)
                throw new FormatException($@"The shipped text for '{what}' is not part of its entry; the install may have changed under the project.");

            if (haystack.IndexOf(needle, first + 1, StringComparison.Ordinal) >= 0)
                throw new FormatException($@"The shipped text for '{what}' appears more than once in its entry, so which one to edit is ambiguous.");

            return haystack[..first] + replacement + haystack[(first + needle.Length)..];
        }
    }

    /// <summary>How much a problem matters. Errors block a compile; warnings are for the user to judge.</summary>
    public enum MaplistProblemSeverity
    {
        Warning,
        Error,
    }

    public record MaplistProblem(MaplistProblemSeverity Severity, string Where, string Message)
    {
        public override string ToString() => $@"{Severity}: {Where} - {Message}";
    }

    /// <summary>
    /// Checks maplist edits against the installed game.
    ///
    /// The interesting checks here are not "does this map exist" but the two that follow from maps
    /// being three lists rather than one: making a map available does nothing for the player unless
    /// they can also see what they are picking, and turning every map off leaves a lobby that cannot
    /// start a match.
    /// </summary>
    public static class MaplistEditValidator
    {
        public static List<MaplistProblem> Validate(MaplistEditDocument document, MapListCatalogue maps)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (maps == null) throw new ArgumentNullException(nameof(maps));

            var problems = new List<MaplistProblem>();
            void Error(string where, string m) => problems.Add(new MaplistProblem(MaplistProblemSeverity.Error, where, m));
            void Warn(string where, string m) => problems.Add(new MaplistProblem(MaplistProblemSeverity.Warning, where, m));

            ValidateAvailability(document, maps, Error, Warn);
            ValidateEntries(document, maps, Error, Warn);
            ValidateFactions(document, maps, Error, Warn);

            return problems;
        }

        /// <summary>
        /// Match-settings toggles, against the install's own pickers.
        ///
        /// The same two failures as maps, and one of its own: a choice is identified by a number, and
        /// a project written against one install can meet another where that number is a different
        /// thing. The label is what catches it, so it is compared where both have one.
        /// </summary>
        private static void ValidateFactions(MaplistEditDocument document, MapListCatalogue maps,
                                             Action<string, string> error, Action<string, string> warn)
        {
            // Whatever this document HOLDS, rather than what it is supposed to hold. The faction
            // toggles moved to the wavelist collection and are migrated out of here on load; a
            // straggler from somewhere else is still worth checking rather than passing silently.
            foreach (var picker in MatchPickers.All)
                ValidatePickerEdits(document.For(picker).ToList(), maps, picker, error, warn);

            foreach (var edit in document.Pickers.Where(e => !MatchPickers.IsKnown(e.Picker)))
                error(edit.Picker ?? @"(no picker)",
                      $@"'{edit.Picker}' is not a match-settings list this build knows how to write");
        }

        /// <summary>
        /// The faction toggles, which live in the wavelist collection but are written from here.
        ///
        /// Checked against the lobby's own lists, because that is what a faction toggle acts on - the
        /// wave catalogue knows which wavelists exist, not which of them the lobby offers. So the
        /// check belongs with the maps even though the edit is stored and made elsewhere.
        /// </summary>
        public static List<MaplistProblem> ValidateFactionToggles(IReadOnlyCollection<PickerOptionEdit> pickers,
                                                                  MapListCatalogue maps)
        {
            if (pickers == null) throw new ArgumentNullException(nameof(pickers));
            if (maps == null) throw new ArgumentNullException(nameof(maps));

            var problems = new List<MaplistProblem>();
            void Error(string where, string m) => problems.Add(new MaplistProblem(MaplistProblemSeverity.Error, where, m));
            void Warn(string where, string m) => problems.Add(new MaplistProblem(MaplistProblemSeverity.Warning, where, m));

            // Only one picker belongs in that collection. Anything else is refused rather than
            // dropped: a project from a newer ModMaker must not half-compile.
            foreach (var edit in pickers.Where(
                         e => !string.Equals(e.Picker, MatchPickers.Factions, StringComparison.OrdinalIgnoreCase)))
                Error(edit.Picker ?? @"(no picker)",
                      $@"'{edit.Picker}' is not a match-settings list the wavelist editor writes");

            ValidatePickerEdits(
                pickers.Where(e => string.Equals(e.Picker, MatchPickers.Factions, StringComparison.OrdinalIgnoreCase)).ToList(),
                maps, MatchPickers.Factions, Error, Warn);

            return problems;
        }

        /// <summary>
        /// One picker's toggles, against the install's own list.
        ///
        /// Takes the edits rather than a document because the two pickers no longer live in the same
        /// one: the factions are in the wavelist collection and the difficulties are here, and both
        /// are checked the same way against the same catalogue.
        /// </summary>
        internal static void ValidatePickerEdits(IReadOnlyCollection<PickerOptionEdit> edits, MapListCatalogue maps,
                                                 string picker, Action<string, string> error, Action<string, string> warn)
        {
            if (edits.Count == 0) return;

            var offered = OptionsOf(maps, picker);
            var noun = picker == MatchPickers.Factions ? @"faction" : @"difficulty";

            foreach (var edit in edits)
            {
                var where = edit.Label ?? (edit.Id != null ? $@"{noun} {edit.Id}" : $@"(no {noun})");

                if (edit.Id == null)
                {
                    error(where, $@"a {noun} edit with no id has nothing to turn on or off");
                    continue;
                }

                var shipped = offered.FirstOrDefault(o => o.Id == edit.Id);
                if (shipped == null)
                {
                    // Enabling something the install does not offer cannot be done: there is no entry
                    // to put back, and inventing one would be writing a choice rather than restoring
                    // it. Turning off something already absent is simply nothing to do.
                    if (edit.Enabled)
                        error(where, $@"the installed game has no {noun} {edit.Id}, so there is no entry to put back");
                    else
                        warn(where, $@"the installed game has no {noun} {edit.Id}, so nothing will be written");
                    continue;
                }

                if (edit.Enabled)
                {
                    warn(where, $@"this {noun} is already offered in the installed game, so nothing will be written");
                    continue;
                }
            }

            // The picker is a list the lobby chooses from. Emptying it leaves a match settings screen
            // that cannot be got past, which nothing in the game explains.
            var off = edits.Where(f => !f.Enabled && f.Id != null)
                           .Select(f => f.Id.Value).ToHashSet();
            if (off.Count == 0) return;

            var remaining = offered.Count(o => o.Id == null || !off.Contains(o.Id.Value));
            if (remaining == 0)
                error(picker, $@"every {noun} is turned off; the lobby would have nothing to pick and no match could start");
            else if (remaining == 1)
                warn(picker, $@"only one {noun} is left, so every match will use it");
        }

        /// <summary>
        /// The options one picker offers.
        ///
        /// Factions are filtered to those that name a wave class: the enemy list also carries the
        /// surprise-me choice, which names none, and that is the absence of a faction rather than one
        /// that could be turned off. Difficulties have no such passenger.
        /// </summary>
        internal static List<MatchSettingsOption> OptionsOf(MapListCatalogue maps, string picker) =>
            string.Equals(picker, MatchPickers.Factions, StringComparison.OrdinalIgnoreCase)
                ? maps.EnemyTypes.Where(o => !string.IsNullOrWhiteSpace(o.Field(@"WaveClass"))).ToList()
                : string.Equals(picker, MatchPickers.Difficulties, StringComparison.OrdinalIgnoreCase)
                    ? maps.ChallengeTypes.ToList()
                    : new List<MatchSettingsOption>();

        private static void ValidateAvailability(MaplistEditDocument document, MapListCatalogue maps,
                                                 Action<string, string> error, Action<string, string> warn)
        {
            foreach (var edit in document.Availability)
            {
                var where = edit.Package ?? @"(no package)";

                if (string.IsNullOrWhiteSpace(edit.Package))
                {
                    error(where, @"an availability edit with no package name has nothing to turn on or off");
                    continue;
                }

                var already = maps.AvailableMaps.Contains(edit.Package, StringComparer.OrdinalIgnoreCase);
                if (already == edit.Available)
                {
                    // Not an error - the install may simply have moved under the project - but the
                    // compiler will write nothing for it, and silence there looks like a failure.
                    warn(where, edit.Available
                        ? @"this map is already available in the installed game, so nothing will be written"
                        : @"this map is already unavailable in the installed game, so nothing will be written");
                }

                var entry = maps.Maps.FirstOrDefault(m =>
                    string.Equals(m.PackageName, edit.Package, StringComparison.OrdinalIgnoreCase));

                if (entry == null && edit.Available)
                {
                    // Real and playable, but the lobby has no name or picture for it. 24 of the
                    // shipped available packages are in exactly this state, so it is a caution rather
                    // than a mistake.
                    warn(where, @"this package has no MasterMapList entry, so the lobby shows it with no name and no banner");
                }
            }

            // Availability is a list the game picks from. Emptying it is a mod that installs and then
            // cannot start a match, which no error message in the game will explain.
            var turnedOff = document.Availability.Where(a => !a.Available)
                                    .Select(a => a.Package)
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var turnedOn = document.Availability.Where(a => a.Available)
                                   .Select(a => a.Package)
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (turnedOff.Count == 0) return;

            var remaining = maps.AvailableMaps.Count(m => !turnedOff.Contains(m)) + turnedOn.Count;
            if (remaining == 0)
                error(@"availability", @"every map is turned off; the lobby would have nothing to pick and no match could start");
            else if (remaining == 1)
                warn(@"availability", @"only one map is left available, so every match will be on it");
        }

        private static void ValidateEntries(MaplistEditDocument document, MapListCatalogue maps,
                                            Action<string, string> error, Action<string, string> warn)
        {
            var music = maps.Maps.Select(m => m.MusicEventName)
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var edit in document.Entries)
            {
                var where = edit.Package ?? $@"map {edit.Id}";

                var entry = Find(maps, edit);
                if (entry == null)
                {
                    error(where, @"this map is not in the installed game's MasterMapList; the edit would have nowhere to go");
                    continue;
                }

                if (entry.IsPlaceholder)
                {
                    // MasterMapList entry 0 is the "Random" picker choice, not a map. Editing its
                    // Galaxy at War position or music would write to something with no level behind it.
                    error(where, @"this entry is the lobby's Random choice rather than a map, and is not editable");
                    continue;
                }

                if (edit.Package != null && entry.PackageName != null
                    && !edit.Package.Equals(entry.PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    // A DLC replaces a map by re-using its id, so the id can now belong to a different
                    // package than the project recorded. The edit follows the id, and says so.
                    warn(where, $@"id {edit.Id} is now '{entry.PackageName}' rather than '{edit.Package}'; the edit follows the id");
                }

                if (edit.MusicEventName != null && !music.Contains(edit.MusicEventName))
                    error(where, $@"'{edit.MusicEventName}' is not a music event any shipped map uses");

                foreach (var (value, axis) in new[] { (edit.GalaxyAtWarX, @"X"), (edit.GalaxyAtWarY, @"Y") })
                {
                    if (value is { } v && (double.IsNaN(v) || double.IsInfinity(v)))
                        error(where, $@"a Galaxy at War {axis} that is not a finite number cannot be written into config");
                }
            }
        }

        private static MapEntry Find(MapListCatalogue maps, MapEntryEdit edit) =>
            (edit.Id != null ? maps.Maps.FirstOrDefault(m => m.Id == edit.Id) : null)
            ?? maps.Maps.FirstOrDefault(m => m.PackageName != null
                                          && m.PackageName.Equals(edit.Package, StringComparison.OrdinalIgnoreCase));
    }
}
