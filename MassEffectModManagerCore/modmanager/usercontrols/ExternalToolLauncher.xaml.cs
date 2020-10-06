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
using MassEffectModManagerCore.modmanager.me3tweaks;
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
        public const string ME3Explorer_Beta = @"ME3Explorer (Nightly)";
        public const string ALOTInstaller = @"ALOT Installer";
        public const string MEIM = @"Mass Effect INI Modder"; //this is no longer external.
        public const string MEM = @"Mass Effect Modder";
        public const string MEM_CMD = @"Mass Effect Modder No Gui";
        public const string MER = @"Mass Effect Randomizer";
        public const string ALOTInstallerV4 = @"ALOT Installer V4";
        private string tool;

        public static List<string> ToolsCheckedForUpdatesInThisSession = new List<string>();
        public Visibility PercentVisibility { get; set; } = Visibility.Collapsed;
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
                    case ALOTInstallerV4:
                        return @"/modmanager/toolicons/alot_big.png";
                    case MER:
                        return @"/modmanager/toolicons/masseffectrandomizer_big.png";
                    case ME3Explorer:
                    case ME3Explorer_Beta:
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

        public static void DownloadToolME3Tweaks(string tool, string url, Version version, string executable,
            Action<string> currentTaskUpdateCallback = null, Action<bool> setPercentVisibilityCallback = null, Action<int> setPercentTaskDone = null, Action<string> resultingExecutableStringCallback = null,
            Action<Exception, string, string> errorExtractingCallback = null)
        {
            Analytics.TrackEvent(@"Downloading new external tool", new Dictionary<string, string>()
            {
                {@"Tool name", Path.GetFileName(executable)},
                {@"Version", version.ToString()}
            });
            var toolName = tool.Replace(@" ", "");
            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_interp_downloadingX, tool));
            setPercentVisibilityCallback.Invoke(true);
            setPercentTaskDone?.Invoke(0);

            WebClient downloadClient = new WebClient();

            downloadClient.Headers[@"user-agent"] = @"ME3TweaksModManager";
            string temppath = Utilities.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                setPercentTaskDone?.Invoke(e.ProgressPercentage);
            };

            Log.Information(@"Downloading file: " + url);
            var extension = Path.GetExtension(url);
            string downloadPath = Path.Combine(temppath, toolName + extension);

            downloadClient.DownloadFileCompleted += (a, b) =>
            {
                extractTool(tool, executable, extension, downloadPath, currentTaskUpdateCallback, setPercentVisibilityCallback, setPercentTaskDone, resultingExecutableStringCallback, errorExtractingCallback);
            };
            downloadClient.DownloadFileAsync(new Uri(url), downloadPath);
        }

        public static void DownloadToolGithub(string localToolFolderName, string tool, List<Release> releases, string executable,
            Action<string> currentTaskUpdateCallback = null, Action<bool> setPercentVisibilityCallback = null, Action<int> setPercentTaskDone = null, Action<string> resultingExecutableStringCallback = null,
            Action<Exception, string, string> errorExtractingCallback = null)
        {
            Release latestRelease = null;
            ReleaseAsset asset = null;
            Uri downloadLink = null;
            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_interp_downloadingX, tool));
            setPercentVisibilityCallback?.Invoke(true);
            setPercentTaskDone?.Invoke(0);
            foreach (var release in releases)
            {

                //Get asset info
                asset = release.Assets.FirstOrDefault();

                if (Path.GetFileName(executable) == @"MassEffectModder.exe")
                {
                    //Requires specific asset
                    asset = release.Assets.FirstOrDefault(x =>
                        x.Name == @"MassEffectModder-v" + release.TagName + @".7z");
                    if (asset == null)
                    {
                        Log.Warning(
                            $@"No applicable assets in release tag {release.TagName} for MassEffectModder, skipping");
                        continue;
                    }

                    latestRelease = release;
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }

                if (Path.GetFileName(executable) == @"MassEffectModderNoGui.exe")
                {
                    //Requires specific asset
                    asset = release.Assets.FirstOrDefault(x =>
                        x.Name == @"MassEffectModderNoGui-v" + release.TagName + @".7z");
                    if (asset == null)
                    {
                        Log.Warning(
                            $@"No applicable assets in release tag {release.TagName} for MassEffectModderNoGui, skipping");
                        continue;
                    }
                    latestRelease = release;
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }

                if (asset != null)
                {
                    latestRelease = release;
                    Log.Information($@"Using release {latestRelease.Name}");
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }
            }

            if (latestRelease == null || downloadLink == null) return;
            Analytics.TrackEvent(@"Downloading new external tool", new Dictionary<string, string>()
            {
                {@"Tool name", Path.GetFileName(executable)},
                {@"Version", latestRelease.TagName}
            });

            WebClient downloadClient = new WebClient();

            downloadClient.Headers[@"Accept"] = @"application/vnd.github.v3+json";
            downloadClient.Headers[@"user-agent"] = @"ME3TweaksModManager";
            string temppath = Utilities.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                setPercentTaskDone?.Invoke(e.ProgressPercentage);
            };

            var extension = Path.GetExtension(asset.BrowserDownloadUrl);
            string downloadPath = Path.Combine(temppath, tool.Replace(@" ", "") + extension);

            downloadClient.DownloadFileCompleted += (a, b) =>
            {
                extractTool(tool, executable, extension, downloadPath, currentTaskUpdateCallback, setPercentVisibilityCallback, setPercentTaskDone, resultingExecutableStringCallback, errorExtractingCallback);
            };
            Log.Information(@"Downloading file: " + downloadLink);

            downloadClient.DownloadFileAsync(downloadLink, downloadPath);
        }



        private static void extractTool(string tool, string executable, string extension, string downloadPath,
            Action<string> currentTaskUpdateCallback = null, Action<bool> setPercentVisibilityCallback = null, Action<int> setPercentTaskDone = null, Action<string> resultingExecutableStringCallback = null,
            Action<Exception, string, string> errorExtractingCallback = null)
        {
            //Todo: Account for errors
            var outputDirectory = Directory.CreateDirectory(Path.GetDirectoryName(executable)).FullName;
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
                    Log.Information(@"Extracting tool archive: " + downloadPath);
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
                            archiveFile.ExtractArchive(outputDirectory); // extract all
                            resultingExecutableStringCallback?.Invoke(executable);
                        }
                        catch (Exception e)
                        {
                            Log.Error($@"Could not extract/run tool {executable} after download: {e.Message}");
                            errorExtractingCallback?.Invoke(e, M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, e.Message), M3L.GetString(M3L.string_errorLaunchingTool));

                        }
                    }
                    break;
                default:
                    Log.Error($@"Failed to download correct file! We don't support this extension. The extension was {extension}");
                    var ex = new Exception(M3L.GetString(M3L.string_interp_unsupportedExtensionX, extension));
                    errorExtractingCallback?.Invoke(ex, M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, ex.Message), M3L.GetString(M3L.string_errorLaunchingTool));
                    break;
            }
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
                Action = M3L.GetString(M3L.string_interp_errorLaunchingToolX, e.Message);
                Thread.Sleep(6000);
            }

            OnClosing(DataEventArgs.Empty);
        }



        public static async Task<List<Release>> FetchReleases(string tool)
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


            Log.Information($@"Getting list of releases from github from ({toolGithubOwner}/{toolGithubRepoName})");
            var client = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
            try
            {
                var releases = await client.Repository.Release.GetAll(toolGithubOwner, toolGithubRepoName);
                if (releases.Count > 0)
                {
                    Log.Information(@"Parsing release information from github");
                    return releases.Where(x => !x.Prerelease && x.Assets.Count > 0).ToList();
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
            Log.Information($@"FetchAndLaunchTool() for {tool}");
            var toolName = tool.Replace(@" ", "");
            var localToolFolderName = getToolStoragePath(tool);
            var localExecutable = Path.Combine(localToolFolderName, toolNameToExeName(tool));
            bool needsDownloading = !File.Exists(localExecutable);

            if (!needsDownloading && ToolsCheckedForUpdatesInThisSession.Contains(tool))
            {
                //Don't check for updates again.
                resultingExecutableStringCallback?.Invoke(localExecutable);
                return;
            }

            if (toolIsGithubBased(tool))
            {
                currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_checkingForUpdates));
                var releases = await FetchReleases(tool);


                //Failed to get release check
                if (releases == null)
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
                    DownloadToolGithub(localToolFolderName, tool, releases, localExecutable,
                        s => currentTaskUpdateCallback?.Invoke(s),
                        vis => setPercentVisibilityCallback?.Invoke(vis),
                        percent => setPercentTaskDone?.Invoke(percent),
                        exe => resultingExecutableStringCallback?.Invoke(exe),
                        (exception, message, caption) => errorExtractingCallback?.Invoke(exception, message, caption)
                    );
                }
                else
                {
                    //Check if it need updated
                    bool needsUpdated = false;
                    var latestRelease = releases.FirstOrDefault(x => hasApplicableAsset(tool, x));
                    if (latestRelease != null)
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(localExecutable);
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
                            try
                            {
                                Version serverVersion = new Version(latestRelease.TagName);
                                Version localVersion =
                                    new Version(
                                        $@"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
                                if (serverVersion > localVersion)
                                {
                                    needsUpdated = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(@"Invalid version number on release: " + latestRelease.TagName);
                            }
                        }
                    }

                    if (!needsUpdated)
                    {
                        resultingExecutableStringCallback?.Invoke(localExecutable);
                    }
                    else
                    {
                        DownloadToolGithub(localToolFolderName, tool, releases, localExecutable,
                            s => currentTaskUpdateCallback?.Invoke(s),
                            vis => setPercentVisibilityCallback?.Invoke(vis),
                            percent => setPercentTaskDone?.Invoke(percent),
                            exe => resultingExecutableStringCallback?.Invoke(exe),
                            (exception, message, caption) => errorExtractingCallback?.Invoke(exception, message, caption)
                        );
                    }
                }
            }
            else
            {
                //Not github based
                string downloadLink = me3tweaksToolGetDownloadUrl(tool);
                Version downloadVersion = me3tweaksToolGetLatestVersion(tool);
                if (downloadVersion != null && downloadLink != null) // has enough info
                {
                    if (needsDownloading) // not present locally
                    {
                        DownloadToolME3Tweaks(tool, downloadLink, downloadVersion, localExecutable,
                            s => currentTaskUpdateCallback?.Invoke(s),
                            vis => setPercentVisibilityCallback?.Invoke(vis),
                            percent => setPercentTaskDone?.Invoke(percent),
                            exe => resultingExecutableStringCallback?.Invoke(exe),
                            (exception, message, caption) => errorExtractingCallback?.Invoke(exception, message, caption)
                        );
                        ToolsCheckedForUpdatesInThisSession.Add(tool);
                        return; //is this the right place for this?
                    }
                    else
                    {
                        //Check if it need updated
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(localExecutable);
                        Version localVersion = new Version($@"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
                        if (downloadVersion > localVersion)
                        {
                            needsDownloading = true;
                        }
                    }



                    if (!needsDownloading)
                    {
                        resultingExecutableStringCallback?.Invoke(localExecutable);
                    }
                    else
                    {
                        DownloadToolME3Tweaks(tool, downloadLink, downloadVersion, localExecutable,
                            s => currentTaskUpdateCallback?.Invoke(s),
                            vis => setPercentVisibilityCallback?.Invoke(vis),
                            percent => setPercentTaskDone?.Invoke(percent),
                            exe => resultingExecutableStringCallback?.Invoke(exe),
                            (exception, message, caption) => errorExtractingCallback?.Invoke(exception, message, caption)
                        );
                    }
                }
            }

            ToolsCheckedForUpdatesInThisSession.Add(tool);
        }

        private static bool hasApplicableAsset(string tool, Release release)
        {
            if (release.Assets.Any())
            {
                if (tool == MEM)
                {
                    return release.Assets.Any(x => x.Name == @"MassEffectModder-v" + release.TagName + @".7z");
                }
                if (tool == MEM_CMD)
                {
                    return release.Assets.Any(x => x.Name == @"MassEffectModderNoGui-v" + release.TagName + @".7z");
                }
                return true; //We don't check the others
            }
            return false;
        }

        private static string getToolStoragePath(string tool)
        {
            if (tool == MEM_CMD)
            {
                // Internal tool
                return Path.Combine(Utilities.GetCachedExecutablePath());
            }
            return Path.Combine(Utilities.GetDataDirectory(), @"ExternalTools", tool);
        }

        private static Version me3tweaksToolGetLatestVersion(string tool)
        {
            switch (tool)
            {
                case ME3Explorer_Beta:
                    if (App.ServerManifest.TryGetValue(@"me3explorerbeta_latestversion", out var me3expbLatestversion))
                    {
                        return new Version(me3expbLatestversion);
                    }
                    break;
                case ALOTInstallerV4:
                    if (App.ServerManifest.TryGetValue(@"alotinstallerv4_latestversion", out var alotinstallerv4_latestversion))
                    {
                        return new Version(alotinstallerv4_latestversion);
                    }
                    break;
            }

            return null;
        }

        private static string me3tweaksToolGetDownloadUrl(string tool)
        {
            switch (tool)
            {
                case ME3Explorer_Beta:
                    if (App.ServerManifest.TryGetValue(@"me3explorerbeta_latestlink", out var me3expbLatestlink))
                    {
                        return me3expbLatestlink;
                    }
                    break;
                case ALOTInstallerV4:
                    if (App.ServerManifest.TryGetValue(@"alotinstallerv4_latestlink", out var alotinstallerv4link))
                    {
                        return alotinstallerv4link;
                    }
                    break;
            }

            return null;
        }

        private static bool toolIsGithubBased(string toolname)
        {
            if (toolname == ME3Explorer_Beta) return false; //me3tweaks. Info is in startup manifest
            if (toolname == ALOTInstallerV4) return false; //me3tweaks. Info is in startup manifest
            return true;
        }

        private static string toolNameToExeName(string toolname)
        {
            if (toolname == ME3Explorer_Beta) return @"ME3Explorer.exe";
            if (toolname == ALOTInstallerV4) return @"ALOTInstallerWPF.exe";
            return toolname.Replace(@" ", @"") + @".exe";
        }

        private static string[] SupportedToolIDs =
        {
            ME3Explorer,
            ME3Explorer_Beta,
            EGMSettings,
            MEM,
            MER,
            ALOTInstaller,
            ALOTInstallerV4
        };
        internal static bool IsSupportedToolID(string toolId) => SupportedToolIDs.Contains(toolId);
    }
}
