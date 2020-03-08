using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using ME3Explorer;
using ME3Explorer.Unreal;

namespace MassEffectModManagerCore.gamefileformats.sfar
{
    public class DLCPackage
    {
        private const string UNKNOWN_FILENAME = "UNKNOWN";
        public string FileName;
        public struct HeaderStruct
        {
            public uint Magic;
            public uint Version;
            public uint DataOffset;
            public uint EntryOffset;
            public uint FileCount;
            public uint BlockTableOffset;
            public uint MaxBlockSize;
            public char[] CompressionScheme;
            public void Serialize(SerializingFile con)
            {
                Magic = con + Magic;
                Version = con + Version;
                DataOffset = con + DataOffset;
                EntryOffset = con + EntryOffset;
                FileCount = con + FileCount;
                BlockTableOffset = con + BlockTableOffset;
                MaxBlockSize = con + MaxBlockSize;
                if (con.isLoading)
                    CompressionScheme = new char[4];
                for (int i = 0; i < 4; i++)
                    CompressionScheme[i] = con + CompressionScheme[i];
                if (Magic != 0x53464152 ||
                    Version != 0x00010000 ||
                    MaxBlockSize != 0x00010000)
                    throw new Exception("This DLC archive format is not supported.");
            }
        }
        [DebuggerDisplay("SFAR FileEntryStruct | {FileName}")]
        public struct FileEntryStruct
        {
            public HeaderStruct Header;
            public uint MyOffset;
            public byte[] Hash;
            public uint BlockSizeIndex;
            public uint UncompressedSize;
            public byte UncompressedSizeAdder;
            public long RealUncompressedSize;
            public uint DataOffset;
            public byte DataOffsetAdder;
            public long RealDataOffset;
            public long BlockTableOffset;
            public long[] BlockOffsets;
            public ushort[] BlockSizes;
            public string FileName;

