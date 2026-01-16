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
    /// Interaction logic for CachedTargetsPanel.xaml
    /// </summary>
    public partial class CachedTargetsPanel : MMBusyPanelBase
    {
        public CachedTargetsPanel()
        {
            DataContext = this;
            LoadCommands();
        }

        public TargetCacheInfo SelectedTarget { get; set; }

        public ICommand ReloadTargetCommand { get; set; }
        public ICommand RemoveTargetCommand { get; set; }

        private void LoadCommands()
        {
            ReloadTargetCommand = new GenericCommand(ReloadTarget, CanReloadTarget);
            RemoveTargetCommand = new GenericCommand(RemoveTarget, CanRemoveTarget);
        }

        private bool CanReloadTarget()
        {
            return SelectedTarget != null && !SelectedTarget.IsValid;
        }

        private bool CanRemoveTarget()
        {
            return SelectedTarget != null;
        }

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
                var newTargetInfo = new TargetCacheInfo(game, path, false, "Directory does not exist", null);
                CachedTargets.Insert(position, newTargetInfo);
                SelectedTarget = newTargetInfo;
            }
        }

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
                M3Utilities.RemoveCachedTarget(SelectedTarget.Game, SelectedTarget.TargetPath);
                CachedTargets.Remove(SelectedTarget);
                Result.ReloadTargets = true;
            }
        }

        public ObservableCollectionExtended<TargetCacheInfo> CachedTargets { get; } = new ObservableCollectionExtended<TargetCacheInfo>();

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            LoadCachedTargets();
        }

        private void LoadCachedTargets()
        {
            var allTargets = M3Utilities.GetAllCachedTargetInfo();
            CachedTargets.ReplaceAll(allTargets);
            
            if (CachedTargets.Any())
            {
                SelectedTarget = CachedTargets.First();
            }
        }
    }
}
