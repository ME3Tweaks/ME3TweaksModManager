using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using LZO2Helper;
using static ME3Explorer.Packages.MEPackage;
using MassEffectModManagerCore.modmanager.helpers;
using SevenZipHelper;

namespace ME3Explorer.Packages
{
    public static class CompressionHelper
    {
        /// <summary>
        /// Maximum size of a compressed chunk. This is not relevant for the table chunk or if an export is larger than the max chunk size
        /// </summary>
        public const int MAX_CHUNK_SIZE = 0x100000; //1 Mebibyte

        /// <summary>
        /// Maximum size of a block within a chunk
        /// </summary>
        public const int MAX_BLOCK_SIZE = 0x20000; //128 Kibibytes

        public const int SIZE_OF_CHUNK_HEADER = 16;
        public const int SIZE_OF_CHUNK_BLOCK_HEADER = 8;


        public struct Chunk
        {
            public int uncompressedOffset;
            public int uncompressedSize;
            public int compressedOffset;
            public int compressedSize;
            public byte[] Compressed;
            public byte[] Uncompressed;
            public ChunkHeader header;
            public List<Block> blocks;
        }

        public struct ChunkHeader
        {
            public int magic;
            public int blocksize;
            public int compressedsize;
            public int uncompressedsize;
        }

        public struct Block
        {
            public int compressedsize;
            public int uncompressedsize;
            public byte[] uncompressedData;
            public byte[] compressedData;
        }

        #region Decompression

        /// <summary>
        ///     decompress an entire ME3, 2, or 1 package file.
        /// </summary>
        /// <param name="pccFileName">pcc file's name to open.</param>
        /// <returns>a decompressed array of bytes.</returns>
        public static Stream Decompress(string pccFileName)
        {
            using (FileStream input = File.OpenRead(pccFileName))
            {
                input.Seek(4, SeekOrigin.Begin); //skip package tag
                ushort versionLo = input.ReadUInt16();
                ushort versionHi = input.ReadUInt16();

                //ME3
                if (versionLo == 684 && versionHi == 194)
                {
                    return DecompressME3(input);
                }
                //ME2 || ME1
                else if (versionLo == 512 && versionHi == 130 || versionLo == 491 && versionHi == 1008)
                {
                    return DecompressME1orME2(input);
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, or ME3 package file.");
                }
            }
        }

        /// <summary>
        ///     decompress an entire ME1 or 2 pcc file.
        /// </summary>
        /// <param name="raw">pcc file passed in stream format</param>
        /// <returns>a decompressed stream.</returns>
        public static MemoryStream DecompressME1orME2(Stream raw)
        {
            raw.Seek(4, SeekOrigin.Begin);
            ushort versionLo = raw.ReadUInt16();
            ushort versionHi = raw.ReadUInt16();
            raw.Seek(12, SeekOrigin.Begin);
            int tempNameSize = raw.ReadInt32();
            raw.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = raw.ReadInt32();
            raw.Seek(32 + tempGenerations * 12, SeekOrigin.Current);

            //if ME1
            if (versionLo == 491 && versionHi == 1008)
            {
                raw.Seek(4, SeekOrigin.Current);
            }

            CompressionType compressionType = (CompressionType)raw.ReadUInt32();


            int pos = 4;
            int NumChunks = raw.ReadInt32();
            var Chunks = new List<Chunk>();

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk
                {
                    uncompressedOffset = raw.ReadInt32(),
                    uncompressedSize = raw.ReadInt32(),
                    compressedOffset = raw.ReadInt32(),
                    compressedSize = raw.ReadInt32()
                };
                c.Compressed = new byte[c.compressedSize];
                c.Uncompressed = new byte[c.uncompressedSize];
                //DebugOutput.PrintLn("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
                //DebugOutput.PrintLn("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
                Chunks.Add(c);
            }

            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;
            for (int i = 0; i < Chunks.Count; i++)
            {
                Chunk c = Chunks[i];
                raw.Seek(c.compressedOffset, SeekOrigin.Begin);
                c.Compressed = raw.ReadToBuffer(c.compressedSize);

                ChunkHeader h = new ChunkHeader
                {
                    magic = BitConverter.ToInt32(c.Compressed, 0),
                    blocksize = BitConverter.ToInt32(c.Compressed, 4),
                    compressedsize = BitConverter.ToInt32(c.Compressed, 8),
                    uncompressedsize = BitConverter.ToInt32(c.Compressed, 12)
                };
                if (h.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                pos = 16;
                int blockCount = (h.uncompressedsize % h.blocksize == 0)
                    ? h.uncompressedsize / h.blocksize
                    : h.uncompressedsize / h.blocksize + 1;
                var BlockList = new List<Block>();
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block
                    {
                        compressedsize = BitConverter.ToInt32(c.Compressed, pos),
                        uncompressedsize = BitConverter.ToInt32(c.Compressed, pos + 4)
                    };
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }

                int outpos = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    var datain = new byte[b.compressedsize];
                    var dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = c.Compressed[pos + j];
                    pos += b.compressedsize;

                    switch (compressionType)
                    {
                        case CompressionType.LZO:
                            {
                                if (
                                    LZO2.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("LZO decompression failed!");
                                break;
                            }
                        case CompressionType.Zlib:
                            {
                                if (ZlibHelper.Zlib.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("Zlib decompression failed!");
                                break;
                            }
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }

                    for (int j = 0; j < b.uncompressedsize; j++)
                        c.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                }

                c.header = h;
                c.blocks = BlockList;
                count++;
                Chunks[i] = c;
            }

            MemoryStream result = new MemoryStream();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.Write(c.Uncompressed);
            }

