

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Octokit;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.usercontrols
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
        public const string ME3EXP_ASIMANAGER = "JUMPLIST_ASIMANAGER";
        public const string ME3EXP_DLCUNPACKER = "JUMPLIST_DLCUNPACKER";
        public const string ME3EXP_MOUNTEDITOR = "JUMPLIST_MOUNTEDITOR";
        public const string ME3EXP_PACKAGEDUMPER = "JUMPLIST_PACKAGEDUMPER";
        private string tool;

        private static List<string> ToolsCheckedForUpdatesInThisSession = new List<string>();
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;
        private string localFolderName;
        private string arguments;
        public string Action { get; set; }
        public int PercentDownloaded { get; set; }

        public string ToolImageSource
        {
            get
            {
                switch (tool)
                {
                    case ALOTInstaller:
                        return "/modmanager/toolicons/alot_big.png";
                    case MER:
                        return "/modmanager/toolicons/masseffectrandomizer_big.png";
                    case ME3Explorer:
                        return "/modmanager/toolicons/me3explorer_big.png";
                    case MEM:
                        return "/modmanager/toolicons/masseffectmodder_big.png";
                    case MEIM:
                        return "/modmanager/toolicons/masseffectinimodder_big.png";
                    default:
                        return null;
                }
            }
        }

        public ExternalToolLauncher(string tool, string arguments = null)
        {
            DataContext = this;
            this.tool = tool;
            this.arguments = arguments;
            InitializeComponent();
        }

        public void StartLaunchingTool()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += async (a, b) =>
            {
                var toolName = tool.Replace(" ", "");
                var localToolFolderName = Path.Combine(Utilities.GetDataDirectory(), "ExternalTools", toolName);
                var localExecutable = Path.Combine(localToolFolderName, toolName + ".exe");
                bool needsDownloading = !File.Exists(localExecutable);

                if (!needsDownloading && ToolsCheckedForUpdatesInThisSession.Contains(tool))
                {
                    //Don't check for updates again.
                    LaunchTool(localExecutable);
                    return;
                }
                Action = "Checking for updates";
                var latestRelease = await FetchLatestRelease();


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
                        Action = "Failed to download " + tool;
                        PercentVisibility = Visibility.Collapsed;
                        PercentDownloaded = 0;
                        Thread.Sleep(5000);
                        OnClosing(EventArgs.Empty);
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
                        int releaseVer = int.Parse(latestRelease.TagName);
                        if (releaseVer > fvi.ProductMajorPart)
                        {
                            needsUpdated = true;
                        }
                    }
                    else
                    {
                        Version serverVersion = new Version(latestRelease.TagName);
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
                    ToolsCheckedForUpdatesInThisSession.Add(tool);
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
                var outputDiretory = Directory.CreateDirectory(Path.GetDirectoryName(executable)).FullName;
                switch (extension)
                {
                    case ".exe":
                        if (File.Exists(executable))
                        {
                            File.Delete(executable);
                        }

                        File.Move(downloadPath, executable);
                        LaunchTool(executable);
                        break;
                    case ".rar":
                    case ".7z":
                    case ".zip":
                        using (var archiveFile = new SevenZipExtractor(downloadPath))
                        {
                            Action = "Extracting " + tool;
                            PercentDownloaded = 0;
                            void progressCallback(object sender, ProgressEventArgs progress)
                            {
                                PercentDownloaded = (int) progress.PercentDone;
                            };
                            archiveFile.Extracting += progressCallback;
                            archiveFile.ExtractArchive(outputDiretory); // extract all
                            LaunchTool(executable);
                        }
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
            Log.Information($"Launching: {localExecutable} {arguments}");
            Process.Start(localExecutable, arguments);
            Thread.Sleep(2500);
            OnClosing(EventArgs.Empty);
        }

        public event EventHandler Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler handler = Close;
            handler?.Invoke(this, e);
        }

        private async Task<Release> FetchLatestRelease()
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
                case MEM:
                    toolGithubOwner = "MassEffectModder";
                    toolGithubRepoName = "MassEffectModderLegacy";
                    break;
                case MEIM:
                    toolGithubOwner = "ME3Tweaks";
                    toolGithubRepoName = "MassEffectIniModder";
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
