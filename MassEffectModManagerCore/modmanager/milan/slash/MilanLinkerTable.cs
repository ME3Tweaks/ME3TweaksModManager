using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// The net index decisions Project Milan made for the packages copied from LE3 (MilanDelta/linkers.json). Mod Manager
    /// makes no decisions of its own: the slash generator applies this table to the copied packages, and the slash diff
    /// turns the edits into patches. Development input only; clients never read it.
    /// </summary>
    [Localizable(false)]
    internal class MilanLinkerTable
    {
        public const string FileName = "linkers.json";
        public const int SupportedVersion = 1;

        /// <summary>
        /// The guid and net object count a linker declares.
        /// </summary>
        public record LinkerEntry(Guid Guid, int Count);

        /// <summary>
        /// Linkers to declare, by linker name (case-insensitive).
        /// </summary>
        public Dictionary<string, LinkerEntry> Linkers { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Linkers that are server side only: forced export declarations lose their count entirely, file declarations get 0.
        /// </summary>
        public HashSet<string> ServerSideOnly { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Net indexes to set, keyed by linker and linker-qualified object path (see <see cref="OverrideKey"/>).
        /// </summary>
        public Dictionary<string, int> Overrides { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Linkers that have at least one override, so packages' exports can be skipped without building their keys.
        /// </summary>
        public HashSet<string> OverrideLinkers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static string OverrideKey(string linker, string objectKey) => linker + "\n" + objectKey;

        /// <summary>
        /// Loads linkers.json from the MilanDelta folder.
        /// </summary>
        /// <exception cref="Exception">The file is missing, malformed, or not a supported version.</exception>
        public static MilanLinkerTable Load(string milanDeltaFolder)
        {
            var path = Path.Combine(milanDeltaFolder, FileName);
            if (!File.Exists(path))
                throw new Exception($"Net index linker table {path} was not found. Project Milan produces it next to the MilanDelta manifest.");

            LinkerTableFile file;
            try
            {
                file = JsonSerializer.Deserialize<LinkerTableFile>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                throw new Exception($"Net index linker table {path} could not be read: {e.Message}", e);
            }

            if (file == null || file.Version != SupportedVersion)
                throw new Exception($"Net index linker table {path} has version {file?.Version}, but only version {SupportedVersion} is supported.");

            var table = new MilanLinkerTable();
            foreach (var (name, entry) in file.Linkers ?? new())
            {
                Guid guid = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(entry.Guid) && !Guid.TryParse(entry.Guid, out guid))
                    throw new Exception($"Net index linker table: linker {name} has an invalid guid '{entry.Guid}'.");
                table.Linkers[name] = new LinkerEntry(guid, entry.Count);
            }

            foreach (var entry in file.Overrides ?? new())
            {
                if (string.IsNullOrEmpty(entry.Linker) || string.IsNullOrEmpty(entry.Key))
                    throw new Exception("Net index linker table: an override has no linker or key.");
                table.Overrides[OverrideKey(entry.Linker, entry.Key)] = entry.NetIndex;
                table.OverrideLinkers.Add(entry.Linker);
            }

            foreach (var linker in file.ServerSideOnly ?? new())
                table.ServerSideOnly.Add(linker);

            return table;
        }

        private class LinkerTableFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("linkers")]
            public Dictionary<string, LinkerJson> Linkers { get; set; }

            [JsonPropertyName("overrides")]
            public List<OverrideJson> Overrides { get; set; }

            [JsonPropertyName("serverSideOnly")]
            public List<string> ServerSideOnly { get; set; }
        }

        private class LinkerJson
        {
            [JsonPropertyName("guid")]
            public string Guid { get; set; }

            [JsonPropertyName("count")]
            public int Count { get; set; }
        }

        private class OverrideJson
        {
            [JsonPropertyName("linker")]
            public string Linker { get; set; }

            [JsonPropertyName("key")]
            public string Key { get; set; }

            [JsonPropertyName("netindex")]
            public int NetIndex { get; set; }
        }
    }
}
