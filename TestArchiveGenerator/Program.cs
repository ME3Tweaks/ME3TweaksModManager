using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ME3TweaksModManager.modmanager.objects.mod.merge;

namespace TestArchiveGenerator
{
    public class TestArchiveGeneratorProgram
    {
        private static string SourceArchive;
        private static string SourceDir;
        static void Main(string[] args)
        {
            ParseArguments(args);
            if (SourceArchive == null && SourceDir == null)
            {
                if (args.Count() == 1 && File.Exists(args[0]))
                {
                    SourceArchive = args[0];
                }
                else
                {
                    Console.WriteLine("--inputarchive and/or --inputdir not specified");
                    return;
                }
            }

            if (SourceArchive != null && !File.Exists(SourceArchive))
            {
                Console.WriteLine("Source archive doesn't exist: " + SourceArchive);
                return;
            }
            if (SourceDir != null && !Directory.Exists(SourceDir))
            {
                Console.WriteLine("Source directory doesn't exist: " + SourceDir);
                return;
            }

            if (SourceArchive != null && SourceDir != null)
            {
                Console.WriteLine("Can only specify one of --inputarchive or --inputdir, not both");
                return;
            }

            List<string> archives = new List<string>();
            if (SourceDir != null)
            {
                archives.AddRange(Directory.GetFiles(SourceDir, "*.7z"));
                archives.AddRange(Directory.GetFiles(SourceDir, "*.zip"));
                archives.AddRange(Directory.GetFiles(SourceDir, "*.rar"));
            }
            else
            {
                archives.Add(SourceArchive);
            }

            foreach (var archive in archives)
            {
                CreateBlankArchive(archive, Directory.GetParent(archive).FullName);
            }

        }

        /// <summary>
        /// Generates a blank mod archive 
        /// </summary>
        /// <param name="inputArchivePath">Input archive to scan and generate</param>
        /// <param name="outputPath">Directory where to place resulting archive</param>
        /// <param name="tempPath">Directory to extract files to for staging before recompress</param>
        public static void CreateBlankArchive(string inputArchivePath, string outputPath, string tempPath = null)
        {
            var size = new FileInfo(inputArchivePath).Length;
            var md5 = CalculateMD5(inputArchivePath);

            string fnamenoExt = $"{Path.GetFileNameWithoutExtension(inputArchivePath)}";
            string ext = Path.GetExtension(inputArchivePath);
            if (ext == ".rar") ext = ".zip";
            var fullname = Path.Combine(Directory.GetParent(inputArchivePath).FullName, fnamenoExt + ext);
            Console.WriteLine("Creating blank archives " + inputArchivePath + " -> " + fullname);

            var extractionStaging = tempPath ?? Path.Combine(Path.GetTempPath(), "BlankMMArchive");
            if (Directory.Exists(extractionStaging)) DeleteFilesAndFoldersRecursively(extractionStaging);
            Directory.CreateDirectory(extractionStaging);
            SevenZipExtractor archive = new SevenZipExtractor(inputArchivePath);
            Console.WriteLine("Extracting archive...");
            archive.ExtractArchive(extractionStaging);
            var files = Directory.GetFiles(extractionStaging, "*", SearchOption.AllDirectories);
            int expectedFileCount = 0;
            foreach (var file in files)
            {
                if (Path.GetFileName(file) == "moddesc.ini")
                {
                    //write blank with guid
                    File.WriteAllText(file, @"blank");
                }
                else
                {
                    var extension = Path.GetExtension(file);
                    if (extension == ".m3m")
                    {
                        // Wipe out asset files, since we test cases don't actually use these and they consume disk space.
                        zeroMergeModAssets(file);
                    }
                    expectedFileCount++;
                }
            }

            if (expectedFileCount == 0) expectedFileCount = 1;
            fullname = $"{Path.GetFileNameWithoutExtension(inputArchivePath)}-{expectedFileCount}-{size}-{md5}{ext}";

            Console.WriteLine("Compressing archive...");
            SevenZipCompressor svc = new SevenZipCompressor();
            // Do not compress any files.
            svc.CompressionLevel = CompressionLevel.None;
            svc.CompressionMode = CompressionMode.Create;
            svc.CompressionMethod = CompressionMethod.Copy;
            svc.CompressDirectory(extractionStaging, Path.Combine(outputPath, fullname));
            DeleteFilesAndFoldersRecursively(extractionStaging);
        }

        private static void zeroMergeModAssets(string file)
        {
            var mergeMod = MergeModLoader.LoadMergeMod(new MemoryStream(File.ReadAllBytes(file)), file, false);

            if (mergeMod.Assets.Count > 0)
            {
                // Decompile
                var dir = Directory.GetParent(file).FullName;
                MergeModLoader.DecompileM3M(file);

                // Zero files.
                foreach (var v in mergeMod.Assets.Keys)
                {
                    Console.WriteLine($@"Zeroing merge mod asset {v} in {Path.GetFileName(file)}");
                    var assetFile = Path.Combine(dir, v);
                    File.WriteAllText(assetFile, @"blank merge mod asset");
                }

                // Reserialize
                Console.WriteLine($@"Reserializing merge mod {Path.GetFileName(file)}");
                var inFile = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(file)}.json");
                MergeModLoader.SerializeManifest(inFile, 1);

                // Delete the extra files.
                foreach (var v in mergeMod.Assets.Keys)
                {
                    File.Delete(Path.Combine(dir, v));
                }
            }
        }

        private static bool shouldZeroFile(string file)
        {
            if (Path.GetExtension(file) == @".m3m") return false;

            var filename = Path.GetFileName(file);
            if (filename == "moddesc.ini") return false;

            return true;
        }

        public static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                switch (argument)
                {
                    case "--inputarchive":
                        if (args.Length < i + 1) NotEnoughArguments(argument);
                        SourceArchive = args[i + 1];
                        i++;
                        break;
                    case "--inputdir":
                        if (args.Length < i + 1) NotEnoughArguments(argument);
                        SourceDir = args[i + 1];
                        i++;
                        break;
                }
            }
        }

        private static void NotEnoughArguments(string argument)
        {
            Console.WriteLine("Missing argument for option: " + argument);
            Environment.Exit(1);
        }

        public static bool DeleteFilesAndFoldersRecursively(string targetDirectory)
        {
            bool result = true;
            foreach (string file in Directory.GetFiles(targetDirectory))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(targetDirectory))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(10); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + targetDirectory);

                Directory.Delete(targetDirectory);
            }
            catch (Exception e)
            {
                return false;
            }
            return result;
        }


        public static string CalculateMD5(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (IOException e)
            {
                return "";
            }
        }

    }
}
