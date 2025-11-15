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
            // todo: change this to unicode.
            return @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs18";
        }

        public static string GetFooter()
        {
            return @"}";
        }

        private const string RT_BOLD = @"\b ";

        public static string ConvertNewlines(string str)
        {
            return str.Replace("\r\n", @"\line").Replace("\n", @"\line");
        }

        public static string EscapeText(string str)
        {
            return str.Replace(@"\", @"\\");
        }

        public static string MakeBold(string str)
        {
            return $@"{{\b {str}}}";
        }

        public static string MakeItalic(string str)
        {
            return $@"{{\b {str}}}";
        }
    }
}
