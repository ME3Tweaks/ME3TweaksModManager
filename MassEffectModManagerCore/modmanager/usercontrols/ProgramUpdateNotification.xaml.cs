using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using AuthenticodeExaminer;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;
using SevenZip;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;
using Path = System.IO.Path;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ProgramUpdateNotification.xaml
    /// </summary>
    public partial class ProgramUpdateNotification : MMBusyPanelBase
    {
        public string CurrentVersion => $@"{App.AppVersion} ({App.BuildDate}) Build {App.BuildNumber}";
        public string LatestVersion { get; set; }
        public string Changelog { get; set; }
        public string PrimaryDownloadLink { get; }
        public string BackupDownloadLink { get; }
        /// <summary>
        /// Mapping of MD5 patches to destination. Value is a list of mirrors we can use, preferring github first
        /// </summary>
        private Dictionary<string, List<(string downloadhash, string downloadLink, string timetamp)>> patchMappingSourceMd5ToLinks = new Dictionary<string, List<(string downloadhash, string downloadLink, string timetamp)>>();
        public string UpdateMessage { get; set; } = M3L.GetString(M3L.string_anUpdateToME3TweaksModManagerIsAvailable);
        private string ChangelogLink;
        public ProgramUpdateNotification(string localExecutableHash = null)
        {
            this.localExecutableHash = localExecutableHash;
            DataContext = this;
            try
            {
                // Latest vesrion
                if (App.ServerManifest.TryGetValue($@"latest_version_hr-{App.CurrentLanguage}",
                    out var localizedLatestVersion))
                {
                    LatestVersion = localizedLatestVersion;
                }
                else
                {
                    LatestVersion = App.ServerManifest[@"latest_version_hr"];
                }

                LatestVersion += $@" Build {App.ServerManifest[@"latest_build_number"]}"; //Do not localize this string.


                // Release notes
                if (App.ServerManifest.TryGetValue($@"release_notes-{App.CurrentLanguage}", out var localizedChangelog))
                {
                    Changelog = GetPlainTextFromHtml(localizedChangelog);
                }
                else
                {
                    Changelog = GetPlainTextFromHtml(App.ServerManifest[@"release_notes"]);
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"There was an exception parsing the version/changelog strings: {e.Message}");
            }

            PrimaryDownloadLink = App.ServerManifest[@"download_link2"];
            BackupDownloadLink = App.ServerManifest[@"download_link"];
            App.ServerManifest.TryGetValue(@"changelog_link", out ChangelogLink);

            M3Log.Information($@"Update available: {LatestVersion}. Prompting user");
            LoadCommands();
        }

        public bool UpdateInProgress { get; set; }
        public long ProgressValue { get; set; }
        public long ProgressMax { get; set; } = 100;
        public string ProgressText { get; set; } = M3L.GetString(M3L.string_downloadingUpdate);
        public bool ProgressIndeterminate { get; private set; }
        public ICommand NotNowCommand { get; set; }
        public ICommand StartUpdateCommand { get; set; }
        public ICommand ViewChangelogCommand { get; set; }

        private void LoadCommands()
        {
            NotNowCommand = new GenericCommand(CloseDialog);
            StartUpdateCommand = new GenericCommand(StartUpdate, CanStartUpdate);
            ViewChangelogCommand = new GenericCommand(ViewChangelog, CanViewChangelog);
        }

        private bool CanViewChangelog() => ChangelogLink != null;

        private void ViewChangelog()
        {
            M3Utilities.OpenWebpage(ChangelogLink);
        }

        private void StartUpdate()
        {
            M3Log.Information($@"Beginning update process");

            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ProgramUpdater");
            bw.DoWork += DownloadAndApplyUpdate;
            bw.RunWorkerAsync();
            UpdateInProgress = true;
        }

        private void DownloadAndApplyUpdate(object sender, DoWorkEventArgs e)
        {
            void pCallback(long done, long total)
            {
                ProgressIndeterminate = false;
                ProgressValue = done;
                ProgressMax = total;
                var hrDone = FileSize.FormatSize(done);
                var hrTotal = FileSize.FormatSize(total);
                ProgressText = M3L.GetString(M3L.string_downloadingUpdate) + $@" {hrDone} / {hrTotal}";
            }

            // PATCH UPDATE
            localExecutableHash ??= M3Utilities.CalculateMD5(App.ExecutableLocation);
            if (App.ServerManifest.TryGetValue(@"build_md5", out var destMd5))
            {
                foreach (var item in App.ServerManifest.Where(x => x.Key.StartsWith(@"upd-") || x.Key.StartsWith(@"gh_upd-")))
                {
                    var updateinfo = item.Key.Split(@"-");
                    if (updateinfo.Length >= 4)
                    {
                        var sourceHash = updateinfo[1];
                        var destHash = updateinfo[2];
                        var downloadHash = updateinfo[3];
                        var timestamp = updateinfo.Length > 4 ? updateinfo[4] : @"0";

                        if (localExecutableHash == sourceHash && destHash == destMd5)
                        {
                            if (!patchMappingSourceMd5ToLinks.TryGetValue(sourceHash, out var patchMappingList))
                            {
                                // ^ Don't bother adding items that will never be useful ^
                                patchMappingList = new List<(string downloadhash, string downloadLink, string timetamp)>();
                                patchMappingSourceMd5ToLinks[sourceHash] = patchMappingList;
                            }

                            if (item.Key.StartsWith(@"gh_upd-"))
                            {
                                // Insert at front.
                                patchMappingList.Insert(0, (downloadHash, item.Value, timestamp));
                            }
                            else
                            {
                                patchMappingList.Add((downloadHash, item.Value, timestamp));
                            }
                        }
                    }
                }

                if (patchMappingSourceMd5ToLinks.TryGetValue(localExecutableHash, out var downloadInfoMirrors))
                {
                    foreach (var downloadInfo in downloadInfoMirrors)
                    {
                        M3Log.Information($@"Downloading patch file {downloadInfo.downloadLink}");
                        var patchUpdate = M3OnlineContent.DownloadToMemory(downloadInfo.downloadLink, pCallback,
                            downloadInfo.downloadhash);
                        if (patchUpdate.errorMessage != null)
                        {
                            M3Log.Warning($@"Patch update download failed: {patchUpdate.errorMessage}");
                        }
                        else
                        {
                            M3Log.Information(@"Download OK: Building new executable");
                            var outPath = BuildUpdateFromPatch(patchUpdate.result, destMd5, downloadInfo.timetamp);
                            if (outPath != null)
                            {
                                ApplyUpdate(outPath, true);
                            }
                        }
                    }
                }
                else
                {
                    M3Log.Warning($@"There is no patch file available to transition our hash {localExecutableHash} to target hash {destMd5}");
                    M3Log.Information($@"We will perform a full update instead");
                }
            }


            // MAIN UPDATE

            var downloadLinks = new string[] { PrimaryDownloadLink, BackupDownloadLink };
            string errorMessage = null;
            foreach (var downloadLink in downloadLinks)
            {
                var updateFile = M3OnlineContent.DownloadToMemory(downloadLink, pCallback);
                ProgressText = M3L.GetString(M3L.string_preparingToApplyUpdate);
                if (updateFile.errorMessage == null)
                {
                    ProgressIndeterminate = true;
                    var outPath = ExtractFullUpdate(updateFile.result);
                    if (outPath != null)
                    {
                        ApplyUpdate(outPath, true);
                    }
                    return; //do not loop.
                }
                else
                {
                    M3Log.Error(@"Error downloading update: " + updateFile.errorMessage);
                    Analytics.TrackEvent(@"Error downloading update",
                        new Dictionary<string, string>() { { @"Error message", updateFile.errorMessage } });
                    errorMessage = updateFile.errorMessage;
                }
            }

            Application.Current.Dispatcher.Invoke(delegate
            {
                M3L.ShowDialog(Window.GetWindow(this), errorMessage, M3L.GetString(M3L.string_errorDownloadingUpdate), MessageBoxButton.OK, MessageBoxImage.Error);
                OnClosing(DataEventArgs.Empty);
            });
        }


        /// <summary>
        /// Extracts a full update from the memorystream download
        /// </summary>
        /// <param name="updatearchive"></param>
        /// <returns></returns>
        private string ExtractFullUpdate(MemoryStream updatearchive)
        {
            M3Log.Information(@"Extracting update from memory");
            SevenZipExtractor sve = new SevenZipExtractor(updatearchive);
            var outDirectory = Directory
                .CreateDirectory(Path.Combine(M3Filesystem.GetTempPath(), @"update")).FullName;
            sve.ExtractArchive(outDirectory);
            return outDirectory;
        }

        /// <summary>
        /// Builds the new update from a patch update
        /// </summary>
        /// <param name="patchStream"></param>
        /// <param name="expectedFinalHash"></param>
        /// <returns></returns>
        private string BuildUpdateFromPatch(MemoryStream patchStream, string expectedFinalHash, string fileTimestamp)
        {
            // patch stream is LZMA'd
            try
            {
                ProgressText = M3L.GetString(M3L.string_applyingPatch);
                ProgressIndeterminate = true;

                patchStream = new MemoryStream(LZMA.DecompressLZMAFile(patchStream.ToArray()));
                using var currentBuildStream = File.OpenRead(App.ExecutableLocation);
                //using var currentBuildStream = File.OpenRead(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\ME3TweaksModManager\Deployment\Staging\ME3TweaksModManager\ME3TweaksModManager.exe");

                MemoryStream outStream = new MemoryStream();
                JPatch.ApplyJPatch(currentBuildStream, patchStream, outStream);
                var calculatedHash = M3Utilities.CalculateMD5(outStream);
                if (calculatedHash == expectedFinalHash)
                {
                    M3Log.Information(@"Patch application successful: Writing new executable to disk");
                    var outDirectory = Directory.CreateDirectory(Path.Combine(M3Filesystem.GetTempPath(), @"update"))
                        .FullName;
                    var updateFile = Path.Combine(outDirectory, @"ME3TweaksModManager.exe");
                    outStream.WriteToFile(updateFile);

                    if (long.TryParse(fileTimestamp, out var buildDateLong) && buildDateLong > 0)
                    {
                        M3Log.Information(@"Updating timestamp on new executable to the original value");
                        try
                        {
                            File.SetLastWriteTimeUtc(updateFile, new DateTime(buildDateLong));
                        }
                        catch (Exception ex)
                        {
                            M3Log.Error($@"Could not set executable date: {ex.Message}");
                        }
                    }
                    M3Log.Information(@"New executable patching complete");
                    return outDirectory;
                }
                else
                {
                    M3Log.Error($@"Patch application failed. The resulting hash was wrong. Expected {expectedFinalHash}, got {calculatedHash}");
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error applying patch update: {e.Message}");
            }

            return null;
        }


        private bool ApplyUpdate(string updateDirectory, bool closeOnBadSignature = true)
        {
            var updateSwapperExecutable = Path.Combine(updateDirectory, @"ME3TweaksUpdater.exe");
            M3Utilities.ExtractInternalFile(@"ME3TweaksModManager.updater.ME3TweaksUpdater.exe", updateSwapperExecutable, true);
            var updateExecutablePath = Directory.GetFiles(updateDirectory, @"ME3TweaksModManager.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (updateExecutablePath != null && File.Exists(updateExecutablePath) && File.Exists(updateSwapperExecutable))
            {
                ProgressText = M3L.GetString(M3L.string_verifyingUpdate);
                var authenticodeInspector = new FileInspector(updateExecutablePath);
                var validationResult = authenticodeInspector.Validate();
                if (validationResult != SignatureCheckResult.Valid)
                {
                    M3Log.Error($@"The update file is not signed ({validationResult.ToString()}).");
                    if (closeOnBadSignature)
                    {
                        M3Log.Error(@"Update will be aborted.");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            M3L.ShowDialog(Window.GetWindow(this),
                                M3L.GetString(M3L.string_unableToApplyUpdateNotSigned),
                                M3L.GetString(M3L.string_errorApplyingUpdate), MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            OnClosing(DataEventArgs.Empty);
                        });
                    }

                    return false;
                }
                else
                {
                    // Check it's signed by not just anyone
                    var signer = authenticodeInspector.GetSignatures().FirstOrDefault()?.SigningCertificate?.GetNameInfo(X509NameType.SimpleName, false);
                    if (signer != null && (signer != @"Michael Perez" && signer != @"ME3Tweaks"))
                    {
                        M3Log.Error($@"This update is signed, but not by ME3Tweaks. This update is being rejected. The signer name is {signer}");
                        if (closeOnBadSignature)
                        {
                            M3Log.Error(@"Update will be aborted.");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                M3L.ShowDialog(Window.GetWindow(this),
                                    M3L.GetString(M3L.string_theDownloadedUpdateFileIsNotSignedByME3TweaksAndIsNotTrusted),
                                    M3L.GetString(M3L.string_errorApplyingUpdate), MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                OnClosing(DataEventArgs.Empty);
                            });
                        }
                        return false;
                    }
                }

                ProgressText = M3L.GetString(M3L.string_applyingUpdate);

                string args = @"--update-boot";
                M3Log.Information($@"Running new mod manager in update mode: {updateExecutablePath} {args}");

                Process process = new Process();
                // Stop the process from opening a new window
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                // Setup executable and parameters
                process.StartInfo.FileName = updateExecutablePath;
                process.StartInfo.Arguments = args;
                process.Start();
                process.WaitForExit();

                ProgressText = M3L.GetString(M3L.string_restartingModManager);
                Thread.Sleep(2000);
                args = $"--update-from {App.BuildNumber} --update-source-path \"{updateExecutablePath}\" --update-dest-path \"{App.ExecutableLocation}\""; //Do not localize
                M3Log.Information($@"Running updater: {updateSwapperExecutable} {args}");

                process = new Process();
                // Stop the process from opening a new window
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                // Setup executable and parameters
                process.StartInfo.FileName = updateSwapperExecutable;
                process.StartInfo.Arguments = args;
                process.Start();
                M3Log.Information(@"Stopping Mod Manager to apply update");
                Log.CloseAndFlush();
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                return true;
            }
            else
            {
                M3Log.Error(@"Could not find ME3TweaksModManager.exe or ME3TweaksUpdater.exe! Update will be aborted.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_unableToApplyUpdateME3TweaksExeNotFound), M3L.GetString(M3L.string_errorApplyingUpdate), MessageBoxButton.OK, MessageBoxImage.Error);
                    OnClosing(DataEventArgs.Empty);
                });
            }

            return false;
        }

        private bool CanStartUpdate()
        {
            return true;
        }

        private string localExecutableHash;

        private void CloseDialog()
        {
            M3Log.Warning(@"Update was declined");
            OnClosing(DataEventArgs.Empty);
        }

        /// <summary>
        /// Strips HTML tags from a string. This is used to cleanse serverside response from Mod Manager Java.
        /// </summary>
        /// <param name="htmlString">string to parse</param>
        /// <returns>stripped and parsed string</returns>
        private string GetPlainTextFromHtml(string htmlString)
        {
            htmlString = htmlString.Replace(@"<br>", Environment.NewLine);
            string htmlTagPattern = @"<.*?>";
            var regexCss = new Regex(@"(\<script(.+?)\</script\>)|(\<style(.+?)\</style\>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            htmlString = regexCss.Replace(htmlString, string.Empty);
            htmlString = Regex.Replace(htmlString, htmlTagPattern, string.Empty);
            //htmlString = Regex.Replace(htmlString, @"^\s+$[\r\n]*", "", RegexOptions.Multiline);
            htmlString = htmlString.Replace(@"&nbsp;", string.Empty);

            return htmlString;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            // We don't accept ESCAPE here
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
        }
    }
}
