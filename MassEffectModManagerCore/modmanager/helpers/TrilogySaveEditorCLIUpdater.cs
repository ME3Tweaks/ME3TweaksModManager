using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using CliWrap.EventStream;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Helpers.MEM;
using ME3TweaksCore.Localization;
using ME3TweaksModManager.modmanager.usercontrols;
using Octokit;
using CliWrap;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Handles updates to Trilogy Save Editor (CLI version)
    /// </summary>
    public class TrilogySaveEditorCLIUpdater
    {

        /// <summary>
        /// Checks for and updates mem if necessary. This method is blocking.
        /// </summary>
        public static async Task<bool> UpdateTSECLI(Action<long, long> downloadProgressChanged = null,
            Action<Exception> exceptionUpdating = null, Action<string> statusMessageUpdate = null)
        {
            var currentLocalVersion = new Version(0, 0);
            var tseCliToolFolder = ExternalToolLauncher.GetToolStoragePath(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD);
            var tseCliToolPath = Path.Combine(tseCliToolFolder, ExternalToolLauncher.ToolNameToExeName(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD));
            Directory.CreateDirectory(tseCliToolFolder);

            var downloadTSE = !File.Exists(tseCliToolPath);
            if (!downloadTSE)
            {
                // File exists
                currentLocalVersion = await GetTSECLIVersionAsync(tseCliToolPath);
            }

            try
            {
                M3Log.Information(@"Checking for updates to TrilogySaveEditorCLI. The local version is " +
                                  currentLocalVersion);
                var client = new GitHubClient(new ProductHeaderValue(@"METweaksCore"));
                var releases = client.Repository.Release.GetAll(@"KarlitosVII", @"trilogy-save-editor-cli").Result;
                M3Log.Information(@"Fetched TrilogySaveEditorCLI releases from github...");

                Release latestReleaseWithApplicableAsset = null;
                if (releases.Any())
                {
                    //The release we want to check is always the latest, so [0]
                    foreach (Release r in releases)
                    {
                        if (!Settings.BetaMode && r.Prerelease)
                        {
                            // Beta only release
                            continue;
                        }

                        if (r.Assets.Count == 0)
                        {
                            // Release has no assets
                            continue;
                        }

                        Version releaseVersion = Version.Parse(r.Name);
                        if (releaseVersion > currentLocalVersion && getApplicableAssetForPlatform(r) != null)
                        {
                            ReleaseAsset applicableAsset = getApplicableAssetForPlatform(r);
                            if (releaseVersion > currentLocalVersion && applicableAsset != null)
                            {
                                M3Log.Information($@"New TrilogySaveEditorCLI update is available: {releaseVersion}");
                                latestReleaseWithApplicableAsset = r;
                                break;
                            }
                        }
                        else if (releaseVersion <= currentLocalVersion)
                        {
                            // M3Log.Information($@"No updates available for TrilogySaveEditorCLI"); // Logged below
                            break;
                        }
                    }

                    //No local version, no latest, but we have asset available somehwere
                    if (IsVersionZero(currentLocalVersion) && latestReleaseWithApplicableAsset == null)
                    {
                        M3Log.Information(@"TrilogySaveEditorCLI does not exist locally, and no applicable version can be found, force pulling latest from github");
                        latestReleaseWithApplicableAsset = releases.FirstOrDefault(x => getApplicableAssetForPlatform(x) != null);
                    }
                    else if (IsVersionZero(currentLocalVersion) && latestReleaseWithApplicableAsset == null)
                    {
                        //No local version, and we have no server version
                        M3Log.Error(@"Cannot pull a copy of TrilogySaveEditorCLI from server, could not find one with assets. Cannot perform save editor functions!");
                        return false;
                    }
                    else if (IsVersionZero(currentLocalVersion))
                    {
                        M3Log.Information(@"TrilogySaveEditorCLI does not exist locally. Pulling a copy from Github.");
                    }

                    if (latestReleaseWithApplicableAsset != null)
                    {
                        ReleaseAsset asset = getApplicableAssetForPlatform(latestReleaseWithApplicableAsset);
                        M3Log.Information(@"TrilogySaveEditorCLI update available: " +
                                          latestReleaseWithApplicableAsset.TagName);
                        //there's an update
                        var downloadClient = new WebClient();
                        downloadClient.Headers[@"Accept"] = @"application/vnd.github.v3+json";
                        downloadClient.Headers[@"user-agent"] =
                            MCoreFilesystem.AppDataFolderName; // Use the appdata folder name as the user agent
                        string downloadPath = Path.Combine(MCoreFilesystem.GetTempDirectory(), @"TSECLI_Update" + Path.GetExtension(asset.BrowserDownloadUrl));
                        DownloadHelper.DownloadFile(new Uri(asset.BrowserDownloadUrl), downloadPath,
                            (bytesReceived, totalBytes) =>
                            {
                                downloadProgressChanged?.Invoke(bytesReceived, totalBytes);
                            });

                        // Handle unzip code here.
                        statusMessageUpdate?.Invoke(M3L.GetString(M3L.string_extractingTSECLI));
                        if (Path.GetExtension(downloadPath) == @".7z")
                        {
                            var res = LZMA.ExtractSevenZipArchive(downloadPath, MCoreFilesystem.GetTempDirectory(),
                                true);
                            if (!res)
                            {
                                M3Log.Error(@"ERROR EXTRACTING 7z TSE-CLI!!");
                                return false;
                            }

                            // Copy into place. 
                            var sourceFile = Path.Combine(MCoreFilesystem.GetTempDirectory(), @"trilogy-save-editor-cli.exe");
                            if (File.Exists(tseCliToolPath))
                            {
                                File.Delete(tseCliToolPath);
                            }

                            File.Move(sourceFile, tseCliToolPath);
                        }
                        else if (Path.GetExtension(downloadPath) == @".zip")
                        {
                            var zf = ZipFile.OpenRead(downloadPath);
                            var zipEntry = zf.Entries.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FullName) == @"trilogy-save-editor-cli");
                            if (zipEntry != null)
                            {
                                if (File.Exists(tseCliToolPath))
                                {
                                    File.Delete(tseCliToolPath);
                                }

                                using (var fs = File.OpenWrite(tseCliToolPath))
                                {
                                    zipEntry.Open().CopyTo(fs);
                                }
                                M3Log.Information(
                                    $@"Updated TrilogySaveEditorCLI to version {GetTSECLIVersion(tseCliToolPath)}");
                            }
                            else
                            {
                                M3Log.Error(@"TrilogySaveEditorCLI file was not found in the archive!");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        //up to date
                        M3Log.Information(@"No updates for TrilogySaveEditorCLI are available");
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Exception(e, @"An error occurred running TrilogySaveEditorCLI updater: ");
                exceptionUpdating?.Invoke(e);
                return false;
            }

            // OK
            return true;
        }


        private static ReleaseAsset getApplicableAssetForPlatform(Release r)
        {
            foreach (var a in r.Assets)
            {
#if WINDOWS
                if (a.Name.StartsWith(@"trilogy-save-editor-cli-")) return a;
#elif LINUX
               // if (a.Name.StartsWith(@"TrilogySaveEditorCLI-Linux-v")) return a;
#elif MACOS
                // if (a.Name.StartsWith(@"TrilogySaveEditorCLI-macOS-v")) return a;
#endif
            }

            return null; //no asset for platform
        }

        private static bool IsVersionZero(Version currentLocalVersion)
        {
            return currentLocalVersion.Major == 0 && currentLocalVersion.Minor == 0;
        }

        private const string TSE_OUTPUT_VERSION_PREFIX = @"trilogy-save-editor-cli "; // This is the output of the text when using -V

        private static Version GetTSECLIVersion(string tseCliFilePath)
        {
            return GetTSECLIVersionAsync(tseCliFilePath).Result;
        }

        private static async Task<Version> GetTSECLIVersionAsync(string tseCliFilePath)
        {
            Version v = new Version(0, 0);
            var cmd = Cli.Wrap(tseCliFilePath).WithArguments(@"-V").WithValidation(CommandResultValidation.None);
            await foreach (var cmdEvent in cmd.ListenAsync())

            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        break;
                    case StandardOutputCommandEvent stdOut:
#if DEBUG
                        Debug.WriteLine(stdOut.Text);
#endif
                        if (IsVersionZero(v) && stdOut.Text.StartsWith(TSE_OUTPUT_VERSION_PREFIX))
                        {
                            var versionStr = stdOut.Text.Substring(TSE_OUTPUT_VERSION_PREFIX.Length);
                            v = Version.Parse(versionStr);
                        }
                        break;
                    case StandardErrorCommandEvent stdErr:
                        Debug.WriteLine(@"STDERR " + stdErr.Text);
                        M3Log.Fatal($@"{stdErr.Text}");
                        break;
                    case ExitedCommandEvent exited:
                        break;
                }
            }

            return v;
        }
    }
}
