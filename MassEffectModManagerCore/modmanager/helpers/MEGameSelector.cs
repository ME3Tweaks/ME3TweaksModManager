using LegendaryExplorerCore.Packages;
using System.Linq;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.helpers
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

        public static MEGame[] GetEnabledGames() => GetGameSelectors().Select(x => x.Game).ToArray();
        public static MEGame[] GetEnabledGamesIncludingLauncher() => GetGameSelectorsIncludingLauncher().Select(x => x.Game).ToArray();
    }
}
