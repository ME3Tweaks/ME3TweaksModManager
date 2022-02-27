using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Shows the options for installing a mod, which then advances to ModInstaller (if a mod is being installed)
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ModInstallOptionsPanel : MMBusyPanelBase
    {
        public Mod ModBeingInstalled { get; private set; }
        public GameTargetWPF SelectedGameTarget { get; set; }
        public bool CompressInstalledPackages { get; set; }
        public GenericCommand InstallCommand { get; private set; }


        private readonly ReadOnlyOption me1ConfigReadOnlyOption = new ReadOnlyOption();

        /// <summary>
        /// All configurable options to display to the user.
        /// </summary>
        //public ObservableCollectionExtended<object> AllAlternateOptions { get; } = new ObservableCollectionExtended<object>();

        /// <summary>
        /// Alternate options that don't have a group assigned to them
        /// </summary>
        //public ObservableCollectionExtended<AlternateOption> AlternateOptions { get; } = new ObservableCollectionExtended<AlternateOption>();
        /// <summary>
        /// All alternate options to show to the user (groups can have 1 or more items)
        /// </summary>
        public ObservableCollectionExtended<AlternateGroup> AlternateGroups { get; } = new ObservableCollectionExtended<AlternateGroup>();
        /// <summary>
        /// List of available targets that can be installed to
        /// </summary>
        public ObservableCollectionExtended<GameTargetWPF> InstallationTargets { get; } = new ObservableCollectionExtended<GameTargetWPF>();

        /// <summary>
        /// If a target change must occur before you can install the mod (the current target is not valid)
        /// </summary>
        public bool PreventInstallUntilTargetChange { get; set; }

        /// <summary>
        /// If all options that this mod supports configuring are automatic and cannot be changed by the user in this dialog
        /// </summary>
        public bool AllOptionsAreAutomatic { get; private set; }

        /// <summary>
        /// Result flag indicating that installation was canceled (maybe remove for 8.0?)
        /// </summary>
        public bool InstallationCancelled { get; private set; }

        /// <summary>
        /// If this is a batch mode install. In the event that all options are automatic this dialog is skipped
        /// </summary>
        public bool BatchMode { get; private set; }
        public ModInstallOptionsPanel(Mod mod, GameTargetWPF gameTargetWPF, bool? installCompressed, bool batchMode)
        {
            ModBeingInstalled = mod;

            if (!mod.IsInArchive)
            {
                foreach (var alt in mod.GetAllAlternates())
                {
                    if (!string.IsNullOrWhiteSpace(alt.ImageAssetName))
                    {
                        alt.LoadImageAsset(mod);
                    }
                }
            }
            LoadCommands();

            if (mod.BannerBitmap == null)
            {
                mod.LoadBannerImage(); // Method will check if it's null
            }
        }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(BeginInstallingMod, CanInstall);
        }




        /// <summary>
        /// Weave-calld when SelectedGameTarget changes
        /// </summary>
        /// <param name="oldT"></param>
        /// <param name="newT"></param>
        public void OnSelectedGameTargetChanged(object oldT, object newT)
        {
            Result.SelectedTarget = newT as GameTargetWPF;
            if (oldT != null && newT != null)
            {
                PreventInstallUntilTargetChange = false;
                SetupOptions(false);
            }
        }

        private void SetupOptions(bool initialSetup)
        {
            AlternateGroups.ClearEx();

            //Write check
            var canWrite = M3Utilities.IsDirectoryWritable(SelectedGameTarget.TargetPath);
            if (!canWrite)
            {
                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogNoWritePermissions), M3L.GetString(M3L.string_cannotWriteToGameDirectory), MessageBoxButton.OK, MessageBoxImage.Warning);
                if (initialSetup)
                {
                    //needs write permissions
                    InstallationCancelled = true;
                    OnClosing(DataEventArgs.Empty);
                }
                else
                {
                    PreventInstallUntilTargetChange = true;
                }
                return;
            }

            if (ModBeingInstalled.Game != MEGame.LELauncher)
            {
                //Detect incompatible DLC
                var dlcMods = SelectedGameTarget.GetInstalledDLCMods();
                if (ModBeingInstalled.IncompatibleDLC.Any())
                {
                    //Check for incompatible DLC.
                    List<string> incompatibleDLC = new List<string>();
                    foreach (var incompat in ModBeingInstalled.IncompatibleDLC)
                    {
                        if (dlcMods.Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                        {
                            var tpmi = TPMIService.GetThirdPartyModInfo(incompat, ModBeingInstalled.Game);
                            if (tpmi != null)
                            {
                                incompatibleDLC.Add($@" - {incompat} ({tpmi.modname})");
                            }
                            else
                            {
                                incompatibleDLC.Add(@" - " + incompat);
                            }
                        }
                    }

                    if (incompatibleDLC.Count > 0)
                    {
                        string message = M3L.GetString(M3L.string_dialogIncompatibleDLCDetectedHeader, ModBeingInstalled.ModName);
                        message += string.Join('\n', incompatibleDLC);
                        message += M3L.GetString(M3L.string_dialogIncompatibleDLCDetectedFooter, ModBeingInstalled.ModName);
                        M3L.ShowDialog(window, message, M3L.GetString(M3L.string_incompatibleDLCDetected), MessageBoxButton.OK, MessageBoxImage.Error);

                        if (initialSetup)
                        {
                            InstallationCancelled = true;
                            OnClosing(DataEventArgs.Empty);
                        }
                        else
                        {
                            PreventInstallUntilTargetChange = true;
                        }

                        return;
                    }
                }

                //Detect outdated DLC
                if (ModBeingInstalled.OutdatedCustomDLC.Count > 0)
                {
                    //Check for incompatible DLC.
                    List<string> outdatedDLC = new List<string>();
                    foreach (var outdatedItem in ModBeingInstalled.OutdatedCustomDLC)
                    {
                        if (dlcMods.Contains(outdatedItem, StringComparer.InvariantCultureIgnoreCase))
                        {
                            var tpmi = TPMIService.GetThirdPartyModInfo(outdatedItem, ModBeingInstalled.Game);
                            if (tpmi != null)
                            {
                                outdatedDLC.Add($@" - {outdatedItem} ({tpmi.modname})");
                            }
                            else
                            {
                                outdatedDLC.Add(@" - " + outdatedItem);
                            }
                        }
                    }

                    if (outdatedDLC.Count > 0)
                    {
                        string message = M3L.GetString(M3L.string_dialogOutdatedDLCHeader, ModBeingInstalled.ModName);
                        message += string.Join('\n', outdatedDLC);
                        message += M3L.GetString(M3L.string_dialogOutdatedDLCFooter, ModBeingInstalled.ModName);
                        InstallationCancelled = true;
                        var result = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_outdatedDLCDetected), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.No)
                        {
                            InstallationCancelled = true;
                            OnClosing(DataEventArgs.Empty);
                            return;
                        }
                    }
                }
            }

            //See if any alternate options are available and display them even if they are all autos
            AllOptionsAreAutomatic = true;
            if (ModBeingInstalled.GetJob(ModJob.JobHeader.ME1_CONFIG) != null)
            {
                me1ConfigReadOnlyOption.IsSelected = true;
                AlternateGroups.Add(new AlternateGroup(me1ConfigReadOnlyOption));
                AllOptionsAreAutomatic = false;
            }

            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                // GROUP OPTIONS COME FIRST.
                var alternateDLCGroups = job.AlternateDLCs.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();
                var alternateFileGroups = job.AlternateFiles.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();

                foreach (var adlcg in alternateDLCGroups)
                {
                    AlternateGroups.Add(new AlternateGroup(job.AlternateDLCs.Where(x => x.GroupName == adlcg).OfType<AlternateOption>().ToList()));
                }

                foreach (var afileg in alternateFileGroups)
                {
                    AlternateGroups.Add(new AlternateGroup(job.AlternateFiles.Where(x => x.GroupName == afileg).OfType<AlternateOption>().ToList()));
                }

                // NON GROUP OPTIONS COME NEXT.
                AlternateGroups.AddRange(job.AlternateDLCs.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
                AlternateGroups.AddRange(job.AlternateFiles.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
            }

            // Set the initial states
            foreach (AlternateGroup o in AlternateGroups)
            {
                o.SetIsSelectedChangeHandler(OnAlternateSelectionChanged);
                internalSetupInitialSelection(o);
                if (o.GroupName != null)
                {
                    // Deselect so UI doesn't show selected. This is only really for single mode since we dont' use 'IsSelected' in multi mode
                    foreach (var v in o.AlternateOptions) v.IsSelected = false;
                }
            }

            SortOptions();
            UpdateOptions(); // Update for DependsOnKeys.

            // Done calculating options


            if (AlternateGroups.Count == 0)
            {
                AllOptionsAreAutomatic = false; //Don't show the UI for this
            }

            var targets = mainwindow.InstallationTargets.Where(x => x.Game == ModBeingInstalled.Game).ToList();
            if (ModBeingInstalled.IsInArchive && targets.Count == 1 && AllOptionsAreAutomatic)
            {
                // All available options were chosen already (compression would come from import dialog)
                BeginInstallingMod();
            }
            else if ((targets.Count == 1 || BatchMode) && AlternateGroups.Count == 0 && (BatchMode || Settings.PreferCompressingPackages || ModBeingInstalled.Game == MEGame.ME1 || ModBeingInstalled.Game.IsLEGame()))
            {
                // ME1 and LE can't compress. If user has elected to compress packages, and there are no alternates/additional targets, just begin installation
                CompressInstalledPackages = Settings.PreferCompressingPackages && ModBeingInstalled.Game > MEGame.ME1;
                BeginInstallingMod();
            }
            else
            {
                // Set the list of targets.
                InstallationTargets.ReplaceAll(targets);
            }
        }

        void internalSetupInitialSelection(AlternateGroup o)
        {
            if (o.AlternateOptions.Count == 1)
            {
                // Single mode
            }
            else
            {
                // Multi mode
            }
            foreach (var option in o.AlternateOptions)
            {
                if (option is AlternateDLC altdlc)
                {
                    altdlc.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled);
                    if (altdlc.IsManual) AllOptionsAreAutomatic = false;
                }
                else if (option is AlternateFile altfile)
                {
                    altfile.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled);
                    if (altfile.IsManual) AllOptionsAreAutomatic = false;
                }
            }
        }

        private void SortOptions()
        {
            return;
            // ModDesc 8: Sorting option disable?

            List<AlternateGroup> newOptions = new List<AlternateGroup>();
            newOptions.AddRange(AlternateGroups.Where(x => x.GroupName != null));
            newOptions.AddRange(AlternateGroups.Where(x => x.GroupName == null && x.SelectedOption.UIIsSelectable));
            newOptions.AddRange(AlternateGroups.Where(x => x.GroupName == null && !x.SelectedOption.UIIsSelectable));

#if DEBUG
            if (newOptions.Count != AlternateGroups.Count)
                throw new Exception(@"Error sorting options! The results was not the same length as the input.");
#endif

            AlternateGroups.ReplaceAll(newOptions);
        }

        private void OnAlternateSelectionChanged(object sender, EventArgs data)
        {
            if (sender is AlternateOption ao && data is DataEventArgs args && args.Data is bool newState)
            {
                // An alternate option was changed by the user.
                UpdateOptions();
            }
        }

        private void UpdateOptions()
        {
            var allOptions = AlternateGroups.SelectMany(x => x.AlternateOptions).ToList();
            foreach (var v in allOptions)
            {
                v.UpdateSelectability(allOptions);
            }
        }


        private bool CanInstall()
        {
            // Todo
            return true;
        }

        private void BeginInstallingMod()
        {
            // Todo: Fill out selected options
            ModInstallOptionsPackage moip = new ModInstallOptionsPackage()
            {
                CompressInstalledPackages = CompressInstalledPackages,
                InstallTarget = SelectedGameTarget,
                ModBeingInstalled = ModBeingInstalled,
            };
            OnClosing(new DataEventArgs(moip));
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {
            GC.Collect(); //this should help with the oddities of missing radio button's somehow still in the visual tree from busyhost
            InitializeComponent();
            InstallationTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == ModBeingInstalled.Game));
            SelectedGameTarget = mainwindow.SelectedGameTarget != null && mainwindow.SelectedGameTarget.Game == ModBeingInstalled.Game ? mainwindow.SelectedGameTarget : InstallationTargets.FirstOrDefault();
            if (SelectedGameTarget != null)
            {
                SetupOptions(true);
            }
        }

        protected override void OnClosing(DataEventArgs e)
        {
            base.OnClosing(e);
            foreach (var ao in AlternateGroups)
            {
                ao.ReleaseAssets();
            }
            AlternateGroups.ClearEx();
        }

        private void AlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
