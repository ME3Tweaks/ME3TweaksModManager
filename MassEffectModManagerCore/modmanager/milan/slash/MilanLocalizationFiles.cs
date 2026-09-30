namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Identifies localized files that an installation may be missing. The base installation copies whatever localizations
    /// the user's LE3 installation has, which can vary, but INT must always be present.
    /// </summary>
    internal static class MilanLocalizationFiles
    {
        /// <summary>
        /// Localizations whose files may be missing. INT is intentionally not in this list.
        /// </summary>
        private static readonly string[] OptionalLocalizations = [@"DEU", @"ESN", @"FRA", @"ITA", @"JPN", @"POL", @"RUS"];

        /// <summary>
        /// If the file is a non-INT localization (Name_FRA.ext or Name_LOC_FRA.ext) that is allowed to be missing.
        /// </summary>
        /// <remarks>
        /// This deliberately doesn't use LEC's GetUnrealLocalization, which also accepts two-letter ME1 suffixes such as
        /// _IT or _RA and would treat unrelated LE3 files as optional.
        /// </remarks>
        public static bool IsOptionalLocalization(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path.Replace('\\', Path.DirectorySeparatorChar));
            var separator = name.LastIndexOf('_');
            if (separator <= 0)
                return false;

            var suffix = name[(separator + 1)..];
            return OptionalLocalizations.Contains(suffix, StringComparer.OrdinalIgnoreCase);
        }
    }
}
