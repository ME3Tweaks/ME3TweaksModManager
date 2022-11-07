using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.objects.deployment.checks;
using ME3TweaksModManager.modmanager.objects.mod;
using PropertyChanged;
using System.Linq;
using System.Windows.Input;

namespace ME3TweaksModManager.modmanager.objects.deployment
{
    /// <summary>
    ///  Class that checks a single mod for issues
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class EncompassingModDeploymentCheck
    {
        public ObservableCollectionExtended<DeploymentChecklistItem> DeploymentChecklistItems { get; } = new ObservableCollectionExtended<DeploymentChecklistItem>();
        public DeploymentValidationTarget DepValidationTarget { get; set; }
        /// <summary>
        /// Validation target for this mod
        /// </summary>
        internal GameTargetWPF internalValidationTarget { get; set; }
        public bool CheckCancelled { get; set; }
        public bool CanReRun { get; set; }
        public Mod ModBeingDeployed { get; }
        public ICommand RerunChecksCommand { get; }

        /// <summary>
        /// Invoked when CheckCancelled changes.
        /// </summary>
        public void OnCheckCancelledChanged()
        {
            if (CheckCancelled)
            {
                foreach (var checkItem in DeploymentChecklistItems)
                {
                    checkItem.SetAbandoned();
                }
            }
        }

        /// <summary>
        /// Instantiates a new mod deployment check object that runs a series of checks on a mod for deployment.
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="dvt"></param>
        public EncompassingModDeploymentCheck(Mod mod, DeploymentValidationTarget dvt)
        {
            ModBeingDeployed = mod;
            DepValidationTarget = dvt;
            internalValidationTarget = dvt.SelectedTarget;

            // Commands
            RerunChecksCommand = new GenericCommand(RunChecksWrapper, CanRerunCheck);

            // Checks
            MetadataChecks.AddMetadataChecks(this);
            LanguageChecks.AddLanguageChecks(this);
            PackageChecks.AddPackageChecks(this);
            AudioChecks.AddAudioChecks(this);
            TextureChecks.AddTextureChecks(this);
            MiscChecks.AddMiscChecks(this);
        }

        public void RunChecks()
        {
            CanReRun = false;
            foreach (var checkItem in DeploymentChecklistItems)
            {
                checkItem.Reset();
            }

            foreach (var checkItem in DeploymentChecklistItems)
            {
                if (CheckCancelled) continue;
                checkItem.ExecuteValidationFunction();
            }

            CanReRun = CanRerunCheck();
        }

        private bool CanRerunCheck()
        {
            return DeploymentChecklistItems.All(x => x.CheckDone) && DeploymentChecklistItems.Any(x => x.HasMessage);
        }

        private void RunChecksWrapper()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ModChecksThread");
            nbw.DoWork += (sender, args) => RunChecks();
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// Sets all checks to the 'abandoned' state, as in they will not run due to previous blocking item
        /// </summary>
        public void SetAbandoned()
        {
            foreach (var check in DeploymentChecklistItems)
            {
                check.SetAbandoned();
            }
        }

    }
}
