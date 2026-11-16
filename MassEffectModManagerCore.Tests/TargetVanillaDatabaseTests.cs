using System;
using System.IO;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class TargetVanillaDatabaseTests
    {
        [TestMethod]
        [DataRow(VanillaHashAlgorithm.SHA256)]
        [DataRow(VanillaHashAlgorithm.MD5)]
        public void RoundTripsAndChecksFiles(VanillaHashAlgorithm algorithm)
        {
            var root = Path.Combine(Path.GetTempPath(), $"VanillaDBTest_{Guid.NewGuid():N}");
            try
            {
                var rel = @"BioGame\CookedPCConsole\Test.pcc";
                var path = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
                var hashName = algorithm == VanillaHashAlgorithm.SHA256 ? "sha256" : null;

                VanillaDatabaseService.WriteTargetDatabase(root, [(rel, 5, MUtilities.CalculateHash(path, hashName))], algorithm);

                var db = VanillaDatabaseService.LoadTargetDatabase(MEGame.LMP, root);
                Assert.IsNotNull(db);
                Assert.AreEqual(algorithm, db.Algorithm);
                Assert.IsTrue(db.IsFileVanilla(rel, new FileInfo(path), hashCheck: true));

                // Same size, different data
                File.WriteAllBytes(path, [5, 4, 3, 2, 1]);
                Assert.IsTrue(db.IsFileVanilla(rel, new FileInfo(path)));
                Assert.IsFalse(db.IsFileVanilla(rel, new FileInfo(path), hashCheck: true));

                // Rewriting replaces the cached copy
                VanillaDatabaseService.WriteTargetDatabase(root, [(rel, 5, MUtilities.CalculateHash(path, hashName))], algorithm);
                Assert.IsTrue(VanillaDatabaseService.LoadTargetDatabase(MEGame.LMP, root).IsFileVanilla(rel, new FileInfo(path), hashCheck: true));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [TestMethod]
        public void MissingDatabaseIsNull()
        {
            Assert.IsNull(VanillaDatabaseService.LoadTargetDatabase(MEGame.LMP, Path.Combine(Path.GetTempPath(), $"VanillaDBTest_{Guid.NewGuid():N}")));
        }

        [TestMethod]
        public void EmbeddedLegacyDatabaseStillLoads()
        {
            Assert.IsTrue(VanillaDatabaseService.LoadDatabaseFor(MEGame.LE3).Count > 0);
        }
    }
}
