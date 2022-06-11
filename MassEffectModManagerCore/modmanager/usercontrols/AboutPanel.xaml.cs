using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
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
            BuildDate = App.BuildDate;
        }

        private void SetupLibCredits()
        {
            var libraries = new List<LibraryCredit>
            {
                // M3 version
                new LibraryCredit(@"AdonisUI*", @"https://github.com/benruehl/adonis-ui", M3L.GetString(M3L.string_library_adonisui)),
                
                // M3 version
                new LibraryCredit(@"SevenZipSharp*", @"https://github.com/squid-box/SevenZipSharp", M3L.GetString(M3L.string_library_sevenzipsharp)),
                
                // M3 version
                new LibraryCredit(@"ini-parser*", @"https://github.com/rickyah/ini-parser", M3L.GetString(M3L.string_library_iniparser)),
                new LibraryCredit(@"Flurl", @"https://flurl.dev", M3L.GetString(M3L.string_library_flurl)),
                new LibraryCredit(@"Fody", @"https://github.com/Fody/Fody", M3L.GetString(M3L.string_library_fody)),
                new LibraryCredit(@"FontAwesome5", @"https://github.com/MartinTopfstedt/FontAwesome5", M3L.GetString(M3L.string_library_fontawesome)),
                new LibraryCredit(@"AppCenter", @"https://appcenter.ms", M3L.GetString(M3L.string_library_appcenter)),
                new LibraryCredit(@"MvvmValidation", @"https://github.com/pglazkov/MvvmValidation", M3L.GetString(M3L.string_library_mvvmvalidation)),
                new LibraryCredit(@"Newtonsoft.Json", @"https://www.newtonsoft.com/json", M3L.GetString(M3L.string_library_newtonsoft)),
                new LibraryCredit(@"Octokit", @"https://github.com/octokit/octokit.net", M3L.GetString(M3L.string_library_octokit)),
                new LibraryCredit(@"Serilog", @"https://serilog.net", M3L.GetString(M3L.string_library_serilog)),
                
                // M3 version
                new LibraryCredit(@"WPF Extended Toolkit*", @"https://github.com/dotnetprojects/WpfExtendedToolkit", M3L.GetString(M3L.string_library_wpftoolkitextended)),
                new LibraryCredit(@"LegendaryExplorerCore", @"https://github.com/ME3Tweaks/LegendaryExplorer", M3L.GetString(M3L.string_library_LegendaryExplorerCore)),
                new LibraryCredit(@"AuthenticodeExaminer", @"https://github.com/vcsjones/AuthenticodeExaminer", M3L.GetString(M3L.string_library_authenticodeexaminer)),
                new LibraryCredit(@"CliWrap", @"https://github.com/Tyrrrz/CliWrap", M3L.GetString(M3L.string_library_cliwrap)),
                new LibraryCredit(@"CommandLineParser", @"https://github.com/commandlineparser/commandline", M3L.GetString(M3L.string_library_commandlineparser)),
                
                // M3 version
                new LibraryCredit(@"ComputerInfo*", @"https://github.com/NickStrupat/ComputerInfo", M3L.GetString(M3L.string_library_computerinfo)),
                
                // M3 version
                new LibraryCredit(@"FluentNexus*", @"https://github.com/Pathoschild/FluentNexus", M3L.GetString(M3L.string_library_fluentnexus)),
                new LibraryCredit(@"RecyclableMemoryStream", @"https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream", M3L.GetString(M3L.string_library_recyclablememorystream)),
                new LibraryCredit(@"SSH.NET", @"https://github.com/sshnet/SSH.NET", M3L.GetString(M3L.string_library_sshnet)),
                new LibraryCredit(@"WatsonWebsocket", @"https://github.com/jchristn/WatsonWebsocket", M3L.GetString(M3L.string_library_watsonwebsocket)),
                new LibraryCredit(@"SingleInstanceCore", @"https://github.com/soheilkd/SingleInstanceCore", M3L.GetString(M3L.string_enablesSingleinstancingOfAnApplication)),
                new LibraryCredit(@"RoboSharp*", @"https://github.com/tjscience/RoboSharp", M3L.GetString(M3L.string_wrapperForRobocopyCommandInCSharp)),
            };


            Libraries.ReplaceAll(libraries.OrderBy(x => x.UIString));
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            M3Utilities.OpenWebpage(@"https://me3tweaks.com");
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
            InitializeComponent();
        }

        private void Navigate_Click(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri.AbsoluteUri == @"internal://minhook-license/")
            {
                new LicenseViewerWindow(new StreamReader(M3Utilities.GetResourceStream(@"ME3TweaksModManager.modmanager.licenses.minhook.txt")).ReadToEnd()).Show();
                return;
            }
            M3Utilities.OpenWebpage(e.Uri.AbsoluteUri);
        }

        private void ClosePanel(object sender, RoutedEventArgs e)
        {
            OnClosing(DataEventArgs.Empty);
        }
    }
}
