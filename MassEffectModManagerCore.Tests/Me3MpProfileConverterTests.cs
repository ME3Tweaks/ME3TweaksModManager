using System;
using System.IO;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Save;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class Me3MpProfileConverterTests
    {
        [TestMethod]
        public void ConvertsLobbyExportToMilanHostRecords()
        {
            var export = SampleExport();
            var bytes = Me3MpProfileConverter.Convert(export.ToString());
            var file = Path.Combine(Path.GetTempPath(), $"MP_Profile_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(file, bytes);
                Me3MpProfileConverter.ValidateFile(file);
                var profile = LocalProfile.DeserializeLocalProfile(file, MEGame.LMP, false, false);
                Assert.AreEqual("base-data", profile.ProfileSettings[0].DataAsString);
                Assert.AreEqual("cached-class", profile.ProfileSettings[1].DataAsString);
                Assert.AreEqual("kit-É", profile.ProfileSettings[9].DataAsString);
                Assert.AreEqual("reward-levels", profile.ProfileSettings[91].DataAsString);
                Assert.AreEqual(7, profile.ProfileSettings[92].DataAsInt);
                Assert.AreEqual("time-1", profile.ProfileSettings[93].DataAsString);
                Assert.AreEqual("time-2", profile.ProfileSettings[94].DataAsString);
                Assert.AreEqual("time-3", profile.ProfileSettings[95].DataAsString);
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        [TestMethod]
        public void RejectsUnsupportedSnapshot()
        {
            var export = SampleExport();
            export["trigger"] = "LoadCompleted";
            Assert.ThrowsExactly<InvalidDataException>(() => Me3MpProfileConverter.Convert(export.ToString()));
        }

        [TestMethod]
        public void RejectsMissingCurrentRewardInsteadOfDiscardingIt()
        {
            var export = SampleExport();
            ((JArray)export["effectiveSettings"]).RemoveAt(3);
            Assert.ThrowsExactly<InvalidDataException>(() => Me3MpProfileConverter.Convert(export.ToString()));
        }

        private static JObject SampleExport() => new()
        {
            ["format"] = "ME3-MP-profile-export",
            ["version"] = 2,
            ["source"] = "ME3-OT",
            ["trigger"] = "mpLobbyCachedSave",
            ["save"] = new JObject
            {
                ["base"] = "base-data", ["classes"] = new JArray("fallback-class"),
                ["characters"] = new JArray("kit-É"), ["faceCodes"] = "",
                ["newReinforcements"] = ""
            },
            ["accomplishments"] = new JObject
            {
                ["completion"] = "completed", ["progress"] = "progress",
                ["rewardLevels"] = "reward-levels", ["completionTimes"] = "time-1",
                ["completionTimes2"] = "time-2", ["completionTimes3"] = "time-3"
            },
            ["effectiveSettings"] = new JArray(
                new JObject { ["id"] = 0, ["dataType"] = 4, ["value"] = "base-data" },
                new JObject { ["id"] = 1, ["dataType"] = 4, ["value"] = "cached-class" },
                new JObject { ["id"] = 9, ["dataType"] = 4, ["value"] = "" },
                new JObject { ["id"] = 92, ["dataType"] = 1, ["value"] = 7 })
        };
    }
}
