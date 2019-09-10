

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ByteSizeLib;
using Microsoft.Win32;
using Octokit;
using Serilog;

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ExternalToolLauncher : UserControl, INotifyPropertyChanged
    {
        public const string ME3Explorer = "ME3Explorer";
        public const string ALOTInstaller = "ALOT Installer";
        public const string MEIM = "Mass Effect INI Modder";
        public const string MEM = "Mass Effect Modder";
        public const string MER = "Mass Effect Randomizer";
        private string tool;
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;
        private string localFolderName;

        public string Action { get; set; }
        public int PercentDownloaded { get; set; }
        public ExternalToolLauncher(string tool)
        {
            DataContext = this;
            this.tool = tool;
            InitializeComponent();
        }

        public void StartLaunchingTool()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += async (a, b) =>
            {
                var toolName = tool.Replace(" ", "");
                var localToolFolderName = Path.Combine(Utilities.GetDataDirectory(), "ExternalTools", toolName);
                Action = "Checking for updates";
                var latestRelease = await FetchProductFromGithubReleases();
                var localExecutable = Path.Combine(localToolFolderName, toolName + ".exe");

                bool needsDownloading = !File.Exists(localExecutable);

                //Failed to get release check
                if (latestRelease == null)
                {
                    if (!needsDownloading)
                    {
                        LaunchTool(localExecutable);
                        return;
                    }
                    else
                    {
                        //Must run on UI thread
                        //MessageBox.Show($"Unable to download {tool}.\nPlease check your network connection and try again.\nIf the issue persists, please come to the ME3Tweaks Discord.");
                        Log.Error("Unable to launch tool - could not download, and does not exist locally: " + localExecutable);
                        return;
                    }
                }

                //Got a release
                if (needsDownloading)
                {
                    DownloadTool(localToolFolderName, latestRelease, localExecutable);
                }
                else
                {
                    //Check if it need updated
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(localExecutable);
                    bool needsUpdated = false;
                    if (tool == MEM)
                    {
                        //Checks based on major
                        int releaseVer = int.Parse(latestRelease.Name);
                        if (releaseVer > fvi.ProductMajorPart)
                        {
                            needsUpdated = true;
                        }
                    }
                    else
                    {
                        Version serverVersion = new Version(latestRelease.Name);
                        Version localVersion = new Version(string.Format("{0}.{1}.{2}.{3}", fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart));
                        if (serverVersion > localVersion)
                        {
                            needsUpdated = true;
                        }
                    }

                    if (!needsUpdated)
                    {
                        LaunchTool(localExecutable);
                    }
                    else
                    {
                        DownloadTool(localToolFolderName, latestRelease, localExecutable);
                    }
                }
            };

            bw.RunWorkerAsync();
        }

        private void DownloadTool(string localToolFolderName, Release latestRelease, string executable)
        {
            var toolName = tool.Replace(" ", "");
            Action = "Downloading " + tool;
            PercentVisibility = Visibility.Visible;
            PercentDownloaded = 0;

            WebClient downloadClient = new WebClient();

            downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
            downloadClient.Headers["user-agent"] = "MassEffectModManager";
            string temppath = Path.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                PercentDownloaded = e.ProgressPercentage;
            };
            var asset = latestRelease.Assets[0];
            var extension = Path.GetExtension(asset.BrowserDownloadUrl);
            string downloadPath = temppath + toolName + extension;

            downloadClient.DownloadFileCompleted += (a, b) =>
            {
                //Todo: Account for errors
                switch (extension)
                {
                    case ".exe":
                        if (File.Exists(executable))
                        {
                            File.Delete(executable);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(executable));
                        File.Move(downloadPath, executable);
                        LaunchTool(executable);
                        break;
                }
            };
            downloadClient.DownloadFileAsync(new Uri(asset.BrowserDownloadUrl), downloadPath);
        }

        private void LaunchTool(string localExecutable)
        {
            Action = "Launching " + tool;
            PercentVisibility = Visibility.Collapsed;
            PercentDownloaded = 0;
            Process.Start(localExecutable);
            //Need to somehow deal with args for ALOT Installer
        }

        /*
*  if (latest != null)
           {
               Log.Information("Latest available: " + latest.TagName);
               Version releaseName = new Version(latest.TagName);
               if (versInfo < releaseName && latest.Assets.Count > 0)
               {

                   
                   updateprogresscontroller.Canceled += async (s, e) =>
                   {
                       if (downloadClient != null)
                       {
                           Log.Information("Application update was in progress but was canceled.");
                           downloadClient.CancelAsync();
                           await updateprogresscontroller.CloseAsync();
                           FetchManifest();
                       }
                   };
                   
               }
               else
               {
                   AddonFilesLabel.Text = "Application update declined";
                   Log.Warning("Application update was declined");
                   await this.ShowMessageAsync("Old versions are not supported", "Outdated versions of ALOT Installer are not supported and may stop working as the installer manifest and MEMNoGui are updated.");
                   FetchManifest();
               }
*/
        private async Task<Release> FetchProductFromGithubReleases()
        {
            string toolGithubOwner = null;
            string toolGithubRepoName = null;
            switch (tool)
            {
                case ALOTInstaller:
                    toolGithubOwner = "ME3Tweaks";
                    toolGithubRepoName = "ALOTInstaller";
                    break;
                case MER:
                    toolGithubOwner = "ME3Tweaks";
                    toolGithubRepoName = "MassEffectRandomizer";
                    break;
                case ME3Explorer:
                    toolGithubOwner = "ME3Tweaks";
                    toolGithubRepoName = "ME3Explorer";
                    break;

            }


            Log.Information($"Checking for application updates from github ({toolGithubOwner}, {toolGithubRepoName})");
            var client = new GitHubClient(new ProductHeaderValue("MassEffectModManager"));
            try
            {
                var releases = await client.Repository.Release.GetAll(toolGithubOwner, toolGithubRepoName);
                if (releases.Count > 0)
                {
                    Log.Information("Parsing release information from github");

                    //The release we want to check is always the latest with assets that is not a pre-release
                    return releases.FirstOrDefault(x => !x.Prerelease && x.Assets.Count > 0);
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for tool update: " + e);
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
