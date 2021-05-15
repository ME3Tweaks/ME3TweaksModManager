using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using SevenZip;
using Utilities = MassEffectModManagerCore.Utilities;

namespace ExecutableModParser
{
    class Program
    {
        private static string SourceEXE;
        private static string SourceDir;
        static void Main(string[] args)
        {
            BuildTransform();
            return;
            
            //ParseArguments(args);
            SourceDir = @"B:\ExeMods\ShorterDreams";
            if (SourceDir != null)
            {
                ParseSourceDir();
                return;
            }
            SourceEXE = @"C:\Users\Mgame\Desktop\SD_v0_1_3.exe";
            //ParseArguments(args);
            //if (SourceEXE == null)
            //{
            //    if (args.Count() == 1 && File.Exists(args[0]))
            //    {
            //        SourceEXE = args[0];
            //    }
            //    else
            //    {
            //        Console.WriteLine("--inputexe not specified");
            //        return;
            //    }
            //}

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

            foreach (var entry in archive.ArchiveFileData)
            {
                if (entry.FileName.Contains("BIOGame\\DLC"))
                {
                    var outName = entry.FileName.Substring(entry.FileName.IndexOf("BIOGame\\DLC\\") + "BIOGame\\DLC\\".Length);
                    outName = $"<alternateredirect index=\"{entry.Index}\" outfile=\"{outName}\"/>";
                    Console.WriteLine(outName);
                }
            }
        }

        private static void BuildTransform()
        {
            var destTransformFS = @"X:\m3modlibrary\ME3\Shorter Dreams";
            var inputExe = @"B:\ExeMods\SD_v0_1_3.exe";

            // Build file info for transformFS
            var tfs = Directory.GetFiles(destTransformFS, "*.*", SearchOption.AllDirectories);
            Dictionary<string, string> md5Map = new Dictionary<string, string>();
            foreach (var tf in tfs)
            {
                md5Map[Utilities.CalculateMD5(tf)] = tf.Substring(destTransformFS.Length + 1);
            }


            SevenZipExtractor archive = new SevenZipExtractor(inputExe, InArchiveFormat.Nsis);
            foreach (var entry in archive.ArchiveFileData)
            {
                if (IsTrashFile(entry.FileName))
                {
                    Console.WriteLine($"<noextract index=\"{entry.Index}\"/> <!-- {entry.FileName} --> ");
                    continue;
                }
                var outStream = new MemoryStream();
                archive.ExtractFile(entry.Index, outStream);
                var amd5 = Utilities.CalculateMD5(outStream);
                var outName = $"<alternateredirect index=\"{entry.Index}\" outfile=\"{md5Map[amd5]}\"/>";
                Console.WriteLine(outName);
            }
        }

        private static void PrintArchiveIndices(SevenZipExtractor archive)
        {
            foreach (var entry in archive.ArchiveFileData)
            {
                Console.WriteLine($"{entry.Index}\t{(entry.IsDirectory ? "Folder" : "File")}\t{entry.FileName}");
            }
        }
        //    foreach (var entry in archive.ArchiveFileData)
        //    {
        //        if (entry.FileName.Contains("BIOGame\\DLC"))
        //        {
        //            var outName = entry.FileName.Substring(entry.FileName.IndexOf("BIOGame\\DLC\\") + "BIOGame\\DLC\\".Length);
        //            outName = $"<alternateredirect index=\"{entry.Index}\" outfile=\"{outName}\"/>";
        //            Console.WriteLine(outName);
        //        }
        //    }
        //}

