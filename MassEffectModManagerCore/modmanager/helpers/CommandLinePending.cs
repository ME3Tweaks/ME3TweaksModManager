namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Class for caching variables used for holding temporary command-line options that need to be processed
    /// </summary>
    public static class CommandLinePending
    {
        /// <summary>
        /// If this boot is upgrading from ME3CMM
        /// </summary>
        public static bool UpgradingFromME3CMM;
        public static string PendingNXMLink;

        /// <summary>
        /// A pending M3Link to handle
        /// </summary>
        public static string PendingM3Link;

        /// <summary>
        /// 
        /// </summary>
        public static string PendingAutoModInstallPath;

        /// <summary>
        /// If game should be booted after all other options have been performed
        /// </summary>
        public static bool PendingGameBoot;

        /// <summary>
        /// If a target should have M3 DLC merge performed on it
        /// </summary>
        public static bool PendingMergeDLCCreation;

        /// <summary>
        /// Game for other options
        /// </summary>
        public static MEGame? PendingGame;

        /// <summary>
        /// The group id for an asi to install to the pending game
        /// </summary>
        public static int PendingInstallASIID;

        /// <summary>
        /// The version for an asi to install to the pending game
        /// </summary>
        public static int PendingInstallASIVersion;

        /// <summary>
        /// If bink should be installed to the pending game
        /// </summary>
        public static bool PendingInstallBink;

        /// <summary>
        /// Feature level (typically moddesc version) of an operation
        /// </summary>
        public static double PendingFeatureLevel;

        /// <summary>
        /// Path to a .json file to compile into an m3m
        /// </summary>
        public static string PendingMergeModCompileManifest;

        /// <summary>
        /// Sets PendingGame to null if there are no items in the pending system that depend on it
        /// </summary>
        public static void ClearGameDependencies()
        {
            if (PendingGame == null)
            {
                // Nothing will work that depends on this
                PendingInstallASIID = 0;
                PendingInstallASIVersion = 0;
                PendingGameBoot = false;
                PendingInstallBink = false;
                PendingMergeDLCCreation = false;
                return;
            }

            // If nothing else needs done, reset PendingGame
            if (PendingGameBoot == false && PendingAutoModInstallPath == null && PendingInstallASIID == 0 && PendingMergeDLCCreation == false)
                PendingGame = null;
        }

        /// <summary>
        /// Returns true if there is a pending game boot request and no other actions that should be performed first are also pending
        /// </summary>
        public static bool CanBootGame()
        {
            if (PendingGameBoot == false || PendingGame == null)
                return false;

            // If stuff is pending you cannot boot the game yet.
            if (PendingAutoModInstallPath != null || PendingInstallASIID > 0 || PendingInstallBink || PendingMergeDLCCreation)
                return false;

            // Nothing is pending
            return true;
        }
    }
}
