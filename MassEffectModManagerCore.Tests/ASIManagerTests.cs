using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class ASIManagerTests
    {
        [TestMethod]
        public void TestASIManager()
        {
            GlobalTest.Init();
            Random random = new Random();
            Console.WriteLine(@"Loading ASI Manager Manifest");
            ASIManager.LoadManifest();

            var games = new[] { Mod.MEGame.ME1, Mod.MEGame.ME2, Mod.MEGame.ME3 };

            foreach (var game in games)
            {
                var root = GlobalTest.GetTestGameFoldersDirectory(game);
                Console.WriteLine(@"TargetTesting: Game directories folder for ME1: " + root);
                var normal = Path.Combine(root, "normal");
                GameTarget gt = new GameTarget(game, normal, true, false);

                var asiDir = MEDirectories.ASIPath(gt);
                if (Directory.Exists(asiDir))
                {
                    // Clean slate
                    Utilities.DeleteFilesAndFoldersRecursively(asiDir);
                }

                var asisForGame = ASIManager.GetASIModsByGame(game);

                // 1: Test Installs of upgrades of versions
                foreach (var asi in asisForGame)
                {
                    // Install every version of an ASI and then ensure only one ASI of that type exists in the directory.
                    foreach (var v in asi.Versions)
                    {
                        var sourceBools = new bool?[] { true, false, null }; //online, local, let app decide
                        foreach (var sourceBool in sourceBools)
                        {
                            // INSTALL FROM SOURCE
                            Console.WriteLine($@"Install source variable: {sourceBool}");
                            Assert.IsTrue(ASIManager.InstallASIToTarget(v, gt, sourceBool),
                                $"Installation of ASI failed: {v.Name}");
                            Assert.AreEqual(1, Directory.GetFiles(asiDir).Length,
                                "The count of files in the ASI directory is not 1 after install of an ASI!");

                            // Check is installed
                            var installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.AreEqual(1, installedASIs.Count,
                                "The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to 1!");

                            // Check it maps to the correct one.
                            var instASI = installedASIs.First();
                            Assert.AreEqual(v, instASI.AssociatedManifestItem, "The parsed installed ASI does not match the one we fed to ASIManager.InstallASIToTarget()!");

                            // Rename it to something random so the next version has to find it by the hash and not the filename.
                            var newPath = Path.Combine(asiDir, Guid.NewGuid() + ".asi");
                            File.Move(instASI.InstalledPath, newPath, false);

                            // Ensure it still can be found.
                            installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.AreEqual(1, installedASIs.Count, "The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to 1 after renaming the file!");

                            // Make multiple clones, to ensure all old ones get deleted on upgrades.
                            for (int i = 0; i < 5; i++)
                            {
                                var clonePath = Path.Combine(asiDir, instASI.AssociatedManifestItem.InstalledPrefix + i + ".asi");
                                File.Copy(newPath, clonePath, true);
                            }

                            installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.AreEqual(6, installedASIs.Count, "The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to 6 after cloning the file 5 times!");
                        }
                    }

                    var finalASIsPreRandomization = gt.GetInstalledASIs();
                    int randomCount = 0;
                    foreach (var iam in finalASIsPreRandomization)
                    {
                        // Test randomly editing it.
                        byte[] randomData = new byte[256];
                        random.NextBytes(randomData);
                        File.WriteAllBytes(iam.InstalledPath, randomData);
                        randomCount++;

                        var unknownInstalledASIs = gt.GetInstalledASIs().OfType<UnknownInstalledASIMod>().ToList();
                        Assert.AreEqual(randomCount, unknownInstalledASIs.Count, "Writing random bytes to installed ASI made amount of installed ASIs not correct!");

                    }

                    foreach (var v in finalASIsPreRandomization)
                    {
                        // Test uninstall and remove
                        Assert.IsTrue(v.Uninstall(), $"ASI failed to uninstall: {v.InstalledPath}");
                    }
                    Assert.AreEqual(0, Directory.GetFiles(asiDir).Length, "Leftover files remain after uninstalling all ASIs from target");
                }
            }
        }
    }
}
