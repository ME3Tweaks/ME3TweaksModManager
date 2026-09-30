using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.compiler
{
    /// <summary>One leaf of one config value, set to something new.</summary>
    public record LeafEdit(string LeafId, JToken Value);

    /// <summary>
    /// Puts an edited leaf back into the config text it came out of.
    ///
    /// By SPLICING, not by reserialising. The value tree is a parse of the shipped text, and turning
    /// that parse back into text would rewrite float renderings, member spacing and the literal
    /// backslash-t padding BioWare leaves in its structs - across every value in the property,
    /// including the ones the user never touched. The user asked to change one number; the diff would
    /// show forty. Worse, the rewritten text no longer matches the shipped text, which is what array
    /// subtraction matches on, so a later splice against it silently does nothing.
    ///
    /// So the original string is authoritative and only the changed leaf's own span is replaced.
    /// Everything outside those spans comes through byte for byte, which is exactly the guarantee
    /// docs/02-data-model.md §0.1 asks for.
    /// </summary>
    public static class ValueWriter
    {
        /// <summary>
        /// Applies <paramref name="edits"/> to <paramref name="original"/>, whose leaf ids are rooted
        /// at <paramref name="idPath"/>.
        ///
        /// Returns false, with a reason, rather than writing something approximate: every failure here
        /// is a value the compiler does not understand well enough to change safely, and the whole
        /// point of the exercise is that a mod never silently omits or mangles an edit.
        /// </summary>
        public static bool TryApply(string original, string idPath, IEnumerable<LeafEdit> edits,
                                    out string result, out string problem)
        {
            result = original;
            problem = null;

            var list = edits?.ToList() ?? [];
            if (list.Count == 0) return true;

            if (original == null)
            {
                problem = @"the shipped value's text was not captured, so there is nothing to edit";
                return false;
            }

            var spans = ValueTreeBuilder.Spans(original, idPath);

            // Resolved in full before anything is written. A partially applied value is a mod that
            // installs with half the user's edit in it, which is worse than a compile that stops.
            var pending = new List<(LeafSpan Span, string Text)>();
            foreach (var edit in list)
            {
                if (!spans.TryGetValue(edit.LeafId, out var span))
                {
                    problem = $@"'{edit.LeafId}' is not an editable leaf of the installed value; " +
                              @"the game's config has changed since this edit was made";
                    return false;
                }

                if (!TryRender(edit.Value, span, original, out var text, out var why))
                {
                    problem = $@"'{edit.LeafId}' could not be written: {why}";
                    return false;
                }

                pending.Add((span, text));
            }

            // Back to front, so an earlier splice does not move the offsets of a later one.
            var builder = new System.Text.StringBuilder(original);
            foreach (var (span, text) in pending.OrderByDescending(p => p.Span.Start))
            {
                builder.Remove(span.Start, span.End - span.Start);
                builder.Insert(span.Start, text);
            }

            result = builder.ToString();
            return true;
        }

        /// <summary>
        /// The config text for one new leaf value.
        ///
        /// Rendered to match the leaf's kind rather than the JSON token's, because the kind is what
        /// the game's parser expects: writing 7 into a float the config spells 6.0 is a change of
        /// shape as well as value, and there is no reason to make the diff say more than it means.
        /// </summary>
        private static bool TryRender(JToken value, LeafSpan span, string original, out string text, out string problem)
        {
            text = null;
            problem = null;

            if (value == null || value.Type == JTokenType.Null)
            {
                problem = @"no value was given";
                return false;
            }

            // A range control - {min, max} - is an authored constraint on a value, not a value. The
            // schema allows one and nothing here resolves it into config yet, so it is named rather
            // than left to fail as "not a number".
            if (value.Type is JTokenType.Object or JTokenType.Array)
            {
                problem = @"it is a range control, which this build cannot compile yet";
                return false;
            }

            switch (span.Kind)
            {
                case @"int":
                    if (value.Type is not (JTokenType.Integer or JTokenType.Float or JTokenType.String)
                        || !long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    {
                        problem = $@"'{value}' is not a whole number";
                        return false;
                    }
                    text = i.ToString(CultureInfo.InvariantCulture);
                    return true;

                case @"float":
                    var parsed = value.Type == JTokenType.String
                        ? DifficultyCatalogue.ParseDouble(value.ToString())
                        : (double?)value;
                    if (parsed == null || double.IsNaN(parsed.Value) || double.IsInfinity(parsed.Value))
                    {
                        problem = $@"'{value}' is not a number";
                        return false;
                    }
                    text = Float(parsed.Value);
                    return true;

                case @"bool":
                    if (!bool.TryParse(value.ToString(), out var b))
                    {
                        problem = $@"'{value}' is not true or false";
                        return false;
                    }
                    // Spelled the way the shipped value spelled it. The game's parser does not care,
                    // but a diff that flips True to true on every untouched-looking line does.
                    var shipped = original[span.Start..span.End];
                    text = char.IsUpper(shipped[0]) ? (b ? @"True" : @"False") : (b ? @"true" : @"false");
                    return true;

                case @"string":
                    var s = value.ToString();
                    // No escape form for a quote inside a coalesced string has been verified, and
                    // guessing at one produces a struct that parses wrongly rather than one that
                    // fails - so this is refused instead.
                    if (s.Contains('"'))
                    {
                        problem = @"it contains a double quote, which has no verified escape in coalesced config";
                        return false;
                    }
                    text = @"""" + s + @"""";
                    return true;

                default:
                    problem = $@"leaf kind '{span.Kind}' cannot be written";
                    return false;
            }
        }

        /// <summary>
        /// Config text for a value with no shipped text behind it, taking the leaf's kind from the
        /// JSON token instead of from a parse of the original.
        ///
        /// Used only where the config does not set the property at all, so its effective value is the
        /// class's compiled default. There is no tree to locate a leaf in, and none is needed: the
        /// value being written IS the whole property.
        /// </summary>
        public static bool TryRenderBare(JToken value, out string text, out string problem)
        {
            text = null;
            problem = null;

            switch (value?.Type)
            {
                case JTokenType.Integer:
                    text = ((long)value).ToString(CultureInfo.InvariantCulture);
                    return true;

                case JTokenType.Float:
                    text = Float((double)value);
                    return true;

                case JTokenType.Boolean:
                    // Lower case, because there is no shipped spelling to match here.
                    text = (bool)value ? @"true" : @"false";
                    return true;

                case JTokenType.String:
                    var s = value.ToString();
                    if (s.Contains('"'))
                    {
                        problem = @"it contains a double quote, which has no verified escape in coalesced config";
                        return false;
                    }
                    text = @"""" + s + @"""";
                    return true;

                default:
                    problem = @"only a number, true/false or a string can be written this way";
                    return false;
            }
        }

        /// <summary>
        /// A float as config spells it, which always carries a decimal point.
        ///
        /// "R" round-trips the value rather than rounding it to a fixed number of places, so a
        /// user's 0.3333333333333333 is not quietly truncated on its way into the mod.
        /// </summary>
        private static string Float(double value)
        {
            var text = value.ToString(@"R", CultureInfo.InvariantCulture);
            if (text.IndexOfAny(['.', 'E', 'e']) < 0) text += @".0";
            return text;
        }
    }
}
