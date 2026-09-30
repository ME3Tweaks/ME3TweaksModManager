namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Identifies localized files that an installation may be missing. The base installation copies whatever localizations
    /// the user's LE3 installation has, which can vary, but INT must always be present.
    /// </summary>
    internal static class MilanLocalizationFiles
    {
        /// <summary>
        /// If the file is a non-INT localization (e.g. Name_FRA.ext or Name_LOC_FRA.ext) that is allowed to be missing.
        /// </summary>
        public static bool IsOptionalLocalization(string path)
        {
            var localization = Path.GetFileName(path.Replace('\\', Path.DirectorySeparatorChar)).GetUnrealLocalization();
            return localization is not (MELocalization.None or MELocalization.INT);
        }
    }
}
