using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// The maplist editor: the game's own strings and the game's own banner art.
    ///
    /// Both are read from the install rather than shipped, which is the whole design of this editor -
    /// a new difficulty or a reskinned map arrives with its name and its picture already attached. The
    /// tests here are about that reading working, and about it failing in a way the editor can show.
    /// </summary>
    [TestClass]
    public class ModMakerMaplistTests
    {
        [ClassInitialize]
        public static void InitLEC(TestContext _) => LegendaryExplorerCoreLib.InitLib(TaskScheduler.Default, null);

        private static readonly object CacheLock = new();
        private static Dictionary<string, CatalogueClass> _classes;
        private static MapListCatalogue _maps;

        /// <summary>
        /// The picker names and banners resolve out of the installed game.
        ///
        /// One test over both because they are one question - "can the editor show a map the way the
        /// lobby does" - and because each costs a pass over the install, so they share it.
        /// </summary>
        [TestMethod]
        public void PickerNamesAndBannersComeFromTheGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);

            // ---------------------------------------------------------------- strings
            var strings = GameStrings.Load(target);
            Assert.IsTrue(strings.Available, @"No INT TLKs loaded; every name would fall back to its number");
            Console.WriteLine($@"TLKs: {strings.LoadedFiles.Count}");

            var named = maps.Maps.Count(m => strings.Resolve(m.PrettyName) != null);
            Console.WriteLine($@"maps with a resolved name: {named} of {maps.Maps.Count}");
            foreach (var map in maps.Maps.Take(6))
                Console.WriteLine($@"  {map.PackageName ?? @"(picker)",-24} {strings.Resolve(map.PrettyName)}");

            Assert.IsTrue(named > maps.Maps.Count * 0.8,
                $@"Only {named} of {maps.Maps.Count} map names resolved; the TLK join is not working");

            // The game stores some codepoints as the literal text `[Ux2623]` and substitutes them when
            // it draws. The hazard maps are named that way, and it is the ONLY thing distinguishing
            // them from the ordinary version of the same map - so the escape has to be decoded rather
            // than shown, and rather than stripped, which would leave six apparent duplicates.
            var names = maps.Maps.Select(m => strings.Resolve(m.PrettyName)).Where(n => n != null).ToList();
            Assert.IsFalse(names.Any(n => n.Contains(@"[Ux", StringComparison.OrdinalIgnoreCase)),
                $@"An undecoded escape reached the editor: {names.FirstOrDefault(n => n.Contains(@"[Ux", StringComparison.OrdinalIgnoreCase))}");

            var hazard = names.Where(n => n.Contains('☣')).ToList();
            Console.WriteLine($@"hazard-marked maps: {hazard.Count} - {string.Join(@", ", hazard)}");
            Assert.IsTrue(hazard.Count > 0, @"LMP ships hazard variants whose names carry the biohazard sign");
            // Every hazard variant sits alongside the ordinary version of the same map, and the mark
            // is the only thing telling them apart. Stripping the escape - the other obvious way to
            // deal with it - would produce six pairs of identically named maps, so that is what is
            // asserted against rather than the mark simply being present.
            foreach (var name in hazard)
                CollectionAssert.Contains(names, name.Replace('☣', ' ').Trim(),
                    @"A hazard variant should sit beside the base map it is named after");

            var stripped = names.Select(n => n.Replace('☣', ' ').Trim()).Distinct().Count();
            Assert.IsTrue(names.Distinct().Count() > stripped,
                @"Removing the mark instead of decoding it would collapse the hazard maps onto their base maps");

            // A ref nothing defines resolves to null rather than to "No Data" - that sentinel reaching
            // the editor would read as a map actually called that.
            Assert.IsNull(strings.Resolve(999999999));
            Assert.IsNull(strings.Resolve((int?)null));
            Assert.IsNull(strings.Resolve(@"not a number"));

            // ---------------------------------------------------------------- banners
            var wanted = maps.AllImagePaths.ToList();
            Assert.IsTrue(wanted.Count > 20, @"The pickers should reference a banner each");

            var clock = Stopwatch.StartNew();
            var banners = BannerLibrary.Load(target, wanted);
            clock.Stop();

            // Measured against the paths that CAN be looked up, not against every path referenced.
            // A path with no package in it is not a decode failure - it is a different fact.
            var qualified = wanted.Where(p => p.Contains('.')).ToList();
            var found = qualified.Count(p => banners.For(p)?.Png != null);

            Console.WriteLine($@"banners: {found} of {qualified.Count} qualified paths in " +
                              $@"{clock.ElapsedMilliseconds} ms, {banners.PackagesSearched} packages searched " +
                              $@"({wanted.Count - qualified.Count} paths name no package)");
            foreach (var problem in banners.Problems) Console.WriteLine($@"  problem: {problem}");

            // Reported, not asserted on. Whether Milan's in-development maps have had their art drawn
            // yet is a fact about Milan, and pinning it here makes this test fail every time someone
            // adds a map before its banner - which is a normal state for a map pack to pass through.
            // The editor already carries it where it belongs: each entry gets its own reason, shown
            // against the map it is about.
            var artless = maps.Maps
                .Where(m => m.IsAvailable && !string.IsNullOrWhiteSpace(m.Image) && m.Image.Contains('.'))
                .Where(m => banners.For(m.Image)?.Png == null)
                .Select(m => $@"{m.PackageName} -> {m.Image}")
                .ToList();

            if (artless.Count > 0)
                Console.WriteLine($@"selectable maps with no banner ({artless.Count}): {string.Join(@", ", artless)}");

            // What IS asserted is that the lookup works at all. A broken resolver looks like nearly
            // everything failing, and no amount of unfinished content looks like that.
            Assert.IsTrue(found >= qualified.Count * 0.8,
                $@"Only {found} of {qualified.Count} qualified paths resolved; the lookup is not working");

            // And whatever did not resolve says why, rather than being quietly absent - which is the
            // difference between "this map has no banner" and "this map's banner could not be found".
            foreach (var path in qualified.Where(p => banners.For(p)?.Png == null))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(banners.For(path)?.Problem),
                    $@"{path} did not resolve and gave no reason");
            }

            // The unqualified ones are reported, every one, rather than quietly missing - and the
            // scanner has already collected them for the same reason.
            foreach (var path in wanted.Except(qualified))
            {
                StringAssert.Contains(banners.For(path)?.Problem ?? string.Empty, @"without naming its package");
                CollectionAssert.Contains(maps.UnqualifiedImages, path,
                    @"The scanner and the banner library must agree on which paths cannot be looked up");
            }

            // Every one measured is a 512x128 DXT1 strip - maps, factions and difficulties alike. The
            // aspect is what decides the editor's layout, so it is worth knowing if it ever changes.
            foreach (var path in wanted)
            {
                var banner = banners.For(path);
                if (banner?.Png == null) continue;

                Assert.AreEqual(4.0, (double)banner.Width / banner.Height, 0.01,
                    $@"{path} is {banner.Width}x{banner.Height}, not a 4:1 banner");
                Assert.IsTrue(banner.Png.Length > 100, $@"{path} decoded to nothing");
                StringAssert.StartsWith(banner.DataUri, @"data:image/png;base64,");
            }

            // An unresolvable path is a REASON, not an absence: the editor has to distinguish "this
            // map has no banner" from "this map's banner was written in a form nothing can look up".
            var unqualified = BannerLibrary.Load(target, new[] { @"OptMapArmaxBlasteroid" });
            var bare = unqualified.For(@"OptMapArmaxBlasteroid");
            Assert.IsNotNull(bare);
            Assert.IsNull(bare.Png);
            StringAssert.Contains(bare.Problem, @"without naming its package");
            Assert.AreEqual(0, unqualified.PackagesSearched, @"A path that cannot be looked up must not cost a scan");
        }

        /// <summary>
        /// The three lists are read separately and their disagreement is derived rather than assumed.
        ///
        /// Counts are not pinned - DLC_MOD_PMR3 is a map pack in development - but the relationships
        /// have to hold whatever the roster is. The placeholder is the one that would quietly become a
        /// bug: MasterMapList entry 0 is the "Random" picker choice, and offering it for editing means
        /// offering an edit to a map that does not exist.
        /// </summary>
        [TestMethod]
        public void ThePlaceholderIsNotAMap()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);

            var placeholders = maps.Placeholders;
            Assert.AreEqual(1, placeholders.Count, @"LMP ships one picker placeholder - the Random choice");
            Assert.IsTrue(string.IsNullOrWhiteSpace(placeholders[0].PackageName));
            Assert.IsNotNull(placeholders[0].Image, @"The Random choice still has a banner of its own");

            // It must not be counted as a map that cannot be played, which is what happens if
            // "no package" and "not in AvailableMaps" are treated as the same thing.
            CollectionAssert.DoesNotContain(maps.EntriesNotAvailable, placeholders[0]);

            Console.WriteLine($@"maps: {maps.Maps.Count} ({placeholders.Count} placeholder), " +
                              $@"available: {maps.AvailableMaps.Count}, " +
                              $@"available with no entry: {maps.AvailableWithoutEntry.Count}, " +
                              $@"described but unavailable: {maps.EntriesNotAvailable.Count}");
        }

        /// <summary>
        /// The payload the page draws: every map named, every banner attached, and the two facts a
        /// person cannot get from the config alone.
        ///
        /// Counts are not pinned - DLC_MOD_PMR3 is a map pack under development - but the shape is.
        /// The two facts are that a map can be described without being playable, and playable without
        /// being described; an editor that conflates them is wrong in a way that looks like a bug in
        /// the game.
        /// </summary>
        [TestMethod]
        public void MaplistPayloadIsBuiltForThePage()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);
            var strings = GameStrings.Load(target);
            var banners = BannerLibrary.Load(target, MaplistEditorPayload.ImagePaths(maps));

            var payload = MaplistEditorPayload.Build(maps, strings, banners);

            Assert.AreEqual(maps.SettingsSection, (string)payload[@"section"]);
            Assert.IsTrue((bool)payload[@"stringsAvailable"]);

            var listed = (JArray)payload[@"maps"];
            var placeholders = (JArray)payload[@"placeholders"];

            // The Random choice is carried, but never as a map: it has no level behind it and offering
            // it for editing offers an edit with nowhere to go.
            Assert.AreEqual(maps.Maps.Count, listed.Count + placeholders.Count);
            Assert.IsFalse(listed.Any(m => (bool)m[@"placeholder"]));
            Assert.IsTrue(placeholders.All(m => (bool)m[@"placeholder"]));

            int named = 0, withArt = 0, withProblem = 0;
            foreach (var map in listed.Cast<JObject>())
            {
                if (map[@"name"] != null) named++;
                if (map[@"banner"] != null) withArt++;
                if (map[@"bannerProblem"] != null) withProblem++;

                // A path either decodes or says why. An entry with neither is the state the editor
                // cannot explain to anyone.
                if (map[@"image"] != null)
                    Assert.IsTrue(map[@"banner"] != null || map[@"bannerProblem"] != null,
                        $@"{map[@"package"]} has an image path that neither resolved nor reported a reason");
            }

            Console.WriteLine($@"payload: {listed.Count} maps, {named} named, {withArt} with art, {withProblem} reported");
            Assert.AreEqual(listed.Count, named, @"Every map should resolve a name from the game's own text");
            Assert.IsTrue(withArt > 0);

            // The music vocabulary is closed and taken from the shipped values - an event name the
            // game does not have is a map that loads in silence.
            var music = ((JArray)payload[@"musicEvents"]).Select(m => (string)m).ToList();
            Assert.IsTrue(music.Count > 0);
            CollectionAssert.AllItemsAreUnique(music);
            foreach (var map in listed.Cast<JObject>())
            {
                if ((string)map[@"music"] is { } used) CollectionAssert.Contains(music, used);
            }

            // Measured from the shipped positions rather than assumed. A hardcoded extent would put
            // every map in the wrong place the moment Milan moved one outside it.
            var bounds = payload[@"galaxyBounds"];
            Assert.IsNotNull(bounds);
            Assert.IsTrue((double)bounds[@"maxX"] > (double)bounds[@"minX"]);
            Assert.IsTrue((double)bounds[@"maxY"] > (double)bounds[@"minY"]);

            // The other two pickers ride along, because a faction is reachable only if it is in BOTH
            // the wave manager's roster and this list - which is invisible from the wave editor.
            var factions = (JArray)payload[@"factions"];
            Assert.IsTrue(factions.Count > 0);
            Assert.IsTrue(factions.All(f => f[@"waveClass"] != null), @"Every faction choice shown must name a wave class");

            // The lobby's "Unknown Enemy" is the surprise-me choice: it carries WaveClass with an
            // EMPTY value rather than omitting it, so it is not a faction at all. The catalogue keeps
            // it - it is really in the picker - and the payload drops it, because a tile you cannot
            // act on is one the editor is only asking the user to scroll past.
            Assert.IsTrue(maps.EnemyTypes.Any(o => string.IsNullOrWhiteSpace(o.Field(@"WaveClass"))),
                @"The 'any' choice should still be in the catalogue");
            Assert.AreEqual(maps.EnemyTypes.Count - 1, factions.Count);
            Assert.AreEqual(maps.ChallengeTypes.Count, ((JArray)payload[@"difficulties"]).Count);
        }

        /// <summary>
        /// A map's presentation is edited in place, leaving everything this build does not model
        /// exactly as the game wrote it.
        ///
        /// Sharper here than in the other editors: a MasterMapList entry carries string refs, an image
        /// path and a Galaxy at War subtitle that ModMaker deliberately does not touch. Regenerating
        /// the entry would not merely reformat those, it would DROP them - and the map would lose its
        /// name and its picture while the mod installed cleanly.
        /// </summary>
        [TestMethod]
        public void EditingAMapKeepsEverythingElseTheGameWrote()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);
            int edited = 0, noPosition = 0, noMusic = 0;

            foreach (var map in maps.Maps.Where(m => !m.IsPlaceholder))
            {
                // Unedited is byte-identical, always.
                Assert.AreEqual(map.RawValue, MapEntryWriter.Write(map, new MapEntryEdit { Id = map.Id }),
                    $@"{map.PackageName} did not survive an empty write");

                if (map.GalaxyAtWarX == null || map.GalaxyAtWarY == null) { noPosition++; continue; }
                if (string.IsNullOrWhiteSpace(map.MusicEventName)) { noMusic++; continue; }

                var written = MapEntryWriter.Write(map, new MapEntryEdit
                {
                    Id = map.Id,
                    GalaxyAtWarX = map.GalaxyAtWarX + 7,
                    GalaxyAtWarY = map.GalaxyAtWarY - 3,
                    MusicEventName = map.MusicEventName,
                });

                // Everything not modelled survives verbatim. Checked against the shipped text rather
                // than against the parsed entry, so a field this build has never heard of counts too.
                foreach (var field in new[] { @"PrettyName", @"Description", @"Image", @"GalaxyAtWarMapSubtitle" })
                {
                    var shipped = FieldText(map.RawValue, field);
                    if (shipped == null) continue;
                    StringAssert.Contains(written, shipped,
                        $@"{map.PackageName}: {field} was rewritten while moving the map");
                }

                Assert.AreNotEqual(map.RawValue, written);
                edited++;
            }

            Console.WriteLine($@"entries edited: {edited} ({noPosition} with no position, {noMusic} with no music)");

            // A proportion rather than a count. This asserted `> 20` against a MasterMapList of 25
            // raw values, three of which were duplicates that only existed because ConfigMerge's
            // AddUnique never suppressed anything. Fixing that dropped the array to 22 and the count
            // to 20, failing a test that was measuring the bug rather than the game. Expressed as a
            // share, it says what it always meant - nearly every shipped map carries both a position
            // and a music event - and survives the next legitimate change to the map list.
            var candidates = maps.Maps.Count(m => !m.IsPlaceholder);
            Assert.IsTrue(edited >= candidates * 0.9,
                $@"Most shipped maps carry both a position and a music event; only {edited} of {candidates} did");
        }

        /// <summary>`Field=value` as the entry spells it, for the fields this build does not model.</summary>
        private static string FieldText(string entry, string field)
        {
            var at = entry.IndexOf(field + @"=", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            var end = entry.IndexOfAny(new[] { ',', ')' }, at);
            return end < 0 ? null : entry[at..end];
        }

        #region The edit model and the compiler - no game required

        /// <summary>
        /// Turning a map off subtracts the INSTALL'S OWN BYTES, not the package name as the project
        /// spells it.
        ///
        /// The fixture pads the shipped value on purpose. `AvailableMaps` is a plain string list, so a
        /// package name looks like something a compiler could simply write out - and if the config
        /// happens to store it with a space around it, the subtraction silently matches nothing, the
        /// map stays playable, and the mod appears to do nothing at all.
        /// </summary>
        [TestMethod]
        public void TurningAMapOffSubtractsTheInstallsOwnBytes()
        {
            const string padded = @"  BioP_MPDish  ";
            // Two available maps, so turning one off is an edit rather than an empty roster - which
            // the validator refuses outright, and rightly.
            var (maps, sections) = Fake(availableRaw: new[] { padded, @"BioP_MPCnfl" });

            var project = ModMakerProject.Create(@"Fewer Maps", @"LMP", @"1");
            var document = new MaplistEditDocument();
            document.Set(new MapAvailabilityEdit { Package = @"BioP_MPDish", Available = false });
            project.MaplistEdits = document;

            var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors));

            var write = OnlyMapWrite(result);
            Assert.AreEqual(@"AvailableMaps", write.Name);
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.Remove), write.ValueType);
            Assert.AreEqual(padded, write.Value, @"The subtraction must carry the install's bytes, padding included");
            Assert.IsNull(write.Replace, @"A string list takes a bare Remove, not a paired replacement");
        }

        /// <summary>
        /// Turning a map on is a bare AddUnique with nothing to subtract, and turning on a map that is
        /// already on writes nothing at all.
        ///
        /// The second half matters more than it looks: LegendaryExplorerCore's AddUnique does not
        /// actually dedupe (docs 08, bug 11), so an AddUnique of a value already present produces a
        /// duplicate rather than a no-op.
        /// </summary>
        [TestMethod]
        public void TurningAMapOnAddsItOnceOrNotAtAll()
        {
            var (maps, sections) = Fake(availableRaw: new[] { @"BioP_MPDish" });

            var project = ModMakerProject.Create(@"More Maps", @"LMP", @"1");
            var document = new MaplistEditDocument();
            document.Set(new MapAvailabilityEdit { Package = @"BioP_MPCnfl", Available = true });
            project.MaplistEdits = document;

            var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors));

            var write = OnlyMapWrite(result);
            Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.AddUnique), write.ValueType);
            Assert.AreEqual(@"BioP_MPCnfl", write.Value);

            // Already available: nothing to write, and it is counted as skipped rather than silently
            // dropped.
            var already = new MaplistEditDocument();
            already.Set(new MapAvailabilityEdit { Package = @"BioP_MPDish", Available = true });
            project.MaplistEdits = already;

            var second = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.AreEqual(0, second.MaplistWritesWritten);
            Assert.AreEqual(1, second.UnchangedEditsSkipped);
        }

        /// <summary>
        /// Editing a map's presentation is one exact-text replacement, and everything the entry
        /// carries that this build does not model comes through untouched.
        /// </summary>
        [TestMethod]
        public void EditingAMapIsOneReplacement()
        {
            var (maps, sections) = Fake();

            var project = ModMakerProject.Create(@"Moved", @"LMP", @"1");
            var document = new MaplistEditDocument();
            document.Set(new MapEntryEdit { Id = 2, Package = @"BioP_MPDish", GalaxyAtWarX = 500, MusicEventName = @"Set_mus_map_mpcer" });
            project.MaplistEdits = document;

            var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors));

            var write = OnlyMapWrite(result);
            Assert.AreEqual(@"MasterMapList", write.Name);
            Assert.AreEqual(ShippedDish, write.Replace.Remove);
            StringAssert.Contains(write.Replace.Add, @"GalaxyAtWarMapPosition=(X=500,Y=216)");
            StringAssert.Contains(write.Replace.Add, @"MusicEventName=""Set_mus_map_mpcer""");

            // The fields ModMaker does not model survive verbatim. Regenerating the entry would drop
            // them, and the map would lose its name and its picture while the mod installed cleanly.
            StringAssert.Contains(write.Replace.Add, @"PrettyName=618041");
            StringAssert.Contains(write.Replace.Add, @"Image=""GUI_MPImages.MatchSettings.OptMapDish""");
            StringAssert.Contains(write.Replace.Add, @"GalaxyAtWarMapSubtitle=706337");
        }

        /// <summary>
        /// The edits a map roster cannot take are refused rather than written.
        ///
        /// The two that matter are specific to maps. The Random choice is a picker entry with no level
        /// behind it, so editing it writes to nothing; and turning every map off produces a mod that
        /// installs and then cannot start a match, which no message in the game will explain.
        /// </summary>
        [TestMethod]
        public void MapEditsTheGameCannotTakeAreRefused()
        {
            var (maps, _) = Fake();

            var placeholder = new MaplistEditDocument();
            placeholder.Set(new MapEntryEdit { Id = 0, Package = null, GalaxyAtWarX = 1 });
            Assert.IsTrue(MaplistEditValidator.Validate(placeholder, maps)
                .Any(p => p.Severity == MaplistProblemSeverity.Error && p.Message.Contains(@"Random")));

            var everythingOff = new MaplistEditDocument();
            foreach (var package in maps.AvailableMaps)
                everythingOff.Set(new MapAvailabilityEdit { Package = package, Available = false });
            Assert.IsTrue(MaplistEditValidator.Validate(everythingOff, maps)
                .Any(p => p.Severity == MaplistProblemSeverity.Error && p.Message.Contains(@"no match could start")));

            var invented = new MaplistEditDocument();
            invented.Set(new MapEntryEdit { Id = 2, Package = @"BioP_MPDish", MusicEventName = @"Set_mus_not_a_thing" });
            Assert.IsTrue(MaplistEditValidator.Validate(invented, maps)
                .Any(p => p.Severity == MaplistProblemSeverity.Error && p.Message.Contains(@"music event")));

            var absent = new MaplistEditDocument();
            absent.Set(new MapEntryEdit { Id = 999, Package = @"BioP_Nowhere", GalaxyAtWarX = 1 });
            Assert.IsTrue(MaplistEditValidator.Validate(absent, maps)
                .Any(p => p.Severity == MaplistProblemSeverity.Error));
        }

        /// <summary>
        /// A DLC replaces a map by re-using its id, so an edit follows the ID and says so when the
        /// package under it has changed.
        /// </summary>
        [TestMethod]
        public void AnEditFollowsTheIdWhenTheMapBehindItChanged()
        {
            var (maps, _) = Fake();

            var document = new MaplistEditDocument();
            document.Set(new MapEntryEdit { Id = 2, Package = @"BioP_MPTowr", GalaxyAtWarX = 1 });

            var problems = MaplistEditValidator.Validate(document, maps);
            Assert.AreEqual(0, problems.Count(p => p.Severity == MaplistProblemSeverity.Error));
            Assert.IsTrue(problems.Any(p => p.Message.Contains(@"follows the id")));
        }

        /// <summary>The collection round-trips through a project, and reading it never creates one.</summary>
        [TestMethod]
        public void TheMaplistCollectionRoundTripsThroughAProject()
        {
            var project = ModMakerProject.Create(@"Maps", @"LMP", @"1");

            Assert.AreEqual(0, project.MaplistEdits.Count);
            Assert.IsNull(project.Document[@"collections"], @"Reading edits must not create a collections map");

            var document = new MaplistEditDocument();
            document.Set(new MapAvailabilityEdit { Package = @"BioP_MPDish", Available = false });
            document.Set(new MapEntryEdit { Id = 2, Package = @"BioP_MPDish", GalaxyAtWarX = 500 });
            project.MaplistEdits = document;

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ModMakerProject.FileExtension);
            try
            {
                project.Save(path);
                var reopened = ModMakerProject.Load(path);

                Assert.AreEqual(2, reopened.MaplistEdits.Count);
                Assert.AreEqual(500d, reopened.MaplistEdits.Entries.Single().GalaxyAtWarX);
                Assert.IsFalse(reopened.MaplistEdits.Availability.Single().Available);

                // A field the user did not set is ABSENT from the file, not null - so a later build
                // that learns to edit it can tell "unset" from "set to nothing".
                var entry = (JObject)reopened.Document[@"collections"][@"maplist"][@"entries"][0];
                Assert.IsNull(entry[@"gawY"]);
                Assert.IsNull(entry[@"music"]);

                reopened.MaplistEdits = new MaplistEditDocument();
                Assert.IsNull(reopened.Document[@"collections"]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>A document from a future build refuses rather than dropping what it cannot read.</summary>
        [TestMethod]
        public void ANewerMaplistFormatIsRefused()
        {
            Assert.Throws<NotSupportedException>(() =>
                MaplistEditDocument.FromJson(JObject.Parse(@"{""format"": 99}")));
        }

        private const string ShippedDish =
            @"(ID=2, PackageName=""BioP_MPDish"", PrettyName=618041, Description=618050, " +
            @"Image=""GUI_MPImages.MatchSettings.OptMapDish"", GalaxyAtWarMapPosition=(X=424,Y=216), " +
            @"GalaxyAtWarMapSubtitle=706337, MusicEventName=""Set_mus_map_mpdish"")";

        private const string ShippedRandom =
            @"(ID=0, PrettyName=618000, Image=""GUI_MPImages.MatchSettings.OptMapRandom"")";

        private const string ShippedCnfl =
            @"(ID=9, PackageName=""BioP_MPCnfl"", PrettyName=618099, GalaxyAtWarMapPosition=(X=100,Y=100), " +
            @"MusicEventName=""Set_mus_map_mpcer"")";

        private const string FakeSection = @"sfxonlinefoundation.sfxonlinegamesettings";

        /// <summary>
        /// A roster built by hand, so the compiler's rules can be exercised without a game.
        ///
        /// The section index is real, because the compiler has to answer "which coalesced sub-file
        /// does this section live in" and that mapping is data rather than a naming convention.
        /// </summary>
        private static (MapListCatalogue, SectionIndex) Fake(string[] availableRaw = null)
        {
            availableRaw ??= new[] { @"BioP_MPDish" };

            var maps = new MapListCatalogue { SettingsSection = FakeSection };
            maps.Maps.Add(new MapEntry { Id = 0, RawValue = ShippedRandom, Image = @"GUI_MPImages.MatchSettings.OptMapRandom" });
            maps.Maps.Add(new MapEntry
            {
                Id = 2, PackageName = @"BioP_MPDish", PrettyName = @"618041", Description = @"618050",
                Image = @"GUI_MPImages.MatchSettings.OptMapDish", GalaxyAtWarX = 424, GalaxyAtWarY = 216,
                MusicEventName = @"Set_mus_map_mpdish", RawValue = ShippedDish,
            });
            maps.Maps.Add(new MapEntry
            {
                Id = 9, PackageName = @"BioP_MPCnfl", PrettyName = @"618099",
                GalaxyAtWarX = 100, GalaxyAtWarY = 100,
                MusicEventName = @"Set_mus_map_mpcer", RawValue = ShippedCnfl,
            });

            foreach (var raw in availableRaw) maps.AvailableMaps.Add(raw.Trim());
            foreach (var map in maps.Maps)
                map.IsAvailable = maps.AvailableMaps.Contains(map.PackageName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var property = new CoalesceProperty(@"AvailableMaps",
                availableRaw.Select(v => new CoalesceValue(v, CoalesceParseAction.New)).ToList());
            var section = new CoalesceSection
            {
                { @"AvailableMaps", property },
                { @"MasterMapList", new CoalesceProperty(@"MasterMapList") },
            };

            var sections = new SectionIndex(
                new Dictionary<string, CoalesceSection>(StringComparer.OrdinalIgnoreCase) { [FakeSection] = section },
                new List<string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [FakeSection] = @"BioGame" });

            return (maps, sections);
        }

        private static CompileInputs Inputs(MapListCatalogue maps, SectionIndex sections) => new()
        {
            Maps = maps, Sections = sections, MountPriority = 4800, App = @"tests",
        };

        /// <summary>
        /// Puts a match-settings toggle in the collection that owns it.
        ///
        /// The two live apart now: a faction toggle is made on the wavelist page, because turning one
        /// off takes every wave under it out of the game, and a page may only post to the collection
        /// it edits. The difficulties stayed with the maps. Both are still the same write to the same
        /// config section, which is what these tests are about - so the split is handled here once
        /// rather than in every test.
        /// </summary>
        private static void Toggle(ModMakerProject project, PickerOptionEdit edit)
        {
            if (string.Equals(edit.Picker, MatchPickers.Factions, StringComparison.OrdinalIgnoreCase))
            {
                var waves = project.WavelistEdits;
                waves.Set(edit);
                project.WavelistEdits = waves;
                return;
            }

            var maps = project.MaplistEdits;
            maps.Set(edit);
            project.MaplistEdits = maps;
        }

        /// <summary>
        /// Turning a choice off subtracts its picker entry, byte for byte - factions and difficulties
        /// alike.
        ///
        /// Neither has an availability list of its own: being in `EnemyTypes` or `ChallengeTypes` IS
        /// being offered. So the edit is a list subtraction, and because Remove matches on exact value
        /// text it has to carry the install's own bytes - an entry rebuilt from the parsed fields
        /// would differ by a space, match nothing, and leave the choice in the picker while the mod
        /// reported success.
        /// </summary>
        [TestMethod]
        public void TurningAMatchSettingsChoiceOffSubtractsItsPickerEntry()
        {
            foreach (var (picker, id, property, raw) in new[]
                     {
                         (MatchPickers.Factions, 2, @"EnemyTypes", ShippedGeth),
                         (MatchPickers.Difficulties, 3, @"ChallengeTypes", ShippedGold),
                     })
            {
                var (maps, sections) = FakeWithPickers();

                var project = ModMakerProject.Create(@"Fewer choices", @"LMP", @"1");
                Toggle(project, new PickerOptionEdit { Picker = picker, Id = id, Enabled = false });

                // The faction case goes in through the wavelist collection and the difficulty through
                // the maplist one, and both come out of the same place: one config write, two homes.
                var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors.Select(e => e.Message)));

                var write = OnlyMapWrite(result);
                Assert.AreEqual(property, write.Name, picker);
                Assert.AreEqual(raw, write.Value, $@"{picker}: the subtraction must carry the install's bytes");
                Assert.IsNull(write.Replace, @"A list subtraction is a bare Remove, not a paired replacement");
                Assert.AreEqual(CoalesceValue.GetValueType(CoalesceParseAction.Remove), write.ValueType);
            }
        }

        /// <summary>
        /// A choice already offered writes nothing, and one the install does not have is refused.
        ///
        /// The two halves are different failures. Enabling something already enabled is a no-op worth
        /// counting; enabling something absent cannot be done at all - there is no entry to put back,
        /// and building one would be writing a choice rather than restoring it.
        /// </summary>
        [TestMethod]
        public void APickerEditThatCannotBeWrittenSaysSo()
        {
            var (maps, sections) = FakeWithPickers();

            var project = ModMakerProject.Create(@"Pickers", @"LMP", @"1");
            Toggle(project, new PickerOptionEdit { Picker = MatchPickers.Factions, Id = 1, Enabled = true });

            var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors.Select(e => e.Message)));
            Assert.AreEqual(0, result.MaplistWritesWritten);
            Assert.AreEqual(1, result.UnchangedEditsSkipped);

            project.WavelistEdits = new WavelistEditDocument();
            Toggle(project, new PickerOptionEdit { Picker = MatchPickers.Difficulties, Id = 77, Enabled = true });

            var refused = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsFalse(refused.Succeeded);
            Assert.IsTrue(refused.Errors.Any(e => e.Message.Contains(@"no difficulty 77", StringComparison.Ordinal)),
                string.Join(@" | ", refused.Errors.Select(e => e.Message)));

            // A picker this build has never heard of is refused rather than written to a guessed
            // property - a project from a newer ModMaker must not half-compile. Both collections have
            // to refuse it: the two are checked by different code paths, and an unknown name reaching
            // either one is the same untranslatable edit.
            var unknown = new MaplistEditDocument();
            unknown.Set(new PickerOptionEdit { Picker = @"consumables", Id = 1, Enabled = false });
            project.MaplistEdits = unknown;

            var strange = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsFalse(strange.Succeeded);
            Assert.IsTrue(strange.Errors.Any(e => e.Message.Contains(@"not a match-settings list", StringComparison.Ordinal)),
                string.Join(@" | ", strange.Errors.Select(e => e.Message)));

            project.MaplistEdits = new MaplistEditDocument();
            var strayed = new WavelistEditDocument();
            strayed.Set(new PickerOptionEdit { Picker = MatchPickers.Difficulties, Id = 1, Enabled = false });
            project.WavelistEdits = strayed;

            var misfiled = ProjectCompiler.Compile(project, Inputs(maps, sections));
            Assert.IsFalse(misfiled.Succeeded);
            Assert.IsTrue(misfiled.Errors.Any(e => e.Message.Contains(@"the wavelist editor writes", StringComparison.Ordinal)),
                string.Join(@" | ", misfiled.Errors.Select(e => e.Message)));
        }

        /// <summary>
        /// Emptying either picker is refused, the same way emptying the map roster is.
        ///
        /// All three leave a lobby that cannot start a match, and none of them says anything in game
        /// about why.
        /// </summary>
        [TestMethod]
        public void EmptyingAPickerIsRefused()
        {
            foreach (var (picker, ids, noun) in new[]
                     {
                         (MatchPickers.Factions, new[] { 1, 2 }, @"faction"),
                         (MatchPickers.Difficulties, new[] { 1, 3 }, @"difficulty"),
                     })
            {
                var (maps, sections) = FakeWithPickers();
                var project = ModMakerProject.Create(@"Nothing to play", @"LMP", @"1");

                foreach (var id in ids) Toggle(project, new PickerOptionEdit { Picker = picker, Id = id, Enabled = false });

                var result = ProjectCompiler.Compile(project, Inputs(maps, sections));
                Assert.IsFalse(result.Succeeded, picker);
                Assert.IsTrue(result.Errors.Any(e => e.Message.Contains($@"every {noun} is turned off", StringComparison.Ordinal)),
                    string.Join(@" | ", result.Errors.Select(e => e.Message)));

                // One left is legal and worth saying out loud: a single-choice lobby is a real
                // decision and also an easy accident.
                project.MaplistEdits = new MaplistEditDocument();
                project.WavelistEdits = new WavelistEditDocument();
                Toggle(project, new PickerOptionEdit { Picker = picker, Id = ids[0], Enabled = false });

                var single = ProjectCompiler.Compile(project, Inputs(maps, sections));
                Assert.IsTrue(single.Succeeded, string.Join(@" | ", single.Errors.Select(e => e.Message)));
                Assert.IsTrue(single.Warnings.Any(w => w.Message.Contains($@"only one {noun} is left", StringComparison.Ordinal)),
                    string.Join(@" | ", single.Warnings.Select(w => w.Message)));
            }
        }

        /// <summary>
        /// Every edit kind round-trips through a project file together, and the two pickers do not
        /// collide.
        ///
        /// The ids restart per list - faction 1 and difficulty 1 are both real and unrelated - so a
        /// document keyed on the id alone would have one silently replace the other. They are in
        /// different collections now, which makes a collision impossible rather than merely handled -
        /// but the key is still built from both, because a project written before the move carries
        /// both in one list and has to survive being read.
        /// </summary>
        [TestMethod]
        public void PickerTogglesSurviveTheProjectFile()
        {
            var document = new MaplistEditDocument();
            document.Set(new MapAvailabilityEdit { Package = @"BioP_MPDish", Available = false });
            document.Set(new PickerOptionEdit
            {
                Picker = MatchPickers.Factions, Id = 1,
                Label = @"SFXGameMPContent.SFXWave_Horde_Cerberus", Enabled = false,
            });
            document.Set(new PickerOptionEdit
            {
                Picker = MatchPickers.Difficulties, Id = 1, Label = @"Bronze", Enabled = false,
            });

            var reloaded = MaplistEditDocument.FromJson(document.ToJson());

            Assert.AreEqual(3, reloaded.Count);
            Assert.AreEqual(2, reloaded.Pickers.Count, @"The two pickers share an id and must not share a key");
            Assert.AreEqual(1, reloaded.For(MatchPickers.Factions).Count());
            Assert.AreEqual(@"Bronze", reloaded.For(MatchPickers.Difficulties).Single().Label);

            // Reverting takes it back out rather than storing an "enabled" that writes nothing, and it
            // reverts one picker without touching the other.
            Assert.IsTrue(reloaded.RevertPickerOption(MatchPickers.Factions, 1));
            Assert.AreEqual(1, reloaded.Pickers.Count);
            Assert.AreEqual(1, reloaded.For(MatchPickers.Difficulties).Count());
            Assert.IsFalse(reloaded.RevertPickerOption(MatchPickers.Factions, 1));
        }

        /// <summary>
        /// A faction toggle round-trips through the wavelist collection, beside the wave edits.
        ///
        /// The toggle carries no picker name when this page writes it - only one list lives there -
        /// so reading one back has to stamp it, or the key it is stored under has a null in it and
        /// every entry collides with every other.
        /// </summary>
        [TestMethod]
        public void FactionTogglesSurviveTheWavelistCollection()
        {
            var document = new WavelistEditDocument();
            document.Rows.Add(new WavelistRowEdit
            {
                Section = @"SFXGameMPContent.SFXWave_Horde_Cerberus",
                InstanceId = @"sfxwave_horde_cerberus1", WaveIndex = 1, Difficulty = @"DO_Level1",
            });
            document.Set(new PickerOptionEdit { Id = 1, Label = @"SFXWave_Horde_Cerberus2", Enabled = false });
            document.Set(new PickerOptionEdit { Id = 2, Label = @"SFXWave_Horde_Geth2", Enabled = false });

            Assert.AreEqual(3, document.Count, @"A faction turned off is an edit the project has to count");

            var reloaded = WavelistEditDocument.FromJson(document.ToJson());

            Assert.AreEqual(1, reloaded.Rows.Count);
            Assert.AreEqual(2, reloaded.Pickers.Count);
            Assert.AreEqual(2, reloaded.For(MatchPickers.Factions).Count(),
                @"An entry written without a picker name has to read back as a faction");
            Assert.IsTrue(reloaded.Pickers.All(p => p.Picker == MatchPickers.Factions));

            Assert.IsTrue(reloaded.RevertPickerOption(1));
            Assert.AreEqual(1, reloaded.Pickers.Count);
            Assert.IsFalse(reloaded.RevertPickerOption(1));
        }

        /// <summary>
        /// A project written before the faction toggles moved has them moved for it, on load.
        ///
        /// Not cosmetic. A page may only post to the collection it edits, so a toggle left in the
        /// maplist collection is one the wavelist editor cannot show and the user cannot undo - while
        /// it is still in every build they make. The difficulties beside it must NOT move.
        /// </summary>
        [TestMethod]
        public void FactionTogglesMoveOutOfTheMaplistCollectionOnLoad()
        {
            var path = Path.Combine(Path.GetTempPath(), $@"modmaker-migrate-{Guid.NewGuid():N}.mmproj");
            try
            {
                var project = ModMakerProject.Create(@"Written before the move", @"LMP", @"1");
                var maplist = new MaplistEditDocument();
                maplist.Set(new MapAvailabilityEdit { Package = @"BioP_MPDish", Available = false });
                maplist.Set(new PickerOptionEdit
                {
                    Picker = MatchPickers.Factions, Id = 2, Label = @"SFXWave_Horde_Geth", Enabled = false,
                });
                maplist.Set(new PickerOptionEdit
                {
                    Picker = MatchPickers.Difficulties, Id = 3, Label = @"Gold", Enabled = false,
                });
                project.MaplistEdits = maplist;
                project.Save(path);

                var loaded = ModMakerProject.Load(path);

                Assert.AreEqual(1, loaded.WavelistEdits.Pickers.Count, @"The faction toggle should have moved");
                Assert.AreEqual(2, loaded.WavelistEdits.For(MatchPickers.Factions).Single().Id);
                Assert.AreEqual(@"SFXWave_Horde_Geth", loaded.WavelistEdits.Pickers[0].Label,
                    @"The entry moves whole; the label is what catches an install where the ids changed");

                Assert.AreEqual(1, loaded.MaplistEdits.Pickers.Count, @"The difficulty toggle must stay put");
                Assert.AreEqual(MatchPickers.Difficulties, loaded.MaplistEdits.Pickers[0].Picker);
                Assert.AreEqual(1, loaded.MaplistEdits.Availability.Count, @"Nothing else may be disturbed");

                // And it still compiles to the same two writes it would have before the move.
                var (maps, sections) = FakeWithPickers();
                var result = ProjectCompiler.Compile(loaded, Inputs(maps, sections));
                Assert.IsTrue(result.Succeeded, string.Join(@" | ", result.Errors.Select(e => e.Message)));
                Assert.AreEqual(3, result.MaplistWritesWritten,
                    @"The map, the faction and the difficulty should each have been written");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private const string ShippedCerberus =
            @"(ID=1, Name=618060, WaveClass=""SFXGameMPContent.SFXWave_Horde_Cerberus"", Image=""GUI_MPImages.MatchSettings.OptEmyCerberus"")";

        private const string ShippedGeth =
            @"  (ID=2, Name=618061, WaveClass=""SFXGameMPContent.SFXWave_Horde_Geth"", Image=""GUI_MPImages.MatchSettings.OptEmyGeth"")  ";

        private const string ShippedBronze =
            @"(ID=1, Name=618070, Image=""GUI_MPImages.MatchSettings.OptDifBronze"")";

        private const string ShippedGold =
            @"  (ID=3, Name=618072, Image=""GUI_MPImages.MatchSettings.OptDifGold"")  ";

        /// <summary>The map fake, plus an enemy picker and a difficulty picker with two entries each.</summary>
        private static (MapListCatalogue, SectionIndex) FakeWithPickers()
        {
            var (maps, sections) = Fake(availableRaw: new[] { @"BioP_MPDish", @"BioP_MPCnfl" });

            void Add(List<MatchSettingsOption> into, int id, string raw, string waveClass)
            {
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"ID"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                };
                if (waveClass != null) fields[@"WaveClass"] = waveClass;
                into.Add(new MatchSettingsOption { Id = id, RawValue = raw, Fields = fields });
            }

            Add(maps.EnemyTypes, 1, ShippedCerberus, @"SFXGameMPContent.SFXWave_Horde_Cerberus");
            Add(maps.EnemyTypes, 2, ShippedGeth, @"SFXGameMPContent.SFXWave_Horde_Geth");
            Add(maps.ChallengeTypes, 1, ShippedBronze, null);
            Add(maps.ChallengeTypes, 3, ShippedGold, null);

            sections.Find(FakeSection).Add(@"EnemyTypes", new CoalesceProperty(@"EnemyTypes"));
            sections.Find(FakeSection).Add(@"ChallengeTypes", new CoalesceProperty(@"ChallengeTypes"));
            return (maps, sections);
        }

        private static PropertyWrite OnlyMapWrite(CompileResult result) =>
            ((CoalescedJob)result.Manifest.Jobs.Single()).Files.Single().Value.Sections.Single().Properties.Single();

        #endregion

        #region Helpers

        /// <summary>
        /// The lobby's own galaxy picture, and the geometry that makes a map position mean something.
        ///
        /// `GalaxyAtWarMapPosition` is two numbers with no stated units. The editor used to scale them
        /// against their own measured extent, which draws a picture that is proportionally right and
        /// absolutely wrong - and changes shape whenever a map is added. The real space was read out
        /// of the movie that draws the map, and this is the test that it still describes the install.
        /// </summary>
        [TestMethod]
        public void TheGalaxyMapAndItsRegionsComeFromTheGame()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var clock = Stopwatch.StartNew();
            var galaxy = GalaxyMap.Load(target);
            clock.Stop();

            foreach (var problem in galaxy.Problems) Console.WriteLine($@"  problem: {problem}");
            Assert.IsTrue(galaxy.Available, @"The Galaxy at War background could not be read");

            Console.WriteLine($@"background: {galaxy.Background.Width}x{galaxy.Background.Height} " +
                              $@"from {galaxy.Background.SourceFile}, {galaxy.Background.Png.Length} bytes, " +
                              $@"{clock.ElapsedMilliseconds} ms");

            // The art ships as a single 4096 mip with nothing smaller behind it, so the smaller level
            // has to be generated. If that ever silently stopped happening the payload would carry
            // about eighteen megabytes of base64 and the page would sit there decoding it.
            Assert.IsTrue(galaxy.Background.Width <= GalaxyMap.MaxDimension
                          && galaxy.Background.Height <= GalaxyMap.MaxDimension,
                $@"The background was not downscaled: {galaxy.Background.Width}x{galaxy.Background.Height}");
            Assert.AreEqual(galaxy.Background.Width, galaxy.Background.Height,
                @"The galaxy art is square - the coordinate space it is drawn into is not");

            // ---------------------------------------------------------------- the regions
            Assert.AreEqual(5, galaxy.Zones.Count, @"The lobby map has five regions");
            foreach (var zone in galaxy.Zones)
            {
                Assert.IsNotNull(zone.Art?.Pixels,
                    $@"{zone.Name}'s art did not decode, so no map can be attributed to it");
                Console.WriteLine($@"  {zone.Name,-22} {zone.Art.Width}x{zone.Art.Height} " +
                                  $@"box x {zone.X0}..{zone.X1} y {zone.Y0}..{zone.Y1}");
            }

            // ---------------------------------------------------------------- membership
            var maps = CachedMaps(target);
            var placed = maps.Maps.Where(m => !m.IsPlaceholder && m.GalaxyAtWarX != null && m.GalaxyAtWarY != null)
                             .ToList();

            var byZone = new Dictionary<string, int>();
            var homeless = new List<string>();
            foreach (var map in placed)
            {
                var zone = galaxy.ZoneAt(map.GalaxyAtWarX.Value, map.GalaxyAtWarY.Value);
                if (zone == null) homeless.Add($@"{map.PackageName} ({map.GalaxyAtWarX}, {map.GalaxyAtWarY})");
                else byZone[zone.Name] = byZone.GetValueOrDefault(zone.Name) + 1;
            }

            foreach (var (name, count) in byZone.OrderByDescending(kv => kv.Value))
                Console.WriteLine($@"  {name,-22} {count} maps");
            Console.WriteLine($@"  (no region)            {homeless.Count} maps: {string.Join(@", ", homeless)}");

            // Membership is decided by the zone art's ALPHA, not by its bounding box: the boxes
            // overlap heavily - Attican's spans nearly the whole map - so a box test would put most
            // maps in the wrong region while looking like it worked.
            Assert.IsTrue(byZone.Count >= 4,
                @"Nearly every region of the galaxy should hold at least one firebase");
            Assert.IsTrue(placed.Count - homeless.Count > placed.Count / 2,
                $@"Only {placed.Count - homeless.Count} of {placed.Count} placed maps landed in any region");

            // Earth's maps all sit at one point, and it had better be the Alliance's region. This is
            // the assertion that would have caught the coordinate space being wrong: at the extent
            // guessed from the shipped values alone, this point lands somewhere else entirely.
            var earth = placed.FirstOrDefault(m => m.PackageName?.Equals(@"BioP_MPRoad", StringComparison.OrdinalIgnoreCase) == true);
            if (earth != null)
            {
                var zone = galaxy.ZoneAt(earth.GalaxyAtWarX.Value, earth.GalaxyAtWarY.Value);
                Assert.AreEqual(@"Earth and Alliance", zone?.Name,
                    $@"Firebase London is on Earth; it resolved to '{zone?.Name ?? @"nowhere"}'");
            }

            // The in-development maps parked at the origin are outside every region, and saying so is
            // the right answer rather than attributing them to whichever box happens to reach 0,0.
            Assert.IsNull(galaxy.ZoneAt(0, 0), @"The origin belongs to no region");
        }

        /// <summary>
        /// The galaxy reaches the page with the space it is drawn in, and the maps carry their region.
        /// </summary>
        [TestMethod]
        public void TheGalaxyReachesThePayload()
        {
            var target = TryGetLMPTarget();
            if (target == null) { Assert.Inconclusive(@"No LMP install found; skipping."); return; }

            var maps = CachedMaps(target);
            var galaxy = GalaxyMap.Load(target);
            var payload = MaplistEditorPayload.Build(maps, GameStrings.Load(target), null, galaxy);

            var art = payload[@"galaxy"] as JObject;
            Assert.IsNotNull(art, @"The page has nothing to draw the positions on");
            StringAssert.StartsWith((string)art[@"image"], @"data:image/png;base64,");

            // The space is wider than it is tall while the art is square. A page that preserved the
            // art's aspect ratio would misplace every map horizontally, by up to a seventh of the
            // width at the edges - so the two numbers travel together and neither is assumed.
            Assert.AreEqual(614.4, (double)art[@"width"], 0.01);
            Assert.AreEqual(512.0, (double)art[@"height"], 0.01);
            Assert.IsTrue((double)art[@"width"] > (double)art[@"height"],
                @"The coordinate space is stretched relative to the art, which is what makes it wrong to preserve the aspect");

            var zones = art[@"zones"] as JArray;
            Assert.AreEqual(5, zones.Count);
            foreach (var zone in zones)
            {
                Assert.IsNotNull((string)zone[@"name"]);
                StringAssert.StartsWith((string)zone[@"image"], @"data:image/png;base64,");
                Assert.IsTrue((double)zone[@"x1"] > (double)zone[@"x0"] && (double)zone[@"y1"] > (double)zone[@"y0"]);
            }

            // Absent rather than null when a map is in no region: much of the galaxy belongs to none.
            var named = payload[@"maps"].Count(m => m[@"zoneName"] != null);
            Console.WriteLine($@"maps carrying a region: {named} of {payload[@"maps"].Count()}");
            Assert.IsTrue(named > 0, @"No map was attributed to a region");
        }

        private static MapListCatalogue CachedMaps(GameTarget target)
        {
            lock (CacheLock)
            {
                if (_maps != null) return _maps;
                _classes ??= ClassScanner.Scan(target);
                return _maps = MapListCatalogue.Build(MergedGameConfig.Load(target), _classes);
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
