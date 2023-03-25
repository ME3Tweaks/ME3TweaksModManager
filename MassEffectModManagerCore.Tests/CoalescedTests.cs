using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.gameini;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class CoalescedTests
    {
        [TestMethod]
        public void TestCoalesced()
        {
            GlobalTest.Init();

            var coalRootPath = GlobalTest.GetTestingDataDirectoryFor("Coalesced");

            var me2Coalesced = Path.Combine(coalRootPath, @"ME2", @"Coalesced.ini");

            var diskData = File.ReadAllBytes(me2Coalesced);
            var fromDisk = new ME2Coalesced(new MemoryStream(diskData));

            // Reserialize
            var stream = new MemoryStream();
            fromDisk.SerializeToMemory(stream);

            // Test reload
            stream.Position = 0;
            var newMe2Coalesced = new ME2Coalesced(stream);

            // testing diff - our code doesn't perfectly reserialize as it was hand written.
            //var reserializeData = stream.ToArray();
            //var isSame = reserializeData.SequenceEqual(diskData);
            //if (!isSame)
            //{
            //    diskData.PrintDiff(reserializeData);
            //    stream.WriteToFile(@"C:\users\public\reserialize.bin");
            //}

            Assert.IsTrue(newMe2Coalesced.Inis.Count == 0xF, @"Serialization of ME2Coalesced did not produce correct number of ini files!");
        }
    }
}
