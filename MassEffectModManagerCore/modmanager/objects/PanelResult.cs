using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Panel IDs for launching panels
    /// </summary>
    public enum EPanelID
    {
        ASI_MANAGER,
        NXM_CONFIGURATOR,
        BACKUP_CREATOR
    }
    /// <summary>
    /// Object that holds information about what to do when a panel closes
    /// </summary>
    public class PanelResult
    {
        /// <summary>
        /// The last selected target in the panel
        /// </summary>
        public GameTargetWPF SelectedTarget { get; set; }
        /// <summary>
        /// Tool to launch after this panel has closed
        /// </summary>
        public string ToolToLaunch { get; set; }

        /// <summary>
        /// Targets to plot manager sync after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTargetWPF> TargetsToPlotManagerSync { get; } = new();
        /// <summary>
        /// Targets to squadmate merge sync when this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTargetWPF> TargetsToSquadmateMergeSync { get; } = new();
        /// <summary>
        /// Targets to email merge sync when this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTargetWPF> TargetsToEmailMergeSync { get; } = new();

        /// <summary>
        /// Targets to TOC after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTargetWPF> TargetsToAutoTOC { get; } = new();

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

        /// <summary>
        /// If panel had exception it will be available here
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// If this result needs any merges performed
        /// </summary>
        public bool NeedsMergeDLC => TargetsToEmailMergeSync.Any() || TargetsToSquadmateMergeSync.Any();

        /// <summary>
        /// Merges values from this panel into the specified one
        /// </summary>
        /// <param name="batchPanelResult"></param>
        public void MergeInto(PanelResult batchPanelResult)
        {
            batchPanelResult.TargetsToSquadmateMergeSync.AddRange(TargetsToSquadmateMergeSync);
            batchPanelResult.TargetsToEmailMergeSync.AddRange(TargetsToEmailMergeSync);
            batchPanelResult.TargetsToPlotManagerSync.AddRange(TargetsToPlotManagerSync);
            batchPanelResult.TargetsToAutoTOC.AddRange(TargetsToAutoTOC);
            if (SelectedTarget != null) batchPanelResult.SelectedTarget = SelectedTarget;
            if (Error != null) batchPanelResult.Error = Error;
            if (PanelToOpen != null) batchPanelResult.PanelToOpen = PanelToOpen;
            if (ReloadTargets) batchPanelResult.ReloadTargets = ReloadTargets;
            if (ReloadMods) batchPanelResult.ReloadMods = ReloadMods;
            if (ModToHighlightOnReload != null) batchPanelResult.ModToHighlightOnReload = ModToHighlightOnReload;
            if (ToolToLaunch != null) batchPanelResult.ToolToLaunch = ToolToLaunch;
        }

        /// <summary>
        /// Gets a list of DLC merge mod targets for this result
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GameTargetWPF> GetMergeTargets()
        {
            return TargetsToEmailMergeSync.Concat(TargetsToSquadmateMergeSync).Distinct();
        }
    }
}
