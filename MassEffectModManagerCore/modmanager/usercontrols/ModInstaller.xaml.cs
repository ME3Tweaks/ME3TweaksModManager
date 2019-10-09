using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MassEffectModManager;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.sfar;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.VisualBasic;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstaller.xaml
    /// </summary>
    public partial class ModInstaller : UserControl, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<object> AlternateOptions { get; } = new ObservableCollectionExtended<object>();

        public bool InstallationSucceeded { get; private set; }
        private const int PERCENT_REFRESH_COOLDOWN = 125;
        public bool ModIsInstalling { get; set; }
        public ModInstaller(Mod modBeingInstalled, GameTarget gameTarget)
        {
            DataContext = this;
            lastPercentUpdateTime = DateTime.Now;
            this.ModBeingInstalled = modBeingInstalled;
            this.gameTarget = gameTarget;
            Action = $"Preparing to install";
            InitializeComponent();
        }


        public Mod ModBeingInstalled { get; }
        private GameTarget gameTarget;
        private DateTime lastPercentUpdateTime;
        public bool InstallationCancelled;
        private const int INSTALL_SUCCESSFUL = 1;
        private const int INSTALL_FAILED_USER_CANCELED_MISSING_MODULES = 2;
        private const int INSTALL_FAILED_ALOT_BLOCKING = 3;

        public string Action { get; set; }
        public int Percent { get; set; }
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;


        public event EventHandler Close;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler handler = Close;
            handler?.Invoke(this, e);
        }

        public void PrepareToInstallMod()
        {
            //Detect incompatible DLC


            //Detect outdated DLC


            //See if any alternate options are available and display them even if they are all autos
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
                }
                else if (o is AlternateFile altfile)
                {
                    altfile.SetupInitialSelection(gameTarget);
                }
            }

            if (AlternateOptions.Count == 0)
            {
                //Just start installing mod
                BeginInstallingMod();
            }
        }

        private void BeginInstallingMod()
        {
            ModIsInstalling = true;
            Log.Information($"BeginInstallingMod(): {ModBeingInstalled.ModName}");
            NamedBackgroundWorker bw = new NamedBackgroundWorker($"ModInstaller-{ModBeingInstalled.ModName}");
            bw.WorkerReportsProgress = true;
            bw.DoWork += InstallModBackgroundThread;
            bw.RunWorkerCompleted += ModInstallationCompleted;
            //bw.ProgressChanged += ModProgressChanged;
            bw.RunWorkerAsync();
        }


        private void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            Log.Information($"Mod Installer Background thread starting");
            var installationJobs = ModBeingInstalled.InstallationJobs;
            var gamePath = gameTarget.TargetPath;
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);

            if (!PrecheckOfficialHeaders(gameDLCPath, installationJobs))
            {
                e.Result = INSTALL_FAILED_USER_CANCELED_MISSING_MODULES;
                return;
            }
            //todo: If statment on this
            Utilities.InstallBinkBypass(gameTarget); //Always install binkw32, don't bother checking if it is already ASI version.

            //Prepare queues
            (Dictionary<ModJob, (Dictionary<string, string> fileMapping, List<string> dlcFoldersBeingInstalled)> unpackedJobMappings, List<(ModJob job, string sfarPath, Dictionary<string, string> sfarInstallationMapping)> sfarJobs) installationQueues = ModBeingInstalled.GetInstallationQueues(gameTarget);

            if (gameTarget.ALOTInstalled)
            {
                //Check if any packages are being installed. If there are, we will block this installation.
                bool installsPackageFile = false;
                foreach (var jobMappings in installationQueues.unpackedJobMappings)
                {
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.Value.fileMapping.Keys.Any(x => x.EndsWith(".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                foreach (var jobMappings in installationQueues.sfarJobs)
                {
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(".pcc", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(".u", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(".upk", StringComparison.InvariantCultureIgnoreCase));
                    installsPackageFile |= jobMappings.sfarInstallationMapping.Keys.Any(x => x.EndsWith(".sfm", StringComparison.InvariantCultureIgnoreCase));
                }

                if (installsPackageFile)
                {
                    if (Settings.DeveloperMode)
                    {
                        Log.Warning("ALOT is installed and user is attemping to install a mod (in developer mode). Prompting user to cancel installation");

                        bool cancel = false;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            var res = Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), $"ALOT is installed and this mod installs package files. Continuing to install this mod will likely cause broken textures to occur or game crashes due to invalid texture pointers and possibly empty mips. It will also put your ALOT installation into an unsupported configuration.\n\nContinue to install {ModBeingInstalled.ModName}? You have been warned.", $"Broken textures warning", MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                            cancel = res == MessageBoxResult.No;
                        });
                        if (cancel)
                        {
                            return;
                        }
                        Log.Warning("User installing mod anyways even with ALOT installed");
                    }
                    else
                    {
                        Log.Error("ALOT is installed. Installing mods that install package files after installing ALOT is not permitted.");
                        //ALOT Installed, this is attempting to install a package file
                        e.Result = INSTALL_FAILED_ALOT_BLOCKING;
                        return;
                    }
                }
            }
            Action = $"Installing";
            PercentVisibility = Visibility.Visible;
            Percent = 0;

            int numdone = 0;

            //Calculate number of installation tasks beforehand - we won't know SFAR cou
            int numFilesToInstall = installationQueues.unpackedJobMappings.Select(x => x.Value.fileMapping.Count).Sum();
            numFilesToInstall += installationQueues.sfarJobs.Select(x => x.sfarInstallationMapping.Count).Sum() * 2; //*2 as we have to extract and install
            Debug.WriteLine("Number of expected installation tasks: " + numFilesToInstall);
            void FileInstalledCallback()
            {
                numdone++;
                var now = DateTime.Now;
                if (numdone > numFilesToInstall) Debug.WriteLine($"Percentage calculated is wrong. Done: {numdone} NumToDoTotal: {numFilesToInstall}");
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

            foreach (var unpackedQueue in installationQueues.unpackedJobMappings)
            {
                //Todo: Implement unpacked copy queue
                //CopyDir.CopyFiles_ProgressBar(unpackedQueue.)

                foreach (var originalMapping in unpackedQueue.Value.fileMapping)
                {
                    //always unpacked
                    //if (unpackedQueue.Key == ModJob.JobHeader.CUSTOMDLC || unpackedQueue.Key == ModJob.JobHeader.BALANCE_CHANGES || unpackedQueue.Key == ModJob.JobHeader.BASEGAME)
                    //{
                    string sourceFile;
                    //todo: maybe handle null parameters as nothing
                    if (unpackedQueue.Key.JobDirectory == null)
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, originalMapping.Value);
                    }
                    else
                    {
                        sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, unpackedQueue.Key.JobDirectory, originalMapping.Value);
                    }

                    var destFile = Path.Combine(unpackedQueue.Key.Header == ModJob.JobHeader.CUSTOMDLC ? MEDirectories.DLCPath(gameTarget) : gameTarget.TargetPath, originalMapping.Key); //official
                    if (ModBeingInstalled.IsInArchive)
                    {
                        int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile);
                        fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                        fullPathMappingDisk[sourceFile] = destFile; //used for redirection
                    }
                    else
                    {
                        fullPathMappingDisk[sourceFile] = destFile;
                    }
                    //}
                }
            }

            //Substage: Add SFAR staging targets
            string sfarStagingDirectory = (ModBeingInstalled.IsInArchive && installationQueues.sfarJobs.Count > 0) ? Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), "SFARJobStaging")).FullName : null; //don't make directory if we don't need one
            if (sfarStagingDirectory != null)
            {
                foreach (var sfarJob in installationQueues.sfarJobs)
                {
                    foreach (var fileToInstall in sfarJob.sfarInstallationMapping)
                    {
                        string sourceFile = FilesystemInterposer.PathCombine(ModBeingInstalled.IsInArchive, ModBeingInstalled.ModPath, sfarJob.job.JobDirectory, fileToInstall.Value);
                        int archiveIndex = ModBeingInstalled.Archive.ArchiveFileNames.IndexOf(sourceFile);
                        if (archiveIndex == -1)
                        {
                            Debugger.Break();
                        }
                        string destFile = Path.Combine(sfarStagingDirectory, sfarJob.job.JobDirectory, fileToInstall.Value);
                        fullPathMappingArchive[archiveIndex] = destFile; //used for extraction indexing
                        fullPathMappingDisk[sourceFile] = destFile; //used for redirection
                        Debug.WriteLine($"SFAR Disk Staging: {fileToInstall.Key} => {destFile}");
                    }
                }
            }


            //Stage: Unpacked files installation
            if (!ModBeingInstalled.IsInArchive)
            {
                //Direct copy
                CopyDir.CopyFiles_ProgressBar(fullPathMappingDisk, FileInstalledCallback);
            }
            else
            {
                //Extraction to destination
                string installationRedirectCallback(string inArchivePath)
                {
                    var redirectedPath = fullPathMappingDisk[inArchivePath];
                    //Debug.WriteLine($"Redirecting {inArchivePath} to {redirectedPath}");
                    return redirectedPath;
                }

                ModBeingInstalled.Archive.FileExtractionStarted += (sender, args) =>
                {
                    CLog.Information("Extracting mod file for installation: " + args.FileInfo.FileName, Settings.LogModInstallation);
                };
                ModBeingInstalled.Archive.FileExtractionFinished += (sender, args) =>
                {
                    FileInstalledCallback();
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
                var metacmm = Path.Combine(addedDLCFolder, "_metacmm.txt");
                string contents = $"{ModBeingInstalled.ModName}\n{ModBeingInstalled.ModVersionString}\n{App.BuildNumber}\n{Guid.NewGuid().ToString()}"; //Todo: Assign guid. guid might not even be necessary here.
                File.WriteAllText(metacmm, contents);
            }

            //Stage: SFAR Installation
            foreach (var sfarJob in installationQueues.sfarJobs)
            {
                InstallIntoSFAR(sfarJob, ModBeingInstalled, FileInstalledCallback, ModBeingInstalled.IsInArchive ? sfarStagingDirectory : null);
            }


            //Main installation step has completed
            CLog.Information("Main stage of mod installation has completed", Settings.LogModInstallation);
            Percent = (int)(numdone * 100.0 / numFilesToInstall);

            //Install supporting ASI files if necessary
            //Todo: Upgrade to version detection code from ME3EXP to prevent conflicts

            Action = "Installing support files";
            CLog.Information("Installing supporting ASI files", Settings.LogModInstallation);
            if (ModBeingInstalled.Game == Mod.MEGame.ME1)
            {
                Utilities.InstallEmbeddedASI("ME1-DLC-ModEnabler-v1.0", 1.0, gameTarget);
            }
            else if (ModBeingInstalled.Game == Mod.MEGame.ME2)
            {
                //None right now
            }
            else
            {
                Utilities.InstallEmbeddedASI("ME3Logger_truncating-v1.0", 1.0, gameTarget);
                if (ModBeingInstalled.GetJob(ModJob.JobHeader.BALANCE_CHANGES) != null)
                {
                    Utilities.InstallEmbeddedASI("BalanceChangesReplacer-v2.0", 2.0, gameTarget);
                }
            }

            if (sfarStagingDirectory != null)
            {
                Utilities.DeleteFilesAndFoldersRecursively(Utilities.GetTempPath());
            }

            if (numFilesToInstall == numdone)
            {
                e.Result = INSTALL_SUCCESSFUL;
                Action = "Installed";
            }
        }

        private bool InstallIntoSFAR((ModJob job, string sfarPath, Dictionary<string, string> fileMapping) sfarJob, Mod mod, Action FileInstalledCallback = null, string ForcedSourcePath = null)
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
                    CLog.Information("Replaced file within SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                else
                {
                    dlc.AddFileQuick(sourcePath, entryPath);
                    CLog.Information("Added new file to SFAR: " + entry.Key, Settings.LogModInstallation);
                }
                FileInstalledCallback?.Invoke();
            }

            //Todo: Support deleting files from sfar (I am unsure if this is actually ever used and I might remove this feature)
            //if (options.DeleteFiles)
            //{
            //    DLCPackage dlc = new DLCPackage(options.SFARPath);
            //    List<int> indexesToDelete = new List<int>();
            //    foreach (string file in options.Files)
            //    {
            //        int idx = dlc.FindFileEntry(file);
            //        if (idx == -1)
            //        {
            //            if (options.IgnoreDeletionErrors)
            //            {
            //                continue;
            //            }
            //            else
            //            {
            //                Console.WriteLine("File doesn't exist in archive: " + file);
            //                EndProgram(1);
            //            }
            //        }
            //        else
            //        {
            //            indexesToDelete.Add(idx);
            //        }
            //    }
            //    if (indexesToDelete.Count > 0)
            //    {
            //        dlc.DeleteEntries(indexesToDelete);
            //    }
            //    else
            //    {
            //        Console.WriteLine("No files were found in the archive that matched the input list for --files.");
            //    }
            //}

            return true;
        }

        /// <summary>
        /// Checks if DLC specified by the job installation headers exist and prompt user to continue or not if the DLC is not found. This is only used for ME3's headers such as CITADEL or RESURGENCE and not CUSTOMDLC or BASEGAME.'
        /// </summary>
        /// <param name="gameDLCPath">Game DLC path</param>
        /// <param name="installationJobs">List of jobs to look through and validate</param>
        /// <returns></returns>
        private bool PrecheckOfficialHeaders(string gameDLCPath, List<ModJob> installationJobs)
        {
            //if (ModBeingInstalled.Game != Mod.MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
            foreach (var job in installationJobs)
            {
                if (job.Header == ModJob.JobHeader.BALANCE_CHANGES) continue; //Don't check balance changes
                if (job.Header == ModJob.JobHeader.BASEGAME) continue; //Don't check basegame
                if (job.Header == ModJob.JobHeader.CUSTOMDLC) continue; //Don't check custom dlc
                string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? Utilities.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header], "CookedPCConsole", "Default.sfar");
                if (!File.Exists(sfarPath))
                {
                    Log.Warning($"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                    //Prompt user
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        string message = $"{ModBeingInstalled.ModName} installs files into the {ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header]} ({ME3Directory.OfficialDLCNames[ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header]]}) DLC, which is not installed.";
                        if (job.RequirementText != null)
                        {
                            message += $"\n{job.RequirementText}";
                        }
                        message += "\n\nThe mod might not function properly without this DLC installed first. Continue installing anyways?";
                        MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(message, $"{ME3Directory.OfficialDLCNames[ModJob.GetHeadersToDLCNamesMap(ModBeingInstalled.Game)[job.Header]]} DLC not installed", MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.No) { cancel = true; return; }

                    }));
                    if (cancel) { Log.Error("User canceling installation"); return false; }
                    Log.Warning($"User continuing installation anyways");
                }
            }
            return true;
        }

        private void ModInstallationCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is int res)
            {
                InstallationSucceeded = res == INSTALL_SUCCESSFUL;
                if (res == INSTALL_FAILED_ALOT_BLOCKING)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show($"Installation of mods that install package files to targets that have ALOT installed is not allowed. Package files must be installed before ALOT.", $"Installation blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                throw new Exception("Mod installer did not have return code.");
            }
            OnClosing(new EventArgs());
        }

        private void InstallStart_Click(object sender, RoutedEventArgs e)
        {
            BeginInstallingMod();
        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            InstallationSucceeded = false;
            InstallationCancelled = true;
            OnClosing(new EventArgs());
        }
    }
}
