using System.IO.Compression;
using System.Text;
using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.helpers.n7hq
{
    /// <summary>
    /// Reads a profile from N7HQ and writes it to LMP's MP_Profile
    /// </summary>
    internal static class N7HqProfileImport
    {
        /// <summary>
        /// Shows the N7HQ browser, archives what it read, then converts it. Must run on the UI thread
        /// </summary>
        public static void Import(Window owner)
        {
            var browser = new N7HqImportWindow { Owner = owner };
            if (browser.ShowDialog() != true || browser.Result == null)
            {
                return;
            }

            string archivePath = null;
            try
            {
                // Archive first, so the data survives even if conversion fails or N7HQ goes away
                archivePath = Archive(browser.Result, browser.ResultJson);
                var converted = N7HqProfileConverter.Convert(browser.Result, N7HqGameData.Embedded);
                var exportJson = converted.Export.ToString(Formatting.Indented);
                AddToArchive(archivePath, @"ME3_MP_Profile.json", exportJson);
                var profileBytes = Me3MpProfileConverter.Convert(exportJson);

                foreach (var warning in converted.Warnings)
                {
                    M3Log.Warning($@"N7HQ import: {warning}");
                }

                var o = browser.Result.Overview;
                var summary = $"Profile: {browser.Result.Name} ({browser.Result.Platform})\nCredits: {o.Credits:N0}\nCharacters: {browser.Result.Characters.Count(c => c.Deployed)} deployed\n\nThe N7HQ pages were saved to:\n{archivePath}";
                if (converted.Warnings.Count > 0)
                {
                    summary += $"\n\n{converted.Warnings.Count} item(s) couldn't be carried over. They are listed in the Mod Manager log.";
                }
                var exists = File.Exists(LMPDirectory.LocalProfilePath);
                var prompt = exists
                    ? $"{summary}\n\nOverwrite the existing LMP MP_Profile? A backup will be kept beside it."
                    : $"{summary}\n\nWrite this as your LMP MP_Profile?";
                if (M3L.ShowDialog(owner, prompt, "Import N7HQ multiplayer profile", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                var destination = LmpProfileImport.WriteProfile(profileBytes);
                M3Log.Information($@"Imported N7HQ profile {browser.Result.Name} ({browser.Result.Platform}) to {destination}");
                M3L.ShowDialog(owner, $"N7HQ profile converted successfully to:\n\n{destination}", "Profile import complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, @"Failed to import N7HQ multiplayer profile");
                M3L.ShowDialog(owner, $"The N7HQ profile could not be converted: {e.Message}" +
                    (archivePath == null ? "" : $"\n\nWhat was read from N7HQ is saved at:\n{archivePath}"),
                    "Profile import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Saves the scrape and the raw pages to a zip in the profile import cache
        /// </summary>
        private static string Archive(N7HqScrape scrape, string scrapeJson)
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ME3TweaksModManager", @"ME3ProfileImports");
            Directory.CreateDirectory(cacheDir);
            var safeName = string.Concat((scrape.Name ?? @"profile").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var path = Path.Combine(cacheDir, $@"N7HQ_{safeName}_{scrape.Platform}_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.zip");

            var withoutPages = JObject.Parse(scrapeJson);
            withoutPages.Remove(@"pages");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            Write(zip, @"scrape.json", withoutPages.ToString(Formatting.Indented));
            foreach (var (page, html) in scrape.Pages ?? new Dictionary<string, string>())
            {
                Write(zip, $@"pages/{page}.html", html);
            }
            return path;
        }

        private static void AddToArchive(string archivePath, string entryName, string text)
        {
            using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update);
            Write(zip, entryName, text);
        }

        private static void Write(ZipArchive zip, string entryName, string text)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
    }
}
