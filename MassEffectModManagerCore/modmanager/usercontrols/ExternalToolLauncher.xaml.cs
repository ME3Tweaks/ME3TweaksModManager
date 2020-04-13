

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
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Octokit;
using Serilog;
using SevenZip;
using Application = System.Windows.Application;
using Exception = System.Exception;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ExternalToolLauncher : MMBusyPanelBase
    {
        //have to be const apparently
        public const string EGMSettings = @"EGMSettings";
        public const string ME3Explorer = @"ME3Explorer";
        public const string ALOTInstaller = @"ALOT Installer";
        public const string MEIM = @"Mass Effect INI Modder";
        public const string MEM = @"Mass Effect Modder";
        public const string MEM_CMD = @"Mass Effect Modder No Gui";
        public const string MER = @"Mass Effect Randomizer";
        public const string ME3EXP_DLCUNPACKER = @"JUMPLIST_DLCUNPACKER";
        public const string ME3EXP_MOUNTEDITOR = @"JUMPLIST_MOUNTEDITOR";
        public const string ME3EXP_PACKAGEDUMPER = @"JUMPLIST_PACKAGEDUMPER";
        private string tool;

        public static List<string> ToolsCheckedForUpdatesInThisSession = new List<string>();
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
                        return @"/modmanager/toolicons/alot_big.png";
                    case MER:
                        return @"/modmanager/toolicons/masseffectrandomizer_big.png";
                    case ME3Explorer:
                        return @"/modmanager/toolicons/me3explorer_big.png";
                    case MEM:
                        return @"/modmanager/toolicons/masseffectmodder_big.png";
                    case MEIM:
                        return @"/modmanager/toolicons/masseffectinimodder_big.png";
                    case EGMSettings:
                        return @"/modmanager/toolicons/egmsettings_big.png";
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

        public static void DownloadTool(string localToolFolderName, string tool, Release latestRelease, string executable,
            Action<string> currentTaskUpdateCallback = null, Action<bool> setPercentVisibilityCallback = null, Action<int> setPercentTaskDone = null, Action<string> resultingExecutableStringCallback = null,
            Action<Exception, string, string> errorExtractingCallback = null)
        {
            Analytics.TrackEvent(@"Downloading new external tool", new Dictionary<string, string>()
            {
                {@"Tool name", Path.GetFileName(executable) },
                {@"Version", latestRelease.TagName}
            });
            var toolName = tool.Replace(@" ", "");
            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_interp_downloadingX, tool));
            setPercentVisibilityCallback.Invoke(true);
            setPercentTaskDone?.Invoke(0);

            WebClient downloadClient = new WebClient();

            downloadClient.Headers[@"Accept"] = @"application/vnd.github.v3+json";
            downloadClient.Headers[@"user-agent"] = @"ME3TweaksModManager";
            string temppath = Path.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                setPercentTaskDone?.Invoke(e.ProgressPercentage);
            };
            var asset = latestRelease.Assets[0];
            if (Path.GetFileName(executable) == @"MassEffectModder.exe")
            {
                //Requires specific asset
                asset = latestRelease.Assets.FirstOrDefault(x => x.Name == @"MassEffectModder-v" + latestRelease.TagName + @".7z");
                if (asset == null)
                {
                    Log.Error(@"Error downloading Mass Effect Modder: Could not find asset in latest release!");
                    return;
                }
            }
            if (Path.GetFileName(executable) == @"MassEffectModderNoGui.exe")
            {
                //Requires specific asset
                asset = latestRelease.Assets.FirstOrDefault(x => x.Name == @"MassEffectModderNoGui-v" + latestRelease.TagName + @".7z");
                if (asset == null)
                {
                    Log.Error(@"Error downloading Mass Effect Modder No Gui: Could not find asset in latest release!");
                    return;
                }
            }
            var extension = Path.GetExtension(asset.BrowserDownloadUrl);
            string downloadPath = temppath + toolName + extension;

            downloadClient.DownloadFileCompleted += (a, b) =>
            {
                //Todo: Account for errors
                var outputDiretory = Directory.CreateDirectory(Path.GetDirectoryName(executable)).FullName;
                switch (extension)
                {
                    case @".exe":
                        if (File.Exists(executable))
                        {
                            File.Delete(executable);
                        }

                        File.Move(downloadPath, executable);
                        resultingExecutableStringCallback?.Invoke(executable);
                        break;
                    case @".rar":
                    case @".7z":
                    case @".zip":
                        using (var archiveFile = new SevenZipExtractor(downloadPath))
                        {
                            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_interp_extractingX, tool));
                            setPercentTaskDone?.Invoke(0);
                            void progressCallback(object sender, ProgressEventArgs progress)
                            {
                                setPercentTaskDone?.Invoke(progress.PercentDone);
                            };
                            archiveFile.Extracting += progressCallback;
                            try
                            {
                                archiveFile.ExtractArchive(outputDiretory); // extract all
                                resultingExecutableStringCallback?.Invoke(executable);
                            }
                            catch (Exception e)
                            {
                                Log.Error($@"Could not extract/run tool {executable} after download: {e.Message}");
                                errorExtractingCallback?.Invoke(e, M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, e.Message), M3L.GetString(M3L.string_errorLaunchingTool));

                            }
                        }
                        break;
                }
            };
            downloadClient.DownloadFileAsync(new Uri(asset.BrowserDownloadUrl), downloadPath);
        }

        private void LaunchTool(string localExecutable)
        {
            Action = M3L.GetString(M3L.string_interp_launching, tool);
            Analytics.TrackEvent(@"Launching tool", new Dictionary<string, string>()
            {
                {@"Tool name", Path.GetFileName(localExecutable) }
            });
            PercentVisibility = Visibility.Collapsed;
            PercentDownloaded = 0;
            Log.Information($@"Launching: {localExecutable} {arguments}");
            try
            {
                Process.Start(localExecutable, arguments);
                Thread.Sleep(2500);

            }
            catch (Exception e)
            {
                Log.Error($@"Error launching tool {localExecutable}: {e.Message}");
                Action = $"Error launching tool:\n{e.Message}";
                Thread.Sleep(6000);
            }

            OnClosing(DataEventArgs.Empty);
        }



        public static async Task<Release> FetchLatestRelease(string tool)
        {
            string toolGithubOwner = null;
            string toolGithubRepoName = null;
            switch (tool)
            {
                case ALOTInstaller:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"ALOTInstaller";
                    break;
                case MER:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"MassEffectRandomizer";
                    break;
                case ME3Explorer:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"ME3Explorer";
                    break;
                case MEM:
                case MEM_CMD:
                    toolGithubOwner = @"MassEffectModder";
                    toolGithubRepoName = @"MassEffectModder";
                    break;
                case MEIM:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"MassEffectIniModder";
                    break;
                case EGMSettings:
                    toolGithubOwner = @"Kinkojiro";
                    toolGithubRepoName = @"EGM-Settings";
                    break;
            }


            Log.Information($@"Checking for application updates from github ({toolGithubOwner}, {toolGithubRepoName})");
            var client = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
            try
            {
                var releases = await client.Repository.Release.GetAll(toolGithubOwner, toolGithubRepoName);
                if (releases.Count > 0)
                {
                    Log.Information(@"Parsing release information from github");

                    //The release we want to check is always the latest with assets that is not a pre-release
                    return releases.FirstOrDefault(x => !x.Prerelease && x.Assets.Count > 0);
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Error checking for tool update: " + e);
            }

            return null;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //Abort download

        }

        public override void OnPanelVisible()
        {
            BackgroundWorker bw = new BackgroundWorker();
            #region callbacks
            void failedToDownload()
            {
                Action = M3L.GetString(M3L.string_interp_failedToDownloadX, tool);
                PercentVisibility = Visibility.Collapsed;
                PercentDownloaded = 0;
                Thread.Sleep(5000);
                OnClosing(DataEventArgs.Empty);
            }
            void launchTool(string exe) => LaunchTool(exe);
            void errorExtracting(Exception e, string message, string caption)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    M3L.ShowDialog(mainwindow, message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                    OnClosing(DataEventArgs.Empty);
                });
            }
            void currentTaskCallback(string s) => Action = s;
            void setPercentDone(int pd) => PercentDownloaded = pd;
            void setPercentVisibility(bool vis) => PercentVisibility = vis ? Visibility.Visible : Visibility.Collapsed;
            #endregion
            bw.DoWork += (a, b) => { FetchAndLaunchTool(tool, currentTaskCallback, setPercentVisibility, setPercentDone, launchTool, failedToDownload, errorExtracting); };
            bw.RunWorkerAsync();
        }

        public static async void FetchAndLaunchTool(string tool,
                Action<string> currentTaskUpdateCallback = null,
                Action<bool> setPercentVisibilityCallback = null,
                Action<int> setPercentTaskDone = null,
                Action<string> resultingExecutableStringCallback = null,
                Action failedToDownloadCallback = null,
                Action<Exception, string, string> errorExtractingCallback = null)

        {
            var toolName = tool.Replace(@" ", "");
            var localToolFolderName = Path.Combine(Utilities.GetDataDirectory(), @"ExternalTools", toolName);
            var localExecutable = Path.Combine(localToolFolderName, toolName + @".exe");
            bool needsDownloading = !File.Exists(localExecutable);

            if (!needsDownloading && ToolsCheckedForUpdatesInThisSession.Contains(tool))
            {
                //Don't check for updates again.
                resultingExecutableStringCallback?.Invoke(localExecutable);
                return;
            }
            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_checkingForUpdates));
            var latestRelease = await FetchLatestRelease(tool);


            //Failed to get release check
            if (latestRelease == null)
            {
                if (!needsDownloading)
                {
                    resultingExecutableStringCallback?.Invoke(localExecutable);
                    return;
                }
                else
                {
                    //Must run on UI thread
                    //MessageBox.Show($"Unable to download {tool}.\nPlease check your network connection and try again.\nIf the issue persists, please come to the ME3Tweaks Discord.");
                    Log.Error(@"Unable to launch tool - could not download, and does not exist locally: " + localExecutable);
                    failedToDownloadCallback?.Invoke();
                    return;
                }
            }

            //Got a release
            if (needsDownloading)
            {
                DownloadTool(localToolFolderName, tool, latestRelease, localExecutable,
                    s => currentTaskUpdateCallback?.Invoke(s),
                    vis => setPercentVisibilityCallback?.Invoke(vis),
                    percent => setPercentTaskDone?.Invoke(percent),
                    exe => resultingExecutableStringCallback?.Invoke(exe),
                    (exception, message, caption) => errorExtractingCallback?.Invoke(exception ,message, caption)
                    );
            }
            else
            {
                //Check if it need updated
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(localExecutable);
                bool needsUpdated = false;
                if (tool == MEM || tool == MEM_CMD)
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
                    Version localVersion = new Version($@"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
                    if (serverVersion > localVersion)
                    {
                        needsUpdated = true;
                    }
                }

                if (!needsUpdated)
                {
                    resultingExecutableStringCallback?.Invoke(localExecutable);
                }
                else
                {
                    DownloadTool(localToolFolderName, tool, latestRelease, localExecutable,
                        s => currentTaskUpdateCallback?.Invoke(s),
                        vis => setPercentVisibilityCallback?.Invoke(vis),
                        percent => setPercentTaskDone?.Invoke(percent),
                        exe => resultingExecutableStringCallback?.Invoke(exe),
                        (exception, message, caption) => errorExtractingCallback?.Invoke(exception, message, caption)
                    );
                }

                ToolsCheckedForUpdatesInThisSession.Add(tool);
            }
        }

        private static string[] SupportedToolIDs =
        {
            ME3Explorer,
            EGMSettings,
            MEM,
            MER,
            ALOTInstaller
        };
        internal static bool IsSupportedToolID(string toolId) => SupportedToolIDs.Contains(toolId);
    }
}
