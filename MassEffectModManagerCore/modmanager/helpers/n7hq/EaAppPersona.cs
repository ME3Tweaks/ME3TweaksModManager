using System.Globalization;
using System.Text.RegularExpressions;

namespace ME3TweaksModManager.modmanager.helpers.n7hq
{
    /// <summary>
    /// Finds the EA ID of the account signed in to the EA app, which is the PC profile name on N7HQ
    /// </summary>
    internal static class EaAppPersona
    {
        // Log names to look through for the username
        private static readonly string[] LogNames =
        {
            @"EADesktopVerbose.log", @"EADesktopVerbose.bak", @"EASteamLauncherVerbose.log", @"EASteamLauncherVerbose.bak"
        };

        // Filter regexes
        private static readonly Regex NameRegex = new(@"""eaId"":""(?<name>[^""]+)""|<GetProfileResponse\b[^>]*?\sPersona=""(?<name>[^""]+)""", RegexOptions.Compiled);
        private static readonly Regex TimeRegex = new(@"\[(?<time>\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?Z)\]", RegexOptions.Compiled);
        private static readonly Regex ValidName = new(@"^[A-Za-z0-9_\-]{1,35}$", RegexOptions.Compiled);

        /// <summary>
        /// The most recently logged EA ID, or null if the logs don't have one
        /// </summary>
        public static string Find()
        {
            var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Electronic Arts", @"EA Desktop", @"Logs");
            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var name in LogNames)
            {
                var file = Path.Combine(logs, name);
                if (!File.Exists(file))
                {
                    continue;
                }
                try
                {
                    // The EA app keeps these open
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    while (reader.ReadLine() is { } line)
                    {
                        var match = NameRegex.Match(line);
                        if (!match.Success || !ValidName.IsMatch(match.Groups[@"name"].Value))
                        {
                            continue;
                        }
                        var time = TimeRegex.Match(line);
                        var when = time.Success && DateTime.TryParse(time.Groups[@"time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var t) ? t : DateTime.MinValue;
                        if (best == null || when >= bestTime)
                        {
                            best = match.Groups[@"name"].Value;
                            bestTime = when;
                        }
                    }
                }
                catch (Exception e)
                {
                    M3Log.Warning($@"Could not read EA app log {name}: {e.Message}");
                }
            }
            return best;
        }
    }
}
