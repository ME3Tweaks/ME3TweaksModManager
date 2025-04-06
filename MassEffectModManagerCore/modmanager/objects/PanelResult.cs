using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge;
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
        public GameTarget SelectedTarget { get; set; }
        /// <summary>
        /// Tool to launch after this panel has closed
        /// </summary>
        public string ToolToLaunch { get; set; }

        /// <summary>
        /// Targets to plot manager sync after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToPlotManagerSync { get; } = new();

        /// <summary>
        /// LE1 targets to run Coalesced merge on after this panel has been closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToLE1Merge { get; } = new();

        /// <summary>
        /// Targets to squadmate merge sync when this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToSquadmateMergeSync { get; } = new();
        /// <summary>
        /// Targets to email merge sync when this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToEmailMergeSync { get; } = new();

        /// <summary>
        /// Targets to TOC after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToAutoTOC { get; } = new();

        /// <summary>
        /// Targets to run GlobalShaderMerge on after this panel has closed
        /// </summary>
        public ConcurrentHashSet<GameTarget> TargetsToGlobalShaderMerge { get; } = new();

        /// <summary>
        /// Mods that should have updates checked for when the panel result is handled
        /// </summary>
        public ConcurrentHashSet<Mod> ModsToCheckForUpdates { get; } = new();

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


        public bool SpawnedFromUnattached { get; set; }

        /// <summary>
        /// If this result needs any merges performed
        /// </summary>
        public bool NeedsMergeDLC => TargetsToEmailMergeSync.Any() || TargetsToSquadmateMergeSync.Any();

        /// <summary>
        /// What moddesc files have been modified by this panel
        /// </summary>
        public List<string> ModifiedModdescFiles { get; } = new List<string>(0);

        /// <summary>
        /// Merges values from this panel into the specified one
        /// </summary>
        /// <param name="batchPanelResult"></param>
        public void MergeInto(PanelResult batchPanelResult)
        {
            batchPanelResult.TargetsToSquadmateMergeSync.AddRange(TargetsToSquadmateMergeSync);
            batchPanelResult.TargetsToEmailMergeSync.AddRange(TargetsToEmailMergeSync);
            batchPanelResult.TargetsToPlotManagerSync.AddRange(TargetsToPlotManagerSync);
            batchPanelResult.TargetsToLE1Merge.AddRange(TargetsToLE1Merge);
            batchPanelResult.TargetsToAutoTOC.AddRange(TargetsToAutoTOC);
            batchPanelResult.TargetsToGlobalShaderMerge.AddRange(TargetsToGlobalShaderMerge);
            batchPanelResult.ModsToCheckForUpdates.AddRange(ModsToCheckForUpdates);
            if (SelectedTarget != null) batchPanelResult.SelectedTarget = SelectedTarget;
            if (Error != null) batchPanelResult.Error = Error;
            if (PanelToOpen != null) batchPanelResult.PanelToOpen = PanelToOpen;
            if (ReloadTargets) batchPanelResult.ReloadTargets = ReloadTargets;
            if (ReloadMods) batchPanelResult.ReloadMods = ReloadMods;
            if (ModifiedModdescFiles.Any())
            {
                foreach (var f in ModifiedModdescFiles)
                {
                    if (!batchPanelResult.ModifiedModdescFiles.Contains(f, StringComparer.InvariantCultureIgnoreCase))
                    {
                        batchPanelResult.ModifiedModdescFiles.Add(f);
                    }
                }
            }
            if (ModToHighlightOnReload != null) batchPanelResult.ModToHighlightOnReload = ModToHighlightOnReload;
            if (ToolToLaunch != null) batchPanelResult.ToolToLaunch = ToolToLaunch;
        }

        /// <summary>
        /// Gets a list of targets that have pending items to place into the M3 Merge DLC
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GameTarget> GetMergeTargets()
        {
            // Only email merge and squadmate merge put into merge dlc right now
            return TargetsToEmailMergeSync.Concat(TargetsToSquadmateMergeSync).Distinct();
        }


        /// <summary>
        /// If this result will modify the game, e.g. running a merge on game.
        /// </summary>
        /// <returns></returns>
        public bool DoesResultModifyGame()
        {
            if (TargetsToAutoTOC.Any()) return true;
            if (TargetsToEmailMergeSync.Any()) return true;
            if (TargetsToLE1Merge.Any()) return true;
            if (TargetsToPlotManagerSync.Any()) return true;
            if (TargetsToSquadmateMergeSync.Any()) return true;
            if (TargetsToGlobalShaderMerge.Any()) return true;
            return false;
        }

        /// <summary>
        /// Adds all relevant game-specific merges for the specified target
        /// </summary>
        /// <param name="target"></param>
        public void AddTargetMerges(GameTarget target)
        {
            if (target.Game.SupportsPlotManagerSync())
            {
                TargetsToPlotManagerSync.Add(target);
            }

            if (target.Game == MEGame.LE1)
            {
                TargetsToLE1Merge.Add(target);
            }

            if (target.Game.SupportsAutoTOC())
            {
                TargetsToAutoTOC.Add(target);
            }

            if (target.Game.SupportsSquadmateMerge()) // ME2 is not supported for squadmate merge
            {
                TargetsToSquadmateMergeSync.Add(target);
            }

            if (target.Game.SupportsEmailMerge())
            {
                TargetsToEmailMergeSync.Add(target);
            }

            if (target.Game.IsLEGame())
            {
                TargetsToGlobalShaderMerge.Add(target);
            }
        }
    }
}