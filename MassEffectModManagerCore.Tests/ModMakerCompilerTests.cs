using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmaker.compiler;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.generator;
using ME3TweaksModManager.modmaker.project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// The resolution half of the compiler: a project turned into a build manifest.
    ///
    /// Most of these need no game, because the rules being tested are about refusing to produce a
    /// wrong mod rather than about the game's data. The two that do need one are the two that matter
    /// most: the text a subtraction carries has to be the install's own bytes, and a section has to be
    /// written into the coalesced sub-file it actually lives in. Both of those fail SILENTLY - the
    /// mod builds, installs, and does nothing - so neither can be left to inspection.
    /// </summary>
    [TestClass]
    public class ModMakerCompilerTests
    {
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        private static readonly object CacheLock = new();
        private static WavelistCatalogue _waves;
        private static SectionIndex _sections;
        private static ObjectiveCatalogue _objectives;
        private static WeaponCatalogue _weaponCatalogue;

        #region The manifest format - no game required

        /// <summary>
        /// A manifest the compiler could emit satisfies the shipped schema.
        ///
        /// Written against the schema rather than the example for the same reason as the project
        /// tests: the example was authored by hand to match and the writer was not, which is exactly
        /// where the two drift apart.
        /// </summary>
        /// <summary>
        /// A write carries the number the FORMAT uses, not the enum's declaration order.
        ///
        /// Two numberings exist and they disagree. `CoalesceParseAction` is a C# enum - Add 0,
        /// AddUnique 1, New 2, None 3, Remove 4, RemoveProperty 5 - and the coalesced format numbers
        /// the same ideas New 0, RemoveProperty 1, Add 2, AddUnique 3, Remove 4.
        /// <see cref="PropertyWrite.ValueType"/> is the format's, because `ModEmitter` hands it
        /// straight to the int overload of `CoalesceValue`, which stores it verbatim.
        ///
        /// The compiler used to cast the enum. That was wrong at every site but one, silently:
        /// `New` went out as 2, which the format reads as Add, so an overwrite appended; `AddUnique`
        /// went out as 1, which is RemoveProperty, so adding one value to a list deleted the list.
        /// Only `Remove` survived, because it is 4 in both.
        ///
        /// This pins the actual integers. Asserting them through `GetValueType` on both sides would
        /// pass whatever the converter did; the point of the test is that the numbers are these.
        /// </summary>
        [TestMethod]
        public void WritesCarryTheFormatsNumbersAndNotTheEnumsOrdinals()
        {
            Assert.AreEqual(0, CoalesceValue.GetValueType(CoalesceParseAction.New));
            Assert.AreEqual(1, CoalesceValue.GetValueType(CoalesceParseAction.RemoveProperty));
            Assert.AreEqual(2, CoalesceValue.GetValueType(CoalesceParseAction.Add));
            Assert.AreEqual(3, CoalesceValue.GetValueType(CoalesceParseAction.AddUnique));
            Assert.AreEqual(4, CoalesceValue.GetValueType(CoalesceParseAction.Remove));

            // The disagreement itself, so that a future build which makes the enum match the format
            // fails here and is read rather than quietly changing what every write means.
            Assert.AreNotEqual((int)CoalesceParseAction.New, CoalesceValue.GetValueType(CoalesceParseAction.New));
            Assert.AreNotEqual((int)CoalesceParseAction.AddUnique, CoalesceValue.GetValueType(CoalesceParseAction.AddUnique));
            Assert.AreNotEqual((int)CoalesceParseAction.RemoveProperty, CoalesceValue.GetValueType(CoalesceParseAction.RemoveProperty));
            Assert.AreEqual((int)CoalesceParseAction.Remove, CoalesceValue.GetValueType(CoalesceParseAction.Remove),
                @"Remove agreeing in both numberings is why half of this went unnoticed");

            // And what the emitter actually does with it: the number on the write is the number on
            // the value, unconverted. That is the fact that makes the wrong number fatal.
            var value = new CoalesceValue(@"x", CoalesceValue.GetValueType(CoalesceParseAction.AddUnique));
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.AddUnique), value.ValueType);
            Assert.AreEqual(new CoalesceValue(@"x", CoalesceParseAction.AddUnique).ValueType, value.ValueType);
        }

        [TestMethod]
        public void ManifestSerialisationMatchesTheSchema()
        {
            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(@"build.schema.json")));

            var manifest = new BuildManifest
            {
                Game = @"LMP",
                Generator = new BuildGenerator { App = @"tests", GeneratedAt = DateTime.UtcNow.ToString(@"o") },
                Mod = new BuildMod
                {
                    Name = @"Silver Grind",
                    Author = @"Mgamerz",
                    Version = @"3",
                    DlcFolder = ModMakerCatalogue.CompilerDLCFolderName,
                    Mount = new BuildMount
                    {
                        Priority = 4800,
                        TlkId = ModMakerCatalogue.CompilerTlkId,
                        Flags = InstallMount.Flags(requireAllPlayers: false),
                    },
                },
            };

            var section = manifest.Coalesced().File(@"BioGame").Section(@"sfxwave_horde_cerberus1 sfxgamecontentdlc_con_mp4_horde.sfxwave_horde_cerberus2");
            section.Properties.Add(new PropertyWrite
            {
                Name = @"enemies",
                Source = @"wavelist/sfxwave_horde_cerberus1/DO_Level3",
                Replace = new ValueReplacement { Remove = @"(Difficulty=DO_Level3,Enemies=())", Add = @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_A"")))" },
            });
            manifest.Coalesced().File(@"BioGame").Section(@"sfxgamecontent.sfxpowercustomaction_pull")
                    .Properties.Add(new PropertyWrite { Name = @"impactradius", Value = @"(BaseValue=750.0)", ValueType = 0 });

            var json = manifest.ToJson();
            Assert.IsTrue(json.IsValid(schema, out IList<string> errors), string.Join(Environment.NewLine, errors));

            // The discriminator has to survive serialisation, or the emitter cannot tell one kind of
            // job from another.
            Assert.AreEqual(@"coalesced", (string)json[@"jobs"][0][@"type"]);
        }

        /// <summary>
        /// A write cannot carry both a plain value and a replacement.
        ///
        /// The schema's oneOf is what stops a compiler bug from emitting both, where the two mean
        /// different things - overwrite the property, versus subtract one array entry and add another.
        /// Asserted because a schema constraint nothing tests is a comment.
        /// </summary>
        [TestMethod]
        public void AValueAndAReplaceCannotBothBeWritten()
        {
            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(@"build.schema.json")));

            var manifest = NewManifest();
            manifest.Coalesced().File(@"BioGame").Section(@"a.b").Properties.Add(new PropertyWrite
            {
                Name = @"x",
                Value = @"1",
                Replace = new ValueReplacement { Remove = @"1", Add = @"2" },
            });

            Assert.IsFalse(manifest.ToJson().IsValid(schema), @"A write with both a value and a replace must not validate");
        }

        #endregion

        #region What the compiler refuses - no game required

        /// <summary>
        /// A leaf value that cannot be resolved against the install is refused, not dropped.
        ///
        /// The value surface resolves entirely against the INSTALLED game, so an edit can stop being
        /// meaningful without the project changing at all - a section renamed, a property no longer
        /// set, an array entry removed underneath a positional id. Compiling anyway would produce a
        /// mod that installs and is missing exactly the edits the compiler could not place, which a
        /// user cannot tell apart from one that works.
        /// </summary>
        [TestMethod]
        public void AnUnresolvableLeafValueIsRefusedRatherThanSilentlyDropped()
        {
            var project = ModMakerProject.Create(@"Has values", @"LMP", @"1");
            project.Values[@"sfxgame.sfxgamemodebase/nosuchproperty"] = 4.5;

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Sections = ValueIndex(@"(BaseValue=6.0)"),
            });

            Assert.IsFalse(result.Succeeded, @"A project this build cannot fully compile must not produce a manifest");
            Assert.IsNull(result.Manifest);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"no property", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>A collection this build has no compiler for is refused for the same reason.</summary>
        [TestMethod]
        public void AnUnknownCollectionIsRefused()
        {
            var project = ModMakerProject.Create(@"From the future", @"LMP", @"1");
            project.Collections[@"objectives"] = new JObject { [@"rows"] = new JArray() };

            var result = ProjectCompiler.Compile(project, new CompileInputs());

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(p => p.Where.Contains(@"objectives", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// Reading a project's wavelist edits does not add anything to the document.
        ///
        /// The getter used to create the `collections` map to look inside it, which left an empty map
        /// in a file that is supposed to hold only what the user changed - and the compiler reads that
        /// getter on every project, edited or not.
        /// </summary>
        [TestMethod]
        public void ReadingEditsDoesNotAddACollectionsMap()
        {
            var project = ModMakerProject.Create(@"Untouched", @"LMP", @"1");

            Assert.AreEqual(0, project.WavelistEdits.Count);
            ProjectCompiler.Compile(project, new CompileInputs { Waves = SyntheticWaves() });

            Assert.IsNull(project.Document[@"collections"], @"Reading must not write");
        }

        #endregion

        #region The wavelist job - synthetic game data

        /// <summary>An edited row becomes a subtraction of the shipped text and an addition of the new one.</summary>
        [TestMethod]
        public void AnEditedRowIsWrittenAsAReplacement()
        {
            var waves = SyntheticWaves();
            var project = ProjectWithEdit(EditedSlots());

            var result = ProjectCompiler.Compile(project, SyntheticInputs(waves));

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            Assert.AreEqual(1, result.WavelistRowsWritten);

            var write = OnlyWrite(result.Manifest);
            Assert.AreEqual(ShippedRow, write.Replace.Remove);
            Assert.AreEqual(@"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_A""),(EnemyType=""WAVE_B"", MinCount=3, MaxCount=3)))",
                write.Replace.Add);
            Assert.IsNull(write.Value, @"A replacement must not also carry a plain value");

            // Fixed rather than chosen: a ModMaker mod is one DLC, and it is the name the generator
            // excludes from its own scans so a built mod never becomes its own baseline.
            Assert.AreEqual(ModMakerCatalogue.CompilerDLCFolderName, result.Manifest.Mod.DlcFolder);
        }

        /// <summary>
        /// An edit that composes back to exactly what shipped writes nothing.
        ///
        /// Remove-then-add of identical text is a no-op with a chance of going wrong, and it would put
        /// a section into the mod that has no reason to be there.
        /// </summary>
        [TestMethod]
        public void AnEditMatchingTheShippedRowIsNotWritten()
        {
            var waves = SyntheticWaves();
            var project = ProjectWithEdit(ShippedSlots());

            var result = ProjectCompiler.Compile(project, SyntheticInputs(waves));

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            Assert.AreEqual(0, result.WavelistRowsWritten);
            Assert.AreEqual(1, result.UnchangedEditsSkipped);
            Assert.AreEqual(0, result.Manifest.Jobs.Count, @"Nothing to write means no job at all");
            Assert.IsTrue(result.Warnings.Any(p => p.Message.Contains(@"change nothing", StringComparison.Ordinal)),
                @"A mod that would do nothing is worth saying out loud");
        }

        /// <summary>
        /// A difficulty with more than one shipped row is refused.
        ///
        /// Exact-text subtraction removes one entry; the others stay, and which of them the game uses
        /// is unverified - so the mod would be built on a guess. Shipped LMP had exactly this in
        /// Collector wave 5.
        /// </summary>
        [TestMethod]
        public void AnAmbiguousDifficultyIsRefused()
        {
            var waves = SyntheticWaves();
            var wave = waves.Factions[0].Waves[0];
            wave.Rows.Add(new WaveDifficultyRow { Difficulty = @"DO_Level3", RawValue = ShippedRow.Replace(@"MinCount=2", @"MinCount=1") });

            var result = ProjectCompiler.Compile(ProjectWithEdit(EditedSlots()), SyntheticInputs(waves));

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"2 rows for this difficulty", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// A section the merged config does not have is refused rather than guessed at.
        ///
        /// Without the section there is no way to know which coalesced sub-file to write into, and a
        /// section written into the wrong sub-file merges into a file the game does not read for it.
        /// The mod builds, installs, and does nothing.
        /// </summary>
        [TestMethod]
        public void AnUnknownCoalescedSubFileIsRefused()
        {
            var result = ProjectCompiler.Compile(
                ProjectWithEdit(EditedSlots()),
                new CompileInputs { Waves = SyntheticWaves(), Sections = EmptyIndex() });

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"coalesced sub-file", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        #endregion

        #region Against a real install

        /// <summary>
        /// The subtraction carries the INSTALL'S OWN BYTES, not a reserialisation of them.
        ///
        /// This is the highest-consequence assertion in the compiler. `ConfigMerge` compares removal
        /// text with `==` and nothing else, so a rewritten float, a lost tab or one different space
        /// makes the subtraction a no-op - and the game then holds both the shipped row and the user's,
        /// for the same difficulty. Nothing throws, at any point.
        /// </summary>
        [TestMethod]
        public void TheRemovalTextIsTheInstallsOwnBytes()
        {
            var waves = CachedWaves();
            if (waves == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var (faction, wave, row) = FirstRealRow(waves);
            var edit = FromShipped(faction, wave, row);

            // A change the writer will actually render differently from what shipped.
            var counted = edit.Slots.First(s => !s.IsFiller);
            counted.MinCount = counted.MaxCount = (counted.MaxCount ?? 1) + 1;

            var project = ModMakerProject.Create(@"Real", @"LMP", @"1");
            var document = new WavelistEditDocument();
            document.Set(edit);
            project.WavelistEdits = document;

            var result = ProjectCompiler.Compile(project, new CompileInputs { Waves = waves, Sections = CachedSections() });
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var write = OnlyWrite(result.Manifest);
            Assert.AreEqual(row.RawValue, write.Replace.Remove,
                @"The removal text must be the shipped bytes, byte for byte");
            Assert.AreNotEqual(write.Replace.Remove, write.Replace.Add, @"A written row must differ from the one it replaces");
        }

        /// <summary>
        /// Edits are written into the coalesced sub-file their section actually lives in.
        ///
        /// A mod's coalesced merges over the game's file by file. BioGame's sections do not reach
        /// BioWeapon, so a section put in the wrong sub-file merges into a file nothing reads it from.
        /// The sub-file is data - it is read from the merged config, never inferred from the name.
        /// </summary>
        [TestMethod]
        public void EditsAreWrittenIntoTheSubFileTheirSectionLivesIn()
        {
            var waves = CachedWaves();
            if (waves == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var sections = CachedSections();
            var (faction, wave, row) = FirstRealRow(waves);
            var edit = FromShipped(faction, wave, row);
            var counted = edit.Slots.First(s => !s.IsFiller);
            counted.MinCount = counted.MaxCount = (counted.MaxCount ?? 1) + 1;

            var project = ModMakerProject.Create(@"Real", @"LMP", @"1");
            var document = new WavelistEditDocument();
            document.Set(edit);
            project.WavelistEdits = document;

            var result = ProjectCompiler.Compile(project, new CompileInputs { Waves = waves, Sections = sections });
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var job = (CoalescedJob)result.Manifest.Jobs.Single();
            var file = job.Files.Single();

            Assert.AreEqual(sections.AssetOf(wave.SectionKey), file.Key);
            Assert.AreEqual(wave.SectionKey, file.Value.Sections.Single().Name);
            Console.WriteLine($@"{wave.SectionKey} -> {file.Key}");

            // The property is spelled as the install spells it, which is what makes the generated file
            // diffable by hand against the one it merges over.
            var property = file.Value.Sections.Single().Properties.Single().Name;
            Assert.IsTrue(property.Equals(@"enemies", StringComparison.OrdinalIgnoreCase), property);
        }

        /// <summary>
        /// A ModMaker DLC loads in multiplayer, and by default lets its owner into a lobby with players
        /// who do not have it.
        ///
        /// Both halves are decisions rather than measurements, and the second is the one that shows.
        /// Config edits DO desync host and clients - a changed wave is something both machines
        /// simulate - but not usually harmfully, and a mod nobody can play without their whole lobby
        /// installing it is a much larger cost. Projects that cannot tolerate the desync turn it off,
        /// and that setting lives in the project rather than in the compile.
        /// </summary>
        [TestMethod]
        public void MountFlagsSayWhereTheModLoadsAndWhoItMatchesWith()
        {
            Assert.AreEqual((int)(EME3MountFileFlag.LoadsInMultiplayer | EME3MountFileFlag.DLCSetupCanMisMatch),
                InstallMount.Flags(requireAllPlayers: false));

            Assert.AreEqual((int)EME3MountFileFlag.LoadsInMultiplayer,
                InstallMount.Flags(requireAllPlayers: true));

            // Never singleplayer, and never a save-file dependency: a config mod that marks saves
            // would follow the player out of multiplayer entirely.
            foreach (var required in new[] { true, false })
            {
                var flags = (EME3MountFileFlag)InstallMount.Flags(required);
                Assert.IsFalse(flags.HasFlag(EME3MountFileFlag.LoadsInSingleplayer));
                Assert.IsFalse(flags.HasFlag(EME3MountFileFlag.SaveFileDependency));
            }
        }

        /// <summary>
        /// The project's setting reaches the manifest, and reaches it from the PROJECT.
        ///
        /// A caller could pass flags of its own, and the window deliberately does not: whether every
        /// player needs the mod is something the author decided and saved, not a property of the
        /// machine the compile happened on.
        /// </summary>
        [TestMethod]
        public void RequiringAllPlayersReachesTheMount()
        {
            var waves = SyntheticWaves();
            var project = ProjectWithEdit(EditedSlots());

            Assert.IsFalse(project.RequireAllPlayers, @"Matching with everyone is the default");
            Assert.AreEqual(InstallMount.Flags(false),
                ProjectCompiler.Compile(project, SyntheticInputs(waves)).Manifest.Mod.Mount.Flags);

            project.RequireAllPlayers = true;
            Assert.AreEqual(InstallMount.Flags(true),
                ProjectCompiler.Compile(project, SyntheticInputs(waves)).Manifest.Mod.Mount.Flags);

            // Stored only when it is on, so a project that never touched the setting stays sparse and
            // would follow a future change of default rather than pinning today's.
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ModMakerProject.FileExtension);
            try
            {
                project.Save(path);
                Assert.IsTrue(ModMakerProject.Load(path).RequireAllPlayers);

                project.RequireAllPlayers = false;
                project.Save(path);
                Assert.IsNull(ModMakerProject.Load(path).Document[@"meta"][@"requireAllPlayers"]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region Helpers

        private const string ShippedRow =
            @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_A""),(EnemyType=""WAVE_B"", MinCount=2, MaxCount=2)))";

        private const string SyntheticSection = @"Pkg.SFXWave_Horde_Test";
        private const string SyntheticSectionKey = @"test1 pkg.sfxwave_horde_test";

        private static BuildManifest NewManifest() => new()
        {
            Game = @"LMP",
            Mod = new BuildMod { Name = @"x", DlcFolder = ModMakerCatalogue.CompilerDLCFolderName },
        };

        private static PropertyWrite OnlyWrite(BuildManifest manifest) =>
            ((CoalescedJob)manifest.Jobs.Single()).Files.Single().Value.Sections.Single().Properties.Single();

        private static List<WavelistSlotEdit> ShippedSlots() => new()
        {
            new WavelistSlotEdit { EnemyType = @"WAVE_A" },
            new WavelistSlotEdit { EnemyType = @"WAVE_B", MinCount = 2, MaxCount = 2 },
        };

        private static List<WavelistSlotEdit> EditedSlots() => new()
        {
            new WavelistSlotEdit { EnemyType = @"WAVE_A" },
            new WavelistSlotEdit { EnemyType = @"WAVE_B", MinCount = 3, MaxCount = 3 },
        };

        private static ModMakerProject ProjectWithEdit(List<WavelistSlotEdit> slots)
        {
            var project = ModMakerProject.Create(@"Synthetic", @"LMP", @"1");
            var document = new WavelistEditDocument();
            document.Set(new WavelistRowEdit
            {
                Section = SyntheticSection,
                InstanceId = @"test1",
                WaveIndex = 1,
                Difficulty = @"DO_Level3",
                Slots = slots,
            });
            project.WavelistEdits = document;
            return project;
        }

        #region The generic value surface

        private const string ValueSection = @"sfxgame.sfxgamemodebase";
        private const string ValueProperty = @"CoolDownTime";

        /// <summary>A section holding <see cref="ValueProperty"/> set to the given values, in BioGame.</summary>
        private static SectionIndex ValueIndex(params string[] values)
        {
            var property = new CoalesceProperty(ValueProperty);
            foreach (var value in values) property.Add(new CoalesceValue(value, CoalesceParseAction.New));

            var section = new CoalesceSection { { ValueProperty, property } };

            return new SectionIndex(
                new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase) { [ValueSection] = section },
                new List<string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ValueSection] = @"BioGame" });
        }

        /// <summary>
        /// A section that exists and is in BioGame, but does not set <see cref="ValueProperty"/> - so
        /// the property's value comes from the class's compiled default, and only the class knows how
        /// it is spelled.
        /// </summary>
        private static CompileInputs UnsetPropertyInputs()
        {
            var other = new CoalesceProperty(@"SomethingElse");
            other.Add(new CoalesceValue(@"1", CoalesceParseAction.New));
            var section = new CoalesceSection { { @"SomethingElse", other } };

            var declaring = new CatalogueClass { Name = @"SFXGameModeBase", Section = ValueSection };
            declaring.OwnProperties.Add(new CatalogueProperty { Name = ValueProperty, DeclaredType = @"FloatProperty" });

            return new CompileInputs
            {
                Sections = new SectionIndex(
                    new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase) { [ValueSection] = section },
                    new List<string>(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ValueSection] = @"BioGame" }),
                Classes = new Dictionary<string, CatalogueClass>(StringComparer.OrdinalIgnoreCase)
                {
                    [declaring.Name] = declaring,
                },
                MountPriority = 4800,
            };
        }

        private static List<PropertyWrite> ValueWrites(CompileResult result) =>
            result.Manifest.Coalesced().File(@"BioGame").Section(ValueSection).Properties;

        private static CompileResult CompileValue(string leafId, JToken value, params string[] shipped)
        {
            var project = ModMakerProject.Create(@"Values", @"LMP", @"1");
            project.Values[$@"{ValueSection}/{leafId}"] = value;

            return ProjectCompiler.Compile(project, new CompileInputs
            {
                Sections = ValueIndex(shipped),
                MountPriority = 4800,
            });
        }

        /// <summary>A scalar property is simply overwritten - parse action 0, the whole value.</summary>
        [TestMethod]
        public void AScalarLeafIsWrittenAsAPlainOverwrite()
        {
            var result = CompileValue(@"cooldowntime", 250, @"100");

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            var writes = ValueWrites(result);
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(ValueProperty, writes[0].Name, @"The install's own spelling, not the key's");
            Assert.AreEqual(@"250", writes[0].Value);
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.New), writes[0].ValueType);
            Assert.AreEqual(1, result.ValuePropertiesWritten);
        }

        /// <summary>
        /// A leaf inside a struct is SPLICED into the shipped text, not reserialised out of the tree.
        ///
        /// The fixture keeps BioWare's own spelling: literal backslash-t padding after a comma, and a
        /// space either side of one '=' but not the other. Every byte of that has to survive an edit
        /// to a different field. Regenerating the text instead would "tidy" all of it, changing values
        /// nobody edited and - because array subtraction elsewhere matches on exact text - breaking
        /// later edits silently.
        /// </summary>
        [TestMethod]
        public void AStructLeafIsSplicedRatherThanReserialised()
        {
            const string shipped = @"(BaseValue=6.0,\t\tFormula=DivideByBonusSum,RankBonuses=(0.0,0.25))";

            var result = CompileValue(@"cooldowntime.basevalue", 4.5, shipped);

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            Assert.AreEqual(
                @"(BaseValue=4.5,\t\tFormula=DivideByBonusSum,RankBonuses=(0.0,0.25))",
                ValueWrites(result)[0].Value);
        }

        /// <summary>The unquoted token stays a literal: there is no way to know what is legal in it.</summary>
        [TestMethod]
        public void AnUnquotedTokenCannotBeEdited()
        {
            var result = CompileValue(@"cooldowntime.formula", @"SomethingElse",
                @"(BaseValue=6.0,Formula=DivideByBonusSum)");

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"not an editable leaf", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// A property the config sets more than once is replaced wholesale: cleared, then re-added.
        ///
        /// The entries nobody touched come back byte-for-byte from the shipped text - including the
        /// second one's spaces around '=' and the first one's tab padding - because they are copied,
        /// not regenerated. See docs/02-data-model.md §0.1.
        /// </summary>
        [TestMethod]
        public void AnArrayIsReplacedWholesaleWithUntouchedEntriesVerbatim()
        {
            const string first = @"(Difficulty=DO_Level1,\t\tWaveCost=25)";
            const string second = @"(Difficulty=DO_Level2,WaveCost = 40)";
            const string third = @"(Difficulty=DO_Level3,WaveCost=60)";

            var result = CompileValue(@"cooldowntime.1.wavecost", 55, first, second, third);

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            var writes = ValueWrites(result);
            Assert.AreEqual(4, writes.Count, @"one clear, then one add per entry");

            // The clear carries no value: RemoveProperty drops the whole property by name.
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.RemoveProperty), writes[0].ValueType);
            Assert.AreEqual(string.Empty, writes[0].Value);

            // AddUnique, because this mod is a DLC and that is what shipped DLC uses for an array
            // addition; Add is the basegame coalesced's spelling.
            Assert.IsTrue(writes.Skip(1).All(w => w.ValueType == CoalesceValue.GetValueType(CoalesceParseAction.AddUnique)),
                @"array entries are added the way a DLC adds them");
            Assert.AreEqual(first, writes[1].Value, @"untouched entries must be byte-identical");
            Assert.AreEqual(@"(Difficulty=DO_Level2,WaveCost = 55)", writes[2].Value);
            Assert.AreEqual(third, writes[3].Value, @"untouched entries must be byte-identical");
        }

        /// <summary>
        /// Entry ids are positional, so an array that changed length underneath a project is refused.
        ///
        /// Writing entry 7 into a three-entry array by picking the nearest one would put the user's
        /// edit somewhere they did not ask for, and it would install cleanly.
        /// </summary>
        [TestMethod]
        public void AnArrayEntryThatIsNoLongerThereIsRefused()
        {
            var result = CompileValue(@"cooldowntime.7.wavecost", 55,
                @"(WaveCost=25)", @"(WaveCost=40)", @"(WaveCost=60)");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Manifest);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"positional", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// Setting a value to what it already is writes nothing.
        ///
        /// Not an error - a user may well type the shipped number - but a property rewritten to its
        /// own current text is a change with nothing in it, and it would pin the value against a
        /// later Milan release for no reason.
        /// </summary>
        [TestMethod]
        public void AnEditThatMatchesTheShippedValueIsNotWritten()
        {
            var result = CompileValue(@"cooldowntime.basevalue", 6.0, @"(BaseValue=6.0,Formula=Divide)");

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            Assert.AreEqual(0, result.ValuePropertiesWritten);
            Assert.AreEqual(1, result.UnchangedEditsSkipped);
            Assert.AreEqual(0, result.RowsWritten);
        }

        /// <summary>
        /// Two edits inside one struct are ONE write.
        ///
        /// Emitted as two, the second would overwrite the first - parse action 0 replaces the value -
        /// and the user would lose an edit with nothing to tell them.
        /// </summary>
        [TestMethod]
        public void TwoLeavesOfOneStructBecomeASingleWrite()
        {
            var project = ModMakerProject.Create(@"Values", @"LMP", @"1");
            project.Values[$@"{ValueSection}/cooldowntime.basevalue"] = 4.5;
            project.Values[$@"{ValueSection}/cooldowntime.rankbonuses.1"] = 0.5;

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Sections = ValueIndex(@"(BaseValue=6.0,RankBonuses=(0.0,0.25))"),
                MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            var writes = ValueWrites(result);
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(@"(BaseValue=4.5,RankBonuses=(0.0,0.5))", writes[0].Value);
            Assert.AreEqual(1, result.ValuePropertiesWritten, @"one property written, not one per leaf");
        }

        /// <summary>
        /// A property the config never sets can still be set: its value comes from the class's
        /// compiled default, and there is no shipped text to splice into because the leaf IS the
        /// whole property. Refusing these would put most of the scalar surface out of reach.
        /// </summary>
        [TestMethod]
        public void APropertyTheConfigNeverSetsCanStillBeSet()
        {
            var project = ModMakerProject.Create(@"Values", @"LMP", @"1");
            project.Values[$@"{ValueSection}/cooldowntime"] = 12;

            var result = ProjectCompiler.Compile(project, UnsetPropertyInputs());

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            var writes = ValueWrites(result);
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(@"12", writes[0].Value);
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.New), writes[0].ValueType);

            // The class's declared spelling, which is the only place it exists - the section that
            // would normally carry it does not set this property at all.
            Assert.AreEqual(ValueProperty, writes[0].Name);
        }

        /// <summary>
        /// ...but only the property itself. A leaf inside a structure the config never wrote down has
        /// no text to be spliced into, and inventing the structure around it would be a guess.
        /// </summary>
        [TestMethod]
        public void ALeafInsideAPropertyTheConfigNeverSetsIsRefused()
        {
            var project = ModMakerProject.Create(@"Values", @"LMP", @"1");
            project.Values[$@"{ValueSection}/cooldowntime.basevalue"] = 12.0;

            var result = ProjectCompiler.Compile(project, UnsetPropertyInputs());

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(p => p.Message.Contains(@"not a value inside it", StringComparison.Ordinal)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// The two value forms the schema allows and this build does not compile are NAMED.
        ///
        /// Both would otherwise fall through to a confusing failure - a dimension suffix as a missing
        /// leaf, a range control as "not a number" - and a refusal nobody can act on is only marginally
        /// better than a silent omission.
        /// </summary>
        [TestMethod]
        public void TheValueFormsThisBuildCannotCompileSayWhichTheyAre()
        {
            var dimensioned = CompileValue(@"cooldowntime.basevalue[do_level3]", 4.5, @"(BaseValue=6.0)");
            Assert.IsFalse(dimensioned.Succeeded);
            Assert.IsTrue(dimensioned.Errors.Any(p => p.Message.Contains(@"dimension", StringComparison.Ordinal)),
                string.Join(@" | ", dimensioned.Problems));

            var ranged = CompileValue(@"cooldowntime.basevalue",
                new JObject { [@"min"] = 1.0, [@"max"] = 9.0 }, @"(BaseValue=6.0)");
            Assert.IsFalse(ranged.Succeeded);
            Assert.IsTrue(ranged.Errors.Any(p => p.Message.Contains(@"range control", StringComparison.Ordinal)),
                string.Join(@" | ", ranged.Problems));
        }

        /// <summary>
        /// The value tree and the spans that locate its leaves in the text cannot drift apart.
        ///
        /// Both come out of one walk precisely so they cannot, and this is the assertion that the walk
        /// still deserves that trust - run over every value in the installed config rather than over a
        /// fixture, because the shapes that break a parser are BioWare's, not any I would invent.
        ///
        /// Drift here does not throw. It writes an edit into the wrong span of the right value, which
        /// compiles, installs, and corrupts a struct.
        /// </summary>
        [TestMethod]
        public void EveryLeafSpanLandsOnItsOwnLeafAcrossTheInstalledConfig()
        {
            var sections = CachedSections();
            if (sections == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            int valuesWalked = 0, leavesFound = 0;
            var problems = new List<string>();

            foreach (var (sectionKey, section) in sections.ByKey)
            {
                foreach (var property in section)
                {
                    var live = MergedGameConfig.Live(property.Value).Select(v => v.Value).ToList();
                    for (int i = 0; i < live.Count; i++)
                    {
                        var text = live[i];
                        if (text == null) continue;

                        var lower = property.Key.ToLowerInvariant();
                        var idBase = live.Count == 1 ? lower : $@"{lower}.{i}";
                        valuesWalked++;

                        var spans = ValueTreeBuilder.Spans(text, idBase);
                        leavesFound += spans.Count;

                        var where = $@"[{sectionKey}] {property.Key}[{i}]";

                        // The two halves of the walk name exactly the same leaves.
                        var fromTree = LeafIds(ValueTreeBuilder.Build(text, idBase));
                        if (!fromTree.SetEquals(spans.Keys))
                            problems.Add($@"{where}: tree ids and span ids differ");

                        // And every span is inside the text, and no two of them overlap - so splicing
                        // one leaf can never disturb another.
                        var ordered = spans.Values.OrderBy(s => s.Start).ToList();
                        var previousEnd = 0;
                        foreach (var span in ordered)
                        {
                            if (span.Start < previousEnd || span.End > text.Length || span.End <= span.Start)
                            {
                                problems.Add($@"{where}: span {span.Start}..{span.End} is out of order or out of bounds");
                                break;
                            }
                            previousEnd = span.End;
                        }
                    }
                }
            }

            Console.WriteLine($@"values walked: {valuesWalked}, editable leaves located: {leavesFound}");
            Assert.IsTrue(valuesWalked > 1000, $@"Only {valuesWalked} values were walked; the config did not load");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems.Take(10)));
        }

        private static HashSet<string> LeafIds(JToken node)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CollectLeafIds(node, ids);
            return ids;
        }

        private static void CollectLeafIds(JToken node, HashSet<string> ids)
        {
            if (node is not JObject o) return;

            switch ((string)o[@"kind"])
            {
                case @"struct":
                    foreach (var member in (JArray)o[@"members"]) CollectLeafIds(member[@"value"], ids);
                    break;
                case @"array":
                    foreach (var item in (JArray)o[@"items"]) CollectLeafIds(item, ids);
                    break;
                case @"literal":
                    break;
                default:
                    ids.Add((string)o[@"id"]);
                    break;
            }
        }

        #endregion

        #region Weapons

        private static WeaponCatalogue CachedWeaponCatalogue()
        {
            lock (CacheLock)
            {
                if (_weaponCatalogue != null) return _weaponCatalogue;
                var target = TryGetLMPTarget();
                if (target == null) return null;

                var classes = ClassScanner.Scan(target);
                var config = MergedGameConfig.Load(target);
                _sections ??= config.BuildSectionIndex();
                return _weaponCatalogue = WeaponCatalogue.Build(config, classes);
            }
        }

        /// <summary>
        /// A weapon stat edit is an overwrite on the WEAPON's own section, never on the ancestor the
        /// value was inherited from.
        ///
        /// The failure this guards against is not a crash. Writing to the ancestor compiles, installs
        /// and changes every weapon under it - editing one SMG's damage would change every SMG in the
        /// game - and nothing about the built mod would say so.
        /// </summary>
        [TestMethod]
        public void AWeaponStatIsWrittenToTheWeaponNotToWhatItInheritedFrom()
        {
            var weapons = CachedWeaponCatalogue();
            if (weapons == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var hornet = weapons.Find(@"SFXGameContent.SFXWeapon_SMG_Hornet");
            Assert.IsNotNull(hornet, @"The Hornet must be readable");

            var project = ModMakerProject.Create(@"Weapons", @"LMP", @"1");
            var edits = new WeaponEditDocument();
            edits.Values[WeaponEditDocument.Key(hornet.Section, @"Damage")] = new WeaponEdit
            {
                Section = hornet.Section, Property = @"Damage", Value = @"99.0",
            };
            project.Document[WeaponEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Weapons = weapons, Sections = CachedSections(), MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var asset = CachedSections().AssetOf(hornet.Section);
            var writes = result.Manifest.Coalesced().File(asset).Section(hornet.Section).Properties;
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(@"Damage", writes[0].Name);
            Assert.AreEqual(@"99.0", writes[0].Value);
            Assert.AreEqual(1, result.WeaponWritesWritten);

            // Nothing was written to the base the Hornet inherits from.
            Assert.AreNotEqual(hornet.Section, hornet.BaseSection);
            var baseAsset = CachedSections().AssetOf(hornet.BaseSection);
            if (baseAsset != null)
            {
                Assert.AreEqual(0, result.Manifest.Coalesced().File(baseAsset).Section(hornet.BaseSection).Properties.Count,
                    @"An edit must never land on the class the value was inherited from");
            }
        }

        /// <summary>Weapon edits without the installed game are refused rather than guessed at.</summary>
        [TestMethod]
        public void WeaponEditsWithoutTheInstalledGameAreRefused()
        {
            var project = ModMakerProject.Create(@"Weapons", @"LMP", @"1");
            var edits = new WeaponEditDocument();
            edits.Values[WeaponEditDocument.Key(@"X.Y", @"Damage")] = new WeaponEdit
            {
                Section = @"X.Y", Property = @"Damage", Value = @"1",
            };
            project.Document[WeaponEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs { MountPriority = 4800 });

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Problems.Any(p => p.ToString().Contains(@"weapons", StringComparison.OrdinalIgnoreCase)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>
        /// Changing a real stat without its stat bar warns, because the loadout screen will go on
        /// drawing the old number and nothing in the game keeps the two in step.
        /// </summary>
        [TestMethod]
        public void ChangingDamageWithoutItsStatBarWarns()
        {
            var weapons = CachedWeaponCatalogue();
            if (weapons == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var withBar = weapons.PlayerWeapons.FirstOrDefault(w => w.Own.ContainsKey(@"StatBarDamage")
                                                                   && w.Own.ContainsKey(@"Damage"));
            if (withBar == null) { Assert.Inconclusive(@"No weapon sets both Damage and StatBarDamage."); return; }

            var edits = new WeaponEditDocument();
            edits.Values[WeaponEditDocument.Key(withBar.Section, @"Damage")] = new WeaponEdit
            {
                Section = withBar.Section, Property = @"Damage", Value = @"123",
            };

            var problems = WeaponEditValidator.Validate(edits, weapons);
            Assert.IsTrue(problems.Any(p => p.Severity == WeaponProblemSeverity.Warning
                                            && p.Message.Contains(@"StatBarDamage")),
                $@"Expected a stat-bar warning; got: {string.Join(@" | ", problems)}");

            // ...and setting the bar too silences it, rather than the warning being unconditional.
            edits.Values[WeaponEditDocument.Key(withBar.Section, @"StatBarDamage")] = new WeaponEdit
            {
                Section = withBar.Section, Property = @"StatBarDamage", Value = @"(X=123)",
            };
            Assert.IsFalse(WeaponEditValidator.Validate(edits, weapons)
                    .Any(p => p.Message.Contains(@"StatBarDamage")),
                @"Setting the bar as well must clear the warning");
        }

        #endregion

        #region Objectives

        /// <summary>
        /// An objective value edit is an ordinary overwrite, written into the section the EDIT names
        /// rather than one derived here.
        ///
        /// Against the install, because the thing that can go wrong is not the shape of the write but
        /// its destination: an objective's rules live in two different sections depending on whether
        /// they are the family's or the variant's, and only the merged config knows which coalesced
        /// sub-file each of those sits in. Getting that wrong builds a mod that installs and does
        /// nothing at all.
        /// </summary>
        [TestMethod]
        public void AnObjectiveValueIsWrittenIntoTheSectionTheEditNames()
        {
            var objectives = CachedObjectives();
            if (objectives == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var family = objectives.Families.First(f => f.Shared.ContainsKey(@"numdisarmsrequired"));
            var section = family.ClassSection;

            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Values[ObjectiveEditDocument.Key(section, @"numdisarmsrequired")] = new ObjectiveEdit
            {
                Section = section,
                Property = @"numdisarmsrequired",
                Value = @"6",
            };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Objectives = objectives,
                Sections = CachedSections(),
                MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var asset = CachedSections().AssetOf(section);
            Assert.IsNotNull(asset, @"The objective's section must resolve to a coalesced sub-file");

            var writes = result.Manifest.Coalesced().File(asset).Section(section).Properties;
            Assert.AreEqual(1, writes.Count);
            Assert.AreEqual(@"numdisarmsrequired", writes[0].Name);
            Assert.AreEqual(@"6", writes[0].Value);
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.New), writes[0].ValueType,
                @"A rule value is an overwrite, not an addition to a list");
            Assert.AreEqual(1, result.ObjectiveWritesWritten);
        }

        /// <summary>
        /// Taking an objective off a wave rewrites the WHOLE array rather than removing one entry.
        ///
        /// It has to. OperationWaves is written twice over in the shipped config, so there is no
        /// single value to subtract, and a Remove that matched only one of the two would leave the
        /// objective still on the wave. The array is cleared once and rebuilt instead - and the waves
        /// nobody touched must come back with the game's own text, duplicates included, because those
        /// duplicates are what the running game actually has.
        /// </summary>
        [TestMethod]
        public void TakingAnObjectiveOffAWaveRebuildsTheWholeArray()
        {
            var objectives = CachedObjectives();
            if (objectives == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Waves[@"6"] = new ObjectiveWaveEdit { WaveIndex = 6, Enabled = false };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Objectives = objectives,
                Sections = CachedSections(),
                MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var section = objectives.CoordinatorSection;
            var writes = result.Manifest.Coalesced().File(CachedSections().AssetOf(section)).Section(section).Properties
                .Where(w => w.Name == ObjectiveCatalogue.OperationWavesProperty).ToList();

            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.RemoveProperty), writes[0].ValueType,
                @"The array must be cleared before it is rebuilt, or the shipped entries survive");
            Assert.IsTrue(writes.Skip(1).All(w => w.ValueType == CoalesceValue.GetValueType(CoalesceParseAction.AddUnique)),
                @"Every rebuilt entry is an AddUnique, matching the DLC convention");

            var entries = writes.Skip(1).Select(w => w.Value).ToList();
            var expected = objectives.Waves.Where(w => w.WaveIndex != 6).Select(w => w.Raw).ToList();
            CollectionAssert.AreEqual(expected, entries,
                @"Every wave that was not turned off keeps the game's own text, duplicates and all");
            Assert.IsFalse(entries.Any(e => e.Contains(@"WaveNumber=5")), @"Wave 6 is zero-based WaveNumber 5");
        }

        /// <summary>
        /// A changed credit scale replaces that wave's entry and leaves every other one alone, still
        /// in the game's own words. Rendering an untouched entry ourselves would silently normalise
        /// text nobody asked us to touch.
        /// </summary>
        [TestMethod]
        public void ChangingACreditScaleRewritesOnlyThatWavesEntry()
        {
            var objectives = CachedObjectives();
            if (objectives == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Waves[@"3"] = new ObjectiveWaveEdit { WaveIndex = 3, Enabled = true, CreditScale = 0.4 };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Objectives = objectives,
                Sections = CachedSections(),
                MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var section = objectives.CoordinatorSection;
            var entries = result.Manifest.Coalesced().File(CachedSections().AssetOf(section)).Section(section).Properties
                .Where(w => w.Name == ObjectiveCatalogue.OperationWavesProperty
                            && w.ValueType == CoalesceValue.GetValueType(CoalesceParseAction.AddUnique))
                .Select(w => w.Value).ToList();

            Assert.IsTrue(entries.Contains(@"(WaveNumber=2, CreditScale=0.4)"),
                $@"Wave 3's entry must carry the new scale; got {string.Join(@" ", entries)}");
            Assert.IsFalse(entries.Any(e => e.Contains(@"CreditScale=0.15")), @"...and the shipped one must be gone");

            foreach (var untouched in objectives.Waves.Where(w => w.WaveIndex != 3))
                Assert.IsTrue(entries.Contains(untouched.Raw), $@"'{untouched.Raw}' must survive verbatim");
        }

        /// <summary>
        /// Objective edits without the game they were made against are refused rather than guessed at.
        /// There is nothing here to resolve a section into a coalesced sub-file, and a manifest built
        /// on a guess produces a mod that installs cleanly and changes nothing.
        /// </summary>
        [TestMethod]
        public void ObjectiveEditsWithoutTheInstalledGameAreRefused()
        {
            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Waves[@"6"] = new ObjectiveWaveEdit { WaveIndex = 6, Enabled = false };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs { MountPriority = 4800 });

            Assert.IsFalse(result.Succeeded, @"Objective edits with no catalogue must not compile");
            Assert.IsTrue(result.Problems.Any(p => p.ToString().Contains(@"objectives", StringComparison.OrdinalIgnoreCase)),
                string.Join(@" | ", result.Problems));
        }

        /// <summary>A project with no objective edits writes nothing at all - not an empty array.</summary>
        [TestMethod]
        public void AProjectWithNoObjectiveEditsWritesNoObjectives()
        {
            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var result = ProjectCompiler.Compile(project, new CompileInputs { MountPriority = 4800 });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
            Assert.AreEqual(0, result.ObjectiveWritesWritten);
        }

        /// <summary>
        /// A wave the shipped array does not name can be given an objective, and it lands in wave
        /// order rather than after the extraction.
        ///
        /// Order is not cosmetic here: the array is what a person reads when a mod misbehaves, and an
        /// entry for wave 4 sitting after wave 11 reads as a mistake even when it works.
        /// </summary>
        [TestMethod]
        public void AnObjectiveCanBeAddedToAWaveThatHasNone()
        {
            var objectives = CachedObjectives();
            if (objectives == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Waves[@"4"] = new ObjectiveWaveEdit { WaveIndex = 4, Enabled = true, CreditScale = 0.2 };
            edits.Waves[@"8"] = new ObjectiveWaveEdit { WaveIndex = 8, Enabled = true };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Objectives = objectives,
                Sections = CachedSections(),
                MountPriority = 4800,
            });

            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

            var section = objectives.CoordinatorSection;
            var entries = result.Manifest.Coalesced().File(CachedSections().AssetOf(section)).Section(section).Properties
                .Where(w => w.Name == ObjectiveCatalogue.OperationWavesProperty
                            && w.ValueType == CoalesceValue.GetValueType(CoalesceParseAction.AddUnique))
                .Select(w => w.Value).ToList();

            // Back to the config's zero-based numbering, and a wave with no scale writes no scale.
            Assert.IsTrue(entries.Contains(@"(WaveNumber=3, CreditScale=0.2)"),
                $@"Wave 4 must be added as WaveNumber 3; got {string.Join(@" ", entries)}");
            Assert.IsTrue(entries.Contains(@"(WaveNumber=7)"),
                $@"Wave 8 must be added with no credit scale; got {string.Join(@" ", entries)}");

            var numbers = entries.Select(WaveNumberOf).ToList();
            var sorted = numbers.OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(sorted, numbers, @"Entries must be written in wave order");

            foreach (var untouched in objectives.Waves)
                Assert.IsTrue(entries.Contains(untouched.Raw), $@"'{untouched.Raw}' must survive verbatim");
        }

        /// <summary>
        /// Duplicate entries for the same wave keep the order the config had them in.
        ///
        /// Which of two entries for one wave the game honours is not established, so reordering them
        /// could change behaviour in a way nothing here would detect. Sorting by wave has to be
        /// stable for that reason, not for tidiness.
        /// </summary>
        [TestMethod]
        public void DuplicateWaveEntriesKeepTheirShippedOrder()
        {
            var objectives = CachedObjectives();
            if (objectives == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var duplicated = objectives.Waves.GroupBy(w => w.WaveIndex).FirstOrDefault(g => g.Count() > 1
                && g.Select(w => w.Raw).Distinct().Count() > 1);
            if (duplicated == null) { Assert.Inconclusive(@"This install has no wave with differing duplicates."); return; }

            var project = ModMakerProject.Create(@"Objectives", @"LMP", @"1");
            var edits = new ObjectiveEditDocument();
            edits.Waves[@"4"] = new ObjectiveWaveEdit { WaveIndex = 4, Enabled = true, CreditScale = 0.2 };
            project.Document[ObjectiveEditDocument.CollectionId] = JObject.Parse(edits.ToJsonString(false));

            var result = ProjectCompiler.Compile(project, new CompileInputs
            {
                Objectives = objectives,
                Sections = CachedSections(),
                MountPriority = 4800,
            });

            var section = objectives.CoordinatorSection;
            var entries = result.Manifest.Coalesced().File(CachedSections().AssetOf(section)).Section(section).Properties
                .Where(w => w.Name == ObjectiveCatalogue.OperationWavesProperty
                            && w.ValueType == CoalesceValue.GetValueType(CoalesceParseAction.AddUnique))
                .Select(w => w.Value).ToList();

            var expected = duplicated.Select(w => w.Raw).ToList();
            var actual = entries.Where(e => expected.Contains(e)).ToList();
            CollectionAssert.AreEqual(expected, actual,
                @"Two entries for one wave must keep the order the config had them in");
        }

        private static int WaveNumberOf(string entry)
        {
            var at = entry.IndexOf(@"WaveNumber=", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return int.MaxValue;
            var rest = entry[(at + 11)..];
            var digits = new string(rest.TakeWhile(char.IsDigit).ToArray());
            return digits.Length > 0 ? int.Parse(digits) : int.MaxValue;
        }

        #endregion

        private static CompileInputs SyntheticInputs(WavelistCatalogue waves) => new()
        {
            Waves = waves,
            Sections = new SectionIndex(
                new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase),
                new List<string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [SyntheticSectionKey] = @"BioGame" }),
            MountPriority = 4800,
        };

        private static SectionIndex EmptyIndex() => new(
            new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase),
            new List<string>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// A catalogue shaped like the real one but small enough to reason about.
        ///
        /// Used for the rules that are about the compiler's own behaviour. Anything that is really an
        /// assertion about the GAME'S data is tested against the install instead, because a fixture
        /// can only ever agree with whoever wrote it.
        /// </summary>
        private static WavelistCatalogue SyntheticWaves()
        {
            var waves = new WavelistCatalogue();
            waves.Enemies.Add(new EnemyDefinition { EnemyType = @"WAVE_A", WaveCost = 10 });
            waves.Enemies.Add(new EnemyDefinition { EnemyType = @"WAVE_B", WaveCost = 20 });
            waves.Budget.Add(new WaveBudgetRange { Difficulty = @"DO_Level3", FirstWavePoints = 600, LastWavePoints = 1800 });

            var faction = new WaveFaction { Section = SyntheticSection, Name = @"SFXWave_Horde_Test", SelectionWeight = 100 };
            var wave = new WaveInstance { InstanceId = @"test1", WaveIndex = 1, SectionKey = SyntheticSectionKey };
            var row = new WaveDifficultyRow { Difficulty = @"DO_Level3", RawValue = ShippedRow };
            row.Slots.Add(new WaveSlot { Index = 0, EnemyType = @"WAVE_A" });
            row.Slots.Add(new WaveSlot { Index = 1, EnemyType = @"WAVE_B", MinCount = 2, MaxCount = 2 });
            wave.Rows.Add(row);
            faction.Waves.Add(wave);
            waves.Factions.Add(faction);
            return waves;
        }

        private static (WaveFaction, WaveInstance, WaveDifficultyRow) FirstRealRow(WavelistCatalogue waves)
        {
            foreach (var faction in waves.Factions)
            foreach (var wave in faction.Waves.Where(w => w.WaveIndex != null && !string.IsNullOrEmpty(w.SectionKey)))
            foreach (var row in wave.Rows)
            {
                // A row with a counted slot, so the edit below has something to change.
                if (row.Slots.Any(s => !s.IsFiller) && !string.IsNullOrEmpty(row.RawValue))
                    return (faction, wave, row);
            }
            throw new InvalidOperationException(@"No usable wave row in the installed game");
        }

        private static WavelistRowEdit FromShipped(WaveFaction faction, WaveInstance wave, WaveDifficultyRow row) => new()
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
                _sections = config.BuildSectionIndex();
                return _waves = WavelistCatalogue.Build(config, classes, instances);
            }
        }

        private static ObjectiveCatalogue CachedObjectives()
        {
            lock (CacheLock)
            {
                if (_objectives != null) return _objectives;

                var target = TryGetLMPTarget();
                if (target == null) return null;

                var classes = ClassScanner.Scan(target);
                var config = MergedGameConfig.Load(target);
                _sections ??= config.BuildSectionIndex();
                return _objectives = ObjectiveCatalogue.Build(config, classes, InstanceCatalogue.Build(config, classes));
            }
        }

        private static SectionIndex CachedSections()
        {
            CachedWaves();
            return _sections;
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

        private static string SchemaFile(string name)
        {
            var directory = Path.GetDirectoryName(typeof(ModMakerCompilerTests).Assembly.Location);
            var candidate = Path.GetFullPath(Path.Combine(
                directory, @"..", @"..", @"..", @"..", @"MassEffectModManagerCore", @"modmaker", @"schema", name));
            Assert.IsTrue(File.Exists(candidate), $@"Schema not found: {candidate}");
            return candidate;
        }

        #endregion
    }
}