            return result;
        }

        public static MemoryStream DecompressUDK(Stream raw, long compressionInfoOffset, CompressionType compressionType = CompressionType.None, int NumChunks = 0)
        {
            //PrintCompressDebug(raw, compressionInfoOffset, compressionType, NumChunks);


            raw.JumpTo(compressionInfoOffset);
            if (compressionType == CompressionType.None)
                compressionType = (CompressionType)raw.ReadUInt32();

            if (NumChunks == 0)
                NumChunks = raw.ReadInt32();
            var Chunks = new List<Chunk>();
            var chunkTableStart = raw.Position;

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk
                {
                    uncompressedOffset = raw.ReadInt32(),
                    uncompressedSize = raw.ReadInt32(),
                    compressedOffset = raw.ReadInt32(),
                    compressedSize = raw.ReadInt32()
                };
                c.Compressed = new byte[c.compressedSize];
                c.Uncompressed = new byte[c.uncompressedSize];
                //DebugOutput.PrintLn("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
                //DebugOutput.PrintLn("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
                Chunks.Add(c);
            }


            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;
            for (int i = 0; i < Chunks.Count; i++)
            {
                Chunk chunk = Chunks[i];
                raw.Seek(chunk.compressedOffset, SeekOrigin.Begin);
                raw.Read(chunk.Compressed, 0, chunk.compressedSize);

                ChunkHeader chunkBlockHeader = new ChunkHeader
                {
                    magic = BitConverter.ToInt32(chunk.Compressed, 0),
                    blocksize = BitConverter.ToInt32(chunk.Compressed, 4),
                    compressedsize = BitConverter.ToInt32(chunk.Compressed, 8),
                    uncompressedsize = BitConverter.ToInt32(chunk.Compressed, 12)
                };




                if (chunkBlockHeader.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                int pos = 16;
                int blockCount = (chunkBlockHeader.uncompressedsize % chunkBlockHeader.blocksize == 0)
                    ? chunkBlockHeader.uncompressedsize / chunkBlockHeader.blocksize : chunkBlockHeader.uncompressedsize / chunkBlockHeader.blocksize + 1;

                #region Sanity checking from April 29 2020
                //int sizeOfChunk = 16;
                //int sizeOfChunkBlock = 8;
                //int maxBlockSizeMEM = 0x20000; // 128KB
                //int sanityCheckMEM = chunkBlockHeader.compressedsize + sizeOfChunk + sizeOfChunkBlock * blockCount;

                //if (sanityCheckMEM != chunk.compressedSize)
                //{
                //    Debug.WriteLine($" >> SANITY CHECK {i} FAILED. CHUNKCOMPSIZE: {chunk.compressedSize}, MEM Expected Chunk Comp Size: {sanityCheckMEM}, Difference: {sanityCheckMEM - chunk.compressedSize}");
                //}
                //else
                //{
                //    Debug.WriteLine($" >> SANITY CHECK {i} OK. CHUNKCOMPSIZE: {chunk.compressedSize}, MEM Expected Chunk Comp Size: {sanityCheckMEM}, Difference: {sanityCheckMEM - chunk.compressedSize}");
                //}
                #endregion

                var BlockList = new List<Block>();
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block
                    {
                        compressedsize = BitConverter.ToInt32(chunk.Compressed, pos),
                        uncompressedsize = BitConverter.ToInt32(chunk.Compressed, pos + 4)
                    };
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }
                int outpos = 0;
                int blocknum = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    //Debug.WriteLine("Decompressing block " + blocknum);
                    var datain = new byte[b.compressedsize];
                    var dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = chunk.Compressed[pos + j];
                    pos += b.compressedsize;

                    switch (compressionType)
                    {
                        case CompressionType.LZO:
                            {
                                if (
                                        LZO2.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("LZO decompression failed!");
                                break;
                            }
                        case CompressionType.Zlib:
                            {
                                if (ZlibHelper.Zlib.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("Zlib decompression failed!");
                                break;
                            }
                        /* WII U
                        case CompressionType.LZMA:
                            dataout = LZMA.Decompress(datain, (uint)b.uncompressedsize);
                            if (dataout.Length != b.uncompressedsize)
                                throw new Exception("LZMA decompression failed!");
                            break;
                            */
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }
                    for (int j = 0; j < b.uncompressedsize; j++)
                        chunk.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                    blocknum++;
                }
                chunk.header = chunkBlockHeader;
                chunk.blocks = BlockList;
                count++;
                Chunks[i] = chunk;
            }

            MemoryStream result = new MemoryStream();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.WriteFromBuffer(c.Uncompressed);
            }

