using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmaker.compiler;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.generator;
using ME3TweaksModManager.modmaker.project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// The enemy stat editor: the payload the page draws, the text an edited category row is written
    /// as, and the checks that run against the installed game.
    ///
    /// The writer is where the risk is. A category is ONE coalesced array entry, so editing one of
    /// forty-one stats means removing all forty-one by exact text and adding them back - and if any
    /// of the forty the user did not touch comes back spelled differently, the mod still builds,
    /// still installs, and quietly rewrites values nobody asked to change. So the tests that matter
    /// most here compare BYTES against the game's own rows rather than values against a fixture.
    /// </summary>
    [TestClass]
    public class ModMakerEnemyEditorTests
    {
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        private static readonly object CacheLock = new();
        private static DifficultyCatalogue _difficulty;
        private static Dictionary<string, CatalogueClass> _classes;

        private const string Handler = @"sfxgamempcontent.sfxdifficultyhandlermp";

        #region The writer - no game required

        /// <summary>
        /// An edit changes the number it was made to and NOTHING else, byte for byte.
        ///
        /// The fixture is deliberately ugly, and every ugly thing in it is copied from the shipped
        /// config: literal `\t` sequences after commas, spaces around some `=` and not others, a
        /// trailing `f` on most numbers and not all. Re-serialising a parsed row would tidy all of
        /// that away and produce a diff in which forty untouched stats appear to have changed.
        /// </summary>
        [TestMethod]
        public void AnEditedStatIsWrittenInPlace()
        {
            const string health = @"(StatName=""MaxHealth"",\t\tStatRange=(X=500.0f, Y=750.0f))";
            const string evade = @"(StatName=""EvadeFrequency"", StatRange=(X=9.0f,Y=7.0))";
            var row = new DifficultyCategoryLevel
            {
                Level = 3,
                RawValue = $@"(Category=""AssaultTrooper"", CategoryData=({health},{evade}))",
            };
            row.Stats.Add(new DifficultyStat { StatName = @"MaxHealth", X = 500, Y = 750, RawValue = health });
            row.Stats.Add(new DifficultyStat { StatName = @"EvadeFrequency", X = 9, Y = 7, RawValue = evade });

            var written = CategoryRowWriter.Write(row, new[]
            {
                new EnemyStatEdit { Level = 3, Category = @"AssaultTrooper", StatName = @"MaxHealth", X = 800, Y = 1200 },
            });

            Assert.AreEqual(
                $@"(Category=""AssaultTrooper"", CategoryData=((StatName=""MaxHealth"",\t\tStatRange=(X=800.0f, Y=1200.0f)),{evade}))",
                written);

            // The untouched stat is not merely equivalent, it is the same bytes - including the `Y=7.0`
            // that carries no `f` and the missing space after its comma.
            StringAssert.Contains(written, evade);
        }

        /// <summary>
        /// A number is spelled the way its neighbours are: a decimal point always, and the trailing
        /// `f` kept where the shipped value had one.
        ///
        /// Nothing in the game depends on either - an edited value is new text whichever way it is
        /// written - but a row where one number is spelled unlike the forty around it reads as a
        /// mistake to whoever is diffing the mod, and diffing is how a subtraction gets debugged.
        /// </summary>
        [TestMethod]
        public void NumbersKeepTheSpellingOfTheValueTheyReplace()
        {
            Assert.AreEqual(@"(X=2.0f, Y=0.25f)", Range(@"(X=1.0f, Y=0.5f)", 2, 0.25));
            Assert.AreEqual(@"(X=2.0, Y=0.25)", Range(@"(X=1.0, Y=0.5)", 2, 0.25));
            Assert.AreEqual(@"(X=412.5f, Y=-0.9f)", Range(@"(X=1.0f, Y=0.5f)", 412.5, -0.9));

            // Whitespace is the shipped value's, not a house style of ours.
            Assert.AreEqual(@"( X = 3.0f ,  Y = 4.0f )", Range(@"( X = 1.0f ,  Y = 2.0f )", 3, 4));
            return;

            static string Range(string shippedRange, double x, double y)
            {
                var stat = new DifficultyStat
                {
                    StatName = @"S", X = 1, Y = 2,
                    RawValue = $@"(StatName=""S"", StatRange={shippedRange})",
                };
                var written = CategoryRowWriter.WriteStat(stat, x, y);
                return written[written.IndexOf(@"StatRange=", StringComparison.Ordinal)..][@"StatRange=".Length..^1];
            }
        }

        /// <summary>
        /// Everything the writer cannot do exactly, it refuses to do at all.
        ///
        /// Each of these is a case where writing SOMETHING would produce a mod that installs and is
        /// wrong: a stat the game does not define at this level would be an invented value, a
        /// StatRange with a shape this build does not model would lose whatever the extra member was,
        /// and a stat whose text appears twice in its row cannot say which occurrence was meant.
        /// </summary>
        [TestMethod]
        public void WhatCannotBeWrittenExactlyIsRefused()
        {
            const string health = @"(StatName=""MaxHealth"", StatRange=(X=500.0f, Y=750.0f))";

            var row = new DifficultyCategoryLevel { Level = 1, RawValue = $@"(Category=""A"", CategoryData=({health}))" };
            row.Stats.Add(new DifficultyStat { StatName = @"MaxHealth", X = 500, Y = 750, RawValue = health });

            var missing = Assert.Throws<FormatException>(() => CategoryRowWriter.Write(row, new[]
            {
                new EnemyStatEdit { Level = 1, Category = @"A", StatName = @"Invented", X = 1, Y = 1 },
            }));
            StringAssert.Contains(missing.Message, @"Invented");

            var odd = new DifficultyStat
            {
                StatName = @"S", RawValue = @"(StatName=""S"", StatRange=(X=1.0f, Y=2.0f, Z=3.0f))",
            };
            Assert.Throws<FormatException>(() => CategoryRowWriter.WriteStat(odd, 5, 6));

            // The same stat text twice: which one an edit meant is unknowable, and replacing the first
            // would leave the second contradicting it.
            var twice = new DifficultyCategoryLevel
            {
                Level = 1, RawValue = $@"(Category=""A"", CategoryData=({health},{health}))",
            };
            twice.Stats.Add(new DifficultyStat { StatName = @"MaxHealth", X = 500, Y = 750, RawValue = health });
            var ambiguous = Assert.Throws<FormatException>(() => CategoryRowWriter.Write(twice, new[]
            {
                new EnemyStatEdit { Level = 1, Category = @"A", StatName = @"MaxHealth", X = 1, Y = 1 },
            }));
            StringAssert.Contains(ambiguous.Message, @"more than once");
        }

        #endregion

        #region The edit document - no game required

        /// <summary>
        /// Both ends of the interpolation are stored even when only one was typed into.
        ///
        /// X and Y are one struct in the game. An edit that recorded only the end that changed would
        /// have to read the other back out of the install when it compiled - and would then silently
        /// adopt a later Milan build's value for the half the user believed they had left alone.
        /// </summary>
        [TestMethod]
        public void BothEndsOfTheRangeAreStored()
        {
            var doc = new EnemyEditDocument();
            doc.Set(new EnemyStatEdit
            {
                Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = 9000, Y = 11250,
            });

            var json = doc.ToJson();
            var stat = (JObject)json[@"stats"][0];
            Assert.AreEqual(9000d, (double)stat[@"x"]);
            Assert.AreEqual(11250d, (double)stat[@"y"]);

            // Re-editing the same cell replaces rather than stacking, and reverting removes.
            doc.Set(new EnemyStatEdit
            {
                Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = 1, Y = 2,
            });
            Assert.AreEqual(1, doc.Count);
            Assert.AreEqual(1d, doc.Find(Handler, 3, @"Banshee", @"MaxHealth").X);

            Assert.IsTrue(doc.Revert(Handler, 3, @"BANSHEE", @"maxhealth"), @"Keys must be case-insensitive");
            Assert.AreEqual(0, doc.Count);
        }

        /// <summary>
        /// Edits are counted per stat but emitted per category row, and the two numbers are different
        /// on purpose: twenty edits to one enemy are ONE config replacement, not twenty.
        /// </summary>
        [TestMethod]
        public void EditsToOneEnemyAreOneRow()
        {
            var doc = new EnemyEditDocument();
            foreach (var stat in new[] { @"MaxHealth", @"MaxShields", @"EvadeFrequency" })
                doc.Set(new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = stat, X = 1, Y = 2 });

            doc.Set(new EnemyStatEdit { Section = Handler, Level = 4, Category = @"Banshee", StatName = @"MaxHealth", X = 1, Y = 2 });
            doc.Set(new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Atlas", StatName = @"MaxHealth", X = 1, Y = 2 });

            Assert.AreEqual(5, doc.Count);
            Assert.AreEqual(3, doc.RowCount, @"Three (level, enemy) rows, not five stats");
            Assert.AreEqual(3, doc.ByRow().Count());
        }

        /// <summary>A document from a future build refuses rather than dropping what it cannot read.</summary>
        [TestMethod]
        public void ANewerEnemyFormatIsRefused()
        {
            Assert.Throws<NotSupportedException>(() =>
                EnemyEditDocument.FromJson(JObject.Parse(@"{""format"": 99, ""stats"": []}")));
        }

        /// <summary>
        /// The enemy collection round-trips through a project, and reading it does not create one.
        ///
        /// The project file is a sparse override document. A getter that adds an empty collection to
        /// it leaves that collection in the saved file, which then says the user edited enemies when
        /// they only opened the tab.
        /// </summary>
        [TestMethod]
        public void TheEnemyCollectionRoundTripsThroughAProject()
        {
            var project = ModMakerProject.Create(@"Silver Grind", @"LMP", @"1");

            Assert.AreEqual(0, project.EnemyEdits.Count);
            Assert.IsNull(project.Document[@"collections"], @"Reading edits must not create a collections map");

            var doc = new EnemyEditDocument();
            doc.Set(new EnemyStatEdit
            {
                Section = Handler, Level = 4, Category = @"Banshee", StatName = @"MaxHealth", X = 9000, Y = 13500,
            });
            project.EnemyEdits = doc;

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ModMakerProject.FileExtension);
            try
            {
                project.Save(path);
                var reopened = ModMakerProject.Load(path);

                Assert.AreEqual(1, reopened.EnemyEdits.Count);
                var edit = reopened.EnemyEdits.Find(Handler, 4, @"Banshee", @"MaxHealth");
                Assert.IsNotNull(edit);
                Assert.AreEqual(13500d, edit.Y);

                // Emptied, the collection is removed rather than written as {} - and with nothing else
                // in the map, the map goes too.
                reopened.EnemyEdits = new EnemyEditDocument();
                Assert.IsNull(reopened.Document[@"collections"]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region The compiler - no game required

        /// <summary>
        /// Several stats on one enemy compile to a SINGLE replacement carrying all of them.
        ///
        /// Emitting one replacement per stat is the obvious mistake and it does not throw: the second
        /// subtraction would try to remove text the first one had already replaced, fail to match -
        /// array subtraction that misses is a no-op - and the mod would install with only the last
        /// edit applied. Nothing anywhere would say so.
        /// </summary>
        [TestMethod]
        public void EditsToOneEnemyCompileToOneReplacement()
        {
            var (difficulty, sections) = FakeHandler();

            var project = ModMakerProject.Create(@"Harder Gold", @"LMP", @"1");
            var doc = new EnemyEditDocument();
            doc.Set(new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = 750, Y = 1125 });
            doc.Set(new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"EvadeFrequency", X = 4, Y = 3 });
            project.EnemyEdits = doc;

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Difficulty = difficulty, Sections = sections, MountPriority = 4800, App = @"tests",
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors));
            Assert.AreEqual(1, result.EnemyRowsWritten);

            var writes = result.Manifest.Coalesced().File(@"BioDifficulty").Section(Handler).Properties;
            Assert.AreEqual(1, writes.Count, @"One array entry replaced, not one per stat");
            Assert.AreEqual(@"Level3DifficultyData", writes[0].Name);

            // The subtraction is the install's own text and the addition carries BOTH edits.
            Assert.AreEqual(ShippedBansheeRow, writes[0].Replace.Remove);
            StringAssert.Contains(writes[0].Replace.Add, @"X=750.0f");
            StringAssert.Contains(writes[0].Replace.Add, @"X=4.0f");
            Assert.IsFalse(writes[0].Replace.Add.Contains(@"X=500.0f"), @"The old value must not survive alongside the new one");
        }

        /// <summary>
        /// An edit composed back to exactly what shipped writes nothing at all, and a mod that would
        /// change nothing says so rather than building silently.
        /// </summary>
        [TestMethod]
        public void AnEnemyEditMatchingTheShippedValueIsNotWritten()
        {
            var (difficulty, sections) = FakeHandler();

            var project = ModMakerProject.Create(@"No-op", @"LMP", @"1");
            var doc = new EnemyEditDocument();
            doc.Set(new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = 500, Y = 750 });
            project.EnemyEdits = doc;

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Difficulty = difficulty, Sections = sections, MountPriority = 4800, App = @"tests",
            });

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, result.EnemyRowsWritten);
            Assert.AreEqual(1, result.UnchangedEditsSkipped);
            Assert.IsTrue(result.Warnings.Any(w => w.Message.Contains(@"change nothing")));
        }

        /// <summary>
        /// Edits the installed game cannot take stop the compile rather than being dropped from it.
        ///
        /// Every one of these arrives from a project written against a different install - an enemy
        /// Milan renamed, a difficulty it dropped, a stat that moved. None throws when compiled; each
        /// produces a mod that installs and leaves the enemy exactly as it was.
        /// </summary>
        [TestMethod]
        public void EnemyEditsTheGameCannotTakeAreRefused()
        {
            var (difficulty, _) = FakeHandler();

            var problems = EnemyEditValidator.Validate(WithEdits(
                new EnemyStatEdit { Section = @"nosuch.handler", Level = 3, Category = @"Banshee", StatName = @"MaxHealth" },
                new EnemyStatEdit { Section = Handler, Level = 9, Category = @"Banshee", StatName = @"MaxHealth" },
                new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Nobody", StatName = @"MaxHealth" },
                new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"Invented" },
                new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = double.NaN, Y = 1 }
            ), difficulty);

            Assert.AreEqual(5, problems.Count(p => p.Severity == EnemyProblemSeverity.Error),
                string.Join(@" | ", problems));

            // A negative value is REPORTED, never blocked: a negative health is nonsense, but a
            // negative damage scale is how the game itself writes Bronze.
            var negative = EnemyEditValidator.Validate(WithEdits(
                new EnemyStatEdit { Section = Handler, Level = 3, Category = @"Banshee", StatName = @"MaxHealth", X = -1, Y = -1 }
            ), difficulty);
            Assert.AreEqual(0, negative.Count(p => p.Severity == EnemyProblemSeverity.Error));
            Assert.AreEqual(1, negative.Count(p => p.Severity == EnemyProblemSeverity.Warning));
        }

        private static EnemyEditDocument WithEdits(params EnemyStatEdit[] edits)
        {
            var doc = new EnemyEditDocument();
            foreach (var edit in edits) doc.Set(edit);
            return doc;
        }

        private const string ShippedBansheeHealth = @"(StatName=""MaxHealth"", StatRange=(X=500.0f, Y=750.0f))";
        private const string ShippedBansheeEvade = @"(StatName=""EvadeFrequency"", StatRange=(X=9.0f, Y=7.0f))";
        private const string ShippedBansheeRow =
            @"(Category=""Banshee"", CategoryData=(" + ShippedBansheeHealth + @"," + ShippedBansheeEvade + @"))";

        /// <summary>
        /// A handler built by hand, so the compiler's rules can be exercised without a game.
        ///
        /// The section index is real - the compiler has to be able to answer "which coalesced sub-file
        /// does this section live in", and that mapping is data rather than a naming convention.
        /// </summary>
        private static (DifficultyCatalogue, SectionIndex) FakeHandler()
        {
            var level = new DifficultyCategoryLevel { Level = 3, RawValue = ShippedBansheeRow };
            level.Stats.Add(new DifficultyStat { StatName = @"MaxHealth", X = 500, Y = 750, RawValue = ShippedBansheeHealth });
            level.Stats.Add(new DifficultyStat { StatName = @"EvadeFrequency", X = 9, Y = 7, RawValue = ShippedBansheeEvade });

            var category = new DifficultyCategory { Name = @"Banshee" };
            category.Levels.Add(level);

            var table = new DifficultyTable { Section = Handler, ClassName = @"SFXDifficultyHandlerMP" };
            table.Levels.Add(3);
            table.Categories.Add(category);

            var catalogue = new DifficultyCatalogue();
            catalogue.Tables.Add(table);

            var section = new CoalesceSection { { @"Level3DifficultyData", new CoalesceProperty(@"Level3DifficultyData") } };
            var sections = new SectionIndex(
                new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase) { [Handler] = section },
                new List<string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [Handler] = @"BioDifficulty" });

            return (catalogue, sections);
        }

        #endregion

        #region Against the installed game

        /// <summary>
        /// Every category row in the game writes back byte-identically when nothing was edited.
        ///
        /// This is the assertion the whole writer rests on. It runs over every (category, level) row
        /// the install ships, not a sample, because the shapes that break in-place substitution are
        /// exactly the ones nobody thinks to write a fixture for.
        /// </summary>
        [TestMethod]
        public void EveryShippedCategoryRowSurvivesAnUneditedWrite()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var mp = MultiplayerTable(target);
            int rows = 0;

            foreach (var category in mp.Categories)
            {
                foreach (var level in category.Levels)
                {
                    Assert.AreEqual(level.RawValue, CategoryRowWriter.Write(level, Array.Empty<EnemyStatEdit>()),
                        $@"{category.Name} level {level.Level} did not survive an empty write");
                    rows++;
                }
            }

            Console.WriteLine($@"category rows: {rows}");
            Assert.IsTrue(rows > 100, @"The MP handler should carry a row per category per level");
        }

        /// <summary>
        /// Editing one stat of a real row changes that stat and leaves every other one alone.
        ///
        /// Run over every stat of every row: the failure this catches is a substitution that matched
        /// the wrong occurrence, and which stat that happens to depends entirely on the text of the
        /// row it lives in.
        /// </summary>
        [TestMethod]
        public void AnEditChangesOnlyTheStatItTouches()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var mp = MultiplayerTable(target);
            int edits = 0;

            foreach (var category in mp.Categories)
            {
                foreach (var level in category.Levels)
                {
                    // Compared against the ROW TEXT rather than against the catalogue. The two are
                    // not the same set: a stat the config spells wrongly is skipped by the reader and
                    // is still sitting in the text, and it must come through a write untouched like
                    // any other - being unreadable is not a licence to drop it.
                    var before = ReadStats(level.RawValue);

                    foreach (var stat in level.Stats)
                    {
                        var written = CategoryRowWriter.Write(level, new[]
                        {
                            new EnemyStatEdit
                            {
                                Level = level.Level, Category = category.Name, StatName = stat.StatName,
                                X = stat.X + 1, Y = stat.Y + 2,
                            },
                        });

                        var after = ReadStats(written);
                        CollectionAssert.AreEquivalent(before.Keys.ToList(), after.Keys.ToList(),
                            $@"{category.Name} level {level.Level}: the set of stats changed");

                        foreach (var name in before.Keys)
                        {
                            if (name.Equals(stat.StatName, StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.AreEqual(stat.X + 1, after[name].x, 0.0001);
                                Assert.AreEqual(stat.Y + 2, after[name].y, 0.0001);
                                continue;
                            }

                            Assert.AreEqual(before[name].x, after[name].x, 0.0001,
                                $@"{category.Name}.{name} moved while editing {stat.StatName}");
                            Assert.AreEqual(before[name].y, after[name].y, 0.0001);
                        }

                        // Not merely equal as numbers - the same bytes. A value re-rendered from its
                        // parsed form compares equal here and still shows up as a change in the diff
                        // of a mod nobody meant to touch those values in.
                        foreach (var other in level.Stats.Where(o => o != stat))
                            StringAssert.Contains(written, other.RawValue,
                                $@"{category.Name}.{other.StatName} was re-spelled while editing {stat.StatName}");

                        edits++;
                    }
                }
            }

            Console.WriteLine($@"stat edits round-tripped: {edits}");
            Assert.IsTrue(edits > 1000);
        }

        /// <summary>
        /// The editor is offered the multiplayer handler and not the singleplayer one.
        ///
        /// Chosen by descent from the MP handler class rather than by section name, so a mod
        /// subclassing it is picked up. Getting this wrong is invisible: the two tables are the same
        /// shape, and a singleplayer table in the enemy editor would look entirely plausible while
        /// editing a game mode Milan does not ship.
        /// </summary>
        [TestMethod]
        public void TheSinglePlayerHandlerIsNotOffered()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var difficulty = CachedDifficulty(target);
            var chosen = EnemyEditorPayload.MultiplayerTable(difficulty, CachedClasses(target));

            Assert.IsNotNull(chosen);
            Assert.AreEqual(@"SFXDifficultyHandlerMP", chosen.ClassName);

            var sp = difficulty.Table(@"SFXDifficultyHandler");
            Assert.IsNotNull(sp, @"The singleplayer handler is still discovered, it is just not offered");
            Assert.AreNotEqual(sp.Section, chosen.Section);
            Assert.IsTrue(sp.Levels.Count > chosen.Levels.Count,
                @"Singleplayer ships more levels, which is the standing proof the count is data");
        }

        /// <summary>
        /// The payload the page draws: a column per level the handler defines, a page per category,
        /// and a value only where the game has one.
        ///
        /// The counts are not pinned - Milan is in development - but the relationships are. The one
        /// that matters most is the last: a cell the game leaves out has to arrive at the page as
        /// ABSENT, because a zero is indistinguishable from a value the user chose.
        /// </summary>
        [TestMethod]
        public void EnemyPayloadIsBuiltForTheGrid()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var mp = MultiplayerTable(target);
            var payload = EnemyEditorPayload.Build(mp, ShippedText()?.EnemyStats);

            var levels = (JArray)payload[@"levels"];
            Assert.AreEqual(mp.Levels.Count, levels.Count);
            CollectionAssert.AreEqual(mp.Levels, levels.Select(l => (int)l[@"level"]).ToList());

            var categories = (JArray)payload[@"categories"];
            Assert.AreEqual(mp.Categories.Count, categories.Count);

            int cells = 0, absent = 0, labelled = 0, stats = 0;
            foreach (var category in categories.Cast<JObject>())
            {
                var name = (string)category[@"name"];
                var shipped = mp.Categories.First(c => c.Name == name);

                var flat = ((JArray)category[@"groups"]).SelectMany(g => (JArray)g[@"stats"]).Cast<JObject>().ToList();

                // Every stat the game ships appears exactly once, whatever the text layer says. A
                // grouping that drops one is how an editable value disappears without a trace.
                CollectionAssert.AreEquivalent(
                    shipped.StatNames.ToList(),
                    flat.Select(s => (string)s[@"name"]).ToList(),
                    $@"{name}: the grouped stats are not the shipped stats");

                foreach (var stat in flat)
                {
                    stats++;
                    if ((bool?)stat[@"unlabelled"] != true) labelled++;

                    var values = (JObject)stat[@"values"];
                    foreach (var level in mp.Levels)
                    {
                        var has = shipped.Levels.Any(l => l.Level == level
                            && l.Stats.Any(s => s.StatName.Equals((string)stat[@"name"], StringComparison.OrdinalIgnoreCase)));

                        if (has) { Assert.IsNotNull(values[level.ToString()]); cells++; }
                        else { Assert.IsNull(values[level.ToString()], @"An absent cell must not be filled in"); absent++; }
                    }
                }
            }

            Console.WriteLine($@"stats: {stats}, labelled: {labelled}, cells: {cells}, absent: {absent}");
            Assert.IsTrue(absent > 0, @"LMP ships stats that stop before the top difficulty");
            Assert.IsTrue(labelled > stats * 0.9,
                $@"Only {labelled} of {stats} stats have a label; the text layer is not joining");
        }

        /// <summary>
        /// A stat value the config does not spell as a number is REPORTED and left out, never read as
        /// zero.
        ///
        /// LMP shipped exactly one for a while: `Gunner.MaxHealth` at level 4 was written
        /// `X=1687.5.0f` in MergedConfig/BioDifficulty.xml - a stray `.0` in an otherwise ordinary
        /// 1687.5, carried in from BioWare's own ME3 difficulty data. Defaulting it, which is what
        /// `?? 0` did, produced a Gunner with no health at Platinum: shown in the editor as a real
        /// value, and scaled along with everything else.
        ///
        /// That is fixed at source now, so this asserts the INVARIANT rather than the bug. Whatever
        /// the config says, a stat this build cannot read has to be named in MalformedValues - and a
        /// gap in the grid has to be one of those, or a stat the game genuinely does not define.
        /// </summary>
        [TestMethod]
        public void MalformedStatValuesAreReportedNotZeroed()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var difficulty = CachedDifficulty(target);
            foreach (var m in difficulty.MalformedValues) Console.WriteLine($@"malformed: {m}");

            var mp = MultiplayerTable(target);

            // Every gap in the grid is either reported as malformed or is a stat the game genuinely
            // does not define at that level. What must not exist is a third kind: a value that was
            // read as something other than what the config says.
            foreach (var (category, stat, missing) in mp.IncompletePairs())
            {
                var reported = difficulty.MalformedValues.Any(m => m.Contains(category) && m.Contains(stat));
                var top = mp.Levels.Max();
                Assert.IsTrue(reported || missing.All(l => l == top),
                    $@"{category}.{stat} is missing at {string.Join(@",", missing)} and nothing said why");
            }

            foreach (var m in difficulty.MalformedValues)
                StringAssert.Contains(m, @"malformed", @"A malformed value must say so in words a person can act on");
        }

        #endregion

        #region Helpers

        private static DifficultyTable MultiplayerTable(GameTarget target) =>
            EnemyEditorPayload.MultiplayerTable(CachedDifficulty(target), CachedClasses(target))
            ?? throw new InvalidOperationException(@"No multiplayer difficulty handler was found");

        /// <summary>
        /// Stat name to its two numbers, read back out of written row text.
        ///
        /// Parsed here rather than by handing the text back to the app's own reader. That reader is
        /// internal, but more to the point a round trip through one parser proves only that it agrees
        /// with itself: what this has to establish is that the written row is readable by something
        /// that was not involved in writing it.
        /// </summary>
        private static Dictionary<string, (double x, double y)> ReadStats(string row)
        {
            var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in Elements(ParseStruct(row)[@"CategoryData"]))
            {
                var fields = ParseStruct(group);
                var range = ParseStruct(fields[@"StatRange"]);
                result[fields[@"StatName"]] = (Number(range[@"X"]), Number(range[@"Y"]));
            }

            return result;
        }

        /// <summary>Difficulty numbers carry a trailing `f` far more often than not.</summary>
        private static double Number(string text) =>
            double.TryParse(text?.Trim().TrimEnd('f', 'F'), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d : double.NaN;

        private static Dictionary<string, string> ParseStruct(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in SplitTop(Strip(text), ','))
            {
                var eq = IndexOfTop(field, '=');
                if (eq < 0) continue;

                // BioWare embeds literal backslash-t sequences after commas - two characters in the
                // config text, not control characters, so no amount of Trim() removes them.
                var key = field[..eq].Replace(@"\t", string.Empty).Replace(@"\n", string.Empty).Trim();
                var value = field[(eq + 1)..].Trim();
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
                result[key] = value;
            }
            return result;
        }

        private static string Strip(string text)
        {
            var t = text.Trim();
            return t.Length >= 2 && t[0] == '(' && t[^1] == ')' ? t[1..^1] : t;
        }

        /// <summary>
        /// The elements of `((A),(B))`. The whole list is itself one parenthesised group, so a single
        /// group spanning the input is descended into - without that step a two-stat category reads
        /// as one unparseable blob rather than as two stats.
        /// </summary>
        private static List<string> Elements(string text)
        {
            var groups = SplitGroups(text);
            if (groups.Count != 1) return groups;

            var inner = SplitGroups(Strip(groups[0]));
            return inner.Count > 0 ? inner : groups;
        }

        private static List<string> SplitGroups(string text)
        {
            var groups = new List<string>();
            int depth = 0, start = -1;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
                if (c == '(') { if (depth == 0) start = i; depth++; }
                else if (c == ')' && --depth == 0) groups.Add(text[start..(i + 1)]);
            }
            return groups;
        }

        private static List<string> SplitTop(string text, char separator)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == separator && depth == 0) { parts.Add(text[start..i]); start = i + 1; }
            }
            parts.Add(text[start..]);
            return parts;
        }

        private static int IndexOfTop(string text, char needle)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == needle && depth == 0) return i;
            }
            return -1;
        }

        private static TextLayer ShippedText()
        {
            var directory = Path.GetDirectoryName(typeof(ModMakerEnemyEditorTests).Assembly.Location);
            var candidate = Path.GetFullPath(Path.Combine(
                directory, @"..", @"..", @"..", @"..", @"MassEffectModManagerCore", @"modmaker", @"data", @"textlayer.json"));
            return File.Exists(candidate) ? TextLayer.Load(candidate) : null;
        }

        private static DifficultyCatalogue CachedDifficulty(GameTarget target)
        {
            lock (CacheLock)
                return _difficulty ??= DifficultyCatalogue.Build(MergedGameConfig.Load(target), CachedClasses(target));
        }

        private static Dictionary<string, CatalogueClass> CachedClasses(GameTarget target)
        {
            lock (CacheLock) return _classes ??= ClassScanner.Scan(target);
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
