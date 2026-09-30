using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The enemy editor's data, shaped for the page.
    ///
    /// The same boundary <see cref="WavelistEditorPayload"/> draws, for the same reason, but with one
    /// difference that shapes the whole class: the wavelist editor's text comes from the game, and
    /// this one's cannot. `MaxEnemyShieldRecharge` and `PowerThreshold_Knockback` are internal names
    /// with no TLK string, no label and no unit anywhere in the install, so every readable word here
    /// comes from the shipped text layer and is joined on by stat name.
    ///
    /// The join is one-directional on purpose. A category the layer has never heard of still gets a
    /// page - with its raw stat names in one ungrouped block - because an authored list cannot
    /// anticipate a mod's new enemy, and an editor that hides what it has no words for is worse than
    /// one that shows an internal name.
    /// </summary>
    public static class EnemyEditorPayload
    {
        /// <summary>
        /// The class the multiplayer difficulty tables hang off. Handlers are FOUND by their property
        /// shape rather than by this name - <see cref="DifficultyCatalogue"/> picks up the
        /// singleplayer one and anything a mod adds - so this is only used to decide which of the
        /// tables found belongs in this editor.
        /// </summary>
        public const string MPHandlerClass = @"SFXDifficultyHandlerMP";

        /// <summary>
        /// Builds the payload for one difficulty handler.
        ///
        /// <paramref name="maps"/> is optional; when the map scan has run, each level column gains the
        /// game's own challenge banner. <paramref name="text"/> is optional in the same way: without
        /// it the editor works and shows internal stat names.
        /// </summary>
        public static JObject Build(DifficultyTable table, EnemyStatsText text = null, MapListCatalogue maps = null,
                                    IEnumerable<string> warnings = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            return new JObject
            {
                [@"format"] = 1,
                [@"handler"] = new JObject
                {
                    [@"section"] = table.Section,
                    [@"class"] = table.ClassName,
                },
                [@"levels"] = Levels(table, text, maps),
                [@"categories"] = Categories(table, text),
                [@"warnings"] = new JArray(warnings ?? Array.Empty<string>()),
            };
        }

        public static string ToJson(DifficultyTable table, EnemyStatsText text = null, MapListCatalogue maps = null,
                                    IEnumerable<string> warnings = null, bool indented = true) =>
            Build(table, text, maps, warnings).ToString(indented ? Formatting.Indented : Formatting.None);

        /// <summary>
        /// The multiplayer handler out of everything the difficulty scan found, or null.
        ///
        /// Selected by class rather than by section name, and by DESCENT rather than by identity, so a
        /// mod subclassing the handler is picked up and the singleplayer table - a different game mode
        /// with two thousand more values and five levels - stays out.
        /// </summary>
        public static DifficultyTable MultiplayerTable(DifficultyCatalogue difficulty,
                                                       IReadOnlyDictionary<string, CatalogueClass> classes,
                                                       string handlerClass = MPHandlerClass)
        {
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));

            var anchor = classes == null ? null : ClassScanner.FindByName(handlerClass, classes);
            if (anchor == null)
            {
                // Without the class there is nothing to descend from. Falling back to the bare name
                // keeps a stock install working; what must not happen is silently returning the
                // singleplayer table, which is the same shape and would look entirely plausible.
                return difficulty.Tables.FirstOrDefault(t =>
                    handlerClass.Equals(t.ClassName, StringComparison.OrdinalIgnoreCase));
            }

            var family = new HashSet<string>(
                ClassScanner.Descendants(anchor.Section, classes).Select(c => c.Section),
                StringComparer.OrdinalIgnoreCase) { anchor.Section };

            return difficulty.Tables.FirstOrDefault(t => family.Contains(t.Section));
        }

        /// <summary>
        /// The difficulty columns, enumerated from the handler. Never a fixed four: the singleplayer
        /// handler ships five inside this same game, and Milan may add one.
        /// </summary>
        private static JArray Levels(DifficultyTable table, EnemyStatsText text, MapListCatalogue maps)
        {
            var result = new JArray();
            for (int i = 0; i < table.Levels.Count; i++)
            {
                int level = table.Levels[i];
                var entry = new JObject
                {
                    [@"level"] = level,
                    // The authored name where there is one, and the level number where there is not.
                    // A level nobody has named still gets a column rather than being dropped.
                    [@"label"] = text?.Level(level)?.Label ?? $@"Level {level}",
                };

                // Challenge banners are positional against the difficulty list - banner ID n goes with
                // the nth level - which is a convention the game declares nowhere. Applied by index,
                // and only where there is a banner to apply.
                var banner = maps?.ChallengeTypes.FirstOrDefault(c => c.Id == i);
                if (banner?.Image != null) entry[@"image"] = banner.Image;

                result.Add(entry);
            }
            return result;
        }

        private static JArray Categories(DifficultyTable table, EnemyStatsText text)
        {
            var result = new JArray();

            foreach (var category in table.Categories.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                var authored = text?.Category(category.Name);
                var entry = new JObject
                {
                    [@"name"] = category.Name,
                    [@"title"] = authored?.Title ?? category.Name,
                };

                if (!string.IsNullOrWhiteSpace(authored?.Intro)) entry[@"intro"] = authored.Intro;
                if (!string.IsNullOrWhiteSpace(authored?.Image)) entry[@"image"] = authored.Image;

                entry[@"groups"] = Groups(table, category, text, authored);
                result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// The authored groups, in authored order, followed by everything the game ships that no group
        /// claims.
        ///
        /// That trailing group is not an error path. A mod's new enemy has no authored text at all and
        /// arrives here whole, which is the documented outcome; so does a stat Milan adds to an enemy
        /// somebody wrote a page for years ago.
        /// </summary>
        private static JArray Groups(DifficultyTable table, DifficultyCategory category,
                                     EnemyStatsText text, EnemyCategoryText authored)
        {
            var shipped = category.StatNames.ToList();
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new JArray();

            foreach (var group in authored?.Groups ?? new List<EnemyStatGroup>())
            {
                var stats = new JArray();
                foreach (var name in group.Stats ?? new List<string>())
                {
                    // Matched against what the install ships, not taken on the layer's word. Text
                    // written for ME3 names stats Milan does not have, and rendering a row for one
                    // would offer an edit with nowhere to go.
                    var actual = shipped.FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (actual == null || !placed.Add(actual)) continue;
                    stats.Add(Stat(table, category, actual, text, authored));
                }

                if (stats.Count == 0) continue;

                var g = new JObject { [@"title"] = group.Title ?? string.Empty };
                if (!string.IsNullOrWhiteSpace(group.Description)) g[@"description"] = group.Description;
                g[@"stats"] = stats;
                result.Add(g);
            }

            var rest = new JArray();
            foreach (var name in shipped.Where(s => !placed.Contains(s))
                                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                rest.Add(Stat(table, category, name, text, authored));

            if (rest.Count > 0)
            {
                result.Add(new JObject
                {
                    [@"title"] = result.Count == 0 ? @"Settings" : @"Other settings",
                    // Flagged rather than described, so the page can mark it without the payload
                    // having to guess whether this is a mod's enemy or a gap in the authoring.
                    [@"ungrouped"] = true,
                    [@"stats"] = rest,
                });
            }

            return result;
        }

        private static JObject Stat(DifficultyTable table, DifficultyCategory category, string statName,
                                    EnemyStatsText text, EnemyCategoryText authored)
        {
            var stat = new JObject { [@"name"] = statName };

            // Category text beats the shared entry: 33 stat names carry more than one label across the
            // legacy pages, and the variation is meaningful - an armoured unit's health is Armor, a
            // biotic's shields are Barriers, a drone's zap is not a melee attack.
            var label = authored?.StatText(statName)?.Label ?? text?.Stat(statName)?.Label;
            var unit = authored?.StatText(statName)?.Unit ?? text?.Stat(statName)?.Unit;
            var description = authored?.StatText(statName)?.Description ?? text?.Stat(statName)?.Description;

            stat[@"label"] = label ?? statName;
            if (!string.IsNullOrWhiteSpace(unit)) stat[@"unit"] = unit;
            if (!string.IsNullOrWhiteSpace(description)) stat[@"description"] = description;

            // Stated, so the page can say "this one has no name yet" instead of quietly showing an
            // internal identifier as though it were a label.
            if (label == null) stat[@"unlabelled"] = true;

            var values = new JObject();
            foreach (int level in table.Levels)
            {
                var row = category.Levels.FirstOrDefault(l => l.Level == level);
                var shipped = row?.Stats.FirstOrDefault(s => s.StatName.Equals(statName, StringComparison.OrdinalIgnoreCase));

                // A missing (category, stat, level) is genuinely absent, not a hole to fill. Thirteen
                // exist in LMP and they are all KaiLeng; writing a value BioWare left out is an
                // untested change, and one the user did not ask for.
                if (shipped == null) continue;

                values[level.ToString(CultureInfo.InvariantCulture)] = new JObject
                {
                    // X and Y, never min and max: either may be the larger, and almost half the
                    // interesting stats run downward.
                    [@"x"] = shipped.X,
                    [@"y"] = shipped.Y,
                };
            }

            stat[@"values"] = values;
            return stat;
        }
    }
}
