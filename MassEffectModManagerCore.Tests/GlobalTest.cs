using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MassEffectModManagerCore.Tests
{
    public static class GlobalTest
    {
        public static string GetTestDirectory() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string GetTestDataDirectory() => Path.Combine(GetTestDirectory(), "testdata");
        public static string GetTestModsDirectory() => Path.Combine(GetTestDataDirectory(), "mods");
    }
}
