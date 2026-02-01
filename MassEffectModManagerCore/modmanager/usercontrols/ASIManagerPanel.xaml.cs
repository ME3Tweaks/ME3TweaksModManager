using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.NativeMods.Interfaces;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ASIManagerPanel : MMBusyPanelBase
    {
        public int SelectedTabIndex { get; set; }
        private object SelectedASIObject { get; set; }
        public string SelectedASIDescription { get; set; }
        public string SelectedASISubtext { get; set; }
        public string SelectedASIName { get; set; }
        public bool InstallInProgress { get; set; }
        public string InstallButtonText { get; set; }
        private GameTarget preselectedTarget;
        public ObservableCollectionExtended<ASIGameWPF> Games { get; } = new();


        private void OnInstallInProgressChanged()
        {
            // Keep UI look up to date
            M3Utilities.RefreshBindings();
        }

        /// <summary>
        /// This ASI Manager is a feature ported from ME3CMM and maintains synchronization with Mass Effect 3 Mod Manager's code for 
        /// managing and installing ASIs. ASIs are useful for debugging purposes, which is why this feature is now 
        /// part of ME3Explorer.
        /// </summary>
        public ASIManagerPanel(GameTarget preselectedTarget = null)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"ASI Manager", this);
            M3Log.Information(@"Opening ASI Manager");
            Directory.CreateDirectory(ASIManager.CachedASIsFolder);
            LoadCommands();
            this.preselectedTarget = preselectedTarget;
        }


        public ICommand InstallUninstallCommand { get; private set; }
        public ICommand SourceCodeCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            InstallUninstallCommand = new GenericCommand(InstallUninstallASI, CanInstallASI);
            SourceCodeCommand = new GenericCommand(ViewSourceCode, ManifestASIIsSelected);
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClosePanel() => !InstallInProgress;

        private void ViewSourceCode()
        {
            if (SelectedASIObject is ASIMod asi)
            {
                M3Utilities.OpenWebpage(asi.LatestVersion.SourceCodeLink);
            }
            else if (SelectedASIObject is IKnownInstalledASIMod kasi && kasi.AssociatedManifestItem != null)
            {
                M3Utilities.OpenWebpage(kasi.AssociatedManifestItem.SourceCodeLink);
            }
        }

        private async void InstallUninstallASI()
        {
            if (SelectedASIObject is IInstalledASIMod instASI)
            {
                //Unknown ASI
                if (instASI is IKnownInstalledASIMod kam && kam.Outdated)
                {
                    if (kam.AssociatedManifestItem.OwningMod.LatestVersion == null)
                    {
                        // Can only uninstall. Mod Manager does not support installing this from the app, only command line
                        kam.Uninstall();
                        RefreshASIStates(instASI.Game);
                    }
                    else
                    {
                        // Await task on background thread 
                        await Task.Run(() => internalInstallASI(kam.AssociatedManifestItem.OwningMod.LatestVersionIncludingHidden));
                    }
                }
                else
                {
                    instASI.Uninstall();
                    RefreshASIStates(instASI.Game);
                }

            }
            else if (SelectedASIObject is ASIMod asi)
            {
                // Await task on background thread 
                await Task.Run(() => internalInstallASI(asi.LatestVersion));
            }
        }

        private async Task internalInstallASI(ASIModVersion asi)
        {
            var originalSelectedObject = SelectedASIObject;
            InstallInProgress = true;
            var target = Games.First(x => x.Game == asi.Game);
            try
            {
                // We don't read the result cause we don't really care
                await Task.Run(() => ASIManager.InstallASIToTarget(asi, target.CurrentGameTarget));
            }
            catch (Exception ex)
            {
                M3Log.Error($@"Exception installing ASI: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_anErrorOccurredInstallingTheASI, ex.Message), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                InstallInProgress = false;
                RefreshASIStates(asi.Game);
                SelectASI(originalSelectedObject);
                UpdateSelectionTexts(SelectedASIObject);
                M3Utilities.RefreshBindings();
            }
        }

        private void SelectASI(object obj)
        {
            // When uninstalling it's okay to not reselect it
            if (obj is ASIMod am)
            {
                // ASI should be going to the installed state
                var asiGame = Games.FirstOrDefault(x => x.Game == am.Game);
                if (asiGame != null)
                {
                    asiGame.SelectedASI = asiGame.DisplayedASIMods.OfType<IKnownInstalledASIMod>().FirstOrDefault(x => x.AssociatedManifestItem.OwningMod.UpdateGroupId == am.UpdateGroupId);
                    SelectedASIObject = asiGame.SelectedASI;
                }
            }
        }

        private bool CanInstallASI()
        {
            if (SelectedASIObject == null) return false;
            if (InstallInProgress) return false;
            if (SelectedASIObject is ASIMod am)
            {
                return !MUtilities.IsGameRunning(am.Game) && (Games.FirstOrDefault(x => x.Game == am.Game)?.GameTargets.Any() ?? false);
            }

            if (SelectedASIObject is InstalledASIMod iam)
            {
                return !MUtilities.IsGameRunning(iam.Game) && (Games.FirstOrDefault(x => x.Game == iam.Game)?.GameTargets.Any() ?? false);
            }

            return false;
        }

        private bool ManifestASIIsSelected() => SelectedASIObject is ASIMod || SelectedASIObject is IKnownInstalledASIMod;

        private void RefreshASIStates(MEGame gameToRefresh = MEGame.Unknown)
        {
            foreach (var game in Games)
            {
                if (gameToRefresh == MEGame.Unknown || gameToRefresh == game.Game)
                    game.RefreshASIStates();
            }
        }

        private void ASIManagerLists_SelectedChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                UpdateSelectionTexts(e.AddedItems[0]);
                SelectedASIObject = e.AddedItems[0];
            }
            else
            {
                UpdateSelectionTexts(null);
                SelectedASIObject = null;
            }
        }

        private void UpdateSelectionTexts(object v)
        {
            if (v is ASIMod asiMod)
            {
                SelectedASIDescription = asiMod.LatestVersion.DescriptionFormatted;
                SelectedASIName = asiMod.LatestVersion.Name;
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, asiMod.LatestVersion.Author, asiMod.LatestVersion.Version);
                subtext += Environment.NewLine;
                //if (asiMod.UIOnly_Outdated)
                //{
                //    subtext += M3L.GetString(M3L.string_installedOutdated);
                //    InstallButtonText = M3L.GetString(M3L.string_updateASI);
                //}
                //else if (asiMod.UIOnly_Installed)
                //{
                //    subtext += M3L.GetString(M3L.string_installedUpToDate);
                //    InstallButtonText = M3L.GetString(M3L.string_uninstallASI);

                //}
                //else
                {
                    subtext += M3L.GetString(M3L.string_notInstalled);
                    InstallButtonText = M3L.GetString(M3L.string_installASI);
                }

                SelectedASISubtext = subtext;
            }
            else if (v is IKnownInstalledASIMod kaim)
            {
                SelectedASIDescription = kaim.AssociatedManifestItem.DescriptionFormatted;
                SelectedASIName = kaim.AssociatedManifestItem.Name;
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, kaim.AssociatedManifestItem.Author, kaim.AssociatedManifestItem.Version);
                subtext += Environment.NewLine;
                if (kaim.Outdated)
                {
                    if (kaim.AssociatedManifestItem.OwningMod.LatestVersion != null)
                    {
                        subtext += M3L.GetString(M3L.string_installedOutdated);
                        InstallButtonText = M3L.GetString(M3L.string_updateASI);
                    }
                    else
                    {
                        // Not managed by M3 UI
                        subtext += M3L.GetString(M3L.string_installedOutdated);
                        InstallButtonText = M3L.GetString(M3L.string_uninstallASI);
                    }
                }
                else
                {
                    subtext += M3L.GetString(M3L.string_installedUpToDate);
                    InstallButtonText = M3L.GetString(M3L.string_uninstallASI);
                }
                SelectedASISubtext = subtext;
            }
            else if (v is IUnknownInstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = nonManifestAsiMod.DllDescription;
                SelectedASIName = nonManifestAsiMod.UnmappedFilename;
                SelectedASISubtext = M3L.GetString(M3L.string_SSINotPresentInManifest);
                InstallButtonText = M3L.GetString(M3L.string_uninstallASI);
            }
            else
            {
                SelectedASIDescription = "";
                SelectedASIName = M3L.GetString(M3L.string_selectAnASIToViewOptions);
                SelectedASISubtext = "";
                SelectedASIObject = null;
                InstallButtonText = M3L.GetString(M3L.string_noASISelected);
            }
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClosePanel())
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            //This has to be done here as mainwindow will not be available until this is called
            MEGame[] gameEnum = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 };
            int index = 0;
            foreach (var game in gameEnum)
            {
                if (!Settings.GenerationSettingOT && game.IsOTGame()) continue;
                if (!Settings.GenerationSettingLE && game.IsLEGame()) continue;
                var targets = mainwindow.InstallationTargets.Where(x => x.Game == game).ToList();
                ASIGameWPF asiGame = null;
                if (targets.Count > 0)
                {
                    asiGame = new ASIGameWPF(game, targets);
                    Games.Add(asiGame);

                    if (preselectedTarget != null && preselectedTarget.Game == game)
                    {
                        asiGame.CurrentGameTargetWPF = (GameTargetWPF)preselectedTarget;
                        SelectedTabIndex = index;
                    }
                    else
                    {
                        asiGame.CurrentGameTargetWPF = asiGame.GameTargetsWPF.FirstOrDefault();
                    }
                    index++;
                }
            }

            UpdateSelectionTexts(null);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (e.AddedItems.Count > 0)
            //{
            //    var newtab = e.AddedItems[0];
            //    var selectedItem = lb.SelectedItem;
            //    UpdateSelectionTexts(selectedItem);
            //}
        }

        public override double MaxWindowWidthPercent { get; set; } = 0.85;
        public override double MaxWindowHeightPercent { get; set; } = 0.85;
    }
}
