using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SevenZip;

namespace ExecutableModParser
{
    class Program
    {
        private static string SourceEXE;
        static void Main(string[] args)
        {
            SourceEXE = @"C:\Users\mgame\Desktop\CEMG_ver_1.exe";
            ParseArguments(args);
            if (SourceEXE == null)
            {
                if (args.Count() == 1 && File.Exists(args[0]))
                {
                    SourceEXE = args[0];
                }
                else
                {
                    Console.WriteLine("--inputexe not specified");
                    return;
                }
            }

            if (SourceEXE != null && !File.Exists(SourceEXE))
            {
                Console.WriteLine("Source archive doesn't exist: " + SourceEXE);
                return;
            }

            SevenZipExtractor archive = new SevenZipExtractor(SourceEXE, InArchiveFormat.Nsis);
            foreach (var entry in archive.ArchiveFileData)
            {
                Console.WriteLine($"{entry.Index}\t{(entry.IsDirectory ? "Folder" : "File")}\t{entry.FileName}");
            }

        }

        public static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                switch (argument)
                {
                    case "--inputexe":
                        if (args.Length < i + 1) NotEnoughArguments(argument);
                        SourceEXE = args[i + 1];
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
    }
}
