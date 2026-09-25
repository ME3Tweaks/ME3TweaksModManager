using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class NexusFileInfoTests
    {
        [TestMethod]
        public void TestNexusDownloadPageLinkRoundTrip()
        {
            var domains = new[] { "masseffect", "masseffect2", "masseffect3", "masseffectlegendaryedition" };
            foreach (var domain in domains)
            {
                var original = new NexusFileInfo() { Domain = domain, ModId = 1234, FileId = 56789 };
                var link = original.ToNexusDownloadPageLink();
                var parsed = NexusFileInfo.FromNexusDownloadPageLink(link);
                Assert.IsNotNull(parsed, $"Failed to parse {link}");
                Assert.AreEqual(domain, parsed.Domain, $"Wrong domain for {link}");
                Assert.AreEqual(1234, parsed.ModId, $"Wrong mod id for {link}");
                Assert.AreEqual(56789, parsed.FileId, $"Wrong file id for {link}");

                // Mod manager download parameter should not affect parsing
                parsed = NexusFileInfo.FromNexusDownloadPageLink(link + "&nmm=1");
                Assert.IsNotNull(parsed, $"Failed to parse {link}&nmm=1");
                Assert.AreEqual(56789, parsed.FileId);
            }
        }

        [TestMethod]
        public void TestNexusDownloadPageLinkInvalid()
        {
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink(null));
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink(""));
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink("https://me3tweaks.com/"));
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink("https://nexusmods.com/skyrim/mods/13?tab=files&file_id=6528")); // Not a Mass Effect domain
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink("https://nexusmods.com/masseffectlegendaryedition/mods/13")); // No file id
            Assert.IsNull(NexusFileInfo.FromNexusDownloadPageLink("https://nexusmods.com/masseffectlegendaryedition/mods/13?tab=files")); // No file id
        }

        [TestMethod]
        public void TestBatchModNexusFileInfo()
        {
            // Explicit values take priority over the download link
            var bm = new BatchMod()
            {
                DownloadLink = "https://nexusmods.com/masseffect3/mods/5?tab=files&file_id=10",
                NexusDomain = "masseffectlegendaryedition",
                NexusModId = 13,
                NexusFileId = 6528
            };
            var info = bm.GetNexusFileInfo();
            Assert.AreEqual("masseffectlegendaryedition", info.Domain);
            Assert.AreEqual(13, info.ModId);
            Assert.AreEqual(6528, info.FileId);

            // Older install groups only have the download link
            bm = new BatchMod() { DownloadLink = "https://nexusmods.com/masseffect3/mods/5?tab=files&file_id=10" };
            info = bm.GetNexusFileInfo();
            Assert.AreEqual("masseffect3", info.Domain);
            Assert.AreEqual(5, info.ModId);
            Assert.AreEqual(10, info.FileId);

            // No source
            Assert.IsNull(new BatchMod().GetNexusFileInfo());
        }
    }
}
