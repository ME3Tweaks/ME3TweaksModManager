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
using MassEffectModManager;
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
        internal const string REGISTRY_KEY = @"Software\Mass Effect Mod Manager";
        internal const string REGISTRY_KEY_ME3CMM = @"Software\Mass Effect 3 Mod Manager";
        internal const string BACKUP_REGISTRY_KEY = @"Software\ALOTAddon"; //Shared. Do not change
        public static string LogDir = Path.Combine(Utilities.GetAppDataFolder(), "logs");
        private static bool POST_STARTUP = false;
        public const string DISCORD_INVITE_LINK = "https://discord.gg/s8HA6dc";
        internal static readonly double HighestSupportedModDesc = 6.0;
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            //API keys are not stored in the git repository for Mod Manager.
            //You will need to provide your own keys for use by defining public properties
            //in a partial APIKeys class.
            var props = typeof(APIKeys).GetProperties();
            if (APIKeys.HasAppCenterKey)
            {
                AppCenter.Start(APIKeys.AppCenterKey,
                                   typeof(Analytics), typeof(Crashes));
            }
        }

        public App() : base()
        {
            var f = Assembly.GetCallingAssembly().GetManifestResourceNames();
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.sevenzipwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "sevenzipwrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.lzo2wrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "lzo2wrapper.dll"), false);
            Utilities.ExtractInternalFile("MassEffectModManagerCore.bundleddlls.zlibwrapper.dll", Path.Combine(Utilities.GetDllDirectory(), "zlibwrapper.dll"), false);
            SetDllDirectory(Utilities.GetDllDirectory());

            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string exePath = assembly.Location;
                string exeFolder = Directory.GetParent(exePath).ToString();
                Log.Logger = new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(App.LogDir, "modmanagerlog.txt"),
                        retainedFileDurationLimit: TimeSpan.FromDays(14),
                        fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();


                string[] args = Environment.GetCommandLineArgs();
                Parsed<Options> parsedCommandLineArgs = null;
                string updateDestinationPath = null;

                #region Update boot
                if (args.Length > 1)
                {
                    var result = Parser.Default.ParseArguments<Options>(args);
                    if (result.GetType() == typeof(Parsed<Options>))
                    {
                        //Parsing succeeded - have to do update check to keep logs in order...
                        parsedCommandLineArgs = (Parsed<Options>)result;
                        if (parsedCommandLineArgs.Value.UpdateDest != null)
                        {
                            if (File.Exists(parsedCommandLineArgs.Value.UpdateDest))
                            {
                                updateDestinationPath = parsedCommandLineArgs.Value.UpdateDest;
                            }
                            if (parsedCommandLineArgs.Value.BootingNewUpdate)
                            {
                                Thread.Sleep(1000); //Delay boot to ensure update executable finishes
                                try
                                {
                                    string updateFile = Path.Combine(exeFolder, "MassEffectModManager-Update.exe");
                                    if (File.Exists(updateFile))
                                    {
                                        File.Delete(updateFile);
                                        Log.Information("Deleted staged update");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Warning("Unable to delete staged update: " + e.ToString());
                                }
                            }
                        }
                    }
                }
                #endregion




                this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
                POST_STARTUP = true;
                ToolTipService.ShowDurationProperty.OverrideMetadata(
                    typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
                Log.Information("===========================================================================");
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = fvi.FileVersion;
                Log.Information("Mass Effect Mod Manager " + version);
                Log.Information("Application boot: " + DateTime.UtcNow.ToString());
                Log.Information("Executable location: " + System.Reflection.Assembly.GetEntryAssembly().Location);

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
                            Log.Information("Applying update");
                            File.Copy(assembly.Location, updateDestinationPath, true);
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
                    Log.Information("Rebooting into normal mode to complete update");
                    ProcessStartInfo psi = new ProcessStartInfo(updateDestinationPath);
                    psi.WorkingDirectory = updateDestinationPath;
                    psi.Arguments = "--completing-update";
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

                Utilities.EnsureModDirectories();

                Log.Information("Mod Manager pre-UI startup has completed");
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw e;
            }
        }

        public static int BuildNumber = Assembly.GetEntryAssembly().GetName().Version.Revision;
        internal static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> ThirdPartyIdentificationService;
        internal static string BugReportURL = "https://github.com/ME3Tweaks/MassEffectModManager/issues";
        internal static Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>> ThirdPartyImportingService;

        public static string AppVersionHR
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += "." + assemblyVersion.Build;
                }

#if DEBUG
                version += " DEBUG";
#else
                version += "TEST BUILD";
#endif
                return $"Mass Effect Mod Manager {version} (Build {BuildNumber})";
            }
        }

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("Mass Effect Mod Manager has crashed! This is the exception that caused the crash:");
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
                string errorMessage = string.Format("Mass Effect Mod Manager has encountered a fatal startup crash:\n" + FlattenException(e));
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
        [Option('u', "update-dest-path",
            HelpText = "Indicates where this booting instance of Mass Effect Mod Manager should attempt to copy itself and reboot to")]
        public string UpdateDest { get; set; }

        [Option('c', "completing-update",
            HelpText = "Indicates that we are booting a new copy of Mass Effect Mod Manager that has just been upgraded")]
        public bool BootingNewUpdate { get; set; }
    }
}
