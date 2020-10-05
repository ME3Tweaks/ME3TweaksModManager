using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using CommandLine;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System.Runtime.InteropServices;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using ME3Explorer.Packages;
using MassEffectModManagerCore.modmanager.usercontrols;
using AuthenticodeExaminer;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.windows;
using Microsoft.AppCenter; // do not remove

namespace MassEffectModManagerCore
{
    [Localizable(false)]
    public partial class App : Application
    {
        public static bool AppDataExistedAtBoot = Directory.Exists(Utilities.GetAppDataFolder(false)); //alphabetically this must come first in App!
        /// <summary>
        /// Registry key for legacy Mass Effect 3 Mod Manager. Used to store the ME3 backup directory
        /// </summary>
        internal const string REGISTRY_KEY_ME3CMM = @"HKEY_CURRENT_USER\Software\Mass Effect 3 Mod Manager";

        /// <summary>
        /// ALOT Addon Registry Key, used for ME1 and ME2 backups
        /// </summary>
        internal const string BACKUP_REGISTRY_KEY = @"HKEY_CURRENT_USER\Software\ALOTAddon"; //Shared. Do not change

        public static string LogDir = Path.Combine(Utilities.GetAppDataFolder(), "logs");
        private static bool POST_STARTUP = false;
        public const string DISCORD_INVITE_LINK = "https://discord.gg/s8HA6dc";
        public static bool UpgradingFromME3CMM;
        public static Visibility IsDebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility DebugOnlyVisibility
        {
#if DEBUG
            get { return Visibility.Visible; }
#else
            get { return Visibility.Collapsed; }
#endif
        }

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif
        /// <summary>
        /// The highest version of ModDesc that this version of Mod Manager can support.
        /// </summary>
        public const double HighestSupportedModDesc = 6.1;

        //Windows 8.1 Update 1
        public static readonly Version MIN_SUPPORTED_OS = new Version("6.3.9600");

        internal static readonly string[] SupportedOperatingSystemVersions =
        {
            "Windows 8.1",
            "Windows 10 (not EOL versions)"
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        public static string BuildDate;
        public static bool IsSigned;

        public App() : base()
        {
            ExecutableLocation = Process.GetCurrentProcess().MainModule.FileName;
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.sevenzipwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "sevenzipwrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.lzo2wrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "lzo2wrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.zlibwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "zlibwrapper.dll"), false);
            SetDllDirectory(Utilities.GetDllDirectory());

            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            //var sourceStream = File.OpenRead(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Staging\ME3TweaksModManager\ME3TweaksModManager.exe");
            //var patchStream = File.OpenRead(@"C:\Users\Mgamerz\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\Deployment\Staging\ME3TweaksModManager\patch");
            //var outStream = new MemoryStream();

            //JPatch.ApplyJPatch(sourceStream, patchStream, outStream);
            //var outHash = Utilities.CalculateMD5(outStream);

            var settingsExist = File.Exists(Settings.SettingsPath); //for init language

            try
            {
                string exeFolder = Directory.GetParent(ExecutableLocation).ToString();
                try
                {
                    LogCollector.CreateLogger();
                }
                catch (Exception e)
                {
                    //Unable to create logger...!

                }

                string[] args = Environment.GetCommandLineArgs();
                //Parsed<Options> parsedCommandLineArgs = null;

                #region Command line

                if (args.Length > 1)
                {
                    var result = Parser.Default.ParseArguments<Options>(args);
                    if (result is Parsed<Options> parsedCommandLineArgs)
                    {
                        //Parsing completed
                        if (parsedCommandLineArgs.Value.UpdateBoot)
                        {
                            //Update unpacked and process was run.
                            //Extract ME3TweaksUpdater.exe to ensure we have newest update executable in case we need to do update hotfixes

                            var updaterExe = Path.Combine(Path.GetDirectoryName(ExecutableLocation), @"ME3TweaksUpdater.exe");
                            if (File.Exists(updaterExe))
                            {
                                //write updated exe
                                Utilities.ExtractInternalFile(@"MassEffectModManagerCore.updater.ME3TweaksUpdater.exe", updaterExe, true);
                            }

                            if (!File.Exists(updaterExe))
                            {
                                // Error like this has no need being localized
                                M3L.ShowDialog(null, $@"Updater missing! The swapper executable should be located at: {updaterExe}. Please report this to ME3Tweaks.", @"Error updating");
                            }

                            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                            return;
                        }

                        if (parsedCommandLineArgs.Value.UpdateFromBuild != 0)
                        {
                            App.UpdatedFrom = parsedCommandLineArgs.Value.UpdateFromBuild;
                        }

                        if (parsedCommandLineArgs.Value.BootingNewUpdate)
                        {
                            App.BootingUpdate = true;
                        }

                        UpgradingFromME3CMM = parsedCommandLineArgs.Value.UpgradingFromME3CMM;
                    }
                    else
                    {
                        Log.Error("Could not parse command line arguments! Args: " + string.Join(' ', args));
                    }
                }

                #endregion


                this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
                ToolTipService.ShowDurationProperty.OverrideMetadata(
                    typeof(DependencyObject), new FrameworkPropertyMetadata(20000));


                Log.Information("===========================================================================");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutableLocation);
                string version = fvi.FileVersion;
                Log.Information("ME3Tweaks Mod Manager " + version);
                Log.Information("Application boot: " + DateTime.UtcNow);
                Log.Information("Running as " + Environment.UserName);
                Log.Information("Executable location: " + ExecutableLocation);
                Log.Information("Operating system: " + RuntimeInformation.OSDescription);
                //Get build date
                var info = new FileInspector(App.ExecutableLocation);
                var signTime = info.GetSignatures().FirstOrDefault()?.TimestampSignatures.FirstOrDefault()?.TimestampDateTime?.UtcDateTime;

