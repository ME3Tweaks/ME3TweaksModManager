using System;
using System.Collections.Generic;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.objects.nexusfiledb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class NexusDBTests
    {
        [TestMethod]
        public void TestNexusDB()
        {
            // Fetch the latest DB
            var downloadResult = OnlineContent.DownloadME3TweaksStaticAsset(@"nexusfiledb.zip");
            if (downloadResult.errorMessage != null)
            {
                Assert.Fail($@"Nexus DB could not be downloaded: {downloadResult.errorMessage}");
            }

            // Test the DB
            var searchGames = new List<string>();
            searchGames.Add(@"masseffect");
            searchGames.Add(@"masseffect2");
            searchGames.Add(@"masseffect3");

            foreach (var domain in searchGames)
            {
                var db = GameDatabase.LoadDatabase(domain, downloadResult.download);

                foreach (var instanceId in db.FileInstances)
                {
                    foreach (var Instance in instanceId.Value)
                    {
                        string resolvedName;
                        if (Instance.ParentPathID == 0)
                        {
                            resolvedName = db.NameTable[instanceId.Key];
                        }
                        else
                        {
                            resolvedName = db.Paths[Instance.ParentPathID].GetFullPath(db, db.NameTable[instanceId.Key]);
                        }

                        // DB does not ship to ME3Tweaks with the debug entries. So we cannot debug test the results.
                        // We will just make sure the lookup code does not throw an exception.
                        //string expectedName = Instance.DebugFullName;
                        //Assert.IsTrue(resolvedName.Equals(expectedName, StringComparison.InvariantCultureIgnoreCase), $@"Wrong name resolved in NexusDB! Expected: {expectedName} Got: {resolvedName}");
                    }
                }
            }
        }
    }
}
