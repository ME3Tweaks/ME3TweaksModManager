/*
 * MassEffectModder
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski <aquadran at users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.helpers;

namespace ModManagerCommandLine
{
    class SFAR_Extract
    {
        const uint SfarTag = 0x53464152; // 'SFAR'
        const uint SfarVersion = 0x00010000;
        const uint LZMATag = 0x6c7a6d61; // 'lzma'
        const uint HeaderSize = 0x20;
        const uint EntryHeaderSize = 0x1e;
        byte[] FileListHash = new byte[] { 0xb5, 0x50, 0x19, 0xcb, 0xf9, 0xd3, 0xda, 0x65, 0xd5, 0x5b, 0x32, 0x1c, 0x00, 0x19, 0x69, 0x7c };
        const long MaxBlockSize = 0x00010000;
        int filenamesIndex;
        uint filesCount;
        List<FileEntry> filesList;
        uint maxBlockSize;
        List<ushort> blockSizes;

        public struct FileEntry
        {
            public byte[] filenameHash;
            public string filenamePath;
            public long uncomprSize;
            public int compressedBlockSizesIndex;
            public uint numBlocks;
            public long dataOffset;
        }

        class FileArrayComparer : IComparer<FileEntry>
        {
            public int Compare(FileEntry x, FileEntry y)
            {
                return StructuralComparisons.StructuralComparer.Compare(x.filenameHash, y.filenameHash);
            }
        }

        public static int getNumberOfFiles(string path)
        {
            if (!File.Exists(path))
                throw new Exception("filename missing");
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                uint tag = stream.ReadUInt32();
                if (tag != SfarTag)
                    throw new Exception("Wrong SFAR tag");
                uint sfarVersion = stream.ReadUInt32();
                if (sfarVersion != SfarVersion)
                    throw new Exception("Wrong SFAR version");

                uint dataOffset = stream.ReadUInt32();
                uint entriesOffset = stream.ReadUInt32();
                return stream.ReadInt32();
            }
        }

        private void loadHeader(Stream stream)
        {
            uint tag = stream.ReadUInt32();
            if (tag != SfarTag)
                throw new Exception("Wrong SFAR tag");
            uint sfarVersion = stream.ReadUInt32();
            if (sfarVersion != SfarVersion)
                throw new Exception("Wrong SFAR version");

            uint dataOffset = stream.ReadUInt32();
            uint entriesOffset = stream.ReadUInt32();
            filesCount = stream.ReadUInt32();
            uint sizesArrayOffset = stream.ReadUInt32();
            maxBlockSize = stream.ReadUInt32();
            uint compressionTag = stream.ReadUInt32();
            if (compressionTag != LZMATag)
                throw new Exception("Not LZMA compression for SFAR file");

            uint numBlockSizes = 0;
            stream.JumpTo(entriesOffset);
            filesList = new List<FileEntry>();
            for (int i = 0; i < filesCount; i++)
            {
                FileEntry file = new FileEntry();
                file.filenameHash = stream.ReadToBuffer(16);
                file.compressedBlockSizesIndex = stream.ReadInt32();
                file.uncomprSize = stream.ReadUInt32();
                file.uncomprSize |= (long)stream.ReadByte() << 32;
                file.dataOffset = stream.ReadUInt32();
                file.dataOffset |= (long)stream.ReadByte() << 32;
                file.numBlocks = (uint)((file.uncomprSize + maxBlockSize - 1) / maxBlockSize);
                filesList.Add(file);
                numBlockSizes += file.numBlocks;
            }

            stream.JumpTo(sizesArrayOffset);
            blockSizes = new List<ushort>();
            for (int i = 0; i < numBlockSizes; i++)
            {
                blockSizes.Add(stream.ReadUInt16());
            }

            filenamesIndex = -1;
            for (int i = 0; i < filesCount; i++)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(filesList[i].filenameHash, FileListHash))
                {
                    stream.JumpTo(filesList[i].dataOffset);
                    int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex];
                    byte[] inBuf = stream.ReadToBuffer(compressedBlockSize);
                    byte[] outBuf = SevenZipHelper.LZMA.Decompress(inBuf, (uint)filesList[i].uncomprSize);
                    if (outBuf.Length == 0)
                        throw new Exception();
                    StreamReader filenamesStream = new StreamReader(new MemoryStream(outBuf));
                    while (filenamesStream.EndOfStream == false)
                    {
                        string name = filenamesStream.ReadLine();
                        byte[] hash = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(name.ToLowerInvariant()));
                        for (int l = 0; l < filesCount; l++)
                        {
                            if (StructuralComparisons.StructuralEqualityComparer.Equals(filesList[l].filenameHash, hash))
                            {
                                FileEntry f = filesList[l];
                                f.filenamePath = name;
                                filesList[l] = f;
                            }
                        }
                    }
                    filenamesIndex = i;
                    break;
                }
            }
            if (filenamesIndex == -1)
                throw new Exception("filenames entry not found");
        }

        public void extract(string SFARfilename, string outPath, bool ipc, ref int currentProgress, int totalNumber, bool keepbiogamedlc = true, bool readOnly = false)
        {
            if (!File.Exists(SFARfilename))
                throw new Exception("SFAR archive not found: " + SFARfilename);

            string stagedfile = Directory.GetParent(SFARfilename) + "\\Default.sfar.STAGED";
            byte[] buffer = File.ReadAllBytes(SFARfilename);
            
            using (MemoryStream stream = new MemoryStream(buffer))
            {
                loadHeader(stream);

                if (!readOnly)
                {
                    //Directory.CreateDirectory(Path.Combine(outPath, "CookedPCConsole"));
                    File.Delete(SFARfilename);
                    using (FileStream outputFile = new FileStream(SFARfilename, FileMode.Create, FileAccess.Write))
                    {
                        outputFile.WriteUInt32(SfarTag);
                        outputFile.WriteUInt32(SfarVersion);
                        outputFile.WriteUInt32(HeaderSize);
                        outputFile.WriteUInt32(HeaderSize);
                        outputFile.WriteUInt32(0);
                        outputFile.WriteUInt32(HeaderSize);
                        outputFile.WriteUInt32((uint)MaxBlockSize);
                        outputFile.WriteUInt32(LZMATag);
                    }
                }

                int lastProgress = -1;
                for (int i = 0; i < filesCount; i++, currentProgress++)
                {
                    if (filenamesIndex == i)
                        continue;
                    if (filesList[i].filenamePath == null)
                        throw new Exception("filename missing");

                    if (ipc)
                    {
                        int newProgress = (100 * currentProgress) / totalNumber;
                        if (lastProgress != newProgress)
                        {
                            Console.WriteLine("[IPC]TASK_PROGRESS " + newProgress);
                            Console.Out.Flush();
                            lastProgress = newProgress;
                        }
                    }

                    string filename = filesList[i].filenamePath;
                    string dir = outPath;
                    if (!keepbiogamedlc)
                    {
                        int prefixpoint = filesList[i].filenamePath.IndexOf("/BIOGame/DLC/", StringComparison.OrdinalIgnoreCase);
                        filename = filesList[i].filenamePath.Substring(prefixpoint + ("/BIOGame/DLC/").Length).Replace('/', '\\');
                        dir = Path.GetDirectoryName(outPath);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dir + filename));
                    using (FileStream outputFile = new FileStream(dir + filename, FileMode.Create, FileAccess.Write))
                    {
                        Console.WriteLine("Extracting " + (dir + filename));
                        stream.JumpTo(filesList[i].dataOffset);
                        if (filesList[i].compressedBlockSizesIndex == -1)
                        {
                            outputFile.WriteFromStream(stream, filesList[i].uncomprSize);
                        }
                        else
                        {
                            List<byte[]> uncompressedBlockBuffers = new List<byte[]>();
                            List<byte[]> compressedBlockBuffers = new List<byte[]>();
                            List<long> blockBytesLeft = new List<long>();
                            long bytesLeft = filesList[i].uncomprSize;
                            for (int j = 0; j < filesList[i].numBlocks; j++)
                            {
                                blockBytesLeft.Add(bytesLeft);
                                int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex + j];
                                int uncompressedBlockSize = (int)Math.Min(bytesLeft, maxBlockSize);
                                if (compressedBlockSize == 0)
                                {
                                    compressedBlockSize = (int)maxBlockSize;
                                }
                                compressedBlockBuffers.Add(stream.ReadToBuffer(compressedBlockSize));
                                uncompressedBlockBuffers.Add(null);
                                bytesLeft -= uncompressedBlockSize;
                            }

                            Parallel.For(0, filesList[i].numBlocks, j =>
                            {
                                int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex + (int)j];
                                int uncompressedBlockSize = (int)Math.Min(blockBytesLeft[(int)j], maxBlockSize);
                                if (compressedBlockSize == 0 || compressedBlockSize == blockBytesLeft[(int)j])
                                {
                                    uncompressedBlockBuffers[(int)j] = compressedBlockBuffers[(int)j];
                                }
                                else
                                {
                                    uncompressedBlockBuffers[(int)j] = SevenZipHelper.LZMA.Decompress(compressedBlockBuffers[(int)j], (uint)uncompressedBlockSize);
                                    if (uncompressedBlockBuffers[(int)j].Length == 0)
                                        throw new Exception();
                                }
                            });

                            for (int j = 0; j < filesList[i].numBlocks; j++)
                            {
                                outputFile.WriteFromBuffer(uncompressedBlockBuffers[j]);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts a list of files from the SFAR to the specified directory, keeping the same folder structure as the searchname.
        /// This method does not modify the original SFAR.
        /// </summary>
        /// <param name="outPath">Directory to extract files to</param>
        /// <param name="searchnames">List of files starting with /BIOGame/DLC/...</param>
        /// <param name="flatFolderExtraction">Indicates that files should be dumped directly to the output folder without the original archive filepath</param>
        public void extractfiles(string sfarfilelocation, string outPath, string[] searchnames, bool flatFolderExtraction, bool ignoreMissingFiles = false)
        {
            using (FileStream sfarFile = new FileStream(sfarfilelocation, FileMode.Open, FileAccess.Read))
            {
                loadHeader(sfarFile);
                if (outPath == null)
                {
                    //do same folder
                    outPath = Directory.GetParent(sfarfilelocation).FullName + "\\";
                }
                else
                {
                    outPath = outPath.Replace('/', '\\');
                    if (!outPath.EndsWith("\\"))
                    {
                        outPath += "\\";
                    }
                }
                List<string> itemstoextract = searchnames.ToList<string>();
                for (int i = 0; i < filesCount; i++)
                {
                    if (filenamesIndex == i)
                        continue;
                    if (filesList[i].filenamePath == null)
                        throw new Exception("SFAR ExtractFiles: filenamePath missing");
                    if (itemstoextract.FindIndex(x => x.Equals(filesList[i].filenamePath, StringComparison.OrdinalIgnoreCase)) == -1)
                        continue; //skip

                    int pos = filesList[i].filenamePath.IndexOf("\\BIOGame\\DLC\\", StringComparison.OrdinalIgnoreCase);
                    string filename = filesList[i].filenamePath.Substring(pos + ("\\BIOGame\\DLC\\").Length).Replace('/', '\\');
                    string dir = Path.GetDirectoryName(outPath) + "\\";
                    if (flatFolderExtraction)
                    {
                        filename = Path.GetFileName(filesList[i].filenamePath);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dir + filename));

                    using (FileStream outputFile = new FileStream(dir + filename, FileMode.Create, FileAccess.Write))
                    {
                        Console.WriteLine("Extracting: " + filesList[i].filenamePath + " to " + outputFile.Name);

                        sfarFile.JumpTo(filesList[i].dataOffset);
                        if (filesList[i].compressedBlockSizesIndex == -1)
                        {
                            outputFile.WriteFromStream(sfarFile, filesList[i].uncomprSize);
                        }
                        else
                        {
                            List<byte[]> uncompressedBlockBuffers = new List<byte[]>();
                            List<byte[]> compressedBlockBuffers = new List<byte[]>();
                            List<long> blockBytesLeft = new List<long>();
                            long bytesLeft = filesList[i].uncomprSize;
                            for (int j = 0; j < filesList[i].numBlocks; j++)
                            {
                                blockBytesLeft.Add(bytesLeft);
                                int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex + j];
                                int uncompressedBlockSize = (int)Math.Min(bytesLeft, maxBlockSize);
                                if (compressedBlockSize == 0)
                                {
                                    compressedBlockSize = (int)maxBlockSize;
                                }
                                compressedBlockBuffers.Add(sfarFile.ReadToBuffer(compressedBlockSize));
                                uncompressedBlockBuffers.Add(null);
                                bytesLeft -= uncompressedBlockSize;
                            }

                            Parallel.For(0, filesList[i].numBlocks, j =>
                            {
                                int compressedBlockSize = blockSizes[filesList[i].compressedBlockSizesIndex + (int)j];
                                int uncompressedBlockSize = (int)Math.Min(blockBytesLeft[(int)j], maxBlockSize);
                                if (compressedBlockSize == 0 || compressedBlockSize == blockBytesLeft[(int)j])
                                {
                                    uncompressedBlockBuffers[(int)j] = compressedBlockBuffers[(int)j];
                                }
                                else
                                {
                                    uncompressedBlockBuffers[(int)j] = SevenZipHelper.LZMA.Decompress(compressedBlockBuffers[(int)j], (uint)uncompressedBlockSize);
                                    if (uncompressedBlockBuffers[(int)j].Length == 0)
                                        throw new Exception();
                                }
                            });

                            for (int j = 0; j < filesList[i].numBlocks; j++)
                            {
                                outputFile.WriteFromBuffer(uncompressedBlockBuffers[j]);
                            }
                        }
                    }
                }
            }
        }
    }
}