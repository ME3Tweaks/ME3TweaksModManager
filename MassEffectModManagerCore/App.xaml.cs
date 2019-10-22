using Serilog;
using Serilog.Sinks.RollingFile.Extension;
using System;
using System.Collections.Generic;
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

namespace MassEffectModManagerCore
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
        /// <summary>
        /// The highest version of ModDesc that this version of Mod Manager can support.
        /// </summary>
        public const double HighestSupportedModDesc = 6.0;
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            //API keys are not stored in the git repository for Mod Manager.
            //You will need to provide your own keys for use by defining public properties
            //in a partial APIKeys class.
#if !DEBUG
            var props = typeof(APIKeys).GetProperties();
            if (APIKeys.HasAppCenterKey)
            {
                AppCenter.Start(APIKeys.AppCenterKey,
                                   typeof(Analytics), typeof(Crashes));
            }
#endif
        }

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
                Log.Logger = new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(App.LogDir, "modmanagerlog.txt"),
                        retainedFileDurationLimit: TimeSpan.FromDays(14),
                        fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();


                string[] args = Environment.GetCommandLineArgs();
                //Parsed<Options> parsedCommandLineArgs = null;
                string updateDestinationPath = null;

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
                            Environment.Exit(0);
                        }
                        if (parsedCommandLineArgs.Value.UpdateDest != null)
                        {
                            if (File.Exists(parsedCommandLineArgs.Value.UpdateDest))
                            {
                                updateDestinationPath = parsedCommandLineArgs.Value.UpdateDest;
                            }
                            //if (parsedCommandLineArgs.Value.BootingNewUpdate)
                            //{
                            //    Thread.Sleep(1000); //Delay boot to ensure update executable finishes
                            //    try
                            //    {
                            //        string updateFile = Path.Combine(exeFolder, "ME3TweaksModManager-Update.exe");
                            //        if (File.Exists(updateFile))
                            //        {
                            //            File.Delete(updateFile);
                            //            Log.Information("Deleted staged update");
                            //        }
                            //    }
                            //    catch (Exception e)
                            //    {
                            //        Log.Warning("Unable to delete staged update: " + e.ToString());
                            //    }
                            //}
                        }

                        if (parsedCommandLineArgs.Value.UpdateFromBuild != 0)
                        {
                            App.UpdatedFrom = parsedCommandLineArgs.Value.UpdateFromBuild;
                        }
                    }
                }
                #endregion




                this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
                POST_STARTUP = true;
                ToolTipService.ShowDurationProperty.OverrideMetadata(
                    typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
                Log.Information("===========================================================================");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutableLocation);
                string version = fvi.FileVersion;
                Log.Information("ME3Tweaks Mod Manager " + version);
                Log.Information("Application boot: " + DateTime.UtcNow.ToString());
                Log.Information("Executable location: " + ExecutableLocation);

                #region Update mode boot
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
                    Environment.Exit(0);
                    Current.Shutdown();
                }
                #endregion
                System.Windows.Controls.ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control),
               new FrameworkPropertyMetadata(true));

                Log.Information("Standardized ME3Tweaks startup has completed. Now beginning Mod Manager startup");
                Log.Information("Loading settings");
                Settings.Load();
                Log.Information("Ensuring mod directories");
                Utilities.DeleteFilesAndFoldersRecursively(Utilities.GetTempPath());

                Log.Information("Mod Manager pre-UI startup has completed");
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw e;
            }
        }

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
                version += "TEST BUILD";
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
                version += "TEST BUILD";
#endif
                return $"ME3Tweaks Mod Manager {version} (Build {BuildNumber})";
            }
        }

        public static string ExecutableLocation { get; private set; }

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("ME3Tweaks Mod Manager has crashed! This is the exception that caused the crash:");
            string st = FlattenException(e.Exception);
            Log.Fatal(errorMessage);
            Log.Fatal(st);
            //Log.Information("Forcing beta mode off before exiting...");
            //Utilities.WriteRegistryKey(Registry.CurrentUser, AlotAddOnGUI.MainWindow.REGISTRY_KEY, AlotAddOnGUI.MainWindow.SETTINGSTR_BETAMODE, 0);
            File.Create(Utilities.GetAppCrashFile());
        }

        /// <summary>
        /// Called when a fatal crash occurs. Only does something if startup has not completed.
        /// </summary>
        /// <param name="e">The fatal exception.</param>
        public static void OnFatalCrash(Exception e)
        {
            if (!POST_STARTUP)
            {
                string errorMessage = string.Format("ME3Tweaks Mod Manager has encountered a fatal startup crash:\n" + FlattenException(e));
                File.WriteAllText(Path.Combine(Utilities.GetAppDataFolder(), "FATAL_STARTUP_CRASH.txt"), errorMessage);
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
    }

    class Options
    {
        public string UpdateDest { get; set; }

        [Option('c', "completing-update",
            HelpText = "Indicates that we are booting a new copy of ME3Tweaks Mod Manager that has just been upgraded. --update-from should be included when calling this parameter.")]
        public int BootingNewUpdate { get; set; }

        [Option("update-from",
            HelpText = "Indicates what build of Mod Manager we are upgrading from.")]
        public int UpdateFromBuild { get; set; }

        [Option("update-boot",
            HelpText = "Indicates that this is process is running in update mode. The process will exit upon starting because the extraction process will have completed.")]
        public bool UpdateBoot { get; set; }
    }
}
