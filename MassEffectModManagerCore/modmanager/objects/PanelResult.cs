using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.modmanager.usercontrols;

namespace MassEffectModManagerCore.modmanager.objects
{

    /// <summary>
    /// Panel IDs for launching panels
    /// </summary>
    public enum EPanelID
    {
        ASI_MANAGER,
    }
    /// <summary>
    /// Object that holds information about what to do when a panel closes
    /// </summary>
    public class PanelResult
    {
        /// <summary>
        /// The last selected target in the panel
        /// </summary>
        public GameTarget SelectedTarget { get; set; }
        /// <summary>
        /// Tool to launch after this panel has closed
        /// </summary>
        public string ToolToLaunch {get;set; }

        /// <summary>
        /// Targets to plot manager sync after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToPlotManagerSync { get; } = new();

        /// <summary>
        /// Targets to TOC after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToAutoTOC { get; } = new();

        /// <summary>
        /// Panel to open after close
        /// </summary>
        public EPanelID? PanelToOpen { get; set; }

        /// <summary>
        /// If targets should be reloaded once this panel has closed
        /// </summary>
        public bool ReloadTargets { get; set; }
        /// <summary>
        /// If mods should reload once this panel has closed
        /// </summary>
        public bool ReloadMods { get; set; }

        /// <summary>
        /// What mod to highlight when mod reload occurs. Only does something if ReloadMods = true
        /// </summary>
        public Mod ModToHighlightOnReload { get; set; }
    }
}
