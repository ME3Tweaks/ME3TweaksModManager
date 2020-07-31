using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.localizations;
using Microsoft.AppCenter.Analytics;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ASIManager.xaml
    /// </summary>
    public partial class ASIManagerPanel : MMBusyPanelBase
    {
        public int SelectedTabIndex { get; set; }

        private object SelectedASIObject;
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


        public ICommand InstallCommand { get; private set; }
        public ICommand SourceCodeCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(InstallUninstallASI, CanInstallASI);
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
                Utilities.OpenWebpage(asi.SourceCodeLink);
            }
        }



        private void InstallUninstallASI()
        {
            if (SelectedASIObject is InstalledASIMod instASI)
            {
                //Unknown ASI
                File.Delete(instASI.InstalledPath);
                RefreshASIStates();
            }
            else if (SelectedASIObject is ASIMod asi)
            {
                InstallInProgress = true;
                var alreadyInstalledAndUpToDate = Games.First(x => x.Game == asi.Game).ApplyASI(asi, () =>
                {
                    InstallInProgress = false;
                    RefreshASIStates();
                    UpdateSelectionTexts(SelectedASIObject);
                    CommandManager.InvalidateRequerySuggested();
                });
                if (!alreadyInstalledAndUpToDate)
                {
                    void exceptionCallback(Exception e)
                    {
                        M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_anErrorOccuredDeletingTheASI, e.Message), M3L.GetString(M3L.string_errorDeletingASI), MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Games.First(x => x.Game == asi.Game).DeleteASI(asi, exceptionCallback); //UI doesn't allow you to install on top of an already installed ASI that is up to date. So we delete ith ere.
                    InstallInProgress = false;
                    RefreshASIStates();
                    UpdateSelectionTexts(SelectedASIObject);
                }
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
            if (v is ASIMod asiMod)
            {
                SelectedASIDescription = asiMod.Description;
                SelectedASIName = asiMod.Name;
                string subtext = M3L.GetString(M3L.string_interp_byXVersionY, asiMod.Author, asiMod.Version);
                subtext += Environment.NewLine;
                if (asiMod.UIOnly_Outdated)
                {
                    subtext += M3L.GetString(M3L.string_installedOutdated);
                    InstallButtonText = M3L.GetString(M3L.string_updateASI);
                }
                else if (asiMod.UIOnly_Installed)
                {
                    subtext += M3L.GetString(M3L.string_installedUpToDate);
                    InstallButtonText = M3L.GetString(M3L.string_uninstallASI);

                }
                else
                {
                    subtext += M3L.GetString(M3L.string_notInstalled);
                    InstallButtonText = M3L.GetString(M3L.string_installASI);

                }

                SelectedASISubtext = subtext;
            }
            else if (v is InstalledASIMod nonManifestAsiMod)
            {
                SelectedASIDescription = M3L.GetString(M3L.string_unknownASIDescription);
                SelectedASIName = nonManifestAsiMod.Filename;
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
            Mod.MEGame[] gameEnum = new[] {Mod.MEGame.ME1, Mod.MEGame.ME2, Mod.MEGame.ME3};
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

            //Technically this could load earlier, but it's not really worth the effort for the miniscule time saved
            ASIManager.LoadManifest(true, Games.ToList(), UpdateSelectionTexts);
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
