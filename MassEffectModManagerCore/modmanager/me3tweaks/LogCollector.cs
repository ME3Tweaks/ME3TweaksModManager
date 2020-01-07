

using Serilog;
using Serilog.Sinks.RollingFile.Extension;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    class LogCollector
    {
        public static string CollectLogs(string logfile)
        {
            Log.Information("Shutting down logger to allow application to pull log file.");
            Log.CloseAndFlush();
            try
            {
                string log = File.ReadAllText(logfile);
                CreateLogger();
                return log;
            }
            catch (Exception e)
            {
                CreateLogger();
                Log.Error("Could not read log file! " + e.Message);
                return null;
            }
        }

        internal static void CreateLogger()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(App.LogDir, "modmanagerlog.txt"),
                                    retainedFileDurationLimit: TimeSpan.FromDays(14),
                                    fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB  
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
        }

        internal static string CollectLatestLog(bool restartLogger)
        {
            Log.Information("Shutting down logger to allow application to pull log file.");
            Log.CloseAndFlush();
            var logFile = new DirectoryInfo(App.LogDir)
                                             .GetFiles("*.txt")
                                             .OrderByDescending(f => f.LastWriteTime)
                                             .FirstOrDefault();
            string logText = null;
            if (logFile != null && File.Exists(logFile.FullName))
            {
                logText = File.ReadAllText(logFile.FullName);
            }

            if (restartLogger)
            {
                CreateLogger();
            }
            return logText;
        }
    }
}
