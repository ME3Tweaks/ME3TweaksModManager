using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.helpers.n7hq
{
    /// <summary>
    /// LMP's player variable IDs and kit powers, generated from the game files into N7HqGameData.json
    /// </summary>
    public class N7HqGameData
    {
        private const string ResourceName = @"ME3TweaksModManager.modmanager.helpers.n7hq.N7HqGameData.json";
        private static readonly Lazy<N7HqGameData> embedded = new(() =>
        {
            using var stream = M3Utilities.ExtractInternalFileToStream(ResourceName);
            using var reader = new StreamReader(stream);
            return Load(reader.ReadToEnd());
        });

        public static N7HqGameData Embedded => embedded.Value;

        /// <summary>
        /// Full class name (or plain variable name for kits and capacities) to ID
        /// </summary>
        public Dictionary<string, int> PlayerVariables { get; set; } = new();

        /// <summary>
        /// What a new profile starts with
        /// </summary>
        public Dictionary<string, int> DefaultPlayerVariables { get; set; } = new();

        public Dictionary<string, N7HqKit> Kits { get; set; } = new();

        public N7HqAccomplishments Accomplishments { get; set; }

        private Dictionary<string, int> byShortName;

        public static N7HqGameData Load(string json)
        {
            var data = JsonConvert.DeserializeObject<N7HqGameData>(json);
            data.Kits = new Dictionary<string, N7HqKit>(data.Kits, StringComparer.OrdinalIgnoreCase);
            data.byShortName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, id) in data.PlayerVariables)
            {
                if (!data.byShortName.TryAdd(ShortName(name), id))
                {
                    throw new InvalidDataException($"Player variable {ShortName(name)} is in the N7 HQ data more than once.");
                }
            }
            return data;
        }

        public int MaxPlayerVariableId => PlayerVariables.Values.Max();

        /// <summary>
        /// Finds an ID by class name without its package, e.g. SFXWeapon_AssaultRifle_Avenger. Null if LMP doesn't have it
        /// </summary>
        public int? FindPlayerVariable(string shortName) => byShortName.TryGetValue(shortName, out var id) ? id : null;

        private static string ShortName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name[(dot + 1)..];
        }
    }

    public class N7HqKit
    {
        public string DefaultName { get; set; }
        public List<N7HqKitPower> Powers { get; set; } = new();
        /// <summary>
        /// Player variable IDs of the starting loadout
        /// </summary>
        public List<int> Weapons { get; set; } = new();
    }

    public class N7HqKitPower
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

    /// <summary>
    /// LMP's challenge groups, keyed by group accomplishment index
    /// </summary>
    public class N7HqAccomplishments
    {
        /// <summary>
        /// ChallengeSystemVersionNumber
        /// </summary>
        public int Version { get; set; }
        public int MaxIndex { get; set; }
        public int MaxProgressIndex { get; set; }
        public int MaxRewardId { get; set; }
        public Dictionary<int, N7HqChallengeGroupData> Groups { get; set; } = new();
    }

    public class N7HqChallengeGroupData
    {
        public string Name { get; set; }
        /// <summary>
        /// Progress index of the group's completed-children counter
        /// </summary>
        public int? Progress { get; set; }
        /// <summary>
        /// Banner reward this group grants
        /// </summary>
        public int? Reward { get; set; }
        public List<N7HqChallengeData> Children { get; set; } = new();
    }

    public class N7HqChallengeData
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public int? Goal { get; set; }
        /// <summary>
        /// Progress index of the counter. Tiers of one challenge share it
        /// </summary>
        public int? Progress { get; set; }
    }
}
