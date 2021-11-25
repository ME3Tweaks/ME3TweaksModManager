using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.objects;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [Localizable(false)]
    [TestClass]
    public class TargetTests
    {
        [TestMethod]
        public void ValidateTargetsME1()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME1);
            Console.WriteLine("TargetTesting: Game directories folder for ME1: " + root);
            var normal = Path.Combine(root, "normal");

            //correct game
            GameTarget gt = new GameTarget(MEGame.ME1, normal, false);
            var failureReason = gt.ValidateTarget();
            Assert.IsNull(failureReason, "GameTarget for ME1 Normal should not have returned a failure string when validating against the correct game. Failure reason returned: " + failureReason);
            Assert.IsTrue(gt.IsValid, "GameTarget for ME1 Normal should have been marked as valid when validating against the correct game, but it wasn't");

            //wrong game
            gt = new GameTarget(MEGame.ME2, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME1 Normal should have returned a failure string when validating against the wrong game (ME2), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME1 Normal should have been marked as invalid when validating against the wrong game (ME2), but wasn't");

            gt = new GameTarget(MEGame.ME3, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME1 Normal should have returned a failure string when validating against the wrong game (ME3), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME1 Normal should have been marked as invalid when validating against the wrong game (ME3), but wasn't");
        }

        [TestMethod]
        public void ValidateTargetsME2()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME2);
            Console.WriteLine("TargetTesting: Game directories folder for ME2: " + root);

            var normal = Path.Combine(root, "normal");

            //correct game
            GameTarget gt = new GameTarget(MEGame.ME2, normal, false);
            var failureReason = gt.ValidateTarget();
            Assert.IsNull(failureReason, "GameTarget for ME2 Normal should not have returned a failure string when validating against the correct game. Failure reason returned: " + failureReason);
            Assert.IsTrue(gt.IsValid, "GameTarget for ME2 Normal should have been marked as valid when validating against the correct game, but it wasn't");

            //Wrong games
            gt = new GameTarget(MEGame.ME1, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME2 Normal should have returned a failure string when validating against the wrong game (ME1), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME2 Normal should have been marked as invalid when validating against the wrong game (ME1), but wasn't");

            gt = new GameTarget(MEGame.ME3, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME2 Normal should have returned a failure string when validating against the wrong game (ME3), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME2 Normal should have been marked as invalid when validating against the wrong game (ME3), but wasn't");

        }

        [TestMethod]
        public void ValidateTargetsME3()
        {
            GlobalTest.Init();
            var root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME3);
            Console.WriteLine("TargetTesting: Game directories folder for ME3: " + root);

            var normal = Path.Combine(root, "normal");

            //correct game
            GameTarget gt = new GameTarget(MEGame.ME3, normal, false);
            var failureReason = gt.ValidateTarget();
            Assert.IsNull(failureReason, "GameTarget for ME3 Normal should not have returned a failure string when validating against the correct game. Failure reason returned: " + failureReason);
            Assert.IsTrue(gt.IsValid, "GameTarget for ME3 Normal should have been marked as valid when validating against the correct game, but it wasn't");

            //wrong game
            gt = new GameTarget(MEGame.ME1, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME3 Normal should have returned a failure string when validating against the wrong game (ME1), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME3 Normal should have been marked as invalid when validating against the wrong game (ME1), but wasn't");

            gt = new GameTarget(MEGame.ME2, normal, false);
            failureReason = gt.ValidateTarget();
            Assert.IsNotNull(failureReason, "GameTarget for ME3 Normal should have returned a failure string when validating against the wrong game (ME2), but none was returned");
            Assert.IsFalse(gt.IsValid, "GameTarget for ME3 Normal should have been marked as invalid when validating against the wrong game (ME2), but wasn't");
        }
    }
}
