using LegendaryExplorerCore.Packages;
using ME3TweaksCore.ME3Tweaks.Online;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.me3tweaks.online;
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

            var dynamicHelp = DynamicHelpService.LoadService(GlobalTest.CombinedServiceData[M3ServiceLoader.DYNAMIC_HELP_SERVICE_KEY]);
            var helpItems = DynamicHelpService.GetHelpItems(@"int");
            Assert.AreNotEqual(0, helpItems.Count, "FetchLatestHelp failed: No items were parsed, the list is empty!");

            var tips = TipsService.LoadService(GlobalTest.CombinedServiceData[M3ServiceLoader.TIPS_SERVICE_KEY]);
            Assert.AreNotEqual(0, TipsService.TipCount, "FetchTipsService failed: No items were parsed, the list is empty!");

            TPIService.LoadService(GlobalTest.CombinedServiceData[M3ServiceLoader.TPI_SERVICE_KEY]);
            Assert.AreNotEqual(0, TPIService.EntryCount, "FetchThirdPartyImportingService failed: No items were parsed, the list is empty!");

            TPMIService.LoadService(GlobalTest.CombinedServiceData[MCoreServiceLoader.TPMI_SERVICE_KEY]);
            Assert.AreNotEqual(0, TPMIService.EntryCount, "Loading TPMI service failed: No items were parsed, the list is empty!");


            BasegameFileIdentificationService.LoadService(GlobalTest.CombinedServiceData[MCoreServiceLoader.BGFI_SERVICE_KEY]);
            Assert.AreNotEqual(0, BasegameFileIdentificationService.GetAllServerEntries().Count, "FetchBasegameIdentificationServiceManifest failed: No items were parsed, the list is empty!");

            var startupManifest = M3OnlineContent.FetchOnlineStartupManifest(true);
            Assert.AreNotEqual(0, startupManifest.Count, "FetchOnlineStartupManifest failed: No items were parsed, the list is empty!");

        }
    }
}