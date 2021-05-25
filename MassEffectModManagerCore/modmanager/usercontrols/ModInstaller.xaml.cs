using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flurl.Http;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstaller.xaml
    /// </summary>
    public partial class ModInstaller : MMBusyPanelBase, INotifyPropertyChanged
    {
        public static readonly int PERCENT_REFRESH_COOLDOWN = 125;
        private readonly ReadOnlyOption me1ConfigReadOnlyOption = new ReadOnlyOption();

        public ObservableCollectionExtended<AlternateOption> AlternateOptions { get; } = new ObservableCollectionExtended<AlternateOption>();
        public ObservableCollectionExtended<GameTarget> InstallationTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public Mod ModBeingInstalled { get; }
        public bool CompressInstalledPackages { get; set; }
        public GameTarget SelectedGameTarget { get; set; }
        public bool InstallationSucceeded { get; private set; }
        public bool ModIsInstalling { get; set; }
        public bool AllOptionsAreAutomatic { get; private set; }

        /// <summary>
        /// Initializes the Mod Installer panel.
        /// </summary>
        /// <param name="modBeingInstalled">The mod to install</param>
        /// <param name="selectedGameTarget">The default selected game target</param>
        /// <param name="installCompressed">If the checkbox for install compressed should be selected. Set to null to use the mod default</param>
        /// <param name="batchMode"></param>
        public ModInstaller(Mod modBeingInstalled, GameTarget selectedGameTarget, bool? installCompressed = null, bool batchMode = false)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Mod Installer", new WeakReference(this));
            Log.Information($@">>>>>>> Starting mod installer for mod: {modBeingInstalled.ModName} {modBeingInstalled.ModVersionString} for game {modBeingInstalled.Game}. Install source: {(modBeingInstalled.IsInArchive ? @"Archive" : @"Library (disk)")}"); //do not localize
            LoadCommands();
            lastPercentUpdateTime = DateTime.Now;
            this.BatchMode = batchMode;
            this.ModBeingInstalled = modBeingInstalled;
            this.SelectedGameTarget = selectedGameTarget;
            selectedGameTarget.ReloadGameTarget(false); //Reload so we can have consistent state with ALOT on disk
            Action = M3L.GetString(M3L.string_preparingToInstall);
            CompressInstalledPackages = (installCompressed ?? modBeingInstalled.PreferCompressed) && modBeingInstalled.Game > MEGame.ME1;
            InitializeComponent();

            if (!ModBeingInstalled.IsInArchive)
            {
                foreach (var alt in ModBeingInstalled.GetAllAlternates())
                {
                    if (!string.IsNullOrWhiteSpace(alt.ImageAssetName))
                    {
                        alt.LoadImageAsset(modBeingInstalled);
                    }
                }
            }
        }

        private void LoadCommands()
        {
            InstallCommand = new GenericCommand(BeginInstallingMod, CanInstall);

        }
        private bool CanInstall()
        {
            return !PreventInstallUntilTargetChange;
        }

        public GenericCommand InstallCommand { get; set; }


        private DateTime lastPercentUpdateTime;
        public bool InstallationCancelled;

        public enum ModInstallCompletedStatus
        {
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
            INSTALL_FAILED_AMD_PROCESSOR_REQUIRED
        }

        public string Action { get; set; }
        public int Percent { get; set; }
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;
        public bool BatchMode { get; set; }

        private void BeginInstallingMod()
        {
            ModIsInstalling = true;
            if (CheckForGameBackup())
            {
                Log.Information($@"BeginInstallingMod(): {ModBeingInstalled.ModName}");
                NamedBackgroundWorker bw = new NamedBackgroundWorker($@"ModInstaller-{ModBeingInstalled.ModName}");
                bw.WorkerReportsProgress = true;
                bw.DoWork += InstallModBackgroundThread;
                bw.RunWorkerCompleted += ModInstallationCompleted;
                bw.RunWorkerAsync();
            }
            else
            {
                Log.Error(@"User aborted installation because they did not have a backup available");
                InstallationSucceeded = false;
                InstallationCancelled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        private bool CheckForGameBackup()
        {
            var hasAnyGameModificationJobs = ModBeingInstalled.InstallationJobs.Any(x => x.Header != ModJob.JobHeader.CUSTOMDLC && x.Header != ModJob.JobHeader.BALANCE_CHANGES);
            if (!hasAnyGameModificationJobs) return true; //Backup not required for DLC-only mods. Or balance change jobs
            var hasBackup = BackupService.GetGameBackupPath(ModBeingInstalled.Game);
            if (hasBackup == null)
            {
                var installAnyways = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_noBackupForXInstallingY, Utilities.GetGameName(ModBeingInstalled.Game), ModBeingInstalled.ModName), M3L.GetString(M3L.string_noBackup), MessageBoxButton.YesNo, MessageBoxImage.Error);
                return installAnyways == MessageBoxResult.Yes;
            }

            return true; //has backup
        }

        private async void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            bool testrun = false; //change to true to test
            Log.Information(@"Mod Installer Background thread starting");
            if (!Settings.LogModInstallation)
            {
                Log.Information(@"Mod installation logging is off. If you want to view the installation log, turn it on in the settings and apply the mod again.");
            }
            var installationJobs = ModBeingInstalled.InstallationJobs;
            var gameDLCPath = M3Directories.GetDLCPath(SelectedGameTarget);
            if (gameDLCPath != null)
            {
                Directory.CreateDirectory(gameDLCPath); //me1/me2 missing dlc might not have this folder
            }

            //Check we can install
            var missingRequiredDLC = ModBeingInstalled.ValidateRequiredModulesAreInstalled(SelectedGameTarget);
            if (missingRequiredDLC.Count > 0)
            {
                Log.Error(@"Required DLC is missing for installation: " + string.Join(@", ", missingRequiredDLC));
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_REQUIRED_DLC_MISSING, missingRequiredDLC);
                Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            // Check optional DLCs
            if (!ModBeingInstalled.ValidateSingleOptionalRequiredDLCInstalled(SelectedGameTarget))
            {
                Log.Error($@"Mod requires installation of at least one of the following DLC, none of which are installed: {String.Join(',', ModBeingInstalled.OptionalSingleRequiredDLC)}");
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING, ModBeingInstalled.OptionalSingleRequiredDLC);
                Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            //Check/warn on official headers
            if (!PrecheckHeaders(installationJobs))
            {
                //logs handled in precheck
                e.Result = ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES;
                Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            if (ModBeingInstalled.RequiresAMD && !App.IsRunningOnAMD)
            {
                e.Result = ModInstallCompletedStatus.INSTALL_FAILED_AMD_PROCESSOR_REQUIRED;
                Log.Error(@"This mod can only be installed on AMD processors, as it does nothing for Intel users.");
                Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            Utilities.InstallBinkBypass(SelectedGameTarget); //Always install binkw32, don't bother checking if it is already ASI version.

            if (ModBeingInstalled.Game == MEGame.ME2 && ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null && installationJobs.Count == 1)
            {
                Log.Information(@"RCW mod: Beginning RCW mod subinstaller");
                e.Result = InstallAttachedRCWMod();
                Log.Information(@"<<<<<<< Finishing modinstaller");
                return;
            }

            //Prepare queues
            Log.Information(@"Building installation queues");
            (Dictionary<ModJob, (Dictionary<string, Mod.InstallSourceFile> fileMapping, List<string> dlcFoldersBeingInstalled)> unpackedJobMappings,
                List<(ModJob job, string sfarPath, Dictionary<string, Mod.InstallSourceFile> sfarInstallationMapping)> sfarJobs) installationQueues = default;
            try
            {
                installationQueues = ModBeingInstalled.GetInstallationQueues(SelectedGameTarget);
            }
            catch (Exception ex)
            {
                Log.Error(@"Error building installation queues: " + App.FlattenException(ex));
                Log.Information(@"<<<<<<< Exiting modinstaller");
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_ERROR_BUILDING_INSTALLQUEUES, ex.Message);
                return;
            }

            var readOnlyTargets = ModBeingInstalled.GetAllRelativeReadonlyTargets(me1ConfigReadOnlyOption.IsSelected);

            if (SelectedGameTarget.TextureModded)
            {
                //Check if any packages are being installed. If there are, we will block this installation.
                bool installsPackageFile = false;
                foreach (var jobMappings in installationQueues.unpackedJobMappings)
                {
                    installsPackageFile |= jobMappings.Key.MergeMods.Any(); // merge mods will modify packages
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                foreach (var jobMappings in installationQueues.sfarJobs)
                {
                    //Only check if it's not TESTPATCH. TestPatch is classes only and will have no bearing on ALOT
                    if (Path.GetFileName(jobMappings.sfarPath) != @"Patch_001.sfar")
                    {
                        installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                        installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                    }
                }

                if (installsPackageFile)
                {
                    // Todo: ME3, LE warn only
                    // ME1/ME2 trigger abort

                    if (Settings.DeveloperMode)
                    {
                        Log.Warning(@"ALOT is installed and user is attempting to install a mod (in developer mode). Prompting user to cancel installation");

                        bool cancel = false;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_devModeAlotInstalledWarning, ModBeingInstalled.ModName), M3L.GetString(M3L.string_brokenTexturesWarning), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                            cancel = res != MessageBoxResult.Yes;
                        });
                        if (cancel)
                        {
                            e.Result = ModInstallCompletedStatus.USER_CANCELED_INSTALLATION;
                            Log.Information(@"<<<<<<< Exiting modinstaller");
                            return;
                        }
                        Log.Warning(@"User installing mod anyways even with ALOT installed");
                    }
                    else
                    {
                        Log.Error(@"ALOT is installed. Installing mods that install package files after installing ALOT is not permitted.");
                        //ALOT Installed, this is attempting to install a package file
                        e.Result = ModInstallCompletedStatus.INSTALL_FAILED_ALOT_BLOCKING;
                        Log.Information(@"<<<<<<< Exiting modinstaller");
                        return;
                    }
                }
                else
                {
                    Log.Information(@"Game is marked as textured modded, but this mod doesn't install any package files, so it's OK to install");
                }
            }

            Action = M3L.GetString(M3L.string_installing);
            PercentVisibility = Visibility.Visible;
            Percent = 0;

            int numdone = 0;

            //Calculate number of installation tasks beforehand
            int numFilesToInstall = installationQueues.unpackedJobMappings.Select(x => x.Value.fileMapping.Count).Sum();
            numFilesToInstall += installationQueues.sfarJobs.Select(x => x.sfarInstallationMapping.Count).Sum() * (ModBeingInstalled.IsInArchive ? 2 : 1); //*2 as we have to extract and install
            Debug.WriteLine(@"Number of expected installation tasks: " + numFilesToInstall);


            //Stage: Unpacked files build map


            Dictionary<string, string> fullPathMappingDisk = new Dictionary<string, string>();
            Dictionary<int, string> fullPathMappingArchive = new Dictionary<int, string>();
            SortedSet<string> customDLCsBeingInstalled = new SortedSet<string>();
            List<string> mappedReadOnlyTargets = new List<string>();

            foreach (var unpackedQueue in installationQueues.unpackedJobMappings)
            {
                Log.Information(@"Building map of unpacked file destinations");

                foreach (var originalMapping in unpackedQueue.Value.fileMapping)
                {
                    //always unpacked
                    //if (unpackedQueue.Key == ModJob.JobHeader.CUSTOMDLC || unpackedQueue.Key == ModJob.JobHeader.BALANCE_CHANGES || unpackedQueue.Key == ModJob.JobHeader.BASEGAME)
                    //{

                    //Resolve source file path
                    string sourceFile;
                    if (unpackedQueue.Key.JobDirectory == null || originalMapping.Value.IsFullRelativeFilePath)
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, originalMapping.Value.FilePath);
                    }
                    else
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, unpackedQueue.Key.JobDirectory, originalMapping.Value.FilePath);
                    }


                    if (unpackedQueue.Key.Header == ModJob.JobHeader.ME1_CONFIG)
                    {
                        var destFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", originalMapping.Key);
                        if (ModBeingInstalled.IsInArchive)
                        {
                            int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                            fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                            if (archiveIndex == -1)
                            {
                                Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
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

                        var destFile = Path.Combine(unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC ? M3Directories.GetDLCPath(SelectedGameTarget) : SelectedGameTarget.TargetPath, originalMapping.Key); //official

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

                        if (ModBeingInstalled.IsInArchive)
                        {
                            int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                            fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                            if (archiveIndex == -1)
                            {
                                Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                                Debugger.Break();
                            }
                        }
                        fullPathMappingDisk[sourceFile] = destFile; //archive also uses this for redirection
                    }

                    if (readOnlyTargets.Contains(originalMapping.Key))
                    {
                        CLog.Information(@"Adding resolved read only target: " + originalMapping.Key + @" -> " + fullPathMappingDisk[sourceFile], Settings.LogModInstallation);
                        mappedReadOnlyTargets.Add(fullPathMappingDisk[sourceFile]);
                    }
                }
            }

            //Substage: Add SFAR staging targets
            string sfarStagingDirectory = (ModBeingInstalled.IsInArchive && installationQueues.sfarJobs.Count > 0) ? Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), @"SFARJobStaging")).FullName : null; //don't make directory if we don't need one
            if (sfarStagingDirectory != null)
            {
                Log.Information(@"Building list of SFAR staging targets");
                foreach (var sfarJob in installationQueues.sfarJobs)
                {
                    foreach (var fileToInstall in sfarJob.sfarInstallationMapping)
                    {
                        string sourceFile = null;
                        if (fileToInstall.Value.IsFullRelativeFilePath)
                        {
                            sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, fileToInstall.Value.FilePath);
                        }
                        else
                        {
                            sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, sfarJob.job.JobDirectory, fileToInstall.Value.FilePath);
                        }
                        int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                        if (archiveIndex == -1)
                        {
                            Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                            Debugger.Break();
                        }
                        string destFile = Path.Combine(sfarStagingDirectory, sfarJob.job.JobDirectory, fileToInstall.Value.FilePath);
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
            Log.Information(@"Checking there is enough space to install mod (this is only an estimate)");

            long requiredSpaceToInstall = 0L;
            if (ModBeingInstalled.IsInArchive)
            {
                foreach (var f in ModBeingInstalled.Archive.ArchiveFileData)
                {
                    if (fullPathMappingArchive.ContainsKey(f.Index))
                    {
                        //we are installing this file
                        requiredSpaceToInstall += (long)f.Size;
                        CLog.Information(@"Adding to size calculation: " + f.FileName + @", size " + f.Size, Settings.LogModInstallation);

                    }
                    else
                    {
                        CLog.Information(@"Skip adding to size calculation: " + f.FileName, Settings.LogModInstallation);
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

            Utilities.DriveFreeBytes(SelectedGameTarget.TargetPath, out var freeSpaceOnTargetDisk);
            requiredSpaceToInstall = (long)(requiredSpaceToInstall * 1.1); //+10% for some overhead
            Log.Information($@"Mod requires {FileSize.FormatSize(requiredSpaceToInstall)} of disk space to install. We have {FileSize.FormatSize(freeSpaceOnTargetDisk)} available");
            if (requiredSpaceToInstall > (long)freeSpaceOnTargetDisk && freeSpaceOnTargetDisk != 0)
            {
                string driveletter = Path.GetPathRoot(SelectedGameTarget.TargetPath);
                Log.Error($@"Insufficient disk space to install mod. Required: {FileSize.FormatSize(requiredSpaceToInstall)}, available on {driveletter}: {FileSize.FormatSize(freeSpaceOnTargetDisk)}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string message = M3L.GetString(M3L.string_interp_dialogNotEnoughSpaceToInstall, driveletter, ModBeingInstalled.ModName, FileSize.FormatSize(requiredSpaceToInstall).ToString(), FileSize.FormatSize(freeSpaceOnTargetDisk).ToString());
                    M3L.ShowDialog(window, message, M3L.GetString(M3L.string_insufficientDiskSpace), MessageBoxButton.OK, MessageBoxImage.Error);
                });
                e.Result = ModInstallCompletedStatus.INSTALL_ABORTED_NOT_ENOUGH_SPACE;
                Log.Information(@"<<<<<<< Exiting modinstaller");
                return;
            }

            //Delete existing custom DLC mods with same name
            foreach (var cdbi in customDLCsBeingInstalled)
            {
                var path = Path.Combine(gameDLCPath, cdbi);
                if (Directory.Exists(path))
                {
                    Log.Information($@"Deleting existing DLC directory: {path}");
                    try
                    {
                        Utilities.DeleteFilesAndFoldersRecursively(path, true);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        try
                        {
                            // for some reason we don't have permission to do this.
                            Log.Warning(@"Unauthorized access exception deleting the existing DLC mod folder. Perhaps permissions aren't being inherited? Prompting for admin to grant writes to folder, which will then be deleted.");
                            Utilities.CreateDirectoryWithWritePermission(path, true);
                            Utilities.DeleteFilesAndFoldersRecursively(path);
                        }
                        catch (Exception finalException)
                        {
                            Log.Error($@"Error deleting existing mod directory after admin attempt, {path}: {finalException.Message}");
                            e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER, new List<string>(new[] { path, finalException.Message }));
                            Log.Information(@"<<<<<<< Exiting modinstaller");
                            return;
                        }
                    }
                    catch (Exception ge)
                    {
                        Log.Error($@"Error deleting existing mod directory {path}: {ge.Message}");
                        e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER, new List<string>(new[] { path, ge.Message }));
                        Log.Information(@"<<<<<<< Exiting modinstaller");
                        return;
                    }
                }
            }

            var basegameFilesInstalled = new List<string>();
            void FileInstalledIntoSFARCallback(Dictionary<string, Mod.InstallSourceFile> sfarMapping, string targetPath)
            {
                numdone++;
                targetPath = targetPath.Replace('/', '\\').TrimStart('\\');
                var fileMapping = sfarMapping.FirstOrDefault(x => x.Key == targetPath);
                CLog.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Value.FilePath} -> (SFAR) {targetPath}", Settings.LogModInstallation);
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
                CLog.Information($@"[{numdone}/{numFilesToInstall}] Installed: {fileMapping.Key} -> {targetPath}", Settings.LogModInstallation);
                //Debug.WriteLine(@"Installed: " + target);
                Action = M3L.GetString(M3L.string_installing);


                //BASEGAME FILE TELEMETRY
                if (Settings.EnableTelemetry && ModBeingInstalled.ModModMakerID == 0)
                {
                    // ME3 is SFAR DLC so we don't track those. Track if it path has 'DLC' directory and the path of file being installed contains an official DLC directory in it
                    // There is probably better way to do this
                    var shouldTrack = SelectedGameTarget.Game != MEGame.ME3 && targetPath.Contains(@"\DLC\", StringComparison.InvariantCultureIgnoreCase)
                                                                            && targetPath.ContainsAny(MEDirectories.OfficialDLC(SelectedGameTarget.Game).Select(x => $@"\{x}\"), StringComparison.InvariantCultureIgnoreCase);
                    if ((shouldTrack || !targetPath.Contains(@"DLC", StringComparison.InvariantCultureIgnoreCase)) //Only track basegame files, or all official directories if ME1/ME2
                        && targetPath.Contains(SelectedGameTarget.TargetPath) // Must be within the game directory (no config files)
                        && !Path.GetFileName(targetPath).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase)) //no pcconsoletoc
                    {
                        //not installing to DLC
                        basegameFilesInstalled.Add(targetPath);
                    }
                }

                if (ModBeingInstalled.Game > MEGame.ME1 && CompressInstalledPackages && File.Exists(targetPath) && targetPath.RepresentsPackageFilePath())
                {
                    var package = MEPackageHandler.QuickOpenMEPackage(targetPath);
                    if (!package.IsCompressed)
                    {
                        // Compress it
                        // Reopen package fully
                        Log.Information($@"Compressing installed package: {targetPath}");
                        package = MEPackageHandler.OpenMEPackage(targetPath);
                        package.Save(compress: true);
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
            if (!ModBeingInstalled.IsInArchive)
            {
                //Direct copy
                Log.Information($@"Installing {fullPathMappingDisk.Count} unpacked files into game directory");
                try
                {
                    CopyDir.CopyFiles_ProgressBar(fullPathMappingDisk, FileInstalledCallback, testrun);
                    Log.Information(@"Files have been copied");
                }
                catch (Exception ex)
                {
                    Log.Error(@"Error extracting files: " + ex.Message);
                    Crashes.TrackError(ex, new Dictionary<string, string>()
                    {
                        {@"Mod name", ModBeingInstalled.ModName },
                        {@"Version", ModBeingInstalled.ModVersionString}
                    });
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_FILE_COPY;
                    if (Application.Current != null)
                    {
                        // handled here so we can show what failed in string
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_errorCopyingFilesToTarget, ex.Message), M3L.GetString(M3L.string_errorInstallingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    Log.Warning(@"<<<<<<< Aborting modinstaller");
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

                //ModBeingInstalled.Archive.FileExtractionStarted += (sender, args) =>
                //{
                //    //CLog.Information("Extracting mod file for installation: " + args.FileInfo.FileName, Settings.LogModInstallation);
                //};
                List<string> filesInstalled = new List<string>();
                //List<string> filesToInstall = installationQueues.unpackedJobMappings.SelectMany(x => x.Value.fileMapping.Keys).ToList();
                ModBeingInstalled.Archive.FileExtractionFinished += (sender, args) =>
                {
                    if (args.FileInfo.IsDirectory) return; //ignore
                    if (!fullPathMappingArchive.ContainsKey(args.FileInfo.Index))
                    {
                        CLog.Information(@"Skipping extraction of archive file: " + args.FileInfo.FileName, Settings.LogModInstallation);
                        return; //archive extracted this file (in memory) but did not do anything with this file (7z)
                    }
                    FileInstalledCallback(fullPathMappingArchive[args.FileInfo.Index]); //put dest filename here as this func logs the mapping based on the destination
                    filesInstalled.Add(args.FileInfo.FileName);
                    FileInfo dest = new FileInfo(fullPathMappingArchive[args.FileInfo.Index]);
                    if (dest.IsReadOnly)
                        dest.IsReadOnly = false;
                    if (Settings.EnableTelemetry)
                    {
                        if (!fullPathMappingArchive[args.FileInfo.Index].Contains(@"DLC", StringComparison.InvariantCultureIgnoreCase)
                         && fullPathMappingArchive[args.FileInfo.Index].Contains(SelectedGameTarget.TargetPath) &&
                           !Path.GetFileName(fullPathMappingArchive[args.FileInfo.Index]).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //not installing to DLC
                            basegameFilesInstalled.Add(fullPathMappingArchive[args.FileInfo.Index]);
                        }
                    }
                    //Debug.WriteLine($"{args.FileInfo.FileName} as file { numdone}");
                    //Debug.WriteLine(numdone);
                };
                try
                {
                    ModBeingInstalled.Archive.ExtractFiles(SelectedGameTarget.TargetPath, installationRedirectCallback, fullPathMappingArchive.Keys.ToArray()); //directory parameter shouldn't be used here as we will be redirecting everything
                }
                catch (Exception ex)
                {
                    Log.Error(@"Error extracting files: " + ex.Message);
                    Crashes.TrackError(ex, new Dictionary<string, string>()
                    {
                        {@"Mod name", ModBeingInstalled.ModName },
                        {@"Filename", ModBeingInstalled.Archive.FileName },
                    });
                    e.Result = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_ARCHIVE_EXTRACTION;
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_errorWhileExtractingArchiveInstall, ex.Message), M3L.GetString(M3L.string_errorExtractingMod), MessageBoxButton.OK, MessageBoxImage.Error); });
                    }
                    Log.Warning(@"<<<<<<< Aborting modinstaller");
                    return;
                }
            }

            //Write MetaCMM for Custom DLC
            List<string> addedDLCFolders = new List<string>();
            foreach (var v in installationQueues.unpackedJobMappings)
            {
                addedDLCFolders.AddRange(v.Value.dlcFoldersBeingInstalled);
            }
            foreach (var addedDLCFolder in addedDLCFolders)
            {
                Log.Information(@"Writing metacmm file for " + addedDLCFolder);
                var metacmm = Path.Combine(addedDLCFolder, @"_metacmm.txt");
                ModBeingInstalled.HumanReadableCustomDLCNames.TryGetValue(Path.GetFileName(addedDLCFolder), out var assignedDLCName);
                var metaOutLines = new List<string>();

                // Write out MetaCMM Classic
                metaOutLines.Add(assignedDLCName ?? ModBeingInstalled.ModName);
                metaOutLines.Add(ModBeingInstalled.ModVersionString);
                metaOutLines.Add(App.BuildNumber.ToString());
                metaOutLines.Add(Guid.NewGuid().ToString()); // This is not used in Mod Manager 6

                // Write MetaCMM Extended
                if (ModBeingInstalled.IncompatibleDLC.Any())
                {
                    metaOutLines.Add($@"{MetaCMM.PrefixIncompatibleDLC}{string.Join(';', ModBeingInstalled.IncompatibleDLC)}");
                }

                var alternates = ModBeingInstalled.GetAllAlternates().Where(x => x.IsSelected).ToList();
                if (alternates.Any())
                {
                    // I hope this covers all cases. Mods targeting moddesc 6 or lower don't need friendlyname or description, but virtually all of them did
                    // as MM4/5 autonaming was ugly
                    metaOutLines.Add($@"{MetaCMM.PrefixOptionsSelectedOnInstall}{string.Join(';', alternates.Where(x => !string.IsNullOrWhiteSpace(x.FriendlyName)).Select(x => x.FriendlyName))}");
                }

                File.WriteAllLines(metacmm, metaOutLines);
            }

            //Stage: SFAR Installation
            foreach (var sfarJob in installationQueues.sfarJobs)
            {
                InstallIntoSFAR(sfarJob, ModBeingInstalled, FileInstalledIntoSFARCallback, ModBeingInstalled.IsInArchive ? sfarStagingDirectory : null);
            }

            //Stage: Merge Mods
            var allMMs = installationJobs.SelectMany(x => x.MergeMods).ToList();
            var totalMerges = allMMs.Sum(x => x.GetMergeCount());
            int doneMerges = 0;

            void mergeProgressUpdate(int localDone, int localTotal)
            {
                //doneMerges++;
                Percent = (int)(doneMerges * 100.0 / totalMerges);
            }
            foreach (var mergeMod in allMMs)
            {
                mergeMod.ApplyMergeMod(ModBeingInstalled, SelectedGameTarget, ref doneMerges, totalMerges, mergeProgressUpdate);
            }


            //Main installation step has completed
            Log.Information(@"Main stage of mod installation has completed");
            Percent = (int)(numdone * 100.0 / numFilesToInstall);

            //Mark items read only
            foreach (var readonlytarget in mappedReadOnlyTargets)
            {
                Log.Information(@"Setting file to read-only: " + readonlytarget);
                File.SetAttributes(readonlytarget, File.GetAttributes(readonlytarget) | FileAttributes.ReadOnly);
            }

            //Remove outdated custom DLC
            foreach (var outdatedDLCFolder in ModBeingInstalled.OutdatedCustomDLC)
            {
                var outdatedDLCInGame = Path.Combine(gameDLCPath, outdatedDLCFolder);
                if (Directory.Exists(outdatedDLCInGame))
                {
                    Log.Information(@"Deleting outdated custom DLC folder: " + outdatedDLCInGame);
                    Utilities.DeleteFilesAndFoldersRecursively(outdatedDLCInGame);
                }
            }

            //Install supporting ASI files if necessary
            Action = M3L.GetString(M3L.string_installingSupportFiles);
            PercentVisibility = Visibility.Collapsed;
            if (ModBeingInstalled.Game == MEGame.ME1)
            {
                Log.Information(@"Installing supporting ASI files");
                ASIManager.InstallASIToTargetByGroupID(16, @"DLC Mod Enabler", SelectedGameTarget); //16 = DLC Mod Enabler
            }
            else if (ModBeingInstalled.Game == MEGame.ME2)
            {
                //None right now
            }
            else if (ModBeingInstalled.Game == MEGame.ME3)
            {
                if (ModBeingInstalled.GetJob(ModJob.JobHeader.BALANCE_CHANGES) != null)
                {
                    ASIManager.InstallASIToTargetByGroupID(5, @"Balance Changes Replacer", SelectedGameTarget);
                }

                if (SelectedGameTarget.Supported)
                {
                    ASIManager.InstallASIToTargetByGroupID(9, @"AutoTOC", SelectedGameTarget);
                    ASIManager.InstallASIToTargetByGroupID(8, @"ME3Logger-Truncating", SelectedGameTarget);
                }
            }

            if (sfarStagingDirectory != null)
            {
                Utilities.DeleteFilesAndFoldersRecursively(Utilities.GetTempPath());
            }

            if (numFilesToInstall == numdone)
            {
                e.Result = ModInstallCompletedStatus.INSTALL_SUCCESSFUL;
                Action = M3L.GetString(M3L.string_installed);
            }
            else
            {
                Log.Warning($@"Number of completed items does not equal the amount of items to install! Number installed {numdone} Number expected: {numFilesToInstall}");
                e.Result = ModInstallCompletedStatus.INSTALL_WRONG_NUMBER_OF_COMPLETED_ITEMS;
            }

            Log.Information(@"<<<<<<< Finishing modinstaller");
            sw.Stop();
            Debug.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            //Submit basegame telemetry in async way
            if (basegameFilesInstalled.Any() /*&& !Settings.DeveloperMode*/) //no dev mode as it could expose files user is working on.
            {
                //BackgroundWorker bw = new NamedBackgroundWorker("BASEGAMECLOUDDB_TELEMETRY");
                //bw.DoWork += (a, b) =>
                //{
                try
                {
                    var files = new List<BasegameFileIdentificationService.BasegameCloudDBFile>();
                    foreach (var file in basegameFilesInstalled)
                    {
                        files.Add(new BasegameFileIdentificationService.BasegameCloudDBFile(file, SelectedGameTarget, ModBeingInstalled));
                    }
                    string output = JsonConvert.SerializeObject(files);

                    await @"https://me3tweaks.com/modmanager/services/basegamefileid".PostStringAsync(output);
                }
                catch (Exception ex)
                {
                    Log.Error(@"Error uploading basegame telemetry: " + ex.Message);
                }
                //};
                //bw.RunWorkerAsync();
            }
        }

        private ModInstallCompletedStatus InstallAttachedRCWMod()
        {
            CLog.Information(@"Installing attached RCW mod. Checking Coalesced.ini first to make sure this mod can be safely applied", Settings.LogModInstallation);
            ME2Coalesced me2c = null;
            try
            {
                me2c = new ME2Coalesced(M3Directories.GetCoalescedPath(SelectedGameTarget));
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);
                Log.Error(@"Error parsing ME2Coalesced: " + e.Message + @". We will abort this installation");
                return ModInstallCompletedStatus.INSTALL_FAILED_BAD_ME2_COALESCED;
            }
            RCWMod rcw = ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD).RCW;
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
                    Log.Error(@"RCW mod specifies a file in coalesced that does not exist in the local one: " + rcwF.FileName);
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
                        Log.Error($@"RCW mod specifies a section in {rcwF.FileName} that does not exist in the local coalesced: {rcwS.SectionName}");
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
                        Log.Error($@"RCW section is null! We didn't find {rcwS.SectionName}");
                        Analytics.TrackEvent(@"RCW Section Null", new Dictionary<string, string>
                        {
                            {@"MissingSection", rcwS.SectionName},
                            {@"InFile", rcwF.FileName}
                        });
                    }
                    foreach (var key in section.Entries)
                    {
                        try
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
                        catch (Exception e)
                        {
                            Crashes.TrackError(e, new Dictionary<string, string>()
                            {
                                {@"FailingEntry", key.RawText},
                                {@"Failing mod", ModBeingInstalled.ModName}
                            });
                            Log.Fatal(@"Crash information:");
                            Log.Warning(@"Section: " + section?.Header);
                            Log.Warning(@"Entries: ");
                            foreach (var k in section.Entries)
                            {
                                Log.Warning($@" - {k.RawText}");
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
                                CLog.Information($@"Removing ini entry {entry.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                section.Entries.RemoveAt(i);
                                deletedSomething = true;
                            }
                        }
                        if (!deletedSomething)
                        {
                            Log.Warning($@"Did not find anything to remove for key {itemToDelete.Key} with value {itemToDelete.Value}");
                        }
                    }

                    foreach (var itemToAdd in rcwS.KeysToAdd)
                    {
                        var existingEntries = section.Entries.Where(x => x.Key.Equals(itemToAdd.Key, StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if (existingEntries.Count <= 0)
                        {
                            //just add it
                            CLog.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                            section.Entries.Add(itemToAdd);
                        }
                        else if (existingEntries.Count > 1)
                        {
                            //Supports multi. Add this key - but making sure the data doesn't already exist!
                            if (existingEntries.Any(x => x.Value == itemToAdd.Value)) //case sensitive
                            {
                                //Duplicate.
                                CLog.Information($@"Not adding duplicate ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                            }
                            else
                            {
                                //Not duplicate - installing key
                                CLog.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
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
                                    CLog.Information($@"Not adding duplicate ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                }
                                else
                                {
                                    //Not duplicate - installing key
                                    CLog.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
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

        private bool InstallIntoSFAR((ModJob job, string sfarPath, Dictionary<string, Mod.InstallSourceFile> fileMapping) sfarJob, Mod mod, Action<Dictionary<string, Mod.InstallSourceFile>, string> FileInstalledCallback = null, string ForcedSourcePath = null)
        {

            int numfiles = sfarJob.fileMapping.Count;

            //Open SFAR
            Log.Information($@"Installing {sfarJob.fileMapping.Count} files into {sfarJob.sfarPath}");
            DLCPackage dlc = new DLCPackage(sfarJob.sfarPath);

            //Add or Replace file install
            foreach (var entry in sfarJob.fileMapping)
            {
                string entryPath = entry.Key.Replace('\\', '/');
                if (!entryPath.StartsWith('/')) entryPath = '/' + entryPath; //Ensure path starts with /
                int index = dlc.FindFileEntry(entryPath);
                //Todo: For archive to immediate installation we will need to modify this ModPath value to point to some temporary directory
                //where we have extracted files destined for SFAR files as we cannot unpack solid archives to streams.
                var sourcePath = Path.Combine(ForcedSourcePath ?? mod.ModPath, sfarJob.job.JobDirectory, entry.Value.FilePath);
                if (entry.Value.IsFullRelativeFilePath)
                {
                    sourcePath = Path.Combine(ForcedSourcePath ?? mod.ModPath, entry.Value.FilePath);
                }
                if (index >= 0)
                {
                    dlc.ReplaceEntry(sourcePath, index);
                    CLog.Information(@"Replaced file within SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                else
                {
                    if (sfarJob.job.Header == ModJob.JobHeader.TESTPATCH)
                    {
                        Debugger.Break();
                        Log.Fatal(@"Installing a NEW file into TESTPATCH! This will break the game. This should be immediately reported to Mgamerz on Discord.");
                        Crashes.TrackError(new Exception(@"Installing a NEW file into TESTPATCH!"), new Dictionary<string, string>()
                        {
                            {@"Mod name", mod.ModName}
                        });
                    }
                    dlc.AddFileQuick(sourcePath, entryPath);
                    CLog.Information(@"Added new file to SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                FileInstalledCallback?.Invoke(sfarJob.fileMapping, entryPath);
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
            //if (ModBeingInstalled.Game != MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
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

                if (!M3Directories.IsOfficialDLCInstalled(job.Header, SelectedGameTarget))
                {
                    Log.Warning($@"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                    //Prompt user
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dlcName = ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header];
                        string resolvedName = dlcName;
                        MEDirectories.OfficialDLCNames(ModBeingInstalled.Game).TryGetValue(dlcName, out resolvedName);
                        string message = M3L.GetString(M3L.string_interp_dialogOfficialTargetDLCNotInstalled, ModBeingInstalled.ModName, dlcName, resolvedName);
                        if (job.RequirementText != null)
                        {
                            message += M3L.GetString(M3L.string_dialogJobDescriptionMessageHeader);
                            message += $"\n{job.RequirementText}"; //Do not localize
                        }

                        message += M3L.GetString(M3L.string_dialogJobDescriptionMessageFooter);
                        MessageBoxResult result = M3L.ShowDialog(window, message, M3L.GetString(M3L.string_dialogJobDescriptionMessageTitle, MEDirectories.OfficialDLCNames(ModBeingInstalled.Game)[ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header]]), MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.No)
                        {
                            cancel = true;
                            return;
                        }

                    });
                    if (cancel)
                    {
                        Log.Error(@"User canceling installation");

                        return false;
                    }

                    Log.Warning(@"User continuing installation anyways");
                }
                else
                {
                    CLog.Information(@"Official headers check passed for header " + job.Header, Settings.LogModInstallation);
                }
            }

            return true;
        }

        private void ModInstallationCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var telemetryResult = ModInstallCompletedStatus.NO_RESULT_CODE;
            if (e.Error != null)
            {
                Log.Error(@"An error occurred during mod installation.");
                Log.Error(App.FlattenException(e.Error));
                telemetryResult = ModInstallCompletedStatus.INSTALL_FAILED_EXCEPTION_IN_MOD_INSTALLER;
                M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_errorOccuredDuringInstallation, App.FlattenException(e.Error)), M3L.GetString(M3L.string_error));
            }
            else
            {
                if (e.Result is (ModInstallCompletedStatus mcis1, string message1))
                {
                    Log.Error(@"An error occurred during mod installation.");
                    telemetryResult = mcis1;
                    M3L.ShowDialog(mainwindow, message1, mcis1.ToString()); // title won't be localized as it's error code
                }
                else if (e.Result is ModInstallCompletedStatus mcis)
                {
                    telemetryResult = mcis;
                    //Success, canceled (generic and handled), ALOT canceled
                    InstallationSucceeded = mcis == ModInstallCompletedStatus.INSTALL_SUCCESSFUL;
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
                        M3L.ShowDialog(window, @"This mod can only be installed on a system with an AMD processor.", M3L.GetString(M3L.string_cannotInstallMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES || mcis == ModInstallCompletedStatus.USER_CANCELED_INSTALLATION || mcis == ModInstallCompletedStatus.INSTALL_ABORTED_NOT_ENOUGH_SPACE)
                    {
                        InstallationCancelled = true;
                    }
                }
                else if (e.Result is (ModInstallCompletedStatus result, List<string> items))
                {
                    telemetryResult = result;
                    //Failures with results
                    Log.Warning(@"Installation failed with status " + result.ToString());
                    switch (result)
                    {
                        case ModInstallCompletedStatus.INSTALL_FAILED_REQUIRED_DLC_MISSING:
                            {
                                string dlcText = "";
                                foreach (var dlc in items)
                                {
                                    var info = ThirdPartyServices.GetThirdPartyModInfo(dlc, ModBeingInstalled.Game);
                                    if (info != null)
                                    {
                                        dlcText += $"\n - {info.modname} ({dlc})"; //Do not localize
                                    }
                                    else
                                    {
                                        dlcText += $"\n - {dlc}"; //Do not localize
                                    }
                                }

                                InstallationCancelled = true;
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogRequiredContentMissing, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            break;
                        case ModInstallCompletedStatus.INSTALL_FAILED_SINGLEREQUIRED_DLC_MISSING:
                            {
                                string dlcText = "";
                                foreach (var dlc in items)
                                {
                                    var info = ThirdPartyServices.GetThirdPartyModInfo(dlc, ModBeingInstalled.Game);
                                    if (info != null)
                                    {
                                        dlcText += $"\n - {info.modname} ({dlc})"; //Do not localize
                                    }
                                    else
                                    {
                                        dlcText += $"\n - {dlc}"; //Do not localize
                                    }
                                }

                                InstallationCancelled = true;
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_error_singleRequiredDlcMissing, ModBeingInstalled.ModName, dlcText), M3L.GetString(M3L.string_requiredContentMissing), MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;
                        case ModInstallCompletedStatus.INSTALL_FAILED_COULD_NOT_DELETE_EXISTING_FOLDER:
                            // Will only be one item in this list
                            var tpmi = ThirdPartyServices.GetThirdPartyModInfo(Path.GetFileName(items[0]), ModBeingInstalled.Game);
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
                    Log.Fatal(@"The application is going to crash due to a sanity check failure. Please report this to ME3Tweaks so this can be fixed.");

                    // Once this issue has been fixed these lines can be commented out or removed (June 14 2020)
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_appAboutToCrashYouFoundBug), M3L.GetString(M3L.string_appCrash), MessageBoxButton.OK, MessageBoxImage.Error);
                    Utilities.OpenWebpage(App.DISCORD_INVITE_LINK);
                    // End bug message
                    if (e.Result == null)
                    {
                        Log.Fatal(@"Mod installer did not have result code (null). This should be caught and handled, but it wasn't!");
                        throw new Exception(@"Mod installer did not have result code (null). This should be caught and handled, but it wasn't!");
                    }
                    else
                    {
                        Log.Fatal(@"Mod installer did not have parsed result code. This should be caught and handled, but it wasn't. The returned object was: " + e.Result.GetType() + @". The data was " + e.Result);
                        throw new Exception(@"Mod installer did not have parsed result code. This should be caught and handled, but it wasn't. The returned object was: " + e.Result.GetType() + @". The data was " + e.Result);
                    }
                }
            }

            var telemetryInfo = new Dictionary<string, string>()
            {
                {@"Mod name", $@"{ModBeingInstalled.ModName} {ModBeingInstalled.ModVersionString}"},
                {@"Installed from", ModBeingInstalled.IsInArchive ? @"Archive" : @"Library"},
                {@"Type", ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null ? @"RCW .me2mod" : @"Standard"},
                {@"Game", ModBeingInstalled.Game.ToString()},
                {@"Result", telemetryResult.ToString()},
                {@"Author", ModBeingInstalled.ModDeveloper}
            };

            string alternateOptionsPicked = "";
            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                foreach (var af in job.AlternateFiles)
                {
                    if (string.IsNullOrWhiteSpace(af.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{af.FriendlyName}={af.IsSelected.ToString()}";
                }
                foreach (var ad in job.AlternateDLCs)
                {
                    if (string.IsNullOrWhiteSpace(ad.FriendlyName)) continue;
                    if (!string.IsNullOrWhiteSpace(alternateOptionsPicked)) alternateOptionsPicked += @";";
                    alternateOptionsPicked += $@"{ad.FriendlyName}={ad.IsSelected.ToString()}";
                }
            }

            if (!string.IsNullOrWhiteSpace(alternateOptionsPicked))
            {
                telemetryInfo[@"Alternate Options Selected"] = alternateOptionsPicked;
            }

            Analytics.TrackEvent(@"Installed a mod", telemetryInfo);
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
                        if (ad.GroupName != null && ad.IsSelected) return; //Cannot deselect group
                        ad.IsSelected = !ad.IsSelected;
                    }
                }
                else if (grid.DataContext is AlternateFile af && af.IsManual)
                {
                    if (af.GroupName != null && af.IsSelected) return; //Cannot deselect group
                    af.IsSelected = !af.IsSelected;
                    Debug.WriteLine(@"Is selected: " + af.IsSelected);
                }
                else if (grid.DataContext is ReadOnlyOption ro)
                {
                    ro.IsSelected = !ro.IsSelected;
                }
            }
        }

        public override void OnPanelVisible()
        {
            GC.Collect(); //this should help with the oddities of missing radio button's somehow still in the visual tree from busyhost
            SetupOptions(true);
        }

        private void SetupOptions(bool initialSetup)
        {
            AlternateOptions.ClearEx();
            //Write check
            var canWrite = Utilities.IsDirectoryWritable(SelectedGameTarget.TargetPath);
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

            if (ModBeingInstalled.Game != MEGame.Unknown)
            {
                //Detect incompatible DLC
                var dlcMods = VanillaDatabaseService.GetInstalledDLCMods(SelectedGameTarget);
                if (ModBeingInstalled.IncompatibleDLC.Any())
                {
                    //Check for incompatible DLC.
                    List<string> incompatibleDLC = new List<string>();
                    foreach (var incompat in ModBeingInstalled.IncompatibleDLC)
                    {
                        if (dlcMods.Contains(incompat, StringComparer.InvariantCultureIgnoreCase))
                        {
                            var tpmi = ThirdPartyServices.GetThirdPartyModInfo(incompat, ModBeingInstalled.Game);
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
                            var tpmi = ThirdPartyServices.GetThirdPartyModInfo(outdatedItem, ModBeingInstalled.Game);
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
                AlternateOptions.Add(me1ConfigReadOnlyOption);
                AllOptionsAreAutomatic = false;
            }

            foreach (var job in ModBeingInstalled.InstallationJobs)
            {
                AlternateOptions.AddRange(job.AlternateDLCs);
                AlternateOptions.AddRange(job.AlternateFiles);
            }

            SortOptions();

            foreach (AlternateOption o in AlternateOptions)
            {
                if (o is AlternateDLC altdlc)
                {
                    altdlc.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled);
                    if (altdlc.IsManual) AllOptionsAreAutomatic = false;
                }
                else if (o is AlternateFile altfile)
                {
                    altfile.SetupInitialSelection(SelectedGameTarget, ModBeingInstalled);
                    if (altfile.IsManual) AllOptionsAreAutomatic = false;
                }
            }

            if (AlternateOptions.Count == 0)
            {
                AllOptionsAreAutomatic = false; //Don't show the UI for this
            }

            var targets = mainwindow.InstallationTargets.Where(x => x.Game == ModBeingInstalled.Game).ToList();
            if (ModBeingInstalled.IsInArchive && targets.Count == 1 && AlternateOptions.Count == 0)
            {
                // All available options were chosen already (compression would come from import dialog)
                BeginInstallingMod();
            }
            else if ((targets.Count == 1 || BatchMode) && AlternateOptions.Count == 0 && (BatchMode || Settings.PreferCompressingPackages || ModBeingInstalled.Game == MEGame.ME1))
            {
                // ME1 can't compress. If user has elected to compress packages, and there are no alternates/additional targets, just begin installation
                CompressInstalledPackages = Settings.PreferCompressingPackages && ModBeingInstalled.Game > MEGame.ME1;
                BeginInstallingMod();
            }
            else
            {
                // Set the list of targets.
                InstallationTargets.ReplaceAll(targets);
            }
        }

        public bool PreventInstallUntilTargetChange { get; set; }

        public void OnSelectedGameTargetChanged(object oldT, object newT)
        {
            if (oldT != null && newT != null)
            {
                PreventInstallUntilTargetChange = false;
                SetupOptions(false);
            }
        }

        private void SortOptions()
        {
            List<AlternateOption> newOptions = new List<AlternateOption>();
            newOptions.AddRange(AlternateOptions.Where(x => x.IsAlways));
            newOptions.AddRange(AlternateOptions.Where(x => x is ReadOnlyOption));
            newOptions.AddRange(AlternateOptions.Where(x => !x.IsAlways && !(x is ReadOnlyOption)));
            AlternateOptions.ReplaceAll(newOptions);
        }

        protected override void OnClosing(DataEventArgs e)
        {
            foreach (var ao in AlternateOptions)
            {
                ao.ReleaseLoadedImageAsset();
            }
            AlternateOptions.ClearEx(); //remove collection of items

            if (ModBeingInstalled.Archive != null)
            {
                ModBeingInstalled.Archive.Dispose();
                ModBeingInstalled.Archive = null;
            }

            base.OnClosing(DataEventArgs.Empty);
        }

        private void DebugPrintInstallationQueue_Click(object sender, RoutedEventArgs e)
        {
            if (ModBeingInstalled != null)
            {
                var queues = ModBeingInstalled.GetInstallationQueues(SelectedGameTarget);
                Debug.WriteLine(@"Installation Queue:");
                foreach (var job in queues.Item1)
                {
                    foreach (var file in job.Value.unpackedJobMapping)
                    {
                        Debug.WriteLine($@"[UNPACKED {job.Key.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
                    }
                }

                foreach (var job in queues.Item2)
                {
                    foreach (var file in job.Item3)
                    {
                        Debug.WriteLine($@"[SFAR {job.job.Header.ToString()}] {file.Value.FilePath} => {file.Key}");
                    }
                }
            }
        }
    }
}
