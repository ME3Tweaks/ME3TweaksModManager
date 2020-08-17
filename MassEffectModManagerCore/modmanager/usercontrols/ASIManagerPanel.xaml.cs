using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Template selector
    /// </summary>
    public class ASIManagerDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            string resourceName = null;
            if (item is ASIMod am)
            {
                resourceName = "DataTemplateQueuedJob";

            }
            else if (item is InstalledASIMod iam)
            {
                resourceName = "DataTemplateQueuedJob";
            }
            else
            {
                throw new InvalidOperationException($"There is no corresponding list box template for {item}");
            }

            var element = container as FrameworkElement;
            return element.FindResource(resourceName) as DataTemplate;
        }
    }

    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    public partial class ASIManagerPanel : MMBusyPanelBase
    {
        
        public int SelectedTabIndex { get; set; }
        private object SelectedASIObject { get; set; }
        public string SelectedASIDescription { get; set; }
        public string SelectedASISubtext { get; set; }
        public string SelectedASIName { get; set; }
        public bool InstallInProgress { get; set; }
        public string InstallButtonText { get; set; }

        public bool ME1TabEnabled { get; set; }
        public bool ME2TabEnabled { get; set; }
        public bool ME3TabEnabled { get; set; }
        private GameTarget preselectedTarget;
        public string InstallLoaderText { get; set; }


        public ObservableCollectionExtended<ASIGame> Games { get; } = new ObservableCollectionExtended<ASIGame>();



        /// <summary>
        /// This ASI Manager is a feature ported from ME3CMM and maintains synchronization with Mass Effect 3 Mod Manager's code for 
        /// managing and installing ASIs. ASIs are useful for debugging purposes, which is why this feature is now 
        /// part of ME3Explorer.
        /// </summary>
        public ASIManagerPanel(GameTarget preselectedTarget = null)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"ASI Manager", new WeakReference(this));
            Log.Information(@"Opening ASI Manager");

            DataContext = this;
            Directory.CreateDirectory(ASIManager.CachedASIsFolder);
            LoadCommands();
            InitializeComponent();
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
            if (SelectedASIObject is ASIModVersion asi)
            {
                Utilities.OpenWebpage(asi.SourceCodeLink);
            }
        }

        private void InstallUninstallASI()
        {
            if (SelectedASIObject is InstalledASIMod instASI)
            {
                //Unknown ASI
                instASI.Uninstall();
                RefreshASIStates();
            }
            else if (SelectedASIObject is ASIMod asi)
            {
                InstallInProgress = true;
                var target = Games.First(x => x.Game == asi.Game);
                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ASIInstallWorker");
                nbw.DoWork += (a, b) =>
                {
                    b.Result = ASIManager.InstallASIToTarget(asi, target.SelectedTarget);
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    InstallInProgress = false;
                    if (b.Error != null)
                    {
                        Log.Error($@"Exception installing ASI: {b.Error.Message}");
                        M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_anErrorOccuredDeletingTheASI, b.Error.Message), M3L.GetString(M3L.string_errorDeletingASI), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    RefreshASIStates();
                    UpdateSelectionTexts(SelectedASIObject);
                    CommandManager.InvalidateRequerySuggested();
                };
                InstallInProgress = true;
                nbw.RunWorkerAsync();
            }
        }

        private bool CanInstallASI()
        {
            if (SelectedASIObject == null) return false;
            if (SelectedASIObject is ASIMod am)
            {
                return !Utilities.IsGameRunning(am.Game) && (Games.FirstOrDefault(x => x.Game == am.Game)?.GameTargets.Any() ?? false);
            }

            if (SelectedASIObject is InstalledASIMod iam)
            {
                return !Utilities.IsGameRunning(iam.Game) && (Games.FirstOrDefault(x => x.Game == iam.Game)?.GameTargets.Any() ?? false);
            }

            return false;
        }

        private bool ManifestASIIsSelected() => SelectedASIObject is ASIMod;

        private void RefreshASIStates()
        {
            foreach (var game in Games)
            {
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
            if (v is ASIModVersion asiMod)
            {
                SelectedASIDescription = asiMod.Description;
                SelectedASIName = asiMod.Name;
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, asiMod.Author, asiMod.Version);
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
            else if (v is InstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = M3L.GetString(M3L.string_unknownASIDescription);
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
            //This has to be done here as mainwindow will not be available until this is called
            Mod.MEGame[] gameEnum = new[] { Mod.MEGame.ME1, Mod.MEGame.ME2, Mod.MEGame.ME3 };
            int index = 0;
            foreach (var game in gameEnum)
            {
                var targets = mainwindow.InstallationTargets.Where(x => x.Game == game).ToList();
                ASIGame asiGame = null;
                if (targets.Count > 0)
                {
                    asiGame = new ASIGame(game, targets);
                    Games.Add(asiGame);

                    if (preselectedTarget != null && preselectedTarget.Game == game)
                    {
                        asiGame.SelectedTarget = preselectedTarget;
                        SelectedTabIndex = index;
                    }
                }

                index++;
            }

            UpdateSelectionTexts(null);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var newtab = e.AddedItems[0];

                //    var selectedItem = lb.SelectedItem;
                //    UpdateSelectionTexts(selectedItem);
            }
        }

    }
}
