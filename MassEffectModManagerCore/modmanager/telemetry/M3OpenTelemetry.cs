using Azure.Monitor.OpenTelemetry.Exporter;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ME3TweaksModManager.modmanager.telemetry
{
    internal static class M3OpenTelemetry
    {
        private static readonly ActivitySource Source = new ActivitySource(@"ME3TweaksModManager");
        private static TracerProvider _tracerProvider;

        /// <summary>
        /// Initializes the OpenTelemetry pipeline and begins exporting to Azure Monitor / Application Insights.
        /// </summary>
        /// <param name="connectionString">The Application Insights connection string.</param>
        public static void Initialize(string connectionString)
        {
            EnsureInstanceGuid();

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName: @"ME3TweaksModManager")
                .AddAttributes(new Dictionary<string, object>
                {
                    [@"BuildDate"] = BuildHelper.BuildDateString,
                    [@"Version"] = MLibraryConsumer.GetAppVersion().ToString(),
                    [@"ai.user.id"] = Settings.InstanceGuid.ToString()
                });

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(Source.Name)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString)
#if DEBUG
                .AddConsoleExporter(options =>
                {
                    options.Targets = ConsoleExporterOutputTargets.Debug;
                })
#endif
                .Build();
        }

        private static void EnsureInstanceGuid()
        {
            if (Settings.InstanceGuid == Guid.Empty)
            {
                Settings.InstanceGuid = Guid.NewGuid();
            }
        }

        /// <summary>
        /// Tracks a named event with optional property bag.
        /// </summary>
        public static void TrackEvent(string name, Dictionary<string, string> properties = null)
        {
            if (!Settings.CanSendTelemetry)
                return;
            using var activity = Source.StartActivity(name, ActivityKind.Internal);
            if (activity != null)
            {
                // Use anonymous user id so Azure Monitor / Application Insights can differentiate users for user count metrics
                // e.g. how many unique users have the same error
                // Not sure this is accurate right now
                activity.SetTag(@"ai.user.id", Settings.InstanceGuid.ToString());

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
                return;
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
            _tracerProvider?.ForceFlush();
            _tracerProvider?.Dispose();
            _tracerProvider = null;
        }
    }
}
