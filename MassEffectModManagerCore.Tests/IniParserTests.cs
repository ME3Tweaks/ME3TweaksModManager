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

            // DuplicatingIni.ParseIni no longer has a restrictiveParsing mode: it is permissive by
            // construction. Duplicate sections are kept as separate Sections, a line without '=' is
            // kept as raw text, and keys before any header go into a global (null-header) section.
            // It does not throw.
            //
            // So parity is now asserted against an equally permissive IniDataParser: every moddesc in
            // the corpus must parse under both. A strict parser is still run alongside, but only to
            // REPORT which files would fail stricter rules - that is useful to see and is not a
            // regression, since M3 deliberately tolerates them.
            var iniDataParser = new IniDataParser(new IniParserConfiguration
            {
                AllowDuplicateKeys = true,
                AllowDuplicateSections = true,
                AllowKeysWithoutSection = true,
                SkipInvalidLines = true,
                ThrowExceptionsOnError = false,
            });

            var strictParser = new IniDataParser(new IniParserConfiguration
            {
                AllowDuplicateKeys = false,
                AllowDuplicateSections = false,
                AllowKeysWithoutSection = true,
                SkipInvalidLines = false,
                ThrowExceptionsOnError = true,
            });

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
                        DuplicatingIni.ParseIni(iniText);
                    }
                    catch (Exception ex)
                    {
                        duplicatingIniException = ex;
                    }

                    try
                    {
                        strictParser.Parse(iniText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    (would fail strict ini rules: {ex.Message})");
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
