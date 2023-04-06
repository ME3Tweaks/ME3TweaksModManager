#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class ExperimentsTest
    {
        [TestMethod]
        public void RunExperimentTest()
        {
            GlobalTest.Init();

            var ancestorPackage = MEPackageHandler.OpenMEPackage(@"C:\Users\mgame\Documents\jjj\BioD_Nor_201BridgeCon_LOC_INT_ANCESTOR.pcc");
            var package1 = MEPackageHandler.OpenMEPackage(@"C:\Users\mgame\Documents\jjj\BioD_Nor_201BridgeCon_LOC_INT_NEWPATCH.pcc");
            var package2 = MEPackageHandler.OpenMEPackage(@"C:\Users\mgame\Documents\jjj\BioD_Nor_201BridgeCon_LOC_INT_HATBOY.pcc");
            var merge = ThreeWayPackageMerge.AttemptMerge(ancestorPackage, package1, package2);

            package2.Save(@"C:\Users\mgame\Documents\jjj\UPD_BioD_Nor_201BridgeCon_LOC_INT_HATBOY.pcc");
        }
    }
}

#endif