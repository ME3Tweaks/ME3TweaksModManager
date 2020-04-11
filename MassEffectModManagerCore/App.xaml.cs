using Serilog;
using Serilog.Sinks.RollingFile.Extension;
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
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System.Runtime.InteropServices;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using System.Linq;
using ME3Explorer.Packages;
using MassEffectModManagerCore.modmanager.usercontrols;
using AuthenticodeExaminer;
using MassEffectModManagerCore.modmanager.windows;
using SevenZip;

namespace MassEffectModManagerCore
{
    [Localizable(false)]
    public partial class App : Application
    {
        public static bool AppDataExistedAtBoot = Directory.Exists(Utilities.GetAppDataFolder(false)); //alphabetically this must come first in App!

        /// <summary>
        /// Registry key for Mass Effect Mod Manager itself. This likely won't be used much
        /// </summary>
        internal const string REGISTRY_KEY = @"HKEY_CURRENT_USER\Software\ME3Tweaks Mod Manager";

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
        public const double HighestSupportedModDesc = 6.0;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

        public static string BuildDate;
        public static bool IsSigned;

        public App() : base()
        {
            // var f = Assembly.GetCallingAssembly().GetManifestResourceNames();
            ExecutableLocation = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.sevenzipwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "sevenzipwrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.lzo2wrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "lzo2wrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.zlibwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "zlibwrapper.dll"), false);
            SetDllDirectory(Utilities.GetDllDirectory());

            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                string exeFolder = Directory.GetParent(ExecutableLocation).ToString();
                LogCollector.CreateLogger();

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

