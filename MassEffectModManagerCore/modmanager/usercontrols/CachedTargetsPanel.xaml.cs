using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Panel for managing cached game targets. Allows users to view, reload, remove, and restore invalid targets.
    /// Tracks changes to target states and signals when targets need to be reloaded in the main window.
    /// </summary>
    public partial class CachedTargetsPanel : MMBusyPanelBase
    {
        /// <summary>
        /// Tracks if the panel has been initialized to prevent re-initialization when returning from sub-panels
        /// </summary>
        private bool _hasInitialized;
        
        /// <summary>
        /// Stores the initial validity state of all targets (Game|Path -> IsValid) for change detection on close
        /// </summary>
        private Dictionary<string, bool> _initialTargetStates;

        public CachedTargetsPanel()
        {
            DataContext = this;
            LoadCommands();
        }

        /// <summary>
        /// The currently selected target in the list
        /// </summary>
        public TargetCacheInfo SelectedTarget { get; set; }

        /// <summary>
        /// Command to reload an invalid target to check if it has become valid
        /// </summary>
        public ICommand ReloadTargetCommand { get; set; }
        
        /// <summary>
        /// Command to remove a target from the cache
        /// </summary>
        public ICommand RemoveTargetCommand { get; set; }
        
        /// <summary>
        /// Command to restore an invalid target from backup
        /// </summary>
        public ICommand RestoreTargetCommand { get; set; }

        /// <summary>
        /// Initializes all commands for the panel
        /// </summary>
        private void LoadCommands()
        {
            ReloadTargetCommand = new GenericCommand(ReloadTarget, CanReloadTarget);
            RemoveTargetCommand = new GenericCommand(RemoveTarget, CanRemoveTarget);
            RestoreTargetCommand = new GenericCommand(RestoreTarget, CanRestoreTarget);
        }

        /// <summary>
        /// Determines if the reload command can execute. Only invalid targets can be reloaded.
        /// </summary>
        /// <returns>True if a target is selected and invalid</returns>
        private bool CanReloadTarget()
        {
            return SelectedTarget != null && !SelectedTarget.IsValid;
        }

        /// <summary>
        /// Determines if the restore command can execute. Only invalid targets with a valid Target object can be restored.
        /// </summary>
        /// <returns>True if a target is selected, invalid, and has a Target object</returns>
        private bool CanRestoreTarget()
        {
            return SelectedTarget != null 
                && !SelectedTarget.IsValid 
                && BackupService.GetBackupStatus(SelectedTarget.Game)?.BackedUp == true
                && Directory.Exists(SelectedTarget.TargetPath);
        }

        /// <summary>
        /// Determines if the remove command can execute. Targets that are registry active cannot be removed.
        /// </summary>
        /// <returns>True if a target is selected and not registry active</returns>
        private bool CanRemoveTarget()
        {
            if (SelectedTarget == null) return false;
            if (SelectedTarget.Target != null)
            {
                return !SelectedTarget.Target.RegistryActive;
            }
            return true;
        }

        /// <summary>
        /// Reloads the selected target to check if it has become valid. Updates the target's validity state
        /// and sets ReloadTargets result if the target is now valid.
        /// </summary>
        private void ReloadTarget()
        {
            if (SelectedTarget == null) return;

            var position = CachedTargets.IndexOf(SelectedTarget);
            var game = SelectedTarget.Game;
            var path = SelectedTarget.TargetPath;

            CachedTargets.RemoveAt(position);

            // Try to reload the target
            if (Directory.Exists(path))
            {
                var target = new GameTargetWPF(game, path, false);
                var failureReason = target.ValidateTarget();
                
                var newTargetInfo = new TargetCacheInfo(
                    game, 
                    path, 
                    failureReason == null, 
                    failureReason, 
                    failureReason == null ? target : null);
                
                CachedTargets.Insert(position, newTargetInfo);
                SelectedTarget = newTargetInfo;

                if (failureReason == null)
                {
                    // Target is now valid, trigger a target reload
                    Result.ReloadTargets = true;
                }
            }
            else
            {
                // Directory still doesn't exist
                var newTargetInfo = new TargetCacheInfo(game, path, false, "Invalid target: Directory does not exist", null);
                CachedTargets.Insert(position, newTargetInfo);
                SelectedTarget = newTargetInfo;
            }
        }

        /// <summary>
        /// Removes the selected target from the cache after user confirmation.
        /// Sets ReloadTargets result to trigger a target refresh in the main window.
        /// </summary>
        private void RemoveTarget()
        {
            if (SelectedTarget == null) return;

            var result = M3L.ShowDialog(window,
                $"Are you sure you want to remove this target from the cache?\n\nGame: {SelectedTarget.Game}\nPath: {SelectedTarget.TargetPath}\n\nThis will not delete any game files.",
                "Remove Cached Target",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                M3TargetCache.RemoveCachedTarget(SelectedTarget.Game, SelectedTarget.TargetPath);
                CachedTargets.Remove(SelectedTarget);
                Result.ReloadTargets = true;
            }
        }

        /// <summary>
        /// Initiates a restore operation for the selected invalid target. Shows the AutoGameRestorePanel
        /// and reloads the target if the restore succeeds.
        /// </summary>
        private void RestoreTarget()
        {
            var target = new GameTargetWPF(SelectedTarget.Game, SelectedTarget.TargetPath, false, skipInit: true);

            var restorePanel = new AutoGameRestorePanel(target);
            restorePanel.Close += (sender, args) =>
            {
                // When restore panel closes, reload the target
                if (restorePanel.RestoreSucceeded)
                {
                    ReloadTarget();
                }
            };

            // Show the restore panel, swap immediately to it
            mainwindow.ShowBusyControl(restorePanel, true);
        }

        /// <summary>
        /// Observable collection of all cached targets displayed in the panel
        /// </summary>
        public ObservableCollectionExtended<TargetCacheInfo> CachedTargets { get; } = new ObservableCollectionExtended<TargetCacheInfo>();

        /// <summary>
        /// Event handler for the close button click
        /// </summary>
        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        /// <summary>
        /// Called when the panel is closing. Compares the current target states with initial states
        /// and sets ReloadTargets result if any changes are detected (removed targets or validity changes).
        /// </summary>
        protected override void OnClosing(DataEventArgs e)
        {
            // Check if any target states have changed
            if (_initialTargetStates != null && !Result.ReloadTargets)
            {
                // Build current state
                var currentStates = new Dictionary<string, bool>();
                foreach (var target in CachedTargets)
                {
                    var key = $"{target.Game}|{target.TargetPath}";
                    currentStates[key] = target.IsValid;
                }

                // Check for removed targets
                if (_initialTargetStates.Count != currentStates.Count)
                {
                    Result.ReloadTargets = true;
                }
                // Check for changed validity states
                else
                {
                    foreach (var kvp in _initialTargetStates)
                    {
                        if (!currentStates.TryGetValue(kvp.Key, out var currentValid) || currentValid != kvp.Value)
                        {
                            M3Log.Information(@"Cached target states have changed, reloading targets");
                            Result.ReloadTargets = true;
                            break;
                        }
                    }
                }
            }

            base.OnClosing(e);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when the panel becomes visible. On first visibility, initializes the component and loads targets.
        /// On subsequent visibility (when returning from sub-panels), reloads the current target to reflect any changes.
        /// </summary>
        public override void OnPanelVisible()
        {
            if (!_hasInitialized)
            {
                // Initialization is always done right before panel becomes visible
                // because we don't want binding to occur before then.
                InitializeComponent(); 
                LoadCachedTargets();
                _hasInitialized = true;
            }
            else
            {
                // We're returning from a sub-panel (like AutoGameRestorePanel)
                // Just reload the current target
                if (SelectedTarget != null)
                {
                    ReloadTarget();
                }
            }
        }

        /// <summary>
        /// Loads all cached targets from disk and stores their initial validity states for change detection.
        /// Selects the first target if any are available.
        /// </summary>
        private void LoadCachedTargets()
        {
            // Fetch targets, filter to generations
            var allTargets = M3TargetCache.GetAllCachedTargetInfo();
            var shownTargets = new List<TargetCacheInfo>();
            if (Settings.GenerationSettingOT)
            {
                shownTargets.AddRange(allTargets.Where(x => x.Game.IsOTGame()));
            }
            if (Settings.GenerationSettingLE)
            {
                shownTargets.AddRange(allTargets.Where(x => x.Game.IsLEGame() || x.Game == MEGame.LELauncher));
            }
            CachedTargets.ReplaceAll(shownTargets);
            
            // Store initial state for change detection on close
            _initialTargetStates = new Dictionary<string, bool>();
            foreach (var target in CachedTargets)
            {
                var key = $"{target.Game}|{target.TargetPath}";
                _initialTargetStates[key] = target.IsValid;
            }
            
            if (CachedTargets.Any())
            {
                SelectedTarget = CachedTargets.First();
            }
        }
    }
}
