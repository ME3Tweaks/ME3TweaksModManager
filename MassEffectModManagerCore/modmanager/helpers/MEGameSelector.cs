using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static MEGameSelector[] GetGameSelectors() => allSupportedGames.Where(x => x != MEGame.LELauncher && x.IsEnabledGeneration()).Select(x => new MEGameSelector(x)).ToArray();
        public static MEGameSelector[] GetGameSelectorsIncudingLauncher() => allSupportedGames.Where(x => x.IsEnabledGeneration()).Select(x => new MEGameSelector(x)).ToArray();
    }
}
