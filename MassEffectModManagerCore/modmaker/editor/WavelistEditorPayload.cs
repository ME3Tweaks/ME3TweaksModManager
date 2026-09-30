using System;
using System.Collections.Generic;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The wavelist editor's data, shaped for the page rather than for the game.
    ///
    /// This is the boundary between the generator and the web app. It exists as its own step because
    /// the two want different shapes: the catalogue is organised the way the config is - factions
    /// holding instances holding rows - while the editor is a grid of waves against difficulties, with
    /// a cost and a budget on every cell. Computing that in the page would put game rules in
    /// JavaScript, where they cannot be tested against a real install.
    ///
    /// Everything here is derived. Nothing is authored, and nothing is looked up by a name this class
    /// knows: the difficulties are whatever the game's budget table defines, and the factions are
    /// whatever descends from the wave base class.
    /// </summary>
    public static class WavelistEditorPayload
    {
        /// <summary>
        /// Builds the payload.
        /// </summary>
        /// <param name="maps">
        /// Optional. Supplies the challenge banners, which is where a difficulty's own art lives.
        /// </param>
        /// <param name="banners">
        /// Optional, and needed for that art to be usable: the catalogue holds the object PATH of a
        /// texture, which a page cannot render. Without this the difficulty columns fall back to
        /// their names, which is what they did before the art was wired up at all.
        /// </param>
        /// <param name="text">
        /// Optional. Supplies what a wavelist is CALLED and which enemy prefix belongs to it - two
        /// things the game states nowhere. Shipped for the base roster and by a mod for its own, from
        /// that mod's `ModMaker` folder.
        /// </param>
        /// <param name="strings">
        /// Optional. Resolves the lobby's own name for a faction choice, which is a string reference.
        /// </param>
        public static JObject Build(WavelistCatalogue waves, MapListCatalogue maps = null,
                                    BannerLibrary banners = null, TextLayer text = null,
                                    GameStrings strings = null)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            var difficulties = Difficulties(waves, maps, banners);

            return new JObject
            {
                [@"format"] = 1,
                [@"difficulties"] = difficulties,
                [@"palette"] = Palette(waves),
                [@"factions"] = Factions(waves, difficulties, text),
                // The lobby's enemy picker, because a faction is switched on and off from this page
                // now: dropping one from that list takes every wavelist under it out of the game, so
                // the toggle belongs beside the waves it silences rather than beside the maps.
                [@"lobbyFactions"] = LobbyFactions(maps, strings, banners),
                // Enemy-type prefix to faction name, for grouping the type picker. Sent as its own
                // map rather than left for the page to invert, because a prefix is not necessarily
                // owned by a faction this list contains - a mod can name one whose wavelist is not
                // installed, and the picker still has to label it.
                [@"factionNames"] = FactionNames(text),
                [@"waveRange"] = new JObject
                {
                    [@"first"] = waves.FirstWave,
                    [@"last"] = waves.LastWave,
                },
                // Waves the budget does not apply to. The extraction wave has none, and an objective
                // wave is unbudgeted until its objective completes, so a cost bar on either would be
                // showing a number the game will not use.
                [@"objectiveWaves"] = new JArray(waves.ObjectiveWaves),
                // What an assassination needs from an objective wave, so the composer can say so
                // while the wave is being built rather than only when it is compiled. Null when the
                // rules could not be read, and the page then says nothing rather than guessing.
                [@"assassination"] = waves.Assassination is { Usable: true } rule
                    ? new JObject
                    {
                        [@"window"] = rule.TargetWindow,
                        [@"threshold"] = rule.MinTargetCost,
                    }
                    : null,
                // The size of the field, which is what makes a cost computable at all: the fixed
                // slots hold their counts on it and the filler makes up the rest, so the opening
                // spawn is one full field rather than the fixed slots alone. Null when the game did
                // not say, and the page then declines to cost a wave rather than guessing.
                [@"maxEnemies"] = waves.MaxEnemies,
                [@"extractionWave"] = waves.LastWave,
                [@"warnings"] = new JArray(waves.Warnings.Concat(UndeclaredFactions(waves, text))),
            };
        }

        public static string ToJson(WavelistCatalogue waves, MapListCatalogue maps = null,
                                    BannerLibrary banners = null, TextLayer text = null,
                                    GameStrings strings = null, bool indented = true) =>
            Build(waves, maps, banners, text, strings).ToString(indented ? Formatting.Indented : Formatting.None);

        /// <summary>
        /// The lobby's enemy picker: one entry per faction the player can choose to fight.
        ///
        /// Joined to a wavelist by WAVE CLASS and not by name. The picker's name is the lobby's -
        /// "Cerberus" for the choice whose class is SFXWave_Horde_Cerberus2 - and only the class says
        /// which wavelist a choice actually starts.
        ///
        /// Only the choices that name a class. The list also carries the surprise-me option, which
        /// names none: that is the ABSENCE of a faction rather than one that could be turned off, and
        /// there is nothing about it to show or edit.
        /// </summary>
        private static JArray LobbyFactions(MapListCatalogue maps, GameStrings strings, BannerLibrary banners) =>
            maps == null
                ? new JArray()
                : new JArray(maps.EnemyTypes
                    .Where(o => !string.IsNullOrWhiteSpace(o.Field(@"WaveClass")))
                    .Select(o => MaplistEditorPayload.Option(o, strings, banners)));

        /// <summary>
        /// One warning per enemy tag nobody has claimed.
        ///
        /// The picker groups by faction and nothing can work one out: an enemy's archetype names the
        /// faction it was BUILT from, not the one it fights for, and Rejects - assembled from units
        /// drawn out of several factions - is the case that proves guessing wrong. So an undeclared
        /// tag is reported rather than papered over, or a mod ships and nobody finds out its faction
        /// has no name until somebody opens the picker and sees a raw tag.
        /// </summary>
        private static IEnumerable<string> UndeclaredFactions(WavelistCatalogue waves, TextLayer text)
        {
            var known = new HashSet<string>(
                (text?.Factions?.Values ?? Enumerable.Empty<FactionText>())
                    .Select(f => f?.EnemyPrefix?.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.ToUpperInvariant()),
                StringComparer.Ordinal);

            // One per tag, naming a type that carries it, because "WAVE_REJ_ is undeclared" is easier
            // to act on when it comes with something to go and look at.
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var enemy in waves.Enemies.Where(e => !e.IsPet))
            {
                var tag = FactionTag(enemy.EnemyType);
                if (tag == null || known.Contains(tag) || seen.ContainsKey(tag)) continue;
                seen[tag] = enemy.EnemyType;
            }

            return seen.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
                $@"Enemy types tagged WAVE_{p.Key}_ (for example {p.Value}) belong to no named " +
                $@"faction. Add a ""factions"" entry with ""enemyPrefix"": ""{p.Key}"" to the mod's " +
                @"ModMaker	extlayer.json, or the enemy picker files them under the raw tag.");
        }

        /// <summary>The faction tag in an enemy type name - REJ of WAVE_REJ_Ravager - or null.</summary>
        private static string FactionTag(string enemyType)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                enemyType ?? string.Empty, @"^WAVE_([A-Za-z0-9]{2,4})_");
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        /// <summary>
        /// Enemy-type prefix to faction name, from every wavelist the text layer describes.
        ///
        /// `WAVE_REJ_Ravager` is a Reject and `WAVE_RPR_Ravager` is a Reaper, and the only thing that
        /// says so is somebody writing it down. Empty is fine: the page falls back to the archetype
        /// path and then to the raw tag.
        /// </summary>
        private static JObject FactionNames(TextLayer text)
        {
            var result = new JObject();
            foreach (var entry in text?.Factions ?? new Dictionary<string, FactionText>())
            {
                var prefix = entry.Value?.EnemyPrefix;
                var title = entry.Value?.Title;
                if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(title)) continue;
                // Upper case, because that is how the game writes it inside the type name and the
                // page matches on what it reads there rather than on what was typed here.
                result[prefix.Trim().ToUpperInvariant()] = title;
            }
            return result;
        }

        /// <summary>
        /// The difficulty columns, in the order the game's budget table defines them - never a fixed
        /// four. The singleplayer handler ships five inside the same game, and Milan may add one.
        /// </summary>
        private static JArray Difficulties(WavelistCatalogue waves, MapListCatalogue maps, BannerLibrary banners)
        {
            var result = new JArray();
            for (int i = 0; i < waves.Budget.Count; i++)
            {
                var range = waves.Budget[i];
                var entry = new JObject
                {
                    [@"key"] = range.Difficulty,
                    [@"label"] = range.Difficulty,
                    [@"firstWavePoints"] = range.FirstWavePoints,
                    [@"lastWavePoints"] = range.LastWavePoints,
                };

                // The challenge banners are positional against the difficulty list: banner ID n goes
                // with the nth difficulty. That is a convention declared nowhere in the game, so it is
                // applied by index and only where there is a banner to apply.
                var banner = maps?.ChallengeTypes.FirstOrDefault(c => c.Id == i);
                if (banner?.Image != null)
                {
                    // The path is kept whether or not it decoded. It is what a person has to quote
                    // when a column shows the wrong art, and it is the only evidence that the
                    // positional convention above matched anything at all.
                    entry[@"imagePath"] = banner.Image;

                    var art = banners?[banner.Image];
                    if (art?.DataUri != null)
                    {
                        entry[@"image"] = art.DataUri;
                        entry[@"imageWidth"] = art.Width;
                        entry[@"imageHeight"] = art.Height;
                    }
                }

                result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// The closed, priced palette every wave draws from. Every enemy type any wavelist uses has a
        /// price, which is what lets a wave be costed live while it is composed.
        /// </summary>
        private static JArray Palette(WavelistCatalogue waves)
        {
            var result = new JArray();
            foreach (var enemy in waves.Enemies.Where(e => !e.IsPet).OrderBy(e => e.WaveCost))
            {
                result.Add(new JObject
                {
                    [@"type"] = enemy.EnemyType,
                    [@"cost"] = enemy.WaveCost,
                    [@"archetype"] = enemy.ArchetypeName,
                });
            }
            return result;
        }

        private static JArray Factions(WavelistCatalogue waves, JArray difficulties, TextLayer text)
        {
            var keys = difficulties.Select(d => (string)d[@"key"]).ToList();
            var result = new JArray();

            foreach (var faction in waves.Factions)
            {
                var wavesJson = new JArray();
                foreach (var wave in faction.Waves.OrderBy(w => w.WaveIndex ?? int.MaxValue))
                {
                    var cells = new JObject();
                    foreach (var key in keys)
                    {
                        // A wave has one row per difficulty in well-formed data. More than one is
                        // reported rather than collapsed, and the cell carries all of them so the user
                        // sees what is actually there.
                        var rows = wave.Rows.Where(r => string.Equals(r.Difficulty, key, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (rows.Count == 0) continue;
                        cells[key] = new JArray(rows.Select(r => Cell(r, waves, wave.WaveIndex, key)));
                    }

                    wavesJson.Add(new JObject
                    {
                        [@"index"] = wave.WaveIndex,
                        [@"instanceId"] = wave.InstanceId,
                        [@"cells"] = cells,
                    });
                }

                var described = text?.Factions != null
                                && text.Factions.TryGetValue(faction.Section, out var meta) ? meta : null;

                result.Add(new JObject
                {
                    [@"section"] = faction.Section,
                    [@"name"] = faction.Name,
                    // What to call it. Null where nobody has said, and the page then falls back to
                    // tidying up the class name - which gives "Cerberus2", readable but not a name.
                    [@"title"] = string.IsNullOrWhiteSpace(described?.Title) ? null : described.Title,
                    [@"enemyPrefix"] = string.IsNullOrWhiteSpace(described?.EnemyPrefix)
                        ? null : described.EnemyPrefix.Trim().ToUpperInvariant(),
                    // Dormant factions keep their wavelists and are turned back on by putting them
                    // into WaveTypes. Editing one is real and correct; it simply will not be reached
                    // until the roster says so, and the editor has to say that rather than hide it.
                    [@"selectable"] = faction.IsSelectable,
                    [@"selectionWeight"] = faction.SelectionWeight,
                    [@"waves"] = wavesJson,
                });
            }

            return result;
        }

        private static JObject Cell(WaveDifficultyRow row, WavelistCatalogue waves, int? waveIndex, string difficulty)
        {
            var slots = new JArray();
            int cost = 0;
            bool priced = true;

            foreach (var slot in row.Slots)
            {
                var unit = waves.CostOf(slot.EnemyType);
                if (unit == null) priced = false;

                // The filler spends whatever budget is left rather than a fixed amount, so it does not
                // contribute a countable cost. Charging it once would understate a wave that leans on
                // it and overstate one that does not.
                if (!slot.IsFiller && unit != null) cost += unit.Value * (slot.MaxCount ?? 1);

                slots.Add(new JObject
                {
                    [@"index"] = slot.Index,
                    [@"type"] = slot.EnemyType,
                    [@"filler"] = slot.IsFiller,
                    [@"min"] = slot.MinCount,
                    [@"max"] = slot.MaxCount,
                    [@"maxPerWave"] = slot.MaxPerWave,
                    [@"unitCost"] = unit,
                });
            }

            var cell = new JObject
            {
                [@"slots"] = slots,
                [@"fixedCost"] = cost,
                // False when some slot's type has no price, so the UI shows "unknown" rather than a
                // total that quietly omits an enemy.
                [@"costComplete"] = priced,
                [@"raw"] = row.RawValue,
            };

            if (waveIndex != null)
            {
                var budget = waves.BudgetFor(difficulty, waveIndex.Value);
                if (budget != null) cell[@"budget"] = budget.Value;

                // Stated rather than computed in the page: these are game rules.
                cell[@"budgetApplies"] = waveIndex.Value != waves.LastWave
                                         && !waves.ObjectiveWaves.Contains(waveIndex.Value);
            }

            return cell;
        }
    }
}
