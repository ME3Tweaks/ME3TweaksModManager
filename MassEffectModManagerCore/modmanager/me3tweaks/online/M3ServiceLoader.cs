using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ME3TweaksCore.ME3Tweaks.Online;
using ME3TweaksCore.Misc;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.usercontrols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.me3tweaks.online
{
    /// <summary>
    /// Class for handling the various online service fetches
    /// </summary>
    internal class M3ServiceLoader
    {
        /// <summary>
        /// Performs various touchups to assets that use the server manifest
        /// </summary>
        public static void TouchupServerManifest(MainWindow window)
        {
            M3UpdateCheck.CheckManifestForUpdates(window);
            M3ServiceLoader.TouchupMixinPackage();
            ExternalToolLauncher.ToolsCheckedForUpdatesInThisSession.Clear(); // We clear this so it rechecks in the event there's an update

            var hasUpdatedLocalization = M3OnlineContent.HasUpdatedLocalization(App.CurrentLanguage);
            if (hasUpdatedLocalization == true)
            {
                window.SetApplicationLanguageAsync(App.CurrentLanguage, false); //Force update of localization
            }
        }

        /// <summary>
        /// Ensures the MixinPackage is up to date.
        /// </summary>
        public static void TouchupMixinPackage()
        {
            if (!ServerManifest.HasManifest)
                return; // We have nothing to do here.

            try
            {
                //Mixins
                if (ServerManifest.TryGetString(ServerManifest.MIXIN_PACKAGE_MD5, out MixinHandler.ServerMixinHash))
                {
                    if (!MixinHandler.IsMixinPackageUpToDate())
                    {
                        //Download new package.
                        var memoryPackage = M3OnlineContent.DownloadToMemory(MixinHandler.MixinPackageEndpoint, hash: MixinHandler.ServerMixinHash);
                        if (memoryPackage.errorMessage != null)
                        {
                            M3Log.Error(@"Error fetching mixin package: " + memoryPackage.errorMessage);
                        }
                        else
                        {
                            File.WriteAllBytes(MixinHandler.MixinPackagePath, memoryPackage.result.ToArray());
                            M3Log.Information(@"Wrote ME3Tweaks Mixin Package to disk");
                            MixinHandler.LoadME3TweaksPackage();
                        }
                    }
                    else
                    {
                        M3Log.Information(@"ME3Tweaks Mixin Package is up to date");
                        MixinHandler.LoadME3TweaksPackage();
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error fetching mixin package: " + e.Message);
            }
        }

        private static bool FirstContentCheck = true;

        /// <summary>
        /// ME3Tweaks Mod Manager combined service URL
        /// </summary>
        public static FallbackLink CombinedServiceFetchURL = new FallbackLink()
        {
            MainURL = @"https://me3tweaks.com/modmanager/services/combinedservicesfetch",
            FallbackURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/staticfiles/liveservices/services/combinedservices.json",
        };

        public const string TIPS_SERVICE_KEY = @"tipsservice";
        public const string TUTORIAL_SERVICE_KEY = @"tutorialservice";
        public const string BLACKLISTING_SERVICE_KEY = @"blacklistingservice";
        public const string TPI_SERVICE_KEY = @"thirdpartyimportingservice";
        public const string NEXUS_UPDATER_SERVICE_KEY = @"nexusupdaterservice";
        public const string DYNAMIC_HELP_SERVICE_KEY = @"dynamichelp";
        public const string MODDESC_UPDATER_SERVICE_KEY = @"moddescupdaterservice";


        // Mod Manager specific service loaders
        private static Dictionary<string, MCoreServiceLoader.OnlineServiceLoader> M3ServiceLoaders = new()
        {
            { TPI_SERVICE_KEY, TPIService.LoadService },
            { BLACKLISTING_SERVICE_KEY, BlacklistingService.LoadService },
            { TUTORIAL_SERVICE_KEY, TutorialService.LoadService },
            { TIPS_SERVICE_KEY, TipsService.LoadService },
            { NEXUS_UPDATER_SERVICE_KEY, NexusUpdaterService.LoadService },
            { MCoreServiceLoader.ASI_MANIFEST_KEY, ASIManager.LoadService }, // Mod Manager controls loading this service so we use it here.
            { DYNAMIC_HELP_SERVICE_KEY, DynamicHelpService.LoadService }, // This just loads the xml document. The UI must update after init
            { MODDESC_UPDATER_SERVICE_KEY, ModDescUpdaterService.LoadService } 
            // Live Localization? (This is done by startup manifest right?)
        };


        /// <summary>
        /// Loads ME3Tweaks services that depend on the ME3Tweaks server
        /// <param name="bw">background worker that is used when dynamic help is loading to report data to the UI thread via ReportProgress()</param>
        /// </summary>
        public static void LoadServices(BackgroundWorker bw)
        {
            // We cache this here in the event that there's some exception.
            var useCachedContent = FirstContentCheck && !MOnlineContent.CanFetchContentThrottleCheck();
            FirstContentCheck = false; // Ensure this is false after the initial usage

            var messageStart = useCachedContent ? M3L.GetString(M3L.string_loadingME3TweaksServices) : M3L.GetString(M3L.string_refreshingME3TweaksServices);
            var messageEnd = useCachedContent ? M3L.GetString(M3L.string_loadedME3TweaksServices) : M3L.GetString(M3L.string_refreshedME3TweaksServices);

            var bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"FetchCombinedServiceManifest", messageStart, messageEnd);
            var combinedServicesManifest = MCoreServiceLoader.LoadServices(CombinedServiceFetchURL, useCachedContent);

            // ME3Tweaks Mod Manager Specific Service Loaders
            foreach (var serviceLoader in M3ServiceLoaders)
            {
                if (combinedServicesManifest != null)
                {
                    serviceLoader.Value.Invoke(combinedServicesManifest[serviceLoader.Key]);
                }
                else
                {
                    serviceLoader.Value.Invoke(null);
                }
            }

            // Rebuild the localized help menu.
            var helpItemsLoading = DynamicHelpService.GetHelpItems(App.CurrentLanguage);
            bw.ReportProgress(0, helpItemsLoading); // The worker thread that calls this configures the UI based on the data here. This is for UI thread marshalling.

            // Update the tutorial assets
            TutorialService.TouchupTutorial();

            BackgroundTaskEngine.SubmitJobCompletion(bgTask);
        }
    }
}
