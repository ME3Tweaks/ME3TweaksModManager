using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MassEffectModManagerCore.ui;

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
        public ProgramUpdateNotification()
        {
            DataContext = this;
            LatestVersion = $"{App.ServerManifest["latest_version_hr"]} Build {App.ServerManifest["latest_build_number"]}";
            Changelog = GetPlainTextFromHtml(App.ServerManifest["release_notes"]);
            LoadCommands();
            InitializeComponent();
        }

        public ICommand NotNowCommand { get; set; }
        private void LoadCommands()
        {
            NotNowCommand = new GenericCommand(CloseDialog, TaskNotRunning);
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
