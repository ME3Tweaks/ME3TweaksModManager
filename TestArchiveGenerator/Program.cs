using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace TestArchiveGenerator
{
    class Program
    {
        private static string SourceArchive;
        private static string SourceDir;
        static void Main(string[] args)
        {
            ParseArguments(args);
            if (SourceArchive == null && SourceDir == null)
            {
                if (args.Count() == 1 && File.Exists(args[0])) {
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
                CreateBlankArchive(archive);
            }

        }

        private static void CreateBlankArchive(string archivePath)
        {
            var size = new FileInfo(archivePath).Length;
            var md5 = CalculateMD5(archivePath);

            string fnamenoExt = $"{Path.GetFileNameWithoutExtension(archivePath)}";
            string ext = Path.GetExtension(archivePath);
            if (ext == ".rar") ext = ".zip";
            var fullname = Path.Combine(Directory.GetParent(archivePath).FullName, fnamenoExt + ext);
            Console.WriteLine("Creating blank archives " + archivePath + " -> " + fullname);

            var extractionStaging = Path.Combine(Path.GetTempPath(), "BlankMMArchive");
            if (Directory.Exists(extractionStaging)) DeleteFilesAndFoldersRecursively(extractionStaging);
            Directory.CreateDirectory(extractionStaging);
            SevenZipExtractor archive = new SevenZipExtractor(archivePath);
            Console.WriteLine("Extracting archive...");
            archive.ExtractArchive(extractionStaging);
            var files = Directory.GetFiles(extractionStaging, "*", SearchOption.AllDirectories);
            int expectedAmount = 0;
            foreach (var file in files)
            {
                if (Path.GetFileName(file) != "moddesc.ini")
                {
                    //write blank with guid
                    File.WriteAllText(file, Guid.NewGuid().ToString());
                }
                else
                {
                    expectedAmount++;
                }
            }

            if (expectedAmount == 0) expectedAmount = 1;
            fullname = $"{Path.GetFileNameWithoutExtension(archivePath)}-{expectedAmount}-{size}-{md5}{ext}";

            Console.WriteLine("Compressing archive...");
            SevenZipCompressor svc = new SevenZipCompressor();
            svc.CompressDirectory(extractionStaging, Path.Combine(Directory.GetParent(archivePath).FullName, fullname));
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
