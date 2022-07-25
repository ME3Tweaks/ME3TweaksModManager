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
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.objects.exceptions;
using ME3TweaksModManager.modmanager.objects.installer;

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
        /// Weave-called when SelectedGameTarget changes
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
                me1ConfigReadOnlyOption.UIIsSelected = true;
                AlternateGroups.Add(new AlternateGroup(me1ConfigReadOnlyOption));
                AllOptionsAreAutomatic = false;
            }

            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                // To respect dev ordering we must enumerate in order
                List<AlternateOption> parsedOptions = new List<AlternateOption>();
                // We don't allow optiongroups to cross dlc/files so we have to parse them separate.
                for (int i = 0; i < job.AlternateDLCs.Count; i++)
                {
                    var alt = job.AlternateDLCs[i];
                    if (parsedOptions.Contains(alt))
                        continue;
                    if (alt.GroupName != null)
                    {
                        // Add the group
                        AlternateGroups.Add(new AlternateGroup(new List<AlternateOption>(job.AlternateDLCs.Where(x=>x.GroupName == alt.GroupName)))); // Add multimode group
                    }
                    else
                    { 
                        // Add only the option
                        AlternateGroups.Add(new AlternateGroup(alt)); // Add single mode group
                    }
                }

                for (int i = 0; i < job.AlternateFiles.Count; i++)
                {
                    var alt = job.AlternateFiles[i];
                    if (parsedOptions.Contains(alt))
                        continue;
                    if (alt.GroupName != null)
                    {
                        if (alt.GroupName != null)
                        {
                            // Add the group
                            AlternateGroups.Add(new AlternateGroup(new List<AlternateOption>(job.AlternateFiles.Where(x => x.GroupName == alt.GroupName)))); // Add multimode group
                        }
                        else
                        {
                            // Add only the option
                            AlternateGroups.Add(new AlternateGroup(alt)); // Add single mode group
                        }
                    }
                    else
                    {
                        AlternateGroups.Add(new AlternateGroup(alt)); // Add single mode group
                    }
                }

                //var alternateDLCGroups = job.AlternateDLCs.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();
                //var alternateFileGroups = job.AlternateFiles.Where(x => x.GroupName != null).Select(x => x.GroupName).Distinct().ToList();

                //foreach (var adlcg in alternateDLCGroups)
                //{
                //    AlternateGroups.Add(new AlternateGroup(job.AlternateDLCs.Where(x => x.GroupName == adlcg).OfType<AlternateOption>().ToList()));
                //}

                //foreach (var afileg in alternateFileGroups)
                //{
                //    AlternateGroups.Add(new AlternateGroup(job.AlternateFiles.Where(x => x.GroupName == afileg).OfType<AlternateOption>().ToList()));
                //}

                //// NON GROUP OPTIONS COME NEXT.
                //AlternateGroups.AddRange(job.AlternateDLCs.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
                //AlternateGroups.AddRange(job.AlternateFiles.Where(x => x.GroupName == null).Select(x => new AlternateGroup(x)));
            }

            // Set the initial states
            foreach (AlternateGroup o in AlternateGroups)
            {
                o.SetIsSelectedChangeHandler(OnAlternateSelectionChanged);
                internalSetupInitialSelection(o);
            }

            SortOptions();

            int numAttemptsRemaining = 15;
            try
            {
                UpdateOptions(ref numAttemptsRemaining, ModBeingInstalled, SelectedGameTarget, initialSetup: true); // Update for DependsOnKeys.
            }
            catch (CircularDependencyException)
            {
                // uh oh
                M3Log.Warning(@"Circular dependency detected in logic for mod alternates");
            }
            // Done calculating options

            // This has to occur after UpdateOptions, otherwise some states won't be accurately reflected.
            foreach (var o in AlternateGroups)
            {
                if (o.GroupName != null)
                {
                    // Deselect so UI doesn't show selected.
                    foreach (var v in o.AlternateOptions)
                    {
                        if (o.SelectedOption != v)
                        {
                            v.UIIsSelected = false;
                        }
                    }
                }
            }

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
            foreach (var option in o.AlternateOptions)
            {
                // Suboptions.
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




            //if (o.AlternateOptions.Count == 1)
            //{
            //    // Single mode
            //}
            //else
            //{
            //    // Multi mode
            //}

        }

        private void SortOptions()
        {
            // ModDesc 8: Sorting option disable
            if (ModBeingInstalled.SortAlternateOptions)
            {
                var remainingOptionsToSort = new List<AlternateGroup>(AlternateGroups);
                List<AlternateGroup> newOptions = new List<AlternateGroup>();

                // Read only option is always top (ME1 only)
                var readOnly = remainingOptionsToSort.FirstOrDefault(x => x.AlternateOptions.Count == 1 && x.AlternateOptions[0] is ReadOnlyOption);
                if (readOnly != null)
                {
                    newOptions.Add(readOnly);
                    remainingOptionsToSort.Remove(readOnly);
                }

                // Put indexed items at the top in ascending order.
                var indexedOptions = remainingOptionsToSort.Where(x => x.SortIndex > 0);
                newOptions.AddRange(indexedOptions.OrderBy(x => x.SortIndex));
                remainingOptionsToSort = remainingOptionsToSort.Except(indexedOptions).ToList();

                // Put remaining options at the bottom.
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName != null));
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName == null && x.SelectedOption.UIIsSelectable));
                newOptions.AddRange(remainingOptionsToSort.Where(x => x.GroupName == null && !x.SelectedOption.UIIsSelectable));

