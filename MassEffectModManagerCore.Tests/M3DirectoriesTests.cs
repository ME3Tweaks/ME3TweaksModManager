using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.objects;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Targets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class M3DirectoriesTests
    {
        [TestMethod]
        public void TestM3DirectoryResults()
        {
            GlobalTest.Init();
            List<GameTarget> targets = new List<GameTarget>();
            var root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME1);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(MEGame.ME1, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME2);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(MEGame.ME2, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }
            root = GlobalTest.GetTestGameFoldersDirectory(MEGame.ME3);
            foreach (var d in Directory.GetDirectories(root))
            {
                GameTarget gt = new GameTarget(MEGame.ME3, d, false, false);
                gt.ValidateTarget();
                if (gt.IsValid)
                {
                    targets.Add(gt);
                }
            }

            foreach (var target in targets)
            {
                string expectedDLCPath;
                string expectedASIPath;
                string expectedBioGamePath;
                string expectedCookedPath;
                string expectedExecutableDir;
                if (target.Game == MEGame.ME1)
                {
                    expectedDLCPath = Path.Combine(target.TargetPath, @"DLC");
                    expectedASIPath = Path.Combine(target.TargetPath, @"Binaries", @"asi");
                    expectedBioGamePath = Path.Combine(target.TargetPath, @"BioGame");
                    expectedCookedPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPC");
                    expectedExecutableDir = Path.Combine(target.TargetPath, @"Binaries");
                }
                else if (target.Game == MEGame.ME2)
                {
                    expectedDLCPath = Path.Combine(target.TargetPath, @"BioGame", @"DLC");
                    expectedASIPath = Path.Combine(target.TargetPath, @"Binaries", @"asi");
                    expectedBioGamePath = Path.Combine(target.TargetPath, @"BioGame");
                    expectedCookedPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPC");
                    expectedExecutableDir = Path.Combine(target.TargetPath, @"Binaries");
                }
                else
                {
                    expectedDLCPath = Path.Combine(target.TargetPath, @"BIOGame", @"DLC");
                    expectedASIPath = Path.Combine(target.TargetPath, @"Binaries", @"Win32", @"asi");
                    expectedBioGamePath = Path.Combine(target.TargetPath, @"BIOGame");
                    expectedCookedPath = Path.Combine(target.TargetPath, @"BIOGame", @"CookedPCConsole");
                    expectedExecutableDir = Path.Combine(target.TargetPath, @"Binaries", @"Win32");
                }

                Assert.AreEqual(expectedDLCPath, M3Directories.GetDLCPath(target));
                Assert.AreEqual(expectedASIPath, M3Directories.GetASIPath(target));
                Assert.AreEqual(expectedBioGamePath, M3Directories.GetBioGamePath(target));
                Assert.AreEqual(expectedCookedPath, M3Directories.GetCookedPath(target));
                Assert.AreEqual(expectedExecutableDir, M3Directories.GetExecutableDirectory(target));
            }
        }
    }
}
