using System;
using System.IO;
using System.Reflection;
using MassEffectModManager.modmanager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MassEffectModManagerCore.Tests
{
    [TestClass]
    public class UnitTest1
    {
        private string GetTestDirectory() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private string GetTestDataDirectory() => Path.Combine(GetTestDirectory(), "testdata");
        private string GetTestModsDirectory() => Path.Combine(GetTestDataDirectory(), "mods");
        [TestMethod]
        public void ValidateModLoading()
        {
            var testingDataPath = GetTestModsDirectory();
            Assert.IsTrue(Directory.Exists(testingDataPath),"Directory for testing doesn't exist.");

            throw new NotImplementedException("Test case not yet defined");
            //Mod m = new Mod();
        }
    }
}
