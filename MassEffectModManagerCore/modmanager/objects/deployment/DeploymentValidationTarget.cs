using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment
{
    /// <summary>
    /// Object that contains info about the validation targets for a mod. Only one of these can exist per game
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class DeploymentValidationTarget
    {
        public MEGame Game { get; }
        public GameTarget SelectedTarget { get; set; }
        public string HeaderString { get; }
        public ObservableCollectionExtended<GameTarget> AvailableTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ArchiveDeploymentPanel DeploymentPanelHost { get; set; }

        public DeploymentValidationTarget(ArchiveDeploymentPanel deploymentPanelHost, MEGame game, IEnumerable<GameTargetWPF> targets)
        {
            DeploymentPanelHost = deploymentPanelHost;
            Game = game;
            HeaderString = M3L.GetString(M3L.string_interp_gamenameValidationTarget, game.ToGameName());
            foreach (var t in targets)
            {
                // 04/21/2023: LE targets can use texture modded
                // targets as we check for MEM and MEM only does
                // TFC appending
                if (t.Game.IsOTGame() && t.TextureModded)
                {
                    M3Log.Warning($@"Target is texture modded, cannot be used for validation: {t.TargetPath}, skipping...");
                    continue;
                }
                AvailableTargets.Add(t);
            }
            SelectedTarget = AvailableTargets.FirstOrDefault();
        }


        public void OnSelectedTargetChanged(object before, object after)
        {
            if (before != null)
            {
                //Target has changed
                DeploymentPanelHost.OnValidationTargetChanged((after as GameTarget).Game);
            }
        }
    }
}
