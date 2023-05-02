#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class ExperimentsTest
    {
        // Do not run this on azure
#if DEBUG
        [TestMethod]
#endif
        public void RunExperimentTest()
        {
            GlobalTest.Init();

            var me3Files = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3, includeTFCs: true, includeAFCs: true, forceReload: true, gameRootOverride: @"Z:\Mass Effect Builds\ME3\PC\Retail")
                .Where(x => (x.Key.RepresentsPackageFilePath() || x.Key.EndsWith(@".afc")) && x.Key.GetUnrealLocalization() == MELocalization.None).Select(x => x.Key)
                .ToList();
            var le3Files = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE3, includeTFCs: true, includeAFCs: true, forceReload: true, gameRootOverride: @"B:\LegendaryBuilds\2.0.0.48602\ME3")
                .Where(x => (x.Key.RepresentsPackageFilePath() || x.Key.EndsWith(@".afc")) && x.Key.GetUnrealLocalization() == MELocalization.None).Select(x => x.Key)
                .ToList();

            var special = le3Files.Except(me3Files);
            foreach (var specialFile in special)
            {
                Debug.WriteLine(specialFile);
            }


        }
    }
}

#endif