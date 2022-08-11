using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.NativeMods.Interfaces;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;
using PropertyChanged;

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
        private GameTargetWPF preselectedTarget;
        public ObservableCollectionExtended<ASIGameWPF> Games { get; } = new();



        /// <summary>
        /// This ASI Manager is a feature ported from ME3CMM and maintains synchronization with Mass Effect 3 Mod Manager's code for 
        /// managing and installing ASIs. ASIs are useful for debugging purposes, which is why this feature is now 
        /// part of ME3Explorer.
        /// </summary>
        public ASIManagerPanel(GameTargetWPF preselectedTarget = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"ASI Manager", new WeakReference(this));
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

        private void InstallUninstallASI()
        {
            if (SelectedASIObject is IInstalledASIMod instASI)
            {
                //Unknown ASI
                if (instASI is IKnownInstalledASIMod kam && kam.Outdated)
                {
                    internalInstallASI(kam.AssociatedManifestItem.OwningMod.LatestVersion);
                }
                else
                {
                    instASI.Uninstall();
                    RefreshASIStates(instASI.Game);
                }

            }
            else if (SelectedASIObject is ASIMod asi)
            {
                internalInstallASI(asi.LatestVersion);
            }
        }

        private void internalInstallASI(ASIModVersion asi)
        {
            InstallInProgress = true;
            var target = Games.First(x => x.Game == asi.Game);
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ASIInstallWorker");
            nbw.DoWork += (a, b) =>
            {
                b.Result = ASIManager.InstallASIToTarget(asi, target.CurrentGameTarget);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                InstallInProgress = false;
                if (b.Error != null)
                {
                    M3Log.Error($@"Exception installing ASI: {b.Error.Message}");
                    M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_anErrorOccuredDeletingTheASI, b.Error.Message), M3L.GetString(M3L.string_errorDeletingASI), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                RefreshASIStates(asi.Game);
                UpdateSelectionTexts(SelectedASIObject);
                CommandManager.InvalidateRequerySuggested();
            };
            InstallInProgress = true;
            nbw.RunWorkerAsync();
        }

        private bool CanInstallASI()
        {
            if (SelectedASIObject == null) return false;
            if (SelectedASIObject is ASIMod am)
            {
                return !M3Utilities.IsGameRunning(am.Game) && (Games.FirstOrDefault(x => x.Game == am.Game)?.GameTargets.Any() ?? false);
            }

            if (SelectedASIObject is InstalledASIMod iam)
            {
                return !M3Utilities.IsGameRunning(iam.Game) && (Games.FirstOrDefault(x => x.Game == iam.Game)?.GameTargets.Any() ?? false);
            }

            return false;
        }

        private bool ManifestASIIsSelected() => SelectedASIObject is ASIMod || SelectedASIObject is KnownInstalledASIMod;

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
                SelectedASIDescription = asiMod.LatestVersion.Description;
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
                SelectedASIDescription = kaim.AssociatedManifestItem.Description;
                SelectedASIName = kaim.AssociatedManifestItem.Name;
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, kaim.AssociatedManifestItem.Author, kaim.AssociatedManifestItem.Version);
                subtext += Environment.NewLine;
                if (kaim.Outdated)
                {
                    subtext += M3L.GetString(M3L.string_installedOutdated);
                    InstallButtonText = M3L.GetString(M3L.string_updateASI);
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
                        asiGame.CurrentGameTargetWPF = preselectedTarget;
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