#if DEBUG
                if (newOptions.Count != AlternateGroups.Count)
                    throw new Exception(@"Error sorting options! The results was not the same length as the input.");
#endif

                AlternateGroups.ReplaceAll(newOptions);
            }
        }

        private void OnAlternateSelectionChanged(object sender, EventArgs data)
        {
            if (sender is AlternateOption ao && data is DataEventArgs args && args.Data is bool newState)
            {
                var altsToUpdate = findOptionsDependentOn(ao);

                if (altsToUpdate.Any())
                {
                    altsToUpdate.Add(ao); // This is required for lookup
                    // An alternate option was changed by the user.
                    int numRemainingAttempts = 15;
                    try
                    {
                        UpdateOptions(ref numRemainingAttempts, ModBeingInstalled, SelectedGameTarget, altsToUpdate);
                    }
                    catch (CircularDependencyException)
                    {
                        MessageBox.Show(M3L.GetString(M3L.string_circularDependencyDialogMessage), M3L.GetString(M3L.string_circularDependency), MessageBoxButton.OK, MessageBoxImage.Error);
                        InstallationCancelled = true;
                        OnClosing(DataEventArgs.Empty);
                    }
                }
            }
        }

        private void UpdateOptions(ref int numAttemptsRemaining, Mod mod, GameTargetWPF target, List<AlternateOption> optionsToUpdate = null, bool initialSetup = false)
        {
            numAttemptsRemaining--;
            if (numAttemptsRemaining <= 0)
            {
                // Tried too many times. This is probably some circular dependency the dev set
                throw new CircularDependencyException();
            }

            // If none were passed in, we parse all of them.
            optionsToUpdate ??=AlternateGroups.SelectMany(x => x.AlternateOptions).ToList();

            List<AlternateOption> secondPassOptions = new List<AlternateOption>();
            foreach (var v in optionsToUpdate)
            {
                // Add the list of options this one depends on so we can pass them through to the validation function, even if that one is not being updated.
                var allDependentOptions = AlternateGroups.SelectMany(x => x.AlternateOptions).Where(x => v.DependsOnKeys.Any(y => y.Key == x.OptionKey)).Concat(optionsToUpdate).Distinct().ToList();
                var stateChanged = v.UpdateSelectability(allDependentOptions, mod, target);
                if (stateChanged)
                {
                    Debug.WriteLine($@"State changed: {v.FriendlyName} to {v.UIIsSelected}");
                    secondPassOptions.AddRange(findOptionsDependentOn(v));
                    //UpdateOptions(ref numAttemptsRemaining, findOptionsDependentOn(v));
                    //break; // Don't parse it again.
                }
            }

            // If anything depends on options that changed, re-evaluate those specific options.
            if (secondPassOptions.Any())
            {
                UpdateOptions(ref numAttemptsRemaining, mod, target, secondPassOptions.Distinct().ToList());
            }

            if (initialSetup)
            {
                foreach (var group in AlternateGroups.Where(x => x.IsMultiSelector))
                {
                    var firstAutoForced = group.AlternateOptions.FirstOrDefault(x => x.UIRequired);
                    if (firstAutoForced != null)
                    {
                        group.SelectedOption = firstAutoForced;
                    }
                    else
                    {
                        if (group.SelectedOption.UINotApplicable)
                        {
                            // Find first option that is not marked as not-applicable
                            var option = group.AlternateOptions.FirstOrDefault(x => !x.UINotApplicable);
                            if (option != null)
                            {
                                // This is a bad setup in moddesc!
                                group.SelectedOption = option;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of alternates that have a state dependency on the specified key
        /// </summary>
        /// <param name="alternateOption"></param>
        /// <returns></returns>
        private List<AlternateOption> findOptionsDependentOn(AlternateOption alternateOption)
        {
            return AlternateGroups.SelectMany(x => x.AlternateOptions).Where(x => x.DependsOnKeys.Any(x => x.Key == alternateOption.OptionKey)).ToList();
        }


        private bool CanInstall()
        {
            foreach (var group in AlternateGroups)
            {
                if (group.IsMultiSelector)
                {
                    // Multi mode

                    // 06/11/2022 - Change to only 8.0 or higher to prevent breaking old mods that abused the group not having a default
                    // option picked
                    // NEEDS A BIT MORE VALIDATION ON PASSING OPTIONS THROUGH
                    if (group.SelectedOption.UINotApplicable && ModBeingInstalled.ModDescTargetVersion >= 8.0) return false; // Option must be selectable by user in order for it to be chosen by multi selector
                }
                else
                {
                    // Single mode
                }
            }

            return true;
        }

        private void BeginInstallingMod()
        {
            // Set the 'SelectedOption' on groups to have UIIsSelected = true so the enumeration works
            // This makes sure at most one option is set - (MM7 backcompat from 8.0 means there might be a state
            // where one option is not chosen due to use of radioboxes with autos...)
            foreach (var v in AlternateGroups.Where(x => x.IsMultiSelector))
            {
                if (v.SelectedOption != null && !v.SelectedOption.UINotApplicable)
                {
                    v.SelectedOption.UIIsSelected = true;
                }

                foreach (var e in v.OtherOptions)
                    e.UIIsSelected = false;
            }


            // Create a map of jobs to headers based on the selection options.
            // Makes sure this is done from the InstallationJobs header so it's in MODDESC order and not UI order
            // This means the UI should change the selection states of alternates
            var optionsMap = new Dictionary<ModJob.JobHeader, List<AlternateOption>>();

            M3Log.Information(@"Building list of alternates to pass to mod installer - they will apply in order", Settings.LogModInstallation);
            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                optionsMap[job.Header] = new List<AlternateOption>();
                foreach (var alt in job.AlternateFiles.Where(x => x.UIIsSelected))
                {
                    M3Log.Information($@"Adding alternate file to install package {job.Header} {alt.FriendlyName}", Settings.LogModInstallation);
                    optionsMap[job.Header].Add(alt);
                }
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    // Custom DLC: add alternate dlc option.
                    foreach (var alt in job.AlternateDLCs.Where(x => x.UIIsSelected))
                    {
                        M3Log.Information($@"Adding alternate dlc to install package {job.Header} {alt.FriendlyName}", Settings.LogModInstallation);
                        optionsMap[job.Header].Add(alt);
                    }
                }
            }

            ModInstallOptionsPackage moip = new ModInstallOptionsPackage()
            {
                CompressInstalledPackages = CompressInstalledPackages,
                InstallTarget = SelectedGameTarget,
                ModBeingInstalled = ModBeingInstalled,
                SelectedOptions = optionsMap
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
                ao.RemoveIsSelectedChangeHandler(OnAlternateSelectionChanged);
            }
            AlternateGroups.ClearEx();
        }
        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        private void DebugEnumerateOptions_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            foreach (var v in AlternateGroups)
            {
                Debug.WriteLine($@"{v.AlternateOptions.Count} options in group {v.GroupName}:");
            }
#endif
        }
    }
}
