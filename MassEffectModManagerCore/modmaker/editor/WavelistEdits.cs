using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// One enemy slot as the user composed it.
    ///
    /// Deliberately the same shape as <see cref="WaveSlot"/> minus its index: an edit is a complete
    /// replacement of a row's slot list, not a patch against the shipped one, so a slot's position is
    /// wherever the user put it rather than an identity it has to keep.
    /// </summary>
    public class WavelistSlotEdit
    {
        [JsonProperty(@"type")]
        public string EnemyType { get; set; }

        /// <summary>
        /// How many of this type spawn at once. Both null on the filler.
        ///
        /// A pair, not a number: with MaxCount above MinCount the game decides how many to bring
        /// back when several of the group die at once and it has more than one free slot to fill.
        /// Shipped data always sets the two equal, which is a convention rather than a requirement.
        /// </summary>
        [JsonProperty(@"min", NullValueHandling = NullValueHandling.Ignore)]
        public int? MinCount { get; set; }

        [JsonProperty(@"max", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxCount { get; set; }

        [JsonProperty(@"maxPerWave", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxPerWave { get; set; }

        /// <summary>
        /// The budget filler. Derived from the absence of counts, exactly as <see cref="WaveSlot"/>
        /// derives it, so a row read back from a project and a row read from the game agree on which
        /// slot spends the leftover budget.
        /// </summary>
        [JsonIgnore]
        public bool IsFiller => MinCount == null && MaxCount == null;
    }

    /// <summary>
    /// One (faction, wave, difficulty) cell the user has changed.
    ///
    /// Note what is NOT stored: the shipped row's original text. That is regenerated on the user's
    /// machine from their own install, so keeping a copy here would go stale the moment Milan shipped
    /// a new build - and a stale copy is worse than none, because the compiler matches array entries
    /// by exact text. The project stores the desired state; the diff against what is installed is
    /// computed at compile time.
    /// </summary>
    public class WavelistRowEdit
    {
        /// <summary>The faction class's section, e.g. SFXGameContentDLC_CON_MP4_Horde.SFXWave_Horde_Cerberus2.</summary>
        [JsonProperty(@"section")]
        public string Section { get; set; }

        /// <summary>The per-object instance holding this wave, e.g. sfxwave_horde_cerberus1.</summary>
        [JsonProperty(@"instanceId")]
        public string InstanceId { get; set; }

        [JsonProperty(@"waveIndex")]
        public int WaveIndex { get; set; }

        /// <summary>The difficulty token as the game writes it - DO_Level1..DO_Level4.</summary>
        [JsonProperty(@"difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty(@"slots")]
        public List<WavelistSlotEdit> Slots { get; set; } = new();

        /// <summary>
        /// The identity of the cell this replaces. Instance id alone is not enough: the same instance
        /// name appears under several faction classes (Cerberus2 reuses the base faction's names
        /// verbatim), so a key without the section would collide across factions.
        /// </summary>
        [JsonIgnore]
        public string Key => MakeKey(Section, InstanceId, WaveIndex, Difficulty);

        public static string MakeKey(string section, string instanceId, int waveIndex, string difficulty) =>
            string.Join('|', section?.ToLowerInvariant(), instanceId?.ToLowerInvariant(),
                        waveIndex.ToString(CultureInfo.InvariantCulture), difficulty?.ToLowerInvariant());
    }

    /// <summary>
    /// The wavelist editor's payload inside a project file - the value of one entry in the project's
    /// `collections` map.
    ///
    /// Sparse, like the rest of the project: it holds only the cells the user changed. A cell that is
    /// not here is not "unchanged and recorded", it is absent, and the compiler re-emits the shipped
    /// row verbatim. Reverting a cell therefore means REMOVING its entry rather than writing the
    /// shipped values back into one, which would pin those values against a future Milan build.
    /// </summary>
    public class WavelistEditDocument
    {
        public const int CurrentFormat = 1;

        /// <summary>The collection id this document is stored under in a project's `collections`.</summary>
        public const string CollectionId = @"wavelist";

        [JsonProperty(@"format")]
        public int Format { get; set; } = CurrentFormat;

        [JsonProperty(@"rows")]
        public List<WavelistRowEdit> Rows { get; set; } = new();

        /// <summary>
        /// Factions turned off in the lobby's enemy picker.
        ///
        /// Here, and not with the maps, because of what turning one off DOES: the faction leaves
        /// `EnemyTypes`, the lobby can no longer pick it, and every wavelist underneath it becomes
        /// unreachable. The toggle and the waves it silences are one subject.
        ///
        /// Shares <see cref="PickerOptionEdit"/> with the difficulty toggles - they are the same list
        /// subtraction on a different property - and each entry still names its picker, so the
        /// compiler and the validator need no special case for which document an edit came from.
        /// </summary>
        [JsonProperty(@"pickers")]
        public List<PickerOptionEdit> Pickers { get; set; } = new();

        [JsonIgnore]
        public int Count => Rows.Count + Pickers.Count;

        /// <summary>Replaces the edit for a cell, or adds it. Keyed by cell, so re-editing does not stack.</summary>
        public void Set(WavelistRowEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Rows.RemoveAll(r => r.Key == edit.Key);
            Rows.Add(edit);
        }

        /// <summary>
        /// Replaces a faction toggle, or adds it. Keyed by picker and id, exactly as the maplist's
        /// difficulty toggles are, so an entry that arrives from an older project slots in beside one
        /// made here.
        /// </summary>
        public void Set(PickerOptionEdit edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            edit.Picker ??= MatchPickers.Factions;
            Pickers.RemoveAll(p => p.Key == edit.Key);
            Pickers.Add(edit);
        }

        /// <summary>Puts a faction back the way the install has it, by dropping its entry.</summary>
        public bool RevertPickerOption(int? id, string picker = MatchPickers.Factions)
        {
            var key = new PickerOptionEdit { Picker = picker, Id = id }.Key;
            return Pickers.RemoveAll(p => p.Key == key) > 0;
        }

        /// <summary>Toggles in one picker. Only <see cref="MatchPickers.Factions"/> belongs here.</summary>
        public IEnumerable<PickerOptionEdit> For(string picker) =>
            Pickers.Where(e => string.Equals(e.Picker, picker, StringComparison.OrdinalIgnoreCase));

        /// <summary>Reverts a cell to shipped by dropping its entry. Returns true if there was one.</summary>
        public bool Revert(string section, string instanceId, int waveIndex, string difficulty)
        {
            var key = WavelistRowEdit.MakeKey(section, instanceId, waveIndex, difficulty);
            return Rows.RemoveAll(r => r.Key == key) > 0;
        }

        public WavelistRowEdit Find(string section, string instanceId, int waveIndex, string difficulty)
        {
            var key = WavelistRowEdit.MakeKey(section, instanceId, waveIndex, difficulty);
            return Rows.FirstOrDefault(r => r.Key == key);
        }

        public JObject ToJson() => JObject.FromObject(this);

        public string ToJsonString(bool indented = true) =>
            ToJson().ToString(indented ? Formatting.Indented : Formatting.None);

        /// <summary>
        /// Reads a document. A format newer than this build refuses rather than parsing what it
        /// recognises: a half-understood edit list silently drops the user's work.
        /// </summary>
        public static WavelistEditDocument FromJson(JObject json)
        {
            if (json == null) return new WavelistEditDocument();

            var format = (int?)json[@"format"] ?? CurrentFormat;
            if (format > CurrentFormat)
                throw new NotSupportedException($@"This wavelist collection is format {format}; this build understands {CurrentFormat}. Update Mod Manager.");

            var doc = json.ToObject<WavelistEditDocument>() ?? new WavelistEditDocument();
            doc.Rows ??= new List<WavelistRowEdit>();
            foreach (var row in doc.Rows) row.Slots ??= new List<WavelistSlotEdit>();

            doc.Pickers ??= new List<PickerOptionEdit>();
            // Only one picker lives in this collection, so an entry that does not name one is a
            // faction rather than an error. Stamped rather than assumed at every use site: the key an
            // entry is stored under is built from its picker name, and a null there collides.
            foreach (var picker in doc.Pickers)
                if (string.IsNullOrWhiteSpace(picker.Picker)) picker.Picker = MatchPickers.Factions;

            return doc;
        }
    }

    /// <summary>
    /// Renders an edited row back into the config text the game reads.
    ///
    /// Only EDITED rows go through here, and only as the ADD half of a replacement: the compiler
    /// pairs this text with a type-4 Remove carrying the shipped row's verbatim
    /// <see cref="WaveDifficultyRow.RawValue"/>. Rows the user did not touch are not emitted at all -
    /// they stay in the game's own coalesced, untouched, which is the point of editing per row rather
    /// than rewriting the array.
    ///
    /// The spacing below is BioWare's own: no space between slots, one space after the comma between a
    /// slot's fields. It is copied so an edited row sits in the array looking like its neighbours;
    /// nothing depends on it, since a row this writes is new text either way.
    /// </summary>
    public static class WaveRowWriter
    {
        public static string Write(WavelistRowEdit row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            var sb = new StringBuilder();
            sb.Append(@"(Difficulty=").Append(row.Difficulty).Append(@",Enemies=(");

            for (int i = 0; i < row.Slots.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Slot(row.Slots[i]));
            }

            sb.Append(@"))");
            return sb.ToString();
        }

        private static string Slot(WavelistSlotEdit slot)
        {
            var sb = new StringBuilder();
            sb.Append(@"(EnemyType=""").Append(slot.EnemyType).Append('"');

            // A filler carries no counts at all - that absence IS what makes it the filler, so writing
            // a zero here would turn it into an ordinary slot that spawns nothing.
            if (slot.MinCount != null) sb.Append(@", MinCount=").Append(slot.MinCount.Value.ToString(CultureInfo.InvariantCulture));
            if (slot.MaxCount != null) sb.Append(@", MaxCount=").Append(slot.MaxCount.Value.ToString(CultureInfo.InvariantCulture));
            if (slot.MaxPerWave != null) sb.Append(@", MaxPerWave=").Append(slot.MaxPerWave.Value.ToString(CultureInfo.InvariantCulture));

            sb.Append(')');
            return sb.ToString();
        }
    }

    /// <summary>How much a problem matters. Errors block a compile; warnings are for the user to judge.</summary>
    public enum WavelistProblemSeverity
    {
        Warning,
        Error,
    }

    public record WavelistProblem(WavelistProblemSeverity Severity, string Cell, string Message)
    {
        public override string ToString() => $@"{Severity}: {Cell} - {Message}";
    }

    /// <summary>
    /// Checks edited rows against the installed game.
    ///
    /// The editor's own UI keeps most of these from arising, but the document can also come from a
    /// project file written against a different install: a faction a mod supplied and the user then
    /// removed, an enemy type that no longer exists, a difficulty Milan dropped. None of those throw
    /// when compiled - they produce a wave the game silently spawns nothing for - so they are checked
    /// here rather than discovered in a match.
    /// </summary>
    public static class WavelistEditValidator
    {
        /// <remarks>
        /// The faction toggles this collection also carries are NOT checked here. They are a write to
        /// the match settings section, and they are checked where that section is written, against the
        /// lobby's own lists - see <see cref="MaplistEditValidator.ValidateFactionToggles"/>. Checking
        /// them in both places would report every problem twice.
        /// </remarks>
        public static List<WavelistProblem> Validate(WavelistEditDocument document, WavelistCatalogue waves)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            var problems = new List<WavelistProblem>();
            var difficulties = new HashSet<string>(waves.Budget.Select(b => b.Difficulty), StringComparer.OrdinalIgnoreCase);

            foreach (var row in document.Rows)
            {
                var cell = $@"{row.Section} {row.InstanceId} wave {row.WaveIndex} {row.Difficulty}";
                void Error(string m) => problems.Add(new WavelistProblem(WavelistProblemSeverity.Error, cell, m));
                void Warn(string m) => problems.Add(new WavelistProblem(WavelistProblemSeverity.Warning, cell, m));

                var faction = waves.Factions.FirstOrDefault(f => f.Section.Equals(row.Section, StringComparison.OrdinalIgnoreCase));
                if (faction == null)
                {
                    Error(@"this faction is not in the installed game; the edit would have nowhere to go");
                    continue;
                }

                var wave = faction.Waves.FirstOrDefault(w =>
                    string.Equals(w.InstanceId, row.InstanceId, StringComparison.OrdinalIgnoreCase));
                if (wave == null)
                {
                    Error(@"this faction has no such wave instance in the installed game");
                    continue;
                }

                if (wave.WaveIndex != null && wave.WaveIndex != row.WaveIndex)
                    Warn($@"the instance now sits at wave {wave.WaveIndex}, not {row.WaveIndex}; the edit follows the instance");

                if (difficulties.Count > 0 && !difficulties.Contains(row.Difficulty))
                    Error($@"'{row.Difficulty}' is not a difficulty the installed game defines");

                if (row.Slots.Count == 0)
                {
                    Error(@"a wave with no slots spawns nothing at all");
                    continue;
                }

                ValidateSlots(row, waves, Error, Warn);
                ValidateBudget(row, waves, Warn);
                ValidateAssassinationTarget(row, waves, Error, Warn);
                ValidateFieldCap(row, waves, Warn);
            }

            return problems;
        }

        private static void ValidateSlots(WavelistRowEdit row, WavelistCatalogue waves,
                                          Action<string> error, Action<string> warn)
        {
            var fillers = row.Slots.Count(s => s.IsFiller);
            if (fillers == 0)
                warn(@"no filler slot, so the wave spends only its fixed slots and leaves the rest of the budget unused");
            else if (fillers > 1)
                error($@"{fillers} slots carry no counts; only one slot can spend the leftover budget");
            else if (!row.Slots[0].IsFiller)
                warn(@"the filler is not the first slot; shipped waves put it first");

            for (int i = 0; i < row.Slots.Count; i++)
            {
                var slot = row.Slots[i];
                var at = $@"slot {i}";

                if (string.IsNullOrWhiteSpace(slot.EnemyType))
                {
                    error($@"{at} has no enemy type");
                    continue;
                }

                // The vocabulary is closed and priced. An unpriced type is not merely uncosted in the
                // UI - it is a token the wave manager will not resolve.
                if (waves.CostOf(slot.EnemyType) == null)
                    error($@"{at}: '{slot.EnemyType}' is not one of the game's priced enemy types");

                if (slot.IsFiller)
                {
                    if (slot.MaxPerWave != null)
                        warn($@"{at}: MaxPerWave on the filler caps the type the wave spends its budget on");
                    continue;
                }

                if (slot.MinCount == null || slot.MaxCount == null)
                {
                    error($@"{at}: a counted slot needs both MinCount and MaxCount, or neither to make it the filler");
                    continue;
                }

                if (slot.MinCount < 1 || slot.MaxCount < 1)
                    error($@"{at}: counts below 1 spawn nothing; remove the slot instead");
                else if (slot.MinCount > slot.MaxCount)
                    error($@"{at}: MinCount {slot.MinCount} is greater than MaxCount {slot.MaxCount}");

                // A range is NOT reported. Shipped data never uses one, but the pair is meaningful:
                // with MaxCount above MinCount the game decides how many to bring back when several
                // of the group die at once and there is room on the field for more than one.

                if (slot.MaxPerWave != null && slot.MaxPerWave < slot.MaxCount)
                    warn($@"{at}: MaxPerWave {slot.MaxPerWave} is below MaxCount {slot.MaxCount}, so the wave cannot field a full group");
            }

            // The engine takes the first listing of a type and skips the rest, so a duplicate is a
            // slot that does nothing rather than a wave that misbehaves. Reported, because what the
            // user composed is not what will run; not an error, because the game loads it happily.
            // The editor never offers a type twice, so this can only arrive from a project file
            // edited by hand or written against a different install.
            var duplicated = row.Slots.Where(s => !string.IsNullOrWhiteSpace(s.EnemyType))
                                      .GroupBy(s => s.EnemyType, StringComparer.OrdinalIgnoreCase)
                                      .Where(g => g.Count() > 1);
            foreach (var g in duplicated)
                warn($@"'{g.Key}' is listed {g.Count()} times; the engine uses the first listing and ignores the rest");
        }

        /// <summary>
        /// An objective wave must be able to hand an assassination a target in time.
        ///
        /// The objective picks its target from the enemies already on the field, a few seconds in. If
        /// nothing eligible has spawned by then it GLITCHES - it does not fail gracefully and it does
        /// not retry - which is why this is the one composition rule that is an error rather than an
        /// observation.
        ///
        /// It applies to every objective wave rather than to "assassination waves", because there is
        /// no such thing in the config: the coordinator picks which objective lands on which wave at
        /// match start, so any objective wave can be the assassination.
        ///
        /// Three facts make it checkable from the slot list alone. Spawning follows LIST ORDER. The
        /// filler is SKIPPED in that order - it spends leftover budget rather than taking a place in
        /// the queue. And the window is a number of ENEMIES, not slots: a slot of three Centurions
        /// uses three of them, so a wave can run out of window before it runs out of slots.
        /// </summary>
        private static void ValidateAssassinationTarget(WavelistRowEdit row, WavelistCatalogue waves,
                                                        Action<string> error, Action<string> warn)
        {
            if (!waves.ObjectiveWaves.Contains(row.WaveIndex)) return;

            var rule = waves.Assassination;
            if (rule is not { Usable: true })
            {
                // Skipped rather than guessed at. A hard rule enforced on invented numbers refuses
                // waves that would have played, which is worse than not checking.
                warn(@"this is an objective wave, but the assassination rules could not be read from " +
                     @"the installed game, so its target could not be checked");
                return;
            }

            var window = rule.TargetWindow.Value;
            var threshold = rule.MinTargetCost.Value;

            var spawned = 0;
            var unpriced = false;
            foreach (var slot in row.Slots)
            {
                if (spawned >= window) break;
                if (slot.IsFiller) continue;

                var unit = waves.CostOf(slot.EnemyType);
                if (unit == null) { unpriced = true; continue; }

                // MaxCount, not MinCount: the window asks what CAN be there, and refusing a wave that
                // might be fine is the wrong way to be wrong about a rule this strict.
                var count = Math.Max(1, slot.MaxCount ?? slot.MinCount ?? 1);
                if (unit.Value >= threshold) return;   // Eligible, and it spawns inside the window.
                spawned += count;
            }

            if (unpriced) return; // Already an error; an incomplete read must not also refuse the wave.

            error($@"an assassination objective could land on this wave and would have no target: none " +
                  $@"of the first {window} enemies to spawn costs at least {threshold} points. Spawning " +
                  @"follows the slot order and skips the budget slot, so move a heavier enemy up the list.");
        }

        /// <summary>
        /// The slot list adding up to more than the field holds - reported, never blocked.
        ///
        /// It is a technique rather than a mistake: eight Engineers, then eight Guardians as they die,
        /// then eight Phantoms, gives the wave a SEQUENCE of compositions instead of one. What the
        /// editor owes is the observation, because it is also exactly what a fourth slot added without
        /// noticing the first three already fill the field looks like.
        /// </summary>
        private static void ValidateFieldCap(WavelistRowEdit row, WavelistCatalogue waves, Action<string> warn)
        {
            if (waves.MaxEnemies is not { } cap || cap <= 0) return;

            var fixedCount = row.Slots.Where(s => !s.IsFiller)
                                .Sum(s => Math.Max(1, s.MaxCount ?? s.MinCount ?? 1));
            if (fixedCount <= cap) return;

            warn($@"these slots ask for {fixedCount} enemies against a field of {cap}, so not all of " +
                 @"them will be out at once; the later ones fill in as the earlier ones die");
        }

        private static void ValidateBudget(WavelistRowEdit row, WavelistCatalogue waves, Action<string> warn)
        {
            // The budget is not a rule the game enforces on the config - it is what the wave manager
            // spends at runtime. Over-spending is the user's call to make, so it is reported and never
            // blocked; and on waves where the budget does not apply there is nothing to report at all.
            if (row.WaveIndex == waves.LastWave || waves.ObjectiveWaves.Contains(row.WaveIndex)) return;

            var budget = waves.BudgetFor(row.Difficulty, row.WaveIndex);
            if (budget == null) return;

            int fixedCost = 0;
            foreach (var slot in row.Slots)
            {
                if (slot.IsFiller) continue;
                var unit = waves.CostOf(slot.EnemyType);
                if (unit == null) return; // Already an error; an incomplete total would mislead.
                fixedCost += unit.Value * (slot.MaxCount ?? 1);
            }

            if (fixedCost > budget.Value)
            {
                warn($@"the fixed slots cost {fixedCost} against a budget of {budget.Value:0.##}; " +
                     @"the filler gets nothing and the wave may spawn fewer enemies than composed");
            }
        }
    }
}
