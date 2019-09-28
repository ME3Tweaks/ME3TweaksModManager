using System;
using System.IO;
using System.Reflection;
using MassEffectModManager.modmanager;
using MassEffectModManagerCore.modmanager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

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
            Assert.IsTrue(Directory.Exists(testingDataPath), "Directory for testing doesn't exist.");

            //Force log startup on.
            Settings.LogModStartup = true;

            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            //Test cases
            Mod aceSlammer = new Mod(Path.Combine(testingDataPath, "Ace Slammer", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(aceSlammer.ValidMod);

            Mod diamondDifficulty = new Mod(Path.Combine(testingDataPath, "Diamond Difficulty", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(diamondDifficulty.ValidMod);

            Mod egm = new Mod(Path.Combine(testingDataPath, "Expanded Galaxy Mod", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(egm.ValidMod);

            Mod firefight = new Mod(Path.Combine(testingDataPath, "Firefight mod", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(firefight.ValidMod);

            Mod spcontroller = new Mod(Path.Combine(testingDataPath, "SP Controller Support", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(spcontroller.ValidMod);

            Mod zombiesupercoal = new Mod(Path.Combine(testingDataPath, "Zombie [SuperCoal]", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsTrue(zombiesupercoal.ValidMod);

            Mod badPath = new Mod(Path.Combine(testingDataPath, "Not A Path", "moddesc.ini"), Mod.MEGame.Unknown);
            Assert.IsFalse(badPath.ValidMod);
        }
    }
}
