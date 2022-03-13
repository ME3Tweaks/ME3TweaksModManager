using System;
using System.Collections.Generic;
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
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
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
        private static List<TutorialStep> TutorialSteps { get; set; } = new(); //in case it takes long time to load

        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// Tutorial manifest URLs
        /// </summary>
        private static FallbackLink TutorialServiceManifestURL = new FallbackLink()
        {
            MainURL = @"https://me3tweaks.com/modmanager/services/tutorialservice",
            FallbackURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/master/ME3TweaksModManager/staticfiles/tutorialservice.json"
        };

        /// <summary>
        /// Looks up importing information for mods through the third party mod importing service. This returns all candidates, the client code must determine which is the appropriate value.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for information</param>
        /// <returns>List of candidates</returns>
        public static IReadOnlyCollection<TutorialStep> GetTutorialSteps()
        {
            return TutorialSteps;
        }

        public static bool LoadService(bool forceRefresh = false)
        {
            TutorialSteps = FetchTutorialServiceManifest(forceRefresh);
            ServiceLoaded = true;
            return true;
        }

        /// <summary>
        /// Touches up any existing tutorial assets or downloads missing ones. This thread will block; it should be run on a background thread.
        /// </summary>
        public static void TouchupTutorial()
        {
#if DEBUG
            return; // DONT DO ANYTHING TESTIN
#endif
            var fileRootPath = M3Utilities.GetTutorialServiceCache();
            foreach (var step in TutorialSteps)
            {
                var imagePath = Path.Combine(fileRootPath, step.imagename);
                bool download = !File.Exists(imagePath) || M3Utilities.CalculateMD5(imagePath) != step.imagemd5;
                if (download)
                {
                    foreach (var endpoint in TutorialServiceManifestURL.GetAllLinks())
                    {
                        Uri myUri = new Uri(endpoint);
                        string host = myUri.Host;

                        var fullurl = endpoint + @"tutorial/" + step.imagename;
                        M3Log.Information($@"Downloading {step.imagename} from endpoint {host}");
                        var downloadedImage = MOnlineContent.DownloadToMemory(fullurl, null, step.imagemd5).Result;
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
                            M3Log.Error($@"Unable to download {step.imagename} from endpoint {host}: {downloadedImage.errorMessage}");
                        }
                    }
                }
            }
        }
        

        private static List<TutorialStep> FetchTutorialServiceManifest(bool overrideThrottling = false)
        {
            M3Log.Information(@"Fetching tutorial manifest");
            string cached = null;
            // Read cached first.
            if (File.Exists(M3Utilities.GetTutorialServiceCacheFile()))
            {
                try
                {
                    cached = File.ReadAllText(M3Utilities.GetTutorialServiceCacheFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content" },
                        {@"Service", @"Tutorial Service" },
                        {@"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            // TODO: UNCOMMENT FOR PRODUCTION
            //if (!File.Exists(M3Utilities.GetTutorialServiceCacheFile()) || overrideThrottling || MOnlineContent.CanFetchContentThrottleCheck())
            //{
            //    foreach (var staticurl in TutorialServiceManifestURL.GetAllLinks())
            //    {
            //        Uri myUri = new Uri(staticurl);
            //        string host = myUri.Host;

            //        try
            //        {
            //            using var wc = new ShortTimeoutWebClient();
            //            string json = WebClientExtensions.DownloadStringAwareOfEncoding(wc, staticurl);
            //            File.WriteAllText(M3Utilities.GetTutorialServiceCacheFile(), json);
            //            return JsonConvert.DeserializeObject<List<IntroTutorial.TutorialStep>>(json);
            //        }
            //        catch (Exception e)
            //        {
            //            //Unable to fetch latest help.
            //            M3Log.Error($@"Error fetching latest tutorial service file from endpoint {host}: {e.Message}");
            //        }
            //    }

            //    if (cached == null)
            //    {
            //        M3Log.Error(@"Unable to fetch latest tutorial service file from server and local file doesn't exist. Returning a blank copy.");
            //        return new List<IntroTutorial.TutorialStep>();
            //    }
            //}

            M3Log.Information(@"Using cached tutorial service file");

            try
            {
                return JsonConvert.DeserializeObject<List<TutorialStep>>(cached);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Unable to parse cached tutorial service file: " + e.Message);
                return new List<TutorialStep>();
            }
        }
    }
}
