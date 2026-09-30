using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>One objective the game can pick, as the config defines it.</summary>
    public class ObjectiveVariant
    {
        /// <summary>Instance section key, "&lt;instance&gt; &lt;classToken&gt;". What a compile writes to.</summary>
        public string SectionKey { get; init; }

        /// <summary>The instance half of the key, e.g. "sfxengagement_disarm_0".</summary>
        public string InstanceId { get; init; }

        /// <summary>Coalesced section of the engagement class this instance belongs to.</summary>
        public string ClassSection { get; init; }

        /// <summary>Bare class name, e.g. SFXEngagement_Disarm.</summary>
        public string ClassName { get; init; }

        /// <summary>
        /// The objective type token from `ObjectivesRequired`, e.g. AnnexZoneHack, DisarmDisable.
        ///
        /// This is the identity a player would recognise: two Annex instances differ by nothing else
        /// that reads as a name - both are "Annex" - and one is the hack and one the upload.
        /// </summary>
        public string ObjectiveType { get; init; }

        /// <summary>
        /// How likely the game is to pick this objective, relative to the others - as set on THIS
        /// instance. Null when the instance does not set it and the family's value applies.
        ///
        /// Zero means never. It is not a disabled flag - it is a weight that happens to be zero - and
        /// the difference matters: Retrieve ships at 0.0 with its rules fully intact, so turning it
        /// back on is one number rather than a feature that has to be rebuilt.
        /// </summary>
        public double? SelectionWeight { get; internal set; }

        /// <summary>
        /// The weight that actually applies: this instance's, or the family's where it sets none.
        ///
        /// Both scopes are real here. Extraction sets no weight on its instance and 0 on its class
        /// section, so reading the instance alone reports its weight as unknown - when in fact the
        /// game will never roll it, which is exactly right for the wave-11 objective.
        /// </summary>
        public double? EffectiveWeight { get; internal set; }

        /// <summary>Where the applying weight is set. "instance", "family", or null when nothing sets it.</summary>
        public string WeightScope { get; internal set; }

        /// <summary>True when the game will never choose this objective as shipped.</summary>
        public bool IsDormant => EffectiveWeight is { } w && w <= 0;

        /// <summary>
        /// `ObjectivesRequired` rows: how many of the thing must be done, and how many may exist.
        ///
        /// A list, not a single value: Retrieve needs three pickups AND one drop-off, and the DLC
        /// variant names three separate pickup types.
        /// </summary>
        public List<ObjectiveRequirement> Requirements { get; } = new();

        /// <summary>Every value this instance's section sets, verbatim, by property name.</summary>
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The raw text of each `ObjectivesRequired` row, in shipped order.</summary>
        public List<string> RequirementText { get; } = new();
    }

    /// <summary>One `ObjectivesRequired` row.</summary>
    public class ObjectiveRequirement
    {
        public string ObjectiveType { get; init; }
        public int? Minimum { get; init; }
        public int? Maximum { get; init; }
        public string Raw { get; init; }
    }

    /// <summary>
    /// The rules shared by every instance of one engagement class.
    ///
    /// Kept apart from the per-instance values because the config keeps them apart, and exactly:
    /// across the 638 config properties of the per-object classes, 99 are set only in instance
    /// sections and 69 only in class-path sections, with none in both. Merging the two here would
    /// invent a shape the compiler then has to guess its way back out of - see docs/02 §1.6.
    /// </summary>
    public class ObjectiveFamily
    {
        /// <summary>Coalesced section of the class, "package.class". Null when it has none.</summary>
        public string ClassSection { get; init; }

        public string ClassName { get; init; }

        /// <summary>Values set on the class-path section, shared by every instance below.</summary>
        public Dictionary<string, string> Shared { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<ObjectiveVariant> Variants { get; } = new();
    }

    /// <summary>A wave the coordinator gives an objective to.</summary>
    public class ObjectiveWave
    {
        /// <summary>
        /// The wave a PLAYER counts, one-based - so the shipped 2/5/9/10 read as 3/6/10/11.
        ///
        /// Rebased on read, once, for the same reason <see cref="WavelistCatalogue.ObjectiveWaves"/>
        /// is: nothing in the config says the two schemes differ and every raw value is a valid wave
        /// number, so carrying them through unconverted looks entirely correct while being wrong.
        /// </summary>
        public int WaveIndex { get; init; }

        /// <summary>The zero-based number as the config writes it. What a compile has to write back.</summary>
        public int WaveNumber { get; init; }

        /// <summary>Credit multiplier for completing it. Absent on the extraction wave.</summary>
        public double? CreditScale { get; init; }

        /// <summary>The entry's text, verbatim. An edit is a Remove of exactly this plus an Add.</summary>
        public string Raw { get; init; }

        /// <summary>
        /// True when an earlier entry in the array already named this wave.
        ///
        /// The merged array has six entries for four waves. Milan re-adds all four with AddUnique;
        /// waves 5 and 9 carry CHANGED CreditScale values, so they are genuinely new values and both
        /// the old and the new survive, while 2 and 10 are byte-identical and correctly suppressed.
        ///
        /// Which of two entries for one wave the game honours is not established, so both are carried
        /// and the later one marked rather than silently dropped. (Until the AddUnique fix in
        /// LegendaryExplorerCore this was eight entries with four shadowed - two of them ours, not
        /// the game's. See docs/15-config-collapse.md.)
        /// </summary>
        public bool Shadowed { get; init; }
    }

    /// <summary>
    /// The objectives surface: which waves carry one, which objectives can be picked, and the rules
    /// each one runs by.
    ///
    /// Three things live here and they are genuinely different, which is why the editor built on this
    /// has three parts rather than one list:
    ///
    ///  - **`OperationWaves`** on the coordinator - which waves have an objective at all, and what
    ///    finishing one is worth.
    ///  - **Selection weights** - which objectives the game may choose between. Retrieve ships at
    ///    weight 0 with its rules intact, so this is where a dormant objective is switched back on.
    ///  - **The rules per objective** - how many bombs, how long, how far away it spawns. Split by the
    ///    config's own scopes: what one variant sets, and what the whole family shares.
    ///
    /// Deliberately NOT here: `SFXOperation_ObjectiveData`. Its twelve instances are the PROPS - which
    /// mesh represents the objective, where it may stand, what it is called - and that is a separate
    /// surface with its own art and its own text, not a rule. It is worth an editor; it is not this
    /// one, and folding it in would put mesh paths next to bomb counts.
    /// </summary>
    public class ObjectiveCatalogue
    {
        public const string CoordinatorClass = @"SFXWaveCoordinator_HordeOperation";
        public const string EngagementBaseClass = @"SFXEngagement";

        /// <summary>The property naming which waves carry an objective.</summary>
        public const string OperationWavesProperty = @"OperationWaves";

        /// <summary>How many waves a match runs. Read, never assumed - see <see cref="NumWaves"/>.</summary>
        public const string NumWavesProperty = @"NumWaves";

        /// <summary>Whether the last wave is the extraction.</summary>
        public const string ExtractionEnabledProperty = @"bExtractionWaveEnabled";

        /// <summary>Per-instance: how likely this objective is to be picked.</summary>
        public const string WeightProperty = @"WeightedChanceOfSelection";

        /// <summary>Per-instance: what has to be done, and how many of it exist.</summary>
        public const string RequirementsProperty = @"ObjectivesRequired";

        /// <summary>Objective-bearing waves, in play order.</summary>
        public List<ObjectiveWave> Waves { get; } = new();

        /// <summary>Engagement classes with at least one instance, in shipped order.</summary>
        public List<ObjectiveFamily> Families { get; } = new();

        /// <summary>Every variant across every family, which is what a selection roll picks from.</summary>
        public IEnumerable<ObjectiveVariant> Variants => Families.SelectMany(f => f.Variants);

        /// <summary>Coalesced section of the coordinator, for compiling wave edits.</summary>
        public string CoordinatorSection { get; private set; }

        /// <summary>
        /// How many waves a match actually runs, from the coordinator's own `NumWaves`.
        ///
        /// Read rather than assumed. Eleven is what this install ships and what every screenshot of
        /// the game shows, which is exactly what makes hardcoding it dangerous: a mod that changes it
        /// would leave an editor offering waves that do not exist, or hiding ones that do, and
        /// nothing about either failure would look like a wrong number. Null when the property could
        /// not be read at all - callers decide what to do about that rather than being handed a
        /// guess dressed as a reading.
        /// </summary>
        public int? NumWaves { get; private set; }

        /// <summary>
        /// True when the last wave is the extraction. The extraction is an objective too - simply
        /// always the same one - so which wave it falls on moves with <see cref="NumWaves"/>.
        /// </summary>
        public bool ExtractionWaveEnabled { get; private set; }

        /// <summary>
        /// The last wave the editor will offer, which is <see cref="NumWaves"/> where it could be
        /// read and otherwise the highest wave the config already puts an objective on.
        ///
        /// Never smaller than what is already there: refusing to show a shipped objective wave
        /// because a cap says it cannot exist would hide real data behind a number we are less sure
        /// of than the data itself.
        /// </summary>
        public int LastWave =>
            Math.Max(NumWaves ?? 0, Waves.Count == 0 ? 0 : Waves.Max(w => w.WaveIndex));

        /// <summary>The wave the extraction falls on, or null when the mode has no extraction wave.</summary>
        public int? ExtractionWave => ExtractionWaveEnabled && NumWaves is { } n && n > 0 ? n : null;

        /// <summary>The verbatim text of every OperationWaves entry, in shipped order.</summary>
        public List<string> OperationWavesRaw { get; } = new();

        public List<string> Warnings { get; } = new();

        /// <summary>
        /// The chance the game picks a given variant, as a fraction of the total weight.
        ///
        /// Computed rather than stored because it is a property of the SET: changing one weight
        /// changes every other objective's odds, and an editor that shows a stored percentage beside
        /// an edited weight is showing two numbers that disagree.
        /// </summary>
        public double? ShareOf(ObjectiveVariant variant)
        {
            var total = Variants.Sum(v => Math.Max(0, v.EffectiveWeight ?? 0));
            if (total <= 0 || variant?.EffectiveWeight is not { } w || w <= 0) return null;
            return w / total;
        }

        public static ObjectiveCatalogue Build(MergedGameConfig config,
                                               IReadOnlyDictionary<string, CatalogueClass> classes,
                                               InstanceCatalogue instances)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (classes == null) throw new ArgumentNullException(nameof(classes));
            if (instances == null) throw new ArgumentNullException(nameof(instances));

            var result = new ObjectiveCatalogue();
            var index = config.BuildSectionIndex();

            result.ReadWaves(index, classes);
            result.ReadFamilies(index, classes, instances);
            return result;
        }

        // ------------------------------------------------------------------ internals

        private void ReadWaves(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var coordinator = ClassScanner.FindByName(CoordinatorClass, classes);
            if (coordinator == null)
            {
                Warnings.Add($@"{CoordinatorClass} was not found; which waves carry an objective is unknown.");
                return;
            }

            CoordinatorSection = coordinator.Section;
            ReadWaveCount(index, coordinator, classes);

            var seen = new HashSet<int>();
            foreach (var value in index.Values(coordinator.Section, OperationWavesProperty))
            {
                OperationWavesRaw.Add(value.Value);

                if (!ConfigStructReader.TryReadStructRaw(value.Value, out var map) || map.Count == 0)
                {
                    Warnings.Add($@"An {OperationWavesProperty} entry could not be read and is not shown: {value.Value}");
                    continue;
                }

                if (ParseInt(map.GetValueOrDefault(@"WaveNumber")) is not { } number)
                {
                    Warnings.Add($@"An {OperationWavesProperty} entry names no WaveNumber: {value.Value}");
                    continue;
                }

                Waves.Add(new ObjectiveWave
                {
                    WaveNumber = number,
                    // +1: OperationWaves is zero-based and WaveIndex is one-based. See the summary on
                    // ObjectiveWave.WaveIndex - this is the single place it is corrected.
                    WaveIndex = number + 1,
                    CreditScale = ParseDouble(map.GetValueOrDefault(@"CreditScale")),
                    Raw = value.Value,
                    Shadowed = !seen.Add(number),
                });
            }

            if (Waves.Count == 0)
                Warnings.Add($@"{coordinator.Section} sets no {OperationWavesProperty}; no wave carries an objective.");

            Waves.Sort((a, b) => a.WaveIndex.CompareTo(b.WaveIndex));

            // Reported rather than corrected. An objective past the last wave is either a mod mid-edit
            // or a NumWaves we misread, and silently dropping the entry would hide whichever it is.
            foreach (var wave in Waves.Where(w => NumWaves is { } n && w.WaveIndex > n))
            {
                Warnings.Add($@"Wave {wave.WaveIndex} carries an objective but {NumWavesProperty} is " +
                             $@"{NumWaves}, so a match never reaches it.");
            }
        }

        /// <summary>
        /// How long a match is, and whether it ends in an extraction.
        ///
        /// Both come from the same two places every other per-object value does: the config section,
        /// last value winning, and the class's compiled default where the section sets none.
        /// </summary>
        private void ReadWaveCount(SectionIndex index, CatalogueClass coordinator,
                                   IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var configured = index.Values(coordinator.Section, NumWavesProperty).ToList();
            NumWaves = (configured.Count > 0 ? ParseInt(configured[^1].Value) : null)
                       ?? ParseInt(ClassDefaults.Effective(coordinator.Section, NumWavesProperty, classes));

            if (NumWaves is null)
            {
                Warnings.Add($@"{coordinator.Section} sets no readable {NumWavesProperty}; how many waves a " +
                             @"match runs is unknown, so only the waves that already carry an objective are offered.");
            }
            else if (NumWaves < 1)
            {
                Warnings.Add($@"{NumWavesProperty} is {NumWaves}, which is not a length a match can have.");
                NumWaves = null;
            }

            var extraction = index.Values(coordinator.Section, ExtractionEnabledProperty).ToList();
            var text = extraction.Count > 0
                ? extraction[^1].Value
                : ClassDefaults.Effective(coordinator.Section, ExtractionEnabledProperty, classes);
            ExtractionWaveEnabled = text != null && text.Trim().StartsWith(@"true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds the engagement classes by descent from SFXEngagement, never by a list of names.
        ///
        /// The same rule the wavelist editor finds factions by, and for the same reason: an objective
        /// Milan adds is a subclass, and a hard-coded roster would silently never show it.
        /// </summary>
        private void ReadFamilies(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes,
                                  InstanceCatalogue instances)
        {
            var engagements = classes.Values
                .Where(c => c.Name.StartsWith(EngagementBaseClass, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (engagements.Count == 0)
            {
                Warnings.Add($@"No {EngagementBaseClass}* classes were found; the objective rules cannot be read.");
                return;
            }

            foreach (var cls in engagements)
            {
                if (!instances.BySection.TryGetValue(cls.Section, out var perObject)) continue;
                if (perObject.Instances.Count == 0) continue;

                var family = new ObjectiveFamily { ClassSection = cls.Section, ClassName = cls.Name };

                // The class-path section, where it has one. An engagement with no shared section is
                // normal - SFXEngagement_Assassination has none - and means every value it uses comes
                // from its instances or from a base class it inherits.
                if (index.Find(cls.Section) is { } shared)
                {
                    foreach (var property in shared)
                    {
                        var live = MergedGameConfig.Live(property.Value).LastOrDefault();
                        if (live != null) family.Shared[property.Key] = live.Value;
                    }
                }

                foreach (var instance in perObject.Instances.OrderBy(i => i.Order))
                {
                    var key = $@"{instance.Id} {perObject.ClassToken}";
                    var section = index.Find(key);
                    if (section == null) continue;

                    var variant = new ObjectiveVariant
                    {
                        SectionKey = key,
                        InstanceId = instance.Id,
                        ClassSection = cls.Section,
                        ClassName = cls.Name,
                    };

                    foreach (var property in section)
                    {
                        var values = MergedGameConfig.Live(property.Value).ToList();
                        if (values.Count == 0) continue;

                        // Every value, not only the modelled ones: the editor shows what it knows how
                        // to show and the compiler needs the rest to leave alone.
                        variant.Values[property.Key] = values[^1].Value;

                        if (!property.Key.Equals(RequirementsProperty, StringComparison.OrdinalIgnoreCase)) continue;

                        foreach (var row in values)
                        {
                            variant.RequirementText.Add(row.Value);
                            if (ReadRequirement(row.Value) is { } requirement) variant.Requirements.Add(requirement);
                            else Warnings.Add($@"[{key}] has an {RequirementsProperty} row that could not be read: {row.Value}");
                        }
                    }

                    var typed = new ObjectiveVariant
                    {
                        SectionKey = variant.SectionKey,
                        InstanceId = variant.InstanceId,
                        ClassSection = variant.ClassSection,
                        ClassName = variant.ClassName,
                        // The first requirement's type names the objective. Where there are several -
                        // Retrieve's pickup and drop-off - the first is the one the player is doing;
                        // the rest are stages of it.
                        ObjectiveType = variant.Requirements.FirstOrDefault()?.ObjectiveType,
                    };
                    typed.Requirements.AddRange(variant.Requirements);
                    typed.RequirementText.AddRange(variant.RequirementText);
                    foreach (var (k, v) in variant.Values) typed.Values[k] = v;

                    if (typed.ObjectiveType == null)
                        Warnings.Add($@"[{key}] sets no {RequirementsProperty}, so what it asks the player to do is unknown.");

                    // The instance first, the family second. Resolved after the family's shared
                    // values are read so both scopes are available, and recorded so the editor can
                    // say which one an edit would be changing - they are different sections and
                    // writing to the wrong one produces a mod that installs and does nothing.
                    typed.SelectionWeight = ParseDouble(typed.Values.GetValueOrDefault(WeightProperty));
                    if (typed.SelectionWeight != null)
                    {
                        typed.EffectiveWeight = typed.SelectionWeight;
                        typed.WeightScope = @"instance";
                    }
                    else if (ParseDouble(family.Shared.GetValueOrDefault(WeightProperty)) is { } inherited)
                    {
                        typed.EffectiveWeight = inherited;
                        typed.WeightScope = @"family";
                    }

                    family.Variants.Add(typed);
                }

                if (family.Variants.Count > 0) Families.Add(family);
            }

            if (Families.Count == 0)
                Warnings.Add(@"No engagement class has any configured instance; there are no objectives to edit.");
        }

        private static ObjectiveRequirement ReadRequirement(string text)
        {
            if (!ConfigStructReader.TryReadStructRaw(text, out var map) || map.Count == 0) return null;

            var type = map.GetValueOrDefault(@"ObjectiveType");
            if (string.IsNullOrWhiteSpace(type)) return null;

            return new ObjectiveRequirement
            {
                ObjectiveType = type.Trim(),
                Minimum = ParseInt(map.GetValueOrDefault(@"MinimumObjectivesRequired")),
                Maximum = ParseInt(map.GetValueOrDefault(@"MaximumObjectivesAllowed")),
                Raw = text,
            };
        }

        private static int? ParseInt(string text) =>
            int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

        /// <summary>
        /// A float as the config writes one, "0.5f" and "0.5" alike.
        ///
        /// The trailing suffix is not decoration - Unreal's own reader stops at it - so a parser that
        /// refuses it would report every weight in the game as unknown.
        /// </summary>
        private static double? ParseDouble(string text)
        {
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t)) return null;

            var n = ValueTreeBuilder.NumericPrefixLength(t);
            if (n == 0) return null;

            return double.TryParse(t.AsSpan(0, n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : null;
        }
    }
}
