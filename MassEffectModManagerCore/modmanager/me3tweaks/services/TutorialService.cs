using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.tutorial;
using ME3TweaksModManager.modmanager.windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using ShortTimeoutWebClient = ME3TweaksCore.Misc.ShortTimeoutWebClient;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Third Party Importing Service, used for importing mods that do not include a moddesc.ini
    /// </summary>
    public class TutorialService
    {
        /// <summary>
        /// The list of loaded tutorial steps
        /// </summary>
        private static List<TutorialStep> Database { get; set; } = new(); //in case it takes long time to load

        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"Tutorial Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetTutorialServiceCacheFile();

        /// <summary>
        /// Tutorial manifest URLs
        /// </summary>
        //private static FallbackLink TutorialServiceManifestURL = new FallbackLink()
        //{
        //    MainURL = @"https://me3tweaks.com/modmanager/services/tutorialservice",
        //    FallbackURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/ME3TweaksModManager/staticfiles/tutorialservice.json"
        //};


        /// <summary>
        /// Gets the tutorial steps
        /// </summary>
        /// <returns>The list of loaded tutorial steps</returns>
        public static IReadOnlyCollection<TutorialStep> GetTutorialSteps()
        {
            return Database;
        }

        /// <summary>
        /// Ensures a specific tutorial step's image is available, downloading if necessary.
        /// </summary>
        /// <param name="step">The tutorial step to ensure has an image</param>
        public static void EnsureStepImageAvailable(TutorialStep step)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.imagename))
                return;

            if (!ServiceLoaded)
                return;

            var fileRootPath = M3Filesystem.GetTutorialServiceCache();
            var imagePath = Path.Combine(fileRootPath, step.imagename);

            // Check if file exists and has correct hash
            if (File.Exists(imagePath) && MUtilities.CalculateHash(imagePath) == step.imagemd5)
                return;

            // Download the image
            foreach (var endpoint in M3OnlineContent.StaticFileBaseEndpoints.GetAllLinks())
            {
                Uri myUri = new Uri(endpoint);
                string host = myUri.Host;

                var fullurl = endpoint + @"tutorial/" + step.imagename;
                M3Log.Information($@"Downloading {step.imagename} from endpoint {host}");
                var downloadedImage = MOnlineContent.DownloadToMemory(fullurl, null, step.imagemd5);
                if (downloadedImage.errorMessage == null)
                {
                    try
                    {
                        downloadedImage.result.WriteToFile(imagePath);
                        M3Log.Information($@"Successfully downloaded tutorial image {step.imagename}");
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($@"Error writing tutorial image {imagePath}: {e.Message}");
                    }

                    return;
                }
                else
                {
                    M3Log.Warning($@"Unable to download tutorial asset {step.imagename} from endpoint {host}: {downloadedImage.errorMessage}");
                }
            }
        }

        public static bool LoadService(JToken data)
        {
            return InternalLoadService(data);
        }

        private static bool InternalLoadService(JToken serviceData)
        {
            // Online first

            if (serviceData != null)
            {
                try
                {
                    Database = serviceData.ToObject<List<TutorialStep>>();
                    ServiceLoaded = true;
#if DEBUG
                    File.WriteAllText(GetServiceCacheFile(), serviceData.ToString(Formatting.Indented));
#else
                    File.WriteAllText(GetServiceCacheFile(), serviceData.ToString(Formatting.None));
#endif
                    M3Log.Information($@"Loaded online {ServiceLoggingName}");
                    return true;
                }
                catch (Exception ex)
                {
                    if (ServiceLoaded)
                    {
                        M3Log.Error($@"Loaded online {ServiceLoggingName}, but failed to cache to disk: {ex.Message}");
                        return true;
                    }
                    else
                    {
                        M3Log.Error($@"Failed to load {ServiceLoggingName}: {ex.Message}");
                        return false;
                    }
                }
            }

            // Use cached if online is not available
            if (File.Exists(GetServiceCacheFile()))
            {
                try
                {
                    var cached = File.ReadAllText(GetServiceCacheFile());
                    Database = JsonConvert.DeserializeObject<List<TutorialStep>>(cached);
                    ServiceLoaded = true;
                    M3Log.Information($@"Loaded cached {ServiceLoggingName}");
                    return true;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Failed to load cached {ServiceLoggingName}: {e.Message}");
                    var relevantInfo = new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content"},
                        {@"Service", ServiceLoggingName},
                        {@"Message", e.Message}
                    };
                    TelemetryInterposer.UploadErrorLog(e, relevantInfo);
                }
            }

            M3Log.Warning($@"Unable to load {ServiceLoggingName} service: No cached content or online content was available to load");
            return false;
        }

        /// <summary>
        /// Touches up any existing tutorial assets or downloads missing ones. This thread will block; it should be run on a background thread.
        /// </summary>
        public static void TouchupTutorial()
        {
#if DEBUG
            //Debug.WriteLine(@"TouchupTutorial() DISABLED IN THIS DEBUG BUILD (see TutorialService.cs!)");
            //return; // DONT DO ANYTHING, TESTIN
#endif
            M3Log.Information(@"TutorialService: Running Touchup");
            if (!ServiceLoaded)
            {
                M3Log.Warning(@"Cannot touchup tutorial: service not loaded.");
                return;
            }
            var fileRootPath = M3Filesystem.GetTutorialServiceCache();
            foreach (var step in Database)
            {
                var imagePath = Path.Combine(fileRootPath, step.imagename);
                bool download = !File.Exists(imagePath) || MUtilities.CalculateHash(imagePath) != step.imagemd5;
                if (download)
                {
                    foreach (var endpoint in M3OnlineContent.StaticFileBaseEndpoints.GetAllLinks())
                    {
                        Uri myUri = new Uri(endpoint);
                        string host = myUri.Host;

                        var fullurl = endpoint + @"tutorial/" + step.imagename;
                        M3Log.Information($@"Downloading {step.imagename} from endpoint {host}");
                        var downloadedImage = MOnlineContent.DownloadToMemory(fullurl, null, step.imagemd5);
                        if (downloadedImage.errorMessage == null)
                        {
                            try
                            {
                                downloadedImage.result.WriteToFile(imagePath);
                            }
                            catch (Exception e)
                            {
                                M3Log.Error($@"Error writing tutorial image {imagePath}: {e.Message}");
                            }

                            break;
                        }
                        else
                        {
                            M3Log.Error($@"Unable to download tutorial asset {step.imagename} from endpoint {host}: {downloadedImage.errorMessage}");
                        }
                    }
                }
            }
        }
    }
}
