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
                            Assert.IsTrue(ASIManager.InstallASIToTarget(v, gt, sourceBool).Result,
                                $"Installation of ASI failed: {v.Name}");
                            Assert.HasCount(1, Directory.GetFiles(asiDir),
                                "The count of files in the ASI directory is not 1 after install of an ASI!");

                            // Check is installed
                            var installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.HasCount(1, installedASIs,
                                "The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to 1!");

                            // Check it maps to the correct one.
                            var instASI = installedASIs.First();
                            Assert.AreEqual(v, instASI.AssociatedManifestItem, "The parsed installed ASI does not match the one we fed to ASIManager.InstallASIToTarget()!");

                            // Rename it to something random so the next version has to find it by the hash and not the filename.
                            var newPath = Path.Combine(asiDir, Guid.NewGuid() + ".asi");
                            File.Move(instASI.InstalledPath, newPath, false);

                            // Ensure it still can be found.
                            installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.HasCount(1, installedASIs, "The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to 1 after renaming the file!");

                            // Make multiple clones, to ensure all old ones get deleted on upgrades.
                            for (int i = 0; i < numASIEnumerations; i++)
                            {
                                var clonePath = Path.Combine(asiDir, instASI.AssociatedManifestItem.InstalledPrefix + i + ".asi");
                                File.Copy(newPath, clonePath, true);
                            }

                            installedASIs = gt.GetInstalledASIs().OfType<KnownInstalledASIMod>().ToList();
                            Assert.HasCount(numASIEnumerations + 1, installedASIs, $"The amount of installed ASIs as fetched by GameTarget GetInstalledASIs() is not equal to {(numASIEnumerations + 1)} after cloning the file {numASIEnumerations} times!");
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
                        Assert.HasCount(randomCount, unknownInstalledASIs, "Writing random bytes to installed ASI made amount of installed ASIs not correct!");

                    }

                    foreach (var v in finalASIsPreRandomization)
                    {
                        // Test uninstall and remove
                        Assert.IsTrue(v.Uninstall(), $"ASI failed to uninstall: {v.InstalledPath}");
                    }
                    Assert.IsEmpty(Directory.GetFiles(asiDir), "Leftover files remain after uninstalling all ASIs from target");
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

        [TestMethod]
        public void TestManifestExtendedGroupsAndUnknownGames()
        {
            GlobalTest.Init();

            // MilanHost under <extendedgroups>, and a group for a game id this build doesn't know.
            var manifest = @"<ASIManifest>
<updategroup groupid='424243' game='4'><asimod><name>LE1 Test ASI</name><installedname>LE1Test</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>22222222222222222222222222222222</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink></asimod></updategroup>
<updategroup groupid='777777' game='99'><asimod><name>Future Game ASI</name><installedname>FutureTest</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>33333333333333333333333333333333</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink></asimod></updategroup>
<extendedgroups>
<updategroup groupid='501' game='8'><asimod><name>MilanHost</name><installedname>MilanHost</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>44444444444444444444444444444444</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink></asimod></updategroup>
</extendedgroups>
</ASIManifest>";

            WithIsolatedManifestCache(() =>
            {
                ASIManager.LoadManifest(forceLocal: false, preloadedManifestData: manifest);

                // Unknown game id is skipped instead of failing the whole manifest
                Assert.IsTrue(ASIManager.GetASIModsByGame(MEGame.LE1).Any(x => x.UpdateGroupId == 424243), "Manifest should still load when it contains an unknown game id.");
                Assert.IsFalse(ASIManager.AllASIMods.Any(x => x.UpdateGroupId == 777777), "Group for an unknown game id should be skipped.");

                // <extendedgroups> is read, and nothing is added on top of the manifest
                var milanHost = ASIManager.GetASIModsByGame(MEGame.LMP).SingleOrDefault(x => x.UpdateGroupId == 501);
                Assert.IsNotNull(milanHost, "MilanHost should be read from <extendedgroups>.");
                Assert.HasCount(1, milanHost.Versions, "MilanHost should only have the manifest's versions.");
                Assert.IsFalse(ASIManager.GetASIModsByGame(MEGame.ME3).Any(x => x.UpdateGroupId == 499), "No ASI groups should be defined in code anymore.");

                // No game should have the same group id twice
                foreach (var game in new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3, MEGame.LMP, MEGame.LELauncher })
                {
                    var dupes = ASIManager.GetASIModsByGame(game).GroupBy(x => x.UpdateGroupId).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
                    Assert.IsEmpty(dupes, $"{game} has duplicate ASI groups: {string.Join(',', dupes)}");
                }
            });
        }

        [TestMethod]
        public void TestStaleCacheIsReplacedByNewerEmbeddedManifest()
        {
            GlobalTest.Init();

            var staleManifest = @"<ASIManifest><updategroup groupid='626262' game='4'><asimod><name>Stale Test ASI</name><installedname>StaleTest</installedname><author>ME3Tweaks</author><version>1</version><description>test</description><hash>55555555555555555555555555555555</hash><sourcecode>https://example.com/source</sourcecode><downloadlink>https://example.com/download</downloadlink></asimod></updategroup></ASIManifest>";

            WithIsolatedManifestCache(() =>
            {
                // Cache older than the embedded manifest gets replaced
                File.WriteAllText(ASIManager.ManifestLocation, staleManifest);
                File.SetLastWriteTimeUtc(ASIManager.ManifestLocation, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                ASIManager.LoadManifest(forceLocal: true);

                Assert.IsFalse(ASIManager.AllASIMods.Any(x => x.UpdateGroupId == 626262), "Stale cached manifest should not be used when the embedded one is newer.");
                Assert.IsTrue(ASIManager.GetASIModsByGame(MEGame.LMP).Any(x => x.UpdateGroupId == 501), "Embedded manifest should be used when it is newer than the cache.");
                Assert.IsFalse(File.ReadAllText(ASIManager.ManifestLocation).Contains(@"626262"), "Stale cache file should be overwritten with the embedded manifest.");

                // Cache newer than the embedded manifest is kept
                File.WriteAllText(ASIManager.ManifestLocation, staleManifest);
                ASIManager.LoadManifest(forceLocal: true);
                Assert.IsTrue(ASIManager.AllASIMods.Any(x => x.UpdateGroupId == 626262), "Cached manifest newer than the embedded one should be used.");
            });
        }

        /// <summary>
        /// Runs a manifest test without touching the real cached/staged manifest files.
        /// </summary>
        private static void WithIsolatedManifestCache(Action test)
        {
            var backupManifestPath = ASIManager.ManifestLocation + @".bak_test";
            var backupStagedPath = ASIManager.StagedManifestLocation + @".bak_test";
            var hadManifest = File.Exists(ASIManager.ManifestLocation);
            var hadStaged = File.Exists(ASIManager.StagedManifestLocation);

            try
            {
                if (hadManifest)
                {
                    File.Move(ASIManager.ManifestLocation, backupManifestPath, true);
                }

                if (hadStaged)
                {
                    File.Move(ASIManager.StagedManifestLocation, backupStagedPath, true);
                }

                test();
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

                if (hadManifest)
                {
                    File.Move(backupManifestPath, ASIManager.ManifestLocation, true);
                }

                if (hadStaged)
                {
                    File.Move(backupStagedPath, ASIManager.StagedManifestLocation, true);
                }

                // Put the real manifest back for any test that runs after this one
                ASIManager.LoadManifest(forceLocal: true);
            }
        }
    }
}
