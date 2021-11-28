using System;
using System.IO;
using ME3TweaksCore.Helpers;
using Serilog;
using Serilog.Sinks.RollingFile.Extension;

namespace ME3TweaksModManager.modmanager.diagnostics
{
    /// <summary>
    /// Interposer used to prefix M3Log messages with their source component. Call only from M3 code
    /// </summary>
    public static class M3Log
    {
        private const string Prefix = @"M3";
        public static string LogDir { get; set; }

        public static void Exception(Exception exception, string preMessage, bool fatal = false, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Error($@"{prefix}{preMessage}");

                // Log exception
                while (exception != null)
                {
                    var line1 = exception.GetType().Name + @": " + exception.Message;
                    foreach (var line in line1.Split("\n")) // do not localize
                    {
                        if (fatal)
                            Log.Fatal(prefix + line);
                        else
                            Log.Error(prefix + line);

                    }

                    if (exception.StackTrace != null)
                    {
                        foreach (var line in exception.StackTrace.Split("\n")) // do not localize
                        {
                            if (fatal)
                                Log.Fatal(prefix + line);
                            else
                                Log.Error(prefix + line);
                        }
                    }

                    exception = exception.InnerException;
                }
            }
        }

        public static void Information(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Information($@"{prefix}{message}");
            }
        }

        public static void Warning(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Warning($@"{prefix}{message}");
            }
        }

        public static void Error(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Error($@"{prefix}{message}");
            }
        }

        public static void Fatal(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Fatal($@"{prefix}{message}");
            }
        }

        public static void Debug(string message, bool condition = true)
        {
            if (condition)
            {
                var prefix = $@"[{Prefix}] ";
                Log.Debug($@"{prefix}{message}");
            }
        }

        /// <summary>
        /// Creates an ILogger for ME3Tweaks Mod Manager. This does NOT assign it to the Log.Logger instance.
        /// </summary>
        /// <returns></returns>
        public static ILogger CreateLogger()
        {
            return new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(MCoreFilesystem.GetAppDataFolder(), @"logs", @"modmanagerlog.txt"),
                                    retainedFileDurationLimit: TimeSpan.FromDays(14),
                                    fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB  
#if DEBUG
                            .WriteTo.Debug()
#endif
                            .CreateLogger();
        }
    }
}
