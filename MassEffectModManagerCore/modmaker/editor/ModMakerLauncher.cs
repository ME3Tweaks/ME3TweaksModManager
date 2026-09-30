using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmaker.generator;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// Opens the ModMaker editor: reads the installed game, joins it to the shipped text layer, and
    /// shows the result.
    /// </summary>
    public static class ModMakerLauncher
    {
        /// <summary>
        /// Builds the catalogue for a target and opens the editor window over <paramref name="owner"/>.
        ///
        /// The scan takes several seconds - it opens every package in the install - so it runs off the
        /// UI thread behind a background task entry. Doing it inline freezes the app with no
        /// indication of why.
        /// </summary>
        public static void Open(Window owner, GameTarget target)
        {
            if (target == null)
            {
                M3L.ShowDialog(owner, @"No supported game target is selected.", @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!ModMakerCatalogue.SupportsGame(target.Game))
            {
                M3L.ShowDialog(owner, $@"ModMaker does not support {target.Game}.", @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"ModMakerCatalogue",
                @"Reading game data for ModMaker", @"ModMaker ready");

            string payload = null;
            string enemyPayload = null;
            string mapPayload = null;
            string valuesPayload = null;
            string objectivePayload = null;
            ObjectiveCatalogue objectives = null;
            string weaponPayload = null;
            WeaponCatalogue weapons = null;
            IReadOnlyDictionary<string, CatalogueClass> scanned = null;
            InstanceCatalogue perObject = null;
            WavelistCatalogue catalogue = null;
            DifficultyCatalogue difficulty = null;
            MapListCatalogue mapRoster = null;
            SectionIndex sections = null;
            GameStrings tlk = null;
            Exception failure = null;

            Task.Run(() =>
            {
                try
                {
                    var classes = ClassScanner.Scan(target, (done, total) =>
                        BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, $@"Reading game data for ModMaker ({done}/{total})"));

                    var config = MergedGameConfig.Load(target);
                    var instances = InstanceCatalogue.Build(config, classes);
                    var waves = WavelistCatalogue.Build(config, classes, instances);
                    var maps = MapListCatalogue.Build(config, classes);
                    difficulty = DifficultyCatalogue.Build(config, classes);

                    foreach (var warning in waves.Warnings) MLog.Warning($@"ModMaker: {warning}");
                    foreach (var warning in difficulty.Warnings) MLog.Warning($@"ModMaker: {warning}");

                    // The enemy editor's words come from the shipped layer, not from the game: stat
                    // names carry no TLK string and no unit anywhere in the install. A layer that
                    // fails to load leaves the editor showing internal names rather than closing it,
                    // which is the same fallback a mod's unknown enemy already gets.
                    var text = LoadTextLayer(target);
                    var table = EnemyEditorPayload.MultiplayerTable(difficulty, classes);
                    if (table == null)
                        MLog.Warning(@"ModMaker: no multiplayer difficulty handler was found; the enemy editor is unavailable.");
                    else
                        enemyPayload = EnemyEditorPayload.ToJson(table, text?.EnemyStats, maps,
                            difficulty.Warnings, indented: false);

                    // The map editor's names and pictures both come out of the install: the config
                    // holds a string ref and an object path, and neither is worth showing. Decoding
                    // costs about a second and stops as soon as every wanted texture is found.
                    mapRoster = maps;
                    var strings = GameStrings.Load(target);
                    if (!strings.Available)
                        MLog.Warning(@"ModMaker: no TLKs were read; maps will be shown by package name " +
                                     @"and string references by number.");

                    // The same instance the map payload used, handed to the window: it caches what it
                    // resolves, and the generic editor asks it about references the user types.
                    tlk = strings;

                    var banners = BannerLibrary.Load(target, MaplistEditorPayload.ImagePaths(maps),
                        (done, total) => BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task,
                            $@"Reading map art for ModMaker ({done}/{total})"));

                    foreach (var problem in banners.Problems) MLog.Warning($@"ModMaker: {problem}");

                    // The lobby's own galaxy picture, and the five regions drawn over it. A map's
                    // position is a position on THIS, so without it the editor can only scale the
                    // shipped values against each other and hope.
                    var galaxy = GalaxyMap.Load(target,
                        (done, total) => BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task,
                            $@"Reading the galaxy map for ModMaker ({done}/{total})"));
                    foreach (var problem in galaxy.Problems) MLog.Warning($@"ModMaker: {problem}");

                    mapPayload = MaplistEditorPayload.ToJson(maps, strings, banners, galaxy, indented: false);

                    // The objectives: which waves carry one, which the game may roll, and the rules
                    // each runs by. Built after the TLKs are loaded because half of what an objective
                    // sets is the text it shows, and "srWaveFailedHint = 724698" is not something a
                    // person can act on.
                    objectives = ObjectiveCatalogue.Build(config, classes, instances);
                    foreach (var warning in objectives.Warnings) MLog.Warning($@"ModMaker objectives: {warning}");
                    objectivePayload = ObjectiveEditorPayload.ToJson(objectives, strings, indented: false);

                    // Built after the art, not before: a difficulty column's banner is one of the
                    // challenge textures, and the catalogue only knows its object path. Building the
                    // wavelist payload first - which is what this did - meant the grid was handed a
                    // path it could not draw, and so drew nothing.
                    payload = WavelistEditorPayload.ToJson(waves, maps, banners, text, strings, indented: false);

                    // Kept as well as serialised: the payload is what the page draws, the catalogue is
                    // what an edit is checked against, and the index is what a compile writes through.
                    // All three come from this one scan, which takes seconds.
                    catalogue = waves;
                    sections = config.BuildSectionIndex();
                    scanned = classes;
                    perObject = instances;

                    // The weapons. After the section index, which the payload needs to resolve every
                    // inherited value, and after the TLKs: a roster of SFXWeapon_SMG_Hornet is a
                    // roster nobody can read, and PrettyName is a string reference rather than a name.
                    weapons = WeaponCatalogue.Build(config, classes);
                    foreach (var warning in weapons.Warnings) MLog.Warning($@"ModMaker weapons: {warning}");

                    // The store-card art, loaded the same way the map banners are. Most player
                    // weapons have one; a roster of names alone is markedly harder to scan than one
                    // showing the card a player would recognise.
                    //
                    // Downscaled here rather than by the browser: the cards ship as large as
                    // 1024x512, and a browser asked for that reduction in one pass produces visible
                    // aliasing, where BannerLibrary's maxDimension path builds a filtered mip chain.
                    // 256 is the art's own width, which the detail pane draws at 176px - going lower
                    // and scaling back up is what made the first cut of these look soft.
                    var cards = BannerLibrary.Load(target, weapons.CardTextures,
                        (done, total) => BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task,
                            $@"Reading weapon art for ModMaker ({done}/{total})"),
                        maxDimension: 256);
                    foreach (var problem in cards.Problems) MLog.Warning($@"ModMaker weapons: {problem}");

                    // The text layer is passed for the descriptions: the game has none of its own
                    // for turrets or enemy-only guns, and what it does have for them is a shared
                    // placeholder. See modmaker\data\textlayer.json, "classText".
                    weaponPayload = WeaponEditorPayload.ToJson(weapons, sections, classes, strings, cards,
                                                               text, indented: false);

                    // The generic surface. Built last because it is the expensive one to serialise -
                    // some eight megabytes of value trees - and because a failure here should cost
                    // that one page rather than the whole editor: the three dedicated editors are
                    // already built by this point and work without it.
                    try
                    {
                        var bundle = DefinitionBundleWriter.Build(new BundleInputs
                        {
                            BundleVersion = App.AppVersion?.ToString() ?? @"0.0.0",
                            Game = target.Game.ToString(),
                            Classes = classes,
                            Config = config,
                            Instances = instances,
                            Waves = waves,
                            Difficulty = difficulty,
                            Maps = maps,
                            Text = text,
                        });

                        foreach (var warning in bundle.Warnings) MLog.Warning($@"ModMaker bundle: {warning}");
                        valuesPayload = ValuesEditorPayload.ToJson(bundle.Bundle, indented: false);
                    }
                    catch (Exception e)
                    {
                        MLog.Exception(e, @"ModMaker: the definition bundle could not be built; " +
                                          @"the generic value editor will not be offered");
                    }
                }
                catch (Exception e)
                {
                    // Caught rather than left to the task's unobserved exception handler, so the user
                    // is told the editor did not open instead of watching a background task finish
                    // and nothing appear.
                    failure = e;
                    MLog.Exception(e, @"ModMaker catalogue build failed");
                }
            }).ContinueWithOnUIThread(_ =>
            {
                BackgroundTaskEngine.SubmitJobCompletion(task);

                if (failure != null)
                {
                    M3L.ShowDialog(owner,
                        $@"ModMaker could not read the game: {failure.Message}",
                        @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                new ModMakerWindow(payload, catalogue, sections, target, target.Game,
                                  enemyPayload, difficulty, mapPayload, mapRoster,
                                  valuesPayload, scanned, perObject, tlk,
                                  objectivePayload, objectives,
                                  weaponPayload, weapons) { Owner = owner }.Show();
            });
        }

        /// <summary>Prefix of the shipped data files, as the assembly manifest names them.</summary>
        private const string DataResourcePrefix = @"ME3TweaksModManager.modmaker.data.";

        /// <summary>
        /// The shipped text-and-curation layer, or null when it cannot be read.
        ///
        /// Null rather than throwing: everything this layer supplies is words. Losing it costs the
        /// user their labels and their prose, which is bad, but it is not worth refusing to open an
        /// editor over - and the editors already render content the layer has never heard of.
        /// </summary>
        /// <summary>
        /// Where a mod puts the files ModMaker should read: `DLC_MOD_Whatever/ModMaker/`.
        ///
        /// A folder rather than a single file, because this is the first thing to go in it and it will
        /// not be the last.
        /// </summary>
        public const string DLCBundleFolder = @"ModMaker";

        /// <summary>The text bundle inside that folder. Named the same as the one ModMaker ships.</summary>
        public const string DLCBundleFile = @"textlayer.json";

        /// <summary>
        /// The shipped text layer with every installed mod's bundle folded in.
        ///
        /// This is what makes a mod's own content readable without ModMaker being updated for it: a
        /// mod that adds an enemy with a new gun ships the words for both, and they show up. Applied
        /// in mount order so the DLC that wins in the game wins here too.
        ///
        /// One bad bundle costs that bundle and nothing else. It is somebody else's file, arriving
        /// from an install this build has never seen, and refusing to open the editor over a mod's
        /// malformed JSON would be the wrong trade every time.
        /// </summary>
        private static TextLayer LoadTextLayer(GameTarget target)
        {
            var layer = LoadShippedTextLayer();
            if (layer == null || target == null) return layer;

            foreach (var (folder, path) in BundlePaths(target))
            {
                try
                {
                    var bundle = TextLayer.Load(path);
                    layer.Overlay(bundle);
                    MLog.Information($@"ModMaker: merged the text bundle from {folder}");
                }
                catch (Exception e)
                {
                    MLog.Error($@"ModMaker: the text bundle in {folder} could not be read and was skipped: {e.Message}");
                }
            }

            return layer;
        }

        /// <summary>
        /// Every installed DLC's bundle, in ascending mount order.
        ///
        /// The basegame is not scanned: the shipped layer already is the basegame's text, and a file
        /// dropped into the game's own folders is not something a mod does.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<(string Folder, string Path)> BundlePaths(GameTarget target)
        {
            string dlcRoot;
            try
            {
                dlcRoot = MEDirectories.GetDLCPath(target.Game, target.TargetPath);
            }
            catch (Exception e)
            {
                MLog.Warning($@"ModMaker: the DLC folder could not be read, so no text bundles were loaded: {e.Message}");
                yield break;
            }

            if (dlcRoot == null || !System.IO.Directory.Exists(dlcRoot)) yield break;

            foreach (var folder in ModMakerCatalogue.ScannableDLCFoldersInMountOrder(target))
            {
                var path = System.IO.Path.Combine(dlcRoot, folder, DLCBundleFolder, DLCBundleFile);
                if (System.IO.File.Exists(path)) yield return (folder, path);
            }
        }

        private static TextLayer LoadShippedTextLayer()
        {
            try
            {
                using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(DataResourcePrefix + @"textlayer.json");
                if (stream == null)
                {
                    MLog.Warning(@"ModMaker: the shipped text layer is not embedded; editors will show internal names.");
                    return null;
                }

                using var reader = new System.IO.StreamReader(stream);
                return TextLayer.Parse(reader.ReadToEnd(), @"textlayer.json");
            }
            catch (Exception e)
            {
                MLog.Error($@"ModMaker: the shipped text layer could not be read: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The target ModMaker should open against: LMP if it is installed, else nothing.
        /// Returned rather than guessed at, so the caller can disable the menu entry.
        /// </summary>
        public static GameTarget PreferredTarget(System.Collections.Generic.IEnumerable<GameTarget> targets) =>
            targets?.FirstOrDefault(t => t != null && ModMakerCatalogue.SupportsGame(t.Game) && t.Game == LegendaryExplorerCore.Packages.MEGame.LMP);
    }
}
