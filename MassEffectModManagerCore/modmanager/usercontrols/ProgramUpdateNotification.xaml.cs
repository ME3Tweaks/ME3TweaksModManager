using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AuthenticodeExaminer;
using ByteSizeLib;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;
using SevenZip;
using SevenZipHelper;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ProgramUpdateNotification.xaml
    /// </summary>
    public partial class ProgramUpdateNotification : MMBusyPanelBase
    {
        public string CurrentVersion => $@"{App.AppVersion} Build {App.BuildNumber}";
        public string LatestVersion { get; set; }
        public string Changelog { get; set; }
        public string PrimaryDownloadLink { get; }
        public string BackupDownloadLink { get; }
        private Dictionary<string, (string downloadhash, string downloadLink)> patchMappingMd5ToLink = new Dictionary<string, (string downloadhash, string downloadLink)>();
        public string UpdateMessage { get; set; } = M3L.GetString(M3L.string_anUpdateToME3TweaksModManagerIsAvailable);
        private string ChangelogLink;
        public ProgramUpdateNotification(string localExecutableHash = null)
        {
            this.localExecutableHash = localExecutableHash;
            DataContext = this;
            LatestVersion = $@"{App.ServerManifest[@"latest_version_hr"]} Build {App.ServerManifest[@"latest_build_number"]}"; //Do not localize this string.
            Changelog = GetPlainTextFromHtml(App.ServerManifest[@"release_notes"]);
            PrimaryDownloadLink = App.ServerManifest[@"download_link2"];
            BackupDownloadLink = App.ServerManifest[@"download_link"];
            App.ServerManifest.TryGetValue(@"changelog_link", out ChangelogLink);


            LoadCommands();
            InitializeComponent();
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
            NotNowCommand = new GenericCommand(CloseDialog, TaskNotRunning);
            StartUpdateCommand = new GenericCommand(StartUpdate, CanStartUpdate);
            ViewChangelogCommand = new GenericCommand(ViewChangelog, CanViewChangelog);
        }

        private bool CanViewChangelog() => ChangelogLink != null;

        private void ViewChangelog()
        {
            Utilities.OpenWebpage(ChangelogLink);
        }

        private void StartUpdate()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ProgramUpdater");
            bw.DoWork += DownloadAndApplyUpdate;
            bw.RunWorkerAsync();
            UpdateInProgress = true;
        }

        private void DownloadAndApplyUpdate(object sender, DoWorkEventArgs e)
        {
            void pCallback(long done, long total)
            {
                ProgressValue = done;
                ProgressMax = total;
                var hrDone = ByteSize.FromBytes(done).ToString(@"0.00");
                var hrTotal = ByteSize.FromBytes(total).ToString(@"0.00");
                ProgressText = M3L.GetString(M3L.string_downloadingUpdate) + $@" {hrDone} / {hrTotal}";
            }

            // PATCH UPDATE
            if (App.ServerManifest.TryGetValue(@"build_md5", out var md5))
            {
                foreach (var item in App.ServerManifest.Where(x => x.Key.StartsWith(@"upd-")))
                {
                    var updateinfo = item.Key.Split(@"-");
                    if (updateinfo.Length == 4)
                    {
                        var sourceHash = updateinfo[1];
                        var destHash = updateinfo[2];
                        var downloadHash = updateinfo[3];
                        if (destHash == md5)
                        {
                            patchMappingMd5ToLink[sourceHash] = (downloadHash, item.Value);
                        }
                    }
                }

                //var localmd5 = localExecutableHash ?? Utilities.CalculateMD5(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Staging\ME3TweaksModManager\ME3TweaksModManager.exe");
                var localmd5 = localExecutableHash ?? Utilities.CalculateMD5(App.ExecutableLocation);
                if (patchMappingMd5ToLink.TryGetValue(localmd5, out var downloadInfo))
                {
                    var patchUpdate = OnlineContent.DownloadToMemory(downloadInfo.downloadLink, pCallback, downloadInfo.downloadhash);
                    if (patchUpdate.errorMessage != null)
                    {
                        Log.Warning($@"Patch update download failed: {patchUpdate.errorMessage}");
                    }
                    else
                    {
                        var outPath = BuildUpdateFromPatch(patchUpdate.result, md5);
                        if (outPath != null)
                        {
                            ApplyUpdate(outPath, true);
                        }
                    }
                }
            }


            // MAIN UPDATE

            var downloadLinks = new string[] { PrimaryDownloadLink, BackupDownloadLink };
            string errorMessage = null;
            foreach (var downloadLink in downloadLinks)
            {
                var updateFile = OnlineContent.DownloadToMemory(downloadLink, pCallback);
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
                    Log.Error(@"Error downloading update: " + updateFile.errorMessage);
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
            Log.Information(@"Extracting update from memory");
            SevenZipExtractor sve = new SevenZipExtractor(updatearchive);
            var outDirectory = Directory
                .CreateDirectory(Path.Combine(Utilities.GetTempPath(), @"update")).FullName;
            sve.ExtractArchive(outDirectory);
            return outDirectory;
        }

        /// <summary>
        /// Builds the new update from a patch update
        /// </summary>
        /// <param name="patchStream"></param>
        /// <param name="expectedFinalHash"></param>
        /// <returns></returns>
        private string BuildUpdateFromPatch(MemoryStream patchStream, string expectedFinalHash)
        {
            // patch stream is LZMA'd
            try
            {
                ProgressText = M3L.GetString(M3L.string_applyingPatch);
                ProgressIndeterminate = true;

                patchStream = new MemoryStream(LZMA.DecompressLZMAFile(patchStream.ToArray()));
                using var currentBuildStream = File.OpenRead(App.ExecutableLocation);
                //using var currentBuildStream = File.OpenRead(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Staging\ME3TweaksModManager\ME3TweaksModManager.exe");

                MemoryStream outStream = new MemoryStream();
                JPatch.ApplyJPatch(currentBuildStream, patchStream, outStream);
                var calculatedHash = Utilities.CalculateMD5(outStream);
                if (calculatedHash == expectedFinalHash)
                {
                    var outDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), @"update"))
                        .FullName;
                    var updateFile = Path.Combine(outDirectory, @"ME3TweaksModManager.exe");
                    outStream.WriteToFile(updateFile);

                    if (App.ServerManifest.TryGetValue(@"build_timestamp", out var buildDateStr) && long.TryParse(buildDateStr, out var buildDateLong))
                    {
                        try
                        {
                            File.SetLastWriteTimeUtc(updateFile, new DateTime(buildDateLong));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($@"Could not set executable date: {ex.Message}");
                        }

                    }
                    return outDirectory;
                }
                else
                {
                    Log.Error($@"Patch application failed. The resulting hash was wrong. Expected {expectedFinalHash}, got {calculatedHash}");
                }
            }
            catch (Exception e)
            {
                Log.Error($@"Error applying patch update: {e.Message}");
            }

            return null;
        }


        private bool ApplyUpdate(string updateDirectory, bool closeOnBadSignature = true)
        {
            var updateSwapperExecutable = Path.Combine(updateDirectory, @"ME3TweaksUpdater.exe");
            Utilities.ExtractInternalFile(@"MassEffectModManagerCore.updater.ME3TweaksUpdater.exe", updateSwapperExecutable, true);
            var updateExecutablePath = Directory.GetFiles(updateDirectory, @"ME3TweaksModManager.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (updateExecutablePath != null && File.Exists(updateExecutablePath) && File.Exists(updateSwapperExecutable))
            {
                ProgressText = M3L.GetString(M3L.string_verifyingUpdate);
                var authenticodeInspector = new FileInspector(updateExecutablePath);
                var validationResult = authenticodeInspector.Validate();
                if (validationResult != SignatureCheckResult.Valid)
                {
                    Log.Error($@"The update file is not signed ({validationResult.ToString()}).");
                    if (closeOnBadSignature)
                    {
                        Log.Error(@"Update will be aborted.");
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
                        Log.Error($@"This update is signed, but not by ME3Tweaks. This update is being rejected. The signer name is {signer}");
                        if (closeOnBadSignature)
                        {
                            Log.Error(@"Update will be aborted.");
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
                Log.Information($@"Running new mod manager in update mode: {updateExecutablePath} {args}");

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
                Log.Information($@"Running updater: {updateSwapperExecutable} {args}");

                process = new Process();
                // Stop the process from opening a new window
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                // Setup executable and parameters
                process.StartInfo.FileName = updateSwapperExecutable;
                process.StartInfo.Arguments = args;
                process.Start();
                Log.Information(@"Stopping Mod Manager to apply update");
                Log.CloseAndFlush();
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                return true;
            }
            else
            {
                Log.Error(@"Could not find ME3TweaksModManager.exe or ME3TweaksUpdater.exe! Update will be aborted.");
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

        private bool TaskRunning;
        private string localExecutableHash;

        private bool TaskNotRunning() => !TaskRunning;

        private void CloseDialog()
        {
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
        }
    }
}
