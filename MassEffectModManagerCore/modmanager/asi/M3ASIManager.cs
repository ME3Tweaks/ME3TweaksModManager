namespace ME3TweaksModManager.modmanager.asi
{
    /// <summary>
    /// Contains M3-specific ASI things
    /// </summary>
    internal class M3ASIManager
    {
        /// <summary>
        /// Gate for ability to see hidden ASIs/s
        /// </summary>
        public static bool CanSeeHiddenASIs() => Settings.DeveloperMode && Settings.AlphaMode;
    }
}
