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
            M3ServiceLoader.TouchupMixinPackage();

            var hasUpdatedLocalization = M3OnlineContent.HasUpdatedLocalization(App.CurrentLanguage);
            if (hasUpdatedLocalization.HasValue)
            {
                window.SetApplicationLanguage(App.CurrentLanguage, false); //Force update of localization
            }
        }

        /// <summary>
        /// Ensures the MixinPackage is up to date.
        /// </summary>
        public static void TouchupMixinPackage()
        {
            if (App.ServerManifest == null)
                return; // We have nothing to do here.

            try
            {
                //Mixins
                if (App.ServerManifest.TryGetValue(@"mixinpackagemd5", out MixinHandler.ServerMixinHash))
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
        private static FallbackLink CombinedServiceFetchURL = new FallbackLink()
        {
            MainURL = @"https://me3tweaks.com/modmanager/services/combinedservicesfetch",
            //FallbackURL = "",
        };


        // Mod Manager specific service loaders
        private static Dictionary<string, MCoreServiceLoader.OnlineServiceLoader> M3ServiceLoaders = new()
        {
            { @"nexusupdaterservice", NexusUpdaterService.LoadService },

            { @"thirdpartyimportingservice", TPIService.LoadService },
            { @"blacklistingservice", BlacklistingService.LoadService },
            { @"tutorialservice", TutorialService.LoadService },
            // Live Localization? (This is done by startup manifest right?)
        };

        /// <summary>
        /// Loads ME3Tweaks services that depend on the ME3Tweaks server
        /// <param name="firstStartup">If this is a service first startup. </param>
        /// </summary>
        public static void LoadServices(MainWindow mw, BackgroundWorker bw)
        {
            // We cache this here in the event that there's some exception.
            var useCachedContent = FirstContentCheck && !MOnlineContent.CanFetchContentThrottleCheck();
            FirstContentCheck = false; // Ensure this is false after the initial usage

            var bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"FetchCombinedServiceManifest", $"Refreshing content from me3tweaks.com", $"Refreshed content from me3tweaks.com");
            var combinedServicesManifest = MCoreServiceLoader.LoadServices(CombinedServiceFetchURL, useCachedContent);

            // This uses it's own system since it's not designed like other services. It uses xml (good job me) instead of json like the other services.
            ASIManager.LoadManifest(false, !useCachedContent);

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

            /*
            var bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"LoadTutorialService", M3L.GetString(M3L.string_checkingTutorialAssets), M3L.GetString(M3L.string_checkedTutorialAssets));
            TutorialService.LoadService(!firstStartup);
            TutorialService.TouchupTutorial();
            BackgroundTaskEngine.SubmitJobCompletion(bgTask);

            bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"LoadDynamicHelp", M3L.GetString(M3L.string_loadingDynamicHelp), M3L.GetString(M3L.string_loadingDynamicHelp));
            var helpItemsLoading = M3OnlineContent.FetchLatestHelp(App.CurrentLanguage, false, !firstStartup);
            bw.ReportProgress(0, helpItemsLoading);
            BackgroundTaskEngine.SubmitJobCompletion(bgTask);

            bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ThirdPartyServicesFetch", M3L.GetString(M3L.string_loadingThirdPartyServices), M3L.GetString(M3L.string_loadedThirdPartyServices));
            TPMIService.LoadService(!firstStartup);
            BasegameFileIdentificationService.LoadService(!firstStartup);
            TPIService.LoadService(!firstStartup);
            ASIManager.LoadManifest(false, !firstStartup);
            BlacklistingService.LoadService(!firstStartup);

            BackgroundTaskEngine.SubmitJobCompletion(bgTask);

            bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"LoadTipsService", M3L.GetString(M3L.string_loadingTipsService), M3L.GetString(M3L.string_loadedTipsService));
            try
            {
                App.TipsService = M3OnlineContent.FetchTipsService(!firstStartup);
                mw.SetTipsForLanguage();
            }
            catch (Exception e)
            {
                M3Log.Error(@"Failed to load tips service: " + e.Message);
            }*/

            BackgroundTaskEngine.SubmitJobCompletion(bgTask);


        }
    }
}
