using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MassEffectModManagerCore.modmanager.helpers;
using Serilog;

namespace ME3Explorer.Unreal
{
    public class TOCCreator
    {
        private static IEnumerable<string> GetTocableFiles(string path)
        {
            string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.txt", "*.cnd", "*.upk", "*.tfc" };
            var res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res.ToArray();
        }

        private static List<string> GetFiles(string basefolder)
        {
            var res = new List<string>();
            string directoryName = Path.GetFileName(Path.GetDirectoryName(basefolder));
            res.AddRange(GetTocableFiles(basefolder));
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            var folders = folder.GetDirectories();
            if (folders.Length != 0)
            {
                if (directoryName != "BIOGame")
                {
                    //treat as dlc and include all folders.
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(Path.Combine(basefolder, f.Name)));
                }
                else
                {
                    //biogame, only do cookedpcconsole and movies.
                    foreach (DirectoryInfo f in folders)
                    {
                        if (f.Name == "CookedPCConsole" || /*f.Name == "DLC" ||*/ f.Name == "Movies" /*|| f.Name == "Splash"*/)
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name)));
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified DLC directory root
        /// </summary>
        /// <param name="directory">DLC_ directory, like DLC_CON_JAM, or the BIOGame directory of the game.</param>
        /// <returns>Memorystream of TOC created, null if there are no entries or input was invalid</returns>
        public static MemoryStream CreateTOCForDirectory(string directory)
        {
            var files = GetFiles(directory);
            var originalFilesList = files;
            if (files.Count > 0)
            {
                //Strip the non-relative path information
                string file0fullpath = files[0];
                int dlcFolderStartSubStrPos = file0fullpath.IndexOf("DLC_", StringComparison.InvariantCultureIgnoreCase);
                if (dlcFolderStartSubStrPos > 0)
                {
                    files = files.Select(x => x.Substring(dlcFolderStartSubStrPos)).ToList();
                    files = files.Select(x => x.Substring(x.IndexOf('\\') + 1)).ToList(); //remove first slash
                }
                else
                {
                    int biogameStrPos = file0fullpath.IndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase);
                    if (biogameStrPos > 0)
                    {
                        files = files.Select(x => x.Substring(biogameStrPos)).ToList();
                    }
                }

                var entries = new List<(string file, int size)>();
                for (int i = 0; i < originalFilesList.Count; i++)
                {
                    entries.Add((files[i], (int)new FileInfo(originalFilesList[i]).Length));
                }

                return CreateTOCForEntries(entries);
            }
            Log.Error(@"There are no tocable fies in directory " + directory);
            return null;
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified list of filenames and sizes. The filenames should already be in the format that will be used in the TOC itself.
        /// </summary>
        /// <param name="filesystemInfo">list of filenames and sizes for the TOC</param>
        /// <returns>memorystream of TOC, null if list is empty</returns>
        public static MemoryStream CreateTOCForEntries(List<(string filename, int size)> filesystemInfo)
        {
            if (filesystemInfo.Count != 0)
            {
                byte[] SHA1 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                MemoryStream fs = new MemoryStream();

                fs.WriteInt32(0x3AB70C13);
                fs.WriteInt32(0x0);
                fs.WriteInt32(0x1);
                fs.WriteInt32(0x8);
                fs.WriteInt32(filesystemInfo.Count);
                for (int i = 0; i < filesystemInfo.Count; i++)
                {
                    (string file, int size) entry = filesystemInfo[i];
                    if (i == filesystemInfo.Count - 1) //Entry Size - is last item?
                        fs.WriteUInt16(0);
                    else
                        fs.WriteUInt16((ushort)(0x1D + entry.file.Length));
                    fs.WriteUInt16(0); //Flags
                    if (Path.GetFileName(entry.file).ToLower() != @"pcconsoletoc.bin")
                    {
                        fs.WriteInt32(entry.size); //Filesize
                    }
                    else
                    {
                        fs.WriteInt32(0); //Filesize
                    }

                    fs.Write(SHA1, 0, 20);
                    fs.WriteStringASCII(entry.file);
                    //foreach (char c in file)
                    //    fs.WriteByte((byte)c);
                    fs.WriteByte(0);
                }

                return fs;
            }

            return null;
        }
    }
}