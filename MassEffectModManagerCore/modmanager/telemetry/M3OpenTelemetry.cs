using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using ME3TweaksCore.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

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
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(Source.Name)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString)
#if DEBUG
                .AddConsoleExporter() // This line adds the console exporter
#endif
                .Build();
        }

        /// <summary>
        /// Tracks a named event with optional property bag.
        /// </summary>
        public static void TrackEvent(string name, Dictionary<string, string> properties = null)
        {
            using var activity = Source.StartActivity(name, ActivityKind.Internal);
            if (activity != null && properties != null)
                foreach (var kvp in properties)
                    activity.SetTag(kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Tracks an exception with optional property bag.
        /// </summary>
        public static void TrackError(Exception exception, Dictionary<string, string> properties = null)
        {
            using var activity = Source.StartActivity(exception?.GetType().Name ?? @"Error", ActivityKind.Internal);
            if (activity != null)
            {
                activity.AddException(exception);
                activity.SetStatus(ActivityStatusCode.Error, exception?.Message);
                if (properties != null)
                    foreach (var kvp in properties)
                        activity.SetTag(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Flushes pending telemetry and disposes the tracer provider.
        /// </summary>
        public static void Shutdown()
        {
            _tracerProvider?.Dispose();
            _tracerProvider = null;
        }
    }
}
