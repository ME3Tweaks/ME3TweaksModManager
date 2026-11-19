using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.helpers.n7hq
{
    /// <summary>
    /// Builds an ME3-MP-profile-export document from an N7HQ scrape, in the same SFXOnlineSaveGameMP
    /// and SFXAccomplishmentStorage string formats ME3 saved. Appearance, character names and loadouts aren't on N7HQ.
    /// </summary>
    public static class N7HqProfileConverter
    {
        public const string Source = "N7HQ";
        public const string Trigger = "n7hqScrape";

        // CachedOnlinePlayerStorage.VersionNumber and DevelopmentVersionNumber in ME3's last release
        private const int SaveVersion = 20;
        private const int DevelopmentVersion = 4;

        // GetPlayerVariables2 writes at most this many
        private const int MaxPlayerVariables = 1350;

        // Consumable slots every kit has, with the wheel slots ME3 used for most of them
        private static readonly (string Name, int Wheel)[] ConsumablePowers =
        {
            ("Consumable_Rocket", 0), ("Consumable_Revive", 4), ("Consumable_Shield", 5), ("Consumable_Ammo", 6)
        };

        // Weekend operations to their WEEKLY<n>GROUP index. The groups are all titled "Weekend Challenge" and
        // numbered in run order, starting with Bloodlust (Oct 19 2012). Other operations have no LMP group,
        // and Lodestar/Prophecy/Heartbreaker/Tribute also show up as their own groups
        private static readonly Dictionary<string, int> WeekendOperations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Operation Bloodlust"] = 600,
            ["Operation Valkyrie"] = 602,
            ["Operation Jackhammer"] = 604,
            ["Operation Blast Furnace"] = 620,
            ["Operation Gearhead"] = 622,
            ["Operation Privateer"] = 625,
            ["Operation Detonator"] = 627,
            ["Operation Onslaught"] = 629,
            ["Operation Alamo"] = 631,
            ["Operation Prophecy"] = 633,
            ["Operation Genesis"] = 636,
            ["Operation Geronimo"] = 718,
            ["Operation Firestorm"] = 720,
            ["Operation Nightfall"] = 722,
            ["Operation Heartbreaker"] = 724,
            ["Operation Tribute"] = 726,
            ["Operation Impact"] = 731,
        };

        // Alliance Mastery was cut from ME3 (no accomplishment data anywhere), but N7HQ still lists it.
        // Nobody can have progress on it, so skip it without a warning
        private const int AllianceMasteryGroup = 675;

        private static readonly Regex CountLine = new(@"^(?<name>.*?)(?:\s+(?<tier>IV|III|II|I))?\s+-\s+(?<count>\d+)$", RegexOptions.Compiled);

        public class Result
        {
            public JObject Export { get; init; }
            /// <summary>Things on N7HQ that couldn't be carried over</summary>
            public List<string> Warnings { get; } = new();
        }

        public static Result Convert(N7HqScrape scrape, N7HqGameData data)
        {
            if (scrape?.Format != N7HqScrape.FormatName || scrape.Version != 1)
            {
                throw new InvalidDataException("Expected a version 1 N7HQ profile scrape.");
            }
            if (scrape.Overview?.Credits == null)
            {
                throw new InvalidDataException("The N7HQ overview page had no credits. Make sure you were logged in and viewing your own profile.");
            }
            if (scrape.Characters.Count == 0 || scrape.Inventory.Count == 0)
            {
                throw new InvalidDataException("No characters or inventory were found on N7HQ.");
            }

            var warnings = new List<string>();
            var save = new JObject
            {
                ["base"] = BuildBase(scrape, data, warnings),
                ["classes"] = new JArray(BuildClasses(scrape, warnings).ToArray<object>()),
                ["characters"] = new JArray(BuildCharacters(scrape, data, warnings).ToArray<object>()),
                ["faceCodes"] = "",
                ["newReinforcements"] = ""
            };

            var export = new JObject
            {
                ["format"] = "ME3-MP-profile-export",
                ["version"] = 2,
                ["source"] = Source,
                ["trigger"] = Trigger,
                ["n7hq"] = new JObject
                {
                    ["name"] = scrape.Name,
                    ["platform"] = scrape.Platform,
                    ["scrapedUtc"] = scrape.ScrapedUtc,
                    ["lastUpdated"] = scrape.Overview.LastUpdated
                },
                ["save"] = save,
                ["accomplishments"] = BuildAccomplishments(scrape, data, warnings, out var currentReward),
                // ID 92 is the selected challenge banner
                ["effectiveSettings"] = new JArray(new JObject { ["id"] = 92, ["dataType"] = 1, ["value"] = currentReward })
            };
            var result = new Result { Export = export };
            result.Warnings.AddRange(warnings);
            return result;
        }

        private static string BuildBase(N7HqScrape scrape, N7HqGameData data, List<string> warnings)
        {
            var count = Math.Min(data.MaxPlayerVariableId + 1, MaxPlayerVariables);
            var values = new int[count];
            foreach (var (name, value) in data.DefaultPlayerVariables)
            {
                Set(data.PlayerVariables[name], value);
            }

            foreach (var item in scrape.Inventory)
            {
                foreach (var (variable, value) in InventoryVariables(item, warnings))
                {
                    var id = data.FindPlayerVariable(variable);
                    if (id == null)
                    {
                        warnings.Add($"LMP has no item for {item.Icon} ({string.Join(", ", item.Lines)}).");
                        continue;
                    }
                    Set(id.Value, value);
                }
            }

            var hex = new StringBuilder(count * 2);
            foreach (var value in values)
            {
                hex.Append(Math.Min(value, 255).ToString("x2", CultureInfo.InvariantCulture));
            }

            var o = scrape.Overview;
            // Credits;next pack;copies;credits spent;platform currency spent;games;seconds played;last level up;inventory
            return string.Join(";", SaveVersion, DevelopmentVersion, o.Credits, -1, 0, 0, 0,
                o.GamesPlayed ?? 0, o.TimePlayedSeconds ?? 0, 0, hex);

            void Set(int id, int value)
            {
                if (id >= values.Length)
                {
                    warnings.Add($"Player variable {id} is past the save limit.");
                    return;
                }
                // Never take away something a new profile starts with
                values[id] = Math.Max(values[id], value);
            }
        }

        /// <summary>
        /// Maps an inventory card to the player variable names it sets, without packages
        /// </summary>
        private static IEnumerable<(string Variable, int Value)> InventoryVariables(N7HqInventoryItem item, List<string> warnings)
        {
            if (item.Icon == null)
            {
                warnings.Add($"An N7HQ {item.Section} card had no icon ({string.Join(", ", item.Lines)}).");
                yield break;
            }
            var key = item.Icon[(item.Icon.LastIndexOf('/') + 1)..];
            // Unowned cards have no level; owned ones without a level bar still count as one copy
            var level = item.Owned ? Math.Max(item.Level ?? 1, 1) : 0;
            switch (item.Section)
            {
                case "weapons":
                    yield return ($"SFXWeapon_{key}", level);
                    break;
                case "weapon_mods":
                    yield return ($"SFXWeaponMod_{key}", level);
                    break;
                case "inventory_characters":
                    yield return (key, level);
                    break;
                case "gear":
                    yield return ($"SFXGameEffect_MatchConsumable_{key}_0", level);
                    break;
                case "consumables":
                    foreach (var line in item.Lines)
                    {
                        var m = CountLine.Match(line);
                        if (!m.Success)
                        {
                            warnings.Add($"Couldn't read the count in '{line}'.");
                            continue;
                        }
                        var amount = int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture);
                        if (key.StartsWith("Consumable_", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return ($"SFXPowerCustomActionMP_{key}", amount);
                        }
                        else if (key.StartsWith("MPCapacity_", StringComparison.OrdinalIgnoreCase) || key.Equals("MPRespec", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return (key, amount);
                        }
                        else
                        {
                            // Tiered boosters, I-IV are _0 to _3
                            var tier = m.Groups["tier"].Success ? RomanTier(m.Groups["tier"].Value) : 0;
                            yield return ($"SFXGameEffect_MatchConsumable_{key}_{tier}", amount);
                        }
                    }
                    break;
                default:
                    warnings.Add($"Unknown N7HQ inventory section {item.Section}.");
                    break;
            }
        }

        private static int RomanTier(string roman) => roman switch
        {
            "I" => 0,
            "II" => 1,
            "III" => 2,
            "IV" => 3,
            _ => throw new InvalidDataException($"Unknown consumable tier {roman}.")
        };

        private static IEnumerable<string> BuildClasses(N7HqScrape scrape, List<string> warnings)
        {
            if (scrape.Classes.Count > 8)
            {
                throw new InvalidDataException("N7HQ listed more than 8 classes.");
            }
            foreach (var cls in scrape.Classes)
            {
                // XP and promotions are per class but shown on each character
                var sample = scrape.Characters.FirstOrDefault(c => string.Equals(c.ClassName, cls.Name, StringComparison.OrdinalIgnoreCase) && c.Xp != null);
                if (sample == null)
                {
                    warnings.Add($"No XP was found for the {cls.Name} class.");
                }
                var xp = (sample?.Xp ?? 0).ToString("0.0000", CultureInfo.InvariantCulture);
                yield return string.Join(";", SaveVersion, DevelopmentVersion, cls.Name, cls.Level ?? sample?.Level ?? 1, xp, sample?.Promotions ?? 0);
            }
        }

        private static IEnumerable<string> BuildCharacters(N7HqScrape scrape, N7HqGameData data, List<string> warnings)
        {
            var consumables = ConsumablePowers.Select(c =>
            {
                var id = data.FindPlayerVariable($"SFXPowerCustomActionMP_{c.Name}")
                         ?? throw new InvalidDataException($"LMP has no {c.Name} power.");
                return $"{c.Name} {id} 1.0000 0 0 0 0 0 0 {c.Wheel} False";
            }).ToList();

            foreach (var character in scrape.Characters)
            {
                // The game makes the record the first time a kit is deployed
                if (!character.Deployed)
                {
                    continue;
                }
                if (!data.Kits.TryGetValue(character.Kit ?? "", out var kit))
                {
                    warnings.Add($"LMP has no {character.Kit} kit.");
                    continue;
                }

                var powers = new List<string>();
                foreach (var power in kit.Powers)
                {
                    var scraped = character.Powers.FirstOrDefault(p => string.Equals(p.Name, power.Name, StringComparison.OrdinalIgnoreCase));
                    if (scraped == null)
                    {
                        warnings.Add($"{character.Kit}: {power.Name} wasn't on N7HQ, so it starts unranked.");
                    }
                    powers.Add(PowerString(power, scraped));
                }
                foreach (var extra in character.Powers.Where(p => kit.Powers.All(k => !string.Equals(k.Name, p.Name, StringComparison.OrdinalIgnoreCase))))
                {
                    warnings.Add($"{character.Kit}: {extra.Title} ({extra.Name}) isn't one of this kit's LMP powers.");
                }
                powers.AddRange(consumables);

                // Kit;name;tints, pattern, pattern color, phong, emissive, skin tone;seconds played;timestamp;powers;hotkeys;weapons;mods;deployed;leveled up
                yield return string.Join(";", SaveVersion, DevelopmentVersion, character.Kit, kit.DefaultName.Replace(";", ""),
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    string.Join(",", powers), "", string.Join(",", kit.Weapons), "", "True", "False");
            }
        }

        /// <summary>
        /// Builds the SFXAccomplishmentStorage strings. Completion holds how many times each accomplishment was
        /// completed, progress holds the shared counters, and reward levels unlock banners
        /// </summary>
        private static JObject BuildAccomplishments(N7HqScrape scrape, N7HqGameData data, List<string> warnings, out int currentReward)
        {
            var table = data.Accomplishments ?? throw new InvalidDataException("The N7HQ data has no challenge table.");
            var completion = new int[table.MaxIndex + 1];
            var progress = new int[table.MaxProgressIndex + 1];
            var rewardLevels = new int[table.MaxRewardId + 1];
            currentReward = 0;

            foreach (var group in scrape.Challenges)
            {
                if (!table.Groups.TryGetValue(group.Id, out var groupData))
                {
                    if (group.Id == AllianceMasteryGroup)
                    {
                        continue;
                    }
                    warnings.Add($"LMP has no challenge group {group.Id} ({group.Title}).");
                    continue;
                }

                Set(completion, group.Id, group.TimesCompleted);
                SetMax(progress, groupData.Progress, group.Progress?[0]);
                if (groupData.Reward is { } reward)
                {
                    // A completed group's banner is unlocked even if the group has since reset
                    SetMax(rewardLevels, reward, Math.Max(group.TimesCompleted, group.Complete ? 1 : 0));
                    if (!string.IsNullOrEmpty(group.Emblem) && string.Equals(group.Emblem, scrape.Overview.BannerEmblem, StringComparison.OrdinalIgnoreCase))
                    {
                        currentReward = reward;
                    }
                }

                // Groups of groups (Mass Effect) have their totals on the child groups themselves
                if (groupData.Children.Count == 0)
                {
                    continue;
                }

                var unused = groupData.Children.ToList();
                var unmatched = new List<N7HqChallenge>();
                foreach (var challenge in group.Children)
                {
                    // Titles repeat across tiers (Promotions), so the goal decides. Otherwise page order
                    var matches = unused.Where(c => SameTitle(c.Title, challenge.Title)).ToList();
                    var match = matches.FirstOrDefault(c => c.Goal != null && c.Goal == challenge.Progress?[1]) ?? matches.FirstOrDefault();
                    if (match == null)
                    {
                        unmatched.Add(challenge);
                        continue;
                    }
                    unused.Remove(match);
                    Apply(match, challenge);
                }

                // A few LMP titles differ from N7HQ's. Pair what's left by goal, in order
                foreach (var challenge in unmatched)
                {
                    var match = unused.FirstOrDefault(c => c.Goal != null && c.Goal == challenge.Progress?[1]
                                                           && group.Children.All(p => !SameTitle(c.Title, p.Title)));
                    if (match == null)
                    {
                        warnings.Add($"LMP has no challenge '{challenge.Title}' in the {group.Title} group.");
                        continue;
                    }
                    unused.Remove(match);
                    Apply(match, challenge);
                }
            }

            foreach (var operation in scrape.PastChallenges)
            {
                if (!operation.Complete || !WeekendOperations.TryGetValue(operation.Title ?? "", out var groupIndex)
                    || !table.Groups.TryGetValue(groupIndex, out var groupData))
                {
                    continue;
                }
                Set(completion, groupIndex, Math.Max(1, completion[groupIndex]));
                foreach (var child in groupData.Children)
                {
                    Set(completion, child.Index, Math.Max(1, completion[child.Index]));
                }
            }

            if (currentReward == 0 && !string.IsNullOrEmpty(scrape.Overview.BannerEmblem))
            {
                warnings.Add($"The active banner {scrape.Overview.BannerText} ({scrape.Overview.BannerEmblem}) wasn't found in any challenge group.");
            }

            var version = table.Version.ToString(CultureInfo.InvariantCulture);
            // Completion times aren't on N7HQ. The game only shows them, it doesn't act on them
            var noTimes = string.Join(",", Enumerable.Repeat(0, 250));
            return new JObject
            {
                ["completion"] = $"{version},{scrape.Overview.ChallengePoints ?? 0},{string.Join(",", completion)}",
                ["progress"] = $"{version},{string.Join(",", progress)}",
                ["rewardLevels"] = $"{version},{string.Join(",", rewardLevels)}",
                ["completionTimes"] = noTimes,
                ["completionTimes2"] = noTimes,
                ["completionTimes3"] = noTimes
            };

            void Apply(N7HqChallengeData match, N7HqChallenge challenge)
            {
                Set(completion, match.Index, Math.Max(challenge.TimesCompleted ?? 0, challenge.Complete ? 1 : 0));
                // N7HQ caps progress at each tier's goal, so the highest tier has the best value
                SetMax(progress, match.Progress, challenge.Progress?[0]);
            }

            static bool SameTitle(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

            void Set(int[] values, int index, int value)
            {
                if (index >= 0 && index < values.Length)
                {
                    values[index] = value;
                }
            }

            void SetMax(int[] values, int? index, int? value)
            {
                if (index is { } i && value is { } v && i >= 0 && i < values.Length)
                {
                    values[i] = Math.Max(values[i], v);
                }
            }
        }

        private static string PowerString(N7HqKitPower power, N7HqPower scraped)
        {
            var rank = Math.Clamp(scraped?.Rank ?? 0, 0, 6);
            // Ranks 4-6 store which side was picked: slot 2*(rank-4) for A, +1 for B, holding rank-3
            var evolutions = new int[6];
            foreach (var row in scraped?.Rows ?? Enumerable.Empty<N7HqPowerRow>())
            {
                if (!row.Active || row.Step is not { Length: 2 })
                {
                    continue;
                }
                var stepRank = row.Step[0] - '0';
                if (stepRank is < 4 or > 6 || stepRank > rank)
                {
                    continue;
                }
                evolutions[2 * (stepRank - 4) + (row.Step[1] == 'B' ? 1 : 0)] = stepRank - 3;
            }
            return $"{power.Name} {power.Id} {rank.ToString("0.0000", CultureInfo.InvariantCulture)} {string.Join(" ", evolutions)} 0 True";
        }
    }
}
