using System;
using System.Collections.Generic;
using System.Text;
using MassEffectModManagerCore.modmanager.me3tweaks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class OnlineContentTests
    {

        [TestMethod]
        public void ValidateOnlineFetches()
        {
            GlobalTest.Init();
            var helpItems = OnlineContent.FetchLatestHelp("int", false, true);
            Assert.AreNotEqual(0, helpItems.Count, "FetchLatestHelp failed: No items were parsed, the list is empty!");
            helpItems.Sort();

            var tips = OnlineContent.FetchTipsService(true);
            Assert.AreNotEqual(0, tips.Count, "FetchTipsService failed: No items were parsed, the list is empty!");

            var tpmiService = OnlineContent.FetchThirdPartyImportingService(true);
            Assert.AreNotEqual(0, tpmiService.Count, "FetchThirdPartyImportingService failed: No items were parsed, the list is empty!");

            var bgfis = OnlineContent.FetchBasegameFileIdentificationServiceManifest(true);
            Assert.AreNotEqual(0, bgfis.Count, "FetchBasegameIdentificationServiceManifest failed: No items were parsed, the list is empty!");

            var startupManifest = OnlineContent.FetchOnlineStartupManifest(true);
            Assert.AreNotEqual(0, startupManifest.Count, "FetchOnlineStartupManifest failed: No items were parsed, the list is empty!");

        }
    }
}