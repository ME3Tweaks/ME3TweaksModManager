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

        internal static string ConvertUnicode(string modDescription)
        {
            // WIP SUPER INNEFICIENT
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < modDescription.Length; i++)
            {
                var c = modDescription[i];
                var codePoint = (int)c;

                if (codePoint > 0x7f)
                {
                    sb.Append($@"\u{codePoint}?");
                } else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
