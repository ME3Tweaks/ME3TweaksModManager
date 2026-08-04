using System;
using System.IO;
using System.Linq;
using IniParser.Model.Configuration;
using IniParser.Parser;
using LegendaryExplorerCore.Misc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SevenZip;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class IniParserTests
    {
        [TestMethod]
        public void TestIniParserParity()
        {
            GlobalTest.Init();

            var compressedModsDirectory = GlobalTest.GetTestingDataDirectoryFor("compressedmods");
            Assert.IsTrue(Directory.Exists(compressedModsDirectory), $"compressedmods directory not found: {compressedModsDirectory}");

            var archives = Directory.GetFiles(compressedModsDirectory, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.IsTrue(archives.Count > 0, $"No .7z or .zip archives found in: {compressedModsDirectory}");

            // Configure IniDataParser to match DuplicatingIni's restrictiveParsing = true behaviour:
            //   - AllowDuplicateSections = false : both parsers throw on duplicate sections
            //   - SkipInvalidLines = false        : both parsers throw on lines without '='
            //   - AllowKeysWithoutSection = true  : DuplicatingIni silently skips pre-section keys;
            //                                       IniDataParser accepts them in the global section
            var iniParserConfig = new IniParserConfiguration
            {
                AllowDuplicateKeys = false,
                AllowDuplicateSections = false,
                AllowKeysWithoutSection = true,
                ThrowExceptionsOnError = true,
            };
            var iniDataParser = new IniDataParser(iniParserConfig);

            int filesChecked = 0;

            foreach (var archivePath in archives)
            {
                Console.WriteLine($"Inspecting archive: {archivePath}");
                using var archive = new SevenZipExtractor(archivePath);

                var moddescEntries = archive.ArchiveFileData
                    .Where(f => !f.IsDirectory && Path.GetFileName(f.FileName).Equals("moddesc.ini", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var entry in moddescEntries)
                {
                    Console.WriteLine($"  Testing: {entry.FileName}");

                    using var ms = new MemoryStream();
                    archive.ExtractFile(entry.FileName, ms);
                    ms.Position = 0;
                    var iniText = new StreamReader(ms).ReadToEnd();

                    Exception duplicatingIniException = null;
                    Exception iniParserException = null;

                    try
                    {
                        DuplicatingIni.ParseIni(iniText, restrictiveParsing: true);
                    }
                    catch (Exception ex)
                    {
                        duplicatingIniException = ex;
                    }

                    try
                    {
                        iniDataParser.Parse(iniText);
                    }
                    catch (Exception ex)
                    {
                        iniParserException = ex;
                    }

                    bool duplicatingFailed = duplicatingIniException != null;
                    bool iniParserFailed = iniParserException != null;

                    Assert.AreEqual(
                        duplicatingFailed,
                        iniParserFailed,
                        $"Parser parity mismatch for '{entry.FileName}' in '{Path.GetFileName(archivePath)}': " +
                        $"DuplicatingIni {(duplicatingFailed ? $"threw ({duplicatingIniException.Message})" : "succeeded")}, " +
                        $"IniDataParser {(iniParserFailed ? $"threw ({iniParserException.Message})" : "succeeded")}");

                    filesChecked++;
                }
            }

            Console.WriteLine($"Checked {filesChecked} moddesc.ini files across {archives.Count} archives.");
        }
    }
}
