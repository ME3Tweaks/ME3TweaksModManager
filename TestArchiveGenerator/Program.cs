using SevenZip;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace TestArchiveGenerator
{
    class Program
    {
        private static string SourceArchive;
        static void Main(string[] args)
        {
            ParseArguments(args);
            if (SourceArchive == null)
            {
                Console.WriteLine("--inputarchive not specified");
                return;
            }
            if (!File.Exists(SourceArchive))
            {
                Console.WriteLine("Source archive doesn't exist: " + SourceArchive);
                return;
            }
            var size = new FileInfo(SourceArchive).Length;
            var md5 = CalculateMD5(SourceArchive);

            string fnamezip = $"{Path.GetFileNameWithoutExtension(SourceArchive)}-{size}-{md5}.zip";
            string fname7z = $"{Path.GetFileNameWithoutExtension(SourceArchive)}-{size}-{md5}.7z";
            fnamezip = Path.Combine(Directory.GetParent(SourceArchive).FullName, fnamezip);
            fname7z = Path.Combine(Directory.GetParent(SourceArchive).FullName, fname7z);
            Console.WriteLine("Creating blank archives (zip and 7z): " + SourceArchive + " -> " + fnamezip);

            var extractionStaging = Path.Combine(Path.GetTempPath(), "BlankMMArchive");
            if (Directory.Exists(extractionStaging)) DeleteFilesAndFoldersRecursively(extractionStaging);
            Directory.CreateDirectory(extractionStaging);
            SevenZipExtractor archive = new SevenZipExtractor(SourceArchive);
            Console.WriteLine("Extracting archive...");
            archive.ExtractArchive(extractionStaging);
            var files = Directory.GetFiles(extractionStaging, "*", SearchOption.AllDirectories);
            foreach(var file in files)
            {
                if (Path.GetFileName(file) != "moddesc.ini")
                {
                    //write blank with guid
                    File.WriteAllText(file, Guid.NewGuid().ToString());
                }
            }

            Console.WriteLine("Compressing archive...");
            SevenZipCompressor svc = new SevenZipCompressor();
            svc.CompressDirectory(extractionStaging, fnamezip);
            svc.CompressDirectory(extractionStaging, fname7z);

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
