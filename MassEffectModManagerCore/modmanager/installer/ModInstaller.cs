using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.M3Merge.PlotManager;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.Shared.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCore.TextureOverride;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.asi;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.gametarget;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using ME3TweaksModManager.modmanager.objects.mod.merge.v1;
using ME3TweaksModManager.modmanager.objects.mod.moddesc;
using ME3TweaksModManager.modmanager.objects.tlk;
using NickStrupat;
using SevenZip;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ME3TweaksModManager.modmanager.installer
{
    /// <summary>
    /// Information about results of mod installation
    /// </summary>
    public class ModInstallationResult
    {
        /// <summary>
        /// Result code for the installer. 'NO_RESULT_CODE' if no result yet.
        /// </summary>
        public EModInstallerResult Result { get; set; } = EModInstallerResult.NO_RESULT_CODE;

        /// <summary>
        /// Error message (if applicable)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// A title for an error dialog (if applicable)
        /// </summary>
        public string ErrorTitle { get; set; }

        /// <summary>
        /// Indicates if the installation was aborted before it began
        /// </summary>
        public bool Aborted => Result is EModInstallerResult.USER_CANCELED_INSTALLATION
                                      or EModInstallerResult.INSTALL_ABORTED_MALFORMED_RCW_FILE
                                      or EModInstallerResult.INSTALL_ABORTED_BAD_ME2_COALESCED
                                      or EModInstallerResult.INSTALL_ABORTED_USER_CANCELED_MISSING_MODULES
                                      or EModInstallerResult.INSTALL_ABORTED_ALOT_BLOCKING
                                      or EModInstallerResult.INSTALL_ABORTED_REQUIRED_DLC_MISSING
                                      or EModInstallerResult.INSTALL_ABORTED_NOT_ENOUGH_SPACE
                                      or EModInstallerResult.INSTALL_ABORTED_NO_BACKUP
                                      or EModInstallerResult.INSTALL_ABORTED_OODLE_MISSING;

        /// <summary>
        /// Image to show on dialogs (if applicable)
        /// </summary>
        public MessageBoxImage ErrorImage { get; internal set; } = MessageBoxImage.Error;
    }

    /// <summary>
    /// Describes the result of the mod installation.
    /// </summary>
    public enum EModInstallerResult
    {
        UNHANDLED_INSTALL_RESULT,
        /// <summary>
        /// Installation completed with no errors
        /// </summary>
        INSTALL_SUCCESSFUL,
        USER_CANCELED_INSTALLATION,
        /// <summary>
        /// Required modules for install were missing and the user declined to continue
        /// </summary>
        INSTALL_ABORTED_USER_CANCELED_MISSING_MODULES,
        INSTALL_ABORTED_ALOT_BLOCKING,
        INSTALL_ABORTED_REQUIRED_DLC_MISSING,
        /// <summary>
        /// Mod installed but the computed number of completed tasks was wrong
        /// </summary>
        INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS,
        /// <summary>
        /// Unset result code
        /// </summary>
        NO_RESULT_CODE,
        INSTALL_ABORTED_MALFORMED_RCW_FILE,
        INSTALL_ABORTED_NOT_ENOUGH_SPACE,
        INSTALL_ABORTED_BAD_ME2_COALESCED,
        INSTALL_FAILED_EXCEPTION_IN_ARCHIVE_EXTRACTION,
        INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER,
        INSTALL_FAILED_EXCEPTION_FILE_COPY,
        INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER,
        // INSTALL_FAILED_INVALID_CONFIG_FOR_COMPAT_PACK_ME3, // Removed 11/11/2025 - no longer in codebase
        INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES,
        INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING,
        /// <summary>
        /// Mods requires an AMD processor (Black Blobs Fix for ME1)
        /// </summary>
        // INSTALL_ABORTED_AMD_PROCESSOR_REQUIRED, // Removed 11/11/2025 - this was moved to SharedInstaller a while ago, it is part of options precheck now
        INSTALL_FAILED_EXCEPTION_APPLYING_MERGE_MOD,
        /// <summary>
        /// No backup was found and user canceled installation when prompted.
        /// </summary>
        INSTALL_ABORTED_NO_BACKUP,
        /// <summary>
        /// LE - Could not source oodle dll
        /// </summary>
        INSTALL_ABORTED_OODLE_MISSING,
        /// <summary>
        /// LE - Failure building BTP file
        /// </summary>
        INSTALL_FAILED_BTP_BUILD_FAILED
    }


    /// <summary>
    /// Installer for M3 mods
    /// </summary>
    internal class ModInstaller
    {
        // Some consistent log strings to help trace exit procedures 
        private const string FINISHING_INSTALLER_LOGSTR = @"<<<<<<< Finishing modinstaller";
        private const string EXITING_INSTALLER_LOGSTR = @"<<<<<<< Exiting modinstaller";


        /// <summary>
        /// The installation result
        /// </summary>
        public ModInstallationResult InstallationResult { get; set; } = new ModInstallationResult();

        /// <summary>
        /// Options for the installer.
        /// </summary>
        public ModInstallOptionsPackage InstallOptionsPackage { get; private set; }

        /// <summary>
        /// Delegate for updating the UI
        /// </summary>
        public Action<string> SetAction { get; set; }

        /// <summary>
        /// Delegate for setting the current task percent
        /// </summary>
        public Action<int> SetPercent { get; set; }

        /// <summary>
        /// Delegate for controlling percentage visibility
        /// </summary>
        public Action<bool> SetPercentVisibility { get; internal set; }

        public ModInstaller(ModInstallOptionsPackage installOptionsPackage)
        {
            InstallOptionsPackage = installOptionsPackage;
        }

        /// <summary>
        /// Performs check for backup and shows dialog if no backup found asking if user wants to continue
        /// </summary>
        /// <returns></returns>
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
                    hasAnyGameModificationJobs |= InstallOptionsPackage.ModBeingInstalled.GetAllInstallableFiles().Any(x => Path.GetFileName(x).Equals(PlotManagerMerge.PLOT_MANAGER_UPDATE_FILENAME, StringComparison.CurrentCultureIgnoreCase));
                }
            }

            if (!hasAnyGameModificationJobs)
                return true; //Backup not required for DLC-only mods. Or balance change jobs

            if (!hasBackup)
            {
                var installAnyways = M3L.ShowDialog(Application.Current.MainWindow, M3L.GetString(M3L.string_interp_dialog_noBackupForXInstallingY, InstallOptionsPackage.ModBeingInstalled.Game.ToGameName(), InstallOptionsPackage.ModBeingInstalled.ModName), M3L.GetString(M3L.string_noBackup), MessageBoxButton.YesNo, MessageBoxImage.Error);
                return installAnyways == MessageBoxResult.Yes;
            }

            return true; //has backup
        }

        /// <summary>
        /// Performs installation precheck. Sets the result code if failures occurred.
        /// </summary>
        public void PerformPrecheck()
        {
            if (!CheckForGameBackup())
            {
                M3Log.Error(@"User aborted installation because they did not have a backup available");
                InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_NO_BACKUP;
                return;
            }

            // Allow skipping if previous dialog was automatically gone through.
            if (!InstallOptionsPackage.SkipPrerequesitesCheck && !SharedInstaller.ValidateModCanInstall(MainWindow.Instance, InstallOptionsPackage.ModBeingInstalled, InstallOptionsPackage.InstallTarget))
            {
                M3Log.Error(@"Mod could not install - aborting");
                InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_USER_CANCELED_MISSING_MODULES;
                // dialog should already have been shown...
                return;
            }

            if (InstallOptionsPackage.InstallTarget.Game.IsLEGame())
            {
                if (!OodleHelper.EnsureOodleDll(InstallOptionsPackage.InstallTarget.TargetPath, M3Filesystem.GetDllDirectory()))
                {
                    M3Log.Error($@"Oodle dll could not be sourced from game: {InstallOptionsPackage.InstallTarget.TargetPath}. Installation cannot proceed");
                    InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_OODLE_MISSING;
                    var message = M3L.GetString(M3L.string_oodleNotFound);
                    if (InstallOptionsPackage.InstallTarget.Supported)
                    {
                        message += " " + M3L.GetString(M3L.string_ensureGameIsValidDiscord);
                    }
                    InstallationResult.ErrorMessage = message;
                    InstallationResult.ErrorTitle = M3L.GetString(M3L.string_cannotInstallMod);
                    return;
                }
            }

            // OK
        }

        /// <summary>
        /// Performs the mod installation. This is a blocking method.
        /// </summary>
        public void InstallMod()
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
            var gameDLCPath = InstallOptionsPackage.InstallTarget.GetDLCPath();
            if (gameDLCPath != null)
            {
                Directory.CreateDirectory(gameDLCPath); //me1/me2 missing dlc might not have this folder
            }

            // Bink only needs installed once in a batch install
            if (!InstallOptionsPackage.BatchMode || InstallOptionsPackage.IsFirstBatchMod)
            {
                var needsBinkInstalled = !InstallOptionsPackage.InstallTarget.IsBinkBypassInstalled();
                if (!needsBinkInstalled && InstallOptionsPackage.ModBeingInstalled.RequiresEnhancedBink && !InstallOptionsPackage.InstallTarget.IsEnhancedBinkInstalled())
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
                        InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                        InstallationResult.ErrorMessage = M3L.GetString(M3L.string_interp_errorInstallingBinkBypassX, be.Message);
                        InstallationResult.ErrorTitle = M3L.GetString(M3L.string_title_errorInstallingBinkBypass);
                        M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                        return;
                    }
                }
            }

            // 06/06/2022 - Change to only install for Launcher if autoboot is on since bink 2007 fixes launcher dir for every game.
            if (!InstallOptionsPackage.BatchMode || InstallOptionsPackage.IsFirstBatchMod)
            {
                if (Settings.SkipLELauncher && InstallOptionsPackage.ModBeingInstalled.Game.IsLEGame())
                {
                    // 11/22/2025 - Cloned games may not be in same directory structure; check first
                    var launcherPath = Path.Combine(Directory.GetParent(InstallOptionsPackage.InstallTarget.TargetPath).FullName, @"Launcher");
                    if (Directory.Exists(launcherPath))
                    {
                        var gt = new GameTarget(MEGame.LELauncher, launcherPath, false, skipInit: true);
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
                }
            }

            if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME2 && InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null && installationJobs.Count == 1)
            {
                M3Log.Information(@"RCW mod: Beginning RCW mod subinstaller");
                InstallAttachedRCWMod();

                // Consistent logging
                if (InstallationResult.Result == EModInstallerResult.INSTALL_SUCCESSFUL)
                {
                    M3Log.Information(FINISHING_INSTALLER_LOGSTR);
                }
                else
                {
                    M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                }
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
                M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES;
                InstallationResult.ErrorMessage = M3L.GetString(M3L.string_interp_errorOccuredBuildingInstallationQueues, ex.Message);
                InstallationResult.ErrorTitle = M3L.GetString(M3L.string_errorInstallingMod);
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

                        // ask user if they want to cancel or continue
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var res = M3L.ShowDialog(MainWindow.Instance,
                                M3L.GetString(M3L.string_warningTexturesAreInstalled),
                                M3L.GetString(M3L.string_warningTextureModsAreInstalled),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                            cancel = res == MessageBoxResult.No;
                        });

                        if (cancel)
                        {
                            // User canceled installation, exit installer thread
                            InstallationResult.Result = EModInstallerResult.USER_CANCELED_INSTALLATION;
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
                                var res = M3L.ShowDialog(MainWindow.Instance,
                                    M3L.GetString(M3L.string_interp_devModeAlotInstalledWarning,
                                        InstallOptionsPackage.ModBeingInstalled.ModName), M3L.GetString(M3L.string_brokenTexturesWarning),
                                    MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                                cancel = res != MessageBoxResult.Yes;
                            });
                            if (cancel)
                            {
                                // This is not a warning so it's listed as info
                                M3Log.Information(EXITING_INSTALLER_LOGSTR);
                                InstallationResult.Result = EModInstallerResult.USER_CANCELED_INSTALLATION;
                                return;
                            }

                            M3Log.Warning(@"User installing mod anyways even with textures installed. IF they compalin, it's their own fault!");
                        }
                        else
                        {
                            M3Log.Error(@"ALOT is installed. Installing mods that install package files after installing ALOT is not permitted.");
                            M3Log.Warning(EXITING_INSTALLER_LOGSTR);

                            //ALOT Installed, this is attempting to install a package file
                            InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_ALOT_BLOCKING;
                            InstallationResult.ErrorMessage = M3L.GetString(M3L.string_dialogInstallationBlockedByALOT);
                            InstallationResult.ErrorTitle = M3L.GetString(M3L.string_installationBlocked);
                            return;
                        }
                    }
                }
                else
                {
                    M3Log.Information(@"Game is marked as textured modded, but this mod doesn't install any package files, so it's OK to install");
                }
            }

            SetAction?.Invoke(M3L.GetString(M3L.string_installing));
            SetPercent?.Invoke(0);
            SetPercentVisibility?.Invoke(true);

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
                            int archiveIndex = Extensions.IndexOf(InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames, sourceFile, StringComparer.InvariantCultureIgnoreCase);
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

                        var destFile = Path.Combine(unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC ? InstallOptionsPackage.InstallTarget.GetDLCPath() : InstallOptionsPackage.InstallTarget.TargetPath, originalMapping.Key); //official

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
                            int archiveIndex = Extensions.IndexOf(InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames, sourceFile, StringComparer.InvariantCultureIgnoreCase);
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
            string sfarStagingDirectory = (InstallOptionsPackage.ModBeingInstalled.IsInArchive && installationQueues.SFARJobs.Count > 0) ? Directory.CreateDirectory(Path.Combine(MCoreFilesystem.GetTempDirectory(), @"SFARJobStaging")).FullName : null; //don't make directory if we don't need one
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
                        int archiveIndex = Extensions.IndexOf(InstallOptionsPackage.ModBeingInstalled.Archive.ArchiveFileNames, sourceFile, StringComparer.InvariantCultureIgnoreCase);
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

            MUtilities.DriveFreeBytes(InstallOptionsPackage.InstallTarget.TargetPath, out var freeSpaceOnTargetDisk);
            requiredSpaceToInstall = (long)(requiredSpaceToInstall * 1.1); //+10% for some overhead
            M3Log.Information($@"Mod requires {FileSize.FormatSize(requiredSpaceToInstall)} of disk space to install. We have {FileSize.FormatSize(freeSpaceOnTargetDisk)} available");
            if (requiredSpaceToInstall > (long)freeSpaceOnTargetDisk && freeSpaceOnTargetDisk != 0)
            {
                string driveletter = Path.GetPathRoot(InstallOptionsPackage.InstallTarget.TargetPath);
                M3Log.Error($@"Insufficient disk space to install mod. Required: {FileSize.FormatSize(requiredSpaceToInstall)}, available on {driveletter}: {FileSize.FormatSize(freeSpaceOnTargetDisk)}");
                M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_NOT_ENOUGH_SPACE;
                InstallationResult.ErrorMessage = M3L.GetString(M3L.string_interp_dialogNotEnoughSpaceToInstall, driveletter, InstallOptionsPackage.ModBeingInstalled.ModName, FileSize.FormatSize(requiredSpaceToInstall).ToString(), FileSize.FormatSize(freeSpaceOnTargetDisk).ToString());
                InstallationResult.ErrorTitle = M3L.GetString(M3L.string_insufficientDiskSpace);
                return;
            }


            //Delete existing custom DLC mods with same name
            // 05/08/2023: Restore ME3CMM feature that also removed DLCs that began with 'x' - disabled DLCs
            foreach (var cdbi in customDLCsBeingInstalled.Concat(customDLCsBeingInstalled.Select(x => 'x' + x)))
            {
                var path = Path.Combine(gameDLCPath, cdbi);
                if (Directory.Exists(path))
                {
                    M3Log.Information($@"Deleting existing DLC directory: {path}");
                    try
                    {
                        MUtilities.DeleteFilesAndFoldersRecursively(path, true);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        try
                        {
                            // for some reason we don't have permission to do this.
                            M3Log.Warning(@"Unauthorized access exception deleting the existing DLC mod folder. Perhaps permissions aren't being inherited? Prompting for admin to grant writes to folder, which will then be deleted.");
                            M3Utilities.CreateDirectoryWithWritePermission(path, true);
                            MUtilities.DeleteFilesAndFoldersRecursively(path);
                        }
                        catch (Exception finalException)
                        {
                            M3Log.Error($@"Error deleting existing mod directory after admin attempt, {path}: {finalException.Message}");
                            M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                            setExistingDLCDeleteFailedResult(path, finalException.Message);
                            return;
                        }
                    }
                    catch (Exception ge)
                    {
                        // Other unknown error
                        M3Log.Error($@"Error deleting existing mod directory {path}: {ge.Message}");
                        M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                        setExistingDLCDeleteFailedResult(path, ge.Message);
                        return;
                    }
                }
            }

            var basegameFilesInstalled = new List<string>();
            var basegameIdentificationServiceRecords = new CaseInsensitiveDictionary<BasegameFileRecord>();
            void FileInstalledIntoSFARCallback(Dictionary<string, Mod.InstallSourceFile> sfarMapping, string targetPath)
            {
                SystemSleepManager.PreventSleep(@"ModInstaller");

                numdone++;
                targetPath = targetPath.Replace('/', '\\').TrimStart('\\');
                var fileMapping = sfarMapping.FirstOrDefault(x => x.Key == targetPath);
                M3Log.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Value.FilePath} -> (SFAR) {targetPath}", Settings.LogModInstallation);
                //Debug.WriteLine(@"Installed: " + target);
                SetAction?.Invoke(M3L.GetString(M3L.string_installing));
                if (numdone > numFilesToInstall)
                {
                    Debug.WriteLine($@"Percentage calculated is wrong. Done: {numdone} NumToDoTotal: {numFilesToInstall}");
                }

                SetPercent?.Invoke((int)(numdone * 100.0 / numFilesToInstall));
            }

            void FileInstalledCallback(string targetPath)
            {
                SystemSleepManager.PreventSleep(@"ModInstaller");
                numdone++;
                var fileMapping = fullPathMappingDisk.FirstOrDefault(x => x.Value == targetPath);
                M3Log.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Key} -> {targetPath}", Settings.LogModInstallation);
                //Debug.WriteLine(@"Installed: " + target);
                SetAction?.Invoke(M3L.GetString(M3L.string_installing));


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
                        // TEST: Reopen package
                        try
                        {
                            var p = MEPackageHandler.OpenMEPackage(targetPath);
                        }
                        catch (Exception e) { }
