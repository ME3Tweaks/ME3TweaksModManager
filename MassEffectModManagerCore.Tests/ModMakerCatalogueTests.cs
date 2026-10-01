using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
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
    /// Smoke tests for the ModMaker catalogue generator.
    ///
    /// These are the tests listed in modmaker/docs/08-spike-results.md, and each one exists because
    /// the exploratory build got that exact answer wrong while reporting success. Every failure mode
    /// they cover is silent: the tool produces plausible output and nothing throws. That is why the
    /// assertions are on NUMBERS from the real game rather than on "did it return something".
    ///
    /// Tests needing a real install are Inconclusive without one, so CI stays green. The exclusion
    /// tests do not need a game and always run - they guard the rule that quietly corrupts a
    /// catalogue if it is ever dropped.
    /// </summary>
    [TestClass]
    public class ModMakerCatalogueTests
    {
        /// <summary>
        /// LegendaryExplorerCore has to be initialised before any package can be opened. M3 does this
        /// at startup; a test host does not, and without it the package scan quietly returns zero
        /// files rather than failing - which is exactly the silent-success failure these tests exist
        /// to catch, so it is worth doing explicitly rather than through a heavier shared fixture.
        /// </summary>
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        // Known-good values measured against LMP. A change here means either the game changed or the
        // generator regressed; both are worth a human looking.
        //
        // Content still being built is deliberately NOT pinned to a count. Project Milan and the
        // DLC_MOD_PMR3 map pack are in development, so map and wavelist totals move for entirely
        // legitimate reasons, and a test that fails every time a level is added stops being read.
        // Those cases assert the INVARIANT instead - the relationships that have to hold whatever the
        // content is - and print the current numbers so drift is still visible to a human.
        private const int ExpectedWaveFactionDescendants = 8;
        private const int MinHornetEffectiveProperties = 100;
        private const int ExpectedDifficultyHandlerProperties = 7;
        private const int ExpectedPerObjectClasses = 47;
        private const int ExpectedConfiguredPerObjectClasses = 22;
        private const int ExpectedInstanceSections = 207;
        private const int ExpectedWavesPerFaction = 11;
        private const int ExpectedPerObjectConfigProperties = 638;
        private const int ExpectedInstanceScopedProperties = 99;
        private const int ExpectedSharedScopedProperties = 69;
        private const int ExpectedWaveSlots = 1126;
        private const int ExpectedFactionsWithWaves = 7;
        private const int ExpectedSelectableFactions = 4;
        private const int ExpectedEnemyVocabulary = 25;
        private const int ExpectedPetVocabulary = 6;
        private const int ExpectedSlotsWithMaxPerWave = 136;
        private const int ExpectedFillerSlots = 308;
        private const int ExpectedMPDifficultyLevels = 4;

        /// <summary>SFXWave_Horde.MaxEnemies - how many enemies are alive at once, and so how big one
        /// full field is. 8 as shipped.</summary>
        private const int ExpectedFieldCap = 8;
        private const int ExpectedMPCategories = 33;
        private const int ExpectedMPPairs = 605;
        private const int ExpectedMPValues = 2402;
        private const int ExpectedIncompletePairs = 18;
        private const int ExpectedLargestCategoryStats = 41;
        private const int ExpectedSPCategories = 25;
        private const int ExpectedMPMapNames = 9;
        private const int ExpectedEnemyTypeOptions = 5;
        private const int ExpectedChallengeTypeOptions = 4;

        // The class scan and the config merge both read the whole install, so they are done once and
        // shared. MSTest does not parallelise within a class by default; the lock is for the day
        // someone turns that on, since a half-built dictionary would fail in a very confusing way.
        private static readonly object CacheLock = new();
        private static Dictionary<string, CatalogueClass> _classes;
        private static MergedGameConfig _config;
        private static WavelistCatalogue _waves;

        private static Dictionary<string, CatalogueClass> CachedClasses(GameTarget target)
        {
            lock (CacheLock) return _classes ??= ClassScanner.Scan(target);
        }

        private static MergedGameConfig CachedConfig(GameTarget target)
        {
            lock (CacheLock) return _config ??= MergedGameConfig.Load(target);
        }

        private static WavelistCatalogue CachedWaves(GameTarget target)
        {
            lock (CacheLock)
            {
                if (_waves != null) return _waves;
                var classes = CachedClasses(target);
                var config = CachedConfig(target);
                return _waves = WavelistCatalogue.Build(config, classes, InstanceCatalogue.Build(config, classes));
            }
        }

        private static DifficultyCatalogue _difficulty;
        private static MapListCatalogue _maps;

        private static DifficultyCatalogue CachedDifficulty(GameTarget target)
        {
            lock (CacheLock) return _difficulty ??= DifficultyCatalogue.Build(CachedConfig(target), CachedClasses(target));
        }

        private static MapListCatalogue CachedMaps(GameTarget target)
        {
            lock (CacheLock) return _maps ??= MapListCatalogue.Build(CachedConfig(target), CachedClasses(target));
        }

        private static IEnumerable<WaveSlot> AllSlots(WavelistCatalogue waves) =>
            waves.Factions.SelectMany(f => f.Waves).SelectMany(w => w.Rows).SelectMany(r => r.Slots);

        #region Exclusion of ModMaker's own output - no game required

        /// <summary>
        /// The generator reads the installed game to learn what the defaults ARE. If it also read back
        /// the DLC ModMaker itself built, then after one build-install-regenerate cycle the "shipped
        /// default" for every edited value would be the user's own edit. Nothing would throw; the
        /// catalogue would just stop describing the game.
        /// </summary>
        [TestMethod]
        public void CompilerOutputIsExcluded()
        {
            Assert.IsTrue(ModMakerCatalogue.IsCompilerOutput(@"DLC_MOD_ModMaker"));
            Assert.IsTrue(ModMakerCatalogue.IsCompilerOutput(@"dlc_mod_modmaker"), @"Match must be case-insensitive");
            Assert.IsTrue(ModMakerCatalogue.IsCompilerOutput(@"C:\Game\BioGame\DLC\DLC_MOD_ModMaker"));
            Assert.IsTrue(ModMakerCatalogue.IsCompilerOutput(@"C:\Game\BioGame\DLC\DLC_MOD_ModMaker\"));
        }

        /// <summary>
        /// The exclusion must not swallow a differently-named mod that merely starts the same way.
        /// A substring match here would silently drop someone else's content from the catalogue.
        /// </summary>
        [TestMethod]
        public void ExclusionDoesNotOverreach()
        {
            Assert.IsFalse(ModMakerCatalogue.IsCompilerOutput(@"DLC_MOD_ModMakerExtras"));
            Assert.IsFalse(ModMakerCatalogue.IsCompilerOutput(@"DLC_MOD_ProjectMilan"));
            Assert.IsFalse(ModMakerCatalogue.IsCompilerOutput(null));
            Assert.IsFalse(ModMakerCatalogue.IsCompilerOutput(@""));

            Assert.IsTrue(ModMakerCatalogue.IsInCompilerOutput(@"C:\G\BioGame\DLC\DLC_MOD_ModMaker\CookedPCConsole\x.pcc"));
            Assert.IsTrue(ModMakerCatalogue.IsInCompilerOutput(@"C:/G/BioGame/DLC/DLC_MOD_ModMaker/CookedPCConsole/x.pcc"));
            Assert.IsFalse(ModMakerCatalogue.IsInCompilerOutput(@"C:\G\BioGame\DLC\DLC_MOD_ModMakerExtras\CookedPCConsole\x.pcc"));
            Assert.IsFalse(ModMakerCatalogue.IsInCompilerOutput(@"C:\G\BioGame\CookedPCConsole\SFXGame.pcc"));
        }

        #endregion

        #region Environment fingerprint - no game required

        [TestMethod]
        public void EnvironmentDifferencesAreDescribed()
        {
            var a = new CatalogueEnvironment
            {
                GamePath = @"C:\Game", GeneratorVersion = @"1", TextLayerVersion = @"1",
                Dlc = { new CatalogueEnvironment.DlcEntry { Folder = @"DLC_MOD_ProjectMilan", MountPriority = 8227 } }
            };
            var b = new CatalogueEnvironment
            {
                GamePath = @"C:\Game", GeneratorVersion = @"1", TextLayerVersion = @"1",
                Dlc =
                {
                    new CatalogueEnvironment.DlcEntry { Folder = @"DLC_MOD_ProjectMilan", MountPriority = 8227 },
                    new CatalogueEnvironment.DlcEntry { Folder = @"DLC_MOD_Something", MountPriority = 9000 }
                }
            };

            Assert.AreEqual(0, CatalogueEnvironment.DescribeDifferences(a, a).Count, @"Identical environments must compare equal");

            var diffs = CatalogueEnvironment.DescribeDifferences(a, b);
            Assert.AreEqual(1, diffs.Count);
            StringAssert.Contains(diffs[0], @"DLC_MOD_Something");

            // Mount priority changing matters even when the DLC set does not: it changes the order
            // config resolves in, which changes the effective value of anything two DLCs both set.
            var c = new CatalogueEnvironment
            {
                GamePath = @"C:\Game", GeneratorVersion = @"1", TextLayerVersion = @"1",
                Dlc = { new CatalogueEnvironment.DlcEntry { Folder = @"DLC_MOD_ProjectMilan", MountPriority = 4000 } }
            };
            Assert.IsTrue(CatalogueEnvironment.DescribeDifferences(a, c).Any(d => d.Contains(@"mount priority")));
        }

        #endregion

        #region Class scan - needs a real LMP install

        [TestMethod]
        public void ClassScanFindsTheRealSurface()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = ClassScanner.Scan(target);
            Assert.IsTrue(classes.Count > 1000, $@"Expected thousands of classes, got {classes.Count}");

            // The partial-load predicate must include property exports. Loading only Class data leaves
            // the child property data null and the walk finds ZERO config properties game-wide, while
            // reporting success.
            var withConfig = classes.Values.Count(c => c.OwnProperties.Count > 0);
            Assert.IsTrue(withConfig > 100, $@"Only {withConfig} classes declared config properties - property exports are probably not being loaded");

            // Flags must come off the deserialized UProperty, not ExportEntry.GetPropertyFlags().
            // The latter only works when the export HasStack, and finds only bools.
            var types = classes.Values.SelectMany(c => c.OwnProperties).Select(p => p.DeclaredType).Distinct().ToList();
            CollectionAssert.Contains(types, @"BoolProperty");
            Assert.IsTrue(types.Count > 3, $@"Only found property types [{string.Join(@", ", types)}] - flags are probably being read the wrong way");

            // Inheritance has to resolve, or a subclass that declares nothing looks empty. The AI
            // Hornet declares a handful and inherits well over a hundred.
            var hornet = ClassScanner.FindByName(@"SFXWeapon_AI_Hornet", classes);
            Assert.IsNotNull(hornet, @"SFXWeapon_AI_Hornet not found");
            var hornetProps = ClassScanner.EffectiveProperties(hornet.Section, classes);
            Assert.IsTrue(hornetProps.Count >= MinHornetEffectiveProperties,
                $@"SFXWeapon_AI_Hornet resolved to {hornetProps.Count} properties; inheritance is not resolving");

            var difficulty = ClassScanner.FindByName(@"SFXDifficultyHandlerMP", classes);
            Assert.IsNotNull(difficulty);
            Assert.AreEqual(ExpectedDifficultyHandlerProperties,
                ClassScanner.EffectiveProperties(difficulty.Section, classes).Count);
        }

        /// <summary>
        /// The scan finds a class's config properties by enumerating the exports it OWNS - the outer
        /// relationship - rather than by walking the UStruct.Children linked list.
        ///
        /// This test is the reason that is safe. It rebuilds the chain walk that LEC itself uses
        /// (ScriptObjectToASTConverter.ConvertClass walks exactly this list), with a partial load wide
        /// enough that every link in it - functions, states, enums, consts, script structs - can be
        /// read, so the walk always completes and can serve as the oracle. Then it asserts the shipped
        /// scanner found precisely the same set for every class in the game.
        ///
        /// It exists because the two must not be allowed to drift. Ownership is an engine invariant: a
        /// class's declared variables have that class as their outer, whatever order the compiler
        /// happened to emit them in and whoever wrote them. Declaration ORDER is not an invariant, and
        /// nothing here may come to depend on it - a modder's class can interleave variables and
        /// functions however it likes and the engine does not care.
        /// </summary>
        [TestMethod]
        public void PropertyEnumerationMatchesTheScriptChain()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = ClassScanner.Scan(target);

            // Everything UField.Next threads through. Without these the chain hits an export the
            // partial load skipped, cannot read its Next, and stops - which is what makes the chain
            // unusable as the scanner's own strategy, and is why it is only the oracle here.
            var fieldClasses = new HashSet<string>(StringComparer.Ordinal)
                { @"Class", @"Function", @"State", @"Enum", @"Const", @"ScriptStruct" };

            var files = MELoadedFiles.GetFilesLoadedInGame(target.Game, forceReload: true, gameRootOverride: target.TargetPath)
                                     .Where(f => !ModMakerCatalogue.IsInCompilerOutput(f.Value))
                                     .ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mismatches = new List<string>();
            int compared = 0, incompleteChains = 0;

            foreach (var f in files)
            {
                using var pack = MEPackageHandler.UnsafePartialLoad(f.Value, e =>
                    fieldClasses.Contains(e.ClassName) || e.ClassName.EndsWith(@"Property", StringComparison.Ordinal));

                foreach (var classExport in pack.Exports.Where(x => x.ClassName == @"Class"))
                {
                    var ifp = classExport.InstancedFullPath;
                    var section = ifp.Contains('.') ? ifp : $@"{Path.GetFileNameWithoutExtension(f.Value)}.{ifp}";

                    // The scan keys on section and keeps the first occurrence, so a class cooked into
                    // several packages must be compared against the copy the scan actually kept.
                    if (!seen.Add(section)) continue;
                    if (!classes.TryGetValue(section, out var scanned)) continue;

                    UClass uclass;
                    try { uclass = ObjectBinary.From<UClass>(classExport); }
                    catch { continue; }

                    var fromChain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool complete = true;
                    int childIdx = uclass.Children, guard = 0;
                    while (childIdx != 0 && guard++ < 20000)
                    {
                        if (pack.GetEntry(childIdx) is not ExportEntry child) { complete = false; break; }
                        ObjectBinary bin;
                        try { bin = ObjectBinary.From(child); }
                        catch { complete = false; break; }

                        if (bin is UProperty p
                            && (p.PropertyFlags.Has(UnrealFlags.EPropertyFlags.Config)
                             || p.PropertyFlags.Has(UnrealFlags.EPropertyFlags.GlobalConfig)))
                            fromChain.Add(child.ObjectName.Name);

                        int next = bin is UField uf ? uf.Next : 0;
                        if (next == childIdx) { complete = false; break; }
                        childIdx = next;
                    }

                    if (!complete) { incompleteChains++; continue; }

                    compared++;
                    var fromScan = new HashSet<string>(scanned.OwnProperties.Select(op => op.Name), StringComparer.OrdinalIgnoreCase);
                    if (fromScan.SetEquals(fromChain)) continue;

                    if (mismatches.Count < 20)
                    {
                        var missing = string.Join(@", ", fromChain.Except(fromScan));
                        var extra = string.Join(@", ", fromScan.Except(fromChain));
                        mismatches.Add($@"{section}: missing [{missing}] extra [{extra}]");
                    }
                }
            }

            Assert.AreEqual(0, incompleteChains,
                $@"{incompleteChains} class chain(s) could not be walked; the oracle is not covering the whole game");
            Assert.IsTrue(compared > 4000, $@"Only {compared} classes were comparable; the oracle is not running");
            Assert.AreEqual(0, mismatches.Count,
                $@"Ownership and the script chain disagree for {mismatches.Count} class(es):{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
        }

        /// <summary>
        /// Content is discovered, not listed. The wavelist editor knows one name - SFXWave_Horde - and
        /// finds every faction by walking SuperClass, which is what makes a faction added by someone
        /// else's mod show up on its own.
        /// </summary>
        [TestMethod]
        public void FactionsAreDiscoveredNotListed()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = ClassScanner.Scan(target);
            var waveBase = ClassScanner.FindByName(@"SFXWave_Horde", classes);
            Assert.IsNotNull(waveBase, @"Anchor SFXWave_Horde is missing - this must surface as a named warning, not an empty editor");

            var factions = ClassScanner.Descendants(waveBase.Section, classes);
            Assert.AreEqual(ExpectedWaveFactionDescendants, factions.Count,
                $@"Expected {ExpectedWaveFactionDescendants} faction classes, found [{string.Join(@", ", factions.Select(f => f.Name))}]");

            // Milan's own unbuilt faction must be among them: it is the proof that discovery reaches
            // content the tool was never told about.
            Assert.IsTrue(factions.Any(f => f.Name.Equals(@"SFXWave_Horde_Rejects", StringComparison.OrdinalIgnoreCase)));

            // Per-object-config is how a faction becomes eleven pages rather than one.
            Assert.IsTrue(factions.All(f => f.PerObjectConfig), @"Faction classes should all carry EClassFlags.PerObjectConfig");
        }

        #endregion

        #region Merged config - needs a real LMP install

        [TestMethod]
        public void MergedConfigResolvesParseActions()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var config = MergedGameConfig.Load(target);

            // Anything reading the coalesced must cite more than one file. A Coalesced*.bin glob reads
            // only the basegame, because a DLC's coalesced is named after the DLC.
            Assert.IsTrue(config.MergedDLC.Count > 0, @"No DLC config was merged - the DLC coalesced files are probably not being found");
            CollectionAssert.DoesNotContain(config.MergedDLC.ToList(), ModMakerCatalogue.CompilerDLCFolderName);

            // Milan replaces a basegame enemy by Removing it and re-adding a version pointing at a
            // newer archetype package. Read unmerged, the entry appears three times.
            var enemyList = config.LiveValues(@"sfxgamempcontent.sfxwave_horde", @"enemylist").ToList();
            Assert.IsTrue(enemyList.Count > 0, @"SFXWave_Horde.EnemyList not found - the merge anchor may have moved");
            var cannibals = enemyList.Count(v => v.Value != null && v.Value.Contains(@"WAVE_RPR_Cannibal"));
            Assert.AreEqual(1, cannibals, @"Cannibal should appear once after the merge; more means Remove entries are being counted as live");
        }

        /// <summary>
        /// AddUnique suppresses a value that is already present, byte-for-byte.
        ///
        /// This began as a report on a LegendaryExplorerCore bug rather than a check on our own code:
        /// ConfigMerge's AddUnique case found a duplicate and then `continue`d the scanning loop
        /// instead of skipping the add, so AddUnique behaved exactly as Add - 841 spurious values on
        /// LMP. That is fixed, and the check is now an assertion rather than a measurement.
        ///
        /// Kept permanently, because this class of bug is invisible in every other way: nothing
        /// throws, nothing looks wrong, the config simply contains values the game does not have.
        /// </summary>
        [TestMethod]
        public void AddUniqueSuppressesDuplicatesInTheMergedConfig()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var duplicates = MergedGameConfig.Load(target).CountUnsuppressedDuplicates();
            Console.WriteLine($@"AddUnique duplicates surviving the merge: {duplicates}");
            Assert.AreEqual(0, duplicates,
                @"An AddUnique whose value is already present must not add it again - " +
                @"see ConfigMerge.MergeEntry's AddUnique case");
        }

        #endregion

        #region Instances - needs a real LMP install

        /// <summary>
        /// A class flagged perobjectconfig is configured per NAMED OBJECT, so one class backs many
        /// editable pages. Reading only class-path sections finds none of this and reports one page
        /// per faction instead of eleven, without failing.
        /// </summary>
        [TestMethod]
        public void PerObjectClassesAndTheirInstancesAreFound()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var instances = InstanceCatalogue.Build(CachedConfig(target), CachedClasses(target));

            Assert.AreEqual(ExpectedPerObjectClasses, instances.ByClassToken.Count,
                @"Number of perobjectconfig classes changed");
            Assert.AreEqual(ExpectedConfiguredPerObjectClasses, instances.ByClassToken.Values.Count(c => !c.Unconfigured));
            Assert.AreEqual(ExpectedInstanceSections, instances.ByClassToken.Values.Sum(c => c.Instances.Count));

            // Classes the shipped config never names are surface a mod can add to, not an error, and
            // must survive into the catalogue rather than being filtered out as empty.
            Assert.IsTrue(instances.ByClassToken.Values.Any(c => c.Unconfigured));

            Assert.AreEqual(0, instances.AmbiguousClassTokens.Count,
                $@"Ambiguous class tokens make '<instance> <class>' keys unresolvable: {string.Join(@", ", instances.AmbiguousClassTokens)}");

            var cerberus = instances.ByClassToken[@"SFXWave_Horde_Cerberus"];
            Assert.AreEqual(ExpectedWavesPerFaction, cerberus.Instances.Count,
                @"A faction is one page per wave; the wave sections are not being read");
        }

        /// <summary>
        /// Instance names cannot be derived from class names. SFXWave_Horde_Cerberus2's instances are
        /// named after the BASE faction, so the same instance name belongs to two different factions
        /// and only the (instance, class) PAIR identifies a wave. Any code that synthesises instance
        /// ids from the class name silently edits the wrong faction.
        /// </summary>
        [TestMethod]
        public void InstanceNamesAreDataNotConvention()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var instances = InstanceCatalogue.Build(CachedConfig(target), CachedClasses(target));

            var cerberus2 = instances.ByClassToken[@"SFXWave_Horde_Cerberus2"];
            Assert.IsTrue(cerberus2.Instances.Count > 0, @"SFXWave_Horde_Cerberus2 has no instances");
            var cerberus = instances.ByClassToken[@"SFXWave_Horde_Cerberus"];

            // Not "most of them differ" but "they are the same list": Cerberus2's eleven waves are
            // named for the base faction, so every instance name belongs to two different factions
            // and the (instance, class) pair is the only thing that identifies a wave.
            CollectionAssert.AreEquivalent(
                cerberus.Instances.Select(i => i.Id.ToLowerInvariant()).ToList(),
                cerberus2.Instances.Select(i => i.Id.ToLowerInvariant()).ToList(),
                @"Cerberus2's instances should be exactly the base faction's names");

            // Exactly one of the eleven - sfxwave_horde_cerberus2 - happens to look like it follows
            // "class name plus index" for Cerberus2. Any rule inferred from a sample would edit the
            // wrong faction for the other ten.
            Assert.AreEqual(1, cerberus2.Instances.Count(
                i => i.Id.StartsWith(cerberus2.ClassToken, StringComparison.OrdinalIgnoreCase)));

            // Wave 10 must not sort before wave 2 when instances are shown to a person.
            var ordered = cerberus.Instances.Select(i => i.Id)
                                  .OrderBy(x => x, Comparer<string>.Create(InstanceCatalogue.CompareNatural)).ToList();
            Assert.AreEqual(@"sfxwave_horde_cerberus2", ordered[1], $@"Natural ordering failed: {string.Join(@", ", ordered)}");
            Assert.AreEqual(@"sfxwave_horde_cerberus11", ordered[^1]);
        }

        /// <summary>
        /// Scope is read from which section shape BioWare used, never derived from a flag - the
        /// globalconfig hypothesis is wrong, and guessing writes a user's edit into a section the game
        /// does not read, producing a mod that builds, installs, mounts and does nothing.
        ///
        /// The own-evidence tally is the measurement the design was written from and is asserted
        /// exactly. The inherited tally is what the editor uses: it may move properties out of
        /// unverified, but must never turn one into Both.
        /// </summary>
        [TestMethod]
        public void PropertyScopeIsReadFromShippedConfig()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = CachedClasses(target);
            var instances = InstanceCatalogue.Build(CachedConfig(target), classes);

            var own = instances.Tally(classes, ownEvidenceOnly: true);
            Console.WriteLine($@"scope, evidence on the class itself : {own}");
            Assert.AreEqual(0, own.Both, @"The two section shapes are supposed to be exactly disjoint");
            Assert.AreEqual(ExpectedInstanceScopedProperties, own.InstanceOnly);
            Assert.AreEqual(ExpectedSharedScopedProperties, own.SharedOnly);
            Assert.AreEqual(ExpectedPerObjectConfigProperties, own.Total);

            // Inherited evidence may only convert unverified into shared. It must not reclassify a
            // property the class itself carries evidence for: SFXWave_Operation sets a shared baseline
            // for OperationTimeLimit and srEndWaveFailure that two of its subclasses then override per
            // instance, and letting the ancestor win there would write those edits into a section the
            // leaf does not read.
            var inherited = instances.Tally(classes);
            Console.WriteLine($@"scope, inherited evidence included  : {inherited}");
            Assert.AreEqual(own.Total, inherited.Total);
            Assert.AreEqual(own.InstanceOnly, inherited.InstanceOnly);
            Assert.IsTrue(inherited.SharedOnly > own.SharedOnly);
            Assert.AreEqual(own.Unverified - (inherited.SharedOnly - own.SharedOnly), inherited.Unverified);

            foreach (var (section, prop, from) in instances.ScopeConflicts(classes))
                Console.WriteLine($@"  set in both of its own shapes: {section}.{prop} ({from})");
            Assert.AreEqual(0, inherited.Both,
                @"A class setting one property in both of its own shapes has no safe write target");

            // SFXWave_Horde_Cerberus has no class-path section of its own; its shared values arrive
            // from SFXWave_Horde by class inheritance. Looking only at the leaf reports them as
            // unverified and hides them from the editor.
            var cerberus = ClassScanner.FindByName(@"SFXWave_Horde_Cerberus", classes);
            var enemyList = instances.ScopeOf(cerberus.Section, @"EnemyList", classes);
            Assert.AreEqual(ConfigScope.Shared, enemyList.Scope);
            Assert.IsFalse(enemyList.SetOnOwnClassPath);
            Assert.IsNotNull(enemyList.SharedInheritedFrom, @"Shared evidence should have been found on an ancestor");

            // ...while the per-wave roster is instance-scoped on the same class.
            Assert.AreEqual(ConfigScope.Instance, instances.ScopeOf(cerberus.Section, @"Enemies", classes).Scope);
        }

        #endregion

        #region Config struct reading - no game required

        /// <summary>
        /// The three shapes that made ME3TweaksCore's StringStructParser unusable here, asserted so a
        /// future switch back to it is a decision rather than a silent regression. Measured against
        /// that parser: it returns the span between the first and last quote for a nested value, it
        /// returns one element for a space-padded list, and it leaves literal backslash-t on keys.
        /// </summary>
        [TestMethod]
        public void ConfigStructsReadTheShapesTheGameUses()
        {
            var row = ConfigStructReader.ReadStruct(
                @"(Difficulty=DO_Level2,Enemies=( (EnemyType=""A""), (EnemyType=""B"", MinCount=1, MaxCount=1) ))");
            Assert.AreEqual(@"DO_Level2", row[@"Difficulty"]);

            // The nested list must come back intact, not truncated at a quote.
            var slots = ConfigStructReader.ReadList(row[@"Enemies"]);
            Assert.AreEqual(2, slots.Count, $@"Got [{string.Join(@" | ", slots)}]");
            Assert.AreEqual(@"B", ConfigStructReader.ReadStruct(slots[1])[@"EnemyType"]);
            Assert.AreEqual(@"1", ConfigStructReader.ReadStruct(slots[1])[@"MaxCount"]);

            // Literal backslash-t sequences sit on the key, not on the value, and no Trim() removes
            // them - they are two characters in the config text, not control characters.
            var enemy = ConfigStructReader.ReadStruct(
                @"(EnemyType=""WAVE_CER_Centurion"",			EnemyArchetypeName = ""Char.Centurion"",		WaveCost=30)");
            Assert.AreEqual(@"Char.Centurion", enemy[@"EnemyArchetypeName"]);
            Assert.AreEqual(@"30", enemy[@"WaveCost"]);

            // A single-element list and a bare struct both have to survive the descend step.
            Assert.AreEqual(1, ConfigStructReader.ReadList(@"((EnemyType=""A""))").Count);
            Assert.AreEqual(1, ConfigStructReader.ReadList(@"(EnemyType=""A"")").Count);
        }

        /// <summary>
        /// Unbalanced parentheses are an error, not something to recover from. Tolerating them would
        /// mean carrying a recovery path forever, through which every later malformed value would
        /// slide unnoticed; the config source is under our control, so a bad value gets fixed there.
        /// What the reader must not do is lose the row quietly - see
        /// <see cref="MalformedWaveRowsAreReportedNotSwallowed"/>.
        /// </summary>
        [TestMethod]
        public void UnbalancedStructsAreRejected()
        {
            Assert.Throws<FormatException>(() => ConfigStructReader.ReadStruct(@"(Difficulty=DO_Level4,Enemies=((EnemyType=""A""))))"));
            Assert.Throws<FormatException>(() => ConfigStructReader.ReadStruct(@"(Difficulty=DO_Level4"));
        }

        #endregion

        #region Wavelists - needs a real LMP install

        /// <summary>
        /// A type listed twice in one row is dropped, and every drop is reported.
        ///
        /// The engine takes the first listing and skips the rest, so the catalogue does the same - a
        /// slot the game never spawns must not appear in the editor, be costed against the budget, or
        /// be written back out. Shipped LMP has exactly one such row today; the test is written so it
        /// still means something after that row is corrected, by comparing what the reader dropped
        /// against what it said it dropped rather than against a fixed number.
        /// </summary>
        [TestMethod]
        public void AnEnemyListedTwiceIsDroppedAndReported()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var typeInRaw = new Regex(@"EnemyType\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);

            int droppedByTheReader = 0;
            foreach (var faction in waves.Factions)
            foreach (var wave in faction.Waves)
            foreach (var row in wave.Rows)
            {
                var kept = row.Slots.Select(s => s.EnemyType).ToList();
                Assert.AreEqual(kept.Count, kept.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    $@"[{wave.InstanceId}] {row.Difficulty} kept a duplicated enemy type: {row.RawValue}");

                var listed = typeInRaw.Matches(row.RawValue).Select(m => m.Groups[1].Value).ToList();
                droppedByTheReader += listed.Count - listed.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            }

            var reported = waves.Warnings.Count(w => w.Contains(@"is listed more than once", StringComparison.Ordinal));
            Assert.AreEqual(droppedByTheReader, reported,
                $@"{droppedByTheReader} duplicated slots were dropped but {reported} were reported");

            Console.WriteLine($@"duplicated slots dropped: {droppedByTheReader}");
        }

        /// <summary>
        /// The headline editor's data. Every number here was got wrong at least once by the
        /// exploratory build while it reported success, which is why they are asserted exactly.
        /// </summary>
        [TestMethod]
        public void WavelistsReadCompletely()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            foreach (var w in waves.Warnings) Console.WriteLine($@"warning: {w}");

            // 1,131, not 994. An earlier slot parser required ')' straight after MaxCount and silently
            // dropped every slot carrying the third field - 12% of the data, and disproportionately
            // the expensive enemies that decide how a wave feels.
            Assert.AreEqual(ExpectedWaveSlots, waves.SlotCount);
            Assert.AreEqual(ExpectedSlotsWithMaxPerWave, AllSlots(waves).Count(s => s.MaxPerWave != null));

            // A slot with no enemy type is a silently emptied wave entry.
            Assert.IsFalse(AllSlots(waves).Any(s => string.IsNullOrWhiteSpace(s.EnemyType)));

            // The vocabulary is closed - every type a wavelist uses has a price - which is what lets
            // the editor offer a fixed palette and cost a wave live.
            Assert.AreEqual(ExpectedEnemyVocabulary, waves.Enemies.Count(e => !e.IsPet));
            Assert.AreEqual(ExpectedPetVocabulary, waves.Enemies.Count(e => e.IsPet));
            var unpriced = AllSlots(waves).Select(s => s.EnemyType).Distinct(StringComparer.OrdinalIgnoreCase)
                                          .Where(t => waves.CostOf(t) == null).ToList();
            Assert.AreEqual(0, unpriced.Count, $@"Enemy types with no price: {string.Join(@", ", unpriced)}");

            // The archetype name is what points an entry at a pawn; losing it while keeping the token
            // would look fine and build a mod that spawns nothing.
            Assert.IsFalse(waves.Enemies.Any(e => !e.IsPet && string.IsNullOrWhiteSpace(e.ArchetypeName)));

            // A slot field this tool does not model is data being dropped, so it must be reported.
            Assert.AreEqual(0, waves.UnmodelledSlotFields.Count,
                $@"Unmodelled slot fields: {string.Join(@", ", waves.UnmodelledSlotFields.Select(kv => $@"{kv.Key} x{kv.Value}"))}");
        }

        /// <summary>
        /// Every wave row in the installed game must parse. This found a real one:
        /// MergedConfig/BioGame.xml had an extra ')' on the DO_Level4 row of
        /// [sfxwave_horde_cerberus1 sfxwave_horde_cerberus2], which cost four slots and a whole
        /// Platinum wave. It is fixed at source, and this guards against it coming back - in Milan's
        /// config or in anyone else's mod.
        ///
        /// A value that does not parse is skipped, and skipping has to be loud, so the same text goes
        /// into MalformedValues and into the Warnings the editor shows. The rejection itself is
        /// covered by UnbalancedStructsAreRejected, which needs no game.
        /// </summary>
        [TestMethod]
        public void MalformedWaveRowsAreReportedNotSwallowed()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            foreach (var m in waves.MalformedValues) Console.WriteLine($@"malformed: {m}");

            Assert.AreEqual(0, waves.MalformedValues.Count,
                $@"Unparseable wave rows: {string.Join(@" | ", waves.MalformedValues)}");

            // A diagnostic nobody surfaces is the same as no diagnostic.
            Assert.IsTrue(waves.MalformedValues.All(m => waves.Warnings.Contains(m)));
        }

        /// <summary>
        /// Slot 0 is the budget filler and carries no counts; every other slot carries both. Verified
        /// as an observation rather than assumed as a rule - the reader classifies by the absence of
        /// counts, so a wave that breaks the convention is described instead of misread.
        /// </summary>
        [TestMethod]
        public void WaveSlotConventionsHold()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var slots = AllSlots(waves).ToList();

            Assert.AreEqual(ExpectedFillerSlots, slots.Count(s => s.IsFiller));
            Assert.IsTrue(slots.Where(s => s.Index == 0).All(s => s.IsFiller), @"Slot 0 should never carry counts");
            Assert.IsTrue(slots.Where(s => s.Index > 0).All(s => !s.IsFiller), @"Every slot after the first should carry counts");
            Assert.IsTrue(slots.Where(s => !s.IsFiller).All(s => s.MinCount == s.MaxCount),
                @"Shipped data never uses a count range, though the pair supports one");

            // The raw row text has to survive untouched: emission is Remove + Add, and Remove matches
            // by exact text, so a pretty-printed original breaks the match silently.
            var rows = waves.Factions.SelectMany(f => f.Waves).SelectMany(w => w.Rows).ToList();
            Assert.IsTrue(rows.All(r => !string.IsNullOrEmpty(r.RawValue)));
            Assert.IsTrue(rows.Any(r => r.RawValue.Contains(@"( (")),
                @"Expected the original spacing to be preserved verbatim");
        }

        /// <summary>
        /// Factions are discovered from one anchor, and the three BioWare replaced in MP4 are dormant
        /// rather than dead - they keep their wavelists and are re-enabled by putting them back into
        /// WaveTypes, which is a headline feature rather than a footnote.
        /// </summary>
        [TestMethod]
        public void FactionSelectionIsSeparateFromFactionContent()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);

            Assert.AreEqual(ExpectedWaveFactionDescendants, waves.Factions.Count);
            Assert.AreEqual(ExpectedFactionsWithWaves, waves.Factions.Count(f => f.Waves.Count > 0));
            Assert.AreEqual(ExpectedSelectableFactions, waves.Factions.Count(f => f.IsSelectable));

            // Dormant, not dead: not selectable, but with all eleven wavelists intact.
            var dormant = waves.Factions.Where(f => !f.IsSelectable && f.Waves.Count > 0).ToList();
            Assert.AreEqual(3, dormant.Count, $@"Expected 3 dormant factions, got [{string.Join(@", ", dormant.Select(f => f.Name))}]");
            Assert.IsTrue(dormant.All(f => f.Waves.Count == ExpectedWavesPerFaction));

            // Milan's own faction ships no waves yet and must still appear, or it can never be built.
            var rejects = waves.Factions.Single(f => f.Name.Equals(@"SFXWave_Horde_Rejects", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(0, rejects.Waves.Count);
            Assert.IsFalse(rejects.IsSelectable);

            // waveindex comes from the property, never from the instance name.
            var withWaves = waves.Factions.First(f => f.Waves.Count > 0);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, ExpectedWavesPerFaction).ToList(),
                withWaves.Waves.Select(w => w.WaveIndex.Value).ToList());
        }

        /// <summary>
        /// The budget is a per-difficulty ramp interpolated across the wave range, not a fixed number.
        /// An earlier reading of this - WavePointsToBudget x OperationEnemyBudgetMultiplier - was
        /// wrong, and a wrong budget makes every cost bar in the editor lie.
        /// </summary>
        [TestMethod]
        public void WaveBudgetRampIsRead()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);

            Assert.AreEqual(4, waves.Budget.Count, @"One point range per difficulty");
            Assert.AreEqual(1, waves.FirstWave);
            Assert.AreEqual(ExpectedWavesPerFaction, waves.LastWave);

            // Platinum exists only because the config re-lists Bronze/Silver/Gold to add it - the
            // cleanest confirmation that config replaces defaults rather than merging with them.
            var platinum = waves.Budget.Single(b => b.Difficulty == @"DO_Level4");
            Assert.AreEqual(900, platinum.FirstWavePoints);
            Assert.AreEqual(2000, platinum.LastWavePoints);

            Assert.AreEqual(240, waves.BudgetFor(@"DO_Level1", 1).Value, 0.001);
            Assert.AreEqual(550, waves.BudgetFor(@"DO_Level1", 11).Value, 0.001);
            Assert.AreEqual(395, waves.BudgetFor(@"DO_Level1", 6).Value, 0.001);
            Assert.IsNull(waves.BudgetFor(@"DO_NotADifficulty", 1));

            // In WaveIndex's base, not the coordinator's. OperationWaves ships WaveNumber 2, 5, 9, 10
            // zero-based; the waves a player sees are 3, 6, 10 and 11, and 11 is the extraction - which
            // is an objective too, just always the same one.
            CollectionAssert.AreEqual(new[] { 3, 6, 10, 11 }, waves.ObjectiveWaves);
            CollectionAssert.Contains(waves.ObjectiveWaves, waves.LastWave,
                @"The extraction wave carries an objective and must not be dropped from the list");
        }

        /// <summary>
        /// A (faction, wave, difficulty) addresses exactly one row, and where it does not, every row
        /// survives and is reported.
        ///
        /// This used to pin a specific anomaly: SFXWave_Horde_Collector wave 5 shipped THREE DO_Level3
        /// rows, all surviving because they arrived as AddUnique and differed by a single field. That
        /// has since been corrected at source, so the assertion is now that the data is clean and that
        /// the reporting mechanism is still wired up - not that the anomaly is still there.
        ///
        /// What must not come back is collapsing. Near-identical rows differ in exactly the field that
        /// distinguishes them, and which one the game honours is unverified, so a reader that silently
        /// picks one destroys the only evidence a human could use to decide.
        /// </summary>
        [TestMethod]
        public void DuplicateDifficultyRowsAreReportedNotCollapsed()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);

            var duplicated = waves.Factions
                .SelectMany(f => f.Waves.Select(w => (Faction: f, Wave: w)))
                .SelectMany(fw => fw.Wave.Rows.GroupBy(r => r.Difficulty, StringComparer.OrdinalIgnoreCase)
                                             .Where(g => g.Count() > 1)
                                             .Select(g => (fw.Faction, fw.Wave, Difficulty: g.Key, Rows: g.ToList())))
                .ToList();

            foreach (var d in duplicated)
                Console.WriteLine($@"duplicate: {d.Faction.Name} wave {d.Wave.WaveIndex} {d.Difficulty} x{d.Rows.Count}");

            // Every duplicate that does exist must have been kept whole and reported by name. The
            // reader is not allowed to merge them away, whether there are none today or ten tomorrow.
            foreach (var d in duplicated)
            {
                Assert.AreEqual(d.Rows.Count, d.Rows.Select(r => r.RawValue).Distinct().Count(),
                    $@"{d.Faction.Name} wave {d.Wave.WaveIndex} {d.Difficulty}: duplicate rows must stay distinguishable");
                Assert.IsTrue(waves.Warnings.Any(w => w.Contains(d.Difficulty, StringComparison.OrdinalIgnoreCase)
                                                   && w.Contains(d.Wave.InstanceId, StringComparison.OrdinalIgnoreCase)),
                    $@"{d.Faction.Name} wave {d.Wave.WaveIndex} {d.Difficulty} is duplicated but not reported. Warnings: {string.Join(@" | ", waves.Warnings)}");
            }

            Assert.AreEqual(0, duplicated.Count,
                $@"Shipped data is expected to address one row per (faction, wave, difficulty): {string.Join(@", ", duplicated.Select(d => $@"{d.Faction.Name}/{d.Wave.WaveIndex}/{d.Difficulty}"))}");
        }

        #endregion

        #region Difficulty and maps - needs a real LMP install

        /// <summary>
        /// The enemy stat surface is (level, category, stat), and none of it is discoverable from the
        /// class hierarchy: the stats are rows inside one config property, not config properties. A
        /// tool that reads only the class scan sees BioDifficulty as two classes and reports the whole
        /// enemy editor as empty without failing.
        /// </summary>
        [TestMethod]
        public void DifficultyStatsAreReadFromConfigRows()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var difficulty = CachedDifficulty(target);
            foreach (var m in difficulty.MalformedValues) Console.WriteLine($@"malformed: {m}");
            Assert.AreEqual(0, difficulty.MalformedValues.Count,
                $@"Unparseable difficulty rows: {string.Join(@" | ", difficulty.MalformedValues)}");

            var mp = difficulty.Table(@"SFXDifficultyHandlerMP");
            Assert.IsNotNull(mp, @"The multiplayer difficulty handler was not found");

            Assert.AreEqual(ExpectedMPDifficultyLevels, mp.Levels.Count);
            Assert.AreEqual(ExpectedMPCategories, mp.Categories.Count);
            Assert.AreEqual(ExpectedMPPairs, mp.PairCount);
            Assert.AreEqual(ExpectedMPValues, mp.ValueCount);
            Assert.AreEqual(ExpectedLargestCategoryStats, mp.Categories.Max(c => c.StatNames.Count()));

            // Handlers are found by the shape of their property names, so the singleplayer one - a
            // different class, a different section, five levels rather than four - comes along too.
            var sp = difficulty.Table(@"SFXDifficultyHandler");
            Assert.IsNotNull(sp, @"The singleplayer handler should be discovered by the same rule");
            Assert.AreEqual(ExpectedSPCategories, sp.Categories.Count);
            Assert.IsTrue(sp.Levels.Count > mp.Levels.Count, @"Singleplayer ships more difficulty levels than multiplayer");
        }

        /// <summary>
        /// StatRange's X and Y are the ends of an interpolation and either may be the larger, so they
        /// are not a minimum and a maximum. Calling them that would produce a UI that refuses to let X
        /// exceed Y - and every stat that improves as it falls, like EvadeFrequency, does exactly that.
        ///
        /// The grid is also nearly but not quite rectangular, and a missing cell has to stay missing:
        /// filling it from a neighbouring level writes a value the user never chose.
        /// </summary>
        [TestMethod]
        public void StatRangeIsAnInterpolationNotABound()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var mp = CachedDifficulty(target).Table(@"SFXDifficultyHandlerMP");
            var stats = mp.Categories.SelectMany(c => c.Levels).SelectMany(l => l.Stats).ToList();

            Assert.IsTrue(stats.Any(s => s.X > s.Y), @"Some stats must descend, or X/Y is being normalised somewhere");
            Assert.IsTrue(stats.Any(s => s.X < s.Y));
            Assert.IsTrue(stats.Any(s => Math.Abs(s.X - s.Y) < 0.0001), @"Most ship as a fixed value with X == Y");
            Assert.IsFalse(stats.Any(s => string.IsNullOrWhiteSpace(s.StatName)));
            Assert.IsFalse(stats.Any(s => string.IsNullOrEmpty(s.RawValue)), @"Raw text is needed for exact-match Remove");

            var incomplete = mp.IncompletePairs();
            foreach (var (cat, stat, missing) in incomplete)
                Console.WriteLine($@"incomplete: {cat}.{stat} missing at level(s) {string.Join(@",", missing)}");
            Assert.AreEqual(ExpectedIncompletePairs, incomplete.Count);

            // The gaps used to be confined to KaiLeng; Harvester has since joined it. What has held
            // throughout, and is the part that actually matters to the editor, is WHERE they are: a
            // stat that stops short stops at the hardest tier, rather than leaving a hole in the
            // middle of a row that a UI would be tempted to interpolate across.
            var topLevel = mp.Levels.Max();
            Assert.IsTrue(incomplete.All(i => i.MissingLevels.All(l => l == topLevel)),
                $@"A stat is missing at a level other than the top one ({topLevel}): {string.Join(@" | ", incomplete.Where(i => i.MissingLevels.Any(l => l != topLevel)).Select(i => $@"{i.Category}.{i.Stat}"))}");
        }

        /// <summary>
        /// Three lists govern maps and they do different jobs, and an editor that treats any one of
        /// them as "the maps" is wrong.
        ///
        /// The roster itself is not pinned here. DLC_MOD_PMR3 is a map pack under active development,
        /// so entries, selectability and art appear and change as levels are built - at the time of
        /// writing 44 entries against 42 selectable packages, where it was 38 against 60. Asserting
        /// those totals would fail on every new level and teach everyone to ignore the test.
        ///
        /// What is asserted is what has to be true of any roster: the three lists are read separately,
        /// they are allowed to disagree in either direction, the disagreement is computed rather than
        /// assumed, and the join key survives.
        /// </summary>
        [TestMethod]
        public void MapListsAreThreeSeparateThings()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);
            foreach (var w in maps.Warnings) Console.WriteLine($@"warning: {w}");
            Assert.AreEqual(0, maps.MalformedValues.Count);

            Console.WriteLine($@"maps: {maps.Maps.Count} entries, {maps.AvailableMaps.Count} selectable, " +
                              $@"{maps.AvailableWithoutEntry.Count} selectable with no entry, " +
                              $@"{maps.EntriesNotAvailable.Count} described but not selectable");

            Assert.IsTrue(maps.Maps.Count > 0, @"MasterMapList is empty");
            Assert.IsTrue(maps.AvailableMaps.Count > 0, @"AvailableMaps is empty; no map could be picked");

            // The two directions of disagreement are computed from the two lists rather than stated,
            // so they stay right as the roster changes. Both are currently populated in one direction
            // only - BioP_MPCnfl is described and not selectable - but either may be empty without
            // that being a fault, which is why the assertion is on the derivation and not the count.
            CollectionAssert.AreEquivalent(
                maps.AvailableMaps.Where(a => !maps.Maps.Any(m => string.Equals(m.PackageName, a, StringComparison.OrdinalIgnoreCase))).ToList(),
                maps.AvailableWithoutEntry,
                @"AvailableWithoutEntry must be derived from the two lists");
            Assert.IsFalse(maps.EntriesNotAvailable.Any(m => m.IsPlaceholder),
                @"A picker placeholder is not a map that cannot be played");

            // Entry 0 is the "Random" CHOICE, not a map: a banner and a name with no PackageName
            // behind them. Counting it as a map that cannot be played, or offering it for editing,
            // are both wrong; the spike hid it by filtering empty package names.
            Assert.AreEqual(1, maps.Placeholders.Count);
            Assert.IsTrue(maps.Placeholders[0].Image.Contains(@"Random", StringComparison.OrdinalIgnoreCase));

            // MPMapNames uses indexed assignment - mpmapnames[0]=... - so the index is in the property
            // NAME. Reading it as an ordinary list finds nothing at all, without failing.
            Assert.AreEqual(ExpectedMPMapNames, maps.MPMapNames.Count,
                @"MPMapNames is written with indexed assignment; an empty result means it is being read as a list");

            // ...and it is stale relative to AvailableMaps, which is a real editing hazard rather than
            // a parse problem: entries here name packages the config explicitly removes from play.
            Assert.IsTrue(maps.MPMapNames.Any(n => !maps.AvailableMaps.Contains(n, StringComparer.OrdinalIgnoreCase)),
                @"MPMapNames is expected to be stale relative to AvailableMaps");

            Assert.IsFalse(maps.Maps.Any(m => !m.IsPlaceholder && string.IsNullOrWhiteSpace(m.PackageName)),
                @"PackageName is the identity the other two lists join on");
        }

        /// <summary>
        /// Map, faction and difficulty banners are all the same kind of asset, addressed by instanced
        /// full path, which is what lets the editor show the game's own art instead of a stale copy
        /// that silently disagrees with what the player will see.
        /// </summary>
        [TestMethod]
        public void MatchSettingsOptionsCarryResolvableImages()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);

            Assert.AreEqual(ExpectedEnemyTypeOptions, maps.EnemyTypes.Count);
            Assert.AreEqual(ExpectedChallengeTypeOptions, maps.ChallengeTypes.Count);

            // Every difficulty banner must be present: they head the difficulty pages, and the count
            // is current data rather than a constant - Milan may add a fifth.
            Assert.IsTrue(maps.ChallengeTypes.All(c => !string.IsNullOrWhiteSpace(c.Image)));
            Assert.IsTrue(maps.EnemyTypes.All(e => !string.IsNullOrWhiteSpace(e.Image)));

            // The faction picker is a second gate on reachability beside WaveTypes, so its mapping to
            // a wave class has to survive.
            Assert.IsTrue(maps.EnemyTypes.Count(e => !string.IsNullOrWhiteSpace(e.Field(@"WaveClass"))) >= 4);

            // The vanilla pickers are stable content and every one of their banners resolves.
            foreach (var path in maps.EnemyTypes.Concat(maps.ChallengeTypes).Select(o => o.Image))
                Assert.IsTrue(path.Contains('.'), $@"Image path '{path}' is not package-qualified");

            // Map banners are not stable content, and DLC_MOD_PMR3 currently writes bare texture names
            // ("OptMapArmaxBlasteroid") where the shipped entries write an instanced full path
            // ("GUI_MPImages.MatchSettings.OptMapDish"). A bare name cannot be resolved - it does not
            // say which package to open - so the requirement is not that every path resolves, but that
            // every path that does NOT is reported rather than quietly becoming a blank tile.
            foreach (var path in maps.UnqualifiedImages) Console.WriteLine($@"unqualified image: {path}");

            var unresolvable = maps.AllImagePaths.Where(p => !p.Contains('.')).ToList();
            CollectionAssert.AreEquivalent(unresolvable, maps.UnqualifiedImages,
                @"Every unresolvable image path must be reported in UnqualifiedImages");
            foreach (var path in maps.UnqualifiedImages)
            {
                Assert.IsTrue(maps.Warnings.Any(w => w.Contains(path, StringComparison.Ordinal)),
                    $@"'{path}' cannot be resolved and was not warned about");
            }

            Console.WriteLine($@"image packages: {string.Join(@", ", maps.Maps.Where(m => m.ImagePackage != null).GroupBy(m => m.ImagePackage).OrderByDescending(g => g.Count()).Select(g => $@"{g.Key} ({g.Count()})"))}");
        }

        /// <summary>
        /// The difficulty columns get the game's own art, decoded and ready to draw.
        ///
        /// This is the whole path, because every step of it existed already and the grid still showed
        /// nothing: the config names a challenge texture, the catalogue pairs banner n with difficulty
        /// n, the banner library decodes it, and the payload has to be built AFTER that or it carries
        /// an object path a page cannot render. It was built before, so it did.
        /// </summary>
        [TestMethod]
        public void DifficultyColumnsCarryTheGamesOwnBanner()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);
            var wanted = maps.ChallengeTypes.Select(c => c.Image).Where(i => i != null).ToList();
            Assert.AreEqual(ExpectedChallengeTypeOptions, wanted.Count);

            // Only the four, not every image the map editor wants: the load stops as soon as it has
            // what was asked for, and these all live in the same package.
            var banners = BannerLibrary.Load(target, wanted);
            foreach (var problem in banners.Problems) Console.WriteLine($@"banner: {problem}");

            var payload = WavelistEditorPayload.Build(CachedWaves(target), maps, banners);
            foreach (var difficulty in payload[@"difficulties"])
            {
                var image = (string)difficulty[@"image"];
                Assert.IsNotNull(image, $@"{difficulty[@"key"]} has no decoded banner ({difficulty[@"imagePath"]})");
                StringAssert.StartsWith(image, @"data:image/png;base64,",
                    @"The page can only draw art it is handed inline");
                Assert.IsTrue((int)difficulty[@"imageWidth"] > 0);
            }
        }

        #endregion

        #region Text layer and the join - no game required

        /// <summary>
        /// The shipped text layer must load and be internally consistent. Every problem Validate()
        /// reports is an authoring mistake that produces a page in the wrong place or with no words on
        /// it, and none of them throw at runtime - a curation entry naming a group that does not exist
        /// just quietly puts the page at the root.
        /// </summary>
        [TestMethod]
        public void TextLayerLoadsAndValidates()
        {
            var layer = TextLayer.Load(SchemaFile(@"example.textlayer.json"));

            Assert.AreEqual(1, layer.Format);
            Assert.IsTrue(layer.Groups.Count > 0);
            Assert.IsTrue(layer.Curation.Count > 0);

            var problems = new CatalogueJoin(layer).Validate();
            foreach (var problem in problems) Console.WriteLine($@"problem: {problem}");
            Assert.AreEqual(0, problems.Count, string.Join(@" | ", problems));

            // Keys are written in whatever case each source used, and System.Text.Json does not keep
            // the comparer a dictionary property was initialised with. A case-sensitive miss looks
            // exactly like "nothing was authored", so it would be invisible and would hit most lookups.
            Assert.IsTrue(layer.Curation.ContainsKey(@"SFXGameContent.SFXPowerCustomAction_Pull"));
            Assert.IsTrue(layer.Descriptions.ContainsKey(@"CoolDownTime"));
        }

        /// <summary>
        /// The anchors the scanners compile in must match the ones the shipped layer declares. They
        /// are two copies of the same fact, and drift between them is silent: the scanner keeps
        /// working off its constant while the layer documents something else.
        /// </summary>
        [TestMethod]
        public void ShippedAnchorsMatchTheScannerConstants()
        {
            var layer = TextLayer.Load(SchemaFile(@"example.textlayer.json"));

            Assert.AreEqual(WavelistCatalogue.WaveBaseClass, layer.Anchor(@"waveBaseClass", null));
            // The anchor is a section and sections are case-insensitive identifiers throughout, so
            // the comparison has to be too.
            Assert.IsTrue(MapListCatalogue.SettingsClass.Equals(
                ClassNameOf(layer.Anchor(@"matchSettings", null)), StringComparison.OrdinalIgnoreCase),
                @"The match-settings anchor is a section; its class half must match the scanner constant");
            Assert.AreEqual(@"MasterMapList", layer.Anchor(@"mapList", null));
            Assert.AreEqual(@"MPMapNames", layer.Anchor(@"mpMapNames", null));

            // A missing anchor falls back to the built-in constant rather than to null - a scanner
            // searching for null finds nothing and reports an empty editor.
            Assert.AreEqual(@"fallback", layer.Anchor(@"notAnAnchor", @"fallback"));
        }

        /// <summary>
        /// Curation resolution: an exact entry wins, then the LONGEST matching family prefix, then
        /// unrecognised. Longest and not first: SFXPowerCustomActionMP has to beat
        /// SFXPowerCustomAction, and a shortest-first scan groups every MP power under the wrong
        /// family without failing.
        /// </summary>
        [TestMethod]
        public void CurationResolvesExactThenFamilyThenUnrecognised()
        {
            var join = new CatalogueJoin(TextLayer.Load(SchemaFile(@"example.textlayer.json")));

            var pull = join.ResolveCuration(FakeClass(@"SFXGameContent.SFXPowerCustomAction_Pull", @"SFXPowerCustomAction_Pull"));
            Assert.AreEqual(CurationDecision.Include, pull.Decision);
            Assert.AreEqual(TextSource.Authored, pull.Source);
            Assert.IsTrue(pull.Recognised);

            // The SFXCustomAction family is excluded wholesale...
            var stub = join.ResolveCuration(FakeClass(@"SFXGameContent.SFXCustomAction_Something", @"SFXCustomAction_Something"));
            Assert.AreEqual(CurationDecision.Exclude, stub.Decision);
            Assert.AreEqual(TextSource.Family, stub.Source);

            // ...but a class the old app really did have a page for is listed individually, and the
            // identity entry has to beat the family rule.
            var melee = join.ResolveCuration(FakeClass(
                @"SFXGameContentDLC_CON_MP5.SFXCustomAction_GethDestroyerMelee", @"SFXCustomAction_GethDestroyerMelee"));
            Assert.AreEqual(CurationDecision.Include, melee.Decision);

            // Unknown content is INCLUDED and flagged, never dropped: an exclusion list cannot
            // anticipate someone else's mod.
            var modded = join.ResolveCuration(FakeClass(@"SomeMod.SFXWave_Horde_Krogan", @"SFXWave_Horde_Krogan"));
            Assert.AreEqual(CurationDecision.Include, modded.Decision);
            Assert.IsFalse(modded.Recognised);
            Assert.AreEqual(CatalogueJoin.UnrecognisedGroup, modded.Group);

            // Partial curation filters properties, and nothing else does.
            var world = join.ResolveCuration(FakeClass(@"SFXGame.BioWorldInfo", @"BioWorldInfo"));
            Assert.AreEqual(CurationDecision.Partial, world.Decision);
            Assert.IsTrue(world.AllowsProperty(@"m_nAutoBotMPKit"));
            Assert.IsFalse(world.AllowsProperty(@"m_bIsPS3"));
            Assert.IsTrue(pull.AllowsProperty(@"anything"), @"Only partial curation filters properties");
        }

        /// <summary>
        /// The specific "section/property" form beats the "*/property" wildcard, because the wildcard
        /// carries the modal label and the specific form carries the exceptions - and the exceptions
        /// are the correct domain distinction, not noise.
        /// </summary>
        [TestMethod]
        public void PropertyTextPrefersTheSpecificOverTheWildcard()
        {
            var join = new CatalogueJoin(TextLayer.Load(SchemaFile(@"example.textlayer.json")));

            var shared = join.ResolveProperty(@"sfxgamecontent.sfxpowercustomaction_throw", @"cooldowntime");
            Assert.AreEqual(@"Recharge Time", shared.Label);
            Assert.AreEqual(@"sec", shared.Unit);
            Assert.AreEqual(TextSource.Shared, shared.Source);

            // Pull specialises the shared prose rather than replacing it, so the common sentence stays
            // translatable in one place.
            var specific = join.ResolveProperty(@"sfxgamecontent.sfxpowercustomaction_pull", @"cooldowntime");
            Assert.AreEqual(TextSource.Authored, specific.Source);
            StringAssert.StartsWith(specific.Description, shared.Description);
            Assert.IsTrue(specific.Description.Length > shared.Description.Length);

            // Nothing authored: the label is the property's own name. Honest, searchable, and
            // obviously different from a real label.
            var bare = join.ResolveProperty(@"somemod.sfxwave_horde_krogan", @"SomeNewProperty");
            Assert.AreEqual(@"SomeNewProperty", bare.Label);
            Assert.AreEqual(TextSource.Generated, bare.Source);
            Assert.IsNull(bare.Description);
        }

        /// <summary>
        /// Only 468 of 2,304 candidate classes are named anywhere in the game, so the last step of the
        /// title chain has to always produce something. A page called "Reegar" is strictly better than
        /// no page at all, and recording WHICH step won is what makes the ugly ones findable.
        /// </summary>
        [TestMethod]
        public void TitlesFallBackToTheClassName()
        {
            var join = new CatalogueJoin(TextLayer.Load(SchemaFile(@"example.textlayer.json")));

            Assert.AreEqual(@"Reegar", CatalogueJoin.TitleFromClassName(@"SFXGameContentDLC_CON_MP5.SFXProjectile_Reegar_MP"));
            Assert.AreEqual(@"Shotgun Quarian", CatalogueJoin.TitleFromClassName(@"SFXWeapon_Shotgun_Quarian"));

            // An abstract base whose whole name is scaffolding keeps it: the user asked for these to
            // be editable for global changes, and a blank title is worse than an ugly one.
            Assert.AreEqual(@"SFXProjectile", CatalogueJoin.TitleFromClassName(@"SFXGame.SFXProjectile"));
            Assert.AreEqual(@"BioWorldInfo", CatalogueJoin.TitleFromClassName(@"SFXGame.BioWorldInfo"));

            var authored = join.ResolveTitle(FakeClass(@"SFXGameContent.SFXPowerCustomAction_Pull", @"SFXPowerCustomAction_Pull"));
            Assert.AreEqual(@"Pull", authored.Title);
            Assert.AreEqual(@"authored", authored.TitleSource);

            var generated = join.ResolveTitle(FakeClass(@"SomeMod.SFXWeapon_Rifle_Krogan", @"SFXWeapon_Rifle_Krogan"));
            Assert.AreEqual(@"Rifle Krogan", generated.Title);
            Assert.AreEqual(@"className", generated.TitleSource);

            // The game's own name, when the name scan finds one, sits between the two.
            var fromGame = join.ResolveTitle(FakeClass(@"SomeMod.SFXWeapon_Rifle_Krogan", @"SFXWeapon_Rifle_Krogan"),
                                             _ => (@"Striker Assault Rifle", @"storeCard"));
            Assert.AreEqual(@"Striker Assault Rifle", fromGame.Title);
            // Which rung found the name is recorded, not inferred - the schema enumerates the values.
            Assert.AreEqual(@"storeCard", fromGame.TitleSource);
        }


        /// <summary>
        /// Every example document must validate against its schema, in the test suite rather than in
        /// an ad-hoc tool nobody runs. The schemas are the contract between the generator, the editor
        /// and the compiler, and the examples are what everyone reads first; a schema that has drifted
        /// from its example teaches the wrong shape to whoever copies it.
        ///
        /// Mod Manager already ships Newtonsoft.Json.Schema, so this costs nothing to keep.
        /// </summary>
        [TestMethod]
        [DataRow(@"textlayer.schema.json", @"example.textlayer.json")]
        [DataRow(@"definitions.schema.json", @"example.definitions.json")]
        [DataRow(@"project.schema.json", @"example.project.json")]
        [DataRow(@"build.schema.json", @"example.build.json")]
        public void ExamplesValidateAgainstTheirSchemas(string schemaFile, string exampleFile)
        {
            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(schemaFile)));
            var document = JToken.Parse(File.ReadAllText(SchemaFile(exampleFile)));

            var valid = document.IsValid(schema, out IList<ValidationError> errors);
            foreach (var e in errors ?? new List<ValidationError>())
                Console.WriteLine($@"{exampleFile}:{e.LineNumber} {e.Path}: {e.Message}");

            Assert.IsTrue(valid, $@"{exampleFile} does not satisfy {schemaFile}: " +
                string.Join(@" | ", (errors ?? new List<ValidationError>()).Select(e => $@"{e.Path}: {e.Message}")));

            // Negative control. A validator that accepts everything passes the assertion above just as
            // happily as a correct one, and a schema silently reduced to "any object" - by a typo in a
            // \, say - would never be noticed. Removing a required property must be rejected.
            var required = schema.Required.FirstOrDefault();
            Assert.IsNotNull(required, $@"{schemaFile} declares no required root properties, so this test cannot prove it validates anything");

            var broken = (JObject)document.DeepClone();
            broken.Remove(required);
            Assert.IsFalse(broken.IsValid(schema),
                $@"{schemaFile} accepted a document with '{required}' missing, so it is not constraining anything");
        }

        private static CatalogueClass FakeClass(string section, string name) =>
            new() { Section = section, Name = name };

        private static string ClassNameOf(string section) =>
            section != null && section.Contains('.') ? section[(section.LastIndexOf('.') + 1)..] : section;

        /// <summary>
        /// Finds a file in the repo's modmaker/schema folder by walking up from the test assembly.
        /// </summary>
        private static string SchemaFile(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, @"MassEffectModManagerCore", @"modmaker", @"schema", name);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException($@"Could not find modmaker/schema/{name} above {AppContext.BaseDirectory}");
        }

        #endregion

        #region Bundle serialisation - needs a real LMP install

        /// <summary>
        /// The end-to-end check: read the installed game, join it to the shipped text layer, and emit
        /// a bundle that satisfies the schema the editor and compiler are written against.
        ///
        /// Validating the REAL generated output rather than a hand-written example is the point. The
        /// example was authored to match the schema; the generator was not, and every field it emits
        /// is a chance to have understood the contract slightly differently.
        /// </summary>
        [TestMethod]
        public void GeneratedBundleSatisfiesTheSchema()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var built = CachedBundle(target);
            foreach (var w in built.Warnings) Console.WriteLine($@"warning: {w}");
            Console.WriteLine($@"classes emitted {built.ClassesEmitted}, excluded {built.ClassesExcluded}, unrecognised {built.ClassesUnrecognised}");
            Console.WriteLine($@"properties emitted {built.PropertiesEmitted}, with compiled default {built.PropertiesWithCompiledDefault}, at type zero {built.PropertiesDefaultedToTypeZero}");
            Console.WriteLine($@"value trees: {built.ValueTreeLeaves} editable leaves, {built.ValueTreeLiterals} preserved literals, {built.PropertiesWithoutShippedValue} structs with no shipped value, {built.ValueTreesTooDeep} too deep");

            var schema = JSchema.Parse(File.ReadAllText(SchemaFile(@"definitions.schema.json")));
            var valid = built.Bundle.IsValid(schema, out IList<ValidationError> errors);
            foreach (var e in (errors ?? new List<ValidationError>()).Take(20))
                Console.WriteLine($@"schema: {e.Path}: {e.Message}");

            Assert.IsTrue(valid, $@"Generated bundle does not satisfy definitions.schema.json: " +
                string.Join(@" | ", (errors ?? new List<ValidationError>()).Take(5).Select(e => $@"{e.Path}: {e.Message}")));
        }

        /// <summary>
        /// The bundle has to describe the real game, not just parse. A schema-valid bundle with no
        /// content in it would pass the test above perfectly.
        /// </summary>
        [TestMethod]
        public void GeneratedBundleDescribesTheInstalledGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var built = CachedBundle(target);
            var bundle = built.Bundle;

            Assert.IsTrue(built.ClassesEmitted > 500, $@"Only {built.ClassesEmitted} classes were emitted");
            Assert.IsTrue(built.PropertiesEmitted > 500, $@"Only {built.PropertiesEmitted} properties were emitted");

            // Every class id must be unique: project keys are "<classId>/<leafId>", so a collision
            // silently makes two different pages write to the same key.
            var ids = bundle[@"classes"].Select(c => (string)c[@"id"]).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                @"Duplicate class ids would make two pages write to the same project key");

            // Property ids likewise.
            var propIds = bundle[@"properties"].Select(x => (string)x[@"id"]).ToList();
            Assert.AreEqual(propIds.Count, propIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            // Every "extends" must resolve, or the inheritance chain the editor walks is broken.
            var idSet = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var cls in bundle[@"classes"])
            {
                var ext = (string)cls[@"extends"];
                if (ext != null) Assert.IsTrue(idSet.Contains(ext), $@"Class '{cls[@"id"]}' extends '{ext}', which is not in the bundle");
            }

            // A per-object class must carry its instances, or its editor has nothing to select.
            var cerberus = bundle[@"classes"].First(c => (string)c[@"id"] == @"sfxgamempcontent.sfxwave_horde_cerberus");
            Assert.AreEqual(ExpectedWavesPerFaction, cerberus[@"perObject"][@"instances"].Count());

            // ...and its class-path section is genuinely null: it has none, and its shared values
            // arrive from SFXWave_Horde by inheritance. Omitting the field instead would say
            // "unknown", which is a different thing.
            Assert.AreEqual(JTokenType.Null, cerberus[@"section"].Type);

            // Every editable leaf carries a default, wherever it sits in a value tree. A property
            // nothing sets still has one - the type's zero value - so an omitted default would say
            // "unknown" about a value that is known. Walked recursively, because most leaves are now
            // inside structs and arrays rather than at the top of a property.
            var leafKinds = new[] { @"float", @"int", @"bool", @"string", @"enum" };
            var leavesChecked = 0;

            void CheckLeaves(JToken node, string where)
            {
                if (node is not JObject o) return;
                var kind = (string)o[@"kind"];
                if (kind == null) return;

                if (leafKinds.Contains(kind))
                {
                    Assert.IsNotNull(o[@"id"], $@"Leaf at {where} has no id");
                    Assert.IsNotNull(o[@"default"], $@"Leaf '{o[@"id"]}' at {where} has no default");
                    leavesChecked++;
                }
                else if (kind == @"struct")
                {
                    foreach (var m in o[@"members"]) CheckLeaves(m[@"value"], where);
                }
                else if (kind == @"array")
                {
                    foreach (var i in o[@"items"]) CheckLeaves(i, where);
                }
            }

            foreach (var entry in bundle[@"properties"]) CheckLeaves(entry[@"value"], (string)entry[@"id"]);
            Console.WriteLine($@"{leavesChecked} editable leaves checked for id and default");
            Assert.IsTrue(leavesChecked > 10000, $@"Only {leavesChecked} leaves were found; the value trees are probably not being built");

            // Leaf ids must be unique within their property, or two controls write to one project key.
            foreach (var entry in bundle[@"properties"])
            {
                var leafIds = new List<string>();
                void Collect(JToken node)
                {
                    if (node is not JObject o) return;
                    var kind = (string)o[@"kind"];
                    if (kind == null) return;
                    if (leafKinds.Contains(kind)) leafIds.Add((string)o[@"id"]);
                    else if (kind == @"struct") foreach (var m in o[@"members"]) Collect(m[@"value"]);
                    else if (kind == @"array") foreach (var i in o[@"items"]) Collect(i);
                }
                Collect(entry[@"value"]);
                Assert.AreEqual(leafIds.Count, leafIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    $@"Property '{entry[@"id"]}' has duplicate leaf ids");
            }

            // Content the text layer has never heard of is present and flagged, not dropped.
            Assert.IsTrue(built.ClassesUnrecognised > 0);
            Assert.IsTrue(bundle[@"classes"].Any(c => (bool)c[@"recognised"] == false));

            // The difficulty axis is read from the game, never counted to four.
            Assert.AreEqual(ExpectedMPDifficultyLevels, bundle[@"dimensions"][@"difficulty"][@"values"].Count());

            // Curation must have actually done something in both directions.
            Assert.IsTrue(built.ClassesExcluded > 0, @"Nothing was excluded, so curation is not being applied");
            Assert.IsTrue(bundle[@"collections"].Any(), @"No structured editors were registered");
        }


        /// <summary>
        /// Compiled class defaults are the other half of the effective-value chain, and reading them
        /// needs the Default__ object in the partial load - its ClassName is the class's OWN name, so
        /// no predicate over class names finds it. Leave it out and every default is null while the
        /// scan still reports success.
        /// </summary>
        [TestMethod]
        public void CompiledDefaultsAreReadAndInherited()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = CachedClasses(target);
            var withDefaults = classes.Values.Count(c => c.Defaults is { Count: > 0 });
            Assert.IsTrue(withDefaults > 500,
                $@"Only {withDefaults} classes had compiled defaults - the Default__ objects are probably not being loaded");

            // A subclass restates only what it changes, so most properties resolve to an ancestor's
            // value. Reading only the leaf reports "no default" for content that plainly has one.
            var hornet = ClassScanner.FindByName(@"SFXWeapon_AI_Hornet", classes);
            var resolved = ClassDefaults.CountWithDefaults(hornet.Section, classes);
            var ownOnly = hornet.Defaults?.Count ?? 0;
            Console.WriteLine($@"Hornet: {resolved} properties resolve to a default, {ownOnly} declared on the class itself");
            Assert.IsTrue(resolved > ownOnly, @"Defaults must resolve up the SuperClass chain, not just off the leaf");
        }

        /// <summary>
        /// Rule 1, in the bundle: config REPLACES the compiled default rather than merging with it, so
        /// where the shipped config sets a property, that - not the compiled default - is what the
        /// game uses and what "revert" has to return to.
        ///
        /// Emitting the compiled default there would be wrong for most of the interesting properties,
        /// and wrong invisibly: the number looks plausible and reverting quietly changes the game.
        /// </summary>
        [TestMethod]
        public void ConfigBeatsTheCompiledDefaultInTheBundle()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var classes = CachedClasses(target);
            var config = CachedConfig(target);
            var bundle = CachedBundle(target).Bundle;

            int checkedPairs = 0;
            foreach (var entry in bundle[@"properties"])
            {
                var section = (string)entry[@"class"];
                var name = (string)entry[@"name"];
                var emitted = entry[@"value"][@"default"];
                if (emitted == null) continue;

                var live = config.LiveValues(section, name).ToList();
                if (live.Count != 1) continue;

                var compiled = ClassDefaults.Effective(section, name, classes);
                if (compiled == null) continue;
                if (string.Equals(compiled.Trim(), live[0].Value?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                // config and the compiled default genuinely disagree here - the bundle must carry the
                // config value. Compared numerically where the leaf is a number: the bundle stores a
                // JSON number, so "45.0" and 45 are the same value written two ways.
                var expected = live[0].Value.Trim();
                var kind = (string)entry[@"value"][@"kind"];
                if (kind is @"float" or @"int")
                {
                    var a = DifficultyCatalogue.ParseDouble(expected);
                    var b = DifficultyCatalogue.ParseDouble(emitted.ToString());
                    if (a == null || b == null) continue;
                    Assert.AreEqual(a.Value, b.Value, 0.0001,
                        $@"{section}.{name}: bundle carries the compiled default '{compiled}' instead of the config value");
                }
                else
                {
                    Assert.AreEqual(expected, emitted.ToString().Trim(),
                        $@"{section}.{name}: bundle carries the compiled default '{compiled}' instead of the config value");
                }
                checkedPairs++;
            }

            Console.WriteLine($@"{checkedPairs} properties where config and the compiled default disagree");
            Assert.IsTrue(checkedPairs > 0,
                @"No property had config and compiled default disagreeing, so this test proved nothing");
        }

        /// <summary>
        /// How big the bundle is, in bytes and per section.
        ///
        /// This is the number the generic value editor's delivery has to be designed around: the page
        /// is handed its payload as an injected string before any script runs, and there is a size at
        /// which that stops being reasonable. Measured rather than estimated, and asserted so that a
        /// change which quietly doubles it is visible here rather than as a slow editor.
        /// </summary>
        [TestMethod]
        public void TheBundleIsSmallEnoughToHandToAPage()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var bundle = CachedBundle(target).Bundle;
            var whole = bundle.ToString(Newtonsoft.Json.Formatting.None);

            Console.WriteLine($@"bundle: {whole.Length:N0} chars ({whole.Length / 1024.0 / 1024.0:F2} MB)");
            foreach (var section in bundle.Properties().OrderByDescending(p => p.Value.ToString(Newtonsoft.Json.Formatting.None).Length))
            {
                var size = section.Value.ToString(Newtonsoft.Json.Formatting.None).Length;
                var count = section.Value is JArray array ? array.Count : 0;
                Console.WriteLine($@"  {section.Name,-20} {size,10:N0} chars{(count > 0 ? $@"  ({count:N0} entries)" : string.Empty)}");
            }

            // What of `properties` the PAGE would not use. The verbatim originals exist so the
            // compiler can re-emit untouched array entries byte-for-byte; an editor only ever renders
            // the parsed tree. If that is a large share, the editor's payload is a trim rather than a
            // new delivery mechanism.
            long originals = 0, trees = 0;
            foreach (var property in bundle[@"properties"])
            {
                foreach (var field in ((JObject)property).Properties())
                {
                    var size = field.Value.ToString(Newtonsoft.Json.Formatting.None).Length;
                    if (field.Name is @"originalValue" or @"originalValues") originals += size;
                    else if (field.Name == @"value") trees += size;
                }
            }

            Console.WriteLine($@"  of which originals  {originals,10:N0} chars ({originals * 100.0 / whole.Length:F1}% of the bundle)");
            Console.WriteLine($@"  of which trees      {trees,10:N0} chars ({trees * 100.0 / whole.Length:F1}% of the bundle)");

            Assert.IsTrue(whole.Length > 100_000, @"The bundle is implausibly small; it did not build");
        }

        /// <summary>
        /// The value editor's payload is the bundle minus what only the compiler needs.
        ///
        /// The verbatim originals are 1.45 MB of an 8.46 MB bundle and a page never renders them, and
        /// a class with nothing editable is a page with nothing on it - which, three and a half
        /// thousand times over, is what makes the list unusable.
        /// </summary>
        [TestMethod]
        public void TheValuePayloadCarriesOnlyWhatAPageDraws()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var bundle = CachedBundle(target).Bundle;
            var payload = ValuesEditorPayload.Build(bundle);

            var whole = payload.ToString(Newtonsoft.Json.Formatting.None);
            Console.WriteLine($@"payload: {whole.Length:N0} chars ({whole.Length / 1024.0 / 1024.0:F2} MB), " +
                              $@"{payload[@"classes"].Count():N0} classes, {payload[@"properties"].Count():N0} properties");

            Assert.IsTrue(whole.Length < bundle.ToString(Newtonsoft.Json.Formatting.None).Length,
                @"The payload must be smaller than the bundle it came from");

            foreach (var property in payload[@"properties"])
            {
                foreach (var field in new[] { @"originalValue", @"originalValues", @"arrayType" })
                    Assert.IsNull(property[field], $@"{property[@"id"]} still carries {field}");
            }

            // Every class offered has something to edit, and knows where it is filed.
            var withProperties = payload[@"properties"].Select(p => (string)p[@"class"])
                                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.IsTrue(payload[@"classes"].Any(), @"No classes reached the payload");
            foreach (var cls in payload[@"classes"])
            {
                Assert.IsTrue(withProperties.Contains((string)cls[@"id"]),
                    $@"{cls[@"id"]} is offered as a page but has nothing editable on it");
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)cls[@"group"]),
                    $@"{cls[@"id"]} has no group, so a page has nowhere to file it");
            }
        }

        /// <summary>
        /// A key the editor would write is a key the compiler resolves.
        ///
        /// This is the seam that matters: the page mints project keys out of a class id and a leaf id
        /// it got from the bundle, and the compiler resolves those against the installed config by a
        /// completely separate route. Nothing but a test that walks the real data end to end would
        /// notice the two drifting - and a key that does not resolve is a refused compile, so the
        /// failure is loud but total.
        /// </summary>
        [TestMethod]
        public void EveryKeyTheEditorMintsIsOneTheCompilerResolves()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var payload = ValuesEditorPayload.Build(CachedBundle(target).Bundle);
            var config = CachedConfig(target);
            var classes = CachedClasses(target);

            var inputs = new CompileInputs
            {
                Sections = config.BuildSectionIndex(),
                Classes = classes,
                Instances = InstanceCatalogue.Build(config, classes),
                MountPriority = 4800,
            };

            // A spread across the whole payload rather than the first few, which would all come from
            // one class and prove only that one section resolves.
            var scalars = payload[@"properties"]
                .Where(p => (string)p[@"value"]?[@"kind"] is @"int" or @"float")
                .ToList();

            // A float leaf whose default is not a number is a config line the game ships with a typo
            // in it - "0.05;" - and the generator now names each one. They are excluded from the
            // arithmetic below rather than crashing it, and reported here, because they are a finding
            // about the install rather than a failure of the round trip.
            var unparseable = scalars
                .Where(p => p[@"value"][@"default"]?.Type == JTokenType.String)
                .Select(p => $@"{p[@"id"]} = {p[@"value"][@"default"]}")
                .ToList();

            foreach (var odd in unparseable) Console.WriteLine($@"unparseable shipped value: {odd}");
            scalars = scalars.Where(p => p[@"value"][@"default"]?.Type != JTokenType.String).ToList();

            Assert.IsTrue(scalars.Count > 100, $@"Only {scalars.Count} scalar properties to sample from");

            var step = Math.Max(1, scalars.Count / 120);
            var refused = new List<string>();
            var compiled = 0;

            for (int i = 0; i < scalars.Count; i += step)
            {
                var property = scalars[i];
                var leaf = property[@"value"];
                var key = $@"{(string)property[@"class"]}/{(string)leaf[@"id"]}";

                // Moved off the shipped value, or the compiler correctly writes nothing.
                var project = ModMakerProject.Create(@"Round trip", @"LMP", @"1");
                project.Values[key] = ((double?)leaf[@"default"] ?? 0) + 1;

                var result = ProjectCompiler.Compile(project, inputs);
                if (result.Succeeded) compiled++;
                else refused.Add($@"{key}: {string.Join(@"; ", result.Errors.Select(e => e.Message))}");
            }

            Console.WriteLine($@"keys compiled: {compiled}, refused: {refused.Count}");
            foreach (var problem in refused.Take(10)) Console.WriteLine($@"  {problem}");

            Assert.AreEqual(0, refused.Count, string.Join(Environment.NewLine, refused.Take(5)));
        }

        /// <summary>
        /// TLK references are marked in the bundle, so the editor can show the words behind the number.
        ///
        /// The flag rather than the kind, and that is the point of the test: a string ref IS an int
        /// and is edited and compiled as one, so `kind` must stay "int" or the compiler would have to
        /// learn a type it does not need. What is being asserted is that the two facts stay separate -
        /// the number's type, and what the number means.
        /// </summary>
        [TestMethod]
        public void StringReferencesAreMarkedInTheBundle()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var bundle = CachedBundle(target).Bundle;
            var strings = bundle[@"properties"]
                .Where(p => (bool?)p[@"value"]?[@"stringref"] == true)
                .ToList();

            Console.WriteLine($@"string-ref properties: {strings.Count}");
            Assert.IsTrue(strings.Count > 0, @"LMP declares StringRefProperty config properties; none were marked");

            foreach (var property in strings)
            {
                Assert.AreEqual(@"int", (string)property[@"value"][@"kind"],
                    $@"{property[@"id"]} must still be an int - the compiler writes a string ref as a number");
                Assert.IsNotNull(property[@"value"][@"default"],
                    $@"{property[@"id"]} has no shipped value to preview");
            }

            // A struct's fields are not marked, and that is deliberate rather than an oversight: a
            // value tree is built from config TEXT, where a string ref and an ordinary int are the
            // same characters. Marking those would mean guessing from field names, and a wrong guess
            // shows a real TLK line beside a number that is not a string reference at all.
            var nested = bundle[@"properties"].Count(p => p[@"value"]?[@"kind"] is { } k
                                                          && (string)k is @"struct" or @"array");
            Console.WriteLine($@"tree-valued properties (never marked): {nested}");
        }

        #region Objectives - needs a real LMP install

        private static ObjectiveCatalogue _objectives;
        private static WeaponCatalogue _weapons;

        /// <summary>
        /// How long a match is comes from the game, not from a constant.
        ///
        /// Eleven is what LMP ships and what every screenshot shows, which is exactly what makes a
        /// hardcoded eleven dangerous: it would be right until the day it silently was not, and an
        /// editor offering waves that do not exist looks no different from one that is correct.
        /// </summary>
        [TestMethod]
        public void TheMatchLengthIsReadFromTheGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var objectives = CachedObjectives(target);

            Assert.AreEqual(11, objectives.NumWaves, @"LMP sets NumWaves=11 on the horde coordinator");
            Assert.IsTrue(objectives.ExtractionWaveEnabled, @"bExtractionWaveEnabled is true");

            // The extraction is not "wave 11", it is "the last wave" - it moves with NumWaves.
            Assert.AreEqual(objectives.NumWaves, objectives.ExtractionWave);
            Assert.AreEqual(11, objectives.LastWave);

            // ...and it is the wave the shipped array gives no credit scale to, which is how we know
            // the two readings agree rather than merely both saying 11.
            var extraction = objectives.Waves.First(w => w.WaveIndex == objectives.ExtractionWave);
            Assert.IsNull(extraction.CreditScale);
        }

        /// <summary>
        /// The cap never hides a wave that already carries an objective. A shipped wave past NumWaves
        /// is a mod mid-edit or a NumWaves we misread; either way the data is more certain than the
        /// number, so the editor still offers it and the catalogue says so out loud.
        /// </summary>
        [TestMethod]
        public void TheWaveCapNeverHidesAnObjectiveTheGameAlreadyHas()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var objectives = CachedObjectives(target);
            var highest = objectives.Waves.Max(w => w.WaveIndex);

            Assert.IsTrue(objectives.LastWave >= highest,
                $@"LastWave {objectives.LastWave} must not be below the highest objective wave {highest}");
        }

        private static WeaponCatalogue CachedWeapons(GameTarget target)
        {
            lock (CacheLock)
            {
                if (_weapons != null) return _weapons;
                return _weapons = WeaponCatalogue.Build(CachedConfig(target), CachedClasses(target));
            }
        }

        /// <summary>
        /// Weapons are found by descent, never by the SFXWeapon name prefix.
        ///
        /// The prefix is the obvious rule and it is wrong by 56 classes: it also catches
        /// SFXWeaponFactory, SFXWeaponNode, SFXWeaponUseModule, the SFXWeaponGameEffect_* pair and all
        /// 43 weapon MODS. None of those are weapons, none carry weapon stats, and an editor built on
        /// the prefix would offer rate-of-fire on a pickup factory.
        /// </summary>
        [TestMethod]
        public void WeaponsAreFoundByDescentNotByName()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            foreach (var warning in weapons.Warnings) Console.WriteLine($@"weapons: {warning}");

            var byName = CachedClasses(target).Values
                .Count(c => c.Name.StartsWith(@"SFXWeapon", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($@"by descent: {weapons.Weapons.Count}, by name prefix: {byName}");

            // A RELATIONSHIP, not a snapshot. Milan is under active development and gains weapons
            // between runs, so pinning the total asserts today's content rather than the rule this
            // test exists for - and a count that has to be bumped whenever the game changes teaches
            // people to bump it without reading why it moved.
            Assert.IsTrue(weapons.Weapons.Count > 50,
                $@"Only {weapons.Weapons.Count} weapons were found, which is too few to be a real read");
            Assert.IsTrue(byName > weapons.Weapons.Count + 40,
                @"The name prefix must be demonstrably wider than descent, or this test proves nothing");

            // The specific things the prefix wrongly catches.
            foreach (var notAWeapon in new[] { @"SFXWeaponFactory", @"SFXWeaponMod", @"SFXWeaponNode" })
            {
                Assert.IsFalse(weapons.Weapons.Any(w => w.ClassName.Equals(notAWeapon, StringComparison.OrdinalIgnoreCase)),
                    $@"{notAWeapon} is not a weapon and must not be listed as one");
            }
        }

        /// <summary>
        /// The player and AI sides are separate, and the AI side is not simply "the player list again".
        /// </summary>
        [TestMethod]
        public void AIWeaponsAreSegmentedFromPlayerWeapons()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);

            Console.WriteLine($@"player: {weapons.PlayerWeapons.Count()}, ai: {weapons.AIWeapons.Count()}");

            // The two sides must partition the catalogue exactly: every weapon on one side or the
            // other, none on both, none on neither. That is the property the editor depends on, and
            // unlike the totals it does not move when Milan adds a gun.
            Assert.AreEqual(weapons.Weapons.Count,
                weapons.PlayerWeapons.Count() + weapons.AIWeapons.Count(),
                @"Every weapon belongs to exactly one side");

            Assert.IsTrue(weapons.AIWeapons.Any(), @"LMP ships AI weapons");
            Assert.IsTrue(weapons.PlayerWeapons.Any(), @"LMP ships player weapons");
            Assert.IsTrue(weapons.AIWeapons.All(w => w.ClassName.StartsWith(@"SFXWeapon_AI_", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(weapons.PlayerWeapons.Any(w => w.ClassName.StartsWith(@"SFXWeapon_AI_", StringComparison.OrdinalIgnoreCase)),
                @"An SFXWeapon_AI_ class must never be filed as a player weapon");
        }

        /// <summary>
        /// An AI weapon is paired to a player weapon only where it actually descends from one.
        ///
        /// Name-matching AI_Hornet to Hornet looks right and produces nine wrong answers: Harvester,
        /// Ravager, the Scion cannon, the Geth rocket launcher, the Geth Prime rifle and the rest
        /// descend straight from a base or from SFXWeapon. They are enemy-only weapons, not variants
        /// of anything, and presenting them as variants would invent a player weapon to compare
        /// against. The count is asserted so that a future Milan build ADDING a player counterpart
        /// shows up here rather than silently.
        /// </summary>
        [TestMethod]
        public void OnlyAIWeaponsThatDescendFromAPlayerWeaponArePaired()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var paired = weapons.AIWeapons.Where(w => w.PairedSection != null).ToList();
            var lone = weapons.AIWeapons.Where(w => w.PairedSection == null).ToList();

            foreach (var w in weapons.AIWeapons.OrderBy(w => w.ClassName, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($@"  {w.ClassName,-38} -> {w.PairedSection ?? @"(enemy-only)"}");

            // Both kinds must exist - a rule that paired everything, or nothing, would be wrong in a
            // way the per-case checks below could not catch on their own.
            Console.WriteLine($@"paired: {paired.Count}, enemy-only: {lone.Count}");
            Assert.IsTrue(paired.Count > 0, @"Some AI weapons are variants of a player weapon");
            Assert.IsTrue(lone.Count > 0, @"Some AI weapons are enemy-only");
            Assert.AreEqual(weapons.AIWeapons.Count(), paired.Count + lone.Count);

            // Every pairing points at a real, player-side weapon - never at another AI weapon.
            foreach (var w in paired)
            {
                var partner = weapons.Find(w.PairedSection);
                Assert.IsNotNull(partner, $@"{w.ClassName} pairs to {w.PairedSection}, which is not a weapon");
                Assert.IsFalse(partner.IsAI, $@"{w.ClassName} must not pair to another AI weapon");
            }

            // The Hornet is the worked example in both directions.
            var hornet = weapons.Find(@"SFXGameContent.SFXWeapon_AI_Hornet");
            Assert.IsNotNull(hornet);
            StringAssert.Contains(hornet.PairedSection, @"SFXWeapon_SMG_Hornet");

            Assert.IsNull(weapons.Weapons.First(w => w.ClassName == @"SFXWeapon_AI_Harvester").PairedSection,
                @"The Harvester has no player counterpart");
        }

        /// <summary>
        /// A weapon's effective value comes from its own config, then an ancestor's, then the compiled
        /// default - and the editor is told WHICH, because that decides what an edit means.
        ///
        /// Weapons inherit heavily: SFXWeapon alone declares 135 properties. An editor showing only a
        /// weapon's own section would show a Hornet with almost nothing on it, and one that silently
        /// wrote an inherited value back would pin today's shared number onto one weapon.
        /// </summary>
        [TestMethod]
        public void WeaponValuesCarryTheScopeTheyCameFrom()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var classes = CachedClasses(target);
            var index = CachedConfig(target).BuildSectionIndex();

            var counts = new Dictionary<WeaponValueScope, int>();
            foreach (var weapon in weapons.Weapons)
            foreach (var name in WeaponCatalogue.PropertyNames(weapon, classes))
            {
                var value = weapons.Effective(weapon, name, index, classes);
                counts[value.Scope] = counts.GetValueOrDefault(value.Scope) + 1;
            }

            foreach (var kv in counts.OrderBy(k => k.Key)) Console.WriteLine($@"  {kv.Key,-10} {kv.Value}");

            Assert.IsTrue(counts.GetValueOrDefault(WeaponValueScope.Own) > 0, @"Weapons set values of their own");

            // The seventeen settled type-zeros now resolve; the eleven unconfirmed ones must NOT.
            // Both halves matter: resolving everything would hide that four struct, four array and
            // two int properties are still open questions - see docs/16-weapon-defaults.md.
            Assert.IsTrue(counts.GetValueOrDefault(WeaponValueScope.TypeDefault) > 0,
                @"Bool, float, string and string-ref absences resolve to the type's zero");
            Assert.IsTrue(counts.GetValueOrDefault(WeaponValueScope.Unset) > 0,
                @"Struct, array and int absences stay unresolved rather than being given an invented zero");

            foreach (var (type, zero) in new[]
                     {
                         (@"BoolProperty", @"False"), (@"FloatProperty", @"0.0"),
                         (@"StrProperty", string.Empty), (@"StringRefProperty", @"0"),
                     })
                Assert.AreEqual(zero, WeaponCatalogue.TypeZero(type), $@"{type}'s zero is settled");

            foreach (var open in new[] { @"StructProperty", @"ArrayProperty", @"IntProperty" })
                Assert.IsNull(WeaponCatalogue.TypeZero(open),
                    $@"{open} has no settled zero and must not be given one");
            Assert.IsTrue(counts.GetValueOrDefault(WeaponValueScope.Inherited) > counts.GetValueOrDefault(WeaponValueScope.Own),
                @"Most of what a weapon has is inherited - that is the shape this editor exists to show");

            // A value the weapon sets itself reports its own section, and one it does not reports the
            // ancestor's. Checked on a real weapon rather than asserted in the abstract.
            var hornet = weapons.Find(@"SFXGameContent.SFXWeapon_SMG_Hornet");
            Assert.IsNotNull(hornet, @"The Hornet must be readable");

            var damage = weapons.Effective(hornet, @"damage", index, classes);
            Assert.AreEqual(WeaponValueScope.Own, damage.Scope, @"The Hornet sets its own damage");
            Assert.AreEqual(hornet.Section, damage.FromSection);

            var inherited = WeaponCatalogue.PropertyNames(hornet, classes)
                .Select(n => weapons.Effective(hornet, n, index, classes))
                .First(v => v.Scope == WeaponValueScope.Inherited);
            Assert.AreNotEqual(hornet.Section, inherited.FromSection,
                @"An inherited value must name the ancestor it came from, not the weapon");
        }

        /// <summary>
        /// Every weapon lands in a category, and the categories are the game's own bases rather than
        /// a list someone typed.
        /// </summary>
        [TestMethod]
        public void WeaponsAreGroupedByTheGamesOwnBases()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);

            foreach (var group in weapons.Weapons.GroupBy(w => w.Category).OrderByDescending(g => g.Count()))
                Console.WriteLine($@"  {group.Key,-16} {group.Count()}");

            Assert.IsTrue(weapons.Weapons.All(w => !string.IsNullOrWhiteSpace(w.Category)),
                @"Every weapon must be filed somewhere");

            foreach (var expected in new[] { @"Assault Rifle", @"Shotgun", @"Pistol", @"Sniper Rifle", @"SMG" })
            {
                Assert.IsTrue(weapons.Weapons.Any(w => w.Category == expected),
                    $@"No weapon was filed under {expected}");
            }
        }

        /// <summary>
        /// The payload keeps the player and enemy weapons apart, and files each side by the game's
        /// own bases.
        /// </summary>
        [TestMethod]
        public void WeaponPayloadSegmentsPlayerAndEnemyWeapons()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var classes = CachedClasses(target);
            var index = CachedConfig(target).BuildSectionIndex();

            var payload = WeaponEditorPayload.Build(weapons, index, classes, GameStrings.Load(target));
            var groups = (JArray)payload[@"groups"];

            Assert.AreEqual(2, groups.Count, @"Player and enemy weapons are two groups, not one list");
            Assert.AreEqual(@"player", (string)groups[0][@"id"]);
            Assert.AreEqual(@"ai", (string)groups[1][@"id"]);

            // Against the catalogue rather than against a number, so this measures the payload's
            // fidelity to what was read rather than how many weapons Milan happens to ship today.
            Assert.AreEqual(weapons.PlayerWeapons.Count(), (int)groups[0][@"count"]);
            Assert.AreEqual(weapons.AIWeapons.Count(), (int)groups[1][@"count"]);

            // No weapon appears on both sides - the segmentation has to be a partition, or an edit
            // made on one side would silently be the other side's weapon.
            var player = ((JArray)groups[0][@"weapons"]).Select(w => (string)w[@"section"]).ToList();
            var ai = ((JArray)groups[1][@"weapons"]).Select(w => (string)w[@"section"]).ToList();
            Assert.AreEqual(0, player.Intersect(ai, StringComparer.OrdinalIgnoreCase).Count());

            foreach (var group in groups)
            {
                var categories = ((JArray)group[@"categories"]).Select(c => (string)c).ToList();
                Assert.IsTrue(categories.Count > 0);
                foreach (var weapon in (JArray)group[@"weapons"])
                    CollectionAssert.Contains(categories, (string)weapon[@"category"],
                        $@"{weapon[@"className"]} is in a category its group does not list");
            }
        }

        /// <summary>
        /// Every stat carries the scope it came from, and an inherited one names the ancestor.
        ///
        /// This is what makes the page usable rather than merely populated: a weapon inherits far
        /// more than it sets, so without a scope every control looks identical and an author cannot
        /// tell what this weapon defines from what it merely receives.
        /// </summary>
        [TestMethod]
        public void TheWeaponTreeOnlyClaimsInheritanceThatIsReal()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var classes = CachedClasses(target);
            var index = CachedConfig(target).BuildSectionIndex();

            var payload = WeaponEditorPayload.Build(weapons, index, classes, GameStrings.Load(target));

            // The roster draws inheritance as INDENTATION, which is a claim about position: an indented
            // row reads as a child of the row enclosing it. That claim is only true if the payload's
            // order makes it true, and it was not - the first cut sorted alphabetically and indented by
            // distance from the base, so Particle Rifle appeared to descend from M-96 Mattock purely
            // because M-96 sorted before it. Nothing about the rendering can catch that; only the
            // ordering can be checked, so it is checked here.
            var trees = 0;
            foreach (var group in (JArray)payload[@"groups"])
            {
                // Per category, because that is the unit the roster renders as one indented list.
                foreach (var category in ((JArray)group[@"weapons"]).GroupBy(w => (string)w[@"category"]))
                {
                    var rows = category.ToList();
                    var open = new List<JToken>();   // the ancestors of the row being read

                    for (var i = 0; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        var depth = (int)row[@"depth"];
                        var parent = (string)row[@"parent"];

                        Assert.IsTrue(depth <= open.Count,
                            $@"{row[@"className"]} is indented to {depth} with only {open.Count} rows above it to hang off");
                        open.RemoveRange(depth, open.Count - depth);

                        if (depth == 0)
                        {
                            Assert.IsNull(parent, $@"{row[@"className"]} sits at the top level but names a parent");
                        }
                        else
                        {
                            // The row it LOOKS like a child of, and the row it SAYS it is a child of,
                            // have to be the same row.
                            Assert.AreEqual((string)open[depth - 1][@"section"], parent,
                                $@"{row[@"className"]} is drawn under {open[depth - 1][@"className"]} but descends from {parent}");

                            // And that has to be true of the class chain, not just of the payload.
                            var chain = ClassScanner.Chain(
                                WeaponSectionOf(weapons, (string)row[@"section"]), classes).ToList();
                            Assert.IsTrue(chain.Any(c => string.Equals(c.Section, parent, StringComparison.OrdinalIgnoreCase)),
                                $@"{row[@"className"]} does not actually descend from {parent}");
                            trees++;
                        }

                        open.Add(row);
                    }

                    // The head of a category is its base where it has one, because everything under it
                    // inherits from it and a base filed alphabetically in the middle reads as a peer.
                    var head = rows[0];
                    var baseRow = rows.FirstOrDefault(r => (bool)r[@"isBase"]);
                    if (baseRow != null)
                    {
                        Assert.AreEqual((string)baseRow[@"section"], (string)head[@"section"],
                            $@"{category.Key} is headed by {head[@"className"]} rather than by its base");
                    }
                }
            }

            Console.WriteLine($@"{trees} weapons are drawn under a parent");
            Assert.IsTrue(trees > 20, @"Almost nothing was indented, so this proved nothing");
        }

        /// <summary>The superclass section of a weapon, for checking a drawn parent against the chain.</summary>
        private static string WeaponSectionOf(WeaponCatalogue weapons, string section)
            => weapons.Find(section)?.SuperSection;

        /// <summary>
        /// A mod ships its words in `DLC_MOD_Whatever/ModMaker/textlayer.json` and they reach the
        /// editor. Without this a mod that adds an enemy with a new gun shows up as two rows of class
        /// names until ModMaker itself is updated for it, which does not scale past the people who can
        /// edit ModMaker.
        /// </summary>
        /// <summary>
        /// A mod names its own faction and tags its own enemies, and both reach the wavelist page.
        ///
        /// This is the case ModMaker cannot solve on its own at all. `WAVE_REJ_Ravager` is a Reject
        /// and `WAVE_RPR_Ravager` is a Reaper; the three letters are an abbreviation, not a rule, and
        /// the class name `SFXWave_Horde_Cerberus2` does not say what Cerberus2 is either. Whoever
        /// made the faction is the only one who knows, so they write it down.
        /// </summary>
        [TestMethod]
        public void AModCanNameItsOwnFactionAndTagItsEnemies()
        {
            var shipped = ShippedTextLayer();

            // The shipped roster describes itself, or a mod's bundle would be the only source there is
            // and the base game would show class names.
            Assert.IsTrue(shipped.Factions.Count >= 8, @"The shipped layer does not describe the base factions");
            Assert.AreEqual(@"Rejects", shipped.Factions[@"sfxgamecontent_milan.sfxwave_horde_rejects"].Title);
            Assert.AreEqual(@"REJ", shipped.Factions[@"sfxgamecontent_milan.sfxwave_horde_rejects"].EnemyPrefix);

            shipped.Overlay(TextLayer.Parse(@"{
              ""format"": 1,
              ""version"": ""mod"",
              ""factions"": {
                ""DLC_MOD_Example.SFXWave_Horde_Example"": { ""title"": ""Example Legion"", ""enemyPrefix"": ""exl"" },
                ""sfxgamecontent_milan.sfxwave_horde_rejects"": { ""title"": ""The Rejects"", ""enemyPrefix"": ""REJ"" }
              }
            }", @"mod"));

            Assert.AreEqual(@"Example Legion", shipped.Factions[@"dlc_mod_example.sfxwave_horde_example"].Title);
            Assert.AreEqual(@"The Rejects", shipped.Factions[@"sfxgamecontent_milan.sfxwave_horde_rejects"].Title);
        }

        /// <summary>
        /// The page is handed the prefix map already inverted, and upper-cased, because it matches
        /// against what it reads out of a type name rather than against what somebody typed.
        /// </summary>
        /// <summary>
        /// An enemy tag nobody has claimed is REPORTED, not guessed at.
        ///
        /// There is an obvious guess available - read the faction out of the enemy's archetype path -
        /// and it is wrong in the case that matters. Rejects is assembled from units that were never
        /// in multiplayer, drawn out of several factions, so its members' archetypes say Reapers and
        /// Cerberus and Geth. Guessing would file the whole group under one of them and look right
        /// while doing it. The tag is declared by whoever made the faction or it is not known.
        /// </summary>
        [TestMethod]
        public void AnUnclaimedEnemyTagIsReportedRatherThanGuessedAt()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var shipped = ShippedTextLayer();

            // As shipped, every tag in the game is spoken for - if this fails, a faction was added to
            // Milan and nobody named it, which is exactly what the warning is for.
            var described = ((JArray)WavelistEditorPayload.Build(waves, null, null, shipped)[@"warnings"])
                .Select(w => (string)w).Where(w => w.Contains(@"belong to no named faction")).ToList();
            foreach (var warning in described) Console.WriteLine(warning);
            Assert.AreEqual(0, described.Count, @"A faction in this install has no name declared for it");

            // With nothing declared, every tag the install uses is named, once each, with a type to
            // go and look at. One warning per tag rather than per enemy: twenty-five lines saying the
            // same four things is a wall nobody reads.
            var silent = ShippedTextLayer();
            silent.Factions.Clear();

            var warnings = ((JArray)WavelistEditorPayload.Build(waves, null, null, silent)[@"warnings"])
                .Select(w => (string)w).Where(w => w.Contains(@"belong to no named faction")).ToList();

            var tags = waves.Enemies.Where(e => !e.IsPet)
                .Select(e => System.Text.RegularExpressions.Regex.Match(e.EnemyType, @"^WAVE_([A-Za-z0-9]{2,4})_"))
                .Where(m => m.Success).Select(m => m.Groups[1].Value.ToUpperInvariant())
                .Distinct().ToList();

            Console.WriteLine($@"{tags.Count} tags in this install: {string.Join(@", ", tags)}");
            Assert.IsTrue(tags.Count >= 4, @"Too few tags for this to be measuring anything");
            Assert.AreEqual(tags.Count, warnings.Count, @"One warning per undeclared tag, no more and no fewer");
            foreach (var tag in tags)
                Assert.IsTrue(warnings.Any(w => w.Contains($@"WAVE_{tag}_")), $@"{tag} went unreported");
        }

        [TestMethod]
        public void TheWavelistPayloadCarriesFactionNames()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var text = ShippedTextLayer();
            text.Overlay(TextLayer.Parse(@"{
              ""format"": 1, ""version"": ""mod"",
              ""factions"": { ""dlc_mod_example.sfxwave_horde_example"": { ""title"": ""Example Legion"", ""enemyPrefix"": ""exl"" } }
            }", @"mod"));

            var payload = WavelistEditorPayload.Build(waves, null, null, text);
            var names = (JObject)payload[@"factionNames"];

            Assert.AreEqual(@"Rejects", (string)names[@"REJ"]);
            Assert.AreEqual(@"Cerberus", (string)names[@"CER"]);
            // Written lower case in the bundle, matched upper case against the type name.
            Assert.AreEqual(@"Example Legion", (string)names[@"EXL"]);
            Assert.IsNull(names[@"exl"], @"The map is keyed the way a type name writes it, not the way the bundle did");

            // An entry naming a faction but no prefix is not a grouping - it titles the wavelist and
            // nothing more - so it must not appear here with an empty key.
            foreach (var pair in names)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Key));
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)pair.Value));
            }

            // And every faction the game has carries the title it was given, so the picker and the
            // headings stop saying "Cerberus2".
            var factions = (JArray)payload[@"factions"];
            var titled = factions.Count(f => (string)f[@"title"] != null);
            Console.WriteLine($@"{titled} of {factions.Count} wavelists are named");
            Assert.IsTrue(titled > 0, @"No wavelist got a title, so the shipped factions block is not reaching the payload");
        }

        [TestMethod]
        public void AModsTextBundleIsFoldedIntoTheShippedOne()
        {
            var shipped = ShippedTextLayer();
            var before = shipped.ClassText.Count;

            var bundle = TextLayer.Parse(@"{
              ""format"": 1,
              ""version"": ""bundle-test"",
              ""classText"": {
                ""dlc_mod_example.sfxweapon_assaultrifle_example"": {
                  ""title"": ""Example Rifle"",
                  ""description"": ""Ships with the mod, described by the mod.""
                },
                ""SFXGameContent.SFXPowerCustomAction_Pull"": {
                  ""title"": ""Pull"",
                  ""description"": ""Overridden by the mod.""
                }
              },
              ""groups"": [ { ""id"": ""weapons"", ""title"": ""Guns"", ""order"": 20 } ]
            }", @"bundle-test");

            shipped.Overlay(bundle);

            // Added.
            Assert.AreEqual(before + 1, shipped.ClassText.Count);
            Assert.AreEqual(@"Ships with the mod, described by the mod.",
                shipped.ClassText[@"dlc_mod_example.sfxweapon_assaultrifle_example"].Description);

            // Overridden, and matched without regard to case - the bundle wrote the section the way
            // the game writes it and the shipped layer keys everything lower case. A case-sensitive
            // miss here would look exactly like the mod not having said anything.
            Assert.AreEqual(@"Overridden by the mod.",
                shipped.ClassText[@"sfxgamecontent.sfxpowercustomaction_pull"].Description);

            // Merged per key, not per file: everything the bundle did not mention survives.
            Assert.IsTrue(shipped.EnemyStats.Categories.Count > 20,
                @"Overlaying a bundle blanked out text it never mentioned");

            // Groups match on id rather than being appended, or a bundle renaming one would leave two.
            Assert.AreEqual(1, shipped.Groups.Count(g => g.Id == @"weapons"));
            Assert.AreEqual(@"Guns", shipped.Groups.First(g => g.Id == @"weapons").Title);
        }

        /// <summary>
        /// Curation and anchors are NOT taken from a bundle. Curation decides whether something becomes
        /// a page at all and anchors are the names discovery searches for, so a bundle carrying either
        /// could hide content or point the scanners somewhere else - and a mod needs neither to
        /// describe what it adds, because unrecognised content already appears, flagged.
        /// </summary>
        [TestMethod]
        public void ATextBundleCannotChangeCurationOrAnchors()
        {
            var shipped = ShippedTextLayer();
            var curation = shipped.Curation.Count;
            var pull = shipped.Curation.GetValueOrDefault(@"sfxgamecontent.sfxpowercustomaction_pull")?.Decision;
            var anchors = shipped.Anchors.Count;

            shipped.Overlay(TextLayer.Parse(@"{
              ""format"": 1,
              ""version"": ""hostile"",
              ""curation"": {
                ""sfxgamecontent.sfxpowercustomaction_pull"": { ""decision"": ""exclude"", ""reason"": ""no"" },
                ""sfxgamecontent.sfxweapon_assaultrifle_mattock"": { ""decision"": ""exclude"", ""reason"": ""no"" }
              },
              ""anchors"": { ""weaponRoot"": { ""value"": ""SomethingElse.Entirely"" } },
              ""familyDefaults"": { ""SFXWeapon"": { ""group"": ""nowhere"" } }
            }", @"hostile"));

            Assert.AreEqual(curation, shipped.Curation.Count);
            Assert.AreEqual(pull, shipped.Curation.GetValueOrDefault(@"sfxgamecontent.sfxpowercustomaction_pull")?.Decision);
            Assert.AreEqual(anchors, shipped.Anchors.Count);
            Assert.AreNotEqual(@"SomethingElse.Entirely", shipped.Anchor(@"weaponRoot", @"SFXGame.SFXWeapon"));
            Assert.AreNotEqual(@"nowhere", shipped.FamilyDefaults.GetValueOrDefault(@"SFXWeapon")?.Group);
        }

        [TestMethod]
        public void EveryWeaponSaysWhereItsDescriptionCameFrom()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var classes = CachedClasses(target);
            var index = CachedConfig(target).BuildSectionIndex();
            var text = ShippedTextLayer();

            var payload = WeaponEditorPayload.Build(weapons, index, classes, GameStrings.Load(target), null, text);
            var rows = ((JArray)payload[@"groups"]).SelectMany(g => (JArray)g[@"weapons"]).ToList();

            var counts = new Dictionary<string, int>();
            foreach (var row in rows)
            {
                var d = row[@"description"];
                Assert.IsNotNull(d, $@"{row[@"className"]} has no description block at all");

                var source = (string)d[@"source"];
                counts[source] = counts.GetValueOrDefault(source) + 1;

                // The three states have to stay distinguishable, because the page renders each one
                // differently and a wrong label is worse than a missing description: presenting the
                // Mattock's words as though they were written for the AI Mattock is a quiet lie.
                switch (source)
                {
                    case @"none":
                        Assert.IsNull((string)d[@"text"], $@"{row[@"className"]} says it has no description but carries one");
                        break;
                    case @"game":
                    case @"custom":
                        Assert.IsFalse(string.IsNullOrWhiteSpace((string)d[@"text"]),
                            $@"{row[@"className"]} claims a {source} description and has no text");
                        break;
                    default:
                        Assert.Fail($@"{row[@"className"]} has an unknown description source {source}");
                        break;
                }

                // Inherited text names the weapon it came from, and that weapon is one the editor
                // can actually open - the page offers it as a link.
                if ((string)d[@"fromSection"] is { } from)
                {
                    Assert.AreNotEqual((string)row[@"section"], from,
                        $@"{row[@"className"]} is described as inheriting from itself");
                    Assert.IsNotNull(weapons.Find(from), $@"{row[@"className"]} is attributed to {from}, which is not a weapon");
                }
            }

            Console.WriteLine(string.Join(@", ", counts.Select(c => $@"{c.Key}: {c.Value}")));

            // The shared root placeholder is not a description. It reads as one - it is prose, and it
            // resolves - so the only thing stopping it being shown for two dozen turrets and enemy
            // guns is that its source section is recognised, and this is what proves that still works.
            var placeholders = rows.Where(r => (bool)r[@"description"][@"placeholder"]).ToList();
            Assert.IsTrue(placeholders.Count > 10,
                @"Nothing was recognised as inheriting the root placeholder, which cannot be right");
            foreach (var row in placeholders.Where(r => (string)r[@"description"][@"source"] != @"custom"))
                Assert.AreEqual(@"none", (string)row[@"description"][@"source"],
                    $@"{row[@"className"]} is showing the SFXWeapon placeholder as its description");

            // And the override reaches a weapon the game never described, which is the whole point of
            // it. Keyed on the text layer rather than on a hardcoded name so this follows the file.
            var authored = text.ClassText.Keys.FirstOrDefault(k => weapons.Find(k)?.Section != null);
            Assert.IsNotNull(authored, @"The shipped text layer describes no weapon; the override path is untested");
            var written = rows.First(r => string.Equals((string)r[@"section"], authored, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(@"custom", (string)written[@"description"][@"source"]);
            Assert.AreEqual(text.ClassText[authored].Description, (string)written[@"description"][@"text"]);
        }

        /// <summary>The text layer as it ships, which is what the editor actually runs against.</summary>
        private static TextLayer ShippedTextLayer()
        {
            var assembly = typeof(ModMakerCatalogue).Assembly;
            using var stream = assembly.GetManifestResourceStream(@"ME3TweaksModManager.modmaker.data.textlayer.json");
            return TextLayer.Parse(new StreamReader(stream!).ReadToEnd(), @"textlayer.json");
        }

        [TestMethod]
        public void WeaponPayloadSaysWhereEveryValueCameFrom()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var weapons = CachedWeapons(target);
            var classes = CachedClasses(target);
            var index = CachedConfig(target).BuildSectionIndex();
            var payload = WeaponEditorPayload.Build(weapons, index, classes, GameStrings.Load(target));

            var all = ((JArray)payload[@"groups"]).SelectMany(g => (JArray)g[@"weapons"]).ToList();
            var stats = all.SelectMany(w => (JArray)w[@"stats"]).ToList();

            Assert.IsTrue(stats.Count > 0);
            var known = new[] { @"own", @"inherited", @"default", @"typedefault", @"unset" };
            foreach (var stat in stats)
                CollectionAssert.Contains(known, (string)stat[@"scope"],
                    $@"'{stat[@"scope"]}' is not a scope the page knows how to draw");

            // An inherited value must name the section it came from - the page uses it to say which
            // class an edit would be overriding.
            foreach (var stat in stats.Where(x => (string)x[@"scope"] == @"inherited"))
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)stat[@"from"]),
                    @"An inherited stat must name its ancestor");

            // ...and an unset one must NOT show a value, or it reads as a zero the game does not have.
            foreach (var stat in stats.Where(x => (string)x[@"scope"] == @"unset"))
                Assert.IsNull((string)stat[@"value"], @"An unresolved stat must show nothing, not a guess");

            var groups = ((JArray)payload[@"knobGroups"]).Select(g => (string)g[@"title"]).ToList();
            CollectionAssert.Contains(groups, @"Stat bars (display only)");
            Assert.IsTrue(((JArray)payload[@"knobGroups"])
                    .Any(g => (string)g[@"title"] == @"Stat bars (display only)" && (string)g[@"note"] != null),
                @"The stat bars must carry their warning - they are the most misleading thing here");
        }

        /// <summary>
        /// The assassination rule is read from the game, not assumed.
        ///
        /// Every number in it was a guess in the older notes and every one of them is in the config:
        /// the eligibility threshold in `AssassinationTargetDataArray`, the spawn cadence in
        /// `SFXWave_Horde.SpawnDelay`, the field cap in `MaxEnemies`.
        /// </summary>
        [TestMethod]
        public void TheAssassinationRuleComesFromTheGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var rule = waves.Assassination;

            Console.WriteLine($@"cadence {waves.SpawnDelay}s, field {waves.MaxEnemies}, " +
                              $@"selection {rule?.SelectionSeconds}s -> window {rule?.TargetWindow}, " +
                              $@"threshold {rule?.MinTargetCost}");

            Assert.IsTrue(rule?.Usable == true, @"The assassination rule could not be read");

            // The MINIMUM of the table, not the first or the highest. Shipped it is 90/70/40/20, and a
            // wave that satisfies only the 20 entry is still playable on the difficulty that asks 20.
            Assert.AreEqual(20, rule.MinTargetCost);

            // The window is the smaller of what the cadence allows and what the field holds. At the
            // shipped 0.3s cadence the cadence allows 13 and the field holds 8, so the field binds.
            Assert.IsTrue(waves.SpawnDelay is > 0, @"The spawn cadence was not read");
            var byCadence = (int)Math.Floor(rule.SelectionSeconds.Value / waves.SpawnDelay.Value);
            Assert.AreEqual(Math.Min(byCadence, waves.MaxEnemies.Value), rule.TargetWindow);
            Assert.IsTrue(rule.TargetWindow <= waves.MaxEnemies,
                @"More enemies than the field holds cannot be present when the target is chosen");
        }

        /// <summary>
        /// Every objective wave the game ships can already hand an assassination a target.
        ///
        /// This is what makes the rule safe to enforce as an ERROR. A hard rule that fires on the
        /// shipped game is a rule that is wrong, and the only way to know is to run it over the
        /// install rather than over an example.
        /// </summary>
        [TestMethod]
        public void NoShippedObjectiveWaveWouldFailTheAssassinationRule()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var rule = waves.Assassination;
            Assert.IsTrue(rule?.Usable == true);

            var document = new WavelistEditDocument();
            var rows = 0;

            foreach (var faction in waves.Factions)
            foreach (var wave in faction.Waves.Where(w => w.WaveIndex != null && waves.ObjectiveWaves.Contains(w.WaveIndex.Value)))
            foreach (var row in wave.Rows)
            {
                // The shipped composition, handed back to the validator as though a user had typed it.
                var edit = new WavelistRowEdit
                {
                    Section = faction.Section,
                    InstanceId = wave.InstanceId,
                    WaveIndex = wave.WaveIndex.Value,
                    Difficulty = row.Difficulty,
                };
                foreach (var slot in row.Slots)
                    edit.Slots.Add(new WavelistSlotEdit
                    {
                        EnemyType = slot.EnemyType,
                        MinCount = slot.MinCount,
                        MaxCount = slot.MaxCount,
                        MaxPerWave = slot.MaxPerWave,
                    });
                document.Rows.Add(edit);
                rows++;
            }

            Assert.IsTrue(rows > 50, $@"Only {rows} objective rows were found, which is too few to prove anything");

            var refused = WavelistEditValidator.Validate(document, waves)
                                               .Where(p => p.Severity == WavelistProblemSeverity.Error
                                                        && p.Message.Contains(@"assassination", StringComparison.OrdinalIgnoreCase))
                                               .ToList();

            foreach (var problem in refused) Console.WriteLine(problem);
            Assert.AreEqual(0, refused.Count,
                $@"{refused.Count} of {rows} shipped objective rows were refused; the rule is wrong, not the game");
        }

        /// <summary>
        /// A wave whose opening enemies are all cheap is refused, and moving one heavy enemy up the
        /// list fixes it.
        ///
        /// The window counts ENEMIES and not slots, which is the part that is easy to get wrong: a
        /// Banshee in slot four is no use if the three slots before it already fill the window.
        /// </summary>
        [TestMethod]
        public void AnObjectiveWaveWithNoTargetInReachIsRefused()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var window = waves.Assassination.TargetWindow.Value;
            var threshold = waves.Assassination.MinTargetCost.Value;

            var cheap = waves.Enemies.First(e => !e.IsPet && e.WaveCost < threshold);
            var heavy = waves.Enemies.First(e => !e.IsPet && e.WaveCost >= threshold);
            Console.WriteLine($@"window {window}, threshold {threshold}, cheap {cheap.EnemyType} ({cheap.WaveCost}), " +
                              $@"heavy {heavy.EnemyType} ({heavy.WaveCost})");

            var faction = waves.Factions.First(f => f.Waves.Any(w => w.WaveIndex != null && waves.ObjectiveWaves.Contains(w.WaveIndex.Value)));
            var wave = faction.Waves.First(w => w.WaveIndex != null && waves.ObjectiveWaves.Contains(w.WaveIndex.Value));

            WavelistRowEdit Row(params WavelistSlotEdit[] slots)
            {
                var edit = new WavelistRowEdit
                {
                    Section = faction.Section,
                    InstanceId = wave.InstanceId,
                    WaveIndex = wave.WaveIndex.Value,
                    Difficulty = wave.Rows[0].Difficulty,
                };
                edit.Slots.AddRange(slots);
                return edit;
            }

            WavelistSlotEdit Filler(string type) => new() { EnemyType = type };
            WavelistSlotEdit Some(string type, int n) => new() { EnemyType = type, MinCount = n, MaxCount = n };

            bool Refused(WavelistRowEdit row)
            {
                var document = new WavelistEditDocument();
                document.Rows.Add(row);
                return WavelistEditValidator.Validate(document, waves)
                    .Any(p => p.Severity == WavelistProblemSeverity.Error
                           && p.Message.Contains(@"no target", StringComparison.OrdinalIgnoreCase));
            }

            // The window filled with cheap enemies, and the only eligible one behind them.
            Assert.IsTrue(Refused(Row(Filler(heavy.EnemyType), Some(cheap.EnemyType, window), Some(heavy.EnemyType, 1))),
                @"A wave whose whole window is cheap enemies must be refused");

            // The same wave with the heavy enemy moved up.
            Assert.IsFalse(Refused(Row(Filler(heavy.EnemyType), Some(heavy.EnemyType, 1), Some(cheap.EnemyType, window))),
                @"One eligible enemy inside the window is enough");

            // The FILLER is skipped in spawn order, so an eligible filler does not rescue the wave -
            // it spends leftover budget rather than taking a place in the queue.
            Assert.IsTrue(Refused(Row(Filler(heavy.EnemyType), Some(cheap.EnemyType, window + 4))),
                @"The filler must not count towards the target window");

            // And a wave one enemy short of filling the window still reaches what comes after it.
            Assert.IsFalse(Refused(Row(Filler(cheap.EnemyType), Some(cheap.EnemyType, window - 1), Some(heavy.EnemyType, 1))),
                @"An eligible enemy in the last place of the window counts");
        }

        private static ObjectiveCatalogue CachedObjectives(GameTarget target)
        {
            lock (CacheLock)
            {
                if (_objectives != null) return _objectives;
                var classes = CachedClasses(target);
                var config = CachedConfig(target);
                return _objectives = ObjectiveCatalogue.Build(config, classes, InstanceCatalogue.Build(config, classes));
            }
        }

        /// <summary>
        /// The objectives surface, read from the game: which waves carry one, which objectives may be
        /// picked, and the rules each runs by.
        ///
        /// Numbers, not "did it return something". Every failure this covers is silent - an objective
        /// editor missing a variant looks exactly like a game that does not have it.
        /// </summary>
        [TestMethod]
        public void ObjectivesAreReadFromTheGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var objectives = CachedObjectives(target);
            foreach (var warning in objectives.Warnings) Console.WriteLine($@"objectives: {warning}");

            // The waves are rebased onto the one-based numbering a player counts in. Shipped
            // OperationWaves is 2/5/9/10 zero-based, which is 3/6/10/11.
            CollectionAssert.AreEqual(new[] { 3, 6, 10, 11 },
                objectives.Waves.Where(w => !w.Shadowed).Select(w => w.WaveIndex).ToList(),
                @"Objective waves must be rebased onto WaveIndex, not carried through zero-based");

            // Six entries for four waves. Milan re-adds all four using AddUnique; the two whose
            // CreditScale it changed are genuinely new values and both survive, and the two it re-adds
            // byte-identically are correctly suppressed. All are carried; the later ones are marked
            // rather than dropped, because which of two entries for one wave the game honours is not
            // established.
            Assert.AreEqual(6, objectives.Waves.Count, @"Every surviving OperationWaves entry must be carried");
            Assert.AreEqual(2, objectives.Waves.Count(w => w.Shadowed),
                @"Only the two waves Milan re-added with a CHANGED credit scale have a second entry");

            // The extraction wave is the one with no credit scale - it is an objective, just always
            // the same one.
            var extraction = objectives.Waves.First(w => w.WaveIndex == 11);
            Assert.IsNull(extraction.CreditScale);
            Assert.IsTrue(objectives.Waves.Where(w => w.WaveIndex < 11).All(w => w.CreditScale != null));

            Console.WriteLine($@"families: {objectives.Families.Count}, variants: {objectives.Variants.Count()}");
            foreach (var variant in objectives.Variants.OrderBy(v => v.ClassName))
            {
                Console.WriteLine($@"  {variant.ObjectiveType,-22} weight={variant.EffectiveWeight,-5} ({variant.WeightScope}) " +
                                  $@"share={objectives.ShareOf(variant),-8:P1} values={variant.Values.Count} " +
                                  $@"reqs=[{string.Join(@", ", variant.Requirements.Select(r => $@"{r.ObjectiveType} {r.Minimum}-{r.Maximum}"))}] {variant.SectionKey}");
            }

            Assert.IsTrue(objectives.Families.Count >= 6, $@"Only {objectives.Families.Count} engagement families were read");
            Assert.IsTrue(objectives.Variants.Count() >= 9, $@"Only {objectives.Variants.Count()} variants were read");

            // Every variant names what the player has to do. Without it the editor has no label for
            // the row and no way to tell two Annex instances apart.
            Assert.IsTrue(objectives.Variants.All(v => v.ObjectiveType != null),
                @"Every objective must name its type: " +
                string.Join(@", ", objectives.Variants.Where(v => v.ObjectiveType == null).Select(v => v.SectionKey)));

            // Retrieve ships at weight 0 with its rules fully intact. That is the headline feature of
            // this editor - a dormant objective is one number from being back in rotation - so if the
            // reading ever stops distinguishing "weight 0" from "absent", this is what notices.
            var dormant = objectives.Variants.Where(v => v.IsDormant).ToList();
            Console.WriteLine($@"dormant: {string.Join(@", ", dormant.Select(v => v.ObjectiveType))}");
            Assert.IsTrue(dormant.Any(v => v.ObjectiveType != null && v.ObjectiveType.Contains(@"Retrieve", StringComparison.OrdinalIgnoreCase)),
                @"RetrievePickup ships dormant; the editor exists partly to turn it back on");

            // Extraction sets no weight on its instance and 0 on its class section. Reading the
            // instance alone reports it as unknown, which is why the effective weight resolves both
            // scopes - and why the scope is recorded rather than inferred.
            var extract = objectives.Variants.First(v => v.ObjectiveType == @"ExtractionPoint");
            Assert.IsNull(extract.SelectionWeight, @"Extraction's instance sets no weight");
            Assert.AreEqual(0d, extract.EffectiveWeight, @"...and its family sets 0, so the game never rolls it");
            Assert.AreEqual(@"family", extract.WeightScope);
            Assert.IsTrue(extract.IsDormant);

            // The two scopes stay apart, exactly as the config keeps them - see docs/02 §1.6.
            var disarm = objectives.Families.First(f => f.ClassName.Equals(@"SFXEngagement_Disarm", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(disarm.Shared.ContainsKey(@"NumDisarmsRequired"), @"NumDisarmsRequired is shared by both Disarm variants");
            Assert.IsTrue(disarm.Variants.All(v => !v.Values.ContainsKey(@"NumDisarmsRequired")), @"...and set by neither of them");
            Assert.AreEqual(2, disarm.Variants.Count);
        }

        #endregion

        private static BundleBuildResult _bundle;

        private static BundleBuildResult CachedBundle(GameTarget target)
        {
            lock (CacheLock)
            {
                if (_bundle != null) return _bundle;
                var classes = CachedClasses(target);
                var config = CachedConfig(target);
                return _bundle = DefinitionBundleWriter.Build(new BundleInputs
                {
                    BundleVersion = @"0.0.0-test",
                    Game = target.Game.ToString(),
                    Classes = classes,
                    Config = config,
                    Instances = InstanceCatalogue.Build(config, classes),
                    Waves = CachedWaves(target),
                    Difficulty = CachedDifficulty(target),
                    Maps = CachedMaps(target),
                    Text = TextLayer.Load(SchemaFile(@"example.textlayer.json")),
                });
            }
        }

        #endregion

        #region Editor hosting - no game required

        /// <summary>
        /// Every editor page is embedded under the exact name the host looks up, and speaks the host's
        /// contract. If a csproj entry is renamed or dropped, the control 404s the page and the window
        /// opens blank - the build stays green either way, so only a test catches it.
        ///
        /// The contract is one shared set of names rather than one per page: the host injects
        /// `__PAYLOAD__` and `__EDITS__` and takes back a `modmaker.edits` message naming its
        /// collection. A page that still reads a name of its own would load and stay empty.
        /// </summary>
        [TestMethod]
        [DataRow(@"wavelist.html", @"wavelist")]
        [DataRow(@"enemy.html", @"enemy")]
        [DataRow(@"maplist.html", @"maplist")]
        [DataRow(@"values.html", @"values")]
        [DataRow(@"objectives.html", @"objectives")]
        [DataRow(@"weapons.html", @"weapons")]
        public void EditorPageIsEmbeddedWhereTheHostLooksForIt(string page, string collection)
        {
            var assembly = typeof(ModMakerCatalogue).Assembly;
            var names = assembly.GetManifestResourceNames();
            var expected = $@"ME3TweaksModManager.modmaker.editor.web.{page}";

            CollectionAssert.Contains(names, expected,
                $@"Embedded resources found: {string.Join(@", ", names.Where(n => n.Contains(@"modmaker")))}");

            using var stream = assembly.GetManifestResourceStream(expected);
            Assert.IsNotNull(stream);
            var html = new StreamReader(stream).ReadToEnd();

            StringAssert.Contains(html, @"window.__PAYLOAD__", @"The page must read the payload the host injects");
            StringAssert.Contains(html, @"window.__EDITS__", @"The page must restore the edits the host injects");
            StringAssert.Contains(html, $@"collection: '{collection}'",
                @"The page must name the collection it edits, or its edits are ignored");

            // The title bar is the page's now, not a WPF strip above it, so a page that forgets to
            // pull it in has no way to save, switch editor or compile at all.
            StringAssert.Contains(html, @"chrome.css", @"The page must load the shared title bar's styles");
            StringAssert.Contains(html, @"chrome.js", @"The page must load the shared title bar");

            // The wavelist composer opens as a row of the grid, and drawBody() empties that grid
            // before rebuilding it - so between the empty and the rebuild the composer is DETACHED.
            // Looking it up by id there returns null, appendChild(null) throws part-way through the
            // loop, and the grid is left truncated at whatever row it had reached. The node is held
            // in a variable for exactly that reason; an id lookup is how the bug comes back.
            if (collection == WavelistEditDocument.CollectionId)
            {
                // Exactly one lookup: the one that CAPTURES it, at load, while it is still attached.
                var lookups = html.Split(@"getElementById('composer')").Length - 1;
                Assert.AreEqual(1, lookups,
                    @"The composer must be looked up once and then held as a node - it is detached " +
                    @"whenever the grid is rebuilt, and a later lookup returns null");
                StringAssert.Contains(html, @"const COMPOSER = document.getElementById('composer')",
                    @"...and that one lookup is the capture");
            }

            // The values page is the only one that asks the host a QUESTION rather than telling it
            // something: it posts string references and waits for the text behind them. Both halves
            // are named here because a rename on either side disables the preview silently - the page
            // asks, nothing answers, and every reference stays a bare number.
            if (collection == ValuesEditorPayload.CollectionId)
            {
                StringAssert.Contains(html, @"command: 'strings'",
                    @"The values page must ask the host to resolve string references");
                StringAssert.Contains(html, @"window.__modmakerStrings",
                    @"The values page must expose where the host delivers the answer");
            }

            // The development-only sample payloads must NOT ship: they are one machine's game data,
            // and stale the moment the game changes.
            Assert.IsFalse(names.Any(n => n.Contains(@"sample", StringComparison.OrdinalIgnoreCase)
                                       && n.Contains(@"payload", StringComparison.OrdinalIgnoreCase)),
                @"The sample payloads are development fixtures and should not be embedded");
        }

        /// <summary>
        /// The shared title bar is embedded, and both sides of its contract agree.
        ///
        /// This is the seam that replaced the WPF toolbar. The bar draws in the page and asks the host
        /// to do the things only the host can - open a file, save one, compile - so a mismatch here is
        /// not a visual bug: it is New, Open, Save and Compile silently doing nothing, in a window
        /// whose only other way to save is the shortcut the same file registers.
        /// </summary>
        [TestMethod]
        public void TheSharedTitleBarIsEmbeddedAndSpeaksTheHostsContract()
        {
            var assembly = typeof(ModMakerCatalogue).Assembly;
            const string prefix = @"ME3TweaksModManager.modmaker.editor.web.";

            foreach (var asset in new[] { @"chrome.js", @"chrome.css" })
            {
                CollectionAssert.Contains(assembly.GetManifestResourceNames(), prefix + asset,
                    $@"{asset} is not embedded, so every page 404s its title bar");
            }

            using var stream = assembly.GetManifestResourceStream(prefix + @"chrome.js");
            var js = new StreamReader(stream!).ReadToEnd();

            StringAssert.Contains(js, @"window.__CHROME__", @"The bar must read the state the host injects");
            StringAssert.Contains(js, @"window.__modmakerChrome", @"The host must be able to push a new state in");
            StringAssert.Contains(js, @"'modmaker.command'", @"The bar must post commands under the type the host filters on");

            // Named one by one rather than counted: the host switches on these strings, and an
            // unhandled one is a button that does nothing rather than an error.
            foreach (var command in new[] { @"'new'", @"'open'", @"'save'", @"'saveAs'", @"'compile'", @"'page'", @"'requireAllPlayers'" })
                StringAssert.Contains(js, command, $@"The bar must be able to ask for {command}");
        }

        /// <summary>
        /// The shipped text layer is embedded where the launcher looks for it.
        ///
        /// Every readable word in the enemy editor comes from this file - stat names carry no TLK
        /// string anywhere in the install - so losing it does not throw, it produces an editor full of
        /// internal identifiers.
        /// </summary>
        [TestMethod]
        public void TheShippedTextLayerIsEmbedded()
        {
            var assembly = typeof(ModMakerCatalogue).Assembly;
            const string expected = @"ME3TweaksModManager.modmaker.data.textlayer.json";

            CollectionAssert.Contains(assembly.GetManifestResourceNames(), expected);

            using var stream = assembly.GetManifestResourceStream(expected);
            var layer = TextLayer.Parse(new StreamReader(stream!).ReadToEnd(), expected);

            Assert.IsTrue(layer.EnemyStats.Categories.Count > 20, @"The enemy text did not survive embedding");
            Assert.IsNotNull(layer.EnemyStats.Category(@"Banshee")?.Groups.FirstOrDefault());

            // BansheeAlt is the "Original Banshee" the Banshee page describes without separately
            // editing, so it carries a title and inherits the rest.
            var alt = layer.EnemyStats.Category(@"BansheeAlt");
            Assert.AreEqual(@"Original Banshee", alt.Title);
            Assert.IsTrue(alt.Groups.Count > 0, @"Inherited text must be resolved, not left empty");
        }

        #endregion

        #region Wavelist editor payload - needs a real LMP install

        /// <summary>
        /// The payload the wavelist page renders. It is built here rather than in JavaScript because
        /// it applies game rules - what a wave costs, what its budget is, and when the budget does not
        /// apply at all - and rules in the page cannot be tested against a real install.
        /// </summary>
        [TestMethod]
        public void WavelistPayloadIsBuiltForTheGrid()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var waves = CachedWaves(target);
            var payload = WavelistEditorPayload.Build(waves, CachedMaps(target));

            // The difficulty columns come from the game's budget table, never a hardcoded four.
            var difficulties = payload[@"difficulties"];
            Assert.AreEqual(ExpectedMPDifficultyLevels, difficulties.Count());

            // Each difficulty names the challenge texture it is headed by. Only the PATH is here: the
            // art itself needs the banner library, which is the separate assertion below - and the
            // split is the bug this pair exists to catch, because the grid was for a while handed a
            // path it could not draw and so drew nothing at all.
            Assert.IsTrue(difficulties.All(d => d[@"imagePath"] != null),
                @"Each difficulty should name the game's own banner");
            Assert.IsTrue(difficulties.All(d => d[@"image"] == null),
                @"Without the banner library there is nothing decoded to draw");

            // The field cap, which is what makes a wave costable: the fixed slots hold their counts on
            // the field and the filler makes the rest of it up.
            Assert.AreEqual(ExpectedFieldCap, (int)payload[@"maxEnemies"]);

            Assert.AreEqual(ExpectedEnemyVocabulary, payload[@"palette"].Count());

            Assert.AreEqual(ExpectedWaveFactionDescendants, payload[@"factions"].Count());

            var cerberus = payload[@"factions"].First(f => (string)f[@"name"] == @"SFXWave_Horde_Cerberus");
            Assert.AreEqual(ExpectedWavesPerFaction, cerberus[@"waves"].Count());

            // Waves come out in play order, not config order or lexical order - wave 10 after wave 2.
            var indices = cerberus[@"waves"].Select(w => (int)w[@"index"]).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, ExpectedWavesPerFaction).ToList(), indices);

            // Every cell must cost, or the budget bar is decorative.
            var cell = cerberus[@"waves"].First(w => (int)w[@"index"] == 7)[@"cells"][@"DO_Level3"][0];
            Assert.IsTrue((bool)cell[@"costComplete"], @"Every enemy type in shipped data has a price");
            Assert.IsTrue((int)cell[@"fixedCost"] > 0);
            Assert.IsNotNull(cell[@"budget"]);
            Assert.IsTrue((bool)cell[@"budgetApplies"], @"Wave 7 is neither the extraction wave nor an objective wave");

            // ...and the two kinds of wave the budget does NOT apply to must say so, or the editor
            // shows a bar the game will not honour.
            var extraction = cerberus[@"waves"].First(w => (int)w[@"index"] == waves.LastWave);
            Assert.IsFalse((bool)extraction[@"cells"][@"DO_Level3"][0][@"budgetApplies"]);
            var objective = cerberus[@"waves"].First(w => (int)w[@"index"] == waves.ObjectiveWaves[0]);
            Assert.IsFalse((bool)objective[@"cells"][@"DO_Level3"][0][@"budgetApplies"]);

            // The filler spends what is left rather than a fixed amount, so it must not be charged.
            var slots = cell[@"slots"];
            Assert.AreEqual(1, slots.Count(x => (bool)x[@"filler"]), @"Exactly one filler per cell");
            Assert.IsTrue(slots.Where(x => (bool)x[@"filler"]).All(x => x[@"unitCost"] != null),
                @"The filler is still priced, it is just not counted");

            // Dormant factions are present with their wavelists intact and flagged as unreachable.
            var dormant = payload[@"factions"].Where(f => !(bool)f[@"selectable"] && f[@"waves"].Any()).ToList();
            Assert.AreEqual(3, dormant.Count);

            // The verbatim row text has to reach the editor: it is what untouched rows are re-emitted
            // from, and it never survives a round trip through a JavaScript object if it is dropped here.
            Assert.IsFalse(payload[@"factions"].SelectMany(f => f[@"waves"]).SelectMany(w => w[@"cells"].Children<JProperty>())
                                  .SelectMany(c => c.Value).Any(c => string.IsNullOrEmpty((string)c[@"raw"])));
        }

        #endregion

        /// <summary>
        /// LMP ships inside the Legendary Edition install tree. Returns null when it is not installed,
        /// so these tests skip rather than fail on a machine without the game.
        /// </summary>
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
    }
}
