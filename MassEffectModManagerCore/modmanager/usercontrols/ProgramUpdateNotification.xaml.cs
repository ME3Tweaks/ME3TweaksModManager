using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
using ByteSizeLib;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;
using SevenZip;
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
        public string UpdateMessage { get; set; } = M3L.GetString(M3L.string_anUpdateToME3TweaksModManagerIsAvailable);
        private string ChangelogLink;
        public ProgramUpdateNotification()
        {
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
                ProgressText = M3L.GetString(M3L.string_downloadingUpdate) + $@" {ByteSize.FromBytes(done).ToString("0.00")} / {ByteSize.FromBytes(total).ToString("0.00")}";
            }
            var downloadLinks = new string[] { PrimaryDownloadLink, BackupDownloadLink };
            string errorMessage = null;
            foreach (var downloadLink in downloadLinks)
            {
                var updateFile = OnlineContent.DownloadToMemory(downloadLink, pCallback);
                ProgressText = M3L.GetString(M3L.string_preparingToApplyUpdate);
                if (updateFile.errorMessage == null)
                {
                    ProgressIndeterminate = true;
                    ApplyUpdateFromStream(updateFile.result);
                    return; //do not loop.
                }
                else
                {
                    Log.Error(@"Error downloading update: " + updateFile.errorMessage);
                    Analytics.TrackEvent(@"Error downloading update", new Dictionary<string, string>() { { @"Error message", updateFile.errorMessage } });
                    errorMessage = updateFile.errorMessage;
                }
            }
            Application.Current.Dispatcher.Invoke(delegate
            {
                M3L.ShowDialog(Window.GetWindow(this), errorMessage, M3L.GetString(M3L.string_errorDownloadingUpdate), MessageBoxButton.OK, MessageBoxImage.Error);
                OnClosing(DataEventArgs.Empty);
            });
        }


        private void ApplyUpdateFromStream(MemoryStream updatearchive)
        {
            Log.Information(@"Extracting update from memory");
            ; SevenZipExtractor sve = new SevenZipExtractor(updatearchive);
            var outDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), M3L.GetString(M3L.string_update))).FullName;
            sve.ExtractArchive(outDirectory);
            var updaterExe = Path.Combine(outDirectory, @"ME3TweaksUpdater.exe");
            Utilities.ExtractInternalFile(@"MassEffectModManagerCore.updater.ME3TweaksUpdater.exe", updaterExe, true);
            var updateExecutablePath = Directory.GetFiles(outDirectory, @"ME3TweaksModManager.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (updateExecutablePath != null && File.Exists(updateExecutablePath) && File.Exists(updaterExe))
            {
                ProgressText = M3L.GetString(M3L.string_verifyingUpdate);
                var isTrusted = AuthenticodeHelper.IsTrusted(updateExecutablePath);
                if (!isTrusted)
                {
                    Log.Error(@"The update file is not signed. Update will be aborted.");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_unableToApplyUpdateNotSigned), M3L.GetString(M3L.string_errorApplyingUpdate), MessageBoxButton.OK, MessageBoxImage.Error);
                        OnClosing(DataEventArgs.Empty);
                    });
                    return;
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
                Log.Information($@"Running updater: {updaterExe} {args}");

                process = new Process();
                // Stop the process from opening a new window
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                // Setup executable and parameters
                process.StartInfo.FileName = updaterExe;
                process.StartInfo.Arguments = args;
                process.Start();
                Log.Information(@"Stopping Mod Manager to apply update");
                Log.CloseAndFlush();
                Environment.Exit(0);
            }
            else
            {
                Log.Error(@"Could not find ME3TweaksModManager.exe! Update will be aborted.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    M3L.ShowDialog(window, M3L.GetString(M3L.string_unableToApplyUpdateME3TweaksExeNotFound), M3L.GetString(M3L.string_errorApplyingUpdate), MessageBoxButton.OK, MessageBoxImage.Error);
                    OnClosing(DataEventArgs.Empty);
                });
            }
        }

        private bool CanStartUpdate()
        {
            return true;
        }

        private bool TaskRunning;

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
            if (e.Key == Key.Escape && !UpdateInProgress)
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
        }
    }
}