#endif
                    }
                }

                SetPercent?.Invoke((int)(numdone * 100.0 / numFilesToInstall));

                var now = DateTime.Now;
#if DEBUG
                if (numdone > numFilesToInstall)
                {
                    Debug.WriteLine($@"Percentage calculated is wrong. Done: {numdone} NumToDoTotal: {numFilesToInstall}");
                }
#endif
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
                    TelemetryInterposer.TrackError(ex, new Dictionary<string, string>()
                    {
                        {@"Mod name", InstallOptionsPackage.ModBeingInstalled.ModName },
                        {@"Version", InstallOptionsPackage.ModBeingInstalled.ModVersionString}
                    });
                    InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_FILE_COPY;
                    if (Application.Current != null)
                    {
                        // handled here so we can show what failed in string
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(MainWindow.Instance, M3L.GetString(M3L.string_interp_dialog_errorCopyingFilesToTarget, ex.Message), M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                    return;
                }
            }
            else
            {
                SetAction?.Invoke(M3L.GetString(M3L.string_loadingModArchive));
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
                        TelemetryInterposer.TrackError(ex, new Dictionary<string, string>()
                        {
                            {@"Mod name", InstallOptionsPackage.ModBeingInstalled.ModName},
                            {@"Filename", InstallOptionsPackage.ModBeingInstalled.Archive.FileName},
                        });
                        InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_IN_ARCHIVE_EXTRACTION;
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(MainWindow.Instance, M3L.GetString(M3L.string_interp_errorWhileExtractingArchiveInstall, ex.Message), M3L.GetString(M3L.string_errorExtractingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                        }

                        M3Log.Warning(EXITING_INSTALLER_LOGSTR);
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

            var installedMetaCmms = new CaseInsensitiveDictionary<MetaCMM>();
            foreach (var addedDLCFolder in addedDLCFolders)
            {
                // Write metacmm files
                M3Log.Information(@"Writing _metacmm file for " + addedDLCFolder);

                // Collect data for meta cmm
                InstallOptionsPackage.ModBeingInstalled.HumanReadableCustomDLCNames.TryGetValue(Path.GetFileName(addedDLCFolder), out var assignedDLCName);
                var alternates = InstallOptionsPackage.SelectedOptions.SelectMany(x => x.Value).ToList();
                IEnumerable<string> optionsChosen = alternates.Where(x => !string.IsNullOrWhiteSpace(x.FriendlyName) && (Settings.DeveloperMode || !x.IsHidden)).Select(x =>
                {
                    if (x.GroupName != null) return $@"{x.GroupName}: {x.FriendlyName}";
                    return x.FriendlyName;
                });


                // Build meta cmm
                var metacmmPath = Path.Combine(addedDLCFolder, @"_metacmm.txt");

                MetaCMM metacmm = new MetaCMM()
                {
                    ModdescSourcePath = M3LoadedMods.GetRelativeModdescPath(InstallOptionsPackage.ModBeingInstalled),
                    ModdescSourceHash = InstallOptionsPackage.ModBeingInstalled.ModDescHash,
                    ModName = assignedDLCName ?? InstallOptionsPackage.ModBeingInstalled.ModName,
                    Version = InstallOptionsPackage.ModBeingInstalled.ModVersionString,
                    InstallTime = DateTime.Now,
                    NexusUpdateCode = InstallOptionsPackage.ModBeingInstalled.NexusModID,
                    OptionKeysSelectedAtInstallTime = alternates.Select(x => x.OptionKey).ToList(),
                    ModDescFeatureLevel = InstallOptionsPackage.ModBeingInstalled.ModDescTargetVersion,
                    RequiresEnhancedBink = InstallOptionsPackage.ModBeingInstalled.RequiresEnhancedBink
                };

                // Metacmm uses observable collections because in some apps it's binded to the interface
                metacmm.RequiredDLC.ReplaceAll(InstallOptionsPackage.ModBeingInstalled.RequiredDLC);
                metacmm.IncompatibleDLC.ReplaceAll(InstallOptionsPackage.ModBeingInstalled.IncompatibleDLC);
                metacmm.OptionsSelectedAtInstallTime.ReplaceAll(optionsChosen);

                // Write it out to disk
                metacmm.WriteMetaCMM(metacmmPath, App.BuildNumber.ToString());

                installedMetaCmms[addedDLCFolder] = metacmm;
            }

            //Stage: SFAR Installation
            foreach (var sfarJob in installationQueues.SFARJobs)
            {
                SystemSleepManager.PreventSleep(@"ModInstaller");
                InstallIntoSFAR(sfarJob, InstallOptionsPackage.ModBeingInstalled, FileInstalledIntoSFARCallback, InstallOptionsPackage.ModBeingInstalled.IsInArchive ? sfarStagingDirectory : null);
            }

            //Stage: Merge Mods
            var allMMs = installationJobs.SelectMany(x => x.MergeMods).ToList();
            allMMs.AddRange(installationJobs.SelectMany(x => x.AlternateFiles.Where(y => y.UIIsSelected && y.MergeMods != null).SelectMany(y => y.MergeMods)));

            if (allMMs.Count > 0)
            {
                var totalWeight = allMMs.Sum(x => x.GetMergeWeight());
                var doneWeight = 0;
                if (totalWeight == 0)
                {
                    Debug.WriteLine(@"Total weight is ZERO!");
                    totalWeight = 1;
                }

                void mergeWeightCompleted(int newWeightDone)
                {
                    SystemSleepManager.PreventSleep(@"ModInstaller");
                    doneWeight += newWeightDone;
                    var percent = (int)(doneWeight * 100.0 / totalWeight);
                    SetPercent?.Invoke(percent);
#if DEBUG
                    if (percent > 100)
                    {
                        Debug.WriteLine(@"Percent calculation is wrong!");
                        Debugger.Break();
                    }
#endif
                }

                // Add a tracked change from an applied merge mod.
                void addBasegameTrackedFile(string file, MergeFileTransition transition)
                {
                    if (file != null)
                    {
                        if (file.Contains(@"BioGame\CookedPC", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // It's basegame, get the current record (if any)
                            var mm = new M3BasegameFileRecord(file, (int)new FileInfo(file).Length, InstallOptionsPackage.InstallTarget, InstallOptionsPackage.ModBeingInstalled, transition.FinalMD5);
                            var existingInfo = BasegameFileIdentificationService.GetBasegameFileSource(InstallOptionsPackage.InstallTarget, file, transition.OriginalMD5);

                            // Text to append to the existing info
                            var newTextToAppend = $@"{InstallOptionsPackage.ModBeingInstalled.ModName} {InstallOptionsPackage.ModBeingInstalled.ModVersionString}";

                            // 01/26/2026 - Add block containing applied m3ms
                            if (transition.AppliedMergeMods != null && transition.AppliedMergeMods.Any())
                            {
                                //if (!string.IsNullOrWhiteSpace(newTextToAppend))
                                //{
                                //    newTextToAppend += "\n"; // do not localize
                                //st}
                                newTextToAppend += BasegameFileRecord.CreateBlock(MergeMod1.MERGEMOD_BGFIS_DATA_BLOCK, string.Join(BasegameFileRecord.BLOCK_SEPARATOR, transition.AppliedMergeMods));
                            }


                            // For tracking which mods have modified this file (not reliable)
                            mm.moddeschashes ??= new();

                            if (existingInfo != null)
                            {
                                if (!existingInfo.source.Contains(newTextToAppend))
                                {
                                    mm.source = $"{existingInfo.source}\n{newTextToAppend}"; // do not localize
                                }

                                mm.moddeschashes.AddRange(existingInfo.moddeschashes);
                            }
                            else
                            {
                                mm.source = newTextToAppend;
                            }

                            //Set record
                            mm.moddeschashes.Add(InstallOptionsPackage.ModBeingInstalled.ModDescHash);
                            basegameIdentificationServiceRecords[Path.GetRelativePath(InstallOptionsPackage.InstallTarget.TargetPath, file)] = mm;
                        }
                    }
                }

                var mmp = new MergeModPackage()
                {
                    Target = InstallOptionsPackage.InstallTarget,
                    AssociatedMod = InstallOptionsPackage.ModBeingInstalled,
                    OpenedBasegameCache = new PackageCache()
                };

                if (allMMs.Count == 1)
                {
                    mmp.OpenedBasegameCache.CacheMaxSize = 1; // This should effectively not cache anything
                }
                else if (allMMs.OfType<MergeMod1>().Any(x => x.FilesToMergeInto.Any(x => x.ApplyToAllLocalizations)) &&
                         (new ComputerInfo().AvailablePhysicalMemory / (ulong)FileSize.GibiByte < 8))
                {
                    // If not a lot of free memory cap it at 4 files, about 1GB for LE1 startups
                    mmp.OpenedBasegameCache.CacheMaxSize = 4;
                }
                else
                {
                    // Cache is uncapped. We can further optimize install time by not saving until the end.
                    mmp.MergeModToSavePackageWith = new CaseInsensitiveDictionary<IMergeMod>();
                    for (int i = allMMs.Count - 1; i >= 0; i--)
                    {
                        var mm = allMMs[i];
                        var allFiles = mm.GetMergeFileTargetFiles();
                        foreach (var af in allFiles)
                        {
                            if (!mmp.MergeModToSavePackageWith.TryGetValue(af, out _))
                            {
                                mmp.MergeModToSavePackageWith[af] = mm; // This merge mod should be the one to save the package.
                            }
                        }
                    }
                }


                // Run at the end of all merge mods so we get the final hash.
                void convertMergeModRecordsToFileIdentificationRecords()
                {
                    SetAction?.Invoke(M3L.GetString(M3L.string_trackingMergemodChanges));
                    mmp.FinalizeFileTransitionMap();
                    foreach (var f in mmp.FileTransitionMap.Where(x => x.Value.WasSavedOnce))
                    {
                        addBasegameTrackedFile(f.Key, f.Value);
                    }
                }

                if (allMMs.Any())
                {
                    SetAction?.Invoke(M3L.GetString(M3L.string_applyingMergemods));
                    SetPercent?.Invoke(0);
                }

                foreach (var mergeMod in allMMs)
                {
                    try
                    {
                        mergeMod.ApplyMergeMod(mmp, mergeWeightCompleted);
                    }
                    catch (Exception ex)
                    {
                        convertMergeModRecordsToFileIdentificationRecords(); // Run conversion now

                        // Error applying merge mod!
                        M3Log.Exception(ex, $@"An error occurred installing mergemod {mergeMod.MergeModFilename}: ");
                        M3Log.Warning(EXITING_INSTALLER_LOGSTR);

                        InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_APPLYING_MERGE_MOD;
                        InstallationResult.ErrorMessage = M3L.GetString(M3L.string_interp_errorApplyingMergeModXY, mergeMod.MergeModFilename, ex.Message);
                        InstallationResult.ErrorTitle = M3L.GetString(M3L.string_errorInstallingMod);
                        return;
                    }
                }

                convertMergeModRecordsToFileIdentificationRecords(); // Run conversion
            }

            // Stage: TLK merge (Game 1)
            if (InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.GAME1_EMBEDDED_TLK) != null)
            {
                SetPercent?.Invoke(0);
                SetAction?.Invoke(M3L.GetString(M3L.string_updatingTLKFiles));

                CompressedTLKMergeData compressedTlkData = null;
                var mergeFiles = InstallOptionsPackage.ModBeingInstalled.PrepareTLKMerge(out compressedTlkData);
                var gameMap = MELoadedFiles.GetFilesLoadedInGame(InstallOptionsPackage.InstallTarget.Game, gameRootOverride: InstallOptionsPackage.InstallTarget.TargetPath);
                int doneMerges = 0;
                int totalTlkMerges = mergeFiles.Count;
                PackageCache cache = new PackageCache() { CacheMaxSize = 12 };

                // 06/05/2022 Change to parallel
                Exception parallelException = null;
                // 01/15/2023 - If in archive you must run in single thread or library may crash app or other errors.
                var maxThreads = InstallOptionsPackage.ModBeingInstalled.IsInArchive ? 1 : 4; // If in archive we can only do one job at a time (7z library is not multi-thread safe)
                Parallel.ForEach(mergeFiles, new ParallelOptions() { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, maxThreads) }, tlkFileMap =>
                {
                    if (parallelException != null)
                        return;

                    SystemSleepManager.PreventSleep(@"ModInstaller");

                    for (int i = 0; i < tlkFileMap.Value.Count; i++)
                    {
                        if (parallelException == null)
                        {
                            try
                            {
                                var tlkXmlFile = tlkFileMap.Value[i];
                                InstallOptionsPackage.ModBeingInstalled.InstallTLKMerge(tlkXmlFile, compressedTlkData, gameMap, i == tlkFileMap.Value.Count - 1, cache, InstallOptionsPackage.InstallTarget, InstallOptionsPackage.ModBeingInstalled, x => basegameIdentificationServiceRecords[x.file] = x);
                            }
                            catch (Exception e)
                            {
                                parallelException = e;
                                M3Log.Exception(e, $@"Error installing TLK merge file {tlkFileMap.Value[i]}");
                            }
                        }
                    }

                    SetPercent?.Invoke((int)(doneMerges * 100.0 / totalTlkMerges));
                    Interlocked.Increment(ref doneMerges);
                });

                if (parallelException != null)
                {
                    M3Log.Warning(EXITING_INSTALLER_LOGSTR);
                    InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                    InstallationResult.ErrorMessage = M3L.GetString(M3L.string_interp_errorInstallingTLKMergesX, parallelException.Message);
                    InstallationResult.ErrorTitle = M3L.GetString(M3L.string_title_errorInstallingTLKMerge);
                    return;
                }
            }

            // Stage: M3CD for LE2, LE3
            if (InstallOptionsPackage.ModBeingInstalled.Game.IsGame2() || InstallOptionsPackage.ModBeingInstalled.Game.IsGame3())
            {
                foreach (var dlcFolderInstalled in addedDLCFolders)
                {
                    M3CConfigMerge.PerformDLCMerge(InstallOptionsPackage.ModBeingInstalled.Game, InstallOptionsPackage.InstallTarget.GetDLCPath(), Path.GetFileName(dlcFolderInstalled));
                }
            }

            // We check for 9.2 or higher here to force mods to update to 9.2 to use this feature,
            // this ensures the developer is aware changes have been made.
            if (InstallOptionsPackage.ModBeingInstalled.ModDescTargetVersion >= ModDescConsts.MODDESC_VERSION_9_2
                && InstallOptionsPackage.ModBeingInstalled.Game.IsLEGame())
            {
                void onMergeUpdate(ProgressInfo pi)
                {
                    SetAction?.Invoke(pi.Status);
                    SetPercent?.Invoke((int)Math.Round(pi.Value));
                }

                foreach (var dlcFolderInstalled in addedDLCFolders)
                {
                    // See if BTP already exists - if it does, do not do merge
                    var dlcName = Path.GetFileName(dlcFolderInstalled);
                    var btpPath = M3CTextureOverrideMerge.GetCombinedTexturePackagePath(InstallOptionsPackage.InstallTarget, dlcName);
                    if (File.Exists(btpPath))
                    {
                        M3Log.Information($@"Detected BTP as part of install, skipping texture override merge");
                        continue;
                    }

                    var pi = new ProgressInfo();
                    pi.OnUpdate = onMergeUpdate;
                    var mergeError = M3CTextureOverrideMerge.PerformDLCMerge(InstallOptionsPackage.InstallTarget, dlcName, !Settings.DeveloperMode, pi);
                    if (mergeError != null)
                    {
                        // An error occurred during merge
                        InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_BTP_BUILD_FAILED;
                        InstallationResult.ErrorMessage = mergeError;
                        InstallationResult.ErrorTitle = M3L.GetString(M3L.string_textureOverrideMergeFailed);
                        InstallationResult.ErrorImage = MessageBoxImage.Error;
                        return;
                    }
                }
            }

            // Stage: 

            // Main installation step has completed
            M3Log.Information(@"Main stage of mod installation has completed");
            SetPercent?.Invoke((int)(numdone * 100.0 / numFilesToInstall));

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
                    MUtilities.DeleteFilesAndFoldersRecursively(outdatedDLCInGame);
                }
            }

            // Install supporting ASI files if necessary
            SetAction?.Invoke(M3L.GetString(M3L.string_installingSupportFiles));
            SetPercentVisibility?.Invoke(false);

            if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME1)
            {
                M3Log.Information(@"Installing supporting ASI files");
                ASIManager.InstallASIToTargetByGroupID(ASIModIDs.ME1_DLC_MOD_ENABLER, @"DLC Mod Enabler", InstallOptionsPackage.InstallTarget); //16 = DLC Mod Enabler
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME2)
            {
                //None right now
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.ME3)
            {
                if (InstallOptionsPackage.ModBeingInstalled.GetJob(ModJob.JobHeader.BALANCE_CHANGES) != null)
                {
                    ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.ME3_BalanceChangesReplacer, @"Balance Changes Replacer", InstallOptionsPackage.InstallTarget);
                }

                if (InstallOptionsPackage.InstallTarget.Supported)
                {
                    ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.ME3_AutoTOC, @"AutoTOC", InstallOptionsPackage.InstallTarget);
                    ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.ME3_ME3Logger, @"ME3Logger-Truncating", InstallOptionsPackage.InstallTarget);
                }
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE1)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE1_AutoTOCLE, @"AutoTOC_LE", InstallOptionsPackage.InstallTarget);
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE1_AutoloadEnabler, @"AutoloadEnabler", InstallOptionsPackage.InstallTarget);
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE2)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE2_AutoTOCLE, @"AutoTOC", InstallOptionsPackage.InstallTarget);
            }
            else if (InstallOptionsPackage.ModBeingInstalled.Game == MEGame.LE3)
            {
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE3_AutoTOCLE, @"AutoTOC", InstallOptionsPackage.InstallTarget);
            }

            // ModDesc 9: Install mod-requested ASI mods
            foreach (var asiMod in InstallOptionsPackage.ModBeingInstalled.ASIModsToInstall)
            {
                var asiToInstall = ASIManager.GetASIModVersion(InstallOptionsPackage.ModBeingInstalled.Game, asiMod.ASIGroupID, asiMod.Version, includeHidden: M3ASIManager.CanSeeHiddenASIs());
                if (asiToInstall != null)
                {
                    ASIManager.InstallASIToTarget(asiToInstall, InstallOptionsPackage.InstallTarget);
                }
                else
                {
                    M3Log.Error($@"Unable to install ASI mod {asiMod.ToString()}: not found in ASI manifest.");
                }
            }

            if (sfarStagingDirectory != null)
            {
                MUtilities.DeleteFilesAndFoldersRecursively(MCoreFilesystem.GetTempDirectory());
            }

            if (numFilesToInstall == numdone)
            {
                // Successful installation
                InstallationResult.Result = EModInstallerResult.INSTALL_SUCCESSFUL;
                SetAction?.Invoke(M3L.GetString(M3L.string_installed));
                if (Settings.ShowInstalledModsInLibrary)
                {
                    var gs = new GameState()
                    {
                        Target = InstallOptionsPackage.InstallTarget,
                        DLCMetaCMMs = installedMetaCmms,
                        BasegameHashes = basegameIdentificationServiceRecords
                    };
                    InstallOptionsPackage.ModBeingInstalled.DetermineIfInstalled(gs);
                }
            }
            else
            {
                M3Log.Warning($@"Number of completed items does not equal the amount of items to install! Number installed {numdone} Number expected: {numFilesToInstall}");
                InstallationResult.Result = EModInstallerResult.INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS;
                InstallationResult.ErrorMessage = M3L.GetString(M3L.string_dialogInstallationSucceededFailedInstallCountCheck);
                InstallationResult.ErrorTitle = M3L.GetString(M3L.string_installationSucceededMaybe);
                InstallationResult.ErrorImage = MessageBoxImage.Warning;
            }

            M3Log.Information(FINISHING_INSTALLER_LOGSTR);
            sw.Stop();
            Debug.WriteLine($@"Installer took {sw.ElapsedMilliseconds}ms");
            if (basegameFilesInstalled.Any() || basegameIdentificationServiceRecords.Any())
            {
                try
                {
                    var files = new List<BasegameFileRecord>(basegameFilesInstalled.Count + basegameIdentificationServiceRecords.Count);
                    files.AddRange(basegameIdentificationServiceRecords.Values);
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

        /// <summary>
        /// Shared code for setting the installation result when unable to delete existing DLC folder
        /// </summary>
        /// <param name="path">DLC folder path that failed to delete</param>
        /// <param name="exceptionMessage">Message for exception to be interwoven into the error message in result</param>
        private void setExistingDLCDeleteFailedResult(string path, string exceptionMessage)
        {
            // Will only be one item in this list
            var tpmi = TPMIService.GetThirdPartyModInfo(Path.GetFileName(path), InstallOptionsPackage.ModBeingInstalled.Game);
            string message = M3L.GetString(M3L.string_interp_unableToFullyDeleteExistingModDirectory, path, exceptionMessage);
            message += @" "; //this is here for localization tool
            if (tpmi != null)
            {
                message += M3L.GetString(M3L.string_interp_thisModShouldBeReinstalled, tpmi.modname);
            }
            else
            {
                message += M3L.GetString(M3L.string_thisModShouldBeReinstalled);
            }


            InstallationResult.Result = EModInstallerResult.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER;
            InstallationResult.ErrorMessage = message;
            InstallationResult.ErrorTitle = M3L.GetString(M3L.string_errorInstallingMod);
        }

        /// <summary>
        /// Installs an RCW mod by applying its changes to the Coalesced.ini. Sets the result information.
        /// </summary>
        private void InstallAttachedRCWMod()
        {
            // TODO: Set install result message!
            M3Log.Information(@"Installing attached RCW mod. Checking Coalesced.ini first to make sure this mod can be safely applied", Settings.LogModInstallation);
            ME2Coalesced me2c = null;
            try
            {
                me2c = new ME2Coalesced(M3Directories.GetCoalescedPath(InstallOptionsPackage.InstallTarget));
            }
            catch (Exception e)
            {
                TelemetryInterposer.TrackError(e);
                M3Log.Error($@"Error parsing ME2Coalesced: {e.Message}. We are aborting this installation");
                InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_BAD_ME2_COALESCED;
                InstallationResult.ErrorMessage = M3L.GetString(M3L.string_dialogInvalidME2Coalesced);
                InstallationResult.ErrorTitle = M3L.GetString(M3L.string_installationAborted);
                return;
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
                    TelemetryInterposer.TrackError(new Exception(@"Unknown Internal ME2 Coalesced File"), new Dictionary<string, string>()
                    {
                        { @"me2mod mod name", rcw.ModName },
                        { @"Missing file", rcwF.FileName }
                    });
                    setMalformedRCWResult();
                    return;
                }

                foreach (var rcwS in rcwF.Sections)
                {
                    var section = me2cF.Value.Sections.FirstOrDefault(x => x.Header.Equals(rcwS.SectionName, StringComparison.InvariantCultureIgnoreCase));
                    if (section == null)
                    {
                        M3Log.Error($@"RCW mod specifies a section in {rcwF.FileName} that does not exist in the local coalesced: {rcwS.SectionName}");
                        TelemetryInterposer.TrackError(new Exception(@"Unknown Internal ME2 Coalesced File Section"), new Dictionary<string, string>()
                        {
                            { @"me2mod mod name", rcw.ModName },
                            { @"File", rcwF.FileName },
                            { @"Missing Section", rcwS.SectionName }
                        });
                        setMalformedRCWResult();
                        return;
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
                            TelemetryInterposer.TrackError(e, new Dictionary<string, string>()
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
                            throw new Exception(@"There was an exception calculating the number of keys in the Ini.", e);
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
            InstallationResult.Result = EModInstallerResult.INSTALL_SUCCESSFUL;
        }

        /// <summary>
        /// Shared result for malformed RCW file
        /// </summary>
        /// <returns></returns>
        private EModInstallerResult setMalformedRCWResult()
        {
            InstallationResult.Result = EModInstallerResult.INSTALL_ABORTED_MALFORMED_RCW_FILE;
            InstallationResult.ErrorMessage = M3L.GetString(M3L.string_dialogInvalidRCWFile);
            InstallationResult.ErrorTitle = M3L.GetString(M3L.string_installationAborted);
            return InstallationResult.Result;
        }

        /// <summary>
        /// Installs files into an SFAR.
        /// </summary>
        /// <param name="sfarJob"></param>
        /// <param name="mod"></param>
        /// <param name="FileInstalledCallback"></param>
        /// <param name="ForcedSourcePath"></param>
        /// <returns></returns>
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
                        TelemetryInterposer.TrackError(new Exception(@"Installing a NEW file into TESTPATCH!"), new Dictionary<string, string>()
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
    }
}
