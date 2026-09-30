using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>One value the user changed on an objective, addressed by the section that owns it.</summary>
    public class ObjectiveEdit
    {
        /// <summary>
        /// The config section this value belongs to - an instance key like
        /// "sfxengagement_disarm_0 sfxengagement_disarm", or a class section like
        /// "sfxgamempcontent.sfxengagement_disarm".
        ///
        /// Carried rather than derived from the objective's name, because the two scopes are
        /// different sections holding different properties and only the catalogue knows which is
        /// which. Deriving it would be guessing at the one thing that decides whether the compiled
        /// mod does anything at all.
        /// </summary>
        [JsonProperty(@"section")]
        public string Section { get; set; }

        [JsonProperty(@"property")]
        public string Property { get; set; }

        /// <summary>The new value, as config text. Rendered by the page, checked here.</summary>
        [JsonProperty(@"value")]
        public string Value { get; set; }
    }

    /// <summary>A change to which waves carry an objective.</summary>
    public class ObjectiveWaveEdit
    {
        /// <summary>The wave a player counts, one-based. Rebased back on compile.</summary>
        [JsonProperty(@"waveIndex")]
        public int WaveIndex { get; set; }

        /// <summary>False to take the objective off this wave entirely.</summary>
        [JsonProperty(@"enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Credit multiplier, or null to leave the wave without one as the extraction is.</summary>
        [JsonProperty(@"creditScale")]
        public double? CreditScale { get; set; }
    }

    /// <summary>
    /// The user's objective edits. Sparse: an objective not in here is not "unchanged", it is absent,
    /// and the compiler leaves the shipped config alone.
    /// </summary>
    public class ObjectiveEditDocument
    {
        public const string CollectionId = @"objectives";

        [JsonProperty(@"format")]
        public int Format { get; set; } = 1;

        /// <summary>Value changes, keyed "&lt;section&gt;|&lt;property&gt;" lowercased.</summary>
        [JsonProperty(@"values")]
        public Dictionary<string, ObjectiveEdit> Values { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Wave changes, keyed by the one-based wave index.</summary>
        [JsonProperty(@"waves")]
        public Dictionary<string, ObjectiveWaveEdit> Waves { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int Count => Values.Count + Waves.Count;

        public static string Key(string section, string property) =>
            $@"{section}|{property}".ToLowerInvariant();

        public static ObjectiveEditDocument FromJson(JObject document) =>
            document == null
                ? new ObjectiveEditDocument()
                : document.ToObject<ObjectiveEditDocument>() ?? new ObjectiveEditDocument();

        public string ToJsonString(bool indented = true) =>
            JObject.FromObject(this).ToString(indented ? Formatting.Indented : Formatting.None);
    }

    public enum ObjectiveProblemSeverity { Warning, Error }

    public class ObjectiveProblem
    {
        public ObjectiveProblemSeverity Severity { get; init; }
        public string Where { get; init; }
        public string Message { get; init; }
        public override string ToString() => $@"{Severity}: {Where}: {Message}";
    }

    /// <summary>
    /// Objective edits checked against the installed game.
    ///
    /// None of these throw when compiled. They produce an objective the game never rolls, or a wave
    /// that asks for something the engine has no rules for - failures that look like the mod simply
    /// not working. So the moment to catch one is while the user still remembers making it.
    /// </summary>
    public static class ObjectiveEditValidator
    {
        public static List<ObjectiveProblem> Validate(ObjectiveEditDocument edits, ObjectiveCatalogue objectives)
        {
            var problems = new List<ObjectiveProblem>();
            if (edits == null || objectives == null) return problems;

            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var family in objectives.Families)
            {
                if (family.ClassSection != null) sections.Add(family.ClassSection);
                foreach (var variant in family.Variants) sections.Add(variant.SectionKey);
            }

            foreach (var (key, edit) in edits.Values)
            {
                var where = edit.Section ?? key;

                if (string.IsNullOrWhiteSpace(edit.Section) || string.IsNullOrWhiteSpace(edit.Property))
                {
                    problems.Add(Error(where, @"names no section or no property, so there is nowhere to write it"));
                    continue;
                }

                if (!sections.Contains(edit.Section))
                {
                    problems.Add(Error(where,
                        $@"is not an objective section in this install; it may have been renamed since the edit was made"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(edit.Value))
                    problems.Add(Error(where, $@"{edit.Property} has no value"));
            }

            // Every weight at zero is a match with no objective on any objective wave. It compiles,
            // installs, and produces waves that announce an objective and then have none.
            var weights = EffectiveWeights(edits, objectives);
            if (weights.Count > 0 && weights.Values.All(w => w <= 0))
            {
                problems.Add(Error(@"objectives",
                    @"every objective's selection weight is zero, so the game has nothing to choose - " +
                    @"the objective waves will have no objective"));
            }

            foreach (var (index, wave) in edits.Waves)
            {
                if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    problems.Add(Error(@"waves", $@"'{index}' is not a wave number"));
                    continue;
                }

                if (parsed < 1)
                {
                    problems.Add(Error($@"wave {parsed}", @"is not a wave a player can reach"));
                    continue;
                }

                if (wave.CreditScale is { } scale && scale < 0)
                    problems.Add(Error($@"wave {parsed}", @"has a negative credit scale"));

                // A warning rather than an error, deliberately. NumWaves is itself an editable config
                // value, so a project that lengthens the match and then puts an objective on the new
                // last wave is doing something coherent - and refusing it would make that
                // combination impossible to author in one project.
                if (wave.Enabled && objectives.NumWaves is { } cap && parsed > cap)
                {
                    problems.Add(Warning($@"wave {parsed}",
                        $@"is past {ObjectiveCatalogue.NumWavesProperty} ({cap}), so a match only reaches it " +
                        @"if the match length is raised too"));
                }

                // Not an error: a wave the config does not currently name is exactly what "add an
                // objective wave" means, and refusing it would make the feature unreachable.
                if (wave.Enabled && objectives.Waves.All(w => w.WaveIndex != parsed))
                    problems.Add(Warning($@"wave {parsed}", @"does not carry an objective as shipped; this adds one"));
            }

            return problems;
        }

        /// <summary>
        /// The weights as they would be after the edits - which is what the check above has to look
        /// at. Reading the shipped weights would let a mod that zeroes every one of them through.
        /// </summary>
        private static Dictionary<string, double> EffectiveWeights(ObjectiveEditDocument edits, ObjectiveCatalogue objectives)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var family in objectives.Families)
            {
                foreach (var variant in family.Variants)
                {
                    // The extraction is never rolled for and never should be; counting it would make
                    // "every weight is zero" impossible to detect.
                    if (variant.WeightScope == null && variant.EffectiveWeight == null) continue;

                    var instanceKey = ObjectiveEditDocument.Key(variant.SectionKey, ObjectiveCatalogue.WeightProperty);
                    var familyKey = family.ClassSection == null
                        ? null
                        : ObjectiveEditDocument.Key(family.ClassSection, ObjectiveCatalogue.WeightProperty);

                    double? weight = variant.EffectiveWeight;

                    if (familyKey != null && edits.Values.TryGetValue(familyKey, out var familyEdit)
                        && variant.WeightScope == @"family" && Parse(familyEdit.Value) is { } fw)
                        weight = fw;

                    if (edits.Values.TryGetValue(instanceKey, out var instanceEdit) && Parse(instanceEdit.Value) is { } iw)
                        weight = iw;

                    if (weight is { } value) result[variant.SectionKey] = value;
                }
            }

            // The extraction point is not part of the roll. Excluded here rather than in the loop so
            // the reason is in one place: it is the wave-11 objective, always, and its weight of zero
            // is correct rather than a mod that has switched everything off.
            foreach (var variant in objectives.Variants.Where(v =>
                         string.Equals(v.ObjectiveType, @"ExtractionPoint", StringComparison.OrdinalIgnoreCase)))
                result.Remove(variant.SectionKey);

            return result;
        }

        private static double? Parse(string text)
        {
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            var n = ValueTreeBuilder.NumericPrefixLength(t);
            if (n == 0) return null;
            return double.TryParse(t.AsSpan(0, n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static ObjectiveProblem Error(string where, string message) =>
            new() { Severity = ObjectiveProblemSeverity.Error, Where = where, Message = message };

        private static ObjectiveProblem Warning(string where, string message) =>
            new() { Severity = ObjectiveProblemSeverity.Warning, Where = where, Message = message };
    }
}
