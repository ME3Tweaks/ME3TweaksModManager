using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Localization;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    public static class M3Localization
    {
        /// <summary>
        /// The internal language codes that Mod Manager supports for UI localization (this does not mean they are implemented!)
        /// </summary>
        public static string[] SupportedLanguages = { @"int", @"pol", @"rus", @"deu", @"fra", @"bra", @"esn", @"kor", @"ita" };

        /// <summary>
        /// The last merged language dictionary - for removing from merged resources if the language changes
        /// </summary>
        private static ResourceDictionary PreviousLocalizationDictionary;
        /// <summary>
        /// The last merged dictionary language
        /// </summary>
        private static string LastLanguage;

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
                var hasUpdatedLoc = M3OnlineContent.HasUpdatedLocalization(lang);
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
                        uriSource = GetCachedLocalizationFile(lang);
                    }
                    else
                    {
                        uriSource = M3OnlineContent.DownloadLocalization(lang) ? GetCachedLocalizationFile(lang) : $@"pack://application:,,,/ME3TweaksModManager;component/modmanager/localizations/{lang}.xaml";
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

            if (PreviousLocalizationDictionary != null && LastLanguage != null && LastLanguage != @"int")
            {
                // Do not uninstall INT - not sure if this might re-use resources if we re-merge int again 
                // but user changing languages is pretty uncommon
                M3Log.Information($@"Uninstalling language dictionary {LastLanguage}");
                Application.Current.Resources.MergedDictionaries.Remove(PreviousLocalizationDictionary);
            }
            PreviousLocalizationDictionary = resourceDictionary;
            LastLanguage = lang;

            M3Log.Information($@"Installing language dictionary {lang}");
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            // Update the core localization.
            LC.SetLanguage(lang);
            return Task.CompletedTask;
        }

        internal static string GetCachedLocalizationFile(string lang) => Path.Combine(M3Filesystem.GetCachedLocalizationFolder(), $@"{lang}-{App.BuildNumber}.xaml");

    }
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
                    var locFile = M3Localization.GetCachedLocalizationFile(lang);
                    if (File.Exists(locFile))
                    {
                        var md5 = MUtilities.CalculateHash(locFile);
                        M3Log.Information($@"Found server livelocalization. HasUpdatedLocalization({lang}) result: {md5 != livelocmd5}");
                        return md5 != livelocmd5;
                    }
                    M3Log.Information($@"Server has localization for {lang}, but we don't have a localization locally stored");
                    return true; //There is online asset but we do not have it locally
                }
            }
            return null; //can't be found or no manifest
        }

        internal static bool DownloadLocalization(string lang)
        {
            if (App.ServerManifest == null)
            {
                M3Log.Error(@"Server manifest is null; cannot download localization");
                return false;
            }
            var livelocmd5 = App.ServerManifest[$@"livelocalization-{lang}"]; // this was checked previously
            var url = LocalizationEndpoint + $@"?lang={lang}&build={App.BuildNumber}";
            var result = M3OnlineContent.DownloadToMemory(url, hash: livelocmd5);
            if (result.errorMessage == null)
            {
                // OK!
                result.result.WriteToFile(M3Localization.GetCachedLocalizationFile(lang));
                M3Log.Information($@"Wrote updated cached localization file for {lang}");
                return true;
            }
            // RIP
            M3Log.Error($@"Error download updated localization file {lang}: {result.result}");
            return false;

        }
    }
}
