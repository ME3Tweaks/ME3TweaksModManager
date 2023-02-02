#if !AZURE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    // For use with JetBrains dotTrace
    [TestClass]
    public class PerfTests
    {
        [TestMethod]
        public void PerfTest()
        {
            TOCCreator.CreateTOCForGame(MEGame.LE3,x=>Debug.WriteLine($"TOC: {x}%"));
        }
    }
}
#endif