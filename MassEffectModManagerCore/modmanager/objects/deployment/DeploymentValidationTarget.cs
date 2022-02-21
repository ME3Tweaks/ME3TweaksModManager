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
        public GameTargetWPF SelectedTarget { get; set; }
        public string HeaderString { get; }
        public ObservableCollectionExtended<GameTargetWPF> AvailableTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();
        public ArchiveDeployment DeploymentHost { get; set; }

        public DeploymentValidationTarget(ArchiveDeployment deploymentHost, MEGame game, IEnumerable<GameTargetWPF> targets)
        {
            DeploymentHost = deploymentHost;
            Game = game;
            HeaderString = M3L.GetString(M3L.string_interp_gamenameValidationTarget, game.ToGameName());
            foreach (var t in targets)
            {
                if (t.TextureModded)
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
                DeploymentHost.OnValidationTargetChanged((after as GameTarget).Game);
            }
        }
    }
}
