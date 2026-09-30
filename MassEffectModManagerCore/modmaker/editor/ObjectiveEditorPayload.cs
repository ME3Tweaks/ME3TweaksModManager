using System;
using System.Collections.Generic;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The objectives editor's data, shaped for the page rather than for the game.
    ///
    /// Three surfaces, because the config genuinely has three and collapsing them would misrepresent
    /// what an edit does:
    ///
    ///  - **waves** — which waves carry an objective, and what finishing one is worth.
    ///  - **objectives** — the variants the game rolls between, each with its weight and the share of
    ///    the roll that weight buys it.
    ///  - **rules** — the knobs, split by the scope that owns them: what one variant sets, and what a
    ///    whole family shares. Those are different sections, and writing to the wrong one produces a
    ///    mod that installs and does nothing.
    ///
    /// The share is computed here rather than stored, because it is a property of the SET: changing
    /// one weight changes every other objective's odds. A page that stored it would show a percentage
    /// next to a weight it no longer matches.
    /// </summary>
    public static class ObjectiveEditorPayload
    {
        /// <summary>
        /// Rule properties worth surfacing as controls, with the words for them.
        ///
        /// A list rather than "every property", because these sections also hold a dozen TLK string
        /// refs each - the begin-wave message, the failure hint, the progress ticker - and those are
        /// text, not rules. They are still carried under `text` so the page can show them, but they
        /// do not belong among the numbers that change how hard an objective is.
        /// </summary>
        private static readonly (string Name, string Label, string Unit, string Help)[] Knobs =
        [
            (@"NumDisarmsRequired", @"Bombs to disarm", null,
                @"How many devices the squad must deal with before the wave is done."),
            (@"NumDropOffsRequired", @"Drop-offs required", null,
                @"How many packages must reach the delivery point."),
            (@"OperationTimeLimit", @"Time limit", @"seconds",
                @"How long the squad has. Zero means the objective is not timed."),
            (@"OperationTimeLimitBuffer", @"Time limit buffer", @"seconds", null),
            (@"OperationScoreReward", @"Score reward", null, null),
            (@"ActivateNextBombDelay", @"Delay between devices", @"seconds", null),
            (@"ActivateObjectiveDelay", @"Delay before it starts", @"seconds", null),
            (@"InstructionReminderDelay", @"Reminder delay", @"seconds", null),
            (@"WaveInstructionVODelay", @"Instruction VO delay", @"seconds", null),
            (@"OperationFirstWarning", @"First warning at", @"seconds", null),
            (@"OperationSecondWarning", @"Second warning at", @"seconds", null),
            (@"SpawnDistanceIdeal", @"Ideal spawn distance", @"uu",
                @"How far from the squad the game tries to place the objective."),
            (@"SpawnPenaltyShortDistance", @"Too-close penalty", null, null),
            (@"SpawnPenaltyLongDistance", @"Too-far penalty", null, null),
            (@"MinDistanceToDropOffZone", @"Minimum drop-off distance", @"uu", null),
            (@"FinishWaveDelay", @"Delay after finishing", @"seconds", null),
        ];

        private static readonly Dictionary<string, (string Label, string Unit, string Help)> KnobText =
            Knobs.ToDictionary(k => k.Name, k => (k.Label, k.Unit, k.Help), StringComparer.OrdinalIgnoreCase);

        public static JObject Build(ObjectiveCatalogue objectives, GameStrings strings = null)
        {
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));

            return new JObject
            {
                [@"format"] = 1,
                [@"coordinatorSection"] = objectives.CoordinatorSection,
                // How many waves the editor may offer. Carried as a number AND as where that number
                // came from, because "11" read from the config and "11" because nothing else was
                // available are different claims and the page says so differently.
                [@"maxWaves"] = objectives.LastWave,
                [@"maxWavesFromConfig"] = objectives.NumWaves != null,
                [@"extractionWave"] = objectives.ExtractionWave,
                [@"waves"] = Waves(objectives),
                [@"families"] = Families(objectives, strings),
                [@"warnings"] = new JArray(objectives.Warnings),
            };
        }

        public static string ToJson(ObjectiveCatalogue objectives, GameStrings strings = null, bool indented = true) =>
            Build(objectives, strings).ToString(indented ? Formatting.Indented : Formatting.None);

        private static JArray Waves(ObjectiveCatalogue objectives)
        {
            var result = new JArray();
            foreach (var wave in objectives.Waves)
            {
                result.Add(new JObject
                {
                    [@"waveIndex"] = wave.WaveIndex,
                    [@"waveNumber"] = wave.WaveNumber,
                    [@"creditScale"] = wave.CreditScale,
                    [@"raw"] = wave.Raw,
                    // Shown, not hidden: two of these waves really are named twice, with DIFFERENT
                    // credit scales, and an editor that quietly showed one would be hiding the thing
                    // an author most needs to know before touching them.
                    [@"shadowed"] = wave.Shadowed,
                });
            }
            return result;
        }

        private static JArray Families(ObjectiveCatalogue objectives, GameStrings strings)
        {
            var result = new JArray();
            foreach (var family in objectives.Families)
            {
                result.Add(new JObject
                {
                    [@"classSection"] = family.ClassSection,
                    [@"className"] = family.ClassName,
                    [@"title"] = Pretty(family.ClassName),
                    [@"shared"] = Rules(family.Shared, strings),
                    [@"variants"] = new JArray(family.Variants.Select(v => Variant(v, objectives, strings))),
                });
            }
            return result;
        }

        private static JObject Variant(ObjectiveVariant variant, ObjectiveCatalogue objectives, GameStrings strings)
        {
            var entry = new JObject
            {
                [@"sectionKey"] = variant.SectionKey,
                [@"instanceId"] = variant.InstanceId,
                [@"className"] = variant.ClassName,
                [@"type"] = variant.ObjectiveType,
                [@"title"] = Pretty(variant.ObjectiveType ?? variant.InstanceId),
                [@"weight"] = variant.EffectiveWeight,
                // Which SECTION the weight comes from. The page needs it because editing a family
                // weight changes every variant under it, and the user should be told that before
                // they do it rather than after.
                [@"weightScope"] = variant.WeightScope,
                [@"weightOnInstance"] = variant.SelectionWeight,
                [@"dormant"] = variant.IsDormant,
                [@"share"] = objectives.ShareOf(variant),
                [@"rules"] = Rules(variant.Values, strings),
                [@"requirements"] = new JArray(variant.Requirements.Select(r => new JObject
                {
                    [@"type"] = r.ObjectiveType,
                    [@"minimum"] = r.Minimum,
                    [@"maximum"] = r.Maximum,
                    [@"raw"] = r.Raw,
                })),
            };

            return entry;
        }

        /// <summary>
        /// One scope's values, split into the numbers that are rules and the refs that are words.
        ///
        /// Both are carried. The rules are what the editor offers controls for; the text is shown
        /// because "srWaveFailedHint = 724698" is meaningless and the line it names is not, and
        /// because an author changing what an objective DOES usually wants to change what it SAYS.
        /// </summary>
        private static JObject Rules(Dictionary<string, string> values, GameStrings strings)
        {
            var knobs = new JArray();
            var text = new JArray();
            var other = new JArray();

            foreach (var (name, value) in values.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Handled as structure elsewhere, not as a loose value.
                if (name.Equals(ObjectiveCatalogue.RequirementsProperty, StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals(ObjectiveCatalogue.WeightProperty, StringComparison.OrdinalIgnoreCase)) continue;

                if (KnobText.TryGetValue(name, out var words))
                {
                    var knob = new JObject
                    {
                        [@"name"] = name,
                        [@"label"] = words.Label,
                        [@"value"] = value,
                    };
                    if (words.Unit != null) knob[@"unit"] = words.Unit;
                    if (words.Help != null) knob[@"help"] = words.Help;
                    knobs.Add(knob);
                    continue;
                }

                // A string ref by shape, not by name: "sr" happens to prefix every one of them in
                // this surface, but what makes it text is that the value resolves to a line.
                if (strings?.Resolve(value) is { } line)
                {
                    text.Add(new JObject
                    {
                        [@"name"] = name,
                        [@"label"] = Pretty(name),
                        [@"value"] = value,
                        [@"text"] = line,
                    });
                    continue;
                }

                other.Add(new JObject { [@"name"] = name, [@"label"] = Pretty(name), [@"value"] = value });
            }

            return new JObject { [@"knobs"] = knobs, [@"text"] = text, [@"other"] = other };
        }

        /// <summary>
        /// A config identifier as words: SFXEngagement_MobileAnnex to "Mobile Annex",
        /// AnnexZoneHack to "Annex Zone Hack", srWaveFailedHint to "Wave Failed Hint".
        /// </summary>
        private static string Pretty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var text = name;
            if (text.StartsWith(@"SFXEngagement_", StringComparison.OrdinalIgnoreCase)) text = text[14..];
            if (text.StartsWith(@"sr", StringComparison.Ordinal) && text.Length > 2 && char.IsUpper(text[2])) text = text[2..];
            text = text.Replace('_', ' ');

            // Split camel case, leaving runs of capitals alone so "DLC" and "VO" survive.
            var spaced = new System.Text.StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && char.IsUpper(text[i]) && (char.IsLower(text[i - 1])
                        || (i + 1 < text.Length && char.IsLower(text[i + 1]) && char.IsUpper(text[i - 1]))))
                    spaced.Append(' ');
                spaced.Append(text[i]);
            }

            return spaced.ToString().Replace(@"  ", @" ").Trim();
        }
    }
}