                if (signTime != null)
                {
                    IsSigned = true;
                    BuildDate = signTime.Value.ToString(@"MMMM dd, yyyy");

                    var signer = info.GetSignatures().FirstOrDefault()?.SigningCertificate?.GetNameInfo(X509NameType.SimpleName, false);
                    if (signer != null && (signer == @"Michael Perez" || signer == @"ME3Tweaks"))
                    {
                        Log.Information(@"Build signed by ME3Tweaks. Build date: " + BuildDate);
                    }
                    else
                    {
                        Log.Warning(@"Build signed, but not by ME3Tweaks.");
                    }
                }
                else
                {
                    //needs localized later.
                    BuildDate = "WARNING: This build is not signed by ME3Tweaks";
#if !DEBUG
                    Log.Warning("This build is not signed by ME3Tweaks. This may not be an official build.");
#endif
                }

                #region Update mode boot



                #endregion

                System.Windows.Controls.ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control),
                    new FrameworkPropertyMetadata(true));

                try
                {
                    var avs = Utilities.GetListOfInstalledAV();
                    Log.Information("Detected the following antivirus products:");
                    foreach (var av in avs)
                    {
                        Log.Information(" - " + av);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Unable to get the list of installed antivirus products: " + e.Message);
                }

                Log.Information("The following backup paths are listed in the registry:");
                Log.Information("Mass Effect ======");
                Log.Information(BackupService.GetGameBackupPath(Mod.MEGame.ME1, true, true));
                Log.Information("Mass Effect 2 ====");
                Log.Information(BackupService.GetGameBackupPath(Mod.MEGame.ME2, true, true));
                Log.Information("Mass Effect 3 ====");
                Log.Information(BackupService.GetGameBackupPath(Mod.MEGame.ME3, true, true));

                //Build 104 changed location of settings from AppData to ProgramData.
                if (!AppDataExistedAtBoot)
                {
                    //First time booting something that uses ProgramData
                    //see if data exists in AppData
                    var oldDir = Utilities.GetPre104DataFolder();
                    if (oldDir != null)
                    {
                        //Exists. We should migrate it
                        try
                        {
                            CopyDir.CopyAll_ProgressBar(new DirectoryInfo(oldDir), new DirectoryInfo(Utilities.GetAppDataFolder()), aboutToCopyCallback: (a) =>
                            {
                                Log.Information("Migrating file from AppData to ProgramData: " + a);
                                return true;
                            });

                            Log.Information("Deleting old data directory: " + oldDir);
                            Utilities.DeleteFilesAndFoldersRecursively(oldDir);
                            Log.Information("Migration from pre 104 settings to 104+ settings completed");
                        }
                        catch (Exception e)
                        {
                            Log.Error("Unable to migrate old settings: " + e.Message);
                        }
                    }
                }


                Log.Information("Loading settings");
                Settings.Load();

                if (!Settings.EnableTelemetry)
                {
                    Log.Warning("Telemetry is disabled :(");
                }
                else
                {
                    InitAppCenter();
                }

                if (Settings.Language != "int" && SupportedLanguages.Contains(Settings.Language))
                {
                    InitialLanguage = Settings.Language;
                }
                if (!settingsExist)
                {
                    //first boot?
                    var currentCultureLang = CultureInfo.InstalledUICulture.Name;
                    if (currentCultureLang.StartsWith("de")) InitialLanguage = Settings.Language = "deu";
                    if (currentCultureLang.StartsWith("ru")) InitialLanguage = Settings.Language = "rus";
                    if (currentCultureLang.StartsWith("pl")) InitialLanguage = Settings.Language = "pol";
                    if (currentCultureLang.StartsWith("pt")) InitialLanguage = Settings.Language = "bra";
                    Analytics.TrackEvent("Auto set startup language", new Dictionary<string, string>() { { @"Language", InitialLanguage } });
                    Log.Information(@"This is a first boot. The system language code is " + currentCultureLang);
                }

                Log.Information("Deleting temp files (if any)");
                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(Utilities.GetTempPath());
                }
                catch (Exception e)
                {
                    Log.Error($"Unable to delete temporary files directory {Utilities.GetTempPath()}: {e.Message}");
                }

                Log.Information("Initializing package handlers");
                MEPackageHandler.Initialize();

                Log.Information("Ensuring default ASI assets are present");
                ASIManager.ExtractDefaultASIResources();

                collectHardwareInfo();

                Log.Information("Mod Manager pre-UI startup has completed. The UI will now load.");
                Log.Information("If the UI fails to start, it may be that a third party tool is injecting itself into Mod Manager, such as RivaTuner or Afterburner, and is corrupting the process.");
                POST_STARTUP = true; //this could be earlier but i'm not sure when crash handler actually is used, doesn't seem to be after setting it...
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw;
            }
        }

        private void InitAppCenter()
        {

#if !DEBUG

            if (APIKeys.HasAppCenterKey)
            {
                Crashes.GetErrorAttachments = (ErrorReport report) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    // Attach some text.
                    string errorMessage = "ME3Tweaks Mod Manager has crashed! This is the exception that caused the crash:\n" + report.StackTrace;
                    Log.Fatal(errorMessage);
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < ByteSizeLib.ByteSize.BytesInMegaByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "crashlog.txt"));
                    }
                    else
                    {
                        //Compress log
                        var compressedLog = SevenZipHelper.LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(log));
                        attachments.Add(ErrorAttachmentLog.AttachmentWithBinary(compressedLog, "crashlog.txt.lzma", "application/x-lzma"));
                    }

                    // Attach binary data.
                    //var fakeImage = System.Text.Encoding.Default.GetBytes("Fake image");
                    //ErrorAttachmentLog binaryLog = ErrorAttachmentLog.AttachmentWithBinary(fakeImage, "ic_launcher.jpeg", "image/jpeg");

                    return attachments;
                };
                AppCenter.Start(APIKeys.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
#else
            if (!APIKeys.HasAppCenterKey)
            {
                Debug.WriteLine(" >>> This build is missing an API key for AppCenter!");
            }
            else
            {
                Debug.WriteLine("This build has an API key for AppCenter");
            }
#endif
        }

        private void collectHardwareInfo()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"HardwareInventory");
            nbw.DoWork += (a, b) =>
            {
                var data = new Dictionary<string, string>();
                try
                {
                    ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                    foreach (ManagementObject moProcessor in mosProcessor.Get())
                    {
                        // For seeing AMD vs Intel (for ME1 lighting)
                        if (moProcessor["name"] != null)
                        {
                            data[@"Processor"] = moProcessor["name"].ToString();
                            IsRunningOnAMD = data[@"Processor"].Contains("AMD");
                        }
                    }

                    data[@"BetaMode"] = Settings.BetaMode.ToString();
                    data[@"DeveloperMode"] = Settings.DeveloperMode.ToString();

                    if (Settings.EnableTelemetry)
                    {
                        Analytics.TrackEvent(@"Hardware Info", data);
                    }
                }
                catch //(Exception e)
                {

                }
            };
            nbw.RunWorkerAsync();
        }

        public static bool IsRunningOnAMD;

        public static string[] SupportedLanguages = { "int", "pol", "rus", "deu", "fra", "bra", "esn" };

        public static int BuildNumber = Assembly.GetEntryAssembly().GetName().Version.Revision;

        /// <summary>
        /// Accesses the third party identification server. Key is the game enum as a string, results are dictionary of DLCName => Info.
        /// </summary>
        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> ThirdPartyIdentificationService;

        internal static string BugReportURL = "https://github.com/ME3Tweaks/ME3TweaksModManager/issues";
        public static Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>> ThirdPartyImportingService;
        public static Dictionary<string, CaseInsensitiveDictionary<List<BasegameFileIdentificationService.BasegameCloudDBFile>>> BasegameFileIdentificationService;
        public static bool BootingUpdate;
        public static int UpdatedFrom = 0;
        public static string InitialLanguage = "int";
        internal static Dictionary<string, List<string>> TipsService;
        internal static string CurrentLanguage = InitialLanguage;

        private static bool? _allowCompressingPackageOnImport;
        /// <summary>
        /// Allow package compression when importing a mod. This is controlled by the server manifest and currently defaults to false.
        /// </summary>
        public static bool AllowCompressingPackagesOnImport
        {
            get
            {
                if (_allowCompressingPackageOnImport != null) return _allowCompressingPackageOnImport.Value;
                if (ServerManifest != null)
                {
                    if (ServerManifest.TryGetValue(@"allowcompressingpackagesonimport", out var acpoiStr) && bool.TryParse(acpoiStr, out var acpoiVal))
                    {
                        _allowCompressingPackageOnImport = acpoiVal;
                    }
                }

                return false;
            }
        }

        public static string AppVersion
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += "." + assemblyVersion.Build;
                }

                return version;
            }
        }

        public static string AppVersionAbout
        {
            get
            {
                string version = AppVersion;
#if DEBUG
                version += " DEBUG";
#elif PRERELEASE
                 //version += " PRERELEASE";
#endif
                return $"{version}, Build {BuildNumber}";
            }
        }

        public static string AppVersionHR
        {
            get
            {
                string version = AppVersion;
#if DEBUG
                version += " DEBUG";
#elif PRERELEASE
                 //version += " PRERELEASE";
#endif
                return $"ME3Tweaks Mod Manager {version} (Build {BuildNumber})";
            }
        }

        /// <summary>
        /// The executable location for this application
        /// </summary>
        public static string ExecutableLocation { get; private set; }

        #region Server Manifest
        #region Static Property Changed

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion
        private static Dictionary<string, string> _serverManifest;
        /// <summary>
        /// The online server manifest that was fetched at boot. If null, the manifest was not fetched
        /// </summary>
        public static Dictionary<string, string> ServerManifest
        {
            get => _serverManifest;
            set => SetProperty(ref _serverManifest, value);
        }

        #endregion

        public static List<IntroTutorial.TutorialStep> TutorialService { get; set; } = new List<IntroTutorial.TutorialStep>(); //in case it takes long time to load

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// Note! This method is called AFTER it is called from the Crashes library.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (!Crashes.IsEnabledAsync().Result)
            {
                string errorMessage = "ME3Tweaks Mod Manager has crashed! This is the exception that caused the crash:\n" + FlattenException(e.Exception);
                Log.Fatal(errorMessage);
            }
        }

        /// <summary>
        /// Called when a fatal crash occurs. Only does something if startup has not completed.
        /// </summary>
        /// <param name="e">The fatal exception.</param>
        public static void OnFatalCrash(Exception e)
        {
            if (!POST_STARTUP)
            {
                Log.Fatal("ME3Tweaks Mod Manager has encountered a fatal startup crash:\n" + FlattenException(e));
            }
        }

        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <param name="exception">Exception to flatten</param>
        /// <returns>Printable string</returns>
        public static string FlattenException(Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (e.ApplicationExitCode == 0)
            {
                Log.Information("Application exiting normally");
                Log.CloseAndFlush();
            }
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            new MainWindow().Show();
        }

        public static bool IsOperatingSystemSupported()
        {
            OperatingSystem os = Environment.OSVersion;
            return os.Version >= App.MIN_SUPPORTED_OS;
        }
    }

    class Options
    {
        public string UpdateDest { get; set; }

        [Option('c', "completing-update",
            HelpText = "Indicates that we are booting a new copy of ME3Tweaks Mod Manager that has just been upgraded. --update-from should be included when calling this parameter.")]
        public bool BootingNewUpdate { get; set; }

        [Option("update-from",
            HelpText = "Indicates what build of Mod Manager we are upgrading from.")]
        public int UpdateFromBuild { get; set; }

        [Option("update-boot",
            HelpText = "Indicates that the process should run in update mode for a single file .net core executable. The process will exit upon starting because the platform extraction process will have completed.")]
        public bool UpdateBoot { get; set; }

        [Option("upgrade-from-me3cmm",
            HelpText = "Indicates that this is an upgrade from ME3CMM, and that a migration should take place.")]
        public bool UpgradingFromME3CMM { get; set; }
    }
}
