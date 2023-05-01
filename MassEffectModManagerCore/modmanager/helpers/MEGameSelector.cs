using System.Linq;
using LegendaryExplorerCore.Packages;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.helpers
{
    [AddINotifyPropertyChangedInterface]
    public class MEGameSelector
    {
        public bool IsSelected { get; set; }
        public MEGame Game { get; set; }

        public MEGameSelector(MEGame game)
        {
            Game = game;
        }

        private static readonly MEGame[] allSupportedGames = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3, MEGame.LELauncher };

        // EnabledGeneration // for searching

        /// <summary>
        /// Does not include LE launcher!
        /// </summary>
        /// <returns></returns>
        public static MEGameSelector[] GetGameSelectors() => allSupportedGames.Where(x => x != MEGame.LELauncher && x.IsEnabledGeneration()).Select(x => new MEGameSelector(x)).ToArray();
        public static MEGameSelector[] GetGameSelectorsIncludingLauncher() => allSupportedGames.Where(x => x.IsEnabledGeneration()).Select(x => new MEGameSelector(x)).ToArray();

        /// <summary>
        /// Gets list of MEGame values that are currently enabled for use in Mod Manager
        /// </summary>
        /// <returns></returns>
        public static MEGame[] GetEnabledGames() => GetGameSelectors().Select(x => x.Game).ToArray();
        public static MEGame[] GetEnabledGamesIncludingLauncher() => GetGameSelectorsIncludingLauncher().Select(x => x.Game).ToArray();

        /// <summary>
        /// If the game is enabled in M3 by Generational settings
        /// </summary>
        /// <param name="argGame"></param>
        /// <returns></returns>
        public static bool IsGenerationEnabledGame(MEGame game)
        {
            if (game.IsOTGame())
                return Settings.GenerationSettingOT;
            if (game.IsLEGame() || game == MEGame.LELauncher)
                return Settings.GenerationSettingLE;
            return false;
        }
    }
}
