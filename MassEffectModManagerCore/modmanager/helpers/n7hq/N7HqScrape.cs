using System.Collections.Generic;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.helpers.n7hq
{
    /// <summary>
    /// What N7HqScraper.js read from the N7HQ profile pages
    /// </summary>
    public class N7HqScrape
    {
        public const string FormatName = "N7HQ-profile-scrape";

        public string Format { get; set; }
        public int Version { get; set; }
        public string ScrapedUtc { get; set; }
        public string Name { get; set; }
        public string Platform { get; set; }
        public N7HqOverview Overview { get; set; }
        public List<N7HqClass> Classes { get; set; } = new();
        public List<N7HqCharacter> Characters { get; set; } = new();
        public List<N7HqInventoryItem> Inventory { get; set; } = new();
        public List<N7HqChallengeGroup> Challenges { get; set; } = new();
        public List<N7HqPastChallenge> PastChallenges { get; set; } = new();

        /// <summary>
        /// Raw page HTML, keyed by page name. Archived, not converted
        /// </summary>
        public Dictionary<string, string> Pages { get; set; }

        public static N7HqScrape FromJson(string json) => JsonConvert.DeserializeObject<N7HqScrape>(json);
    }

    public class N7HqOverview
    {
        public string TimePlayedText { get; set; }
        public int? TimePlayedSeconds { get; set; }
        public int? GamesPlayed { get; set; }
        public int? Credits { get; set; }
        public int? N7Rating { get; set; }
        public int? ChallengePoints { get; set; }
        /// <summary>
        /// Active challenge banner title, and its emblem icon (e.g. emblems/MstrReckoning)
        /// </summary>
        public string BannerText { get; set; }
        public string BannerEmblem { get; set; }
        public string LastUpdated { get; set; }
    }

    public class N7HqClass
    {
        public string Name { get; set; }
        public int? Level { get; set; }
    }

    public class N7HqCharacter
    {
        public string Kit { get; set; }
        public string ClassName { get; set; }
        public bool Deployed { get; set; }
        public int? Level { get; set; }
        public long? Xp { get; set; }
        public int? Promotions { get; set; }
        public List<N7HqPower> Powers { get; set; } = new();
    }

    public class N7HqPower
    {
        /// <summary>
        /// The power icon folder, which matches the game's PowerName
        /// </summary>
        public string Name { get; set; }
        public string Title { get; set; }
        public int? Rank { get; set; }
        public int? MaxRank { get; set; }
        public List<N7HqPowerRow> Rows { get; set; } = new();
    }

    public class N7HqPowerRow
    {
        /// <summary>
        /// 1, 2, 3, 4A, 4B, 5A, 5B, 6A, 6B
        /// </summary>
        public string Step { get; set; }
        public string Title { get; set; }
        public bool Active { get; set; }
    }

    public class N7HqInventoryItem
    {
        /// <summary>
        /// weapons, weapon_mods, inventory_characters, consumables, gear
        /// </summary>
        public string Section { get; set; }
        public string Group { get; set; }
        /// <summary>
        /// Icon path below icons/multiplayer, e.g. guns/AssaultRifle_Avenger
        /// </summary>
        public string Icon { get; set; }
        public bool Owned { get; set; }
        public int? Level { get; set; }
        public int? MaxLevel { get; set; }
        public List<string> Lines { get; set; } = new();
    }

    /// <summary>
    /// A challenge group. Id is the group's accomplishment index (the page's g&lt;id&gt; list)
    /// </summary>
    public class N7HqChallengeGroup
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int TimesCompleted { get; set; }
        public bool Complete { get; set; }
        /// <summary>
        /// [current, goal]
        /// </summary>
        public int[] Progress { get; set; }
        public int? Points { get; set; }
        public string Emblem { get; set; }
        public string EmblemText { get; set; }
        public List<N7HqChallenge> Children { get; set; } = new();
    }

    /// <summary>
    /// A weekend operation from the Past Weekend Challenges list
    /// </summary>
    public class N7HqPastChallenge
    {
        public string Title { get; set; }
        public string Date { get; set; }
        public bool Complete { get; set; }
    }

    public class N7HqChallenge
    {
        public string Title { get; set; }
        public string Description { get; set; }
        /// <summary>
        /// [current, goal]. N7HQ caps current at the goal
        /// </summary>
        public int[] Progress { get; set; }
        public int? Points { get; set; }
        public int? TimesCompleted { get; set; }
        public bool Complete { get; set; }
    }
}
