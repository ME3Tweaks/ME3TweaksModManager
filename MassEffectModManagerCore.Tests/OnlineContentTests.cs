using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;

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

            TPIService.LoadService(true);
            Assert.AreNotEqual(0, TPIService.EntryCount, "FetchThirdPartyImportingService failed: No items were parsed, the list is empty!");

            BasegameFileIdentificationService.LoadService();
            Assert.AreNotEqual(0, BasegameFileIdentificationService.GetAllServerEntries().Count, "FetchBasegameIdentificationServiceManifest failed: No items were parsed, the list is empty!");

            var startupManifest = M3OnlineContent.FetchOnlineStartupManifest(true);
            Assert.AreNotEqual(0, startupManifest.Count, "FetchOnlineStartupManifest failed: No items were parsed, the list is empty!");

        }
    }
}