        private static void ParseSourceDir()
        {
            Console.WriteLine($@"Parsing directory {SourceDir}");
            var exe = Directory.GetFiles(SourceDir, "*.exe").FirstOrDefault();
            var archive = new SevenZipExtractor(exe, InArchiveFormat.Nsis);
            PrintArchiveIndices(archive);

            var catalog = new List<M3FileInfo>();
            foreach (var e in archive.ArchiveFileData)
            {
                if (!IsTrashFile(e.FileName))
                {
                    Console.WriteLine($@"Cataloging archive file {e.FileName}");
                    var outStream = new MemoryStream();
                    archive.ExtractFile(e.Index, outStream);
                    catalog.Add(new M3FileInfo()
                    {
                        md5 = Utilities.CalculateMD5(outStream),
                        size = outStream.Length,
                        dataStream = outStream,
                        path = e.FileName,
                        entry = e
                    });
                }
            }

            var optionDirectories = Directory.GetDirectories(SourceDir);

            // Get file lists of each directory
            var optionFileMapping = new Dictionary<string, List<M3FileInfo>>();
            foreach (var d in optionDirectories)
            {
                List<string> files = Directory.GetFiles(d, "*.*", SearchOption.AllDirectories).Select(x => x.Substring(SourceDir.Length + 2 + Path.GetFileName(d).Length)).ToList();
                List<M3FileInfo> infos = new List<M3FileInfo>();

                foreach (var f in files)
                {
                    if (IsTrashFile(f)) continue;
                    Console.WriteLine($"Indexing {d}\\{f}");
                    var md5 = Utilities.CalculateMD5($"{d}\\{f}");
                    var info = catalog.FirstOrDefault(x => x.md5 == md5);
                    if (info == null)
                    {
                        Debugger.Break();
                    }
                    infos.Add(info);
                }

                optionFileMapping[Path.GetFileName(d)] = infos;
            }

            var allFiles = optionFileMapping[Path.GetFileName(optionDirectories[0])];
            for (int i = 1; i < optionDirectories.Length; i++)
            {
                allFiles = allFiles.Intersect(optionFileMapping[Path.GetFileName(optionDirectories[i])]).ToList();
            }

            Console.WriteLine(@"The following files are present in every directory:");
            //var mainOutDir = @"X:\m3modlibrary\ME3\Citadel Epiloge Mod - Lite";
            foreach (var f in allFiles)
            {
                Console.WriteLine($"{f.entry.Index}\t{f.path}");
                //var p = f.path.Substring(@"BIOGame\DLC\".Length);
                //var outP = Path.Combine(mainOutDir, p);
                //Directory.CreateDirectory(Directory.GetParent(outP).FullName);
                //f.dataStream.WriteToFile(outP);
            }

            Console.WriteLine("The following files are unique:");
            var nonUnique = allFiles.Select(x => x.entry.Index).ToList();
            var items = catalog.Select(x => x.entry.Index).Except(nonUnique).Select(x => catalog.FirstOrDefault(y => y.entry.Index == x));
            var longestName = items.MaxBy(x => x.path.Length).path.Length + 3;
            foreach (var f in items)
            {
                var folder = GetDiskPathForEntry(f, optionFileMapping);
                Console.WriteLine($"{f.entry.Index}\t{f.entry.FileName.PadRight(longestName)} | {folder}");
                // CEML SPECIFIC
                //if (!string.IsNullOrWhiteSpace(folder) && !folder.Contains(' '))
                //{
                //    var outP = Path.Combine(@"X:\m3modlibrary\ME3\Citadel Epiloge Mod - Lite\Alternates\Music", folder, Path.GetFileName(f.path));
                //    f.dataStream.WriteToFile(outP);
                //}
                //else if (f.entry.Index == 53 || f.entry.Index == 54)
                //{
                //    var outP = Path.Combine(@"X:\m3modlibrary\ME3\Citadel Epiloge Mod - Lite\Alternates\MiriJack", Path.GetFileName(f.path));
                //    f.dataStream.WriteToFile(outP);
                //}
                //else if (f.entry.Index >= 55)
                //{
                //    var outP = Path.Combine(@"X:\m3modlibrary\ME3\Citadel Epiloge Mod - Lite\Alternates\NoUltimateParty", Path.GetFileName(f.path));
                //    f.dataStream.WriteToFile(outP);
                //}
            }
        }

        private static bool IsTrashFile(string fname)
        {
            if (fname.StartsWith("$PLUGINS")) return true;
            if (fname.EndsWith(".dll")) return true;
            if (fname.EndsWith(".bmp")) return true;
            if (fname.EndsWith("test.xml")) return true;
            if (fname.EndsWith(".ini")) return true;
            if (fname.EndsWith("PCConsoleTOC.bin")) return true;
            if (fname.EndsWith(".exe")) return true;
            return false;
        }

        private static string GetDiskPathForEntry(M3FileInfo info, Dictionary<string, List<M3FileInfo>> mapping)
        {
            string str = "";
            foreach (var optionFolder in mapping.Keys)
            {
                if (mapping[optionFolder].Contains(info)) str += $" {optionFolder}";
            }

            return str == "" ? null : str.Trim();
        }

        public class M3FileInfo
        {
            private sealed class Md5SizePathEqualityComparer : IEqualityComparer<M3FileInfo>
            {
                public bool Equals(M3FileInfo x, M3FileInfo y)
                {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return x.md5 == y.md5 && x.size == y.size && x.path == y.path;
                }

                public int GetHashCode(M3FileInfo obj)
                {
                    return HashCode.Combine(obj.md5, obj.size, obj.path);
                }
            }

            public static IEqualityComparer<M3FileInfo> Md5SizePathComparer { get; } = new Md5SizePathEqualityComparer();

            public string md5 { get; set; }
            public long size { get; set; }
            public MemoryStream dataStream { get; set; }
            public string path { get; set; }
            public ArchiveFileInfo entry { get; set; }
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
    }
}
