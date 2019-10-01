using System;
using System.IO;

namespace TestDirectoryGenerator
{
    class Program
    {
        private static string SourceDir;
        private static string DestDir;
        private static bool UseGUIDContents;
        static void Main(string[] args)
        {
            ParseArguments(args);
            if (SourceDir == null)
            {
                Console.WriteLine("--inputdir not specified");
                return;
            }
            if (DestDir == null)
            {
                Console.WriteLine("--outputdir not specified");
                return;
            }
            if (!Directory.Exists(SourceDir))
            {
                Console.WriteLine("Source directory doesn't exist: " + SourceDir);
                return;
            }
            if (Directory.Exists(DestDir))
            {
                if (Directory.GetFiles(DestDir).Length > 0)
                {
                    Console.WriteLine("Destination directory is not empty.");
                    return;
                }
            }
            Console.WriteLine("Creating blank copy: " + SourceDir + " -> " + DestDir);
            CopyDir.CreateBlankCopy(new DirectoryInfo(SourceDir), new DirectoryInfo(DestDir), UseGUIDContents);
        }

        public static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                switch (argument)
                {
                    case "--inputdir":
                        if (args.Length < i + 1) NotEnoughArguments(argument);
                        SourceDir = args[i + 1];
                        i++;
                        break;
                    case "--outputdir":
                        if (args.Length < i + 1) NotEnoughArguments(argument);
                        DestDir = args[i + 1];
                        i++;
                        break;
                    case "--useguidcontents":
                        UseGUIDContents = true;
                        break;
                }
            }
        }

        private static void NotEnoughArguments(string argument)
        {
            Console.WriteLine("Missing argument for option: " + argument);
            Environment.Exit(1);
        }
    }
}