            return result;
        }

        /// <summary>
        ///     decompress an entire ME3 pcc file into a new stream
        /// </summary>
        /// <param name="input">pcc file passed in stream format</param>
        /// <returns>a decompressed array of bytes</returns>
        public static MemoryStream DecompressME3(Stream input)
        {
            input.Seek(0, SeekOrigin.Begin);
            var magic = input.ReadUInt32();
            if (magic != 0x9E2A83C1)
            {
                throw new FormatException("not a pcc file");
            }


            var versionLo = input.ReadUInt16();
            var versionHi = input.ReadUInt16();

            if (versionLo != 684 &&
                versionHi != 194)
            {
                throw new FormatException("unsupported pcc version");
            }

            long headerSize = 8;

            input.Seek(4, SeekOrigin.Current);
            headerSize += 4;

            var folderNameLength = input.ReadInt32();
            headerSize += 4;

            var folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            input.Seek(folderNameByteLength, SeekOrigin.Current);
            headerSize += folderNameByteLength;

            var packageFlagsOffset = input.Position;
            var packageFlags = input.ReadUInt32();
            headerSize += 4;

            if ((packageFlags & 0x02000000u) == 0)
            {
                throw new FormatException("pcc file is already decompressed");
            }

            if ((packageFlags & 8) != 0)
            {
                input.Seek(4, SeekOrigin.Current);
                headerSize += 4;
            }

            uint nameCount = input.ReadUInt32();
            uint nameOffset = input.ReadUInt32();

            input.Seek(52, SeekOrigin.Current);
            headerSize += 60;

            var generationsCount = input.ReadUInt32();
            input.Seek(generationsCount * 12, SeekOrigin.Current);
            headerSize += generationsCount * 12;

            input.Seek(20, SeekOrigin.Current);
            headerSize += 24;

            var blockCount = input.ReadUInt32();
            int headBlockOff = (int)input.Position;
            var afterBlockTableOffset = headBlockOff + (blockCount * 16);
            var indataOffset = afterBlockTableOffset + 8;

            input.Seek(0, SeekOrigin.Begin);
            MemoryStream output = new MemoryStream();
            output.Seek(0, SeekOrigin.Begin);

            output.WriteFromStream(input, headerSize);
            output.WriteUInt32(0); // block count

            input.Seek(afterBlockTableOffset, SeekOrigin.Begin);
            output.WriteFromStream(input, 8);

            //check if has extra name list (don't know it's usage...)
            if ((packageFlags & 0x10000000) != 0)
            {
                long curPos = output.Position;
                output.WriteFromStream(input, nameOffset - curPos);
            }

            //decompress blocks in parallel
            var tasks = new Task<byte[]>[blockCount];
            var uncompressedOffsets = new uint[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                input.Seek(headBlockOff, SeekOrigin.Begin);
                uncompressedOffsets[i] = input.ReadUInt32();
                var uncompressedSize = input.ReadUInt32();
                var compressedOffset = input.ReadUInt32();
                var compressedSize = input.ReadUInt32();
                headBlockOff = (int)input.Position;

                var buff = new byte[compressedSize];
                input.Seek(compressedOffset, SeekOrigin.Begin);
                input.Read(buff, 0, buff.Length);

                tasks[i] = AmaroK86.MassEffect3.ZlibBlock.ZBlock.DecompressAsync(buff);
            }

            Task.WaitAll(tasks);
            for (int i = 0; i < blockCount; i++)
            {
                output.Seek(uncompressedOffsets[i], SeekOrigin.Begin);
                output.Write(tasks[i].Result);
            }

            //Do not change the IsCompressed bit as it will not accurately reflect the state of the file on disk.
            //output.Seek(packageFlagsOffset, SeekOrigin.Begin);
            //output.WriteValueU32(packageFlags & ~0x02000000u, endian); //Mark file as decompressed.
            return output;
        }

        #endregion
    }
}