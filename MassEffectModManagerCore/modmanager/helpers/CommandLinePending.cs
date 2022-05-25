using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

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
        /// 
        /// </summary>
        public static string PendingAutoModInstallPath;
        
        /// <summary>
        /// If game should be booted after all other options have been performed
        /// </summary>
        public static bool PendingGameBoot;

        /// <summary>
        /// Game for other options
        /// </summary>
        public static MEGame? PendingGame;

        /// <summary>
        /// The group id for an asi to install to the pending game
        /// </summary>
        public static int PendingInstallASIID;

        /// <summary>
        /// If bink should be installed to the pending game
        /// </summary>
        public static bool PendingInstallBink;

        /// <summary>
        /// Sets PendingGame to null if there are no items in the pending system that depend on it
        /// </summary>
        public static void ClearGameDependencies()
        {
            if (PendingGame == null)
            {
                // Nothing will work that depends on this
                PendingInstallASIID = 0;
                PendingGameBoot = false;
                PendingInstallBink = false;
                return;
            }

            // If nothing else needs done, reset PendingGame
            if (PendingGameBoot == false && PendingAutoModInstallPath == null && PendingInstallASIID == 0)
                PendingGame = null;
        }

        /// <summary>
        /// Returns true if there is a pending game boot request and no other actions that should be performed first are also pending
        /// </summary>
        public static bool CanBootGame()
        {
            if (PendingGameBoot == false || PendingGame == null)
                return false;
            return PendingAutoModInstallPath != null || PendingInstallASIID > 0 || PendingInstallBink;
        }
    }
}
