using System.Collections.Generic;
using System.Linq;
using MassEffectModManagerCore.ui;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for AboutPanel.xaml
    /// </summary>
    public partial class AboutPanel : MMBusyPanelBase
    {
        public bool TelemetryKeyAvailable => APIKeys.HasAppCenterKey;
        public string BuildDate { get; set; }
        public string NetVersion => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        public ObservableCollectionExtended<LibraryCredit> Libraries { get; } =
            new ObservableCollectionExtended<LibraryCredit>();

        public class LibraryCredit
        {
            public LibraryCredit(string libname, string url, string tooltip)
            {
                UIString = libname;
                URL = url;
                Tooltip = tooltip;
            }
            public string UIString { get; }
            public string URL { get; }
            public string Tooltip { get; }
        }

        public AboutPanel()
        {
            SetupLibCredits();
            InitializeComponent();
            BuildDate = App.BuildDate;
        }

        private void SetupLibCredits()
        {
            var libraries = new List<LibraryCredit>
            {
                new LibraryCredit(@"AdonisUI", @"https://github.com/benruehl/adonis-ui", "Library for dark/white themes in WPF\nME3Tweaks Mod Manager uses a customized version of this library"),
                new LibraryCredit(@"SevenZipSharp", @"https://github.com/squid-box/SevenZipSharp", "Compression and decompression library for 7z\nME3Tweaks Mod Manager uses a customized version of this library"),
                new LibraryCredit(@"ini-parser", @"https://github.com/rickyah/ini-parser", "Library for parsing ini files\nME3Tweaks Mod Manager uses a customized version of this library"),
                new LibraryCredit(@"Flurl", @"https://flurl.dev", "Modern asynchronous HTTP client library"),
                new LibraryCredit(@"Fody", @"https://github.com/Fody/Fody", "IL weaver system that removes huge amount of boilerplate code"),
                new LibraryCredit(@"FontAwesome.WPF", @"https://github.com/charri/Font-Awesome-WPF", "Vector icon pack"),
                new LibraryCredit(@"AppCenter", @"https://appcenter.ms", "Telemetry library for use with Microsoft Azure"),
                new LibraryCredit(@"MvvmValidation", @"https://github.com/pglazkov/MvvmValidation", "Lightweight validation framework"),
                new LibraryCredit(@"Newtonsoft.Json", @"https://www.newtonsoft.com/json", "Flexible JSON parser for C#"),
                new LibraryCredit(@"Octokit", @"https://github.com/octokit/octokit.net", "GitHub API client"),
                new LibraryCredit(@"Serilog", @"https://serilog.net", "Highly extensible logger"),
                new LibraryCredit(@"WPF Extended Toolkit", @"https://github.com/dotnetprojects/WpfExtendedToolkit", "Additional useful WPF controls\nME3Tweaks Mod Manager uses a customized version of this library"),
                new LibraryCredit(@"ME3ExplorerCore", @"https://github.com/ME3Tweaks/ME3Explorer", "Mass Effect trilogy modding toolset backend"),
                new LibraryCredit(@"AuthenticodeExaminer", @"https://github.com/vcsjones/AuthenticodeExaminer", @"Manages inspecting Authenticode file signatures"),
                new LibraryCredit(@"CliWrap", @"https://github.com/Tyrrrz/CliWrap", "Async wrapper for interacting with command line applications"),
                new LibraryCredit(@"CommandLineParser", @"https://github.com/commandlineparser/commandline", "Extensive command line argument parser"),
                new LibraryCredit(@"ComputerInfo", @"https://github.com/NickStrupat/ComputerInfo", "Small library for easily fetching CPU, OS, and memory information"),
                new LibraryCredit(@"FluentNexus", @"https://github.com/Pathoschild/FluentNexus", "Modern async HTTP client for the Nexus Mods API"),
                new LibraryCredit(@"RecyclableMemoryStream", @"https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream", "Library for a recyclable pool of memory buffers"),
                new LibraryCredit(@"SSH.NET", @"https://github.com/sshnet/SSH.NET", "Library for handling SSH connections"),
                new LibraryCredit(@"WatsonWebsocket", @"https://github.com/jchristn/WatsonWebsocket", "Library for handling websocket connections"),
                new LibraryCredit(@"NTFS-Streams", @"https://github.com/RichardD2/NTFS-Streams", @"Library for reading NTFS alternate data streams"),
            };


            Libraries.ReplaceAll(libraries.OrderBy(x=>x.UIString));
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            Utilities.OpenWebpage(@"https://me3tweaks.com");
        }

        private void About_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void OnPanelVisible()
        {
        }

        private void Navigate_Click(object sender, RequestNavigateEventArgs e)
        {
            Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }

        private void ClosePanel(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
