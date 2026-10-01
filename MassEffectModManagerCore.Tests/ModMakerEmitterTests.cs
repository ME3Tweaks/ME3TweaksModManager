using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmaker.compiler;
using ME3TweaksModManager.modmaker.generator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    /// <summary>
    /// The emission half of the compiler: a build manifest turned into a mod on disk.
    ///
    /// No install needed, and that is worth saying out loud - the emitter reads the manifest and
    /// nothing else, so if any of this ever starts needing a game, something has leaked back into it
    /// that belongs in <see cref="ProjectCompiler"/>.
    ///
    /// The DLC folder is built by ME3TweaksCore's StarterKit, so these do not re-test StarterKit. They
    /// test the parts ModMaker is responsible for: that it asked for the right thing, that the config
    /// it adds lands in the coalesced in the right shape, and that adding it did not destroy what
    /// StarterKit put there.
    /// </summary>
    [TestClass]
    public class ModMakerEmitterTests
    {
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        private const string Section = @"sfxwave_horde_cerberus1 sfxgamecontentdlc_con_mp4_horde.sfxwave_horde_cerberus2";
        private const string ShippedRow = @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_CER_AssaultTrooper""),(EnemyType=""WAVE_CER_Guardian"", MinCount=2, MaxCount=2)))";
        private const string EditedRow = @"(Difficulty=DO_Level3,Enemies=((EnemyType=""WAVE_CER_AssaultTrooper""),(EnemyType=""WAVE_CER_Guardian"", MinCount=4, MaxCount=4)))";

        /// <summary>
        /// The mod has the shape the game and Mod Manager expect, and the mount carries what the
        /// manifest resolved.
        ///
        /// The mount matters more than it looks: LMP writes a different game byte from LE3, the
        /// priority decides whether this DLC's config wins over the content it is overriding, and the
        /// flags decide whether it loads at all in multiplayer. All three are read back off disk here
        /// rather than trusted from the call.
        /// </summary>
        [TestMethod]
        public void AnEmittedModHasTheShapeTheGameExpects()
        {
            var directory = TempDirectory();
            try
            {
                var manifest = ManifestWithOneEdit();
                var result = ModEmitter.Emit(manifest, directory);

                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

                var dlc = Path.Combine(directory, ModMakerCatalogue.CompilerDLCFolderName);
                var cooked = Path.Combine(dlc, MEGame.LMP.CookedDirName());

                Assert.IsTrue(File.Exists(Path.Combine(directory, @"moddesc.ini")), @"No moddesc.ini");
                Assert.IsTrue(Directory.Exists(cooked), @"No cooked directory");
                Assert.IsTrue(File.Exists(result.CoalescedPath), @"No coalesced");
                Assert.IsTrue(Directory.EnumerateFiles(cooked, @"*.tlk").Any(), @"No TLK; the DLC would have no name");

                var mount = new MountFile(Path.Combine(cooked, @"Mount.dlc"));
                Assert.AreEqual(MEGame.LMP, mount.Game, @"An LMP mount is not an LE3 mount - the header byte differs");
                Assert.AreEqual(4801, mount.MountPriority);
                Assert.AreEqual(ModMakerCatalogue.CompilerTlkId, mount.TLKID);
                // 6: loads in multiplayer, and lets its owner match with players who do not have it.
                Assert.AreEqual(6, (int)mount.MountFlags.FlagValue, @"The resolved flags must reach the mount file");
                var flags = (EME3MountFileFlag)mount.MountFlags.FlagValue;
                Assert.IsTrue(flags.HasFlag(EME3MountFileFlag.LoadsInMultiplayer));
                Assert.IsTrue(flags.HasFlag(EME3MountFileFlag.DLCSetupCanMisMatch));
            }
            finally
            {
                Cleanup(directory);
            }
        }

        /// <summary>
        /// The moddesc says what the project says, not what StarterKit's template says.
        ///
        /// StarterKit writes a fixed 1.0, which for something rebuilt on every compile is wrong: a mod
        /// whose version never changes does not look newer than the copy already installed.
        /// </summary>
        [TestMethod]
        public void TheModdescCarriesTheProjectsOwnVersion()
        {
            var directory = TempDirectory();
            try
            {
                var manifest = ManifestWithOneEdit();
                manifest.Mod.Version = @"7";
                var result = ModEmitter.Emit(manifest, directory);
                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

                // Read back through the same parser Mod Manager uses, rather than by string matching:
                // the descriptor names are what matter, not how the ini writer spaces them.
                var moddesc = DuplicatingIni.LoadIni(Path.Combine(directory, @"moddesc.ini"));
                Assert.AreEqual(@"7", moddesc[@"ModInfo"][@"modver"].Value);
                Assert.AreEqual(@"LMP", moddesc[@"ModInfo"][@"game"].Value);
                Assert.AreEqual(@"ModMaker Test Mod", moddesc[@"ModInfo"][@"modname"].Value);
                Assert.AreEqual(ModMakerCatalogue.CompilerDLCFolderName, moddesc[@"CUSTOMDLC"][@"sourcedirs"].Value);
                Assert.AreEqual(ModMakerCatalogue.CompilerDLCFolderName, moddesc[@"CUSTOMDLC"][@"destdirs"].Value);
            }
            finally
            {
                Cleanup(directory);
            }
        }

        /// <summary>
        /// The replacement reaches the coalesced as two values in one property, Remove before Add.
        ///
        /// Order is load-bearing and invisible: applied the other way round the game adds the new row
        /// and then removes it again, leaving the wave exactly as it shipped. Nothing throws, and the
        /// mod looks built.
        /// </summary>
        [TestMethod]
        public void TheReplacementSurvivesIntoTheCoalesced()
        {
            var directory = TempDirectory();
            try
            {
                var result = ModEmitter.Emit(ManifestWithOneEdit(), directory);
                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));
                Assert.AreEqual(1, result.PropertiesWritten);

                var written = ReadBack(directory);
                var asset = written.GetAsset(@"BioGame", createIfNotFound: false);
                Assert.IsNotNull(asset, @"The emitted coalesced has no BioGame at all");
                Assert.IsTrue(asset.Sections.TryGetValue(Section, out var section),
                    @"The edited section is not in the emitted coalesced");

                var values = section[@"enemies"].ToList();
                Assert.AreEqual(2, values.Count, @"A replacement is a subtraction and an addition, not one write");

                Assert.AreEqual(CoalesceParseAction.Remove, values[0].ParseAction);
                Assert.AreEqual(ShippedRow, values[0].Value, @"The subtraction must carry the shipped text unchanged");

                Assert.AreEqual(CoalesceParseAction.AddUnique, values[1].ParseAction);
                Assert.AreEqual(EditedRow, values[1].Value);
            }
            finally
            {
                Cleanup(directory);
            }
        }

        /// <summary>
        /// What StarterKit put in the coalesced is still there afterwards.
        ///
        /// The DLC's own BioEngine section declares its seek-free paths; a DLC without it does not
        /// load its content. Writing a fresh coalesced holding only the mod's edits would produce a
        /// file that looks right and a DLC the game ignores.
        /// </summary>
        [TestMethod]
        public void StarterKitsOwnConfigIsNotOverwritten()
        {
            var directory = TempDirectory();
            try
            {
                var result = ModEmitter.Emit(ManifestWithOneEdit(), directory);
                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Problems));

                var written = ReadBack(directory);
                var engine = written.GetAsset(@"BioEngine", createIfNotFound: false);
                Assert.IsNotNull(engine, @"StarterKit's BioEngine is gone; the DLC would not load its own content");

                var text = string.Join(@" ", engine.Sections.SelectMany(s => s.Value)
                                                   .SelectMany(p => p.Value)
                                                   .Select(v => v.Value ?? string.Empty));
                StringAssert.Contains(text, ModMakerCatalogue.CompilerDLCFolderName,
                    @"BioEngine should name this DLC's own folder");
            }
            finally
            {
                Cleanup(directory);
            }
        }

        /// <summary>
        /// A manifest this build cannot emit is refused before anything is written.
        ///
        /// Refused rather than half-written: a mod folder that exists but is missing its config is
        /// installable, and installing it looks like success.
        /// </summary>
        [TestMethod]
        public void AManifestThisBuildCannotEmitIsRefused()
        {
            var directory = TempDirectory();
            try
            {
                var future = ManifestWithOneEdit();
                future.Format = BuildManifest.CurrentFormat + 1;
                Assert.IsFalse(ModEmitter.Emit(future, directory).Succeeded);

                var wrongGame = ManifestWithOneEdit();
                wrongGame.Game = @"ME2";
                Assert.IsFalse(ModEmitter.Emit(wrongGame, directory).Succeeded);

                var nameless = ManifestWithOneEdit();
                nameless.Mod.Name = null;
                Assert.IsFalse(ModEmitter.Emit(nameless, directory).Succeeded);

                Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(directory).Count(),
                    @"A refused emit must not leave a half-built mod behind");
            }
            finally
            {
                Cleanup(directory);
            }
        }

        #region Helpers

        private static BuildManifest ManifestWithOneEdit()
        {
            var manifest = new BuildManifest
            {
                Game = MEGame.LMP.ToString(),
                Mod = new BuildMod
                {
                    Name = @"ModMaker Test Mod",
                    Author = @"Tests",
                    Description = @"Emitted by the test suite.",
                    Version = @"1",
                    DlcFolder = ModMakerCatalogue.CompilerDLCFolderName,
                    Mount = new BuildMount
                    {
                        Priority = 4801,
                        TlkId = ModMakerCatalogue.CompilerTlkId,
                        // LoadsInMultiplayer | LoadsInSingleplayer | DLCSetupCanMisMatch
                        Flags = InstallMount.Flags(requireAllPlayers: false),
                    },
                },
            };

            manifest.Coalesced().File(@"BioGame").Section(Section).Properties.Add(new PropertyWrite
            {
                Name = @"enemies",
                Source = @"wavelist/sfxwave_horde_cerberus1/DO_Level3",
                Replace = new ValueReplacement { Remove = ShippedRow, Add = EditedRow },
            });

            return manifest;
        }

        private static ConfigAssetBundle ReadBack(string directory) =>
            ConfigAssetBundle.FromDLCFolder(
                MEGame.LMP,
                Path.Combine(directory, ModMakerCatalogue.CompilerDLCFolderName, MEGame.LMP.CookedDirName()),
                ModMakerCatalogue.CompilerDLCFolderName);

        private static string TempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $@"m3modmaker_emit_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Cleanup(string directory)
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { /* A locked package file is not a test failure. */ }
        }

        #endregion
    }
}
