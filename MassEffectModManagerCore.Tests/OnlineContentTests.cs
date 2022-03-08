using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksModManager.modmanager.me3tweaks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class OnlineContentTests
    {

        [TestMethod]
        public void ValidateOnlineFetches()
        {
            GlobalTest.Init();
            var helpItems = M3OnlineContent.FetchLatestHelp("int", false, true);
            Assert.AreNotEqual(0, helpItems.Count, "FetchLatestHelp failed: No items were parsed, the list is empty!");
            helpItems.Sort();

            var tips = M3OnlineContent.FetchTipsService(true);
            Assert.AreNotEqual(0, tips.Count, "FetchTipsService failed: No items were parsed, the list is empty!");

            var tpmiService = M3OnlineContent.FetchThirdPartyImportingService(true);
            Assert.AreNotEqual(0, tpmiService.Count, "FetchThirdPartyImportingService failed: No items were parsed, the list is empty!");

            BasegameFileIdentificationService.LoadService();
            Assert.AreNotEqual(0, BasegameFileIdentificationService.GetAllServerEntries().Count, "FetchBasegameIdentificationServiceManifest failed: No items were parsed, the list is empty!");

            var startupManifest = M3OnlineContent.FetchOnlineStartupManifest(true);
            Assert.AreNotEqual(0, startupManifest.Count, "FetchOnlineStartupManifest failed: No items were parsed, the list is empty!");

        }
    }
}