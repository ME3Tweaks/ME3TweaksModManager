using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// An option in one of the match-settings pickers - a map, a faction or a difficulty.
    ///
    /// All three carry an Image, and it is the same kind of asset in each case, which is why they are
    /// one type: a 512x128 banner the editor can show instead of a stale embedded copy.
    /// </summary>
    public class MatchSettingsOption
    {
        public int? Id { get; init; }

        /// <summary>Instanced full path to a Texture2D export, e.g. GUI_MPImages.MatchSettings.OptMapDish.</summary>
        public string Image { get; init; }

        /// <summary>Package half of <see cref="Image"/>, or null when there is no image.</summary>
        public string ImagePackage =>
            string.IsNullOrEmpty(Image) || !Image.Contains('.') ? null : Image[..Image.IndexOf('.')];

        /// <summary>Fields this type does not model, kept rather than discarded.</summary>
        public Dictionary<string, string> Fields { get; init; }

        /// <summary>The entry as shipped, for exact-match Remove emission.</summary>
        public string RawValue { get; init; }

        public string Field(string name) => Fields.GetValueOrDefault(name);
    }

    /// <summary>One entry of MasterMapList.</summary>
    public class MapEntry : MatchSettingsOption
    {
        /// <summary>The package the map lives in; the identity AvailableMaps and MPMapNames use.</summary>
        public string PackageName { get; init; }

        public string PrettyName { get; init; }
        public string Description { get; init; }
        public string MusicEventName { get; init; }

        /// <summary>
        /// The place the map is, as a string ref: "Benning", "Palaven".
        ///
        /// The game calls it `GalaxyAtWarMapSubtitle` because the Galaxy at War screen is where it
        /// labels a pin with it - but it is the line the lobby shows under the map's name, which is
        /// the only place most players ever read it. Named for what it says rather than for the one
        /// screen it was named after.
        /// </summary>
        public string LocationRef { get; init; }
        public double? GalaxyAtWarX { get; init; }
        public double? GalaxyAtWarY { get; init; }

        /// <summary>
        /// An entry with no package behind it: a picker option rather than a map.
        ///
        /// MasterMapList entry 0 is the "Random" choice - it has a banner and a name string but no
        /// PackageName, because it resolves to a real map at match start. It must not be offered as an
        /// editable map, and it must not be counted as a map that cannot be played.
        /// </summary>
        public bool IsPlaceholder => string.IsNullOrWhiteSpace(PackageName);

        /// <summary>True when AvailableMaps lists this map's package - i.e. it can actually be picked.</summary>
        public bool IsAvailable { get; internal set; }

        /// <summary>True when the separate, shorter MPMapNames list names it.</summary>
        public bool InMPMapNames { get; internal set; }
    }

    /// <summary>
    /// The multiplayer map roster and the match-settings options beside it.
    ///
    /// Three lists govern maps and they do different jobs, which has to be right before an editor is
    /// built over them:
    ///
    ///   MasterMapList  every map the game knows, with its name, description, selection image,
    ///                  Galaxy at War position and music
    ///   AvailableMaps  which package names can actually be picked - a plain list of strings
    ///   MPMapNames     a separate, shorter list on SFXPlayerControllerMP, written with INDEXED
    ///                  assignment (mpmapnames[0]=...) rather than the Add/Remove list form, and
    ///                  stale relative to AvailableMaps in shipped data
    ///
    /// Being in MasterMapList does not make a map selectable and being selectable does not require a
    /// MasterMapList entry; the two disagree in both directions in the shipped game. An editor that
    /// treats either list as "the maps" gets it wrong.
    /// </summary>
    public class MapListCatalogue
    {
        public const string SettingsClass = @"SFXOnlineGameSettings";

        /// <summary>
        /// The coalesced section the match settings were actually read from, or null.
        ///
        /// Resolved from the class scan rather than spelled out, because the section is
        /// "package.class" and the package is not something to assume - a mod subclassing the
        /// settings puts it somewhere else entirely. Anything writing these lists back needs the same
        /// answer the reading used, so it is recorded rather than re-derived.
        ///
        /// Settable for the same reason <see cref="SectionIndex"/> has a public constructor: the
        /// compiler's rules about WHERE a map edit is written need exercising without loading a game,
        /// and a catalogue built by hand has to be able to say what section it stands for.
        /// </summary>
        public string SettingsSection { get; set; }
        public const string PlayerControllerClass = @"SFXPlayerControllerMP";

        private static readonly Regex IndexedMapNameRx =
            new(@"^mpmapnames(?:\[(?<i>\d+)\])?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<MapEntry> Maps { get; } = new();

        /// <summary>Package names that can be picked. Not all of them have a MasterMapList entry.</summary>
        public List<string> AvailableMaps { get; } = new();

        /// <summary>The separate short list, in index order.</summary>
        public List<string> MPMapNames { get; } = new();

        /// <summary>The lobby faction picker. Each entry maps a choice to a WaveClass.</summary>
        public List<MatchSettingsOption> EnemyTypes { get; } = new();

        /// <summary>Bronze / Silver / Gold / Platinum, each with a banner of its own.</summary>
        public List<MatchSettingsOption> ChallengeTypes { get; } = new();

        public List<string> Warnings { get; } = new();
        public List<string> MalformedValues { get; } = new();

        /// <summary>
        /// Image paths that name a texture without naming the package it lives in, e.g.
        /// "OptMapArmaxBlasteroid" rather than "GUI_MPImages.MatchSettings.OptMapDish".
        ///
        /// These cannot be resolved. The editor extracts banners from the game's own packages by
        /// instanced full path, and a bare object name does not say which package to open - finding it
        /// would mean searching every package in the install for a matching Texture2D, which can match
        /// more than one and would be guessing at which the game means.
        ///
        /// So they are collected rather than silently dropped. An entry listed here shows up in the
        /// editor with no art and a reason, which is the difference between "this map has no banner"
        /// and "this map's banner was written in a form nothing can look up".
        /// </summary>
        public List<string> UnqualifiedImages { get; } = new();

        /// <summary>Selectable packages with no MasterMapList entry - no name, no image, still playable.</summary>
        public List<string> AvailableWithoutEntry =>
            AvailableMaps.Where(a => !Maps.Any(m => string.Equals(m.PackageName, a, StringComparison.OrdinalIgnoreCase)))
                         .ToList();

        /// <summary>Real maps that are described but cannot be picked. Excludes picker placeholders.</summary>
        public List<MapEntry> EntriesNotAvailable => Maps.Where(m => !m.IsPlaceholder && !m.IsAvailable).ToList();

        /// <summary>Picker options with no map behind them - the "Random" choice.</summary>
        public List<MapEntry> Placeholders => Maps.Where(m => m.IsPlaceholder).ToList();

        /// <summary>
        /// Every image path referenced by any picker. The editor extracts these from the game's own
        /// packages in memory, so the art never goes stale against the installed game.
        /// </summary>
        public IEnumerable<string> AllImagePaths =>
            Maps.Cast<MatchSettingsOption>().Concat(EnemyTypes).Concat(ChallengeTypes)
                .Select(o => o.Image).Where(i => !string.IsNullOrEmpty(i)).Distinct(StringComparer.OrdinalIgnoreCase);

        public static MapListCatalogue Build(MergedGameConfig config, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (classes == null) throw new ArgumentNullException(nameof(classes));

            var result = new MapListCatalogue();
            var index = config.BuildSectionIndex();

            var settings = ClassScanner.FindByName(SettingsClass, classes);
            if (settings == null)
            {
                result.Warnings.Add($@"{SettingsClass} was not found in the installed game; the map list could not be read.");
                return result;
            }

            result.SettingsSection = settings.Section;

            foreach (var v in index.Values(settings.Section, @"MasterMapList"))
                result.ReadMap(v.Value, settings.Section);

            foreach (var v in index.Values(settings.Section, @"AvailableMaps"))
            {
                if (!string.IsNullOrWhiteSpace(v.Value)) result.AvailableMaps.Add(v.Value.Trim());
            }

            foreach (var v in index.Values(settings.Section, @"EnemyTypes"))
                result.ReadOption(result.EnemyTypes, v.Value, settings.Section, @"EnemyTypes");

            foreach (var v in index.Values(settings.Section, @"ChallengeTypes"))
                result.ReadOption(result.ChallengeTypes, v.Value, settings.Section, @"ChallengeTypes");

            result.ReadMapNames(index, classes);

            foreach (var map in result.Maps)
            {
                map.IsAvailable = result.AvailableMaps.Contains(map.PackageName, StringComparer.OrdinalIgnoreCase);
                map.InMPMapNames = result.MPMapNames.Contains(map.PackageName, StringComparer.OrdinalIgnoreCase);
            }

            result.CollectUnqualifiedImages();

            if (result.Maps.Count == 0)
                result.Warnings.Add($@"{settings.Section}.MasterMapList is empty; the map editor would have nothing to show.");

            return result;
        }

        // ------------------------------------------------------------------ internals

        private void CollectUnqualifiedImages()
        {
            foreach (var path in AllImagePaths)
            {
                if (path.Contains('.')) continue;
                UnqualifiedImages.Add(path);
                Warnings.Add($@"Image '{path}' names no package, so it cannot be resolved; the entry will have no art.");
            }
        }

        /// <summary>
        /// MPMapNames uses indexed assignment, so the INDEX lives in the property name rather than in
        /// the array. Reading it as an ordinary list finds nothing at all, without failing.
        /// </summary>
        private void ReadMapNames(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var controller = ClassScanner.FindByName(PlayerControllerClass, classes);
            if (controller == null)
            {
                Warnings.Add($@"{PlayerControllerClass} was not found; MPMapNames could not be read.");
                return;
            }

            if (index.Find(controller.Section) is not { } section) return;

            var found = new List<(int Order, string Value)>();
            foreach (var prop in section)
            {
                var m = IndexedMapNameRx.Match(prop.Key);
                if (!m.Success) continue;

                var order = m.Groups[@"i"].Success
                    ? int.Parse(m.Groups[@"i"].Value, CultureInfo.InvariantCulture)
                    : found.Count;

                foreach (var v in MergedGameConfig.Live(prop.Value))
                {
                    if (!string.IsNullOrWhiteSpace(v.Value)) found.Add((order, v.Value.Trim()));
                }
            }

            MPMapNames.AddRange(found.OrderBy(f => f.Order).Select(f => f.Value));
        }

        private void ReadMap(string raw, string sectionKey)
        {
            if (!TryRead(raw, $@"[{sectionKey}] MasterMapList", out var fields)) return;

            var gaw = new Dictionary<string, string>();
            var gawText = fields.GetValueOrDefault(@"GalaxyAtWarMapPosition");
            if (!string.IsNullOrWhiteSpace(gawText))
                TryRead(gawText, $@"[{sectionKey}] MasterMapList.GalaxyAtWarMapPosition", out gaw);

            Maps.Add(new MapEntry
            {
                Id = ParseInt(fields.GetValueOrDefault(@"ID")),
                PackageName = fields.GetValueOrDefault(@"PackageName"),
                PrettyName = fields.GetValueOrDefault(@"PrettyName"),
                Description = fields.GetValueOrDefault(@"Description"),
                Image = fields.GetValueOrDefault(@"Image"),
                MusicEventName = fields.GetValueOrDefault(@"MusicEventName"),
                LocationRef = fields.GetValueOrDefault(@"GalaxyAtWarMapSubtitle"),
                GalaxyAtWarX = DifficultyCatalogue.ParseDouble(gaw.GetValueOrDefault(@"X")),
                GalaxyAtWarY = DifficultyCatalogue.ParseDouble(gaw.GetValueOrDefault(@"Y")),
                Fields = fields,
                RawValue = raw,
            });
        }

        private void ReadOption(List<MatchSettingsOption> into, string raw, string sectionKey, string property)
        {
            if (!TryRead(raw, $@"[{sectionKey}] {property}", out var fields)) return;
            into.Add(new MatchSettingsOption
            {
                Id = ParseInt(fields.GetValueOrDefault(@"ID")),
                Image = fields.GetValueOrDefault(@"Image"),
                Fields = fields,
                RawValue = raw,
            });
        }

        private bool TryRead(string text, string context, out Dictionary<string, string> fields)
        {
            fields = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                fields = ConfigStructReader.ReadStruct(text);
                return true;
            }
            catch (FormatException e)
            {
                var message = $@"{context}: malformed config value, skipped - {e.Message}";
                MalformedValues.Add(message);
                Warnings.Add(message);
                return false;
            }
        }

        private static int? ParseInt(string s) =>
            int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }
}
