using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Helper for generating RichText. It's really ugly, so we put utility methods here.
    /// </summary>
    public static class RichTextHelper
    {
        // References:
        // https://github.com/xceedsoftware/wpftoolkit/wiki/RichTextBox
        // https://metacpan.org/dist/RTF-Writer/view/lib/RTF/Cookbook.pod

        /// <summary>
        /// Returns the default RichText header.
        /// </summary>
        /// <returns></returns>
        public static string GetHeader()
        {
            return @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs18 "; // Must contain space on the end
        }

        public static string GetFooter()
        {
            return @"}";
        }

        private const string RT_BOLD = @"\b ";
        private const string RT_ITALIC = @"\i ";
        private const string RT_NEWLINE = @"\line ";
        public static string ConvertNewlines(string str)
        {
            return str.Replace("\r\n", RT_NEWLINE).Replace("\n", RT_NEWLINE); // do not localize
        }

        public static string EscapeText(string str)
        {
            return str.Replace(@"\", @"\\");
        }

        public static string MakeBold(string str)
        {
            return $@"{{{RT_BOLD}{str}}}";
        }

        public static string MakeItalic(string str)
        {
            return $@"{{{RT_ITALIC}{str}}}";
        }

        /// <summary>
        /// This method should convert an input string to a format that can be used in an RTF
        /// document. Characters that can't be displayed in an RTF document should be encoded into
        /// unicode.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        internal static string ConvertUnicode(string text)
        {
            // Fast-path for empty input
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            // Reserve an estimated capacity to avoid frequent reallocations. Worst-case
            // each character can expand to something like "\\u-32768?" (~9 chars),
            // but most text will be ASCII so a smaller multiplier is sufficient.

            // We change size of allocation based on lang;
            // RUS has a lot more unicode than INT for example
            int allocMultiplier = 1;
            switch (Settings.Language)
            {
                case @"DEU":
                    allocMultiplier = 2;
                    break;
                case @"ITA":
                    allocMultiplier = 2;
                    break;
                case "@RUS":
                    allocMultiplier = 6;
                    break;
            }

            var sb = new StringBuilder(text.Length * allocMultiplier);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // Handle high surrogate followed by low surrogate (valid pair)
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        short high = (short)c;
                        short low = (short)text[i + 1];
                        sb.Append("\\u").Append(high).Append('?');
                        sb.Append("\\u").Append(low).Append('?');
                        i++; // consumed low surrogate
                    }
                    else
                    {
                        // Unmatched high surrogate - emit replacement
                        sb.Append('?');
                    }
                }
                else if (char.IsLowSurrogate(c))
                {
                    // Unmatched low surrogate - emit replacement
                    sb.Append('?');
                }
                else if (c > 0x7f)
                {
                    // Non-ASCII BMP character: emit signed 16-bit RTF escape
                    sb.Append("\\u").Append((short)c).Append('?');
                }
                else
                {
                    // ASCII character - append directly
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