            public void Serialize(SerializingFile con, HeaderStruct header)
            {
                Header = header;
                MyOffset = (uint)con.GetPos();
                if (con.isLoading)
                    Hash = new byte[16];
                for (int i = 0; i < 16; i++)
                    Hash[i] = con + Hash[i];
                BlockSizeIndex = con + BlockSizeIndex;
                UncompressedSize = con + UncompressedSize;
                UncompressedSizeAdder = con + UncompressedSizeAdder;
                RealUncompressedSize = UncompressedSize + UncompressedSizeAdder << 32;
                DataOffset = con + DataOffset;
                DataOffsetAdder = con + DataOffsetAdder;
                RealDataOffset = DataOffset + DataOffsetAdder << 32;
                if (BlockSizeIndex == 0xFFFFFFFF) //Uncompressed
                {
                    BlockOffsets = new long[1];
                    BlockOffsets[0] = RealDataOffset;
                    BlockSizes = new ushort[1];
                    BlockSizes[0] = (ushort)UncompressedSize;
                    BlockTableOffset = 0;
                }
                else //Compressed
                {

                    int numBlocks = (int)Math.Ceiling(UncompressedSize / (double)header.MaxBlockSize);
                    if (con.isLoading)
                    {
                        BlockOffsets = new long[numBlocks];
                        BlockSizes = new ushort[numBlocks];
                    }
                    BlockOffsets[0] = RealDataOffset;
                    long pos = con.Memory.Position;
                    con.Seek((int)getBlockOffset((int)BlockSizeIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
                    BlockTableOffset = con.Memory.Position;
                    BlockSizes[0] = con + BlockSizes[0];
                    for (int i = 1; i < numBlocks; i++)
                    {
                        BlockSizes[i] = con + BlockSizes[i];
                        BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
                    }
                    con.Seek((int)pos, SeekOrigin.Begin);
                }
            }

            private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
            {
                return entryOffset + (numEntries * 0x1E) + (blockIndex * 2);
            }

        }

        private static readonly byte[] TOCHash = { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C };

        public HeaderStruct Header;
        public FileEntryStruct[] Files;


        public DLCPackage(string filename)
        {
            Load(filename);
        }

        public void Load(string filename)
        {
            this.FileName = filename;
            SerializingFile con = new SerializingFile(new FileStream(FileName, FileMode.Open, FileAccess.Read));
            Serialize(con);
            con.Memory.Close();
        }

        public void Serialize(SerializingFile con)
        {
            if (con.isLoading)
                Header = new HeaderStruct();
            Header.Serialize(con);
            con.Seek((int)Header.EntryOffset, SeekOrigin.Begin);
            if (con.isLoading)
                Files = new FileEntryStruct[Header.FileCount];
            for (int i = 0; i < Header.FileCount; i++)
                Files[i].Serialize(con, Header);
            if (con.isLoading)
                ReadFileNames();
        }


        public void ReadFileNames()
        {
            FileEntryStruct entry;
            int fileIndex = -1;
            //Get list of files
            for (int i = 0; i < Header.FileCount; i++)
            {
                entry = Files[i];
                entry.FileName = UNKNOWN_FILENAME;
                Files[i] = entry;
                //find toc
                if (Files[i].Hash.SequenceEqual(TOCHash))
                    fileIndex = i;
            }
            if (fileIndex == -1)
                return;
            MemoryStream m = DecompressEntry(fileIndex);
            m.Seek(0, 0);
            StreamReader r = new StreamReader(m);
            while (!r.EndOfStream)
            {
                string line = r.ReadLine();
                byte[] hash = ComputeHash(line);
                fileIndex = -1;
                //Match name to hash
                for (int i = 0; i < Header.FileCount; i++)
                {
                    if (Files[i].Hash.SequenceEqual(hash))
                    {
                        fileIndex = i;
                        break;
                    }
                }

                //assign if found
                if (fileIndex != -1)
                {
                    entry = Files[fileIndex];
                    entry.FileName = line;
                    Files[fileIndex] = entry;
                }
            }
        }

        public List<byte[]> GetBlocks(int Index)
        {
            List<byte[]> res = new List<byte[]>();
            FileEntryStruct e = Files[Index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                res.Add(buff);
                fs.Close();
                return res;
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        buff = new byte[compressedBlockSize];
                        fs.Read(buff, 0, buff.Length);
                        res.Add(buff);
                        left -= compressedBlockSize;
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(left, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            throw new Exception("SFAR compressed block size smaller than 5");
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        res.Add(inputBlock);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            fs.Close();
            return res;
        }

        public MemoryStream DecompressEntry(int index)
        {
            MemoryStream result = MixinHandler.MixinMemoryStreamManager.GetStream();
            FileEntryStruct e = Files[index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            //byte[] buff;
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                fs.CopyToEx(result, (int)e.RealUncompressedSize);
                //buff = new byte[e.RealUncompressedSize];
                //fs.Read(buff, 0, buff.Length);
                //result.Write(buff, 0, buff.Length);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        fs.CopyToEx(result, (int)compressedBlockSize);

                        //buff = new byte[compressedBlockSize];
                        //fs.Read(buff, 0, buff.Length);
                        //result.Write(buff, 0, buff.Length);
                        left -= compressedBlockSize;
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(left, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            throw new Exception("SFAR compressed block size smaller than 5 bytes");
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        uint actualUncompressedBlockSize = uncompressedBlockSize;
                        uint actualCompressedBlockSize = compressedBlockSize;
                        outputBlock = SevenZipHelper.LZMA.Decompress(inputBlock, actualUncompressedBlockSize);
                        if (outputBlock.Length != actualUncompressedBlockSize)
                            throw new Exception("SFAR decompression error");
                        result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            fs.Close();
            result.Position = 0;
            return result;
        }

        public MemoryStream DecompressEntry(int index, FileStream fs)
        {
            FileEntryStruct e = Files[index];
            //MemoryStream result = new MemoryStream((int)e.RealUncompressedSize);
            MemoryStream result = MixinHandler.MixinMemoryStreamManager.GetStream();
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                result.Write(buff, 0, buff.Length);
            }
            else
            {
                //Compressed
                while (left > 0)
                {
                    uint compressedBlockSize = e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        buff = new byte[compressedBlockSize];
                        fs.Read(buff, 0, buff.Length);
                        result.Write(buff, 0, buff.Length);
                        left -= compressedBlockSize;
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(left, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            throw new Exception("compressed block size smaller than 5");
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        uint actualUncompressedBlockSize = uncompressedBlockSize;
                        uint actualCompressedBlockSize = compressedBlockSize;

                        outputBlock = SevenZipHelper.LZMA.Decompress(inputBlock, actualUncompressedBlockSize);
                        if (outputBlock.Length != actualUncompressedBlockSize)
                            throw new Exception("Decompression Error");
                        result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            return result;
        }

        public static byte[] ComputeHash(string input)
        {
            byte[] bytes = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
                bytes[i] = (byte)Sanitize(input[i]);
            var md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(bytes);
        }

        public static char Sanitize(char c)
        {
            switch ((ushort)c)
            {
                case 0x008C: return (char)0x9C;
                case 0x009F: return (char)0xFF;
                case 0x00D0:
                case 0x00DF:
                case 0x00F0:
                case 0x00F7: return c;
            }
            if ((c >= 'A' && c <= 'Z') || (c >= 'À' && c <= 'Þ'))
                return char.ToLowerInvariant(c);
            return c;
        }

        //public void WriteString(MemoryStream m, string s)
        //{
        //    foreach (char c in s)
        //        m.WriteByte((byte) c);
        //}

        public void ReBuild()
        {
            string path = Path.Combine(Path.GetDirectoryName(FileName), Path.GetFileNameWithoutExtension(FileName) + ".tmp");
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            //Debug.WriteLine("Creating Header Dummy...");
            for (int i = 0; i < 8; i++)
                fs.Write(BitConverter.GetBytes(0), 0, 4);
            Header.EntryOffset = 0x20;
            //Debug.WriteLine("Creating File Table...");
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                fs.Write(BitConverter.GetBytes(e.BlockSizeIndex), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
            }
            Header.BlockTableOffset = (uint)fs.Position;
            //Debug.WriteLine("Creating Block Table...");
            for (int i = 0; i < Header.FileCount; i++)
                if (Files[i].BlockSizeIndex != 0xFFFFFFFF)
                    foreach (ushort u in Files[i].BlockSizes)
                        fs.Write(BitConverter.GetBytes(u), 0, 2);
            Header.DataOffset = (uint)fs.Position;
            //Debug.WriteLine("Appending Files...");
            uint pos = (uint)fs.Position;
            for (int i = 0; i < Header.FileCount; i++)
            {
                List<byte[]> blocks = GetBlocks(i);
                FileEntryStruct e = Files[i];
                //Debug.WriteLine("Rebuilding \"" + e.FileName + "\" (" + (i + 1) + "/" + Header.FileCount + ") " + BytesToString(e.UncompressedSize) + " ...");
                e.DataOffset = pos;
                e.DataOffsetAdder = 0;
                for (int j = 0; j < blocks.Count; j++)
                {
                    MemoryStream m = new MemoryStream(blocks[j]);
                    fs.Write(m.ToArray(), 0, (int)m.Length);
                    pos += (uint)m.Length;
                }
                Files[i] = e;
            }
            //Debug.WriteLine("Updating FileTable...");
            fs.Seek(0x20, 0);
            pos = (uint)fs.Position;
            uint blocksizeindex = 0;
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                if (e.BlockSizeIndex != 0xFFFFFFFF)
                {
                    fs.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                    e.BlockSizeIndex = blocksizeindex;
                    blocksizeindex += (uint)e.BlockSizes.Length;
                }
                else
                    fs.Write(BitConverter.GetBytes(0xFFFFFFFF), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
                e.MyOffset = pos;
                Files[i] = e;
                pos += 0x1E;
            }
            fs.Seek(0, 0);
            //Debug.WriteLine("Rebuilding Header...");
            fs.Write(BitConverter.GetBytes(Header.Magic), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.Version), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.DataOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.EntryOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.FileCount), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.BlockTableOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.MaxBlockSize), 0, 4);
            foreach (char c in Header.CompressionScheme)
                fs.WriteByte((byte)c);
            fs.Close();
            File.Delete(FileName);
            File.Move(path, FileName);
        }

        private int findTOCIndex()
        {
            for (int i = 0; i < Header.FileCount; i++)
            {
                if (Files[i].Hash.SequenceEqual(TOCHash))
                    return i;
            }
            return -1;
        }

        public void DeleteEntry(int Index)
        {
            try
            {
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                int tocIndex = findTOCIndex();
                if (tocIndex == -1)
                    return;
                MemoryStream m = DecompressEntry(tocIndex, fs);
                fs.Close();
                FileEntryStruct e = Files[Index];
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                string file = e.FileName + "\r\n";
                toc = toc.Replace(file, "");
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), tocIndex);
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
                l.RemoveAt(Index);
                Files = l.ToArray();
                Header.FileCount--;
                ReBuild();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR DELETING ENTRY FROM SFAR TOC\n" + ex.Message);
            }
        }

        public void DeleteEntries(List<int> Index)
        {
            try
            {
                Index.Sort();
                Index.Reverse();
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                int f = findTOCIndex();
                if (f == -1)
                    return;
                MemoryStream m = DecompressEntry(f, fs);
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                for (int i = 0; i < Index.Count; i++)
                {
                    FileEntryStruct e = Files[Index[i]];
                    string file = e.FileName + "\r\n";
                    toc = toc.Replace(file, "");
                }
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), f);
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
                for (int i = 0; i < Index.Count; i++)
                {
                    l.RemoveAt(Index[i]);
                    Header.FileCount--;
                }
                Files = l.ToArray();
                ReBuild();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR DELETING ENTRY FROM SFAR TOC\n" + ex.Message);
            }
        }

