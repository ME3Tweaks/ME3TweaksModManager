using System;
using System.Collections.Generic;
using System.Text;
using MassEffectModManagerCore.modmanager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class TargetTests
    {
        [TestMethod]
        public void ValidateTargetsME1()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME1);
        }

        [TestMethod]
        public void ValidateTargetsME2()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME2);
        }

        [TestMethod]
        public void ValidateTargetsME3()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(Mod.MEGame.ME3);
        }
    }
}
