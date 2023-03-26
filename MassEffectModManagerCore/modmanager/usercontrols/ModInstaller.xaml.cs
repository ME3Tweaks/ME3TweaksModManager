using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flurl.Http;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.tlk;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using SevenZip;
using Extensions = WinCopies.Util.Extensions;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;
using Mod = ME3TweaksModManager.modmanager.objects.mod.Mod;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstaller.xaml
    /// </summary>
    public partial class ModInstaller : MMBusyPanelBase, INotifyPropertyChanged
    {
        /// <summary>
        /// The time between percent updates in ms.
        /// </summary>
        public static readonly int PERCENT_REFRESH_COOLDOWN = 125;

        /// <summary>
        /// Options for the installer.
        /// </summary>
        public ModInstallOptionsPackage InstallOptionsPackage { get; private set; }

        /// <summary>
        /// If installation of the mod succeeded; to be read by whatever invoked this panel
        /// </summary>
        public bool InstallationSucceeded { get; private set; }

        /// <summary>
        /// If installation of the mod was canceled; maybe the game was running. Maybe this should be an interface...
        /// </summary>
        public bool InstallationCancelled { get; private set; }


        // Todo: Is this still necessary now that it's split?
        public bool ModIsInstalling { get; set; }

        /// <summary>
        /// Initializes the Mod Installer panel.
        /// </summary>
        /// <param name="package">The installation options package</param>
        public ModInstaller(ModInstallOptionsPackage package)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod Installer", new WeakReference(this));
            M3Log.Information($@">>>>>>> Starting mod installer for mod: {package.ModBeingInstalled.ModName} {package.ModBeingInstalled.ModVersionString} for game {package.ModBeingInstalled.Game}. Install source: {(package.ModBeingInstalled.IsInArchive ? @"Archive" : @"Library (disk)")}"); //do not localize
            InstallOptionsPackage = package;
            LoadCommands();
            lastPercentUpdateTime = DateTime.Now;
            package.InstallTarget.ReloadGameTarget(false); //Reload so we can have consistent state with ALOT on disk
            Action = M3L.GetString(M3L.string_preparingToInstall);
        }

        private void LoadCommands()
        {
            // Has no commands anymore
        }


        private DateTime lastPercentUpdateTime;

        /// <summary>
        /// Describes the result of the mod installation.
        /// </summary>
        public enum ModInstallCompletedStatus
        {
            UNHANDLED_INSTALL_RESULT,
            INSTALL_SUCCESSFUL,
            USER_CANCELED_INSTALLATION,
            INSTALL_FAILED_USER_CANCELED_MISSING_MODULES,
            INSTALL_FAILED_ALOT_BLOCKING,
            INSTALL_FAILED_REQUIRED_DLC_MISSING,
            INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS,
            NO_RESULT_CODE,
            INSTALL_FAILED_MALFORMED_RCW_FILE,
            INSTALL_ABORTED_NOT_ENOUGH_SPACE,
            INSTALL_FAILED_BAD_ME2_COALESCED,
            INSTALL_FAILED_EXCEPTION_IN_ARCHIVE_EXTRACTION,
            INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER,
            INSTALL_FAILED_EXCEPTION_FILE_COPY,
            INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER,
            INSTALL_FAILED_INVALID_CONFIG_FOR_COMPAT_PACK_ME3,
            INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES,
            INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING,
            INSTALL_FAILED_AMD_PROCESSOR_REQUIRED,
            INSTALL_FAILED_EXCEPTION_APPLYING_MERGE_MOD
        }

        /// <summary>
        /// The current ongoing action to display to the user.
        /// </summary>
        public string Action { get; set; }
        /// <summary>
        /// The current percentage to show the user.
        /// </summary>
        public int Percent { get; set; }

        /// <summary>
        /// The bound percentage visibility.
        /// </summary>
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;

        private void BeginInstallingMod()
        {
            ModIsInstalling = true;
            if (CheckForGameBackup())
            {
                if (InstallOptionsPackage.InstallTarget.Game.IsLEGame())
                {
                    if (!OodleHelper.EnsureOodleDll(InstallOptionsPackage.InstallTarget.TargetPath, M3Filesystem.GetDllDirectory()))
                    {
                        M3Log.Error($@"Oodle dll could not be sourced from game: {InstallOptionsPackage.InstallTarget.TargetPath}. Installation cannot proceed");
                        InstallationSucceeded = false;
                        InstallationCancelled = true;
                        M3L.ShowDialog(mainwindow, @"The compression library for opening and saving Legendary Edition packages could not be located. Ensure your game is properly installed. If you continue to have issues, please come to the ME3Tweaks Discord.", M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
                        OnClosing(DataEventArgs.Empty);
                        return;
                    }
                }
                M3Log.Information($@"BeginInstallingMod(): {InstallOptionsPackage.ModBeingInstalled.ModName}");
                NamedBackgroundWorker bw = new NamedBackgroundWorker($@"ModInstaller-{InstallOptionsPackage.ModBeingInstalled.ModName}");
                bw.WorkerReportsProgress = true;
                bw.DoWork += InstallModBackgroundThread;
                bw.RunWorkerCompleted += ModInstallationCompleted;
                bw.RunWorkerAsync();
            }
            else
            {
                M3Log.Error(@"User aborted installation because they did not have a backup available");
                InstallationSucceeded = false;
                InstallationCancelled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        private bool CheckForGameBackup()
        {
            var hasBackup = BackupService.GetBackupStatus(InstallOptionsPackage.ModBeingInstalled.Game).BackedUp;
            var hasAnyGameModificationJobs = InstallOptionsPackage.ModBeingInstalled.InstallationJobs.Any(x => x.Header != ModJob.JobHeader.CUSTOMDLC && x.Header != ModJob.JobHeader.BALANCE_CHANGES);

            // 06/06/2022 - Check for PlotSync since it modifies basegame file
            if (!hasBackup && (InstallOptionsPackage.ModBeingInstalled.Game.IsGame1() || InstallOptionsPackage.ModBeingInstalled.Game.IsGame2()))
            {
                var custDlcJob = InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (custDlcJob != null)
                {
                    hasAnyGameModificationJobs |= InstallOptionsPackage.ModBeingInstalled.GetAllInstallableFiles().Any(x => Path.GetFileName(x).Equals(PlotManagerUpdatePanel.PLOT_MANAGER_UPDATE_FILENAME, StringComparison.CurrentCultureIgnoreCase));
                }
            }

            if (!hasAnyGameModificationJobs) return true; //Backup not required for DLC-only mods. Or balance change jobs

            if (!hasBackup)
            {
                var installAnyways = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_noBackupForXInstallingY, InstallOptionsPackage.ModBeingInstalled.Game.ToGameName(), InstallOptionsPackage.ModBeingInstalled.ModName), M3L.GetString(M3L.string_noBackup), MessageBoxButton.YesNo, MessageBoxImage.Error);
                return installAnyways == MessageBoxResult.Yes;
            }

            return true; //has backup
        }

        private async void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            bool testrun = false; //change to true to test
            M3Log.Information(@"Mod Installer Background thread starting");
            if (!Settings.LogModInstallation)
            {
                M3Log.Information(@"Mod installation logging is off. If you want to view the installation log, turn it on in the settings and apply the mod again.");
            }

            InstallOptionsPackage.ModBeingInstalled.ReOpenArchiveIfNecessary();

            var installationJobs = InstallOptionsPackage.ModBeingInstalled.InstallationJobs;
            var gameDLCPath = M3Directories.GetDLCPath(InstallOptionsPackage.InstallTarget);
            if (gameDLCPath != null)
            {
                Directory.CreateDirectory(gameDLCPath); //me1/me2 missing dlc might not have this folder
            }

            //Check we can install
            // Todo: Also put this in ModOptions dialog. Maybe with a SharedModInstaller.cs?
            var missingRequiredDLC = InstallOptionsPackage.ModBeingInstalled.ValidateRequiredModulesAreInstalled(InstallOptionsPackage.InstallTarget);
            if (missingRequiredDLC.Count > 0)
            {
                M3Log.Error(@"Required DLC is missing for installation: " + string.Join(@", ", missingRequiredDLC));
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_REQUIRED_DLC_MISSING, missingRequiredDLC);
                M3Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            // Check optional DLCs
            if (!InstallOptionsPackage.ModBeingInstalled.ValidateSingleOptionalRequiredDLCInstalled(InstallOptionsPackage.InstallTarget))
            {
                M3Log.Error($@"Mod requires installation of at least one of the following DLC, none of which are installed: {String.Join(',', InstallOptionsPackage.ModBeingInstalled.OptionalSingleRequiredDLC)}");
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING, InstallOptionsPackage.ModBeingInstalled.OptionalSingleRequiredDLC);
                M3Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            //Check/warn on official headers
            if (!PrecheckHeaders(installationJobs))
            {
                //logs handled in precheck
                e.Result = ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES;
                M3Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            if (InstallOptionsPackage.ModBeingInstalled.RequiresAMD && !App.IsRunningOnAMD)
            {
                e.Result = ModInstallCompletedStatus.INSTALL_FAILED_AMD_PROCESSOR_REQUIRED;
                M3Log.Error(@"This mod can only be installed on AMD processors, as it does nothing for Intel users.");
                M3Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            var needsBinkInstalled = !InstallOptionsPackage.InstallTarget.IsBinkBypassInstalled();
            if (!needsBinkInstalled && InstallOptionsPackage.ModBeingInstalled.RequiresEnhancedBink &&
                !InstallOptionsPackage.InstallTarget.IsEnhancedBinkInstalled())
            {
                needsBinkInstalled = true;
            }

            if (needsBinkInstalled)
            {
                try
                {
                    InstallOptionsPackage.InstallTarget.InstallBinkBypass(true);
                }
                catch (Exception be)
                {
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_errorInstallingBinkBypassX, be.Message), M3L.GetString(M3L.string_title_errorInstallingBinkBypass), MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                    M3Log.Information(@"<<<<<<< Exiting modinstaller");
                    return;
                }
            }

            // 06/06/2022 - Change to only install for Launcher if autoboot is on since bink 2007 fixes launcher dir for every game.
            if (Settings.SkipLELauncher && InstallOptionsPackage.ModBeingInstalled.Game.IsLEGame())
            {
                GameTargetWPF gt = new GameTargetWPF(MEGame.LELauncher, Path.Combine(Directory.GetParent(InstallOptionsPackage.InstallTarget.TargetPath).FullName, @"Launcher"), false, skipInit: true);
                if (gt.IsValid && !gt.IsBinkBypassInstalled())
                {
                    // Bink isn't installed and it needs autoboot
                    if (MUtilities.IsGameRunning(MEGame.LELauncher))
                    {
                        M3Log.Warning(@"LE Launcher bink bypass needs installed for autoboot but launcher is running - skipping install");
                    }
                    else
                    {
                        // If it fails to install we don't really care
                        gt.InstallBinkBypass(false);
                    }
                }
                else
                {
                    M3Log.Information(@"LE Launcher bink bypass is already installed - not installing for autoboot");
                }
            }

            if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME2 && InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null && installationJobs.Count == 1)
            {
                M3Log.Information(@"RCW mod: Beginning RCW mod subinstaller");
                e.Result = InstallAttachedRCWMod();
                M3Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            //Prepare queues
            // Todo: Move these to objects because this is way too hard to read.
            M3Log.Information(@"Building installation queues");
            InstallMapping installationQueues = null;
            try
            {
                installationQueues = InstallOptionsPackage.ModBeingInstalled.GetInstallationQueues(InstallOptionsPackage);
            }
            catch (Exception ex)
            {
                M3Log.Error(@"Error building installation queues: " + App.FlattenException(ex));
                M3Log.Information(@"<<<<<<< Exiting modinstaller");
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES, ex.Message);
                return;
            }

            var readOnlyTargets = InstallOptionsPackage.ModBeingInstalled.GetAllRelativeReadonlyTargets(InstallOptionsPackage.SetME1ReadOnlyConfigFiles);

            if (InstallOptionsPackage.InstallTarget.TextureModded)
            {
                //Check if any packages are being installed. If there are, we will block this installation.
                bool installsPackageFile = false;
                foreach (var jobMappings in installationQueues.UnpackedJobMappings)
                {
                    installsPackageFile |= jobMappings.Key.MergeMods.Any(); // merge mods will modify packages
                    installsPackageFile |= jobMappings.Key.AlternateFiles.Any(x => x.UIIsSelected && x.Operation == AlternateFile.AltFileOperation.OP_APPLY_MERGEMODS); // merge mods will modify packages
                    installsPackageFile |= jobMappings.Key.Game1TLKXmls != null && jobMappings.Key.Game1TLKXmls.Any(); // TLK merge will modify packages
                    installsPackageFile |= jobMappings.Value.FileMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.FileMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.FileMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.FileMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                foreach (var jobMappings in installationQueues.SFARJobs)
                {
                    //Only check if it's not TESTPATCH. TestPatch is classes only and will have no bearing on ALOT
                    if (Path.GetFileName(jobMappings.SFARPath) != @"Patch_001.sfar")
                    {
                        installsPackageFile |= jobMappings.SFARInstallationMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.SFARInstallationMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.SFARInstallationMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.SFARInstallationMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                    }
                }

                if (installsPackageFile)
                {
                    if (InstallOptionsPackage.ModBeingInstalled.Game.IsLEGame())
                    {
                        M3Log.Warning(
                            @"Textures are installed and user is attempting to install a mod. Warning user about texture tools no longer working after this");

                        bool cancel = false;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this),
                                M3L.GetString(M3L.string_warningTexturesAreInstalled),
                                M3L.GetString(M3L.string_warningTextureModsAreInstalled),
                                MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                            cancel = res != MessageBoxResult.OK;
                        });
                        if (cancel)
                        {
                            e.Result = ModInstallCompletedStatus.USER_CANCELED_INSTALLATION;
                            M3Log.Information(@"<<<<<<< Exiting modinstaller.");
                            return;
                        }
                        M3Log.Warning(@"User installing mod anyways even with textures installed. If they complain, it's their own fault!");
                    }
                    else
                    {
                        if (Settings.DeveloperMode)
                        {
                            M3Log.Warning(@"Textures are installed and user is attempting to install a mod (in developer mode). Prompting user to cancel installation");

                            bool cancel = false;
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                var res = M3L.ShowDialog(Window.GetWindow(this),
                                    M3L.GetString(M3L.string_interp_devModeAlotInstalledWarning,
                                        InstallOptionsPackage.ModBeingInstalled.ModName), M3L.GetString(M3L.string_brokenTexturesWarning),
                                    MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                                cancel = res != MessageBoxResult.Yes;
                            });
                            if (cancel)
                            {
                                e.Result = ModInstallCompletedStatus.USER_CANCELED_INSTALLATION;
                                M3Log.Information(@"<<<<<<< Exiting modinstaller");
                                return;
                            }

                            M3Log.Warning(@"User installing mod anyways even with textures installed. IF they compalin, it's their own fault!");
                        }
                        else
                        {
                            M3Log.Error(
                                @"ALOT is installed. Installing mods that install package files after installing ALOT is not permitted.");
                            //ALOT Installed, this is attempting to install a package file
                            e.Result = ModInstallCompletedStatus.INSTALL_FAILED_ALOT_BLOCKING;
                            M3Log.Information(@"<<<<<<< Exiting modinstaller");
                            return;
                        }
                    }
                }
                else
                {
                    M3Log.Information(@"Game is marked as textured modded, but this mod doesn't install any package files, so it's OK to install");
                }
            }

            Action = M3L.GetString(M3L.string_installing);
            PercentVisibility = Visibility.Visible;
            Percent = 0;

            int numdone = 0;

            //Calculate number of installation tasks beforehand
            int numFilesToInstall = installationQueues.UnpackedJobMappings.Select(x => x.Value.FileMapping.Count).Sum();
            numFilesToInstall += installationQueues.SFARJobs.Select(x => x.SFARInstallationMapping.Count).Sum() * (InstallOptionsPackage.ModBeingInstalled.IsInArchive ? 2 : 1); //*2 as we have to extract and install
            Debug.WriteLine(@"Number of expected installation tasks: " + numFilesToInstall);


            //Stage: Unpacked files build map


            Dictionary<string, string> fullPathMappingDisk = new Dictionary<string, string>();
            Dictionary<int, string> fullPathMappingArchive = new Dictionary<int, string>();
            SortedSet<string> customDLCsBeingInstalled = new SortedSet<string>();
            List<string> mappedReadOnlyTargets = new List<string>();

            foreach (var unpackedQueue in installationQueues.UnpackedJobMappings)
            {
                M3Log.Information(@"Building map of unpacked file destinations");

                foreach (var originalMapping in unpackedQueue.Value.FileMapping)
                {
                    //always unpacked
                    //if (unpackedQueue.Key == ModJob.JobHeader.CUSTOMDLC || unpackedQueue.Key == ModJob.JobHeader.BALANCE_CHANGES || unpackedQueue.Key == ModJob.JobHeader.BASEGAME)
                    //{

                    //Resolve source file path
                    string sourceFile;
                    if (unpackedQueue.Key.JobDirectory == null || originalMapping.Value.IsFullRelativeFilePath)
                    {
                        sourceFile = FilesystemInterposer.PathCombine(InstallOptionsPackage.ModBeingInstalled.IsInArchive, InstallOptionsPackage.ModBeingInstalled.ModPath, originalMapping.Value.FilePath);
                    }
                    else
                    {
                        sourceFile = FilesystemInterposer.PathCombine(InstallOptionsPackage.ModBeingInstalled.IsInArchive, InstallOptionsPackage.ModBeingInstalled.ModPath, unpackedQueue.Key.JobDirectory, originalMapping.Value.FilePath);
                    }


                    if (unpackedQueue.Key.Header == ModJob.JobHeader.ME1_CONFIG)
                    {
                        var destFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", originalMapping.Key);
                        if (InstallOptionsPackage.ModBeingInstalled.IsInArchive)
                        {
                            int archiveIndex = InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                            fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                            if (archiveIndex == -1)
                            {
                                M3Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                                Debugger.Break();
                            }
                            fullPathMappingDisk[sourceFile] = destFile; //used for redirection
                        }
                        else
                        {
                            fullPathMappingDisk[sourceFile] = destFile;
                        }
                    }
                    else
                    {

                        var destFile = Path.Combine(unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC ? M3Directories.GetDLCPath(InstallOptionsPackage.InstallTarget) : InstallOptionsPackage.InstallTarget.TargetPath, originalMapping.Key); //official

                        //Extract Custom DLC name
                        if (unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC)
                        {
                            var custDLC = destFile.Substring(gameDLCPath.Length, destFile.Length - gameDLCPath.Length).TrimStart('\\', '/');
                            var nextSlashIndex = custDLC.IndexOf('\\');
                            if (nextSlashIndex == -1) nextSlashIndex = custDLC.IndexOf('/');
                            if (nextSlashIndex != -1)
                            {
                                custDLC = custDLC.Substring(0, nextSlashIndex);
                                customDLCsBeingInstalled.Add(custDLC);
                            }
                        }

                        if (InstallOptionsPackage.ModBeingInstalled.IsInArchive)
                        {
                            int archiveIndex = InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                            fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                            if (archiveIndex == -1)
                            {
                                M3Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                                Debugger.Break();
                            }
                        }
                        fullPathMappingDisk[sourceFile] = destFile; //archive also uses this for redirection
                    }

                    if (readOnlyTargets.Contains(originalMapping.Key))
                    {
                        M3Log.Information(@"Adding resolved read only target: " + originalMapping.Key + @" -> " + fullPathMappingDisk[sourceFile], Settings.LogModInstallation);
                        mappedReadOnlyTargets.Add(fullPathMappingDisk[sourceFile]);
                    }
                }
            }

            //Substage: Add SFAR staging targets
            string sfarStagingDirectory = (InstallOptionsPackage.ModBeingInstalled.IsInArchive && installationQueues.SFARJobs.Count > 0) ? Directory.CreateDirectory(Path.Combine(M3Filesystem.GetTempPath(), @"SFARJobStaging")).FullName : null; //don't make directory if we don't need one
            if (sfarStagingDirectory != null)
            {
                M3Log.Information(@"Building list of SFAR staging targets");
                foreach (var sfarJob in installationQueues.SFARJobs)
                {
                    foreach (var fileToInstall in sfarJob.SFARInstallationMapping)
                    {
                        string sourceFile = null;
                        if (fileToInstall.Value.IsFullRelativeFilePath)
                        {
                            sourceFile = FilesystemInterposer.PathCombine(InstallOptionsPackage.ModBeingInstalled.IsInArchive, InstallOptionsPackage.ModBeingInstalled.ModPath, fileToInstall.Value.FilePath);
                        }
                        else
                        {
                            sourceFile = FilesystemInterposer.PathCombine(InstallOptionsPackage.ModBeingInstalled.IsInArchive, InstallOptionsPackage.ModBeingInstalled.ModPath, sfarJob.Job.JobDirectory, fileToInstall.Value.FilePath);
                        }
                        int archiveIndex = InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                        if (archiveIndex == -1)
                        {
                            M3Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                            Debugger.Break();
                        }
                        string destFile = Path.Combine(sfarStagingDirectory, sfarJob.Job.JobDirectory, fileToInstall.Value.FilePath);
                        if (fileToInstall.Value.IsFullRelativeFilePath)
                        {
                            destFile = Path.Combine(sfarStagingDirectory, fileToInstall.Value.FilePath);
                        }
                        fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                        fullPathMappingDisk[sourceFile] = destFile; //used for redirection
                        Debug.WriteLine($@"SFAR Disk Staging: {fileToInstall.Key} => {destFile}");
                    }
                }
            }

            //Check we have enough disk space
            M3Log.Information(@"Checking there is enough space to install mod (this is only an estimate)");

            long requiredSpaceToInstall = 0L;
            if (InstallOptionsPackage.ModBeingInstalled.IsInArchive)
            {
                foreach (var f in InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileData)
                {
                    if (fullPathMappingArchive.ContainsKey(f.Index))
                    {
                        //we are installing this file
                        requiredSpaceToInstall += (long)f.Size;
                        M3Log.Information(@"Adding to size calculation: " + f.FileName + @", size " + f.Size, Settings.LogModInstallation);

                    }
                    else
                    {
                        M3Log.Information(@"Skip adding to size calculation: " + f.FileName, Settings.LogModInstallation);
                    }
                }
            }
            else
            {
                foreach (var file in fullPathMappingDisk)
                {
                    requiredSpaceToInstall += new FileInfo(file.Key).Length;
                }
            }

            M3Utilities.DriveFreeBytes(InstallOptionsPackage.InstallTarget.TargetPath, out var freeSpaceOnTargetDisk);
            requiredSpaceToInstall = (long)(requiredSpaceToInstall * 1.1); //+10% for some overhead
            M3Log.Information($@"Mod requires {FileSize.FormatSize(requiredSpaceToInstall)} of disk space to install. We have {FileSize.FormatSize(freeSpaceOnTargetDisk)} available");
            if (requiredSpaceToInstall > (long)freeSpaceOnTargetDisk && freeSpaceOnTargetDisk != 0)
            {
                string driveletter = Path.GetPathRoot(InstallOptionsPackage.InstallTarget.TargetPath);
                M3Log.Error($@"Insufficient disk space to install mod. Required: {FileSize.FormatSize(requiredSpaceToInstall)}, available on {driveletter}: {FileSize.FormatSize(freeSpaceOnTargetDisk)}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string message = M3L.GetString(M3L.string_interp_dialogNotEnoughSpaceToInstall, driveletter, InstallOptionsPackage.ModBeingInstalled.ModName, FileSize.FormatSize(requiredSpaceToInstall).ToString(), FileSize.FormatSize(freeSpaceOnTargetDisk).ToString());
                    M3L.ShowDialog(window, message, M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
                });
                e.Result = ModInstallCompletedStatus.INSTALL_ABORTED_NOT_ENOUGH_SPACE;
                M3Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            //Delete existing custom DLC mods with same name
            foreach (var cdbi in customDLCsBeingInstalled)
            {
                var path = Path.Combine(gameDLCPath, cdbi);
                if (Directory.Exists(path))
                {
                    M3Log.Information($@"Deleting existing DLC directory: {path}");
                    try
                    {
                        M3Utilities.DeleteFilesAndFoldersRecursively(path, true);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        try
                        {
                            // for some reason we don't have permission to do this.
                            M3Log.Warning(@"Unauthorized access exception deleting the existing DLC mod folder. Perhaps permissions aren't being inherited? Prompting for admin to grant writes to folder, which will then be deleted.");
                            M3Utilities.CreateDirectoryWithWritePermission(path, true);
                            M3Utilities.DeleteFilesAndFoldersRecursively(path);
                        }
                        catch (Exception finalException)
                        {
                            M3Log.Error($@"Error deleting existing mod directory after admin attempt, {path}: {finalException.Message}");
                            e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER, new List<string>(new[] { path, finalException.Message }));
                            M3Log.Information(@"<<<<<<< Exiting modinstaller");
                            return;
                        }
                    }
                    catch (Exception ge)
                    {
                        M3Log.Error($@"Error deleting existing mod directory {path}: {ge.Message}");
                        e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER, new List<string>(new[] { path, ge.Message }));
                        M3Log.Information(@"<<<<<<< Exiting modinstaller");
                        return;
                    }
                }
            }

            var basegameFilesInstalled = new List<string>();
            var basegameCloudDBUpdates = new List<BasegameFileRecord>();
            void FileInstalledIntoSFARCallback(Dictionary<string, Mod.InstallSourceFile> sfarMapping, string targetPath)
            {
                numdone++;
                targetPath = targetPath.Replace('/', '\\').TrimStart('\\');
                var fileMapping = sfarMapping.FirstOrDefault(x => x.Key == targetPath);
                M3Log.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Value.FilePath} -> (SFAR) {targetPath}", Settings.LogModInstallation);
                //Debug.WriteLine(@"Installed: " + target);
                Action = M3L.GetString(M3L.string_installing);
                var now = DateTime.Now;
                if (numdone > numFilesToInstall) Debug.WriteLine($@"Percentage calculated is wrong. Done: {numdone} NumToDoTotal: {numFilesToInstall}");
                if ((now - lastPercentUpdateTime).Milliseconds > PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    Percent = (int)(numdone * 100.0 / numFilesToInstall);
                    lastPercentUpdateTime = now;
                }
            }

            void FileInstalledCallback(string targetPath)
            {
                numdone++;
                var fileMapping = fullPathMappingDisk.FirstOrDefault(x => x.Value == targetPath);
                M3Log.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Key} -> {targetPath}", Settings.LogModInstallation);
                //Debug.WriteLine(@"Installed: " + target);
                Action = M3L.GetString(M3L.string_installing);


                //BASEGAME FILE TRACKING
                // ME3 is SFAR DLC so we don't track those. Track if it path has 'DLC' directory and the path of file being installed contains an official DLC directory in it
                // There is probably better way to do this
                var shouldTrack = InstallOptionsPackage.InstallTarget.Game != MEGame.ME3 && targetPath.Contains(@"\DLC\", StringComparison.InvariantCultureIgnoreCase)
                                                                        && targetPath.ContainsAny(MEDirectories.OfficialDLC(InstallOptionsPackage.InstallTarget.Game).Select(x => $@"\{x}\"), StringComparison.InvariantCultureIgnoreCase);
                if ((shouldTrack || !targetPath.Contains(@"DLC", StringComparison.InvariantCultureIgnoreCase)) //Only track basegame files, or all official directories if ME1/ME2
                    && targetPath.Contains(InstallOptionsPackage.InstallTarget.TargetPath) // Must be within the game directory (no config files)
                    && !Path.GetFileName(targetPath).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase)) //no pcconsoletoc
                {
                    //not installing to DLC
                    basegameFilesInstalled.Add(targetPath);
                }

                if (InstallOptionsPackage.ModBeingInstalled.Game is MEGame.ME2 or MEGame.ME3 && InstallOptionsPackage.CompressInstalledPackages && File.Exists(targetPath) && targetPath.RepresentsPackageFilePath())
                {
                    var package = MEPackageHandler.QuickOpenMEPackage(targetPath);
                    if (!package.IsCompressed)
                    {
                        // Compress it
                        // Reopen package fully
                        M3Log.Information($@"Compressing installed package: {targetPath}");
                        package = MEPackageHandler.OpenMEPackage(targetPath);
                        package.Save(compress: true);

#if DEBUG
                        // TEST: REopen package
                        try
                        {
                            var p = MEPackageHandler.OpenMEPackage(targetPath);
                        }
                        catch (Exception e) { }
#endif
                    }
                }

                var now = DateTime.Now;
                if (numdone > numFilesToInstall) Debug.WriteLine($@"Percentage calculated is wrong. Done: {numdone} NumToDoTotal: {numFilesToInstall}");
                if ((now - lastPercentUpdateTime).Milliseconds > PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    Percent = (int)(numdone * 100.0 / numFilesToInstall);
                    lastPercentUpdateTime = now;
                }
            }

            //Stage: Unpacked files installation
            if (!InstallOptionsPackage.ModBeingInstalled.IsInArchive)
            {
                //Direct copy
                M3Log.Information($@"Installing {fullPathMappingDisk.Count} unpacked files into game directory");
                try
                {
                    CopyDir.CopyFiles_ProgressBar(fullPathMappingDisk, FileInstalledCallback, testrun);
                    M3Log.Information(@"Files have been copied");
                }
                catch (Exception ex)
                {
                    M3Log.Error(@"Error extracting files: " + ex.Message);
                    Crashes.TrackError(ex, new Dictionary<string, string>()
                    {
                        {@"Mod name", InstallOptionsPackage.ModBeingInstalled.ModName },
                        {@"Version", InstallOptionsPackage.ModBeingInstalled.ModVersionString}
                    });
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_FILE_COPY;
                    if (Application.Current != null)
                    {
                        // handled here so we can show what failed in string
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_errorCopyingFilesToTarget, ex.Message), M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    M3Log.Warning(@"<<<<<<< Aborting modinstaller");
                    return;
                }
            }
            else
            {
                Action = M3L.GetString(M3L.string_loadingModArchive);
                //Extraction to destination
                string installationRedirectCallback(ArchiveFileInfo info)
                {
                    var inArchivePath = info.FileName;
                    var redirectedPath = fullPathMappingDisk[inArchivePath];
                    //Debug.WriteLine($@"Redirecting {inArchivePath} to {redirectedPath}");
                    return redirectedPath;
                }

                List<string> filesInstalled = new List<string>();
                //List<string> filesToInstall = installationQueues.unpackedJobMappings.SelectMany(x => x.Value.fileMapping.Keys).ToList();
                InstallOptionsPackage.ModBeingInstalled.Archive.FileExtractionFinished += (sender, args) =>
                {
                    if (args.FileInfo.IsDirectory) return; //ignore
                    if (!fullPathMappingArchive.ContainsKey(args.FileInfo.Index))
                    {
                        M3Log.Information(@"Skipping extraction of archive file: " + args.FileInfo.FileName, Settings.LogModInstallation);
                        return; //archive extracted this file (in memory) but did not do anything with this file (7z)
                    }
                    FileInstalledCallback(fullPathMappingArchive[args.FileInfo.Index]); //put dest filename here as this func logs the mapping based on the destination
                    filesInstalled.Add(args.FileInfo.FileName);
                    FileInfo dest = new FileInfo(fullPathMappingArchive[args.FileInfo.Index]);
                    if (dest.IsReadOnly)
                        dest.IsReadOnly = false;

                    if (!fullPathMappingArchive[args.FileInfo.Index].Contains(@"DLC", StringComparison.InvariantCultureIgnoreCase)
                     && fullPathMappingArchive[args.FileInfo.Index].Contains(InstallOptionsPackage.InstallTarget.TargetPath) &&
                       !Path.GetFileName(fullPathMappingArchive[args.FileInfo.Index]).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase))
                    {
                        //not installing to DLC
                        basegameFilesInstalled.Add(fullPathMappingArchive[args.FileInfo.Index]);
                    }

                    //Debug.WriteLine($"{args.FileInfo.FileName} as file { numdone}");
                    //Debug.WriteLine(numdone);
                };

                if (fullPathMappingArchive.Any())
                {
                    try
                    {
                        InstallOptionsPackage.ModBeingInstalled.Archive.ExtractFiles(InstallOptionsPackage.InstallTarget.TargetPath, installationRedirectCallback, fullPathMappingArchive.Keys.ToArray()); //directory parameter shouldn't be used here as we will be redirecting everything
                    }
                    catch (Exception ex)
                    {
                        M3Log.Error(@"Error extracting files: " + ex.Message);
                        Crashes.TrackError(ex, new Dictionary<string, string>()
                        {
                            {@"Mod name", InstallOptionsPackage.ModBeingInstalled.ModName},
                            {@"Filename", InstallOptionsPackage.ModBeingInstalled.Archive.FileName},
                        });
                        e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_ARCHIVE_EXTRACTION;
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_errorWhileExtractingArchiveInstall, ex.Message), M3L.GetString(M3L.string_errorExtractingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                        }

                        M3Log.Warning(@"<<<<<<< Aborting modinstaller");
                        return;
                    }
                }
            }

            //Write MetaCMM for Custom DLC
            List<string> addedDLCFolders = new List<string>();
            foreach (var v in installationQueues.UnpackedJobMappings)
            {
                addedDLCFolders.AddRange(v.Value.DLCFoldersBeingInstalled);
            }
            foreach (var addedDLCFolder in addedDLCFolders)
            {
                // Write metacmm files
                M3Log.Information(@"Writing _metacmm file for " + addedDLCFolder);

                // Collect data for meta cmm
                InstallOptionsPackage.ModBeingInstalled.HumanReadableCustomDLCNames.TryGetValue(Path.GetFileName(addedDLCFolder), out var assignedDLCName);
                var alternates = InstallOptionsPackage.SelectedOptions.SelectMany(x => x.Value);
                IEnumerable<string> optionsChosen = alternates.Where(x => !string.IsNullOrWhiteSpace(x.FriendlyName)).Select(x =>
                {
                    if (x.GroupName != null) return $@"{x.GroupName}: {x.FriendlyName}";
                    return x.FriendlyName;
                });

                // Build meta cmm
                var metacmmPath = Path.Combine(addedDLCFolder, @"_metacmm.txt");

                MetaCMM metacmm = new MetaCMM()
                {
                    ModdescSourcePath = InstallOptionsPackage.ModBeingInstalled.ModDescPath,
                    ModName = assignedDLCName ?? InstallOptionsPackage.ModBeingInstalled.ModName,
                    Version = InstallOptionsPackage.ModBeingInstalled.ModVersionString,
                };

                // Metacmm uses observable collections because in some apps it's binded to the interface
                metacmm.RequiredDLC.ReplaceAll(InstallOptionsPackage.ModBeingInstalled.RequiredDLC);
                metacmm.IncompatibleDLC.ReplaceAll(InstallOptionsPackage.ModBeingInstalled.IncompatibleDLC);
                metacmm.OptionsSelectedAtInstallTime.ReplaceAll(optionsChosen);
                metacmm.RequiresEnhancedBink = InstallOptionsPackage.ModBeingInstalled.RequiresEnhancedBink;

                // Write it out to disk
                metacmm.WriteMetaCMM(metacmmPath, App.BuildNumber.ToString());
            }

            //Stage: SFAR Installation
            foreach (var sfarJob in installationQueues.SFARJobs)
            {
                InstallIntoSFAR(sfarJob, InstallOptionsPackage.ModBeingInstalled, FileInstalledIntoSFARCallback, InstallOptionsPackage.ModBeingInstalled.IsInArchive ? sfarStagingDirectory : null);
            }

            //Stage: Merge Mods
            var allMMs = installationJobs.SelectMany(x => x.MergeMods).ToList();
            allMMs.AddRange(installationJobs.SelectMany(x => x.AlternateFiles.Where(y => y.UIIsSelected && y.MergeMods != null).SelectMany(y => y.MergeMods)));
            var totalWeight = allMMs.Sum(x => x.GetMergeWeight());
            var doneWeight = 0;
            if (totalWeight == 0)
            {
                Debug.WriteLine(@"Total weight is ZERO!");
                totalWeight = 1;
            }

            void mergeWeightCompleted(int newWeightDone)
            {
                doneWeight += newWeightDone;
                Percent = (int)(doneWeight * 100.0 / totalWeight);
#if DEBUG
                if (Percent > 100)
                {
                    Debug.WriteLine(@"Percent calculation is wrong!");
                    Debugger.Break();
                }
#endif
            }

            void addBasegameTrackedFile(string originalmd5, string file)
            {
                if (file != null)
                {
                    if (file.Contains(@"BioGame\CookedPC", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // It's basegame
                        var mm = new M3BasegameFileRecord(file, (int)new FileInfo(file).Length, InstallOptionsPackage.InstallTarget, InstallOptionsPackage.ModBeingInstalled);
                        var existingInfo = BasegameFileIdentificationService.GetBasegameFileSource(InstallOptionsPackage.InstallTarget, file, originalmd5);
                        var newTextToAppend = $@"{InstallOptionsPackage.ModBeingInstalled.ModName} {InstallOptionsPackage.ModBeingInstalled.ModVersionString}";
                        if (existingInfo != null && !existingInfo.source.Contains(newTextToAppend))
                        {
                            mm.source = $@"{existingInfo.source} + {newTextToAppend}";
                        }
                        basegameCloudDBUpdates.Add(mm);
                    }
                }
            }

            Percent = 0;
            foreach (var mergeMod in allMMs)
            {
                try
                {
                    Action = M3L.GetString(M3L.string_applyingMergemods);
                    mergeMod.ApplyMergeMod(InstallOptionsPackage.ModBeingInstalled, InstallOptionsPackage.InstallTarget, mergeWeightCompleted, addBasegameTrackedFile);
                }
                catch (Exception ex)
                {
                    // Error applying merge mod!
                    InstallationSucceeded = false;
                    M3Log.Error($@"An error occurred installing mergemod {mergeMod.MergeModFilename}: {ex.Message}");
                    M3Log.Error(ex.StackTrace);
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_APPLYING_MERGE_MOD;
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_errorApplyingMergeModXY, mergeMod.MergeModFilename, ex.Message), M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error));
                    }

                    M3Log.Warning(@"<<<<<<< Aborting modinstaller");
                    return;
                }
            }

            // Stage: TLK merge (Game 1)
            if (InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.GAME1_EMBEDDED_TLK) != null)
            {
                PackageCache pc = new PackageCache();
                Percent = 0;
                Action = M3L.GetString(M3L.string_updatingTLKFiles);

                CompressedTLKMergeData compressedTlkData = null;
                var mergeFiles = InstallOptionsPackage.ModBeingInstalled.PrepareTLKMerge(out compressedTlkData);
                var gameMap = MELoadedFiles.GetFilesLoadedInGame(InstallOptionsPackage.InstallTarget.Game, gameRootOverride: InstallOptionsPackage.InstallTarget.TargetPath);
                int doneMerges = 0;
                int totalTlkMerges = mergeFiles.Count;
                PackageCache cache = new PackageCache();

                // 06/05/2022 Change to parallel
                Exception parallelException = null;
                // 01/15/2023 - If in archive you must run in single thread or library may crash app or other errors.
                var maxThreads = InstallOptionsPackage.ModBeingInstalled.IsInArchive ? 1 : 4; // If in archive we can only do one job at a time (7z library is not multi-thread safe)
                Parallel.ForEach(mergeFiles, new ParallelOptions() { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, maxThreads) }, tlkFileMap =>
                {
                    if (parallelException != null)
                        return;

                    for (int i = 0; i < tlkFileMap.Value.Count; i++)
                    {
                        if (parallelException == null)
                        {
                            try
                            {
                                var tlkXmlFile = tlkFileMap.Value[i];
                                InstallOptionsPackage.ModBeingInstalled.InstallTLKMerge(tlkXmlFile, compressedTlkData, gameMap, i == tlkFileMap.Value.Count - 1, cache, InstallOptionsPackage.InstallTarget, InstallOptionsPackage.ModBeingInstalled, x => basegameCloudDBUpdates.Add(x));
                            }
                            catch (Exception e)
                            {
                                parallelException = e;
                                M3Log.Exception(e, $@"Error installing TLK merge file {tlkFileMap.Value[i]}");
                            }
                        }
                    }

                    Percent = (int)(doneMerges * 100.0 / totalTlkMerges);
                    Interlocked.Increment(ref doneMerges);
                });

                if (parallelException != null)
                {
                    // Handle exception in parallel tlk merge
                    InstallationSucceeded = false;
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_errorInstallingTLKMergesX, parallelException.Message), M3L.GetString(M3L.string_title_errorInstallingTLKMerge), MessageBoxButton.OK, MessageBoxImage.Error));
                    }

                    M3Log.Warning(@"<<<<<<< Aborting modinstaller");
                    return;
                }
            }

            // Main installation step has completed
            M3Log.Information(@"Main stage of mod installation has completed");
            Percent = (int)(numdone * 100.0 / numFilesToInstall);

            // Mark items read only
            foreach (var readonlytarget in mappedReadOnlyTargets)
            {
                M3Log.Information(@"Setting file to read-only: " + readonlytarget);
                File.SetAttributes(readonlytarget, File.GetAttributes(readonlytarget) | FileAttributes.ReadOnly);
            }

            // Remove outdated custom DLC
            foreach (var outdatedDLCFolder in InstallOptionsPackage.ModBeingInstalled.OutdatedCustomDLC)
            {
                var outdatedDLCInGame = Path.Combine(gameDLCPath, outdatedDLCFolder);
                if (Directory.Exists(outdatedDLCInGame))
                {
                    M3Log.Information(@"Deleting outdated custom DLC folder: " + outdatedDLCInGame);
                    M3Utilities.DeleteFilesAndFoldersRecursively(outdatedDLCInGame);
                }
            }

            // Install supporting ASI files if necessary
            Action = M3L.GetString(M3L.string_installingSupportFiles);
            PercentVisibility = Visibility.Collapsed;
            if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME1)
            {
                M3Log.Information(@"Installing supporting ASI files");
                ASIManager.InstallASIToTargetByGroupID(16, @"DLC Mod Enabler", InstallOptionsPackage.InstallTarget); //16 = DLC Mod Enabler
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME2)
            {
                //None right now
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME3)
            {
                if (InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.BALANCE_CHANGES) != null)
                {
                    ASIManager.InstallASIToTargetByGroupID(ASIModIDs.ME3_BALANCE_CHANGES_REPLACER, @"Balance Changes Replacer", InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.InstallTarget.Supported)
                {
                    ASIManager.InstallASIToTargetByGroupID(ASIModIDs.ME3_AUTOTOC, @"AutoTOC", InstallOptionsPackage.InstallTarget);
                    ASIManager.InstallASIToTargetByGroupID(ASIModIDs.ME3_LOGGER, @"ME3Logger-Truncating", InstallOptionsPackage.InstallTarget);
                }
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE1)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModIDs.LE1_AUTOTOC, @"AutoTOC_LE", InstallOptionsPackage.InstallTarget);
                ASIManager.InstallASIToTargetByGroupID(ASIModIDs.LE1_AUTOLOAD_ENABLER, @"AutoloadEnabler", InstallOptionsPackage.InstallTarget);
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE2)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModIDs.LE2_AUTOTOC, @"AutoTOC", InstallOptionsPackage.InstallTarget);
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE3)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModIDs.LE3_AUTOTOC, @"AutoTOC", InstallOptionsPackage.InstallTarget);
            }

            if (sfarStagingDirectory != null)
            {
                M3Utilities.DeleteFilesAndFoldersRecursively(M3Filesystem.GetTempPath());
            }

            if (numFilesToInstall == numdone)
            {
                e.Result = ModInstallCompletedStatus.INSTALL_SUCCESSFUL;
                Action = M3L.GetString(M3L.string_installed);
            }
            else
            {
                M3Log.Warning($@"Number of completed items does not equal the amount of items to install! Number installed {numdone} Number expected: {numFilesToInstall}");
                e.Result = ModInstallCompletedStatus.INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS;
            }

            M3Log.Information(@"<<<<<<< Finishing modinstaller");
            sw.Stop();
            Debug.WriteLine($@"Elapsed: {sw.ElapsedMilliseconds}");
            //Submit basegame tracking in async way
            if (basegameFilesInstalled.Any() || basegameCloudDBUpdates.Any())
            {
                try
                {
                    var files = new List<BasegameFileRecord>(basegameFilesInstalled.Count + basegameCloudDBUpdates.Count);
                    files.AddRange(basegameCloudDBUpdates);
                    foreach (var file in basegameFilesInstalled)
                    {
                        var entry = new M3BasegameFileRecord(file, (int)new FileInfo(file).Length, InstallOptionsPackage.InstallTarget, InstallOptionsPackage.ModBeingInstalled);
                        files.Add(entry);
                    }
                    BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(files);
                }
                catch (Exception ex)
                {
                    M3Log.Error(@"Error tracking basegame files: " + ex.Message);
                    M3Log.Error(ex.StackTrace);
                }
            }
        }

        private ModInstallCompletedStatus InstallAttachedRCWMod()
        {
            M3Log.Information(@"Installing attached RCW mod. Checking Coalesced.ini first to make sure this mod can be safely applied", Settings.LogModInstallation);
            ME2Coalesced me2c = null;
            try
            {
                me2c = new ME2Coalesced(M3Directories.GetCoalescedPath(InstallOptionsPackage.InstallTarget));
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);
                M3Log.Error(@"Error parsing ME2Coalesced: " + e.Message + @". We will abort this installation");
                return ModInstallCompletedStatus.INSTALL_FAILED_BAD_ME2_COALESCED;
            }
            RCWMod rcw = InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD).RCW;
            foreach (var rcwF in rcw.Files)
            {
                var me2cF = me2c.Inis.FirstOrDefault(x => x.Key.Equals(rcwF.FileName, StringComparison.InvariantCultureIgnoreCase));
                if (me2cF.Key == null)
                {
                    //it seems some .me2 mod files only use the filename directly.
                    me2cF = me2c.Inis.FirstOrDefault(x => Path.GetFileName(x.Key).Equals(rcwF.FileName, StringComparison.InvariantCultureIgnoreCase));
                }
                if (me2cF.Key == null)
                {
                    M3Log.Error(@"RCW mod specifies a file in coalesced that does not exist in the local one: " + rcwF.FileName);
                    Crashes.TrackError(new Exception(@"Unknown Internal ME2 Coalesced File"), new Dictionary<string, string>()
                    {
                        { @"me2mod mod name", rcw.ModName },
                        { @"Missing file", rcwF.FileName }
                    });
                    return ModInstallCompletedStatus.INSTALL_FAILED_MALFORMED_RCW_FILE;
                }

                foreach (var rcwS in rcwF.Sections)
                {
                    var section = me2cF.Value.Sections.FirstOrDefault(x => x.Header.Equals(rcwS.SectionName, StringComparison.InvariantCultureIgnoreCase));
                    if (section == null)
                    {
                        M3Log.Error($@"RCW mod specifies a section in {rcwF.FileName} that does not exist in the local coalesced: {rcwS.SectionName}");
                        Crashes.TrackError(new Exception(@"Unknown Internal ME2 Coalesced File Section"), new Dictionary<string, string>()
                        {
                            { @"me2mod mod name", rcw.ModName },
                            { @"File", rcwF.FileName },
                            { @"Missing Section", rcwS.SectionName }
                        });
                        return ModInstallCompletedStatus.INSTALL_FAILED_MALFORMED_RCW_FILE;
                    }
                }


                //Apply mod
                foreach (var rcwS in rcwF.Sections)
                {
                    var section = me2cF.Value.Sections.FirstOrDefault(x => x.Header.Equals(rcwS.SectionName, StringComparison.InvariantCultureIgnoreCase));
                    Dictionary<string, int> keyCount = new Dictionary<string, int>();
                    if (section == null)
                    {
                        M3Log.Error($@"RCW section is null! We didn't find {rcwS.SectionName}");
                        TelemetryInterposer.TrackEvent(@"RCW Section Null", new Dictionary<string, string>
                        {
                            {@"MissingSection", rcwS.SectionName},
                            {@"InFile", rcwF.FileName}
                        });
                    }
                    foreach (var key in section.Entries)
                    {
                        try
                        {
                            if (key != null)
                            {
                                if (keyCount.TryGetValue(key.Key, out var existingCount))
                                {
                                    keyCount[key.Key] = existingCount + 1;
                                }
                                else
                                {
                                    keyCount[key.Key] = 1;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Crashes.TrackError(e, new Dictionary<string, string>()
                            {
                                {@"FailingEntry", key.RawText},
                                {@"Failing mod", InstallOptionsPackage.ModBeingInstalled.ModName}
                            });
                            M3Log.Fatal(@"Crash information:");
                            M3Log.Warning(@"Section: " + section?.Header);
                            M3Log.Warning(@"Entries: ");
                            foreach (var k in section.Entries)
                            {
                                M3Log.Warning($@" - {k.RawText}");
                            }
                            throw new Exception(@"There was an exception calculating the number of keys in the Ini. This issue is being investigated, please ensure telemetry is on.", e);
                        }
                    }

                    Dictionary<string, bool> keysSupportingMulti = keyCount.ToDictionary(x => x.Key, x => x.Value > 1);

                    //Remove items
                    foreach (var itemToDelete in rcwS.KeysToDelete)
                    {
                        bool deletedSomething = false;
                        for (int i = section.Entries.Count - 1; i >= 0; i--)
                        {
                            var entry = section.Entries[i];
                            if (entry.Key == itemToDelete.Key && entry.Value == itemToDelete.Value) //case sensitive
                            {
                                M3Log.Information($@"Removing ini entry {entry.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                section.Entries.RemoveAt(i);
                                deletedSomething = true;
                            }
                        }
                        if (!deletedSomething)
                        {
                            M3Log.Warning($@"Did not find anything to remove for key {itemToDelete.Key} with value {itemToDelete.Value}");
                        }
                    }

                    foreach (var itemToAdd in rcwS.KeysToAdd)
                    {
                        var existingEntries = section.Entries.Where(x => x.Key.Equals(itemToAdd.Key, StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if (existingEntries.Count <= 0)
                        {
                            //just add it
                            M3Log.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                            section.Entries.Add(itemToAdd);
                        }
                        else if (existingEntries.Count > 1)
                        {
                            //Supports multi. Add this key - but making sure the data doesn't already exist!
                            if (existingEntries.Any(x => x.Value == itemToAdd.Value)) //case sensitive
                            {
                                //Duplicate.
                                M3Log.Information($@"Not adding duplicate ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                            }
                            else
                            {
                                //Not duplicate - installing key
                                M3Log.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                section.Entries.Add(itemToAdd);
                            }
                        }
                        else
                        {
                            //Only one key exists currently. We need to check multi lookup to choose how to install
                            if (keysSupportingMulti.TryGetValue(itemToAdd.Key, out var _))
                            {
                                //Supports multi. Add this key - but making sure the data doesn't already exist!
                                if (existingEntries.Any(x => x.Value == itemToAdd.Value)) //case sensitive
                                {
                                    //Duplicate.
                                    M3Log.Information($@"Not adding duplicate ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                }
                                else
                                {
                                    //Not duplicate - installing key
                                    M3Log.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                    section.Entries.Add(itemToAdd);
                                }
                            }
                            else
                            {
                                //Replace existing key
                                existingEntries[0].Value = itemToAdd.Value;
                            }
                        }
                    }
                }
            }
            me2c.Serialize();
            return ModInstallCompletedStatus.INSTALL_SUCCESSFUL;
        }

        private bool InstallIntoSFAR(SFARFileMapping sfarJob, Mod mod, Action<Dictionary<string, Mod.InstallSourceFile>, string> FileInstalledCallback = null, string ForcedSourcePath = null)
        {

            int numfiles = sfarJob.SFARInstallationMapping.Count;

            //Open SFAR
            M3Log.Information($@"Installing {sfarJob.SFARInstallationMapping.Count} files into {sfarJob.SFARPath}");
            DLCPackage dlc = new DLCPackage(sfarJob.SFARPath);

            //Add or Replace file install
            foreach (var entry in sfarJob.SFARInstallationMapping)
            {
                string entryPath = entry.Key.Replace('\\', '/');
                if (!entryPath.StartsWith('/')) entryPath = '/' + entryPath; //Ensure path starts with /
                int index = dlc.FindFileEntry(entryPath);
                //Todo: For archive to immediate installation we will need to modify this ModPath value to point to some temporary directory
                //where we have extracted files destined for SFAR files as we cannot unpack solid archives to streams.
                var sourcePath = Path.Combine(ForcedSourcePath ?? mod.ModPath, sfarJob.Job.JobDirectory, entry.Value.FilePath);
                if (entry.Value.IsFullRelativeFilePath)
                {
                    sourcePath = Path.Combine(ForcedSourcePath ?? mod.ModPath, entry.Value.FilePath);
                }
                if (index >= 0)
                {
                    dlc.ReplaceEntry(sourcePath, index);
                    M3Log.Information(@"Replaced file within SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                else
                {
                    if (sfarJob.Job.Header == ModJob.JobHeader.TESTPATCH)
                    {
                        Debugger.Break();
                        M3Log.Fatal(@"Installing a NEW file into TESTPATCH! This will break the game. This should be immediately reported to Mgamerz on Discord.");
                        Crashes.TrackError(new Exception(@"Installing a NEW file into TESTPATCH!"), new Dictionary<string, string>()
                        {
                            {@"Mod name", mod.ModName}
                        });
                    }
                    dlc.AddFileQuick(sourcePath, entryPath);
                    M3Log.Information(@"Added new file to SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                FileInstalledCallback?.Invoke(sfarJob.SFARInstallationMapping, entryPath);
            }

            return true;
        }

        /// <summary>
        /// Checks if DLC specified by the job installation headers exist and prompt user to continue or not if the DLC is not found. This is only used jobs that are not CUSTOMDLC.'
        /// </summary>
        /// <param name="installationJobs">List of jobs to look through and validate</param>
        /// <returns></returns>
        private bool PrecheckHeaders(List<ModJob> installationJobs)
        {
            //if (InstallOptionsPackage.ModBeingInstalled.Game != MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
            foreach (var job in installationJobs)
            {
                if (job.Header == ModJob.JobHeader.ME1_CONFIG)
                {
                    //Make sure config files exist.
                    var destFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOEngine.ini");
                    if (!File.Exists(destFile))
                    {
                        bool cancel = false;

                        Application.Current.Dispatcher.Invoke(() =>
                        {

                            M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_dialogRunGameOnceFirst), M3L.GetString(M3L.string_gameMustBeRunAtLeastOnce), MessageBoxButton.OK, MessageBoxImage.Error);
                            cancel = true;
                            return;
                        });
                        if (cancel) return false;
                    }

                    continue;
                }

                if (!InstallOptionsPackage.InstallTarget.IsOfficialDLCInstalled(job.Header))
                {
                    M3Log.Warning($@"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                    //Prompt user
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dlcName = ModJob.GetHeadersToDLCNamesMap(InstallOptionsPackage.ModBeingInstalled.Game)[job.Header];
                        string resolvedName = dlcName;
                        MEDirectories.OfficialDLCNames(InstallOptionsPackage.ModBeingInstalled.Game).TryGetValue(dlcName, out resolvedName);
                        string message = M3L.GetString(M3L.string_interp_dialogOfficialTargetDLCNotInstalled, InstallOptionsPackage.ModBeingInstalled.ModName, dlcName, resolvedName);
                        if (job.RequirementText != null)
                        {
                            message += M3L.GetString(M3L.string_dialogJobDescriptionMessageHeader);
                            message += $"\n{job.RequirementText}"; //Do not localize
                        }

                        message += M3L.GetString(M3L.string_dialogJobDescriptionMessageFooter);
                        MessageBoxResult result = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_dialogJobDescriptionMessageTitle, MEDirectories.OfficialDLCNames(InstallOptionsPackage.ModBeingInstalled.Game)[ModJob.GetHeadersToDLCNamesMap(InstallOptionsPackage.ModBeingInstalled.Game)[job.Header]]), MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.No)
                        {
                            cancel = true;
                            return;
                        }

                    });
                    if (cancel)
                    {
                        M3Log.Error(@"User canceling installation");

                        return false;
                    }

                    M3Log.Warning(@"User continuing installation anyways");
                }
                else
                {
                    M3Log.Information(@"Official headers check passed for header " + job.Header, Settings.LogModInstallation);
                }
            }

            return true;
        }

        private void ModInstallationCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var telemetryResult = ModInstallCompletedStatus.NO_RESULT_CODE;

            // Only make changes if user didn't cancel
            if (!InstallationCancelled)
            {
                if (InstallOptionsPackage.ModBeingInstalled.Game.IsGame1() || InstallOptionsPackage.ModBeingInstalled.Game.IsGame2())
                {
                    Result.TargetsToPlotManagerSync.Add(InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE1)
                {
                    Result.TargetsToCoalescedMerge.Add(InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME3 || InstallOptionsPackage.ModBeingInstalled.Game.IsLEGame())
                {
                    Result.TargetsToAutoTOC.Add(InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.ModBeingInstalled.Game.IsGame3())
                {
                    Result.TargetsToSquadmateMergeSync.Add(InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.ModBeingInstalled.Game.IsGame2())
                {
                    Result.TargetsToEmailMergeSync.Add(InstallOptionsPackage.InstallTarget);
                }
            }

            if (e.Error != null)
            {
                M3Log.Error(@"An error occurred during mod installation.");
                M3Log.Error(App.FlattenException(e.Error));
                telemetryResult = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_errorOccuredDuringInstallation, App.FlattenException(e.Error)), M3L.GetString(M3L.string_error));
            }
            else
            {
                if (e.Result is (ModInstallCompletedStatus mcis1, string message1))
                {
                    M3Log.Error(@"An error occurred during mod installation.");
                    telemetryResult = mcis1;
                    M3L.ShowDialog(mainwindow, message1, mcis1.ToString()); // title won't be localized as it's error code
                }
                else if (e.Result is ModInstallCompletedStatus mcis)
                {
                    Result.SelectedTarget = InstallOptionsPackage.InstallTarget;
                    telemetryResult = mcis;
                    //Success, canceled (generic and handled), ALOT canceled
                    InstallationSucceeded = mcis == ModInstallCompletedStatus.INSTALL_SUCCESSFUL;

                    if (InstallationSucceeded && !string.IsNullOrWhiteSpace(InstallOptionsPackage.ModBeingInstalled.PostInstallToolLaunch))
                    {
                        Result.ToolToLaunch = InstallOptionsPackage.ModBeingInstalled.PostInstallToolLaunch;
                    }

                    if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_ALOT_BLOCKING)
                    {
                        InstallationCancelled = true;
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogInstallationBlockedByALOT), M3L.GetString(M3L.string_installationBlocked), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (mcis == ModInstallCompletedStatus.INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS)
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogInstallationSucceededFailedInstallCountCheck), M3L.GetString(M3L.string_installationSucceededMaybe), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_MALFORMED_RCW_FILE)
                    {
                        InstallationCancelled = true;
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogInvalidRCWFile), M3L.GetString(M3L.string_installationAborted), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_BAD_ME2_COALESCED)
                    {
                        InstallationCancelled = true;
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogInvalidME2Coalesced), M3L.GetString(M3L.string_installationAborted), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_AMD_PROCESSOR_REQUIRED)
                    {
                        InstallationCancelled = true;
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_modRequiresAMDProcessor), M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (mcis is ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES or ModInstallCompletedStatus.USER_CANCELED_INSTALLATION or ModInstallCompletedStatus.INSTALL_ABORTED_NOT_ENOUGH_SPACE)
                    {
                        InstallationCancelled = true;
                    }
                    else if (mcis is ModInstallCompletedStatus.INSTALL_SUCCESSFUL)
                    {
                        // This is handled below but is here for visual clarity.
                    }
                    else
                    {
                        // Track unhandled results
                        TelemetryInterposer.TrackEvent(@"Unhandled install result", new CaseInsensitiveDictionary<string>()
                        {
                            {@"Result name", mcis.ToString()}
                        });
                    }
                }
                else if (e.Result is (ModInstallCompletedStatus dlcCode, List<DLCRequirement> failReqs))
                {
                    telemetryResult = dlcCode;
                    // A DLC requirement failed
                    string dlcText = "";
                    foreach (var dlc in failReqs)
                    {
                        var info = TPMIService.GetThirdPartyModInfo(dlc.DLCFolderName,
                            InstallOptionsPackage.ModBeingInstalled.Game);
                        if (info != null)
                        {
                            dlcText += $"\n - {info.modname} ({dlc.DLCFolderName})"; //Do not localize
                        }
                        else
                        {
                            dlcText += $"\n - {dlc.DLCFolderName}"; //Do not localize
                        }

                        if (dlc.MinVersion != null)
                        {
                            dlcText += @" " + M3L.GetString(M3L.string_interp_minVersionAppend, dlc.MinVersion);
                        }
                    }

                    // Show dialog
                    if (dlcCode == ModInstallCompletedStatus.INSTALL_FAILED_REQUIRED_DLC_MISSING)
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogRequiredContentMissing, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (dlcCode == ModInstallCompletedStatus.INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING)
                    {
                        M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_error_singleRequiredDlcMissing, InstallOptionsPackage.ModBeingInstalled.ModName, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    InstallationCancelled = true;

                }
                else if (e.Result is (ModInstallCompletedStatus result, List<string> items))
                {
                    telemetryResult = result;
                    //Failures with results
                    M3Log.Warning(@"Installation failed with status " + result.ToString());
                    switch (result)
                    {
                        case ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER:
                            // Will only be one item in this list
                            var tpmi = TPMIService.GetThirdPartyModInfo(Path.GetFileName(items[0]), InstallOptionsPackage.ModBeingInstalled.Game);
                            string message = M3L.GetString(M3L.string_interp_unableToFullyDeleteExistingModDirectory, items[0], items[1]);
                            message += @" "; //this is here for localization tool
                            if (tpmi != null)
                            {
                                message += M3L.GetString(M3L.string_interp_thisModShouldBeReinstalled, tpmi.modname);
                            }
                            else
                            {
                                message += M3L.GetString(M3L.string_thisModShouldBeReinstalled);
                            }
                            M3L.ShowDialog(window, message, M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case ModInstallCompletedStatus.INSTALL_FAILED_INVALID_CONFIG_FOR_COMPAT_PACK_ME3:
                            M3L.ShowDialog(window, string.Join('\n', items), M3L.GetString(M3L.string_invalidCompatibilityPack), MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case ModInstallCompletedStatus.INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES:
                            M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_errorOccuredBuildingInstallationQueues, items[0]), M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                    }
                }
                else
                {
                    M3Log.Fatal(@"The application is going to crash due to a sanity check failure in the mod installer (no result!). Please report this to ME3Tweaks so this can be fixed.");

                    // Once this issue has been fixed these lines can be commented out or removed (June 14 2020)
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_appAboutToCrashYouFoundBug), M3L.GetString(M3L.string_appCrash), MessageBoxButton.OK, MessageBoxImage.Error);
                    M3Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
                    // End bug message
                    if (e.Result == null)
                    {
                        M3Log.Fatal(@"Mod installer did not have result code (null). This should be caught and handled, but it wasn't!");
                        throw new Exception(@"Mod installer did not have result code (null). This should be caught and handled, but it wasn't!");
                    }
                    else
                    {
                        M3Log.Fatal(@"Mod installer did not have parsed result code. This should be caught and handled, but it wasn't. The returned object was: " + e.Result.GetType() + @". The data was " + e.Result);
                        throw new Exception(@"Mod installer did not have parsed result code. This should be caught and handled, but it wasn't. The returned object was: " + e.Result.GetType() + @". The data was " + e.Result);
                    }
                }
            }

            var telemetryInfo = new Dictionary<string, string>()
            {
                {@"Mod name", $@"{InstallOptionsPackage.ModBeingInstalled.ModName} {InstallOptionsPackage.ModBeingInstalled.ModVersionString}"},
                {@"Installed from", InstallOptionsPackage.ModBeingInstalled.IsInArchive ? @"Archive" : @"Library"},
                {@"Type", InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null ? @"RCW .me2mod" : @"Standard"},
                {@"Game", InstallOptionsPackage.ModBeingInstalled.Game.ToString()},
                {@"Result", telemetryResult.ToString()},
                {@"Author", InstallOptionsPackage.ModBeingInstalled.ModDeveloper}
            };

            string alternateOptionsPicked = "";
            foreach (var job in InstallOptionsPackage.ModBeingInstalled.InstallationJobs)
            {
                foreach (var af in job.AlternateFiles)
                {
                    if (string.IsNullOrWhiteSpace(af.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{af.FriendlyName}={af.UIIsSelected.ToString()}";
                }
                foreach (var ad in job.AlternateDLCs)
                {
                    if (string.IsNullOrWhiteSpace(ad.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{ad.FriendlyName}={ad.UIIsSelected.ToString()}";
                }
            }

            if (!string.IsNullOrWhiteSpace(alternateOptionsPicked))
            {
                telemetryInfo[@"Alternate Options Selected"] = alternateOptionsPicked;
            }

            TelemetryInterposer.TrackEvent(@"Installed a mod", telemetryInfo);
            OnClosing(DataEventArgs.Empty);
        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            InstallationSucceeded = false;
            InstallationCancelled = true;
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !ModIsInstalling)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        private void AlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid)
            {
                if (grid.DataContext is AlternateDLC ad)
                {
                    if (!ad.UIIsSelectable)
                    {
                        return; //cannot select this item
                    }
                    if (ad.IsManual)
                    {
                        if (ad.GroupName != null && ad.UIIsSelected) return; //Cannot deselect group
                        ad.UIIsSelected = !ad.UIIsSelected;
                    }
                }
                else if (grid.DataContext is AlternateFile af && af.IsManual)
                {
                    if (af.GroupName != null && af.UIIsSelected) return; //Cannot deselect group
                    af.UIIsSelected = !af.UIIsSelected;
                    Debug.WriteLine(@"Is selected: " + af.UIIsSelected);
                }
                else if (grid.DataContext is ReadOnlyOption ro)
                {
                    ro.UIIsSelected = !ro.UIIsSelected;
                }
            }
        }

        public override void OnPanelVisible()
        {
            GC.Collect(); //this should help with the oddities of missing radio button's somehow still in the visual tree from busyhost
            InitializeComponent();
            BeginInstallingMod();
        }

        protected override void OnClosing(DataEventArgs e)
        {
            if (InstallOptionsPackage.ModBeingInstalled.Archive != null)
            {
                InstallOptionsPackage.ModBeingInstalled.Archive.Dispose();
                InstallOptionsPackage.ModBeingInstalled.Archive = null;
            }

            base.OnClosing(DataEventArgs.Empty);
        }

        private void DebugPrintInstallationQueue_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            //if (InstallOptionsPackage.ModBeingInstalled != null)
            //{
            //    var queues = InstallOptionsPackage.ModBeingInstalled.GetInstallationQueues(InstallOptionsPackage.InstallTarget);
            //    Debug.WriteLine(@"Installation Queue:");
            //    foreach (var job in queues.Item1)
            //    {
            //        foreach (var file in job.Value.unpackedJobMapping)
            //        {
            //            Debug.WriteLine($@"[UNPACKED {job.Key.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }

            //    foreach (var job in queues.Item2)
            //    {
            //        foreach (var file in job.Item3)
            //        {
            //            Debug.WriteLine($@"[SFAR {job.job.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
            //        }
            //    }
            //}
#endif
        }

        // ISizeAdjustable Interface
        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
