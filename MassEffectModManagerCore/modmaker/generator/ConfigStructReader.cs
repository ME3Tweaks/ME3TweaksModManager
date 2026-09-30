using System;
using System.Collections.Generic;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>One field of a struct, located in the text it was read from.</summary>
    /// <remarks>
    /// Offsets rather than a substring, because the compiler edits a struct by SPLICING the original
    /// text - replacing exactly the span of the leaf that changed and leaving every other byte alone.
    /// Rebuilding the text from parsed pieces would rewrite float renderings, spacing and BioWare's
    /// tab padding across values nobody edited. See docs/02-data-model.md §0.1.
    /// </remarks>
    internal readonly record struct StructField(string Name, int Start, int End);

    /// <summary>
    /// Reads the struct and list syntax BioWare writes into coalesced values.
    ///
    /// ME3TweaksCore already has StringStructParser, and it is the natural thing to reach for. It
    /// cannot be used for this data - measured, not assumed, against the shapes the wavelists actually
    /// use:
    ///
    ///  - GetSplitMapValues mishandles a struct whose nested value contains quotes. On
    ///    (Difficulty=DO_Level2,Enemies=( (EnemyType="A"), (EnemyType="B") )) it returns Enemies as
    ///    `A"), (EnemyType="B` - the span between the first and last quote. It does not throw. Every
    ///    wave row in the game is that shape.
    ///  - GetParenthesisSplitValues returns ONE item for `( (A), (B) )`, because its outer-layer trim
    ///    requires the second character to be '(' and BioWare puts a space there. Silently, a
    ///    three-enemy wave becomes one unparseable blob.
    ///  - Neither strips the literal backslash-t sequences BioWare embeds after commas, so keys come
    ///    back as `\t\t\tEnemyArchetypeName` and `\t\tWaveCost` and no lookup by name matches.
    ///
    /// So this reader exists, deliberately small, and it never returns a partial parse quietly: a
    /// value it cannot read throws with the text that broke it.
    ///
    /// Everything here works in OFFSETS over one string, with the string-returning methods as thin
    /// wrappers. That is what lets <see cref="ValueTreeBuilder"/> assign a leaf id and record where
    /// that leaf's text lives in the same walk - two walkers that had to agree on both would be a
    /// standing invitation to drift apart, and the symptom would be an edit spliced into the wrong
    /// span rather than an error.
    /// </summary>
    internal static class ConfigStructReader
    {
        /// <summary>
        /// As <see cref="ReadStruct(string)"/>, but leaving values exactly as written - quotes
        /// included.
        ///
        /// Whether a value was quoted is information, not formatting: `Formula=DivideByBonusSum` is an
        /// enum token the game resolves, while `Name="Reegar"` is data. Stripping the quotes first
        /// makes the two indistinguishable, and the value tree has to tell them apart to decide what
        /// is editable text and what must be preserved verbatim.
        /// </summary>
        public static Dictionary<string, string> ReadStructRaw(string text) => ReadStruct(text, keepQuotes: true);

        /// <summary>
        /// Reads (A=1,B=(X=2),C="text") into its top-level fields. Nested values are returned intact
        /// so the caller can read them in turn; quoted values are unquoted; field names are
        /// normalised. Duplicate field names keep the LAST occurrence, matching how UnrealScript
        /// parses a struct literal.
        /// </summary>
        public static Dictionary<string, string> ReadStruct(string text) => ReadStruct(text, keepQuotes: false);

        private static Dictionary<string, string> ReadStruct(string text, bool keepQuotes)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            var fields = ReadFields(text, 0, text.Length);
            foreach (var field in fields)
            {
                var value = text[field.Start..field.End];
                if (!keepQuotes && value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
                result[field.Name] = value;
            }
            return result;
        }

        /// <summary>
        /// As <see cref="ReadStructRaw"/>, but reporting failure by returning false.
        ///
        /// The strictness is unchanged - a value this cannot read is still not half-read - but callers
        /// that run over tens of thousands of values should not drive control flow with exceptions,
        /// which stop the debugger once per malformed value in the game.
        /// </summary>
        public static bool TryReadStructRaw(string text, out Dictionary<string, string> fields)
        {
            try
            {
                fields = ReadStructRaw(text);
                return true;
            }
            catch (FormatException)
            {
                fields = null;
                return false;
            }
        }

        /// <summary>
        /// The fields of the struct occupying <paramref name="full"/>[start..end), with each value
        /// located rather than copied. False when the span is not a readable struct.
        ///
        /// Duplicate field names keep the last occurrence's VALUE at the first occurrence's position,
        /// which is what building a dictionary in source order does and therefore what the
        /// string-returning readers have always done.
        /// </summary>
        public static bool TryReadFields(string full, int start, int end, out List<StructField> fields)
        {
            try
            {
                fields = ReadFields(full, start, end);
                return true;
            }
            catch (FormatException)
            {
                fields = null;
                return false;
            }
        }

        private static List<StructField> ReadFields(string full, int start, int end)
        {
            StripOuterParens(full, ref start, ref end);

            var ordered = new List<StructField>();
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (fieldStart, fieldEnd) in SplitTopLevel(full, start, end, ','))
            {
                var eq = IndexOfTopLevel(full, fieldStart, fieldEnd, '=');
                if (eq < 0)
                {
                    // A field with no '=' is not something this data ever contains. Ignoring it would
                    // drop a value; guessing at it would invent one.
                    throw new FormatException(
                        $@"Config struct field has no '=': '{full[fieldStart..fieldEnd]}' in '{full[start..end]}'");
                }

                var key = NormaliseKey(full, fieldStart, eq);
                if (key.Length == 0) continue;

                int valueStart = eq + 1, valueEnd = fieldEnd;
                Trim(full, ref valueStart, ref valueEnd);

                var field = new StructField(key, valueStart, valueEnd);
                if (positions.TryGetValue(key, out var at)) ordered[at] = field;
                else
                {
                    positions[key] = ordered.Count;
                    ordered.Add(field);
                }
            }

            return ordered;
        }

        /// <summary>
        /// Reads ((A),(B)) - or ( (A), (B) ), which is what the wave rows use - into its elements,
        /// each still wrapped in its own parentheses.
        ///
        /// A single group spanning the whole input is descended into once, so both a list and a
        /// single-element list read correctly; a bare struct with no inner groups comes back as a
        /// one-element list rather than as nothing.
        /// </summary>
        public static List<string> ReadList(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            int start = 0, end = text.Length;
            Trim(text, ref start, ref end);

            var groups = TopLevelGroups(text, start, end);
            if (groups.Count == 1)
            {
                int innerStart = groups[0].Start, innerEnd = groups[0].End;
                StripOuterParens(text, ref innerStart, ref innerEnd);

                var descended = TopLevelGroups(text, innerStart, innerEnd);
                if (descended.Count > 0) return Slice(text, descended);
            }
            return Slice(text, groups);
        }

        private static List<string> Slice(string text, List<(int Start, int End)> spans)
        {
            var result = new List<string>(spans.Count);
            foreach (var (start, end) in spans) result.Add(text[start..end]);
            return result;
        }

        /// <summary>
        /// Strips the literal backslash-t / backslash-n sequences and whitespace BioWare leaves around
        /// field names. These are two characters in the config text, not control characters, so no
        /// amount of Trim() removes them.
        /// </summary>
        private static string NormaliseKey(string full, int start, int end)
        {
            Trim(full, ref start, ref end);
            var k = full[start..end];

            bool trimmed = true;
            while (trimmed && k.Length >= 2)
            {
                trimmed = false;
                foreach (var esc in EscapeSequences)
                {
                    if (k.StartsWith(esc, StringComparison.Ordinal)) { k = k[2..].TrimStart(); trimmed = true; }
                    if (k.EndsWith(esc, StringComparison.Ordinal)) { k = k[..^2].TrimEnd(); trimmed = true; }
                }
            }
            return k.Trim();
        }

        private static readonly string[] EscapeSequences = { @"\t", @"\n", @"\r" };

        /// <summary>Advances the bounds past leading and trailing whitespace.</summary>
        public static void Trim(string text, ref int start, ref int end)
        {
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        }

        /// <summary>
        /// Removes one wrapping parenthesis pair from the bounds. Unbalanced input throws, including
        /// text left over after the closing parenthesis.
        ///
        /// Being strict here is a deliberate choice about where a data error gets fixed. LMP currently
        /// ships one wave row with a parenthesis too many, in Milan's own MergedConfig/BioGame.xml, and
        /// tolerating it would mean carrying a recovery path forever so that every future malformed
        /// value slides through it unnoticed. The row is reported by name instead and corrected at
        /// source. What must never happen is the row disappearing quietly - see WavelistCatalogue,
        /// which turns this exception into a named warning rather than a skipped row.
        /// </summary>
        private static void StripOuterParens(string text, ref int start, ref int end)
        {
            Trim(text, ref start, ref end);
            if (end - start < 2 || text[start] != '(') return;

            var close = MatchingClose(text, start, end);
            if (close < 0) throw new FormatException($@"Unclosed '(' in '{text[start..end]}'");

            int afterStart = close + 1, afterEnd = end;
            Trim(text, ref afterStart, ref afterEnd);
            if (afterEnd > afterStart)
                throw new FormatException(
                    $@"Unbalanced parentheses: '{text[afterStart..afterEnd]}' follows the closing ')' in '{text[start..end]}'");

            start++;
            end = close;
        }

        /// <summary>Index of the ')' closing the '(' at <paramref name="open"/>, or -1 if unbalanced.</summary>
        public static int MatchingClose(string text, int open, int end)
        {
            int depth = 0;
            bool inQuotes = false;
            for (int i = open; i < end; i++)
            {
                char c = text[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;
                if (c == '(') depth++;
                else if (c == ')' && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Every parenthesised group at nesting depth 0, in order.</summary>
        public static List<(int Start, int End)> TopLevelGroups(string text, int start, int end)
        {
            var groups = new List<(int, int)>();
            bool inQuotes = false;
            int depth = 0, open = -1;

            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;

                if (c == '(')
                {
                    if (depth == 0) open = i;
                    depth++;
                }
                else if (c == ')')
                {
                    if (depth == 0) throw new FormatException($@"Unbalanced ')' at {i} in '{text[start..end]}'");
                    if (--depth == 0) groups.Add((open, i + 1));
                }
            }

            if (depth != 0) throw new FormatException($@"Unbalanced '(' in '{text[start..end]}'");
            return groups;
        }

        public static List<(int Start, int End)> SplitTopLevel(string text, int start, int end, char separator)
        {
            var parts = new List<(int, int)>();
            bool inQuotes = false;
            int depth = 0, from = start;

            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;

                if (c == '(') depth++;
                else if (c == ')')
                {
                    if (depth == 0) throw new FormatException($@"Unbalanced ')' at {i} in '{text[start..end]}'");
                    depth--;
                }
                else if (c == separator && depth == 0)
                {
                    parts.Add((from, i));
                    from = i + 1;
                }
            }

            if (depth != 0) throw new FormatException($@"Unbalanced '(' in '{text[start..end]}'");

            int tailStart = from, tailEnd = end;
            Trim(text, ref tailStart, ref tailEnd);
            if (tailEnd > tailStart || parts.Count > 0) parts.Add((from, end));
            return parts;
        }

        public static int IndexOfTopLevel(string text, int start, int end, char needle)
        {
            bool inQuotes = false;
            int depth = 0;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == needle && depth == 0) return i;
            }
            return -1;
        }
    }
}
