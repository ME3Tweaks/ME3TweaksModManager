using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using LegendaryExplorerCore.TLK.ME2ME3;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class TLKTests
    {
        [TestMethod]
        public void TestTLKs()
        {
            GlobalTest.Init();

            Console.WriteLine(@"Testing TLK operations");
            var tlksDir = Path.Combine(GlobalTest.FindDirectoryInParentDirectories(GlobalTest.TESTDATA_FOLDER_NAME), "tlk", "me3");

            var tlksToTestOn = Directory.GetFiles(tlksDir, "*.tlk", SearchOption.AllDirectories);
            foreach (var tlk in tlksToTestOn)
            {
                TalkFile talkFileMe2 = new TalkFile();
                talkFileMe2.LoadTlkData(tlk);
                var tlkStream = LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression.SaveToTlkStream(talkFileMe2.StringRefs);
                var reloadedTlk = new TalkFile();
                tlkStream.Position = 0;
                reloadedTlk.LoadTlkDataFromStream(tlkStream);

                foreach (var v in talkFileMe2.StringRefs)
                {
                    var fd = reloadedTlk.FindDataById(v.StringID);

                    if (fd == "\"Male\"") continue; //Male/Female, we don't have way to distinguish these
                    Assert.AreEqual($"\"{v.Data}\"", fd);
                }
            }
        }
    }
}
