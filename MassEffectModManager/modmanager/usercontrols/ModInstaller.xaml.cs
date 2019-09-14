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
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.objects;
using Serilog;
using static MassEffectModManager.modmanager.Mod;

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModInstaller.xaml
    /// </summary>
    public partial class ModInstaller : UserControl, INotifyPropertyChanged
    {
        public ModInstaller(Mod mod, GameTarget gameTarget)
        {
            DataContext = this;
            lastPercentUpdateTime = DateTime.Now;
            this.mod = mod;
            this.gameTarget = gameTarget;
            Action = $"Preparing to install";
            InitializeComponent();
        }

        public Mod mod { get; }
        private GameTarget gameTarget;
        private DateTime lastPercentUpdateTime;
        private readonly int INSTALL_FAILED_USER_CANCELED_MISSING_MODULES = 1;

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

        public void BeginInstallingMod()
        {
            Log.Information($"BeginInstallingMod(): {mod.ModName}");
            NamedBackgroundWorker bw = new NamedBackgroundWorker($"ModInstaller-{mod.ModName}");
            bw.WorkerReportsProgress = true;
            bw.DoWork += InstallModBackgroundThread;
            bw.RunWorkerCompleted += ModInstallationCompleted;
            bw.ProgressChanged += ModProgressChanged;
            bw.RunWorkerAsync();
        }

        private void ModProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void InstallModBackgroundThread(object sender, DoWorkEventArgs e)
        {
            Log.Information($"Mod Installer Background thread starting");
            var installationJobs = mod.InstallationJobs;
            var gamePath = gameTarget.TargetPath;
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);

            if (!PrecheckOfficialHeaders(gamePath, gameDLCPath, installationJobs))
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
                    numFilesToInstall += Directory.GetFiles(Path.Combine(mod.ModPath, mapping.Key), "*", SearchOption.AllDirectories).Length;
                }
            }

            Action = $"Installing";
            PercentVisibility = Visibility.Visible;
            Percent = 0;

            int numdone = 0;
            foreach (var job in installationJobs)
            {
                Log.Information($"Processing installation job: {job.Header}");
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    //Already have variable from before
                    void callback()
                    {
                        numdone++;
                        var now = DateTime.Now;
                        Debug.WriteLine("Time delta: " + (now - lastPercentUpdateTime).Milliseconds);
                        if ((now - lastPercentUpdateTime).Milliseconds > 250)
                        {
                            //Don't update UI too often. Once per second is enough.
                            Percent = (int)(numdone * 100.0 / numFilesToInstall);
                            lastPercentUpdateTime = now;
                        }
                    }


                    foreach (var mapping in customDLCMapping)
                    {
                        var source = Path.Combine(mod.ModPath, mapping.Key);
                        var target = Path.Combine(gameDLCPath, mapping.Value);
                        Log.Information($"Copying CustomDLC to target: {source} -> {target}");
                        CopyDir.CopyAll_ProgressBar(new DirectoryInfo(source), new DirectoryInfo(target), callback);
                        Log.Information($"Installed CustomDLC {mapping.Value}");
                    }
                }
            }
            Thread.Sleep(3000);
        }

        private bool PrecheckOfficialHeaders(string gamePath, string gameDLCPath, List<ModJob> installationJobs)
        {
            if (mod.Game != MEGame.ME3) { return true; } //me1/me2 don't have dlc header checks like me3
            foreach (var job in installationJobs)
            {
                if (job.Header == ModJob.JobHeader.BALANCE_CHANGES) continue; //Don't check balance changes
                if (job.Header == ModJob.JobHeader.BASEGAME) continue; //Don't check basegame
                if (job.Header == ModJob.JobHeader.CUSTOMDLC) continue; //Don't check custom dlc
                if (job.Header == ModJob.JobHeader.TESTPATCH)
                {
                    //testpatch dlc
                    string sfarPath = Utilities.GetTestPatchPath(gameTarget);
                    if (!File.Exists(sfarPath))
                    {
                        Log.Warning($"DLC not installed that mod is marked to modify: {job.Header}, prompting user.");
                        //Prompt user
                        bool cancel = false;
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            string message = $"{mod.ModName} installs files into the {ModJob.HeadersToDLCNamesMap[job.Header]} DLC, which is not installed.";
                            if (job.RequirementText != null)
                            {
                                message += $"\n{job.RequirementText}";
                            }
                            message += "\n\nContinue installing anyways?";
                            MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(message, "DLC not installed", MessageBoxButton.YesNo, MessageBoxImage.Error);
                            if (result == MessageBoxResult.No) { cancel = true; return; }

                        }));
                        if (cancel) return false;
                        Log.Warning($"User continuing installation anyways");

                    }
                }
                else
                {
                    //standard dlc
                }

            }
            return true;
        }

        private void ModInstallationCompleted(object sender, RunWorkerCompletedEventArgs e)
            {
                OnClosing(EventArgs.Empty);
            }
        }
    }
