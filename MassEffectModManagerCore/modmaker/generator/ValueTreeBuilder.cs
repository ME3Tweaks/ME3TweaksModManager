using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>Where one editable leaf's text sits in the value it was read from.</summary>
    /// <param name="Kind">The leaf kind: int, float, bool or string.</param>
    /// <param name="Start">Index of the first character of the leaf's text.</param>
    /// <param name="End">Index one past the last character. For a string this INCLUDES the quotes.</param>
    public readonly record struct LeafSpan(string Kind, int Start, int End);

    /// <summary>
    /// Turns a coalesced value into the bundle's value tree: nested structs, arrays, editable leaves
    /// and preserved literals.
    ///
    /// Most of the editable surface is not scalar. `CoolDownTime` is
    /// `(BaseValue=6.0,Formula=DivideByBonusSum,RankBonuses=(0.0,0.25))` - a struct holding a number
    /// worth editing, an enum token that must survive untouched, and an array of numbers. A generator
    /// that only understands scalars leaves 856 of these out of the bundle entirely.
    ///
    /// What becomes editable is decided from the text, and deliberately conservatively:
    ///
    ///  - a number, or true/false, becomes a leaf the user can change
    ///  - a QUOTED string becomes an editable string leaf - it was quoted, so it is data
    ///  - an unquoted token becomes a LITERAL, preserved verbatim and not editable
    ///
    /// The last one is the important one. An unquoted token is an enum value, an object path or a
    /// formula name, and this tool does not know which values are legal for it. Offering a free text
    /// box would let a user write something the game silently ignores; preserving it keeps the
    /// surrounding struct editable without inventing an affordance that cannot be validated. Enum
    /// options can be added later from the game's own enum definitions, and the leaf kind "enum"
    /// exists for exactly that.
    ///
    /// <para>
    /// The walk serves two callers that must never disagree. The generator wants the TREE, to put in
    /// the bundle; the compiler wants the SPANS, so it can splice a changed leaf back into the
    /// original text without reserialising the parts nobody edited (docs/02-data-model.md §0.1). Both
    /// come out of <see cref="Node"/> in one pass, because two walks that had to assign identical ids
    /// would drift, and the symptom of drift is an edit written into the wrong place rather than an
    /// error.
    /// </para>
    /// </summary>
    public static class ValueTreeBuilder
    {
        /// <summary>
        /// Builds a node from one config value. <paramref name="idPath"/> is the leaf id prefix, which
        /// must already be lowercase and match the schema's id pattern.
        /// </summary>
        public static JObject Build(string text, string idPath, BuildStats stats = null)
        {
            stats ??= new BuildStats();
            var s = text ?? string.Empty;
            return Node(s, 0, s.Length, idPath, stats, depth: 0, spans: null);
        }

        /// <summary>
        /// Where every editable leaf of one config value lives in that value's text, keyed by the same
        /// leaf id <see cref="Build"/> gives it.
        ///
        /// Literals get no entry: they are not editable, so there is nothing to splice into them.
        /// </summary>
        public static Dictionary<string, LeafSpan> Spans(string text, string idPath)
        {
            var spans = new Dictionary<string, LeafSpan>(StringComparer.Ordinal);
            var s = text ?? string.Empty;
            Node(s, 0, s.Length, idPath, new BuildStats(), depth: 0, spans: spans);
            return spans;
        }

        /// <summary>Counts of what the tree turned out to contain. Diagnostic, not part of the bundle.</summary>
        public class BuildStats
        {
            public int Leaves;
            public int Literals;
            public int Structs;
            public int Arrays;

            /// <summary>Values abandoned because they nest deeper than the guard allows.</summary>
            public int TooDeep;
        }

        /// <summary>
        /// Nesting guard. Real config reaches three or four levels; anything beyond this is either
        /// malformed or a shape nobody has seen, and recursing without a bound on generated input is
        /// how a catalogue build turns into a stack overflow.
        /// </summary>
        private const int MaxDepth = 12;

        private static JObject Node(string full, int start, int end, string idPath, BuildStats stats,
                                    int depth, Dictionary<string, LeafSpan> spans)
        {
            ConfigStructReader.Trim(full, ref start, ref end);

            if (depth >= MaxDepth)
            {
                stats.TooDeep++;
                return Literal(full[start..end], stats);
            }

            if (!IsParenthesised(full, start, end)) return Scalar(full, start, end, idPath, stats, spans);

            int innerStart = start + 1, innerEnd = end - 1;

            // The readers throw on text they cannot make sense of, which is right for a caller reading
            // one known value and wrong here: this walks every value in the game, and a malformed one
            // must come back as a preserved literal rather than stop a catalogue build.
            try
            {
                // A struct has named fields at its top level; an array has positional groups. The
                // distinction is a top-level '=' - and it must be found at depth 0 only, or a nested
                // struct inside an array element makes every array look like a struct.
                if (ConfigStructReader.IndexOfTopLevel(full, innerStart, innerEnd, '=') >= 0)
                    return Struct(full, start, end, idPath, stats, depth, spans);

                var groups = ConfigStructReader.TopLevelGroups(full, innerStart, innerEnd);
                if (groups.Count > 0) return Array(full, groups, idPath, stats, depth, spans);

                // Parenthesised, no assignment, no nested groups: a plain list of scalars such as
                // RankBonuses=(0.0,0.25).
                var parts = new List<(int Start, int End)>();
                foreach (var (partStart, partEnd) in ConfigStructReader.SplitTopLevel(full, innerStart, innerEnd, ','))
                {
                    int s = partStart, e = partEnd;
                    ConfigStructReader.Trim(full, ref s, ref e);
                    if (e > s) parts.Add((partStart, partEnd));
                }
                if (parts.Count > 0) return Array(full, parts, idPath, stats, depth, spans);
            }
            catch (FormatException)
            {
                return Literal(full[start..end], stats);
            }

            return Literal(full[start..end], stats);
        }

        private static JObject Struct(string full, int start, int end, string idPath, BuildStats stats,
                                      int depth, Dictionary<string, LeafSpan> spans)
        {
            stats.Structs++;

            if (!ConfigStructReader.TryReadFields(full, start, end, out var fields))
                return Literal(full[start..end], stats);

            var members = new JArray();
            foreach (var field in fields)
            {
                members.Add(new JObject
                {
                    [@"name"] = field.Name,
                    [@"value"] = Node(full, field.Start, field.End, Join(idPath, field.Name), stats, depth + 1, spans),
                });
            }

            return new JObject { [@"kind"] = @"struct", [@"members"] = members };
        }

        private static JObject Array(string full, List<(int Start, int End)> items, string idPath,
                                     BuildStats stats, int depth, Dictionary<string, LeafSpan> spans)
        {
            stats.Arrays++;
            var nodes = new JArray();

            for (int i = 0; i < items.Count; i++)
            {
                // Array elements have no name in the data, so the index is the only identity available.
                // That makes their ids positional, and reordering an array renames every leaf after the
                // move - which is why an editor that reorders has to rewrite the project's keys rather
                // than assume they still line up.
                nodes.Add(Node(full, items[i].Start, items[i].End,
                               Join(idPath, i.ToString(CultureInfo.InvariantCulture)), stats, depth + 1, spans));
            }

            return new JObject { [@"kind"] = @"array", [@"items"] = nodes };
        }

        private static JObject Scalar(string full, int start, int end, string idPath, BuildStats stats,
                                      Dictionary<string, LeafSpan> spans)
        {
            var text = full[start..end];

            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
                return Editable(@"string", idPath, new JValue(text[1..^1]), start, end, stats, spans);

            if (bool.TryParse(text, out var b))
                return Editable(@"bool", idPath, new JValue(b), start, end, stats, spans);

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return Editable(@"int", idPath, new JValue(i), start, end, stats, spans);

            var f = DifficultyCatalogue.ParseDouble(text);
            if (f != null)
                return Editable(@"float", idPath, new JValue(f.Value), start, end, stats, spans);

            // A number with something stuck to the end of it - "589176;", "0.5f" - is a NUMBER to the
            // game: Unreal's config readers stop at the first character that cannot continue one, so
            // the trailing text is already ignored where it matters. Treating it as an opaque literal
            // meant the editor would not offer it and the compiler could not write it, for what is
            // usually one stray character in one config line.
            //
            // The span covers the digits ONLY. That is the whole trick: an edit replaces the number
            // and leaves the rest of the shipped text exactly where it was, which is the same promise
            // made to every value this builder touches.
            var digits = NumericPrefixLength(text);
            if (digits > 0)
            {
                var head = text[..digits];
                if (int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pi))
                    return Editable(@"int", idPath, new JValue(pi), start, start + digits, stats, spans);

                var pf = DifficultyCatalogue.ParseDouble(head);
                if (pf != null)
                    return Editable(@"float", idPath, new JValue(pf.Value), start, start + digits, stats, spans);
            }

            // An unquoted token: an enum value, an object path, a formula name. Preserved, not offered
            // for editing - see the class summary.
            return Literal(text, stats);
        }

        /// <summary>
        /// How many characters at the start of <paramref name="text"/> form a number, or 0 for none.
        ///
        /// Shared with the bundle writer so the two agree: a value the tree offers as an editable
        /// number must be one the bundle reports as a number, or the editor shows text for a leaf the
        /// compiler is perfectly able to write.
        /// </summary>
        public static int NumericPrefixLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int i = 0;
            if (text[i] == '+' || text[i] == '-') i++;

            int beforeDigits = i;
            while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
            if (i < text.Length && text[i] == '.')
            {
                i++;
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
            }

            // A sign or a lone "." is not a number, and neither is the whole string when it already
            // parsed above - the caller has tried the exact forms first.
            if (i == beforeDigits) return 0;
            return i;
        }

        private static JObject Editable(string kind, string id, JToken defaultValue, int start, int end,
                                        BuildStats stats, Dictionary<string, LeafSpan> spans)
        {
            stats.Leaves++;
            // A leaf id can repeat only if the id scheme itself collides, which Join exists to prevent.
            // Keeping the FIRST is arbitrary but must be deterministic; the compiler's round-trip test
            // is what would notice if it ever mattered.
            spans?.TryAdd(id, new LeafSpan(kind, start, end));

            return new JObject
            {
                [@"kind"] = kind,
                [@"id"] = id,
                [@"default"] = defaultValue,
            };
        }

        private static JObject Literal(string text, BuildStats stats)
        {
            stats.Literals++;
            return new JObject { [@"kind"] = @"literal", [@"text"] = text };
        }

        /// <summary>
        /// Leaf ids must match ^[a-z0-9_]+(\.[a-z0-9_]+)*$, so every segment is lowercased and
        /// anything else is replaced rather than dropped: dropping characters can make two different
        /// members collide on one id, which silently merges two values into one.
        /// </summary>
        private static string Join(string prefix, string segment)
        {
            var cleaned = string.Create(segment.Length, segment, (span, source) =>
            {
                for (int i = 0; i < source.Length; i++)
                {
                    var c = char.ToLowerInvariant(source[i]);
                    span[i] = char.IsLetterOrDigit(c) || c == '_' ? c : '_';
                }
            });

            if (cleaned.Length == 0) cleaned = @"_";
            return string.IsNullOrEmpty(prefix) ? cleaned : prefix + @"." + cleaned;
        }

        private static bool IsParenthesised(string full, int start, int end) =>
            end - start >= 2
            && full[start] == '('
            && ConfigStructReader.MatchingClose(full, start, end) == end - 1;
    }
}
