using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SevenZip;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]

    public class ModExtractionTests
    {
        //This method just runs some code to make sure no exceptions are hit.
        //In future hopefully this will have code to actually check results.
        [TestMethod]
        public void TestBuildingExtractionQueues()
        {
            GlobalTest.Init();

            Console.WriteLine("Fetching third party services");
            App.ThirdPartyImportingService = OnlineContent.FetchThirdPartyImportingService();
            App.ThirdPartyIdentificationService = OnlineContent.FetchThirdPartyIdentificationManifest();

            var compressedModsDirectory = Path.Combine(GlobalTest.GetTestDataDirectory(), "compressedmods");
            List<Mod> modsFoundInArchive = new List<Mod>();

            void addModCallback(Mod m)
            {
                Console.WriteLine($"Found mod in archive: {m.ModName}");
                modsFoundInArchive.Add(m);
            }

            void failedModCallback(Mod m)
            {
                //Console.WriteLine($"A mod failed to load. This may be expected: {m.ModName}");
            }

            void logMessageCallback(string m)
            {
                Console.WriteLine(m);
            }

            #region Get Targets
            List<GameTarget> targets = new List<GameTarget>();
            var root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME1);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(Mod.MEGame.ME1, d, false, false);
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME2);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(Mod.MEGame.ME2, d, false, false);
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME3);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(Mod.MEGame.ME3, d, false, false);
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            #endregion


            foreach (var archive in Directory.GetFiles(compressedModsDirectory))
            {
                modsFoundInArchive.Clear();
                var realArchiveInfo = GlobalTest.ParseRealArchiveAttributes(archive);
                Console.WriteLine($"Inspecting archive: { archive}");
                ModArchiveImporter.InspectArchive(archive, addModCallback, failedModCallback, logMessageCallback, forcedMD5: realArchiveInfo.md5, forcedSize: realArchiveInfo.size);
                var archiveZ = new SevenZipExtractor(archive);
                foreach (var mod in modsFoundInArchive)
                {
                    mod.GetAllRelativeReferences(archiveZ);
                    var targetsForMod = targets.Where(x => x.Game == mod.Game).ToList();
                    foreach (var target in targetsForMod)
                    {
                        mod.GetInstallationQueues(target);
                    }
                }
            }
        }
    }
}
