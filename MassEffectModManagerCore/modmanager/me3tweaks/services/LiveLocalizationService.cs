using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Localization;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Service handler for loading localizations from ME3Tweaks/cache for the specific build. Allows localization updates outside of releases
    /// </summary>
    public partial class M3OnlineContent
    {

        /// <summary>
        /// Checks if there is an updated localization file on ME3Tweaks. Returns null if the app manifest can't be found or if there is no server listed localization
        /// Returns true the local md5 doesn't match the server one or we don't have it locally.
        /// Returns false if it does match (we have up to date version)
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        public static bool? HasUpdatedLocalization(string lang)
        {
            if (App.ServerManifest != null)
            {
                if (App.ServerManifest.TryGetValue($@"livelocalization-{lang}", out var livelocmd5))
                {
                    var locFile = getCachedLocalizationFile(lang);
                    if (File.Exists(locFile))
                    {
                        var md5 = M3Utilities.CalculateMD5(locFile);
                        M3Log.Information($@"Found server livelocalization. HasUpdatedLocalization({lang}) result: {md5 != livelocmd5}");
                        return md5 != livelocmd5;
                    }
                    M3Log.Information($@"Server has localization for {lang}, but we don't have a localization locally stored");
                    return true; //There is online asset but we do not have it locally
                }
            }
            return null; //can't be found or no manifest
        }

        private static string getCachedLocalizationFile(string lang) => Path.Combine(M3Filesystem.GetCachedLocalizationFolder(), $@"{lang}-{App.BuildNumber}.xaml");

        /// <summary>
        /// Sets the application's strings based on the language. This call should be run on a background thread (or awaited) as it will block if there is a new localization file to fetch.
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="forcedDictionary"></param>
        public static Task InternalSetLanguage(string lang, ResourceDictionary forcedDictionary, bool localAssetsOnly = false)
        {
            string uriSource = null;
            if (forcedDictionary == null)
            {
                var hasUpdatedLoc = HasUpdatedLocalization(lang);
                if (hasUpdatedLoc.HasValue)
                {
                    var hasUpdateOnME3Tweaks = hasUpdatedLoc.Value;
                    if (hasUpdateOnME3Tweaks && localAssetsOnly)
                    {
                        // Load built-in. Do not use a local one if it exists
                        uriSource = $@"pack://application:,,,/ME3TweaksModManager;component/modmanager/localizations/{lang}.xaml";
                    }
                    else if (!hasUpdateOnME3Tweaks)
                    {
                        // We are up to date
                        uriSource = getCachedLocalizationFile(lang);
                    }
                    else
                    {
                        uriSource = DownloadLocalization(lang) ? getCachedLocalizationFile(lang) : $@"pack://application:,,,/ME3TweaksModManager;component/modmanager/localizations/{lang}.xaml";
                    }
                }
                else
                {
                    // Load built-in
                    uriSource = $@"pack://application:,,,/ME3TweaksModManager;component/modmanager/localizations/{lang}.xaml";
                }
            }


            var resourceDictionary = forcedDictionary ?? new ResourceDictionary
            {
                // Pick uri from configuration
                Source = new Uri(uriSource, UriKind.Absolute)
            };
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary); //Todo: Remove non-INT dictionaries. For example if user changes languages a bunch.

            // Update the core localization.
            LC.SetLanguage(lang);

            // Install localization core strings into the WPF application in the event we bind to the strings, I guess.
            // Not really sure if this is necessary once the dictionaries are properly split
            var coreLocalization = LC.GetLocalizationDictionary();
            foreach (var locStr in coreLocalization)
            {
                App.Current.Resources[locStr.Key] = locStr.Value;
            }

            return Task.CompletedTask;
        }

        private static bool DownloadLocalization(string lang)
        {
            var livelocmd5 = App.ServerManifest[$@"livelocalization-{lang}"]; // this was checked previously
            var url = LocalizationEndpoint + $@"?lang={lang}&build={App.BuildNumber}";
            var result =M3OnlineContent.DownloadToMemory(url, hash: livelocmd5);
            if (result.errorMessage == null)
            {
                // OK!
                result.result.WriteToFile(getCachedLocalizationFile(lang));
                M3Log.Information($@"Wrote updated cached localization file for {lang}");
                return true;
            }
            // RIP
            M3Log.Error($@"Error download updated localization file {lang}: {result.result}");
            return false;

        }
    }
}