        public void AddFileQuick(string onDiskNewFile, string inArchivePath)
        {
            FileStream sfarStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            byte[] newFileBytes = File.ReadAllBytes(onDiskNewFile);
            //Create Entry
            List<FileEntryStruct> tmp = new List<FileEntryStruct>(Files);
            FileEntryStruct e = new FileEntryStruct();
            e.FileName = inArchivePath;
            e.BlockOffsets = new long[0];
            e.Hash = ComputeHash(inArchivePath);
            e.BlockSizeIndex = 0xFFFFFFFF;
            e.UncompressedSize = (uint)newFileBytes.Length;
            e.UncompressedSizeAdder = 0;
            tmp.Add(e);
            Files = tmp.ToArray();


            //
            //Find TOC
            int f = findTOCIndex();
            if (f == -1)
                return;
            MemoryStream tocMemory = DecompressEntry(f, sfarStream);
            //
            //No idea what most of the rest of this stuff is so i probably shouldn't change it
            //Update TOC
            tocMemory.WriteStringASCII(inArchivePath);
            tocMemory.WriteByte(0xD);
            tocMemory.WriteByte(0xA);


            //
            //Append new FileTable
            int count = (int)Header.FileCount + 1;
            long oldsize = sfarStream.Length;
            long offset = oldsize;
            //Debug.WriteLine("File End Offset : 0x" + offset.ToString("X10"));
            sfarStream.Seek(oldsize, 0);
            Header.EntryOffset = (uint)offset;
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                sfarStream.Write(e.Hash, 0, 16);
                sfarStream.Write(BitConverter.GetBytes(e.BlockSizeIndex), 0, 4);
                sfarStream.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                sfarStream.WriteByte(e.UncompressedSizeAdder);
                sfarStream.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                sfarStream.WriteByte(e.DataOffsetAdder);
            }
            offset += count * 0x1E;
            //Debug.WriteLine("Table End Offset : 0x" + offset.ToString("X10"));
            Header.BlockTableOffset = (uint)offset;
            //
            //Append blocktable
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                if (e.BlockSizeIndex != 0xFFFFFFFF && i != f)
                    foreach (ushort u in e.BlockSizes)
                        sfarStream.Write(BitConverter.GetBytes(u), 0, 2);
            }
            offset = sfarStream.Length;
            //Debug.WriteLine("Block Table End Offset : 0x" + offset.ToString("X10"));
            long dataoffset = offset;
            sfarStream.Write(newFileBytes, 0, newFileBytes.Length);
            offset += newFileBytes.Length;
            //Debug.WriteLine("New Data End Offset : 0x" + offset.ToString("X10"));
            //
            //Append TOC
            long tocoffset = offset;
            sfarStream.Write(tocMemory.ToArray(), 0, (int)tocMemory.Length);
            offset = sfarStream.Length;
            //Debug.WriteLine("New TOC Data End Offset : 0x" + offset.ToString("X10"));
            //update filetable
            sfarStream.Seek(oldsize, 0);
            uint blocksizeindex = 0;
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                sfarStream.Write(e.Hash, 0, 16);
                if (e.BlockSizeIndex == 0xFFFFFFFF || i == f)
                    sfarStream.Write(BitConverter.GetBytes(-1), 0, 4);
                else
                {
                    sfarStream.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                    e.BlockSizeIndex = blocksizeindex;
                    blocksizeindex += (uint)e.BlockSizes.Length;
                    Files[i] = e;
                }
                if (i == f)
                {
                    sfarStream.Write(BitConverter.GetBytes(tocMemory.Length), 0, 4);
                    sfarStream.WriteByte(0);
                    sfarStream.Write(BitConverter.GetBytes(tocoffset), 0, 4);
                    byte b = (byte)((tocoffset & 0xFF00000000) >> 32);
                    sfarStream.WriteByte(b);
                }
                else if (i == count - 1)
                {
                    sfarStream.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                    sfarStream.WriteByte(0);
                    sfarStream.Write(BitConverter.GetBytes(dataoffset), 0, 4);
                    byte b = (byte)((dataoffset & 0xFF00000000) >> 32);
                    sfarStream.WriteByte(b);
                }
                else
                {
                    sfarStream.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                    sfarStream.WriteByte(e.UncompressedSizeAdder);
                    sfarStream.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                    sfarStream.WriteByte(e.DataOffsetAdder);
                }
            }
            //Update Header
            sfarStream.Seek(0xC, 0);
            sfarStream.Write(BitConverter.GetBytes(Header.EntryOffset), 0, 4);
            sfarStream.Write(BitConverter.GetBytes(count), 0, 4);
            sfarStream.Write(BitConverter.GetBytes(Header.BlockTableOffset), 0, 4);
            //
            sfarStream.Close();
        }


        public void ReplaceEntry(string filein, int Index)
        {
            byte[] FileIN = File.ReadAllBytes(filein);
            ReplaceEntry(FileIN, Index);
        }

        public void ReplaceEntry(byte[] FileIN, int Index)
        {
            FileStream fs = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs.Seek(0, SeekOrigin.End);
            uint offset = (uint)fs.Length;
            //append data
            fs.Write(FileIN, 0, FileIN.Length);

            //uncompressed entry
            FileEntryStruct e = Files[Index];
            e.BlockSizes = new ushort[0];
            e.BlockOffsets = new long[1];
            e.BlockOffsets[0] = offset;
            e.BlockSizeIndex = 0xFFFFFFFF;
            e.DataOffset = offset;
            e.UncompressedSize = (uint)FileIN.Length;

            fs.Seek(e.MyOffset, 0);
            fs.Write(e.Hash, 0, 16);
            fs.Write(BitConverter.GetBytes(0xFFFFFFFF), 0, 4);
            fs.Write(BitConverter.GetBytes(FileIN.Length), 0, 4);
            fs.WriteByte(e.UncompressedSizeAdder);
            fs.Write(BitConverter.GetBytes(offset), 0, 4);
            fs.WriteByte(0);
            Files[Index] = e;
            fs.Close();
        }

        public enum DLCTOCUpdateResult
        {
            RESULT_UPDATED,
            RESULT_UPDATE_NOT_NECESSARY,
            RESULT_ERROR_NO_ENTRIES,
            RESULT_ERROR_NO_TOC
        }
        public DLCTOCUpdateResult UpdateTOCbin(bool rebuildSFAR = false)
        {
            int archiveFileIndex = -1;
            for (int i = 0; i < Files.Length; i++)
            {
                if (Path.GetFileName(Files[i].FileName) == "PCConsoleTOC.bin")
                {
                    archiveFileIndex = i;
                    break;
                }
            }

            if (archiveFileIndex == -1)
            {
                Debug.WriteLine("Couldn't find PCConsoleTOC.bin in SFAR");
                return DLCTOCUpdateResult.RESULT_ERROR_NO_TOC;
            }

            //Collect list of information from the SFAR Header of files and their sizes
            var entries = new List<(string filepath, int size)>();
            foreach (var file in Files)
            {
                if (file.FileName != UNKNOWN_FILENAME)
                {
                    string consoleDirFilename = file.FileName.Substring(file.FileName.IndexOf("DLC_", StringComparison.InvariantCultureIgnoreCase));
                    consoleDirFilename = consoleDirFilename.Substring(consoleDirFilename.IndexOf('/') + 1);
                    entries.Add((consoleDirFilename.Replace('/', '\\'), (int)file.UncompressedSize));
                }
            }

            //Substring out the dlc name


            //Read the current TOC and see if an update is necessary.
            bool tocNeedsUpdating = false;

            var tocMemoryStream = DecompressEntry(archiveFileIndex);
            TOCBinFile toc = new TOCBinFile(tocMemoryStream);

            int actualTocEntries = toc.Entries.Count;
            actualTocEntries -= toc.Entries.Count(x => x.name.EndsWith("PCConsoleTOC.txt", StringComparison.InvariantCultureIgnoreCase));
            actualTocEntries -= toc.Entries.Count(x => x.name.EndsWith("GlobalPersistentCookerData.upk", StringComparison.InvariantCultureIgnoreCase));
            if (actualTocEntries != entries.Count)
            {
                tocNeedsUpdating = true;
            }
            else
            {
                //Check sizes to see if all of ours match.
                foreach (var entry in toc.Entries)
                {
                    if (entry.name.EndsWith("PCConsoleTOC.txt", StringComparison.InvariantCultureIgnoreCase) || entry.name.EndsWith("GlobalPersistentCookerData.upk", StringComparison.InvariantCultureIgnoreCase)) continue; //These files don't actually exist in SFARs
                    var matchingNewEntry = entries.FirstOrDefault(x => x.filepath.Equals(entry.name, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingNewEntry.filepath == null)
                    {
                        //same number of files but we could not find it in the list. A delete and add might have caused this.
                        tocNeedsUpdating = true;
                        break;
                    }
                    if (matchingNewEntry.size != entry.size)
                    {
                        //size is different.
                        tocNeedsUpdating = true;
                        break;
                    }
                }
            }


            if (tocNeedsUpdating)
            {
                MemoryStream newTocStream = TOCCreator.CreateTOCForEntries(entries);
                byte[] newmem = newTocStream.ToArray();
                //if (tocMemoryStream.ToArray().SequenceEqual(newTocStream.ToArray()))
                //{
                //    //no update needed
                //    return DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY;
                //}
                ReplaceEntry(newmem, archiveFileIndex);

            }
            else
            {
                return DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY; // no update needed
            }



            //int IndexTOC = archiveFileIndex;
            //byte[] originalTOCBinary = tocMemoryStream.ToArray();
            //TOCBinFile TOC = new TOCBinFile(tocMemoryStream);
            //int count = 0;
            //if (TOC.Entries == null)
            //{
            //    Debug.WriteLine("No TOC entries found!");
            //    return null;
            //}

            //for (int i = 0; i < TOC.Entries.Count; i++)
            //{
            //    TOCBinFile.Entry e = TOC.Entries[i];
            //    archiveFileIndex = -1;

            //    //Find file in archive that this TOC entry is for
            //    for (int fileIndex = 0; fileIndex < Files.Length; fileIndex++)
            //    {
            //        if (Files[fileIndex].FileName.Replace('/', '\\').Contains(e.name))
            //        {
            //            archiveFileIndex = fileIndex;
            //            break;
            //        }
            //    }

            //    if (archiveFileIndex == -1)
            //    {
            //        //Not in the archive/
            //        List<string> parts = new List<string>(this.FileName.Split('\\'));
            //        parts.RemoveAt(parts.Count - 1);
            //        parts.RemoveAt(parts.Count - 1);
            //        string path = string.Join("\\", parts) + "\\" + e.name;
            //        if (File.Exists(path))
            //        {
            //            FileInfo fi = new FileInfo(path);
            //            if (fi.Length != e.size)
            //            {
            //                e.size = (int)fi.Length;
            //                TOC.Entries[i] = e;
            //            }
            //        }
            //        else
            //            Debug.WriteLine("Entry not found: " + e.name);
            //    }
            //    else
            //    {
            //        if (Files[archiveFileIndex].UncompressedSize != e.size)
            //        {
            //            e.size = (int)Files[archiveFileIndex].UncompressedSize;
            //            TOC.Entries[i] = e;
            //        }
            //    }
            //}
            //ReplaceEntry(TOC.Save().ToArray(), IndexTOC);
            if (rebuildSFAR)
            {
                Load(FileName);
                ReBuild();
            }

            return DLCTOCUpdateResult.RESULT_UPDATED;
        }
        public int FindFileEntry(string fileName)
        {
            return Files.IndexOf(Files.FirstOrDefault(x => x.FileName.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}

