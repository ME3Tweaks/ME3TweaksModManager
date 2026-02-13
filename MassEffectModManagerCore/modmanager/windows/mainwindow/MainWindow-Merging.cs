using LegendaryExplorerCore.Gammtek.Extensions;
using ME3TweaksCore.Localization;
using ME3TweaksCore.ME3Tweaks.M3Merge;
using ME3TweaksCore.ME3Tweaks.M3Merge.Bio2DATable;
using ME3TweaksCore.ME3Tweaks.M3Merge.Game2Email;
using ME3TweaksCore.ME3Tweaks.M3Merge.GlobalShader;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols;
using System.Windows;
using System.Windows.Input;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Partial class for MainWindow - Merge DLC and coalesced file management
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Command to run target merge for a specific game
        /// </summary>
        public ICommand RunTargetMergeCommand { get; set; }

        /// <summary>
        /// Initializes merge-related commands
        /// </summary>
        private void LoadMergeCommands()
        {
            RunTargetMergeCommand = new RelayCommand(RunTargetMerge);
        }

        /// <summary>
        /// Runs target merge on the given game
        /// </summary>
        /// <param name="obj">Contains MEGame enum value that converts to a target</param>
        private void RunTargetMerge(object obj)
        {
            if (obj is MEGame game)
            {
                var target = GetCurrentTarget(game);
                if (target != null)
                {
                    var pr = new PanelResult();
                    pr.AddTargetMerges(target);
                    HandlePanelResult(pr);
                }
                else
                {
                    M3Log.Error(@"RunTargetMerge game target was null! This shouldn't be possible");
                }
            }
        }

        /// <summary>
        /// Handles target merge for changes that go into the basegame (and not M3MergeDLC).
        /// </summary>
        /// <param name="result">Panel result to handle</param>
        private void HandleBasegameTargetMerges(PanelResult result)
        {

            foreach (var v in result.TargetsToPlotManagerSync)
            {
                SyncPlotManagerForTarget(v);
            }

            foreach (var v in result.TargetsToLE1Merge)
            {
                MergeLE1CoalescedForTarget(v);
                MergeLE12DAsForTarget(v);
            }

            foreach (var v in result.TargetsToGlobalShaderMerge)
            {
                RunShaderMergeForTarget(v);
            }
        }

        /// <summary>
        /// Handles the creation of the M3MergeDLC and any merges that generate content in it.
        /// </summary>
        /// <param name="result">Panel result to handle</param>
        private void HandleDLCTargetMerges(PanelResult result)
        {
            if (!Settings.SessionOnly_SuppressDLCMerge)
            {
                var targetMergeMapping = new Dictionary<GameTarget, M3MergeDLC>();
                if (result.NeedsMergeDLC)
                {
                    // Remove any if existing.
                    foreach (var mergeTarget in result.GetMergeTargets())
                    {
                        M3MergeDLC.RemoveMergeDLC(mergeTarget);

                        var mergeDLC = new M3MergeDLC(mergeTarget);
                        targetMergeMapping[mergeTarget] = mergeDLC;

                        // Generate a new one - IF NECESSARY!
                        // This is so if user deletes merge DLC it doesn't re-create itself immediately even if it's not necessary, e.g. user removed all merge DLC-eligible items.

                        bool needsGenerated = !Settings.SessionOnly_SuppressDLCMerge && (SQMOutfitMerge.NeedsMerged(mergeTarget) || ME2EmailMerge.NeedsMergedGame2(mergeTarget));
                        if (needsGenerated)
                        {
                            try
                            {
                                mergeDLC.GenerateMergeDLC();
                            }
                            catch (Exception e)
                            {
                                M3Log.Exception(e, @"Error generating ME3Tweaks Merge DLC: ");
                                // This should have a dialog here, right?
                                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_errorGeneratingMergeDLC, e.Message), M3L.GetString(M3L.string_errorGeneratingMergeDLC), MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }


                foreach (var v in result.TargetsToSquadmateMergeSync)
                {
                    ShowRunAndDone(
                        (config) =>
                            SQMOutfitMerge.RunSquadmateOutfitMerge(targetMergeMapping[v], config.UpdateTitle),
                        LC.GetString(LC.string_synchronizingSquadmateOutfits),
                        M3L.GetString(M3L.string_synchronizedSquadmateOutfits),
                        null);
                }

                foreach (var v in result.TargetsToEmailMergeSync)
                {
                    ShowRunAndDone(
                        (config) => ME2EmailMerge.RunGame2EmailMerge(targetMergeMapping[v], config.UpdateTitle),
                        M3L.GetString(M3L.string_synchronizingEmails),
                        M3L.GetString(M3L.string_synchronizedEmails),
                        null);
                }
            }
        }


        /// <summary>
        /// Synchronizes the plot manager for the specified target
        /// </summary>
        /// <param name="target">The game target to sync the plot manager for</param>
        private void SyncPlotManagerForTarget(GameTarget target)
        {
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"SyncPlotManager",
                M3L.GetString(M3L.string_interp_syncingPlotManagerForGame, target.Game.ToGameName()),
                M3L.GetString(M3L.string_interp_syncedPlotManagerForGame, target.Game.ToGameName()));
            var pmuUI = new PlotManagerUpdatePanel(target);
            pmuUI.Close += (a, b) =>
            {
                BackgroundTaskEngine.SubmitJobCompletion(task);
                ReleaseBusyControl();
            };
            ShowBusyControl(pmuUI);
        }

        /// <summary>
        /// Merges LE1 coalesced files for the specified target
        /// </summary>
        /// <param name="target">The game target to merge coalesced files for</param>
        private void MergeLE1CoalescedForTarget(GameTarget target)
        {
            if (!Settings.EnableLE1CoalescedMerge)
            {
                M3Log.Warning(@"Cannot perform LE1 Coalesced Merge: feature is disabled by user request");
                return;
            }

            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"MergeLE1Coalesced", M3L.GetString(M3L.string_mergingCoalescedFiles),
                M3L.GetString(M3L.string_mergedCoalescedFiles));
            var coalMergePanel = new LE1CoalescedMergePanel(target);
            coalMergePanel.Close += (a, b) =>
            {
                BackgroundTaskEngine.SubmitJobCompletion(task);
                ReleaseBusyControl();
            };
            ShowBusyControl(coalMergePanel);
        }

        /// <summary>
        /// Merges LE1 2DA files for the specified target
        /// </summary>
        /// <param name="target">The game target to merge 2DA files for</param>
        private void MergeLE12DAsForTarget(GameTarget target)
        {
            if (!Settings.EnableLE12DAMerge)
            {
                M3Log.Warning(@"Cannot perform LE1 2DA Merge: feature is disabled by user request");
                return;
            }

            ShowRunAndDone((config) => Bio2DAMerge.RunBio2DAMerge(target),
                M3L.GetString(M3L.string_merging2DATables),
                M3L.GetString(M3L.string_merged2DATables),
                null,
                x =>
                {
                    if (x != null)
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorMerging2DAX, x.Message), M3L.GetString(M3L.string_error), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
        }

        /// <summary>
        /// Runs global shader merge for the specified target
        /// </summary>
        /// <param name="target">The game target to merge global shaders for</param>
        private void RunShaderMergeForTarget(GameTarget target)
        {
            ShowRunAndDone((config) => GlobalShaderMerge.RunShaderMerge(target, true),
                M3L.GetString(M3L.string_mergingGlobalShaders),
                M3L.GetString(M3L.string_mergedGlobalShaders),
                null,
                x =>
                {
                    if (x != null)
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorMergingGlobalShadersXMessage, x.Message), M3L.GetString(M3L.string_error), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
        }
    }
}
