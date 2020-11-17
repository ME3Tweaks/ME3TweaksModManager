using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.gamefileformats;
using ME3Explorer;

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
                TalkFileME2ME3 talkFileMe2 = new TalkFileME2ME3();
                talkFileMe2.LoadTlkData(tlk);
                var tlkStream = HuffmanCompressionME2ME3.SaveToTlkStream(talkFileMe2.StringRefs);
                var reloadedTlk = new TalkFileME2ME3();
                tlkStream.Position = 0;
                reloadedTlk.LoadTlkDataFromStream(tlkStream);

                foreach (var v in talkFileMe2.StringRefs)
                {
                    var fd = reloadedTlk.findDataById(v.StringID);
                    Assert.AreEqual(v.Data, fd);
                }
            }
        }
    }
}
