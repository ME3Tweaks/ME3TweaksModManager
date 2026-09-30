using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The maplist editor's data, shaped for the page.
    ///
    /// The other two editors compute game rules here - a wave's cost, a stat's absence. This one
    /// mostly RESOLVES: the config names a map by string ref and its banner by object path, and both
    /// answers are in the install rather than in the config. So the payload carries the resolved name
    /// and the decoded picture, and the page is spared knowing that either was ever a number.
    ///
    /// Everything the payload calls a fact about the roster is derived. Nothing counts to four, nothing
    /// indexes a fixed array, and no picture ships with ModMaker - which is what lets a new difficulty
    /// or a reskinned map arrive with its name and art already attached.
    /// </summary>
    public static class MaplistEditorPayload
    {
        /// <summary>
        /// The Armax Arena variants: `BioP_MPArmax<name>_Modifier_1/2/3`.
        ///
        /// 24 of the shipped available packages have this shape and no MasterMapList entry of their
        /// own, so listing them flat triples the list with rows that have no name and no art. They are
        /// grouped under the arena instead - which is also what they are in the game.
        /// </summary>
        private static readonly Regex VariantRx =
            new(@"^(?<base>.*?)_Modifier_(?<n>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static JObject Build(MapListCatalogue maps, GameStrings strings = null, BannerLibrary banners = null,
                                   GalaxyMap galaxy = null)
        {
            if (maps == null) throw new ArgumentNullException(nameof(maps));

            // The banner library's problems are NOT merged in here. Every one is already attached to
            // the entry it belongs to as `bannerProblem`, where it can be shown against the map it is
            // about - and the scanner reports the same unresolvable paths from the other side, so
            // merging both produced a banner of forty-six lines saying one thing twice.
            var warnings = new JArray(maps.Warnings);

            return new JObject
            {
                [@"format"] = 1,
                [@"section"] = maps.SettingsSection,
                [@"maps"] = Maps(maps, strings, banners, galaxy),
                // The Random choice, carried so the page can show the picker as the player sees it and
                // still refuse to edit it. Dropping it would leave a lobby option nobody can account
                // for; treating it as a map would offer an edit to something with no level behind it.
                [@"placeholders"] = new JArray(maps.Placeholders.Select(m => Map(m, maps, strings, banners, galaxy))),
                [@"variants"] = Variants(maps),
                [@"unlistedPackages"] = new JArray(Unlisted(maps)),
                [@"musicEvents"] = new JArray(MusicEvents(maps)),
                [@"galaxyBounds"] = GalaxyBounds(maps),
                [@"galaxy"] = Galaxy(galaxy),
                // Only the choices that name a faction. The lobby's list also carries "Unknown Enemy",
                // the surprise-me option, which names no wave class - so it is not a faction, it is
                // the absence of one, and there is nothing about it to show or edit. The catalogue
                // keeps it; a picker of things you cannot act on does not.
                [@"factions"] = new JArray(maps.EnemyTypes
                    .Where(o => !string.IsNullOrWhiteSpace(o.Field(@"WaveClass")))
                    .Select(o => Option(o, strings, banners))),
                [@"difficulties"] = new JArray(maps.ChallengeTypes.Select(o => Option(o, strings, banners))),
                // Stated rather than left for the page to work out, because it is the fact that makes
                // this editor make sense: being described and being playable are different things.
                [@"describedButUnavailable"] = new JArray(
                    maps.EntriesNotAvailable.Select(m => m.PackageName)),
                [@"stringsAvailable"] = strings?.Available ?? false,
                [@"warnings"] = warnings,
            };
        }

        public static string ToJson(MapListCatalogue maps, GameStrings strings = null, BannerLibrary banners = null,
                                    GalaxyMap galaxy = null, bool indented = true) =>
            Build(maps, strings, banners, galaxy).ToString(indented ? Formatting.Indented : Formatting.None);

        /// <summary>Every image path the pickers reference, so the caller can decode them in one pass.</summary>
        public static IEnumerable<string> ImagePaths(MapListCatalogue maps) =>
            maps?.AllImagePaths ?? Enumerable.Empty<string>();

        private static JArray Maps(MapListCatalogue maps, GameStrings strings, BannerLibrary banners, GalaxyMap galaxy) =>
            new(maps.Maps.Where(m => !m.IsPlaceholder)
                    .OrderBy(m => strings?.Resolve(m.PrettyName) ?? m.PackageName, StringComparer.OrdinalIgnoreCase)
                    .Select(m => Map(m, maps, strings, banners, galaxy)));

        private static JObject Map(MapEntry map, MapListCatalogue maps, GameStrings strings, BannerLibrary banners,
                                   GalaxyMap galaxy)
        {
            var entry = new JObject
            {
                [@"id"] = map.Id,
                [@"package"] = map.PackageName,
                [@"placeholder"] = map.IsPlaceholder,
                [@"available"] = map.IsAvailable,
                [@"music"] = map.MusicEventName,
                [@"raw"] = map.RawValue,
            };

            // The resolved text AND the ref it came from. The ref is what the config says and what a
            // person has to quote when a name looks wrong; the text is what they recognise.
            Name(entry, @"name", map.PrettyName, strings);
            Name(entry, @"description", map.Description, strings);
            // The line the lobby prints under the name. Sent even though the description is no longer
            // drawn: the description is still what the edit panel quotes when a string ref looks
            // wrong, and this is what the row shows in its place.
            Name(entry, @"location", map.LocationRef, strings);

            if (map.GalaxyAtWarX != null && map.GalaxyAtWarY != null)
            {
                entry[@"galaxy"] = new JObject
                {
                    [@"x"] = map.GalaxyAtWarX.Value,
                    [@"y"] = map.GalaxyAtWarY.Value,
                };

                // Which region of the galaxy the map sits in - answered by the zone art's own alpha,
                // not by a box. Absent rather than null when the position is in no region at all,
                // which is a real answer: much of the galaxy belongs to none, and the in-development
                // maps parked at (0, 0) are outside every one of them.
                if (galaxy?.ZoneAt(map.GalaxyAtWarX.Value, map.GalaxyAtWarY.Value) is { } zone)
                {
                    entry[@"zone"] = zone.Id;
                    entry[@"zoneName"] = zone.Name;
                }
            }

            Art(entry, map.Image, banners);

            // A map's own variants, so the row can say "and three modifier versions" rather than the
            // page having to re-derive the grouping.
            var variants = VariantsOf(maps, map.PackageName).ToList();
            if (variants.Count > 0) entry[@"variants"] = new JArray(variants);

            return entry;
        }

        /// <summary>
        /// One match-settings choice, for whichever page shows that picker.
        ///
        /// Internal rather than private because the faction picker is drawn by the wavelist editor
        /// now - a faction turned off takes its wavelist with it - and the two pages must not disagree
        /// about what a choice is. One shape, built once.
        /// </summary>
        internal static JObject Option(MatchSettingsOption option, GameStrings strings, BannerLibrary banners)
        {
            var entry = new JObject { [@"id"] = option.Id };

            // Factions and difficulties carry their names under field names of their own, and which
            // ones vary by picker - so every unmodelled field comes through and the first that
            // resolves to text is the name. Naming the fields here would break on the next picker.
            foreach (var (field, value) in option.Fields ?? new Dictionary<string, string>())
            {
                if (strings?.Resolve(value) is not { } text) continue;
                entry[$@"text_{field.ToLowerInvariant()}"] = text;
                if (entry[@"name"] == null) entry[@"name"] = text;
            }

            // The shipped text, for an exact-match Remove. A picker option is a struct with a dozen
            // fields and no key the config itself uses, so turning one off means subtracting the
            // bytes the install carries rather than something rebuilt to look like them.
            if (option.RawValue is { Length: > 0 } raw) entry[@"raw"] = raw;

            // The faction picker maps a choice to a wave class, which is the second gate on whether a
            // faction is reachable at all - the wave manager's roster being the first.
            //
            // The "any" choice carries the field with an EMPTY value rather than omitting it, so the
            // two are collapsed here: "no particular faction" is one state, and leaving an empty
            // string in the payload makes the page test for both spellings of it.
            if (option.Field(@"WaveClass") is { Length: > 0 } waveClass) entry[@"waveClass"] = waveClass;

            Art(entry, option.Image, banners);
            return entry;
        }

        private static void Name(JObject entry, string key, string strRef, GameStrings strings)
        {
            if (strings?.Resolve(strRef) is { } text) entry[key] = text;
            if (!string.IsNullOrWhiteSpace(strRef)) entry[key + @"Ref"] = strRef;
        }

        private static void Art(JObject entry, string path, BannerLibrary banners)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            entry[@"image"] = path;

            var banner = banners?.For(path);
            if (banner?.DataUri is { } uri) entry[@"banner"] = uri;
            // A path that could not be resolved is a REASON, not an absence. "This map has no banner"
            // and "this map's banner was written in a form nothing can look up" want different words,
            // and the second is the state Milan's in-development map pack is currently in.
            else if (banner?.Problem is { } problem) entry[@"bannerProblem"] = problem;
        }

        /// <summary>Base package name to its modifier variants, for the packages that have any.</summary>
        private static JObject Variants(MapListCatalogue maps)
        {
            var result = new JObject();
            foreach (var group in maps.AvailableMaps.Select(a => (Package: a, Match: VariantRx.Match(a)))
                                      .Where(x => x.Match.Success)
                                      .GroupBy(x => x.Match.Groups[@"base"].Value, StringComparer.OrdinalIgnoreCase))
            {
                result[group.Key] = new JArray(group.Select(x => x.Package).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            }
            return result;
        }

        private static IEnumerable<string> VariantsOf(MapListCatalogue maps, string package) =>
            package == null
                ? []
                : maps.AvailableMaps.Where(a =>
                    VariantRx.Match(a) is { Success: true } m
                    && m.Groups[@"base"].Value.Equals(package, StringComparison.OrdinalIgnoreCase))
                  .OrderBy(a => a, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Playable packages with neither a MasterMapList entry nor a base map that claims them as a
        /// variant. Real content with no name and no art, and worth showing as exactly that.
        /// </summary>
        private static IEnumerable<string> Unlisted(MapListCatalogue maps)
        {
            var claimed = maps.Maps.SelectMany(m => VariantsOf(maps, m.PackageName))
                               .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return maps.AvailableWithoutEntry.Where(p => !claimed.Contains(p))
                       .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The Wwise event vocabulary, taken from the shipped values.
        ///
        /// Closed on purpose: an event name the game does not have is a map that loads in silence, and
        /// nothing in the config says which names are legal. The shipped set is the only list anyone
        /// has, and several maps share an entry in it.
        /// </summary>
        private static IEnumerable<string> MusicEvents(MapListCatalogue maps) =>
            maps.Maps.Select(m => m.MusicEventName)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The picture the positions are positions ON, and the regions drawn over it.
        ///
        /// Every measurement here comes from the movie that draws the lobby map rather than from the
        /// shipped values (see <see cref="GalaxyMap"/>), which is what lets the page place a map where
        /// the game places it instead of somewhere proportionally similar.
        /// </summary>
        private static JToken Galaxy(GalaxyMap galaxy)
        {
            if (galaxy is not { Available: true }) return JValue.CreateNull();

            return new JObject
            {
                [@"image"] = galaxy.Background.DataUri,
                // The space is WIDER than it is tall while the art is square: the game stretches it
                // 1.2:1, and a page that preserves the aspect ratio misplaces every map horizontally.
                [@"width"] = GalaxyMap.SpaceWidth,
                [@"height"] = GalaxyMap.SpaceHeight,
                // The page decides regions for itself, because a map that has been moved is no longer
                // in the region it shipped in. Sending the threshold rather than letting the page pick
                // one keeps the two from drawing the boundaries in different places.
                [@"alphaThreshold"] = GalaxyMap.AlphaThreshold,
                [@"zones"] = new JArray(galaxy.Zones.Select(z => new JObject
                {
                    [@"id"] = z.Id,
                    [@"name"] = z.Name,
                    [@"image"] = z.Art?.DataUri,
                    [@"x0"] = z.X0,
                    [@"y0"] = z.Y0,
                    [@"x1"] = z.X1,
                    [@"y1"] = z.Y1,
                })),
            };
        }

        /// <summary>
        /// The extent of the Galaxy at War coordinate space, measured from the shipped positions.
        ///
        /// Kept now that the real space is known, but for a different job: it is no longer what the
        /// page scales by - it reports where the shipped maps actually are, so a position outside the
        /// drawable space shows up as such instead of being quietly clipped to an edge.
        /// </summary>
        private static JToken GalaxyBounds(MapListCatalogue maps)
        {
            var placed = maps.Maps.Where(m => m.GalaxyAtWarX != null && m.GalaxyAtWarY != null).ToList();
            if (placed.Count == 0) return JValue.CreateNull();

            return new JObject
            {
                [@"minX"] = placed.Min(m => m.GalaxyAtWarX.Value),
                [@"maxX"] = placed.Max(m => m.GalaxyAtWarX.Value),
                [@"minY"] = placed.Min(m => m.GalaxyAtWarY.Value),
                [@"maxY"] = placed.Max(m => m.GalaxyAtWarY.Value),
                [@"count"] = placed.Count,
            };
        }
    }
}
