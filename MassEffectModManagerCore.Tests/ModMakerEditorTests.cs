using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.generator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// The wavelist editor's edit model: the document a project stores, the text an edited row is
    /// written as, and the checks that run against the installed game.
    ///
    /// The document tests need no game. The writer and validator tests do, because what they are
    /// really asserting is agreement with the game's own data - a writer that emits a shape the reader
    /// cannot read back, or a validator that rejects a wave the game ships, is wrong in a way no
    /// hand-written fixture would reveal.
    /// </summary>
    [TestClass]
    public class ModMakerEditorTests
    {
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        private static readonly object CacheLock = new();
        private static WavelistCatalogue _waves;

        #region The edit document - no game required

        /// <summary>
        /// A cell is identified by faction AND instance, never by instance alone.
        ///
        /// Instance names are reused across factions - Cerberus2's instances carry the base faction's
        /// names verbatim - so a key built from the instance alone silently merges two factions' edits
        /// into one, and the last one saved wins.
        /// </summary>
        [TestMethod]
        public void CellKeysAreQualifiedByFaction()
        {
            var a = Edit(@"Pkg.SFXWave_Horde_Cerberus", @"sfxwave_horde_cerberus1", 1, @"DO_Level1");
            var b = Edit(@"Pkg.SFXWave_Horde_Cerberus2", @"sfxwave_horde_cerberus1", 1, @"DO_Level1");

            Assert.AreNotEqual(a.Key, b.Key, @"Two factions sharing an instance name must not share a key");

            var doc = new WavelistEditDocument();
            doc.Set(a);
            doc.Set(b);
            Assert.AreEqual(2, doc.Count);

            // Keys are case-insensitive: the config writes section names in mixed case and instance
            // names in lower, and the page round-trips whatever it was given.
            var c = Edit(@"pkg.sfxwave_horde_cerberus", @"SFXWAVE_HORDE_CERBERUS1", 1, @"do_level1");
            doc.Set(c);
            Assert.AreEqual(2, doc.Count, @"A key differing only in case must replace, not duplicate");
        }

        /// <summary>Editing the same cell twice replaces the edit; it does not stack two of them.</summary>
        [TestMethod]
        public void SetReplacesTheEditForACell()
        {
            var doc = new WavelistEditDocument();
            doc.Set(Edit(@"Pkg.Faction", @"inst1", 3, @"DO_Level2", (@"WAVE_A", 1)));
            doc.Set(Edit(@"Pkg.Faction", @"inst1", 3, @"DO_Level2", (@"WAVE_B", 2)));

            Assert.AreEqual(1, doc.Count);
            Assert.AreEqual(@"WAVE_B", doc.Find(@"Pkg.Faction", @"inst1", 3, @"DO_Level2").Slots[0].EnemyType);
        }

        /// <summary>
        /// Reverting REMOVES the entry rather than writing the shipped values into one.
        ///
        /// The two look identical the day they are made and diverge later: an entry holding today's
        /// shipped values pins them, so a wave Milan rebalances in a future build would keep the old
        /// composition in every project that had ever "reverted" it. Absence inherits; a recorded copy
        /// does not.
        /// </summary>
        [TestMethod]
        public void RevertRemovesTheEntry()
        {
            var doc = new WavelistEditDocument();
            doc.Set(Edit(@"Pkg.Faction", @"inst1", 3, @"DO_Level2", (@"WAVE_A", 1)));

            Assert.IsTrue(doc.Revert(@"Pkg.Faction", @"inst1", 3, @"DO_Level2"));
            Assert.AreEqual(0, doc.Count);
            Assert.IsNull(doc.Find(@"Pkg.Faction", @"inst1", 3, @"DO_Level2"));
            Assert.IsFalse(doc.Revert(@"Pkg.Faction", @"inst1", 3, @"DO_Level2"), @"Reverting an unedited cell is not a change");
        }

        /// <summary>
        /// A filler slot survives a round trip through JSON as a filler.
        ///
        /// It is defined by the ABSENCE of counts, so the serialiser must omit them rather than write
        /// nulls or zeroes: a zero turns the budget filler into an ordinary slot that spawns nothing,
        /// and the wave quietly stops spending most of its points.
        /// </summary>
        [TestMethod]
        public void FillerSlotsRoundTripAsFillers()
        {
            var doc = new WavelistEditDocument();
            var edit = Edit(@"Pkg.Faction", @"inst1", 3, @"DO_Level2");
            edit.Slots.Add(new WavelistSlotEdit { EnemyType = @"WAVE_FILL" });
            edit.Slots.Add(new WavelistSlotEdit { EnemyType = @"WAVE_A", MinCount = 2, MaxCount = 2, MaxPerWave = 3 });
            doc.Set(edit);

            var json = doc.ToJsonString();
            StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex(@"""min"":\s*null"),
                @"An omitted count must be absent from the JSON, not present as null");

            var back = WavelistEditDocument.FromJson(JObject.Parse(json));
            var slots = back.Rows.Single().Slots;

            Assert.IsTrue(slots[0].IsFiller);
            Assert.IsFalse(slots[1].IsFiller);
            Assert.AreEqual(2, slots[1].MinCount);
            Assert.AreEqual(2, slots[1].MaxCount);
            Assert.AreEqual(3, slots[1].MaxPerWave);
        }

        /// <summary>
        /// A document from a newer build refuses rather than parsing the half it recognises. Silently
        /// dropping the parts it does not understand loses the user's work without telling them.
        /// </summary>
        [TestMethod]
        public void ANewerFormatIsRefused()
        {
            var json = JObject.Parse(@"{""format"": 99, ""rows"": []}");
            Assert.Throws<NotSupportedException>(() => WavelistEditDocument.FromJson(json));
        }

        /// <summary>
        /// The written shape is BioWare's: quoted enemy type, counts omitted entirely on the filler,
        /// and MaxPerWave only where there is one.
        /// </summary>
        [TestMethod]
        public void RowsAreWrittenInTheGamesShape()
        {
            var edit = Edit(@"Pkg.Faction", @"inst1", 3, @"DO_Level3");
            edit.Slots.Add(new WavelistSlotEdit { EnemyType = @"WAVE_CER_AssaultTrooper" });
            edit.Slots.Add(new WavelistSlotEdit { EnemyType = @"WAVE_CER_Nemesis", MinCount = 2, MaxCount = 2, MaxPerWave = 3 });

            Assert.AreEqual(
                @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_CER_AssaultTrooper""),(EnemyType=""WAVE_CER_Nemesis"", MinCount=2, MaxCount=2, MaxPerWave=3)))",
                WaveRowWriter.Write(edit));
        }

        /// <summary>
        /// The exact JSON the page posts, parsed by the exact code that receives it.
        ///
        /// This string was copied from the running editor - not written to match the C# model - which
        /// is the whole point: the two sides are written in different languages by different rules, and
        /// the only thing keeping them compatible is that someone checked. The page writes explicit
        /// nulls where the C# writer omits the field; both must mean "no count", i.e. the filler.
        /// </summary>
        [TestMethod]
        public void ThePagesWireFormatParses()
        {
            const string fromThePage =
                @"{""format"":1,""rows"":[{""section"":""SFXGameContentDLC_CON_MP4_Horde.SFXWave_Horde_Cerberus2""," +
                @"""instanceId"":""sfxwave_horde_cerberus1"",""waveIndex"":1,""difficulty"":""DO_Level3""," +
                @"""slots"":[{""type"":""WAVE_CER_AssaultTrooper"",""min"":null,""max"":null,""maxPerWave"":null}," +
                @"{""type"":""WAVE_CER_Centurion"",""min"":6,""max"":6,""maxPerWave"":null}," +
                @"{""type"":""WAVE_CER_Nemesis"",""min"":2,""max"":2,""maxPerWave"":null}]}]}";

            var doc = WavelistEditDocument.FromJson(JObject.Parse(fromThePage));

            Assert.AreEqual(1, doc.Count);
            var row = doc.Rows.Single();
            Assert.AreEqual(@"SFXGameContentDLC_CON_MP4_Horde.SFXWave_Horde_Cerberus2", row.Section);
            Assert.AreEqual(@"sfxwave_horde_cerberus1", row.InstanceId);
            Assert.AreEqual(1, row.WaveIndex);
            Assert.AreEqual(@"DO_Level3", row.Difficulty);
            Assert.AreEqual(3, row.Slots.Count);

            // An explicit null count from the page has to mean the same as an omitted one.
            Assert.IsTrue(row.Slots[0].IsFiller, @"An explicit null count must still read as the filler");
            Assert.AreEqual(6, row.Slots[1].MaxCount);
            Assert.IsNull(row.Slots[2].MaxPerWave);

            Assert.AreEqual(
                @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_CER_AssaultTrooper""),(EnemyType=""WAVE_CER_Centurion"", MinCount=6, MaxCount=6),(EnemyType=""WAVE_CER_Nemesis"", MinCount=2, MaxCount=2)))",
                WaveRowWriter.Write(row));

            // And the document the host would save is readable again by the page's own restore path.
            Assert.AreEqual(doc.Count, WavelistEditDocument.FromJson(JObject.Parse(doc.ToJsonString())).Count);
        }

        #endregion

        #region Against the installed game

        /// <summary>
        /// Every row the game ships can be turned into an edit, written back out, and read again as
        /// the same slots.
        ///
        /// This is the property that matters, and it is stronger than comparing the writer's output to
        /// BioWare's text: the two differ in whitespace on rows nobody edited, and that is fine,
        /// because an untouched row is re-emitted from its verbatim original rather than from here.
        /// What must never differ is the MEANING - if the writer emits a shape the reader parses
        /// differently, an edited wave changes in ways the user did not ask for and nothing throws.
        /// </summary>
        [TestMethod]
        public void EveryShippedRowSurvivesAWriteAndReread()
        {
            var waves = CachedWaves();
            if (waves == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            int checkedRows = 0;
            foreach (var faction in waves.Factions)
            foreach (var wave in faction.Waves)
            foreach (var row in wave.Rows)
            {
                var edit = new WavelistRowEdit
                {
                    Section = faction.Section,
                    InstanceId = wave.InstanceId,
                    WaveIndex = wave.WaveIndex ?? 0,
                    Difficulty = row.Difficulty,
                    Slots = row.Slots.Select(s => new WavelistSlotEdit
                    {
                        EnemyType = s.EnemyType,
                        MinCount = s.MinCount,
                        MaxCount = s.MaxCount,
                        MaxPerWave = s.MaxPerWave,
                    }).ToList(),
                };

                var written = WaveRowWriter.Write(edit);
                var reread = ReadRow(written);

                Assert.AreEqual(row.Difficulty, reread.Difficulty, $@"Difficulty changed on rewrite: {written}");
                Assert.AreEqual(row.Slots.Count, reread.Slots.Count, $@"Slot count changed on rewrite: {written}");

                for (int i = 0; i < row.Slots.Count; i++)
                {
                    var a = row.Slots[i];
                    var b = reread.Slots[i];
                    Assert.AreEqual(a.EnemyType, b.EnemyType, $@"Slot {i} type changed: {written}");
                    Assert.AreEqual(a.MinCount, b.MinCount, $@"Slot {i} MinCount changed: {written}");
                    Assert.AreEqual(a.MaxCount, b.MaxCount, $@"Slot {i} MaxCount changed: {written}");
                    Assert.AreEqual(a.MaxPerWave, b.MaxPerWave, $@"Slot {i} MaxPerWave changed: {written}");
                    Assert.AreEqual(a.IsFiller, b.IsFiller, $@"Slot {i} changed filler status: {written}");
                }

                checkedRows++;
            }

            // Rows, not slots: 7 factions x 11 waves x 4 difficulties. A floor rather than the exact
            // number, because the wavelists are content under development.
            Assert.IsTrue(checkedRows > 250, $@"Only {checkedRows} rows were round-tripped; the catalogue is not loading");
        }

        /// <summary>
        /// Re-composing a shipped wave exactly as it shipped raises nothing.
        ///
        /// A validator that flags the game's own data is not a validator, it is noise, and a user who
        /// sees a warning on every untouched cell stops reading them.
        /// </summary>
        [TestMethod]
        public void ShippedWavesValidateClean()
        {
            var waves = CachedWaves();
            if (waves == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var doc = new WavelistEditDocument();
            foreach (var faction in waves.Factions)
            foreach (var wave in faction.Waves.Where(w => w.WaveIndex != null))
            foreach (var row in wave.Rows)
            {
                doc.Set(new WavelistRowEdit
                {
                    Section = faction.Section,
                    InstanceId = wave.InstanceId,
                    WaveIndex = wave.WaveIndex.Value,
                    Difficulty = row.Difficulty,
                    Slots = row.Slots.Select(s => new WavelistSlotEdit
                    {
                        EnemyType = s.EnemyType,
                        MinCount = s.MinCount,
                        MaxCount = s.MaxCount,
                        MaxPerWave = s.MaxPerWave,
                    }).ToList(),
                });
            }

            var problems = WavelistEditValidator.Validate(doc, waves);
            foreach (var p in problems) Console.WriteLine($@"problem: {p}");

            Assert.AreEqual(0, problems.Count(p => p.Severity == WavelistProblemSeverity.Error),
                $@"The game's own waves must not produce errors: {string.Join(@" | ", problems.Where(p => p.Severity == WavelistProblemSeverity.Error))}");
        }

        /// <summary>
        /// The checks that catch an edit the game cannot load, each of which produces a wave that
        /// spawns wrongly rather than anything that throws.
        /// </summary>
        [TestMethod]
        public void BrokenEditsAreCaught()
        {
            var waves = CachedWaves();
            if (waves == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var faction = waves.Factions.First(f => f.Waves.Any(w => w.WaveIndex != null && w.Rows.Count > 0));
            var wave = faction.Waves.First(w => w.WaveIndex != null && w.Rows.Count > 0);
            var difficulty = wave.Rows[0].Difficulty;
            var priced = waves.Enemies.First(e => !e.IsPet).EnemyType;
            var dearest = waves.Enemies.Where(e => !e.IsPet).OrderByDescending(e => e.WaveCost).First();

            WavelistRowEdit Row(params WavelistSlotEdit[] slots) => new()
            {
                Section = faction.Section,
                InstanceId = wave.InstanceId,
                WaveIndex = wave.WaveIndex.Value,
                Difficulty = difficulty,
                Slots = slots.ToList(),
            };

            List<WavelistProblem> Check(WavelistRowEdit row)
            {
                var doc = new WavelistEditDocument();
                doc.Set(row);
                return WavelistEditValidator.Validate(doc, waves);
            }

            bool HasError(List<WavelistProblem> p, string fragment) =>
                p.Any(x => x.Severity == WavelistProblemSeverity.Error && x.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));

            // An enemy type the game does not price is not merely uncosted in the UI - it is a token
            // the wave manager will not resolve.
            Assert.IsTrue(HasError(Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = @"WAVE_NOT_A_REAL_ENEMY", MinCount = 1, MaxCount = 1 })), @"priced enemy types"));

            // Only one slot can spend what is left of the budget.
            Assert.IsTrue(HasError(Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = priced })), @"leftover budget"));

            // A slot with one count and not the other is neither a counted slot nor a filler.
            Assert.IsTrue(HasError(Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = priced, MaxCount = 2 })), @"both MinCount and MaxCount"));

            Assert.IsTrue(HasError(Check(Row()), @"no slots"));

            // A type listed twice is a warning and not an error: the engine loads the row and skips
            // the second listing, so the wave runs - it simply is not the wave that was composed. The
            // editor cannot produce this; a hand-edited project file can.
            var duplicated = Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = dearest.EnemyType, MinCount = 1, MaxCount = 1 },
                new WavelistSlotEdit { EnemyType = dearest.EnemyType, MinCount = 2, MaxCount = 2 }));

            Assert.IsFalse(duplicated.Any(p => p.Severity == WavelistProblemSeverity.Error),
                $@"A duplicated type must not block a compile: {string.Join(@" | ", duplicated)}");
            Assert.IsTrue(duplicated.Any(p => p.Message.Contains(@"ignores the rest", StringComparison.Ordinal)),
                $@"A duplicated type was not reported: {string.Join(@" | ", duplicated)}");

            // A count range is a supported composition - the game decides within it as the group
            // refills - so it raises nothing, not even a warning. Shipped data never uses one, which
            // is a convention rather than a rule, and the editor must not turn it into one.
            var ranged = Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = dearest.EnemyType, MinCount = 1, MaxCount = 2 }));
            Assert.IsFalse(ranged.Any(p => p.Message.Contains(@"MinCount", StringComparison.Ordinal)),
                $@"A MinCount/MaxCount range must be accepted silently: {string.Join(@" | ", ranged)}");

            // Backwards, though, is an error: it is not a range, it is a typo.
            Assert.IsTrue(HasError(Check(Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = dearest.EnemyType, MinCount = 3, MaxCount = 2 })), @"greater than MaxCount"));

            // A faction that is not installed cannot be edited into existence.
            var orphan = Row(new WavelistSlotEdit { EnemyType = priced });
            orphan.Section = @"Nowhere.SFXWave_Horde_Invented";
            Assert.IsTrue(HasError(Check(orphan), @"not in the installed game"));

            // Over-budget is the user's call and must stay a warning: the budget is what the wave
            // manager spends at runtime, not a rule the config enforces.
            // One slot, counted high. Repeating the dearest enemy across many slots would blow the
            // budget too, but it would also break the one-type-per-wave rule and report that instead.
            var expensive = Row(
                new WavelistSlotEdit { EnemyType = priced },
                new WavelistSlotEdit { EnemyType = dearest.EnemyType, MinCount = 99, MaxCount = 99 });

            var budgetProblems = Check(expensive);
            Assert.IsFalse(budgetProblems.Any(p => p.Severity == WavelistProblemSeverity.Error),
                @"Spending over budget must not be an error");
            Assert.IsTrue(budgetProblems.Any(p => p.Message.Contains(@"against a budget of", StringComparison.Ordinal)),
                $@"Over-budget was not reported: {string.Join(@" | ", budgetProblems)}");
        }

        #endregion

        #region Helpers

        private static WavelistRowEdit Edit(string section, string instance, int wave, string difficulty,
                                            params (string Type, int Count)[] slots)
        {
            var edit = new WavelistRowEdit
            {
                Section = section,
                InstanceId = instance,
                WaveIndex = wave,
                Difficulty = difficulty,
            };
            foreach (var (type, count) in slots)
                edit.Slots.Add(new WavelistSlotEdit { EnemyType = type, MinCount = count, MaxCount = count });
            return edit;
        }

        /// <summary>Parses a written row back with the same reader the scanner uses.</summary>
        private static WaveDifficultyRow ReadRow(string text)
        {
            var fields = ConfigStructReader.ReadStruct(text);
            var row = new WaveDifficultyRow { Difficulty = fields[@"Difficulty"], RawValue = text };

            int index = 0;
            foreach (var slotText in ConfigStructReader.ReadList(fields[@"Enemies"]))
            {
                var slot = ConfigStructReader.ReadStruct(slotText);
                row.Slots.Add(new WaveSlot
                {
                    Index = index++,
                    EnemyType = slot.GetValueOrDefault(@"EnemyType"),
                    MinCount = ParseInt(slot.GetValueOrDefault(@"MinCount")),
                    MaxCount = ParseInt(slot.GetValueOrDefault(@"MaxCount")),
                    MaxPerWave = ParseInt(slot.GetValueOrDefault(@"MaxPerWave")),
                });
            }
            return row;
        }

        private static int? ParseInt(string s) => int.TryParse(s?.Trim(), out var i) ? i : null;

        private static WavelistCatalogue CachedWaves()
        {
            lock (CacheLock)
            {
                if (_waves != null) return _waves;

                var target = TryGetLMPTarget();
                if (target == null) return null;

                var classes = ClassScanner.Scan(target);
                var config = MergedGameConfig.Load(target);
                var instances = InstanceCatalogue.Build(config, classes);
                return _waves = WavelistCatalogue.Build(config, classes, instances);
            }
        }

        private static GameTarget TryGetLMPTarget()
        {
            try
            {
                var root = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\BioWare\Mass Effect Legendary Edition",
                    @"Install Dir", null) as string;
                if (root == null) return null;

                var path = Path.Combine(root, @"Game", @"LMP");
                if (!Directory.Exists(path)) return null;

                var target = new GameTarget(MEGame.LMP, path, false, false);
                target.ValidateTarget();
                return target.IsValid ? target : null;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
