using System;
using System.IO;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Save;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class LempMpProfileConverterTests
    {
        [TestMethod]
        public void ConvertsLempSaveToMilanHostRecords()
        {
            var bytes = LempMpProfileConverter.Convert(SampleSave());
            var file = Path.Combine(Path.GetTempPath(), $"MP_Profile_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(file, bytes);
                Me3MpProfileConverter.ValidateFile(file);
                var s = LocalProfile.DeserializeLocalProfile(file, MEGame.LMP, false, false).ProfileSettings;
                Assert.AreEqual("20;4;base", s[0].DataAsString);
                Assert.AreEqual("20;4;Adept;20;750000.0000;24", s[1].DataAsString);
                Assert.AreEqual("20;4;Soldier;20;1.0000;26", s[2].DataAsString);
                Assert.IsFalse(s.ContainsKey(3));
                Assert.AreEqual("20;4;SoldierKrogan;Grünt;46", s[9].DataAsString);
                Assert.AreEqual("20;", s[87].DataAsString);
                Assert.AreEqual("20;4;13 290", s[88].DataAsString);
                Assert.AreEqual("22,completion", s[89].DataAsString);
                Assert.AreEqual("22,progress", s[90].DataAsString);
                Assert.AreEqual("22,rewards", s[91].DataAsString);
                Assert.AreEqual(116, s[92].DataAsInt);
                Assert.AreEqual("0,t1", s[93].DataAsString);
                Assert.AreEqual("0,t2", s[94].DataAsString);
                Assert.AreEqual("0,t3", s[95].DataAsString);
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        [TestMethod]
        public void RejectsUnknownVersion()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => LempMpProfileConverter.Convert(SampleSave().Replace("version=1", "version=2")));
        }

        [TestMethod]
        public void RejectsUnknownRecordInsteadOfDroppingIt()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => LempMpProfileConverter.Convert(SampleSave() + "newthing=1\n"));
        }

        [TestMethod]
        public void RejectsNonIntegerCurrentReward()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => LempMpProfileConverter.Convert(SampleSave().Replace("challenge=116", "challenge=abc")));
        }

        private static string SampleSave() =>
            "# LEMP multiplayer save, written by mp_save.asi.\r\n" +
            "version=1\r\n" +
            "base=20;4;base\r\n" +
            "reinforcements=20;4;13 290\r\n" +
            "facecodes=20;\r\n" +
            "selected=SentinelKrogan\r\n" +
            "class=20;4;Adept;20;750000.0000;24\r\n" +
            "class=20;4;Soldier;20;1.0000;26\r\n" +
            "class=\r\n" +
            "character=20;4;SoldierKrogan;Grünt;46\r\n" +
            "challenge=1\r\n" +
            "challenge=22,completion\r\n" +
            "challenge=22,progress\r\n" +
            "challenge=22,rewards\r\n" +
            "challenge=116\r\n" +
            "challenge=0,t1\r\n" +
            "challenge=0,t2\r\n" +
            "challenge=0,t3\r\n" +
            "gaw=1,100,100,100,100,100\r\n";
    }
}
