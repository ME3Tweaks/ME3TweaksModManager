using System.Threading;
using System.Threading.Tasks;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// ASIGameWPF that loads its data (bink status, installed ASI hashing) on a background thread
    /// so the UI does not stall. ME3TweaksCore's ASIGame classes remain synchronous; this class only wraps
    /// how they are driven. Bind the UI to <see cref="SelectedGameTargetWPF"/> instead of CurrentGameTargetWPF
    /// and use <see cref="IsLoading"/> to display a loading indicator.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class ASIGameAsyncWPF : ASIGameWPF
    {
        /// <summary>
        /// If a load or refresh is currently in progress for this game. Data shown for this game should be considered stale/incomplete while this is true.
        /// </summary>
        public bool IsLoading { get; private set; } = true;

        /// <summary>
        /// The target the user has chosen. Changing this after <see cref="LoadAsync"/> has been called
        /// will asynchronously load the data for the new target.
        /// </summary>
        public GameTargetWPF SelectedGameTargetWPF { get; set; }

        // Only one operation touches the underlying (synchronous, non-threadsafe) ASIGame at a time
        private readonly SemaphoreSlim operationLock = new(1, 1);

        // Number of operations queued or running. Only modified on the thread that calls the async methods (UI thread)
        private int activeOperations;

        // Incremented for each load request; a load that was superseded by a newer request is skipped
        private int loadVersion;

        // If a load has been requested. Before this, changing SelectedGameTargetWPF doesn't cause a load, so the owner can control load order.
        private bool loadRequested;

        public ASIGameAsyncWPF(MEGame game, List<GameTargetWPF> targets) : base(game, targets)
        {
        }

        // Do not delete - fody will link this
        public void OnSelectedGameTargetWPFChanged()
        {
            if (loadRequested)
            {
                _ = LoadAsync();
            }
        }

        /// <summary>
        /// Loads the data for <see cref="SelectedGameTargetWPF"/> on a background thread.
        /// </summary>
        public async Task LoadAsync()
        {
            loadRequested = true;
            var version = ++loadVersion;
            var target = SelectedGameTargetWPF;
            await RunExclusiveAsync(() =>
            {
                if (version == loadVersion) // Not superseded by a newer request
                {
                    ApplyTarget(target);
                }
            });
        }

        /// <summary>
        /// Rebuilds the ASI list for the current target on a background thread.
        /// </summary>
        public Task RefreshASIStatesAsync()
        {
            return RunExclusiveAsync(RefreshASIStates);
        }

        private async Task RunExclusiveAsync(Action action)
        {
            activeOperations++;
            IsLoading = true;
            try
            {
                await operationLock.WaitAsync();
                try
                {
                    await Task.Run(action);
                }
                finally
                {
                    operationLock.Release();
                }
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $@"Error loading ASI information for {Game}:");
            }
            finally
            {
                activeOperations--;
                IsLoading = activeOperations > 0;
            }
        }

        private void ApplyTarget(GameTargetWPF target)
        {
            // Setting the target causes ASIGame to refresh, but only if the target actually changes.
            var willRefresh = !Equals(CurrentGameTarget, target);
            CurrentGameTargetWPF = target;
            if (!willRefresh && target != null)
            {
                RefreshBinkStatus();
                RefreshASIStates();
            }
        }
    }
}
