using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Save;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.helpers.n7hq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class N7HqProfileConverterTests
    {
        [TestMethod]
        public void BuildsSaveStringsFromScrape()
        {
            var data = N7HqGameData.Embedded;
            var result = N7HqProfileConverter.Convert(SampleScrape(), data);
            var save = result.Export["save"];

            // Credits, games and time come from the overview. Avenger is ID 0 at level 10
            var baseParts = ((string)save["base"]).Split(';');
            Assert.AreEqual("20", baseParts[0]);
            Assert.AreEqual("1234", baseParts[2]);
            Assert.AreEqual("55", baseParts[7]);
            Assert.AreEqual("3725", baseParts[8]);
            var vars = baseParts[10];
            Assert.AreEqual((data.MaxPlayerVariableId + 1) * 2, vars.Length);
            Assert.AreEqual("0a", vars[..2]);
            Assert.AreEqual(4, Var(vars, data.FindPlayerVariable("AdeptHumanMale")));
            Assert.AreEqual(57, Var(vars, data.FindPlayerVariable("SFXGameEffect_MatchConsumable_AmmoPower_Warp_0")));
            Assert.AreEqual(3, Var(vars, data.FindPlayerVariable("SFXGameEffect_MatchConsumable_AmmoPower_Warp_3")));
            Assert.AreEqual(5, Var(vars, data.FindPlayerVariable("SFXGameEffect_MatchConsumable_Gear_MeleeDamage_0")));
            // New profile defaults are kept
            Assert.AreEqual(1, Var(vars, data.FindPlayerVariable("SFXWeapon_Pistol_Predator")));
            Assert.AreEqual(2, Var(vars, data.FindPlayerVariable("MPCapacity_Ammo")));

            Assert.AreEqual("20;4;Adept;20;10500000.0000;11", (string)save["classes"][0]);

            // Undeployed kits are left for the game to create
            var characters = save["characters"].Select(c => (string)c).ToList();
            Assert.AreEqual(1, characters.Count);
            var parts = characters[0].Split(';');
            Assert.AreEqual(22, parts.Length);
            Assert.AreEqual("AdeptHumanMale", parts[2]);
            var powers = parts[16].Split(',');
            Assert.IsTrue(powers.Contains("Warp 185 6.0000 0 1 0 2 3 0 0 True"));
            Assert.IsTrue(powers.Contains("MPPassive 206 4.0000 0 1 0 0 0 0 0 True"));
            Assert.IsTrue(powers.Any(p => p.StartsWith("Consumable_Ammo 86 ")));
            Assert.AreEqual("True", parts[20]);
        }

        [TestMethod]
        public void ConvertsToReadableProfile()
        {
            var result = N7HqProfileConverter.Convert(SampleScrape(), N7HqGameData.Embedded);
            var bytes = Me3MpProfileConverter.Convert(result.Export.ToString());
            var file = Path.Combine(Path.GetTempPath(), $"MP_Profile_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(file, bytes);
                Me3MpProfileConverter.ValidateFile(file);
                var profile = LocalProfile.DeserializeLocalProfile(file, MEGame.LMP, false, false);
                StringAssert.StartsWith(profile.ProfileSettings[0].DataAsString, "20;4;1234;");
                StringAssert.StartsWith(profile.ProfileSettings[9].DataAsString, "20;4;AdeptHumanMale;");
                Assert.AreEqual(0, profile.ProfileSettings[92].DataAsInt);
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        [TestMethod]
        public void BuildsChallengeStrings()
        {
            var scrape = SampleScrape();
            scrape.Overview.ChallengePoints = 4321;
            scrape.Overview.BannerEmblem = "MapMstr";
            scrape.Challenges.Add(new N7HqChallengeGroup
            {
                Id = 126, Title = "Map Mastery", TimesCompleted = 8, Complete = true, Progress = new[] { 27, 40 }, Emblem = "MapMstr",
                Children =
                {
                    new N7HqChallenge { Title = "Firebase Ghost: Bronze Waves", Progress = new[] { 100, 100 }, TimesCompleted = 9, Complete = true },
                    new N7HqChallenge { Title = "Firebase Ghost: Silver Waves", Progress = new[] { 40, 100 }, TimesCompleted = 0 }
                }
            });
            // Same title, told apart by the goal
            scrape.Challenges.Add(new N7HqChallengeGroup
            {
                Id = 125, Title = "N7 Mastery", Progress = new[] { 1, 6 },
                Children =
                {
                    new N7HqChallenge { Title = "Promotions", Progress = new[] { 12, 12 }, TimesCompleted = 1, Complete = true },
                    new N7HqChallenge { Title = "Promotions", Progress = new[] { 6, 6 }, TimesCompleted = 1, Complete = true }
                }
            });
            // Cut from ME3 but Milan adds it. N7HQ only lists its one ME3 child
            scrape.Challenges.Add(new N7HqChallengeGroup
            {
                Id = 675, Title = "Alliance Mastery", Progress = new[] { 0, 7 }, Emblem = "MstrAlliance",
                Children = { new N7HqChallenge { Title = "Alliance Infiltrator Waves Completed", Progress = new[] { 50, 200 }, TimesCompleted = 0 } }
            });
            scrape.PastChallenges.Add(new N7HqPastChallenge { Title = "Operation Bloodlust", Complete = true });
            scrape.PastChallenges.Add(new N7HqPastChallenge { Title = "Operation Valkyrie", Complete = false });

            var result = N7HqProfileConverter.Convert(scrape, N7HqGameData.Embedded);
            var acc = result.Export["accomplishments"];
            var completion = Ints((string)acc["completion"]);
            Assert.AreEqual(22, completion[0]);
            Assert.AreEqual(4321, completion[1]);
            int Done(int index) => completion[index + 2];
            Assert.AreEqual(8, Done(126));
            Assert.AreEqual(9, Done(480));
            Assert.AreEqual(0, Done(481));
            Assert.AreEqual(1, Done(470));
            Assert.AreEqual(1, Done(471));
            Assert.AreEqual(1, Done(600));
            Assert.AreEqual(1, Done(601));
            Assert.AreEqual(0, Done(602));

            var group = N7HqGameData.Embedded.Accomplishments.Groups[126];
            var progress = Ints((string)acc["progress"]);
            Assert.AreEqual(27, progress[group.Progress!.Value + 1]);
            Assert.AreEqual(40, progress[group.Children[1].Progress!.Value + 1]);
            // Promotion tiers share a counter
            Assert.AreEqual(12, progress[N7HqGameData.Embedded.Accomplishments.Groups[125].Children[0].Progress!.Value + 1]);
            Assert.AreEqual(50, progress[689 + 1]);

            Assert.AreEqual(8, Ints((string)acc["rewardLevels"])[group.Reward!.Value + 1]);
            Assert.AreEqual(group.Reward, (int)result.Export["effectiveSettings"][0]["value"]);
            Assert.AreEqual(0, result.Warnings.Count, string.Join("\n", result.Warnings));
        }

        private static List<int> Ints(string csv) => csv.Split(',').Select(int.Parse).ToList();

        [TestMethod]
        public void ReportsItemsLmpDoesNotHave()
        {
            var scrape = SampleScrape();
            scrape.Inventory.Add(new N7HqInventoryItem { Section = "weapons", Icon = "guns/AssaultRifle_DoesNotExist", Owned = true, Level = 3, Lines = { "Imaginary Rifle III" } });
            var result = N7HqProfileConverter.Convert(scrape, N7HqGameData.Embedded);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("AssaultRifle_DoesNotExist")));
        }

        [TestMethod]
        public void KeepsLockedKitsLocked()
        {
            var data = N7HqGameData.Embedded;
            var scrape = SampleScrape();
            // levels/5-0, levels/4-2 and the Shaman's missing bar
            scrape.Inventory.Add(new N7HqInventoryItem { Section = "inventory_characters", Icon = "kits/AdeptDrell", Owned = false, Level = 0, MaxLevel = 5, Lines = { "Drell Adept" } });
            scrape.Inventory.Add(new N7HqInventoryItem { Section = "inventory_characters", Icon = "kits/InfiltratorFembot", Owned = true, Level = 2, MaxLevel = 3, Lines = { "Alliance Infiltration Unit" } });
            scrape.Inventory.Add(new N7HqInventoryItem { Section = "inventory_characters", Icon = "kits/AdeptKrogan", Owned = true, Lines = { "Krogan Shaman" } });
            scrape.Inventory.Add(new N7HqInventoryItem { Section = "weapons", Icon = "guns/AssaultRifle_Cobra", Owned = false, Level = 0, MaxLevel = 10, Lines = { "Phaeston I" } });
            // N7HQ calls locked kits deployed too
            scrape.Characters.Add(new N7HqCharacter { Kit = "AdeptDrell", ClassName = "Adept", Deployed = true, Xp = 10500000, Promotions = 11 });
            var result = N7HqProfileConverter.Convert(scrape, data);
            var characters = result.Export["save"]["characters"].Select(c => ((string)c).Split(';')[2]).ToList();
            CollectionAssert.AreEqual(new[] { "AdeptHumanMale" }, characters);
            var vars = ((string)result.Export["save"]["base"]).Split(';')[10];
            Assert.AreEqual(0, Var(vars, data.FindPlayerVariable("AdeptDrell")));
            Assert.AreEqual(2, Var(vars, data.FindPlayerVariable("InfiltratorFembot")));
            Assert.AreEqual(0, Var(vars, data.FindPlayerVariable("AdeptKrogan")));
            Assert.AreEqual(0, Var(vars, data.FindPlayerVariable("SFXWeapon_AssaultRifle_Cobra")));
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Krogan Shaman")));
        }

        [TestMethod]
        public void RejectsEmptyScrape()
        {
            var scrape = SampleScrape();
            scrape.Overview.Credits = null;
            Assert.ThrowsExactly<InvalidDataException>(() => N7HqProfileConverter.Convert(scrape, N7HqGameData.Embedded));
        }

        private static int Var(string hex, int? id) => Convert.ToInt32(hex.Substring(id!.Value * 2, 2), 16);

        private static N7HqScrape SampleScrape() => new()
        {
            Format = N7HqScrape.FormatName,
            Version = 1,
            Name = "Tester",
            Platform = "xbox",
            Overview = new N7HqOverview { Credits = 1234, GamesPlayed = 55, TimePlayedSeconds = 3725 },
            Classes = { new N7HqClass { Name = "Adept", Level = 20 } },
            Characters =
            {
                new N7HqCharacter
                {
                    Kit = "AdeptHumanMale", ClassName = "Adept", Deployed = true, Level = 20, Xp = 10500000, Promotions = 11,
                    Powers =
                    {
                        Power("Warp", 6, "4B", "5B", "6A"),
                        Power("Singularity", 6, "4A", "5A", "6A"),
                        Power("Shockwave", 6, "4A", "5A", "6A"),
                        Power("MPPassive", 4, "4B"),
                        Power("MPMeleePassive", 4, "4B")
                    }
                },
                new N7HqCharacter { Kit = "AdeptHumanFemale", ClassName = "Adept", Deployed = false, Xp = 10500000, Promotions = 11 }
            },
            Inventory =
            {
                new N7HqInventoryItem { Section = "weapons", Icon = "guns/AssaultRifle_Avenger", Owned = true, Level = 10, Lines = { "M-8 Avenger X" } },
                new N7HqInventoryItem { Section = "inventory_characters", Icon = "kits/AdeptHumanMale", Owned = true, Level = 4, Lines = { "Male Human Adept" } },
                new N7HqInventoryItem { Section = "consumables", Icon = "consumables/AmmoPower_Warp", Owned = true, Lines = { "Warp Rounds I - 57", "Warp Rounds II - 0", "Warp Rounds III - 1", "Warp Rounds IV - 3" } },
                new N7HqInventoryItem { Section = "consumables", Icon = "consumables/Consumable_Ammo", Owned = true, Lines = { "Thermal Clip Pack - 249" } },
                new N7HqInventoryItem { Section = "gear", Icon = "mods/Gear_MeleeDamage", Owned = true, Level = 5, Lines = { "Hydraulic Joints V" } }
            }
        };

        private static N7HqPower Power(string name, int rank, params string[] picks)
        {
            var power = new N7HqPower { Name = name, Title = name, Rank = rank, MaxRank = 6 };
            foreach (var step in new[] { "1", "2", "3", "4A", "4B", "5A", "5B", "6A", "6B" })
            {
                power.Rows.Add(new N7HqPowerRow { Step = step, Active = step.Length == 1 || picks.Contains(step) });
            }
            return power;
        }
    }
}
