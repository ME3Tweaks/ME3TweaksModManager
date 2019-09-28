using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MassEffectModManager.GameDirectories;
using MassEffectModManager.gamefileformats.sfar;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.ui;
using Serilog;
using static MassEffectModManager.modmanager.Mod;

namespace MassEffectModManager.modmanager.usercontrols
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
        public ModInstaller(Mod mod, GameTarget gameTarget)
        {
            DataContext = this;
            lastPercentUpdateTime = DateTime.Now;
            this.Mod = mod;
            this.gameTarget = gameTarget;
            Action = $"Preparing to install";
            InitializeComponent();
        }


        public Mod Mod { get; }
        private GameTarget gameTarget;
        private DateTime lastPercentUpdateTime;
        public bool InstallationCancelled;
        private const int INSTALL_SUCCESSFUL = 1;
        private const int INSTALL_FAILED_USER_CANCELED_MISSING_MODULES = 2;

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
            foreach (var job in Mod.InstallationJobs)
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
            Log.Information($"BeginInstallingMod(): {Mod.ModName}");
            NamedBackgroundWorker bw = new NamedBackgroundWorker($"ModInstaller-{Mod.ModName}");
            bw.WorkerReportsProgress = true;
            bw.DoWork += InstallModBackgroundThread;
            bw.RunWorkerCompleted += ModInstallationCompleted;
            //bw.ProgressChanged += ModProgressChanged;
            bw.RunWorkerAsync();
        }


        private void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            Log.Information($"Mod Installer Background thread starting");
            var installationJobs = Mod.InstallationJobs;
            var gamePath = gameTarget.TargetPath;
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);

            if (!PrecheckOfficialHeaders(gameDLCPath, installationJobs))
            {
                e.Result = INSTALL_FAILED_USER_CANCELED_MISSING_MODULES;
                return;
            }
            Utilities.InstallBinkBypass(gameTarget); //Always install binkw32, don't bother checking if it is already ASI version.

            //Calculate number of installation tasks beforehand
            int numFilesToInstall = installationJobs.Where(x => x.Header != ModJob.JobHeader.CUSTOMDLC).Select(x => x.FilesToInstall.Count).Sum();
            var customDLCMapping = installationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC)?.CustomDLCFolderMapping;
            if (customDLCMapping != null)
            {
                foreach (var mapping in customDLCMapping)
                {
                    numFilesToInstall += Directory.GetFiles(Path.Combine(Mod.ModPath, mapping.Key), "*", SearchOption.AllDirectories).Length;
                }
            }

            Action = $"Installing";
            PercentVisibility = Visibility.Visible;
            Percent = 0;

            int numdone = 0;
            void FileInstalledCallback()
            {
                numdone++;
                var now = DateTime.Now;
                if ((now - lastPercentUpdateTime).Milliseconds > PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    Percent = (int)(numdone * 100.0 / numFilesToInstall);
                    lastPercentUpdateTime = now;
                }
            }
            foreach (var job in installationJobs)
            {
                Log.Information($"Processing installation job: {job.Header}");

                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    #region Installation: CustomDLC
                    //Already have variable from before
                    void callback()
                    {
                        numdone++;
                        var now = DateTime.Now;
                        //Debug.WriteLine("Time delta: " + (now - lastPercentUpdateTime).Milliseconds);
                        if ((now - lastPercentUpdateTime).Milliseconds > PERCENT_REFRESH_COOLDOWN)
                        {
                            //Don't update UI too often. Once per second is enough.
                            Percent = (int)(numdone * 100.0 / numFilesToInstall);
                            lastPercentUpdateTime = now;
                        }
                    }

                    foreach (var mapping in customDLCMapping)
                    {
                        var source = Path.Combine(Mod.ModPath, mapping.Key);
                        var target = Path.Combine(gameDLCPath, mapping.Value);
                        Log.Information($"Copying CustomDLC to target: {source} -> {target}");
                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(source), new DirectoryInfo(target), callback);
                        Log.Information($"Installed CustomDLC {mapping.Value}");
                    }
                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.BASEGAME)
                {
                    #region Installation: BASEGAME
                    foreach (var entry in job.FilesToInstall)
                    {
                        var destPath = Path.GetFullPath(Path.Combine(gamePath, entry.Key.TrimStart('/', '\\')));
                        Log.Information($"Installing BASEGAME file: {entry.Value} -> {destPath}");
                        File.Copy(entry.Value, destPath, true);
                        FileInstalledCallback();
                    }
                    #endregion
                }
                else if (Mod.Game == MEGame.ME3 && ModJob.SupportedNonCustomDLCJobHeaders.Contains(job.Header)) //previous else if will catch BASEGAME
                {
                    #region Installation: DLC (Unpacked and SFAR) - ME3 ONLY
                    string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? Utilities.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.HeadersToDLCNamesMap[job.Header], "CookedPCConsole", "Default.sfar");


                    if (File.Exists(sfarPath))
                    {
                        if (new FileInfo(sfarPath).Length == 32)
                        {
                            //Unpacked
                            foreach (var entry in job.FilesToInstall)
                            {
                                var destPath = Path.GetFullPath(Path.Combine(gamePath, entry.Key.TrimStart('/', '\\')));
                                Log.Information($"Installing (unpacked) file: {entry.Value} -> {destPath}");
                                File.Copy(entry.Value, destPath, true);
                                FileInstalledCallback();
                            }
                        }
                        else
                        {
                            //Packed

                            InstallIntoSFAR(job, sfarPath, FileInstalledCallback);
                        }
                    }
                    else
                    {
                        Log.Warning($"SFAR doesn't exist {sfarPath}, skipping job: {job.Header}");
                        numdone += job.FilesToInstall.Count;
                    }
                    #endregion
                }
            }
            Percent = (int)(numdone * 100.0 / numFilesToInstall);

            //Install supporting ASI files if necessary
            if (Mod.Game != MEGame.ME2)
            {
                Action = "Installing support files";
                string asiFname = Mod.Game == MEGame.ME1 ? "ME1-DLC-ModEnabler-v1" : "ME3Logger_truncating-v1";
                string asiTargetDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetExecutableDirectory(gameTarget), "asi")).FullName;

                var existingmatchingasis = Directory.GetFiles(asiTargetDirectory, asiFname.Substring(0, asiFname.LastIndexOf('-')) + "*").ToList();
                bool higherVersionInstalled = false;
                if (existingmatchingasis.Count > 0)
                {
                    foreach (var v in existingmatchingasis)
                    {
                        string shortName = Path.GetFileNameWithoutExtension(v);
                        var asiName = shortName.Substring(shortName.LastIndexOf('-') + 2); //Todo: Try catch this as it might explode if for some reason filename is like ASIMod-.asi
                        if (int.TryParse(asiName, out int version))
                        {
                            higherVersionInstalled = version > 1;
                            Log.Information("A newer version of a supporting ASI is installed: " + shortName + ". Not installing ASI.");
                            break;
                        }
                    }
                }

                //Todo: Use ASI manifest to identify malformed names

                if (!higherVersionInstalled)
                {
                    string asiPath = "MassEffectModManagerCore.modmanager.asi." + asiFname + ".asi";
                    Utilities.ExtractInternalFile(asiPath, Path.Combine(asiTargetDirectory, asiFname + ".asi"), true);
                }
            }

            if (numFilesToInstall == numdone)
            {
                e.Result = INSTALL_SUCCESSFUL;
                Action = "Installed";
            }
            Thread.Sleep(1000);
        }

        private bool InstallIntoSFAR(ModJob job, string sfarPath, Action FileInstalledCallback = null)
        {

            int numfiles = job.FilesToInstall.Count;


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
            DLCPackage dlc = new DLCPackage(sfarPath);

            //precheck
            bool allfilesfound = true;
            //if (options.ReplaceFiles)
            //{
            //    for (int i = 0; i < numfiles; i++)
            //    {
            //        int idx = dlc.FindFileEntry(sfarFiles[i]);
            //        if (idx == -1)
            //        {
            //            Console.WriteLine("Specified file does not exist in the SFAR Archive: " + sfarFiles[i]);
            //            allfilesfound = false;
            //        }
            //    }
            //}

            //Add or Replace
            if (allfilesfound)
            {
                foreach (var entry in job.FilesToInstall)
                {
                    int index = dlc.FindFileEntry(entry.Key);
                    if (index >= 0)
                    {
                        dlc.ReplaceEntry(entry.Value, index);
                        Log.Information("Replaced file within SFAR: " + entry.Key);
                    }
                    else
                    {
                        dlc.AddFileQuick(entry.Value, entry.Key);
                        Log.Information("Added SFAR file: " + entry.Key);
                    }
                    FileInstalledCallback?.Invoke();
                }
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
            if (Mod.Game != MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
            foreach (var job in installationJobs)
            {
                if (job.Header == ModJob.JobHeader.BALANCE_CHANGES) continue; //Don't check balance changes
                if (job.Header == ModJob.JobHeader.BASEGAME) continue; //Don't check basegame
                if (job.Header == ModJob.JobHeader.CUSTOMDLC) continue; //Don't check custom dlc
                string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? Utilities.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.HeadersToDLCNamesMap[job.Header], "CookedPCConsole", "Default.sfar");
                if (!File.Exists(sfarPath))
                {
                    Log.Warning($"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                    //Prompt user
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        string message = $"{Mod.ModName} installs files into the {ModJob.HeadersToDLCNamesMap[job.Header]} ({ME3Directory.OfficialDLCNames[ModJob.HeadersToDLCNamesMap[job.Header]]}) DLC, which is not installed.";
                        if (job.RequirementText != null)
                        {
                            message += $"\n{job.RequirementText}";
                        }
                        message += "\n\nThe mod might not function properly without this DLC installed first. Continue installing anyways?";
                        MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(message, $"{ME3Directory.OfficialDLCNames[ModJob.HeadersToDLCNamesMap[job.Header]]} DLC not installed", MessageBoxButton.YesNo, MessageBoxImage.Error);
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

            InstallationSucceeded = e.Result is int res && res == INSTALL_SUCCESSFUL;
            OnClosing(new EventArgs());
        }

        private void InstallStart_Click(object sender, RoutedEventArgs e)
        {

        }

        private void InstallCancel_Click(object sender, RoutedEventArgs e)
        {
            InstallationSucceeded = false;
            InstallationCancelled = true;
            OnClosing(new EventArgs());
        }
    }
}
