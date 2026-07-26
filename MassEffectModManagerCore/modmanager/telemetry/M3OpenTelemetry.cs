using Azure.Monitor.OpenTelemetry.Exporter;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;
using System.Threading;

namespace ME3TweaksModManager.modmanager.telemetry
{
    internal static class M3OpenTelemetry
    {
        private static readonly ActivitySource Source = new ActivitySource(@"ME3TweaksModManager");
        private static readonly TimeSpan PerformanceMetricsInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PerformanceMetricsMaxDuration = TimeSpan.FromHours(1);
        private static readonly Stopwatch PerformanceMetricsUptime = new Stopwatch();
        private static TracerProvider _tracerProvider;
        private static Timer _performanceMetricsTimer;

        /// <summary>
        /// Initializes the OpenTelemetry pipeline and begins exporting to Azure Monitor / Application Insights.
        /// </summary>
        /// <param name="connectionString">The Application Insights connection string.</param>
        public static void Initialize(string connectionString)
        {
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(Source.Name)
                .AddAzureMonitorTraceExporter(o =>
                {
                    o.ConnectionString = connectionString;
                    o.EnableLiveMetrics = false; // While kind of useful it's way too much stuff we don't care about
                    o.EnableStandardMetrics = false; // We don't need the standard metrics, we only want our custom performance metrics
                    o.EnablePerformanceCounters = false; // Generates too much logs, but actually would be useful for performance

                })
#if DEBUG
                .AddConsoleExporter(options =>
                {
                    // This line directs the output specifically to the Debug console
                    options.Targets = ConsoleExporterOutputTargets.Debug;
                })
#endif
                .Build();

            PerformanceMetricsUptime.Restart();
            StartPerformanceMetricsSampler();
        }

        private static void EnsureInstanceGuid()
        {
            if (Settings.InstanceGuid == Guid.Empty)
            {
                Settings.InstanceGuid = Guid.NewGuid();
            }
        }

        private static void AddBuildInfo(Dictionary<string, string> data)
        {
            data[@"BuildDate"] = BuildHelper.BuildDateString;
            data[@"Version"] = MLibraryConsumer.GetAppVersion().ToString();
        }

        private static void StartPerformanceMetricsSampler()
        {
            StopPerformanceMetricsSampler();
            _performanceMetricsTimer = new Timer(SamplePerformanceMetrics, null, PerformanceMetricsInterval, PerformanceMetricsInterval);
        }

        private static void StopPerformanceMetricsSampler()
        {
            _performanceMetricsTimer?.Dispose();
            _performanceMetricsTimer = null;
        }

        private static void SamplePerformanceMetrics(object state)
        {
            if (!Settings.CanSendTelemetry)
                return;

            try
            {
                if (PerformanceMetricsUptime.Elapsed > PerformanceMetricsMaxDuration)
                {
                    StopPerformanceMetricsSampler();
                    return;
                }

                using var process = Process.GetCurrentProcess();
                process.Refresh();

                var currentMemoryMebibytes = process.WorkingSet64 / 1048576d;
                var processCpuTimeSeconds = process.TotalProcessorTime.TotalSeconds;
                var uptimeSeconds = PerformanceMetricsUptime.Elapsed.TotalSeconds;

                using var activity = Source.StartActivity(@"PerformanceMetrics", ActivityKind.Internal);
                if (activity == null)
                    return;

                activity.SetTag(@"ai.user.id", Settings.InstanceGuid.ToString());
                activity.SetTag(@"memory.usage.mib", currentMemoryMebibytes);
                activity.SetTag(@"process.cpu.time.seconds", processCpuTimeSeconds);
                activity.SetTag(@"app.uptime.seconds", uptimeSeconds);
            }
            catch (Exception e)
            {
                MLog.Warning($@"Failed to sample performance telemetry: {e.Message}");
            }
        }

        /// <summary>
        /// Tracks a named event with optional property bag.
        /// </summary>
        public static void TrackEvent(string name, Dictionary<string, string> properties = null)
        {
            if (!Settings.CanSendTelemetry)
                return;
            EnsureInstanceGuid();
            using var activity = Source.StartActivity(name, ActivityKind.Internal);
            if (activity != null)
            {
                // Use anonymous user id so Azure Monitor / Application Insights can differentiate users for user count metrics
                // e.g. how many unique users have the same error
                // Not sure this is accurate right now
                activity.SetTag(@"ai.user.id", Settings.InstanceGuid.ToString());

                if (properties != null)
                {
                    AddBuildInfo(properties);
                    foreach (var kvp in properties)
                        activity.SetTag(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Tracks an exception with optional property bag.
        /// </summary>
        public static void TrackError(Exception exception, Dictionary<string, string> properties = null)
        {
            if (!Settings.CanSendTelemetry)
                return;
            EnsureInstanceGuid();
            using var activity = Source.StartActivity(exception?.GetType().Name ?? @"Error", ActivityKind.Internal);
            if (activity != null)
            {
                // Use anonymous user id so Azure Monitor / Application Insights can differentiate users for user count metrics
                // e.g. how many unique users have the same error
                activity.SetTag(@"ai.user.id", Settings.InstanceGuid.ToString());

                activity.AddException(exception);
                activity.SetStatus(ActivityStatusCode.Error, exception?.Message);
                if (properties != null)
                {
                    AddBuildInfo(properties);
                    foreach (var kvp in properties)
                        activity.SetTag(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Tracks a crash exception, attaches the latest log file, and immediately flushes telemetry to the server.
        /// This method must be called right before the application exits due to an unhandled crash.
        /// </summary>
        public static void TrackCrash(Exception exception, Dictionary<string, string> properties = null)
        {
            if (!Settings.CanSendTelemetry)
                return;
            EnsureInstanceGuid();
            var activityName = @"Crash";
            if (exception?.GetType() != null)
            {
                activityName = $@"{exception.GetType()} - Crash";
            }
            using (var activity = Source.StartActivity(activityName, ActivityKind.Internal))
            {
                if (activity != null)
                {
                    activity.SetTag(@"ai.user.id", Settings.InstanceGuid.ToString());
                    activity.AddException(exception);
                    activity.SetStatus(ActivityStatusCode.Error, exception?.Message);

                    // Attach the latest log file; do not restart the logger since the app is about to exit
                    string logText = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), false);
                    if (logText != null)
                    {
                        // Application Insights limits property values to 8192; keep the most recent (tail) of the log
                        const int maxLogLength = 8192;
                        if (logText.Length > maxLogLength)
                            logText = logText.Substring(logText.Length - maxLogLength);
                        activity.SetTag(@"log", logText);
                    }

                    if (properties != null)
                        foreach (var kvp in properties)
                            activity.SetTag(kvp.Key, kvp.Value);
                }
            }
            // Force flush so telemetry is delivered before the process exits
            if (_tracerProvider != null && _tracerProvider.ForceFlush())
            {
                Debug.WriteLine(@"Flush succeeded!");
            }
            else
            {
                Debug.WriteLine(@"Flush failed or no tracer provider");
            }
        }

        /// <summary>
        /// Flushes pending telemetry and disposes the tracer provider.
        /// </summary>
        public static void Shutdown()
        {
            StopPerformanceMetricsSampler();
            PerformanceMetricsUptime.Reset();

            _tracerProvider?.ForceFlush();
            _tracerProvider?.Dispose();
            _tracerProvider = null;
        }
    }
}
