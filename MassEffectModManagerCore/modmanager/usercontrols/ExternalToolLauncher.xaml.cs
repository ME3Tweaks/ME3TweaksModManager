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
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Octokit;
using SevenZip;
using Application = System.Windows.Application;
using Exception = System.Exception;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ExternalToolLauncher : MMBusyPanelBase
    {
        //have to be const apparently
        public const string EGMSettings = @"EGMSettings";
        public const string EGMSettingsLE = @"EGMSettingsLE";
        public const string LegendaryExplorer = @"Legendary Explorer";
        public const string LegendaryExplorer_Beta = @"Legendary Explorer (Nightly)";
        public const string ALOTInstaller = @"ALOT Installer";
        public const string MEIM = @"Mass Effect INI Modder"; //this is no longer external.
        public const string MEM = @"Mass Effect Modder"; // OT only
        public const string MEM_CMD = @"Mass Effect Modder No Gui"; // OT Only
        public const string MEM_LE = @"Mass Effect Modder LE"; // LE only
        public const string MEM_LE_CMD = @"Mass Effect Modder No Gui LE"; // LE Only
        public const string MER = @"Mass Effect Randomizer";
        public const string ME2R = @"Mass Effect 2 Randomizer";
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
                        return @"/modmanager/toolicons/alot_big.png";
                    case MER:
                    case ME2R:
                        return @"/modmanager/toolicons/masseffectrandomizer_big.png";
                    case LegendaryExplorer:
                        return @"/modmanager/toolicons/lex_big.png";
                    case LegendaryExplorer_Beta:
                        return @"/modmanager/toolicons/lex_big_nightly.png";
                    case MEM:
                    case MEM_LE:
                        return @"/modmanager/toolicons/masseffectmodder_big.png";
                    case MEIM:
                        return @"/modmanager/toolicons/masseffectinimodder_big.png";
                    case EGMSettings:
                    case EGMSettingsLE:
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
            currentTaskUpdateCallback?.Invoke($@"{M3L.GetString(M3L.string_interp_downloadingX, tool)} {version}");

            setPercentVisibilityCallback.Invoke(true);
            setPercentTaskDone?.Invoke(0);

            WebClient downloadClient = new WebClient();

            downloadClient.Headers[@"user-agent"] = @"ME3TweaksModManager";
            string temppath = M3Utilities.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) =>
            {
                setPercentTaskDone?.Invoke(e.ProgressPercentage);
            };

            M3Log.Information(@"Downloading file: " + url);
            var extension = Path.GetExtension(url);
            string downloadPath = Path.Combine(temppath, toolName + extension);

            downloadClient.DownloadDataCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    M3Log.Error($@"Error downloading ME3Tweaks tool: {b.Error.Message}");
                    errorExtractingCallback?.Invoke(b.Error,
                        M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, b.Error.Message),
                        M3L.GetString(M3L.string_errorLaunchingTool));
                }
                else
                {
                    extractTool(tool, executable, extension, new MemoryStream(b.Result), currentTaskUpdateCallback, setPercentVisibilityCallback, setPercentTaskDone, resultingExecutableStringCallback, errorExtractingCallback);
                }
            };
            downloadClient.DownloadDataAsync(new Uri(url), downloadPath);
        }

        public static void DownloadToolGithub(string localToolFolderName, string tool, List<Release> releases,
            string executable,
            Action<string> currentTaskUpdateCallback = null, Action<bool> setPercentVisibilityCallback = null,
            Action<int> setPercentTaskDone = null, Action<string> resultingExecutableStringCallback = null,
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

                #region MEM SPECIFIC

                if (Path.GetFileName(executable).StartsWith(@"MassEffectModderNoGui"))
                {
                    //Requires specific asset
                    if (int.TryParse(release.TagName, out var relVer))
                    {
                        if (tool == MEM_CMD && relVer >= 500)
                        {
                            asset = null;
                            M3Log.Warning(
                                $@"MassEffectModderNoGui versions >= 500 are not supported for Original Trilogy, skipping version {relVer}");
                            continue;
                        }
                        else if (tool == MEM_LE_CMD && relVer < 500)
                        {
                            asset = null;
                            M3Log.Warning(
                                $@"MassEffectModderNoGui versions < 500 are not supported for Legendary Edition, skipping version {relVer}");
                            continue;
                        }
                    }

                    asset = release.Assets.FirstOrDefault(x =>
                        x.Name == @"MassEffectModderNoGui-v" + release.TagName + @".7z");
                    if (asset == null)
                    {
                        M3Log.Warning(
                            $@"No applicable assets in release tag {release.TagName} for MassEffectModderNoGui, skipping");
                        continue;
                    }

                    latestRelease = release;
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }
                else if (Path.GetFileName(executable).StartsWith(@"MassEffectModder"))
                {
                    //Requires specific asset
                    if (int.TryParse(release.TagName, out var relVer))
                    {
                        if (tool == MEM && relVer >= 500)
                        {
                            asset = null;
                            M3Log.Warning(
                                $@"MassEffectModder versions >= 500 are not supported for Original Trilogy, skipping version {relVer}");
                            continue;
                        }
                        else if (tool == MEM_LE && relVer < 500)
                        {
                            asset = null;
                            M3Log.Warning(
                                $@"MassEffectModder versions < 500 are not supported for Legendary Edition, skipping version {relVer}");
                            continue;
                        }
                    }

                    asset = release.Assets.FirstOrDefault(x =>
                        x.Name == @"MassEffectModder-v" + release.TagName + @".7z");
                    if (asset == null)
                    {
                        M3Log.Warning(
                            $@"No applicable assets in release tag {release.TagName} for MassEffectModder, skipping");
                        continue;
                    }

                    latestRelease = release;
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }

                if (asset != null)
                {
                    latestRelease = release;
                    M3Log.Information($@"Using release {latestRelease.Name}");
                    downloadLink = new Uri(asset.BrowserDownloadUrl);
                    break;
                }
            }

            #endregion

            if (latestRelease == null || downloadLink == null) return;
            Analytics.TrackEvent(@"Downloading new external tool", new Dictionary<string, string>()
            {
                {@"Tool name", Path.GetFileName(executable)},
                {@"Version", latestRelease.TagName}
            });
            currentTaskUpdateCallback?.Invoke(
                $@"{M3L.GetString(M3L.string_interp_downloadingX, tool)} {latestRelease.TagName}");

            WebClient downloadClient = new WebClient();

            downloadClient.Headers[@"Accept"] = @"application/vnd.github.v3+json";
            downloadClient.Headers[@"user-agent"] = @"ME3TweaksModManager";
            //string temppath = Utilities.GetTempPath();
            downloadClient.DownloadProgressChanged += (s, e) => { setPercentTaskDone?.Invoke(e.ProgressPercentage); };

            var extension = Path.GetExtension(asset.BrowserDownloadUrl);
            //string downloadPath = Path.Combine(temppath, tool.Replace(@" ", "") + extension);

            downloadClient.DownloadDataCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    extractTool(tool, executable, extension, new MemoryStream(b.Result), currentTaskUpdateCallback,
                        setPercentVisibilityCallback, setPercentTaskDone, resultingExecutableStringCallback,
                        errorExtractingCallback);
                }
                else
                {
                    M3Log.Error($@"Error downloading tool: {b.Error.Message}");
                    errorExtractingCallback?.Invoke(b.Error,
                        M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, b.Error.Message),
                        M3L.GetString(M3L.string_errorLaunchingTool));
                }
            };
            M3Log.Information(@"Downloading file: " + downloadLink);
            downloadClient.DownloadDataAsync(downloadLink);
        }


        private static void extractTool(string tool, string executable, string extension, MemoryStream downloadStream,
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

                    downloadStream.WriteToFile(executable);
                    resultingExecutableStringCallback?.Invoke(executable);
                    break;
                case @".rar":
                case @".7z":
                case @".zip":
                    M3Log.Information(@"Extracting tool archive");
                    try
                    {
                        downloadStream.Position = 0;
                        using (var archiveFile = new SevenZipExtractor(downloadStream))
                        {
                            currentTaskUpdateCallback?.Invoke(M3L.GetString(M3L.string_interp_extractingX, tool));
                            setPercentTaskDone?.Invoke(0);

                            void progressCallback(object sender, ProgressEventArgs progress)
                            {
                                setPercentTaskDone?.Invoke(progress.PercentDone);
                            }

                            ;
                            archiveFile.Extracting += progressCallback;
                            try
                            {
                                archiveFile.ExtractArchive(outputDirectory); // extract all
                                                                             // Touchup for MEM LE versions
                                if (Path.GetFileName(executable) == @"MassEffectModderLE.exe")
                                    executable = Path.Combine(Directory.GetParent(executable).FullName, @"MassEffectModder.exe");
                                if (Path.GetFileName(executable) == @"MassEffectModderNoGuiLE.exe")
                                    executable = Path.Combine(Directory.GetParent(executable).FullName, @"MassEffectModderNoGui.exe");


                                resultingExecutableStringCallback?.Invoke(executable);
                            }
                            catch (Exception e)
                            {
                                M3Log.Error($@"Could not extract/run tool {executable} after download: {e.Message}");
                                errorExtractingCallback?.Invoke(e, M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, e.Message), M3L.GetString(M3L.string_errorLaunchingTool));

                            }
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($@"Exception extracting archive: {e.Message}");
                        errorExtractingCallback?.Invoke(e, M3L.GetString(M3L.string_interp_errorDownloadingAndLaunchingTool, e.Message), M3L.GetString(M3L.string_errorLaunchingTool));
                    }

                    break;
                default:
                    M3Log.Error($@"Failed to download correct file! We don't support this extension. The extension was {extension}");
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

            // Touchup for MEM LE versions
            if (Path.GetFileName(localExecutable) == @"MassEffectModderLE.exe")
                localExecutable = Path.Combine(Directory.GetParent(localExecutable).FullName, @"MassEffectModder.exe");
            if (Path.GetFileName(localExecutable) == @"MassEffectModderNoGuiLE.exe")
                localExecutable = Path.Combine(Directory.GetParent(localExecutable).FullName, @"MassEffectModderNoGui.exe");

            PercentVisibility = Visibility.Collapsed;
            PercentDownloaded = 0;
            M3Log.Information($@"Launching: {localExecutable} {arguments}");
            try
            {
                Process.Start(localExecutable, arguments);
                Thread.Sleep(2500);

            }
            catch (Exception e)
            {
                M3Log.Error($@"Error launching tool {localExecutable}: {e.Message}");
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
                case ME2R:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"MassEffect2Randomizer";
                    break;
                case LegendaryExplorer:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"LegendaryExplorer";
                    break;
                case MEM:
                case MEM_LE:
                case MEM_CMD:
                case MEM_LE_CMD:
                    toolGithubOwner = @"MassEffectModder";
                    toolGithubRepoName = @"MassEffectModder";
                    break;
                case MEIM:
                    toolGithubOwner = @"ME3Tweaks";
                    toolGithubRepoName = @"MassEffectIniModder";
                    break;
                case EGMSettings:
                case EGMSettingsLE:
                    toolGithubOwner = @"Kinkojiro";
                    toolGithubRepoName = @"EGM-Settings";
                    break;
            }


            M3Log.Information($@"Getting list of releases from github from ({toolGithubOwner}/{toolGithubRepoName})");
            var client = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
            try
            {
                var releases = await client.Repository.Release.GetAll(toolGithubOwner, toolGithubRepoName);
                if (releases.Count > 0)
                {
                    M3Log.Information(@"Parsing release information from github");
                    return releases.Where(x => !x.Prerelease && x.Assets.Count > 0).ToList();
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error checking for tool update: " + e);
            }

            return null;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //Abort download

        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            BackgroundWorker bw = new BackgroundWorker();
            #region callbacks
            void failedToDownload(string failureMessage)
            {
                Action = M3L.GetString(M3L.string_interp_failedToDownloadX, tool, failureMessage);
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
            Action<string> failedToDownloadCallback = null,
            Action<Exception, string, string> errorExtractingCallback = null)
        {
            M3Log.Information($@"FetchAndLaunchTool() for {tool}");
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

            var prereqCheckMessage = checkToolPrerequesites(tool);
            if (prereqCheckMessage != null)
            {
                M3Log.Error($@"Prerequisite not met: {prereqCheckMessage}");
                failedToDownloadCallback?.Invoke(M3L.GetString(M3L.string_interp_prerequisiteNotMetX, prereqCheckMessage));
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
                        M3Log.Error(@"Unable to launch tool - could not download, and does not exist locally: " + localExecutable);
                        failedToDownloadCallback?.Invoke(M3L.GetString(M3L.string_downloadFailed));
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
                        if (tool is MEM or MEM_CMD or MEM_LE or MEM_LE_CMD)
                        {
                            //Checks based on major
                            int releaseVer = int.Parse(latestRelease.TagName);
                            if (releaseVer > fvi.ProductMajorPart && releaseVer < 500 && tool is MEM or MEM_CMD)
                            {
                                needsUpdated = true;
                            }
                            if (releaseVer > fvi.ProductMajorPart && releaseVer >= 500 && tool is MEM_LE or MEM_LE_CMD)
                            {
                                needsUpdated = true;
                            }
                        }
                        else
                        {
                            try
                            {
                                Version serverVersion = new Version(latestRelease.TagName);
                                Version localVersion = new Version($@"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
                                if (serverVersion > localVersion)
                                {
                                    needsUpdated = true;
                                }
                            }
                            catch (Exception e)
                            {
                                M3Log.Error($@"Invalid version number on release {latestRelease.TagName}: {e.Message}");
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
                try
                {
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
                                (exception, message, caption) =>
                                    errorExtractingCallback?.Invoke(exception, message, caption)
                            );
                            ToolsCheckedForUpdatesInThisSession.Add(tool);
                            return; //is this the right place for this?
                        }
                        else
                        {
                            //Check if it need updated
                            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(localExecutable);
                            Version localVersion =
                                new Version(
                                    $@"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart}");
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
                                (exception, message, caption) =>
                                    errorExtractingCallback?.Invoke(exception, message, caption)
                            );
                        }
                    }
                    else
                    {
                        // Not enough information!
                        M3Log.Error(@"Unable to download ME3Tweaks hosted tool: Information not present in startup manifest. Ensure M3 can connect to the internet at boot time");
                        failedToDownloadCallback?.Invoke(M3L.GetString(M3L.string_error_cantDownloadNotEnoughInfoInStartupManifest));
                    }
                }
                catch (Exception ex)
                {
                    M3Log.Error($@"Error downloading ME3Tweaks too: {ex.Message}");
                    failedToDownloadCallback?.Invoke(ex.Message);
                }
            }

            ToolsCheckedForUpdatesInThisSession.Add(tool);
        }

        private static string checkToolPrerequesites(string toolname)
        {
            switch (toolname)
            {
                case LegendaryExplorer_Beta:
                    {
                        if (!M3Utilities.IsNetRuntimeInstalled(5))
                        {
                            return M3L.GetString(M3L.string_error_net5RuntimeMissing);
                        }
                        break;
                    }
            }

            return null; // nothing wrong
        }

        private static bool hasApplicableAsset(string tool, Release release)
        {
            if (release.Assets.Any())
            {
                // Version gating
                int.TryParse(release.TagName, out var relVersion);
                if (tool is MEM_LE or MEM_LE_CMD && relVersion < 500)
                {
                    return false;
                }
                if (tool is MEM or MEM_CMD && relVersion > 500)
                {
                    return false;
                }

                if (tool is MEM or MEM_LE)
                {
                    return release.Assets.Any(x => x.Name == @"MassEffectModder-v" + release.TagName + @".7z");
                }
                if (tool is MEM_CMD or MEM_LE_CMD)
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
                return Path.Combine(M3Utilities.GetCachedExecutablePath());
            }

            if (tool == EGMSettingsLE)
            {
                // Same as OT path
                return Path.Combine(M3Utilities.GetDataDirectory(), @"ExternalTools", @"EGMSettings");
            }


            return Path.Combine(M3Utilities.GetDataDirectory(), @"ExternalTools", tool);
        }

        private static Version me3tweaksToolGetLatestVersion(string tool)
        {
            switch (tool)
            {
                case LegendaryExplorer_Beta:
                    if (App.ServerManifest.TryGetValue(@"legendaryexplorernightly_latestversion", out var lexbVersion))
                    {
                        return new Version(lexbVersion);
                    }
                    break;
            }

            return null;
        }

        private static string me3tweaksToolGetDownloadUrl(string tool)
        {
            switch (tool)
            {
                case LegendaryExplorer_Beta:
                    if (App.ServerManifest.TryGetValue(@"legendaryexplorernightly_latestlink", out var lexNightlyLatestLink))
                    {
                        return lexNightlyLatestLink;
                    }
                    break;
            }

            return null;
        }

        private static bool toolIsGithubBased(string toolname)
        {
            if (toolname == LegendaryExplorer_Beta) return false; //me3tweaks. Info is in startup manifest
            return true;
        }

        private static string toolNameToExeName(string toolname)
        {
            if (toolname == LegendaryExplorer_Beta) return @"LegendaryExplorer.exe";
            if (toolname == ME2R) return @"ME2Randomizer.exe";
            if (toolname == MEM_LE) return @"MassEffectModder.exe";
            if (toolname == MEM_LE_CMD) return @"MassEffectModderNoGui.exe";
            if (toolname == EGMSettingsLE) return @"EGMSettings.exe";
            return toolname.Replace(@" ", @"") + @".exe";
        }

        private static string[] SupportedToolIDs =
        {
            LegendaryExplorer,
            LegendaryExplorer_Beta,
            EGMSettings,
            EGMSettingsLE,
            MEM,
            MEM_LE,
            MER,
            ME2R,
            ALOTInstaller,
        };
        internal static bool IsSupportedToolID(string toolId) => SupportedToolIDs.Contains(toolId);
    }
}