                            var updaterExe = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ExecutableLocation)), @"ME3TweaksUpdater.exe");
                            if (File.Exists(updaterExe))
                            {
                                //write updated exe
                                Utilities.ExtractInternalFile(@"MassEffectModManagerCore.updater.ME3TweaksUpdater.exe", updaterExe, true);
                            }
                            else
                            {
                                MessageBox.Show("Updater missing: " + updaterExe);
                            }

                            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                            return;
                        }

                        //if (parsedCommandLineArgs.Value.UpdateDest != null)
                        //{
                        //    if (File.Exists(parsedCommandLineArgs.Value.UpdateDest))
                        //    {
                        //        updateDestinationPath = parsedCommandLineArgs.Value.UpdateDest;
                        //    }

                        //    //if (parsedCommandLineArgs.Value.BootingNewUpdate)
                        //    //{
                        //    //    Thread.Sleep(1000); //Delay boot to ensure update executable finishes
                        //    //    try
                        //    //    {
                        //    //        string updateFile = Path.Combine(exeFolder, "ME3TweaksModManager-Update.exe");
                        //    //        if (File.Exists(updateFile))
                        //    //        {
                        //    //            File.Delete(updateFile);
                        //    //            Log.Information("Deleted staged update");
                        //    //        }
                        //    //    }
                        //    //    catch (Exception e)
                        //    //    {
                        //    //        Log.Warning("Unable to delete staged update: " + e.ToString());
                        //    //    }
                        //    //}
                        //}

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
                    typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));


                Log.Information("===========================================================================");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutableLocation);
                string version = fvi.FileVersion;
                Log.Information("ME3Tweaks Mod Manager " + version);
                Log.Information("Application boot: " + DateTime.UtcNow);
                Log.Information("Executable location: " + ExecutableLocation);
                Log.Information("Operating system: " + RuntimeInformation.OSDescription);
                //Get build date
                var info = new FileInspector(App.ExecutableLocation);
                var signTime = info.GetSignatures().FirstOrDefault()?.TimestampSignatures.FirstOrDefault()?.TimestampDateTime?.UtcDateTime;

                if (signTime != null)
                {
                    IsSigned = true;
                    BuildDate = signTime.Value.ToString(@"MMMM dd, yyyy");
                    Log.Information("Build signed by ME3Tweaks. Build date: " + BuildDate);
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

                /*
                if (updateDestinationPath != null)
                {
                    Log.Information(" >> In update mode. Update destination: " + updateDestinationPath);
                    int i = 0;
                    while (i < 8)
                    {

                        i++;
                        try
                        {
                            Log.Information($"Applying update: {ExecutableLocation} -> {updateDestinationPath}");
                            File.Copy(ExecutableLocation, updateDestinationPath, true);
                            Log.Information("Update applied, restarting...");
                            break;
                        }
                        catch (Exception e)
                        {
                            Log.Error("Error applying update: " + e.Message);
                            if (i < 8)
                            {
                                Thread.Sleep(1000);
                                Log.Warning("Attempt #" + (i + 1));
                            }
                            else
                            {
                                Log.Fatal("Unable to apply update after 8 attempts. We are giving up.");
                                MessageBox.Show("Update was unable to apply. See the application log for more information. If this continues to happen please come to the ME3Tweaks discord, or download a new copy from GitHub.");
                                Environment.Exit(1);
                            }
                        }
                    }
                    ProcessStartInfo psi = new ProcessStartInfo(updateDestinationPath);
                    psi.WorkingDirectory = Directory.GetParent(updateDestinationPath).FullName;
                    psi.Arguments = "--completing-update";
                    if (App.UpdatedFrom > 0)
                    {
                        psi.Arguments += " --update-from " + App.UpdatedFrom;
                    }
                    Log.Information($"Booting new update: {updateDestinationPath} {psi.Arguments}");

                    Process.Start(psi);
                            Application.Current.Shutdown();
                    Current.Shutdown();
                    
                }*/

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
                Log.Information("ME1: " + Utilities.GetGameBackupPath(Mod.MEGame.ME1), false);
                Log.Information("ME1 (w/ vanilla check): " + Utilities.GetGameBackupPath(Mod.MEGame.ME1), true);
                Log.Information("ME2: " + Utilities.GetGameBackupPath(Mod.MEGame.ME2), false);
                Log.Information("ME2 (w/ vanilla check): " + Utilities.GetGameBackupPath(Mod.MEGame.ME2), true);
                Log.Information("ME3: " + Utilities.GetGameBackupPath(Mod.MEGame.ME3), false);
                Log.Information("ME3 (w/ vanilla check): " + Utilities.GetGameBackupPath(Mod.MEGame.ME3), true);

                Log.Information("Standardized ME3Tweaks startup has completed. Now beginning Mod Manager startup");
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
                var settingsExist = File.Exists(Settings.SettingsPath);
                Settings.Load();

                if (!Settings.EnableTelemetry)
                {
                    Log.Warning("Telemetry is disabled :(");
                    Analytics.SetEnabledAsync(false);
                    Crashes.SetEnabledAsync(false);
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
                    Log.Information(@"This is a first boot. The system language code is " + currentCultureLang);
                }

                Log.Information("Deleting temp files (if any)");
                Utilities.DeleteFilesAndFoldersRecursively(Utilities.GetTempPath());

                Log.Information("Initializing package handlers");

                MEPackageHandler.Initialize();

                Log.Information("Ensuring default ASI assets are present");
                ASIManagerPanel.ExtractDefaultASIResources();

                

                Log.Information("Mod Manager pre-UI startup has completed. The UI will now load.");
                Log.Information("If the UI fails to start, it may be that a third party tool is injecting itself into Mod Manager, such as RivaTuner or Afterburner and is corrupting the process.");
                POST_STARTUP = true; //this could be earlier but i'm not sure when crash handler actually is used, doesn't seem to be after setting it...
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw;
            }
        }

        public static string[] SupportedLanguages = { "int", /*"pol",*/ "rus", "deu"/*, "fra"*/};
        public static Dictionary<string, string> ServerManifest { get; set; }

        public static int BuildNumber = Assembly.GetEntryAssembly().GetName().Version.Revision;

        /// <summary>
        /// Accesses the third party identification server. Key is the game enum as a string, results are dictionary of DLCName => Info.
        /// </summary>
        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> ThirdPartyIdentificationService;

        internal static string BugReportURL = "https://github.com/ME3Tweaks/ME3TweaksModManager/issues";
        public static Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>> ThirdPartyImportingService;
        public static bool BootingUpdate;
        public static int UpdatedFrom = 0;
        public static string InitialLanguage = "int";
        internal static Dictionary<string, List<string>> TipsService;
        internal static string CurrentLanguage = InitialLanguage;

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
#else
                version += " PRERELEASE";
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
#else
                version += " PRERELEASE";
#endif
                return $"ME3Tweaks Mod Manager {version} (Build {BuildNumber})";
            }
        }

        public static string ExecutableLocation { get; private set; }
        public static Dictionary<string, string> OnlineManifest { get; internal set; }

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
        /// Performs the upgrade migration from Mass Effect 3 Mod MAnager to ME3Tweaks Mod Manager, transitioning settings and mods.
        /// </summary>
        public static void UpgradeFromME3CMM()
        {
            /*
             * Process:
             *  1. Migrate settings
             *  2. Migrate the mods folder into a subdirectory named ME3
             *  3. 
             */
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
