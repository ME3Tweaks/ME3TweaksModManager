using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// Resolves the string refs the config carries into text the user can read.
    ///
    /// Maps, factions and difficulties are all named by number - `PrettyName=618041` - so an editor
    /// that shows what the config says shows a table of integers. The names are in the game's TLKs,
    /// which means they are already localised and already right for whatever version of Milan is
    /// installed: there is nothing here to author and nothing to keep in step.
    ///
    /// Loaded in MOUNT ORDER, DLC first, because <see cref="LMPTalkFiles"/> returns the first match
    /// and a DLC's override of a basegame string has to win. Getting that backwards does not fail, it
    /// shows the old name.
    /// </summary>
    public class GameStrings
    {
        /// <summary>
        /// Formatting tokens the game substitutes at runtime - levels, ranks, counts. Left in place
        /// they read as corruption; there is nothing sensible to substitute for them here.
        /// </summary>
        private static readonly Regex CustomToken = new(@"\s*<CUSTOM\d+>\s*", RegexOptions.Compiled);

        /// <summary>
        /// A TLK entry beginning `$12345` is a redirect to another entry, and some redirect and then
        /// append their own text - `$728176 Slower power use.` - so the tail is kept.
        /// </summary>
        private static readonly Regex Redirect = new(@"^\$(\d+)\b", RegexOptions.Compiled);

        /// <summary>
        /// `[Ux2623]` - a codepoint the game's text renderer substitutes, stored in the TLK as those
        /// eight literal characters rather than as the character itself.
        ///
        /// The hazard maps are why this is worth handling: their names are `[Ux2623] Firebase Dagger
        /// [Ux2623]`, and left alone that is exactly what the editor shows. It also cannot simply be
        /// stripped - the escape is the only thing distinguishing the six hazard variants from the
        /// ordinary version of the same map, so removing it would leave six apparent duplicates.
        /// </summary>
        private static readonly Regex UnicodeEscape =
            new(@"\[Ux(?<hex>[0-9A-Fa-f]{1,6})\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Dictionary<int, string> _cache = new();

        /// <summary>TLK files that were loaded, in the order they were loaded. Diagnostic.</summary>
        public List<string> LoadedFiles { get; } = new();

        /// <summary>True when at least one TLK loaded. False means every name falls back to its number.</summary>
        public bool Available => LoadedFiles.Count > 0;

        /// <summary>
        /// Loads the target's INT TLKs.
        ///
        /// English only, deliberately: this text is shown so the author can recognise a map, and the
        /// mod being built does not carry it. What a ModMaker mod adds to the TLK is a different job -
        /// the localisation overlay in docs 02 - and it has its own path.
        /// </summary>
        public static GameStrings Load(GameTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var result = new GameStrings();

            // Shared static state in LEC, so it is reset first: a previous load against another
            // install would otherwise answer for this one, and the wrong name is indistinguishable
            // from a name we simply have.
            LMPTalkFiles.ClearLoadedTlks();

            foreach (var path in TlkPaths(target))
            {
                try
                {
                    LMPTalkFiles.LoadTlkData(path);
                    result.LoadedFiles.Add(path);
                }
                catch (Exception)
                {
                    // One unreadable TLK costs its own strings, not the editor.
                }
            }

            return result;
        }

        /// <summary>DLC TLKs first, then the basegame's - mount order, so DLC overrides win.</summary>
        private static IEnumerable<string> TlkPaths(GameTarget target)
        {
            var paths = new List<string>();

            var dlcRoot = MEDirectories.GetDLCPath(target.Game, target.TargetPath);
            if (Directory.Exists(dlcRoot))
            {
                foreach (var folder in ModMakerCatalogue.ScannableDLCFoldersInMountOrder(target))
                {
                    var cooked = Path.Combine(dlcRoot, folder, target.Game.CookedDirName());
                    if (Directory.Exists(cooked)) paths.AddRange(Directory.GetFiles(cooked, @"*_INT.tlk"));
                }
                paths.Reverse();
            }

            var basegame = MEDirectories.GetCookedPath(target.Game, target.TargetPath);
            if (Directory.Exists(basegame))
                paths.AddRange(Directory.GetFiles(basegame, @"*_INT.tlk", SearchOption.TopDirectoryOnly));

            return paths;
        }

        /// <summary>
        /// The text for a string ref, or null when there is none.
        ///
        /// Null rather than the number: the caller decides what to show, and the number is worth
        /// showing beside a name rather than instead of one.
        /// </summary>
        public string Resolve(int? strRef)
        {
            if (strRef is not { } id || id <= 0) return null;
            if (_cache.TryGetValue(id, out var hit)) return hit;

            var text = Lookup(id, 0);
            _cache[id] = text;
            return text;
        }

        /// <summary>As <see cref="Resolve(int?)"/>, taking the config's own text form.</summary>
        public string Resolve(string strRef) =>
            int.TryParse(strRef?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? Resolve(id) : null;

        private static string Lookup(int id, int depth)
        {
            // "No Data" is the sentinel this API returns for an absent ref, and it comes back
            // wrapped in quotes like every hit does - so it is checked after unquoting.
            var raw = Unquote(LMPTalkFiles.FindDataById(id));
            if (string.IsNullOrWhiteSpace(raw) || raw == @"No Data") return null;

            var text = Unescape(CustomToken.Replace(raw.Replace('\r', ' ').Replace('\n', ' '), @" ")).Trim();
            if (depth >= 8 || Redirect.Match(text) is not { Success: true } m) return NullIfEmpty(text);

            var target = Lookup(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), depth + 1);
            var tail = text[m.Length..].Trim();
            return NullIfEmpty(string.Join(@" ", new[] { target, tail }).Trim());
        }

        /// <summary>
        /// Substitutes the game's `[Ux####]` codepoint escapes.
        ///
        /// A value outside the Unicode range, or a surrogate half, is left exactly as written rather
        /// than dropped or replaced: at that point it is not an escape at all, and something that
        /// merely looks like one is just text.
        /// </summary>
        internal static string Unescape(string text) =>
            text == null ? null : UnicodeEscape.Replace(text, m =>
            {
                var code = int.Parse(m.Groups[@"hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return code is > 0 and <= 0x10FFFF && code is < 0xD800 or > 0xDFFF
                    ? char.ConvertFromUtf32(code)
                    : m.Value;
            });

        private static string Unquote(string s)
        {
            var t = s?.Trim();
            return t is { Length: >= 2 } && t[0] == '"' && t[^1] == '"' ? t[1..^1] : t;
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
