using System;
using System.IO;
using System.Formats.Tar;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SevenZip;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class NativeArchiveTests
    {
        [DataTestMethod]
        [DataRow("../outside.txt")]
        [DataRow("..\\outside.txt")]
        public void DefaultExtractionRejectsTraversal(string entryName)
        {
            var root = Path.Combine(Path.GetTempPath(), "M3ArchiveTest-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var zipPath = Path.Combine(root, "unsafe.zip");
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry(entryName).Open()))
                    writer.Write("must stay inside extraction directory");

                using var archive = new SevenZipExtractor(zipPath);
                Assert.Throws<SevenZipException>(() => archive.ExtractArchive(Path.Combine(root, "extracted")));
                Assert.IsFalse(File.Exists(Path.Combine(root, "outside.txt")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // tar -C dir . produces a "./" directory entry which resolves to the extraction root itself
        [TestMethod]
        public void DefaultExtractionAllowsRootDirectoryEntry()
        {
            var root = Path.Combine(Path.GetTempPath(), "M3ArchiveTest-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var tarPath = Path.Combine(root, "dotroot.tar");
                var expected = Encoding.UTF8.GetBytes("root entry content");
                using (var fs = File.Create(tarPath))
                using (var writer = new TarWriter(fs, TarEntryFormat.Ustar, false))
                {
                    writer.WriteEntry(new UstarTarEntry(TarEntryType.Directory, "./"));
                    writer.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, "./content.txt") { DataStream = new MemoryStream(expected) });
                }

                using (var archive = new SevenZipExtractor(tarPath))
                {
                    CollectionAssert.Contains(archive.ArchiveFileNames.ToList(), ".");
                    archive.ExtractArchive(Path.Combine(root, "extracted"));
                }
                CollectionAssert.AreEqual(expected, File.ReadAllBytes(Path.Combine(root, "extracted", "content.txt")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // Deliberately independent of GlobalTest.Init(), game data and online services.
        [TestMethod]
        public void NativeLibraryExtractsAndCreatesModArchives()
        {
            var root = Path.Combine(Path.GetTempPath(), "M3ArchiveTest-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var zipPath = Path.Combine(root, "mod.zip");
                var expected = Encoding.UTF8.GetBytes("synthetic mod content");
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                using (var entry = zip.CreateEntry("folder/content.txt").Open())
                    entry.Write(expected);

                using (var archive = new SevenZipExtractor(zipPath))
                {
                    Assert.AreEqual(1u, archive.FilesCount);
                    using var content = new MemoryStream();
                    archive.ExtractFile(0, content);
                    CollectionAssert.AreEqual(expected, content.ToArray());
                    archive.ExtractArchive(Path.Combine(root, "extracted"));
                }
                CollectionAssert.AreEqual(expected,
                    File.ReadAllBytes(Path.Combine(root, "extracted", "folder", "content.txt")));

                var sevenZipPath = Path.Combine(root, "mod.7z");
                new SevenZipCompressor().CompressFiles(sevenZipPath,
                    Path.Combine(root, "extracted", "folder", "content.txt"));
                using (var archive = new SevenZipExtractor(sevenZipPath))
                using (var content = new MemoryStream())
                {
                    archive.ExtractFile(0, content);
                    CollectionAssert.AreEqual(expected, content.ToArray());
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
