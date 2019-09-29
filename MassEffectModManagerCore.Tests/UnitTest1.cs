using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MassEffectModManager.modmanager;
using MassEffectModManagerCore.modmanager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class UnitTest1
    {
        private string GetTestDirectory() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private string GetTestDataDirectory() => Path.Combine(GetTestDirectory(), "testdata");
        private string GetTestModsDirectory() => Path.Combine(GetTestDataDirectory(), "mods");
        [TestMethod]
        public void ValidateModLoading()
        {
            var testingDataPath = GetTestModsDirectory();
            Assert.IsTrue(Directory.Exists(testingDataPath), "Directory for testing doesn't exist.");

            //Force log startup on.
            Settings.LogModStartup = true;

            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            //Test cases
            Mod aceSlammer = new Mod(Path.Combine(testingDataPath, "Ace Slammer", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(aceSlammer.ValidMod, "Ace slammer didn't parse into a valid mod!");

            Mod diamondDifficulty = new Mod(Path.Combine(testingDataPath, "Diamond Difficulty", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(diamondDifficulty.ValidMod, "Diamond Difficulty didn't parse into a valid mod!");

            Mod egm = new Mod(Path.Combine(testingDataPath, "Expanded Galaxy Mod", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(egm.ValidMod, "EGM didn't parse into a valid mod!");
            var egmCustDLCJob = egm.GetJob(ModJob.JobHeader.CUSTOMDLC);
            Assert.IsNotNull(egmCustDLCJob, "Could not find EGM Custom DLC job!");
            Assert.AreEqual(egm.InstallationJobs.Count, 2, $"EGM: Wrong number of installation jobs for custom dlc! Should be 2, got: {egm.InstallationJobs.Count}");
            Assert.AreEqual(egmCustDLCJob.AlternateDLCs.Count, 6, $"EGM: Wrong number of alternate DLC on CustomDLC job! Should be 6, got: {egmCustDLCJob.AlternateDLCs.Count,}");
            Assert.AreEqual(egmCustDLCJob.AlternateFiles.Count, 6, $"EGM: Wrong number of alternate files on CustomDLC job! Should be 1, got: {egmCustDLCJob.AlternateFiles.Count,}");
            var egmBasegameJob = egm.GetJob(ModJob.JobHeader.BASEGAME);
            Assert.IsNotNull(egmBasegameJob, "EGM Basegame job is null when it should exist!");
            Assert.AreEqual(egmBasegameJob.FilesToInstall.Count, 23, $"EGM basegame job should install 23 files, but we see {egmBasegameJob.FilesToInstall.Count}");

            Assert.IsNull(egm.GetJob(ModJob.JobHeader.CITADEL), "EGM somehow returned a job for non-existent header!");

            Mod firefight = new Mod(Path.Combine(testingDataPath, "Firefight mod", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(firefight.ValidMod);

            //SP Controller Support 2.31 - invalid cause missing ) on moddesc.ini altfiles in customdlc.
            Mod spcontroller = new Mod(Path.Combine(testingDataPath, "SP Controller Support", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsFalse(spcontroller.ValidMod, "SP Controller support (wrong parenthesis on customdlc alts!) loaded as a valid mod when it shouldn't!");

            Mod zombiesupercoal = new Mod(Path.Combine(testingDataPath, "Zombie [SuperCoal]", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(zombiesupercoal.ValidMod, "zombie mod supercoal failed to load as a valid mod!");

            // Can't find moddesc.ini
            Mod badPath = new Mod(Path.Combine(testingDataPath, "Not A Path", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsFalse(badPath.ValidMod, "An invalid/non-existing moddesc.ini somehow loaded as a valid mod!");

            //Same Gender Romances
            Mod sameGender = new Mod(Path.Combine(testingDataPath, "Same - Gender Romances for ME3", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(sameGender.ValidMod, "Same-Gender Romances failed to load as a valid mod!");
            var sgCustDLCJob = sameGender.InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC);
            Assert.IsNotNull(sgCustDLCJob, "Could not find Same-Gender Romances Custom DLC job!");
            Assert.AreEqual(sameGender.InstallationJobs.Count, 1, $"Same-gender romances: Wrong number of installation jobs! Should be 1, got: {sameGender.InstallationJobs.Count}");

        }
    }
}
