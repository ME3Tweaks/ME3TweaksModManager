using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Serilog;
using SevenZip;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ProgramUpdateNotification.xaml
    /// </summary>
    public partial class ProgramUpdateNotification : UserControl, INotifyPropertyChanged
    {
        public event EventHandler<EventArgs> Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler<EventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        public string CurrentVersion => $"{App.AppVersion} Build {App.BuildNumber}";
        public string LatestVersion { get; set; }
        public string Changelog { get; set; }
        public string PrimaryDownloadLink { get; }
        public string BackupDownloadLink { get; }

        public ProgramUpdateNotification()
        {
            DataContext = this;
            LatestVersion = $"{App.ServerManifest["latest_version_hr"]} Build {App.ServerManifest["latest_build_number"]}";
            Changelog = GetPlainTextFromHtml(App.ServerManifest["release_notes"]);
            PrimaryDownloadLink = App.ServerManifest["download_link2"];
            BackupDownloadLink = App.ServerManifest["download_link"];
            LoadCommands();
            InitializeComponent();
        }

        public bool UpdateInProgress { get; set; }
        public long ProgressValue { get; set; }
        public long ProgressMax { get; set; } = 100;
        public string ProgressText { get; set; } = "Downloading update";
        public bool ProgressIndeterminate { get; private set; }
        public ICommand NotNowCommand { get; set; }
        public ICommand StartUpdateCommand { get; set; }
        private void LoadCommands()
        {
            NotNowCommand = new GenericCommand(CloseDialog, TaskNotRunning);
            StartUpdateCommand = new GenericCommand(StartUpdate, CanStartUpdate);
        }

        private void StartUpdate()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ProgramUpdater");
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
                ProgressText = $"Downloading update {ByteSize.FromBytes(done)} / {ByteSize.FromBytes(total)}";
            }
            var updateFile = OnlineContent.DownloadToMemory(PrimaryDownloadLink, pCallback);
            ProgressText = "Preparing to apply update";
            ProgressIndeterminate = true;
            if (updateFile.errorMessage == null) ApplyUpdateFromStream(updateFile.result);

        }

        private void ApplyUpdateFromStream(MemoryStream updatearchive)
        {
            SevenZipExtractor sve = new SevenZipExtractor(updatearchive);
            var outDirectory = Directory.CreateDirectory(Path.Combine(Utilities.GetTempPath(), "Update")).FullName;
            sve.ExtractArchive(outDirectory);

            var updateExecutablePath = Path.Combine(outDirectory, "ME3TweaksModManager.exe");
            if (File.Exists(updateExecutablePath))
            {

                ProgressText = "Restarting Mod Manager";
                Thread.Sleep(2000);

                string args = $"--update-dest-path \"{Directory.GetParent(System.Reflection.Assembly.GetEntryAssembly().Location)}\"";
                Log.Information("Running update process: " + updateExecutablePath + " " + args);
                Process.Start(updateExecutablePath, args);
                Log.Information("Stopping Mod Manager for updates.");
                Log.CloseAndFlush();
                Environment.Exit(0);
            }
            else
            {
                Log.Error("Could not find ME3TweaksModManager.exe! Update will be aborted.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(Window.GetWindow(this), "Unable to apply update: ME3TweaksModManager.exe was not found in the archive.","Error applying update",MessageBoxButton.OK,MessageBoxImage.Error);
                    OnClosing(EventArgs.Empty);
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
            OnClosing(EventArgs.Empty);
        }

        /// <summary>
        /// Strips HTML tags from a string. This is used to cleanse serverside response from Mod Manager Java.
        /// </summary>
        /// <param name="htmlString">string to parse</param>
        /// <returns>stripped and parsed string</returns>
        private string GetPlainTextFromHtml(string htmlString)
        {
            string htmlTagPattern = "<.*?>";
            var regexCss = new Regex("(\\<script(.+?)\\</script\\>)|(\\<style(.+?)\\</style\\>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            htmlString = regexCss.Replace(htmlString, string.Empty);
            htmlString = Regex.Replace(htmlString, htmlTagPattern, string.Empty);
            htmlString = Regex.Replace(htmlString, @"^\s+$[\r\n]*", "", RegexOptions.Multiline);
            htmlString = htmlString.Replace("&nbsp;", string.Empty);

            return htmlString;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
