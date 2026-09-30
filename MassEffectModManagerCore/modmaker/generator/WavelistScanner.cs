using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>One enemy slot in one difficulty row of one wave.</summary>
    public class WaveSlot
    {
        /// <summary>EnemyType token, e.g. WAVE_CER_Guardian. Priced by the enemy vocabulary.</summary>
        public string EnemyType { get; init; }

        /// <summary>How many spawn at once. Null on the filler slot, which carries no counts.</summary>
        public int? MinCount { get; init; }
        public int? MaxCount { get; init; }

        /// <summary>
        /// Cap on how many of this type the whole wave may contain, as opposed to at once. Present on
        /// 137 of 1,131 slots, all expensive units. It is what stops a budget-spending filler from
        /// flooding a wave with Banshees, so it has to survive into the editor - a wave rebuilt
        /// without it plays very differently at the same cost.
        /// </summary>
        public int? MaxPerWave { get; init; }

        /// <summary>
        /// The budget filler: no counts, spends whatever of the wave budget is left. Determined by the
        /// absence of counts rather than by position, so a wave that breaks the convention is
        /// described rather than misread.
        /// </summary>
        public bool IsFiller => MinCount == null && MaxCount == null;

        public int Index { get; init; }
    }

    /// <summary>
    /// One difficulty's composition of one wave: a single row of the instance's `enemies` array.
    /// </summary>
    public class WaveDifficultyRow
    {
        /// <summary>DO_Level1..DO_Level4 - Bronze, Silver, Gold, Platinum.</summary>
        public string Difficulty { get; init; }

        public List<WaveSlot> Slots { get; } = new();

        /// <summary>
        /// The row's value text, byte for byte as the merged config holds it.
        ///
        /// This is what an edit SUBTRACTS. The wavelist editor is a dedicated editor and so is exempt
        /// from the whole-array replacement rule (docs/02-data-model.md §0.1): it edits one row at a
        /// time, emitting a type-4 Remove of this exact text followed by the replacement. ConfigMerge
        /// compares removal text with `==` and nothing else, so a re-serialisation of the parsed
        /// slots - however faithful - is not good enough. A rewritten float or one different space
        /// makes the subtraction a no-op, and the game then holds both rows for the same difficulty.
        /// Nothing throws, at any point.
        /// </summary>
        public string RawValue { get; init; }
    }

    /// <summary>One wave of one faction: a per-object instance of the faction class.</summary>
    public class WaveInstance
    {
        /// <summary>Instance id, verbatim from the section key. Not unique across factions.</summary>
        public string InstanceId { get; init; }

        /// <summary>
        /// The coalesced section this instance's config lives under: "&lt;instance&gt; &lt;class&gt;".
        ///
        /// Kept rather than rebuilt from the instance id and the faction, because the class token in
        /// it is the one the config uses, which is not always the one the class scan reports. A
        /// compiler writing to a section key it guessed produces a mod that installs and does
        /// nothing.
        /// </summary>
        public string SectionKey { get; init; }

        /// <summary>
        /// From the `waveindex` property, NOT parsed out of the instance name. The name is not
        /// required to encode it, and Cerberus2's instances are named for the base faction.
        /// </summary>
        public int? WaveIndex { get; init; }

        public List<WaveDifficultyRow> Rows { get; } = new();
    }

    /// <summary>One faction class and its waves.</summary>
    public class WaveFaction
    {
        public string Section { get; init; }
        public string Name { get; init; }
        public List<WaveInstance> Waves { get; } = new();

        /// <summary>
        /// Selection weight if the wave manager can pick this faction, else null.
        ///
        /// Three of LMP's seven factions are absent here: BioWare REPLACED the basegame factions with
        /// MP4 versions instead of adding to them. Their wavelists are intact and putting them back
        /// into WaveTypes turns them on, so this is an editable state, not a reason to hide a faction.
        /// </summary>
        public int? SelectionWeight { get; init; }

        public bool IsSelectable => SelectionWeight != null;
    }

    /// <summary>An entry of the shared enemy vocabulary: a spawnable and its price.</summary>
    public class EnemyDefinition
    {
        public string EnemyType { get; init; }
        public string ArchetypeName { get; init; }
        public int WaveCost { get; init; }

        /// <summary>PetList entries are summonables keyed by class name rather than by EnemyType.</summary>
        public bool IsPet { get; init; }
    }

    /// <summary>The point range a difficulty's budget is interpolated across.</summary>
    public class WaveBudgetRange
    {
        public string Difficulty { get; init; }
        public double FirstWavePoints { get; init; }
        public double LastWavePoints { get; init; }
    }

    /// <summary>
    /// What an assassination objective needs from the wave it is placed on.
    ///
    /// The objective picks its target from the enemies ALREADY ON THE FIELD, a few seconds after the
    /// wave starts. If nothing eligible has spawned by then it glitches rather than failing
    /// gracefully - so this is a hard rule, and it is checkable from the slot list alone because
    /// spawning follows list order.
    ///
    /// Every number here is read from the game. The one inference is
    /// <see cref="SelectionSeconds"/>; see its note.
    /// </summary>
    public class AssassinationRule
    {
        /// <summary>
        /// The cheapest enemy the objective will accept as a target.
        ///
        /// `AssassinationTargetDataArray` is a per-difficulty table - 90, 70, 40, 20 as shipped, with
        /// more time allowed for the cheaper ones. The MINIMUM across it is what makes a wave valid:
        /// a wave that can only satisfy the 90 entry still works on the difficulty that asks for 20,
        /// and a validator that used the highest would refuse waves the game plays happily.
        /// </summary>
        public int? MinTargetCost { get; internal set; }

        /// <summary>
        /// How long after the wave starts the target is chosen.
        ///
        /// THE ONE INFERRED NUMBER. Milan chooses within roughly four seconds, and
        /// `WaveInstructionVODelay` on the assassination objective is 4.0 - the voice line that names
        /// the target, which cannot play before the target exists. Read from there rather than
        /// hardcoded so a Milan build that changes it moves this too; the field is named for the VO
        /// rather than for the selection, so if the two ever come apart this is the line to revisit.
        ///
        /// It matters less than it looks: <see cref="TargetWindow"/> is clamped by the field cap, and
        /// at the shipped 0.3s cadence the cap is what binds.
        /// </summary>
        public double? SelectionSeconds { get; internal set; }

        /// <summary>
        /// How many enemies can be on the field when the target is chosen.
        ///
        /// Two limits, and the smaller wins:
        ///
        /// - **Cadence.** One enemy every `SpawnDelay` seconds, so the window fits
        ///   `SelectionSeconds / SpawnDelay` of them. At the shipped 0.3s that is 13.
        /// - **The field cap.** `MaxEnemies` is how many can be alive at once, and in the first few
        ///   seconds nothing has died to make room - so the fourteenth enemy cannot be there however
        ///   fast they spawn. At the shipped 8 that is 8.
        ///
        /// Earlier notes put this at 8 by assuming a 0.5s cadence. The number is right and the
        /// reasoning was not: it is 8 because the field holds 8, and it would still be 8 if spawning
        /// were twice as fast again.
        /// </summary>
        public int? TargetWindow { get; internal set; }

        /// <summary>True when every number needed to check a wave was found.</summary>
        public bool Usable => MinTargetCost != null && TargetWindow is > 0;
    }

    /// <summary>
    /// The wavelists: the headline editor's data, read out of the installed game.
    ///
    /// Nothing here enumerates factions by name. The one anchor is SFXWave_Horde; every faction is
    /// found by walking SuperClass down from it, which is what lets a faction added by someone else's
    /// mod appear on its own. The waves themselves are per-object instances (docs/02 §1.6), and
    /// difficulty is a row INSIDE each instance's `enemies` array rather than part of the section key
    /// - the legacy app's (difficulty, faction, wave) path model does not map onto the real data.
    /// </summary>
    public class WavelistCatalogue
    {
        public const string WaveBaseClass = @"SFXWave_Horde";

        /// <summary>The class carrying the assassination objective's own settings.</summary>
        public const string AssassinationClass = @"SFXObjective_Assassination";

        /// <summary>Per-difficulty target data. Its `MinWavePointCost` is what makes an enemy eligible.</summary>
        public const string TargetDataProperty = @"AssassinationTargetDataArray";

        /// <summary>Seconds between one enemy spawning and the next.</summary>
        public const string SpawnDelayProperty = @"SpawnDelay";
        public const string WaveManagerClass = @"SFXWaveManager_Horde";
        public const string CoordinatorClass = @"SFXWaveCoordinator_HordeOperation";

        public List<WaveFaction> Factions { get; } = new();

        /// <summary>The closed, priced palette every wave draws from. From SFXWave_Horde.EnemyList.</summary>
        public List<EnemyDefinition> Enemies { get; } = new();

        /// <summary>Per-difficulty budget ramp, from SFXWaveManager_Horde.WavePointsRanges.</summary>
        public List<WaveBudgetRange> Budget { get; } = new();

        /// <summary>Wave numbers the budget is interpolated across, from WaveProgressionRange.</summary>
        public int FirstWave { get; private set; } = 1;
        public int LastWave { get; private set; } = 11;

        /// <summary>Wave numbers carrying an objective, from SFXWaveCoordinator_HordeOperation.</summary>
        /// <summary>
        /// Waves that carry an objective, in the SAME base as <see cref="WaveInstance.WaveIndex"/>.
        ///
        /// The two do not agree in the config and nothing says so. An instance's WaveIndex is
        /// one-based - sfxwave_horde_cerberus1 is WaveIndex 1, through to 11 - while the coordinator's
        /// OperationWaves carry a zero-based WaveNumber, shipping 2, 5, 9 and 10 for the waves a player
        /// knows as 3, 6, 10 and 11. Carrying the raw numbers through put the objective marker and the
        /// budget suppression on the wrong four rows, and both are plausible either way: every value
        /// is a valid wave number, so nothing looked wrong.
        ///
        /// Wave 11 belongs in this list. It is the extraction, and extraction is an objective - it is
        /// simply always the same one, which is why it is also reported separately as
        /// <see cref="LastWave"/>.
        /// </summary>
        public List<int> ObjectiveWaves { get; } = new();

        /// <summary>
        /// Everything that did not read cleanly, in words a person can act on.
        ///
        /// A missing anchor, an unparseable row, an unknown slot field or a duplicated difficulty row
        /// all land here rather than being dropped. The editor must show these: silently producing a
        /// smaller wavelist than the game has is the exact failure this whole generator is built to
        /// avoid.
        /// </summary>
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// Config values that did not parse, and so are not represented in this catalogue at all.
        ///
        /// Kept separate from the general warnings because these are data bugs to be corrected in the
        /// config source, not conditions the tool should learn to live with. Each one is a wave row
        /// the editor cannot show and the compiler cannot round-trip.
        /// </summary>
        public List<string> MalformedValues { get; } = new();

        /// <summary>
        /// How many enemies the game keeps alive at once - SFXWave_Horde.MaxEnemies, 8 as shipped.
        ///
        /// Not a limit on the slot list, and not a wave total. It is the size of the FIELD, and that
        /// makes it the number the editor needs to say anything true about cost: the fixed slots hold
        /// their counts on the field and the filler makes the rest of it up, so what a wave spends on
        /// its opening spawn cannot be computed without this.
        ///
        /// Null when it is set nowhere. Left null rather than defaulted to 8, so the editor says the
        /// field size is unknown instead of quietly costing a wave against a number this tool made up.
        /// </summary>
        public int? MaxEnemies { get; private set; }

        /// <summary>
        /// Seconds between spawns - `SFXWave_Horde.SpawnDelay`, 0.3 as shipped.
        ///
        /// Read rather than assumed because it decides how many enemies are on the field when an
        /// objective picks its target, and a build that changes it changes that count.
        /// </summary>
        public double? SpawnDelay { get; private set; }

        /// <summary>What an assassination objective needs from the wave it lands on. Null when unread.</summary>
        public AssassinationRule Assassination { get; private set; }

        /// <summary>Slot fields encountered that this tool does not model, and how many times.</summary>
        public SortedDictionary<string, int> UnmodelledSlotFields { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Total enemy slots read. 1,131 on LMP.</summary>
        public int SlotCount => Factions.Sum(f => f.Waves.Sum(w => w.Rows.Sum(r => r.Slots.Count)));

        /// <summary>Price of a spawnable, or null if the vocabulary does not name it.</summary>
        public int? CostOf(string enemyType) =>
            Enemies.FirstOrDefault(e => !e.IsPet && e.EnemyType.Equals(enemyType, StringComparison.OrdinalIgnoreCase))?.WaveCost;

        /// <summary>
        /// The point budget for a wave on a difficulty, interpolated across the wave range:
        /// budget(n) = X + (Y - X) * (n - first) / (last - first).
        ///
        /// This is what the legacy editor computed and displayed against enemy cost sums, which makes
        /// it the formula with a decade of published mods behind it. It is NOT WavePointsToBudget x
        /// OperationEnemyBudgetMultiplier; those are separate knobs and their relationship to this
        /// ramp is not established.
        ///
        /// Two waves ignore it - the extraction wave, and an objective wave until its objective
        /// completes - so callers showing a budget bar must suppress it there rather than show a
        /// number the game will not use.
        /// </summary>
        public double? BudgetFor(string difficulty, int wave)
        {
            var range = Budget.FirstOrDefault(b => b.Difficulty.Equals(difficulty, StringComparison.OrdinalIgnoreCase));
            if (range == null) return null;
            if (LastWave <= FirstWave) return range.FirstWavePoints;

            var t = (wave - FirstWave) / (double)(LastWave - FirstWave);
            return range.FirstWavePoints + (range.LastWavePoints - range.FirstWavePoints) * t;
        }

        /// <summary>
        /// Reads the wavelists from a merged config, a class scan and the instance dimension.
        /// </summary>
        public static WavelistCatalogue Build(
            MergedGameConfig config,
            IReadOnlyDictionary<string, CatalogueClass> classes,
            InstanceCatalogue instances)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (classes == null) throw new ArgumentNullException(nameof(classes));
            if (instances == null) throw new ArgumentNullException(nameof(instances));

            var result = new WavelistCatalogue();
            var index = config.BuildSectionIndex();

            var waveBase = ClassScanner.FindByName(WaveBaseClass, classes);
            if (waveBase == null)
            {
                // Named, not silent. An empty wavelist editor and a game whose wave classes were not
                // found look identical to a user, and only one of them is a bug worth reporting.
                result.Warnings.Add($@"Wave base class {WaveBaseClass} was not found in the installed game; no wavelists could be read.");
                return result;
            }

            result.ReadVocabulary(index, waveBase.Section);
            result.ReadFieldCap(index, classes, waveBase);
            result.ReadAssassination(index, classes);
            result.ReadManager(index, classes);
            result.ReadCoordinator(index, classes);
            result.ReadFactions(index, classes, instances, waveBase);
            return result;
        }

        // ------------------------------------------------------------------ internals

        private void ReadVocabulary(SectionIndex index, string waveBaseSection)
        {
            foreach (var v in index.Values(waveBaseSection, @"EnemyList"))
            {
                if (!TryReadStruct(v.Value, @"EnemyList", out var fields)) continue;
                Enemies.Add(new EnemyDefinition
                {
                    EnemyType = fields.GetValueOrDefault(@"EnemyType"),
                    ArchetypeName = fields.GetValueOrDefault(@"EnemyArchetypeName"),
                    WaveCost = ParseInt(fields.GetValueOrDefault(@"WaveCost")) ?? 0,
                });
            }

            foreach (var v in index.Values(waveBaseSection, @"PetList"))
            {
                if (!TryReadStruct(v.Value, @"PetList", out var fields)) continue;
                Enemies.Add(new EnemyDefinition
                {
                    EnemyType = fields.GetValueOrDefault(@"ClassName"),
                    WaveCost = ParseInt(fields.GetValueOrDefault(@"WaveCost")) ?? 0,
                    IsPet = true,
                });
            }

            if (Enemies.Count == 0)
                Warnings.Add($@"No enemy vocabulary found on {waveBaseSection}; wave costs cannot be computed.");
        }

        /// <summary>
        /// Reads the field cap, config first and the compiled default second.
        ///
        /// That order is the general rule for every value in this catalogue - config overrides the
        /// class - and it matters here because MaxEnemies is exactly the sort of knob a Milan build
        /// changes without touching the class.
        /// </summary>
        private void ReadFieldCap(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes,
                                  CatalogueClass waveBase)
        {
            // Last, not first: a scalar set more than once in the merged config is the later write
            // winning, which is the same rule the rest of this scanner reads by.
            var configured = index.Values(waveBase.Section, @"MaxEnemies").ToList();
            MaxEnemies = (configured.Count > 0 ? ParseInt(configured[^1].Value) : null)
                         ?? ParseInt(ClassDefaults.Effective(waveBase.Section, @"MaxEnemies", classes));

            if (MaxEnemies == null)
                Warnings.Add($@"{WaveBaseClass}.MaxEnemies was not found; wave spawn costs cannot be computed.");

            var delay = index.Values(waveBase.Section, SpawnDelayProperty).ToList();
            SpawnDelay = (delay.Count > 0 ? ParseDouble(delay[^1].Value) : null)
                         ?? ParseDouble(ClassDefaults.Effective(waveBase.Section, SpawnDelayProperty, classes));
        }

        /// <summary>
        /// Reads what an assassination objective needs, so the editor can check a wave against it.
        ///
        /// Every piece is optional. A missing one leaves <see cref="AssassinationRule.Usable"/> false
        /// and the check is skipped with a warning rather than guessed at - a hard rule enforced on
        /// invented numbers refuses waves that would have played.
        /// </summary>
        private void ReadAssassination(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var rule = new AssassinationRule();
            Assassination = rule;

            var objective = ClassScanner.FindByName(AssassinationClass, classes);
            if (objective == null)
            {
                Warnings.Add($@"{AssassinationClass} was not found; assassination waves cannot be checked.");
                return;
            }

            // The MINIMUM across the table - see AssassinationRule.MinTargetCost.
            foreach (var entry in index.Values(objective.Section, TargetDataProperty))
            {
                if (!ConfigStructReader.TryReadStructRaw(entry.Value, out var fields)) continue;
                var cost = ParseInt(fields.GetValueOrDefault(@"MinWavePointCost"));
                if (cost == null) continue;
                if (rule.MinTargetCost == null || cost < rule.MinTargetCost) rule.MinTargetCost = cost;
            }

            if (rule.MinTargetCost == null)
                Warnings.Add($@"{AssassinationClass}.{TargetDataProperty} named no {@"MinWavePointCost"}; " +
                             @"assassination waves cannot be checked.");

            var vo = index.Values(objective.Section, @"WaveInstructionVODelay").ToList();
            rule.SelectionSeconds = (vo.Count > 0 ? ParseDouble(vo[^1].Value) : null)
                                    ?? ParseDouble(ClassDefaults.Effective(objective.Section, @"WaveInstructionVODelay", classes));

            if (rule.SelectionSeconds is not { } seconds || SpawnDelay is not { } delay || delay <= 0)
            {
                Warnings.Add(@"The spawn cadence or the assassination selection delay was not found; " +
                             @"assassination waves cannot be checked.");
                return;
            }

            var byCadence = (int)Math.Floor(seconds / delay);
            rule.TargetWindow = MaxEnemies is { } cap && cap > 0 ? Math.Min(byCadence, cap) : byCadence;
        }

        private void ReadManager(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var manager = ClassScanner.FindByName(WaveManagerClass, classes);
            if (manager == null)
            {
                Warnings.Add($@"{WaveManagerClass} was not found; wave budgets and faction selection are unknown.");
                return;
            }

            foreach (var v in index.Values(manager.Section, @"WavePointsRanges"))
            {
                if (!TryReadStruct(v.Value, @"WavePointsRanges", out var fields)) continue;
                if (!TryReadStruct(fields.GetValueOrDefault(@"PointRange"), @"PointRange", out var range)) continue;
                Budget.Add(new WaveBudgetRange
                {
                    Difficulty = fields.GetValueOrDefault(@"Difficulty"),
                    FirstWavePoints = ParseDouble(range.GetValueOrDefault(@"X")) ?? 0,
                    LastWavePoints = ParseDouble(range.GetValueOrDefault(@"Y")) ?? 0,
                });
            }

            foreach (var v in index.Values(manager.Section, @"WaveProgressionRange"))
            {
                if (!TryReadStruct(v.Value, @"WaveProgressionRange", out var range)) continue;
                FirstWave = (int?)ParseDouble(range.GetValueOrDefault(@"X")) ?? FirstWave;
                LastWave = (int?)ParseDouble(range.GetValueOrDefault(@"Y")) ?? LastWave;
            }

            if (Budget.Count == 0)
                Warnings.Add($@"{WaveManagerClass}.WavePointsRanges is empty; no budget can be shown.");
        }

        private void ReadCoordinator(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var coordinator = ClassScanner.FindByName(CoordinatorClass, classes);
            if (coordinator == null)
            {
                Warnings.Add($@"{CoordinatorClass} was not found; objective waves are unknown.");
                return;
            }

            foreach (var v in index.Values(coordinator.Section, @"OperationWaves"))
            {
                if (!TryReadStruct(v.Value, @"OperationWaves", out var fields)) continue;
                var n = ParseInt(fields.GetValueOrDefault(@"WaveNumber"));
                if (n == null) continue;

                // Rebased onto WaveIndex - see the ObjectiveWaves summary. Done here, once, so that
                // nothing downstream has to know the config holds two different numbering schemes.
                var waveIndex = n.Value + 1;

                // Duplicated deliberately in shipped data - Milan re-adds waves with a changed
                // CreditScale using AddUnique, which only suppresses byte-identical values - so the
                // wave numbers are collected as a set while the raw entries stay available.
                if (!ObjectiveWaves.Contains(waveIndex)) ObjectiveWaves.Add(waveIndex);
            }
            ObjectiveWaves.Sort();
        }

        private void ReadFactions(
            SectionIndex index,
            IReadOnlyDictionary<string, CatalogueClass> classes,
            InstanceCatalogue instances,
            CatalogueClass waveBase)
        {
            var weights = ReadWaveTypes(index, classes);

            // Discovered, never listed: every descendant of the anchor is a faction, including ones
            // this tool has no name for.
            foreach (var cls in ClassScanner.Descendants(waveBase.Section, classes))
            {
                var faction = new WaveFaction
                {
                    Section = cls.Section,
                    Name = cls.Name,
                    SelectionWeight = weights.TryGetValue(cls.Section, out var w) ? w : null,
                };

                if (instances.BySection.TryGetValue(cls.Section, out var perObject))
                {
                    foreach (var inst in perObject.Instances)
                        faction.Waves.Add(ReadWave(index, perObject.ClassToken, inst.Id));
                }

                Factions.Add(faction);
            }

            foreach (var section in weights.Keys.Where(s => !Factions.Any(f => f.Section.Equals(s, StringComparison.OrdinalIgnoreCase))))
                Warnings.Add($@"{WaveManagerClass}.WaveTypes names '{section}', which is not a descendant of {waveBase.Section}.");
        }

        /// <summary>
        /// Faction class sections the wave manager may pick, and their selection weights.
        /// WaveClassName is a full package.class path, which is the same identity the class scan keys
        /// on, so no name resolution is needed.
        /// </summary>
        private Dictionary<string, int> ReadWaveTypes(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var manager = ClassScanner.FindByName(WaveManagerClass, classes);
            if (manager == null) return weights;

            foreach (var v in index.Values(manager.Section, @"WaveTypes"))
            {
                if (!TryReadStruct(v.Value, @"WaveTypes", out var fields)) continue;
                var name = fields.GetValueOrDefault(@"WaveClassName");
                if (string.IsNullOrWhiteSpace(name)) continue;
                weights[name] = ParseInt(fields.GetValueOrDefault(@"SelectionWeight")) ?? 0;
            }
            return weights;
        }

        private WaveInstance ReadWave(SectionIndex index, string classToken, string instanceId)
        {
            var sectionKey = $@"{instanceId} {classToken}";
            // CoalesceValue is a struct, so FirstOrDefault() yields a default instance rather than
            // null and cannot be null-tested.
            var indexValues = index.Values(sectionKey, @"WaveIndex").ToList();
            var waveIndex = indexValues.Count > 0 ? ParseInt(indexValues[0].Value) : null;
            var wave = new WaveInstance { InstanceId = instanceId, WaveIndex = waveIndex, SectionKey = sectionKey };

            if (waveIndex == null)
                Warnings.Add($@"[{sectionKey}] has no waveindex; its position in the rotation is unknown.");

            foreach (var v in index.Values(sectionKey, @"Enemies"))
            {
                var raw = v.Value;
                if (!TryReadStruct(raw, sectionKey, out var row)) continue;

                var difficulty = row.GetValueOrDefault(@"Difficulty");
                var entry = new WaveDifficultyRow { Difficulty = difficulty, RawValue = raw };

                List<string> slotGroups;
                try
                {
                    slotGroups = ConfigStructReader.ReadList(row.GetValueOrDefault(@"Enemies"));
                }
                catch (FormatException e)
                {
                    Warnings.Add($@"[{sectionKey}] {difficulty}: enemy list did not parse ({e.Message}).");
                    continue;
                }

                // A type the engine has already seen in this list is skipped by the engine, so it is
                // skipped here too. Reading it faithfully would be the worse choice: the editor would
                // show a slot that never spawns, cost it against the budget, and re-emit it - three
                // ways of telling the user something the game does not do.
                var seenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                int slotIndex = 0;
                foreach (var group in slotGroups)
                {
                    if (!TryReadStruct(group, sectionKey, out var slot)) continue;

                    foreach (var key in slot.Keys)
                    {
                        if (KnownSlotFields.Contains(key)) continue;
                        UnmodelledSlotFields[key] = UnmodelledSlotFields.GetValueOrDefault(key) + 1;
                    }

                    var enemyType = slot.GetValueOrDefault(@"EnemyType");
                    if (!string.IsNullOrWhiteSpace(enemyType) && !seenTypes.Add(enemyType))
                    {
                        // Reported, not silent: the row is still worth correcting at source, even
                        // though the game's own behaviour makes it harmless.
                        Warnings.Add($@"[{sectionKey}] {difficulty}: '{enemyType}' is listed more than once; the engine uses the first listing and ignores the rest.");
                        continue;
                    }

                    entry.Slots.Add(new WaveSlot
                    {
                        Index = slotIndex++,
                        EnemyType = enemyType,
                        MinCount = ParseInt(slot.GetValueOrDefault(@"MinCount")),
                        MaxCount = ParseInt(slot.GetValueOrDefault(@"MaxCount")),
                        MaxPerWave = ParseInt(slot.GetValueOrDefault(@"MaxPerWave")),
                    });
                }

                wave.Rows.Add(entry);
            }

            // Every (faction, wave, difficulty) triple is expected to have exactly one row. One
            // triple in shipped LMP has three, all surviving because they are AddUnique and differ by
            // a single field. Which one the game uses is not established, so they are reported and
            // left alone - collapsing near-identical rows would destroy the difference that
            // distinguishes them.
            foreach (var g in wave.Rows.GroupBy(r => r.Difficulty ?? @"(none)", StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                Warnings.Add($@"[{instanceId} {classToken}] has {g.Count()} rows for {g.Key}; only one is expected and which the game uses is unverified.");

            return wave;
        }

        private static readonly HashSet<string> KnownSlotFields =
            new(StringComparer.OrdinalIgnoreCase) { @"EnemyType", @"MinCount", @"MaxCount", @"MaxPerWave" };

        private bool TryReadStruct(string text, string context, out Dictionary<string, string> fields)
        {
            fields = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                fields = ConfigStructReader.ReadStruct(text);
                return true;
            }
            catch (FormatException e)
            {
                // The row is skipped, but never quietly: a malformed value is a data bug to fix at
                // source, and the warning has to carry enough to find it.
                var message = $@"{context}: malformed config value, skipped - {e.Message}";
                MalformedValues.Add(message);
                Warnings.Add(message);
                return false;
            }
        }

        private static string Shorten(string text) => text.Length <= 120 ? text : text[..117] + @"...";

        private static int? ParseInt(string s) =>
            int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

        /// <summary>Config numbers carry a trailing 'f' often enough that ignoring it loses values.</summary>
        private static double? ParseDouble(string s)
        {
            var t = s?.Trim().TrimEnd('f', 'F');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }
}
