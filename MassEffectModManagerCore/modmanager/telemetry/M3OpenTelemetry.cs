using Azure.Monitor.OpenTelemetry.Exporter;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Exceptions;
using ME3TweaksCore.Helpers;
using NickStrupat;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Threading;

namespace ME3TweaksModManager.modmanager.telemetry
{
    /// <summary>
    /// Enriching activty class
    /// </summary>
    public class GlobalAttributeEnrichingProcessor : BaseProcessor<Activity>
    {
        private readonly string _buildDate;
        private readonly string _version;
        private readonly string _environment;
        private readonly string _userId;

        public GlobalAttributeEnrichingProcessor(string buildDate, string version, string environment, string userId)
        {
            _buildDate = buildDate;
            _version = version;
            _environment = environment;
            _userId = userId;
        }

        public override void OnStart(Activity activity)
        {
            activity.SetTag("BuildDate", _buildDate);
            activity.SetTag("Version", _version);
            activity.SetTag("Environment", _environment);
            // Use OTel's own semantic convention for user id, not "ai.user.id"
            activity.SetTag("enduser.id", _userId);
        }
    }

    /// <summary>
    /// Class for handling telemetry for ME3Tweaks Mod Manager
    /// </summary>
    internal static class M3OpenTelemetry
    {
        private static readonly ActivitySource Source = new ActivitySource(@"ME3TweaksModManager");

        // Performance sampling timer
        private static readonly TimeSpan PerformanceMetricsInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PerformanceMetricsMaxDuration = TimeSpan.FromHours(1);
        private static readonly Stopwatch PerformanceMetricsUptime = new Stopwatch();
        private static TracerProvider _tracerProvider;
        private static Timer _performanceMetricsTimer;

        #region Telemetry event queuing
        /// <summary>
        /// Telemetry events that are queued for submission; items are stored here if the preview panel hasn't been shown as user
        /// would not have had choice to turn it off yet.
        /// </summary>
        private static List<(string, Dictionary<string, string>)> QueuedTelemetryItems = new List<(string, Dictionary<string, string>)>();

        /// <summary>
        /// If telemetry has been flushed after checking if it is enabled. Once flushed items won't be queued anymore
        /// </summary>
        private static bool FlushedTelemetry;

        /// <summary>
        /// Flushes the startup telemetry events and disables the queue.
        /// </summary>
        public static void FlushTelemetryItems()
        {
            FlushedTelemetry = true;
            if (Settings.EnableTelemetry && QueuedTelemetryItems != null)
            {
                foreach (var v in QueuedTelemetryItems)
                {
                    M3OpenTelemetry.TrackEvent(v.Item1, v.Item2);
                }
            }

            QueuedTelemetryItems = null; // Just release the memory. This variable is never used again
        }
        #endregion

        internal static void InitOpenTelemetry()
        {
#if !DEBUG
            if (APIKeys.HasAppInsightsConnectionString)
            {
                M3Log.Information(@"Initializing Application Insights telemetry");
                InternalInitialize(APIKeys.AppInsightsConnectionString);
            }
            else
            {
                M3Log.Error(@"This build is not configured correctly for Application Insights!");
            }
#else
            if (!APIKeys.HasAppInsightsConnectionString)
            {
                Debug.WriteLine(@" >>> This build is missing an Application Insights connection string!");
            }
            else
            {
                Debug.WriteLine(@"This build has an Application Insights connection string");
                InternalInitialize(APIKeys.AppInsightsConnectionString);
            }
#endif
        }


        /// <summary>
        /// Initializes the OpenTelemetry pipeline and begins exporting to Azure Monitor / Application Insights.
        /// </summary>
        /// <param name="connectionString">The Application Insights connection string.</param>
        private static void InternalInitialize(string connectionString)
        {
            EnsureInstanceGuid();

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: @"ME3TweaksModManager");
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(Source.Name)
                .AddProcessor(new GlobalAttributeEnrichingProcessor(
                    BuildHelper.BuildDateString,                 // For more accurate version filtering
                    MLibraryConsumer.GetAppVersion().ToString(), // For version filtering
                    new ComputerInfo().OSFullName,               // for platform-specific analysis
                    Settings.InstanceGuid.ToString())            // For determining how widespread issues may be
                )
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
                    options.Targets = ConsoleExporterOutputTargets.Debug;
                })
#endif
                .Build();

            PerformanceMetricsUptime.Restart();
            StartPerformanceMetricsSampler();
        }

        /// <summary>
        /// Ensures instance guid has been set
        /// </summary>
        private static void EnsureInstanceGuid()
        {
            if (Settings.InstanceGuid == Guid.Empty)
            {
                Settings.InstanceGuid = Guid.NewGuid();
            }
        }

        #region Performance sampling
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

                activity.SetTag(@"memory.usage.mib", currentMemoryMebibytes);
                activity.SetTag(@"process.cpu.time.seconds", processCpuTimeSeconds);
                activity.SetTag(@"app.uptime.seconds", uptimeSeconds);
            }
            catch (Exception e)
            {
                MLog.Warning($@"Failed to sample performance telemetry: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Tracks a named event with optional property bag.
        /// </summary>
        public static void TrackEvent(string name, Dictionary<string, string> properties = null)
        {
            if (!Settings.ShowedPreviewPanel && !FlushedTelemetry && QueuedTelemetryItems != null)
            {
                // Queue a telemetry item until user consent completes
                QueuedTelemetryItems.Add((name, properties));
                return;
            }

            if (!Settings.CanSendTelemetry)
            {
                // Telemetry is disabled
                return;
            }

            using var activity = Source.StartActivity(name, ActivityKind.Internal);
            if (activity != null)
            {
                if (properties != null)
                {
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
            {
                // Telemetry is disabled
                return;
            }

            if (exception is NoTelemetryException)
                return; // This exception doesn't trigger telemetry submission for it
            using var activity = Source.StartActivity(exception?.GetType().Name ?? @"Error", ActivityKind.Internal);
            if (activity != null)
            {
                activity.AddException(exception);
                activity.SetStatus(ActivityStatusCode.Error, exception?.Message);
                if (properties != null)
                {
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
            {
                // Telemetry is disabled
                return;
            }
            var activityName = @"Crash";
            if (exception?.GetType() != null)
            {
                activityName = $@"{exception.GetType()} - Crash";
            }
            using (var activity = Source.StartActivity(activityName, ActivityKind.Internal))
            {
                if (activity != null)
                {
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
