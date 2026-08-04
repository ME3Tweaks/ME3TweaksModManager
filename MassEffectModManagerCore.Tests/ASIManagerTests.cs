using System;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.Online;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmanager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class ASIManagerTests
    {
        private const int numASIEnumerations = 2;

        [TestMethod]
        public void TestASIManager()
        {
#if AZURE
            return; // This doesn't work with updates from web host
#endif
            GlobalTest.Init();
            Random random = new Random();
            Console.WriteLine(@"Loading ASI Manager Manifest");
            ASIManager.LoadService(GlobalTest.CombinedServiceData[MCoreServiceLoader.ASI_MANIFEST_KEY]);

            var games = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 };

            foreach (var game in games)
            {
                var root = GlobalTest.GetTestGameFoldersDirectory(game);
                var normal = Path.Combine(root, "normal");
                GameTarget gt = new GameTarget(game, normal, true, false, isTest: true);

                var asiDir = gt.GetASIPath();
                if (Directory.Exists(asiDir))
                {
                    // Clean slate
                    MUtilities.DeleteFilesAndFoldersRecursively(asiDir);
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
                            if (sourceBool == true)
                            {
                                // Slow it down a bit for server
                                Thread.Sleep(1000);
                            }

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
                            for (int i = 0; i < numASIEnumerations; i++)
                            {
                                var clonePath = Path.Combine(asiDir, instASI.AssociatedManifestItem.InstalledPrefix + i + ".asi");
                                File.Copy(newPath, clonePath, true);
                            }

                            installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.AreEqual(numASIEnumerations + 1, installedASIs.Count, $"The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to {(numASIEnumerations + 1)} after cloning the file {numASIEnumerations} times!");
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

        [TestMethod]
        public void TestManifestLoadOrderPreloadedThenCachedThenEmbedded()
        {
            GlobalTest.Init();

            const int preloadedGroupId = 424242;
            const int cachedGroupId = 525252;
            var preloadedManifest = @"<asimanifest><updategroup groupid='424242' game='4'><asimod><name>Preloaded Test ASI</name><installedname>PreloadedTest</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>c1bc233ee7bbbe2bf00acdaaa1457f2f</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink><beta>0</beta><hidden>0</hidden><devsonly>0</devsonly></asimod></updategroup></asimanifest>";
            var cachedManifest = @"<asimanifest><updategroup groupid='525252' game='4'><asimod><name>Cached Test ASI</name><installedname>CachedTest</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>c1bc233ee7bbbe2bf00acdaaa1457f2f</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink><beta>0</beta><hidden>0</hidden><devsonly>0</devsonly></asimod></updategroup></asimanifest>";

            var backupManifestPath = ASIManager.ManifestLocation + @".bak_test";
            var backupStagedPath = ASIManager.StagedManifestLocation + @".bak_test";

            var hadManifest = File.Exists(ASIManager.ManifestLocation);
            var hadStaged = File.Exists(ASIManager.StagedManifestLocation);

            try
            {
                if (hadManifest)
                {
                    File.Copy(ASIManager.ManifestLocation, backupManifestPath, true);
                }

                if (hadStaged)
                {
                    File.Copy(ASIManager.StagedManifestLocation, backupStagedPath, true);
                }

                if (File.Exists(ASIManager.ManifestLocation))
                {
                    File.Delete(ASIManager.ManifestLocation);
                }

                if (File.Exists(ASIManager.StagedManifestLocation))
                {
                    File.Delete(ASIManager.StagedManifestLocation);
                }

                ASIManager.LoadManifest(forceLocal: false, preloadedManifestData: preloadedManifest);
                Assert.IsTrue(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == preloadedGroupId), "Preloaded manifest should be used when forceLocal is false.");
                Assert.IsFalse(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == cachedGroupId), "Preloaded manifest should not be replaced by cached data when preloaded data is available.");

                File.WriteAllText(ASIManager.ManifestLocation, cachedManifest);
                if (File.Exists(ASIManager.StagedManifestLocation))
                {
                    File.Delete(ASIManager.StagedManifestLocation);
                }

                ASIManager.LoadManifest(forceLocal: false, preloadedManifestData: null);
                Assert.IsTrue(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == cachedGroupId), "Cached manifest should load when no preloaded data is available.");
                Assert.IsFalse(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == preloadedGroupId), "Cached manifest should be preferred over old preloaded content.");

                File.Delete(ASIManager.ManifestLocation);
                ASIManager.LoadManifest(forceLocal: true, preloadedManifestData: preloadedManifest);
                Assert.IsFalse(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == preloadedGroupId), "Force-local load should ignore preloaded manifest and fall back to embedded when cached manifest is unavailable.");
            }
            finally
            {
                if (File.Exists(ASIManager.ManifestLocation))
                {
                    File.Delete(ASIManager.ManifestLocation);
                }

                if (File.Exists(ASIManager.StagedManifestLocation))
                {
                    File.Delete(ASIManager.StagedManifestLocation);
                }

                if (hadManifest && File.Exists(backupManifestPath))
                {
                    File.Copy(backupManifestPath, ASIManager.ManifestLocation, true);
                    File.Delete(backupManifestPath);
                }

                if (hadStaged && File.Exists(backupStagedPath))
                {
                    File.Copy(backupStagedPath, ASIManager.StagedManifestLocation, true);
                    File.Delete(backupStagedPath);
                }
            }
        }
    }
}
