using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.sfar;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.VisualBasic;
using Serilog;
using SevenZip;
namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstaller.xaml
    /// </summary>
    public partial class ModInstaller : MMBusyPanelBase
    {
        public ObservableCollectionExtended<object> AlternateOptions { get; } = new ObservableCollectionExtended<object>();

        public bool InstallationSucceeded { get; private set; }
        public static readonly int PERCENT_REFRESH_COOLDOWN = 125;
        public bool ModIsInstalling { get; set; }
        public bool AllOptionsAreAutomatic { get; private set; }
        private readonly ReadOnlyOption me1ConfigReadOnlyOption = new ReadOnlyOption();
        public ModInstaller(Mod modBeingInstalled, GameTarget gameTarget)
        {
            MemoryAnalyzer.AddTrackedMemoryItem("Mod Installer", new WeakReference(this));
            Log.Information(@"Starting mod installer for mod: " + modBeingInstalled.ModName + " for game " + modBeingInstalled.Game);
            DataContext = this;
            lastPercentUpdateTime = DateTime.Now;
            this.ModBeingInstalled = modBeingInstalled;
            this.gameTarget = gameTarget;
            Action = M3L.GetString(M3L.string_preparingToInstall);
            InitializeComponent();
        }


        public Mod ModBeingInstalled { get; }
        private GameTarget gameTarget;
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
            INSTALL_FAILED_MALFORMED_RCW_FILE
        }



        public string Action { get; set; }
        public int Percent { get; set; }
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;

        private void BeginInstallingMod()
        {
            ModIsInstalling = true;
            Log.Information($@"BeginInstallingMod(): {ModBeingInstalled.ModName}");
            NamedBackgroundWorker bw = new NamedBackgroundWorker($@"ModInstaller-{ModBeingInstalled.ModName}");
            bw.WorkerReportsProgress = true;
            bw.DoWork += InstallModBackgroundThread;
            bw.RunWorkerCompleted += ModInstallationCompleted;
            //bw.ProgressChanged += ModProgressChanged;
            bw.RunWorkerAsync();
        }


        private void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            Log.Information(@"Mod Installer Background thread starting");
            var installationJobs = ModBeingInstalled.InstallationJobs;
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);

            Directory.CreateDirectory(gameDLCPath); //me1/me2 missing dlc might not have this folder

            //Check we can install
            var missingRequiredDLC = ModBeingInstalled.ValidateRequiredModulesAreInstalled(gameTarget);
            if (missingRequiredDLC.Count > 0)
            {
                e.Result = (ModInstallCompletedStatus.INSTALL_FAILED_REQUIRED_DLC_MISSING, missingRequiredDLC);
                return;
            }


            //Check/warn on official headers
            if (!PrecheckHeaders(installationJobs))
            {
                e.Result = ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES;
                return;
            }

            //todo: If statment on this
            Utilities.InstallBinkBypass(gameTarget); //Always install binkw32, don't bother checking if it is already ASI version.

            if (ModBeingInstalled.Game == Mod.MEGame.ME2 && ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null && installationJobs.Count == 1)
            {
                e.Result = InstallAttachedRCWMod();
                return;
            }

            //Prepare queues
            (Dictionary<ModJob, (Dictionary<string, string> fileMapping, List<string> dlcFoldersBeingInstalled)> unpackedJobMappings,
                List<(ModJob job, string sfarPath, Dictionary<string, string> sfarInstallationMapping)> sfarJobs) installationQueues =
                ModBeingInstalled.GetInstallationQueues(gameTarget);

            var readOnlyTargets = ModBeingInstalled.GetAllRelativeReadonlyTargets(me1ConfigReadOnlyOption.IsSelected);

            if (gameTarget.ALOTInstalled)
            {
                //Check if any packages are being installed. If there are, we will block this installation.
                bool installsPackageFile = false;
                foreach (var jobMappings in installationQueues.unpackedJobMappings)
                {
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                foreach (var jobMappings in installationQueues.sfarJobs)
                {
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(@".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                if (installsPackageFile)
                {
                    if (Settings.DeveloperMode)
                    {
                        Log.Warning(@"ALOT is installed and user is attempting to install a mod (in developer mode). Prompting user to cancel installation");

                        bool cancel = false;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var res = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_interp_devModeAlotInstalledWarning, ModBeingInstalled.ModName), M3L.GetString(M3L.string_brokenTexturesWarning), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                            cancel = res == MessageBoxResult.No;
                        });
                        if (cancel)
                        {
                            e.Result = ModInstallCompletedStatus.USER_CANCELED_INSTALLATION;
                            return;
                        }
                        Log.Warning(@"User installing mod anyways even with ALOT installed");
                    }
                    else
                    {
                        Log.Error(@"ALOT is installed. Installing mods that install package files after installing ALOT is not permitted.");
                        //ALOT Installed, this is attempting to install a package file
                        e.Result = ModInstallCompletedStatus.INSTALL_FAILED_ALOT_BLOCKING;
                        return;
                    }
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
            void FileInstalledCallback(string target)
            {
                numdone++;
                Debug.WriteLine(@"Installed: " + target);
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

            //Stage: Unpacked files build map
            Dictionary<string, string> fullPathMappingDisk = new Dictionary<string, string>();
            Dictionary<int, string> fullPathMappingArchive = new Dictionary<int, string>();
            SortedSet<string> customDLCsBeingInstalled = new SortedSet<string>();
            List<string> mappedReadOnlyTargets = new List<string>();
            foreach (var unpackedQueue in installationQueues.unpackedJobMappings)
            {
                foreach (var originalMapping in unpackedQueue.Value.fileMapping)
                {
                    //always unpacked
                    //if (unpackedQueue.Key == ModJob.JobHeader.CUSTOMDLC || unpackedQueue.Key == ModJob.JobHeader.BALANCE_CHANGES || unpackedQueue.Key == ModJob.JobHeader.BASEGAME)
                    //{

                    //Resolve source file path
                    string sourceFile;
                    if (unpackedQueue.Key.JobDirectory == null)
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, originalMapping.Value);
                    }
                    else
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, unpackedQueue.Key.JobDirectory, originalMapping.Value);
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

                        var destFile = Path.Combine(unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC ? MEDirectories.DLCPath(gameTarget) : gameTarget.TargetPath, originalMapping.Key); //official

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
                        CLog.Information("Adding resolved read only target: " + originalMapping.Key + " -> " + fullPathMappingDisk[sourceFile], Settings.LogModInstallation);
                        mappedReadOnlyTargets.Add(fullPathMappingDisk[sourceFile]);
                    }
                    //}
                }
            }

            //Substage: Add SFAR staging targets
            string sfarStagingDirectory = (ModBeingInstalled.IsInArchive && installationQueues.sfarJobs.Count > 0) ? Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), @"SFARJobStaging")).FullName : null; //don't make directory if we don't need one
            if (sfarStagingDirectory != null)
            {
                foreach (var sfarJob in installationQueues.sfarJobs)
                {
                    foreach (var fileToInstall in sfarJob.sfarInstallationMapping)
                    {
                        string sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, sfarJob.job.JobDirectory, fileToInstall.Value);
                        int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile, StringComparer.InvariantCultureIgnoreCase);
                        if (archiveIndex == -1)
                        {
                            Log.Error($@"Archive Index is -1 for file {sourceFile}. This will probably throw an exception!");
                            Debugger.Break();
                        }
                        string destFile = Path.Combine(sfarStagingDirectory, sfarJob.job.JobDirectory, fileToInstall.Value);
                        fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                        fullPathMappingDisk[sourceFile] = destFile; //used for redirection
                        Debug.WriteLine($@"SFAR Disk Staging: {fileToInstall.Key} => {destFile}");
                    }
                }
            }

            //Delete existing custom DLC mods with same name
            foreach (var cdbi in customDLCsBeingInstalled)
            {
                var path = Path.Combine(gameDLCPath, cdbi);
                if (Directory.Exists(path))
                {
                    Log.Information($@"Deleting existing DLC directory: {path}");
                    Utilities.DeleteFilesAndFoldersRecursively(path);
                }
            }

            //Stage: Unpacked files installation
            if (!ModBeingInstalled.IsInArchive)
            {
                //Direct copy
                Log.Information($@"Installing {fullPathMappingDisk.Count} unpacked files into game directory");
                CopyDir.CopyFiles_ProgressBar(fullPathMappingDisk, FileInstalledCallback);
            }
            else
            {
                Action = M3L.GetString(M3L.string_loadingModArchive);
                //Extraction to destination
                string installationRedirectCallback(ArchiveFileInfo info)
                {
                    var inArchivePath = info.FileName;
                    var redirectedPath = fullPathMappingDisk[inArchivePath];
                    Debug.WriteLine($@"Redirecting {inArchivePath} to {redirectedPath}");
                    return redirectedPath;
                }

                ModBeingInstalled.Archive.FileExtractionStarted += (sender, args) =>
                {
                    //CLog.Information("Extracting mod file for installation: " + args.FileInfo.FileName, Settings.LogModInstallation);
                };
                List<string> filesInstalled = new List<string>();
                List<string> filesToInstall = installationQueues.unpackedJobMappings.SelectMany(x => x.Value.fileMapping.Keys).ToList();
                ModBeingInstalled.Archive.FileExtractionFinished += (sender, args) =>
                {
                    if (args.FileInfo.IsDirectory) return; //ignore
                    if (!fullPathMappingArchive.ContainsKey(args.FileInfo.Index)) return; //archive extracted this file (in memory) but did not do anything with this file (7z)
                    FileInstalledCallback(args.FileInfo.FileName);
                    filesInstalled.Add(args.FileInfo.FileName);
                    //Debug.WriteLine($"{args.FileInfo.FileName} as file { numdone}");
                    //Debug.WriteLine(numdone);
                };
                ModBeingInstalled.Archive.ExtractFiles(gameTarget.TargetPath, installationRedirectCallback, fullPathMappingArchive.Keys.ToArray()); //directory parameter shouldn't be used here as we will be redirecting everything
            }

            //Write MetaCMM
            List<string> addedDLCFolders = new List<string>();
            foreach (var v in installationQueues.unpackedJobMappings)
            {
                addedDLCFolders.AddRange(v.Value.dlcFoldersBeingInstalled);
            }
            foreach (var addedDLCFolder in addedDLCFolders)
            {
                var metacmm = Path.Combine(addedDLCFolder, @"_metacmm.txt");
                ModBeingInstalled.HumanReadableCustomDLCNames.TryGetValue(Path.GetFileName(addedDLCFolder), out var assignedDLCName);
                string contents = $"{assignedDLCName ?? ModBeingInstalled.ModName}\n{ModBeingInstalled.ModVersionString}\n{App.BuildNumber}\n{Guid.NewGuid().ToString()}"; //Do not localize
                File.WriteAllText(metacmm, contents);
            }

            //Stage: SFAR Installation
            foreach (var sfarJob in installationQueues.sfarJobs)
            {
                InstallIntoSFAR(sfarJob, ModBeingInstalled, FileInstalledCallback, ModBeingInstalled.IsInArchive ? sfarStagingDirectory : null);
            }


            //Main installation step has completed
            CLog.Information(@"Main stage of mod installation has completed", Settings.LogModInstallation);
            Percent = (int)(numdone * 100.0 / numFilesToInstall);

            //Mark items read only
            foreach (var readonlytarget in mappedReadOnlyTargets)
            {
                CLog.Information("Setting file to read-only: " + readonlytarget, Settings.LogModInstallation);
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
            //Todo: Upgrade to version detection code from ME3EXP to prevent conflicts

            Action = M3L.GetString(M3L.string_installingSupportFiles);
            CLog.Information(@"Installing supporting ASI files", Settings.LogModInstallation);
            if (ModBeingInstalled.Game == Mod.MEGame.ME1)
            {
                //Todo: Convert to ASI Manager installer
                Utilities.InstallEmbeddedASI(@"ME1-DLC-ModEnabler-v1.0", 1.0, gameTarget); //Todo: Switch to ASI Manager
            }
            else if (ModBeingInstalled.Game == Mod.MEGame.ME2)
            {
                //None right now
            }
            else
            {
                if (ModBeingInstalled.GetJob(ModJob.JobHeader.BALANCE_CHANGES) != null)
                {
                    //Todo: Convert to ASI Manager installer
                    Utilities.InstallEmbeddedASI(@"BalanceChangesReplacer-v2.0", 2.0, gameTarget); //todo: Switch to ASI Manager
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
        }

        private ModInstallCompletedStatus InstallAttachedRCWMod()
        {
            CLog.Information(@"Installing attached RCW mod. Checking Coalesced.ini first to make sure this mod can be safely applied", Settings.LogModInstallation);
            ME2Coalesced me2c = new ME2Coalesced(ME2Directory.CoalescedPath(gameTarget));
            RCWMod rcw = ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD).RCW;
            foreach (var rcwF in rcw.Files)
            {
                var me2cF = me2c.Inis.FirstOrDefault(x => x.Key == rcwF.FileName);
                if (me2cF.Key == null)
                {
                    Log.Error(@"RCW mod specifies a file in coalesced that does not exist in the local one: " + rcwF);
                    return ModInstallCompletedStatus.INSTALL_FAILED_MALFORMED_RCW_FILE;
                }

                foreach (var rcwS in rcwF.Sections)
                {
                    var section = me2cF.Value.Sections.FirstOrDefault(x => x.Header == rcwS.SectionName);
                    if (section == null)
                    {
                        Log.Error($@"RCW mod specifies a section in {rcwF.FileName} that does not exist in the local coalesced: {rcwS.SectionName}");
                        return ModInstallCompletedStatus.INSTALL_FAILED_MALFORMED_RCW_FILE;
                    }
                }


                //Apply mod
                foreach (var rcwS in rcwF.Sections)
                {
                    var section = me2cF.Value.Sections.FirstOrDefault(x => x.Header == rcwS.SectionName);
                    Dictionary<string, int> keyCount = new Dictionary<string, int>();
                    foreach (var key in section.Entries)
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

                    Dictionary<string, bool> keysSupportingMulti = keyCount.ToDictionary(x => x.Key, x => x.Value > 1);

                    //Remove items
                    foreach (var itemToDelete in rcwS.KeysToDelete)
                    {
                        for (int i = section.Entries.Count - 1; i > 0; i--)
                        {
                            var entry = section.Entries[i];
                            if (entry.Key == itemToDelete.Key && entry.Value == itemToDelete.Value)
                            {
                                CLog.Information($@"Removing ini entry {entry.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                                section.Entries.RemoveAt(i);
                            }
                        }
                    }

                    foreach (var itemToAdd in rcwS.KeysToAdd)
                    {
                        var existingEntries = section.Entries.Where(x => x.Key == itemToAdd.Key).ToList();
                        if (existingEntries.Count <= 0)
                        {
                            //just add it
                            CLog.Information($@"Adding ini entry {itemToAdd.RawText} in section {section.Header} of file {me2cF.Key}", Settings.LogModInstallation);
                            section.Entries.Add(itemToAdd);
                        }
                        else if (existingEntries.Count > 1)
                        {
                            //Supports multi. Add this key - but making sure the data doesn't already exist!
                            if (existingEntries.Any(x => x.Value == itemToAdd.Value))
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
                            if (keysSupportingMulti[itemToAdd.Key])
                            {
                                //Supports multi. Add this key - but making sure the data doesn't already exist!
                                if (existingEntries.Any(x => x.Value == itemToAdd.Value))
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

        private bool InstallIntoSFAR((ModJob job, string sfarPath, Dictionary<string, string> fileMapping) sfarJob, Mod mod, Action<string> FileInstalledCallback = null, string ForcedSourcePath = null)
        {

            int numfiles = sfarJob.fileMapping.Count;
            //Todo: Check all newfiles exist
            //foreach (string str in diskFiles)
            //{
            //    if (!File.Exists(str))
            //    {
            //        Console.WriteLine("Source file on disk doesn't exist: " + str);
            //        EndProgram(1);
            //    }
            //}

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
                var sourcePath = Path.Combine(ForcedSourcePath ?? mod.ModPath, sfarJob.job.JobDirectory, entry.Value);
                if (index >= 0)
                {
                    dlc.ReplaceEntry(sourcePath, index);
                    CLog.Information(@"Replaced file within SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                else
                {
                    dlc.AddFileQuick(sourcePath, entryPath);
                    CLog.Information(@"Added new file to SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                FileInstalledCallback?.Invoke(entryPath);
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
            //if (ModBeingInstalled.Game != Mod.MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
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

                if (!MEDirectories.IsOfficialDLCInstalled(job.Header, gameTarget))
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
            if (e.Error != null)
            {
                Log.Error(@"An error occured during mod installation.\n" + App.FlattenException(e.Error));
            }

            var telemetryResult = ModInstallCompletedStatus.NO_RESULT_CODE;
            if (e.Result is ModInstallCompletedStatus mcis)
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
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogInvalidRCWFile), M3L.GetString(M3L.string_installationAborted), MessageBoxButton.OK, MessageBoxImage.Warning);

                }
                else if (mcis == ModInstallCompletedStatus.INSTALL_FAILED_USER_CANCELED_MISSING_MODULES)
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
                        break;
                }
            }
            else
            {
                throw new Exception(@"Mod installer did not have return code. This should be caught and handled, but it wasn't.");
            }
            Analytics.TrackEvent(@"Installed a mod", new Dictionary<string, string>()
            {
                { @"Mod name", $"{ModBeingInstalled.ModName} {ModBeingInstalled.ModVersionString}" },
                { @"Installed from", ModBeingInstalled.IsInArchive ? @"Archive" : @"Library" },
                { @"Type", ModBeingInstalled.GetJob(ModJob.JobHeader.ME2_RCWMOD) != null ? @"RCW .me2mod" : @"Standard" },
                { @"Game", ModBeingInstalled.Game.ToString() },
                { @"Result", telemetryResult.ToString() }
            });
            OnClosing(DataEventArgs.Empty);
        }

        private void InstallStart_Click(object sender, RoutedEventArgs e)
        {
            BeginInstallingMod();
        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            InstallationSucceeded = false;
            InstallationCancelled = true;
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void AlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DockPanel dp)
            {
                if (dp.DataContext is AlternateDLC ad && ad.IsManual)
                {
                    ad.IsSelected = !ad.IsSelected;
                }
                else if (dp.DataContext is AlternateFile af && af.IsManual)
                {
                    af.IsSelected = !af.IsSelected;
                }
                else if (dp.DataContext is ReadOnlyOption ro)
                {
                    ro.IsSelected = !ro.IsSelected;
                }
            }
        }

        public override void OnPanelVisible()
        {
            //Write check
            //Write check
            var canWrite = Utilities.IsDirectoryWritable(gameTarget.TargetPath);
            if (!canWrite)
            {
                //needs write permissions
                InstallationCancelled = true;
                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogNoWritePermissions), M3L.GetString(M3L.string_cannotWriteToGameDirectory), MessageBoxButton.OK, MessageBoxImage.Warning);
                OnClosing(DataEventArgs.Empty);
                return;
            }

            //Detect incompatible DLC
            var dlcMods = VanillaDatabaseService.GetInstalledDLCMods(gameTarget);
            if (ModBeingInstalled.IncompatibleDLC.Count > 0)
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
                    InstallationCancelled = true;
                    M3L.ShowDialog(window, message, M3L.GetString(M3L.string_incompatibleDLCDetected), MessageBoxButton.OK, MessageBoxImage.Error);
                    OnClosing(DataEventArgs.Empty);
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

            foreach (object o in AlternateOptions)
            {
                if (o is AlternateDLC altdlc)
                {
                    altdlc.SetupInitialSelection(gameTarget);
                    if (altdlc.IsManual) AllOptionsAreAutomatic = false;
                }
                else if (o is AlternateFile altfile)
                {
                    altfile.SetupInitialSelection(gameTarget);
                    if (altfile.IsManual) AllOptionsAreAutomatic = false;
                }
            }

            if (AlternateOptions.Count == 0)
            {
                //Just start installing mod
                BeginInstallingMod();
            }
        }
    }
}
