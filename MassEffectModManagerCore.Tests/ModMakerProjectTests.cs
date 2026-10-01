using System;
using System.IO;
using System.Linq;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.project;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// The `.mmproj` project file. No game needed: this is about the document, not about the install.
    /// </summary>
    [TestClass]
    public class ModMakerProjectTests
    {
        public TestContext TestContext { get; set; }

        private string TempFile() =>
            Path.Combine(Path.GetTempPath(), $@"m3modmaker_{Guid.NewGuid():N}{ModMakerProject.FileExtension}");

        /// <summary>
        /// A saved project satisfies the shipped schema.
        ///
        /// Written against the schema rather than against the example, because the example was
        /// authored by hand to match and the writer was not - which is exactly where the two drift.
        /// </summary>
        [TestMethod]
        public void ASavedProjectSatisfiesTheSchema()
        {
            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(@"project.schema.json")));

            var project = ModMakerProject.Create(@"Silver Grind", @"LMP", @"0.0.2-draft");
            project.Values[@"sfxpowercustomaction_pull/power.cooldowntime.base"] = 4.5;
            project.WavelistEdits = OneEdit();

            var path = TempFile();
            try
            {
                project.Save(path);
                var saved = JObject.Parse(File.ReadAllText(path));

                Assert.IsTrue(saved.IsValid(schema, out System.Collections.Generic.IList<string> errors),
                    string.Join(Environment.NewLine, errors));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// `LMP` is a legal bundle game.
        ///
        /// The same mistake as in definitions.schema.json, found the same way: the enum listed only
        /// LE3, and Milan's identity is MEGame.LMP. A project generated against Milan - which is every
        /// project - would have failed validation while looking entirely correct.
        /// </summary>
        [TestMethod]
        public void TheSchemaAcceptsMilansGameIdentity()
        {
            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(@"project.schema.json")));
            var project = ModMakerProject.Create(@"Milan project", @"LMP", @"1");

            var path = TempFile();
            try
            {
                project.Save(path);
                Assert.IsTrue(JObject.Parse(File.ReadAllText(path)).IsValid(schema));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Keys this build does not understand survive being opened and saved.
        ///
        /// The schema requires it, and the reason is worth restating: a project written by a newer Mod
        /// Manager, or against a bundle that has since changed, must not be quietly stripped by
        /// someone opening it. Deserialising into typed properties and re-serialising does exactly
        /// that stripping, and the user finds out when their mod compiles without half its edits.
        /// </summary>
        [TestMethod]
        public void UnknownContentSurvivesARoundTrip()
        {
            var path = TempFile();
            try
            {
                var original = ModMakerProject.Create(@"Round trip", @"LMP", @"1");
                original.Values[@"someclass/some.leaf"] = 3;
                original.Document[@"aFutureTopLevelKey"] = @"kept";
                original.Meta[@"aFutureMetaKey"] = 7;
                original.Collections[@"anEditorThatDoesNotExistYet"] = new JObject { [@"payload"] = true };
                original.Save(path);

                // Opened and saved again by a build that models none of those.
                var reopened = ModMakerProject.Load(path);
                reopened.WavelistEdits = OneEdit();
                reopened.Save();

                var final = JObject.Parse(File.ReadAllText(path));
                Assert.AreEqual(@"kept", (string)final[@"aFutureTopLevelKey"]);
                Assert.AreEqual(7, (int)final[@"meta"][@"aFutureMetaKey"]);
                Assert.AreEqual(true, (bool)final[@"collections"][@"anEditorThatDoesNotExistYet"][@"payload"]);
                Assert.AreEqual(3, (int)final[@"values"][@"someclass/some.leaf"]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Timestamps are not rewritten by being read.
        ///
        /// Newtonsoft parses an ISO date into a DateTime by default and writes it back in local time
        /// with a different offset, so merely opening a project would change a field nobody edited -
        /// invisible in the editor, and noise in whatever the user keeps their project in.
        /// </summary>
        [TestMethod]
        public void ReadingAProjectDoesNotRewriteItsTimestamps()
        {
            var path = TempFile();
            try
            {
                var original = ModMakerProject.Create(@"Timestamps", @"LMP", @"1");
                var created = (string)original.Meta[@"created"];
                original.Save(path);

                var reopened = ModMakerProject.Load(path);
                Assert.AreEqual(created, (string)reopened.Meta[@"created"]);

                reopened.Save();

                // Asserted against the file's TEXT, not against a re-parse: JObject.Parse applies
                // Newtonsoft's default date handling and would turn the timestamp back into a DateTime
                // here, testing the test rather than the file.
                StringAssert.Contains(File.ReadAllText(path), created);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>The wavelist collection survives a save and a load with its slots intact.</summary>
        [TestMethod]
        public void WavelistEditsRoundTripThroughTheProject()
        {
            var path = TempFile();
            try
            {
                var project = ModMakerProject.Create(@"Waves", @"LMP", @"1");
                project.WavelistEdits = OneEdit();
                project.Save(path);

                var edits = ModMakerProject.Load(path).WavelistEdits;
                Assert.AreEqual(1, edits.Count);

                var row = edits.Rows.Single();
                Assert.AreEqual(@"Pkg.SFXWave_Horde_Reaper2", row.Section);
                Assert.AreEqual(7, row.WaveIndex);
                Assert.IsTrue(row.Slots[0].IsFiller, @"The filler must survive as a filler");
                Assert.AreEqual(2, row.Slots[1].MaxCount);
                Assert.AreEqual(4, row.Slots[1].MaxPerWave);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// An emptied collection is REMOVED, not written as an empty object.
        ///
        /// The file is a sparse override document: an empty collection is a statement that says
        /// nothing, and leaving one behind makes "the user reverted every wave" look different from
        /// "the user never touched a wave" when they are the same thing.
        /// </summary>
        [TestMethod]
        public void RevertingEveryEditLeavesNoCollectionBehind()
        {
            var project = ModMakerProject.Create(@"Emptied", @"LMP", @"1");
            project.WavelistEdits = OneEdit();
            Assert.IsNotNull(project.Document[@"collections"]);

            project.WavelistEdits = new WavelistEditDocument();
            Assert.IsNull(project.Document[@"collections"], @"An empty collections map should not be written at all");
        }

        /// <summary>
        /// A file that is not a project, and a project from a newer build, are both refused.
        ///
        /// Refused rather than opened empty: an editor that silently shows a blank project over the
        /// user's file is how work gets overwritten.
        /// </summary>
        [TestMethod]
        public void UnreadableProjectsAreRefused()
        {
            var path = TempFile();
            try
            {
                File.WriteAllText(path, @"{""format"": 1, ""kind"": ""something.else""}");
                Assert.Throws<InvalidDataException>(() => ModMakerProject.Load(path));

                File.WriteAllText(path, $@"{{""format"": 99, ""kind"": ""{ModMakerProject.KindString}""}}");
                Assert.Throws<NotSupportedException>(() => ModMakerProject.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static WavelistEditDocument OneEdit()
        {
            var document = new WavelistEditDocument();
            document.Set(new WavelistRowEdit
            {
                Section = @"Pkg.SFXWave_Horde_Reaper2",
                InstanceId = @"sfxwave_horde_reaper7",
                WaveIndex = 7,
                Difficulty = @"DO_Level3",
                Slots =
                {
                    new WavelistSlotEdit { EnemyType = @"WAVE_RPR_Marauder" },
                    new WavelistSlotEdit { EnemyType = @"WAVE_RPR_Banshee", MinCount = 2, MaxCount = 2, MaxPerWave = 4 },
                },
            });
            return document;
        }

        private static string SchemaFile(string name)
        {
            var directory = Path.GetDirectoryName(typeof(ModMakerProjectTests).Assembly.Location);
            var candidate = Path.GetFullPath(Path.Combine(
                directory, @"..", @"..", @"..", @"..", @"MassEffectModManagerCore", @"modmaker", @"schema", name));
            Assert.IsTrue(File.Exists(candidate), $@"Schema not found: {candidate}");
            return candidate;
        }
    }
}
