using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.ME3Tweaks.Online;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mod = ME3TweaksModManager.modmanager.objects.mod.Mod;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class ModValidationTests
    {

        [TestMethod]
        public void ValidateModLoading()
        {
            GlobalTest.Init();

            // Services require: TPMI
            TPMIService.LoadService(GlobalTest.CombinedServiceData[MCoreServiceLoader.TPMI_SERVICE_KEY]);

            Console.WriteLine("Executing path: " + GlobalTest.GetTestModsDirectory());
            var testingDataPath = GlobalTest.GetTestModsDirectory();
            Assert.IsTrue(Directory.Exists(testingDataPath), "Directory for testing doesn't exist: " + testingDataPath);

            //Force log startup on.


            //Test cases
            Mod aceSlammer = new Mod(Path.Combine(testingDataPath, "Ace Slammer", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(aceSlammer.ValidMod, "Ace slammer didn't parse into a valid mod! Reason: " + aceSlammer.LoadFailedReason);

            Mod diamondDifficulty = new Mod(Path.Combine(testingDataPath, "Diamond Difficulty", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(diamondDifficulty.ValidMod, "Diamond Difficulty didn't parse into a valid mod! Reason: " + diamondDifficulty.LoadFailedReason);

            Mod egm = new Mod(Path.Combine(testingDataPath, "Expanded Galaxy Mod", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(egm.ValidMod, "EGM didn't parse into a valid mod! Reason: " + egm.LoadFailedReason);
            var egmCustDLCJob = egm.GetJob(ModJob.JobHeader.CUSTOMDLC);
            Assert.IsNotNull(egmCustDLCJob, "Could not find EGM Custom DLC job!");
            Assert.AreEqual(2, egm.InstallationJobs.Count, $"EGM: Wrong number of installation jobs for custom dlc! Should be 2, got: {egm.InstallationJobs.Count}");
            Assert.AreEqual(6, egmCustDLCJob.AlternateDLCs.Count, $"EGM: Wrong number of alternate DLC on CustomDLC job! Should be 6, got: {egmCustDLCJob.AlternateDLCs.Count}");
            Assert.AreEqual(1, egmCustDLCJob.AlternateFiles.Count, $"EGM: Wrong number of alternate files on CustomDLC job! Should be 1, got: {egmCustDLCJob.AlternateFiles.Count}");
            var egmBasegameJob = egm.GetJob(ModJob.JobHeader.BASEGAME);
            Assert.IsNotNull(egmBasegameJob, "EGM Basegame job is null when it should exist!");
            Assert.AreEqual(23, egmBasegameJob.FilesToInstall.Count, $"EGM basegame job should install 23 files, but we see {egmBasegameJob.FilesToInstall.Count}");

            Assert.IsNull(egm.GetJob(ModJob.JobHeader.CITADEL), "EGM somehow returned a job for non-existent header!");

            Mod firefight = new Mod(Path.Combine(testingDataPath, "Firefight mod", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(firefight.ValidMod, "Firefight mod didn't parse into a valid mod! Reason: " + firefight.LoadFailedReason);

            //SP Controller Support 2.31 - invalid cause missing ) on moddesc.ini altfiles in customdlc.
            Mod spcontroller = new Mod(Path.Combine(testingDataPath, "SP Controller Support", "moddesc.ini"), MEGame.Unknown);
            Assert.IsFalse(spcontroller.ValidMod, "SP Controller support (wrong parenthesis on customdlc alts!) loaded as a valid mod when it shouldn't!");

            Mod zombiesupercoal = new Mod(Path.Combine(testingDataPath, "Zombie [SuperCoal]", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(zombiesupercoal.ValidMod, "zombie mod supercoal failed to load as a valid mod! Reason: " + zombiesupercoal.LoadFailedReason);

            // Can't find moddesc.ini
            Mod badPath = new Mod(Path.Combine(testingDataPath, "Not A Path", "moddesc.ini"), MEGame.Unknown);
            Assert.IsFalse(badPath.ValidMod, "An invalid/non-existing moddesc.ini somehow loaded as a valid mod!");

            //Same Gender Romances
            Mod sameGender = new Mod(Path.Combine(testingDataPath, "Same-Gender Romances for ME3", "moddesc.ini"), MEGame.Unknown);
            Assert.IsTrue(sameGender.ValidMod, "Same-Gender Romances failed to load as a valid mod!  Reason: " + sameGender.LoadFailedReason);
            var sgCustDLCJob = sameGender.InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC);
            Assert.IsNotNull(sgCustDLCJob, "Could not find Same-Gender Romances Custom DLC job!");
            Assert.AreEqual(1, sameGender.InstallationJobs.Count, $"Same-gender romances: Wrong number of installation jobs! Should be 1, got: {sameGender.InstallationJobs.Count}");

        }

        [TestMethod]
        public void ValidateArchiveModLoading()
        {
#if !AZURE
            Console.WriteLine("ValidateArchiveModLoading() must be run with AZURE compilation flag defined or tests will fail due to dummy data. Test skipped.");
            return;
#endif

            GlobalTest.Init();

            Console.WriteLine("Fetching third party services");
            TPIService.LoadService(GlobalTest.CombinedServiceData);
            TPMIService.LoadService(GlobalTest.CombinedServiceData);

            var compressedModsDirectory = GlobalTest.GetTestingDataDirectoryFor(@"compressedmods");
            List<Mod> modsFoundInArchive = new List<Mod>();
            List<MEMMod> textureModsFoundInArchive = new List<MEMMod>();

            void addModCallback(Mod m)
            {
                Console.WriteLine($"Found mod in archive: {m.ModName}");
                modsFoundInArchive.Add(m);
            }

            void addTextureMod(MEMMod m)
            {
                Console.WriteLine($"Found texture mod in archive: {m.ModName}");
                textureModsFoundInArchive.Add(m);
            }

            void failedModCallback(Mod m)
            {
                Console.WriteLine($"A mod failed to load. This may be expected: {m.ModName}");
            }

            void logMessageCallback(string m)
            {
                Console.WriteLine(m);
            }
            foreach (var archive in Directory.GetFiles(compressedModsDirectory))
            {
                modsFoundInArchive.Clear();
                var realArchiveInfo = GlobalTest.ParseRealArchiveAttributes(archive);
                Console.WriteLine($"Inspecting archive: { archive}");
                ModImport.FindModsInArchive(archive, addModCallback, failedModCallback,addTextureMod, logMessageCallback, forcedMD5: realArchiveInfo.md5, forcedSize: realArchiveInfo.size);
                Assert.AreEqual(realArchiveInfo.nummodsexpected, modsFoundInArchive.Count(x => x.ValidMod), $"{archive} did not parse correct amount of mods.");

                foreach (var v in modsFoundInArchive)
                {
                    var cookedName = v.Game == MEGame.ME3 ? @"CookedPCConsole" : "CookedPC";
                    // Check nothing has FilesToInstall containing two 'CookedPCConsole' items in the string. 
                    // This is fun edge case due to TESTPATCH having two names DLC_TestPatch and TESTPATCH

                    foreach (var mj in v.InstallationJobs)
                    {
                        foreach (var fti in mj.FilesToInstall)
                        {
                            var numAppearances = Regex.Matches(fti.Key, cookedName).Count;
                            if (numAppearances > 1)
                            {
                                Assert.Fail($@"Found more than 1 instance of {cookedName} in FilesToInstall targetpath item {fti.Key}! This indicates queue building was wrong. Mod: {v.ModName}, file {archive}");
                            }
                        }
                    }
                }
            }
        }
    }
}
